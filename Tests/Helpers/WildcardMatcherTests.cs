using DiskSpaceTracker.Helpers;
using FluentAssertions;
using Xunit;

namespace DiskSpaceTracker.Tests.Helpers;

public class WildcardMatcherTests
{
    [Theory]
    [InlineData("hello", "hello", true)]
    [InlineData("hello", "h*o", true)]
    [InlineData("hello", "h?llo", true)]
    [InlineData("hello", "*", true)]
    [InlineData("", "*", true)]
    [InlineData("", "", true)]
    [InlineData("hello", "H*O", true)]      // case-insensitive
    [InlineData("HELLO", "h*o", true)]
    [InlineData("hello", "h*x", false)]
    [InlineData("hello", "hell", false)]
    [InlineData("hello", "?ello", true)]
    [InlineData("ab", "a?c", false)]
    [InlineData("abc", "a*c", true)]
    [InlineData("aXXXc", "a*c", true)]
    [InlineData("a", "??", false)]
    public void IsMatch_Works(string text, string pattern, bool expected) =>
        WildcardMatcher.IsMatch(text, pattern).Should().Be(expected);

    [Theory]
    [InlineData("abc", "abc", true)]
    [InlineData("ABC", "abc", true)]           // exact path is case-insensitive too
    [InlineData("abc", "a*", true)]
    [InlineData("abc", "a?c", true)]
    [InlineData("abc", "x*", false)]
    [InlineData(null, "a", false)]
    [InlineData("a", null, false)]
    public void MatchOrEquals_Works(string? text, string? pattern, bool expected) =>
        WildcardMatcher.MatchOrEquals(text, pattern).Should().Be(expected);
}
