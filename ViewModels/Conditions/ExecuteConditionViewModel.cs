using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskSpaceTracker.Services;

namespace DiskSpaceTracker.ViewModels.Conditions;

/// <summary>"Execute, then wait closing" condition: launches a payload and waits for it to exit.</summary>
public sealed partial class ExecuteConditionViewModel : ConditionViewModelBase
{
    private readonly IDialogService _dialogs;

    public ExecuteConditionViewModel(IDialogService dialogs) => _dialogs = dialogs;

    [ObservableProperty]
    public partial string Path { get; set; } = string.Empty;

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var picked = await _dialogs.PickFileOrFolderAsync();
        if (!string.IsNullOrEmpty(picked)) Path = picked;
    }
}
