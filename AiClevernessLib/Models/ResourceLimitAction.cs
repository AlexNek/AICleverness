namespace AiCleverness.Models;

/// <summary>Action to take when a resource limit is exceeded.</summary>
public enum ResourceLimitAction
{
    Halt,
    Warn,
    Throttle
}
