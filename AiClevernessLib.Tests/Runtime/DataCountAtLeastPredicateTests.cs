using System.Text.Json;
using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime.DecisionTree;
using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class DataCountAtLeastPredicateTests
{
    [Fact]
    public void Evaluate_ReturnsFalse_WhenTypeIsMissing()
    {
        // Arrange
        var predicate = new DataCountAtLeastPredicate();
        var context = CreateContext(new Dictionary<string, JsonElement>
        {
            ["min"] = ParseJson("1")
        });

        // Act
        var result = predicate.Evaluate(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ReturnsFalse_WhenTypeIsNotAString()
    {
        // Arrange
        var predicate = new DataCountAtLeastPredicate();
        var context = CreateContext(new Dictionary<string, JsonElement>
        {
            ["type"] = ParseJson("1"),
            ["min"] = ParseJson("1")
        });

        // Act
        var result = predicate.Evaluate(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ReturnsFalse_WhenMinimumIsMissing()
    {
        // Arrange
        var predicate = new DataCountAtLeastPredicate();
        var context = CreateContext(new Dictionary<string, JsonElement>
        {
            ["type"] = ParseJson("\"evidence\"")
        });

        // Act
        var result = predicate.Evaluate(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ReturnsFalse_WhenMinimumIsNotAnInteger()
    {
        // Arrange
        var predicate = new DataCountAtLeastPredicate();
        var context = CreateContext(new Dictionary<string, JsonElement>
        {
            ["type"] = ParseJson("\"evidence\""),
            ["min"] = ParseJson("1.5")
        });

        // Act
        var result = predicate.Evaluate(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ReturnsFalse_WhenMinimumIsNegative()
    {
        // Arrange
        var predicate = new DataCountAtLeastPredicate();
        var context = CreateContext(
            new Dictionary<string, JsonElement>
            {
                ["type"] = ParseJson("\"evidence\""),
                ["min"] = ParseJson("-1")
            });

        // Act
        var result = predicate.Evaluate(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ReturnsTrue_WhenMinimumIsZeroAndNoMatchingDataExists()
    {
        // Arrange
        var predicate = new DataCountAtLeastPredicate();
        var context = CreateContext(new Dictionary<string, JsonElement>
        {
            ["type"] = ParseJson("\"evidence\""),
            ["min"] = ParseJson("0")
        });

        // Act
        var result = predicate.Evaluate(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ReturnsTrue_WhenMatchingDataCountMeetsMinimum()
    {
        // Arrange
        var predicate = new DataCountAtLeastPredicate();
        var data = new DataStore();
        data.Add(CreateData("evidence-1", "evidence"));
        data.Add(CreateData("evidence-2", "evidence"));
        data.Add(CreateData("other-1", "other"));
        var context = CreateContext(
            new Dictionary<string, JsonElement>
            {
                ["type"] = ParseJson("\"evidence\""),
                ["min"] = ParseJson("2")
            },
            data);

        // Act
        var result = predicate.Evaluate(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ReturnsFalse_WhenMatchingDataCountIsBelowMinimum()
    {
        // Arrange
        var predicate = new DataCountAtLeastPredicate();
        var data = new DataStore();
        data.Add(CreateData("evidence-1", "evidence"));
        var context = CreateContext(
            new Dictionary<string, JsonElement>
            {
                ["type"] = ParseJson("\"evidence\""),
                ["min"] = ParseJson("2")
            },
            data);

        // Act
        var result = predicate.Evaluate(context);

        // Assert
        result.Should().BeFalse();
    }

    private static DecisionPredicateContext CreateContext(
        IReadOnlyDictionary<string, JsonElement> parameters,
        DataStore? data = null)
        => new("test-node", new DecisionState(), data ?? new DataStore(), parameters);

    private static DecisionData CreateData(string id, string type)
        => new()
        {
            Id = id,
            Source = "test-source",
            Type = type,
            Content = "test-content",
            CreatedAt = DateTimeOffset.UtcNow
        };

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
