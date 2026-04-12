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
                    OwnerUserId = 0
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

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubMessageHandler());
    }

    private sealed class StubMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
    }
}
