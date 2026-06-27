using System;
using System.IO;
using System.Text.Json;
using DiskSpaceTracker.Json;
using DiskSpaceTracker.Models.Presets;
using Microsoft.Extensions.Logging;

namespace DiskSpaceTracker.Services;

/// <summary>
/// JSON-on-disk preset persistence using a source-generated <see cref="JsonSerializerContext"/>.
/// The file lives next to the executable as <c>presets.json</c>. Read failures degrade
/// gracefully into an empty container so a corrupt file never crashes startup.
/// </summary>
public sealed class JsonPresetStore : IPresetStore
{
    private readonly ILogger<JsonPresetStore> _logger;
    private readonly string _path;

    public JsonPresetStore(ILogger<JsonPresetStore> logger)
    {
        _logger = logger;
        var baseDir = AppContext.BaseDirectory;
        _path = Path.Combine(baseDir, "presets.json");
    }

    public PresetData Load()
    {
        try
        {
            if (!File.Exists(_path)) return new PresetData();
            using var stream = File.OpenRead(_path);
            var data = JsonSerializer.Deserialize(stream, PresetJsonContext.Default.PresetData);
            return data ?? new PresetData();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load presets from {Path}", _path);
            return new PresetData();
        }
    }

    private readonly object _saveLock = new();

    public void Save(PresetData data)
    {
        // Serialize concurrent saves (the store is a singleton) so two writers can't race on the
        // shared temp path.
        lock (_saveLock)
        {
            var tmp = _path + ".tmp";
            try
            {
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                // Write to a sibling temp file then atomically swap it into place. A crash or power
                // loss mid-write leaves the previous presets.json intact, not a half-written file.
                using (var stream = File.Create(tmp))
                {
                    JsonSerializer.Serialize(stream, data, PresetJsonContext.Default.PresetData);
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(_path))
                    File.Replace(tmp, _path, destinationBackupFileName: null);
                else
                    File.Move(tmp, _path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save presets to {Path}", _path);
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort: do not mask the real failure */ }
                throw;
            }
        }
    }
}
