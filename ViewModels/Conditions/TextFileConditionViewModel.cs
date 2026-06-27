using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskSpaceTracker.Models;
using DiskSpaceTracker.Services;

namespace DiskSpaceTracker.ViewModels.Conditions;

/// <summary>"Analyze text file" condition: file path + line content + line selector + comparison operator.</summary>
public sealed partial class TextFileConditionViewModel : ConditionViewModelBase
{
    private readonly IDialogService _dialogs;

    public TextFileConditionViewModel(IDialogService dialogs) => _dialogs = dialogs;

    [ObservableProperty]
    public partial string Path { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Content { get; set; } = string.Empty;

    /// <summary>
    /// Line selector. Empty string (the default), the literal "any", or "(any)" all mean
    /// "match any line". The placeholder shown in the TextBox is "(any)".
    /// </summary>
    [ObservableProperty]
    public partial string LineNumber { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ComparisonOperator Comparison { get; set; } = ComparisonOperator.Equal;

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var picked = await _dialogs.PickFileOrFolderAsync();
        if (!string.IsNullOrEmpty(picked)) Path = picked;
    }
}
