using AiCleverness.Models;

namespace AiCleverness.Abstractions;

/// <summary>
/// Abstraction for scheduling deferred or recurring agent executions.
/// </summary>
/// <remarks>
/// <para>
/// Implementations might use in-memory timers, background services,
/// or external schedulers (quartz, Hangfire, OS cron). The interface is
/// intentionally minimal and does not dictate the scheduling mechanism.
/// </para>
/// </remarks>
public interface IExecutionScheduler
{
    /// <summary>
    /// Cancels (disables) a schedule so it will no longer fire.
    /// </summary>
    /// <param name="scheduleId">Schedule identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the schedule was found and cancelled.</returns>
    Task<bool> CancelAsync(
        string scheduleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a schedule permanently.
    /// </summary>
    /// <param name="scheduleId">Schedule identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the schedule was found and deleted.</returns>
    Task<bool> DeleteAsync(
        string scheduleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a schedule by its identifier.
    /// </summary>
    /// <param name="scheduleId">Schedule identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The schedule, or <c>null</c> if not found.</returns>
    Task<ScheduledExecution?> GetAsync(
        string scheduleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the execution history for a given schedule.
    /// </summary>
    /// <param name="scheduleId">Schedule identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ScheduledExecutionResult>> GetHistoryAsync(
        string scheduleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all active (enabled and not expired) schedules.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ScheduledExecution>> ListActiveAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a new execution.
    /// </summary>
    /// <param name="schedule">The schedule definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The schedule identifier.</returns>
    Task<string> ScheduleAsync(
        ScheduledExecution schedule,
        CancellationToken cancellationToken = default);
}
