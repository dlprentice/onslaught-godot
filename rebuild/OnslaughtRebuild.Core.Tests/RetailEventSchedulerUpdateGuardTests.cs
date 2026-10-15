// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>Reconstruction API rejection semantics, not newly measured retail behavior.</summary>
public sealed class RetailEventSchedulerUpdateGuardTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 199)]
    [InlineData(true, 199)]
    public void CaughtNestedUpdateDoesNotAlterTheOuterClockOrRearm(bool float24, int initialFrames)
    {
        var actual = new RetailEventScheduler(float24);
        for (int index = 0; index < initialFrames; index++)
            actual.Update();
        int handle = actual.AddEvent(42, 11, RetailEventScheduler.NextFrame, data: 91).Handle;
        var expected = new RetailEventScheduler(actual.Snapshot);
        int nestedCallbacks = 0;

        static void Rearm(RetailEventScheduler owner, RetailEventDispatch item) =>
            owner.AddEvent(item.EventNum, 999, RetailEventScheduler.NextFrame, reuseHandle: item.Handle);

        RetailEventDispatch[] expectedDispatches = expected.Update(Rearm).ToArray();
        RetailEventDispatch[] actualDispatches = actual.Update((owner, item) =>
        {
            var before = PublicState(owner);
            Assert.Throws<InvalidOperationException>(() => owner.Update((_, _) => nestedCallbacks++));
            Assert.Equal(before, PublicState(owner));
            Rearm(owner, item);
        }).ToArray();

        Assert.Equal(0, nestedCallbacks);
        Assert.Equal(expectedDispatches, actualDispatches);
        AssertEquivalent(expected.Snapshot, actual.Snapshot);
        Assert.Equal(handle, Assert.Single(actualDispatches).Handle);
        Assert.Equal(11, actual.ListenerOf(handle));
        Assert.Equal(91, Assert.Single(actual.Snapshot.Slots).Data);

        // The next legitimate update must deliver at the unchanged clock and
        // recycle the same event. Check behavior beyond the refusal itself.
        Assert.Equal(expected.Update().ToArray(), actual.Update().ToArray());
        AssertEquivalent(expected.Snapshot, actual.Snapshot);
        Assert.Equal(RetailEventScheduler.MaxEvents, actual.FreeEventCount());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterruptedUpdateRetriesDoNotAdvanceStateAndInitRestoresUsability(bool float24)
    {
        var scheduler = new RetailEventScheduler(float24);
        scheduler.AddEvent(42, 11, RetailEventScheduler.NextFrame, data: 91);
        var interruption = new InvalidOperationException("test callback interruption");
        InvalidOperationException observed = Assert.Throws<InvalidOperationException>(
            () => scheduler.Update((_, _) => throw interruption));
        Assert.Same(interruption, observed);
        Assert.Equal(1u, scheduler.FrameCount);
        var before = PublicState(scheduler);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            Assert.Throws<InvalidOperationException>(() => scheduler.Update());
            Assert.Equal(before, PublicState(scheduler));
            Assert.Throws<InvalidOperationException>(() => scheduler.Flush());
            Assert.Throws<InvalidOperationException>(() => scheduler.Snapshot);
        }

        scheduler.Init();
        var fresh = new RetailEventScheduler(float24);
        AssertEquivalent(fresh.Snapshot, scheduler.Snapshot);
        Assert.Equal(fresh.AddEvent(7, 5, RetailEventScheduler.NextFrame),
            scheduler.AddEvent(7, 5, RetailEventScheduler.NextFrame));
        Assert.Equal(fresh.Update().ToArray(), scheduler.Update().ToArray());
        AssertEquivalent(fresh.Snapshot, scheduler.Snapshot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UncaughtNestedUpdateStillInterruptsTheFlushWithoutAnExtraFrame(bool float24)
    {
        var scheduler = new RetailEventScheduler(float24);
        scheduler.AddEvent(1, 1, RetailEventScheduler.NextFrame);
        Assert.Throws<InvalidOperationException>(() => scheduler.Update((owner, _) => owner.Update()));
        Assert.Equal(1u, scheduler.FrameCount);
        Assert.Equal(BitConverter.SingleToUInt32Bits(RetailEventScheduler.ClockTick),
            BitConverter.SingleToUInt32Bits(scheduler.Time));
        Assert.Equal(1, scheduler.CurrentBufferNum);
        Assert.Equal(0, scheduler.ReadyToFlushBuffer);
        Assert.Equal(1, scheduler.TotalEvents);
        Assert.Equal(0u, scheduler.TotalEventsProcessed);
        Assert.Throws<InvalidOperationException>(() => scheduler.Snapshot);
        Assert.Throws<InvalidOperationException>(() => scheduler.Flush());
    }

    private static (uint Frame, uint Time, int Current, int Ready, int Live,
        int FreeHead, uint Total, uint Last, bool Valid) PublicState(RetailEventScheduler scheduler) =>
        (scheduler.FrameCount, BitConverter.SingleToUInt32Bits(scheduler.Time),
         scheduler.CurrentBufferNum, scheduler.ReadyToFlushBuffer, scheduler.TotalEvents,
         scheduler.FreeListHead, scheduler.TotalEventsProcessed,
         scheduler.EventsProcessedInLastUpdate, scheduler.IsValid);

    private static void AssertEquivalent(RetailEventSchedulerSnapshot expected, RetailEventSchedulerSnapshot actual)
    {
        Assert.Equal(expected.TimeBits, actual.TimeBits);
        Assert.Equal(expected.FrameCount, actual.FrameCount);
        Assert.Equal(expected.CurrentBufferNum, actual.CurrentBufferNum);
        Assert.Equal(expected.ReadyToFlushBuffer, actual.ReadyToFlushBuffer);
        Assert.Equal(expected.LiveEvents, actual.LiveEvents);
        Assert.Equal(expected.TotalProcessed, actual.TotalProcessed);
        Assert.Equal(expected.ProcessedThisUpdate, actual.ProcessedThisUpdate);
        Assert.Equal(expected.Valid, actual.Valid);
        Assert.Equal(expected.FreeList, actual.FreeList);
        Assert.Equal(expected.Float24Arithmetic, actual.Float24Arithmetic);
        Assert.Equal(expected.Slots.ToArray(), actual.Slots.ToArray());
        Assert.Equal(expected.Overflow.ToArray(), actual.Overflow.ToArray());
        Assert.Equal(expected.Lanes.Count, actual.Lanes.Count);
        for (int index = 0; index < expected.Lanes.Count; index++)
        {
            Assert.Equal(expected.Lanes[index].LaneIndex, actual.Lanes[index].LaneIndex);
            Assert.Equal(expected.Lanes[index].Handles.ToArray(), actual.Lanes[index].Handles.ToArray());
        }
    }
}
