using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DiskSpaceTracker.Helpers;

namespace DiskSpaceTracker.Services;

/// <summary>
/// Process-existence condition + listing helper: numeric strings are treated as PIDs, plain names
/// accept an optional ".exe" suffix and match case-insensitively, paths are reduced to their leaf
/// base name, and wildcards are expanded against <see cref="Process.GetProcesses"/> by name.
/// </summary>
public sealed class ProcessConditionService : IProcessConditionService
{
    public bool Test(string? name, bool shouldExist)
    {
        if (string.IsNullOrWhiteSpace(name)) return !shouldExist;

        var trimmed = name.Trim().Trim('"', '\'');
        if (trimmed.Length == 0) return !shouldExist;

        // Path → leaf base name. ProcessName never includes .exe.
        if (trimmed.IndexOfAny(['\\', '/']) >= 0 || (trimmed.Length >= 2 && trimmed[1] == ':'))
        {
            try { trimmed = Path.GetFileNameWithoutExtension(trimmed); }
            catch { /* keep raw */ }
        }
        else if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^4];
        }

        if (string.IsNullOrWhiteSpace(trimmed)) return !shouldExist;

        bool exists;
        if (int.TryParse(trimmed, out var pid))
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                exists = p is not null;
            }
            catch (ArgumentException)
            {
                exists = false;
            }
        }
        else if (PathNormalizer.HasWildcard(trimmed))
        {
            exists = false;
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (WildcardMatcher.IsMatch(p.ProcessName, trimmed))
                    {
                        exists = true;
                        break;
                    }
                }
                catch (InvalidOperationException) { /* process exited / inaccessible: skip */ }
                catch (System.ComponentModel.Win32Exception) { /* protected process: access denied, skip */ }
                finally { p.Dispose(); }
            }
        }
        else
        {
            var matches = Process.GetProcessesByName(trimmed);
            exists = matches.Length > 0;
            foreach (var m in matches) m.Dispose();
        }

        return exists == shouldExist;
    }

    public IReadOnlyList<ProcessGroup> ListProcesses(string? filterContains = null)
    {
        var processes = Process.GetProcesses();
        try
        {
            IEnumerable<Process> source = processes;
            if (!string.IsNullOrWhiteSpace(filterContains))
            {
                var needle = filterContains;
                source = source.Where(p => p.ProcessName.Contains(needle, StringComparison.OrdinalIgnoreCase));
            }
            return source
                .GroupBy(static p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
                .OrderBy(static g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(static g => new ProcessGroup(g.Key, g.Count()))
                .ToArray();
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }
    }
}
