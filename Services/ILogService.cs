using DiskSpaceTracker.Models;
using DiskSpaceTracker.ViewModels;

namespace DiskSpaceTracker.Services;

/// <summary>
/// Funnels automation events into the logs sidebar. Marshals to the UI thread internally so
/// callers can fire-and-forget from any thread.
/// </summary>
public interface ILogService
{
    /// <summary>Bind the service to the live <see cref="LogsViewModel"/> backing the sidebar.</summary>
    void Bind(LogsViewModel viewModel);

    /// <summary>Append a log entry. Categories used: Engine, Run, Action, Error, Manual, Config.</summary>
    void Log(string mode, string message, DriveSnapshot? snapshot = null);
}
