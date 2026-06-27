using System.Text.Json.Serialization;
using DiskSpaceTracker.Models.Presets;

namespace DiskSpaceTracker.Json;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> for preset persistence. Strict typing
/// keeps the entire serializer path AOT-safe — no reflection, no <c>JsonObject</c>, no dynamic
/// shapes. Indentation is enabled so the file is human-friendly when inspected on disk.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PresetData))]
[JsonSerializable(typeof(PresetEntry))]
[JsonSerializable(typeof(PresetTriggerToggles))]
[JsonSerializable(typeof(PresetTabData))]
[JsonSerializable(typeof(PresetAfterActions))]
[JsonSerializable(typeof(PresetConditions))]
[JsonSerializable(typeof(PresetWaitTime))]
[JsonSerializable(typeof(PresetExecute))]
[JsonSerializable(typeof(PresetFileExist))]
[JsonSerializable(typeof(PresetFileLocked))]
[JsonSerializable(typeof(PresetProcessExist))]
[JsonSerializable(typeof(PresetRegEntry))]
[JsonSerializable(typeof(PresetTextFile))]
public sealed partial class PresetJsonContext : JsonSerializerContext;
