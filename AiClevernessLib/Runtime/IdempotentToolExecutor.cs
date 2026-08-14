using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using AiCleverness.Abstractions;
using AiCleverness.Models;

using Microsoft.Extensions.Logging;

namespace AiCleverness.Runtime;

/// <summary>
/// Decorator around <see cref="IToolExecutor"/> that checks an idempotency cache
/// before executing a tool. Prevents duplicate execution of side-effecting tools
/// during quality-gate retries.
/// </summary>
/// <remarks>
/// <para>
/// Cache key is computed as: {executionScope}:{invocationId} when InvocationId is set,
/// or {executionScope}:{toolName}:{argumentsHash} as a semantic fallback.
/// </para>
/// <para>
/// Only successful results are cached. Failed invocations are always retried.
/// </para>
/// </remarks>
public sealed class IdempotentToolExecutor : IToolExecutor
{
    private readonly IIdempotencyCache _cache;

    private readonly string _executionScope;

    private readonly IToolExecutor _inner;

    private readonly ILogger<IdempotentToolExecutor>? _logger;

    /// <param name="inner">The underlying tool executor to delegate to.</param>
    /// <param name="cache">The idempotency cache.</param>
    /// <param name="executionScope">
    /// Scope prefix for cache keys (typically the execution ID).
    /// Prevents cross-execution cache pollution.
    /// </param>
    /// <param name="logger">Optional logger.</param>
    public IdempotentToolExecutor(
        IToolExecutor inner,
        IIdempotencyCache cache,
        string executionScope,
        ILogger<IdempotentToolExecutor>? logger = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _executionScope = executionScope ?? throw new ArgumentNullException(nameof(executionScope));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ToolResult> ExecuteAsync(
        ITool tool,
        ToolInvocation invocation,
        ToolExecutionPolicy policy,
        CancellationToken cancellationToken)
    {
        var key = ComputeKey(tool, invocation);

        if (_cache.TryGet(key, out var cached))
        {
            _logger?.LogDebug(
                "Idempotency cache hit for tool {ToolName} (key: {Key})",
                tool.Name,
                key);
            return cached;
        }

        var result = await _inner.ExecuteAsync(tool, invocation, policy, cancellationToken);

        if (result.Success)
        {
            _cache.Set(key, result);
            _logger?.LogDebug(
                "Cached successful result for tool {ToolName} (key: {Key})",
                tool.Name,
                key);
        }

        return result;
    }

    private string ComputeKey(ITool tool, ToolInvocation invocation)
    {
        if (!string.IsNullOrWhiteSpace(invocation.InvocationId))
            return $"{_executionScope}:{invocation.InvocationId}";

        return $"{_executionScope}:{tool.Name}:{HashArguments(invocation.Arguments)}";
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Arguments are simple types serialized for hashing only.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "Arguments are simple types serialized for hashing only.")]
    private static string HashArguments(IReadOnlyDictionary<string, object> arguments)
    {
        if (arguments.Count == 0)
            return "empty";

        var sorted = arguments
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={JsonSerializer.Serialize(kv.Value)}")
            .ToArray();

        var combined = string.Join("&", sorted);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(bytes)[..16]; // First 8 bytes = 16 hex chars
    }
}
