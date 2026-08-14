using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiClevernessLib.Demo;

/// <summary>
/// Policy that blocks any goal containing a destructive keyword.
/// </summary>
public sealed class NoDangerousRequestsPolicy : IAgentPolicy
{
    private const string DangerousKeyword = "delete";

    public string Name => "no-dangerous-requests";

    public int Priority => 100;

    public bool AppliesTo(IAgentContext context) => true;

    /// <inheritdoc />
    public Task<PolicyResult> EvaluateAsync(
        IAgentContext context,
        CancellationToken cancellationToken = default)
    {
        var isDangerous = context.Goal.Contains(DangerousKeyword, StringComparison.OrdinalIgnoreCase);

        return Task.FromResult(
            isDangerous
                ? new PolicyResult(
                    true,
                    1.0,
                    "block",
                    $"Goal contains the forbidden keyword '{DangerousKeyword}'.")
                : new PolicyResult(true, 0.0, "allow"));
    }
}
