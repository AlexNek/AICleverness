using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;
using AiCleverness.Runtime.Transcript;

using AiClevernessLib.Tests.Testing;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class MarkdownTranscriptTests
{
    [Fact]
    public async Task RunAsync_WhenTranscriptIsDisabled_CreatesNoArtifact()
    {
        // Arrange
        var directory = NewDirectory();
        var runtime = new AgentRuntime(
            new TranscriptTestLlmClient(new LlmResponse("answer")),
            new ToolRegistry());

        // Act
        var result = await runtime.RunAsync(
            new AgentRequest(
                "test goal",
                Parameters: new Dictionary<string, object>
                {
                    [AgentPropertyKeys.MarkdownTranscriptDirectory] = directory
                }));

        // Assert
        result.Success.Should().BeTrue();
        result.Metadata[AgentResultMetadataKeys.MarkdownTranscriptStatus]
            .Should().Be("RedactorUnavailable");
        Directory.Exists(directory).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WhenTranscriptIsEnabled_WritesOneFileAndReturnsMetadataPath()
    {
        // Arrange
        var directory = NewDirectory();
        var runtime = CreateRuntime(
            new TranscriptTestLlmClient(new LlmResponse("answer fake-secret")),
            text => text.Replace("fake-secret", "[HOST-REDACTED]", StringComparison.Ordinal));

        // Act
        var result = await runtime.RunAsync(
            new AgentRequest(
                "goal fake-secret",
                Parameters: new Dictionary<string, object>
                {
                    [AgentPropertyKeys.MarkdownTranscriptDirectory] = directory
                }));

        // Assert
        result.Success.Should().BeTrue();
        result.Metadata[AgentResultMetadataKeys.MarkdownTranscriptStatus]
            .Should().Be("Completed");
        var path = result.Metadata[AgentResultMetadataKeys.MarkdownTranscriptPath]
            .Should().BeOfType<string>().Subject;
        Path.IsPathFullyQualified(path).Should().BeTrue();
        File.Exists(path).Should().BeTrue();
        Directory.GetFiles(directory, "*.md").Should().ContainSingle().Which.Should().Be(path);

        var content = await File.ReadAllTextAsync(path);
        content.Should().Contain("# Agent task");
        content.Should().Contain("## Final response");
        content.Should().Contain("[HOST-REDACTED]");
        content.Should().NotContain("fake-secret");
    }

    [Fact]
    public async Task RunAsync_WhenHostRedactorIsMissing_FailsClosedAndContinuesExecution()
    {
        // Arrange
        var directory = NewDirectory();
        var runtime = new AgentRuntime(
            new TranscriptTestLlmClient(new LlmResponse("answer")),
            new ToolRegistry(),
            options: new AgentRuntimeOptions());

        // Act
        var result = await runtime.RunAsync(
            new AgentRequest(
                "test goal",
                Parameters: new Dictionary<string, object>
                {
                    [AgentPropertyKeys.MarkdownTranscriptDirectory] = directory
                }));

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("answer");
        result.Metadata[AgentResultMetadataKeys.MarkdownTranscriptStatus]
            .Should().Be("RedactorUnavailable");
        Directory.Exists(directory).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_RedactsHostSecretsWithoutRequestSecretParameters()
    {
        // Arrange
        const string hostSecret = "fake-secret";
        const string apiKey = "fake-api-key";
        var directory = NewDirectory();
        var llm = new TranscriptTestLlmClient(
            new LlmResponse(
                null,
                [
                    new LlmToolCall(
                        "call-1",
                        "capture",
                        $"{{\"credential\":\"{hostSecret}\",\"authorization\":\"{apiKey}\"}}")
                ]),
            new LlmResponse($"answer {hostSecret}"));
        var executor = new FakeToolExecutor().EnqueueSuccess($"tool output {hostSecret}");
        var runtime = new AgentRuntime(
            llm,
            CreateTools(),
            toolExecutor: executor,
            options: new AgentRuntimeOptions
            {
                TranscriptRedactor = text => text.Replace(
                    hostSecret,
                    "[HOST-REDACTED]",
                    StringComparison.Ordinal)
            });
        var request = new AgentRequest(
            "capture a value",
            AllowedToolNames: ["capture"],
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.MarkdownTranscriptDirectory] = directory
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        request.Parameters.Should().NotContainKey("sensitive_values");
        request.Parameters.Values
            .OfType<string>()
            .Should().NotContain(value => value.Contains(hostSecret, StringComparison.Ordinal));
        var path = (string)result.Metadata[AgentResultMetadataKeys.MarkdownTranscriptPath];
        var content = await File.ReadAllTextAsync(path);
        content.Should().NotContain(hostSecret);
        content.Should().NotContain(apiKey);
        content.Should().Contain("[HOST-REDACTED]");
        content.Should().Contain("[REDACTED]");
    }

    [Fact]
    public async Task RunAsync_PreservesTranscriptEventOrderingAcrossToolAndFinalResponse()
    {
        // Arrange
        var directory = NewDirectory();
        var llm = new TranscriptTestLlmClient(
            new LlmResponse(
                "checking",
                [new LlmToolCall("call-1", "capture", "{\"message\":\"raw-value\"}")]),
            new LlmResponse("finished"));
        var runtime = CreateRuntime(
            llm,
            static text => text);

        // Act
        var result = await runtime.RunAsync(
            new AgentRequest(
                "use the tool",
                Parameters: new Dictionary<string, object>
                {
                    [AgentPropertyKeys.MarkdownTranscriptDirectory] = directory
                }));

        // Assert
        var path = (string)result.Metadata[AgentResultMetadataKeys.MarkdownTranscriptPath];
        var content = await File.ReadAllTextAsync(path);
        var decisionIndex = content.IndexOf("### Model decision", StringComparison.Ordinal);
        var toolResultIndex = content.IndexOf("### Tool result", StringComparison.Ordinal);
        var finalIndex = content.IndexOf("## Final response", StringComparison.Ordinal);
        decisionIndex.Should().BeGreaterThanOrEqualTo(0);
        toolResultIndex.Should().BeGreaterThan(decisionIndex);
        finalIndex.Should().BeGreaterThan(toolResultIndex);
        content.Should().Contain("raw-value");
        content.Should().Contain("finished");
    }

    [Fact]
    public async Task RunAsync_QualityRetryUsesOneTranscriptArtifactAndCapturesBothAttempts()
    {
        // Arrange
        var directory = NewDirectory();
        var runtime = new AgentRuntime(
            new TranscriptTestLlmClient(
                new LlmResponse("bad answer"),
                new LlmResponse("good answer")),
            new ToolRegistry(),
            qualityGates: [new TranscriptRetryQualityGate()],
            options: new AgentRuntimeOptions
            {
                TranscriptRedactor = static text => text
            });
        var request = new AgentRequest(
            "answer well",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.MarkdownTranscriptDirectory] = directory,
                [AgentPropertyKeys.MaxQualityRetries] = 1
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        var path = (string)result.Metadata[AgentResultMetadataKeys.MarkdownTranscriptPath];
        Directory.GetFiles(directory, "*.md").Should().ContainSingle();
        var content = await File.ReadAllTextAsync(path);
        content.Should().Contain("bad answer");
        content.Should().Contain("good answer");
        content.Should().Contain("### Quality retry 1");
    }

    [Fact]
    public async Task RunAsync_DebugTranscriptWritesExplicitSystemPromptOnlyOnceAcrossQualityRetry()
    {
        // Arrange
        const string systemPrompt = "fake explicit system prompt";
        var directory = NewDirectory();
        var runtime = new AgentRuntime(
            new TranscriptTestLlmClient(
                new LlmResponse("bad answer"),
                new LlmResponse("good answer")),
            new ToolRegistry(),
            qualityGates: [new TranscriptRetryQualityGate()],
            options: new AgentRuntimeOptions
            {
                TranscriptRedactor = static text => text
            });
        var request = new AgentRequest(
            "answer well",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.MarkdownTranscriptDirectory] = directory,
                [AgentPropertyKeys.MarkdownTranscriptDebug] = true,
                [AgentPropertyKeys.SystemPrompt] = systemPrompt,
                [AgentPropertyKeys.MaxQualityRetries] = 1
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        var path = (string)result.Metadata[AgentResultMetadataKeys.MarkdownTranscriptPath];
        var content = await File.ReadAllTextAsync(path);
        content.Split("## Debug runtime", StringSplitOptions.None).Length.Should().Be(3);
        content.Split("**System prompt:**", StringSplitOptions.None).Length.Should().Be(2);
        content.Should().Contain("### Quality retry 1");
        content.Should().Contain("Use the good answer.");
        content.Should().Contain("max_quality_retries:");
        content.Should().NotContain($"{AgentPropertyKeys.SystemPrompt}:");
        content.Should().Contain(systemPrompt);
    }

    [Fact]
    public async Task RunAsync_DebugTranscriptWritesDefaultSystemPromptOnlyOnceAcrossQualityRetry()
    {
        // Arrange
        const string defaultSystemPrompt = "fake default system prompt";
        var directory = NewDirectory();
        var runtime = new AgentRuntime(
            new TranscriptTestLlmClient(
                new LlmResponse("bad answer"),
                new LlmResponse("good answer")),
            new ToolRegistry(),
            qualityGates: [new TranscriptRetryQualityGate()],
            options: new AgentRuntimeOptions
            {
                DefaultSystemPrompt = defaultSystemPrompt,
                TranscriptRedactor = static text => text
            });
        var request = new AgentRequest(
            "answer well",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.MarkdownTranscriptDirectory] = directory,
                [AgentPropertyKeys.MarkdownTranscriptDebug] = true,
                [AgentPropertyKeys.MaxQualityRetries] = 1
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        var path = (string)result.Metadata[AgentResultMetadataKeys.MarkdownTranscriptPath];
        var content = await File.ReadAllTextAsync(path);
        content.Split("## Debug runtime", StringSplitOptions.None).Length.Should().Be(3);
        content.Split("**System prompt:**", StringSplitOptions.None).Length.Should().Be(2);
        content.Should().Contain(defaultSystemPrompt);
        content.Should().NotContain($"{AgentPropertyKeys.SystemPrompt}:");
        content.Should().Contain("Use the good answer.");
    }

    [Fact]
    public async Task RunAsync_WhenLlmFails_FinalizesTranscriptWithFailureStatus()
    {
        // Arrange
        var directory = NewDirectory();
        var runtime = new AgentRuntime(
            new TranscriptTestLlmClient(new InvalidOperationException("fake provider failure")),
            new ToolRegistry(),
            options: new AgentRuntimeOptions
            {
                TranscriptRedactor = static text => text
            });
        var request = new AgentRequest(
            "failing goal",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.MarkdownTranscriptDirectory] = directory
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.Metadata[AgentResultMetadataKeys.MarkdownTranscriptStatus]
            .Should().Be("Completed");
        var path = (string)result.Metadata[AgentResultMetadataKeys.MarkdownTranscriptPath];
        var content = await File.ReadAllTextAsync(path);
        content.Should().Contain("### Execution status: `LLM failure`");
        content.Should().Contain("## Final response");
        content.Should().Contain("fake provider failure");
    }

    [Fact]
    public async Task RunAsync_WhenCancelledDuringInitialization_FinalizesTranscript()
    {
        // Arrange
        var directory = NewDirectory();
        var runtime = new AgentRuntime(
            new TranscriptTestLlmClient(new LlmResponse("never used")),
            new ToolRegistry(),
            options: new AgentRuntimeOptions
            {
                TranscriptRedactor = static text => text
            });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act
        var act = () => runtime.RunAsync(
            new AgentRequest(
                "cancelled goal",
                Parameters: new Dictionary<string, object>
                {
                    [AgentPropertyKeys.MarkdownTranscriptDirectory] = directory
                }),
            cancellationToken: cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        Directory.GetFiles(directory, "*.md").Should().ContainSingle();
        var content = await File.ReadAllTextAsync(Directory.GetFiles(directory, "*.md")[0]);
        content.Should().Contain("### Execution status: `Cancelled`");
    }

    [Fact]
    public async Task RunAsync_ConcurrentExecutionsCreateSeparateTranscriptArtifacts()
    {
        // Arrange
        var directory = NewDirectory();
        var runtime = CreateRuntime(
            new TranscriptTestLlmClient(
                new LlmResponse("concurrent answer"),
                new LlmResponse("concurrent answer")),
            static text => text);

        // Act
        var results = await Task.WhenAll(
            runtime.RunAsync(
                new AgentRequest(
                    "first",
                    Parameters: new Dictionary<string, object>
                    {
                        [AgentPropertyKeys.MarkdownTranscriptDirectory] = directory
                    })),
            runtime.RunAsync(
                new AgentRequest(
                    "second",
                    Parameters: new Dictionary<string, object>
                    {
                        [AgentPropertyKeys.MarkdownTranscriptDirectory] = directory
                    })));

        // Assert
        var paths = results
            .Select(result => (string)result.Metadata[AgentResultMetadataKeys.MarkdownTranscriptPath])
            .ToArray();
        paths.Should().OnlyHaveUniqueItems();
        paths.Should().OnlyContain(path => File.Exists(path));
        Directory.GetFiles(directory, "*.md").Should().HaveCount(2);
    }

    [Fact]
    public void MarkdownTranscriptBuilder_UsesLongerFenceWhenContentContainsBackticks()
    {
        // Arrange
        var content = "line\n```\n````";

        // Act
        var fenced = MarkdownTranscriptBuilder.Fenced(content);

        // Assert
        fenced.Should().StartWith("`````" + Environment.NewLine);
        fenced.Should().EndWith("`````" + Environment.NewLine);
        fenced.Should().Contain(content);
    }

    [Fact]
    public void AgentRuntime_PreservesExistingConstructorAndRuntimeInterfaces()
    {
        // Arrange / Act
        var runtime = new AgentRuntime(
            new TranscriptTestLlmClient(new LlmResponse("answer")),
            new ToolRegistry());

        // Assert
        runtime.Should().BeAssignableTo<IAgentRuntime>();
        runtime.Should().BeAssignableTo<IStreamingAgentRuntime>();
    }

    private static AgentRuntime CreateRuntime(
        TranscriptTestLlmClient llm,
        Func<string, string> redactor) =>
        new(
            llm,
            CreateTools(),
            options: new AgentRuntimeOptions
            {
                TranscriptRedactor = redactor
            });

    private static ToolRegistry CreateTools()
    {
        var tools = new ToolRegistry();
        tools.Register(new TranscriptTestTool());
        return tools;
    }

    private static string NewDirectory() => Path.Combine(
        Path.GetTempPath(),
        "AiClevernessTranscriptTests",
        Guid.NewGuid().ToString("N"));
}
