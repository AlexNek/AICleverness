using System.Text.Json;

using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime.Security;

/// <summary>
/// Default implementation of <see cref="IScopeValidator"/> that checks argument values
/// against the scope constraints (size limits, path restrictions, etc.).
/// </summary>
public sealed class DefaultScopeValidator : IScopeValidator
{
    /// <inheritdoc/>
    public Task<ScopeValidationResult> ValidateAsync(
        ITool tool,
        ToolInvocation invocation,
        ToolInputScope scope,
        CancellationToken cancellationToken = default)
    {
        // Check input size limit.
        if (scope.MaxInputSizeBytes.HasValue)
        {
            foreach (var (key, value) in invocation.Arguments)
            {
                var size = EstimateSize(value);
                if (size > scope.MaxInputSizeBytes.Value)
                {
                    return Task.FromResult(
                        new ScopeValidationResult(
                            false,
                            $"Argument '{key}' exceeds maximum size ({size} bytes > {scope.MaxInputSizeBytes.Value} bytes limit).",
                            key));
                }
            }
        }

        // Check path restrictions.
        if (scope.AllowedPaths.Count > 0)
        {
            foreach (var (key, value) in invocation.Arguments)
            {
                if (value is string str && LooksLikePath(str))
                {
                    if (!scope.AllowedPaths.Any(p => str.StartsWith(
                            p,
                            StringComparison.OrdinalIgnoreCase)))
                    {
                        return Task.FromResult(
                            new ScopeValidationResult(
                                false,
                                $"Argument '{key}' contains path '{str}' outside allowed paths.",
                                key));
                    }
                }
            }
        }

        // Check host restrictions.
        if (scope.AllowedHosts.Count > 0)
        {
            foreach (var (key, value) in invocation.Arguments)
            {
                if (value is string str && Uri.TryCreate(str, UriKind.Absolute, out var uri))
                {
                    if (!scope.AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
                    {
                        return Task.FromResult(
                            new ScopeValidationResult(
                                false,
                                $"Argument '{key}' references host '{uri.Host}' outside allowed hosts.",
                                key));
                    }
                }
            }
        }

        return Task.FromResult(new ScopeValidationResult(true));
    }

    private static long EstimateSize(object? value)
    {
        if (value is null) return 0;
        if (value is string str) return str.Length * 2L;
        if (value is byte[] bytes) return bytes.Length;
        // Fallback: serialize and measure
        try
        {
            var json = JsonSerializer.Serialize(value, AiClevernessJsonContext.Default.Object);
            return json.Length * 2L;
        }
        catch
        {
            return 0;
        }
    }

    private static bool LooksLikePath(string value)
    {
        return value.Contains('/') || value.Contains('\\') ||
               value.StartsWith("~", StringComparison.Ordinal) ||
               (value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':');
    }
}
