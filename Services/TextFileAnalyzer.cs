using System;
using System.IO;
using System.Text;
using DiskSpaceTracker.Helpers;
using DiskSpaceTracker.Models;

namespace DiskSpaceTracker.Services;

/// <summary>
/// Evaluates a text-file line condition. Performance-critical hot paths:
/// <list type="bullet">
/// <item><b>Tail reader</b> for negative-only selectors on UTF-8 / ANSI files: reads the file in 64 KB
/// chunks from the end while counting <c>0x0A</c> bytes — only the tail is actually parsed.</item>
/// <item><b>Sequential scan + ring buffer</b> when both positive and negative selectors are present.
/// The ring buffer is sized to <see cref="LineSelector.NeedLastK"/>.</item>
/// <item><b>Trivial fast path</b>: empty needle with contains/starts/ends operator returns immediately
/// once a line is read (every line "contains" empty / "starts with" empty / "ends with" empty).</item>
/// <item>Negation is collapsed at the entry point via <see cref="NormalizedComparison"/>; the inner loop
/// only deals with the four core operators.</item>
/// </list>
/// File I/O uses <see cref="FileShare.ReadWrite"/> so the analyzer never blocks the producer.
/// </summary>
public sealed class TextFileAnalyzer : ITextFileAnalyzer
{
    private const int ChunkSize = 65536;

    public bool Test(string filePath, string lineContent, string lineNumber, ComparisonOperator op)
    {
        var path = PathNormalizer.Normalize(filePath);
        if (!File.Exists(path)) return false;

        var normalized = NormalizedComparison.From(op);
        var negated = normalized.Negated;
        var coreOp = normalized.CoreOp;
        var needle = lineContent ?? string.Empty;
        var trivialAllLines = needle.Length == 0 && coreOp is ComparisonOperator.Contains or ComparisonOperator.StartsWith or ComparisonOperator.EndsWith;

        var retIfMatch = !negated;
        var retFinal = negated;

        var selector = LineSelectorParser.Parse(lineNumber);
        var posRanges = LineSelectorParser.NormalizeRanges(selector.PosRanges);
        var negRanges = LineSelectorParser.NormalizeRanges(selector.NegRanges);

        var tailOnly = !selector.MatchAny
            && selector.PosIndices.Count == 0
            && posRanges.Length == 0
            && selector.NeedLastK > 0;

        try
        {
            if (tailOnly)
            {
                var tailResult = TryTailEvaluate(path, selector, negRanges, needle, coreOp, trivialAllLines, retIfMatch, retFinal);
                if (tailResult.HasValue) return tailResult.Value;
                // Fall through to the StreamReader path for UTF-16.
            }

            return SequentialEvaluate(path, selector, posRanges, negRanges, needle, coreOp, trivialAllLines, retIfMatch, retFinal);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tail-reader fast path. Returns <c>null</c> when the file is UTF-16 (caller falls back to the
    /// sequential path) or when the file is empty.
    /// </summary>
    private static bool? TryTailEvaluate(
        string path,
        LineSelector selector,
        (int A, int B)[] negRanges,
        string needle,
        ComparisonOperator coreOp,
        bool trivialAllLines,
        bool retIfMatch,
        bool retFinal)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, ChunkSize, FileOptions.RandomAccess);
        var len = fs.Length;
        if (len <= 0) return retFinal;

        Span<byte> bom = stackalloc byte[3];
        var savedPosition = fs.Position;
        var bomRead = fs.Read(bom);
        fs.Position = savedPosition;

        Encoding encoding;
        if (bomRead >= 2 && bom[0] == 0xFF && bom[1] == 0xFE) return null; // UTF-16 LE → caller falls back.
        if (bomRead >= 2 && bom[0] == 0xFE && bom[1] == 0xFF) return null; // UTF-16 BE → caller falls back.

        encoding = (bomRead >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
            ? Encoding.UTF8
            : Encoding.Default;

        // Find the byte offset where the last (NeedLastK + 1) lines begin by scanning backwards
        // in 64 KB chunks counting LF bytes. Scan ONE newline more than needed: a file ending in
        // a line terminator would otherwise stop the scan on that trailing terminator, leaving the
        // last content line outside the window and shifting every negative (-k) index by one. The
        // needed tail is then read in a single contiguous pass.
        var buffer = new byte[ChunkSize];
        int needLines = Math.Max(1, selector.NeedLastK);
        int scanLines = needLines + 1;
        long position = len;
        long tailStart = 0;
        int lfCount = 0;

        while (position > 0)
        {
            int readSize = (int)Math.Min(ChunkSize, position);
            position -= readSize;
            fs.Seek(position, SeekOrigin.Begin);
            int n = fs.Read(buffer, 0, readSize);
            int boundary = -1;
            for (int i = n - 1; i >= 0; i--)
            {
                if (buffer[i] == 0x0A)
                {
                    lfCount++;
                    if (lfCount >= scanLines) { boundary = i; break; }
                }
            }
            if (boundary >= 0) { tailStart = position + boundary + 1; break; }
            tailStart = position;
        }

        int tailLen = (int)(len - tailStart);
        var bytes = new byte[tailLen];
        fs.Seek(tailStart, SeekOrigin.Begin);
        fs.ReadExactly(bytes, 0, tailLen);
        var tailText = encoding.GetString(bytes);
        // Split on \r\n, \n, or \r. A single trailing terminator ends the last line rather than
        // starting a new empty one; Split() yields a trailing "" that is dropped so the -k indices
        // line up with the sequential path.
        var tailLines = tailText.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        if (tailLines.Length > 1 && tailLines[^1].Length == 0
            && tailText.Length > 0 && (tailText[^1] == '\n' || tailText[^1] == '\r'))
        {
            Array.Resize(ref tailLines, tailLines.Length - 1);
        }

        // -k means "the kth-from-last line". The slice we have starts with extras at the front
        // (because we may have read past needed lines due to the chunked LF count).
        foreach (var k in selector.NegIndices)
        {
            var idx = tailLines.Length - k;
            if (idx >= 0 && idx < tailLines.Length)
            {
                if (TestLine(tailLines[idx], coreOp, needle, trivialAllLines))
                    return retIfMatch;
            }
        }
        for (int r = 0; r < negRanges.Length; r++)
        {
            for (int k = negRanges[r].A; k <= negRanges[r].B; k++)
            {
                var idx = tailLines.Length - k;
                if (idx >= 0 && idx < tailLines.Length)
                {
                    if (TestLine(tailLines[idx], coreOp, needle, trivialAllLines))
                        return retIfMatch;
                }
            }
        }
        return retFinal;
    }

    /// <summary>
    /// Sequential StreamReader scan with optional ring buffer for negative selectors. Used for
    /// any file that is UTF-16 or that has both positive and negative selectors.
    /// </summary>
    private static bool SequentialEvaluate(
        string path,
        LineSelector selector,
        (int A, int B)[] posRanges,
        (int A, int B)[] negRanges,
        string needle,
        ComparisonOperator coreOp,
        bool trivialAllLines,
        bool retIfMatch,
        bool retFinal)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, ChunkSize, FileOptions.SequentialScan);
        using var reader = new StreamReader(fs, detectEncodingFromByteOrderMarks: true);

        if (selector.MatchAny)
        {
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (TestLine(line, coreOp, needle, trivialAllLines))
                    return retIfMatch;
            }
            return retFinal;
        }

        string[]? ring = null;
        int ringWrite = 0;
        int ringCapacity = selector.NeedLastK;
        if (ringCapacity > 0) ring = new string[ringCapacity];

        var posIndices = selector.PosIndices;
        int i = 0;
        string? current;
        while ((current = reader.ReadLine()) is not null)
        {
            i++;
            if (ring is not null)
            {
                ring[ringWrite] = current;
                ringWrite = (ringWrite + 1) % ringCapacity;
            }
            if (posIndices.Contains(i) || LineSelectorParser.InRange(posRanges, i))
            {
                if (TestLine(current, coreOp, needle, trivialAllLines))
                    return retIfMatch;
            }
        }

        if (ring is not null && i > 0)
        {
            int count = Math.Min(i, ringCapacity);
            foreach (var k in selector.NegIndices)
            {
                if (k > count) continue;
                var idx = ringWrite - k;
                if (idx < 0) idx += ringCapacity;
                var ln = ring[idx];
                if (ln is null) continue;
                if (TestLine(ln, coreOp, needle, trivialAllLines))
                    return retIfMatch;
            }
            for (int r = 0; r < negRanges.Length; r++)
            {
                for (int k = negRanges[r].A; k <= negRanges[r].B; k++)
                {
                    if (k > count) continue;
                    var idx = ringWrite - k;
                    if (idx < 0) idx += ringCapacity;
                    var ln = ring[idx];
                    if (ln is null) continue;
                    if (TestLine(ln, coreOp, needle, trivialAllLines))
                        return retIfMatch;
                }
            }
        }

        return retFinal;
    }

    private static bool TestLine(string line, ComparisonOperator coreOp, string needle, bool trivialAllLines)
    {
        if (trivialAllLines) return true;
        // Trim trailing whitespace before comparing.
        var span = line.AsSpan().TrimEnd();
        return coreOp switch
        {
            ComparisonOperator.Equal => span.Equals(needle.AsSpan(), StringComparison.OrdinalIgnoreCase),
            ComparisonOperator.Contains => span.Contains(needle, StringComparison.OrdinalIgnoreCase),
            ComparisonOperator.StartsWith => span.StartsWith(needle, StringComparison.OrdinalIgnoreCase),
            ComparisonOperator.EndsWith => span.EndsWith(needle, StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }
}
