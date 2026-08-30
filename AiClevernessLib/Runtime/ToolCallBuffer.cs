using System.Text;

using AiCleverness.Models;

namespace AiCleverness.Runtime;

/// <summary>
/// Accumulates partial streaming tool call chunks into complete, executable tool calls.
/// Handles multiple concurrent tool call accumulations and interleaved text.
/// </summary>
internal sealed class ToolCallBuffer
{
    private readonly Dictionary<string, ToolCallAccumulator> _accumulators = new();

    /// <summary>
    /// Gets the number of tool calls currently being accumulated.
    /// </summary>
    public int PendingCount => _accumulators.Count;

    /// <summary>
    /// Accumulates streaming tool call updates.
    /// Call this for each streamed chunk that contains tool call information.
    /// </summary>
    public void Accumulate(IReadOnlyList<StreamingToolCallUpdate>? toolCallUpdates)
    {
        if (toolCallUpdates is null or { Count: 0 })
            return;

        foreach (var update in toolCallUpdates)
        {
            if (!_accumulators.TryGetValue(update.ToolCallId, out var accumulator))
            {
                accumulator = new ToolCallAccumulator(update.ToolCallId);
                _accumulators[update.ToolCallId] = accumulator;
            }

            if (update.FunctionName is not null)
                accumulator.FunctionName = update.FunctionName;

            if (update.ArgumentsChunk is not null)
                accumulator.ArgumentsBuilder.Append(update.ArgumentsChunk);
        }
    }

    /// <summary>
    /// Forces flush of all accumulators regardless of completeness.
    /// Used at end-of-stream to handle tool calls that never properly closed.
    /// </summary>
    public IReadOnlyList<CompletedToolCall> FlushAll()
    {
        var completed = new List<CompletedToolCall>();

        foreach (var (id, accumulator) in _accumulators)
        {
            if (accumulator.FunctionName is not null)
            {
                completed.Add(
                    new CompletedToolCall(
                        id,
                        accumulator.FunctionName,
                        accumulator.GetArguments()));
            }
        }

        _accumulators.Clear();
        return completed;
    }

    /// <summary>
    /// Flushes all tool calls that have syntactically complete JSON arguments.
    /// Completed accumulators are removed from the buffer to free memory.
    /// </summary>
    public IReadOnlyList<CompletedToolCall> FlushCompleted()
    {
        var completed = new List<CompletedToolCall>();
        var toRemove = new List<string>();

        foreach (var (id, accumulator) in _accumulators)
        {
            if (accumulator.FunctionName is null)
                continue;

            var args = accumulator.GetArguments();
            if (IsJsonComplete(args))
            {
                completed.Add(new CompletedToolCall(id, accumulator.FunctionName, args));
                toRemove.Add(id);
            }
        }

        foreach (var id in toRemove)
            _accumulators.Remove(id);

        return completed;
    }

    /// <summary>
    /// Checks if a JSON string is syntactically complete (balanced braces/brackets).
    /// </summary>
    private static bool IsJsonComplete(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        var trimmed = json.AsSpan().Trim();
        if (trimmed.Length == 0)
            return false;

        var firstChar = trimmed[0];
        if (firstChar != '{' && firstChar != '[')
            return false;

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            switch (c)
            {
                case '{':
                case '[':
                    depth++;
                    break;
                case '}':
                case ']':
                    depth--;
                    if (depth == 0)
                        return i == trimmed.Length - 1;
                    break;
            }
        }

        return depth == 0;
    }
}
