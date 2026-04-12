using BrrainzBot.Host;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace BrrainzBot.Modules.Onboarding;

public sealed class OnboardingModule(
    DiscordSocketClient client,
    BotSettings settings,
    IAiProviderClient aiProviderClient,
    IVerificationSessionStore sessionStore,
    IAuditLog auditLog,
    ILogger<OnboardingModule> logger) : IDiscordModule
{
    private const string StartVerificationCustomId = "onboarding:start";
    private const string VerificationModalCustomId = "onboarding:modal";
    private const string WhyHereCustomId = "onboarding:why";
    private const string WhatDoYouWantCustomId = "onboarding:what";
    private const string RuleParaphraseCustomId = "onboarding:rule";

    public string Name => "Onboarding";

    public Task RegisterAsync(CancellationToken cancellationToken)
    {
        client.Ready += HandleReadyAsync;
        client.UserJoined += HandleUserJoinedAsync;
        client.ButtonExecuted += HandleButtonExecutedAsync;
        client.ModalSubmitted += HandleModalSubmittedAsync;
        _ = Task.Run(() => RunStaleCleanupLoopAsync(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    private async Task HandleReadyAsync()
    {
        foreach (var guildSettings in settings.Guilds.Where(g => g.EnableOnboarding))
        {
            var guild = client.GetGuild(guildSettings.GuildId);
            if (guild == null)
            {
                logger.LogWarning("Guild {GuildId} was not found in cache during onboarding initialization.", guildSettings.GuildId);
                continue;
            }

            var channel = guild.GetTextChannel(guildSettings.WelcomeChannelId);
            if (channel == null)
            {
                logger.LogWarning("Welcome channel {ChannelId} was not found for guild {GuildId}.", guildSettings.WelcomeChannelId, guildSettings.GuildId);
                continue;
            }

            await EnsureWelcomeMessageAsync(channel, guildSettings);
        }
    }

    private async Task HandleUserJoinedAsync(SocketGuildUser user)
    {
        var guildSettings = settings.FindGuild(user.Guild.Id);
        if (guildSettings is not { EnableOnboarding: true })
            return;

        var newRole = user.Guild.GetRole(guildSettings.NewRoleId);
        if (newRole == null)
        {
            logger.LogWarning("The NEW role {RoleId} is missing for guild {GuildId}.", guildSettings.NewRoleId, user.Guild.Id);
            return;
        }

        await user.AddRoleAsync(newRole);
        var session = new VerificationSession
        {
            GuildId = user.Guild.Id,
            UserId = user.Id,
            UserName = user.DisplayName,
            JoinedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(guildSettings.Onboarding.StaleTimeout)
        };

        await sessionStore.UpsertAsync(session, CancellationToken.None);
        await auditLog.WriteAsync("user_joined", new
        {
            guildId = user.Guild.Id,
            userId = user.Id,
            userName = user.DisplayName
        }, CancellationToken.None);
    }

    private async Task HandleButtonExecutedAsync(SocketMessageComponent component)
    {
        if (!string.Equals(component.Data.CustomId, StartVerificationCustomId, StringComparison.Ordinal))
            return;

        if (component.GuildId is not { } guildId)
            return;

        var guildSettings = settings.FindGuild(guildId);
        if (guildSettings is not { EnableOnboarding: true })
            return;

        var session = await GetOrCreateSessionAsync(guildId, component.User, guildSettings.Onboarding.StaleTimeout);
        if (session.CooldownUntil is { } cooldownUntil && cooldownUntil > DateTimeOffset.UtcNow)
        {
            await component.RespondAsync(
                $"Please give it a moment before retrying. You can try again <t:{cooldownUntil.ToUnixTimeSeconds()}:R>.",
                ephemeral: true);
            return;
        }

        if (session.AttemptCount >= guildSettings.Onboarding.MaxAttempts)
        {
            await component.RespondAsync(
                "You have used all verification attempts for now. Please wait for a moderator or rejoin later.",
                ephemeral: true);
            return;
        }

        var modal = new ModalBuilder()
            .WithTitle("Quick verification")
            .WithCustomId(VerificationModalCustomId)
            .AddTextInput(guildSettings.Onboarding.FirstQuestionLabel, WhyHereCustomId, TextInputStyle.Paragraph, maxLength: 300)
            .AddTextInput(guildSettings.Onboarding.SecondQuestionLabel, WhatDoYouWantCustomId, TextInputStyle.Paragraph, maxLength: 300)
            .AddTextInput(guildSettings.Onboarding.ThirdQuestionLabel, RuleParaphraseCustomId, TextInputStyle.Paragraph, maxLength: 300)
            .Build();

        await component.RespondWithModalAsync(modal);
    }

    private async Task HandleModalSubmittedAsync(SocketModal modal)
    {
        if (!string.Equals(modal.Data.CustomId, VerificationModalCustomId, StringComparison.Ordinal))
            return;

        if (modal.GuildId is not { } guildId)
            return;

        var guildSettings = settings.FindGuild(guildId);
        if (guildSettings is not { EnableOnboarding: true })
            return;

        var session = await GetOrCreateSessionAsync(guildId, modal.User, guildSettings.Onboarding.StaleTimeout);
        if (session.AttemptCount >= guildSettings.Onboarding.MaxAttempts)
        {
            await modal.RespondAsync("You have already used all verification attempts.", ephemeral: true);
            return;
        }

        var answersById = modal.Data.Components.ToDictionary(component => component.CustomId, component => component.Value ?? string.Empty);
        var answers = new VerificationAnswers(
            answersById.GetValueOrDefault(WhyHereCustomId, string.Empty),
            answersById.GetValueOrDefault(WhatDoYouWantCustomId, string.Empty),
            answersById.GetValueOrDefault(RuleParaphraseCustomId, string.Empty));

        session.AttemptCount++;
        var prompt = new VerificationPrompt(
            guildSettings.Name,
            guildSettings.GuildTopicPrompt,
            guildSettings.Onboarding.RulesHint,
            modal.User.Username,
            modal.User.Id,
            session.AttemptCount,
            answers);

        try
        {
            var decision = await aiProviderClient.EvaluateAsync(prompt, CancellationToken.None);
            session.LastDecisionReason = decision.Reason;
            session.LastOutcome = decision.Outcome;
            session.History.Add($"{DateTimeOffset.UtcNow:u} [{decision.Outcome}] {decision.Reason}");

            switch (decision.Outcome)
            {
                case VerificationOutcome.Approve:
                    await ApproveAsync(modal, guildSettings, session, decision);
                    break;
                case VerificationOutcome.Retry:
                    await RetryAsync(modal, guildSettings, session, decision, notifyOwner: false);
                    break;
                default:
                    await RetryAsync(modal, guildSettings, session, decision, notifyOwner: guildSettings.Onboarding.NotifyOwnerOnUncertain);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Verification failed for user {UserId} in guild {GuildId}", modal.User.Id, guildId);
            session.LastDecisionReason = ex.Message;
            session.LastOutcome = VerificationOutcome.Uncertain;
            session.CooldownUntil = DateTimeOffset.UtcNow.Add(guildSettings.Onboarding.Cooldown);
            await sessionStore.UpsertAsync(session, CancellationToken.None);
            await NotifyOwnerAsync(guildSettings, $"Technical failure during verification for {modal.User.Username} ({modal.User.Id}). Error: {ex.Message}");
            await auditLog.WriteAsync("verification_technical_failure", new
            {
                guildId,
                userId = modal.User.Id,
                error = ex.Message
            }, CancellationToken.None);
            await modal.RespondAsync("Something went wrong on the bot side. I’ve kept you in the welcome area and notified the server owner.", ephemeral: true);
        }
    }

    private async Task ApproveAsync(SocketModal modal, GuildSettings guildSettings, VerificationSession session, VerificationDecision decision)
    {
        var guild = client.GetGuild(guildSettings.GuildId);
        var member = guild?.GetUser(modal.User.Id);
        if (guild == null || member == null)
            throw new InvalidOperationException("The guild member could not be resolved during approval.");

        var memberRole = guild.GetRole(guildSettings.MemberRoleId);
        var newRole = guild.GetRole(guildSettings.NewRoleId);
        if (memberRole == null || newRole == null)
            throw new InvalidOperationException("Required roles are missing for approval.");

        await member.AddRoleAsync(memberRole);
        await member.RemoveRoleAsync(newRole);
        await sessionStore.RemoveAsync(session.GuildId, session.UserId, CancellationToken.None);
        await auditLog.WriteAsync("verification_approved", new
        {
            guildId = session.GuildId,
            userId = session.UserId,
            reason = decision.Reason,
            confidence = decision.Confidence
        }, CancellationToken.None);
        await modal.RespondAsync(decision.FriendlyReply, ephemeral: true);
    }

    private async Task RetryAsync(SocketModal modal, GuildSettings guildSettings, VerificationSession session, VerificationDecision decision, bool notifyOwner)
    {
        var cooldown = decision.SuggestedCooldown ?? guildSettings.Onboarding.Cooldown;
        session.CooldownUntil = DateTimeOffset.UtcNow.Add(cooldown);
        await sessionStore.UpsertAsync(session, CancellationToken.None);
        await auditLog.WriteAsync("verification_retry", new
        {
            guildId = session.GuildId,
            userId = session.UserId,
            outcome = decision.Outcome.ToString(),
            reason = decision.Reason,
            confidence = decision.Confidence,
            attempt = session.AttemptCount
        }, CancellationToken.None);

        if (notifyOwner)
        {
            await NotifyOwnerAsync(guildSettings,
                $"Uncertain verification in {guildSettings.Name} for {modal.User.Username} ({modal.User.Id}). Reason: {decision.Reason}");
        }

        await modal.RespondAsync(decision.FriendlyReply, ephemeral: true);
    }

    private async Task NotifyOwnerAsync(GuildSettings guildSettings, string content)
    {
        try
        {
            var guild = client.GetGuild(guildSettings.GuildId);
            IUser? owner = guild?.GetUser(guildSettings.OwnerUserId);
            owner ??= await client.Rest.GetUserAsync(guildSettings.OwnerUserId);
            if (owner != null)
                await owner.SendMessageAsync(content);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify owner {OwnerUserId} for guild {GuildId}", guildSettings.OwnerUserId, guildSettings.GuildId);
        }
    }

    private async Task EnsureWelcomeMessageAsync(SocketTextChannel channel, GuildSettings guildSettings)
    {
        var messages = await channel.GetMessagesAsync(limit: 20).FlattenAsync();
        var existing = messages.FirstOrDefault(m =>
            m.Author.Id == client.CurrentUser.Id &&
            m.Components.Any() &&
            m.Embeds.Any(embed => string.Equals(embed.Title, guildSettings.Onboarding.WelcomeMessageTitle, StringComparison.Ordinal)));

        if (existing != null)
            return;

        var embed = new EmbedBuilder()
            .WithTitle(guildSettings.Onboarding.WelcomeMessageTitle)
            .WithDescription($"{guildSettings.Onboarding.WelcomeMessageBody}\n\n{guildSettings.Onboarding.RulesHint}")
            .WithColor(new Color(77, 122, 255))
            .Build();

        var component = new ComponentBuilder()
            .WithButton(guildSettings.Onboarding.StartButtonLabel, StartVerificationCustomId, ButtonStyle.Primary)
            .Build();

        await channel.SendMessageAsync(embed: embed, components: component);
    }

    private async Task<VerificationSession> GetOrCreateSessionAsync(ulong guildId, IUser user, TimeSpan staleTimeout)
    {
        return await sessionStore.GetAsync(guildId, user.Id, CancellationToken.None) ?? new VerificationSession
        {
            GuildId = guildId,
            UserId = user.Id,
            UserName = user.Username,
            JoinedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(staleTimeout)
        };
    }

    private async Task RunStaleCleanupLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var sessions = await sessionStore.ListAsync(cancellationToken);
            foreach (var session in sessions.Where(s => s.ExpiresAt <= DateTimeOffset.UtcNow))
            {
                var guildSettings = settings.FindGuild(session.GuildId);
                var guild = client.GetGuild(session.GuildId);
                var member = guild?.GetUser(session.UserId);
                if (guildSettings == null || member == null)
                {
                    await sessionStore.RemoveAsync(session.GuildId, session.UserId, cancellationToken);
                    continue;
                }

                try
                {
                    await member.KickAsync("Verification expired");
                    await sessionStore.RemoveAsync(session.GuildId, session.UserId, cancellationToken);
                    await auditLog.WriteAsync("verification_expired", new
                    {
                        guildId = session.GuildId,
                        userId = session.UserId
                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to kick stale NEW user {UserId} from guild {GuildId}", session.UserId, session.GuildId);
                }
            }
        }
    }
}
