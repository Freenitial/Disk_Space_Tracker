using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskSpaceTracker.Models;
using DiskSpaceTracker.Models.Presets;
using DiskSpaceTracker.Services;

namespace DiskSpaceTracker.ViewModels;

/// <summary>
/// Backing for the "Presets" menu button. Provides the list of saved presets, exposes "Save
/// current" and "Delete" commands, and translates between the live <see cref="AutomationPanelViewModel"/>
/// and the on-disk <see cref="PresetEntry"/> DTO.
/// </summary>
public sealed partial class PresetSelectorViewModel : ViewModelBase
{
    private readonly IPresetStore _store;
    private readonly IDialogService _dialogs;

    private AutomationPanelViewModel? _panel;

    public PresetSelectorViewModel(IPresetStore store, IDialogService dialogs)
    {
        _store = store;
        _dialogs = dialogs;
    }

    public ObservableCollection<string> PresetNames { get; } = new();

    /// <summary>Bind the selector to the live panel — wire-up done after both view-models exist.</summary>
    public void BindPanel(AutomationPanelViewModel panel)
    {
        _panel = panel;
        Reload();
    }

    /// <summary>Refresh the preset list from disk.</summary>
    public void Reload()
    {
        PresetNames.Clear();
        var data = _store.Load();
        foreach (var name in data.Items.Keys.OrderBy(static n => n, StringComparer.OrdinalIgnoreCase))
            PresetNames.Add(name);
    }

    /// <summary>Apply a named preset to the live panel.</summary>
    [RelayCommand]
    public void Apply(string? name)
    {
        if (_panel is null || string.IsNullOrEmpty(name)) return;
        var data = _store.Load();
        if (!data.Items.TryGetValue(name, out var entry)) return;
        ApplyEntry(_panel, entry);
    }

    /// <summary>Prompt for a name then save the current panel state.</summary>
    [RelayCommand]
    private async Task SaveAsAsync()
    {
        if (_panel is null) return;

        var name = await _dialogs.PromptTextAsync("Save preset", "Choose a title");
        if (string.IsNullOrWhiteSpace(name)) return;

        var data = _store.Load();
        if (data.Items.ContainsKey(name))
        {
            var overwrite = await _dialogs.ConfirmAsync("Confirm", $"Preset '{name}' already exists. Overwrite?");
            if (!overwrite) return;
        }

        data.Items[name] = SnapshotEntry(_panel);
        _store.Save(data);
        Reload();
    }

    /// <summary>Delete a named preset after a confirmation.</summary>
    [RelayCommand]
    public async Task DeleteAsync(string? name)
    {
        if (string.IsNullOrEmpty(name)) return;
        var ok = await _dialogs.ConfirmAsync("Confirm", $"Delete preset '{name}'?");
        if (!ok) return;
        var data = _store.Load();
        data.Items.Remove(name);
        _store.Save(data);
        Reload();
    }

    private static PresetEntry SnapshotEntry(AutomationPanelViewModel panel)
    {
        var entry = new PresetEntry
        {
            Toggles = new PresetTriggerToggles
            {
                Start = panel.TabFor(AutomationTriggerKind.Start).IsActive,
                Pause = panel.TabFor(AutomationTriggerKind.Pause).IsActive,
                Resume = panel.TabFor(AutomationTriggerKind.Resume).IsActive,
                Reset = panel.TabFor(AutomationTriggerKind.Reset).IsActive,
            },
        };

        foreach (var tab in panel.Tabs)
        {
            entry.Tabs[tab.Kind.DisplayName()] = new PresetTabData
            {
                AllConditions = tab.AllConditions,
                After = new PresetAfterActions
                {
                    Loop = tab.AfterActions.Loop,
                    Bip = tab.AfterActions.Bip,
                    Restart = tab.AfterActions.Restart,
                },
                Conditions = new PresetConditions
                {
                    WaitTime = new PresetWaitTime
                    {
                        Enabled = tab.Conditions.WaitTime.IsEnabled,
                        Time = tab.Conditions.WaitTime.Time,
                    },
                    Execute = new PresetExecute
                    {
                        Enabled = tab.Conditions.Execute.IsEnabled,
                        Path = tab.Conditions.Execute.Path,
                    },
                    FileExist = new PresetFileExist
                    {
                        Enabled = tab.Conditions.FileExist.IsEnabled,
                        ShouldExist = tab.Conditions.FileExist.ShouldExist,
                        Path = tab.Conditions.FileExist.Path,
                    },
                    FileLocked = new PresetFileLocked
                    {
                        Enabled = tab.Conditions.FileLocked.IsEnabled,
                        ShouldBeUsed = tab.Conditions.FileLocked.ShouldBeUsed,
                        Path = tab.Conditions.FileLocked.Path,
                    },
                    ProcessExist = new PresetProcessExist
                    {
                        Enabled = tab.Conditions.ProcessExist.IsEnabled,
                        ShouldExist = tab.Conditions.ProcessExist.ShouldExist,
                        Name = tab.Conditions.ProcessExist.Name,
                    },
                    RegEntry = new PresetRegEntry
                    {
                        Enabled = tab.Conditions.Registry.IsEnabled,
                        ShouldExist = tab.Conditions.Registry.ShouldExist,
                        Key = tab.Conditions.Registry.Key,
                        Value = tab.Conditions.Registry.Value,
                        Data = tab.Conditions.Registry.Data,
                    },
                    TextFile = new PresetTextFile
                    {
                        Enabled = tab.Conditions.TextFile.IsEnabled,
                        Path = tab.Conditions.TextFile.Path,
                        Content = tab.Conditions.TextFile.Content,
                        LineNumber = tab.Conditions.TextFile.LineNumber,
                        Comparison = tab.Conditions.TextFile.Comparison,
                    },
                },
            };
        }
        return entry;
    }

    private static void ApplyEntry(AutomationPanelViewModel panel, PresetEntry entry)
    {
        panel.TabFor(AutomationTriggerKind.Start).IsActive = entry.Toggles.Start;
        panel.TabFor(AutomationTriggerKind.Pause).IsActive = entry.Toggles.Pause;
        panel.TabFor(AutomationTriggerKind.Resume).IsActive = entry.Toggles.Resume;
        panel.TabFor(AutomationTriggerKind.Reset).IsActive = entry.Toggles.Reset;

        foreach (var tab in panel.Tabs)
        {
            if (!entry.Tabs.TryGetValue(tab.Kind.DisplayName(), out var t)) continue;

            tab.AllConditions = t.AllConditions;
            tab.AfterActions.Loop = t.After.Loop;
            tab.AfterActions.Bip = t.After.Bip;
            tab.AfterActions.Restart = t.After.Restart;

            tab.Conditions.WaitTime.IsEnabled = t.Conditions.WaitTime.Enabled;
            tab.Conditions.WaitTime.Time = t.Conditions.WaitTime.Time;

            tab.Conditions.Execute.IsEnabled = t.Conditions.Execute.Enabled;
            tab.Conditions.Execute.Path = t.Conditions.Execute.Path;

            tab.Conditions.FileExist.IsEnabled = t.Conditions.FileExist.Enabled;
            tab.Conditions.FileExist.ShouldExist = t.Conditions.FileExist.ShouldExist;
            tab.Conditions.FileExist.Path = t.Conditions.FileExist.Path;

            tab.Conditions.FileLocked.IsEnabled = t.Conditions.FileLocked.Enabled;
            tab.Conditions.FileLocked.ShouldBeUsed = t.Conditions.FileLocked.ShouldBeUsed;
            tab.Conditions.FileLocked.Path = t.Conditions.FileLocked.Path;

            tab.Conditions.ProcessExist.IsEnabled = t.Conditions.ProcessExist.Enabled;
            tab.Conditions.ProcessExist.ShouldExist = t.Conditions.ProcessExist.ShouldExist;
            tab.Conditions.ProcessExist.Name = t.Conditions.ProcessExist.Name;

            tab.Conditions.Registry.IsEnabled = t.Conditions.RegEntry.Enabled;
            tab.Conditions.Registry.ShouldExist = t.Conditions.RegEntry.ShouldExist;
            tab.Conditions.Registry.Key = t.Conditions.RegEntry.Key;
            tab.Conditions.Registry.Value = t.Conditions.RegEntry.Value;
            tab.Conditions.Registry.Data = t.Conditions.RegEntry.Data;

            tab.Conditions.TextFile.IsEnabled = t.Conditions.TextFile.Enabled;
            tab.Conditions.TextFile.Path = t.Conditions.TextFile.Path;
            tab.Conditions.TextFile.Content = t.Conditions.TextFile.Content;
            tab.Conditions.TextFile.LineNumber = t.Conditions.TextFile.LineNumber;
            tab.Conditions.TextFile.Comparison = t.Conditions.TextFile.Comparison;
        }
    }
}
