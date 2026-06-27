using System.Threading.Tasks;
using DiskSpaceTracker.ViewModels;

namespace DiskSpaceTracker.Services;

/// <summary>
/// Cross-cutting dialog operations: file/folder picker, simple message boxes, modal helpers.
/// Implementations pull the active <see cref="Avalonia.Controls.TopLevel"/> at call time so
/// view-models stay UI-framework agnostic.
/// </summary>
public interface IDialogService
{
    /// <summary>Open a file or folder picker.</summary>
    Task<string?> PickFileOrFolderAsync(string title = "Select a File or Folder");

    /// <summary>Open the process picker dialog and return the selected process name (without ".exe").</summary>
    Task<string?> PickProcessAsync();

    /// <summary>Show a yes/no confirmation. Defaults to "No".</summary>
    Task<bool> ConfirmAsync(string title, string message);

    /// <summary>Show an information popup with an OK button.</summary>
    Task ShowMessageAsync(string title, string message);

    /// <summary>Prompt for a single line of text. Returns <c>null</c> on cancel.</summary>
    Task<string?> PromptTextAsync(string title, string label, string seed = "");

    /// <summary>Open the report window with the supplied content.</summary>
    Task ShowReportAsync(string content);
}
