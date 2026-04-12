namespace BrrainzBot.Host;

public interface IDiscordModule
{
    string Name { get; }
    Task RegisterAsync(CancellationToken cancellationToken);
}

public interface IBotSettingsProvider
{
    BotSettings Current { get; }
    event Action<BotSettings>? Changed;
}

public interface IAuditLog
{
    Task WriteAsync(string eventName, object payload, CancellationToken cancellationToken);
}

public interface IAiProviderClient
{
    Task<VerificationDecision> EvaluateAsync(VerificationPrompt prompt, CancellationToken cancellationToken);
}

public interface IVerificationSessionStore
{
    Task<VerificationSession?> GetAsync(ulong guildId, ulong userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<VerificationSession>> ListAsync(CancellationToken cancellationToken);
    Task UpsertAsync(VerificationSession session, CancellationToken cancellationToken);
    Task RemoveAsync(ulong guildId, ulong userId, CancellationToken cancellationToken);
}
