using System.Net;
using BrrainzBot.Host;
using BrrainzBot.Modules.Onboarding;
using Discord.Net;

namespace BrrainzBot.Tests;

public sealed class OnboardingFailurePolicyTests
{
    [Fact]
    public void IsTransientDiscordFailure_MatchesDiscord5xxOnly()
    {
        Assert.True(OnboardingFailurePolicy.IsTransientDiscordFailure(CreateHttpException(HttpStatusCode.InternalServerError)));
        Assert.True(OnboardingFailurePolicy.IsTransientDiscordFailure(CreateHttpException(HttpStatusCode.ServiceUnavailable)));
        Assert.False(OnboardingFailurePolicy.IsTransientDiscordFailure(CreateHttpException(HttpStatusCode.TooManyRequests)));
        Assert.False(OnboardingFailurePolicy.IsTransientDiscordFailure(new InvalidOperationException("boom")));
    }

    [Fact]
    public void RestoreSessionState_RewindsAttemptCooldownAndHistory()
    {
        var originalCooldown = DateTimeOffset.UtcNow.AddMinutes(5);
        var session = new VerificationSession
        {
            ServerId = 1,
            UserId = 2,
            UserName = "mibac",
            JoinedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            AttemptCount = 1,
            CooldownUntil = originalCooldown,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            LastDecisionReason = "previous",
            LastOutcome = VerificationOutcome.Retry,
            History = ["earlier history"]
        };

        var snapshot = OnboardingFailurePolicy.CaptureSessionState(session);

        session.AttemptCount = 2;
        session.CooldownUntil = DateTimeOffset.UtcNow.AddMinutes(20);
        session.LastDecisionReason = "temporary discord failure";
        session.LastOutcome = VerificationOutcome.Uncertain;
        session.History.Add("temporary mutation");

        OnboardingFailurePolicy.RestoreSessionState(session, snapshot);

        Assert.Equal(1, session.AttemptCount);
        Assert.Equal(originalCooldown, session.CooldownUntil);
        Assert.Equal("previous", session.LastDecisionReason);
        Assert.Equal(VerificationOutcome.Retry, session.LastOutcome);
        Assert.Equal(["earlier history"], session.History);
    }

    [Fact]
    public async Task RunWithTransientDiscordRetriesAsync_RetriesTransientFailuresAndSucceeds()
    {
        var attempts = 0;

        await OnboardingFailurePolicy.RunWithTransientDiscordRetriesAsync(
            () =>
            {
                attempts++;
                if (attempts < 3)
                    throw CreateHttpException(HttpStatusCode.ServiceUnavailable);
                return Task.CompletedTask;
            },
            retryDelayFactory: static _ => TimeSpan.Zero);

        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task RunWithTransientDiscordRetriesAsync_ThrowsAfterRetryBudget()
    {
        var attempts = 0;

        var exception = await Assert.ThrowsAsync<HttpException>(() =>
            OnboardingFailurePolicy.RunWithTransientDiscordRetriesAsync(
                () =>
                {
                    attempts++;
                    throw CreateHttpException(HttpStatusCode.ServiceUnavailable);
                },
                retryDelayFactory: static _ => TimeSpan.Zero));

        Assert.Equal(4, attempts);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.HttpCode);
    }

    private static HttpException CreateHttpException(HttpStatusCode statusCode) => new(statusCode, null!, null, "unit test", []);
}
