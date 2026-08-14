namespace AiCleverness.Abstractions;

/// <summary>
/// Registry for discovering and selecting strategies by name or criteria.
/// </summary>
public interface IStrategyRegistry
{
    /// <summary>Gets all strategy names.</summary>
    IReadOnlyCollection<string> Names { get; }

    /// <summary>Gets all registered strategies.</summary>
    IReadOnlyList<IAgentStrategy> GetAll();

    /// <summary>Gets strategies that can execute in the given context.</summary>
    IReadOnlyList<IAgentStrategy> GetApplicable(IAgentContext context);

    /// <summary>Gets a strategy by name. Returns null if not found.</summary>
    IAgentStrategy? GetStrategy(string name);

    /// <summary>Registers a strategy.</summary>
    void Register(IAgentStrategy strategy);
}
