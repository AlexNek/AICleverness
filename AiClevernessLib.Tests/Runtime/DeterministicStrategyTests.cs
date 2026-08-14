using AiCleverness.Models;
using AiCleverness.Runtime;
using AiCleverness.Runtime.Strategies;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class DeterministicStrategyTests
{
    public sealed class CachedResultStrategyTests
    {
        [Fact]
        public async Task CanExecute_IsCaseInsensitive()
        {
            var strategy = new CachedResultStrategy();
            strategy.AddEntry("Hello World", "result");

            // CachedResultStrategy uses OrdinalIgnoreCase
            strategy.CanExecute(CreateContext("hello world")).Should().BeTrue();
        }

        [Fact]
        public async Task CanExecute_WhenGoalIsCached_ReturnsTrue()
        {
            var strategy = new CachedResultStrategy();
            strategy.AddEntry("What is 2+2?", "4");

            var context = CreateContext("What is 2+2?");

            strategy.CanExecute(context).Should().BeTrue();
        }

        [Fact]
        public async Task CanExecute_WhenGoalIsNotCached_ReturnsFalse()
        {
            var strategy = new CachedResultStrategy();

            var context = CreateContext("Unknown question");

            strategy.CanExecute(context).Should().BeFalse();
        }

        [Fact]
        public void Clear_RemovesAllEntries()
        {
            var strategy = new CachedResultStrategy();
            strategy.AddEntry("a", "1");
            strategy.AddEntry("b", "2");

            strategy.Clear();

            strategy.CanExecute(CreateContext("a")).Should().BeFalse();
            strategy.CanExecute(CreateContext("b")).Should().BeFalse();
        }

        [Fact]
        public async Task ExecuteAsync_ReturnsCachedOutput()
        {
            var strategy = new CachedResultStrategy();
            strategy.AddEntry("hello", "world");

            var context = CreateContext("hello");
            var result = await strategy.ExecuteAsync(context);

            result.Success.Should().BeTrue();
            result.Output.Should().Be("world");
        }

        [Fact]
        public async Task ExecuteAsync_WhenNotCached_ReturnsFailed()
        {
            var strategy = new CachedResultStrategy();

            var context = CreateContext("unknown");
            var result = await strategy.ExecuteAsync(context);

            result.Success.Should().BeFalse();
        }

        [Fact]
        public void RemoveEntry_RemovesCachedGoal()
        {
            var strategy = new CachedResultStrategy();
            strategy.AddEntry("key", "value");

            strategy.RemoveEntry("key").Should().BeTrue();
            strategy.CanExecute(CreateContext("key")).Should().BeFalse();
        }
    }

    public sealed class RuleBasedStrategyTests
    {
        [Fact]
        public async Task AddRule_WithContextAccess_UsesContextData()
        {
            var strategy = new RuleBasedStrategy("ContextAware")
                .AddRule(
                    ctx => ctx.Goal.Contains("uppercase"),
                    ctx => ctx.Goal.ToUpperInvariant());

            var context = CreateContext("make this uppercase");
            var result = await strategy.ExecuteAsync(context);

            result.Success.Should().BeTrue();
            result.Output.Should().Be("MAKE THIS UPPERCASE");
        }

        [Fact]
        public async Task CanExecute_WhenNoRuleMatches_ReturnsFalse()
        {
            var strategy = new RuleBasedStrategy("TestRules")
                .AddGoalPrefixRule("greet:", "Hello!");

            var context = CreateContext("calculate something");

            strategy.CanExecute(context).Should().BeFalse();
        }

        [Fact]
        public async Task CanExecute_WhenRuleMatches_ReturnsTrue()
        {
            var strategy = new RuleBasedStrategy("TestRules")
                .AddGoalPrefixRule("greet:", "Hello!");

            var context = CreateContext("greet: user");

            strategy.CanExecute(context).Should().BeTrue();
        }

        [Fact]
        public async Task ExecuteAsync_FirstMatchWins()
        {
            var strategy = new RuleBasedStrategy("TestRules")
                .AddRule(_ => true, _ => "first")
                .AddRule(_ => true, _ => "second");

            var context = CreateContext("anything");
            var result = await strategy.ExecuteAsync(context);

            result.Output.Should().Be("first");
        }

        [Fact]
        public async Task ExecuteAsync_NoMatchingRule_ReturnsFailed()
        {
            var strategy = new RuleBasedStrategy("Empty");

            var context = CreateContext("anything");
            var result = await strategy.ExecuteAsync(context);

            result.Success.Should().BeFalse();
        }

        [Fact]
        public async Task ExecuteAsync_ReturnsFirstMatchingRuleOutput()
        {
            var strategy = new RuleBasedStrategy("TestRules")
                .AddGoalPrefixRule("greet:", "Hello!")
                .AddGoalPrefixRule("bye:", "Goodbye!");

            var context = CreateContext("bye: user");
            var result = await strategy.ExecuteAsync(context);

            result.Success.Should().BeTrue();
            result.Output.Should().Be("Goodbye!");
        }

        [Fact]
        public void Name_ReturnsConfiguredName()
        {
            var strategy = new RuleBasedStrategy("MyRules");

            strategy.Name.Should().Be("MyRules");
        }
    }

    private static DefaultAgentContext CreateContext(string goal) =>
        new() { Goal = goal, State = new AgentState(), Memory = new InMemoryAgentMemory() };
}
