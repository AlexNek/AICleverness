# Streaming Tool Buffer

When an LLM streams tool calls as partial JSON chunks, the `ToolCallBuffer`
accumulates them into complete invocations.

```csharp
var buffer = new ToolCallBuffer();

// Feed streaming chunks
buffer.Accumulate([new StreamingToolCallUpdate("call-1", "search", "{\"q\":")]);
buffer.Accumulate([new StreamingToolCallUpdate("call-1", null, "\"AI\"}")]);

// Flush completed tool calls (balanced JSON detected)
var ready = buffer.FlushCompleted();
// ready[0] = CompletedToolCall("call-1", "search", "{\"q\":\"AI\"}")
```

## Inputs and Outputs

- **Input** — `StreamingToolCallUpdate` records: `ToolCallId`,
  `FunctionName` (only needed on the first chunk), `ArgumentsChunk`
- **Output** — `CompletedToolCall` records: `Id`, `Name`, `Arguments`

## What the Buffer Handles

- Nested JSON objects and arrays
- Strings containing braces
- Escaped quotes inside strings
- Multiple concurrent tool calls interleaved in one stream
- Array arguments

Flush only returns calls whose accumulated argument JSON is balanced, so
tools never receive truncated input.
