namespace DiskSpaceTracker.Models;

/// <summary>
/// A single fixed local drive entry rendered in the drive selector. <see cref="DriveLetter"/>
/// is the two-character form ("C:") used everywhere internally; <see cref="Display"/> is the
/// "C: (Local Disk)" form shown in the combo.
/// </summary>
public sealed record DriveItem(string DriveLetter, string Label)
{
    /// <summary>Display string in the format "C: (Local Disk)".</summary>
    public string Display => $"{DriveLetter} ({Label})";

    public override string ToString() => Display;
}
