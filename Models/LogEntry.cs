using System;

namespace DiskSpaceTracker.Models;

/// <summary>
/// A single row in the logs sidebar grid. <see cref="Snapshot"/> is only populated for entries
/// whose <see cref="Mode"/> is "Run"; the report generator uses it to inject contextual blocks.
/// </summary>
public sealed record LogEntry(
    DateTimeOffset Timestamp,
    string Mode,
    string Message,
    DriveSnapshot? Snapshot = null)
{
    /// <summary>"HH:mm:ss" form used for display in the grid.</summary>
    public string TimeText => Timestamp.ToString("HH:mm:ss");
}
