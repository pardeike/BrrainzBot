using BrrainzBot.Host;
using Discord;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Net;

namespace BrrainzBot.Infrastructure;

public sealed class ServerAdministrationService(
    RuntimeSecrets secrets,
    IVerificationSessionStore sessionStore,
    ILogger<ServerAdministrationService> logger)
{
    private const int SetMembersParallelism = 8;
    private const ulong ViewChannelPermissionBit = (ulong)ChannelPermission.ViewChannel;

    public async Task<CreateMemberRoleResult> CreateMemberRoleAsync(
        BotSettings settings,
        ulong? requestedServerId,
        CancellationToken cancellationToken)
    {
        var serverSettings = ResolveServer(settings, requestedServerId);

        try
        {
            return await WithConnectedClientAsync(async client =>
            {
                var server = client.GetGuild(serverSettings.ServerId)
                    ?? throw new InvalidOperationException($"The bot cannot access server {serverSettings.ServerId}. Check the invite and the server ID.");

                EnsurePermission(server.CurrentUser.GuildPermissions.ManageRoles, "Manage Roles");
                EnsurePermission(server.CurrentUser.GuildPermissions.ManageChannels, "Manage Channels");

                var everyoneRole = server.EveryoneRole;
                var grantablePermissionsRaw = server.CurrentUser.GuildPermissions.RawValue;
                var copyablePermissionsRaw = everyoneRole.Permissions.RawValue & server.CurrentUser.GuildPermissions.RawValue;
                var skippedPermissions = DescribeGuildPermissions(everyoneRole.Permissions.RawValue & ~server.CurrentUser.GuildPermissions.RawValue);
                var memberRole = ResolveMemberRole(server, serverSettings);
                var createdRole = false;
                var previousRoleId = serverSettings.MemberRoleId;

                if (memberRole == null)
                {
                    var created = await server.CreateRoleAsync(
                        "MEMBER",
                        new GuildPermissions(copyablePermissionsRaw),
                        color: null,
                        isHoisted: false,
                        isMentionable: false);
                    memberRole = server.GetRole(created.Id) ?? (IRole)created;
                    createdRole = true;
                }
                else
                {
                    var currentPermissionsRaw = memberRole.Permissions.RawValue;
                    var targetPermissionsRaw = (currentPermissionsRaw & ~grantablePermissionsRaw) | copyablePermissionsRaw;
                    var needsRename = !string.Equals(memberRole.Name, "MEMBER", StringComparison.Ordinal);
                    var needsPermissionSync = currentPermissionsRaw != targetPermissionsRaw;

                    if (needsRename || needsPermissionSync)
                    {
                        await memberRole.ModifyAsync(properties =>
                        {
                            properties.Name = "MEMBER";
                            properties.Permissions = new GuildPermissions(targetPermissionsRaw);
                        });
                    }
                }

                var copiedOverwrites = 0;
                var removedOverwrites = 0;
                var skippedChannels = new List<string>();

                foreach (var channel in server.Channels.Where(channel => channel is not SocketThreadChannel))
                {
                    var everyoneOverwrite = channel.GetPermissionOverwrite(everyoneRole);
                    var memberOverwrite = channel.GetPermissionOverwrite(memberRole);
                    var currentAllow = memberOverwrite?.AllowValue ?? 0;
                    var currentDeny = memberOverwrite?.DenyValue ?? 0;
                    var targetAllow = currentAllow;
                    var targetDeny = currentDeny;
                    var hasTargetOverwrite = false;
                    var removeWhenEmpty = false;
                    var isWelcomeChannel = channel.Id == serverSettings.WelcomeChannelId;

                    if (everyoneOverwrite is { } overwrite)
                    {
                        var preservedAllow = currentAllow & ~grantablePermissionsRaw;
                        var preservedDeny = currentDeny & ~grantablePermissionsRaw;
                        targetAllow = preservedAllow | (overwrite.AllowValue & grantablePermissionsRaw);
                        targetDeny = preservedDeny | (overwrite.DenyValue & grantablePermissionsRaw);
                        hasTargetOverwrite = true;
                        removeWhenEmpty = false;
                    }
                    else if (memberOverwrite.HasValue)
                    {
                        targetAllow = currentAllow & ~grantablePermissionsRaw;
                        targetDeny = currentDeny & ~grantablePermissionsRaw;
                        hasTargetOverwrite = true;
                        removeWhenEmpty = true;
                    }

                    if (isWelcomeChannel)
                    {
                        targetAllow &= ~ViewChannelPermissionBit;
                        targetDeny |= ViewChannelPermissionBit;
                        hasTargetOverwrite = true;
                        removeWhenEmpty = false;
                    }

                    if (!hasTargetOverwrite)
                        continue;

                    try
                    {
                        if (removeWhenEmpty && targetAllow == 0 && targetDeny == 0)
                        {
                            await channel.RemovePermissionOverwriteAsync(memberRole);
                            removedOverwrites++;
                        }
                        else if (currentAllow != targetAllow || currentDeny != targetDeny)
                        {
                            await channel.AddPermissionOverwriteAsync(memberRole, new OverwritePermissions(targetAllow, targetDeny));
                            copiedOverwrites++;
                        }
                    }
                    catch (HttpException ex) when (IsMissingPermissions(ex))
                    {
                        skippedChannels.Add(channel.Name);
                        logger.LogWarning(
                            ex,
                            "Skipping MEMBER overwrite sync for channel {ChannelName} in server {ServerId}",
                            channel.Name,
                            server.Id);
                    }
                }

                return new CreateMemberRoleResult(
                    serverSettings.Name,
                    serverSettings.ServerId,
                    previousRoleId,
                    memberRole.Id,
                    createdRole,
                    previousRoleId != memberRole.Id,
                    copiedOverwrites,
                    removedOverwrites,
                    skippedPermissions,
                    skippedChannels);
            }, cancellationToken);
        }
        catch (HttpException ex) when (IsMissingPermissions(ex))
        {
            throw new InvalidOperationException(BuildMissingPermissionsHint("create-member"), ex);
        }
    }

    public async Task<SetMembersResult> SetMembersAsync(
        BotSettings settings,
        ulong? requestedServerId,
        IProgress<SetMembersProgress>? progress,
        CancellationToken cancellationToken)
    {
        var serverSettings = ResolveServer(settings, requestedServerId);
        if (serverSettings.MemberRoleId == 0 || serverSettings.MemberRoleId == serverSettings.ServerId)
            throw new InvalidOperationException("This server does not have a real MEMBER role configured. Run `brrainzbot create-member` first.");

        try
        {
            return await WithConnectedClientAsync(async client =>
            {
                var server = client.GetGuild(serverSettings.ServerId)
                    ?? throw new InvalidOperationException($"The bot cannot access server {serverSettings.ServerId}. Check the invite and the server ID.");

                EnsurePermission(server.CurrentUser.GuildPermissions.ManageRoles, "Manage Roles");

                var memberRole = server.GetRole(serverSettings.MemberRoleId)
                    ?? throw new InvalidOperationException("The configured MEMBER role does not exist on the server. Run `brrainzbot create-member` first.");
                var now = DateTimeOffset.UtcNow;
                var activeOnboardingUserIds = (await sessionStore.ListAsync(cancellationToken))
                    .Where(session => session.ExpiresAt > now)
                    .Where(session => session.ServerId == serverSettings.ServerId)
                    .Select(session => session.UserId)
                    .ToHashSet();

                progress?.Report(new SetMembersProgress(
                    Phase: "Downloading members",
                    TotalUsers: 0,
                    ProcessedUsers: 0,
                    CheckedMembers: 0,
                    BotsSkipped: 0,
                    AlreadyHadMember: 0,
                    ActiveOnboardingSkipped: 0,
                    Added: 0,
                    Failed: 0));

                await server.DownloadUsersAsync();
                var totalUsers = server.Users.Count;

                var checkedMembers = 0;
                var botsSkipped = 0;
                var alreadyHadMember = 0;
                var activeOnboardingSkipped = 0;
                var added = 0;
                var failed = 0;
                var processedUsers = 0;

                progress?.Report(new SetMembersProgress(
                    Phase: "Assigning MEMBER",
                    TotalUsers: totalUsers,
                    ProcessedUsers: 0,
                    CheckedMembers: 0,
                    BotsSkipped: 0,
                    AlreadyHadMember: 0,
                    ActiveOnboardingSkipped: 0,
                    Added: 0,
                    Failed: 0));

                var members = server.Users.OrderBy(member => member.Id).ToArray();

                await Parallel.ForEachAsync(
                    members,
                    new ParallelOptions
                    {
                        CancellationToken = cancellationToken,
                        MaxDegreeOfParallelism = SetMembersParallelism
                    },
                    async (member, _) =>
                    {
                        if (member.IsBot)
                        {
                            Interlocked.Increment(ref botsSkipped);
                        }
                        else
                        {
                            Interlocked.Increment(ref checkedMembers);

                            if (member.Roles.Any(role => role.Id == memberRole.Id))
                            {
                                Interlocked.Increment(ref alreadyHadMember);
                            }
                            else if (activeOnboardingUserIds.Contains(member.Id))
                            {
                                Interlocked.Increment(ref activeOnboardingSkipped);
                            }
                            else
                            {
                                try
                                {
                                    await member.AddRoleAsync(memberRole);
                                    Interlocked.Increment(ref added);
                                }
                                catch (Exception ex)
                                {
                                    Interlocked.Increment(ref failed);
                                    logger.LogWarning(ex, "Failed to add MEMBER to user {UserId} in server {ServerId}", member.Id, server.Id);
                                }
                            }
                        }

                        var processed = Interlocked.Increment(ref processedUsers);
                        if (processed == totalUsers || processed % 100 == 0)
                        {
                            progress?.Report(new SetMembersProgress(
                                Phase: "Assigning MEMBER",
                                TotalUsers: totalUsers,
                                ProcessedUsers: processed,
                                CheckedMembers: Volatile.Read(ref checkedMembers),
                                BotsSkipped: Volatile.Read(ref botsSkipped),
                                AlreadyHadMember: Volatile.Read(ref alreadyHadMember),
                                ActiveOnboardingSkipped: Volatile.Read(ref activeOnboardingSkipped),
                                Added: Volatile.Read(ref added),
                                Failed: Volatile.Read(ref failed)));
                        }
                    });

                return new SetMembersResult(
                    serverSettings.Name,
                    serverSettings.ServerId,
                    memberRole.Id,
                    checkedMembers,
                    botsSkipped,
                    alreadyHadMember,
                    activeOnboardingSkipped,
                    added,
                    failed);
            }, cancellationToken);
        }
        catch (HttpException ex) when (IsMissingPermissions(ex))
        {
            throw new InvalidOperationException(BuildMissingPermissionsHint("set-members"), ex);
        }
    }

    private static ServerSettings ResolveServer(BotSettings settings, ulong? requestedServerId)
    {
        if (requestedServerId is { } serverId)
        {
            return settings.FindServer(serverId)
                ?? throw new InvalidOperationException($"Server {serverId} is not in the current config.");
        }

        return settings.Servers.Count switch
        {
            0 => throw new InvalidOperationException("No servers are configured yet."),
            1 => settings.Servers[0],
            _ => throw new InvalidOperationException("More than one server is configured. Pass an explicit server ID.")
        };
    }

    private static IRole? ResolveMemberRole(SocketGuild server, ServerSettings serverSettings)
    {
        if (serverSettings.MemberRoleId != 0 && serverSettings.MemberRoleId != server.Id)
        {
            var configuredRole = server.GetRole(serverSettings.MemberRoleId);
            if (configuredRole != null)
                return configuredRole;
        }

        return server.Roles.FirstOrDefault(role => string.Equals(role.Name, "MEMBER", StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsurePermission(bool allowed, string permissionName)
    {
        if (!allowed)
            throw new InvalidOperationException($"The bot needs `{permissionName}` on this server for this command.");
    }

    private static bool IsMissingPermissions(HttpException ex) =>
        ex.HttpCode == HttpStatusCode.Forbidden || ex.DiscordCode == DiscordErrorCode.MissingPermissions;

    private static string BuildMissingPermissionsHint(string commandName) =>
        $"Discord returned Missing Permissions during `{commandName}`. The usual causes are: the bot role is below MEMBER, the bot is missing Manage Roles or Manage Channels, or the server has server-wide 2FA enabled and the bot owner account does not have 2FA enabled.";

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

    private async Task<TResult> WithConnectedClientAsync<TResult>(
        Func<DiscordSocketClient, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers
        });

        Task OnReadyAsync()
        {
            ready.TrySetResult();
            return Task.CompletedTask;
        }

        client.Ready += OnReadyAsync;

        try
        {
            await client.LoginAsync(TokenType.Bot, secrets.DiscordToken);
            await client.StartAsync();
            await ready.Task.WaitAsync(cancellationToken);
            return await action(client);
        }
        finally
        {
            client.Ready -= OnReadyAsync;

            try
            {
                await client.StopAsync();
            }
            catch
            {
            }

            try
            {
                await client.LogoutAsync();
            }
            catch
            {
            }
        }
    }
}

public sealed record CreateMemberRoleResult(
    string ServerName,
    ulong ServerId,
    ulong PreviousMemberRoleId,
    ulong MemberRoleId,
    bool CreatedRole,
    bool UpdatedConfig,
    int CopiedChannelOverrides,
    int RemovedChannelOverrides,
    IReadOnlyList<string> SkippedServerPermissions,
    IReadOnlyList<string> SkippedChannels);

public sealed record SetMembersResult(
    string ServerName,
    ulong ServerId,
    ulong MemberRoleId,
    int CheckedMembers,
    int BotsSkipped,
    int AlreadyHadMember,
    int ActiveOnboardingSkipped,
    int Added,
    int Failed);

public sealed record SetMembersProgress(
    string Phase,
    int TotalUsers,
    int ProcessedUsers,
    int CheckedMembers,
    int BotsSkipped,
    int AlreadyHadMember,
    int ActiveOnboardingSkipped,
    int Added,
    int Failed);
