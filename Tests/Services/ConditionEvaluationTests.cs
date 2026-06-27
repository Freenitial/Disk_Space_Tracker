using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DiskSpaceTracker.Services;
using FluentAssertions;
using Xunit;

namespace DiskSpaceTracker.Tests.Services;

public class ConditionEvaluationTests
{
    [Theory]
    [InlineData(true, new[] { true, true, true }, true)]
    [InlineData(true, new[] { true, false, true }, false)]   // AND: one false -> false
    [InlineData(false, new[] { false, false, true }, true)]  // OR: one true -> true
    [InlineData(false, new[] { false, false, false }, false)]
    public void Combine_AndOr(bool allMode, bool[] results, bool expected) =>
        ConditionEvaluation.Combine(allMode, results).Should().Be(expected);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Combine_Empty_IsSatisfied(bool allMode) =>
        ConditionEvaluation.Combine(allMode, Array.Empty<bool>()).Should().BeTrue();

    [Fact]
    public async Task RunProbesAsync_PreservesOrderAndResults()
    {
        var probes = new List<Func<bool>> { () => true, () => false, () => true };
        (await ConditionEvaluation.RunProbesAsync(probes)).Should().Equal(true, false, true);
    }

    [Fact]
    public async Task RunProbesAsync_Empty_ReturnsEmpty() =>
        (await ConditionEvaluation.RunProbesAsync(Array.Empty<Func<bool>>())).Should().BeEmpty();

    // Blocking probes must run OFF the calling (UI) thread.
    [Fact]
    public async Task RunProbesAsync_RunsProbesOffTheCallingThread()
    {
        int callerThread = Environment.CurrentManagedThreadId;
        int probeThread = callerThread;
        bool onThreadPool = false;

        await ConditionEvaluation.RunProbesAsync(new List<Func<bool>>
        {
            () =>
            {
                probeThread = Environment.CurrentManagedThreadId;
                onThreadPool = Thread.CurrentThread.IsThreadPoolThread;
                return true;
            }
        });

        onThreadPool.Should().BeTrue();            // ran on a thread-pool thread (never the UI thread)
        probeThread.Should().NotBe(callerThread);  // ...i.e. not inline on the caller
    }

    // A single throwing probe must NOT fault the batch (which would abort the whole tick);
    // it degrades to "not met" while the others still evaluate.
    [Fact]
    public async Task RunProbesAsync_IsolatesAThrowingProbe()
    {
        var probes = new List<Func<bool>>
        {
            () => true,
            () => throw new InvalidOperationException("boom"),
            () => true,
        };
        (await ConditionEvaluation.RunProbesAsync(probes)).Should().Equal(true, false, true);
    }
}
