namespace AiCleverness.Models;

/// <summary>Result of scope validation against a tool invocation.</summary>
public sealed record ScopeValidationResult(
    bool IsWithinScope,
    string? Violation = null,
    string? ViolatingArgument = null);
