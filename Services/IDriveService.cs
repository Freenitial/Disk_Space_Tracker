using System.Collections.Generic;
using DiskSpaceTracker.Models;

namespace DiskSpaceTracker.Services;

/// <summary>Lists local fixed drives and queries free space.</summary>
public interface IDriveService
{
    /// <summary>Enumerate fixed local drives, sorted by drive letter.</summary>
    IReadOnlyList<DriveItem> GetFixedDrives();

    /// <summary>
    /// Free space on <paramref name="driveLetter"/> (e.g. "C:") expressed in megabytes,
    /// rounded to two decimals.
    /// </summary>
    double GetFreeSpaceMb(string driveLetter);
}
