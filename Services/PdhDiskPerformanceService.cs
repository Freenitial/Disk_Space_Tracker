using System;
using System.Runtime.Versioning;
using DiskSpaceTracker.Interop;
using Microsoft.Extensions.Logging;

namespace DiskSpaceTracker.Services;

/// <summary>
/// AOT-safe disk read/write throughput counters ("Disk Read/Write Bytes/sec" for a logical disk)
/// implemented on top of the Windows PDH API. Each call to <see cref="Sample"/> issues a single
/// <c>PdhCollectQueryData</c> followed by two formatted reads. The first sample is intentionally
/// zero (PDH always discards the first delta to compute a rate).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PdhDiskPerformanceService : IDiskPerformanceService
{
    private readonly ILogger<PdhDiskPerformanceService> _logger;
    private nint _query;
    private nint _readCounter;
    private nint _writeCounter;
    private bool _firstSampleDone;
    private bool _disposed;

    public PdhDiskPerformanceService(ILogger<PdhDiskPerformanceService> logger)
    {
        _logger = logger;
    }

    public bool Bind(string driveLetter)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PdhDiskPerformanceService));
        ReleaseQuery();

        if (string.IsNullOrWhiteSpace(driveLetter)) return false;
        var letter = driveLetter.Length >= 1 ? driveLetter[..1] : driveLetter;
        var instance = $"{letter}:";

        var status = Pdh.PdhOpenQuery(null, 0, out _query);
        if (status != 0)
        {
            _logger.LogWarning("PdhOpenQuery failed with status 0x{Status:X8}", status);
            _query = 0;
            return false;
        }

        if (!AddCounter($@"\LogicalDisk({instance})\Disk Read Bytes/sec", out _readCounter)) return CleanupBindFailure();
        if (!AddCounter($@"\LogicalDisk({instance})\Disk Write Bytes/sec", out _writeCounter)) return CleanupBindFailure();

        _firstSampleDone = false;
        return true;
    }

    public DiskThroughputSample Sample()
    {
        if (_query == 0) return default;
        var status = Pdh.PdhCollectQueryData(_query);
        if (status != 0) return default;

        if (!_firstSampleDone)
        {
            _firstSampleDone = true;
            return default;
        }

        var read = ReadCounter(_readCounter);
        var write = ReadCounter(_writeCounter);
        return new DiskThroughputSample(read, write);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ReleaseQuery();
        GC.SuppressFinalize(this);
    }

    // Safety net: if Dispose is somehow never called, the finalizer still closes the native PDH
    // query handle. ReleaseQuery only touches the unmanaged handle and primitive fields, so it is
    // finalizer-safe.
    ~PdhDiskPerformanceService() => ReleaseQuery();

    private bool AddCounter(string path, out nint counter)
    {
        var status = Pdh.PdhAddEnglishCounter(_query, path, 0, out counter);
        if (status != 0)
        {
            _logger.LogWarning("PdhAddEnglishCounter('{Path}') failed with 0x{Status:X8}", path, status);
            counter = 0;
            return false;
        }
        return true;
    }

    private static double ReadCounter(nint counter)
    {
        if (counter == 0) return 0d;
        var status = Pdh.PdhGetFormattedCounterValue(counter, Pdh.PDH_FMT_DOUBLE | Pdh.PDH_FMT_NOCAP100, out _, out var value);
        if (status != 0) return 0d;
        // Even on a successful call the per-counter CStatus must be valid before DoubleValue is
        // meaningful — otherwise we'd read a stale/garbage union payload. Transient "no data yet"
        // states map to 0 throughput, which is the correct value to display.
        return value.CStatus is Pdh.PDH_CSTATUS_VALID_DATA or Pdh.PDH_CSTATUS_NEW_DATA
            ? value.DoubleValue
            : 0d;
    }

    private bool CleanupBindFailure()
    {
        ReleaseQuery();
        return false;
    }

    private void ReleaseQuery()
    {
        if (_query != 0)
        {
            _ = Pdh.PdhCloseQuery(_query);
            _query = 0;
        }
        _readCounter = 0;
        _writeCounter = 0;
        _firstSampleDone = false;
    }
}
