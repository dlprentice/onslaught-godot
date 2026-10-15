// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// The released <c>start_or_end</c> lane an event is filed under
/// (<c>references/Onslaught/eventmanager.h:15-17</c>). Confirmed in the
/// pristine image: <c>CEventManager::AddEvent</c> at <c>0x0044B591</c> indexes
/// <c>this + 16 * (3 * (offset_buffer + 1) + start_or_end)</c>, so the lane is
/// the minor axis of a <c>[200][3]</c> array of 16-byte sets based at
/// <c>this+0x30</c>, and <c>CEventManager::Flush</c> at <c>0x0044B65B</c> walks
/// exactly three of them per ring slot.
/// </summary>
public enum RetailEventPriority
{
    /// <summary><c>START_OF_FRAME</c>.</summary>
    StartOfFrame = 0,

    /// <summary><c>MIDDLE_OF_FRAME</c>.</summary>
    MiddleOfFrame = 1,

    /// <summary><c>END_OF_FRAME</c>.</summary>
    EndOfFrame = 2,
}

/// <summary>
/// Where <c>CEventManager::AddEvent</c> put an admission, or why it refused it.
/// </summary>
public enum RetailEventPlacement
{
    /// <summary>
    /// <c>mValid == FALSE</c>. Retail logs "FATAL ERROR: Trying to add an event
    /// when event manager was invalid" (string at <c>0x00628D94</c>, reached
    /// from <c>0x0044B380</c>) and returns without storing anything.
    /// </summary>
    RejectedInvalidManager,

    /// <summary>
    /// <c>to_call == NULL</c>. Retail returns silently (<c>0x0044B3A2</c>).
    /// </summary>
    RejectedNullListener,

    /// <summary>
    /// <c>time &gt; 1000000.0f</c>. Retail drops the event with no diagnostic
    /// (<c>0x0044B3F5</c>). The event is lost, not deferred.
    /// </summary>
    RejectedTooFarAhead,

    /// <summary>
    /// The 20 000-entry pool is empty. Retail logs "FATAL ERROR:  Run out of
    /// free scheduled events!!" (string at <c>0x00628D60</c>) and returns.
    /// </summary>
    RejectedPoolExhausted,

    /// <summary>
    /// Filed in the ring slot currently being added to, because
    /// <c>time &lt;= mTime + 0.051f</c>.
    /// </summary>
    ImmediateBucket,

    /// <summary>Filed in a ring slot <c>floor((time-mTime-0.001)*20)</c> ahead.</summary>
    DelayedBucket,

    /// <summary>Filed in the time-ordered overflow list, at or beyond 198 slots.</summary>
    Overflow,
}

/// <summary>
/// What one <c>CEventManager::AddEvent</c> call did.
/// </summary>
/// <param name="Placement">Which arm of the released routing ran.</param>
/// <param name="Handle">Pool index of the scheduled event, or -1 if none was taken.</param>
/// <param name="BufferIndex">Ring slot, or -1 for overflow and rejections.</param>
/// <param name="OverflowIndex">Insertion index in the overflow list, else -1.</param>
/// <param name="DueTimeBits">
/// Exact float32 bits retail stored in <c>CScheduledEvent::mTime</c>. Carried as
/// bits because the immediate-negative arm rounds
/// <c>mTime + 0.0001f</c> through a float store and the ordering scan compares
/// these values directly.
/// </param>
public readonly record struct RetailEventAdmission(
    RetailEventPlacement Placement,
    int Handle,
    int BufferIndex,
    int OverflowIndex,
    uint DueTimeBits)
{
    /// <summary>The stored due time as a float.</summary>
    public float DueTime => BitConverter.UInt32BitsToSingle(DueTimeBits);
}

/// <summary>
/// One <c>to_call-&gt;HandleEvent(next_event)</c> retail would have made, in the
/// order retail would have made it.
/// </summary>
public readonly record struct RetailEventDispatch(
    int Handle,
    int EventNum,
    int Listener,
    uint DueTimeBits,
    RetailEventPriority Priority,
    bool FromOverflow);

/// <summary>Pool words differing from the deterministic Init baseline.</summary>
public sealed record RetailEventSlotSnapshot(int Handle, int NextFree, short EventNum,
    int Listener, int Data, uint TimeBits, bool Reuse);
public sealed record RetailEventLaneSnapshot(int LaneIndex, IReadOnlyList<int> Handles);
public sealed record RetailEventSchedulerSnapshot(uint TimeBits, uint FrameCount,
    int CurrentBufferNum, int ReadyToFlushBuffer, int LiveEvents, uint TotalProcessed,
    uint ProcessedThisUpdate, bool Valid, int FreeList,
    IReadOnlyList<RetailEventSlotSnapshot> Slots,
    IReadOnlyList<RetailEventLaneSnapshot> Lanes, IReadOnlyList<int> Overflow)
{
    public bool Float24Arithmetic { get; init; }
}

/// <summary>
/// The released <c>CEventManager</c> scheduling core: bucket routing, the frame
/// clock, the flush order, and the free-list recycler. Nothing here reads a
/// clock, a device, or a file — retail's own time base is a frame counter times
/// a constant, which is why this is reproducible at all.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop: <c>references/Onslaught/eventmanager.cpp</c> and
/// <c>eventmanager.h</c>. Retail identities read out of the pristine
/// <c>74154bfa…</c> image using its PE section map:
/// </para>
/// <list type="bullet">
/// <item><c>0x0044B370</c> <c>AddEvent(event_num, to_call, time, start_or_end, data, re_use_event)</c>
/// — <c>eventmanager.cpp:170-279</c>.</item>
/// <item><c>0x0044B2D0</c> <c>AddEvent(time_from_now, …)</c> — <c>eventmanager.cpp:143-146</c>;
/// it computes <c>time_from_now + mTime</c> on the x87 stack, stores the sum
/// as float32 at <c>0x0044B2F6</c>, then calls <c>0x0044B370</c>.</item>
/// <item><c>0x0044B2A0</c> <c>GetNextFreeEvent</c> — <c>eventmanager.cpp:112-125</c>.</item>
/// <item><c>0x0044B5C0</c> <c>Update</c> — <c>eventmanager.cpp:284-304</c>, with
/// <c>AdvanceTime</c> inlined and <c>Flush</c> called at <c>0x0044B5F6</c>.</item>
/// <item><c>0x0044B640</c> <c>Flush</c> — <c>eventmanager.cpp:311-411</c>.</item>
/// <item><c>0x0044B310</c> <c>AddEvent(CScheduledEvent*)</c> — <c>eventmanager.cpp:152-162</c>,
/// with <c>FreeEvent</c> inlined at <c>0x0044B346</c>.</item>
/// </list>
/// <para>
/// <b>Source and retail agree</b> on every branch, constant and statement order
/// checked. The pristine image carries both diagnostic strings verbatim
/// (<c>0x00628D94</c>, <c>0x00628D60</c>) and all six scheduling constants as
/// exact float32: <c>0.051f</c> at <c>0x005DB294</c>, <c>0.0001f</c> at
/// <c>0x005D8570</c>, <c>1000000.0f</c> at <c>0x005DB290</c>, <c>0.001f</c> at
/// <c>0x005D8580</c>, <c>GAME_FR = 20.0f</c> at <c>0x005D857C</c> and
/// <c>CLOCK_TICK = 0.05f</c> at <c>0x005D8578</c> (the last two are
/// <c>thing.h:28-29</c>). The member layout the code indexes reproduces the
/// header's field order exactly — <c>mValid+4</c>, <c>mTime+8</c>,
/// <c>mTotalEventProcessedNum+0xC</c>, <c>mCurrentBufferNum+0x10</c>,
/// <c>mFrameCount+0x14</c>, <c>mNumEventsInEventManager+0x18</c>,
/// <c>mReadyToFlushBuffer+0x1C</c>, <c>mEventsProcessedThisUpdate+0x20</c>,
/// <c>mCurrentProcessOverflowEventNum+0x24</c>, <c>mEventFreeList+0x28</c>,
/// <c>mEventsPool+0x2C</c>, ring at <c>+0x30</c> — and closes arithmetically:
/// <c>0x30 + 200 * 3 * 16 = 0x25B0</c>, which is where the code loads
/// <c>mOverflowEventListBuffer</c>.
/// </para>
/// <para>
/// <b>Two details relevant to the translation.</b> First, the event
/// number is stored as a <b>signed 16-bit field</b>: <c>CScheduledEvent::Set</c>
/// at <c>0x004DE1F0</c> writes <c>mov word ptr [esi+4], ax</c>, the re-use arm
/// at <c>0x0044B4E0</c> does the same, and <c>AddEvent(CScheduledEvent*)</c>
/// re-reads it with <c>movsx eax, word ptr [esi+4]</c> at <c>0x0044B32E</c>.
/// This confirms the <c>short</c> storage in <c>event.h</c>: an event number
/// outside <c>[-32768, 32767]</c> wraps silently despite the <c>int</c> parameter.
/// Second, retail keeps every intermediate of the delay computation on the x87
/// stack: there is no float store between <c>time - mTime</c>,
/// <c>- 0.001f</c>, <c>* 20.0f</c> and <c>floor</c>, so the arithmetic runs at
/// the x87 precision control, not at float. This implementation uses
/// <c>double</c> by default for those intermediates, which reproduces the Win32
/// CRT default of 53-bit precision control; this static body does not establish
/// the control word of every runtime caller. Explicit PC24 mode instead rounds
/// each arithmetic operation through RetailFloat24. The immediate-negative arm rounds
/// <c>mTime + 0.0001f</c> with <c>fstp dword</c> at <c>0x0044B3DF</c>.
/// The relative-time overload also rounds its sum before this body is called.
/// </para>
/// <para>
/// <b>Not established here.</b> Which listeners exist, what
/// <c>HandleEvent</c> does, and how <c>SPtrSet</c>/<c>OPtrSet</c> allocate. This
/// type models listeners as opaque non-zero integer identities so the routing,
/// ordering and recycling contracts can be pinned without inventing a
/// listener graph. The 20 000-entry pool agrees with <c>eventmanager.h:20</c>
/// and retail <c>Init</c> at <c>0x0044B060</c>: allocation size
/// <c>0x61A84 = 20000 * 20 + 4</c>, 20 000 constructor iterations and
/// 19 999 next-record links followed by null. Exact body pins and the static
/// evidence boundary are in <c>reverse-engineering/source-code/io/event-system.md</c>.
/// </para>
/// </remarks>
public sealed class RetailEventScheduler
{
    /// <summary><c>NUM_EVENT_LIST_BUFFERS</c> — <c>eventmanager.h:24</c>; the
    /// <c>mov ecx, 0xC8</c> at <c>0x0044B518</c> and <c>mov esi, 0xC8</c> at
    /// <c>0x0044B5C8</c> both carry it.</summary>
    public const int EventListBuffers = 200;

    /// <summary><c>NUM_PRIORITY</c> — <c>eventmanager.h:23</c>; the two
    /// <c>mov dword ptr [esp+0x10], 3</c> loop counters in <c>Flush</c>.</summary>
    public const int PriorityLanes = 3;

    /// <summary><c>MAX_NUM_EVENTS</c> — <c>eventmanager.h:20</c>, confirmed by
    /// the allocation and constructor count in retail Init <c>0x0044B060</c>.</summary>
    public const int MaxEvents = 20_000;

    /// <summary><c>CLOCK_TICK</c> — <c>thing.h:29</c>; <c>0x005D8578</c>.</summary>
    public const float ClockTick = 0.05f;

    /// <summary><c>GAME_FR</c> — <c>thing.h:28</c>; <c>0x005D857C</c>.</summary>
    public const float GameFrameRate = 20.0f;

    /// <summary><c>NEXT_FRAME</c> — <c>eventmanager.h:14</c>. Negative, so it
    /// takes the immediate arm's <c>time &lt; 0.0</c> branch.</summary>
    public const float NextFrame = -1.0f;

    /// <summary>The "already due" window — <c>eventmanager.cpp:186</c>; <c>0x005DB294</c>.</summary>
    internal const float ImmediateWindow = 0.051f;

    /// <summary>Floor applied to negative times — <c>eventmanager.cpp:192</c>; <c>0x005D8570</c>.</summary>
    internal const float ImmediateFloor = 0.0001f;

    /// <summary>Inclusive-boundary bias — <c>eventmanager.cpp:209</c>; <c>0x005D8580</c>.</summary>
    internal const float DelayBias = 0.001f;

    /// <summary>Hard drop ceiling — <c>eventmanager.cpp:202</c>; <c>0x005DB290</c>.</summary>
    internal const float MaximumTime = 1_000_000.0f;

    /// <summary><c>NUM_EVENT_LIST_BUFFERS - 2</c> — the <c>cmp ecx, 0xC6</c> at <c>0x0044B42D</c>.</summary>
    internal const int OverflowBucketThreshold = EventListBuffers - 2;

    private readonly PooledEvent[] _pool = new PooledEvent[MaxEvents];
    private readonly List<int>[] _ring =
        new List<int>[EventListBuffers * PriorityLanes];
    private readonly List<int> _overflow = [];
    private readonly List<RetailEventDispatch> _dispatched = [];
    private int _freeList = -1;
    private float _time;
    private uint _frameCount;
    private int _currentBufferNum;
    private int _readyToFlushBuffer;
    private int _liveEvents;
    private int _overflowCursor;
    private uint _totalProcessed;
    private uint _processedThisUpdate;
    private bool _valid;
    private readonly SortedSet<int> _changedSlots = [];
    private bool _flushing;
    private bool _interruptedFlush;

    /// <summary>Capture only between complete flushes; callbacks are not continuations.</summary>
    public RetailEventSchedulerSnapshot Snapshot
    {
        get
        {
            if (_flushing || _interruptedFlush)
                throw new InvalidOperationException("Cannot capture an active or interrupted Flush.");
            return new(BitConverter.SingleToUInt32Bits(_time), _frameCount,
                _currentBufferNum, _readyToFlushBuffer, _liveEvents, _totalProcessed,
                _processedThisUpdate, _valid, _freeList,
                Array.AsReadOnly(_changedSlots.Select(SlotSnapshot).ToArray()),
                Array.AsReadOnly(Enumerable.Range(0, _ring.Length)
                    .Where(index => _ring[index].Count != 0)
                    .Select(index => new RetailEventLaneSnapshot(index, Array.AsReadOnly(_ring[index].ToArray())))
                    .ToArray()),
                Array.AsReadOnly(_overflow.ToArray())) { Float24Arithmetic = Float24Arithmetic };
        }
    }

    public RetailEventScheduler(RetailEventSchedulerSnapshot snapshot) : this()
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Float24Arithmetic = snapshot.Float24Arithmetic;
        static bool Handle(int value) => value >= -1 && value < MaxEvents;
        if (!Handle(snapshot.FreeList) ||
            (uint)snapshot.CurrentBufferNum >= EventListBuffers ||
            (uint)snapshot.ReadyToFlushBuffer >= EventListBuffers ||
            snapshot.LiveEvents < 0 || snapshot.LiveEvents > MaxEvents ||
            snapshot.TimeBits != BitConverter.SingleToUInt32Bits(TimeAtFrameCount(snapshot.FrameCount)) ||
            snapshot.Slots is null || snapshot.Lanes is null || snapshot.Overflow is null)
            throw new ArgumentException("Invalid scheduler snapshot.", nameof(snapshot));
        int previous = -1;
        foreach (RetailEventSlotSnapshot slot in snapshot.Slots)
        {
            if (slot is null || slot.Handle <= previous || !Handle(slot.Handle) ||
                !Handle(slot.NextFree))
                throw new ArgumentException("Invalid or noncanonical pool slot.", nameof(snapshot));
            previous = slot.Handle;
            _pool[slot.Handle] = new PooledEvent { NextFree = slot.NextFree,
                EventNum = slot.EventNum, Listener = slot.Listener, Data = slot.Data,
                TimeBits = slot.TimeBits, Reuse = slot.Reuse };
            TrackSlot(slot.Handle);
            if (!_changedSlots.Contains(slot.Handle))
                throw new ArgumentException("Baseline pool slots must be omitted.", nameof(snapshot));
        }
        var queued = new HashSet<int>();
        void Queue(int handle, List<int> destination)
        {
            if (handle < 0 || !Handle(handle) || !queued.Add(handle))
                throw new ArgumentException("Invalid or multiply queued handle.", nameof(snapshot));
            destination.Add(handle);
        }
        previous = -1;
        foreach (RetailEventLaneSnapshot lane in snapshot.Lanes)
        {
            if (lane is null || lane.LaneIndex <= previous ||
                (uint)lane.LaneIndex >= _ring.Length || lane.Handles is null || lane.Handles.Count == 0)
                throw new ArgumentException("Invalid or noncanonical lane.", nameof(snapshot));
            previous = lane.LaneIndex;
            foreach (int handle in lane.Handles) Queue(handle, _ring[lane.LaneIndex]);
        }
        foreach (int handle in snapshot.Overflow) Queue(handle, _overflow);
        var free = new HashSet<int>();
        for (int handle = snapshot.FreeList; handle != -1; handle = _pool[handle].NextFree)
            if (!free.Add(handle) || queued.Contains(handle))
                throw new ArgumentException("Free-list cycle or queued ownership overlap.", nameof(snapshot));
        if (snapshot.Valid ? snapshot.LiveEvents != queued.Count : queued.Count != 0)
            throw new ArgumentException("Scheduler event count disagrees with queue ownership.", nameof(snapshot));
        _freeList = snapshot.FreeList;
        _time = BitConverter.UInt32BitsToSingle(snapshot.TimeBits);
        _frameCount = snapshot.FrameCount;
        _currentBufferNum = snapshot.CurrentBufferNum;
        _readyToFlushBuffer = snapshot.ReadyToFlushBuffer;
        _liveEvents = snapshot.LiveEvents;
        _totalProcessed = snapshot.TotalProcessed;
        _processedThisUpdate = snapshot.ProcessedThisUpdate;
        _valid = snapshot.Valid;
    }

    private RetailEventSlotSnapshot SlotSnapshot(int handle)
    {
        PooledEvent slot = _pool[handle];
        return new(handle, slot.NextFree, slot.EventNum, slot.Listener, slot.Data, slot.TimeBits, slot.Reuse);
    }

    private void TrackSlot(int handle)
    {
        PooledEvent slot = _pool[handle];
        bool initial = slot.NextFree == (handle == MaxEvents - 1 ? -1 : handle + 1) &&
            slot.EventNum == 0 && slot.Listener == 0 && slot.Data == 0 && slot.TimeBits == 0 && !slot.Reuse;
        if (initial) _changedSlots.Remove(handle);
        else _changedSlots.Add(handle);
    }

    /// <summary>Builds an initialised manager, as <c>CEventManager::Init</c> leaves one.</summary>
    public RetailEventScheduler(bool useFloat24Arithmetic = false)
    {
        Float24Arithmetic = useFloat24Arithmetic;
        for (int i = 0; i < _ring.Length; i++)
        {
            _ring[i] = [];
        }

        Init();
    }

    /// <summary><c>mTime</c>.</summary>
    public float Time => _time;

    /// <summary>Explicit PC24/RN routing arithmetic; default remains PC53.</summary>
    public bool Float24Arithmetic { get; }

    /// <summary><c>mFrameCount</c>.</summary>
    public uint FrameCount => _frameCount;

    /// <summary><c>mCurrentBufferNum</c> — the slot new near-term events land in.</summary>
    public int CurrentBufferNum => _currentBufferNum;

    /// <summary><c>mReadyToFlushBuffer</c> — the slot the next <see cref="Flush"/> drains.</summary>
    public int ReadyToFlushBuffer => _readyToFlushBuffer;

    /// <summary><c>mNumEventsInEventManager</c>.</summary>
    public int TotalEvents => _liveEvents;

    /// <summary><c>mTotalEventProcessedNum</c>.</summary>
    public uint TotalEventsProcessed => _totalProcessed;

    /// <summary><c>mEventsProcessedThisUpdate</c>.</summary>
    public uint EventsProcessedInLastUpdate => _processedThisUpdate;

    /// <summary><c>mValid</c>.</summary>
    public bool IsValid => _valid;

    /// <summary>Head of <c>mEventFreeList</c> as a pool index, or -1 when empty.</summary>
    public int FreeListHead => _freeList;

    /// <summary>
    /// <c>CEventManager::Init</c> — <c>eventmanager.cpp:41-68</c>. The pool is
    /// chained <c>0-&gt;1-&gt;…-&gt;19998-&gt;19999-&gt;NULL</c> and the head is
    /// entry 0, so the first allocation is handle 0. This Core reset also clears
    /// its containers; it does not model retail Init twice without Shutdown.
    /// </summary>
    public void Init()
    {
        if (_flushing) throw new InvalidOperationException("Cannot reset during Flush.");
        _interruptedFlush = false;
        _changedSlots.Clear();
        _time = 0.0f;
        _currentBufferNum = 0;
        _frameCount = 0;
        _liveEvents = 0;
        _processedThisUpdate = 0;
        _totalProcessed = 0;
        _readyToFlushBuffer = 0;
        _overflowCursor = 0;
        _overflow.Clear();
        _dispatched.Clear();

        foreach (List<int> lane in _ring)
        {
            lane.Clear();
        }

        for (int i = 0; i < MaxEvents - 1; i++)
        {
            _pool[i] = default;
            _pool[i].NextFree = i + 1;
        }

        _pool[MaxEvents - 1] = default;
        _pool[MaxEvents - 1].NextFree = -1;
        _freeList = 0;
        _valid = true;
    }

    /// <summary>
    /// <c>CEventManager::Shutdown</c> — <c>eventmanager.cpp:72-107</c>, restricted
    /// to the part that is deterministic state: <c>mValid</c> clears and every
    /// list empties. The allocator teardown and the developer-only
    /// <c>((char*)mEventsPool)-0x44</c> poke have no reconstruction meaning.
    /// </summary>
    public void Shutdown()
    {
        _valid = false;
        foreach (List<int> lane in _ring)
        {
            lane.Clear();
        }

        _overflow.Clear();
    }

    /// <summary>
    /// <c>CEventManager::GetNextFreeEvent</c> — <c>eventmanager.cpp:112-125</c>,
    /// <c>0x0044B2A0</c>. Pops the head, or returns -1 and leaves the head alone.
    /// </summary>
    public int GetNextFreeEvent()
    {
        int head = _freeList;
        if (head >= 0)
        {
            _freeList = _pool[head].NextFree;
        }

        return head;
    }

    /// <summary>
    /// <c>CEventManager::AddEvent(const float&amp; time_from_now, …)</c> —
    /// <c>eventmanager.cpp:143-146</c>, <c>0x0044B2D0</c>. The sum is formed on
    /// the x87 stack, rounded to float32 by <c>fstp dword</c> at
    /// <c>0x0044B2F6</c>, then passed by reference to the absolute scheduler.
    /// </summary>
    public RetailEventAdmission AddEventTimeFromNow(
        float timeFromNow,
        int eventNum,
        int listener,
        RetailEventPriority priority = RetailEventPriority.StartOfFrame,
        int data = 0,
        int reuseHandle = -1) => AddEvent(
            eventNum,
            listener,
            StoredSum(_time, timeFromNow),
            priority,
            data,
            reuseHandle);

    /// <summary>
    /// <c>CEventManager::AddEvent(const int, CMonitor*, const float&amp;, const int, CMonitor*, CScheduledEvent*)</c>
    /// — <c>eventmanager.cpp:170-279</c>, <c>0x0044B370</c>.
    /// </summary>
    /// <remarks>
    /// The four rejections, the two placements and the overflow ordering are all
    /// reproduced. The overflow scan starts at
    /// <c>mCurrentProcessOverflowEventNum</c>, not at zero, and advances while
    /// the resident due time is <b>less than or equal to</b> the new one
    /// (<c>test ah, 0x41 / je</c> at <c>0x0044B459</c>), so equal-time events
    /// keep insertion order. The ring append is plain FIFO within a lane.
    /// </remarks>
    public RetailEventAdmission AddEvent(
        int eventNum,
        int listener,
        float time,
        RetailEventPriority priority = RetailEventPriority.StartOfFrame,
        int data = 0,
        int reuseHandle = -1)
    {
        if (!_valid)
        {
            return new RetailEventAdmission(
                RetailEventPlacement.RejectedInvalidManager, -1, -1, -1, 0);
        }

        if (listener == 0)
        {
            return new RetailEventAdmission(
                RetailEventPlacement.RejectedNullListener, -1, -1, -1, 0);
        }

        uint nextTimeBits = BitConverter.SingleToUInt32Bits(time);
        int offsetBuffer;
        RetailEventPlacement placement;

        if (Float24Arithmetic ? RetailFloat24.Add(_time, ImmediateWindow) >= time : IsImmediate(_time, time))
        {
            placement = RetailEventPlacement.ImmediateBucket;
            offsetBuffer = _currentBufferNum;
            if (time < 0.0)
            {
                // fld mTime / fadd 0.0001f / fstp dword: this one IS rounded to
                // float before it is stored as the due time.
                nextTimeBits = BitConverter.SingleToUInt32Bits(
                    StoredSum(_time, ImmediateFloor));
            }
        }
        else
        {
            if (time > MaximumTime)
            {
                return new RetailEventAdmission(
                    RetailEventPlacement.RejectedTooFarAhead, -1, -1, -1, nextTimeBits);
            }

            offsetBuffer = Float24Arithmetic
                ? (int)Math.Floor(RetailFloat24.Multiply(
                    RetailFloat24.Subtract(RetailFloat24.Subtract(time, _time), DelayBias), GameFrameRate))
                : DelayBufferOffset(_time, time);

            if (offsetBuffer >= OverflowBucketThreshold)
            {
                float nextTime = BitConverter.UInt32BitsToSingle(nextTimeBits);
                int addPoint = _overflowCursor;
                while (addPoint < _overflow.Count &&
                       DueTimeOf(_overflow[addPoint]) <= nextTime)
                {
                    addPoint++;
                }

                int overflowHandle =
                    Acquire(reuseHandle, eventNum, nextTimeBits, listener, data);
                if (overflowHandle < 0)
                {
                    return new RetailEventAdmission(
                        RetailEventPlacement.RejectedPoolExhausted, -1, -1, -1, nextTimeBits);
                }

                _overflow.Insert(addPoint, overflowHandle);
                _liveEvents++;
                return new RetailEventAdmission(
                    RetailEventPlacement.Overflow,
                    overflowHandle,
                    -1,
                    addPoint,
                    nextTimeBits);
            }

            placement = RetailEventPlacement.DelayedBucket;
            offsetBuffer = (_currentBufferNum + offsetBuffer) % EventListBuffers;
        }

        int handle = Acquire(reuseHandle, eventNum, nextTimeBits, listener, data);
        if (handle < 0)
        {
            return new RetailEventAdmission(
                RetailEventPlacement.RejectedPoolExhausted, -1, -1, -1, nextTimeBits);
        }

        _ring[LaneIndex(offsetBuffer, priority)].Add(handle);
        _liveEvents++;
        return new RetailEventAdmission(
            placement, handle, offsetBuffer, -1, nextTimeBits);
    }

    /// <summary>
    /// <c>GetNextFreeEvent</c> followed by <c>CScheduledEvent::Set</c> —
    /// how a caller builds the event it then hands to
    /// <see cref="AddOwnedEvent"/>. The manager does not count it until it
    /// files it. Returns -1 when the pool is empty.
    /// </summary>
    public int PrepareOwnedEvent(int eventNum, int listener, float time, int data = 0) =>
        Acquire(-1, eventNum, BitConverter.SingleToUInt32Bits(time), listener, data);

    /// <summary>
    /// Models the event's <c>CActiveReader&lt;CMonitor&gt; mToCall</c> losing its
    /// target (<c>activereader.h</c>), so <c>GetToCall()</c> returns NULL while
    /// the event stays filed. Retail reaches this whenever the listener dies
    /// between filing and flushing; it is the only way a filed event can have a
    /// null listener, since <see cref="AddEvent"/> refuses one outright.
    /// </summary>
    public void ClearListener(int handle)
    {
        _pool[handle].Listener = 0;
        TrackSlot(handle);
    }

    /// <summary>
    /// A deleted listener's readers all lose their target at once: every
    /// filed ring or overflow event of <paramref name="listener"/> goes
    /// through <see cref="ClearListener"/>.
    /// </summary>
    public void ClearListenerEvents(int listener)
    {
        if (listener == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(listener), "A filed event has no null listener to clear.");
        }

        foreach (List<int> lane in _ring)
        {
            foreach (int handle in lane)
            {
                if (_pool[handle].Listener == listener)
                {
                    ClearListener(handle);
                }
            }
        }

        foreach (int handle in _overflow)
        {
            if (_pool[handle].Listener == listener)
            {
                ClearListener(handle);
            }
        }
    }

    /// <summary>
    /// <c>CEventManager::AddEvent(CScheduledEvent*)</c> —
    /// <c>eventmanager.cpp:152-162</c>, <c>0x0044B310</c>. Re-files an owned
    /// event at <c>its own time + mTime</c>, always at
    /// <see cref="RetailEventPriority.StartOfFrame"/>, then frees it. The event
    /// number is re-read through <c>movsx</c>, so it round-trips as int16.
    /// </summary>
    public RetailEventAdmission AddOwnedEvent(int handle)
    {
        if (handle < 0)
        {
            return new RetailEventAdmission(
                RetailEventPlacement.RejectedNullListener, -1, -1, -1, 0);
        }

        ref PooledEvent owned = ref _pool[handle];
        RetailEventAdmission admission = AddEvent(
            owned.EventNum,
            owned.Listener,
            StoredSum(_time, BitConverter.UInt32BitsToSingle(owned.TimeBits)),
            RetailEventPriority.StartOfFrame,
            owned.Data);
        FreeEvent(handle);
        return admission;
    }

    /// <summary>
    /// <c>CEventManager::Update</c> — <c>eventmanager.cpp:284-288</c>,
    /// <c>0x0044B5C0</c>, with <c>AdvanceTime</c> inlined ahead of the call to
    /// <c>Flush</c>.
    /// </summary>
    public IReadOnlyList<RetailEventDispatch> Update(
        Action<RetailEventScheduler, RetailEventDispatch>? handler = null)
    {
        // Apply the reconstruction's existing Flush admission rule before
        // advancing time. A rejected nested/retry Update must not rotate the
        // queue or change the clock that the outer callback is still using.
        if (_flushing || _interruptedFlush)
            throw new InvalidOperationException("Cannot resume an active or interrupted Flush.");
        AdvanceTime();
        return Flush(handler);
    }

    /// <summary>
    /// <c>CEventManager::AdvanceTime</c> — <c>eventmanager.cpp:293-304</c>,
    /// inlined at <c>0x0044B5C3-0x0044B5F3</c>.
    /// </summary>
    /// <remarks>
    /// The frame counter increments first and the new <c>mTime</c> is the
    /// <b>new</b> count times <c>CLOCK_TICK</c>: <c>fild qword</c> over
    /// <c>{mFrameCount, 0}</c> — a zero-extended <c>ULONG</c>, so the conversion
    /// is exact — then one <c>fmul</c> by <c>0.05f</c> and one <c>fstp dword</c>.
    /// A single rounding to float, computed here as
    /// <c>(float)((double)frames * (double)0.05f)</c>. That is bit-identical to
    /// retail for every frame count below 2^24, where the exact product still
    /// fits a 53-bit significand and the precision control cannot matter.
    /// <c>mReadyToFlushBuffer</c> takes the <b>old</b> slot before the rotate.
    /// </remarks>
    public void AdvanceTime()
    {
        _frameCount++;
        _readyToFlushBuffer = _currentBufferNum;
        _time = TimeAtFrameCount(_frameCount);
        _currentBufferNum = (_currentBufferNum + 1) % EventListBuffers;
    }

    /// <summary>
    /// The stored clock word from <see cref="AdvanceTime"/>. Shared with
    /// Simulation's level clock while its mission dispatch is still separate
    /// from this event pool. This is multiplication by the stored 0.05f,
    /// not division by 20 and not repeated addition.
    /// </summary>
    internal static float TimeAtFrameCount(uint frameCount) =>
        (float)((double)frameCount * (double)ClockTick);

    // Relative and NEXT_FRAME additions have their own final F32 store.
    // Delay routing instead retains each PC24 result until floor's F64 argument.
    private float StoredSum(float left, float right) => Float24Arithmetic
        ? (float)RetailFloat24.Add(left, right)
        : RelativeDueTime(left, right);

    // Shared by the pool and the sparse Level100 ground-shutdown owner.
    // Preserve the scheduler's existing bounded arithmetic contract.
    internal static float RelativeDueTime(float now, float delay) =>
        (float)((double)now + delay);

    internal static bool IsImmediate(float now, float due) =>
        (double)now + ImmediateWindow >= due;

    internal static int DelayBufferOffset(float now, float due) =>
        (int)Math.Floor((((double)due - now) - DelayBias) * GameFrameRate);

    /// <summary>
    /// <c>CEventManager::Flush</c> — <c>eventmanager.cpp:311-411</c>,
    /// <c>0x0044B640</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Order, exactly: all three priority lanes of the ready slot in lane order
    /// and insertion order within a lane; then the overflow list from
    /// <c>mCurrentProcessOverflowEventNum</c> while the head's due time is
    /// <b>strictly</b> less than <c>mTime</c> (<c>test ah, 1 / je</c> at
    /// <c>0x0044B6D9</c> — an event due exactly on the frame boundary waits a
    /// frame). The overflow count is captured once after the ring callbacks,
    /// before any overflow callback (<c>0x0044B6BF</c>), and bounds that walk
    /// even if callbacks append entries. Cleanup follows. This is the restriction
    /// <c>eventmanager.h:38-41</c> documents: an overflow event is effectively
    /// last, whatever priority it was filed with.
    /// </para>
    /// <para>
    /// <c>SetReuse(FALSE)</c> happens before the callback and the overflow
    /// cursor advances before the callback, both matching retail's instruction
    /// order. <c>mTotalEventProcessedNum</c> increments for every visited event
    /// including ones whose listener is null. Cleanup frees only events the
    /// callback did not re-arm, but decrements <c>mNumEventsInEventManager</c>
    /// for all of them — so a callback that re-adds its own event through
    /// <c>re_use_event</c> keeps the handle and the count balances, which is
    /// how <c>CPanCamera::Update</c> (<c>Camera.cpp:392</c>) can re-post
    /// <c>UPDATE_CAMERA</c> every frame forever without draining the pool.
    /// </para>
    /// <para>
    /// The callback is optional and supplied by the owning world; the World110
    /// tree prefix now dispatches its collision listeners here. Retail iterates
    /// the ready slot through the set's own cursor, so
    /// an append made during the walk would be visited — the index walk here
    /// preserves that. With the clock unchanged inside callbacks, the only slot a callback
    /// can target immediately is <c>mCurrentBufferNum</c>, which
    /// <c>AdvanceTime</c> has already rotated past the slot being drained, and
    /// the two ring slots that would wrap onto it are both at or beyond the
    /// overflow threshold.
    /// </para>
    /// </remarks>
    public IReadOnlyList<RetailEventDispatch> Flush(
        Action<RetailEventScheduler, RetailEventDispatch>? handler = null)
    {
        if (_flushing || _interruptedFlush)
            throw new InvalidOperationException("Cannot resume an active or interrupted Flush.");
        _flushing = true;
        try { return FlushCore(handler); }
        catch { _interruptedFlush = true; throw; }
        finally { _flushing = false; }
    }

    private IReadOnlyList<RetailEventDispatch> FlushCore(
        Action<RetailEventScheduler, RetailEventDispatch>? handler)
    {
        uint previous = _totalProcessed;
        int ready = _readyToFlushBuffer;
        _overflowCursor = 0;
        _dispatched.Clear();

        for (int lane = 0; lane < PriorityLanes; lane++)
        {
            List<int> slot = _ring[LaneIndex(ready, (RetailEventPriority)lane)];
            for (int i = 0; i < slot.Count; i++)
            {
                int handle = slot[i];
                _pool[handle].Reuse = false;
                TrackSlot(handle);
                if (_pool[handle].Listener != 0)
                {
                    Dispatch(handler, handle, (RetailEventPriority)lane, fromOverflow: false);
                }

                _totalProcessed++;
            }
        }

        int overflowCount = _overflow.Count;
        while (overflowCount > _overflowCursor &&
               DueTimeOf(_overflow[_overflowCursor]) < _time)
        {
            int handle = _overflow[_overflowCursor];
            _pool[handle].Reuse = false;
            TrackSlot(handle);
            _overflowCursor++;
            if (_pool[handle].Listener != 0)
            {
                Dispatch(
                    handler, handle, RetailEventPriority.EndOfFrame, fromOverflow: true);
            }

            _totalProcessed++;
        }

        for (int lane = 0; lane < PriorityLanes; lane++)
        {
            List<int> slot = _ring[LaneIndex(ready, (RetailEventPriority)lane)];
            for (int i = 0; i < slot.Count; i++)
            {
                int handle = slot[i];
                if (!_pool[handle].Reuse)
                {
                    FreeEvent(handle);
                }

                _liveEvents--;
            }

            slot.Clear();
        }

        if (_overflowCursor > 0)
        {
            for (int i = _overflowCursor - 1; i >= 0; i--)
            {
                int handle = _overflow[i];
                if (!_pool[handle].Reuse)
                {
                    FreeEvent(handle);
                }

                _liveEvents--;
            }

            _overflow.RemoveRange(0, _overflowCursor);
        }

        _overflowCursor = 0;
        _processedThisUpdate = _totalProcessed - previous;
        return _dispatched;
    }

    /// <summary>The float32 bits stored in a live event's <c>mTime</c>.</summary>
    public uint DueTimeBitsOf(int handle) => _pool[handle].TimeBits;

    /// <summary>The int16 event number a live event carries.</summary>
    public short EventNumOf(int handle) => _pool[handle].EventNum;

    /// <summary>The actual owner identity bound to a live admission.</summary>
    public int ListenerOf(int handle) => _pool[handle].Listener;

    /// <summary><c>CScheduledEvent::GetReuse</c>.</summary>
    public bool ReuseOf(int handle) => _pool[handle].Reuse;

    /// <summary>Number of entries still on the free list. Walks the chain.</summary>
    public int FreeEventCount()
    {
        int count = 0;
        for (int cursor = _freeList; cursor >= 0; cursor = _pool[cursor].NextFree)
        {
            count++;
        }

        return count;
    }

    private void Dispatch(
        Action<RetailEventScheduler, RetailEventDispatch>? handler,
        int handle,
        RetailEventPriority priority,
        bool fromOverflow)
    {
        var dispatch = new RetailEventDispatch(
            handle,
            _pool[handle].EventNum,
            _pool[handle].Listener,
            _pool[handle].TimeBits,
            priority,
            fromOverflow);
        _dispatched.Add(dispatch);
        handler?.Invoke(this, dispatch);
    }

    /// <summary>
    /// <c>CEventManager::FreeEvent</c> — <c>eventmanager.cpp:129-135</c>, inlined
    /// at <c>0x0044B346</c> and twice inside <c>Flush</c>. LIFO: the freed entry
    /// becomes the head, so the next allocation returns it.
    /// </summary>
    public void FreeEvent(int handle)
    {
        _pool[handle].NextFree = _freeList;
        _pool[handle].Data = 0;
        _pool[handle].Listener = 0;
        _freeList = handle;
        TrackSlot(handle);
    }

    private int Acquire(
        int reuseHandle, int eventNum, uint timeBits, int listener, int data)
    {
        if (reuseHandle >= 0)
        {
            // eventmanager.cpp:265-273 / 0x0044B4CE and 0x0044B56A. The listener
            // is deliberately NOT reassigned on this arm, and a null data
            // argument leaves the existing reader in place.
            ref PooledEvent reused = ref _pool[reuseHandle];
            reused.TimeBits = timeBits;
            reused.EventNum = unchecked((short)eventNum);
            reused.Reuse = true;
            if (data != 0)
            {
                reused.Data = data;
            }

            TrackSlot(reuseHandle);
            return reuseHandle;
        }

        int handle = GetNextFreeEvent();
        if (handle < 0)
        {
            return -1;
        }

        // CScheduledEvent::Set — scheduledevent.cpp:17-24 / 0x004DE1F0.
        ref PooledEvent fresh = ref _pool[handle];
        fresh.EventNum = unchecked((short)eventNum);
        fresh.TimeBits = timeBits;
        fresh.Listener = listener;
        fresh.Data = data;
        fresh.Reuse = false;
        fresh.NextFree = -1;
        TrackSlot(handle);
        return handle;
    }

    private float DueTimeOf(int handle) =>
        BitConverter.UInt32BitsToSingle(_pool[handle].TimeBits);

    private static int LaneIndex(int buffer, RetailEventPriority priority) =>
        (buffer * PriorityLanes) + (int)priority;

    private struct PooledEvent
    {
        public int NextFree;
        public short EventNum;
        public int Listener;
        public int Data;
        public uint TimeBits;
        public bool Reuse;
    }
}
