using BrrainzBot.Host;
using Spectre.Console;

namespace BrrainzBot.Cli;

internal static class SetupWizard
{
    public static (BotSettings Settings, RuntimeSecrets Secrets) Run(BotSettings? existingSettings, RuntimeSecrets? existingSecrets, AppPaths paths)
    {
        AnsiConsole.Write(new FigletText("Setup").Color(Color.CornflowerBlue));
        AnsiConsole.MarkupLine($"This wizard stores config in [grey]{Markup.Escape(paths.RootDirectory)}[/].");
        AnsiConsole.WriteLine();

        var friendlyName = AskText("What should this installation call itself?", existingSettings?.FriendlyName ?? "BrrainzBot");
        var repository = AskText("Which GitHub repository should self-update use?", existingSettings?.Updates.Repository ?? "ap/BrrainzBot");
        var discordToken = AskSecret("Discord bot token", existingSecrets?.DiscordToken);
        var aiBaseUrl = AskText("OpenAI-compatible base URL", existingSettings?.Ai.BaseUrl ?? "https://api.openai.com/v1");
        var aiModel = AskText("AI model name", existingSettings?.Ai.Model ?? "gpt-4.1-mini");
        var aiApiKey = AskSecret("AI API key", existingSecrets?.AiApiKey);
        var guildCount = AnsiConsole.Prompt(
            new TextPrompt<int>("How many Discord servers should this installation manage?")
                .DefaultValue(existingSettings?.Guilds.Count is > 0 ? existingSettings.Guilds.Count : 1)
                .Validate(value => value > 0 ? ValidationResult.Success() : ValidationResult.Error("[red]Enter at least one guild.[/]")));

        var guilds = new List<GuildSettings>();
        for (var index = 0; index < guildCount; index++)
        {
            var existingGuild = existingSettings?.Guilds.ElementAtOrDefault(index);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]Guild {index + 1} of {guildCount}[/]");
            guilds.Add(BuildGuildSettings(existingGuild));
        }

        var settings = new BotSettings
        {
            FriendlyName = friendlyName,
            GitHubRepository = repository,
            Updates = new UpdateSettings
            {
                Repository = repository,
                Enabled = true,
                Channel = "stable"
            },
            Ai = new AiProviderSettings
            {
                BaseUrl = aiBaseUrl,
                Model = aiModel,
                ProviderType = "OpenAiCompatible"
            },
            Guilds = guilds
        };

        var secrets = new RuntimeSecrets
        {
            DiscordToken = discordToken,
            AiApiKey = aiApiKey
        };

        return (settings, secrets);
    }

    private static GuildSettings BuildGuildSettings(GuildSettings? existing)
    {
        var name = AskText("A friendly label for this guild", existing?.Name ?? "My Discord Server");
        var guildTopicPrompt = AskText(
            "Describe who belongs in this server and what common confusion/spam should be filtered out",
            existing?.GuildTopicPrompt ?? "This server is for real humans who want to participate in the intended community. Filter out spam, scams, and obvious topic mismatches.");

        return new GuildSettings
        {
            Name = name,
            GuildId = AskUlong("Guild ID", existing?.GuildId),
            WelcomeChannelId = AskUlong("Welcome channel ID", existing?.WelcomeChannelId),
            NewRoleId = AskUlong("NEW role ID", existing?.NewRoleId),
            MemberRoleId = AskUlong("MEMBER role ID", existing?.MemberRoleId),
            OwnerUserId = AskUlong("Owner user ID for uncertain-case DMs", existing?.OwnerUserId),
            EnableOnboarding = AskConfirmation("Enable onboarding / jail for this guild?", existing?.EnableOnboarding ?? true),
            EnableSpamGuard = AskConfirmation("Enable spam guard for this guild?", existing?.EnableSpamGuard ?? true),
            GuildTopicPrompt = guildTopicPrompt,
            PublicReadOnlyChannelIds = [],
            Onboarding = new OnboardingSettings
            {
                WelcomeMessageTitle = AskText("Welcome title", existing?.Onboarding.WelcomeMessageTitle ?? "Welcome to the server"),
                WelcomeMessageBody = AskText("Welcome body", existing?.Onboarding.WelcomeMessageBody ?? "Click below to verify and unlock the full server."),
                StartButtonLabel = AskText("Start button label", existing?.Onboarding.StartButtonLabel ?? "Start verification"),
                RulesHint = AskText("Rules hint shown in the welcome panel", existing?.Onboarding.RulesHint ?? "Be kind, stay on topic, and do not spam."),
                MaxAttempts = AskInt("Maximum verification attempts", existing?.Onboarding.MaxAttempts ?? 3),
                Cooldown = TimeSpan.FromMinutes(AskInt("Cooldown after a failed attempt (minutes)", (int)(existing?.Onboarding.Cooldown.TotalMinutes ?? 10))),
                StaleTimeout = TimeSpan.FromHours(AskInt("Auto-kick NEW users after how many hours?", (int)(existing?.Onboarding.StaleTimeout.TotalHours ?? 24))),
                NotifyOwnerOnUncertain = true,
                NotifyOwnerOnTechnicalFailure = true,
                FirstQuestionLabel = AskText("First verification question", existing?.Onboarding.FirstQuestionLabel ?? "What brought you here?"),
                SecondQuestionLabel = AskText("Second verification question", existing?.Onboarding.SecondQuestionLabel ?? "What do you want to do here?"),
                ThirdQuestionLabel = AskText("Third verification question", existing?.Onboarding.ThirdQuestionLabel ?? "Paraphrase one expectation or rule.")
            },
            SpamGuard = new SpamGuardSettings
            {
                HoneypotChannelName = AskText("Spam honeypot channel name", existing?.SpamGuard.HoneypotChannelName ?? "intro"),
                PastMessageIntervalSeconds = AskInt("Delete how many seconds of past messages on trigger?", existing?.SpamGuard.PastMessageIntervalSeconds ?? 300),
                FutureMessageIntervalSeconds = AskInt("Continue deleting for how many seconds after trigger?", existing?.SpamGuard.FutureMessageIntervalSeconds ?? 300),
                MessageDeltaIntervalSeconds = AskInt("Duplicate detection window in seconds", existing?.SpamGuard.MessageDeltaIntervalSeconds ?? 120),
                MinimumMessageLength = AskInt("Minimum message length for duplicate tracking", existing?.SpamGuard.MinimumMessageLength ?? 40),
                LinkRequired = AskConfirmation("Require a link for duplicate tracking?", existing?.SpamGuard.LinkRequired ?? true),
                MessageSimilarityThreshold = AskDouble("Similarity threshold (0-1)", existing?.SpamGuard.MessageSimilarityThreshold ?? 0.85)
            }
        };
    }

    private static string AskText(string prompt, string defaultValue) => AnsiConsole.Prompt(
        new TextPrompt<string>(prompt)
            .DefaultValue(defaultValue)
            .AllowEmpty());

    private static string AskSecret(string prompt, string? existingValue)
    {
        var effectivePrompt = existingValue is null
            ? $"{prompt}:"
            : $"{prompt} [[leave blank to keep current]]:";
        var answer = AnsiConsole.Prompt(new TextPrompt<string>(effectivePrompt).Secret().AllowEmpty());
        return string.IsNullOrWhiteSpace(answer) && !string.IsNullOrWhiteSpace(existingValue) ? existingValue : answer;
    }

    private static ulong AskUlong(string prompt, ulong? defaultValue) => AnsiConsole.Prompt(
        new TextPrompt<ulong>($"{prompt}:")
            .DefaultValue(defaultValue ?? 0)
            .Validate(value => value > 0 ? ValidationResult.Success() : ValidationResult.Error("[red]Enter a Discord snowflake ID.[/]")));

    private static int AskInt(string prompt, int defaultValue) => AnsiConsole.Prompt(
        new TextPrompt<int>($"{prompt}:").DefaultValue(defaultValue));

    private static double AskDouble(string prompt, double defaultValue) => AnsiConsole.Prompt(
        new TextPrompt<double>($"{prompt}:")
            .DefaultValue(defaultValue)
            .Validate(value => value is >= 0 and <= 1 ? ValidationResult.Success() : ValidationResult.Error("[red]Enter a value between 0 and 1.[/]")));

    private static bool AskConfirmation(string prompt, bool defaultValue) => AnsiConsole.Confirm(prompt, defaultValue);
}
