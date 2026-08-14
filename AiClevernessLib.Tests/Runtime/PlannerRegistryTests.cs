using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class PlannerRegistryTests
{
    [Fact]
    public void Constructor_WithPlanners_RegistersAll()
    {
        var planners = new INamedAgentPlanner[]
                           {
                               new FakeNamedPlanner("One"), new FakeNamedPlanner("Two")
                           };
        var registry = new PlannerRegistry(planners);

        registry.Names.Should().HaveCount(2);
    }

    [Fact]
    public void GetAll_ReturnsAllRegistered()
    {
        var registry = new PlannerRegistry();
        registry.Register(new FakeNamedPlanner("A"));
        registry.Register(new FakeNamedPlanner("B"));

        registry.GetAll().Should().HaveCount(2);
    }

    [Fact]
    public void GetByTag_ReturnsMatchingPlanners()
    {
        var registry = new PlannerRegistry();
        registry.Register(new FakeNamedPlanner("Seq", ["sequential"]));
        registry.Register(new FakeNamedPlanner("Par", ["parallel"]));
        registry.Register(new FakeNamedPlanner("Both", ["sequential", "parallel"]));

        registry.GetByTag("sequential").Should().HaveCount(2);
        registry.GetByTag("parallel").Should().HaveCount(2);
        registry.GetByTag("unknown").Should().BeEmpty();
    }

    [Fact]
    public void GetPlanner_IsCaseInsensitive()
    {
        var registry = new PlannerRegistry();
        registry.Register(new FakeNamedPlanner("MyPlanner"));

        registry.GetPlanner("myplanner").Should().NotBeNull();
        registry.GetPlanner("MYPLANNER").Should().NotBeNull();
    }

    [Fact]
    public void GetPlanner_MissingName_ReturnsNull()
    {
        var registry = new PlannerRegistry();

        registry.GetPlanner("missing").Should().BeNull();
    }

    [Fact]
    public async Task NamedPlanner_CreatePlanAsync_ReturnsStructuredPlan()
    {
        var planner = new FakeNamedPlanner("Test");
        var request = new AgentRequest("Build a house");
        var context = CreateContext();

        var plan = await planner.CreatePlanAsync(request, context);

        plan.PlannerName.Should().Be("Test");
        plan.Goal.Should().Be("Build a house");
        plan.Steps.Should().HaveCount(1);
        plan.Steps[0].Name.Should().Be("fake-step");
    }

    [Fact]
    public void Names_ReturnsAllPlannerNames()
    {
        var registry = new PlannerRegistry();
        registry.Register(new FakeNamedPlanner("Alpha"));
        registry.Register(new FakeNamedPlanner("Beta"));

        registry.Names.Should().BeEquivalentTo("Alpha", "Beta");
    }

    [Fact]
    public void Register_And_GetPlanner_ReturnsSamePlanner()
    {
        var registry = new PlannerRegistry();
        var planner = new FakeNamedPlanner("TestPlanner");

        registry.Register(planner);

        registry.GetPlanner("TestPlanner").Should().BeSameAs(planner);
    }

    private static DefaultAgentContext CreateContext() =>
        new() { Goal = "test", State = new AgentState(), Memory = new InMemoryAgentMemory() };

    private sealed class FakeNamedPlanner : INamedAgentPlanner
    {
        public PlannerMetadata Metadata { get; }

        public string Name { get; }

        public FakeNamedPlanner(string name, IReadOnlyList<string>? tags = null)
        {
            Name = name;
            Metadata = new PlannerMetadata
                           {
                               Name = name,
                               Description = $"Fake planner: {name}",
                               RequiresLlm = false,
                               Tags = tags ?? Array.Empty<string>()
                           };
        }

        public Task<ExecutionPlan> CreatePlanAsync(
            AgentRequest request,
            IAgentContext context,
            CancellationToken cancellationToken = default)
        {
            var plan = new ExecutionPlan
                           {
                               PlannerName = Name,
                               Steps = [new PlannedStep("fake-step", "action", "Do the thing")],
                               Goal = request.Goal
                           };
            return Task.FromResult(plan);
        }

        public Task<IReadOnlyList<PlannedStep>> PlanAsync(
            AgentRequest request,
            IAgentContext context,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<PlannedStep> steps =
                    [new PlannedStep("fake-step", "action", "Do the thing")];
            return Task.FromResult(steps);
        }
    }
}
