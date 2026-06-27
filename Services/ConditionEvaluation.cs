using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiskSpaceTracker.Services;

/// <summary>
/// Pure helpers for the automation tick, extracted from <see cref="AutomationEngine"/> so the
/// combination logic and the off-thread execution can be unit-tested without an Avalonia dispatcher
/// or the main view-model.
/// </summary>
public static class ConditionEvaluation
{
    /// <summary>
    /// Combine per-condition results: logical AND when <paramref name="allConditionsMode"/> is true
    /// (the "All (and)" radio), otherwise logical OR ("Any (or)"). An empty set is satisfied —
    /// matching the engine's rule that a trigger with no enabled conditions is always met.
    /// </summary>
    public static bool Combine(bool allConditionsMode, IReadOnlyList<bool> enabledResults)
    {
        if (enabledResults.Count == 0) return true;
        if (allConditionsMode)
        {
            for (int i = 0; i < enabledResults.Count; i++)
                if (!enabledResults[i]) return false;
            return true;
        }
        for (int i = 0; i < enabledResults.Count; i++)
            if (enabledResults[i]) return true;
        return false;
    }

    /// <summary>
    /// Run blocking condition probes OFF the calling thread (on the thread pool) so the UI/engine
    /// thread is never blocked by file / registry / process I/O. Results preserve input order. The
    /// probes must be self-contained (capture parameter VALUES, not view-model references) so they
    /// never touch UI-affine state off the UI thread.
    /// </summary>
    public static Task<bool[]> RunProbesAsync(IReadOnlyList<Func<bool>> probes)
        => Task.Run(() =>
        {
            var results = new bool[probes.Count];
            for (int i = 0; i < probes.Count; i++)
            {
                // Isolate each probe: a throwing condition (e.g. an inaccessible process) degrades to
                // "not met" instead of faulting the whole batch and aborting the entire tick.
                try { results[i] = probes[i](); }
                catch { results[i] = false; }
            }
            return results;
        });
}
