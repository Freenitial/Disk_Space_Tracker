using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskSpaceTracker.Models;

namespace DiskSpaceTracker.ViewModels;

/// <summary>
/// Backing collection for the logs sidebar. Capped at <see cref="MaxEntries"/> rows; new
/// entries are inserted at index 0 so the most recent appears on top.
/// <see cref="IsVisible"/> drives the sidebar slide-in animation.
/// </summary>
public sealed partial class LogsViewModel : ViewModelBase
{
    public const int MaxEntries = 1000;

    public ObservableCollection<LogEntry> Entries { get; } = new();

    [ObservableProperty]
    public partial bool IsVisible { get; set; }

    /// <summary>Append a log row at the top of the list. Trims the tail when the cap is exceeded.</summary>
    public void Append(LogEntry entry)
    {
        Entries.Insert(0, entry);
        while (Entries.Count > MaxEntries)
            Entries.RemoveAt(Entries.Count - 1);
    }

    /// <summary>Toggle the sidebar visibility — bound to the "Logs" button in the toolbar.</summary>
    [RelayCommand]
    private void Toggle() => IsVisible = !IsVisible;
}
