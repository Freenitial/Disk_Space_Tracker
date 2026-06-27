using System;
using System.Globalization;
using System.Text.RegularExpressions;
using DiskSpaceTracker.Models;

namespace DiskSpaceTracker.Helpers;

/// <summary>
/// Parses the very flexible "line numbers" specification used by the text-file analyzer.
///
/// <para>Accepted forms (case-insensitive, comma- or semicolon-separated tokens):</para>
/// <list type="bullet">
/// <item><description><c>any</c> — match any line.</description></item>
/// <item><description>Single positive index: <c>5</c>.</description></item>
/// <item><description>Single negative index: <c>-1</c> (last line), <c>-3</c>.</description></item>
/// <item><description>Positive range: <c>5:10</c> or <c>5..10</c> or <c>5_10</c>.</description></item>
/// <item><description>Negative range: <c>-1:-3</c> (any of last 3 lines).</description></item>
/// <item><description>Open positive: <c>5:</c> (line 5 onwards).</description></item>
/// <item><description>Open prefix: <c>:5</c> (lines 1..5) or <c>:-3</c> (any of last 3 lines).</description></item>
/// </list>
/// </summary>
public static partial class LineSelectorParser
{
    [GeneratedRegex(@"^\s*([+-]?\d+)?\s*(?:_{1}|:{1}|\.{2,3})\s*([+-]?\d+)?\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex RangeRegex();

    /// <summary>Parse a selector string into a <see cref="LineSelector"/>. Returns an empty selector for null/blank input.</summary>
    public static LineSelector Parse(string? spec)
    {
        var result = new LineSelector();
        // Empty / "any" / "(any)" all mean "match every line". The TextBox shows "(any)" as
        // its placeholder, so an empty value is equivalent to the explicit token.
        if (string.IsNullOrWhiteSpace(spec))
        {
            result.MatchAny = true;
            return result;
        }

        var trimmed = spec.Trim();
        if (string.Equals(trimmed, "any", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "(any)", StringComparison.OrdinalIgnoreCase))
        {
            result.MatchAny = true;
            return result;
        }

        var tokens = spec.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            // Plain integer first (avoid regex on simple cases — the hot path for "any" / "1" / "-1").
            if (int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n))
            {
                if (n > 0)
                    result.PosIndices.Add(n);
                else if (n < 0)
                {
                    var k = -n;
                    result.NegIndices.Add(k);
                    if (k > result.NeedLastK) result.NeedLastK = k;
                }
                continue;
            }

            var match = RangeRegex().Match(token);
            if (!match.Success) continue;

            var aText = match.Groups[1].Success ? match.Groups[1].Value : null;
            var bText = match.Groups[2].Success ? match.Groups[2].Value : null;
            if (aText is null && bText is null) continue;

            if (aText is null)
            {
                // Open prefix, e.g. ":5" or ":-3".
                var b = int.Parse(bText!, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                if (b > 0)
                    result.PosRanges.Add((1, b));
                else if (b < 0)
                {
                    var m = -b;
                    result.NegRanges.Add((1, m));
                    if (m > result.NeedLastK) result.NeedLastK = m;
                }
                continue;
            }

            if (bText is null)
            {
                // Open positive end, e.g. "5:".
                var a = int.Parse(aText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                if (a > 0)
                    result.PosRanges.Add((a, int.MaxValue));
                else if (a < 0)
                {
                    var kEnd = -a;
                    result.NegRanges.Add((1, kEnd));
                    if (kEnd > result.NeedLastK) result.NeedLastK = kEnd;
                }
                continue;
            }

            var aVal = int.Parse(aText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            var bVal = int.Parse(bText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

            // A zero endpoint is meaningless for 1-based line numbers; treat it as "absent" so
            // "0:5" behaves like ":5" and "5:0" like "5:" instead of being silently mis-parsed.
            if (aVal == 0 && bVal == 0)
                continue;
            if (aVal == 0)
            {
                if (bVal > 0) result.PosRanges.Add((1, bVal));
                else { var m = -bVal; result.NegRanges.Add((1, m)); if (m > result.NeedLastK) result.NeedLastK = m; }
                continue;
            }
            if (bVal == 0)
            {
                if (aVal > 0) result.PosRanges.Add((aVal, int.MaxValue));
                else { var m = -aVal; result.NegRanges.Add((1, m)); if (m > result.NeedLastK) result.NeedLastK = m; }
                continue;
            }

            if (aVal > 0 && bVal > 0)
            {
                if (aVal > bVal) (aVal, bVal) = (bVal, aVal);
                result.PosRanges.Add((aVal, bVal));
            }
            else if (aVal < 0 && bVal < 0)
            {
                var p = -aVal;
                var q = -bVal;
                if (p > q) (p, q) = (q, p);
                result.NegRanges.Add((p, q));
                if (q > result.NeedLastK) result.NeedLastK = q;
            }
            else
            {
                // Mixed sign: the two endpoints live in different coordinate spaces and can't form a
                // single contiguous range, so treat each independently. Positive -> open forward
                // range; negative -> last-K window.
                int posEnd = aVal > 0 ? aVal : bVal;
                int negEnd = aVal < 0 ? -aVal : -bVal;
                result.PosRanges.Add((posEnd, int.MaxValue));
                result.NegRanges.Add((1, negEnd));
                if (negEnd > result.NeedLastK) result.NeedLastK = negEnd;
            }
        }

        return result;
    }

    /// <summary>
    /// Sort and merge overlapping or adjacent ranges. Operates on either positive or negative
    /// range lists (the caller decides the meaning).
    /// </summary>
    public static (int A, int B)[] NormalizeRanges(System.Collections.Generic.List<(int A, int B)> ranges)
    {
        if (ranges.Count == 0) return Array.Empty<(int A, int B)>();
        if (ranges.Count == 1) return new[] { ranges[0] };

        var sorted = ranges.ToArray();
        Array.Sort(sorted, static (x, y) =>
        {
            var c = x.A.CompareTo(y.A);
            return c != 0 ? c : x.B.CompareTo(y.B);
        });

        var merged = new System.Collections.Generic.List<(int A, int B)>(sorted.Length);
        var (curA, curB) = sorted[0];
        for (int i = 1; i < sorted.Length; i++)
        {
            var (a, b) = sorted[i];
            // Guard curB == int.MaxValue overflowing curB + 1 (e.g. an open-ended "5:" range becomes
            // (5, int.MaxValue)); such a range already absorbs everything from curA onward.
            if (curB == int.MaxValue || a <= curB + 1)
            {
                if (b > curB) curB = b;
            }
            else
            {
                merged.Add((curA, curB));
                curA = a; curB = b;
            }
        }
        merged.Add((curA, curB));
        return merged.ToArray();
    }

    /// <summary>True when <paramref name="i"/> falls within any normalized positive range.</summary>
    public static bool InRange((int A, int B)[] ranges, int i)
    {
        for (int idx = 0; idx < ranges.Length; idx++)
        {
            ref readonly var r = ref ranges[idx];
            if (i >= r.A && i <= r.B) return true;
        }
        return false;
    }
}
