using System.Reflection;

namespace BrrainzBot.Host;

public static class BotMetadata
{
    public static string ProductName => typeof(BotMetadata).Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "BrrainzBot";
    public static string Version => typeof(BotMetadata).Assembly.GetName().Version?.ToString() ?? "0.1.1";
}
