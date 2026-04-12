using System.Text.Json;
using BrrainzBot.Host;

namespace BrrainzBot.Infrastructure;

public sealed class BotConfigurationStore
{
    public async Task<(BotSettings Settings, RuntimeSecrets Secrets)> LoadAsync(AppPaths paths, CancellationToken cancellationToken)
    {
        var settings = await LoadSettingsAsync(paths, cancellationToken);
        var secrets = await LoadSecretsAsync(paths, cancellationToken);
        return (settings, secrets);
    }

    public async Task SaveAsync(AppPaths paths, BotSettings settings, RuntimeSecrets secrets, CancellationToken cancellationToken)
    {
        paths.EnsureDirectoriesExist();
        await SaveSettingsAsync(paths, settings, cancellationToken);
        await SaveSecretsAsync(paths, secrets, cancellationToken);
        TryRestrictSecretsFile(paths.SecretsFilePath);
    }

    public bool Exists(AppPaths paths) => File.Exists(paths.ConfigFilePath) && File.Exists(paths.SecretsFilePath);

    public string ToDisplayJson(BotSettings settings) => JsonSerializer.Serialize(settings, JsonDefaults.Options);

    public async Task<BotSettings> LoadSettingsAsync(AppPaths paths, CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.ConfigFilePath))
            throw new FileNotFoundException($"Configuration file not found at {paths.ConfigFilePath}");

        var json = await File.ReadAllTextAsync(paths.ConfigFilePath, cancellationToken);
        var settings = JsonSerializer.Deserialize<BotSettings>(json, JsonDefaults.Options)
            ?? throw new InvalidOperationException("Failed to read bot settings.");

        if (settings.Servers.Count > 0 || !json.Contains("\"Guilds\"", StringComparison.Ordinal))
            return settings;

        var legacy = JsonSerializer.Deserialize<LegacyBotSettings>(json, JsonDefaults.Options)
            ?? throw new InvalidOperationException("Failed to read legacy bot settings.");
        return legacy.ToBotSettings();
    }

    public async Task<RuntimeSecrets> LoadSecretsAsync(AppPaths paths, CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.SecretsFilePath))
            throw new FileNotFoundException($"Secrets file not found at {paths.SecretsFilePath}");

        await using var secretsStream = File.OpenRead(paths.SecretsFilePath);
        return await JsonSerializer.DeserializeAsync<RuntimeSecrets>(secretsStream, JsonDefaults.Options, cancellationToken)
            ?? throw new InvalidOperationException("Failed to read runtime secrets.");
    }

    public async Task SaveSettingsAsync(AppPaths paths, BotSettings settings, CancellationToken cancellationToken)
    {
        paths.EnsureDirectoriesExist();
        await SaveFileAsync(paths.ConfigFilePath, settings, cancellationToken);
    }

    public async Task SaveSecretsAsync(AppPaths paths, RuntimeSecrets secrets, CancellationToken cancellationToken)
    {
        paths.EnsureDirectoriesExist();
        await SaveFileAsync(paths.SecretsFilePath, secrets, cancellationToken);
        TryRestrictSecretsFile(paths.SecretsFilePath);
    }

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

    private sealed class LegacyBotSettings
    {
        public string FriendlyName { get; init; } = "BrrainzBot";
        public string? GitHubRepository { get; init; }
        public UpdateSettings Updates { get; init; } = new();
        public AiProviderSettings Ai { get; init; } = new();
        public List<LegacyGuildSettings> Guilds { get; init; } = [];

        public BotSettings ToBotSettings() => new()
        {
            FriendlyName = FriendlyName,
            GitHubRepository = GitHubRepository,
            Updates = Updates,
            Ai = Ai,
            Servers = [.. Guilds.Select(guild => guild.ToServerSettings())]
        };
    }

    private sealed class LegacyGuildSettings
    {
        public string Name { get; init; } = "My Discord Server";
        public ulong GuildId { get; init; }
        public bool IsActive { get; init; }
        public ulong WelcomeChannelId { get; init; }
        public ulong NewRoleId { get; init; }
        public ulong MemberRoleId { get; init; }
        public ulong OwnerUserId { get; init; }
        public bool EnableOnboarding { get; init; } = true;
        public bool EnableSpamGuard { get; init; } = true;
        public string GuildTopicPrompt { get; init; } = string.Empty;
        public List<ulong> PublicReadOnlyChannelIds { get; init; } = [];
        public OnboardingSettings Onboarding { get; init; } = new();
        public SpamGuardSettings SpamGuard { get; init; } = new();

        public ServerSettings ToServerSettings() => new()
        {
            Name = Name,
            ServerId = GuildId,
            IsActive = IsActive,
            WelcomeChannelId = WelcomeChannelId,
            NewRoleId = NewRoleId,
            MemberRoleId = MemberRoleId,
            OwnerUserId = OwnerUserId,
            EnableOnboarding = EnableOnboarding,
            EnableSpamGuard = EnableSpamGuard,
            ServerTopicPrompt = GuildTopicPrompt,
            PublicReadOnlyChannelIds = [.. PublicReadOnlyChannelIds],
            Onboarding = Onboarding,
            SpamGuard = SpamGuard
        };
    }
}
