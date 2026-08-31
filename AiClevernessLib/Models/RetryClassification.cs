namespace AiCleverness.Models;

/// <summary>Classifies an error for retry decisions.</summary>
public sealed record RetryClassification(
    RetryCategory Category,
    bool ShouldRetry,
    TimeSpan? SuggestedDelay = null,
    int? MaxRetries = null,
    string? Reason = null);
