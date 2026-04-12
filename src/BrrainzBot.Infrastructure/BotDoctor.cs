using System.Net.Http.Headers;
using System.Text.Json;
using BrrainzBot.Host;

namespace BrrainzBot.Infrastructure;

public sealed class BotDoctor(IHttpClientFactory httpClientFactory)
{
    public async Task<DiagnosticReport> RunAsync(BotSettings settings, RuntimeSecrets secrets, AppPaths paths, CancellationToken cancellationToken)
    {
        var report = new DiagnosticReport();
        ValidateLocal(settings, secrets, paths, report);

        if (string.IsNullOrWhiteSpace(secrets.DiscordToken))
        {
            report.AddWarning("discord.token.missing", "The Discord token is missing, so remote Discord validation was skipped.");
            return report;
        }

        using var client = httpClientFactory.CreateClient(ServiceCollectionExtensions.DiscordProbeHttpClientName);
        client.BaseAddress = new Uri("https://discord.com/api/v10/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", secrets.DiscordToken);

        var tokenProbe = await client.GetAsync("users/@me", cancellationToken);
        if (!tokenProbe.IsSuccessStatusCode)
        {
            report.AddError("discord.token.invalid", "Discord rejected the bot token. Remote validation could not continue.");
            return report;
        }

        foreach (var guild in settings.Guilds)
        {
            await ValidateGuildAsync(client, guild, report, cancellationToken);
        }

        return report;
    }

    private static void ValidateLocal(BotSettings settings, RuntimeSecrets secrets, AppPaths paths, DiagnosticReport report)
    {
        if (!File.Exists(paths.ConfigFilePath))
            report.AddError("config.missing", $"Configuration file is missing at {paths.ConfigFilePath}.");

        if (!File.Exists(paths.SecretsFilePath))
            report.AddError("secrets.missing", $"Secrets file is missing at {paths.SecretsFilePath}.");

        if (string.IsNullOrWhiteSpace(secrets.DiscordToken))
            report.AddError("discord.token.empty", "Discord token is empty.");

        if (string.IsNullOrWhiteSpace(secrets.AiApiKey))
            report.AddWarning("ai.key.empty", "AI API key is empty.");

        if (settings.Guilds.Count == 0)
            report.AddError("guilds.empty", "At least one guild must be configured.");

        foreach (var guild in settings.Guilds)
        {
            if (guild.GuildId == 0)
                report.AddError("guild.id.zero", $"{guild.Name}: GuildId must not be zero.");
            if (guild.WelcomeChannelId == 0)
                report.AddError("guild.welcome.zero", $"{guild.Name}: WelcomeChannelId must not be zero.");
            if (guild.NewRoleId == 0)
                report.AddError("guild.newrole.zero", $"{guild.Name}: NewRoleId must not be zero.");
            if (guild.MemberRoleId == 0)
                report.AddError("guild.memberrole.zero", $"{guild.Name}: MemberRoleId must not be zero.");
            if (guild.EnableSpamGuard && guild.SpamGuard.HoneypotChannelId == 0)
                report.AddError("guild.honeypot.zero", $"{guild.Name}: HoneypotChannelId must not be zero when SpamGuard is enabled.");
            if (guild.MemberRoleId == guild.NewRoleId && guild.MemberRoleId != 0)
                report.AddError("guild.roles.same", $"{guild.Name}: MemberRoleId and NewRoleId must not be the same.");
            if (guild.OwnerUserId == 0)
                report.AddError("guild.owner.zero", $"{guild.Name}: OwnerUserId must not be zero.");
            if (guild.Onboarding.MaxAttempts <= 0)
                report.AddError("guild.maxattempts.invalid", $"{guild.Name}: MaxAttempts must be greater than zero.");
            if (guild.UsesEveryoneAsMemberState)
            {
                report.AddInfo(
                    "guild.memberrole.everyone",
                    $"{guild.Name}: MemberRoleId matches the guild ID, so approval will only remove NEW and rely on @everyone as the member state.");
            }
        }
    }

    private static async Task ValidateGuildAsync(HttpClient client, GuildSettings guild, DiagnosticReport report, CancellationToken cancellationToken)
    {
        using var guildResponse = await client.GetAsync($"guilds/{guild.GuildId}", cancellationToken);
        if (!guildResponse.IsSuccessStatusCode)
        {
            report.AddError("discord.guild.unreachable", $"{guild.Name}: the bot could not access the guild with ID {guild.GuildId}.");
            return;
        }

        using var channelResponse = await client.GetAsync($"guilds/{guild.GuildId}/channels", cancellationToken);
        if (!channelResponse.IsSuccessStatusCode)
        {
            report.AddWarning("discord.channels.unreachable", $"{guild.Name}: could not validate channels remotely.");
        }
        else
        {
            var channelsJson = await channelResponse.Content.ReadAsStringAsync(cancellationToken);
            using var channelsDoc = JsonDocument.Parse(channelsJson);
            var channelIds = channelsDoc.RootElement.EnumerateArray().Select(c => c.GetProperty("id").GetString()).ToHashSet();
            if (!channelIds.Contains(guild.WelcomeChannelId.ToString()))
                report.AddError("discord.welcome.notfound", $"{guild.Name}: WelcomeChannelId does not exist in the guild.");
            if (guild.EnableSpamGuard && !channelIds.Contains(guild.SpamGuard.HoneypotChannelId.ToString()))
                report.AddError("discord.honeypot.notfound", $"{guild.Name}: HoneypotChannelId does not exist in the guild.");
        }

        using var roleResponse = await client.GetAsync($"guilds/{guild.GuildId}/roles", cancellationToken);
        if (!roleResponse.IsSuccessStatusCode)
        {
            report.AddWarning("discord.roles.unreachable", $"{guild.Name}: could not validate roles remotely.");
            return;
        }

        var rolesJson = await roleResponse.Content.ReadAsStringAsync(cancellationToken);
        using var rolesDoc = JsonDocument.Parse(rolesJson);
        var roleIds = rolesDoc.RootElement.EnumerateArray().Select(c => c.GetProperty("id").GetString()).ToHashSet();
        if (!roleIds.Contains(guild.NewRoleId.ToString()))
            report.AddError("discord.newrole.notfound", $"{guild.Name}: NewRoleId does not exist in the guild.");
        if (!roleIds.Contains(guild.MemberRoleId.ToString()))
            report.AddError("discord.memberrole.notfound", $"{guild.Name}: MemberRoleId does not exist in the guild.");
    }
}
