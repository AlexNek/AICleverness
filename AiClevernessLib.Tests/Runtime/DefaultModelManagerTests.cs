using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime.Capabilities;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class DefaultModelManagerTests
{
    private static readonly CapabilityProfile Profile = new()
    {
        Id = "test/text",
        Name = "Test Text",
        Priority = 100
    };

    private static readonly ModelDefinition ModelA =
        new() { Name = "model-a", ProviderKey = "test" };

    private static readonly ModelDefinition ModelB =
        new() { Name = "model-b", ProviderKey = "test" };

    private static readonly ModelDefinition Outsider =
        new() { Name = "model-x", ProviderKey = "test" };

    [Fact]
    public async Task Resolve_PolicyReturnsOutOfSetCandidate_RanksValidPicksAndStops()
    {
        // Arrange — policy first picks a valid candidate, then returns a model
        // that is not among the offered candidates.
        var policy = new ScriptedSelectionPolicy([ModelB, Outsider]);
        var manager = new DefaultModelManager(
            new DefaultCapabilityResolver([Profile]),
            new FixedCatalog([ModelA, ModelB]),
            policy);

        // Act
        var result = await manager.ResolveAsync(new CapabilityRequirements());

        // Assert — ranking stopped at the invalid pick; no repeated selections.
        result.Should().NotBeNull();
        result!.Model.Should().Be(ModelB);
        result.Fallbacks.Should().BeEmpty();
        policy.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Resolve_PolicyRanksAllCandidates_SelectedPlusFallbacks()
    {
        // Arrange
        var policy = new ScriptedSelectionPolicy([ModelB, ModelA]);
        var manager = new DefaultModelManager(
            new DefaultCapabilityResolver([Profile]),
            new FixedCatalog([ModelA, ModelB]),
            policy);

        // Act
        var result = await manager.ResolveAsync(new CapabilityRequirements());

        // Assert
        result.Should().NotBeNull();
        result!.Model.Should().Be(ModelB);
        result.Fallbacks.Should().ContainSingle().Which.Should().Be(ModelA);
        policy.CallCount.Should().Be(2); // loop ends when no candidates remain
    }

    private sealed class ScriptedSelectionPolicy(IEnumerable<ModelDefinition> picks)
        : IModelSelectionPolicy
    {
        private readonly Queue<ModelDefinition> _picks = new(picks);

        public int CallCount { get; private set; }

        public ValueTask<ModelDefinition?> SelectAsync(
            IReadOnlyList<ModelDefinition> candidates,
            CapabilityRequirements requirements,
            CancellationToken ct = default)
        {
            CallCount++;
            return new ValueTask<ModelDefinition?>(
                _picks.Count > 0 ? _picks.Dequeue() : null);
        }
    }

    private sealed class FixedCatalog(IReadOnlyList<ModelDefinition> candidates) : IModelCatalog
    {
        public ValueTask<IReadOnlyList<ModelDefinition>> GetCandidatesAsync(
            CapabilityProfile profile,
            CancellationToken ct = default)
        {
            return new ValueTask<IReadOnlyList<ModelDefinition>>(candidates);
        }
    }
}
