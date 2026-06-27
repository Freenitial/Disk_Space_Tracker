using DiskSpaceTracker.Models;
using DiskSpaceTracker.ViewModels;

namespace DiskSpaceTracker.Services;

/// <summary>
/// Central orchestrator for the four automation triggers. Owns a 2-second tick that:
/// <list type="number">
/// <item>Updates the WaitTime countdowns of eligible triggers (and resets them when leaving eligibility).</item>
/// <item>Evaluates active triggers, fires their action when the conditions latch, and resets the latch
/// after a Loop or after the trigger toggle is turned off.</item>
/// <item>Auto-stops the engine when no trigger is active.</item>
/// </list>
/// </summary>
public interface IAutomationEngine
{
    /// <summary>Bind the engine to the live <see cref="MainWindowViewModel"/> that drives state and UI.</summary>
    void Bind(MainWindowViewModel main);

    /// <summary>Start the timer if not already running.</summary>
    void Start();

    /// <summary>Stop the timer.</summary>
    void Stop();

    /// <summary>Number of currently active triggers (used by the badge in the Auto panel).</summary>
    int ActiveTriggerCount { get; }

    /// <summary>True when the engine timer is running.</summary>
    bool IsRunning { get; }

    /// <summary>Notify the engine that the toggled state of a trigger may have changed.</summary>
    void OnTriggerToggled(AutomationTriggerKind kind);

    /// <summary>
    /// Clear all transient "Execute" runtime states on every trigger. Invoked from the manual
    /// Reset button to tell the engine to start fresh.
    /// </summary>
    void ResetAllExecuteStates();
}
