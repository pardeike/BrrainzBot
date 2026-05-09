using System.Net;
using BrrainzBot.Host;
using Discord.Net;

namespace BrrainzBot.Modules.Onboarding;

internal static class OnboardingFailurePolicy
{
    private static readonly TimeSpan[] TransientDiscordRetryDelays =
    [
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromMilliseconds(750),
        TimeSpan.FromSeconds(1.5)
    ];

    public static VerificationSessionSnapshot CaptureSessionState(VerificationSession session) => new(
        session.AttemptCount,
        session.CooldownUntil,
        session.LastDecisionReason,
        session.LastOutcome,
        [.. session.History]);

    public static void RestoreSessionState(VerificationSession session, VerificationSessionSnapshot snapshot)
    {
        session.AttemptCount = snapshot.AttemptCount;
        session.CooldownUntil = snapshot.CooldownUntil;
        session.LastDecisionReason = snapshot.LastDecisionReason;
        session.LastOutcome = snapshot.LastOutcome;
        session.History.Clear();
        session.History.AddRange(snapshot.History);
    }

    public static bool ShouldRestoreAttempt(Exception exception) => exception is HttpException { HttpCode: var statusCode }
        && IsTransientDiscordStatusCode(statusCode);

    public static async Task RunWithTransientDiscordRetriesAsync(
        Func<Task> operation,
        Action<int, TimeSpan, HttpException>? onRetry = null,
        Func<int, TimeSpan>? retryDelayFactory = null,
        CancellationToken cancellationToken = default)
    {
        retryDelayFactory ??= GetTransientDiscordRetryDelay;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (HttpException ex) when (IsTransientDiscordStatusCode(ex.HttpCode) && attempt <= TransientDiscordRetryDelays.Length)
            {
                var delay = retryDelayFactory(attempt);
                onRetry?.Invoke(attempt, delay, ex);
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken);
            }
        }
    }

    public static TimeSpan GetTransientDiscordRetryDelay(int attempt) => attempt switch
    {
        >= 1 and <= 3 => TransientDiscordRetryDelays[attempt - 1],
        _ => TransientDiscordRetryDelays[^1]
    };

    internal static bool IsTransientDiscordFailure(Exception exception) => ShouldRestoreAttempt(exception);

    private static bool IsTransientDiscordStatusCode(HttpStatusCode statusCode) => (int)statusCode is >= 500 and <= 599;
}

internal sealed record VerificationSessionSnapshot(
    int AttemptCount,
    DateTimeOffset? CooldownUntil,
    string? LastDecisionReason,
    VerificationOutcome? LastOutcome,
    IReadOnlyList<string> History);

internal sealed class RoleAssignmentTransientFailureException(HttpException innerException)
    : Exception(innerException.Message, innerException)
{
    public HttpException HttpException => innerException;
}
