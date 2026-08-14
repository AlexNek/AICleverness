using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Security;

/// <summary>
/// Tool-call validator that classifies danger level from the tool definition
/// and blocks calls that exceed the configured maximum allowed level.
/// </summary>
public sealed class DangerLevelToolCallValidator : IToolCallValidator
{
    private readonly DangerLevel _maxAllowedLevel;

    public string Name => "DangerLevelValidator";

    /// <summary>
    /// Creates a danger-level validator with the specified maximum allowed level.
    /// Tools with a danger level above this will be blocked.
    /// </summary>
    public DangerLevelToolCallValidator(DangerLevel maxAllowedLevel = DangerLevel.High)
    {
        _maxAllowedLevel = maxAllowedLevel;
    }

    /// <inheritdoc/>
    public Task<ToolCallValidationResult> ValidateAsync(
        ITool tool,
        ToolInvocation invocation,
        IAgentContext context,
        CancellationToken cancellationToken = default)
    {
        var dangerLevel = ParseDangerLevel(tool.Definition.DangerLevel);

        if (dangerLevel > _maxAllowedLevel)
        {
            return Task.FromResult(
                new ToolCallValidationResult(
                    false,
                    $"Tool '{tool.Name}' has danger level '{dangerLevel}' which exceeds the maximum allowed level '{_maxAllowedLevel}'.",
                    dangerLevel));
        }

        return Task.FromResult(new ToolCallValidationResult(true, null, dangerLevel));
    }

    private static DangerLevel ParseDangerLevel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DangerLevel.Safe;

        return Enum.TryParse<DangerLevel>(value, ignoreCase: true, out var level)
                   ? level
                   : DangerLevel.Safe;
    }
}
