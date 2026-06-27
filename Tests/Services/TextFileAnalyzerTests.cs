using System;
using System.IO;
using System.Text;
using DiskSpaceTracker.Models;
using DiskSpaceTracker.Services;
using FluentAssertions;
using Xunit;

namespace DiskSpaceTracker.Tests.Services;

/// <summary>
/// File-based tests for the tail-reader and sequential paths, including the trailing-newline
/// invariant: a file ending in a line terminator must NOT shift the -k indices.
/// </summary>
public sealed class TextFileAnalyzerTests : IDisposable
{
    private readonly string _dir;
    private readonly TextFileAnalyzer _sut = new();

    public TextFileAnalyzerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "dst_tfa_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private string WriteUtf8(string content, bool bom = false)
    {
        var path = Path.Combine(_dir, "u8_" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: bom));
        return path;
    }

    private string WriteUtf16(string content)
    {
        var path = Path.Combine(_dir, "u16_" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, content, Encoding.Unicode); // emits a BOM -> forces sequential path
        return path;
    }

    // ---- A trailing newline must not shift negative indices ----

    [Fact]
    public void TailReader_TrailingNewline_LastLineIsContentNotEmpty()
    {
        var f = WriteUtf8("a\nb\nc\n");
        _sut.Test(f, "c", "-1", ComparisonOperator.Equal).Should().BeTrue();
        _sut.Test(f, "", "-1", ComparisonOperator.Equal).Should().BeFalse();
        _sut.Test(f, "b", "-2", ComparisonOperator.Equal).Should().BeTrue();
        _sut.Test(f, "a", "-3", ComparisonOperator.Equal).Should().BeTrue();
    }

    [Fact]
    public void TailReader_NoTrailingNewline()
    {
        var f = WriteUtf8("a\nb\nc");
        _sut.Test(f, "c", "-1", ComparisonOperator.Equal).Should().BeTrue();
        _sut.Test(f, "b", "-2", ComparisonOperator.Equal).Should().BeTrue();
    }

    [Fact]
    public void TailReader_CrlfTrailingNewline()
    {
        var f = WriteUtf8("a\r\nb\r\nc\r\n");
        _sut.Test(f, "c", "-1", ComparisonOperator.Equal).Should().BeTrue();
        _sut.Test(f, "b", "-2", ComparisonOperator.Equal).Should().BeTrue();
    }

    [Fact]
    public void TailReader_NegativeRange()
    {
        var f = WriteUtf8("a\nb\nc\nd\n");
        _sut.Test(f, "d", "-1:-2", ComparisonOperator.Equal).Should().BeTrue();
        _sut.Test(f, "c", "-1:-2", ComparisonOperator.Equal).Should().BeTrue();
        _sut.Test(f, "b", "-1:-2", ComparisonOperator.Equal).Should().BeFalse(); // only last 2
    }

    // ---- Tail path (UTF-8) and sequential path (UTF-16) must agree ----

    [Fact]
    public void TailAndSequentialPaths_Agree_OnNegativeSelector()
    {
        const string content = "alpha\nbeta\ngamma\n";
        var u8 = WriteUtf8(content);
        var u16 = WriteUtf16(content);
        foreach (var (needle, expected) in new[] { ("gamma", true), ("beta", false) })
        {
            _sut.Test(u8, needle, "-1", ComparisonOperator.Equal).Should().Be(expected);
            _sut.Test(u16, needle, "-1", ComparisonOperator.Equal).Should().Be(expected);
        }
    }

    // ---- Positive / any selectors (sequential path) ----

    [Fact]
    public void PositiveSelectors()
    {
        var f = WriteUtf8("a\nb\nc\n");
        _sut.Test(f, "a", "1", ComparisonOperator.Equal).Should().BeTrue();
        _sut.Test(f, "b", "2", ComparisonOperator.Equal).Should().BeTrue();
        _sut.Test(f, "b", "1:2", ComparisonOperator.Equal).Should().BeTrue();
    }

    [Fact]
    public void AnySelector_AndOperators()
    {
        var f = WriteUtf8("hello world\nfoo bar\n");
        _sut.Test(f, "world", "any", ComparisonOperator.Contains).Should().BeTrue();
        _sut.Test(f, "foo", "any", ComparisonOperator.StartsWith).Should().BeTrue();
        _sut.Test(f, "bar", "any", ComparisonOperator.EndsWith).Should().BeTrue();
        _sut.Test(f, "zzz", "any", ComparisonOperator.Contains).Should().BeFalse();
    }

    [Fact]
    public void NonexistentFile_ReturnsFalse() =>
        _sut.Test(Path.Combine(_dir, "does-not-exist.txt"), "x", "1", ComparisonOperator.Equal).Should().BeFalse();
}
