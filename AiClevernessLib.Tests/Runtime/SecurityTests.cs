using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;
using AiCleverness.Runtime.Security;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class SecurityTests
{
    public sealed class ApprovalServiceTests
    {
        [Fact]
        public void ApprovalDecision_AutoApproved_FactoryWorks()
        {
            var decision = ApprovalDecision.AutoApproved("test reason");

            decision.Approved.Should().BeTrue();
            decision.Reason.Should().Be("test reason");
            decision.DecidedAt.Should().NotBeNull();
        }

        [Fact]
        public void ApprovalDecision_Denied_FactoryWorks()
        {
            var decision = ApprovalDecision.Denied("not allowed");

            decision.Approved.Should().BeFalse();
            decision.Reason.Should().Be("not allowed");
        }

        [Fact]
        public async Task AutoApproval_AlwaysApproves()
        {
            var service = new AutoApprovalService();
            var request = new ApprovalRequest(
                "deploy",
                new ToolInvocation("deploy"),
                DangerLevel.Critical);

            var decision = await service.RequestApprovalAsync(request);

            decision.Approved.Should().BeTrue();
            decision.ApprovedBy.Should().Be("system");
        }
    }

    public sealed class DangerLevelValidatorTests
    {
        [Fact]
        public async Task NullDangerLevel_TreatedAsSafe()
        {
            var validator = new DangerLevelToolCallValidator(DangerLevel.Low);
            var tool = new FakeTool("generic", dangerLevel: null);
            var invocation = new ToolInvocation("generic");

            var result = await validator.ValidateAsync(tool, invocation, CreateContext());

            result.IsAllowed.Should().BeTrue();
            result.DangerLevel.Should().Be(DangerLevel.Safe);
        }

        [Fact]
        public async Task Safe_Tool_IsAllowed()
        {
            var validator = new DangerLevelToolCallValidator();
            var tool = new FakeTool("read", dangerLevel: "Safe");
            var invocation = new ToolInvocation("read");

            var result = await validator.ValidateAsync(tool, invocation, CreateContext());

            result.IsAllowed.Should().BeTrue();
            result.DangerLevel.Should().Be(DangerLevel.Safe);
        }

        [Fact]
        public async Task Tool_AtMaxLevel_IsAllowed()
        {
            var validator = new DangerLevelToolCallValidator();
            var tool = new FakeTool("write", dangerLevel: "High");
            var invocation = new ToolInvocation("write");

            var result = await validator.ValidateAsync(tool, invocation, CreateContext());

            result.IsAllowed.Should().BeTrue();
            result.DangerLevel.Should().Be(DangerLevel.High);
        }

        [Fact]
        public async Task Tool_ExceedingMaxLevel_IsBlocked()
        {
            var validator = new DangerLevelToolCallValidator(DangerLevel.Medium);
            var tool = new FakeTool("deploy", dangerLevel: "Critical");
            var invocation = new ToolInvocation("deploy");

            var result = await validator.ValidateAsync(tool, invocation, CreateContext());

            result.IsAllowed.Should().BeFalse();
            result.Reason.Should().Contain("Critical");
            result.Reason.Should().Contain("Medium");
        }
    }

    public sealed class ScopeValidatorTests
    {
        [Fact]
        public async Task AllowedHosts_Allows_AuthorizedHost()
        {
            var validator = new DefaultScopeValidator();
            var tool = new FakeTool("fetch");
            var invocation = new ToolInvocation(
                "fetch",
                new Dictionary<string, object> { ["url"] = "https://api.example.com/v1/data" });
            var scope = new ToolInputScope { AllowedHosts = ["api.example.com"] };

            var result = await validator.ValidateAsync(tool, invocation, scope);

            result.IsWithinScope.Should().BeTrue();
        }

        [Fact]
        public async Task AllowedHosts_Blocks_UnauthorizedHost()
        {
            var validator = new DefaultScopeValidator();
            var tool = new FakeTool("fetch");
            var invocation = new ToolInvocation(
                "fetch",
                new Dictionary<string, object> { ["url"] = "https://evil.com/data" });
            var scope =
                new ToolInputScope { AllowedHosts = ["api.example.com", "internal.corp.net"] };

            var result = await validator.ValidateAsync(tool, invocation, scope);

            result.IsWithinScope.Should().BeFalse();
            result.Violation.Should().Contain("evil.com");
        }

        [Fact]
        public async Task AllowedPaths_Allows_InScopePath()
        {
            var validator = new DefaultScopeValidator();
            var tool = new FakeTool("write");
            var invocation = new ToolInvocation(
                "write",
                new Dictionary<string, object> { ["path"] = "/home/user/document.txt" });
            var scope = new ToolInputScope { AllowedPaths = ["/home/user"] };

            var result = await validator.ValidateAsync(tool, invocation, scope);

            result.IsWithinScope.Should().BeTrue();
        }

        [Fact]
        public async Task AllowedPaths_Blocks_OutOfScopePath()
        {
            var validator = new DefaultScopeValidator();
            var tool = new FakeTool("write");
            var invocation = new ToolInvocation(
                "write",
                new Dictionary<string, object> { ["path"] = "/etc/shadow" });
            var scope = new ToolInputScope { AllowedPaths = ["/home/user", "/tmp"] };

            var result = await validator.ValidateAsync(tool, invocation, scope);

            result.IsWithinScope.Should().BeFalse();
            result.Violation.Should().Contain("outside allowed paths");
        }

        [Fact]
        public async Task MaxInputSizeBytes_Blocks_OversizedArgument()
        {
            var validator = new DefaultScopeValidator();
            var tool = new FakeTool("write");
            var largeString = new string('x', 1000);
            var invocation = new ToolInvocation(
                "write",
                new Dictionary<string, object> { ["content"] = largeString });
            var scope = new ToolInputScope { MaxInputSizeBytes = 100 };

            var result = await validator.ValidateAsync(tool, invocation, scope);

            result.IsWithinScope.Should().BeFalse();
            result.Violation.Should().Contain("exceeds maximum size");
            result.ViolatingArgument.Should().Be("content");
        }

        [Fact]
        public async Task ReadOnly_Scope_Values()
        {
            var scope = ToolInputScope.ReadOnly;

            scope.AllowWrites.Should().BeFalse();
            scope.AllowExecution.Should().BeFalse();
            scope.AllowSecretAccess.Should().BeFalse();
        }

        [Fact]
        public async Task Unrestricted_Scope_AllowsEverything()
        {
            var validator = new DefaultScopeValidator();
            var tool = new FakeTool("write");
            var invocation = new ToolInvocation(
                "write",
                new Dictionary<string, object>
                    {
                        ["path"] = "/etc/passwd", ["url"] = "https://evil.com/exfil"
                    });

            var result = await validator.ValidateAsync(
                             tool,
                             invocation,
                             ToolInputScope.Unrestricted);

            result.IsWithinScope.Should().BeTrue();
        }
    }

    public sealed class ToolInputScopeTests
    {
        [Fact]
        public void ReadOnly_RestrictsEverything()
        {
            var scope = ToolInputScope.ReadOnly;

            scope.AllowWrites.Should().BeFalse();
            scope.AllowExecution.Should().BeFalse();
            scope.AllowSecretAccess.Should().BeFalse();
        }

        [Fact]
        public void Unrestricted_AllowsEverything()
        {
            var scope = ToolInputScope.Unrestricted;

            scope.AllowWrites.Should().BeTrue();
            scope.AllowExecution.Should().BeTrue();
            scope.AllowSecretAccess.Should().BeTrue();
            scope.AllowedPaths.Should().BeEmpty();
            scope.AllowedHosts.Should().BeEmpty();
            scope.MaxInputSizeBytes.Should().BeNull();
        }
    }

    private static DefaultAgentContext CreateContext() =>
        new() { Goal = "test", State = new AgentState(), Memory = new InMemoryAgentMemory() };

    private sealed class FakeTool : ITool
    {
        private readonly string? _dangerLevel;

        public ToolDefinition Definition => new(Name, Description, DangerLevel: _dangerLevel);

        public string Description => $"Fake tool: {Name}";

        public string Name { get; }

        public FakeTool(string name, string? dangerLevel = null)
        {
            Name = name;
            _dangerLevel = dangerLevel;
        }

        public Task<ToolResult> InvokeAsync(
            ToolInvocation invocation,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ToolResult(true, "ok"));
        }
    }
}
