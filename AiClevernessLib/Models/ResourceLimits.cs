namespace AiCleverness.Models;

/// <summary>Enforcement limits for resource consumption during an execution.</summary>
public sealed record ResourceLimits
{
    public decimal? MaxCost { get; init; }
    public TimeSpan? MaxDuration { get; init; }
    public int? MaxNodeVisits { get; init; }
    public int? MaxLlmCalls { get; init; }
    public int? MaxToolCalls { get; init; }
    public int? MaxTotalTokens { get; init; }
    public ResourceLimitAction OnExceeded { get; init; } = ResourceLimitAction.Halt;
    public static ResourceLimits Unlimited => new();
}
