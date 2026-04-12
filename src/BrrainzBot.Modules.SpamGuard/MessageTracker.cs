using System.Collections.Concurrent;

namespace BrrainzBot.Modules.SpamGuard;

public sealed class MessageTracker(int deltaInterval, int minimumMessageLength, bool linkRequired, double similarityThreshold, ulong honeypotChannelId)
{
    private readonly ConcurrentDictionary<ulong, List<TrackedMessage>> _userMessages = new();
    private readonly ConcurrentDictionary<ulong, DateTimeOffset> _honeypotDetectedUsers = new();

    public (SpamDetectionResult Result, ulong FirstChannelId) CheckMessage(
        ulong userId,
        ulong channelId,
        string content,
        DateTimeOffset timestamp)
    {
        if (string.IsNullOrWhiteSpace(content))
            return (SpamDetectionResult.Ignored, 0);

        CleanupHoneypotDetections(timestamp, deltaInterval);

        if (_honeypotDetectedUsers.TryGetValue(userId, out var detectionTime) &&
            (timestamp - detectionTime).TotalSeconds <= deltaInterval)
            return (SpamDetectionResult.HoneypotDetected, 0);

        if (channelId == honeypotChannelId)
        {
            _honeypotDetectedUsers[userId] = timestamp;
            return (SpamDetectionResult.HoneypotTriggered, 0);
        }

        if (!ShouldTrackMessage(content, minimumMessageLength, linkRequired))
            return (SpamDetectionResult.Ignored, 0);

        var (isDuplicate, firstChannelId) = CheckForDuplicate(userId, channelId, content, timestamp, deltaInterval, similarityThreshold);
        if (isDuplicate)
            return (SpamDetectionResult.DuplicateDetected, firstChannelId);

        AddMessage(userId, channelId, content, timestamp, deltaInterval);
        return (SpamDetectionResult.Clean, 0);
    }

    public void PerformPeriodicCleanup(DateTimeOffset currentTime, int currentDeltaInterval)
    {
        CleanupHoneypotDetections(currentTime, currentDeltaInterval);
        foreach (var userId in _userMessages.Keys.ToArray())
        {
            PurgeOldMessages(userId, currentTime, currentDeltaInterval);
        }
    }

    private static bool ShouldTrackMessage(string content, int minimumMessageLength, bool linkRequired)
    {
        if (content.Length < minimumMessageLength)
            return false;

        return !linkRequired || ContainsLink(content);
    }

    private void AddMessage(ulong userId, ulong channelId, string content, DateTimeOffset timestamp, int currentDeltaInterval)
    {
        PurgeOldMessages(userId, timestamp, currentDeltaInterval);
        var messages = _userMessages.GetOrAdd(userId, _ => []);
        lock (messages)
        {
            messages.Add(new TrackedMessage(channelId, content, timestamp));
        }
    }

    private (bool IsDuplicate, ulong FirstChannelId) CheckForDuplicate(
        ulong userId,
        ulong currentChannelId,
        string content,
        DateTimeOffset timestamp,
        int currentDeltaInterval,
        double similarityThreshold)
    {
        if (!_userMessages.TryGetValue(userId, out var messages))
            return (false, 0);

        lock (messages)
        {
            foreach (var message in messages)
            {
                if (message.ChannelId == currentChannelId)
                    continue;

                if ((timestamp - message.Timestamp).TotalSeconds > currentDeltaInterval)
                    continue;

                if (AreSimilar(content, message.Content, similarityThreshold))
                    return (true, message.ChannelId);
            }
        }

        return (false, 0);
    }

    private void PurgeOldMessages(ulong userId, DateTimeOffset currentTime, int currentDeltaInterval)
    {
        if (!_userMessages.TryGetValue(userId, out var messages))
            return;

        lock (messages)
        {
            _ = messages.RemoveAll(message => (currentTime - message.Timestamp).TotalSeconds > currentDeltaInterval);
            if (messages.Count == 0)
                _ = _userMessages.TryRemove(userId, out _);
        }
    }

    private void CleanupHoneypotDetections(DateTimeOffset currentTime, int currentDeltaInterval)
    {
        var expiredUsers = _honeypotDetectedUsers
            .Where(kvp => (currentTime - kvp.Value).TotalSeconds > currentDeltaInterval)
            .Select(kvp => kvp.Key)
            .ToArray();

        foreach (var userId in expiredUsers)
        {
            _ = _honeypotDetectedUsers.TryRemove(userId, out _);
        }
    }

    private static bool ContainsLink(string content)
    {
        if (content.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("https://", StringComparison.OrdinalIgnoreCase))
            return true;

        if (content.Contains("www.", StringComparison.OrdinalIgnoreCase))
        {
            var wwwIndex = content.IndexOf("www.", StringComparison.OrdinalIgnoreCase);
            if (wwwIndex + 4 < content.Length && content.IndexOf('.', wwwIndex + 4) > wwwIndex)
                return true;
        }

        var urlPattern = System.Text.RegularExpressions.Regex.Match(content, @"\b[a-z0-9-]+\.[a-z]{2,}(/\S*)?\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return urlPattern.Success;
    }

    private static bool AreSimilar(string left, string right, double similarityThreshold)
    {
        var normalizedLeft = left.ToLowerInvariant().Trim();
        var normalizedRight = right.ToLowerInvariant().Trim();
        var distance = LevenshteinDistance(normalizedLeft, normalizedRight);
        var maxLength = Math.Max(normalizedLeft.Length, normalizedRight.Length);
        if (maxLength == 0)
            return true;

        var similarity = 1.0 - (double)distance / maxLength;
        return similarity >= similarityThreshold;
    }

    private static int LevenshteinDistance(string left, string right)
    {
        if (left.Length == 0)
            return right.Length;
        if (right.Length == 0)
            return left.Length;

        const int maxComparisonLength = 500;
        if (left.Length > maxComparisonLength)
            left = left[..maxComparisonLength];
        if (right.Length > maxComparisonLength)
            right = right[..maxComparisonLength];

        var distances = new int[left.Length + 1, right.Length + 1];
        for (var i = 0; i <= left.Length; i++)
            distances[i, 0] = i;
        for (var j = 0; j <= right.Length; j++)
            distances[0, j] = j;

        for (var i = 1; i <= left.Length; i++)
        {
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                distances[i, j] = Math.Min(
                    Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + cost);
            }
        }

        return distances[left.Length, right.Length];
    }

    private sealed record TrackedMessage(ulong ChannelId, string Content, DateTimeOffset Timestamp);
}

public enum SpamDetectionResult
{
    Clean,
    Ignored,
    HoneypotTriggered,
    HoneypotDetected,
    DuplicateDetected
}
