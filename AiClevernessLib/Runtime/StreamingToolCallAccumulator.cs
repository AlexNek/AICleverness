using System.Text;

using AiCleverness.Models;

namespace AiCleverness.Runtime;

/// <summary>
/// Accumulates <see cref="LlmToolCallDelta"/> fragments by index into
/// complete <see cref="LlmToolCall"/> instances. Used by the streaming
/// LLM call strategy to assemble tool calls from incremental chunks.
/// </summary>
internal sealed class StreamingToolCallAccumulator
{
    private readonly Dictionary<int, ToolCallEntry> _entries = new();

    /// <summary>
    /// Adds a batch of deltas to the accumulator.
    /// Each delta is routed to the correct entry by its <see cref="LlmToolCallDelta.Index"/>.
    /// </summary>
    public void AddDeltas(IReadOnlyList<LlmToolCallDelta>? deltas)
    {
        if (deltas is null or { Count: 0 })
            return;

        foreach (var delta in deltas)
        {
            if (!_entries.TryGetValue(delta.Index, out var entry))
            {
                entry = new ToolCallEntry();
                _entries[delta.Index] = entry;
            }

            if (delta.Id is not null)
                entry.Id = delta.Id;

            if (delta.Name is not null)
                entry.Name = delta.Name;

            if (delta.ArgumentsFragment is not null)
                entry.Arguments.Append(delta.ArgumentsFragment);
        }
    }

    /// <summary>
    /// Gets whether any deltas have been accumulated.
    /// </summary>
    public bool HasEntries => _entries.Count > 0;

    /// <summary>
    /// Builds the final list of <see cref="LlmToolCall"/> from accumulated fragments.
    /// Entries without an Id or Name are assigned generated values.
    /// </summary>
    public IReadOnlyList<LlmToolCall> Build()
    {
        if (_entries.Count == 0)
            return Array.Empty<LlmToolCall>();

        var result = new List<LlmToolCall>(_entries.Count);

        foreach (var (index, entry) in _entries.OrderBy(kv => kv.Key))
        {
            var id = entry.Id ?? $"call_{index}";
            var name = entry.Name ?? $"unknown_{index}";
            var arguments = entry.Arguments.ToString();

            result.Add(new LlmToolCall(id, name, arguments));
        }

        return result;
    }

    private sealed class ToolCallEntry
    {
        public StringBuilder Arguments { get; } = new();

        public string? Id { get; set; }

        public string? Name { get; set; }
    }
}
