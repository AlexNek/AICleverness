using AiCleverness.Abstractions;
using AiCleverness.Models;
using AiCleverness.Runtime;

using FluentAssertions;

namespace AiClevernessLib.Tests.Runtime;

public class PersistenceAndHostingTests
{
    // ── CheckpointEntry ────────────────────────────────────────────────────

    [Fact]
    public void CheckpointEntry_From_CreatesFromSnapshot()
    {
        var snapshot = new ExecutionSnapshot
                           {
                               ExecutionId = "exec-1",
                               CreatedAt = DateTimeOffset.UtcNow,
                               Status = ExecutionStatus.Running,
                               Goal = "test"
                           };

        var entry = CheckpointEntry.From(snapshot, "before-tool");

        entry.ExecutionId.Should().Be("exec-1");
        entry.Status.Should().Be(ExecutionStatus.Running);
        entry.Label.Should().Be("before-tool");
        entry.CheckpointId.Should().NotBeNullOrEmpty();
        entry.CapturedAt.Should().Be(snapshot.CapturedAt);
    }
    // ── ExecutionSnapshot ──────────────────────────────────────────────────

    [Fact]
    public void ExecutionSnapshot_Capture_PreservesMetadata()
    {
        var ids = new ExecutionIds("exec-1", "trace-1", "corr-1");
        var request = new AgentRequest("Test goal", new[] { "tool-a" });
        var options = new AgentRuntimeOptions { DefaultMaxTurns = 5 };
        var metadata = ExecutionMetadata.Create(
            ids,
            request,
            options,
            new[] { "tool-a", "tool-b" });
        var state = new ExecutionState();
        state.MarkStarted();
        state.IncrementTurn();
        state.IncrementToolInvocation();
        state.MarkCompleted(ExecutionStatus.Completed);

        var result = new AgentResult(true, "output text", "reasoning");

        var snapshot = ExecutionSnapshot.Capture(metadata, state, result);

        snapshot.ExecutionId.Should().Be("exec-1");
        snapshot.TraceId.Should().Be("trace-1");
        snapshot.CorrelationId.Should().Be("corr-1");
        snapshot.Status.Should().Be(ExecutionStatus.Completed);
        snapshot.Goal.Should().Be("Test goal");
        snapshot.AvailableToolNames.Should().Contain("tool-a").And.Contain("tool-b");
        snapshot.TurnCount.Should().Be(1);
        snapshot.ToolInvocationCount.Should().Be(1);
        snapshot.ResultOutput.Should().Be("output text");
        snapshot.ResultReasoning.Should().Be("reasoning");
        snapshot.ResultSuccess.Should().BeTrue();
        snapshot.SchemaVersion.Should().Be(1);
    }

    [Fact]
    public void ExecutionSnapshot_Capture_WithoutResult_LeavesResultFieldsNull()
    {
        var ids = ExecutionIds.Create();
        var metadata = ExecutionMetadata.Create(
            ids,
            new AgentRequest("g"),
            new AgentRuntimeOptions());
        var state = new ExecutionState();

        var snapshot = ExecutionSnapshot.Capture(metadata, state);

        snapshot.ResultOutput.Should().BeNull();
        snapshot.ResultReasoning.Should().BeNull();
        snapshot.ResultSuccess.Should().BeNull();
    }

    // ── HostedRuntimeOptions ───────────────────────────────────────────────

    [Fact]
    public void HostedRuntimeOptions_HasSensibleDefaults()
    {
        var options = new HostedRuntimeOptions();

        options.ShutdownGracePeriod.Should().Be(TimeSpan.FromSeconds(30));
        options.MaxConcurrentExecutions.Should().Be(10);
        options.AutoCheckpointOnShutdown.Should().BeFalse();
    }

    [Fact]
    public async Task InMemoryCheckpointStore_DeleteAsync_RemovesAllForExecution()
    {
        var store = new InMemoryCheckpointStore();
        await store.SaveAsync(CreateSnapshot("exec-1", ExecutionStatus.Running));
        await store.SaveAsync(CreateSnapshot("exec-1", ExecutionStatus.Completed));

        await store.DeleteAsync("exec-1");

        var entries = await store.ListAsync("exec-1");
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task InMemoryCheckpointStore_ListAsync_ReturnsOrderedDescending()
    {
        var store = new InMemoryCheckpointStore();
        await store.SaveAsync(CreateSnapshot("exec-1", ExecutionStatus.Pending), "p");
        await Task.Delay(10);
        await store.SaveAsync(CreateSnapshot("exec-1", ExecutionStatus.Running), "r");
        await Task.Delay(10);
        await store.SaveAsync(CreateSnapshot("exec-1", ExecutionStatus.Completed), "c");

        var entries = await store.ListAsync("exec-1");

        entries.Should().HaveCount(3);
        entries[0].Label.Should().Be("c");
        entries[2].Label.Should().Be("p");
    }

    [Fact]
    public async Task InMemoryCheckpointStore_LoadAsync_ByCheckpointId()
    {
        var store = new InMemoryCheckpointStore();
        var entry = await store.SaveAsync(CreateSnapshot("exec-1", ExecutionStatus.Running), "mid");

        var loaded = await store.LoadAsync(entry.CheckpointId);
        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be(ExecutionStatus.Running);
    }

    [Fact]
    public async Task InMemoryCheckpointStore_LoadAsync_NonExistentReturnsNull()
    {
        var store = new InMemoryCheckpointStore();

        var loaded = await store.LoadAsync("nonexistent");

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task InMemoryCheckpointStore_LoadLatestAsync_NoSnapshots_ReturnsNull()
    {
        var store = new InMemoryCheckpointStore();

        var result = await store.LoadLatestAsync("nonexistent");

        result.Should().BeNull();
    }

    // ── InMemoryCheckpointStore ────────────────────────────────────────────

    [Fact]
    public async Task InMemoryCheckpointStore_SaveAndLoadLatest()
    {
        var store = new InMemoryCheckpointStore();
        var snapshot = CreateSnapshot("exec-1", ExecutionStatus.Running);

        await store.SaveAsync(snapshot);

        var loaded = await store.LoadLatestAsync("exec-1");
        loaded.Should().NotBeNull();
        loaded!.ExecutionId.Should().Be("exec-1");
        loaded.Status.Should().Be(ExecutionStatus.Running);
    }

    [Fact]
    public async Task InMemoryCheckpointStore_SaveMultiple_LoadLatestReturnsMostRecent()
    {
        var store = new InMemoryCheckpointStore();
        var s1 = CreateSnapshot("exec-1", ExecutionStatus.Running);
        var s2 = CreateSnapshot("exec-1", ExecutionStatus.Completed);

        await store.SaveAsync(s1, "first");
        await Task.Delay(10); // Ensure different timestamps
        await store.SaveAsync(s2, "second");

        var latest = await store.LoadLatestAsync("exec-1");
        latest.Should().NotBeNull();
        latest!.Status.Should().Be(ExecutionStatus.Completed);
    }

    // ── InMemoryExecutionJournal ───────────────────────────────────────────

    [Fact]
    public async Task InMemoryJournal_AppendAndReadAll()
    {
        var journal = new InMemoryExecutionJournal();
        var evt = new ExecutionStartedEvent("exec-1", new AgentRequest("test"));

        var entry = await journal.AppendAsync("exec-1", evt, "{\"goal\":\"test\"}");

        entry.SequenceNumber.Should().Be(1);
        entry.ExecutionId.Should().Be("exec-1");
        entry.EventType.Should().Be("ExecutionStarted");
        entry.SerializedPayload.Should().Be("{\"goal\":\"test\"}");

        var all = await journal.ReadAllAsync("exec-1");
        all.Should().HaveCount(1);
    }

    [Fact]
    public async Task InMemoryJournal_DeleteAsync_RemovesAllForExecution()
    {
        var journal = new InMemoryExecutionJournal();
        var evt = new ExecutionStartedEvent("exec-1", new AgentRequest("g"));

        await journal.AppendAsync("exec-1", evt);
        await journal.AppendAsync("exec-1", evt);

        await journal.DeleteAsync("exec-1");

        var all = await journal.ReadAllAsync("exec-1");
        all.Should().BeEmpty();
        (await journal.GetLatestSequenceAsync("exec-1")).Should().Be(-1);
    }

    [Fact]
    public async Task InMemoryJournal_GetLatestSequence_ReturnsLastSeq()
    {
        var journal = new InMemoryExecutionJournal();
        var evt = new ExecutionStartedEvent("exec-1", new AgentRequest("g"));

        await journal.AppendAsync("exec-1", evt);
        await journal.AppendAsync("exec-1", evt);

        var seq = await journal.GetLatestSequenceAsync("exec-1");

        seq.Should().Be(2);
    }

    [Fact]
    public async Task InMemoryJournal_GetLatestSequence_ReturnsMinusOneWhenEmpty()
    {
        var journal = new InMemoryExecutionJournal();

        var seq = await journal.GetLatestSequenceAsync("nonexistent");

        seq.Should().Be(-1);
    }

    [Fact]
    public async Task InMemoryJournal_ReadAfter_ReturnsOnlyNewerEntries()
    {
        var journal = new InMemoryExecutionJournal();
        var evt = new ExecutionStartedEvent("exec-1", new AgentRequest("g"));

        await journal.AppendAsync("exec-1", evt);
        await journal.AppendAsync("exec-1", evt);
        await journal.AppendAsync("exec-1", evt);

        var after = await journal.ReadAfterAsync("exec-1", 2);
        after.Should().HaveCount(1);
        after[0].SequenceNumber.Should().Be(3);
    }

    [Fact]
    public async Task InMemoryJournal_SeparateExecutions_HaveIndependentSequences()
    {
        var journal = new InMemoryExecutionJournal();
        var evt1 = new ExecutionStartedEvent("exec-1", new AgentRequest("a"));
        var evt2 = new ExecutionStartedEvent("exec-2", new AgentRequest("b"));

        await journal.AppendAsync("exec-1", evt1);
        await journal.AppendAsync("exec-1", evt1);
        await journal.AppendAsync("exec-2", evt2);

        (await journal.GetLatestSequenceAsync("exec-1")).Should().Be(2);
        (await journal.GetLatestSequenceAsync("exec-2")).Should().Be(1);
    }

    [Fact]
    public async Task InMemoryJournal_SequenceNumbers_AreMonotonic()
    {
        var journal = new InMemoryExecutionJournal();
        var evt1 = new ExecutionStartedEvent("exec-1", new AgentRequest("g"));
        var evt2 = new LlmCalledEvent("exec-1", Array.Empty<LlmMessage>());
        var evt3 = new ExecutionCompletedEvent(
            "exec-1",
            new AgentResult(true),
            TimeSpan.FromSeconds(1));

        await journal.AppendAsync("exec-1", evt1);
        await journal.AppendAsync("exec-1", evt2);
        await journal.AppendAsync("exec-1", evt3);

        var all = await journal.ReadAllAsync("exec-1");
        all.Select(e => e.SequenceNumber).Should().BeInAscendingOrder();
        all.Select(e => e.SequenceNumber).Should().Equal(1L, 2L, 3L);
    }

    // ── JournalEntry ───────────────────────────────────────────────────────

    [Fact]
    public void JournalEntry_From_CreatesFromExecutionEvent()
    {
        var evt = new ToolInvokedEvent(
            "exec-1",
            "my-tool",
            new ToolInvocation("my-tool", new Dictionary<string, object>()));

        var entry = JournalEntry.From(evt, 42, "{\"tool\":\"my-tool\"}");

        entry.SequenceNumber.Should().Be(42);
        entry.ExecutionId.Should().Be("exec-1");
        entry.EventType.Should().Be("ToolInvoked");
        entry.SerializedPayload.Should().Be("{\"tool\":\"my-tool\"}");
        entry.TraceId.Should().Be(evt.TraceId);
    }

    // ── ReplayModels ───────────────────────────────────────────────────────

    [Fact]
    public void ReplayRequest_DefaultsToOriginalParameters()
    {
        var request = new ReplayRequest { ExecutionId = "exec-1" };

        request.UseOriginalParameters.Should().BeTrue();
        request.FromCheckpointId.Should().BeNull();
        request.OverrideGoal.Should().BeNull();
        request.OverrideToolNames.Should().BeNull();
    }

    [Fact]
    public void ReplayResult_CarriesExpectedFields()
    {
        var now = DateTimeOffset.UtcNow;
        var result = new ReplayResult
                         {
                             ReplayExecutionId = "replay-1",
                             OriginalExecutionId = "exec-1",
                             Success = true,
                             Result = new AgentResult(true, "output"),
                             Duration = TimeSpan.FromSeconds(5),
                             StartedAt = now,
                             CompletedAt = now.AddSeconds(5)
                         };

        result.ReplayExecutionId.Should().Be("replay-1");
        result.OriginalExecutionId.Should().Be("exec-1");
        result.Success.Should().BeTrue();
        result.Result!.Output.Should().Be("output");
        result.Duration.Should().Be(TimeSpan.FromSeconds(5));
    }

    // ── ScheduledExecution ─────────────────────────────────────────────────

    [Fact]
    public void ScheduledExecution_IsDue_WhenEnabledAndPastNextRunAt()
    {
        var schedule = new ScheduledExecution
                           {
                               Request = new AgentRequest("test"),
                               NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                               IsEnabled = true
                           };

        schedule.IsDue.Should().BeTrue();
    }

    [Fact]
    public void ScheduledExecution_IsExpired_WhenMaxOccurrencesReached()
    {
        var schedule = new ScheduledExecution
                           {
                               Request = new AgentRequest("test"),
                               NextRunAt = DateTimeOffset.UtcNow,
                               MaxOccurrences = 3,
                               OccurrenceCount = 3
                           };

        schedule.IsExpired.Should().BeTrue();
        schedule.IsDue.Should().BeFalse();
    }

    [Fact]
    public void ScheduledExecution_IsNotDue_WhenDisabled()
    {
        var schedule = new ScheduledExecution
                           {
                               Request = new AgentRequest("test"),
                               NextRunAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                               IsEnabled = false
                           };

        schedule.IsDue.Should().BeFalse();
    }

    [Fact]
    public void ScheduledExecution_IsNotDue_WhenFutureNextRunAt()
    {
        var schedule = new ScheduledExecution
                           {
                               Request = new AgentRequest("test"),
                               NextRunAt = DateTimeOffset.UtcNow.AddMinutes(5),
                               IsEnabled = true
                           };

        schedule.IsDue.Should().BeFalse();
    }

    [Fact]
    public void ScheduledExecution_IsNotExpired_WhenUnlimitedOccurrences()
    {
        var schedule = new ScheduledExecution
                           {
                               Request = new AgentRequest("test"),
                               NextRunAt = DateTimeOffset.UtcNow,
                               MaxOccurrences = null,
                               OccurrenceCount = 100
                           };

        schedule.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void ShutdownCoordinator_DuplicateRegistration_Throws()
    {
        var coordinator = new DefaultShutdownCoordinator(
            Array.Empty<IShutdownHook>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultShutdownCoordinator>
                .Instance);

        coordinator.RegisterExecution("exec-1");
        var act = () => coordinator.RegisterExecution("exec-1");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task ShutdownCoordinator_DuplicateShutdown_ReturnsFalse()
    {
        var coordinator = new DefaultShutdownCoordinator(
            Array.Empty<IShutdownHook>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultShutdownCoordinator>
                .Instance);

        await coordinator.ShutdownAsync("first", TimeSpan.FromSeconds(1));
        var second = await coordinator.ShutdownAsync("second", TimeSpan.FromSeconds(1));

        second.Should().BeFalse();
    }

    [Fact]
    public async Task ShutdownCoordinator_RegisterAfterShutdown_Throws()
    {
        var coordinator = new DefaultShutdownCoordinator(
            Array.Empty<IShutdownHook>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultShutdownCoordinator>
                .Instance);

        await coordinator.ShutdownAsync("test", TimeSpan.FromSeconds(1));

        var act = () => coordinator.RegisterExecution("exec-late");
        act.Should().Throw<InvalidOperationException>().WithMessage("*shutdown*");
    }

    // ── Shutdown Coordinator ───────────────────────────────────────────────

    [Fact]
    public void ShutdownCoordinator_RegisterExecution_ReturnsToken()
    {
        var coordinator = new DefaultShutdownCoordinator(
            Array.Empty<IShutdownHook>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultShutdownCoordinator>
                .Instance);

        var token = coordinator.RegisterExecution("exec-1");

        token.Should().NotBe(default);
        coordinator.ActiveExecutionCount.Should().Be(1);
    }

    [Fact]
    public async Task ShutdownCoordinator_ShutdownAsync_SignalsAllExecutions()
    {
        var coordinator = new DefaultShutdownCoordinator(
            Array.Empty<IShutdownHook>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultShutdownCoordinator>
                .Instance);

        var token = coordinator.RegisterExecution("exec-1");

        var completed = await coordinator.ShutdownAsync("test", TimeSpan.FromSeconds(5));

        token.IsCancellationRequested.Should().BeTrue();
        coordinator.IsShuttingDown.Should().BeTrue();
    }

    [Fact]
    public void ShutdownCoordinator_UnregisterExecution_DecreasesCount()
    {
        var coordinator = new DefaultShutdownCoordinator(
            Array.Empty<IShutdownHook>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultShutdownCoordinator>
                .Instance);

        coordinator.RegisterExecution("exec-1");
        coordinator.RegisterExecution("exec-2");
        coordinator.ActiveExecutionCount.Should().Be(2);

        coordinator.UnregisterExecution("exec-1");
        coordinator.ActiveExecutionCount.Should().Be(1);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static ExecutionSnapshot CreateSnapshot(string executionId, ExecutionStatus status)
    {
        return new ExecutionSnapshot
                   {
                       ExecutionId = executionId,
                       CreatedAt = DateTimeOffset.UtcNow,
                       Status = status,
                       Goal = $"test-{executionId}"
                   };
    }
}
