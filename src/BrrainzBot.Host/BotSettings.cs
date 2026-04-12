using System.Text.Json.Serialization;

namespace BrrainzBot.Host;

public sealed class BotSettings
{
    public string FriendlyName { get; init; } = "BrrainzBot";
    public string? GitHubRepository { get; init; } = "ap/BrrainzBot";
    public UpdateSettings Updates { get; init; } = new();
    public AiProviderSettings Ai { get; init; } = new();
    public List<GuildSettings> Guilds { get; init; } = [];

    public GuildSettings? FindGuild(ulong guildId) => Guilds.FirstOrDefault(g => g.GuildId == guildId);
}

public sealed class UpdateSettings
{
    public string Repository { get; init; } = "ap/BrrainzBot";
    public string Channel { get; init; } = "stable";
    public bool Enabled { get; init; } = true;
}

public sealed class AiProviderSettings
{
    public string ProviderType { get; init; } = "OpenAiCompatible";
    public string BaseUrl { get; init; } = "https://api.openai.com/v1";
    public string Model { get; init; } = "gpt-4.1-mini";
    public bool RequireHttps { get; init; } = true;
    public bool AllowInsecureLocalEndpoint { get; init; }
    public string ApiKeyEnvironmentVariable { get; init; } = "BRRAINZBOT_OPENAI_API_KEY";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}

public sealed class GuildSettings
{
    public string Name { get; init; } = "My Discord Server";
    public ulong GuildId { get; init; }
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

    [JsonIgnore]
    public bool UsesEveryoneAsMemberState => GuildId != 0 && MemberRoleId == GuildId;
}

public sealed class OnboardingSettings
{
    public string WelcomeMessageTitle { get; init; } = "Welcome to the server";
    public string WelcomeMessageBody { get; init; } =
        "Click the button below to verify. The bot will ask a few quick questions so the server can stay human and on-topic.";
    public string StartButtonLabel { get; init; } = "Start verification";
    public string RulesHint { get; init; } = "Be kind, stay on topic, and do not spam.";
    public int MaxAttempts { get; init; } = 3;
    public TimeSpan Cooldown { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan StaleTimeout { get; init; } = TimeSpan.FromHours(24);
    public bool NotifyOwnerOnUncertain { get; init; } = true;
    public bool NotifyOwnerOnTechnicalFailure { get; init; } = true;
    public string FirstQuestionLabel { get; init; } = "What brought you here?";
    public string SecondQuestionLabel { get; init; } = "What do you want to do here?";
    public string ThirdQuestionLabel { get; init; } = "Paraphrase one expectation or rule.";
}

public sealed class SpamGuardSettings
{
    public ulong HoneypotChannelId { get; init; }
    public int PastMessageIntervalSeconds { get; init; } = 300;
    public int FutureMessageIntervalSeconds { get; init; } = 300;
    public int MessageDeltaIntervalSeconds { get; init; } = 120;
    public int MinimumMessageLength { get; init; } = 40;
    public bool LinkRequired { get; init; } = true;
    public double MessageSimilarityThreshold { get; init; } = 0.85;
}
