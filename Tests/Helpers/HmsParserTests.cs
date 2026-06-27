using System;
using DiskSpaceTracker.Helpers;
using FluentAssertions;
using Xunit;

namespace DiskSpaceTracker.Tests.Helpers;

public class HmsParserTests
{
    [Theory]
    [InlineData("0", 0)]
    [InlineData("90", 90)]
    [InlineData("1:30", 90)]
    [InlineData("1:00:00", 3600)]
    [InlineData("01:02:03", 3723)]
    [InlineData("1:70", 130)]            // no per-field 0-59 validation
    public void Parse_ValidForms(string input, int expectedSeconds) =>
        HmsParser.Parse(input).Should().Be(TimeSpan.FromSeconds(expectedSeconds));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("-5")]                   // negative not allowed
    [InlineData("1:2:3:4")]              // too many parts
    [InlineData("1::2")]                 // empty middle field
    public void Parse_InvalidForms_ReturnNull(string? input) =>
        HmsParser.Parse(input).Should().BeNull();

    [Theory]
    [InlineData("1：30")]            // full-width colon
    [InlineData("1∶30")]            // ratio sign as colon
    public void Parse_NormalizesExoticColons(string input) =>
        HmsParser.Parse(input).Should().Be(TimeSpan.FromSeconds(90));

    [Fact]
    public void Parse_HugeInput_ReturnsNullWithoutThrowing() =>
        HmsParser.Parse("99999999999999999999").Should().BeNull();

    [Fact]
    public void Parse_HugeComponents_DoNotOverflow() =>
        HmsParser.Parse("9999999999:9999999999:9999999999").Should().BeNull();

    [Fact]
    public void Parse_LongInputWithCleanupChar_DoesNotOverflowStack()
    {
        // Exercises the pooled-heap fallback in Cleanup() for inputs longer than the stack limit.
        var huge = new string('1', 400) + "：" + new string('2', 400);
        FluentActions.Invoking(() => HmsParser.Parse(huge)).Should().NotThrow();
        HmsParser.Parse(huge).Should().BeNull(); // overflows the duration cap
    }

    [Theory]
    [InlineData(0, "00:00:00")]
    [InlineData(3723, "01:02:03")]
    [InlineData(360000, "100:00:00")]
    public void Format_PadsHoursToTwoDigits(int seconds, string expected) =>
        HmsParser.Format(TimeSpan.FromSeconds(seconds)).Should().Be(expected);

    [Theory]
    [InlineData(3723, "1:02:03")]
    [InlineData(360000, "100:00:00")]
    public void FormatLong_DoesNotPadHours(int seconds, string expected) =>
        HmsParser.FormatLong(TimeSpan.FromSeconds(seconds)).Should().Be(expected);

    [Fact]
    public void Format_NegativeSpan_ClampsToZero() =>
        HmsParser.Format(TimeSpan.FromSeconds(-5)).Should().Be("00:00:00");
}
