using BrrainzBot.Host;
using BrrainzBot.Infrastructure;

namespace BrrainzBot.Tests;

public sealed class BotDoctorTests
{
    [Fact]
    public async Task DoctorReportsMissingCoreValuesBeforeRemoteValidation()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory());
        var settings = new BotSettings
        {
            Guilds =
            [
                new GuildSettings
                {
                    Name = "Test Guild",
                    GuildId = 0,
                    WelcomeChannelId = 0,
                    NewRoleId = 0,
                    MemberRoleId = 0,
                    OwnerUserId = 0,
                    SpamGuard = new SpamGuardSettings
                    {
                        HoneypotChannelId = 0
                    }
                }
            ]
        };
        var secrets = new RuntimeSecrets();
        var paths = AppPaths.FromRoot(Path.Combine(Path.GetTempPath(), $"brrainzbot-tests-{Guid.NewGuid():N}"));

        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);

        Assert.True(report.HasErrors);
        Assert.Contains(report.Messages, message => message.Code == "discord.token.empty");
        Assert.Contains(report.Messages, message => message.Code == "guild.id.zero");
    }

    [Fact]
    public async Task DoctorExplainsWhenBotIsDisabled()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory());
        var settings = new BotSettings
        {
            Enabled = false,
            Guilds =
            [
                new GuildSettings
                {
                    Name = "Test Guild",
                    GuildId = 123,
                    WelcomeChannelId = 456,
                    NewRoleId = 789,
                    MemberRoleId = 1000,
                    OwnerUserId = 999,
                    SpamGuard = new SpamGuardSettings
                    {
                        HoneypotChannelId = 654
                    }
                }
            ]
        };
        var secrets = new RuntimeSecrets();
        var paths = CreatePathsWithPlaceholderFiles();

        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);

        Assert.Contains(report.Messages, message => message.Code == "bot.disabled");
    }

    [Fact]
    public async Task DoctorExplainsLikelyCauseWhenGuildCannotBeReached()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/v10/users/@me")
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            if (request.RequestUri?.AbsolutePath == "/api/v10/guilds/123")
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }));

        var settings = new BotSettings
        {
            Guilds =
            [
                new GuildSettings
                {
                    Name = "Test Guild",
                    GuildId = 123,
                    WelcomeChannelId = 456,
                    NewRoleId = 789,
                    MemberRoleId = 1000,
                    OwnerUserId = 999,
                    SpamGuard = new SpamGuardSettings
                    {
                        HoneypotChannelId = 654
                    }
                }
            ]
        };
        var secrets = new RuntimeSecrets
        {
            DiscordToken = "token"
        };
        var paths = CreatePathsWithPlaceholderFiles();

        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);

        var message = Assert.Single(report.Messages, m => m.Code == "discord.guild.unreachable");
        Assert.Contains("guild ID is wrong", message.Message);
        Assert.Contains("invited", message.Message);
    }

    [Fact]
    public async Task DoctorExplainsWhenGuildIdIsUsedAsMemberRoleId()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory());
        var settings = new BotSettings
        {
            Guilds =
            [
                new GuildSettings
                {
                    Name = "Test Guild",
                    GuildId = 123,
                    WelcomeChannelId = 456,
                    NewRoleId = 789,
                    MemberRoleId = 123,
                    OwnerUserId = 999,
                    SpamGuard = new SpamGuardSettings
                    {
                        HoneypotChannelId = 654
                    }
                }
            ]
        };
        var secrets = new RuntimeSecrets();
        var paths = CreatePathsWithPlaceholderFiles();

        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);

        Assert.Contains(report.Messages, message => message.Code == "guild.memberrole.everyone");
        Assert.DoesNotContain(report.Messages, message => message.Code == "guild.memberrole.zero");
    }

    [Fact]
    public async Task DoctorRejectsWhenNewAndMemberRoleIdsAreTheSame()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory());
        var settings = new BotSettings
        {
            Guilds =
            [
                new GuildSettings
                {
                    Name = "Test Guild",
                    GuildId = 123,
                    WelcomeChannelId = 456,
                    NewRoleId = 789,
                    MemberRoleId = 789,
                    OwnerUserId = 999,
                    SpamGuard = new SpamGuardSettings
                    {
                        HoneypotChannelId = 654
                    }
                }
            ]
        };
        var secrets = new RuntimeSecrets();
        var paths = CreatePathsWithPlaceholderFiles();

        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);

        Assert.Contains(report.Messages, message => message.Code == "guild.roles.same");
    }

    [Fact]
    public async Task DoctorRejectsMissingHoneypotChannelWhenSpamGuardIsEnabled()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory());
        var settings = new BotSettings
        {
            Guilds =
            [
                new GuildSettings
                {
                    Name = "Test Guild",
                    GuildId = 123,
                    WelcomeChannelId = 456,
                    NewRoleId = 789,
                    MemberRoleId = 1000,
                    OwnerUserId = 999,
                    EnableSpamGuard = true,
                    SpamGuard = new SpamGuardSettings
                    {
                        HoneypotChannelId = 0
                    }
                }
            ]
        };
        var secrets = new RuntimeSecrets();
        var paths = CreatePathsWithPlaceholderFiles();

        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);

        Assert.Contains(report.Messages, message => message.Code == "guild.honeypot.zero");
    }

    private static AppPaths CreatePathsWithPlaceholderFiles()
    {
        var paths = AppPaths.FromRoot(Path.Combine(Path.GetTempPath(), $"brrainzbot-tests-{Guid.NewGuid():N}"));
        paths.EnsureDirectoriesExist();
        File.WriteAllText(paths.ConfigFilePath, "{}");
        File.WriteAllText(paths.SecretsFilePath, "{}");
        return paths;
    }

    private sealed class StubHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage>? responder = null) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubMessageHandler(responder));
    }

    private sealed class StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage>? responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder?.Invoke(request) ?? new HttpResponseMessage(System.Net.HttpStatusCode.OK));
    }
}
