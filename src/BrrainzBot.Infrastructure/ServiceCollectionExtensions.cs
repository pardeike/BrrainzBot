using BrrainzBot.Host;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace BrrainzBot.Infrastructure;

public static class ServiceCollectionExtensions
{
    public const string AiHttpClientName = "BrrainzBot.Ai";
    public const string GitHubHttpClientName = "BrrainzBot.GitHub";
    public const string DiscordProbeHttpClientName = "BrrainzBot.DiscordProbe";

    public static IServiceCollection AddBrrainzBotInfrastructure(
        this IServiceCollection services,
        BotSettings settings,
        RuntimeSecrets secrets,
        AppPaths paths)
    {
        services.AddSingleton(settings);
        services.AddSingleton(secrets);
        services.AddSingleton(paths);
        services.AddSingleton<BotConfigurationStore>();
        services.AddSingleton<BotDoctor>();
        services.AddSingleton<ServerAdministrationService>();
        services.AddSingleton<ReloadingBotSettingsProvider>();
        services.AddSingleton<IBotSettingsProvider>(provider => provider.GetRequiredService<ReloadingBotSettingsProvider>());
        services.AddSingleton<IAuditLog, JsonAuditLog>();
        services.AddSingleton<IVerificationSessionStore, JsonVerificationSessionStore>();
        services.AddSingleton<IAiProviderClient, OpenAiCompatibleClient>();
        services.AddSingleton<GitHubReleaseService>();
        services.AddSingleton<SelfUpdateService>();
        services.AddHttpClient();
        services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents =
                Discord.GatewayIntents.Guilds |
                Discord.GatewayIntents.GuildMembers |
                Discord.GatewayIntents.GuildMessages |
                Discord.GatewayIntents.MessageContent
        }));
        services.AddHostedService(provider => provider.GetRequiredService<ReloadingBotSettingsProvider>());
        services.AddHostedService<DiscordGatewayHostedService>();
        return services;
    }
}
