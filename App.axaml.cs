using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DiskSpaceTracker.Composition;
using DiskSpaceTracker.Services;
using DiskSpaceTracker.ViewModels;
using DiskSpaceTracker.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DiskSpaceTracker;

/// <summary>
/// Application bootstrap. Builds the DI container at framework-init time, resolves the main
/// view-model, wires the log/engine bindings, and shows the root window.
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _services;

    /// <summary>Public accessor used by the dialog service to resolve windows on demand.</summary>
    public IServiceProvider Services => _services
        ?? throw new InvalidOperationException("Service container has not been initialized yet.");

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var collection = new ServiceCollection();
        collection.AddAppServices();
        _services = collection.BuildServiceProvider(validateScopes: true);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainViewModel = _services.GetRequiredService<MainWindowViewModel>();
            var logService = _services.GetRequiredService<ILogService>();
            var engine = _services.GetRequiredService<IAutomationEngine>();

            // Wire the log service to the live logs view-model and the engine to the main VM.
            logService.Bind(mainViewModel.Logs);
            engine.Bind(mainViewModel);

            var window = _services.GetRequiredService<MainWindow>();
            window.DataContext = mainViewModel;
            desktop.MainWindow = window;

            // Dispose the DI container on shutdown so IDisposable singletons run their cleanup —
            // most importantly PdhDiskPerformanceService, which closes its native PDH query handle.
            desktop.ShutdownRequested += (_, _) => (_services as IDisposable)?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
