namespace DiskSpaceTracker.Models;

/// <summary>
/// Run state of the disk-space tracker. Used by the automation engine to gate triggers
/// and by the title bar to render a colored status indicator.
/// </summary>
public enum RunState
{
    /// <summary>No tracking session active. Default state at startup or after a reset.</summary>
    Reset = 0,
    /// <summary>Tracking is currently running and the timer is ticking.</summary>
    Started = 1,
    /// <summary>An active tracking session is currently paused.</summary>
    Paused = 2,
}
