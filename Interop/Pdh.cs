using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DiskSpaceTracker.Interop;

/// <summary>
/// Minimal P/Invoke surface for the Windows Performance Data Helper (PDH) API. Used in lieu
/// of <c>System.Diagnostics.PerformanceCounter</c> because the BCL counter type is not
/// AOT-compatible (it depends on reflection-based type loading at runtime).
///
/// <para>Only the calls we actually need are exposed:
/// <c>PdhOpenQuery</c>, <c>PdhAddEnglishCounterW</c>, <c>PdhCollectQueryData</c>,
/// <c>PdhGetFormattedCounterValue</c>, <c>PdhCloseQuery</c>.</para>
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class Pdh
{
    public const uint PDH_FMT_DOUBLE = 0x00000200;
    public const uint PDH_FMT_NOCAP100 = 0x00008000;

    /// <summary>Per-counter CStatus values that indicate the formatted value is usable.</summary>
    public const uint PDH_CSTATUS_NEW_DATA = 1;
    public const uint PDH_CSTATUS_VALID_DATA = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct PDH_FMT_COUNTERVALUE
    {
        public uint CStatus;
        public double DoubleValue;
        // The native union holds several formatted representations, but for PDH_FMT_DOUBLE the
        // double is the only meaningful payload. Declared as the largest member to match the
        // marshaller's size expectation.
    }

    [LibraryImport("pdh.dll", EntryPoint = "PdhOpenQueryW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint PdhOpenQuery(string? szDataSource, nuint dwUserData, out nint phQuery);

    [LibraryImport("pdh.dll", EntryPoint = "PdhAddEnglishCounterW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint PdhAddEnglishCounter(nint hQuery, string szFullCounterPath, nuint dwUserData, out nint phCounter);

    [LibraryImport("pdh.dll")]
    public static partial uint PdhCollectQueryData(nint hQuery);

    [LibraryImport("pdh.dll", EntryPoint = "PdhGetFormattedCounterValue")]
    public static partial uint PdhGetFormattedCounterValue(nint hCounter, uint dwFormat, out uint lpdwType, out PDH_FMT_COUNTERVALUE pValue);

    [LibraryImport("pdh.dll")]
    public static partial uint PdhCloseQuery(nint hQuery);
}
