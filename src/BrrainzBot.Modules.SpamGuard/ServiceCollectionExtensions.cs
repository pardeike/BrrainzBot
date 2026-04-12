using BrrainzBot.Host;
using Microsoft.Extensions.DependencyInjection;

namespace BrrainzBot.Modules.SpamGuard;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSpamGuardModule(this IServiceCollection services)
    {
        services.AddSingleton<IDiscordModule, SpamGuardModule>();
        return services;
    }
}
