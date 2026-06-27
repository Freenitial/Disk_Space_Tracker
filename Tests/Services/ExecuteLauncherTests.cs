using System;
using System.IO;
using System.Runtime.Versioning;
using DiskSpaceTracker.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DiskSpaceTracker.Tests.Services;

[SupportedOSPlatform("windows")]
public class ExecuteLauncherTests
{
    private static readonly string Cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
    private readonly ExecuteLauncher _sut = new(NullLogger<ExecuteLauncher>.Instance);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Launch_EmptyPayload_ReturnsNull(string payload) =>
        _sut.Launch(payload).Should().BeNull();

    [Fact]
    public void IsRunning_Null_ReturnsFalse() =>
        _sut.IsRunning(null).Should().BeFalse();

    [Fact]
    public void Launch_ReturnsLiveProcess_AndDetectsExit()
    {
        using var p = _sut.Launch($"\"{Cmd}\" /c exit 0");
        p.Should().NotBeNull();
        p!.WaitForExit(5000).Should().BeTrue();
        _sut.IsRunning(p).Should().BeFalse(); // polled on the SAME object — no PID re-resolution
    }

    [Fact]
    public void IsRunning_TracksTheLaunchedProcessUntilKilled()
    {
        using var p = _sut.Launch($"\"{Cmd}\" /c ping 127.0.0.1 -n 6 -w 1000");
        p.Should().NotBeNull();
        _sut.IsRunning(p).Should().BeTrue();
        p!.Kill();
        p.WaitForExit(5000);
        _sut.IsRunning(p).Should().BeFalse();
    }

    // cmd /s /c quoting must handle a .bat whose path (and the redirect target) contain spaces.
    // The bat writes a marker file; with incorrect quoting cmd would fail to find the script.
    [Fact]
    public void Launch_BatWithSpacesInPath_ExecutesAndQuotesCorrectly()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dst exec " + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var marker = Path.Combine(dir, "ran marker.txt");
            var bat = Path.Combine(dir, "do it.bat");
            // Relative redirect target so the bat content stays pure ASCII (the temp dir may contain
            // non-ASCII characters that cmd would misread under its OEM codepage). The bat runs with
            // WorkingDirectory = its own folder, so "ran marker.txt" lands next to it.
            File.WriteAllText(bat, "@echo done> \"ran marker.txt\"\r\n");

            using var p = _sut.Launch($"\"{bat}\"");
            p.Should().NotBeNull();
            p!.WaitForExit(5000).Should().BeTrue();
            File.Exists(marker).Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }
    }
}
