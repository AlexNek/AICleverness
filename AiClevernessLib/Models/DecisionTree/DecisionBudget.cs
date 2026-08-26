namespace AiCleverness.Models.DecisionTree;

/// <summary>Execution and prompt limits for a decision tree.</summary>
public sealed record DecisionBudget
{
    private int _maxNodeVisits = 20;
    private int _maxLlmCalls = 10;
    private TimeSpan _maxElapsedTime = TimeSpan.FromSeconds(120);
    private int _maxContextTokens = 4000;
    private bool _maxNodeVisitsSpecified;
    private bool _maxLlmCallsSpecified;
    private bool _maxElapsedTimeSpecified;
    private bool _maxContextTokensSpecified;

    public int MaxNodeVisits
    {
        get => _maxNodeVisits;
        init
        {
            _maxNodeVisits = value;
            _maxNodeVisitsSpecified = true;
        }
    }

    public int MaxLlmCalls
    {
        get => _maxLlmCalls;
        init
        {
            _maxLlmCalls = value;
            _maxLlmCallsSpecified = true;
        }
    }

    public TimeSpan MaxElapsedTime
    {
        get => _maxElapsedTime;
        init
        {
            _maxElapsedTime = value;
            _maxElapsedTimeSpecified = true;
        }
    }

    public int MaxContextTokens
    {
        get => _maxContextTokens;
        init
        {
            _maxContextTokens = value;
            _maxContextTokensSpecified = true;
        }
    }

    public ResourceLimitAction OnExceeded { get; init; } = ResourceLimitAction.Halt;

    internal bool HasMaxNodeVisits => _maxNodeVisitsSpecified;

    internal bool HasMaxLlmCalls => _maxLlmCallsSpecified;

    internal bool HasMaxElapsedTime => _maxElapsedTimeSpecified;

    internal bool HasMaxContextTokens => _maxContextTokensSpecified;
}