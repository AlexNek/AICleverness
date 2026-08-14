using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class StrategyRegistryTests
{
    [Fact]
    public void Constructor_WithStrategies_RegistersAll()
    {
        var strategies = new IAgentStrategy[] { new FakeStrategy("A"), new FakeStrategy("B") };
        var registry = new StrategyRegistry(strategies);

        registry.Names.Should().HaveCount(2);
    }

    [Fact]
    public void GetAll_ReturnsAllRegistered()
    {
        var registry = new StrategyRegistry();
        registry.Register(new FakeStrategy("A"));
        registry.Register(new FakeStrategy("B"));
        registry.Register(new FakeStrategy("C"));

        registry.GetAll().Should().HaveCount(3);
    }

    [Fact]
    public void GetApplicable_ReturnsOnlyMatchingStrategies()
    {
        var registry = new StrategyRegistry();
        registry.Register(new FakeStrategy("Always", canExecute: true));
        registry.Register(new FakeStrategy("Never", canExecute: false));
        registry.Register(new FakeStrategy("Also", canExecute: true));

        var context = CreateContext();
        registry.GetApplicable(context).Should().HaveCount(2);
    }

    [Fact]
    public void GetStrategy_IsCaseInsensitive()
    {
        var registry = new StrategyRegistry();
        registry.Register(new FakeStrategy("MyStrategy"));

        registry.GetStrategy("mystrategy").Should().NotBeNull();
    }

    [Fact]
    public void GetStrategy_MissingName_ReturnsNull()
    {
        var registry = new StrategyRegistry();

        registry.GetStrategy("nope").Should().BeNull();
    }

    [Fact]
    public void Names_ReturnsAllStrategyNames()
    {
        var registry = new StrategyRegistry();
        registry.Register(new FakeStrategy("X"));
        registry.Register(new FakeStrategy("Y"));

        registry.Names.Should().BeEquivalentTo("X", "Y");
    }

    [Fact]
    public void Register_And_GetStrategy_ReturnsSameStrategy()
    {
        var registry = new StrategyRegistry();
        var strategy = new FakeStrategy("Cache");

        registry.Register(strategy);

        registry.GetStrategy("Cache").Should().BeSameAs(strategy);
    }

    private static DefaultAgentContext CreateContext() =>
        new() { Goal = "test", State = new AgentState(), Memory = new InMemoryAgentMemory() };

    private sealed class FakeStrategy : IAgentStrategy
    {
        private readonly bool _canExecute;

        public string Name { get; }

        public FakeStrategy(string name, bool canExecute = true)
        {
            Name = name;
            _canExecute = canExecute;
        }

        public bool CanExecute(IAgentContext context) => _canExecute;

        public Task<StrategyResult> ExecuteAsync(
            IAgentContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new StrategyResult(true, $"Result from {Name}"));
        }
    }
}
