using BrrainzBot.Modules.Onboarding;

namespace BrrainzBot.Tests;

public sealed class OnboardingInputGuardTests
{
    [Fact]
    public void SanitizeAnswer_StripsControls_CollapsesWhitespace_AndLimitsLength()
    {
        var raw = "  hello\t\tthere\r\nworld\u0007 " + new string('x', 400);

        var sanitized = OnboardingInputGuard.SanitizeAnswer(raw);

        Assert.StartsWith("hello there world", sanitized);
        Assert.DoesNotContain('\u0007', sanitized);
        Assert.True(sanitized.Length <= OnboardingInputGuard.AnswerMaxLength);
        Assert.DoesNotContain("  ", sanitized);
    }

    [Fact]
    public void LooksLikeLowSignalSubmission_DetectsPlaceholdersAndRepeatedAnswers()
    {
        Assert.True(OnboardingInputGuard.LooksLikeLowSignalSubmission("bla", "bla", "bla"));
        Assert.True(OnboardingInputGuard.LooksLikeLowSignalSubmission("test", "123", "ok"));
        Assert.False(OnboardingInputGuard.LooksLikeLowSignalSubmission(
            "I joined for RimWorld modding.",
            "I want to discuss Harmony patches.",
            "Stay on topic and do not spam."));
    }
}
