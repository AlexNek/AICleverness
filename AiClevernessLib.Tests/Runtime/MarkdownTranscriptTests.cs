using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Models.DecisionTree;
using AiCleverness.Runtime;
using AiCleverness.Runtime.Transcript;

using AiClevernessLib.Tests.Testing;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class MarkdownTranscriptTests
{
    [Fact]
    public void TranscriptBuilderDecorator_PreservesDefaultSectionsAndAllowsSingleSectionOverride()
    {
        // Arrange
        var builder = new OverridingTranscriptBuilder();

        // Act
        var header = builder.Header("goal", "execution-id", DateTimeOffset.UtcNow, debug: false);
        var action = builder.DecisionAction(
            "node-id",
            "collect",
            "Collect evidence",
            DecisionActionStatus.Success,
            "Found matching evidence.",
            null,
            null);

        // Assert
        header.Should().Contain("# Agent task");
        action.Should().Contain("### Custom action: `Collect evidence`");
        action.Should().Contain("**Outcome:**");
    }

    [Fact]
    public void DecisionAction_UsesNodeNameAndRendersOutcomeSeparatelyFromError()
    {
        // Arrange
        var builder = new MarkdownTranscriptBuilder();

        // Act
        var content = builder.DecisionAction(
            "node-id",
            "collect",
            "Collect evidence",
            DecisionActionStatus.Success,
            "Found matching evidence.",
            "informational error-like text",
            null);

        // Assert
        content.Should().Contain("### Decision action: `Collect evidence`");
        content.Should().Contain("**Outcome:**");
        content.Should().Contain("Found matching evidence.");
        content.Should().Contain("**Error:**");
        content.Should().Contain("informational error-like text");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DecisionAction_FallsBackToActionKeyWhenNodeNameIsUnavailable(string? nodeName)
    {
        // Arrange
        var builder = new MarkdownTranscriptBuilder();

        // Act
        var content = builder.DecisionAction(
            "node-id",
            "collect",
            nodeName,
            DecisionActionStatus.Success,
            null,
            null,
            null);

        // Assert
        content.Should().Contain("### Decision action: `collect`");
    }

    [Fact]
    public async Task RunAsync_UsesCustomBuilderAndSinkPerExecution()
    {
        // Arrange
        var directory = NewDirectory();
        var builders = new List<RecordingTranscriptBuilder>();
        var sinks = new List<RecordingTranscriptSink>();
        var gate = new object();
        var runtime = new AgentRuntime(
            new TranscriptTestLlmClient(new LlmResponse("custom answer")),
            new ToolRegistry(),
            options: new AgentRuntimeOptions
            {
                TranscriptRedactor = static text => text,
                TranscriptBuilderFactory = () =>
                {
                    var builder = new RecordingTranscriptBuilder();
                    lock (gate)
                        builders.Add(builder);
                    return builder;
                },
                TranscriptSinkFactory = path =>
                {
                    var sink = new RecordingTranscriptSink(path);
                    lock (gate)
                        sinks.Add(sink);
                    return sink;
                }
            });

        // Act
        var result = await runtime.RunAsync(
            new AgentRequest(
                "custom goal",
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
        builders.Should().ContainSingle();
        sinks.Should().ContainSingle();
        path.Should().Be(sinks[0].FilePath);
        sinks[0].IsCompleted.Should().BeTrue();
        sinks[0].Content.Should().Contain("custom answer");
        Directory.Exists(directory).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_CreatesDistinctCustomTranscriptComponentsForConcurrentExecutions()
    {
        // Arrange
        var directory = NewDirectory();
        var builders = new List<RecordingTranscriptBuilder>();
        var sinks = new List<RecordingTranscriptSink>();
        var gate = new object();
        var runtime = new AgentRuntime(
            new TranscriptTestLlmClient(
                new LlmResponse("first answer"),
                new LlmResponse("second answer")),
            new ToolRegistry(),
            options: new AgentRuntimeOptions
            {
                TranscriptRedactor = static text => text,
                TranscriptBuilderFactory = () =>
                {
                    var builder = new RecordingTranscriptBuilder();
                    lock (gate)
                        builders.Add(builder);
                    return builder;
                },
                TranscriptSinkFactory = path =>
                {
                    var sink = new RecordingTranscriptSink(path);
                    lock (gate)
                        sinks.Add(sink);
                    return sink;
                }
            });

        // Act
        var results = await Task.WhenAll(
            runtime.RunAsync(new AgentRequest(
                "first goal",
                Parameters: new Dictionary<string, object>
                {
                    [AgentPropertyKeys.MarkdownTranscriptDirectory] = directory
                })),
            runtime.RunAsync(new AgentRequest(
                "second goal",
                Parameters: new Dictionary<string, object>
                {
                    [AgentPropertyKeys.MarkdownTranscriptDirectory] = directory
                })));

        // Assert
        results.Should().OnlyContain(result => result.Success);
        builders.Should().HaveCount(2);
        builders.Select(builder => builder).Distinct().Should().HaveCount(2);
        sinks.Should().HaveCount(2);
        sinks.Select(sink => sink).Distinct().Should().HaveCount(2);
        sinks.Should().OnlyContain(sink => sink.IsCompleted);
        sinks.Select(sink => sink.Content).Should().Contain(content => content.Contains("first answer", StringComparison.Ordinal));
        sinks.Select(sink => sink.Content).Should().Contain(content => content.Contains("second answer", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_WhenCustomBuilderFactoryFails_ContinuesWithoutTranscript()
    {
        // Arrange
        var directory = NewDirectory();
        var runtime = new AgentRuntime(
            new TranscriptTestLlmClient(new LlmResponse("answer")),
            new ToolRegistry(),
            options: new AgentRuntimeOptions
            {
                TranscriptRedactor = static text => text,
                TranscriptBuilderFactory = () => throw new InvalidOperationException("fake builder failure")
            });

        // Act
        var result = await runtime.RunAsync(
            new AgentRequest(
                "goal",
                Parameters: new Dictionary<string, object>
                {
                    [AgentPropertyKeys.MarkdownTranscriptDirectory] = directory
                }));

        // Assert
        result.Success.Should().BeTrue();
        result.Metadata[AgentResultMetadataKeys.MarkdownTranscriptStatus]
            .Should().Be("Unavailable");
        Directory.Exists(directory).Should().BeFalse();
    }

    [Fact]
    public void DecisionResult_RendersStatePropertiesBeforeSelectedPath()
    {
        // Arrange
        var builder = new MarkdownTranscriptBuilder();
        var properties = new[]
        {
            new KeyValuePair<string, string>("alpha", "one"),
            new KeyValuePair<string, string>("unsafe*key", "line one\nline two")
        };

        // Act
        var content = builder.DecisionResult(
            DecisionTreeOutcome.Terminal,
            succeeded: true,
            verdict: "approved",
            error: null,
            new ResourceUsage(),
            ["terminal"],
            stateProperties: properties);

        // Assert
        content.Should().Contain("### State properties");
        content.Should().Contain("**alpha:** `one`");
        content.Should().Contain("**unsafe\\*key:**");
        content.Should().Contain("line one");
        content.IndexOf("### State properties", StringComparison.Ordinal)
            .Should().BeLessThan(content.IndexOf("### Selected path", StringComparison.Ordinal));
    }

    [Fact]
    public void DecisionResult_OmitsStatePropertiesWhenEntriesAreMissing()
    {
        // Arrange
        var builder = new MarkdownTranscriptBuilder();

        // Act
        var withoutProperties = builder.DecisionResult(
            DecisionTreeOutcome.Terminal,
            succeeded: true,
            verdict: null,
            error: null,
            new ResourceUsage(),
            [],
            stateProperties: null);
        var withEmptyProperties = builder.DecisionResult(
            DecisionTreeOutcome.Terminal,
            succeeded: true,
            verdict: null,
            error: null,
            new ResourceUsage(),
            [],
            stateProperties: Array.Empty<KeyValuePair<string, string>>());

        // Assert
        withoutProperties.Should().NotContain("### State properties");
        withEmptyProperties.Should().NotContain("### State properties");
    }

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
    public async Task RunAsync_DebugTranscriptWritesExplicitSystemPromptOnceWithoutRetry()
    {
        // Arrange
        const string systemPrompt = "fake explicit system prompt";
        var directory = NewDirectory();
        var runtime = new AgentRuntime(
            new TranscriptTestLlmClient(new LlmResponse("good answer")),
            new ToolRegistry(),
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
                [AgentPropertyKeys.SystemPrompt] = systemPrompt
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        var path = (string)result.Metadata[AgentResultMetadataKeys.MarkdownTranscriptPath];
        var content = await File.ReadAllTextAsync(path);
        var runtimeSections = content.Split("## Debug runtime", StringSplitOptions.None).Skip(1).ToArray();
        runtimeSections.Should().ContainSingle();
        runtimeSections[0].Should().Contain("**System prompt:**");
        runtimeSections[0].Should().Contain(systemPrompt);
        content.Should().NotContain($"{AgentPropertyKeys.SystemPrompt}:");
    }

    [Fact]
    public async Task RunAsync_DebugTranscriptWritesExplicitSystemPromptOnlyOnceAcrossQualityRetry()
    {
        // Arrange
        const string systemPrompt = "fake explicit system prompt";
        var directory = NewDirectory();
        var llm = new FakeChatClient()
            .EnqueueResponse("bad answer")
            .EnqueueResponse("good answer");
        var runtime = new AgentRuntime(
            llm,
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
        var runtimeSections = content.Split("## Debug runtime", StringSplitOptions.None).Skip(1).ToArray();
        runtimeSections.Should().HaveCount(2);
        runtimeSections[0].Should().Contain("**System prompt:**");
        runtimeSections[0].Should().Contain(systemPrompt);
        runtimeSections[1].Should().NotContain("**System prompt:**");
        runtimeSections[1].Should().Contain("**Quality feedback:**");
        runtimeSections[1].Should().Contain("Use the good answer.");
        runtimeSections[1].Should().Contain("**Quality retry count:**");
        content.Should().Contain("### Quality retry 1");
        content.Should().NotContain($"{AgentPropertyKeys.SystemPrompt}:");

        llm.Calls.Should().HaveCount(2);
        llm.Calls[0].SystemMessage.Should().Be(systemPrompt);
        llm.Calls[0].UserMessage.Should().Be("answer well");
        llm.Calls[1].SystemMessage.Should().Be(
            $"{systemPrompt}{Environment.NewLine}{Environment.NewLine}" +
            $"Quality feedback from previous attempt:{Environment.NewLine}Use the good answer.");
        llm.Calls[1].UserMessage.Should().Be("answer well");
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
        var runtimeSections = content.Split("## Debug runtime", StringSplitOptions.None).Skip(1).ToArray();
        runtimeSections.Should().HaveCount(2);
        runtimeSections[0].Should().Contain("**System prompt:**");
        runtimeSections[0].Should().Contain(defaultSystemPrompt);
        runtimeSections[1].Should().NotContain("**System prompt:**");
        runtimeSections[1].Should().Contain("**Quality feedback:**");
        runtimeSections[1].Should().Contain("Use the good answer.");
        content.Should().NotContain($"{AgentPropertyKeys.SystemPrompt}:");
    }

    [Fact]
    public async Task RunAsync_WhenDebugTranscriptIsDisabled_PreservesNormalTranscriptAndPromptDelivery()
    {
        // Arrange
        const string systemPrompt = "fake explicit system prompt";
        var directory = NewDirectory();
        var llm = new FakeChatClient().EnqueueResponse("good answer");
        var runtime = new AgentRuntime(
            llm,
            new ToolRegistry(),
            options: new AgentRuntimeOptions
            {
                TranscriptRedactor = static text => text
            });
        var request = new AgentRequest(
            "answer well",
            Parameters: new Dictionary<string, object>
            {
                [AgentPropertyKeys.MarkdownTranscriptDirectory] = directory,
                [AgentPropertyKeys.SystemPrompt] = systemPrompt
            });

        // Act
        var result = await runtime.RunAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        var path = (string)result.Metadata[AgentResultMetadataKeys.MarkdownTranscriptPath];
        var content = await File.ReadAllTextAsync(path);
        content.Should().Contain("## Request");
        content.Should().Contain("answer well");
        content.Should().NotContain("## Debug request parameters");
        content.Should().NotContain("## Debug runtime");
        content.Should().NotContain(systemPrompt);
        llm.Calls.Should().ContainSingle();
        llm.Calls[0].SystemMessage.Should().Be(systemPrompt);
        llm.Calls[0].UserMessage.Should().Be("answer well");
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

    [Theory]
    [InlineData("```json\n{\"answer\":\"subscription_pricing\"}\n```")]
    [InlineData("```\n{\"answer\":\"subscription_pricing\"}\n```")]
    public void DecisionLlmAttempt_StripsValidCodeFencesBeforeRendering(string response)
    {
        // Arrange
        const string plainResponse = "{\"answer\":\"subscription_pricing\"}";

        // Act
        var actual = BuildDecisionLlmAttempt(response);
        var expected = BuildDecisionLlmAttempt(plainResponse);

        // Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public void DecisionLlmAttempt_PreservesEmptyResponseDisplay()
    {
        // Act
        var transcript = BuildDecisionLlmAttempt(null);

        // Assert
        transcript.Should().Contain(
            $"**Raw LLM output:**{Environment.NewLine}```json{Environment.NewLine}(empty){Environment.NewLine}```{Environment.NewLine}");
    }

    [Fact]
    public void DecisionLlmAttempt_RendersEmptyStringResponseAsEmpty()
    {
        // Act
        var transcript = BuildDecisionLlmAttempt(string.Empty);

        // Assert
        transcript.Should().Contain(
            $"**Raw LLM output:**{Environment.NewLine}```json{Environment.NewLine}(empty){Environment.NewLine}```{Environment.NewLine}");
    }

    [Fact]
    public void DecisionLlmAttempt_LeavesIncompleteCodeFenceForSafeFallback()
    {
        // Arrange
        const string response = "```json\n{\"answer\":\"subscription_pricing\"}";

        // Act
        var transcript = BuildDecisionLlmAttempt(response);

        // Assert
        transcript.Should().Contain("````json" + Environment.NewLine);
        transcript.Should().Contain(response);
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

    private static string BuildDecisionLlmAttempt(string? response) =>
        new MarkdownTranscriptBuilder().DecisionLlmAttempt(
            "node",
            1,
            [new LlmMessage("user", "input")],
            response,
            finishReason: null,
            usage: null);

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
