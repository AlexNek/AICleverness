namespace AiCleverness.Abstractions;

/// <summary>
/// A result from a vector similarity search.
/// </summary>
public sealed record VectorSearchResult(
    VectorMemoryEntry Entry,
    double Score);
