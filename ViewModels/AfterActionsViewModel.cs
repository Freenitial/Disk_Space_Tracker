using CommunityToolkit.Mvvm.ComponentModel;
using DiskSpaceTracker.Models;

namespace DiskSpaceTracker.ViewModels;

/// <summary>
/// Three "After:" toggles per trigger: Loop, Bip, Restart. <see cref="ShowRestart"/> is only
/// true for the Reset trigger; the checkbox is hidden for the other triggers.
/// </summary>
public sealed partial class AfterActionsViewModel : ViewModelBase
{
    public AfterActionsViewModel(AutomationTriggerKind kind)
    {
        ShowRestart = kind == AutomationTriggerKind.Reset;
    }

    [ObservableProperty]
    public partial bool Loop { get; set; }

    [ObservableProperty]
    public partial bool Bip { get; set; }

    [ObservableProperty]
    public partial bool Restart { get; set; }

    public bool ShowRestart { get; }
}
