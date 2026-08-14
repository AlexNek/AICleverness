namespace AiCleverness.Models;

/// <summary>
/// Metadata describing a stored checkpoint.
/// </summary>
public sealed record CheckpointEntry(
    string ExecutionId,
    string CheckpointId,
    DateTimeOffset CapturedAt,
    ExecutionStatus Status,
    long? SizeBytes = null,
    string? Label = null)
{
    /// <summary>Creates a new entry from a snapshot.</summary>
    public static CheckpointEntry From(
        ExecutionSnapshot snapshot,
        string? label = null,
        long? sizeBytes = null) =>
        new(
            snapshot.ExecutionId,
            Guid.NewGuid().ToString("N"),
            snapshot.CapturedAt,
            snapshot.Status,
            sizeBytes,
            label);
}
