using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime.DecisionTree;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class DefaultDecisionDataPolicyTests
{
    [Fact]
    public void Select_BoundsItemsContentAndAddsSelectionMarker()
    {
        var policy = new DefaultDecisionDataPolicy(
            new DecisionDataPolicyOptions
            {
                MaxItems = 1,
                MaxContentLengthPerItem = 20,
                MaxAggregateRepresentationLength = 250,
                MaxFieldLength = 50
            });
        var first = Data("first", "first evidence that is long");
        var second = Data("second", "second evidence");

        var selection = policy.Select(
            [first, second],
            new DecisionDataSelectionContext(
                new DecisionTree { TreeId = "tree", StartNodeId = "classify" },
                new DecisionNode { Type = EDecisionNodeType.Classify },
                new DecisionState(),
                new Dictionary<string, string>()));

        selection.OmittedItemCount.Should().Be(1);
        selection.TruncatedItemCount.Should().Be(1);
        selection.Items.Should().HaveCount(2);
        selection.Items[0].Id.Should().Be("first");
        selection.Items[0].Content.Should().Contain("truncated");
        selection.Items[0].Content.Length.Should().BeLessThanOrEqualTo(20);
        selection.Items[1].Type.Should().Be("selection");
        selection.Items[1].Content.Should().Contain("omitted 1");
    }

    [Fact]
    public void Select_AppliesTypeAndSourceFiltersInInputOrder()
    {
        var policy = new DefaultDecisionDataPolicy(
            new DecisionDataPolicyOptions
            {
                IncludedTypes = new HashSet<string>(StringComparer.Ordinal) { "allowed" },
                IncludedSources = new HashSet<string>(StringComparer.Ordinal) { "source-a" }
            });

        var selection = policy.Select(
            [
                Data("first", "one", "source-a", "allowed"),
                Data("second", "two", "source-a", "other"),
                Data("third", "three", "source-b", "allowed")
            ],
            new DecisionDataSelectionContext(
                new DecisionTree { TreeId = "tree", StartNodeId = "classify" },
                new DecisionNode { Type = EDecisionNodeType.Classify },
                new DecisionState(),
                new Dictionary<string, string>()));

        selection.IsComplete.Should().BeTrue();
        selection.Items.Should().ContainSingle().Which.Id.Should().Be("first");
    }

    [Fact]
    public void Select_PreservesMetadataWhenTruncationCannotProduceUniqueMarkerKey()
    {
        // Arrange
        var policy = new DefaultDecisionDataPolicy(
            new DecisionDataPolicyOptions
            {
                MaxMetadataKeyLength = 1
            });
        var data = Data("item", "content") with
        {
            Metadata = new Dictionary<string, string>
            {
                ["["] = "first",
                ["[x"] = "second"
            }
        };

        // Act
        var selection = policy.Select(
            [data],
            new DecisionDataSelectionContext(
                new DecisionTree { TreeId = "tree", StartNodeId = "classify" },
                new DecisionNode { Type = EDecisionNodeType.Classify },
                new DecisionState(),
                new Dictionary<string, string>()));

        // Assert
        selection.Items.Should().ContainSingle();
        selection.Items[0].Metadata.Should().ContainKey("[")
            .WhoseValue.Should().Be("first");
    }

    [Fact]
    public void Select_OmitsDiagnosticMarkerWhenAggregateBudgetCannotFitIt()
    {
        // Arrange
        var policy = new DefaultDecisionDataPolicy(
            new DecisionDataPolicyOptions
            {
                MaxAggregateRepresentationLength = 1
            });

        // Act
        var selection = policy.Select(
            [Data("item", "content")],
            new DecisionDataSelectionContext(
                new DecisionTree { TreeId = "tree", StartNodeId = "classify" },
                new DecisionNode { Type = EDecisionNodeType.Classify },
                new DecisionState(),
                new Dictionary<string, string>()));

        // Assert
        selection.Items.Should().BeEmpty();
        selection.OmittedItemCount.Should().Be(1);
    }

    private static DecisionData Data(
        string id,
        string content,
        string source = "test-source",
        string type = "evidence") =>
        new()
        {
            Id = id,
            Source = source,
            Type = type,
            Content = content
        };
}
