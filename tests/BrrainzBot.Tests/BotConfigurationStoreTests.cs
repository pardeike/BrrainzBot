using BrrainzBot.Host;
using BrrainzBot.Infrastructure;

namespace BrrainzBot.Tests;

public sealed class BotConfigurationStoreTests
{
    [Fact]
    public async Task LoadSettingsAsyncMigratesLegacyGuildConfig()
    {
        var root = Path.Combine(Path.GetTempPath(), $"brrainzbot-store-tests-{Guid.NewGuid():N}");
        var paths = AppPaths.FromRoot(root);
        paths.EnsureDirectoriesExist();

        await File.WriteAllTextAsync(
            paths.ConfigFilePath,
            """
            {
              "FriendlyName": "BrrainzBot",
              "GitHubRepository": "pardeike/BrrainzBot",
              "Updates": {
                "Repository": "pardeike/BrrainzBot",
                "Channel": "stable",
                "Enabled": true
              },
              "Ai": {
                "ProviderType": "OpenAiCompatible",
                "BaseUrl": "https://api.openai.com/v1",
                "Model": "gpt-5.4-nano",
                "RequireHttps": true,
                "AllowInsecureLocalEndpoint": false,
                "ApiKeyEnvironmentVariable": "BRRAINZBOT_OPENAI_API_KEY",
                "Timeout": "00:00:30"
              },
              "Guilds": [
                {
                  "Name": "Legacy Server",
                  "GuildId": 123,
                  "IsActive": true,
                  "WelcomeChannelId": 456,
                  "NewRoleId": 789,
                  "MemberRoleId": 1000,
                  "OwnerUserId": 999,
                  "EnableOnboarding": true,
                  "EnableSpamGuard": false,
                  "GuildTopicPrompt": "Legacy prompt",
                  "PublicReadOnlyChannelIds": [],
                  "Onboarding": {
                    "WelcomeMessageTitle": "Welcome",
                    "WelcomeMessageBody": "Body",
                    "StartButtonLabel": "Start",
                    "RulesHint": "Rules",
                    "MaxAttempts": 3,
                    "Cooldown": "00:10:00",
                    "StaleTimeout": "1.00:00:00",
                    "NotifyOwnerOnUncertain": true,
                    "NotifyOwnerOnTechnicalFailure": true,
                    "FirstQuestionLabel": "Q1",
                    "SecondQuestionLabel": "Q2",
                    "ThirdQuestionLabel": "Q3"
                  },
                  "SpamGuard": {
                    "HoneypotChannelId": 654,
                    "PastMessageIntervalSeconds": 300,
                    "FutureMessageIntervalSeconds": 300,
                    "MessageDeltaIntervalSeconds": 120,
                    "MinimumMessageLength": 40,
                    "LinkRequired": true,
                    "MessageSimilarityThreshold": 0.85
                  }
                }
              ]
            }
            """);

        var store = new BotConfigurationStore();
        var settings = await store.LoadSettingsAsync(paths, CancellationToken.None);

        var server = Assert.Single(settings.Servers);
        Assert.Equal((ulong)123, server.ServerId);
        Assert.Equal("Legacy prompt", server.ServerTopicPrompt);
        Assert.Equal((ulong)654, server.SpamGuard.HoneypotChannelId);
    }
}
