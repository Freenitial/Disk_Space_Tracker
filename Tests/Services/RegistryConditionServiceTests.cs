using System.Runtime.Versioning;
using DiskSpaceTracker.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DiskSpaceTracker.Tests.Services;

[SupportedOSPlatform("windows")]
public class RegistryConditionServiceTests
{
    private readonly RegistryConditionService _sut = new(NullLogger<RegistryConditionService>.Instance);

    [Fact]
    public void ExistingMultiSegmentKey_IsFound() =>
        _sut.Test(@"HKLM\SOFTWARE\Microsoft", null, null, shouldExist: true).Should().BeTrue();

    [Fact]
    public void MissingKey_IsNotFound() =>
        _sut.Test(@"HKLM\SOFTWARE\__DST_DoesNotExist__zzz", null, null, shouldExist: true).Should().BeFalse();

    [Fact]
    public void ShouldExistFalse_OnExistingKey_IsFalse() =>
        _sut.Test(@"HKLM\SOFTWARE\Microsoft", null, null, shouldExist: false).Should().BeFalse();

    // A multi-segment WILDCARD path must resolve correctly: the shared base key stays valid for the
    // whole walk in ExpandKeyPaths rather than being disposed mid-walk and swallowed to "not found".
    [Fact]
    public void MultiSegmentWildcardKey_IsFound() =>
        _sut.Test(@"HKLM\SOFTWARE\Micro*", null, null, shouldExist: true).Should().BeTrue();

    [Fact]
    public void HiveRootOnly_IsFound() =>
        _sut.Test("HKLM", null, null, shouldExist: true).Should().BeTrue();

    [Fact]
    public void ExistingValue_IsFound() =>
        _sut.Test(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", null, shouldExist: true)
            .Should().BeTrue();

    [Fact]
    public void MissingValue_IsNotFound() =>
        _sut.Test(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "__Dst_NoSuchValue__", null, shouldExist: true)
            .Should().BeFalse();

    [Fact]
    public void UnknownHive_ReturnsNotFound() =>
        _sut.Test(@"BOGUSHIVE\foo\bar", null, null, shouldExist: true).Should().BeFalse();
}
