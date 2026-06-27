using FluentAssertions;
using Xunit;

namespace DiskSpaceTracker.Tests;

/// <summary>Sanity check that the test project builds, references the app, and the runner works.</summary>
public class SmokeTests
{
    [Fact]
    public void TestHarness_Works()
    {
        (2 + 2).Should().Be(4);
    }

    [Fact]
    public void AppAssembly_IsReferenced()
    {
        // A type from the app assembly resolves -> the ProjectReference is wired up.
        typeof(DiskSpaceTracker.Helpers.UnitFormatter).Assembly.Should().NotBeNull();
    }
}
