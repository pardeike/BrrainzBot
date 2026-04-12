namespace BrrainzBot.Host;

public sealed class RuntimeSecrets
{
    public string DiscordToken { get; init; } = string.Empty;
    public string AiApiKey { get; init; } = string.Empty;

    public RuntimeSecrets Redacted() => new()
    {
        DiscordToken = Mask(DiscordToken),
        AiApiKey = Mask(AiApiKey)
    };

    private static string Mask(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "(missing)";

        if (value.Length <= 6)
            return new string('*', value.Length);

        return $"{value[..3]}***{value[^3..]}";
    }
}
