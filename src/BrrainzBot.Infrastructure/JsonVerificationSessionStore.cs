using System.Text.Json;
using BrrainzBot.Host;

namespace BrrainzBot.Infrastructure;

public sealed class JsonVerificationSessionStore(AppPaths paths) : IVerificationSessionStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<VerificationSession?> GetAsync(ulong guildId, ulong userId, CancellationToken cancellationToken)
    {
        var sessions = await LoadAllAsync(cancellationToken);
        return sessions.FirstOrDefault(s => s.GuildId == guildId && s.UserId == userId);
    }

    public async Task<IReadOnlyList<VerificationSession>> ListAsync(CancellationToken cancellationToken) => await LoadAllAsync(cancellationToken);

    public async Task UpsertAsync(VerificationSession session, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var sessions = await LoadAllInternalAsync(cancellationToken);
            var index = sessions.FindIndex(s => s.GuildId == session.GuildId && s.UserId == session.UserId);
            if (index >= 0)
                sessions[index] = session;
            else
                sessions.Add(session);

            await SaveAllInternalAsync(sessions, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAsync(ulong guildId, ulong userId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var sessions = await LoadAllInternalAsync(cancellationToken);
            _ = sessions.RemoveAll(s => s.GuildId == guildId && s.UserId == userId);
            await SaveAllInternalAsync(sessions, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<VerificationSession>> LoadAllAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await LoadAllInternalAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<VerificationSession>> LoadAllInternalAsync(CancellationToken cancellationToken)
    {
        paths.EnsureDirectoriesExist();
        if (!File.Exists(paths.SessionStateFilePath))
            return [];

        await using var stream = File.OpenRead(paths.SessionStateFilePath);
        return await JsonSerializer.DeserializeAsync<List<VerificationSession>>(stream, JsonDefaults.Options, cancellationToken) ?? [];
    }

    private async Task SaveAllInternalAsync(List<VerificationSession> sessions, CancellationToken cancellationToken)
    {
        paths.EnsureDirectoriesExist();
        var temporaryPath = $"{paths.SessionStateFilePath}.tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, sessions, JsonDefaults.Options, cancellationToken);
        }

        File.Move(temporaryPath, paths.SessionStateFilePath, true);
    }
}
