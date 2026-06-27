using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using DiskSpaceTracker.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace DiskSpaceTracker.Services;

/// <summary>
/// Registry condition evaluator. Hive token tolerant (HKLM, HKEY_LOCAL_MACHINE, with or without
/// a "Computer\" prefix), 4 evaluation modes selected from the (hasValue, hasData) pair, wildcard
/// expansion on key segments and on value names.
///
/// <para>The shared base hive handle is opened once per <see cref="Test"/> call and is NEVER
/// disposed by the inner helpers — only sub-keys they open are disposed; disposing the base key
/// while walking wildcard paths would break multi-segment wildcard lookups. The registry view is
/// pinned to <see cref="RegistryView.Registry64"/> for a deterministic 64-bit view on this
/// win-x64 process.</para>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RegistryConditionService : IRegistryConditionService
{
    private static readonly Dictionary<string, RegistryHive> HiveTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HKLM"] = RegistryHive.LocalMachine,
        ["HKEY_LOCAL_MACHINE"] = RegistryHive.LocalMachine,
        ["HKCU"] = RegistryHive.CurrentUser,
        ["HKEY_CURRENT_USER"] = RegistryHive.CurrentUser,
        ["HKCR"] = RegistryHive.ClassesRoot,
        ["HKEY_CLASSES_ROOT"] = RegistryHive.ClassesRoot,
        ["HKU"] = RegistryHive.Users,
        ["HKEY_USERS"] = RegistryHive.Users,
        ["HKCC"] = RegistryHive.CurrentConfig,
        ["HKEY_CURRENT_CONFIG"] = RegistryHive.CurrentConfig,
    };

    private readonly ILogger<RegistryConditionService> _logger;

    public RegistryConditionService(ILogger<RegistryConditionService> logger) => _logger = logger;

    public bool Test(string key, string? value, string? data, bool shouldExist)
    {
        if (string.IsNullOrWhiteSpace(key)) return !shouldExist;

        if (!TryParseKey(key, out var hive, out var subPath)) return !shouldExist;

        var hasValue = value is not null;
        var normValue = NormalizeValueName(value);
        var hasData = data is not null;
        var dataPattern = data ?? string.Empty;

        bool hit;
        try
        {
            hit = TestInProcessView(hive, subPath, hasValue, normValue, hasData, dataPattern);
        }
        catch (Exception ex)
        {
            // Access-denied vs absent are both treated as "not a hit" for the boolean condition,
            // but log so the distinction is diagnosable instead of completely silent.
            _logger.LogDebug(ex, "Registry test failed for key '{Key}'", key);
            hit = false;
        }
        return shouldExist ? hit : !hit;
    }

    private static bool TryParseKey(string key, out RegistryHive hive, out string subPath)
    {
        hive = default;
        subPath = string.Empty;

        var expanded = Environment.ExpandEnvironmentVariables(key.Trim().Trim('"', '\'').Replace('/', '\\'))
            .TrimEnd('\\');
        var parts = expanded.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        int hiveIdx = -1;
        for (int i = 0; i < parts.Length; i++)
        {
            var token = parts[i].Trim().TrimEnd(':').ToUpperInvariant();
            if (HiveTokens.TryGetValue(token, out var h))
            {
                hive = h;
                hiveIdx = i;
                break;
            }
        }
        if (hiveIdx < 0) return false;

        subPath = hiveIdx + 1 < parts.Length ? string.Join('\\', parts, hiveIdx + 1, parts.Length - hiveIdx - 1) : string.Empty;
        return true;
    }

    private static string NormalizeValueName(string? value)
    {
        if (value is null) return string.Empty;
        var v = value.Trim();
        return (v == "(Default)" || v == "@") ? string.Empty : v;
    }

    private static bool TestInProcessView(RegistryHive hive, string subPath, bool hasValue, string valueName, bool hasData, string dataPattern)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        var segments = string.IsNullOrEmpty(subPath)
            ? Array.Empty<string>()
            : subPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);

        var keyHasWildcard = AnyWildcard(segments);
        var valueHasWildcard = hasValue && PathNormalizer.HasWildcard(valueName);

        switch (hasValue, hasData)
        {
            // (1) Pure key existence.
            case (false, false):
                if (!keyHasWildcard)
                {
                    var k = FastOpen(baseKey, segments);
                    try { return k is not null; }
                    finally { Close(k, baseKey); }
                }
                foreach (var _ in ExpandKeyPaths(baseKey, segments)) return true;
                return false;

            // (2) Value existence (no data check).
            case (true, false):
                if (!keyHasWildcard && !valueHasWildcard)
                {
                    var k = FastOpen(baseKey, segments);
                    try
                    {
                        if (k is null) return false;
                        return k.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not null;
                    }
                    finally { Close(k, baseKey); }
                }

                foreach (var rel in ExpandKeyPaths(baseKey, segments))
                {
                    var key = OpenRelative(baseKey, rel);
                    if (key is null) continue;
                    try
                    {
                        if (!valueHasWildcard)
                        {
                            if (key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not null)
                                return true;
                        }
                        else
                        {
                            foreach (var name in key.GetValueNames())
                            {
                                if (WildcardMatcher.IsMatch(name, valueName)
                                    && key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not null)
                                    return true;
                            }
                        }
                    }
                    finally { Close(key, baseKey); }
                }
                return false;

            // (3) Value data compare.
            case (true, true):
                if (!keyHasWildcard && !valueHasWildcard)
                {
                    var k = FastOpen(baseKey, segments);
                    try
                    {
                        if (k is null) return false;
                        var raw = k.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                        return raw is not null && WildcardMatcher.MatchOrEquals(StringifyValue(raw), dataPattern);
                    }
                    finally { Close(k, baseKey); }
                }
                foreach (var rel in ExpandKeyPaths(baseKey, segments))
                {
                    var key = OpenRelative(baseKey, rel);
                    if (key is null) continue;
                    try
                    {
                        if (!valueHasWildcard)
                        {
                            var raw = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                            if (raw is not null && WildcardMatcher.MatchOrEquals(StringifyValue(raw), dataPattern))
                                return true;
                        }
                        else
                        {
                            foreach (var name in key.GetValueNames())
                            {
                                if (!WildcardMatcher.IsMatch(name, valueName)) continue;
                                var raw = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                                if (raw is not null && WildcardMatcher.MatchOrEquals(StringifyValue(raw), dataPattern))
                                    return true;
                            }
                        }
                    }
                    finally { Close(key, baseKey); }
                }
                return false;

            // (4) Default-value compare (no value name supplied, but data is).
            case (false, true):
                if (!keyHasWildcard)
                {
                    var k = FastOpen(baseKey, segments);
                    try
                    {
                        if (k is null) return false;
                        var raw = k.GetValue(string.Empty, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                        return raw is not null && WildcardMatcher.MatchOrEquals(StringifyValue(raw), dataPattern);
                    }
                    finally { Close(k, baseKey); }
                }
                foreach (var rel in ExpandKeyPaths(baseKey, segments))
                {
                    var key = OpenRelative(baseKey, rel);
                    if (key is null) continue;
                    try
                    {
                        var raw = key.GetValue(string.Empty, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                        if (raw is not null && WildcardMatcher.MatchOrEquals(StringifyValue(raw), dataPattern))
                            return true;
                    }
                    finally { Close(key, baseKey); }
                }
                return false;
        }
    }

    /// <summary>Dispose an opened key, but never the shared base key.</summary>
    private static void Close(RegistryKey? key, RegistryKey baseKey)
    {
        if (key is not null && !ReferenceEquals(key, baseKey)) key.Dispose();
    }

    private static bool AnyWildcard(string[] segments)
    {
        for (int i = 0; i < segments.Length; i++)
            if (PathNormalizer.HasWildcard(segments[i])) return true;
        return false;
    }

    private static RegistryKey? FastOpen(RegistryKey baseKey, string[] segments)
    {
        if (segments.Length == 0) return baseKey;
        RegistryKey? current = null;
        try
        {
            current = baseKey.OpenSubKey(segments[0]);
            for (int i = 1; i < segments.Length && current is not null; i++)
            {
                var next = current.OpenSubKey(segments[i]);
                current.Dispose();
                current = next;
            }
            return current;
        }
        catch
        {
            current?.Dispose();
            return null;
        }
    }

    private static RegistryKey? OpenRelative(RegistryKey baseKey, string rel)
        => string.IsNullOrEmpty(rel) ? baseKey : baseKey.OpenSubKey(rel);

    /// <summary>
    /// Lazily yield the relative paths that match a wildcarded segment list. Only opens the
    /// child keys necessary at each level — no recursive enumeration. Never disposes the shared
    /// base key (only the per-level sub-keys it opens).
    /// </summary>
    private static IEnumerable<string> ExpandKeyPaths(RegistryKey baseKey, string[] segments)
    {
        if (segments.Length == 0)
        {
            yield return string.Empty;
            yield break;
        }

        var current = new List<string> { string.Empty };
        for (int i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];
            var next = new List<string>();
            foreach (var rel in current)
            {
                var k = OpenRelative(baseKey, rel);
                if (k is null) continue;
                try
                {
                    string[] subKeys;
                    try { subKeys = k.GetSubKeyNames(); }
                    catch { continue; }

                    foreach (var name in subKeys)
                    {
                        if (WildcardMatcher.IsMatch(name, seg))
                            next.Add(string.IsNullOrEmpty(rel) ? name : $"{rel}\\{name}");
                    }
                }
                finally { Close(k, baseKey); }
            }
            current = next;
            if (current.Count == 0) yield break;
        }

        foreach (var rel in current) yield return rel;
    }

    private static string StringifyValue(object value) => value switch
    {
        null => string.Empty,
        byte[] bytes => BitConverter.ToString(bytes),
        string[] strings => string.Join(',', strings),
        _ => value.ToString() ?? string.Empty,
    };
}
