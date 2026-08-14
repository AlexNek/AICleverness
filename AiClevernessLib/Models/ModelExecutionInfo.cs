namespace AiCleverness.Models;

/// <summary>
/// Full provenance stored in the execution context. Every observer, journal,
/// cost tracker, and replay engine has full context.
/// </summary>
public sealed record ModelExecutionInfo
{
    public int Attempt { get; init; }

    public bool IsFallback { get; init; }

    public required ModelDefinition Model { get; init; }

    public required CapabilityProfile Profile { get; init; }

    public string? SelectionReason { get; init; }
}
