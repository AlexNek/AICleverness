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
                if (!TryEstimateSize(value, out var size))
                {
                    return Task.FromResult(
                        new ScopeValidationResult(
                            false,
                            $"Argument '{key}' could not be safely size-validated.",
                            key));
                }

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
                    if (!scope.AllowedPaths.Any(p => IsWithinAllowedPath(str, p)))
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

    private static bool TryEstimateSize(object? value, out long size)
    {
        if (value is null)
        {
            size = 0;
            return true;
        }

        if (value is string str)
        {
            size = str.Length * 2L;
            return true;
        }

        if (value is byte[] bytes)
        {
            size = bytes.Length;
            return true;
        }

        // Fallback: serialize and measure. Serialization failure is rejected by the caller.
        try
        {
            var json = JsonSerializer.Serialize(value, AiClevernessJsonContext.Default.Object);
            size = json.Length * 2L;
            return true;
        }
        catch
        {
            size = 0;
            return false;
        }
    }

    private static bool IsWithinAllowedPath(string candidatePath, string allowedPath)
    {
        try
        {
            var candidateFullPath = Path.GetFullPath(candidatePath);
            var allowedFullPath = Path.GetFullPath(allowedPath);
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (string.Equals(candidateFullPath, allowedFullPath, comparison))
                return true;

            var root = Path.GetPathRoot(allowedFullPath);
            var boundary = allowedFullPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            if (boundary.Length == 0)
                boundary = root ?? allowedFullPath;

            if (!boundary.EndsWith(Path.DirectorySeparatorChar)
                && !boundary.EndsWith(Path.AltDirectorySeparatorChar))
            {
                boundary += Path.DirectorySeparatorChar;
            }

            return candidateFullPath.StartsWith(boundary, comparison);
        }
        catch
        {
            // Invalid or unsupported paths must not pass a security boundary.
            return false;
        }
    }

    private static bool LooksLikePath(string value)
    {
        return value.Contains('/') || value.Contains('\\') ||
               value.StartsWith("~", StringComparison.Ordinal) ||
               (value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':');
    }
}
