using System.Diagnostics;
using BrrainzBot.Host;
using BrrainzBot.Infrastructure;
using BrrainzBot.Modules.Onboarding;
using BrrainzBot.Modules.SpamGuard;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace BrrainzBot.Cli;

internal static class CliApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "help";
        var remaining = args.Skip(1).ToArray();
        var paths = ResolvePaths(remaining);
        var commandArgs = StripRootArguments(remaining);

        return command switch
        {
            "help" or "--help" or "-h" => ShowHelp(),
            "setup" => await SetupAsync(paths),
            "status" => await StatusAsync(paths, commandArgs),
            "doctor" => await DoctorAsync(paths),
            "print-config" => await PrintConfigAsync(paths),
            "run" => await RunBotAsync(paths),
            "self-update" => await SelfUpdateAsync(paths),
            "__internal-apply-update" => await ApplyUpdateAsync(commandArgs),
            _ => ShowUnknownCommand(command)
        };
    }

    private static int ShowHelp()
    {
        AnsiConsole.Write(new FigletText("BrrainzBot").Color(Color.CornflowerBlue));
        AnsiConsole.MarkupLine("[grey]Friendly Discord onboarding and spam defense.[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Usage: [aqua]brrainzbot[/] <command> [grey][[--root path]][/]");
        AnsiConsole.MarkupLine("Commands:");
        AnsiConsole.MarkupLine("  [green]setup[/]         Create or update the configuration with a guided wizard.");
        AnsiConsole.MarkupLine("  [green]status[/]        Show per-guild activation, or switch one guild on or off.");
        AnsiConsole.MarkupLine("  [green]doctor[/]        Validate configuration, Discord IDs, and AI settings.");
        AnsiConsole.MarkupLine("  [green]print-config[/]  Show the current configuration with secrets redacted.");
        AnsiConsole.MarkupLine("  [green]run[/]           Start the bot.");
        AnsiConsole.MarkupLine("  [green]self-update[/]   Fetch the latest release from GitHub after confirmation.");
        return 0;
    }

    private static int ShowUnknownCommand(string command)
    {
        AnsiConsole.MarkupLine($"[red]Unknown command:[/] {Markup.Escape(command)}");
        AnsiConsole.MarkupLine("Run [aqua]brrainzbot help[/] to see the available commands.");
        return 1;
    }

    private static async Task<int> SetupAsync(AppPaths paths)
    {
        var store = new BotConfigurationStore();
        var existing = store.Exists(paths)
            ? await store.LoadAsync(paths, CancellationToken.None)
            : ((BotSettings?)null, (RuntimeSecrets?)null);

        var result = SetupWizard.Run(existing.Item1, existing.Item2, paths);
        await store.SaveAsync(paths, result.Settings, result.Secrets, CancellationToken.None);

        AnsiConsole.MarkupLine($"[green]Saved configuration to[/] {Markup.Escape(paths.ConfigFilePath)}");
        AnsiConsole.MarkupLine($"[green]Saved secrets to[/] {Markup.Escape(paths.SecretsFilePath)}");
        AnsiConsole.MarkupLine("Next steps:");
        AnsiConsole.MarkupLine("  1. Run [aqua]brrainzbot doctor[/] to validate the setup.");
        AnsiConsole.MarkupLine("  2. Use [aqua]brrainzbot status[/] to confirm which guilds are active.");
        AnsiConsole.MarkupLine("  3. Run [aqua]brrainzbot run[/] when you are ready.");
        return 0;
    }

    private static async Task<int> StatusAsync(AppPaths paths, IReadOnlyList<string> args)
    {
        var store = new BotConfigurationStore();
        var settings = await store.LoadSettingsAsync(paths, CancellationToken.None);

        if (args.Count == 0)
            return ShowStatus(settings);

        if (args.Count != 2 || (args[0] != "on" && args[0] != "off") || !ulong.TryParse(args[1], out var guildId))
        {
            AnsiConsole.MarkupLine("[red]Usage:[/] [aqua]brrainzbot status[/], [aqua]brrainzbot status on <guildId>[/], [aqua]brrainzbot status off <guildId>[/]");
            return 1;
        }

        var guild = settings.FindGuild(guildId);
        if (guild == null)
        {
            AnsiConsole.MarkupLine($"[red]Guild {guildId} is not in the current config.[/]");
            return 1;
        }

        var updatedSettings = ReplaceGuild(
            settings,
            guildId,
            new GuildSettings
            {
                Name = guild.Name,
                GuildId = guild.GuildId,
                IsActive = args[0] == "on",
                WelcomeChannelId = guild.WelcomeChannelId,
                NewRoleId = guild.NewRoleId,
                MemberRoleId = guild.MemberRoleId,
                OwnerUserId = guild.OwnerUserId,
                EnableOnboarding = guild.EnableOnboarding,
                EnableSpamGuard = guild.EnableSpamGuard,
                GuildTopicPrompt = guild.GuildTopicPrompt,
                PublicReadOnlyChannelIds = [.. guild.PublicReadOnlyChannelIds],
                Onboarding = guild.Onboarding,
                SpamGuard = guild.SpamGuard
            });
        await store.SaveSettingsAsync(paths, updatedSettings, CancellationToken.None);

        AnsiConsole.MarkupLine(
            $"[green]{Markup.Escape(guild.Name)}[/] is now {(args[0] == "on" ? "[green]on[/]" : "[yellow]off[/]")}.");
        AnsiConsole.MarkupLine("A running bot process will pick this up automatically within a few seconds.");
        return 0;
    }

    private static int ShowStatus(BotSettings settings)
    {
        var table = new Table().AddColumns("Guild", "Guild ID", "Active", "Onboarding", "SpamGuard");
        foreach (var guild in settings.Guilds.OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
        {
            table.AddRow(
                Markup.Escape(guild.Name),
                guild.GuildId.ToString(),
                guild.IsActive ? "[green]on[/]" : "[yellow]off[/]",
                guild.EnableOnboarding ? "[green]on[/]" : "[grey]off[/]",
                guild.EnableSpamGuard ? "[green]on[/]" : "[grey]off[/]");
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private static async Task<int> DoctorAsync(AppPaths paths)
    {
        var (settings, secrets) = await LoadRequiredConfigurationAsync(paths);
        var services = new ServiceCollection()
            .AddBrrainzBotInfrastructure(settings, secrets, paths)
            .BuildServiceProvider();

        var doctor = services.GetRequiredService<BotDoctor>();
        var report = await doctor.RunAsync(settings, secrets, paths, CancellationToken.None);
        var table = new Table().AddColumns("Severity", "Code", "Message");
        foreach (var message in report.Messages)
        {
            var color = message.Severity switch
            {
                DiagnosticSeverity.Error => "red",
                DiagnosticSeverity.Warning => "yellow",
                _ => "grey"
            };
            table.AddRow($"[{color}]{message.Severity}[/]", Markup.Escape(message.Code), Markup.Escape(message.Message));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        if (report.HasErrors)
        {
            AnsiConsole.MarkupLine("[red]Doctor found blocking problems.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[green]Doctor finished without blocking problems.[/]");
        return 0;
    }

    private static async Task<int> PrintConfigAsync(AppPaths paths)
    {
        var store = new BotConfigurationStore();
        var (settings, secrets) = await store.LoadAsync(paths, CancellationToken.None);
        AnsiConsole.MarkupLine("[green]Paths[/]");
        AnsiConsole.MarkupLine($"  root: {Markup.Escape(paths.RootDirectory)}");
        AnsiConsole.MarkupLine($"  config: {Markup.Escape(paths.ConfigFilePath)}");
        AnsiConsole.MarkupLine($"  secrets: {Markup.Escape(paths.SecretsFilePath)}");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]Config[/]");
        AnsiConsole.Write(new Panel(new Text(store.ToDisplayJson(settings))).Expand());
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]Secrets[/]");
        AnsiConsole.Write(new Panel(new Text(System.Text.Json.JsonSerializer.Serialize(secrets.Redacted(), JsonDefaults.Options))).Expand());
        return 0;
    }

    private static async Task<int> RunBotAsync(AppPaths paths)
    {
        var (settings, secrets) = await LoadRequiredConfigurationAsync(paths);

        using var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                });
            })
            .ConfigureServices(services =>
            {
                services.AddBrrainzBotInfrastructure(settings, secrets, paths);
                if (settings.Guilds.Any(g => g.EnableOnboarding))
                    services.AddOnboardingModule();
                if (settings.Guilds.Any(g => g.EnableSpamGuard))
                    services.AddSpamGuardModule();
            })
            .Build();

        AnsiConsole.MarkupLine($"[green]Starting {Markup.Escape(settings.FriendlyName)} {Markup.Escape(BotMetadata.Version)}[/]");
        await host.RunAsync();
        return 0;
    }

    private static async Task<int> SelfUpdateAsync(AppPaths paths)
    {
        BotSettings settings;
        try
        {
            (settings, _) = await LoadRequiredConfigurationAsync(paths);
        }
        catch
        {
            settings = new BotSettings();
        }

        var services = new ServiceCollection()
            .AddBrrainzBotInfrastructure(settings, new RuntimeSecrets(), paths)
            .BuildServiceProvider();

        var updater = services.GetRequiredService<SelfUpdateService>();
        var prepared = await updater.PrepareAsync(settings.Updates, CancellationToken.None);
        if (prepared == null)
        {
            AnsiConsole.MarkupLine("[yellow]No matching release asset was found for this platform.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"Current version: [aqua]{Markup.Escape(BotMetadata.Version)}[/]");
        AnsiConsole.MarkupLine($"Latest release: [aqua]{Markup.Escape(prepared.Release.TagName)}[/]");
        AnsiConsole.MarkupLine($"Asset: [grey]{Markup.Escape(prepared.Asset.Name)}[/] ({prepared.Asset.SizeBytes / 1024d / 1024d:F1} MiB)");
        AnsiConsole.Write(new Panel(prepared.Release.Body).Header("Release notes").Expand());

        if (!AnsiConsole.Confirm("Download and replace the current binary?", defaultValue: false))
            return 0;

        var currentExecutable = Environment.ProcessPath ?? throw new InvalidOperationException("Could not resolve the current executable path.");
        var helperExecutable = updater.CreateHelperExecutable(currentExecutable);
        var startInfo = new ProcessStartInfo
        {
            FileName = helperExecutable,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("__internal-apply-update");
        startInfo.ArgumentList.Add(currentExecutable);
        startInfo.ArgumentList.Add(prepared.ExtractedBinaryPath);
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());

        Process.Start(startInfo);
        AnsiConsole.MarkupLine("[green]The updater helper has started. This process will now exit so the binary can be replaced.[/]");
        return 0;
    }

    private static async Task<int> ApplyUpdateAsync(IReadOnlyList<string> args)
    {
        if (args.Count < 4)
            return 1;

        var targetExecutable = args[1];
        var sourceExecutable = args[2];
        _ = int.TryParse(args[3], out var waitForProcessId);
        var paths = AppPaths.CreateDefault();
        var services = new ServiceCollection()
            .AddBrrainzBotInfrastructure(new BotSettings(), new RuntimeSecrets(), paths)
            .BuildServiceProvider();
        var updater = services.GetRequiredService<SelfUpdateService>();
        await updater.ApplyAsync(targetExecutable, sourceExecutable, waitForProcessId, CancellationToken.None);
        return 0;
    }

    private static async Task<(BotSettings Settings, RuntimeSecrets Secrets)> LoadRequiredConfigurationAsync(AppPaths paths)
    {
        var store = new BotConfigurationStore();
        if (!store.Exists(paths))
            throw new FileNotFoundException("Run `brrainzbot setup` before using this command.");

        return await store.LoadAsync(paths, CancellationToken.None);
    }

    private static AppPaths ResolvePaths(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--root", StringComparison.Ordinal))
                return AppPaths.FromRoot(args[i + 1]);
        }

        return AppPaths.CreateDefault();
    }

    private static string[] StripRootArguments(string[] args)
    {
        var filtered = new List<string>(args.Length);
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--root", StringComparison.Ordinal))
            {
                i++;
                continue;
            }

            filtered.Add(args[i]);
        }

        return [.. filtered];
    }

    private static BotSettings ReplaceGuild(BotSettings settings, ulong guildId, GuildSettings replacement) => new()
    {
        FriendlyName = settings.FriendlyName,
        GitHubRepository = settings.GitHubRepository,
        Updates = settings.Updates,
        Ai = settings.Ai,
        Guilds = [.. settings.Guilds.Select(g => g.GuildId == guildId ? replacement : g)]
    };
}
