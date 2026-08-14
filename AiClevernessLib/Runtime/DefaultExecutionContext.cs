using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime;

/// <summary>
/// Default implementation of <see cref="IExecutionContext"/>.
/// Created once per agent execution and threaded through all components.
/// </summary>
public sealed class DefaultExecutionContext : IExecutionContext
{
    /// <inheritdoc/>
    public IAgentContext AgentContext { get; }

    /// <inheritdoc/>
    public IExecutionArtifactCollection Artifacts { get; } =
        new DefaultExecutionArtifactCollection();

    /// <inheritdoc/>
    public CancellationToken CancellationToken { get; }

    /// <inheritdoc/>
    public IExecutionItems Items { get; } = new DefaultExecutionItems();

    /// <inheritdoc/>
    public ExecutionMetadata Metadata { get; }

    /// <inheritdoc/>
    public ExecutionState State { get; } = new();

    /// <summary>
    /// Creates a new execution context.
    /// </summary>
    public DefaultExecutionContext(
        ExecutionMetadata metadata,
        IAgentContext agentContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(agentContext);

        Metadata = metadata;
        AgentContext = agentContext;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Factory method to create a fully-wired execution context from request data.
    /// </summary>
    public static DefaultExecutionContext Create(
        AgentRequest request,
        AgentRuntimeOptions options,
        IAgentContext agentContext,
        IReadOnlyList<string>? availableToolNames = null,
        CancellationToken cancellationToken = default)
    {
        var ids = ExecutionIds.Create();
        var metadata = ExecutionMetadata.Create(ids, request, options, availableToolNames);
        return new DefaultExecutionContext(metadata, agentContext, cancellationToken);
    }

    /// <summary>
    /// Creates a child execution context sharing trace/correlation identifiers.
    /// </summary>
    public DefaultExecutionContext CreateChild(
        AgentRequest request,
        AgentRuntimeOptions options,
        IAgentContext agentContext,
        IReadOnlyList<string>? availableToolNames = null,
        CancellationToken cancellationToken = default)
    {
        var parentIds = new ExecutionIds(
            Metadata.ExecutionId,
            Metadata.TraceId,
            Metadata.CorrelationId);

        var childIds = parentIds.CreateChild();
        var metadata = ExecutionMetadata.Create(childIds, request, options, availableToolNames);
        return new DefaultExecutionContext(metadata, agentContext, cancellationToken);
    }
}
