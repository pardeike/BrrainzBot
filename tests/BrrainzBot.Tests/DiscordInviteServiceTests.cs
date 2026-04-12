using BrrainzBot.Host;
using BrrainzBot.Infrastructure;

namespace BrrainzBot.Tests;

public sealed class DiscordInviteServiceTests
{
    [Fact]
    public async Task CreateAsyncUsesConfiguredServerWhenExactlyOneServerExists()
    {
        var service = new DiscordInviteService(new StubHttpClientFactory(_ => JsonResponse("""{ "id": "1492890066526277692" }""")), new RuntimeSecrets
        {
            DiscordToken = "token"
        });
        var settings = new BotSettings
        {
            Servers =
            [
                new ServerSettings
                {
                    Name = "Test Server",
                    ServerId = 123,
                    WelcomeChannelId = 456,
                    MemberRoleId = 1000,
                    OwnerUserId = 999
                }
            ]
        };

        var result = await service.CreateAsync(settings, null, null, CancellationToken.None);

        Assert.Equal((ulong)1492890066526277692, result.ClientId);
        Assert.Equal((ulong)123, result.ServerId);
        Assert.Equal(
            "https://discord.com/oauth2/authorize?client_id=1492890066526277692&permissions=268512274&integration_type=0&scope=bot&guild_id=123&disable_guild_select=true",
            result.Url);
    }

    [Fact]
    public async Task CreateAsyncCanUseExplicitClientIdWithoutToken()
    {
        var service = new DiscordInviteService(new StubHttpClientFactory(_ => throw new InvalidOperationException("HTTP should not be called.")), new RuntimeSecrets());

        var result = await service.CreateAsync(null, null, 1492890066526277692, CancellationToken.None);

        Assert.Equal((ulong)1492890066526277692, result.ClientId);
        Assert.Null(result.ServerId);
        Assert.Equal(
            "https://discord.com/oauth2/authorize?client_id=1492890066526277692&permissions=268512274&integration_type=0&scope=bot",
            result.Url);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(System.Net.HttpStatusCode.OK)
    {
        Content = new StringContent(json)
    };

    private sealed class StubHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> responder) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubMessageHandler(responder))
        {
            BaseAddress = new Uri("https://discord.com/api/v10/")
        };
    }

    private sealed class StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
