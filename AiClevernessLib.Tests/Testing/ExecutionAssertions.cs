using AiCleverness.Models;

namespace AiClevernessLib.Tests.Testing;

/// <summary>
/// Fluent assertion helpers for <see cref="AgentResult"/>.
/// Provides readable assertions for agent execution outcomes.
/// </summary>
public static class ExecutionAssertions
{
    /// <summary>
    /// Asserts that the result output contains the specified text.
    /// </summary>
    public static AgentResult ShouldContainOutput(this AgentResult result, string expected)
    {
        if (result.Output is null || !result.Output.Contains(expected, StringComparison.Ordinal))
        {
            throw new AgentAssertionException(
                $"Expected output to contain '{expected}', " +
                $"but got: '{result.Output ?? "(null)"}'");
        }

        return result;
    }

    /// <summary>
    /// Asserts that the result failed.
    /// </summary>
    /// <exception cref="AgentAssertionException">The result was successful.</exception>
    public static AgentResult ShouldFail(this AgentResult result)
    {
        if (result.Success)
        {
            throw new AgentAssertionException(
                $"Expected agent execution to fail, but it succeeded. " +
                $"Output: {result.Output ?? "(none)"}");
        }

        return result;
    }

    /// <summary>
    /// Asserts that the result has at least the specified number of steps.
    /// </summary>
    public static AgentResult ShouldHaveAtLeastSteps(this AgentResult result, int minCount)
    {
        if (result.Steps.Count < minCount)
        {
            throw new AgentAssertionException(
                $"Expected at least {minCount} step(s), but got {result.Steps.Count}.");
        }

        return result;
    }

    /// <summary>
    /// Asserts that the result has no errors (successful with no reasoning indicating failure).
    /// </summary>
    public static AgentResult ShouldHaveNoErrors(this AgentResult result)
    {
        if (!result.Success)
        {
            throw new AgentAssertionException(
                $"Expected no errors, but execution failed: {result.Reasoning ?? "(no reasoning)"}");
        }

        return result;
    }

    /// <summary>
    /// Asserts that the result steps contain a step matching the given predicate.
    /// </summary>
    public static AgentResult ShouldHaveStepMatching(
        this AgentResult result,
        Func<string, bool> predicate)
    {
        if (!result.Steps.Any(predicate))
        {
            throw new AgentAssertionException(
                $"Expected at least one step matching the predicate, " +
                $"but none found. Steps: [{string.Join(", ", result.Steps)}]");
        }

        return result;
    }

    /// <summary>
    /// Asserts that the result has usage information.
    /// </summary>
    public static AgentResult ShouldHaveUsage(this AgentResult result)
    {
        if (result.Usage is null)
        {
            throw new AgentAssertionException("Expected usage information, but it was null.");
        }

        return result;
    }

    /// <summary>
    /// Asserts that the result output does not contain the specified text.
    /// </summary>
    public static AgentResult ShouldNotContainOutput(this AgentResult result, string unexpected)
    {
        if (result.Output is not null
            && result.Output.Contains(unexpected, StringComparison.Ordinal))
        {
            throw new AgentAssertionException(
                $"Expected output to NOT contain '{unexpected}', " +
                $"but it did.");
        }

        return result;
    }

    /// <summary>
    /// Asserts that the result was successful.
    /// </summary>
    /// <exception cref="AgentAssertionException">The result was not successful.</exception>
    public static AgentResult ShouldSucceed(this AgentResult result)
    {
        if (!result.Success)
        {
            throw new AgentAssertionException(
                $"Expected agent execution to succeed, but it failed. " +
                $"Reasoning: {result.Reasoning ?? "(none)"}");
        }

        return result;
    }

    /// <summary>
    /// Asserts that the total prompt tokens are at least the specified amount.
    /// </summary>
    public static AgentResult ShouldUseAtLeastPromptTokens(this AgentResult result, int minTokens)
    {
        if (result.Usage is null || result.Usage.PromptTokens < minTokens)
        {
            var actual = result.Usage?.PromptTokens ?? 0;
            throw new AgentAssertionException(
                $"Expected at least {minTokens} prompt tokens, but got {actual}.");
        }

        return result;
    }
}

/// <summary>
/// Exception thrown when an agent execution assertion fails.
/// </summary>
public sealed class AgentAssertionException : Exception
{
    public AgentAssertionException(string message)
        : base(message)
    {
    }
}
