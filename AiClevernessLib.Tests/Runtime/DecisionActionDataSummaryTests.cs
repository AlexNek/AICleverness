using AiCleverness.Models.DecisionTree;
using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class DecisionActionDataSummaryTests
{
    [Fact]
    public void Create_ReturnsNullForNullInput()
    {
        // Arrange
        IReadOnlyList<DecisionData>? data = null;

        // Act
        var summary = DecisionActionDataSummary.Create(data);

        // Assert
        summary.Should().BeNull();
    }

    [Fact]
    public void Create_ReturnsNullForEmptyInput()
    {
        // Arrange
        var data = Array.Empty<DecisionData>();

        // Act
        var summary = DecisionActionDataSummary.Create(data);

        // Assert
        summary.Should().BeNull();
    }

    [Fact]
    public void Create_ReportsCountTypeAndPreviewForSingleItem()
    {
        // Arrange
        var data = new[] { CreateData("CandidateLink", "https://test.example.com/item") };

        // Act
        var summary = DecisionActionDataSummary.Create(data);

        // Assert
        summary.Should().NotBeNull();
        summary!.ItemCount.Should().Be(1);
        summary.Types.Should().Equal("CandidateLink");
        summary.ContentPreviews.Should().Equal("https://test.example.com/item");
    }

    [Fact]
    public void Create_PreservesDistinctTypeOrderAndPreviewOrder()
    {
        // Arrange
        var data = new[]
        {
            CreateData("CandidateLink", "first"),
            CreateData("CandidateLink", "second"),
            CreateData("ModelList", "third")
        };

        // Act
        var summary = DecisionActionDataSummary.Create(data);

        // Assert
        summary.Should().NotBeNull();
        summary!.ItemCount.Should().Be(3);
        summary.Types.Should().Equal("CandidateLink", "ModelList");
        summary.ContentPreviews.Should().Equal("first", "second", "third");
    }

    [Fact]
    public void Create_TruncatesContentTo80CharactersIncludingEllipsis()
    {
        // Arrange
        var content = new string('x', 100);
        var data = new[] { CreateData("CandidateLink", content) };

        // Act
        var summary = DecisionActionDataSummary.Create(data);

        // Assert
        summary.Should().NotBeNull();
        summary!.ContentPreviews.Should().ContainSingle()
            .Which.Should().Be(new string('x', 79) + "…");
        summary.ContentPreviews[0].Should().HaveLength(80);
    }

    [Fact]
    public void Create_CapsContentPreviewsAtFiveItemsAndPreservesTotalCount()
    {
        // Arrange
        var data = Enumerable.Range(1, 10)
            .Select(index => CreateData("CandidateLink", $"item-{index}"))
            .ToArray();

        // Act
        var summary = DecisionActionDataSummary.Create(data);

        // Assert
        summary.Should().NotBeNull();
        summary!.ItemCount.Should().Be(10);
        summary.ContentPreviews.Should().Equal("item-1", "item-2", "item-3", "item-4", "item-5");
    }

    [Fact]
    public void Create_CapsDistinctTypesAtTenItems()
    {
        // Arrange
        var data = Enumerable.Range(1, 12)
            .Select(index => CreateData($"Type{index}", $"content-{index}"))
            .ToArray();

        // Act
        var summary = DecisionActionDataSummary.Create(data);

        // Assert
        summary.Should().NotBeNull();
        summary!.Types.Should().Equal(Enumerable.Range(1, 10).Select(index => $"Type{index}"));
    }

    [Fact]
    public void Create_TruncatesTypeTo80CharactersIncludingEllipsis()
    {
        // Arrange
        var type = new string('t', 100);
        var data = new[] { CreateData(type, "content") };

        // Act
        var summary = DecisionActionDataSummary.Create(data);

        // Assert
        summary.Should().NotBeNull();
        summary!.Types.Should().ContainSingle()
            .Which.Should().Be(new string('t', 79) + "…");
        summary.Types[0].Should().HaveLength(80);
    }

    [Fact]
    public void Constructor_CopiesCallerOwnedLists()
    {
        // Arrange
        var types = new List<string> { "original-type" };
        var previews = new List<string> { "original-preview" };
        var summary = new DecisionActionDataSummary(1, types, previews);

        // Act
        types[0] = "changed-type";
        previews[0] = "changed-preview";
        types.Add("another-type");
        previews.Add("another-preview");

        // Assert
        summary.Types.Should().Equal("original-type");
        summary.ContentPreviews.Should().Equal("original-preview");
    }

    [Fact]
    public void WithExpression_CopiesReplacementLists()
    {
        // Arrange
        var replacementTypes = new List<string> { "replacement-type" };
        var replacementPreviews = new List<string> { "replacement-preview" };
        var summary = new DecisionActionDataSummary(1, ["original-type"], ["original-preview"]);

        // Act
        var updated = summary with
        {
            Types = replacementTypes,
            ContentPreviews = replacementPreviews
        };
        replacementTypes[0] = "changed-type";
        replacementPreviews[0] = "changed-preview";

        // Assert
        updated.Types.Should().Equal("replacement-type");
        updated.ContentPreviews.Should().Equal("replacement-preview");
    }

    private static DecisionData CreateData(string type, string content)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Source = "test",
            Type = type,
            Content = content,
            CreatedAt = DateTimeOffset.UtcNow
        };
}
