// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/MissionScript/EventFunction.cpp (~CEventFunction 0052f9a0, Clone 0052fbb0, Execute
// 0052fda0), ScriptEventNB.cpp (SScriptEvent, ~SScriptEvent 005387b0; CScriptEventNB::Init 00538860, Shutdown 005388d0,
// RegisterEventListener 00538960, PostEvent 00538b70, HandleEvent 00538c70) and IScript.cpp (CPostEventData,
// ~CPostEventData 005386d0), reconstructed from the retail executable; not in Stuart's release.

using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Script;

/// <summary>SEventParam: one event name a handler listens for, and the named event once IScript::Init binds it.</summary>
public sealed class EventParam(ScriptData name)
{
    /// <summary>mName: the copy's string symbol value itself (not a clone).</summary>
    public ScriptData? Name { get; private set; } = name;

    public ScriptEvent? Event { get; set; }

    /// <summary>~SEventParam.</summary>
    public void Delete()
    {
        Name = null;
        Event = null;
    }
}

/// <summary>
/// CEventFunction: a script's handler for named events. When one of its events is posted it runs from its entry with
/// one bool per listened-for event: whether that event has ever fired.
/// </summary>
public sealed class ScriptEventFunction(int entryPc, ScriptCode code, SPtrSetPool pool) : Monitor
{
    public int EntryPc { get; } = entryPc;

    public SPtrSet<EventParam> Params { get; } = new(pool);

    public ScriptCode Code { get; } = code;

    /// <summary>AddParam (inline): a new SEventParam appended.</summary>
    public void AddParam(ScriptData name) => Params.Append(new EventParam(name));

    /// <summary>CEventFunction::Execute.</summary>
    public void Execute(ScriptRuntime runtime)
    {
        var args = new List<ScriptData>();
        var it = new ListIterator<EventParam>(Params);
        for (EventParam? param = it.First(); param is not null; param = it.Next())
        {
            ScriptEvent e = param.Event ?? throw new InvalidOperationException("event function run before IScript::Init bound it");
            args.Add(new BoolData(e.Fired));
        }

        runtime.Vm.CallFunction(Code, EntryPc, args);
    }

    /// <summary>~CEventFunction: the parameters are deleted, then the monitor's readers are told.</summary>
    public void Delete()
    {
        for (EventParam? p = Params.First(); p is not null; p = Params.Next())
        {
            p.Delete();
        }

        Params.RemoveAll();
        Destroy();
    }
}

/// <summary>SScriptEvent: a named event, the functions listening (as readers) and whether it has ever fired.</summary>
public sealed class ScriptEvent
{
    public ScriptEvent(ScriptData name, ScriptEventFunction function, SPtrSetPool pool)
    {
        Name = name.Clone();
        Functions = new SPtrSet<ActiveReader<ScriptEventFunction>>(pool);
        AddFunction(function);
        Fired = false;
    }

    public ScriptData? Name { get; private set; }

    public SPtrSet<ActiveReader<ScriptEventFunction>> Functions { get; }

    /// <summary>mFired: set by every post of the name and never cleared.</summary>
    public bool Fired { get; set; }

    public void AddFunction(ScriptEventFunction function)
    {
        var reader = new ActiveReader<ScriptEventFunction>();
        reader.SetReader(function);
        Functions.Append(reader);
    }

    /// <summary>~SScriptEvent.</summary>
    public void Delete()
    {
        Name?.Delete();
        Name = null;
        for (ActiveReader<ScriptEventFunction>? r = Functions.First(); r is not null; r = Functions.Next())
        {
            r.Release();
        }

        Functions.RemoveAll();
    }
}

/// <summary>CPostEventData: a posted event's name, delivered next frame (the scheduled event's data).</summary>
public sealed class PostEventData(ScriptData name) : Monitor
{
    public ScriptData? Name { get; private set; } = name;

    /// <summary>~CPostEventData.</summary>
    public void Delete()
    {
        Name?.Delete();
        Name = null;
        Destroy();
    }
}

/// <summary>
/// CScriptEventNB (SCRIPT_EVENT_NB): the named events scripts post and listen for. A post marks every event of that
/// name fired and runs its live functions, in the order they registered.
/// </summary>
public sealed class ScriptEventNB(ScriptRuntime runtime) : Monitor
{
    /// <summary>SCRIPT_EVENT_NB_POST: IScript::PostEvent's message.</summary>
    public const int PostEventNum = 2000;

    /// <summary>mEvents (null until Init; Init makes a new set without freeing an old one).</summary>
    public SPtrSet<ScriptEvent>? Events { get; private set; }

    /// <summary>CScriptEventNB::Init (CGame's level start).</summary>
    public void Init() => Events = new SPtrSet<ScriptEvent>(runtime.Context.Pool);

    /// <summary>CScriptEventNB::Shutdown (CGame's level end).</summary>
    public override void Shutdown()
    {
        base.Shutdown();
        if (Events is not null)
        {
            for (ScriptEvent? e = Events.First(); e is not null; e = Events.Next())
            {
                e.Delete();
            }

            Events.RemoveAll();
        }

        Events = null;
    }

    /// <summary>
    /// RegisterEventListener: the function joins the last event with the parameter's name, or a new event appended to
    /// the list.
    /// </summary>
    public ScriptEvent RegisterEventListener(EventParam param, ScriptEventFunction function)
    {
        SPtrSet<ScriptEvent> events = Events ?? throw new InvalidOperationException("SCRIPT_EVENT_NB used before Init");
        ScriptEvent? found = null;
        for (ScriptEvent? e = events.First(); e is not null; e = events.Next())
        {
            if (ScriptData.SameString(param.Name!.GetString(), e.Name!.GetString()))
            {
                found = e;
            }
        }

        if (found is null)
        {
            found = new ScriptEvent(param.Name!, function, runtime.Context.Pool);
            events.Append(found);
        }
        else
        {
            found.AddFunction(function);
        }

        return found;
    }

    /// <summary>CScriptEventNB::PostEvent: now, by name (CGame posts "game playing" this way).</summary>
    public void PostEvent(byte[] name)
    {
        SPtrSet<ScriptEvent> events = Events ?? throw new InvalidOperationException("SCRIPT_EVENT_NB used before Init");
        if (events.Size == 0 && !ScriptData.SameString("game playing"u8.ToArray(), name))
        {
            runtime.LogMessage($"Warning: No listeners for posted event '{System.Text.Encoding.Latin1.GetString(name)}'");
        }

        var it = new ListIterator<ScriptEvent>(events);
        for (ScriptEvent? e = it.First(); e is not null; e = it.Next())
        {
            if (ScriptData.SameString(e.Name!.GetString(), name))
            {
                e.Fired = true;
                var functions = new ListIterator<ActiveReader<ScriptEventFunction>>(e.Functions);
                for (ActiveReader<ScriptEventFunction>? reader = functions.First(); reader is not null; reader = functions.Next())
                {
                    reader.ToRead?.Execute(runtime);
                }
            }
        }
    }

    /// <summary>CScriptEventNB::HandleEvent: IScript::PostEvent's message arrives; the same walk, then the data is deleted.</summary>
    public override void HandleEvent(Event e)
    {
        if (e.EventNum != PostEventNum)
        {
            return;
        }

        var data = (PostEventData)((ScheduledEvent)e).Data!;
        PostEvent(data.Name!.GetString()!);
        data.Delete();
    }
}
