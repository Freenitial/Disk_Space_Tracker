namespace DiskSpaceTracker.Models;

/// <summary>
/// The four automation triggers exposed in the Auto panel. Each trigger is gated on a
/// specific <see cref="RunState"/> before its conditions are evaluated.
/// </summary>
public enum AutomationTriggerKind
{
    /// <summary>Fires when the engine wants to start tracking. Only evaluated while in <see cref="RunState.Reset"/>.</summary>
    Start = 0,
    /// <summary>Fires to pause an active session. Only evaluated while in <see cref="RunState.Started"/>.</summary>
    Pause = 1,
    /// <summary>Fires to resume a paused session. Only evaluated while in <see cref="RunState.Paused"/>.</summary>
    Resume = 2,
    /// <summary>Fires to perform a reset. Evaluated whenever the run state is not <see cref="RunState.Reset"/>.</summary>
    Reset = 3,
}

/// <summary>
/// Helper utilities for <see cref="AutomationTriggerKind"/>: display names and gating predicate.
/// </summary>
public static class AutomationTriggerKindExtensions
{
    /// <summary>
    /// User-facing display name for the trigger.
    /// </summary>
    public static string DisplayName(this AutomationTriggerKind kind) => kind switch
    {
        AutomationTriggerKind.Start => "Start (if reset)",
        AutomationTriggerKind.Pause => "Pause (if started)",
        AutomationTriggerKind.Resume => "Resume (if paused)",
        AutomationTriggerKind.Reset => "Reset",
        _ => kind.ToString(),
    };

    /// <summary>
    /// Returns true when the given <paramref name="state"/> allows the trigger to be evaluated.
    /// </summary>
    public static bool IsEligible(this AutomationTriggerKind kind, RunState state) => kind switch
    {
        AutomationTriggerKind.Start => state == RunState.Reset,
        AutomationTriggerKind.Pause => state == RunState.Started,
        AutomationTriggerKind.Resume => state == RunState.Paused,
        AutomationTriggerKind.Reset => state != RunState.Reset,
        _ => true,
    };
}
