using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using DiskSpaceTracker.Helpers;
using DiskSpaceTracker.Models;
using DiskSpaceTracker.ViewModels;
using DiskSpaceTracker.ViewModels.Conditions;
using Microsoft.Extensions.Logging;

namespace DiskSpaceTracker.Services;

/// <summary>
/// Central orchestrator. Tick loop:
/// <list type="number">
/// <item>Tick the WaitTime countdown of each eligible+active trigger; reset when leaving eligibility.</item>
/// <item>Evaluate each active trigger that is currently eligible (the per-state gate).
/// On a met → fire the action. The latch <c>SatisfiedLastTick</c> prevents re-firing every tick.</item>
/// <item>Execute conditions are stateful: launch on first eligibility, transition to "executed=true"
/// only after the launched process exits.</item>
/// <item>After-actions (Loop, Bip, Restart) follow the firing.</item>
/// <item>If no trigger remains active, the engine timer stops.</item>
/// </list>
/// </summary>
public sealed class AutomationEngine : IAutomationEngine, IDisposable
{
    private readonly ILogService _log;
    private readonly INotificationSoundService _sound;
    private readonly IExecuteLauncher _launcher;
    private readonly ITextFileAnalyzer _textAnalyzer;
    private readonly IFileSystemConditionService _fs;
    private readonly IRegistryConditionService _registry;
    private readonly IProcessConditionService _process;
    private readonly ILogger<AutomationEngine> _logger;

    private readonly DispatcherTimer _timer;
    private readonly Dictionary<AutomationTriggerKind, TriggerRuntime> _runtime = new();

    private MainWindowViewModel? _main;
    private bool _tickBusy;

    public AutomationEngine(
        ILogService log,
        INotificationSoundService sound,
        IExecuteLauncher launcher,
        ITextFileAnalyzer textAnalyzer,
        IFileSystemConditionService fs,
        IRegistryConditionService registry,
        IProcessConditionService process,
        ILogger<AutomationEngine> logger)
    {
        _log = log;
        _sound = sound;
        _launcher = launcher;
        _textAnalyzer = textAnalyzer;
        _fs = fs;
        _registry = registry;
        _process = process;
        _logger = logger;

        foreach (var kind in Enum.GetValues<AutomationTriggerKind>())
            _runtime[kind] = new TriggerRuntime();

        // Use the no-handler ctor and hook Tick manually so the timer does not auto-start.
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += OnTick;
    }

    public bool IsRunning => _timer.IsEnabled;

    public int ActiveTriggerCount => _main?.Automation?.ActiveTriggerCount ?? 0;

    public void Bind(MainWindowViewModel main) => _main = main;

    public void Start()
    {
        if (_timer.IsEnabled) return;
        _log.Log("Engine", "Starting automation engine");
        _timer.Start();
    }

    public void Stop()
    {
        if (!_timer.IsEnabled) return;
        _timer.Stop();
        // Release any in-flight Execute process handles — OnTick won't run again to dispose them.
        DisposeExecuteProcesses();
        _log.Log("Engine", "Automation engine stopped");
    }

    public void OnTriggerToggled(AutomationTriggerKind kind)
    {
        if (_main is null) return;
        if (_main.Automation.ActiveTriggerCount > 0)
        {
            if (!_timer.IsEnabled) Start();
        }
        else
        {
            Stop();
        }
    }

    public void ResetAllExecuteStates() => DisposeExecuteProcesses();

    /// <summary>Dispose every in-flight Execute process handle and clear its state. Disposing the
    /// Process object frees our OS handle; it does not kill the (fire-and-forget) child process.</summary>
    private void DisposeExecuteProcesses()
    {
        foreach (var runtime in _runtime.Values)
        {
            runtime.ExecuteProcess?.Dispose();
            runtime.ExecuteProcess = null;
            runtime.ExecuteFinished = false;
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        DisposeExecuteProcesses();
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        // Reentrancy guard: if the previous tick's off-thread probes are still running when the
        // timer fires again, skip this tick rather than overlapping evaluations.
        if (_main is null || _tickBusy) return;
        _tickBusy = true;

        try
        {
            // (1) WaitTime countdown maintenance for every trigger (UI thread, cheap).
            foreach (var tab in _main.Automation.Tabs)
            {
                var kind = tab.Kind;
                var runtime = _runtime[kind];
                var eligible = kind.IsEligible(_main.RunStateValue);
                var prevElig = runtime.EligibleLastTick;

                if (tab.IsActive && eligible && tab.Conditions.WaitTime.IsEnabled)
                {
                    if (!prevElig)
                    {
                        runtime.WaitStart = null;
                        runtime.WaitTargetSeconds = null;
                    }
                    UpdateWaitCountdown(tab, runtime);
                }
                else if (prevElig)
                {
                    runtime.WaitStart = null;
                    runtime.WaitTargetSeconds = null;
                    tab.Conditions.WaitTime.Countdown = string.Empty;
                }

                runtime.EligibleLastTick = eligible;
            }

            // (2a) Build per-tab evaluations on the UI thread. Stateful conditions (WaitTime/Execute)
            // are evaluated inline; the blocking I/O conditions are captured as probes over parameter
            // VALUES snapshotted here, so the off-thread step never touches view-model state.
            var evals = new List<TabEval>();
            var probes = new List<Func<bool>>();
            foreach (var tab in _main.Automation.Tabs)
            {
                if (!tab.IsActive) continue;

                var kind = tab.Kind;
                var runtime = _runtime[kind];
                if (!kind.IsEligible(_main.RunStateValue))
                {
                    runtime.SatisfiedLastTick = false;
                    continue;
                }

                var te = new TabEval(tab, runtime, runtime.SatisfiedLastTick);
                foreach (var condition in tab.Conditions.AllConditions)
                {
                    if (!condition.IsEnabled) continue;
                    switch (condition)
                    {
                        case WaitTimeConditionViewModel wt:
                            te.Add(condition, EvaluateWaitTime(wt, runtime));
                            break;
                        case ExecuteConditionViewModel ex:
                            te.Add(condition, EvaluateExecute(ex, runtime));
                            break;
                        case FileExistConditionViewModel fe:
                        {
                            var path = fe.Path; var shouldExist = fe.ShouldExist;
                            te.AddProbe(condition, probes.Count);
                            probes.Add(() => _fs.TestExistence(path, shouldExist));
                            break;
                        }
                        case FileLockedConditionViewModel fl:
                        {
                            // retries:0 → no Thread.Sleep; this runs off the UI thread anyway.
                            var path = fl.Path; var shouldBeUsed = fl.ShouldBeUsed;
                            te.AddProbe(condition, probes.Count);
                            probes.Add(() => _fs.TestLocked(path, shouldBeUsed, retries: 0));
                            break;
                        }
                        case ProcessExistConditionViewModel pe:
                        {
                            var name = pe.Name; var shouldExist = pe.ShouldExist;
                            te.AddProbe(condition, probes.Count);
                            probes.Add(() => _process.Test(name, shouldExist));
                            break;
                        }
                        case RegistryConditionViewModel rg:
                        {
                            var key = rg.Key;
                            var value = string.IsNullOrEmpty(rg.Value) ? null : rg.Value;
                            var data = string.IsNullOrEmpty(rg.Data) ? null : rg.Data;
                            var shouldExist = rg.ShouldExist;
                            te.AddProbe(condition, probes.Count);
                            probes.Add(() => _registry.Test(key, value, data, shouldExist));
                            break;
                        }
                        case TextFileConditionViewModel tf:
                        {
                            var path = tf.Path; var content = tf.Content; var line = tf.LineNumber; var cmp = tf.Comparison;
                            te.AddProbe(condition, probes.Count);
                            probes.Add(() => _textAnalyzer.Test(path, content, line, cmp));
                            break;
                        }
                        default:
                            te.Add(condition, true);
                            break;
                    }
                }
                evals.Add(te);
            }

            // (2b) Run the blocking probes OFF the UI thread; the await resumes back on the UI thread
            // (the DispatcherTimer's synchronization context), so every view-model write below is on
            // the UI thread.
            var probeResults = probes.Count == 0
                ? Array.Empty<bool>()
                : await ConditionEvaluation.RunProbesAsync(probes);

            if (_main is null) return; // shut down during the await

            // (2c) Apply per-condition results, combine, and fire (UI thread).
            foreach (var te in evals)
            {
                // A UI handler (trigger toggle / reset) can run during the await above; don't apply
                // results or fire for a trigger that was deactivated in the meantime.
                if (!te.Tab.IsActive) continue;

                var results = new bool[te.Conditions.Count];
                for (int i = 0; i < te.Conditions.Count; i++)
                {
                    var ce = te.Conditions[i];
                    var result = ce.ProbeIndex >= 0 ? probeResults[ce.ProbeIndex] : ce.Immediate;
                    ce.Condition.IsSatisfied = result;
                    results[i] = result;
                }

                var met = ConditionEvaluation.Combine(te.Tab.AllConditions, results);
                if (met && !te.Prev)
                    Fire(te.Tab, te.Runtime);

                // Latch: when looping without a WaitTime gate, never latch (re-fire while conditions
                // hold); otherwise reflect current met status.
                var loop = te.Tab.AfterActions.Loop;
                var hasWaitTime = te.Tab.Conditions.WaitTime.IsEnabled;
                te.Runtime.SatisfiedLastTick = (loop && !hasWaitTime) ? false : met;
            }

            // (3) Auto-stop when no triggers remain active.
            if (_main.Automation.ActiveTriggerCount <= 0)
                Stop();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Automation tick failure");
            _log.Log("Error", $"Automation tick failure: {ex.Message}");
        }
        finally
        {
            _tickBusy = false;
        }
    }

    private void UpdateWaitCountdown(AutomationTabViewModel tab, TriggerRuntime runtime)
    {
        var ts = HmsParser.Parse(tab.Conditions.WaitTime.Time);
        if (ts is null)
        {
            tab.Conditions.WaitTime.Countdown = string.Empty;
            runtime.WaitStart = null;
            runtime.WaitTargetSeconds = null;
            return;
        }

        if (runtime.WaitStart is null)
            runtime.WaitStart = DateTimeOffset.Now;
        // Refresh the target every tick so editing the Time textbox mid-countdown takes effect.
        runtime.WaitTargetSeconds = (long)Math.Max(0, Math.Floor(ts.Value.TotalSeconds));

        var elapsed = DateTimeOffset.Now - runtime.WaitStart.Value;
        var elapsedSeconds = (long)Math.Max(0, Math.Floor(elapsed.TotalSeconds));
        var remain = Math.Max(0, (runtime.WaitTargetSeconds ?? 0) - elapsedSeconds);
        tab.Conditions.WaitTime.Countdown = remain > 0 ? FormatHms(remain) : string.Empty;
    }

    private static string FormatHms(long sec)
    {
        var h = sec / 3600;
        var m = (sec % 3600) / 60;
        var s = sec % 60;
        return $"{h:00}:{m:00}:{s:00}";
    }

    private bool EvaluateWaitTime(WaitTimeConditionViewModel wt, TriggerRuntime runtime)
    {
        var ts = HmsParser.Parse(wt.Time);
        if (ts is null) return false;

        if (runtime.WaitStart is null)
        {
            runtime.WaitStart = DateTimeOffset.Now;
            runtime.WaitTargetSeconds = (long)Math.Max(0, Math.Floor(ts.Value.TotalSeconds));
            return false;
        }

        // Refresh the target every tick so a mid-countdown edit of the Time textbox takes effect.
        runtime.WaitTargetSeconds = (long)Math.Max(0, Math.Floor(ts.Value.TotalSeconds));
        var elapsed = DateTimeOffset.Now - runtime.WaitStart.Value;
        var elapsedSeconds = (long)Math.Max(0, Math.Floor(elapsed.TotalSeconds));
        return elapsedSeconds >= (runtime.WaitTargetSeconds ?? 0);
    }

    private bool EvaluateExecute(ExecuteConditionViewModel ex, TriggerRuntime runtime)
    {
        if (runtime.ExecuteFinished) return true;

        if (runtime.ExecuteProcess is not null)
        {
            if (!_launcher.IsRunning(runtime.ExecuteProcess))
            {
                runtime.ExecuteProcess.Dispose();
                runtime.ExecuteProcess = null;
                runtime.ExecuteFinished = true;
                _log.Log("Action", $"Execute finished: {ex.Path}");
                return true;
            }
            return false;
        }

        var proc = _launcher.Launch(ex.Path);
        if (proc is not null)
        {
            runtime.ExecuteProcess = proc;
            _log.Log("Action", $"Execute launched: {ex.Path}, waiting for exit");
            return false;
        }

        _log.Log("Error", $"Execute launch failed: {ex.Path}");
        return false;
    }

    private void Fire(AutomationTabViewModel tab, TriggerRuntime runtime)
    {
        if (_main is null) return;

        _log.Log("Action", $"Triggered: {tab.Kind.DisplayName()}");

        if (tab.AfterActions.Bip)
        {
            try { _sound.PlayNotification(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Bip failed"); }
        }

        switch (tab.Kind)
        {
            case AutomationTriggerKind.Start:
                if (_main.RunStateValue == RunState.Reset) _main.StartTracking(fromAutomation: true);
                break;
            case AutomationTriggerKind.Pause:
                if (_main.RunStateValue == RunState.Started) _main.PauseTracking(fromAutomation: true);
                break;
            case AutomationTriggerKind.Resume:
                if (_main.RunStateValue == RunState.Paused) _main.ResumeTracking(fromAutomation: true);
                break;
            case AutomationTriggerKind.Reset:
                _main.ResetTracking(fromAutomation: true);
                if (tab.AfterActions.Restart && _main.RunStateValue == RunState.Reset)
                    _main.StartTracking(fromAutomation: true);
                break;
        }

        // Re-arm the Execute condition for the next cycle — but ONLY if its launched process
        // actually finished. If one is still in-flight (e.g. the trigger fired on a different
        // condition in ANY mode), keep tracking it instead of orphaning it and relaunching a
        // duplicate next cycle.
        if (runtime.ExecuteFinished)
        {
            runtime.ExecuteProcess?.Dispose();
            runtime.ExecuteProcess = null;
            runtime.ExecuteFinished = false;
        }

        if (tab.AfterActions.Loop)
        {
            if (tab.Conditions.WaitTime.IsEnabled)
            {
                // Re-arm the wait from scratch: null BOTH fields so the next tick re-initializes
                // WaitStart and WaitTargetSeconds together. Nulling only the target while keeping
                // WaitStart would make the countdown read 0 and the wait fire immediately on loop.
                runtime.WaitStart = null;
                runtime.WaitTargetSeconds = null;
                tab.Conditions.WaitTime.IsSatisfied = false;
            }
            runtime.SatisfiedLastTick = false;
            _log.Log("Engine", $"Loop for trigger '{tab.Kind.DisplayName()}'");
        }
        else
        {
            tab.IsActive = false;
            OnTriggerToggled(tab.Kind);
        }

        runtime.LastFire = DateTimeOffset.Now;
    }

    /// <summary>Per-trigger transient runtime state held outside the view-model.</summary>
    private sealed class TriggerRuntime
    {
        public bool SatisfiedLastTick;
        public bool EligibleLastTick;
        public DateTimeOffset? WaitStart;
        public long? WaitTargetSeconds;
        public Process? ExecuteProcess;
        public bool ExecuteFinished;
        public DateTimeOffset? LastFire;
    }

    /// <summary>One tab's in-flight evaluation for the current tick.</summary>
    private sealed class TabEval
    {
        public readonly AutomationTabViewModel Tab;
        public readonly TriggerRuntime Runtime;
        public readonly bool Prev;
        public readonly List<CondEval> Conditions = new();

        public TabEval(AutomationTabViewModel tab, TriggerRuntime runtime, bool prev)
        {
            Tab = tab;
            Runtime = runtime;
            Prev = prev;
        }

        public void Add(ConditionViewModelBase condition, bool immediate)
            => Conditions.Add(new CondEval(condition, immediate, -1));

        public void AddProbe(ConditionViewModelBase condition, int probeIndex)
            => Conditions.Add(new CondEval(condition, false, probeIndex));
    }

    /// <summary>One condition's result: an immediate value, or an index into the off-thread probe results.</summary>
    private sealed class CondEval
    {
        public readonly ConditionViewModelBase Condition;
        public readonly bool Immediate;
        public readonly int ProbeIndex;

        public CondEval(ConditionViewModelBase condition, bool immediate, int probeIndex)
        {
            Condition = condition;
            Immediate = immediate;
            ProbeIndex = probeIndex;
        }
    }
}
