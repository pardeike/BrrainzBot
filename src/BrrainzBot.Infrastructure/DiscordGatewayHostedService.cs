using BrrainzBot.Host;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BrrainzBot.Infrastructure;

public sealed class DiscordGatewayHostedService(
    DiscordSocketClient client,
    IEnumerable<IDiscordModule> modules,
    RuntimeSecrets secrets,
    ILogger<DiscordGatewayHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        client.Log += OnLogAsync;
        foreach (var module in modules)
        {
            logger.LogInformation("Registering Discord module {ModuleName}", module.Name);
            await module.RegisterAsync(cancellationToken);
        }

        await client.LoginAsync(TokenType.Bot, secrets.DiscordToken);
        await client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await client.StopAsync();
        await client.LogoutAsync();
    }

    private Task OnLogAsync(LogMessage message)
    {
        var level = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };

        logger.Log(level, message.Exception, "[Discord] {Source}: {Message}", message.Source, message.Message);
        return Task.CompletedTask;
    }
}
