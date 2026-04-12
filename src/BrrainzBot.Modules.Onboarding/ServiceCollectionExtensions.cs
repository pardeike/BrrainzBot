using BrrainzBot.Host;
using Microsoft.Extensions.DependencyInjection;

namespace BrrainzBot.Modules.Onboarding;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOnboardingModule(this IServiceCollection services)
    {
        services.AddSingleton<IDiscordModule, OnboardingModule>();
        return services;
    }
}
