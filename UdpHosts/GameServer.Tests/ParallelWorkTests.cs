using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Shared.Common;
using Xunit;

namespace GameServer.Tests;

/// <summary>
///     The helper the server's threaded passes run on: it decides how many threads background work
///     gets, and it is the one place that has to be right about running an index range over them -
///     every index exactly once, and a failure out of a body arriving at the caller as itself.
/// </summary>
public class ParallelWorkTests
{
    [Fact]
    public void Range_RunsEveryIndexExactlyOnce()
    {
        const int Count = 1_000;
        int[] seen = new int[Count];

        ParallelWork.Range(Count, 8, index => Interlocked.Increment(ref seen[index]));

        Assert.All(seen, hits => Assert.Equal(1, hits));
    }

    [Fact]
    public void Range_WithOneThreadRunsInIndexOrderOnTheCallingThread()
    {
        var order = new List<int>();
        int callingThread = Environment.CurrentManagedThreadId;

        ParallelWork.Range(16, 1, index =>
        {
            Assert.Equal(callingThread, Environment.CurrentManagedThreadId);
            order.Add(index);
        });

        Assert.Equal(Enumerable.Range(0, 16), order);
    }

    [Fact]
    public void Range_WithAnOffsetRunsExactlyThatRange()
    {
        bool[] seen = new bool[8];

        ParallelWork.Range(2, 4, 4, index => seen[index] = true);

        Assert.Equal(new[] { false, false, true, true, true, true, false, false }, seen);
    }

    [Fact]
    public void Range_RethrowsASingleFailureAsItself()
    {
        var expected = new InvalidOperationException("the pass failed");

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            ParallelWork.Range(64, 4, index =>
            {
                if (index == 7)
                {
                    throw expected;
                }
            }));

        Assert.Same(expected, thrown);
    }

    [Fact]
    public void Range_OfNothingRunsNothing()
    {
        int calls = 0;

        ParallelWork.Range(0, 4, _ => calls++);

        Assert.Equal(0, calls);
    }

    [Fact]
    public void Slices_CoverTheRangeInAscendingOrderWithoutOverlap()
    {
        var slices = ParallelWork.Slices(10, 3);

        Assert.Equal(3, slices.Length);
        Assert.Equal(0, slices[0].From);
        Assert.Equal(10, slices[^1].From + slices[^1].Count);

        for (int i = 1; i < slices.Length; i++)
        {
            Assert.Equal(slices[i - 1].From + slices[i - 1].Count, slices[i].From);
            Assert.True(slices[i].Count > 0);
        }
    }

    [Fact]
    public void Slices_DoNotSplitWhatThereIsNotEnoughOf()
    {
        Assert.Empty(ParallelWork.Slices(0, 4));
        Assert.Single(ParallelWork.Slices(1, 8));
    }

    [Fact]
    public void Resolve_TreatsZeroAndNegativeAsAutomaticAndAPositiveNumberAsGiven()
    {
        Assert.Equal(ParallelWork.AutomaticDegree, ParallelWork.Resolve(0));
        Assert.Equal(ParallelWork.AutomaticDegree, ParallelWork.Resolve(-4));
        Assert.Equal(4, ParallelWork.Resolve(4));
        Assert.InRange(ParallelWork.AutomaticDegree, 1, ParallelWork.MaxAutomaticDegree);
    }
}
