using System;
using System.IO;
using System.Threading;
using DiskSpaceTracker.Helpers;

namespace DiskSpaceTracker.Services;

/// <summary>
/// File-existence and file-locked condition evaluators. Wildcards are supported on the leaf
/// segment via
/// <see cref="Directory.EnumerateFiles(string, string, SearchOption)"/> /
/// <see cref="Directory.EnumerateDirectories(string, string, SearchOption)"/> (lazy enumeration,
/// short-circuits on first match).
/// </summary>
public sealed class FileSystemConditionService : IFileSystemConditionService
{
    public bool TestExistence(string path, bool shouldExist, bool fileOnly = false, bool directoryOnly = false)
    {
        if (fileOnly && directoryOnly)
        {
            fileOnly = false;
            directoryOnly = false;
        }

        var norm = PathNormalizer.Normalize(path);

        if (!PathNormalizer.HasWildcard(norm))
        {
            bool exists = (fileOnly, directoryOnly) switch
            {
                (true, _) => File.Exists(norm),
                (_, true) => Directory.Exists(norm),
                _ => File.Exists(norm) || Directory.Exists(norm),
            };
            return exists == shouldExist;
        }

        var any = HasMatch(norm,
            checkFiles: fileOnly || !directoryOnly,
            checkDirs: directoryOnly || !fileOnly);
        return any == shouldExist;
    }

    public bool TestLocked(string path, bool shouldBeUsed, int retries = 1, int retryDelayMs = 100)
    {
        var norm = PathNormalizer.Normalize(path);

        if (!PathNormalizer.HasWildcard(norm))
        {
            var locked = IsFileLocked(norm, retries, retryDelayMs);
            return locked == shouldBeUsed;
        }

        var dir = Path.GetDirectoryName(norm);
        var leaf = Path.GetFileName(norm);
        if (string.IsNullOrEmpty(dir)) dir = ".";

        try
        {
            foreach (var match in Directory.EnumerateFiles(dir, leaf, SearchOption.TopDirectoryOnly))
            {
                var locked = IsFileLocked(PathNormalizer.Normalize(match), retries, retryDelayMs);
                if (shouldBeUsed)
                {
                    if (locked) return true;
                }
                else
                {
                    if (locked) return false;
                }
            }
        }
        catch
        {
            return false;
        }

        // Reached the end of the enumeration without finding a contradicting match.
        return !shouldBeUsed;
    }

    private static bool HasMatch(string normPattern, bool checkFiles, bool checkDirs)
    {
        var dir = Path.GetDirectoryName(normPattern);
        var leaf = Path.GetFileName(normPattern);
        if (string.IsNullOrEmpty(dir)) dir = ".";

        try
        {
            if (checkFiles)
            {
                foreach (var _ in Directory.EnumerateFiles(dir, leaf, SearchOption.TopDirectoryOnly))
                    return true;
                if (!checkDirs) return false;
            }
            if (checkDirs)
            {
                foreach (var _ in Directory.EnumerateDirectories(dir, leaf, SearchOption.TopDirectoryOnly))
                    return true;
            }
        }
        catch
        {
            return false;
        }
        return false;
    }

    private static bool IsFileLocked(string path, int retries, int retryDelayMs)
    {
        for (int attempt = 0; attempt <= retries; attempt++)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return false;
            }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            catch (UnauthorizedAccessException)
            {
                try
                {
                    var attr = File.GetAttributes(path);
                    if ((attr & FileAttributes.Directory) != 0) return false;
                }
                catch
                {
                    // ignore
                }
                return true;
            }
            catch (IOException)
            {
                if (attempt < retries)
                {
                    Thread.Sleep(retryDelayMs);
                    continue;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }
}
