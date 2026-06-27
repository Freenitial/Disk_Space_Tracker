namespace DiskSpaceTracker.Services;

/// <summary>
/// File-system condition evaluators: existence and "in use / locked" checks. Both support
/// wildcards via direct enumeration (no LINQ materialization, no GetFiles).
/// </summary>
public interface IFileSystemConditionService
{
    /// <summary>
    /// Check whether the path matches existence expectations. Wildcards are supported on the
    /// leaf segment. <paramref name="fileOnly"/> / <paramref name="directoryOnly"/> control
    /// what kinds of entries are accepted; pass both false for "either".
    /// </summary>
    bool TestExistence(string path, bool shouldExist, bool fileOnly = false, bool directoryOnly = false);

    /// <summary>
    /// Check whether the file is locked (open with no shared access). Returns
    /// <paramref name="shouldBeUsed"/> ⇔ locked.
    /// </summary>
    bool TestLocked(string path, bool shouldBeUsed, int retries = 1, int retryDelayMs = 100);
}
