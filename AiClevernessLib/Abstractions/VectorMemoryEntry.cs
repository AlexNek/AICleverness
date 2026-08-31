namespace AiCleverness.Abstractions;

/// <summary>
/// An entry stored in vector memory.
/// </summary>
public sealed record VectorMemoryEntry(
    string Id,
    string Text,
    ReadOnlyMemory<float> Vector,
    IReadOnlyDictionary<string, string>? Metadata = null)
{
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        Metadata ?? new Dictionary<string, string>();
}
