using AiCleverness.Abstractions;

namespace AiCleverness.Models;

/// <summary>
/// Describes the capabilities required for an execution.
/// Used by <see cref="ICapabilityResolver"/> to select the appropriate model/provider.
/// Built by an agent via <see cref="IAgent.DetermineCapabilities"/>.
/// </summary>
public sealed record CapabilityRequirements
{
    /// <summary>Required capabilities expressed in the shared structured form.</summary>
    public Capabilities Capabilities { get; init; } = new();

    /// <summary>Custom properties for extension scenarios.</summary>
    public IReadOnlyDictionary<string, object> Properties { get; init; } =
        new Dictionary<string, object>();
}
