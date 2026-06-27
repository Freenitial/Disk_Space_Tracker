using System.Diagnostics;

namespace DiskSpaceTracker.Services;

/// <summary>
/// Launches an external command line, returning the live <see cref="Process"/>. Implementation
/// handles .ps1 / .bat / .cmd / .msi / .lnk / .exe and falls back to <c>cmd /c</c> for arbitrary
/// commands.
/// </summary>
public interface IExecuteLauncher
{
    /// <summary>
    /// Start the payload and return the live <see cref="Process"/>, or <c>null</c> if launch failed.
    /// The caller OWNS the returned process and must dispose it. Returning the live object instead of
    /// a bare PID avoids the PID-reuse TOCTOU when later polling for exit.
    /// </summary>
    Process? Launch(string payload);

    /// <summary>True when <paramref name="process"/> is non-null and has not yet exited.</summary>
    bool IsRunning(Process? process);
}
