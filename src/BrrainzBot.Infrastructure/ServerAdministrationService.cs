using BrrainzBot.Host;
using Discord;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Net;

namespace BrrainzBot.Infrastructure;

public sealed class ServerAdministrationService(RuntimeSecrets secrets, ILogger<ServerAdministrationService> logger)
{
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
                var memberRole = ResolveMemberRole(server, serverSettings);
                var createdRole = false;
                var previousRoleId = serverSettings.MemberRoleId;

                if (memberRole == null)
                {
                    var created = await server.CreateRoleAsync(
                        "MEMBER",
                        everyoneRole.Permissions,
                        color: null,
                        isHoisted: false,
                        isMentionable: false);
                    memberRole = server.GetRole(created.Id) ?? (IRole)created;
                    createdRole = true;
                }
                else
                {
                    await memberRole.ModifyAsync(properties =>
                    {
                        properties.Name = "MEMBER";
                        properties.Permissions = everyoneRole.Permissions;
                    });
                }

                var copiedOverwrites = 0;
                var removedOverwrites = 0;

                foreach (var channel in server.Channels.Where(channel => channel is not SocketThreadChannel))
                {
                    var everyoneOverwrite = channel.GetPermissionOverwrite(everyoneRole);
                    var memberOverwrite = channel.GetPermissionOverwrite(memberRole);

                    if (everyoneOverwrite is { } overwrite)
                    {
                        await channel.AddPermissionOverwriteAsync(memberRole, overwrite);
                        copiedOverwrites++;
                    }
                    else if (memberOverwrite.HasValue)
                    {
                        await channel.RemovePermissionOverwriteAsync(memberRole);
                        removedOverwrites++;
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
                    removedOverwrites);
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
        CancellationToken cancellationToken)
    {
        var serverSettings = ResolveServer(settings, requestedServerId);
        if (serverSettings.MemberRoleId == 0 || serverSettings.UsesEveryoneAsMemberState)
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

                await server.DownloadUsersAsync();

                var checkedMembers = 0;
                var botsSkipped = 0;
                var alreadyHadMember = 0;
                var newUsersSkipped = 0;
                var added = 0;
                var failed = 0;

                foreach (var member in server.Users.OrderBy(member => member.Id))
                {
                    if (member.IsBot)
                    {
                        botsSkipped++;
                        continue;
                    }

                    checkedMembers++;

                    if (member.Roles.Any(role => role.Id == memberRole.Id))
                    {
                        alreadyHadMember++;
                        continue;
                    }

                    if (serverSettings.NewRoleId != 0 && member.Roles.Any(role => role.Id == serverSettings.NewRoleId))
                    {
                        newUsersSkipped++;
                        continue;
                    }

                    try
                    {
                        await member.AddRoleAsync(memberRole);
                        added++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        logger.LogWarning(ex, "Failed to add MEMBER to user {UserId} in server {ServerId}", member.Id, server.Id);
                    }
                }

                return new SetMembersResult(
                    serverSettings.Name,
                    serverSettings.ServerId,
                    memberRole.Id,
                    checkedMembers,
                    botsSkipped,
                    alreadyHadMember,
                    newUsersSkipped,
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
        if (serverSettings.MemberRoleId != 0 && serverSettings.MemberRoleId != serverSettings.ServerId)
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
        $"Discord returned Missing Permissions during `{commandName}`. The usual causes are: the bot role is below NEW or MEMBER, the bot is missing Manage Roles or Manage Channels, or the server has server-wide 2FA enabled and the bot owner account does not have 2FA enabled.";

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
    int RemovedChannelOverrides);

public sealed record SetMembersResult(
    string ServerName,
    ulong ServerId,
    ulong MemberRoleId,
    int CheckedMembers,
    int BotsSkipped,
    int AlreadyHadMember,
    int NewUsersSkipped,
    int Added,
    int Failed);
