using System.Text.Json;
using BrrainzBot.Host;

namespace BrrainzBot.Infrastructure;

public sealed class BotConfigurationStore
{
    public async Task<(BotSettings Settings, RuntimeSecrets Secrets)> LoadAsync(AppPaths paths, CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.ConfigFilePath))
            throw new FileNotFoundException($"Configuration file not found at {paths.ConfigFilePath}");

        if (!File.Exists(paths.SecretsFilePath))
            throw new FileNotFoundException($"Secrets file not found at {paths.SecretsFilePath}");

        await using var configStream = File.OpenRead(paths.ConfigFilePath);
        var settings = await JsonSerializer.DeserializeAsync<BotSettings>(configStream, JsonDefaults.Options, cancellationToken)
            ?? throw new InvalidOperationException("Failed to read bot settings.");

        await using var secretsStream = File.OpenRead(paths.SecretsFilePath);
        var secrets = await JsonSerializer.DeserializeAsync<RuntimeSecrets>(secretsStream, JsonDefaults.Options, cancellationToken)
            ?? throw new InvalidOperationException("Failed to read runtime secrets.");

        return (settings, secrets);
    }

    public async Task SaveAsync(AppPaths paths, BotSettings settings, RuntimeSecrets secrets, CancellationToken cancellationToken)
    {
        paths.EnsureDirectoriesExist();
        await SaveFileAsync(paths.ConfigFilePath, settings, cancellationToken);
        await SaveFileAsync(paths.SecretsFilePath, secrets, cancellationToken);
        TryRestrictSecretsFile(paths.SecretsFilePath);
    }

    public bool Exists(AppPaths paths) => File.Exists(paths.ConfigFilePath) && File.Exists(paths.SecretsFilePath);

    public string ToDisplayJson(BotSettings settings) => JsonSerializer.Serialize(settings, JsonDefaults.Options);

    private static async Task SaveFileAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var temporaryPath = $"{path}.tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, value, JsonDefaults.Options, cancellationToken);
        }

        File.Move(temporaryPath, path, true);
    }

    private static void TryRestrictSecretsFile(string path)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        catch
        {
            // File mode hardening is best effort. The doctor command still warns if secrets look unsafe.
        }
    }
}
