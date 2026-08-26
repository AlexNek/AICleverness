namespace AiCleverness.Models.DecisionTree;

/// <summary>Generic evidence or other data produced during a decision run.</summary>
public sealed record DecisionData
{
    public required string Id { get; init; }
    public required string Source { get; init; }
    public required string Type { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string? ActionId { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
