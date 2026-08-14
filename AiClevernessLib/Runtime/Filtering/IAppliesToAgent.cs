using AiCleverness.Abstractions;

namespace AiCleverness.Runtime.Filtering;

/// <summary>
/// Internal marker interface for filtered wrappers.
/// Checked by middleware to skip non-applicable extensions.
/// </summary>
internal interface IAppliesToAgent
{
    bool AppliesTo(IAgentContext context);
}
