using System.Threading.Tasks;

namespace DiskSpaceTracker.Services;

/// <summary>
/// Thin wrapper around Avalonia's clipboard, accessed through the <see cref="Avalonia.Controls.TopLevel"/>
/// of the active window. Stays decoupled from views so view-models can copy without referencing UI types.
/// </summary>
public interface IClipboardService
{
    Task SetTextAsync(string text);
}
