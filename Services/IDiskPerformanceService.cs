using System;

namespace DiskSpaceTracker.Services;

/// <summary>One sample of disk read/write throughput for a logical drive, in bytes per second.</summary>
public readonly record struct DiskThroughputSample(double ReadBytesPerSec, double WriteBytesPerSec);

/// <summary>
/// Disk read/write performance counters for a logical drive, backed by PDH (AOT-safe).
/// </summary>
public interface IDiskPerformanceService : IDisposable
{
    /// <summary>
    /// Bind the counters to the given drive letter (e.g. "C:"). Disposes any currently bound
    /// query. Subsequent calls to <see cref="Sample"/> return data for this drive.
    /// </summary>
    /// <returns>True when the counters were bound successfully.</returns>
    bool Bind(string driveLetter);

    /// <summary>Take an instantaneous sample. Returns zeros on the very first call (warm-up).</summary>
    DiskThroughputSample Sample();
}
