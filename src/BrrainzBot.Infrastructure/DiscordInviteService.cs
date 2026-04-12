using System.Net.Http.Headers;
using System.Text.Json;
using BrrainzBot.Host;

namespace BrrainzBot.Infrastructure;

public sealed class DiscordInviteService(IHttpClientFactory httpClientFactory, RuntimeSecrets secrets)
{
    public const ulong RequiredBotPermissions = 268512274;

    public async Task<InviteLinkResult> CreateAsync(
        BotSettings? settings,
        ulong? serverId,
        ulong? clientId,
        CancellationToken cancellationToken)
    {
        var resolvedClientId = clientId ?? await ResolveClientIdAsync(cancellationToken);
        var resolvedServerId = ResolveServerId(settings, serverId);
        return new InviteLinkResult(resolvedClientId, resolvedServerId, BuildInviteUrl(resolvedClientId, resolvedServerId));
    }

    public static string BuildInviteUrl(ulong clientId, ulong? serverId)
    {
        var url = $"https://discord.com/oauth2/authorize?client_id={clientId}&permissions={RequiredBotPermissions}&integration_type=0&scope=bot";
        if (serverId is > 0)
            url += $"&guild_id={serverId.Value}&disable_guild_select=true";

        return url;
    }

    private async Task<ulong> ResolveClientIdAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(secrets.DiscordToken))
        {
            throw new InvalidOperationException(
                "No Discord token is available. Run `brrainzbot setup` first or pass `--client-id <appId>`.");
        }

        using var client = httpClientFactory.CreateClient(ServiceCollectionExtensions.DiscordProbeHttpClientName);
        client.BaseAddress = new Uri("https://discord.com/api/v10/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", secrets.DiscordToken);

        using var response = await client.GetAsync("oauth2/applications/@me", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Discord rejected the bot token while resolving the application ID (HTTP {(int)response.StatusCode}).");
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(raw);
        if (document.RootElement.TryGetProperty("id", out var idElement)
            && ulong.TryParse(idElement.GetString(), out var appId))
        {
            return appId;
        }

        throw new InvalidOperationException("Discord did not return a valid application ID.");
    }

    private static ulong? ResolveServerId(BotSettings? settings, ulong? serverId)
    {
        if (serverId is > 0)
            return serverId;

        return settings?.Servers.Count == 1 ? settings.Servers[0].ServerId : null;
    }
}

public sealed record InviteLinkResult(ulong ClientId, ulong? ServerId, string Url);
