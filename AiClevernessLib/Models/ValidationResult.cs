namespace AiCleverness.Models;

/// <summary>
/// Simple pass/fail validation result.
/// </summary>
public sealed record ValidationResult(
    bool IsValid,
    string? Error = null);
