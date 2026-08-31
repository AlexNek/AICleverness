namespace AiCleverness.Abstractions;

/// <summary>
/// Result of input validation.
/// </summary>
public sealed record InputValidationResult(
    bool IsValid,
    string? Error = null)
{
    /// <summary>A valid result.</summary>
    public static InputValidationResult Valid => new(true);

    /// <summary>Creates an invalid result with error.</summary>
    public static InputValidationResult Invalid(string error) => new(false, error);
}
