using System;
using DiskSpaceTracker.Helpers;
using FluentAssertions;
using Xunit;

namespace DiskSpaceTracker.Tests.Helpers;

public class UnitFormatterTests
{
    private const double K = 1024d;

    // ---- Convert ----

    [Theory]
    [InlineData("1", "GB", "MB", 1024d)]
    [InlineData("1", "KB", "B", 1024d)]
    [InlineData("1", "MB", "KB", 1024d)]
    [InlineData("1", "B", "B", 1d)]
    [InlineData("1.5", "GB", "MB", 1536d)]
    [InlineData("1,5", "GB", "MB", 1536d)]      // comma decimal separator accepted
    [InlineData(" 2 ", "MB", "KB", 2048d)]      // surrounding whitespace stripped
    public void Convert_ConvertsBetweenUnits(string value, string from, string to, double expected)
        => UnitFormatter.Convert(value, from, to).Should().BeApproximately(expected, 1e-9);

    [Fact]
    public void Convert_RoundTrip_IsLossless()
    {
        var bytes = UnitFormatter.Convert("3.5", "GB", "B");
        UnitFormatter.Convert(bytes.ToString(System.Globalization.CultureInfo.InvariantCulture), "B", "GB")
            .Should().BeApproximately(3.5, 1e-9);
    }

    [Fact]
    public void Convert_InvalidNumber_Throws() =>
        FluentActions.Invoking(() => UnitFormatter.Convert("abc", "B", "KB")).Should().Throw<FormatException>();

    [Theory]
    [InlineData("1", "X", "B")]
    [InlineData("1", "B", "X")]
    public void Convert_UnknownUnit_Throws(string v, string from, string to) =>
        FluentActions.Invoking(() => UnitFormatter.Convert(v, from, to)).Should().Throw<ArgumentException>();

    // ---- FormatConverted ----

    [Fact]
    public void FormatConverted_SmallValue_NotCollapsedToZero() =>
        UnitFormatter.FormatConverted(500 / (K * K * K), "GB").Should().Be("0.000000466");

    [Fact]
    public void FormatConverted_SmallValue_NeverScientificNotation()
    {
        UnitFormatter.FormatConverted(50000 / (K * K * K), "GB").Should().Be("0.000046566");
        UnitFormatter.FormatConverted(1 / (K * K * K), "GB").Should().Be("0.000000001");
    }

    [Theory]
    [InlineData(322122547.2, "322122547")]   // 0.3 GB in bytes -> whole number
    [InlineData(1741.0, "1741")]
    [InlineData(524288.0, "524288")]
    public void FormatConverted_BytesAreAlwaysWholeNumbers(double value, string expected) =>
        UnitFormatter.FormatConverted(value, "B").Should().Be(expected);

    [Theory]
    [InlineData("B")]
    [InlineData("KB")]
    [InlineData("MB")]
    [InlineData("GB")]
    public void FormatConverted_NeverUsesScientificNotation(string unit)
    {
        foreach (var v in new[] { 1e-12, 5e-9, 0.000001, 1234567890.5, 0.3 })
            UnitFormatter.FormatConverted(v, unit).ToLowerInvariant().Should().NotContain("e");
    }

    [Fact]
    public void FormatConverted_RespectsByteResolutionCap()
    {
        // GB caps at 9 decimals, MB at 6, KB at 3 (floor(log10(bytes-per-unit))).
        UnitFormatter.FormatConverted(0.0001234567899, "GB").Should().Be("0.000123457");
        UnitFormatter.FormatConverted(1.7 / K, "MB").Should().Be("0.00166");
        UnitFormatter.FormatConverted(1741 / K, "KB").Should().Be("1.7");
    }

    [Theory]
    [InlineData(0d, "GB", "0")]
    [InlineData(double.NaN, "GB", "0")]
    [InlineData(double.PositiveInfinity, "GB", "0")]
    public void FormatConverted_NonFinite_ReturnsZero(double v, string unit, string expected) =>
        UnitFormatter.FormatConverted(v, unit).Should().Be(expected);

    // ---- Format ----

    [Theory]
    [InlineData(1536d, "1536")]
    [InlineData(1.5d, "1.5")]
    [InlineData(1.005d, "1")]      // rounds to 2 decimals; 1.005 -> ~1.0 banker's
    [InlineData(1234.567d, "1234.57")]
    [InlineData(0d, "0")]
    public void Format_RoundsToTwoDecimalsAndTrims(double v, string expected) =>
        UnitFormatter.Format(v).Should().Be(expected);

    // ---- FormatFromMb ----

    [Theory]
    [InlineData(500d, "500 MB")]
    [InlineData(1000d, "1000 MB")]      // boundary: not > 1000 -> stays MB
    [InlineData(2048d, "2 GB")]
    [InlineData(1536d, "1.5 GB")]
    public void FormatFromMb_SwitchesToGbAbove1000(double mb, string expected) =>
        UnitFormatter.FormatFromMb(mb).Should().Be(expected);

    // ---- Expand ----

    [Fact]
    public void Expand_ExpandsThroughLowerUnits() =>
        UnitFormatter.Expand("1 GB").Should().Be("1 GB / 1024 MB / 1048576 KB / 1073741824 B");

    [Theory]
    [InlineData("2 KB", "2 KB / 2048 B")]
    [InlineData("0 MB", "0 MB")]
    [InlineData("5 B", "5 B")]
    public void Expand_HandlesVariousInputs(string input, string expected) =>
        UnitFormatter.Expand(input).Should().Be(expected);

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("not a size", "not a size")]
    public void Expand_PassesThroughUnparseable(string? input, string expected) =>
        UnitFormatter.Expand(input).Should().Be(expected);

    // ---- RoundedBytesFromMb ----

    [Fact]
    public void RoundedBytesFromMb_NormalValue() =>
        UnitFormatter.RoundedBytesFromMb(1.0).Should().Be(1049000); // round(1048576/1000)*1000

    [Theory]
    [InlineData(1e15)]
    [InlineData(double.MaxValue)]
    [InlineData(double.PositiveInfinity)]
    public void RoundedBytesFromMb_OutOfRange_DoesNotWrapNegative(double mb) =>
        UnitFormatter.RoundedBytesFromMb(mb).Should().BeGreaterThanOrEqualTo(0L);
}
