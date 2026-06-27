using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskSpaceTracker.Services;

namespace DiskSpaceTracker.ViewModels.Conditions;

/// <summary>"File exist" condition. Wildcards on the leaf segment are supported.</summary>
public sealed partial class FileExistConditionViewModel : ConditionViewModelBase
{
    private readonly IDialogService _dialogs;

    public FileExistConditionViewModel(IDialogService dialogs) => _dialogs = dialogs;

    [ObservableProperty]
    public partial string Path { get; set; } = string.Empty;

    /// <summary>True ↔ "File should exist"; false ↔ "File should NOT exist".</summary>
    [ObservableProperty]
    public partial bool ShouldExist { get; set; } = true;

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var picked = await _dialogs.PickFileOrFolderAsync();
        if (!string.IsNullOrEmpty(picked)) Path = picked;
    }
}
