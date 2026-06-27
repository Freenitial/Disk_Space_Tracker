using DiskSpaceTracker.Models.Presets;

namespace DiskSpaceTracker.Services;

/// <summary>
/// Persistence for the named presets. Stored as JSON next to the executable; serialization
/// goes through <see cref="DiskSpaceTracker.Json.PresetJsonContext"/> for AOT safety.
/// </summary>
public interface IPresetStore
{
    /// <summary>Load all presets. Returns an empty container when the file is missing or invalid.</summary>
    PresetData Load();

    /// <summary>Save the given preset collection, overwriting the on-disk file.</summary>
    void Save(PresetData data);
}
