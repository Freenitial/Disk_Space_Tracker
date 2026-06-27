using System;
using System.IO;
using DiskSpaceTracker.Models.Presets;
using DiskSpaceTracker.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DiskSpaceTracker.Tests.Services;

public class JsonPresetStoreTests
{
    [Fact]
    public void SaveThenLoad_RoundTrips_AndLeavesNoTempFile()
    {
        var store = new JsonPresetStore(NullLogger<JsonPresetStore>.Instance);
        store.Save(new PresetData());

        var path = Path.Combine(AppContext.BaseDirectory, "presets.json");
        File.Exists(path).Should().BeTrue();
        File.Exists(path + ".tmp").Should().BeFalse(); // temp swapped in atomically, not left behind

        store.Load().Should().NotBeNull();
    }

    [Fact]
    public void Save_Twice_HitsAtomicReplacePath()
    {
        var store = new JsonPresetStore(NullLogger<JsonPresetStore>.Instance);
        store.Save(new PresetData());
        store.Save(new PresetData()); // second save replaces the existing file via File.Replace
        store.Load().Should().NotBeNull();
        File.Exists(Path.Combine(AppContext.BaseDirectory, "presets.json") + ".tmp").Should().BeFalse();
    }
}
