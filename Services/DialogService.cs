using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using DiskSpaceTracker.ViewModels;
using DiskSpaceTracker.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DiskSpaceTracker.Services;

/// <summary>
/// Dialog implementation that resolves the active main window at call time. Process picker
/// and report windows are constructed via DI so their view-models receive their own services.
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly IServiceProvider _services;

    public DialogService(IServiceProvider services) => _services = services;

    public async Task<string?> PickFileOrFolderAsync(string title = "Select a File or Folder")
    {
        var window = ResolveMainWindow();
        if (window is null) return null;

        var top = TopLevel.GetTopLevel(window);
        if (top is null) return null;

        // A single file picker — Cancel returns null cleanly.
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickProcessAsync()
    {
        var window = ResolveMainWindow();
        if (window is null) return null;

        var dialog = _services.GetRequiredService<ProcessPickerWindow>();
        var vm = _services.GetRequiredService<ProcessPickerViewModel>();
        dialog.DataContext = vm;
        return await dialog.ShowDialog<string?>(window).ConfigureAwait(true);
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var window = ResolveMainWindow();
        if (window is null) return false;

        var dialog = new ConfirmWindow(title, message);
        return await dialog.ShowDialog<bool>(window).ConfigureAwait(true);
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var window = ResolveMainWindow();
        if (window is null) return;

        var dialog = new MessageWindow(title, message);
        await dialog.ShowDialog(window).ConfigureAwait(true);
    }

    public async Task<string?> PromptTextAsync(string title, string label, string seed = "")
    {
        var window = ResolveMainWindow();
        if (window is null) return null;

        var dialog = new TextPromptWindow(title, label, seed);
        return await dialog.ShowDialog<string?>(window).ConfigureAwait(true);
    }

    public async Task ShowReportAsync(string content)
    {
        var window = ResolveMainWindow();
        if (window is null) return;

        var dialog = _services.GetRequiredService<ReportWindow>();
        var vm = _services.GetRequiredService<ReportWindowViewModel>();
        vm.Content = content;
        dialog.DataContext = vm;
        await dialog.ShowDialog(window).ConfigureAwait(true);
    }

    private static Window? ResolveMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }
}
