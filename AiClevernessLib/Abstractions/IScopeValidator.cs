using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Validates that a tool invocation operates within its declared scope.
/// </summary>
public interface IScopeValidator
{
    /// <summary>
    /// Validates the invocation arguments against the given scope constraints.
    /// </summary>
    Task<ScopeValidationResult> ValidateAsync(
        ITool tool,
        ToolInvocation invocation,
        ToolInputScope scope,
        CancellationToken cancellationToken = default);
}
