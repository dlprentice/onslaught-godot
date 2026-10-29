// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/eventmanager.h, src/eventmanager.cpp (CEventManager: Init 0044b060, AddEvent 0044b2d0,
// 0044b310, 0044b370, Update 0044b5c0, AdvanceTime 0044b600, Flush 0044b640), src/scheduledevent.h and
// src/scheduledevent.cpp (CScheduledEvent::Set 004de1f0), in Stuart's release (scheduledevent.cpp is reconstructed).

using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Events;

/// <summary>CScheduledEvent: an event with the time it is due and an optional monitor as data.</summary>
public sealed class ScheduledEvent : Event
{
    /// <summary>mBeingReused: set when a handler re-adds the event that is calling it, so Flush does not free it.</summary>
    public bool BeingReused { get; internal set; }

    public float Time { get; internal set; }

    public Monitor? Data => DataReader.ToRead;

    internal ActiveReader<Monitor> DataReader { get; } = new();

    internal ScheduledEvent? NextFree { get; set; }

    /// <summary>CScheduledEvent::Set.</summary>
    internal void Set(int eventNum, float time, Monitor? toCall, Monitor? data)
    {
        SetNum(eventNum);
        Time = time;
        SetToCall(toCall);
        BeingReused = false;
        DataReader.SetReader(data);
    }

    public void SetData(Monitor? data) => DataReader.SetReader(data);
}

/// <summary>When in its frame an event runs: the three priority lists of a frame's buffer.</summary>
public enum EventPriority
{
    StartOfFrame = 0,
    MiddleOfFrame = 1,
    EndOfFrame = 2,
}

/// <summary>
/// CEventManager: the game's scheduler. Events due within the next ten seconds go into one of 200 circular per-tick
/// buffers (one list per priority, appended in order); later ones go into an overflow list kept in time order. Each
/// tick AdvanceTime moves the clock and the buffer, and Flush runs the ready buffer's three lists in order, then the
/// overflow events due strictly before the new time. Every float operation follows the compiled code: dword
/// operands, results at 24 bits, which C# float reproduces.
/// </summary>
public sealed class EventManager
{
    /// <summary>MAX_NUM_EVENTS.</summary>
    public const int MaxEvents = 20000;

    /// <summary>NUM_EVENT_LIST_BUFFERS: 10 seconds at 20 ticks a second.</summary>
    public const int BufferCount = 200;

    /// <summary>NUM_PRIORITY.</summary>
    public const int PriorityCount = 3;

    /// <summary>CLOCK_TICK (thing.h).</summary>
    public const float ClockTick = 0.05f;

    /// <summary>GAME_FR (thing.h).</summary>
    public const float GameFrameRate = 20.0f;

    /// <summary>NEXT_FRAME: a negative time means "next frame".</summary>
    public const float NextFrame = -1.0f;

    private readonly List<ScheduledEvent>[,] _buffers = new List<ScheduledEvent>[BufferCount, PriorityCount];
    private List<ScheduledEvent> _overflow = new();
    private ScheduledEvent[] _pool = [];
    private ScheduledEvent? _freeList;
    private int _currentProcessOverflowEventNum;

    public EventManager()
    {
        for (int i = 0; i < BufferCount; i++)
        {
            for (int j = 0; j < PriorityCount; j++)
            {
                _buffers[i, j] = new List<ScheduledEvent>();
            }
        }
    }

    /// <summary>mValid: false until Init.</summary>
    public bool IsValid { get; private set; }

    /// <summary>mTime: game seconds since Init, the EVT probe.</summary>
    public float Time { get; private set; }

    /// <summary>mFrameCount: ticks since Init.</summary>
    public int FrameCount { get; private set; }

    /// <summary>mCurrentBufferNum: the buffer next-frame events go into.</summary>
    public int CurrentBufferNum { get; private set; }

    /// <summary>mReadyToFlushBuffer: the buffer the next Flush runs.</summary>
    public int ReadyToFlushBuffer { get; private set; }

    /// <summary>mNumEventsInEventManager.</summary>
    public int TotalEvents { get; private set; }

    /// <summary>mTotalEventProcessedNum: every event run (or skipped for want of a target) since Init.</summary>
    public uint TotalEventsProcessed { get; private set; }

    /// <summary>mEventsProcessedThisUpdate.</summary>
    public uint EventsProcessedInLastUpdate { get; private set; }

    /// <summary>
    /// When set, every core AddEvent call ('A': the absolute time it received, as bits, and the priority; a call that
    /// schedules nothing names why in <see cref="EventLogEntry.Dropped"/>) and every dispatch to a live target ('F')
    /// is appended, for comparison with a retail or native-port event trace.
    /// </summary>
    public List<EventLogEntry>? Log { get; set; }

    /// <summary>Events in the overflow list, in time order.</summary>
    public IReadOnlyList<ScheduledEvent> Overflow => _overflow;

    /// <summary>The events waiting in one buffer's priority list, in run order.</summary>
    public IReadOnlyList<ScheduledEvent> Buffer(int buffer, EventPriority priority) => _buffers[buffer, (int)priority];

    /// <summary>CEventManager::Init: clock and counters to zero, a fresh pool with its free list in index order.</summary>
    public void Init()
    {
        Time = 0.0f;
        CurrentBufferNum = 0;
        FrameCount = 0;
        TotalEvents = 0;
        EventsProcessedInLastUpdate = 0;
        TotalEventsProcessed = 0;
        ReadyToFlushBuffer = 0;
        _currentProcessOverflowEventNum = 0;
        _overflow = new List<ScheduledEvent>();
        _pool = new ScheduledEvent[MaxEvents];
        for (int i = 0; i < MaxEvents; i++)
        {
            _pool[i] = new ScheduledEvent();
        }

        for (int i = 0; i < MaxEvents - 1; i++)
        {
            _pool[i].NextFree = _pool[i + 1];
        }

        _pool[MaxEvents - 1].NextFree = null;
        _freeList = _pool[0];
        IsValid = true;
    }

    /// <summary>CEventManager::Shutdown: buffers and overflow emptied, the pool released.</summary>
    public void Shutdown()
    {
        IsValid = false;
        for (int j = 0; j < PriorityCount; j++)
        {
            for (int i = 0; i < BufferCount; i++)
            {
                _buffers[i, j].Clear();
            }
        }

        _overflow.Clear();
        _pool = [];
        _freeList = null;
    }

    /// <summary>AddEvent(time_from_now, ...): due <paramref name="timeFromNow"/> seconds after the current time.</summary>
    public void AddEventIn(float timeFromNow, int eventNum, Monitor? toCall,
        EventPriority priority = EventPriority.StartOfFrame, Monitor? data = null, ScheduledEvent? reuseEvent = null) =>
        AddEvent(eventNum, toCall, timeFromNow + Time, priority, data, reuseEvent);

    /// <summary>AddEvent(CScheduledEvent*): schedules a copy due at its time from now, then frees it.</summary>
    public void AddEvent(ScheduledEvent? e)
    {
        if (e is null)
        {
            return;
        }

        AddEvent(e.EventNum, e.ToCall, e.Time + Time, EventPriority.StartOfFrame, e.Data, null);
        FreeEvent(e);
    }

    /// <summary>
    /// AddEvent(event_num, to_call, time, ...), retail's argument order: <paramref name="time"/> is absolute game time.
    /// An event due by the next tick (time &lt;= mTime + 0.051) goes into the current buffer; a negative time means the
    /// next frame (mTime + 0.0001). Otherwise its buffer is floor(((time - mTime) - 0.001) * 20) ticks ahead, or the
    /// overflow list (in time order) from 198 on; anything past 1,000,000 s is dropped. A re-used event keeps its
    /// target and takes the new data only when it is non-null, as retail does.
    /// </summary>
    public void AddEvent(int eventNum, Monitor? toCall, float time,
        EventPriority priority = EventPriority.StartOfFrame, Monitor? data = null, ScheduledEvent? reuseEvent = null)
    {
        if (!IsValid || toCall is null)
        {
            LogAdd(eventNum, toCall, time, priority, !IsValid ? "invalid" : "no target");
            return; // retail logs "FATAL ERROR: Trying to add an event when event manager was invalid" for the first
        }

        float nextTime = time;
        int offsetBuffer;
        if (time <= Time + 0.051f)
        {
            offsetBuffer = CurrentBufferNum;
            if (time < 0.0)
            {
                nextTime = Time + 0.0001f;
            }
        }
        else
        {
            if (time > 1000000.0f)
            {
                LogAdd(eventNum, toCall, time, priority, "past 1e6");
                return;
            }

            // fld time; a float subtract of mTime; a float subtract of 0.001f;
            // a float multiply by 20.0f; fstp qword; call floor; fistp qword.
            float delay = (time - Time - 0.001f) * GameFrameRate;
            offsetBuffer = X87.Fist(Math.Floor(delay));
            if (offsetBuffer >= BufferCount - 2)
            {
                int addPoint = _currentProcessOverflowEventNum;
                while (addPoint < _overflow.Count && _overflow[addPoint].Time <= nextTime)
                {
                    addPoint++;
                }

                ScheduledEvent? overflowEvent = Prepare(reuseEvent, eventNum, nextTime, toCall, data);
                if (overflowEvent is null)
                {
                    LogAdd(eventNum, toCall, time, priority, "pool empty");
                    return;
                }

                LogAdd(eventNum, toCall, time, priority, null);

                _overflow.Insert(addPoint, overflowEvent);
                TotalEvents++;
                return;
            }

            offsetBuffer += CurrentBufferNum;
            offsetBuffer %= BufferCount;
        }

        ScheduledEvent? e = Prepare(reuseEvent, eventNum, nextTime, toCall, data);
        if (e is null)
        {
            LogAdd(eventNum, toCall, time, priority, "pool empty");
            return;
        }

        LogAdd(eventNum, toCall, time, priority, null);

        _buffers[offsetBuffer, (int)priority].Add(e);
        TotalEvents++;
    }

    private void LogAdd(int eventNum, Monitor? toCall, float time, EventPriority priority, string? dropped) =>
        Log?.Add(new EventLogEntry('A', eventNum, toCall?.GetType().Name, BitConverter.SingleToUInt32Bits(time),
            (int)priority, dropped));

    /// <summary>
    /// A dispatch: retail calls HandleEvent only when the target is still alive. The log records the priority list it
    /// came from (the overflow list's as 0).
    /// </summary>
    private void Dispatch(ScheduledEvent e, Monitor? toCall, int priority)
    {
        if (toCall is not null)
        {
            Log?.Add(new EventLogEntry('F', e.EventNum, toCall.GetType().Name, 0, priority, null));
            toCall.HandleEvent(e);
        }
    }

    /// <summary>CEventManager::Update: AdvanceTime, then Flush.</summary>
    public void Update()
    {
        AdvanceTime();
        Flush();
    }

    /// <summary>
    /// CEventManager::AdvanceTime: <c>mTime = ++mFrameCount * CLOCK_TICK</c> (fild qword, fmul dword: one 24-bit
    /// rounding), and the buffer moves on.
    /// </summary>
    public void AdvanceTime()
    {
        FrameCount++;
        Time = (float)X87.Mul(FrameCount, ClockTick);
        ReadyToFlushBuffer = CurrentBufferNum;
        CurrentBufferNum = (CurrentBufferNum + 1) % BufferCount;
    }

    /// <summary>
    /// CEventManager::Flush: runs the ready buffer's start, middle and end lists in order, then the overflow events due
    /// strictly before mTime; then frees what it ran (except events a handler re-used) and empties them.
    /// </summary>
    public void Flush()
    {
        uint previous = TotalEventsProcessed;
        int buffer = ReadyToFlushBuffer;
        _currentProcessOverflowEventNum = 0;

        for (int j = 0; j < PriorityCount; j++)
        {
            List<ScheduledEvent> list = _buffers[buffer, j];
            for (int i = 0; i < list.Count; i++)
            {
                ScheduledEvent e = list[i];
                Monitor? toCall = e.ToCall;
                e.BeingReused = false;
                Dispatch(e, toCall, j);
                TotalEventsProcessed++;
            }
        }

        while (_overflow.Count > _currentProcessOverflowEventNum
               && _overflow[_currentProcessOverflowEventNum].Time < Time)
        {
            ScheduledEvent e = _overflow[_currentProcessOverflowEventNum];
            Monitor? toCall = e.ToCall;
            e.BeingReused = false;
            _currentProcessOverflowEventNum++;
            Dispatch(e, toCall, 0);
            TotalEventsProcessed++;
        }

        for (int j = 0; j < PriorityCount; j++)
        {
            List<ScheduledEvent> list = _buffers[buffer, j];
            foreach (ScheduledEvent e in list)
            {
                if (!e.BeingReused)
                {
                    FreeEvent(e);
                }

                TotalEvents--;
            }

            list.Clear();
        }

        if (_currentProcessOverflowEventNum > 0)
        {
            for (int i = _currentProcessOverflowEventNum - 1; i >= 0; i--)
            {
                if (!_overflow[i].BeingReused)
                {
                    FreeEvent(_overflow[i]);
                }

                TotalEvents--;
            }

            _overflow.RemoveRange(0, _currentProcessOverflowEventNum);
        }

        _currentProcessOverflowEventNum = 0;
        EventsProcessedInLastUpdate = TotalEventsProcessed - previous;
    }

    /// <summary>CEventManager::GetNextFreeEvent: the head of the free list, or null when the pool is exhausted.</summary>
    public ScheduledEvent? GetNextFreeEvent()
    {
        ScheduledEvent? e = _freeList;
        if (_freeList is not null)
        {
            _freeList = _freeList.NextFree;
        }

        return e; // retail logs "FATAL ERROR:  Run out of free scheduled events!!" when null
    }

    private void FreeEvent(ScheduledEvent e)
    {
        e.NextFree = _freeList;
        e.SetData(null);
        e.SetToCall(null);
        _freeList = e;
    }

    private ScheduledEvent? Prepare(ScheduledEvent? reuse, int eventNum, float time, Monitor? toCall, Monitor? data)
    {
        if (reuse is null)
        {
            ScheduledEvent? e = GetNextFreeEvent();
            e?.Set(eventNum, time, toCall, data);
            return e;
        }

        reuse.Time = time;
        reuse.SetNum(eventNum);
        reuse.BeingReused = true;
        if (data is not null)
        {
            reuse.SetData(data);
        }

        return reuse;
    }
}

/// <summary>
/// One line of <see cref="EventManager.Log"/>: 'A' for an AddEvent call (the absolute time it received as float bits,
/// the priority, and why nothing was scheduled, if so) or 'F' for a dispatch (with the priority list it came from).
/// </summary>
public readonly record struct EventLogEntry(char Kind, int EventNum, string? Target, uint TimeBits, int Priority, string? Dropped)
{
    public override string ToString() => Kind == 'A'
        ? $"A ev {EventNum} {Target ?? "(null)"} t {TimeBits:x8} p {Priority}{(Dropped is null ? "" : $" dropped: {Dropped}")}"
        : $"F ev {EventNum} {Target}";
}
