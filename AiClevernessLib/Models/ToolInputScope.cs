namespace AiCleverness.Models;

/// <summary>Defines the scope and isolation constraints for tool inputs.</summary>
public sealed record ToolInputScope
{
    public IReadOnlyList<string> AllowedEnvironmentVariables { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AllowedHosts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AllowedPaths { get; init; } = Array.Empty<string>();
    public bool AllowExecution { get; init; }
    public bool AllowSecretAccess { get; init; }
    public bool AllowWrites { get; init; } = true;
    public long? MaxInputSizeBytes { get; init; }
    public IReadOnlyDictionary<string, object> Properties { get; init; } = new Dictionary<string, object>();
    public static ToolInputScope ReadOnly => new() { AllowSecretAccess = false, AllowWrites = false, AllowExecution = false };
    public static ToolInputScope Unrestricted => new() { AllowSecretAccess = true, AllowWrites = true, AllowExecution = true };
}
