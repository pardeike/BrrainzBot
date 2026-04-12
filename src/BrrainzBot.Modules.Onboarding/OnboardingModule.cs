using BrrainzBot.Host;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace BrrainzBot.Modules.Onboarding;

public sealed class OnboardingModule(
    DiscordSocketClient client,
    IBotSettingsProvider settingsProvider,
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
        settingsProvider.Changed += HandleSettingsChanged;
        _ = Task.Run(() => RunMaintenanceLoopAsync(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    private async Task HandleReadyAsync()
    {
        await SyncWelcomeMessagesAsync();
    }

    private async Task HandleUserJoinedAsync(SocketGuildUser user)
    {
        var serverSettings = FindActiveServerSettings(user.Guild.Id);
        if (serverSettings == null)
            return;
        var session = new VerificationSession
        {
            ServerId = user.Guild.Id,
            UserId = user.Id,
            UserName = user.DisplayName,
            JoinedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(serverSettings.Onboarding.StaleTimeout)
        };

        await sessionStore.UpsertAsync(session, CancellationToken.None);
        await auditLog.WriteAsync("user_joined", new
        {
            serverId = user.Guild.Id,
            userId = user.Id,
            userName = user.DisplayName
        }, CancellationToken.None);
    }

    private async Task HandleButtonExecutedAsync(SocketMessageComponent component)
    {
        if (!string.Equals(component.Data.CustomId, StartVerificationCustomId, StringComparison.Ordinal))
            return;

        if (component.GuildId is not { } serverId)
            return;

        var serverSettings = FindActiveServerSettings(serverId);
        if (serverSettings == null)
            return;

        var session = await GetOrCreateSessionAsync(serverId, component.User, serverSettings.Onboarding.StaleTimeout);
        if (session.CooldownUntil is { } cooldownUntil && cooldownUntil > DateTimeOffset.UtcNow)
        {
            await component.RespondAsync(
                $"Please give it a moment before retrying. You can try again <t:{cooldownUntil.ToUnixTimeSeconds()}:R>.",
                ephemeral: true);
            return;
        }

        if (session.AttemptCount >= serverSettings.Onboarding.MaxAttempts)
        {
            await component.RespondAsync(
                "You have used all verification attempts for now. Please wait for a moderator or rejoin later.",
                ephemeral: true);
            return;
        }

        var modal = new ModalBuilder()
            .WithTitle("Quick verification")
            .WithCustomId(VerificationModalCustomId)
            .AddTextInput(serverSettings.Onboarding.FirstQuestionLabel, WhyHereCustomId, TextInputStyle.Paragraph, maxLength: 300)
            .AddTextInput(serverSettings.Onboarding.SecondQuestionLabel, WhatDoYouWantCustomId, TextInputStyle.Paragraph, maxLength: 300)
            .AddTextInput(serverSettings.Onboarding.ThirdQuestionLabel, RuleParaphraseCustomId, TextInputStyle.Paragraph, maxLength: 300)
            .Build();

        await component.RespondWithModalAsync(modal);
    }

    private async Task HandleModalSubmittedAsync(SocketModal modal)
    {
        if (!string.Equals(modal.Data.CustomId, VerificationModalCustomId, StringComparison.Ordinal))
            return;

        if (modal.GuildId is not { } serverId)
            return;

        var serverSettings = FindActiveServerSettings(serverId);
        if (serverSettings == null)
            return;

        var session = await GetOrCreateSessionAsync(serverId, modal.User, serverSettings.Onboarding.StaleTimeout);
        if (session.AttemptCount >= serverSettings.Onboarding.MaxAttempts)
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
            serverSettings.Name,
            serverSettings.ServerTopicPrompt,
            serverSettings.Onboarding.RulesHint,
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
                    await ApproveAsync(modal, serverSettings, session, decision);
                    break;
                case VerificationOutcome.Retry:
                    await RetryAsync(modal, serverSettings, session, decision, notifyOwner: false);
                    break;
                default:
                    await RetryAsync(modal, serverSettings, session, decision, notifyOwner: serverSettings.Onboarding.NotifyOwnerOnUncertain);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Verification failed for user {UserId} in server {ServerId}", modal.User.Id, serverId);
            session.LastDecisionReason = ex.Message;
            session.LastOutcome = VerificationOutcome.Uncertain;
            session.CooldownUntil = DateTimeOffset.UtcNow.Add(serverSettings.Onboarding.Cooldown);
            await sessionStore.UpsertAsync(session, CancellationToken.None);
            if (serverSettings.Onboarding.NotifyOwnerOnTechnicalFailure)
            {
                await NotifyOwnerAsync(serverSettings, $"Technical failure during verification for {modal.User.Username} ({modal.User.Id}). Error: {ex.Message}");
            }
            await auditLog.WriteAsync("verification_technical_failure", new
            {
                serverId,
                userId = modal.User.Id,
                error = ex.Message
            }, CancellationToken.None);
            await modal.RespondAsync("Something went wrong on the bot side. I’ve kept you in the welcome area and notified the server owner.", ephemeral: true);
        }
    }

    private async Task ApproveAsync(SocketModal modal, ServerSettings serverSettings, VerificationSession session, VerificationDecision decision)
    {
        var server = client.GetGuild(serverSettings.ServerId);
        var member = server?.GetUser(modal.User.Id);
        if (server == null || member == null)
            throw new InvalidOperationException("The server member could not be resolved during approval.");

        var memberRole = server.GetRole(serverSettings.MemberRoleId)
            ?? throw new InvalidOperationException("The member role is missing for approval.");
        await member.AddRoleAsync(memberRole);

        await sessionStore.RemoveAsync(session.ServerId, session.UserId, CancellationToken.None);
        await auditLog.WriteAsync("verification_approved", new
        {
            serverId = session.ServerId,
            userId = session.UserId,
            reason = decision.Reason,
            confidence = decision.Confidence
        }, CancellationToken.None);
        await modal.RespondAsync(decision.FriendlyReply, ephemeral: true);
    }

    private async Task RetryAsync(SocketModal modal, ServerSettings serverSettings, VerificationSession session, VerificationDecision decision, bool notifyOwner)
    {
        var cooldown = decision.SuggestedCooldown ?? serverSettings.Onboarding.Cooldown;
        session.CooldownUntil = DateTimeOffset.UtcNow.Add(cooldown);
        await sessionStore.UpsertAsync(session, CancellationToken.None);
        await auditLog.WriteAsync("verification_retry", new
        {
            serverId = session.ServerId,
            userId = session.UserId,
            outcome = decision.Outcome.ToString(),
            reason = decision.Reason,
            confidence = decision.Confidence,
            attempt = session.AttemptCount
        }, CancellationToken.None);

        if (notifyOwner)
        {
            await NotifyOwnerAsync(serverSettings,
                $"Uncertain verification in {serverSettings.Name} for {modal.User.Username} ({modal.User.Id}). Reason: {decision.Reason}");
        }

        await modal.RespondAsync(decision.FriendlyReply, ephemeral: true);
    }

    private async Task NotifyOwnerAsync(ServerSettings serverSettings, string content)
    {
        try
        {
            var server = client.GetGuild(serverSettings.ServerId);
            IUser? owner = server?.GetUser(serverSettings.OwnerUserId);
            owner ??= await client.Rest.GetUserAsync(serverSettings.OwnerUserId);
            if (owner != null)
                await owner.SendMessageAsync(content);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to notify owner {OwnerUserId} for server {ServerId}", serverSettings.OwnerUserId, serverSettings.ServerId);
        }
    }

    private void HandleSettingsChanged(BotSettings settings)
    {
        _ = Task.Run(SyncWelcomeMessagesAsync);
        logger.LogInformation("Applied updated activation state from config.");
    }

    private ServerSettings? FindActiveServerSettings(ulong serverId) =>
        settingsProvider.Current.FindServer(serverId) is { IsActive: true } serverSettings ? serverSettings : null;

    private async Task EnsureWelcomeMessageAsync(SocketTextChannel channel, ServerSettings serverSettings)
    {
        var messages = await channel.GetMessagesAsync(limit: 20).FlattenAsync();
        var existing = messages.FirstOrDefault(m =>
            m.Author.Id == client.CurrentUser.Id &&
            m.Components.Any() &&
            (m.Embeds.Any(embed => string.Equals(embed.Title, serverSettings.Onboarding.WelcomeMessageTitle, StringComparison.Ordinal))
             || string.Equals(m.Content, BuildWelcomeMessageContent(serverSettings), StringComparison.Ordinal)));

        if (existing != null)
            return;

        var component = new ComponentBuilder()
            .WithButton(serverSettings.Onboarding.StartButtonLabel, StartVerificationCustomId, ButtonStyle.Primary)
            .Build();

        await channel.SendMessageAsync(text: BuildWelcomeMessageContent(serverSettings), components: component);
    }

    private async Task<VerificationSession> GetOrCreateSessionAsync(ulong serverId, IUser user, TimeSpan staleTimeout)
    {
        return await sessionStore.GetAsync(serverId, user.Id, CancellationToken.None) ?? new VerificationSession
        {
            ServerId = serverId,
            UserId = user.Id,
            UserName = user.Username,
            JoinedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(staleTimeout)
        };
    }

    private async Task SyncWelcomeMessagesAsync()
    {
        foreach (var serverSettings in settingsProvider.Current.Servers.Where(s => s.IsActive))
        {
            var server = client.GetGuild(serverSettings.ServerId);
            if (server == null)
            {
                logger.LogWarning("Server {ServerId} was not found in cache during onboarding initialization.", serverSettings.ServerId);
                continue;
            }

            var channel = server.GetTextChannel(serverSettings.WelcomeChannelId);
            if (channel == null)
            {
                logger.LogWarning("Welcome channel {ChannelId} was not found for server {ServerId}.", serverSettings.WelcomeChannelId, serverSettings.ServerId);
                continue;
            }

            try
            {
                await EnsureWelcomeMessageAsync(channel, serverSettings);
            }
            catch (HttpException ex) when (ex.HttpCode == System.Net.HttpStatusCode.Forbidden || ex.DiscordCode == DiscordErrorCode.MissingPermissions)
            {
                logger.LogWarning(
                    ex,
                    "The bot cannot create the welcome prompt in channel {ChannelId} for server {ServerId}. Check the channel or parent-category posting permissions for the bot role.",
                    serverSettings.WelcomeChannelId,
                    serverSettings.ServerId);
            }
        }
    }

    private static string BuildWelcomeMessageContent(ServerSettings serverSettings) =>
        $"**{serverSettings.Onboarding.WelcomeMessageTitle}**\n{serverSettings.Onboarding.WelcomeMessageBody}\n\n{serverSettings.Onboarding.RulesHint}";

    private async Task RunMaintenanceLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await SyncWelcomeMessagesAsync();
            var sessions = await sessionStore.ListAsync(cancellationToken);
            foreach (var session in sessions.Where(s => s.ExpiresAt <= DateTimeOffset.UtcNow))
            {
                var serverSettings = FindActiveServerSettings(session.ServerId);
                var server = client.GetGuild(session.ServerId);
                var member = server?.GetUser(session.UserId);
                if (serverSettings == null || member == null)
                {
                    await sessionStore.RemoveAsync(session.ServerId, session.UserId, cancellationToken);
                    continue;
                }

                var resolvedServer = server!;
                var memberRole = resolvedServer.GetRole(serverSettings.MemberRoleId);
                if (memberRole != null && member.Roles.Any(role => role.Id == memberRole.Id))
                {
                    await sessionStore.RemoveAsync(session.ServerId, session.UserId, cancellationToken);
                    continue;
                }

                try
                {
                    await member.KickAsync("Verification expired");
                    await sessionStore.RemoveAsync(session.ServerId, session.UserId, cancellationToken);
                    await auditLog.WriteAsync("verification_expired", new
                    {
                        serverId = session.ServerId,
                        userId = session.UserId
                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to kick stale unverified user {UserId} from server {ServerId}", session.UserId, session.ServerId);
                }
            }
        }
    }
}
