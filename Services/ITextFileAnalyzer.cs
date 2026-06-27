using DiskSpaceTracker.Models;

namespace DiskSpaceTracker.Services;

/// <summary>
/// Evaluates the "Analyze text file" condition. The implementation provides several
/// performance-critical fast paths:
/// <list type="bullet">
/// <item>BOM detection then byte-level tail-reader for negative-only selectors on UTF-8 / ANSI files.</item>
/// <item>StreamReader sequential scan + ring buffer when both positive and negative selectors are used.</item>
/// <item>Trivial fast-path when the needle is empty and the operator is contains/starts/ends.</item>
/// </list>
/// </summary>
public interface ITextFileAnalyzer
{
    /// <summary>
    /// Evaluate the predicate. Returns <c>false</c> when the file does not exist or any I/O
    /// error occurs.
    /// </summary>
    bool Test(string filePath, string lineContent, string lineNumber, ComparisonOperator op);
}
