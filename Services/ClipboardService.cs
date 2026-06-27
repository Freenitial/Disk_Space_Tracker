using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace DiskSpaceTracker.Services;

/// <summary>
/// Clipboard wrapper. Avalonia's clipboard is reachable from any <see cref="TopLevel"/>; we
/// pick the main window from the desktop lifetime so view-models can copy without holding
/// a reference to a specific view.
/// </summary>
public sealed class ClipboardService : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        var clipboard = ResolveClipboard();
        if (clipboard is null) return;
        // No ConfigureAwait(false): the clipboard is UI-thread-affine, so the continuation must
        // stay on the captured (UI) synchronization context.
        await clipboard.SetTextAsync(text);
    }

    private static IClipboard? ResolveClipboard()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = desktop.MainWindow;
            if (window is null) return null;
            return TopLevel.GetTopLevel(window)?.Clipboard;
        }
        return null;
    }
}
