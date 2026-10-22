// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/Monitor.h, src/Monitor.cpp (CMonitor, 004bac40 and 004bacb0), src/activereader.h and
// src/activereader.cpp (CGenericActiveReader, CActiveReader, 00401000), src/Event.h (CEvent), in Stuart's release
// (Monitor.cpp is reconstructed, the rest is his).

namespace OnslaughtRebuild.Parity.Events;

/// <summary>IListener: anything an event can be delivered to.</summary>
public interface IListener
{
    void HandleEvent(Event e);
}

/// <summary>
/// CMonitor: a listener that tells every active reader still watching it when it goes, so a scheduled event aimed at a
/// thing that has died finds no one to call. Shutdown and destruction (<see cref="Destroy"/>) both notify; a reader set
/// again afterwards registers afresh.
/// </summary>
public class Monitor : IListener
{
    private List<ActiveReader>? _deletionList;

    public virtual void HandleEvent(Event e)
    {
    }

    /// <summary>CMonitor::Shutdown: every reader forgets this monitor.</summary>
    public virtual void Shutdown() => NotifyReaders();

    /// <summary>~CMonitor: the same notification when the object is destroyed.</summary>
    public void Destroy() => NotifyReaders();

    /// <summary>mDeletionList's size: the readers watching this monitor.</summary>
    public int DeletionCount => _deletionList?.Count ?? 0;

    internal void AddDeletionEvent(ActiveReader reader) => (_deletionList ??= new List<ActiveReader>()).Add(reader);

    internal void RemoveDeletionEvent(ActiveReader reader) => _deletionList?.Remove(reader);

    private void NotifyReaders()
    {
        if (_deletionList is null)
        {
            return;
        }

        foreach (ActiveReader reader in _deletionList)
        {
            reader.ToReadDied();
        }

        _deletionList = null;
    }
}

/// <summary>CGenericActiveReader: a pointer to a monitor that becomes null when the monitor goes.</summary>
public class ActiveReader
{
    private Monitor? _toRead;

    protected Monitor? Target => _toRead;

    /// <summary>CGenericActiveReader::SetReader: leaves the old monitor's list, joins the new one's.</summary>
    public void SetReader(Monitor? toRead)
    {
        if (ReferenceEquals(toRead, _toRead))
        {
            return;
        }

        _toRead?.RemoveDeletionEvent(this);
        _toRead = toRead;
        _toRead?.AddDeletionEvent(this);
    }

    /// <summary>~CGenericActiveReader: leave the monitor's list.</summary>
    public void Release() => SetReader(null);

    internal void ToReadDied() => _toRead = null;
}

/// <summary>CActiveReader&lt;T&gt;.</summary>
public sealed class ActiveReader<T> : ActiveReader
    where T : Monitor
{
    public T? ToRead => (T?)Target;

    public void SetReader(T? toRead) => base.SetReader(toRead);
}

/// <summary>CEvent: an event number (stored as a short) and the monitor it is for.</summary>
public class Event
{
    private short _eventNum;

    public Event()
    {
    }

    public Event(int eventNum, Monitor? toCall = null)
    {
        SetNum(eventNum);
        ToCallReader.SetReader(toCall);
    }

    public int EventNum => _eventNum;

    public Monitor? ToCall => ToCallReader.ToRead;

    internal ActiveReader<Monitor> ToCallReader { get; } = new();

    public void SetNum(int eventNum) => _eventNum = unchecked((short)eventNum);

    public void SetToCall(Monitor? toCall) => ToCallReader.SetReader(toCall);
}
