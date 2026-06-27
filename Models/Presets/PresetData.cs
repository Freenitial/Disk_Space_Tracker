using System.Collections.Generic;

namespace DiskSpaceTracker.Models.Presets;

/// <summary>
/// Root preset DTO persisted to disk as JSON. The <see cref="Items"/> dictionary keys are
/// user-defined preset names; values capture a complete snapshot of the four trigger tabs
/// plus the master toggles. Source-generated serialization keeps everything AOT-safe.
/// </summary>
public sealed class PresetData
{
    public Dictionary<string, PresetEntry> Items { get; set; } = new();
}

/// <summary>One preset entry: master toggles plus per-tab settings.</summary>
public sealed class PresetEntry
{
    public PresetTriggerToggles Toggles { get; set; } = new();
    public Dictionary<string, PresetTabData> Tabs { get; set; } = new();
}

/// <summary>State of the four "Auto-Trigger" master switches at save time.</summary>
public sealed class PresetTriggerToggles
{
    public bool Start { get; set; }
    public bool Pause { get; set; }
    public bool Resume { get; set; }
    public bool Reset { get; set; }
}

/// <summary>Settings for a single trigger tab: All/Any, After-actions, and 7 conditions.</summary>
public sealed class PresetTabData
{
    public bool AllConditions { get; set; } = true;
    public PresetAfterActions After { get; set; } = new();
    public PresetConditions Conditions { get; set; } = new();
}

public sealed class PresetAfterActions
{
    public bool Loop { get; set; }
    public bool Bip { get; set; }
    public bool Restart { get; set; }
}

public sealed class PresetConditions
{
    public PresetWaitTime WaitTime { get; set; } = new();
    public PresetExecute Execute { get; set; } = new();
    public PresetFileExist FileExist { get; set; } = new();
    public PresetFileLocked FileLocked { get; set; } = new();
    public PresetProcessExist ProcessExist { get; set; } = new();
    public PresetRegEntry RegEntry { get; set; } = new();
    public PresetTextFile TextFile { get; set; } = new();
}

public sealed class PresetWaitTime
{
    public bool Enabled { get; set; }
    public string Time { get; set; } = string.Empty;
}

public sealed class PresetExecute
{
    public bool Enabled { get; set; }
    public string Path { get; set; } = string.Empty;
}

public sealed class PresetFileExist
{
    public bool Enabled { get; set; }
    public bool ShouldExist { get; set; } = true;
    public string Path { get; set; } = string.Empty;
}

public sealed class PresetFileLocked
{
    public bool Enabled { get; set; }
    public bool ShouldBeUsed { get; set; } = true;
    public string Path { get; set; } = string.Empty;
}

public sealed class PresetProcessExist
{
    public bool Enabled { get; set; }
    public bool ShouldExist { get; set; } = true;
    public string Name { get; set; } = string.Empty;
}

public sealed class PresetRegEntry
{
    public bool Enabled { get; set; }
    public bool ShouldExist { get; set; } = true;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

public sealed class PresetTextFile
{
    public bool Enabled { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string LineNumber { get; set; } = string.Empty;
    public ComparisonOperator Comparison { get; set; } = ComparisonOperator.Equal;
}
