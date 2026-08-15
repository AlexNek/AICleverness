# Streaming Tool Buffer

Some providers send a tool call as many small JSON parts instead of one
complete message. The `ToolCallBuffer` collects these parts and puts them
back together into a complete tool call.

```csharp
var buffer = new ToolCallBuffer();

// Give the buffer the streaming parts
buffer.Accumulate([new StreamingToolCallUpdate("call-1", "search", "{\"q\":")]);
buffer.Accumulate([new StreamingToolCallUpdate("call-1", null, "\"AI\"}")]);

// Take out the tool calls that are now complete
var ready = buffer.FlushCompleted();
// ready[0] = CompletedToolCall("call-1", "search", "{\"q\":\"AI\"}")
```

## Input and Output

- **Input** — `StreamingToolCallUpdate` records with `ToolCallId`,
  `FunctionName` (only needed with the first part), and `ArgumentsChunk`
  (one part of the arguments JSON).
- **Output** — `CompletedToolCall` records with `Id`, `Name`, and the
  complete `Arguments`.

## What the Buffer Handles Correctly

- JSON objects and arrays inside the arguments
- Text values that contain braces themselves
- Escaped quotes inside text values
- Several tool calls mixed together in one stream
- Arguments that are arrays

The buffer only returns a tool call when its argument JSON is complete.
Your tools never receive a cut-off input.
