using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;
using AiCleverness.Runtime.Workflows;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class WorkflowTests
{
    public sealed class ConditionalWorkflowTests
    {
        [Fact]
        public async Task TrueBranch_WhenConditionMet()
        {
            var runtime = CreateRuntime(
                new LlmResponse("check-output"),
                new LlmResponse("true-branch-output"));

            var workflow = new WorkflowDefinition
                               {
                                   Id = "w1",
                                   Name = "Cond",
                                   EntryNodeId = "check",
                                   Nodes =
                                       [
                                           new WorkflowNode
                                               {
                                                   Id = "check",
                                                   Name = "Check",
                                                   Type = WorkflowNodeType.Agent,
                                                   Request = new AgentRequest("check"),
                                                   Children = ["cond"]
                                               },
                                           new WorkflowNode
                                               {
                                                   Id = "cond",
                                                   Name = "Branch",
                                                   Type = WorkflowNodeType.Condition,
                                                   Condition = "check",
                                                   Children = ["yes", "no"]
                                               },
                                           new WorkflowNode
                                               {
                                                   Id = "yes",
                                                   Name = "Yes",
                                                   Type = WorkflowNodeType.Agent,
                                                   Request = new AgentRequest("yes path")
                                               },
                                           new WorkflowNode
                                               {
                                                   Id = "no",
                                                   Name = "No",
                                                   Type = WorkflowNodeType.Agent,
                                                   Request = new AgentRequest("no path")
                                               }
                                       ]
                               };

            var executor = new ConditionalWorkflowExecutor(runtime);
            var result = await executor.ExecuteAsync(workflow);

            result.Success.Should().BeTrue();
            result.NodeResults.Should().ContainKey("yes");
            result.NodeResults.Should().NotContainKey("no");
        }
    }

    public sealed class CoordinatorReviewerTests
    {
        [Fact]
        public async Task Approved_OnFirstCycle()
        {
            var runtime = CreateRuntime(
                new LlmResponse("draft output"),
                new LlmResponse("This is approved."));

            var pattern = new CoordinatorReviewerPattern(runtime);
            var result = await pattern.RunAsync(
                             "write something",
                             "Review: {{output}}. Say 'approved' if good.");

            result.Approved.Should().BeTrue();
            result.FinalOutput.Should().Be("draft output");
            result.CyclesUsed.Should().Be(1);
        }

        [Fact]
        public async Task Rejected_ThenApproved()
        {
            var runtime = CreateRuntime(
                new LlmResponse("bad draft"),
                new LlmResponse("needs improvement"),
                new LlmResponse("good draft"),
                new LlmResponse("approved"));

            var pattern = new CoordinatorReviewerPattern(runtime, maxReviewCycles: 3);
            var result = await pattern.RunAsync(
                             "write",
                             "Review: {{output}}. Say 'approved' if good.");

            result.Approved.Should().BeTrue();
            result.CyclesUsed.Should().Be(2);
            result.ReviewerFeedback.Should().HaveCount(1);
        }
    }

    public sealed class ParallelWorkflowTests
    {
        [Fact]
        public async Task Executes_IndependentNodes_InParallel()
        {
            var runtime = CreateRuntime(
                new LlmResponse("result-a"),
                new LlmResponse("result-b"));

            var workflow = new WorkflowDefinition
                               {
                                   Id = "w1",
                                   Name = "Parallel",
                                   EntryNodeId = "a",
                                   Nodes =
                                       [
                                           new WorkflowNode
                                               {
                                                   Id = "a",
                                                   Name = "A",
                                                   Type = WorkflowNodeType.Agent,
                                                   Request = new AgentRequest("task A")
                                               },
                                           new WorkflowNode
                                               {
                                                   Id = "b",
                                                   Name = "B",
                                                   Type = WorkflowNodeType.Agent,
                                                   Request = new AgentRequest("task B")
                                               }
                                       ]
                               };

            var executor = new ParallelWorkflowExecutor(runtime);
            var result = await executor.ExecuteAsync(workflow);

            result.Success.Should().BeTrue();
            result.NodeResults.Should().HaveCount(2);
        }

        [Fact]
        public async Task Respects_Dependencies()
        {
            var runtime = CreateRuntime(
                new LlmResponse("first"),
                new LlmResponse("second"));

            var workflow = new WorkflowDefinition
                               {
                                   Id = "w1",
                                   Name = "Deps",
                                   EntryNodeId = "a",
                                   Nodes =
                                       [
                                           new WorkflowNode
                                               {
                                                   Id = "a",
                                                   Name = "A",
                                                   Type = WorkflowNodeType.Agent,
                                                   Request = new AgentRequest("first")
                                               },
                                           new WorkflowNode
                                               {
                                                   Id = "b",
                                                   Name = "B",
                                                   Type = WorkflowNodeType.Agent,
                                                   Request = new AgentRequest("second"),
                                                   DependsOn = ["a"]
                                               }
                                       ]
                               };

            var executor = new ParallelWorkflowExecutor(runtime);
            var result = await executor.ExecuteAsync(workflow);

            result.Success.Should().BeTrue();
            result.NodeResults.Should().HaveCount(2);
        }
    }

    public sealed class SequentialAgentPipelineTests
    {
        [Fact]
        public async Task Chains_Outputs()
        {
            var runtime = CreateRuntime(
                new LlmResponse("step1"),
                new LlmResponse("combined"));

            var pipeline = new SequentialAgentPipeline(runtime);
            var requests = new List<AgentRequest>
                               {
                                   new("generate"), new("refine: {{previous_output}}")
                               };

            var result = await pipeline.RunAsync(requests);

            result.Success.Should().BeTrue();
            result.Output.Should().Be("combined");
        }

        [Fact]
        public async Task EmptyPipeline_ReturnsSuccess()
        {
            var runtime = CreateRuntime();
            var pipeline = new SequentialAgentPipeline(runtime);

            var result = await pipeline.RunAsync([]);

            result.Success.Should().BeTrue();
        }

        [Fact]
        public async Task Stops_OnFirstFailure()
        {
            var runtimeFails = new AgentRuntime(
                new FakeLlmClient([new LlmResponse(null)]),
                new ToolRegistry(),
                options: new AgentRuntimeOptions { DefaultMaxTurns = 1 });

            var pipeline = new SequentialAgentPipeline(runtimeFails);
            var requests = new List<AgentRequest> { new("fail"), new("never reached") };

            var result = await pipeline.RunAsync(requests);

            result.Success.Should().BeFalse();
        }
    }

    public sealed class SequentialWorkflowTests
    {
        [Fact]
        public async Task Executes_AllNodes_InOrder()
        {
            var runtime = CreateRuntime(
                new LlmResponse("step1-output"),
                new LlmResponse("step2-output"));

            var workflow = new WorkflowDefinition
                               {
                                   Id = "w1",
                                   Name = "Test",
                                   EntryNodeId = "n1",
                                   Nodes =
                                       [
                                           new WorkflowNode
                                               {
                                                   Id = "n1",
                                                   Name = "Step 1",
                                                   Type = WorkflowNodeType.Agent,
                                                   Request = new AgentRequest("do step 1")
                                               },
                                           new WorkflowNode
                                               {
                                                   Id = "n2",
                                                   Name = "Step 2",
                                                   Type = WorkflowNodeType.Agent,
                                                   Request = new AgentRequest("do step 2"),
                                                   DependsOn = ["n1"]
                                               }
                                       ]
                               };

            var executor = new SequentialWorkflowExecutor(runtime);
            var result = await executor.ExecuteAsync(workflow);

            result.Success.Should().BeTrue();
            result.NodeResults.Should().HaveCount(2);
            result.Output.Should().Be("step2-output");
        }

        [Fact]
        public async Task Stops_OnFailure()
        {
            var runtime =
                CreateRuntime(new LlmResponse(null)); // null content = fails after max turns
            var runtimeWithOneTurn = new AgentRuntime(
                new FakeLlmClient([new LlmResponse(null)]),
                new ToolRegistry(),
                options: new AgentRuntimeOptions { DefaultMaxTurns = 1 });

            var workflow = new WorkflowDefinition
                               {
                                   Id = "w1",
                                   Name = "Test",
                                   EntryNodeId = "n1",
                                   Nodes =
                                       [
                                           new WorkflowNode
                                               {
                                                   Id = "n1",
                                                   Name = "Fails",
                                                   Type = WorkflowNodeType.Agent,
                                                   Request = new AgentRequest("fail")
                                               },
                                           new WorkflowNode
                                               {
                                                   Id = "n2",
                                                   Name = "Never",
                                                   Type = WorkflowNodeType.Agent,
                                                   Request = new AgentRequest("never"),
                                                   DependsOn = ["n1"]
                                               }
                                       ]
                               };

            var executor = new SequentialWorkflowExecutor(runtimeWithOneTurn);
            var result = await executor.ExecuteAsync(workflow);

            result.Success.Should().BeFalse();
            result.NodeResults.Should().HaveCount(1);
        }
    }

    private static AgentRuntime CreateRuntime(params LlmResponse[] responses) =>
        new(new FakeLlmClient(responses), new ToolRegistry());

    private sealed class FakeLlmClient : ILlmClient
    {
        private readonly Queue<LlmResponse> _responses;

        public FakeLlmClient(IEnumerable<LlmResponse> responses)
        {
            _responses = new Queue<LlmResponse>(responses);
        }

        public Task<LlmResponse> CompleteAsync(
            IReadOnlyList<LlmMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            LlmCompletionOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (_responses.Count == 0)
                return Task.FromResult(new LlmResponse(null));
            return Task.FromResult(_responses.Dequeue() with { Usage = new LlmTokenUsage(10, 5) });
        }
    }
}
