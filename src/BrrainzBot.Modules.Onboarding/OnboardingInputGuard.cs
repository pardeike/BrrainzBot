using System.Text;

namespace BrrainzBot.Modules.Onboarding;

internal static class OnboardingInputGuard
{
    public const int AnswerMaxLength = 160;

    private static readonly HashSet<string> PlaceholderAnswers = new(StringComparer.OrdinalIgnoreCase)
    {
        "a",
        "aa",
        "aaa",
        "abc",
        "asdf",
        "asdasd",
        "bla",
        "blah",
        "hello",
        "hi",
        "idk",
        "n/a",
        "na",
        "none",
        "ok",
        "test",
        "testing",
        "123",
        "1234",
        "???",
        "..."
    };

    public static string SanitizeAnswer(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(Math.Min(normalized.Length, AnswerMaxLength));
        var lastWasWhitespace = false;

        foreach (var character in normalized)
        {
            if (char.IsControl(character))
            {
                if (character is '\r' or '\n' or '\t')
                {
                    if (!lastWasWhitespace && builder.Length > 0)
                    {
                        builder.Append(' ');
                        lastWasWhitespace = true;
                    }
                }

                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (!lastWasWhitespace && builder.Length > 0)
                {
                    builder.Append(' ');
                    lastWasWhitespace = true;
                }

                continue;
            }

            builder.Append(character);
            lastWasWhitespace = false;

            if (builder.Length >= AnswerMaxLength)
                break;
        }

        return builder.ToString().Trim();
    }

    public static bool LooksLikeLowSignalSubmission(string whyHere, string whatDoYouWant, string ruleParaphrase)
    {
        var answers = new[] { whyHere, whatDoYouWant, ruleParaphrase };

        if (answers.Any(string.IsNullOrWhiteSpace))
            return true;

        if (answers.All(answer => string.Equals(answer, answers[0], StringComparison.OrdinalIgnoreCase)))
            return true;

        return answers.All(answer => PlaceholderAnswers.Contains(answer));
    }
}
