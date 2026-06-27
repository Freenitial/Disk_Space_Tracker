using System;
using System.Collections.Generic;
using DiskSpaceTracker.Helpers;
using FluentAssertions;
using Xunit;

namespace DiskSpaceTracker.Tests.Helpers;

public class LineSelectorParserTests
{
    [Theory]
    [InlineData("any")]
    [InlineData("(any)")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_AnyTokens_SetMatchAny(string? spec) =>
        LineSelectorParser.Parse(spec).MatchAny.Should().BeTrue();

    [Fact]
    public void Parse_PositiveIndex()
    {
        var s = LineSelectorParser.Parse("5");
        s.PosIndices.Should().Contain(5);
        s.MatchAny.Should().BeFalse();
    }

    [Fact]
    public void Parse_NegativeIndex_TracksNeedLastK()
    {
        var s = LineSelectorParser.Parse("-3");
        s.NegIndices.Should().Contain(3);
        s.NeedLastK.Should().Be(3);
    }

    [Fact]
    public void Parse_MultipleTokens()
    {
        var s = LineSelectorParser.Parse("1,2,3");
        s.PosIndices.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Theory]
    [InlineData("5:10")]
    [InlineData("10:5")]   // out-of-order normalizes
    [InlineData("5..10")]
    [InlineData("5_10")]
    public void Parse_PositiveRange(string spec) =>
        LineSelectorParser.Parse(spec).PosRanges.Should().Contain((5, 10));

    [Fact]
    public void Parse_OpenPositiveEnd() =>
        LineSelectorParser.Parse("5:").PosRanges.Should().Contain((5, int.MaxValue));

    [Fact]
    public void Parse_OpenPrefix() =>
        LineSelectorParser.Parse(":5").PosRanges.Should().Contain((1, 5));

    [Fact]
    public void Parse_NegativeRange()
    {
        var s = LineSelectorParser.Parse("-1:-3");
        s.NegRanges.Should().Contain((1, 3));
        s.NeedLastK.Should().Be(3);
    }

    // ---- Mixed-sign ranges must keep both endpoints, not drop the positive one ----

    [Theory]
    [InlineData("-3:5")]
    [InlineData("5:-3")]
    public void Parse_MixedSignRange_KeepsBothEndpoints(string spec)
    {
        var s = LineSelectorParser.Parse(spec);
        s.PosRanges.Should().Contain((5, int.MaxValue));
        s.NegRanges.Should().Contain((1, 3));
        s.NeedLastK.Should().Be(3);
    }

    // ---- Zero endpoints are treated as "absent" ----

    [Fact]
    public void Parse_ZeroStart_BehavesLikeOpenPrefix() =>
        LineSelectorParser.Parse("0:5").PosRanges.Should().Contain((1, 5));

    [Fact]
    public void Parse_ZeroEnd_BehavesLikeOpenPositive() =>
        LineSelectorParser.Parse("5:0").PosRanges.Should().Contain((5, int.MaxValue));

    [Fact]
    public void Parse_BothZero_YieldsNothing()
    {
        var s = LineSelectorParser.Parse("0:0");
        s.PosRanges.Should().BeEmpty();
        s.NegRanges.Should().BeEmpty();
        s.PosIndices.Should().BeEmpty();
        s.NegIndices.Should().BeEmpty();
    }

    // ---- NormalizeRanges ----

    [Fact]
    public void NormalizeRanges_MergesOverlapping() =>
        LineSelectorParser.NormalizeRanges(new List<(int, int)> { (1, 3), (2, 5) })
            .Should().BeEquivalentTo(new[] { (1, 5) });

    [Fact]
    public void NormalizeRanges_MergesAdjacent() =>
        LineSelectorParser.NormalizeRanges(new List<(int, int)> { (1, 2), (3, 4) })
            .Should().BeEquivalentTo(new[] { (1, 4) });

    [Fact]
    public void NormalizeRanges_KeepsDisjoint() =>
        LineSelectorParser.NormalizeRanges(new List<(int, int)> { (1, 2), (5, 6) })
            .Should().BeEquivalentTo(new[] { (1, 2), (5, 6) });

    [Theory]
    [InlineData(7, true)]
    [InlineData(5, true)]
    [InlineData(10, true)]
    [InlineData(4, false)]
    [InlineData(11, false)]
    public void InRange_Works(int i, bool expected) =>
        LineSelectorParser.InRange(new[] { (5, 10) }, i).Should().Be(expected);

    // An open-ended range (X, int.MaxValue) absorbs later ranges without the curB + 1 addition
    // overflowing and breaking the merge.
    [Fact]
    public void NormalizeRanges_OpenEndedRange_AbsorbsLaterRanges() =>
        LineSelectorParser.NormalizeRanges(new List<(int, int)> { (5, int.MaxValue), (100, 200) })
            .Should().BeEquivalentTo(new[] { (5, int.MaxValue) });
}
