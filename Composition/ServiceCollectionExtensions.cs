using DiskSpaceTracker.Models;
using DiskSpaceTracker.Services;
using DiskSpaceTracker.ViewModels;
using DiskSpaceTracker.ViewModels.Conditions;
using DiskSpaceTracker.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DiskSpaceTracker.Composition;

/// <summary>
/// Single registration entry-point for the DI container. Keeps lifetimes explicit:
/// services are singletons (long-lived), view-models are transient by default except
/// the ones that the application bootstrap binds to a single live instance.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Cross-cutting infrastructure services.
        services.AddSingleton<IDriveService, DriveService>();
        services.AddSingleton<IDiskPerformanceService, PdhDiskPerformanceService>();
        services.AddSingleton<IFileSystemConditionService, FileSystemConditionService>();
        services.AddSingleton<IRegistryConditionService, RegistryConditionService>();
        services.AddSingleton<IProcessConditionService, ProcessConditionService>();
        services.AddSingleton<ITextFileAnalyzer, TextFileAnalyzer>();
        services.AddSingleton<IExecuteLauncher, ExecuteLauncher>();
        services.AddSingleton<IPresetStore, JsonPresetStore>();
        services.AddSingleton<INotificationSoundService, WindowsNotificationSoundService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<IAutomationEngine, AutomationEngine>();

        // Live aggregate view-models (singletons — the UI binds to these throughout the session).
        services.AddSingleton<LogsViewModel>();
        services.AddSingleton<UnitConverterViewModel>();
        services.AddSingleton<PresetSelectorViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton(serviceProvider =>
        {
            var dialogs = serviceProvider.GetRequiredService<IDialogService>();
            var engine = serviceProvider.GetRequiredService<IAutomationEngine>();
            var presetSelector = serviceProvider.GetRequiredService<PresetSelectorViewModel>();
            var logs = serviceProvider.GetRequiredService<LogsViewModel>();

            // Build four trigger tabs with their full set of seven conditions.
            var tabs = new System.Collections.Generic.List<AutomationTabViewModel>(4);
            foreach (var kind in System.Enum.GetValues<AutomationTriggerKind>())
            {
                var conditions = new ConditionsCollectionViewModel(
                    new WaitTimeConditionViewModel(),
                    new ExecuteConditionViewModel(dialogs),
                    new FileExistConditionViewModel(dialogs),
                    new FileLockedConditionViewModel(dialogs),
                    new ProcessExistConditionViewModel(dialogs),
                    new RegistryConditionViewModel(),
                    new TextFileConditionViewModel(dialogs));

                tabs.Add(new AutomationTabViewModel(
                    kind,
                    conditions,
                    new AfterActionsViewModel(kind),
                    engine));
            }

            var panel = new AutomationPanelViewModel(tabs, presetSelector, logs);
            presetSelector.BindPanel(panel);
            return panel;
        });

        // Transient view-models that match a per-show dialog instance.
        services.AddTransient<ProcessPickerViewModel>();
        services.AddTransient<ReportWindowViewModel>();

        // Window types — created by DI so they can resolve their data context indirectly.
        services.AddTransient<MainWindow>();
        services.AddTransient<ProcessPickerWindow>();
        services.AddTransient<ReportWindow>();

        return services;
    }
}
