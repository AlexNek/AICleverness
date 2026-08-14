using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Validates the DI container at startup to detect missing or misconfigured services.
/// </summary>
/// <remarks>
/// <para>
/// The analyzer inspects the <see cref="IServiceProvider"/> for required and optional
/// services, producing a <see cref="StartupAnalysisResult"/> with findings and suggestions.
/// </para>
/// <para>
/// This is typically called during application startup, before the first execution.
/// </para>
/// </remarks>
public interface IStartupAnalyzer
{
    /// <summary>
    /// Analyzes the service provider and returns findings.
    /// </summary>
    /// <param name="serviceProvider">The application's service provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<StartupAnalysisResult> AnalyzeAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);
}
