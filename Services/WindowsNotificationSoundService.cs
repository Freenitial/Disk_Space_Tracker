using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace DiskSpaceTracker.Services;

/// <summary>
/// Plays the standard Windows notification sound (notify.wav) used by the "Bip" after-action.
/// Calls the Win32 <c>PlaySoundW</c> API directly to avoid the indirect dependency on
/// <c>System.Windows.Extensions</c> (not part of net10.0-windows by default) and stay fully
/// AOT-safe.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsNotificationSoundService : INotificationSoundService
{
    private static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "Media",
        "notify.wav");

    private const uint SND_ASYNC = 0x00000001;
    private const uint SND_FILENAME = 0x00020000;
    private const uint SND_NODEFAULT = 0x00000002;

    [LibraryImport("winmm.dll", EntryPoint = "PlaySoundW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PlaySound(string? pszSound, nint hmod, uint fdwSound);

    private readonly ILogger<WindowsNotificationSoundService> _logger;

    public WindowsNotificationSoundService(ILogger<WindowsNotificationSoundService> logger)
        => _logger = logger;

    public void PlayNotification()
    {
        try
        {
            // Play asynchronously so a "Bip" after-action never blocks the engine tick (and thus the
            // UI) for the WAV's duration. Skip entirely when the file is missing rather than calling
            // PlaySound with SND_FILENAME and a null path.
            if (!File.Exists(DefaultPath)) return;
            _ = PlaySound(DefaultPath, 0, SND_ASYNC | SND_FILENAME | SND_NODEFAULT);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to play notification sound");
        }
    }
}
