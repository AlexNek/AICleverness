using System.Collections.Concurrent;

using AiCleverness.Abstractions;
using AiCleverness.Models;

namespace AiCleverness.Runtime;

/// <summary>
/// In-memory <see cref="IExecutionJournal"/> for tests and single-process scenarios.
/// Sequence numbers are assigned atomically per execution.
/// </summary>
public sealed class InMemoryExecutionJournal : IExecutionJournal
{
    private readonly ConcurrentDictionary<string, JournalBucket> _buckets = new();

    /// <inheritdoc />
    public Task<JournalEntry> AppendAsync(
        string executionId,
        ExecutionEvent evt,
        string? serializedPayload = null,
        CancellationToken cancellationToken = default)
    {
        var entry = GetBucket(executionId).Append(evt, serializedPayload);
        return Task.FromResult(entry);
    }

    /// <inheritdoc />
    public Task DeleteAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        _buckets.TryRemove(executionId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<long> GetLatestSequenceAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        if (!_buckets.TryGetValue(executionId, out var bucket))
            return Task.FromResult(-1L);

        return Task.FromResult(bucket.LatestSequence());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JournalEntry>> ReadAfterAsync(
        string executionId,
        long afterSequence,
        CancellationToken cancellationToken = default)
    {
        if (!_buckets.TryGetValue(executionId, out var bucket))
            return Task.FromResult<IReadOnlyList<JournalEntry>>(Array.Empty<JournalEntry>());

        return Task.FromResult<IReadOnlyList<JournalEntry>>(bucket.ReadAfter(afterSequence));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<JournalEntry>> ReadAllAsync(
        string executionId,
        CancellationToken cancellationToken = default)
    {
        if (!_buckets.TryGetValue(executionId, out var bucket))
            return Task.FromResult<IReadOnlyList<JournalEntry>>(Array.Empty<JournalEntry>());

        return Task.FromResult<IReadOnlyList<JournalEntry>>(bucket.ReadAll());
    }

    private JournalBucket GetBucket(string executionId) =>
        _buckets.GetOrAdd(executionId, _ => new JournalBucket());

    private sealed class JournalBucket
    {
        private readonly List<JournalEntry> _entries = new();

        private readonly object _lock = new();

        private long _sequence;

        public JournalEntry Append(ExecutionEvent evt, string? serializedPayload)
        {
            long seq;
            lock (_lock)
            {
                seq = ++_sequence;
                var entry = JournalEntry.From(evt, seq, serializedPayload);
                _entries.Add(entry);
                return entry;
            }
        }

        public long LatestSequence()
        {
            lock (_lock)
            {
                return _sequence;
            }
        }

        public IReadOnlyList<JournalEntry> ReadAfter(long afterSequence)
        {
            lock (_lock)
            {
                return _entries
                    .Where(e => e.SequenceNumber > afterSequence)
                    .OrderBy(e => e.SequenceNumber)
                    .ToList()
                    .AsReadOnly();
            }
        }

        public IReadOnlyList<JournalEntry> ReadAll()
        {
            lock (_lock)
            {
                return _entries.OrderBy(e => e.SequenceNumber).ToList().AsReadOnly();
            }
        }
    }
}
