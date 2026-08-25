namespace AiCleverness.Models.DecisionTree;

/// <summary>Default decision-tree settings used by dependency injection configuration.</summary>
public sealed class DecisionTreeExecutionOptions
{
    public int DefaultMaxNodeVisits { get; set; } = 20;
    public int DefaultMaxLlmCalls { get; set; } = 10;
    public TimeSpan DefaultMaxElapsedTime { get; set; } = TimeSpan.FromSeconds(120);
    public int DefaultMaxContextTokens { get; set; } = 4000;
    public string? TraceId { get; set; }
    public string? CorrelationId { get; set; }
}
