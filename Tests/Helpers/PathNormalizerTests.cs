using System;
using DiskSpaceTracker.Helpers;
using FluentAssertions;
using Xunit;

namespace DiskSpaceTracker.Tests.Helpers;

public class PathNormalizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_NullOrBlank_ReturnsEmpty(string? input) =>
        PathNormalizer.Normalize(input).Should().BeEmpty();

    [Fact]
    public void Normalize_AlreadyClean_ReturnedUnchanged()
    {
        const string p = @"C:\already\clean\file.txt";
        PathNormalizer.Normalize(p).Should().Be(p);
    }

    [Fact]
    public void Normalize_FlipsForwardSlashes() =>
        PathNormalizer.Normalize("C:/temp/file").Should().Be(@"C:\temp\file");

    [Fact]
    public void Normalize_TrimsSurroundingQuotes() =>
        PathNormalizer.Normalize("\"C:\\temp\\file\"").Should().Be(@"C:\temp\file");

    [Fact]
    public void Normalize_ExpandsEnvironmentVariables()
    {
        var expanded = PathNormalizer.Normalize(@"%SystemRoot%\x");
        expanded.Should().NotContain("%");
        expanded.Should().EndWith(@"\x");
    }

    [Fact]
    public void Normalize_LongPath_GetsPrefix()
    {
        var longPath = @"C:\" + new string('a', 300);
        PathNormalizer.Normalize(longPath).Should().StartWith(@"\\?\C:\");
    }

    [Theory]
    [InlineData("a*", true)]
    [InlineData("a?b", true)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void HasWildcard_Works(string? input, bool expected) =>
        PathNormalizer.HasWildcard(input).Should().Be(expected);
}
