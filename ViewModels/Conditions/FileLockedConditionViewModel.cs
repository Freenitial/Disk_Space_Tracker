using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskSpaceTracker.Services;

namespace DiskSpaceTracker.ViewModels.Conditions;

/// <summary>"File locked" condition: tests whether a file is held open with no shared access.</summary>
public sealed partial class FileLockedConditionViewModel : ConditionViewModelBase
{
    private readonly IDialogService _dialogs;

    public FileLockedConditionViewModel(IDialogService dialogs) => _dialogs = dialogs;

    [ObservableProperty]
    public partial string Path { get; set; } = string.Empty;

    /// <summary>True ↔ "File should be in use"; false ↔ "File should NOT be in use".</summary>
    [ObservableProperty]
    public partial bool ShouldBeUsed { get; set; } = true;

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var picked = await _dialogs.PickFileOrFolderAsync();
        if (!string.IsNullOrEmpty(picked)) Path = picked;
    }
}
