using System.Text.Json;
using BrrainzBot.Host;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BrrainzBot.Infrastructure;

public sealed class ReloadingBotSettingsProvider(
    BotSettings initialSettings,
    AppPaths paths,
    ILogger<ReloadingBotSettingsProvider> logger) : IBotSettingsProvider, IHostedService
{
    private BotSettings _current = initialSettings;
    private DateTimeOffset? _lastObservedWriteUtc = File.Exists(paths.ConfigFilePath)
        ? File.GetLastWriteTimeUtc(paths.ConfigFilePath)
        : null;
    private CancellationTokenSource? _reloadLoopCancellation;
    private Task? _reloadLoopTask;

    public BotSettings Current => _current;
    public event Action<BotSettings>? Changed;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _reloadLoopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _reloadLoopTask = Task.Run(() => RunReloadLoopAsync(_reloadLoopCancellation.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_reloadLoopCancellation == null || _reloadLoopTask == null)
            return;

        _reloadLoopCancellation.Cancel();
        try
        {
            await _reloadLoopTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RunReloadLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                await ReloadIfNeededAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to reload config from {ConfigFilePath}", paths.ConfigFilePath);
            }
        }
    }

    private async Task ReloadIfNeededAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.ConfigFilePath))
            return;

        var currentWriteUtc = File.GetLastWriteTimeUtc(paths.ConfigFilePath);
        if (_lastObservedWriteUtc is { } lastObservedWriteUtc && currentWriteUtc <= lastObservedWriteUtc)
            return;

        await using var stream = File.OpenRead(paths.ConfigFilePath);
        var reloaded = await JsonSerializer.DeserializeAsync<BotSettings>(stream, JsonDefaults.Options, cancellationToken);
        if (reloaded == null)
            return;

        _current = reloaded;
        _lastObservedWriteUtc = currentWriteUtc;
        logger.LogInformation("Reloaded bot settings from {ConfigFilePath}", paths.ConfigFilePath);
        Changed?.Invoke(reloaded);
    }
}
