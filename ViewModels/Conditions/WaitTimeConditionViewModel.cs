using CommunityToolkit.Mvvm.ComponentModel;

namespace DiskSpaceTracker.ViewModels.Conditions;

/// <summary>"Wait time" condition: a HH:MM:SS textbox plus a live countdown rendered next to it.</summary>
public sealed partial class WaitTimeConditionViewModel : ConditionViewModelBase
{
    [ObservableProperty]
    public partial string Time { get; set; } = string.Empty;

    /// <summary>Read-only countdown text driven by the engine tick.</summary>
    [ObservableProperty]
    public partial string Countdown { get; set; } = string.Empty;
}
