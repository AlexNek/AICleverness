namespace AiCleverness.Models;

/// <summary>
/// Rich provenance — not just which model, but how it was chosen.
/// </summary>
public sealed record ModelResolutionResult
{
    /// <summary>How many profiles were tried before succeeding.</summary>
    public int Attempts { get; init; }

    /// <summary>Whether this result used a fallback profile.</summary>
    public bool IsFallback { get; init; }

    /// <summary>The selected model definition.</summary>
    public required ModelDefinition Model { get; init; }

    /// <summary>The capability profile that produced this model.</summary>
    public required CapabilityProfile Profile { get; init; }

    /// <summary>Human-readable reason for this selection (for diagnostics).</summary>
    public string? SelectionReason { get; init; }
}
