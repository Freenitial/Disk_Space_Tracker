using System.Collections.Generic;

namespace DiskSpaceTracker.Models;

/// <summary>
/// Parsed line selector for the text-file analyzer: a flag for "any line", explicit positive
/// 1-based indices, positive ranges, and the negative-from-end forms. <see cref="NeedLastK"/>
/// is the maximum K such that the selector touches the last K lines — used to size the ring
/// buffer or trigger the tail-reader.
/// </summary>
public sealed class LineSelector
{
    public bool MatchAny { get; set; }
    public HashSet<int> PosIndices { get; } = new();
    public List<(int A, int B)> PosRanges { get; } = new();
    public HashSet<int> NegIndices { get; } = new();
    public List<(int A, int B)> NegRanges { get; } = new();
    public int NeedLastK { get; set; }
}
