using BrrainzBot.Modules.SpamGuard;

namespace BrrainzBot.Tests;

public sealed class MessageTrackerTests
{
    [Fact]
    public void HoneypotMessageFlagsUserImmediately()
    {
        var tracker = new MessageTracker(120, 10, linkRequired: false, 0.85, "welcome");

        var (result, _) = tracker.CheckMessage("welcome", 42, 100, "hello there", DateTimeOffset.UtcNow);

        Assert.Equal(SpamDetectionResult.HoneypotTriggered, result);
    }

    [Fact]
    public void SimilarMessagesInDifferentChannelsAreDetected()
    {
        var tracker = new MessageTracker(120, 10, linkRequired: false, 0.85, "honeypot");
        var now = DateTimeOffset.UtcNow;
        _ = tracker.CheckMessage("general", 42, 100, "this is definitely spam", now);

        var (result, firstChannelId) = tracker.CheckMessage("links", 42, 101, "this is definitely spam!", now.AddSeconds(10));

        Assert.Equal(SpamDetectionResult.DuplicateDetected, result);
        Assert.Equal<ulong>(100, firstChannelId);
    }

    [Fact]
    public void ShortMessagesCanBeIgnored()
    {
        var tracker = new MessageTracker(120, 20, linkRequired: false, 0.85, "honeypot");

        var (result, _) = tracker.CheckMessage("general", 42, 100, "tiny", DateTimeOffset.UtcNow);

        Assert.Equal(SpamDetectionResult.Ignored, result);
    }
}
