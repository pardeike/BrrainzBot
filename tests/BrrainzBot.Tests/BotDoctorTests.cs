using System.Text.Json;
using BrrainzBot.Host;
using BrrainzBot.Infrastructure;
using Discord;

namespace BrrainzBot.Tests;

public sealed class BotDoctorTests
{
    [Fact]
    public async Task DoctorReportsMissingCoreValuesBeforeRemoteValidation()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory());
        var settings = new BotSettings
        {
            Servers =
            [
                new ServerSettings
                {
                    Name = "Test Server",
                    ServerId = 0,
                    WelcomeChannelId = 0,
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
        var paths = CreatePathsWithPlaceholderFiles();

        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);

        Assert.True(report.HasErrors);
        Assert.Contains(report.Messages, message => message.Code == "discord.token.empty");
        Assert.Contains(report.Messages, message => message.Code == "server.id.zero");
        Assert.Contains(report.Messages, message => message.Code == "server.memberrole.zero");
    }

    [Fact]
    public async Task DoctorExplainsWhenServerIsInactive()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory());
        var settings = new BotSettings
        {
            Servers = [CreateServerSettings(isActive: false)]
        };
        var secrets = new RuntimeSecrets();
        var paths = CreatePathsWithPlaceholderFiles();

        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);

        Assert.Contains(report.Messages, message => message.Code == "server.inactive");
        Assert.Contains(report.Messages, message => message.Message.Contains("brrainzbot enable 123", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DoctorExplainsLikelyCauseWhenServerCannotBeReached()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/v10/users/@me")
                return JsonResponse("""{ "id": "777" }""");

            if (request.RequestUri?.AbsolutePath == "/api/v10/guilds/123")
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }));

        var settings = new BotSettings
        {
            Servers = [CreateServerSettings()]
        };
        var secrets = new RuntimeSecrets
        {
            DiscordToken = "token"
        };
        var paths = CreatePathsWithPlaceholderFiles();

        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);

        var message = Assert.Single(report.Messages, m => m.Code == "discord.server.unreachable");
        Assert.Contains("server ID is wrong", message.Message);
        Assert.Contains("invited", message.Message);
    }

    [Fact]
    public async Task DoctorRejectsOldEveryoneMemberRoleMode()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory());
        var settings = new BotSettings
        {
            Servers =
            [
                new ServerSettings
                {
                    Name = "Test Server",
                    ServerId = 123,
                    IsActive = false,
                    WelcomeChannelId = 456,
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

        Assert.Contains(report.Messages, message => message.Code == "server.memberrole.everyone.invalid");
    }

    [Fact]
    public async Task DoctorRejectsMissingHoneypotChannelWhenSpamGuardIsEnabled()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory());
        var settings = new BotSettings
        {
            Servers =
            [
                CreateServerSettings(enableSpamGuard: true, honeypotChannelId: 0)
            ]
        };
        var secrets = new RuntimeSecrets();
        var paths = CreatePathsWithPlaceholderFiles();

        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);

        Assert.Contains(report.Messages, message => message.Code == "server.honeypot.zero");
    }

    [Fact]
    public async Task DoctorDoesNotWarnWhenWelcomeDeniesMemberViewChannel()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory(HealthyResponder(
            welcomePermissionOverwrites: """
                [
                  { "id": "1000", "type": 0, "allow": "0", "deny": "1024" }
                ]
                """)));

        var settings = new BotSettings
        {
            Servers = [CreateServerSettings()]
        };
        var secrets = new RuntimeSecrets
        {
            DiscordToken = "token"
        };
        var paths = CreatePathsWithPlaceholderFiles();

        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);

        Assert.DoesNotContain(report.Messages, message => message.Code == "discord.welcome.member.visible");
        Assert.DoesNotContain(report.Messages, message => message.Code == "discord.welcome.everyone.hidden");
    }

    [Fact]
    public async Task DoctorWarnsWhenWelcomeHidesEveryone()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory(HealthyResponder(
            welcomePermissionOverwrites: """
                [
                  { "id": "123", "type": 0, "allow": "0", "deny": "1024" },
                  { "id": "1000", "type": 0, "allow": "0", "deny": "1024" }
                ]
                """)));

        var settings = new BotSettings
        {
            Servers = [CreateServerSettings()]
        };
        var secrets = new RuntimeSecrets
        {
            DiscordToken = "token"
        };
        var paths = CreatePathsWithPlaceholderFiles();

        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);

        Assert.Contains(report.Messages, message => message.Code == "discord.welcome.everyone.hidden");
    }

    [Fact]
    public async Task DoctorReportsMissingRequiredBotPermissions()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory(HealthyResponder(
            botPermissions: RequiredPermissionsWithoutManageChannels())));

        var settings = new BotSettings
        {
            Servers = [CreateServerSettings()]
        };
        var secrets = new RuntimeSecrets
        {
            DiscordToken = "token"
        };
        var paths = CreatePathsWithPlaceholderFiles();

        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);

        var message = Assert.Single(report.Messages, entry => entry.Code == "discord.bot_permissions.missing");
        Assert.Contains("Manage Channels", message.Message);
        Assert.DoesNotContain("Manage Roles", message.Message);
    }

    [Fact]
    public async Task DoctorWarnsWhenEveryoneHasPermissionsTheBotCannotCopyToMember()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory(HealthyResponder(
            everyonePermissions: EveryonePermissionsWithCreateInvite())));

        var settings = new BotSettings
        {
            Servers = [CreateServerSettings()]
        };
        var secrets = new RuntimeSecrets
        {
            DiscordToken = "token"
        };
        var paths = CreatePathsWithPlaceholderFiles();

        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);

        var message = Assert.Single(report.Messages, entry => entry.Code == "discord.memberrole.partial_copy");
        Assert.Contains("Create Instant Invite", message.Message);
    }

    [Fact]
    public async Task DoctorDoesNotWarnAboutPartialCopyWhenMemberAlreadyHasThosePermissions()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory(HealthyResponder(
            everyonePermissions: EveryonePermissionsWithCreateInvite(),
            memberPermissions: EveryonePermissionsWithCreateInvite())));

        var settings = new BotSettings
        {
            Servers = [CreateServerSettings()]
        };
        var secrets = new RuntimeSecrets
        {
            DiscordToken = "token"
        };
        var paths = CreatePathsWithPlaceholderFiles();

        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);

        Assert.DoesNotContain(report.Messages, entry => entry.Code == "discord.memberrole.partial_copy");
    }

    [Fact]
    public async Task DoctorWarnsWhenExistingMembersStillNeedBackfill()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory(HealthyResponder(
            memberPages:
            [
                """
                [
                  {
                    "user": { "id": "2001", "bot": false },
                    "roles": []
                  },
                  {
                    "user": { "id": "2002", "bot": false },
                    "roles": [ "1000" ]
                  }
                ]
                """
            ])));

        var settings = new BotSettings
        {
            Servers = [CreateServerSettings()]
        };
        var secrets = new RuntimeSecrets
        {
            DiscordToken = "token"
        };
        var paths = CreatePathsWithPlaceholderFiles();

        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);

        var message = Assert.Single(report.Messages, entry => entry.Code == "discord.memberrole.backfill_needed");
        Assert.Contains("set-members 123", message.Message);
    }

    [Fact]
    public async Task DoctorDoesNotWarnAboutBackfillForActiveOnboardingSessions()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory(HealthyResponder(
            memberPages:
            [
                """
                [
                  {
                    "user": { "id": "2001", "bot": false },
                    "roles": []
                  }
                ]
                """
            ])));

        var settings = new BotSettings
        {
            Servers = [CreateServerSettings()]
        };
        var secrets = new RuntimeSecrets
        {
            DiscordToken = "token"
        };
        var paths = CreatePathsWithSessions(new VerificationSession
        {
            ServerId = 123,
            UserId = 2001,
            UserName = "pending-user",
            JoinedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        });

        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);

        Assert.DoesNotContain(report.Messages, entry => entry.Code == "discord.memberrole.backfill_needed");
    }

    private static ServerSettings CreateServerSettings(bool isActive = true, bool enableSpamGuard = false, ulong honeypotChannelId = 654) => new()
    {
        Name = "Test Server",
        ServerId = 123,
        IsActive = isActive,
        WelcomeChannelId = 456,
        MemberRoleId = 1000,
        OwnerUserId = 999,
        EnableSpamGuard = enableSpamGuard,
        SpamGuard = new SpamGuardSettings
        {
            HoneypotChannelId = honeypotChannelId
        }
    };

    private static AppPaths CreatePathsWithPlaceholderFiles()
    {
        var paths = AppPaths.FromRoot(Path.Combine(Path.GetTempPath(), $"brrainzbot-tests-{Guid.NewGuid():N}"));
        paths.EnsureDirectoriesExist();
        File.WriteAllText(paths.ConfigFilePath, "{}");
        File.WriteAllText(paths.SecretsFilePath, "{}");
        return paths;
    }

    private static AppPaths CreatePathsWithSessions(params VerificationSession[] sessions)
    {
        var paths = CreatePathsWithPlaceholderFiles();
        File.WriteAllText(paths.SessionStateFilePath, JsonSerializer.Serialize(sessions));
        return paths;
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> HealthyResponder(
        string? welcomePermissionOverwrites = null,
        ulong? everyonePermissions = null,
        ulong? memberPermissions = null,
        ulong? botPermissions = null,
        params string[] memberPages)
    {
        welcomePermissionOverwrites ??=
            """
            [
              { "id": "1000", "type": 0, "allow": "0", "deny": "1024" }
            ]
            """;

        everyonePermissions ??= 0;
        memberPermissions ??= 0;
        botPermissions ??= AllRequiredPermissions();

        var pages = memberPages.Length == 0
            ? new[]
            {
                """
                [
                  {
                    "user": { "id": "2002", "bot": false },
                    "roles": [ "1000" ]
                  }
                ]
                """
            }
            : memberPages;

        return request =>
        {
            var pathAndQuery = request.RequestUri?.PathAndQuery;
            return pathAndQuery switch
            {
                "/api/v10/users/@me" => JsonResponse("""{ "id": "777" }"""),
                "/api/v10/guilds/123" => JsonResponse("""{ "id": "123" }"""),
                "/api/v10/guilds/123/channels" => JsonResponse($$"""
                    [
                      {
                        "id": "456",
                        "name": "welcome",
                        "permission_overwrites": {{welcomePermissionOverwrites}}
                      },
                      {
                        "id": "654",
                        "name": "honeypot",
                        "permission_overwrites": []
                      }
                    ]
                    """),
                "/api/v10/guilds/123/roles" => JsonResponse($$"""
                    [
                      { "id": "123", "name": "@everyone", "position": 0, "permissions": "{{everyonePermissions}}" },
                      { "id": "1000", "name": "MEMBER", "position": 2, "permissions": "{{memberPermissions}}" },
                      { "id": "555", "name": "BrrainzBot", "position": 3, "permissions": "{{botPermissions}}" }
                    ]
                    """),
                "/api/v10/guilds/123/members/777" => JsonResponse("""
                    {
                      "roles": [ "555" ]
                    }
                    """),
                "/api/v10/guilds/123/members?limit=1000" => JsonResponse(pages.ElementAtOrDefault(0) ?? "[]"),
                "/api/v10/guilds/123/members?limit=1000&after=2002" => JsonResponse(pages.ElementAtOrDefault(1) ?? "[]"),
                "/api/v10/guilds/123/members?limit=1000&after=2001" => JsonResponse(pages.ElementAtOrDefault(1) ?? "[]"),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            };
        };
    }

    private static HttpResponseMessage JsonResponse(string json) => new(System.Net.HttpStatusCode.OK)
    {
        Content = new StringContent(json)
    };

    private static ulong RequiredPermissionsWithoutManageChannels() => Pack(
        GuildPermission.ViewChannel,
        GuildPermission.ReadMessageHistory,
        GuildPermission.SendMessages,
        GuildPermission.ManageMessages,
        GuildPermission.ManageRoles,
        GuildPermission.KickMembers);

    private static ulong AllRequiredPermissions() => Pack(
        GuildPermission.ViewChannel,
        GuildPermission.ReadMessageHistory,
        GuildPermission.SendMessages,
        GuildPermission.ManageMessages,
        GuildPermission.ManageRoles,
        GuildPermission.ManageChannels,
        GuildPermission.KickMembers);

    private static ulong EveryonePermissionsWithCreateInvite() => AllRequiredPermissions() | (ulong)GuildPermission.CreateInstantInvite;

    private static ulong Pack(params GuildPermission[] permissions) => permissions.Aggregate(0UL, static (value, permission) => value | (ulong)permission);

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
