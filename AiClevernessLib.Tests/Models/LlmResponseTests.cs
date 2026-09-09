using AiCleverness.Models;

using FluentAssertions;

namespace AiClevernessLib.Tests.Models;

public sealed class LlmResponseTests
{
    [Fact]
    public void Constructor_WithReasoningContent_PreservesValue()
    {
        // Arrange
        const string reasoning = "The user asked X, so I concluded Y.";

        // Act
        var response = new LlmResponse("final answer", ReasoningContent: reasoning);

        // Assert
        response.Content.Should().Be("final answer");
        response.ReasoningContent.Should().Be(reasoning);
    }

    [Fact]
    public void Constructor_WithoutReasoningContent_DefaultsToNull()
    {
        // Act
        var response = new LlmResponse("final answer");

        // Assert
        response.ReasoningContent.Should().BeNull();
    }

    [Fact]
    public void Constructor_PositionalToolCalls_RemainsSourceCompatible()
    {
        // Arrange
        var toolCalls = new[] { new LlmToolCall("call-1", "echo", "{}") };

        // Act — existing positional call site: ToolCalls is still the second argument.
        var response = new LlmResponse(null, toolCalls);

        // Assert
        response.Content.Should().BeNull();
        response.ToolCalls.Should().BeSameAs(toolCalls);
        response.ReasoningContent.Should().BeNull();
    }
}
