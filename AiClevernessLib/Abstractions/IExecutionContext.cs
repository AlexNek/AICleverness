using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Unified execution context available throughout the lifecycle of a single agent run.
/// Combines immutable metadata, mutable state, typed items, and artifact storage.
/// </summary>
/// <remarks>
/// <para>
/// This context is created once per execution and threaded through policies, planners,
/// strategies, quality gates, validators, transformers, and observers. It supersedes
/// the ad-hoc property bag in <see cref="IAgentContext"/> with strongly typed collections.
/// </para>
/// <para>
/// Implementations must be thread-safe for concurrent observer/middleware access.
/// </para>
/// </remarks>
public interface IExecutionContext
{
    /// <summary>The original agent context for backward compatibility.</summary>
    IAgentContext AgentContext { get; }

    /// <summary>Collection of artifacts produced during execution.</summary>
    IExecutionArtifactCollection Artifacts { get; }

    /// <summary>Cancellation token for this execution.</summary>
    CancellationToken CancellationToken { get; }

    /// <summary>Typed item collection for intermediate data produced during execution.</summary>
    IExecutionItems Items { get; }

    /// <summary>Immutable metadata describing this execution (ids, request, options).</summary>
    ExecutionMetadata Metadata { get; }

    /// <summary>Mutable execution state that tracks lifecycle progress.</summary>
    ExecutionState State { get; }
}
