using System.Text.Json;
using BrrainzBot.Host;

namespace BrrainzBot.Infrastructure;

public sealed class JsonAuditLog(AppPaths paths) : IAuditLog
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task WriteAsync(string eventName, object payload, CancellationToken cancellationToken)
    {
        paths.EnsureDirectoriesExist();
        var logPath = Path.Combine(paths.LogsDirectory, $"{DateTime.UtcNow:yyyy-MM-dd}.jsonl");
        var envelope = new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            eventName,
            payload
        };

        var line = JsonSerializer.Serialize(envelope, JsonDefaults.Options);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(logPath, line + Environment.NewLine, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}
