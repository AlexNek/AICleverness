namespace AiCleverness.Models;

/// <summary>
/// Defines the scope and isolation constraints for tool inputs.
/// Used to restrict what data a tool can access or modify.
/// </summary>
public sealed record ToolInputScope
{
    /// <summary>
    /// Allowed environment variable names the tool may read. Empty means unrestricted.
    /// </summary>
    public IReadOnlyList<string> AllowedEnvironmentVariables { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Allowed network hosts the tool may contact. Empty means unrestricted.
    /// </summary>
    public IReadOnlyList<string> AllowedHosts { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Allowed file system paths the tool may access. Empty means unrestricted.
    /// </summary>
    public IReadOnlyList<string> AllowedPaths { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Whether the tool may execute external processes or code.
    /// </summary>
    public bool AllowExecution { get; init; }

    /// <summary>
    /// Whether the tool may access secrets/credentials.
    /// </summary>
    public bool AllowSecretAccess { get; init; }

    /// <summary>
    /// Whether the tool may perform write/mutation operations.
    /// </summary>
    public bool AllowWrites { get; init; } = true;

    /// <summary>
    /// Maximum size in bytes for any single input argument. Null means unrestricted.
    /// </summary>
    public long? MaxInputSizeBytes { get; init; }

    /// <summary>
    /// Custom scope properties for extension scenarios.
    /// </summary>
    public IReadOnlyDictionary<string, object> Properties { get; init; } =
        new Dictionary<string, object>();

    /// <summary>Creates a read-only scope (no writes, no execution, no secrets).</summary>
    public static ToolInputScope ReadOnly =>
        new() { AllowSecretAccess = false, AllowWrites = false, AllowExecution = false };

    /// <summary>Creates an unrestricted scope (everything allowed).</summary>
    public static ToolInputScope Unrestricted =>
        new() { AllowSecretAccess = true, AllowWrites = true, AllowExecution = true };
}

/// <summary>
/// Result of scope validation against a tool invocation.
/// </summary>
public sealed record ScopeValidationResult(
    bool IsWithinScope,
    string? Violation = null,
    string? ViolatingArgument = null);
