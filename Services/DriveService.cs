using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiskSpaceTracker.Models;
using Microsoft.Extensions.Logging;

namespace DiskSpaceTracker.Services;

/// <summary>
/// Default <see cref="IDriveService"/> backed by <see cref="DriveInfo"/>. Avoids
/// <c>System.Management</c> / WMI which is not AOT-friendly. Filters to fixed local drives.
/// </summary>
public sealed class DriveService : IDriveService
{
    private readonly ILogger<DriveService> _logger;

    public DriveService(ILogger<DriveService> logger) => _logger = logger;

    public IReadOnlyList<DriveItem> GetFixedDrives()
    {
        var result = new List<DriveItem>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed) continue;
            if (!drive.IsReady) continue;

            var letter = drive.Name.Length >= 2 ? drive.Name[..2] : drive.Name;
            string label;
            try
            {
                label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel;
            }
            catch (UnauthorizedAccessException)
            {
                label = "Local Disk";
            }
            catch (IOException)
            {
                label = "Local Disk";
            }

            result.Add(new DriveItem(letter, label));
        }

        return result.OrderBy(static d => d.DriveLetter, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public double GetFreeSpaceMb(string driveLetter)
    {
        if (string.IsNullOrWhiteSpace(driveLetter)) return 0d;
        var letter = driveLetter.Length >= 1 ? driveLetter[..1] : driveLetter;

        try
        {
            var info = new DriveInfo(letter);
            if (!info.IsReady) return 0d;
            // 1 MB == 1_048_576 bytes (DriveInfo reports binary), rounded to 2 decimals.
            return Math.Round((double)info.AvailableFreeSpace / (1024d * 1024d), 2);
        }
        catch (Exception ex)
        {
            // Drive not ready / removed / access denied: report 0 free (graceful UI degradation),
            // but log so a persistent failure is diagnosable instead of silently masked.
            _logger.LogDebug(ex, "GetFreeSpaceMb failed for drive '{Drive}'", driveLetter);
            return 0d;
        }
    }
}
