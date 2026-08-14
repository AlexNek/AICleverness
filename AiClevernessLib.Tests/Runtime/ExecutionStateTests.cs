using AiCleverness.Models;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public class ExecutionStateTests
{
    [Fact]
    public void Defaults_ArePending()
    {
        var state = new ExecutionState();

        state.Status.Should().Be(ExecutionStatus.Pending);
        state.StartedAt.Should().BeNull();
        state.CompletedAt.Should().BeNull();
        state.Duration.Should().BeNull();
        state.TurnCount.Should().Be(0);
        state.QualityRetryCount.Should().Be(0);
        state.ToolRetryCount.Should().Be(0);
        state.ToolInvocationCount.Should().Be(0);
        state.StatusDetail.Should().BeNull();
    }

    [Fact]
    public void Duration_WhenCompleted_ReturnsTotalDuration()
    {
        var state = new ExecutionState();
        state.MarkStarted();
        state.MarkCompleted(ExecutionStatus.Completed);

        state.Duration.Should().NotBeNull();
        state.Duration!.Value.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void Duration_WhenRunning_ReturnsElapsedSinceStart()
    {
        var state = new ExecutionState();
        state.MarkStarted();

        state.Duration.Should().NotBeNull();
        state.Duration!.Value.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void IncrementQualityRetry_IncrementsCounter()
    {
        var state = new ExecutionState();

        state.IncrementQualityRetry();
        state.IncrementQualityRetry();

        state.QualityRetryCount.Should().Be(2);
    }

    [Fact]
    public void IncrementToolInvocation_IncrementsCounter()
    {
        var state = new ExecutionState();

        state.IncrementToolInvocation();
        state.IncrementToolInvocation();

        state.ToolInvocationCount.Should().Be(2);
    }

    [Fact]
    public void IncrementToolRetry_IncrementsCounter()
    {
        var state = new ExecutionState();

        state.IncrementToolRetry();

        state.ToolRetryCount.Should().Be(1);
    }

    [Fact]
    public void IncrementTurn_IncrementsCounter()
    {
        var state = new ExecutionState();

        state.IncrementTurn();
        state.IncrementTurn();
        state.IncrementTurn();

        state.TurnCount.Should().Be(3);
    }

    [Fact]
    public void MarkCompleted_SetsStatusAndTimestamp()
    {
        var state = new ExecutionState();
        state.MarkStarted();

        state.MarkCompleted(ExecutionStatus.Completed);

        state.Status.Should().Be(ExecutionStatus.Completed);
        state.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkStarted_SetsStatusAndTimestamp()
    {
        var state = new ExecutionState();

        state.MarkStarted();

        state.Status.Should().Be(ExecutionStatus.Running);
        state.StartedAt.Should().NotBeNull();
        state.StartedAt!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ToSnapshot_MapsFieldsCorrectly()
    {
        var state = new ExecutionState();
        state.MarkStarted();
        state.IncrementTurn();
        state.IncrementTurn();
        state.IncrementQualityRetry();
        state.IncrementToolRetry();
        state.MarkCompleted(ExecutionStatus.Completed);

        var snapshot = state.ToSnapshot("corr-123");

        snapshot.Status.Should().Be(ExecutionStatus.Completed);
        snapshot.CorrelationId.Should().Be("corr-123");
        snapshot.StartedAt.Should().Be(state.StartedAt);
        snapshot.CompletedAt.Should().Be(state.CompletedAt);
        snapshot.TurnCount.Should().Be(2);
        snapshot.QualityRetryCount.Should().Be(1);
        snapshot.ToolRetryCount.Should().Be(1);
    }
}
