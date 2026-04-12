using BrrainzBot.Host;
using Spectre.Console;

namespace BrrainzBot.Cli;

internal static class SetupWizard
{
    private const string DocsHomeUrl = "https://bot.brrai.nz/";
    private const string DiscordSetupUrl = "https://bot.brrai.nz/discord-setup/";
    private const string OpenAiGuideUrl = "https://bot.brrai.nz/openai-compatible/";

    public static (BotSettings Settings, RuntimeSecrets Secrets) Run(BotSettings? existingSettings, RuntimeSecrets? existingSecrets, AppPaths paths)
    {
        AnsiConsole.Write(new FigletText("Setup").Color(Color.CornflowerBlue));
        AnsiConsole.MarkupLine($"This wizard stores config in [grey]{Markup.Escape(paths.RootDirectory)}[/].");
        AnsiConsole.MarkupLine($"Guides: [link]{DocsHomeUrl}[/]  [grey]|[/]  Discord: [link]{DiscordSetupUrl}[/]  [grey]|[/]  AI: [link]{OpenAiGuideUrl}[/]");
        AnsiConsole.WriteLine();

        WriteSectionHeader("Before you start", "Have your Discord bot token, role IDs, channel IDs, and AI API key ready. Most installs should begin with one server.");
        var friendlyName = AskRequiredText("What should this installation call itself?", existingSettings?.FriendlyName ?? "BrrainzBot");

        AnsiConsole.MarkupLine("[grey]Self-update uses GitHub owner/repository format. Example: pardeike/BrrainzBot.[/]");
        var repository = AskRequiredText(
            "Which GitHub repository should self-update use?",
            existingSettings?.Updates.Repository ?? existingSettings?.GitHubRepository ?? "pardeike/BrrainzBot");

        WriteSectionHeader("Discord bot", $"Create the app in the Discord Developer Portal, then copy the bot token. Full guide: {DiscordSetupUrl}");
        var discordToken = AskSecret("Discord bot token", existingSecrets?.DiscordToken);

        WriteSectionHeader("AI endpoint", $"Use OpenAI or any compatible endpoint. Guide: {OpenAiGuideUrl}");
        var aiBaseUrl = AskRequiredText("OpenAI-compatible base URL", existingSettings?.Ai.BaseUrl ?? "https://api.openai.com/v1");
        var aiModel = AskRequiredText("AI model name", existingSettings?.Ai.Model ?? "gpt-5.4-nano");
        var aiApiKey = AskSecret("AI API key", existingSecrets?.AiApiKey);

        var servers = BuildServers(existingSettings?.Servers ?? []);

        var settings = new BotSettings
        {
            FriendlyName = friendlyName,
            GitHubRepository = repository,
            Updates = new UpdateSettings
            {
                Repository = repository,
                Enabled = true,
                Channel = existingSettings?.Updates.Channel ?? "stable"
            },
            Ai = new AiProviderSettings
            {
                BaseUrl = aiBaseUrl,
                Model = aiModel,
                ProviderType = existingSettings?.Ai.ProviderType ?? "OpenAiCompatible",
                RequireHttps = existingSettings?.Ai.RequireHttps ?? true,
                AllowInsecureLocalEndpoint = existingSettings?.Ai.AllowInsecureLocalEndpoint ?? false,
                ApiKeyEnvironmentVariable = existingSettings?.Ai.ApiKeyEnvironmentVariable ?? "BRRAINZBOT_OPENAI_API_KEY",
                Timeout = existingSettings?.Ai.Timeout ?? TimeSpan.FromSeconds(30)
            },
            Servers = servers
        };

        var secrets = new RuntimeSecrets
        {
            DiscordToken = discordToken,
            AiApiKey = aiApiKey
        };

        return (settings, secrets);
    }

    private static List<ServerSettings> BuildServers(IReadOnlyList<ServerSettings> existingServers)
    {
        var servers = new List<ServerSettings>();
        var index = 0;

        while (true)
        {
            var existingServer = index < existingServers.Count ? existingServers[index] : null;
            WriteSectionHeader(
                $"Server {index + 1}",
                index == 0
                    ? "Set up one server fully first. You can add more later by rerunning setup."
                    : "You can keep values from the previous server, then change only what is different.");

            servers.Add(BuildServerSettings(existingServer));
            index++;

            var defaultAddAnother = index < existingServers.Count;
            if (!AskConfirmation("Add another server?", defaultAddAnother))
                break;
        }

        return servers;
    }

    private static ServerSettings BuildServerSettings(ServerSettings? existing)
    {
        var draft = ServerDraft.FromExisting(existing);

        EditIdentityAndActivation(draft);
        EditDiscordIdsAndRoles(draft);
        EditOnboarding(draft);
        if (draft.EnableSpamGuard)
            EditSpamGuard(draft);

        while (true)
        {
            ShowServerSummary(draft);

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Review this server")
                    .AddChoices(
                        "Save this server",
                        "Edit identity and activation",
                        "Edit IDs and roles",
                        "Edit onboarding",
                        "Edit SpamGuard"));

            switch (action)
            {
                case "Save this server":
                    return draft.ToSettings();
                case "Edit identity and activation":
                    EditIdentityAndActivation(draft);
                    break;
                case "Edit IDs and roles":
                    EditDiscordIdsAndRoles(draft);
                    break;
                case "Edit onboarding":
                    EditOnboarding(draft);
                    break;
                case "Edit SpamGuard":
                    EditSpamGuard(draft);
                    break;
            }
        }
    }

    private static void EditIdentityAndActivation(ServerDraft draft)
    {
        WriteSectionHeader("Identity and activation", "Use plain labels here. This setup speaks in server terms throughout.");
        draft.Name = AskRequiredText("A friendly label for this server", draft.Name);
        draft.ServerId = AskSnowflake(
            "Server ID",
            draft.ServerId,
            "Enable Developer Mode in Discord, then right-click the server and choose Copy ID.");
        draft.OwnerUserId = AskSnowflake(
            "Owner user ID for uncertain-case DMs",
            draft.OwnerUserId,
            "This should be your own Discord user ID.");
        draft.IsActive = AskConfirmation("Turn this server on now?", draft.IsActive);
        draft.EnableSpamGuard = AskConfirmation("Turn on spam cleanup for this server?", draft.EnableSpamGuard);
    }

    private static void EditDiscordIdsAndRoles(ServerDraft draft)
    {
        WriteSectionHeader("IDs and roles", $"Create the roles and channels first, then copy their IDs. Guide: {DiscordSetupUrl}");
        draft.WelcomeChannelId = AskSnowflake("Welcome channel ID", draft.WelcomeChannelId);
        draft.MemberRoleId = AskSnowflake(
            "MEMBER role ID",
            draft.MemberRoleId,
            "Use a real MEMBER role. If you still need to create it, run `brrainzbot create-member <serverId>` after setup.");
    }

    private static void EditOnboarding(ServerDraft draft)
    {
        WriteSectionHeader("Onboarding", "Describe the server in direct language so the AI can tell real people from obvious wrong-server arrivals.");
        draft.ServerTopicPrompt = AskRequiredText(
            "Who belongs here and what should be filtered out?",
            draft.ServerTopicPrompt,
            "Keep it short and concrete. One strong paragraph is enough.");

        draft.WelcomeMessageTitle = AskRequiredText("Welcome title", draft.WelcomeMessageTitle);
        draft.WelcomeMessageBody = AskRequiredText("Welcome body", draft.WelcomeMessageBody);
        draft.StartButtonLabel = AskRequiredText("Start button label", draft.StartButtonLabel);
        draft.RulesHint = AskRequiredText("Rules hint shown in the welcome panel", draft.RulesHint);
        draft.MaxAttempts = AskInt("Maximum verification attempts", draft.MaxAttempts);
        draft.CooldownMinutes = AskInt("Cooldown after a failed attempt (minutes)", draft.CooldownMinutes);
        draft.StaleTimeoutHours = AskInt("Auto-kick unverified newcomers after how many hours?", draft.StaleTimeoutHours);
        draft.FirstQuestionLabel = AskRequiredText("First verification question", draft.FirstQuestionLabel);
        draft.SecondQuestionLabel = AskRequiredText("Second verification question", draft.SecondQuestionLabel);
        draft.ThirdQuestionLabel = AskRequiredText("Third verification question", draft.ThirdQuestionLabel);
    }

    private static void EditSpamGuard(ServerDraft draft)
    {
        WriteSectionHeader("Spam cleanup", "This is the second job of the bot. It is separate from onboarding.");
        draft.HoneypotChannelId = AskSnowflake("Spam honeypot channel ID", draft.HoneypotChannelId);
        draft.PastMessageIntervalSeconds = AskInt("Delete how many seconds of past messages on trigger?", draft.PastMessageIntervalSeconds);
        draft.FutureMessageIntervalSeconds = AskInt("Continue deleting for how many seconds after trigger?", draft.FutureMessageIntervalSeconds);
        draft.MessageDeltaIntervalSeconds = AskInt("Duplicate detection window in seconds", draft.MessageDeltaIntervalSeconds);
        draft.MinimumMessageLength = AskInt("Minimum message length for duplicate tracking", draft.MinimumMessageLength);
        draft.LinkRequired = AskConfirmation("Require a link for duplicate tracking?", draft.LinkRequired);
        draft.MessageSimilarityThreshold = AskDouble("Similarity threshold (0-1)", draft.MessageSimilarityThreshold);
    }

    private static void ShowServerSummary(ServerDraft draft)
    {
        var table = new Table().AddColumns("Setting", "Value");
        table.AddRow("Name", Markup.Escape(draft.Name));
        table.AddRow("Server ID", draft.ServerId == 0 ? "[red]missing[/]" : draft.ServerId.ToString());
        table.AddRow("Active", draft.IsActive ? "[green]on[/]" : "[yellow]off[/]");
        table.AddRow("Onboarding", "[green]core[/]");
        table.AddRow("Spam cleanup", draft.EnableSpamGuard ? "[green]on[/]" : "[grey]off[/]");
        table.AddRow("Welcome channel ID", draft.WelcomeChannelId == 0 ? "[red]missing[/]" : draft.WelcomeChannelId.ToString());
        table.AddRow("MEMBER role ID", draft.MemberRoleId == 0 ? "[red]missing[/]" : draft.MemberRoleId.ToString());
        table.AddRow("Owner user ID", draft.OwnerUserId == 0 ? "[red]missing[/]" : draft.OwnerUserId.ToString());

        if (draft.EnableSpamGuard)
            table.AddRow("Honeypot channel ID", draft.HoneypotChannelId == 0 ? "[red]missing[/]" : draft.HoneypotChannelId.ToString());

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void WriteSectionHeader(string title, string description)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]{Markup.Escape(title)}[/]");
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(description)}[/]");
        AnsiConsole.WriteLine();
    }

    private static string AskRequiredText(string prompt, string defaultValue, string? helpText = null)
    {
        if (!string.IsNullOrWhiteSpace(helpText))
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(helpText)}[/]");

        return AnsiConsole.Prompt(
            new TextPrompt<string>(prompt)
                .DefaultValue(defaultValue)
                .Validate(value =>
                    string.IsNullOrWhiteSpace(value)
                        ? ValidationResult.Error("[red]This value is required.[/]")
                        : ValidationResult.Success()));
    }

    private static string AskSecret(string prompt, string? existingValue)
    {
        var effectivePrompt = existingValue is null
            ? $"{prompt}:"
            : $"{prompt} [[leave blank to keep current]]:";
        var answer = AnsiConsole.Prompt(
            new TextPrompt<string>(effectivePrompt)
                .Secret()
                .Validate(value =>
                    string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(existingValue)
                        ? ValidationResult.Error("[red]This value is required.[/]")
                        : ValidationResult.Success())
                .AllowEmpty());
        return string.IsNullOrWhiteSpace(answer) && !string.IsNullOrWhiteSpace(existingValue) ? existingValue : answer;
    }

    private static ulong AskSnowflake(string prompt, ulong? existingValue, string? helpText = null)
    {
        if (!string.IsNullOrWhiteSpace(helpText))
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(helpText)}[/]");

        var textPrompt = new TextPrompt<ulong>($"{prompt}:")
            .Validate(value => value > 0 ? ValidationResult.Success() : ValidationResult.Error("[red]Enter a Discord snowflake ID.[/]"));

        if (existingValue is > 0)
            textPrompt = textPrompt.DefaultValue(existingValue.Value);

        return AnsiConsole.Prompt(textPrompt);
    }

    private static int AskInt(string prompt, int defaultValue) => AnsiConsole.Prompt(
        new TextPrompt<int>($"{prompt}:").DefaultValue(defaultValue));

    private static double AskDouble(string prompt, double defaultValue) => AnsiConsole.Prompt(
        new TextPrompt<double>($"{prompt}:")
            .DefaultValue(defaultValue)
            .Validate(value => value is >= 0 and <= 1 ? ValidationResult.Success() : ValidationResult.Error("[red]Enter a value between 0 and 1.[/]")));

    private static bool AskConfirmation(string prompt, bool defaultValue) => AnsiConsole.Confirm(prompt, defaultValue);

    private sealed class ServerDraft
    {
        public string Name { get; set; } = "My Discord Server";
        public ulong ServerId { get; set; }
        public bool IsActive { get; set; }
        public ulong WelcomeChannelId { get; set; }
        public ulong MemberRoleId { get; set; }
        public ulong OwnerUserId { get; set; }
        public bool EnableSpamGuard { get; set; } = true;
        public string ServerTopicPrompt { get; set; } =
            "This server is for real people who want to take part in this community. Reject spam, scams, and obvious wrong-server arrivals.";
        public List<ulong> PublicReadOnlyChannelIds { get; set; } = [];
        public string WelcomeMessageTitle { get; set; } = "Welcome to the server";
        public string WelcomeMessageBody { get; set; } = "Click below to verify and unlock the full server.";
        public string StartButtonLabel { get; set; } = "Start verification";
        public string RulesHint { get; set; } = "Be kind, stay on topic, and do not spam.";
        public int MaxAttempts { get; set; } = 3;
        public int CooldownMinutes { get; set; } = 10;
        public int StaleTimeoutHours { get; set; } = 24;
        public string FirstQuestionLabel { get; set; } = "What brought you here?";
        public string SecondQuestionLabel { get; set; } = "What do you want to do here?";
        public string ThirdQuestionLabel { get; set; } = "Paraphrase one expectation or rule.";
        public ulong HoneypotChannelId { get; set; }
        public int PastMessageIntervalSeconds { get; set; } = 300;
        public int FutureMessageIntervalSeconds { get; set; } = 300;
        public int MessageDeltaIntervalSeconds { get; set; } = 120;
        public int MinimumMessageLength { get; set; } = 40;
        public bool LinkRequired { get; set; } = true;
        public double MessageSimilarityThreshold { get; set; } = 0.85;

        public static ServerDraft FromExisting(ServerSettings? existing)
        {
            if (existing == null)
                return new ServerDraft();

            return new ServerDraft
            {
                Name = existing.Name,
                ServerId = existing.ServerId,
                IsActive = existing.IsActive,
                WelcomeChannelId = existing.WelcomeChannelId,
                MemberRoleId = existing.MemberRoleId != 0 && existing.MemberRoleId != existing.ServerId ? existing.MemberRoleId : 0,
                OwnerUserId = existing.OwnerUserId,
                EnableSpamGuard = existing.EnableSpamGuard,
                ServerTopicPrompt = existing.ServerTopicPrompt,
                PublicReadOnlyChannelIds = [.. existing.PublicReadOnlyChannelIds],
                WelcomeMessageTitle = existing.Onboarding.WelcomeMessageTitle,
                WelcomeMessageBody = existing.Onboarding.WelcomeMessageBody,
                StartButtonLabel = existing.Onboarding.StartButtonLabel,
                RulesHint = existing.Onboarding.RulesHint,
                MaxAttempts = existing.Onboarding.MaxAttempts,
                CooldownMinutes = (int)existing.Onboarding.Cooldown.TotalMinutes,
                StaleTimeoutHours = (int)existing.Onboarding.StaleTimeout.TotalHours,
                FirstQuestionLabel = existing.Onboarding.FirstQuestionLabel,
                SecondQuestionLabel = existing.Onboarding.SecondQuestionLabel,
                ThirdQuestionLabel = existing.Onboarding.ThirdQuestionLabel,
                HoneypotChannelId = existing.SpamGuard.HoneypotChannelId,
                PastMessageIntervalSeconds = existing.SpamGuard.PastMessageIntervalSeconds,
                FutureMessageIntervalSeconds = existing.SpamGuard.FutureMessageIntervalSeconds,
                MessageDeltaIntervalSeconds = existing.SpamGuard.MessageDeltaIntervalSeconds,
                MinimumMessageLength = existing.SpamGuard.MinimumMessageLength,
                LinkRequired = existing.SpamGuard.LinkRequired,
                MessageSimilarityThreshold = existing.SpamGuard.MessageSimilarityThreshold
            };
        }

        public ServerSettings ToSettings() => new()
        {
            Name = Name,
            ServerId = ServerId,
            IsActive = IsActive,
            WelcomeChannelId = WelcomeChannelId,
            MemberRoleId = MemberRoleId,
            OwnerUserId = OwnerUserId,
            EnableSpamGuard = EnableSpamGuard,
            ServerTopicPrompt = ServerTopicPrompt,
            PublicReadOnlyChannelIds = [.. PublicReadOnlyChannelIds],
            Onboarding = new OnboardingSettings
            {
                WelcomeMessageTitle = WelcomeMessageTitle,
                WelcomeMessageBody = WelcomeMessageBody,
                StartButtonLabel = StartButtonLabel,
                RulesHint = RulesHint,
                MaxAttempts = MaxAttempts,
                Cooldown = TimeSpan.FromMinutes(CooldownMinutes),
                StaleTimeout = TimeSpan.FromHours(StaleTimeoutHours),
                NotifyOwnerOnUncertain = true,
                NotifyOwnerOnTechnicalFailure = true,
                FirstQuestionLabel = FirstQuestionLabel,
                SecondQuestionLabel = SecondQuestionLabel,
                ThirdQuestionLabel = ThirdQuestionLabel
            },
            SpamGuard = new SpamGuardSettings
            {
                HoneypotChannelId = HoneypotChannelId,
                PastMessageIntervalSeconds = PastMessageIntervalSeconds,
                FutureMessageIntervalSeconds = FutureMessageIntervalSeconds,
                MessageDeltaIntervalSeconds = MessageDeltaIntervalSeconds,
                MinimumMessageLength = MinimumMessageLength,
                LinkRequired = LinkRequired,
                MessageSimilarityThreshold = MessageSimilarityThreshold
            }
        };
    }
}
