using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BrrainzBot.Host;
using Microsoft.Extensions.Logging;

namespace BrrainzBot.Infrastructure;

public sealed class OpenAiCompatibleClient(
    IHttpClientFactory httpClientFactory,
    BotSettings settings,
    RuntimeSecrets secrets,
    ILogger<OpenAiCompatibleClient> logger) : IAiProviderClient
{
    private const string SystemPrompt = """
You are deciding whether a Discord newcomer should be allowed into a server.
Return strict JSON only.
Schema:
{
  "outcome": "Approve" | "Retry" | "Uncertain",
  "reason": "short internal reason",
  "friendlyReply": "warm, short message to the user",
  "confidence": 0.0,
  "suggestedCooldownMinutes": 0
}
Rules:
- Approve real, on-topic, non-spammy users even if imperfect.
- Retry when the user seems human but vague, inattentive, or too short.
- Uncertain only for edge cases where owner awareness would help.
- Keep the reply friendly and non-technical.
""";

    public async Task<VerificationDecision> EvaluateAsync(VerificationPrompt prompt, CancellationToken cancellationToken)
    {
        var ai = settings.Ai;
        ValidateEndpoint(ai);

        var client = httpClientFactory.CreateClient(ServiceCollectionExtensions.AiHttpClientName);
        client.BaseAddress = new Uri(EnsureTrailingSlash(ai.BaseUrl));
        client.Timeout = ai.Timeout;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secrets.AiApiKey);

        var userPromptPayload = new
        {
            server = new
            {
                name = prompt.ServerName,
                guidance = prompt.ServerTopicPrompt,
                rulesHint = prompt.RulesHint
            },
            user = new
            {
                name = prompt.UserName,
                id = prompt.UserId,
                attempt = prompt.AttemptNumber
            },
            answers = new
            {
                whyHere = prompt.Answers.WhyHere,
                whatDoYouWant = prompt.Answers.WhatDoYouWant,
                ruleParaphrase = prompt.Answers.RuleParaphrase
            }
        };
        var userPrompt = JsonSerializer.Serialize(userPromptPayload, JsonDefaults.Options);

        var requestBody = new
        {
            model = ai.Model,
            temperature = 0.2,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        using var response = await client.PostAsync(
            "chat/completions",
            new StringContent(JsonSerializer.Serialize(requestBody, JsonDefaults.Options), Encoding.UTF8, "application/json"),
            cancellationToken);

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("AI provider returned {StatusCode}: {Body}", response.StatusCode, raw);
            throw new InvalidOperationException($"AI provider returned {(int)response.StatusCode}: {raw}");
        }

        var content = ExtractMessageContent(raw);
        var payload = JsonSerializer.Deserialize<AiDecisionPayload>(content, JsonDefaults.Options)
            ?? throw new InvalidOperationException("AI provider returned an empty decision payload.");

        return new VerificationDecision(
            ParseOutcome(payload.Outcome),
            payload.Reason ?? "No reason supplied.",
            payload.FriendlyReply ?? "Thanks. Please try again in a moment.",
            Math.Clamp(payload.Confidence ?? 0.5, 0, 1),
            payload.SuggestedCooldownMinutes is > 0 ? TimeSpan.FromMinutes(payload.SuggestedCooldownMinutes.Value) : null);
    }

    private static VerificationOutcome ParseOutcome(string? rawOutcome) => rawOutcome?.Trim().ToLowerInvariant() switch
    {
        "approve" => VerificationOutcome.Approve,
        "retry" => VerificationOutcome.Retry,
        "uncertain" => VerificationOutcome.Uncertain,
        _ => VerificationOutcome.Uncertain
    };

    private static string ExtractMessageContent(string raw)
    {
        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;
        var firstChoice = root.GetProperty("choices")[0];
        var message = firstChoice.GetProperty("message");
        var content = message.GetProperty("content");

        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString() ?? throw new InvalidOperationException("AI response content was empty."),
            JsonValueKind.Array => string.Concat(content.EnumerateArray()
                .Where(item => item.TryGetProperty("text", out _))
                .Select(item => item.GetProperty("text").GetString())),
            _ => throw new InvalidOperationException("AI response content shape was not supported.")
        };
    }

    private static void ValidateEndpoint(AiProviderSettings ai)
    {
        var uri = new Uri(EnsureTrailingSlash(ai.BaseUrl));
        if (ai.RequireHttps && uri.Scheme != Uri.UriSchemeHttps && !(ai.AllowInsecureLocalEndpoint && uri.IsLoopback))
            throw new InvalidOperationException("The AI endpoint must use HTTPS unless an insecure local endpoint is explicitly allowed.");
    }

    private static string EnsureTrailingSlash(string value) => value.EndsWith('/') ? value : value + "/";

    private sealed class AiDecisionPayload
    {
        public string? Outcome { get; init; }
        public string? Reason { get; init; }
        public string? FriendlyReply { get; init; }
        public double? Confidence { get; init; }
        public int? SuggestedCooldownMinutes { get; init; }
    }
}
