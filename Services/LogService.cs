using System;
using Avalonia.Threading;
using DiskSpaceTracker.Models;
using DiskSpaceTracker.ViewModels;

namespace DiskSpaceTracker.Services;

/// <summary>
/// Default <see cref="ILogService"/>. Marshals log appends to the UI thread via
/// <see cref="Dispatcher.UIThread"/> so the engine timer and condition evaluators can
/// invoke <see cref="Log"/> from any thread without races.
/// </summary>
public sealed class LogService : ILogService
{
    private LogsViewModel? _viewModel;

    public void Bind(LogsViewModel viewModel) => _viewModel = viewModel;

    public void Log(string mode, string message, DriveSnapshot? snapshot = null)
    {
        var vm = _viewModel;
        if (vm is null) return;

        var entry = new LogEntry(DateTimeOffset.Now, mode, message, snapshot);

        if (Dispatcher.UIThread.CheckAccess())
        {
            vm.Append(entry);
        }
        else
        {
            Dispatcher.UIThread.Post(() => vm.Append(entry));
        }
    }
}
