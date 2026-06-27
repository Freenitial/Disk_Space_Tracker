using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskSpaceTracker.Services;

namespace DiskSpaceTracker.ViewModels.Conditions;

/// <summary>"Process exist" condition with a dedicated picker dialog.</summary>
public sealed partial class ProcessExistConditionViewModel : ConditionViewModelBase
{
    private readonly IDialogService _dialogs;

    public ProcessExistConditionViewModel(IDialogService dialogs) => _dialogs = dialogs;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShouldExist { get; set; } = true;

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var picked = await _dialogs.PickProcessAsync();
        if (!string.IsNullOrEmpty(picked)) Name = picked;
    }
}
