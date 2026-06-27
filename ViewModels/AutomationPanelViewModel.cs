using System.Collections.Generic;
using System.Linq;
using DiskSpaceTracker.Models;

namespace DiskSpaceTracker.ViewModels;

/// <summary>
/// Aggregate view-model for the bottom Auto panel: holds the four trigger tabs and exposes a
/// derived "active count" used by the engine to decide whether to keep ticking. Named tab
/// accessors (<see cref="StartTab"/>, etc.) exist for direct XAML bindings of the master switches.
/// </summary>
public sealed class AutomationPanelViewModel : ViewModelBase
{
    public AutomationPanelViewModel(
        IReadOnlyList<AutomationTabViewModel> tabs,
        PresetSelectorViewModel presets,
        LogsViewModel logs)
    {
        Tabs = tabs;
        Presets = presets;
        Logs = logs;
        StartTab = TabFor(AutomationTriggerKind.Start);
        PauseTab = TabFor(AutomationTriggerKind.Pause);
        ResumeTab = TabFor(AutomationTriggerKind.Resume);
        ResetTab = TabFor(AutomationTriggerKind.Reset);
    }

    /// <summary>The four tabs in display order: Start, Pause, Resume, Reset.</summary>
    public IReadOnlyList<AutomationTabViewModel> Tabs { get; }

    public PresetSelectorViewModel Presets { get; }

    /// <summary>Same instance as MainWindowViewModel.Logs — the "Logs" button toggles its visibility.</summary>
    public LogsViewModel Logs { get; }

    public AutomationTabViewModel StartTab { get; }
    public AutomationTabViewModel PauseTab { get; }
    public AutomationTabViewModel ResumeTab { get; }
    public AutomationTabViewModel ResetTab { get; }

    /// <summary>Resolve a tab by trigger kind. Throws when an unknown kind is passed.</summary>
    public AutomationTabViewModel TabFor(AutomationTriggerKind kind) => Tabs.First(t => t.Kind == kind);

    /// <summary>Number of tabs currently armed (master switch on). Hand-rolled loop (no LINQ
    /// delegate/iterator allocation) since the engine reads this every tick.</summary>
    public int ActiveTriggerCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < Tabs.Count; i++)
                if (Tabs[i].IsActive) n++;
            return n;
        }
    }
}
