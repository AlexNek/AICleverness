namespace AiCleverness.Models;

/// <summary>Describes an execution scheduled for future or recurring invocation.</summary>
public sealed record ScheduledExecution
{
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool IsDue => IsEnabled && !IsExpired && DateTimeOffset.UtcNow >= NextRunAt;
    public bool IsEnabled { get; init; } = true;
    public bool IsExpired => MaxOccurrences.HasValue && OccurrenceCount >= MaxOccurrences.Value;
    public string? Label { get; init; }
    public int? MaxOccurrences { get; init; }
    public required DateTimeOffset NextRunAt { get; init; }
    public int OccurrenceCount { get; init; }
    public TimeSpan? RecurrenceInterval { get; init; }
    public required AgentRequest Request { get; init; }
    public string ScheduleId { get; init; } = Guid.NewGuid().ToString("N");
}
