using System.Collections.Generic;

namespace DiskSpaceTracker.Services;

/// <summary>One row in the process picker dialog.</summary>
public sealed record ProcessGroup(string Name, int Count)
{
    /// <summary>"chrome.exe" or "chrome.exe (x14)" depending on the count.</summary>
    public string Display => Count > 1 ? $"{Name}.exe (x{Count})" : $"{Name}.exe";
}

/// <summary>Process-existence condition evaluator + listing helper for the picker dialog.</summary>
public interface IProcessConditionService
{
    /// <summary>
    /// Returns <c>true</c> when the existence of a process matching <paramref name="name"/>
    /// matches <paramref name="shouldExist"/>. Accepts plain names (with or without ".exe"),
    /// numeric PIDs, and wildcard patterns.
    /// </summary>
    bool Test(string? name, bool shouldExist);

    /// <summary>List running processes grouped by name, sorted by name.</summary>
    IReadOnlyList<ProcessGroup> ListProcesses(string? filterContains = null);
}
