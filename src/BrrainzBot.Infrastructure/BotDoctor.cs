using System.Net.Http.Headers;
using System.Text.Json;
using BrrainzBot.Host;
using Discord;

namespace BrrainzBot.Infrastructure;

public sealed class BotDoctor(IHttpClientFactory httpClientFactory)
{
    private static readonly (string Name, Func<GuildPermissions, bool> HasPermission)[] RequiredBotPermissions =
    [
        ("View Channels", permissions => permissions.ViewChannel),
        ("Read Message History", permissions => permissions.ReadMessageHistory),
        ("Send Messages", permissions => permissions.SendMessages),
        ("Manage Messages", permissions => permissions.ManageMessages),
        ("Manage Roles", permissions => permissions.ManageRoles),
        ("Manage Channels", permissions => permissions.ManageChannels),
        ("Kick Members", permissions => permissions.KickMembers)
    ];

    public async Task<DiagnosticReport> RunAsync(BotSettings settings, RuntimeSecrets secrets, AppPaths paths, CancellationToken cancellationToken)
    {
        var report = new DiagnosticReport();
        ValidateLocal(settings, secrets, paths, report);
        var activeSessionsByServer = await LoadActiveSessionsByServerAsync(paths, cancellationToken);

        if (string.IsNullOrWhiteSpace(secrets.DiscordToken))
        {
            report.AddWarning("discord.token.missing", "The Discord token is missing, so remote Discord validation was skipped.");
            return report;
        }

        using var client = httpClientFactory.CreateClient(ServiceCollectionExtensions.DiscordProbeHttpClientName);
        client.BaseAddress = new Uri("https://discord.com/api/v10/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", secrets.DiscordToken);

        var botUserId = await ProbeBotUserIdAsync(client, report, cancellationToken);
        if (botUserId == null)
            return report;

        foreach (var server in settings.Servers)
        {
            await ValidateServerAsync(
                client,
                botUserId.Value,
                server,
                activeSessionsByServer.GetValueOrDefault(server.ServerId) ?? [],
                report,
                cancellationToken);
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

        if (settings.Servers.Count == 0)
            report.AddError("servers.empty", "At least one server must be configured.");

        foreach (var server in settings.Servers)
        {
            if (!server.IsActive)
                report.AddInfo("server.inactive", $"{server.Name}: this server is currently off. Use `brrainzbot enable {server.ServerId}` when you are ready.");
            if (server.ServerId == 0)
                report.AddError("server.id.zero", $"{server.Name}: ServerId must not be zero.");
            if (server.WelcomeChannelId == 0)
                report.AddError("server.welcome.zero", $"{server.Name}: WelcomeChannelId must not be zero.");
            if (server.MemberRoleId == 0)
                report.AddError("server.memberrole.zero", $"{server.Name}: MemberRoleId must not be zero.");
            if (server.MemberRoleId == server.ServerId && server.MemberRoleId != 0)
                report.AddError("server.memberrole.everyone.invalid", $"{server.Name}: MemberRoleId must point to a real MEMBER role. Run `brrainzbot create-member {server.ServerId}` and update setup.");
            if (server.EnableSpamGuard && server.SpamGuard.HoneypotChannelId == 0)
                report.AddError("server.honeypot.zero", $"{server.Name}: HoneypotChannelId must not be zero when spam cleanup is enabled.");
            if (server.OwnerUserId == 0)
                report.AddError("server.owner.zero", $"{server.Name}: OwnerUserId must not be zero.");
            if (server.Onboarding.MaxAttempts <= 0)
                report.AddError("server.maxattempts.invalid", $"{server.Name}: MaxAttempts must be greater than zero.");
        }
    }

    private static async Task<ulong?> ProbeBotUserIdAsync(HttpClient client, DiagnosticReport report, CancellationToken cancellationToken)
    {
        using var tokenProbe = await client.GetAsync("users/@me", cancellationToken);
        if (!tokenProbe.IsSuccessStatusCode)
        {
            report.AddError("discord.token.invalid", "Discord rejected the bot token. Remote validation could not continue.");
            return null;
        }

        var raw = await tokenProbe.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(raw);
        return document.RootElement.TryGetProperty("id", out var idElement) && ulong.TryParse(idElement.GetString(), out var botUserId)
            ? botUserId
            : null;
    }

    private static async Task ValidateServerAsync(
        HttpClient client,
        ulong botUserId,
        ServerSettings server,
        HashSet<ulong> activeSessionUserIds,
        DiagnosticReport report,
        CancellationToken cancellationToken)
    {
        if (server.ServerId == 0)
            return;

        using var serverResponse = await client.GetAsync($"guilds/{server.ServerId}", cancellationToken);
        if (!serverResponse.IsSuccessStatusCode)
        {
            report.AddError(
                "discord.server.unreachable",
                $"{server.Name}: the bot could not access the server with ID {server.ServerId} (HTTP {(int)serverResponse.StatusCode}). " +
                "This usually means the server ID is wrong, the bot has not been invited to that server, or it was removed.");
            return;
        }

        var channels = await LoadChannelsAsync(client, server, report, cancellationToken);
        var roles = await LoadRolesAsync(client, server, report, cancellationToken);

        if (channels != null)
        {
            if (!channels.TryGetValue(server.WelcomeChannelId, out var welcomeChannel))
            {
                report.AddError("discord.welcome.notfound", $"{server.Name}: WelcomeChannelId does not exist in the server.");
            }
            else
            {
                ValidateWelcomeLayout(server, welcomeChannel, report);
            }

            if (server.EnableSpamGuard && !channels.ContainsKey(server.SpamGuard.HoneypotChannelId))
                report.AddError("discord.honeypot.notfound", $"{server.Name}: HoneypotChannelId does not exist in the server.");
        }

        if (roles != null)
        {
            if (!roles.ContainsKey(server.MemberRoleId))
                report.AddError("discord.memberrole.notfound", $"{server.Name}: MemberRoleId does not exist in the server.");

            await ValidateRoleHierarchyAsync(client, botUserId, server, roles, report, cancellationToken);
        }

        await ValidateMemberBackfillAsync(client, server, activeSessionUserIds, report, cancellationToken);
    }

    private static async Task<Dictionary<ulong, ChannelSnapshot>?> LoadChannelsAsync(
        HttpClient client,
        ServerSettings server,
        DiagnosticReport report,
        CancellationToken cancellationToken)
    {
        using var channelResponse = await client.GetAsync($"guilds/{server.ServerId}/channels", cancellationToken);
        if (!channelResponse.IsSuccessStatusCode)
        {
            report.AddWarning("discord.channels.unreachable", $"{server.Name}: could not validate channels remotely.");
            return null;
        }

        var channelsJson = await channelResponse.Content.ReadAsStringAsync(cancellationToken);
        using var channelsDoc = JsonDocument.Parse(channelsJson);
        var channels = new Dictionary<ulong, ChannelSnapshot>();

        foreach (var channelElement in channelsDoc.RootElement.EnumerateArray())
        {
            if (!ulong.TryParse(channelElement.GetProperty("id").GetString(), out var channelId))
                continue;

            var overwrites = new List<PermissionOverwriteSnapshot>();
            if (channelElement.TryGetProperty("permission_overwrites", out var overwriteArray))
            {
                foreach (var overwriteElement in overwriteArray.EnumerateArray())
                {
                    if (!ulong.TryParse(overwriteElement.GetProperty("id").GetString(), out var overwriteId))
                        continue;

                    var type = overwriteElement.TryGetProperty("type", out var typeElement)
                        ? typeElement.ValueKind == JsonValueKind.String
                            ? int.Parse(typeElement.GetString() ?? "0")
                            : typeElement.GetInt32()
                        : 0;

                    var allow = ulong.Parse(overwriteElement.GetProperty("allow").GetString() ?? "0");
                    var deny = ulong.Parse(overwriteElement.GetProperty("deny").GetString() ?? "0");
                    overwrites.Add(new PermissionOverwriteSnapshot(overwriteId, type, allow, deny));
                }
            }

            channels[channelId] = new ChannelSnapshot(
                channelId,
                channelElement.GetProperty("name").GetString() ?? channelId.ToString(),
                overwrites);
        }

        return channels;
    }

    private static async Task<Dictionary<ulong, RoleSnapshot>?> LoadRolesAsync(
        HttpClient client,
        ServerSettings server,
        DiagnosticReport report,
        CancellationToken cancellationToken)
    {
        using var roleResponse = await client.GetAsync($"guilds/{server.ServerId}/roles", cancellationToken);
        if (!roleResponse.IsSuccessStatusCode)
        {
            report.AddWarning("discord.roles.unreachable", $"{server.Name}: could not validate roles remotely.");
            return null;
        }

        var rolesJson = await roleResponse.Content.ReadAsStringAsync(cancellationToken);
        using var rolesDoc = JsonDocument.Parse(rolesJson);
        return rolesDoc.RootElement.EnumerateArray()
            .Where(role => ulong.TryParse(role.GetProperty("id").GetString(), out _))
            .ToDictionary(
                role => ulong.Parse(role.GetProperty("id").GetString()!),
                role => new RoleSnapshot(
                    ulong.Parse(role.GetProperty("id").GetString()!),
                    role.GetProperty("name").GetString() ?? "unknown",
                    role.GetProperty("position").GetInt32(),
                    role.TryGetProperty("permissions", out var permissionsElement)
                        ? ulong.Parse(permissionsElement.GetString() ?? "0")
                        : 0));
    }

    private static void ValidateWelcomeLayout(ServerSettings server, ChannelSnapshot welcomeChannel, DiagnosticReport report)
    {
        var everyoneOverwrite = welcomeChannel.FindRoleOverwrite(server.ServerId);
        var memberOverwrite = welcomeChannel.FindRoleOverwrite(server.MemberRoleId);

        if (Denies(everyoneOverwrite, ChannelPermission.ViewChannel))
        {
            report.AddWarning(
                "discord.welcome.everyone.hidden",
                $"{server.Name}: #welcome should stay visible to people who do not have MEMBER yet. Remove the @everyone ViewChannel deny there.");
        }

        if (!Denies(memberOverwrite, ChannelPermission.ViewChannel))
        {
            report.AddWarning(
                "discord.welcome.member.visible",
                $"{server.Name}: #welcome should deny ViewChannel for MEMBER.");
        }
    }

    private static async Task ValidateRoleHierarchyAsync(
        HttpClient client,
        ulong botUserId,
        ServerSettings server,
        IReadOnlyDictionary<ulong, RoleSnapshot> roles,
        DiagnosticReport report,
        CancellationToken cancellationToken)
    {
        using var memberResponse = await client.GetAsync($"guilds/{server.ServerId}/members/{botUserId}", cancellationToken);
        if (!memberResponse.IsSuccessStatusCode)
        {
            report.AddWarning("discord.botmember.unreachable", $"{server.Name}: could not validate the bot role hierarchy remotely.");
            return;
        }

        var raw = await memberResponse.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(raw);

        if (!document.RootElement.TryGetProperty("roles", out var roleArray))
            return;

        var botRoleIds = roleArray.EnumerateArray()
            .Select(element => element.GetString())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => ulong.Parse(value!))
            .ToList();

        var botRoles = botRoleIds
            .Where(roles.ContainsKey)
            .Select(roleId => roles[roleId])
            .ToList();

        if (botRoles.Count == 0)
        {
            report.AddWarning("discord.botrole.unresolved", $"{server.Name}: could not resolve the bot roles in the server role list.");
            return;
        }

        var highestBotRolePosition = botRoles
            .Select(role => role.Position)
            .DefaultIfEmpty(int.MinValue)
            .Max();

        var botPermissions = new GuildPermissions(botRoles
            .Aggregate(0UL, (combined, role) => combined | role.PermissionsRawValue));

        var missingPermissions = RequiredBotPermissions
            .Where(requirement => !requirement.HasPermission(botPermissions))
            .Select(requirement => requirement.Name)
            .ToList();

        if (missingPermissions.Count > 0)
        {
            report.AddError(
                "discord.bot_permissions.missing",
                $"{server.Name}: the bot is missing required server permissions: {string.Join(", ", missingPermissions)}.");
        }

        if (roles.TryGetValue(server.ServerId, out var everyoneRole)
            && roles.TryGetValue(server.MemberRoleId, out var memberRoleForCopyCheck))
        {
            var uncopiableMissingPermissions = DescribeGuildPermissions(
                everyoneRole.PermissionsRawValue
                & ~botPermissions.RawValue
                & ~memberRoleForCopyCheck.PermissionsRawValue);
            if (uncopiableMissingPermissions.Count > 0)
            {
                report.AddWarning(
                    "discord.memberrole.partial_copy",
                    $"{server.Name}: `@everyone` has permissions the bot cannot grant to MEMBER with the current invite: {string.Join(", ", uncopiableMissingPermissions)}. `create-member` will copy the rest and you will need to set these manually if you want them on MEMBER.");
            }
        }

        if (roles.TryGetValue(server.MemberRoleId, out var memberRole)
            && highestBotRolePosition <= memberRole.Position)
        {
            report.AddError(
                "discord.role_hierarchy.invalid",
                $"{server.Name}: the bot role must be above MEMBER in the Discord role list.");
        }
    }

    private static async Task<Dictionary<ulong, HashSet<ulong>>> LoadActiveSessionsByServerAsync(AppPaths paths, CancellationToken cancellationToken)
    {
        var store = new JsonVerificationSessionStore(paths);
        var sessions = await store.ListAsync(cancellationToken);
        return sessions
            .GroupBy(session => session.ServerId)
            .ToDictionary(group => group.Key, group => group.Select(session => session.UserId).ToHashSet());
    }

    private static async Task ValidateMemberBackfillAsync(
        HttpClient client,
        ServerSettings server,
        HashSet<ulong> activeSessionUserIds,
        DiagnosticReport report,
        CancellationToken cancellationToken)
    {
        if (server.MemberRoleId == 0 || server.MemberRoleId == server.ServerId)
            return;

        try
        {
            var pendingBackfillCount = 0;
            ulong? afterUserId = null;

            while (true)
            {
                var path = afterUserId is { } after
                    ? $"guilds/{server.ServerId}/members?limit=1000&after={after}"
                    : $"guilds/{server.ServerId}/members?limit=1000";

                using var response = await client.GetAsync(path, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    report.AddWarning("discord.members.unreachable", $"{server.Name}: could not validate existing members remotely.");
                    return;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(json);
                var members = document.RootElement.EnumerateArray().ToArray();
                if (members.Length == 0)
                    break;

                foreach (var member in members)
                {
                    var userElement = member.GetProperty("user");
                    var userId = ulong.Parse(userElement.GetProperty("id").GetString()!);
                    var isBot = userElement.TryGetProperty("bot", out var botElement) && botElement.GetBoolean();
                    if (isBot)
                        continue;

                    var hasMemberRole = member.GetProperty("roles")
                        .EnumerateArray()
                        .Select(role => role.GetString())
                        .Any(roleId => ulong.TryParse(roleId, out var parsedRoleId) && parsedRoleId == server.MemberRoleId);
                    if (hasMemberRole || activeSessionUserIds.Contains(userId))
                        continue;

                    pendingBackfillCount++;
                }

                afterUserId = ulong.Parse(members[^1].GetProperty("user").GetProperty("id").GetString()!);
                if (members.Length < 1000)
                    break;
            }

            if (pendingBackfillCount > 0)
            {
                report.AddWarning(
                    "discord.memberrole.backfill_needed",
                    $"{server.Name}: {pendingBackfillCount} existing non-bot users still lack MEMBER and are not in active onboarding. Run `brrainzbot set-members {server.ServerId}` before going live.");
            }
        }
        catch (Exception)
        {
            report.AddWarning("discord.members.unreachable", $"{server.Name}: could not validate existing members remotely.");
        }
    }

    private static bool Denies(PermissionOverwriteSnapshot? overwrite, ChannelPermission permission) =>
        overwrite != null && (overwrite.Deny & PermissionBit(permission)) != 0;

    private static ulong PermissionBit(ChannelPermission permission) => (ulong)permission;

    private static IReadOnlyList<string> DescribeGuildPermissions(ulong rawPermissions) => Enum
        .GetValues<GuildPermission>()
        .Where(permission => permission != 0 && (rawPermissions & (ulong)permission) != 0)
        .Select(permission => FormatPermissionName(permission.ToString()))
        .ToList();

    private static string FormatPermissionName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;

        var parts = new List<string>();
        var current = new System.Text.StringBuilder();

        foreach (var character in name)
        {
            if (current.Length > 0 && char.IsUpper(character))
            {
                parts.Add(current.ToString());
                current.Clear();
            }

            current.Append(character);
        }

        if (current.Length > 0)
            parts.Add(current.ToString());

        return string.Join(' ', parts);
    }

    private sealed record ChannelSnapshot(ulong Id, string Name, List<PermissionOverwriteSnapshot> Overwrites)
    {
        public PermissionOverwriteSnapshot? FindRoleOverwrite(ulong roleId) =>
            Overwrites.FirstOrDefault(overwrite => overwrite.Type == 0 && overwrite.Id == roleId);
    }

    private sealed record PermissionOverwriteSnapshot(ulong Id, int Type, ulong Allow, ulong Deny);

    private sealed record RoleSnapshot(ulong Id, string Name, int Position, ulong PermissionsRawValue);
}
