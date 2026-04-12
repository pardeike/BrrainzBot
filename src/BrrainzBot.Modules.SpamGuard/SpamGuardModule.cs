using BrrainzBot.Host;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace BrrainzBot.Modules.SpamGuard;

public sealed class SpamGuardModule(
    DiscordSocketClient client,
    BotSettings settings,
    IAuditLog auditLog,
    ILogger<SpamGuardModule> logger) : IDiscordModule
{
    private readonly Dictionary<ulong, MessageTracker> _trackers = new();

    public string Name => "SpamGuard";

    public Task RegisterAsync(CancellationToken cancellationToken)
    {
        client.MessageReceived += HandleMessageReceivedAsync;
        _ = Task.Run(() => RunCleanupLoopAsync(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    private async Task HandleMessageReceivedAsync(SocketMessage message)
    {
        if (message.Channel is not SocketTextChannel channel)
            return;

        if (message.Author.IsBot)
            return;

        if (message.Author is not SocketGuildUser guildUser)
            return;

        var guildSettings = settings.FindGuild(channel.Guild.Id);
        if (guildSettings is not { EnableSpamGuard: true })
            return;

        if (guildUser.GuildPermissions.Administrator || guildUser.GuildPermissions.ManageMessages || guildUser.GuildPermissions.ModerateMembers)
            return;

        var spamSettings = guildSettings.SpamGuard;
        var tracker = GetTracker(channel.Guild.Id, spamSettings);
        var (result, firstChannelId) = tracker.CheckMessage(message.Author.Id, channel.Id, message.Content, message.Timestamp);

        switch (result)
        {
            case SpamDetectionResult.HoneypotTriggered:
            case SpamDetectionResult.DuplicateDetected:
                await DeleteUserMessagesInIntervalAsync(
                    channel.Guild,
                    message.Author.Id,
                    message.Timestamp.AddSeconds(-spamSettings.PastMessageIntervalSeconds),
                    message.Timestamp.AddSeconds(spamSettings.FutureMessageIntervalSeconds),
                    message.Author.Username);
                break;
            case SpamDetectionResult.HoneypotDetected:
                await DeleteMessageAsync(message, channel.Name, "known_spammer");
                break;
        }

        if (result is SpamDetectionResult.HoneypotTriggered or SpamDetectionResult.DuplicateDetected or SpamDetectionResult.HoneypotDetected)
        {
            await auditLog.WriteAsync("spam_detected", new
            {
                guildId = channel.Guild.Id,
                channelId = channel.Id,
                userId = message.Author.Id,
                userName = message.Author.Username,
                result = result.ToString(),
                firstChannelId
            }, CancellationToken.None);
        }
    }

    private async Task DeleteUserMessagesInIntervalAsync(SocketGuild guild, ulong userId, DateTimeOffset startTime, DateTimeOffset endTime, string userName)
    {
        foreach (var channel in guild.TextChannels)
        {
            var permissions = guild.CurrentUser.GetPermissions(channel);
            if (!permissions.ViewChannel || !permissions.ReadMessageHistory || !permissions.ManageMessages)
                continue;

            var messages = await channel.GetMessagesAsync(100).FlattenAsync();
            var userMessages = messages
                .Where(m => m.Author.Id == userId && m.Timestamp >= startTime && m.Timestamp <= endTime)
                .ToList();

            foreach (var userMessage in userMessages)
            {
                await DeleteMessageAsync(userMessage, channel.Name, "spam_interval_cleanup");
            }

            logger.LogInformation("Checked #{Channel} for spam cleanup of {User}", channel.Name, userName);
        }
    }

    private async Task DeleteMessageAsync(IMessage message, string channelName, string reason)
    {
        try
        {
            await message.DeleteAsync();
            logger.LogInformation("Deleted message {MessageId} in #{Channel} because {Reason}", message.Id, channelName, reason);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete message {MessageId} in #{Channel}", message.Id, channelName);
        }
    }

    private MessageTracker GetTracker(ulong guildId, SpamGuardSettings settingsForGuild)
    {
        if (_trackers.TryGetValue(guildId, out var tracker))
            return tracker;

        tracker = new MessageTracker(
            settingsForGuild.MessageDeltaIntervalSeconds,
            settingsForGuild.MinimumMessageLength,
            settingsForGuild.LinkRequired,
            settingsForGuild.MessageSimilarityThreshold,
            settingsForGuild.HoneypotChannelId);
        _trackers[guildId] = tracker;
        return tracker;
    }

    private async Task RunCleanupLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            foreach (var guildSettings in settings.Guilds.Where(g => g.EnableSpamGuard))
            {
                if (_trackers.TryGetValue(guildSettings.GuildId, out var tracker))
                {
                    tracker.PerformPeriodicCleanup(DateTimeOffset.UtcNow, guildSettings.SpamGuard.MessageDeltaIntervalSeconds);
                }
            }
        }
    }
}
