namespace AiCleverness.Models;

/// <summary>
/// Categorizes the danger level of a tool invocation.
/// Used by validators, approval services, and policies to make access control decisions.
/// </summary>
public enum DangerLevel
{
    /// <summary>Safe operation with no side effects.</summary>
    Safe,

    /// <summary>Low-risk operation (e.g., reading non-sensitive data).</summary>
    Low,

    /// <summary>Medium-risk operation (e.g., writing to a file, sending a non-critical API call).</summary>
    Medium,

    /// <summary>High-risk operation (e.g., deleting data, executing code, accessing secrets).</summary>
    High,

    /// <summary>Critical operation requiring explicit approval (e.g., production deployment, financial transaction).</summary>
    Critical
}
