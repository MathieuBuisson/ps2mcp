using System;
using Ps2Mcp.Introspection;

namespace Ps2Mcp.Cli.Tests;

// Unit tests for SystemProcessHandle.ConvertToMilliseconds. The mapping from TimeSpan
// to the int argument of Process.WaitForExit(int) has multiple edge cases (infinite,
// negative non-(-1), overflow, sub-millisecond truncation); exercising them through
// the real Process API would require spawning real processes for each case, so the
// mapping is extracted into a testable internal static method and verified here.
//
// The -1 sentinel (returned for Timeout.InfiniteTimeSpan) is what the WaitForExit
// instance method branches on to call the parameterless Process.WaitForExit() for
// true infinite wait. The original implementation special-cased this check inside
// WaitForExit itself, which left the conversion logic untested against the concrete
// implementation and allowed a regression where InfiniteTimeSpan was clamped to 0
// and turned into an immediate-return that the caller interpreted as a timeout.
public sealed class SystemProcessHandleTests
{
    [Fact]
    public void ConvertToMilliseconds_TimeoutInfiniteTimeSpan_ReturnsMinusOneSentinel()
    {
        var result = SystemProcessHandle.ConvertToMilliseconds(Timeout.InfiniteTimeSpan);

        Assert.Equal(-1, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1500)]
    [InlineData(60_000)]
    public void ConvertToMilliseconds_FinitePositiveTimeSpan_ReturnsMilliseconds(int milliseconds)
    {
        var result = SystemProcessHandle.ConvertToMilliseconds(TimeSpan.FromMilliseconds(milliseconds));

        Assert.Equal(milliseconds, result);
    }

    [Theory]
    [InlineData(1, 1000)]
    [InlineData(30, 30_000)]
    [InlineData(60, 60_000)]
    public void ConvertToMilliseconds_SubSecondToMinutes_RoundsToWholeMilliseconds(int seconds, int expectedMs)
    {
        var result = SystemProcessHandle.ConvertToMilliseconds(TimeSpan.FromSeconds(seconds));

        Assert.Equal(expectedMs, result);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.1)]
    public void ConvertToMilliseconds_SubMillisecondTruncatesToZero(double milliseconds)
    {
        // (int) cast truncates rather than rounds; a 0.5 ms timeout becomes 0 ms.
        // This is consistent with .NET's Process.WaitForExit(int) contract.
        var result = SystemProcessHandle.ConvertToMilliseconds(TimeSpan.FromMilliseconds(milliseconds));

        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(-10_000)]
    public void ConvertToMilliseconds_NegativeNonInfiniteTimeSpan_ClampsToZero(int milliseconds)
    {
        // Note: in .NET 10, Timeout.InfiniteTimeSpan is defined as
        // TimeSpan.FromMilliseconds(-1) (Ticks = -10000), NOT new TimeSpan(-1) (Ticks = -1).
        // So TimeSpan.FromMilliseconds(-1) IS the infinite sentinel and is matched by
        // the exact-equality check above. We use a different negative value here that
        // is NOT the infinite sentinel to exercise the negative-clamp branch.
        var result = SystemProcessHandle.ConvertToMilliseconds(TimeSpan.FromMilliseconds(milliseconds));

        Assert.Equal(0, result);
    }

    [Fact]
    public void ConvertToMilliseconds_OneTickBeforeZero_ClampsToZero()
    {
        // new TimeSpan(-1) creates a TimeSpan with _ticks = -1, which is NOT the same
        // value as Timeout.InfiniteTimeSpan (Ticks = -10000 in .NET 10). This verifies
        // the exact-equality check distinguishes the two and the negative-clamp branch
        // catches all other negatives.
        var result = SystemProcessHandle.ConvertToMilliseconds(new TimeSpan(-1));

        Assert.Equal(0, result);
    }

    [Fact]
    public void ConvertToMilliseconds_TimeSpanMaxValue_ClampsToIntMaxValue()
    {
        // TimeSpan.MaxValue is ~10,675,199 days, far exceeding int.MaxValue ms (~24.8 days).
        var result = SystemProcessHandle.ConvertToMilliseconds(TimeSpan.MaxValue);

        Assert.Equal(int.MaxValue, result);
    }

    [Fact]
    public void ConvertToMilliseconds_TimeSpanMinValue_ClampsToZero()
    {
        // TimeSpan.MinValue is negative but not the InfiniteTimeSpan sentinel; the
        // negative-clamp branch catches it and returns 0.
        var result = SystemProcessHandle.ConvertToMilliseconds(TimeSpan.MinValue);

        Assert.Equal(0, result);
    }

    [Fact]
    public void ConvertToMilliseconds_TimeoutInfiniteTimeSpan_DistinctFromOneTickBeforeZero()
    {
        // Guards against future refactors that lose the exact-equality check on
        // Timeout.InfiniteTimeSpan and accidentally collapse it into the negative
        // clamp (which would map it to 0 — the original bug). In .NET 10,
        // Timeout.InfiniteTimeSpan.Ticks = -10000; new TimeSpan(-1).Ticks = -1.
        // These are different values and the exact-equality check must distinguish
        // them.
        var infinite = SystemProcessHandle.ConvertToMilliseconds(Timeout.InfiniteTimeSpan);
        var oneTickBeforeZero = SystemProcessHandle.ConvertToMilliseconds(new TimeSpan(-1));

        Assert.Equal(-1, infinite);
        Assert.Equal(0, oneTickBeforeZero);
        Assert.NotEqual(infinite, oneTickBeforeZero);
    }

    [Fact]
    public void ConvertToMilliseconds_Net10InfiniteTimeSpanIsFromMillisecondsNegativeOne()
    {
        // Document the .NET 10 contract that drives the test design above: in .NET 10,
        // Timeout.InfiniteTimeSpan and TimeSpan.FromMilliseconds(-1) are the same value.
        // This test will fail if .NET changes the definition, alerting us to re-evaluate
        // the negative-clamp branch.
        Assert.Equal(Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(-1));
    }
}
