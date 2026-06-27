using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskSpaceTracker.Services;

namespace DiskSpaceTracker.ViewModels;

/// <summary>
/// Backing for the process picker dialog. Lists running processes grouped by name, supports
/// a contains-search box, and exposes Refresh / OK commands. The dialog returns the selected
/// process name (without the ".exe" suffix or "(xN)" decoration).
/// </summary>
public sealed partial class ProcessPickerViewModel : ViewModelBase
{
    private readonly IProcessConditionService _processes;
    private IReadOnlyList<ProcessGroup> _allProcesses = Array.Empty<ProcessGroup>();

    public ProcessPickerViewModel(IProcessConditionService processes)
    {
        _processes = processes;
        Refresh();
    }

    public ObservableCollection<ProcessGroup> Items { get; } = new();

    [ObservableProperty]
    public partial string Search { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ProcessGroup? SelectedItem { get; set; }

    [RelayCommand]
    public void Refresh()
    {
        // Snapshot the full process list ONCE; keystroke filtering then runs in-memory instead of
        // re-enumerating every running process on each character typed into the search box.
        _allProcesses = _processes.ListProcesses(null);
        ApplyFilter();
    }

    partial void OnSearchChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        Items.Clear();
        IEnumerable<ProcessGroup> source = string.IsNullOrWhiteSpace(Search)
            ? _allProcesses
            : _allProcesses.Where(p => p.Name.Contains(Search, StringComparison.OrdinalIgnoreCase));
        foreach (var item in source) Items.Add(item);
    }
}
