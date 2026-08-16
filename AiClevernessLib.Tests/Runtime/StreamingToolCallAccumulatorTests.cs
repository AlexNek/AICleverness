using AiCleverness.Models;
using AiCleverness.Runtime;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class StreamingToolCallAccumulatorTests
{
    [Fact]
    public void AddDeltas_SingleToolCall_AccumulatesFromMultipleFragments()
    {
        // Arrange
        var accumulator = new StreamingToolCallAccumulator();

        // Act
        accumulator.AddDeltas([new LlmToolCallDelta(0, Id: "call-1", Name: "search", ArgumentsFragment: "{\"q\":")]);
        accumulator.AddDeltas([new LlmToolCallDelta(0, ArgumentsFragment: "\"hello")]);
        accumulator.AddDeltas([new LlmToolCallDelta(0, ArgumentsFragment: "\"}")]);
        var result = accumulator.Build();

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("call-1");
        result[0].Name.Should().Be("search");
        result[0].Arguments.Should().Be("{\"q\":\"hello\"}");
    }

    [Fact]
    public void AddDeltas_MultipleConcurrentToolCalls_AccumulatesByIndex()
    {
        // Arrange
        var accumulator = new StreamingToolCallAccumulator();

        // Act
        accumulator.AddDeltas(
        [
            new LlmToolCallDelta(0, Id: "call-1", Name: "search", ArgumentsFragment: "{\"q\":"),
            new LlmToolCallDelta(1, Id: "call-2", Name: "calculate", ArgumentsFragment: "{\"expr\":")
        ]);
        accumulator.AddDeltas(
        [
            new LlmToolCallDelta(0, ArgumentsFragment: "\"AI\"}"),
            new LlmToolCallDelta(1, ArgumentsFragment: "\"2+2\"}")
        ]);
        var result = accumulator.Build();

        // Assert
        result.Should().HaveCount(2);
        result[0].Id.Should().Be("call-1");
        result[0].Name.Should().Be("search");
        result[0].Arguments.Should().Be("{\"q\":\"AI\"}");
        result[1].Id.Should().Be("call-2");
        result[1].Name.Should().Be("calculate");
        result[1].Arguments.Should().Be("{\"expr\":\"2+2\"}");
    }

    [Fact]
    public void AddDeltas_IncompleteFragments_BuildReturnsGeneratedIdAndName()
    {
        // Arrange
        var accumulator = new StreamingToolCallAccumulator();

        // Act — no Id or Name provided, just argument fragments
        accumulator.AddDeltas([new LlmToolCallDelta(0, ArgumentsFragment: "{\"partial\":")]);
        accumulator.AddDeltas([new LlmToolCallDelta(0, ArgumentsFragment: "true}")]);
        var result = accumulator.Build();

        // Assert — generates fallback id and name
        result.Should().HaveCount(1);
        result[0].Id.Should().Be("call_0");
        result[0].Name.Should().Be("unknown_0");
        result[0].Arguments.Should().Be("{\"partial\":true}");
    }

    [Fact]
    public void AddDeltas_NullOrEmpty_IsNoOp()
    {
        // Arrange
        var accumulator = new StreamingToolCallAccumulator();

        // Act
        accumulator.AddDeltas(null);
        accumulator.AddDeltas([]);

        // Assert
        accumulator.HasEntries.Should().BeFalse();
        accumulator.Build().Should().BeEmpty();
    }

    [Fact]
    public void Build_NoEntries_ReturnsEmpty()
    {
        // Arrange
        var accumulator = new StreamingToolCallAccumulator();

        // Act & Assert
        accumulator.Build().Should().BeEmpty();
    }

    [Fact]
    public void AddDeltas_OrderedByIndex_RegardlessOfInsertionOrder()
    {
        // Arrange
        var accumulator = new StreamingToolCallAccumulator();

        // Act — insert index 2 before index 0
        accumulator.AddDeltas([new LlmToolCallDelta(2, Id: "c3", Name: "third", ArgumentsFragment: "{}")]);
        accumulator.AddDeltas([new LlmToolCallDelta(0, Id: "c1", Name: "first", ArgumentsFragment: "{}")]);
        accumulator.AddDeltas([new LlmToolCallDelta(1, Id: "c2", Name: "second", ArgumentsFragment: "{}")]);
        var result = accumulator.Build();

        // Assert — ordered by index
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("first");
        result[1].Name.Should().Be("second");
        result[2].Name.Should().Be("third");
    }
}
