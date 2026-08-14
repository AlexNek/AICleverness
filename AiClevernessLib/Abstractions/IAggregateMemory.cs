namespace AiCleverness.Abstractions;

/// <summary>
/// Composite memory interface providing access to all memory tiers.
/// Combines working memory (ephemeral, within-execution), long-term memory (persistent),
/// and vector memory (semantic similarity).
/// </summary>
/// <remarks>
/// <para>
/// This interface extends <see cref="IAgentMemory"/> to maintain backward compatibility.
/// Existing code that uses <see cref="IAgentMemory"/> continues to work unchanged.
/// </para>
/// <para>
/// The aggregate memory is the recommended way to access memory in new code,
/// as it provides typed access to each tier.
/// </para>
/// </remarks>
public interface IAggregateMemory : IAgentMemory
{
    /// <summary>Persistent memory surviving across executions.</summary>
    ILongTermMemory LongTerm { get; }

    /// <summary>Vector-based semantic memory for similarity search.</summary>
    IVectorMemory Vector { get; }

    /// <summary>Short-term memory for the current execution. Cleared on completion.</summary>
    IWorkingMemory Working { get; }
}
