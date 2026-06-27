namespace DiskSpaceTracker.Models;

/// <summary>
/// Frozen snapshot of the disk-space tracker UI state at the moment of an event (typically a
/// "Run" log entry). Used by the report generator so older runs can be reconstructed accurately
/// even after the live values have moved on.
/// </summary>
public sealed record DriveSnapshot(
    string Drive,
    string Initial,
    string Current,
    string Diff,
    string Added,
    string Deleted,
    string Elapsed);
