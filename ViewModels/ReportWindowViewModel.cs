using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskSpaceTracker.Services;

namespace DiskSpaceTracker.ViewModels;

/// <summary>Backing for the report modal: holds the rendered text and a "Copy" command.</summary>
public sealed partial class ReportWindowViewModel : ViewModelBase
{
    private readonly IClipboardService _clipboard;

    public ReportWindowViewModel(IClipboardService clipboard) => _clipboard = clipboard;

    [ObservableProperty]
    public partial string Content { get; set; } = string.Empty;

    [RelayCommand]
    private async Task CopyAsync() => await _clipboard.SetTextAsync(Content);
}
