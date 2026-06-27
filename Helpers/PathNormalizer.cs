using System;

namespace DiskSpaceTracker.Helpers;

/// <summary>
/// Fast path normalization that does NOT touch the disk (no Path.GetFullPath which resolves
/// symlinks, hits the working directory, etc.): trim quotes, expand environment variables,
/// normalize slashes, and prefix \\?\ for >=260 chars to enable the long-path API. Designed to
/// be called inside hot loops.
/// </summary>
public static class PathNormalizer
{
    /// <summary>
    /// Normalize a user-provided path. Returns an empty string for null or whitespace inputs.
    /// </summary>
    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        // Fast path for already-clean paths: no env vars to expand, no forward slashes to flip, no
        // surrounding quotes/whitespace to trim, and short enough to skip the long-path prefix.
        // This is the common case in the per-tick hot loop and avoids all the allocations below.
        if (path.Length < 260
            && path.IndexOf('%') < 0
            && path.IndexOf('/') < 0
            && !char.IsWhiteSpace(path[0]) && !char.IsWhiteSpace(path[^1])
            && path[0] is not ('"' or '\'') && path[^1] is not ('"' or '\''))
        {
            return path;
        }

        var s = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"', '\''))
            .Replace('/', '\\');

        // Long-path prefix: matches a drive letter "C:" or a UNC "\\" prefix.
        if (s.Length >= 2)
        {
            var hasDrive = s[1] == ':';
            var isUnc = s[0] == '\\' && s[1] == '\\';
            if ((hasDrive || isUnc) && s.Length >= 260 && !s.StartsWith(@"\\?\", StringComparison.Ordinal))
            {
                s = @"\\?\" + s;
            }
        }
        return s;
    }

    /// <summary>True when the path contains an unescaped wildcard '*' or '?'.</summary>
    public static bool HasWildcard(string? s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        return s.IndexOfAny(['*', '?']) >= 0;
    }
}
