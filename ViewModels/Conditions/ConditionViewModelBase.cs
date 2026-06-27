using CommunityToolkit.Mvvm.ComponentModel;

namespace DiskSpaceTracker.ViewModels.Conditions;

/// <summary>
/// Base for the seven condition view-models. <see cref="IsEnabled"/> is the user toggle
/// shown next to each condition row; <see cref="IsSatisfied"/> reflects the most recent
/// evaluation result and is what subscribers display for visual feedback.
/// </summary>
public abstract partial class ConditionViewModelBase : ViewModelBase
{
    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsSatisfied { get; set; }
}
