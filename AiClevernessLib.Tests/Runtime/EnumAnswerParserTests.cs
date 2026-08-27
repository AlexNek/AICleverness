using AiCleverness.Runtime.DecisionTree;
using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class EnumAnswerParserTests
{
    private static readonly IReadOnlyList<string> AllowedAnswers = ["yes", "no", "unknown"];

    [Fact]
    public void Parse_PlainJson_ReturnsAnswer()
    {
        var parser = new EnumAnswerParser();

        var result = parser.Parse("{\"answer\":\"yes\",\"observation\":\"found\",\"confidence\":\"high\"}", AllowedAnswers);

        result.Should().NotBeNull();
        result!.Value.Should().Be("yes");
        result.Observation.Should().Be("found");
        result.Confidence.Should().Be("high");
    }

    [Fact]
    public void Parse_JsonWrappedInMarkdownCodeFences_ReturnsAnswerAndOptionalFields()
    {
        var parser = new EnumAnswerParser();
        var fenced = "  \n```json\n{\"answer\":\"yes\",\"observation\":\"found\",\"confidence\":\"high\"}\n```\n  ";

        var result = parser.Parse(fenced, AllowedAnswers);

        result.Should().NotBeNull();
        result!.Value.Should().Be("yes");
        result.Observation.Should().Be("found");
        result.Confidence.Should().Be("high");
    }

    [Fact]
    public void Parse_JsonWrappedInBareCodeFences_ReturnsAnswer()
    {
        var parser = new EnumAnswerParser();
        var fenced = "```\n{\"answer\":\"no\"}\n```";

        var result = parser.Parse(fenced, AllowedAnswers);

        result.Should().NotBeNull();
        result!.Value.Should().Be("no");
    }

    [Fact]
    public void Parse_JsonWithLeadingAndTrailingWhitespace_ReturnsAnswer()
    {
        var parser = new EnumAnswerParser();
        var padded = "  \n  {\"answer\":\"yes\"}  \n  ";

        var result = parser.Parse(padded, AllowedAnswers);

        result.Should().NotBeNull();
        result!.Value.Should().Be("yes");
    }

    [Theory]
    [InlineData("```json\n{\"answer\":\"yes\"}")]
    [InlineData("```json\n{\"answer\":\"yes\"}\n``` trailing text")]
    [InlineData("```json {\"answer\":\"yes\"}")]
    public void Parse_MalformedCodeFence_ReturnsNull(string fenced)
    {
        var parser = new EnumAnswerParser();

        var result = parser.Parse(fenced, AllowedAnswers);

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_FencedAnswerNotInAllowedValues_ReturnsNull()
    {
        var parser = new EnumAnswerParser();

        var result = parser.Parse("```json\n{\"answer\":\"maybe\"}\n```", AllowedAnswers);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"answer\":123}")]
    public void Parse_InvalidRootOrAnswerType_ReturnsNull(string json)
    {
        var parser = new EnumAnswerParser();

        var result = parser.Parse(json, AllowedAnswers);

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_NullInput_ReturnsNull()
    {
        var parser = new EnumAnswerParser();

        var result = parser.Parse(null, AllowedAnswers);

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsNull()
    {
        var parser = new EnumAnswerParser();

        var result = parser.Parse("not-json", AllowedAnswers);

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_AnswerNotInAllowedValues_ReturnsNull()
    {
        var parser = new EnumAnswerParser();

        var result = parser.Parse("{\"answer\":\"maybe\"}", AllowedAnswers);

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_AnswerMatchesCaseInsensitive()
    {
        var parser = new EnumAnswerParser();

        var result = parser.Parse("{\"answer\":\"YES\"}", AllowedAnswers);

        result.Should().NotBeNull();
        result!.Value.Should().Be("yes");
    }
}
