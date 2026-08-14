using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiClevernessLib.Demo;

/// <summary>
/// Sample tool that returns deterministic weather data for a city.
/// </summary>
public sealed class WeatherTool : ITool
{
    /// <summary>Tool name used for registration and LLM tool calls.</summary>
    public const string ToolName = "get_weather";

    private const string CityArgument = "city";

    private const string DescriptionText = "Returns the current weather for a city.";

    public ToolDefinition Definition { get; } = new(
        ToolName,
        DescriptionText,
        ParametersSchema:
        """
        {"type":"object","properties":{"city":{"type":"string","description":"City name"}},"required":["city"]}
        """);

    public string Description => DescriptionText;

    public string Name => ToolName;

    /// <inheritdoc />
    public Task<ToolResult> InvokeAsync(
        ToolInvocation invocation,
        CancellationToken cancellationToken = default)
    {
        if (invocation.Arguments.TryGetValue(CityArgument, out var raw) &&
            raw is string city &&
            !string.IsNullOrWhiteSpace(city))
        {
            return Task.FromResult(
                new ToolResult(true, $"Weather in {city}: 21°C, partly cloudy."));
        }

        return Task.FromResult(
            new ToolResult(false, Error: $"Missing required argument '{CityArgument}'."));
    }
}
