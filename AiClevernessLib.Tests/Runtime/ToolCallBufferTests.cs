using AiCleverness.Models;
using AiCleverness.Runtime;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class ToolCallBufferTests
{
    [Fact]
    public void Accumulate_ArrayJson_DetectsCompletion()
    {
        var buffer = new ToolCallBuffer();

        buffer.Accumulate([new StreamingToolCallUpdate("call-1", "multi", "[1,")]);
        buffer.FlushCompleted().Should().BeEmpty();

        buffer.Accumulate([new StreamingToolCallUpdate("call-1", null, "2,3]")]);
        var completed = buffer.FlushCompleted();

        completed.Should().HaveCount(1);
        completed[0].Arguments.Should().Be("[1,2,3]");
    }

    [Fact]
    public void Accumulate_EmptyUpdates_NoOp()
    {
        var buffer = new ToolCallBuffer();

        buffer.Accumulate([]);
        buffer.PendingCount.Should().Be(0);
    }

    [Fact]
    public void Accumulate_EscapedQuotes_DoesNotConfuse()
    {
        var buffer = new ToolCallBuffer();
        var json = "{\"text\":\"say \\\"hello\\\"\"}";

        buffer.Accumulate([new StreamingToolCallUpdate("call-1", "echo", json)]);
        var completed = buffer.FlushCompleted();

        completed.Should().HaveCount(1);
    }

    [Fact]
    public void Accumulate_FunctionName_SetOnFirstChunkOnly()
    {
        var buffer = new ToolCallBuffer();

        buffer.Accumulate([new StreamingToolCallUpdate("call-1", "search", "{\"q\":")]);
        buffer.Accumulate([new StreamingToolCallUpdate("call-1", null, "\"test\"}")]);

        var completed = buffer.FlushCompleted();
        completed[0].Name.Should().Be("search");
    }

    [Fact]
    public void Accumulate_MultipleChunks_FlushesWhenComplete()
    {
        var buffer = new ToolCallBuffer();

        buffer.Accumulate([new StreamingToolCallUpdate("call-1", "search", "{\"query\":")]);
        buffer.FlushCompleted().Should().BeEmpty();

        buffer.Accumulate([new StreamingToolCallUpdate("call-1", null, "\"hello\"}")]);
        var completed = buffer.FlushCompleted();

        completed.Should().HaveCount(1);
        completed[0].Arguments.Should().Be("{\"query\":\"hello\"}");
    }

    [Fact]
    public void Accumulate_MultipleConcurrentToolCalls_TracksIndependently()
    {
        var buffer = new ToolCallBuffer();

        buffer.Accumulate(
            [
                new StreamingToolCallUpdate("call-1", "search", "{\"q\":"),
                new StreamingToolCallUpdate("call-2", "calculate", "{\"expr\":")
            ]);
        buffer.FlushCompleted().Should().BeEmpty();
        buffer.PendingCount.Should().Be(2);

        buffer.Accumulate([new StreamingToolCallUpdate("call-1", null, "\"AI\"}")]);
        var first = buffer.FlushCompleted();
        first.Should().HaveCount(1);
        first[0].Name.Should().Be("search");
        buffer.PendingCount.Should().Be(1);

        buffer.Accumulate([new StreamingToolCallUpdate("call-2", null, "\"2+2\"}")]);
        var second = buffer.FlushCompleted();
        second.Should().HaveCount(1);
        second[0].Name.Should().Be("calculate");
        buffer.PendingCount.Should().Be(0);
    }

    [Fact]
    public void Accumulate_NestedJson_WaitsForFullCompletion()
    {
        var buffer = new ToolCallBuffer();

        buffer.Accumulate(
                [new StreamingToolCallUpdate("call-1", "complex", "{\"outer\":{\"inner\":")]);
        buffer.FlushCompleted().Should().BeEmpty();

        buffer.Accumulate([new StreamingToolCallUpdate("call-1", null, "\"value\"}}")]);
        var completed = buffer.FlushCompleted();

        completed.Should().HaveCount(1);
        completed[0].Arguments.Should().Be("{\"outer\":{\"inner\":\"value\"}}");
    }

    [Fact]
    public void Accumulate_NullUpdates_NoOp()
    {
        var buffer = new ToolCallBuffer();

        buffer.Accumulate(null);
        buffer.PendingCount.Should().Be(0);
    }

    [Fact]
    public void Accumulate_SingleChunk_CompleteJson_FlushesImmediately()
    {
        var buffer = new ToolCallBuffer();

        buffer.Accumulate(
            [
                new StreamingToolCallUpdate("call-1", "search", "{\"query\":\"hello\"}")
            ]);

        var completed = buffer.FlushCompleted();

        completed.Should().HaveCount(1);
        completed[0].Id.Should().Be("call-1");
        completed[0].Name.Should().Be("search");
        completed[0].Arguments.Should().Be("{\"query\":\"hello\"}");
    }

    [Fact]
    public void Accumulate_StringsWithBraces_DoesNotConfuse()
    {
        var buffer = new ToolCallBuffer();
        var json = "{\"text\":\"this has {braces} in it\"}";

        buffer.Accumulate([new StreamingToolCallUpdate("call-1", "echo", json)]);
        var completed = buffer.FlushCompleted();

        completed.Should().HaveCount(1);
        completed[0].Arguments.Should().Be(json);
    }

    [Fact]
    public void FlushAll_ForcesIncompleteFlush()
    {
        var buffer = new ToolCallBuffer();

        buffer.Accumulate([new StreamingToolCallUpdate("call-1", "search", "{\"q\":\"partial")]);
        buffer.FlushCompleted().Should().BeEmpty();

        var forced = buffer.FlushAll();
        forced.Should().HaveCount(1);
        forced[0].Name.Should().Be("search");
        forced[0].Arguments.Should().Be("{\"q\":\"partial");
        buffer.PendingCount.Should().Be(0);
    }

    [Fact]
    public void FlushAll_WithNoFunctionName_SkipsEntry()
    {
        var buffer = new ToolCallBuffer();

        // Accumulate with no function name set
        buffer.Accumulate([new StreamingToolCallUpdate("call-1", null, "partial")]);

        var forced = buffer.FlushAll();
        forced.Should().BeEmpty();
    }

    [Fact]
    public void FlushCompleted_RemovesCompletedFromBuffer()
    {
        var buffer = new ToolCallBuffer();

        buffer.Accumulate([new StreamingToolCallUpdate("call-1", "echo", "{\"msg\":\"hi\"}")]);
        buffer.FlushCompleted().Should().HaveCount(1);

        // Second flush should return nothing
        buffer.FlushCompleted().Should().BeEmpty();
    }
}
