using CommunityToolkit.Mvvm.ComponentModel;

namespace DiskSpaceTracker.ViewModels.Conditions;

/// <summary>"Registry entry" condition: Key, Value name, Data, plus Should-exist toggle.</summary>
public sealed partial class RegistryConditionViewModel : ConditionViewModelBase
{
    [ObservableProperty]
    public partial string Key { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Data { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShouldExist { get; set; } = true;
}
