using AiCleverness.Abstractions;
using AiCleverness.Models;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public sealed class ResourceAndRecoveryTests
{
    public sealed class PartialSuccessTests
    {
        [Fact]
        public void IsPartialSuccess_WhenAllSucceeded_ReturnsFalse()
        {
            var result = new PartialSuccessResult
                             {
                                 Result = new AgentResult(true, "done"),
                                 SucceededSteps =
                                     [
                                         new StepOutcome("s1", StepStatus.Succeeded),
                                         new StepOutcome("s2", StepStatus.Succeeded)
                                     ]
                             };

            result.IsPartialSuccess.Should().BeFalse();
            result.SuccessRatio.Should().Be(1.0);
        }

        [Fact]
        public void IsPartialSuccess_WhenMixed_ReturnsTrue()
        {
            var result = new PartialSuccessResult
                             {
                                 Result = new AgentResult(true, "partial"),
                                 SucceededSteps =
                                         [new StepOutcome("s1", StepStatus.Succeeded, "ok")],
                                 FailedSteps =
                                         [new StepOutcome("s2", StepStatus.Failed, Error: "boom")]
                             };

            result.IsPartialSuccess.Should().BeTrue();
            result.SuccessRatio.Should().Be(0.5);
            result.TotalSteps.Should().Be(2);
        }

        [Fact]
        public void SuccessRatio_WithSkipped_CalculatesCorrectly()
        {
            var result = new PartialSuccessResult
                             {
                                 Result = new AgentResult(false, "partial"),
                                 SucceededSteps = [new StepOutcome("s1", StepStatus.Succeeded)],
                                 FailedSteps = [new StepOutcome("s2", StepStatus.Failed)],
                                 SkippedSteps = [new StepOutcome("s3", StepStatus.Skipped)]
                             };

            result.TotalSteps.Should().Be(3);
            result.SuccessRatio.Should().BeApproximately(1.0 / 3.0, 0.01);
        }
    }

    public sealed class RecoveryDecisionTests
    {
        [Fact]
        public void Abort_CreatesCorrectDecision()
        {
            var decision = RecoveryDecision.Abort("fatal");

            decision.Action.Should().Be(RecoveryAction.Abort);
        }

        [Fact]
        public void Compensate_CreatesCorrectDecision()
        {
            var decision = RecoveryDecision.Compensate("rollback needed");

            decision.Action.Should().Be(RecoveryAction.Compensate);
        }

        [Fact]
        public void Retry_CreatesCorrectDecision()
        {
            var decision = RecoveryDecision.Retry(TimeSpan.FromSeconds(2), "transient error");

            decision.Action.Should().Be(RecoveryAction.Retry);
            decision.DelayBeforeRetry.Should().Be(TimeSpan.FromSeconds(2));
            decision.Reason.Should().Be("transient error");
        }

        [Fact]
        public void Skip_CreatesCorrectDecision()
        {
            var decision = RecoveryDecision.Skip("non-critical step");

            decision.Action.Should().Be(RecoveryAction.Skip);
            decision.Reason.Should().Be("non-critical step");
        }
    }

    public sealed class ResourceLimitsTests
    {
        [Fact]
        public void DefaultAction_IsHalt()
        {
            var limits = new ResourceLimits { MaxTotalTokens = 1000 };

            limits.OnExceeded.Should().Be(ResourceLimitAction.Halt);
        }

        [Fact]
        public void Unlimited_HasNoConstraints()
        {
            var limits = ResourceLimits.Unlimited;

            limits.MaxTotalTokens.Should().BeNull();
            limits.MaxLlmCalls.Should().BeNull();
            limits.MaxToolCalls.Should().BeNull();
            limits.MaxCost.Should().BeNull();
            limits.MaxDuration.Should().BeNull();
        }
    }

    public sealed class ResourceUsageTests
    {
        [Fact]
        public void Exceeds_MaxCost_ReturnsTrue()
        {
            var usage = new ResourceUsage();
            usage.RecordLlmUsage(100, 50, 5.0m);
            var limits = new ResourceLimits { MaxCost = 4.0m };

            usage.Exceeds(limits).Should().BeTrue();
        }

        [Fact]
        public void Exceeds_MaxDuration_ReturnsTrue()
        {
            var usage = new ResourceUsage { Duration = TimeSpan.FromMinutes(5) };
            var limits = new ResourceLimits { MaxDuration = TimeSpan.FromMinutes(2) };

            usage.Exceeds(limits).Should().BeTrue();
        }

        [Fact]
        public void Exceeds_MaxLlmCalls_ReturnsTrue()
        {
            var usage = new ResourceUsage();
            usage.RecordLlmUsage(10, 10);
            usage.RecordLlmUsage(10, 10);
            usage.RecordLlmUsage(10, 10);
            var limits = new ResourceLimits { MaxLlmCalls = 2 };

            usage.Exceeds(limits).Should().BeTrue();
        }

        [Fact]
        public void Exceeds_MaxToolCalls_ReturnsTrue()
        {
            var usage = new ResourceUsage();
            usage.RecordToolCall();
            usage.RecordToolCall();
            usage.RecordToolCall();
            var limits = new ResourceLimits { MaxToolCalls = 2 };

            usage.Exceeds(limits).Should().BeTrue();
        }

        [Fact]
        public void Exceeds_MaxTotalTokens_ReturnsTrue()
        {
            var usage = new ResourceUsage();
            usage.RecordLlmUsage(5000, 5000);
            var limits = new ResourceLimits { MaxTotalTokens = 8000 };

            usage.Exceeds(limits).Should().BeTrue();
        }

        [Fact]
        public void Exceeds_UnlimitedLimits_AlwaysFalse()
        {
            var usage = new ResourceUsage();
            usage.RecordLlmUsage(999999, 999999, 999m);
            usage.RecordToolCall(999m);

            usage.Exceeds(ResourceLimits.Unlimited).Should().BeFalse();
        }

        [Fact]
        public void Exceeds_WithinLimits_ReturnsFalse()
        {
            var usage = new ResourceUsage();
            usage.RecordLlmUsage(100, 50);
            var limits = new ResourceLimits { MaxTotalTokens = 10000, MaxLlmCalls = 10 };

            usage.Exceeds(limits).Should().BeFalse();
        }

        [Fact]
        public void RecordCost_AddsCost()
        {
            var usage = new ResourceUsage();

            usage.RecordCost(5.0m);

            usage.Cost.Should().Be(5.0m);
        }

        [Fact]
        public void RecordLlmUsage_TracksTokensAndCalls()
        {
            var usage = new ResourceUsage();

            usage.RecordLlmUsage(100, 50, 0.01m);
            usage.RecordLlmUsage(200, 80, 0.02m);

            usage.InputTokens.Should().Be(300);
            usage.OutputTokens.Should().Be(130);
            usage.TotalTokens.Should().Be(430);
            usage.LlmCalls.Should().Be(2);
            usage.Cost.Should().Be(0.03m);
        }

        [Fact]
        public void RecordToolCall_TracksCallsAndCost()
        {
            var usage = new ResourceUsage();

            usage.RecordToolCall(0.001m);
            usage.RecordToolCall(0.002m);

            usage.ToolCalls.Should().Be(2);
            usage.Cost.Should().Be(0.003m);
        }
    }

    public sealed class RetryClassificationTests
    {
        [Fact]
        public void ClientError_ShouldNotRetry()
        {
            var classification = new RetryClassification(
                RetryCategory.ClientError,
                ShouldRetry: false,
                Reason: "Bad request");

            classification.ShouldRetry.Should().BeFalse();
        }

        [Fact]
        public void RateLimited_ShouldRetryAfterDelay()
        {
            var classification = new RetryClassification(
                RetryCategory.RateLimited,
                ShouldRetry: true,
                SuggestedDelay: TimeSpan.FromSeconds(30),
                MaxRetries: 3);

            classification.ShouldRetry.Should().BeTrue();
            classification.SuggestedDelay.Should().Be(TimeSpan.FromSeconds(30));
            classification.MaxRetries.Should().Be(3);
        }

        [Fact]
        public void Transient_ShouldRetry()
        {
            var classification = new RetryClassification(
                RetryCategory.Transient,
                ShouldRetry: true,
                SuggestedDelay: TimeSpan.FromSeconds(1));

            classification.ShouldRetry.Should().BeTrue();
            classification.Category.Should().Be(RetryCategory.Transient);
        }
    }
}
