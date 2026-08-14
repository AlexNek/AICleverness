using AiCleverness.Abstractions;

namespace AiCleverness.Runtime;

/// <summary>
/// Shared access to the per-execution step log stored in the execution items
/// under <see cref="ExecutionItemKeys.Steps"/>.
/// </summary>
internal static class ExecutionSteps
{
    /// <summary>Appends a step to the shared step log, creating it if needed.</summary>
    public static void Add(IExecutionContext context, string step) =>
        Get(context).Add(step);

    /// <summary>
    /// Gets the shared step log, creating it in the execution items if it does not exist yet.
    /// </summary>
    public static List<string> Get(IExecutionContext context) =>
        context.Items.GetOrAdd(ExecutionItemKeys.Steps, () => new List<string>());
}
