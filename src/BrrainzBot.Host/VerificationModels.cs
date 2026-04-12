namespace BrrainzBot.Host;

public enum VerificationOutcome
{
    Approve,
    Retry,
    Uncertain
}

public sealed record VerificationAnswers(string WhyHere, string WhatDoYouWant, string RuleParaphrase);

public sealed record VerificationPrompt(
    string ServerName,
    string ServerTopicPrompt,
    string RulesHint,
    string UserName,
    ulong UserId,
    int AttemptNumber,
    VerificationAnswers Answers);

public sealed record VerificationDecision(
    VerificationOutcome Outcome,
    string Reason,
    string FriendlyReply,
    double Confidence,
    TimeSpan? SuggestedCooldown);

public sealed class VerificationSession
{
    public required ulong ServerId { get; init; }
    public required ulong UserId { get; init; }
    public required string UserName { get; init; }
    public required DateTimeOffset JoinedAt { get; init; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? CooldownUntil { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string? LastDecisionReason { get; set; }
    public VerificationOutcome? LastOutcome { get; set; }
    public List<string> History { get; init; } = [];
}
