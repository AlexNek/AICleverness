using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Entry point for model resolution. AgentRuntime calls this, nothing else.
/// Calls resolver → catalog → policy. Returns a rich result, not just a model name.
/// </summary>
public interface IModelManager
{
    ValueTask<ModelResolutionResult?> ResolveAsync(
        CapabilityRequirements requirements,
        CancellationToken ct = default);
}
