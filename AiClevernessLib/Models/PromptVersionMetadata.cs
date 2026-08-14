namespace AiCleverness.Models;

/// <summary>
/// Version metadata for a prompt template.
/// Enables prompt versioning, A/B testing, and audit trails.
/// </summary>
public sealed record PromptVersionMetadata
{
    /// <summary>Author of this version.</summary>
    public string? Author { get; init; }

    /// <summary>Description of changes in this version.</summary>
    public string? ChangeDescription { get; init; }

    /// <summary>UTC timestamp when this version was created.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Whether this is the active/default version.</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>Optional tags for categorization (e.g., "production", "experimental").</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>Semantic version string (e.g., "1.2.0").</summary>
    public required string VersionString { get; init; }
}
