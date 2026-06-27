using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskSpaceTracker.Helpers;
using DiskSpaceTracker.Models;
using DiskSpaceTracker.Services;

namespace DiskSpaceTracker.ViewModels;

/// <summary>
/// Top-level view-model for the main window. Holds the disk metrics, the timer, the drive
/// selector, the unit converter, the logs collection, and the automation panel. Exposes the
/// four primary commands (Start/Pause/Resume, Reset, Auto toggle, Report) plus the nine copy
/// buttons (3 unit × 3 metric grid).
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IDriveService _driveService;
    private readonly IDiskPerformanceService _disk;
    private readonly IClipboardService _clipboard;
    private readonly IDialogService _dialogs;
    private readonly ILogService _log;
    private readonly IAutomationEngine _engine;

    private readonly DispatcherTimer _timer;
    private double _initialFreeMb;
    private double _maxAddedMb;
    private double _maxDeletedMb;
    private double _maxIgnoredDeletedMb;
    private double _maxIgnoredAddedMb;
    private double _currentDiffMb;
    private DateTimeOffset _lastTickTime;

    /// <summary>
    /// Remembers whether the Logs sidebar was visible the last time the Auto panel was open,
    /// so re-opening Auto restores Logs to the same state. Closing Auto always hides Logs.
    /// </summary>
    private bool _logsVisibleBeforeAutoClose;

    public MainWindowViewModel(
        IDriveService driveService,
        IDiskPerformanceService disk,
        IClipboardService clipboard,
        IDialogService dialogs,
        ILogService log,
        IAutomationEngine engine,
        AutomationPanelViewModel automation,
        LogsViewModel logs,
        UnitConverterViewModel converter)
    {
        _driveService = driveService;
        _disk = disk;
        _clipboard = clipboard;
        _dialogs = dialogs;
        _log = log;
        _engine = engine;

        Automation = automation;
        Logs = logs;
        Converter = converter;

        Drives = new ObservableCollection<DriveItem>(_driveService.GetFixedDrives());
        SelectedDrive = Drives.FirstOrDefault(d => d.DriveLetter.StartsWith("C", StringComparison.OrdinalIgnoreCase))
                        ?? Drives.FirstOrDefault();

        // IMPORTANT: do NOT use the (interval, priority, handler) ctor — it auto-starts the timer
        // and the first tick computes Elapsed = Now - default(DateTimeOffset) ≈ 2026 years.
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;

        _initialFreeMb = SelectedDrive is null ? 0d : _driveService.GetFreeSpaceMb(SelectedDrive.DriveLetter);
        InitialFreeText = string.Empty;
        CurrentFreeText = UnitFormatter.FormatFromMb(_initialFreeMb);
    }

    public ObservableCollection<DriveItem> Drives { get; }
    public AutomationPanelViewModel Automation { get; }
    public LogsViewModel Logs { get; }
    public UnitConverterViewModel Converter { get; }

    [ObservableProperty]
    public partial DriveItem? SelectedDrive { get; set; }

    [ObservableProperty]
    public partial bool DriveSelectionEnabled { get; set; } = true;

    [ObservableProperty]
    public partial string InitialFreeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentFreeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentDiffText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AddedDiffText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DeletedDiffText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IgnoreDeleted { get; set; }

    [ObservableProperty]
    public partial bool IgnoreAdded { get; set; }

    [ObservableProperty]
    public partial bool IncludeUnitOnCopy { get; set; }

    [ObservableProperty]
    public partial string ReadSpeedText { get; set; } = "R: 0 MBs";

    [ObservableProperty]
    public partial string WriteSpeedText { get; set; } = "W: 0 MBs";

    [ObservableProperty]
    public partial string ElapsedText { get; set; } = "00:00:00";

    [ObservableProperty]
    public partial string StartButtonText { get; set; } = "Start";

    [ObservableProperty]
    public partial bool AutoPanelOpen { get; set; }

    [ObservableProperty]
    public partial RunState RunStateValue { get; set; } = RunState.Reset;

    public TimeSpan Elapsed { get; private set; } = TimeSpan.Zero;

    /// <summary>Friendly status label rendered in the title bar ("Reset", "Started", "Paused").</summary>
    public string StatusText => RunStateValue switch
    {
        RunState.Started => "Started",
        RunState.Paused => "Paused",
        _ => "Reset",
    };

    partial void OnRunStateValueChanged(RunState value) => OnPropertyChanged(nameof(StatusText));

    partial void OnSelectedDriveChanged(DriveItem? value)
    {
        if (value is null) return;
        _log.Log("Config", $"Selected drive changed to {value.DriveLetter}");
        if (RunStateValue == RunState.Reset)
        {
            _initialFreeMb = _driveService.GetFreeSpaceMb(value.DriveLetter);
            CurrentFreeText = UnitFormatter.FormatFromMb(_initialFreeMb);
        }
    }

    /// <summary>Primary button: Start / Pause / Resume depending on state.</summary>
    [RelayCommand]
    public void StartOrPauseOrResume()
    {
        switch (RunStateValue)
        {
            case RunState.Started: PauseTracking(); break;
            case RunState.Paused: ResumeTracking(); break;
            default: StartTracking(); break;
        }
    }

    [RelayCommand]
    public void Reset() => ResetTracking();

    /// <summary>
    /// Toggle the Auto panel. The Logs sidebar is logically a child of Auto: closing Auto
    /// also hides Logs (saving its visibility), opening Auto restores Logs to its previous
    /// state.
    /// </summary>
    [RelayCommand]
    private void ToggleAutoPanel()
    {
        if (AutoPanelOpen)
        {
            // Closing — remember Logs state then hide both.
            _logsVisibleBeforeAutoClose = Logs.IsVisible;
            Logs.IsVisible = false;
            AutoPanelOpen = false;
        }
        else
        {
            // Opening — restore Logs to the state it was in when we last had Auto open.
            AutoPanelOpen = true;
            if (_logsVisibleBeforeAutoClose) Logs.IsVisible = true;
        }
    }

    [RelayCommand]
    private async Task ShowReportAsync() => await _dialogs.ShowReportAsync(BuildReport());

    [RelayCommand]
    private async Task CopyValueAsync(string parameter)
    {
        // parameter is "metric:unit" (e.g. "Diff:Bytes", "Added:GB", "Deleted:MB")
        var parts = parameter.Split(':');
        if (parts.Length != 2) return;

        var metric = parts[0];
        var unit = parts[1];
        double mb = metric switch
        {
            "Diff" => _currentDiffMb,
            "Added" => _maxAddedMb,
            "Deleted" => Math.Abs(_maxDeletedMb),
            _ => 0d,
        };

        string textValue = unit switch
        {
            "Bytes" => UnitFormatter.RoundedBytesFromMb(mb).ToString(System.Globalization.CultureInfo.InvariantCulture),
            "MB" => UnitFormatter.Format(mb),
            "GB" => UnitFormatter.Format(Math.Round(mb / 1024d, 2)),
            _ => UnitFormatter.Format(mb),
        };

        var output = IncludeUnitOnCopy ? $"{textValue} {unit}" : textValue;
        await _clipboard.SetTextAsync(output);
    }

    /// <summary>
    /// Public entry: start a tracking session. Called by the user-facing button or by the
    /// automation engine when the Start trigger fires.
    /// </summary>
    public void StartTracking(bool fromAutomation = false)
    {
        if (RunStateValue == RunState.Started) return;

        if (SelectedDrive is null) return;
        _initialFreeMb = _driveService.GetFreeSpaceMb(SelectedDrive.DriveLetter);
        InitialFreeText = UnitFormatter.FormatFromMb(_initialFreeMb);
        CurrentDiffText = "0 MB";

        _disk.Bind(SelectedDrive.DriveLetter);

        Elapsed = TimeSpan.Zero;
        _lastTickTime = DateTimeOffset.Now;
        _timer.Start();

        StartButtonText = "Pause";
        RunStateValue = RunState.Started;
        DriveSelectionEnabled = false;
        _log.Log(fromAutomation ? "Run" : "Manual", fromAutomation ? "Started tracking" : "Started");
    }

    /// <summary>Pause an in-progress session.</summary>
    public void PauseTracking(bool fromAutomation = false)
    {
        if (RunStateValue != RunState.Started) return;
        _timer.Stop();
        StartButtonText = "Resume";
        RunStateValue = RunState.Paused;
        _log.Log(fromAutomation ? "Run" : "Manual", fromAutomation ? "Paused tracking" : "Paused");
    }

    /// <summary>Resume a paused session.</summary>
    public void ResumeTracking(bool fromAutomation = false)
    {
        if (RunStateValue != RunState.Paused) return;
        _lastTickTime = DateTimeOffset.Now;
        _timer.Start();
        StartButtonText = "Pause";
        RunStateValue = RunState.Started;
        _log.Log(fromAutomation ? "Run" : "Manual", fromAutomation ? "Resumed tracking" : "Resumed");
    }

    /// <summary>Reset all metrics, stop the timer, and re-enable drive selection.</summary>
    public void ResetTracking(bool fromAutomation = false)
    {
        _timer.Stop();
        StartButtonText = "Start";
        RunStateValue = RunState.Reset;
        DriveSelectionEnabled = true;

        // Manual reset purges the engine's transient Execute states.
        if (!fromAutomation) _engine.ResetAllExecuteStates();

        _initialFreeMb = SelectedDrive is null ? 0 : _driveService.GetFreeSpaceMb(SelectedDrive.DriveLetter);
        CurrentFreeText = UnitFormatter.FormatFromMb(_initialFreeMb);
        InitialFreeText = string.Empty;
        CurrentDiffText = string.Empty;
        AddedDiffText = string.Empty;
        DeletedDiffText = string.Empty;
        ReadSpeedText = "R: 0 MBs";
        WriteSpeedText = "W: 0 MBs";

        _maxAddedMb = 0;
        _maxDeletedMb = 0;
        _maxIgnoredDeletedMb = 0;
        _maxIgnoredAddedMb = 0;
        _currentDiffMb = 0;
        Elapsed = TimeSpan.Zero;
        ElapsedText = "00:00:00";

        _log.Log(fromAutomation ? "Run" : "Manual", fromAutomation ? "Reset performed" : "Reseted");
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTimeOffset.Now;
        Elapsed += now - _lastTickTime;
        _lastTickTime = now;
        ElapsedText = HmsParser.Format(Elapsed);
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (SelectedDrive is null) return;
        var current = _driveService.GetFreeSpaceMb(SelectedDrive.DriveLetter);
        var diff = Math.Round(_initialFreeMb - current, 2);

        CurrentFreeText = UnitFormatter.FormatFromMb(current);
        _currentDiffMb = diff;
        CurrentDiffText = UnitFormatter.FormatFromMb(diff);

        if (IgnoreDeleted)
        {
            if (diff > _maxIgnoredDeletedMb) _maxIgnoredDeletedMb = diff;
            AddedDiffText = UnitFormatter.FormatFromMb(_maxIgnoredDeletedMb);
        }
        else
        {
            if (diff > _maxAddedMb) _maxAddedMb = diff;
            AddedDiffText = UnitFormatter.FormatFromMb(_maxAddedMb);
        }

        if (IgnoreAdded)
        {
            if (diff < _maxIgnoredAddedMb) _maxIgnoredAddedMb = diff;
            DeletedDiffText = UnitFormatter.FormatFromMb(Math.Abs(_maxIgnoredAddedMb));
        }
        else
        {
            if (diff < _maxDeletedMb) _maxDeletedMb = diff;
            DeletedDiffText = UnitFormatter.FormatFromMb(Math.Abs(_maxDeletedMb));
        }

        var sample = _disk.Sample();
        ReadSpeedText = $"R: {Math.Round(sample.ReadBytesPerSec / (1024d * 1024d))} MBs";
        WriteSpeedText = $"W: {Math.Round(sample.WriteBytesPerSec / (1024d * 1024d))} MBs";
    }

    /// <summary>Take a snapshot of the current values for inclusion in a "Run" log entry.</summary>
    public DriveSnapshot Snapshot() => new(
        SelectedDrive?.DriveLetter ?? "",
        InitialFreeText,
        CurrentFreeText,
        CurrentDiffText,
        AddedDiffText,
        DeletedDiffText,
        ElapsedText);

    /// <summary>Build the textual report from the log history and a live snapshot.</summary>
    public string BuildReport()
    {
        var sb = new StringBuilder();
        var hasRun = false;

        // Walk the log entries in chronological order (the collection is newest-first).
        for (int i = Logs.Entries.Count - 1; i >= 0; i--)
        {
            var entry = Logs.Entries[i];
            sb.Append('[').Append(entry.TimeText).Append("] ")
              .Append(entry.Mode).Append(" - ").AppendLine(entry.Message);
            if (entry.Mode == "Run" && entry.Snapshot is not null)
            {
                hasRun = true;
                sb.AppendLine(BuildSimpleReport(entry.Snapshot));
                sb.AppendLine();
            }
        }

        // Always finish with a "live" snapshot block so the report includes current values.
        if (hasRun) sb.AppendLine();
        sb.AppendLine(BuildSimpleReport(Snapshot()));
        return sb.ToString();
    }

    private static string BuildSimpleReport(DriveSnapshot snap)
    {
        var diff = UnitFormatter.Expand(snap.Diff);
        var added = UnitFormatter.Expand(snap.Added);
        var deleted = UnitFormatter.Expand(snap.Deleted);

        var sb = new StringBuilder();
        sb.Append(DateTime.Now.ToString("HH:mm:ss")).AppendLine(" ___ Snap :");
        sb.Append("Current Drive: ").AppendLine(snap.Drive);
        sb.Append("Initial Free Space: ").AppendLine(snap.Initial);
        sb.Append("Current Free Space: ").AppendLine(snap.Current);
        sb.Append("Current Difference: ").AppendLine(diff);
        sb.Append("Maximum Added: ").AppendLine(added);
        sb.Append("Maximum Deleted: ").AppendLine(deleted);
        sb.Append("Elapsed Time: ").Append(snap.Elapsed);
        return sb.ToString();
    }
}
