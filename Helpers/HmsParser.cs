using System;
using System.Buffers;
using System.Globalization;

namespace DiskSpaceTracker.Helpers;

/// <summary>
/// Parses flexible HH:MM:SS / MM:SS / plain-seconds time strings, accepting NBSP-class
/// whitespace and full-width / ratio-sign colons.
/// </summary>
public static class HmsParser
{
    /// <summary>
    /// Try to parse a duration written as "HH:MM:SS", "MM:SS", or a plain non-negative
    /// integer number of seconds. Whitespace variants U+00A0 / U+2007 / U+202F are accepted,
    /// as are the full-width colon U+FF1A and ratio sign U+2236.
    /// </summary>
    /// <returns>The parsed <see cref="TimeSpan"/>, or <c>null</c> if the input is unrecognized.</returns>
    public static TimeSpan? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var t = text;
        // Normalize NBSP variants and full-width / ratio colons (cheap allocation guard for ASCII fast path).
        if (NeedsCleanup(t)) t = Cleanup(t);
        t = t.Trim();

        if (t.IndexOf(':') < 0)
        {
            // Plain seconds form.
            if (!long.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var sec) || sec < 0)
                return null;
            return SafeFromHms(0, 0, sec);
        }

        var parts = t.Split(':');
        if (parts.Length is < 2 or > 3) return null;

        long h = 0, m = 0, s = 0;
        switch (parts.Length)
        {
            case 2:
                if (!TryParseUnsigned(parts[0], out m) || !TryParseUnsigned(parts[1], out s))
                    return null;
                return SafeFromHms(0, m, s);
            case 3:
                if (!TryParseUnsigned(parts[0], out h)
                    || !TryParseUnsigned(parts[1], out m)
                    || !TryParseUnsigned(parts[2], out s))
                    return null;
                return SafeFromHms(h, m, s);
        }
        return null;
    }

    /// <summary>
    /// Format a <see cref="TimeSpan"/> as HH:MM:SS with hours zero-padded to two digits.
    /// </summary>
    public static string Format(TimeSpan span)
    {
        var totalSeconds = (long)Math.Floor(span.TotalSeconds);
        if (totalSeconds < 0) totalSeconds = 0;
        var h = totalSeconds / 3600;
        var rem = totalSeconds % 3600;
        var m = rem / 60;
        var s = rem % 60;
        return string.Create(CultureInfo.InvariantCulture, $"{h:00}:{m:00}:{s:00}");
    }

    /// <summary>
    /// Format a <see cref="TimeSpan"/> as H:MM:SS with no leading-zero padding on hours
    /// (used when the elapsed time can exceed 99h).
    /// </summary>
    public static string FormatLong(TimeSpan span)
    {
        var totalSeconds = (long)Math.Floor(span.TotalSeconds);
        if (totalSeconds < 0) totalSeconds = 0;
        var h = totalSeconds / 3600;
        var rem = totalSeconds % 3600;
        var m = rem / 60;
        var s = rem % 60;
        return string.Create(CultureInfo.InvariantCulture, $"{h}:{m:00}:{s:00}");
    }

    private static bool TryParseUnsigned(string text, out long value)
    {
        return long.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 0;
    }

    /// <summary>
    /// Largest duration accepted (~1000 years). Beyond this, the input is treated as unparseable
    /// rather than letting checked arithmetic or <see cref="TimeSpan.FromSeconds(double)"/> throw
    /// <see cref="OverflowException"/> on absurd inputs like "9999999999999999999".
    /// </summary>
    private const long MaxDurationSeconds = 1000L * 365 * 24 * 3600;

    private static TimeSpan? SafeFromHms(long h, long m, long s)
    {
        try
        {
            long total = checked((h * 3600L) + (m * 60L) + s);
            if (total < 0 || total > MaxDurationSeconds) return null;
            return TimeSpan.FromSeconds(total);
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static bool NeedsCleanup(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c is ' ' or ' ' or ' ' or '：' or '∶') return true;
        }
        return false;
    }

    private static string Cleanup(string s)
    {
        // Bound the stack allocation: a long user-supplied string falls back to a pooled heap
        // buffer so it can never overflow the stack.
        const int StackLimit = 256;
        char[]? rented = s.Length > StackLimit ? ArrayPool<char>.Shared.Rent(s.Length) : null;
        Span<char> buf = rented is null ? stackalloc char[s.Length] : rented.AsSpan(0, s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            buf[i] = c switch
            {
                ' ' or ' ' or ' ' => ' ',
                '：' or '∶' => ':',
                _ => c,
            };
        }
        var result = new string(buf);
        if (rented is not null) ArrayPool<char>.Shared.Return(rented);
        return result;
    }
}
