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
        Assert.Contains(report.Messages, message => message.Code == "server.id.zero");
    }

    [Fact]
    public async Task DoctorExplainsWhenServerIsInactive()
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

        Assert.Contains(report.Messages, message => message.Code == "server.inactive");
        Assert.Contains(report.Messages, message => message.Message.Contains("brrainzbot enable 123", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DoctorExplainsLikelyCauseWhenServerCannotBeReached()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/v10/users/@me")
            {
                return JsonResponse("""
                    {
                      "id": "777"
                    }
                    """);
            }

            if (request.RequestUri?.AbsolutePath == "/api/v10/guilds/123")
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }));

        var settings = new BotSettings
        {
            Servers =
            [
                new ServerSettings
                {
                    Name = "Test Server",
                    ServerId = 123,
                    IsActive = true,
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

        var message = Assert.Single(report.Messages, m => m.Code == "discord.server.unreachable");
        Assert.Contains("server ID is wrong", message.Message);
        Assert.Contains("invited", message.Message);
    }

    [Fact]
    public async Task DoctorExplainsWhenServerIdIsUsedAsMemberRoleId()
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

        Assert.Contains(report.Messages, message => message.Code == "server.memberrole.everyone");
        Assert.DoesNotContain(report.Messages, message => message.Code == "server.memberrole.zero");
    }

    [Fact]
    public async Task DoctorRejectsWhenNewAndMemberRoleIdsAreTheSame()
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

        Assert.Contains(report.Messages, message => message.Code == "server.roles.same");
    }

    [Fact]
    public async Task DoctorRejectsMissingHoneypotChannelWhenSpamGuardIsEnabled()
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

        Assert.Contains(report.Messages, message => message.Code == "server.honeypot.zero");
    }

    [Fact]
    public async Task DoctorWarnsWhenWelcomeChannelLayoutLooksWrongForEveryoneMode()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory(request =>
        {
            return request.RequestUri?.AbsolutePath switch
            {
                "/api/v10/users/@me" => JsonResponse("""{ "id": "777" }"""),
                "/api/v10/guilds/123" => JsonResponse("""{ "id": "123", "mfa_level": 0 }"""),
                "/api/v10/guilds/123/channels" => JsonResponse("""
                    [
                      {
                        "id": "456",
                        "name": "welcome",
                        "permission_overwrites": []
                      }
                    ]
                    """),
                "/api/v10/guilds/123/roles" => JsonResponse("""
                    [
                      { "id": "123", "name": "@everyone", "position": 0 },
                      { "id": "789", "name": "NEW", "position": 1 }
                    ]
                    """),
                "/api/v10/guilds/123/members/777" => JsonResponse("""
                    {
                      "roles": [ "555" ]
                    }
                    """),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            };
        }));

        var settings = new BotSettings
        {
            Servers =
            [
                new ServerSettings
                {
                    Name = "Test Server",
                    ServerId = 123,
                    IsActive = true,
                    WelcomeChannelId = 456,
                    NewRoleId = 789,
                    MemberRoleId = 123,
                    OwnerUserId = 999
                }
            ]
        };
        var secrets = new RuntimeSecrets
        {
            DiscordToken = "token"
        };
        var paths = CreatePathsWithPlaceholderFiles();

        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);

        Assert.Contains(report.Messages, message => message.Code == "discord.welcome.everyone.visible");
        Assert.Contains(report.Messages, message => message.Code == "discord.welcome.new.hidden");
    }

    [Fact]
    public async Task DoctorDoesNotWarnWhenWelcomeDeniesMemberViewChannel()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory(request =>
        {
            return request.RequestUri?.AbsolutePath switch
            {
                "/api/v10/users/@me" => JsonResponse("""{ "id": "777" }"""),
                "/api/v10/guilds/123" => JsonResponse("""{ "id": "123", "mfa_level": 0 }"""),
                "/api/v10/guilds/123/channels" => JsonResponse("""
                    [
                      {
                        "id": "456",
                        "name": "welcome",
                        "permission_overwrites": [
                          { "id": "1000", "type": 0, "allow": "0", "deny": "1024" }
                        ]
                      }
                    ]
                    """),
                "/api/v10/guilds/123/roles" => JsonResponse($$"""
                    [
                      { "id": "123", "name": "@everyone", "position": 0, "permissions": "0" },
                      { "id": "789", "name": "NEW", "position": 1, "permissions": "0" },
                      { "id": "1000", "name": "MEMBER", "position": 2, "permissions": "0" },
                      { "id": "555", "name": "BrrainzBot", "position": 3, "permissions": "{{AllRequiredPermissions()}}" }
                    ]
                    """),
                "/api/v10/guilds/123/members/777" => JsonResponse("""
                    {
                      "roles": [ "555" ]
                    }
                    """),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            };
        }));

        var settings = new BotSettings
        {
            Servers =
            [
                new ServerSettings
                {
                    Name = "Test Server",
                    ServerId = 123,
                    IsActive = true,
                    WelcomeChannelId = 456,
                    NewRoleId = 789,
                    MemberRoleId = 1000,
                    OwnerUserId = 999
                }
            ]
        };
        var secrets = new RuntimeSecrets
        {
            DiscordToken = "token"
        };
        var paths = CreatePathsWithPlaceholderFiles();

        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);

        Assert.DoesNotContain(report.Messages, message => message.Code == "discord.welcome.member.visible");
    }

    [Fact]
    public async Task DoctorReportsMissingRequiredBotPermissions()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory(request =>
        {
            return request.RequestUri?.AbsolutePath switch
            {
                "/api/v10/users/@me" => JsonResponse("""{ "id": "777" }"""),
                "/api/v10/guilds/123" => JsonResponse("""{ "id": "123", "mfa_level": 0 }"""),
                "/api/v10/guilds/123/channels" => JsonResponse("""
                    [
                      {
                        "id": "456",
                        "name": "welcome",
                        "permission_overwrites": []
                      }
                    ]
                    """),
                "/api/v10/guilds/123/roles" => JsonResponse($$"""
                    [
                      { "id": "123", "name": "@everyone", "position": 0, "permissions": "0" },
                      { "id": "789", "name": "NEW", "position": 1, "permissions": "0" },
                      { "id": "1000", "name": "MEMBER", "position": 2, "permissions": "0" },
                      { "id": "555", "name": "BrrainzBot", "position": 3, "permissions": "{{RequiredPermissionsWithoutManageChannels()}}" }
                    ]
                    """),
                "/api/v10/guilds/123/members/777" => JsonResponse("""
                    {
                      "roles": [ "555" ]
                    }
                    """),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            };
        }));

        var settings = new BotSettings
        {
            Servers =
            [
                new ServerSettings
                {
                    Name = "Test Server",
                    ServerId = 123,
                    IsActive = true,
                    WelcomeChannelId = 456,
                    NewRoleId = 789,
                    MemberRoleId = 1000,
                    OwnerUserId = 999
                }
            ]
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
    public async Task DoctorWarnsWhenServerWide2FaIsEnabled()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory(request =>
        {
            return request.RequestUri?.AbsolutePath switch
            {
                "/api/v10/users/@me" => JsonResponse("""{ "id": "777" }"""),
                "/api/v10/guilds/123" => JsonResponse("""{ "id": "123", "mfa_level": 1 }"""),
                "/api/v10/guilds/123/channels" => JsonResponse("""
                    [
                      {
                        "id": "456",
                        "name": "welcome",
                        "permission_overwrites": []
                      }
                    ]
                    """),
                "/api/v10/guilds/123/roles" => JsonResponse($$"""
                    [
                      { "id": "123", "name": "@everyone", "position": 0, "permissions": "0" },
                      { "id": "789", "name": "NEW", "position": 1, "permissions": "0" },
                      { "id": "1000", "name": "MEMBER", "position": 2, "permissions": "0" },
                      { "id": "555", "name": "BrrainzBot", "position": 3, "permissions": "{{AllRequiredPermissions()}}" }
                    ]
                    """),
                "/api/v10/guilds/123/members/777" => JsonResponse("""
                    {
                      "roles": [ "555" ]
                    }
                    """),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            };
        }));

        var settings = new BotSettings
        {
            Servers =
            [
                new ServerSettings
                {
                    Name = "Test Server",
                    ServerId = 123,
                    IsActive = true,
                    WelcomeChannelId = 456,
                    NewRoleId = 789,
                    MemberRoleId = 1000,
                    OwnerUserId = 999
                }
            ]
        };
        var secrets = new RuntimeSecrets
        {
            DiscordToken = "token"
        };
        var paths = CreatePathsWithPlaceholderFiles();

        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);

        var message = Assert.Single(report.Messages, entry => entry.Code == "discord.server_2fa.enabled");
        Assert.Contains("server-wide 2FA enabled", message.Message);
    }

    [Fact]
    public async Task DoctorWarnsWhenEveryoneHasPermissionsTheBotCannotCopyToMember()
    {
        var doctor = new BotDoctor(new StubHttpClientFactory(request =>
        {
            return request.RequestUri?.AbsolutePath switch
            {
                "/api/v10/users/@me" => JsonResponse("""{ "id": "777" }"""),
                "/api/v10/guilds/123" => JsonResponse("""{ "id": "123", "mfa_level": 0 }"""),
                "/api/v10/guilds/123/channels" => JsonResponse("""
                    [
                      {
                        "id": "456",
                        "name": "welcome",
                        "permission_overwrites": []
                      }
                    ]
                    """),
                "/api/v10/guilds/123/roles" => JsonResponse($$"""
                    [
                      { "id": "123", "name": "@everyone", "position": 0, "permissions": "{{EveryonePermissionsWithCreateInvite()}}" },
                      { "id": "789", "name": "NEW", "position": 1, "permissions": "0" },
                      { "id": "1000", "name": "MEMBER", "position": 2, "permissions": "0" },
                      { "id": "555", "name": "BrrainzBot", "position": 3, "permissions": "{{AllRequiredPermissions()}}" }
                    ]
                    """),
                "/api/v10/guilds/123/members/777" => JsonResponse("""
                    {
                      "roles": [ "555" ]
                    }
                    """),
                _ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            };
        }));

        var settings = new BotSettings
        {
            Servers =
            [
                new ServerSettings
                {
                    Name = "Test Server",
                    ServerId = 123,
                    IsActive = true,
                    WelcomeChannelId = 456,
                    NewRoleId = 789,
                    MemberRoleId = 1000,
                    OwnerUserId = 999
                }
            ]
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

    private static AppPaths CreatePathsWithPlaceholderFiles()
    {
        var paths = AppPaths.FromRoot(Path.Combine(Path.GetTempPath(), $"brrainzbot-tests-{Guid.NewGuid():N}"));
        paths.EnsureDirectoriesExist();
        File.WriteAllText(paths.ConfigFilePath, "{}");
        File.WriteAllText(paths.SecretsFilePath, "{}");
        return paths;
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
