using AiCleverness.Abstractions;
using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime;
using AiCleverness.Runtime.Conversation;
using AiCleverness.Runtime.DecisionTree;
using AiClevernessLib.Tests.Testing;

using FluentAssertions;

using DecisionTreeModel = AiCleverness.Models.DecisionTree.DecisionTree;

namespace AiClevernessLib.Tests.Runtime;

public sealed class DecisionTreeTranscriptTests
{
    [Fact]
    public async Task ExecuteAsync_NormalTranscriptRedactsDecisionContentAndWritesDecisionSections()
    {
        // Arrange
        var directory = NewDirectory();
        try
        {
            var pipeline = new DecisionTreeCompletionPipeline()
                .Enqueue("{\"answer\":\"supported\",\"observation\":\"fake-secret\",\"confidence\":\"high\"}");
            var options = new DecisionTreeExecutionOptions
            {
                TranscriptDirectory = directory,
                TranscriptRedactor = text => text.Replace(
                    "fake-secret",
                    "[REDACTED]",
                    StringComparison.Ordinal)
            };
            var executor = CreateExecutor(pipeline, options);

            // Act
            var result = await executor.ExecuteAsync(CreateTree());

            // Assert
            result.Succeeded.Should().BeTrue();
            var path = Directory.GetFiles(directory, "*.md").Should().ContainSingle().Which;
            var content = await File.ReadAllTextAsync(path);
            content.Should().Contain("## Decision node:");
            content.Should().Contain("### Decision action:");
            content.Should().Contain("### Decision question answered");
            content.Should().Contain("## Decision result");
            content.Should().Contain("### Decision budget");
            content.Should().Contain("[REDACTED]");
            content.Should().NotContain("fake-secret");
            content.Should().NotContain("## Debug runtime");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task ExecuteAsync_DebugTranscriptPreservesDecisionContent()
    {
        // Arrange
        var directory = NewDirectory();
        try
        {
            var pipeline = new DecisionTreeCompletionPipeline()
                .Enqueue("{\"answer\":\"supported\",\"observation\":\"debug-secret\",\"confidence\":\"high\"}");
            var options = new DecisionTreeExecutionOptions
            {
                TranscriptDirectory = directory,
                TranscriptDebug = true
            };
            var executor = CreateExecutor(pipeline, options);

            // Act
            var result = await executor.ExecuteAsync(CreateTree());

            // Assert
            result.Succeeded.Should().BeTrue();
            var path = Directory.GetFiles(directory, "*.md").Should().ContainSingle().Which;
            var content = await File.ReadAllTextAsync(path);
            content.Should().Contain("**Debug mode:** `True`");
            content.Should().Contain("debug-secret");
            content.Should().Contain("## Decision result");
            content.Should().Contain("### Decision budget");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static DecisionTreeExecutor CreateExecutor(
        DecisionTreeCompletionPipeline pipeline,
        DecisionTreeExecutionOptions options)
    {
        var action = new DecisionTreeTestAction();
        IDecisionPredicate[] predicates = [new DataExistsPredicate()];
        return new DecisionTreeExecutor(
            pipeline,
            new DefaultConversationManager(),
            new InMemoryExecutionJournal(),
            eventPublisher: null,
            actions: [action],
            predicates,
            new DefaultDecisionLlmContextBuilder(),
            new DecisionTreeLoader([action], predicates),
            options);
    }

    private static DecisionTreeModel CreateTree()
        => new()
        {
            TreeId = "transcript-tree",
            Version = 1,
            StartNodeId = "collect",
            Budget = new DecisionBudget
            {
                MaxNodeVisits = 5,
                MaxLlmCalls = 1,
                MaxElapsedTime = TimeSpan.FromSeconds(10),
                MaxContextTokens = 100
            },
            Nodes = new Dictionary<string, DecisionNode>
            {
                ["collect"] = new()
                {
                    Type = EDecisionNodeType.Action,
                    ActionName = "collect",
                    Transitions =
                    [
                        new() { Condition = "success", NextNodeId = "question" },
                        new() { Condition = "transientFailure", NextNodeId = "failed" },
                        new() { Condition = "permanentFailure", NextNodeId = "failed" }
                    ]
                },
                ["question"] = new()
                {
                    Type = EDecisionNodeType.Question,
                    Question = "Is the evidence supported?",
                    Answers = ["supported"],
                    Transitions =
                    [
                        new() { Condition = "supported", NextNodeId = "approved" },
                        new() { Condition = "unknown", NextNodeId = "unknown" }
                    ]
                },
                ["approved"] = new()
                {
                    Type = EDecisionNodeType.Terminal,
                    Verdict = "approved-fake-secret"
                },
                ["unknown"] = new()
                {
                    Type = EDecisionNodeType.Terminal,
                    Verdict = "unknown"
                },
                ["failed"] = new()
                {
                    Type = EDecisionNodeType.Terminal,
                    Verdict = "failed"
                }
            }
        };

    private static string NewDirectory()
        => Path.Combine(
            Path.GetTempPath(),
            "AiClevernessDecisionTranscriptTests",
            Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
