using System;

namespace DiskSpaceTracker.Helpers;

/// <summary>
/// Lightweight wildcard matcher: <c>*</c> matches zero or more characters, <c>?</c> matches a
/// single character, and the comparison is case-insensitive (invariant-culture uppercase
/// folding). Avoids allocating a full regex for the hot path.
/// </summary>
public static class WildcardMatcher
{
    /// <summary>
    /// Returns true when <paramref name="text"/> matches <paramref name="pattern"/>. Comparison
    /// is case-insensitive via invariant-culture uppercase folding.
    /// </summary>
    public static bool IsMatch(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern)
    {
        // Iterative algorithm with two pointers + backtracking on the most recent '*' star.
        int ti = 0, pi = 0;
        int starIdx = -1, matchIdx = 0;

        while (ti < text.Length)
        {
            if (pi < pattern.Length && (pattern[pi] == '?' || EqualsIgnoreCase(pattern[pi], text[ti])))
            {
                ti++; pi++;
            }
            else if (pi < pattern.Length && pattern[pi] == '*')
            {
                starIdx = pi;
                matchIdx = ti;
                pi++;
            }
            else if (starIdx != -1)
            {
                pi = starIdx + 1;
                matchIdx++;
                ti = matchIdx;
            }
            else
            {
                return false;
            }
        }

        while (pi < pattern.Length && pattern[pi] == '*') pi++;
        return pi == pattern.Length;
    }

    private static bool EqualsIgnoreCase(char a, char b)
        => char.ToUpperInvariant(a) == char.ToUpperInvariant(b);

    /// <summary>
    /// Either an exact case-insensitive equality (when no wildcards) or a wildcard match.
    /// </summary>
    public static bool MatchOrEquals(string? text, string? pattern)
    {
        if (text is null) return false;
        if (pattern is null) return false;
        return PathNormalizer.HasWildcard(pattern)
            ? IsMatch(text.AsSpan(), pattern.AsSpan())
            : string.Equals(text, pattern, StringComparison.OrdinalIgnoreCase);
    }
}
