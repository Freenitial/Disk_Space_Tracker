namespace DiskSpaceTracker.Services;

/// <summary>
/// Registry condition evaluator. Supports wildcards on key segments and on value names, four
/// distinct evaluation modes (key existence, value-existence, value-data compare, default-value
/// compare), and the hive prefixes HKLM, HKEY_LOCAL_MACHINE, HKCU, HKEY_CURRENT_USER, HKCR,
/// HKU, HKCC.
/// </summary>
public interface IRegistryConditionService
{
    /// <summary>
    /// Returns <c>true</c> when the registry state matches the configured expectation.
    /// </summary>
    /// <param name="key">Full registry path including the hive prefix.</param>
    /// <param name="value">
    /// Registry value name; pass <c>null</c> for "no value name provided" (key-existence mode),
    /// pass an empty string for the "(Default)" value.
    /// </param>
    /// <param name="data">
    /// Expected value data (supports wildcards). Pass <c>null</c> when no data check is required.
    /// </param>
    /// <param name="shouldExist">
    /// When <c>true</c>, the predicate is positive; when <c>false</c>, the result is inverted.
    /// </param>
    bool Test(string key, string? value, string? data, bool shouldExist);
}
