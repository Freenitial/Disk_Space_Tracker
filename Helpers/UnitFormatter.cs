using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DiskSpaceTracker.Helpers;

/// <summary>
/// Formatting helpers for byte-volume values and the related converters: display thresholds
/// (switch to GB above 1000 MB) and the "expand to lower units" behavior used by the report
/// generator.
/// </summary>
public static partial class UnitFormatter
{
    [GeneratedRegex(@"^\s*([+-]?[0-9]+(?:[.,][0-9]+)?)\s*([KMG]?B)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValueWithUnitRegex();

    private const double Kilo = 1024d;

    /// <summary>Bytes order used for the report expander: GB / MB / KB / B.</summary>
    private static readonly string[] ExpansionUnits = ["GB", "MB", "KB", "B"];

    /// <summary>Display a value given in MB, switching to GB if the absolute amount exceeds 1000 MB.</summary>
    public static string FormatFromMb(double mb)
    {
        if (Math.Abs(mb) > 1000d)
        {
            var gb = Math.Round(mb / Kilo, 2);
            return $"{Format(gb)} GB";
        }
        return $"{Format(mb)} MB";
    }

    /// <summary>Format a number with up to 2 decimals, dropping trailing zeros, invariant culture.</summary>
    public static string Format(double value)
        // Round once, then format with "0.##" (which already drops the decimals for whole numbers
        // and trailing zeros) — no second rounding pass, no truncate/compare dance.
        => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Format a converted value in PLAIN decimal notation (never scientific), trimming trailing
    /// zeros. Unlike <see cref="Format"/> (which rounds to 2 decimals for display), this keeps
    /// tiny ratios visible — e.g. 500 B in GB shows "0.000000466" instead of collapsing to "0"
    /// or rendering as "4.66E-05" — but never finer than 1-byte resolution: decimals are capped
    /// per unit (B→0, KB→3, MB→6, GB→9 = floor(log10 of bytes-per-unit)), since showing finer
    /// than a single byte is meaningless. B therefore always renders as a whole number.
    /// </summary>
    public static string FormatConverted(double value, string unit)
    {
        if (value == 0d || !double.IsFinite(value))
            return "0";

        // Cap precision at 1-byte resolution: never show a decimal place finer than a single
        // byte (= floor(log10 of bytes-per-unit)). B is atomic -> 0 decimals (whole bytes).
        // OrdinalIgnoreCase comparisons avoid the per-call ToUpperInvariant() string allocation
        // (this runs on every keystroke in the converter).
        int unitMax =
            string.Equals(unit, "B", StringComparison.OrdinalIgnoreCase) ? 0 :
            string.Equals(unit, "KB", StringComparison.OrdinalIgnoreCase) ? 3 :
            string.Equals(unit, "MB", StringComparison.OrdinalIgnoreCase) ? 6 :
            9; // GB or unknown

        double abs = Math.Abs(value);

        // Choose decimal places so ~7 significant digits survive even for very small values
        // (for abs < 1, add one place per leading zero after the decimal point), then clamp to
        // the unit's byte-resolution cap so we never display sub-byte precision.
        int decimals = abs >= 1d ? 6 : (int)Math.Floor(-Math.Log10(abs)) + 7;
        decimals = Math.Clamp(decimals, 0, unitMax);

        // "F" never switches to scientific notation; trim trailing zeros (and a bare point).
        string s = value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        if (s.IndexOf('.') >= 0)
            s = s.TrimEnd('0').TrimEnd('.');
        return s is "" or "-" or "-0" ? "0" : s;
    }

    /// <summary>Convert a numeric value between B/KB/MB/GB. Accepts comma or point as decimal separator.</summary>
    public static double Convert(string textValue, string fromUnit, string toUnit)
    {
        // Strip ASCII space AND the NBSP-class spaces (U+00A0/U+2007/U+202F) the same way HmsParser
        // does, so a pasted value with non-breaking spaces still parses.
        var normalized = textValue.Replace(',', '.')
            .Replace(" ", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(" ", string.Empty);
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            throw new FormatException($"Cannot parse numeric value: '{textValue}'.");

        return FromBytes(ToBytes(v, fromUnit), toUnit);
    }

    private static double ToBytes(double v, string unit) => unit switch
    {
        "B" => v,
        "KB" => v * Kilo,
        "MB" => v * Kilo * Kilo,
        "GB" => v * Kilo * Kilo * Kilo,
        _ => throw new ArgumentException($"Unknown unit: {unit}", nameof(unit)),
    };

    private static double FromBytes(double bytes, string unit) => unit switch
    {
        "B" => bytes,
        "KB" => bytes / Kilo,
        "MB" => bytes / (Kilo * Kilo),
        "GB" => bytes / (Kilo * Kilo * Kilo),
        _ => throw new ArgumentException($"Unknown unit: {unit}", nameof(unit)),
    };

    /// <summary>
    /// Expand a "X UNIT" string into "X UNIT / Y UNIT / ..." through the lower units (GB → MB → KB → B).
    /// Used by the report generator. Returns the input as-is if it can't be parsed or the value is zero.
    /// </summary>
    public static string Expand(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text ?? string.Empty;
        var match = ValueWithUnitRegex().Match(text);
        if (!match.Success) return text.Trim();

        var rawNumber = match.Groups[1].Value;
        var unit = match.Groups[2].Value.ToUpperInvariant();

        var normalized = rawNumber.Replace(',', '.').Replace(" ", string.Empty);
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return text.Trim();

        if (value == 0d) return $"{rawNumber.Trim()} {unit}";

        var idx = Array.IndexOf(ExpansionUnits, unit);
        if (idx < 0) return $"{rawNumber.Trim()} {unit}";

        var inBytes = ToBytes(value, unit);
        var parts = new List<string>(4) { $"{rawNumber.Trim()} {unit}" };
        for (int i = idx + 1; i < ExpansionUnits.Length; i++)
        {
            var converted = FromBytes(inBytes, ExpansionUnits[i]);
            parts.Add($"{Format(converted)} {ExpansionUnits[i]}");
        }
        return string.Join(" / ", parts);
    }

    /// <summary>Round a megabyte value to the nearest 1000-byte boundary.</summary>
    public static long RoundedBytesFromMb(double mb)
    {
        // Clamp before the cast: an out-of-range double cast to long would silently wrap to
        // long.MinValue (unchecked), so the value is clamped before the cast.
        var rounded = Math.Round((mb * Kilo * Kilo) / 1000d);
        if (!double.IsFinite(rounded)) return 0L;
        // Cap so (long)rounded * 1000 cannot overflow: long.MaxValue/1000 rounds UP as a double and
        // would wrap to negative. 9e15 thousands = 9e18 bytes (~9 EB) is far beyond any real disk.
        const double maxThousands = 9.0e15;
        rounded = Math.Clamp(rounded, -maxThousands, maxThousands);
        return (long)rounded * 1000L;
    }
}
