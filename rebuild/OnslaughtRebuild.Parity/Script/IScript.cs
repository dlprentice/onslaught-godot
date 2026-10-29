// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/MissionScript/IScript.cpp (IScript 005333b0, ~IScript 00533450, Init 00533500, Ready
// 005335a0, Arrived 005335d0, StartedDying 00533660, Hit 00533690, Died 005337e0, Shutdown 00533810,
// FinishedPlayingAnim 00533840, HandleEvent 005385e0, UpdateWaypointFollowing 00538470) and src/thing.cpp
// (CComplexThing::SetScript 004f4230, the clone and the INIT_SCRIPT event), reconstructed from the retail executable;
// not in Stuart's release.

using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Script;

/// <summary>The script events, by the object code's event-table index.</summary>
public static class ScriptEventIndex
{
    public const int Init = 0;
    public const int Arrived = 1;
    public const int Timer = 2;
    public const int Died = 3;
    public const int Hit = 4;
    public const int StartedDying = 5;
    public const int Ready = 6;
    public const int Shutdown = 7;
}

/// <summary>
/// IScript: a thing's mission script. It runs its own copy of the world's code on the shared machine; the thing's
/// notifications raise the script's events, and the natives (IScript.Natives.cs) are what the code calls.
/// </summary>
public sealed partial class IScript : Monitor
{
    /// <summary>SCRIPT_MESSAGE_FOLLOW_WAYPOINT: UpdateWaypointFollowing, every frame while following.</summary>
    public const int MessageFollowWaypoint = 2000;

    /// <summary>SCRIPT_MESSAGE_RESUME: a Wait native's saved machine carries on (the event's data).</summary>
    public const int MessageResume = 2001;

    /// <summary>SCRIPT_MESSAGE_TIMER: SetTimer's timer went off.</summary>
    public const int MessageTimer = 2002;

    private readonly ScriptRuntime _runtime;

    /// <summary>IScript::IScript: the thing and its code (the code learns its script).</summary>
    public IScript(ComplexThing thing, ScriptCode code, ThingContext context)
    {
        _runtime = ScriptRuntime.Of(context);
        DefaultThing = thing;
        Code = code;
        code.Script = this;
        Waypoint = null;
        ArrivedValue = 0;
        FollowingWaypoints = false;
        WaitingForWaypoints = false;
        Thing = thing;
        AnimVm = null;
        SavedVms = new SPtrSet<ScriptVm>(context.Pool);
    }

    /// <summary>
    /// CComplexThing::SetScript's part: a copy of the world's code (CWorld::GetCopyOfScript, CMissionScriptObjectCode::
    /// Clone) and the script made on it. The caller deletes the old script first and schedules INIT_SCRIPT for the next
    /// frame.
    /// </summary>
    public static IScript Create(ComplexThing thing, ScriptObjectCode stored, ThingContext context)
    {
        ScriptRuntime runtime = ScriptRuntime.Of(context);
        ScriptCode code = ScriptCode.Clone(stored, context.Pool, runtime.LogMessage);
        return new IScript(thing, code, context);
    }

    public ScriptRuntime Runtime => _runtime;

    /// <summary>mDefaultThing: the script's own thing.</summary>
    public ComplexThing DefaultThing { get; }

    /// <summary>mCode (null once deleted).</summary>
    public ScriptCode? Code { get; private set; }

    /// <summary>mThing: the thing a native acts on; POINTER sets it and each call puts it back.</summary>
    public Thing? Thing { get; set; }

    /// <summary>mWaypoint: the point the thing is heading for.</summary>
    public Waypoint? Waypoint { get; private set; }

    /// <summary>mFollowingWaypoints.</summary>
    public bool FollowingWaypoints { get; private set; }

    /// <summary>mWaitingForWaypoints: FollowWaypointWait's script resumes at the path's end.</summary>
    public bool WaitingForWaypoints { get; private set; }

    /// <summary>mWaypointVM.</summary>
    public ScriptVm? WaypointVm { get; private set; }

    /// <summary>mArrivedValue: Arrived's argument at the path's end (FollowWaypoint's second argument).</summary>
    public int ArrivedValue { get; private set; }

    /// <summary>mSavedVMs: the machines the Wait natives suspended, in order.</summary>
    public SPtrSet<ScriptVm> SavedVms { get; }

    /// <summary>mAnimVM: the machine PlayAnimationWait suspended.</summary>
    public ScriptVm? AnimVm { get; private set; }

    /// <summary>The script has been deleted (~IScript).</summary>
    public bool Deleted { get; private set; }

    private ScriptVm Vm => _runtime.Vm;

    private bool IsLevelLost => _runtime.Context.Game?.State == GameState.LevelLost;

    /// <summary>Init: the init event, then each event function listens for its named events.</summary>
    public void Init()
    {
        if (IsLevelLost)
        {
            Vm.Clean();
        }
        else
        {
            Vm.CallEvent(Code!, ScriptEventIndex.Init, []);
        }

        SPtrSet<ScriptEventFunction> functions = Code!.EventFunctions!;
        for (ScriptEventFunction? function = functions.First(); function is not null; function = functions.Next())
        {
            var it = new ListIterator<EventParam>(function.Params);
            for (EventParam? param = it.First(); param is not null; param = it.Next())
            {
                param.Event = _runtime.EventNB.RegisterEventListener(param, function);
            }
        }
    }

    /// <summary>Ready (no argument is passed).</summary>
    public void Ready() => Notify(ScriptEventIndex.Ready);

    /// <summary>Arrived: the end of a FollowWaypoint path, with its value; not while the machine runs.</summary>
    public void Arrived(int value)
    {
        if (Vm.Running || IsLevelLost)
        {
            return;
        }

        ScriptData arg = new IntData(value);
        _runtime.EventArg = arg;
        if (IsLevelLost)
        {
            arg.Delete();
            Vm.Clean();
        }
        else
        {
            Vm.CallEvent(Code!, ScriptEventIndex.Arrived, [arg]);
        }
    }

    /// <summary>StartedDying (CComplexThing::StartDieProcess and the unit and feature overrides).</summary>
    public void StartedDying() => Notify(ScriptEventIndex.StartedDying);

    /// <summary>Hit: something complex hit the thing; not while the machine runs.</summary>
    public void Hit(ComplexThing other)
    {
        if (Vm.Running || IsLevelLost)
        {
            return;
        }

        ScriptData arg = new ThingPtrData(other);
        _runtime.EventArg = arg;
        if (IsLevelLost)
        {
            arg.Delete();
            Vm.Clean();
        }
        else
        {
            Vm.CallEvent(Code!, ScriptEventIndex.Hit, [arg]);
        }
    }

    /// <summary>Died (CComplexThing::AddShutdownEvent and the overrides; the caller then deletes the script).</summary>
    public void Died() => Notify(ScriptEventIndex.Died);

    /// <summary>
    /// IScript::Shutdown (CComplexThing's SHUTDOWN event, while the game is at most playing): the shutdown event only. It
    /// does not a direct call to CMonitor::Shutdown, so readers of the script are not told.
    /// </summary>
    public override void Shutdown() => Notify(ScriptEventIndex.Shutdown);

    private void Notify(int scriptEvent)
    {
        if (IsLevelLost)
        {
            Vm.Clean();
        }
        else
        {
            Vm.CallEvent(Code!, scriptEvent, []);
        }
    }

    /// <summary>FinishedPlayingAnim: PlayAnimationWait's machine carries on.</summary>
    public void FinishedPlayingAnim()
    {
        if (AnimVm is null)
        {
            return;
        }

        ScriptVm saved = AnimVm;
        AnimVm = null;
        Resume(saved);
    }

    /// <summary>HandleEvent: waypoint following, a resume and the timer.</summary>
    public override void HandleEvent(Event e)
    {
        switch (e.EventNum)
        {
            case MessageFollowWaypoint:
                UpdateWaypointFollowing();
                break;
            case MessageResume:
                if (((ScheduledEvent)e).Data is ScriptVm vm)
                {
                    Resume(vm);
                }

                break;
            case MessageTimer:
                if (!Vm.Running)
                {
                    Notify(ScriptEventIndex.Timer);
                }

                break;
        }
    }

    /// <summary>
    /// A saved machine carries on: MISSION_SCRIPT_VM = *vm (its values move across), the copy leaves the saved set and
    /// is deleted, then the machine runs from the saved program counter.
    /// </summary>
    private void Resume(ScriptVm vm)
    {
        Vm.Restore(vm);
        SavedVms.Remove(vm);
        DeleteVm(vm);
        int pc = Vm.Pc;
        if (IsLevelLost)
        {
            Vm.Clean();
        }
        else
        {
            Vm.GotoInstruction(pc);
        }
    }

    /// <summary>delete vm: its stack's values (~CDataStack), then its readers (~CMonitor).</summary>
    private static void DeleteVm(ScriptVm vm)
    {
        vm.Stack.Clear();
        vm.Destroy();
    }

    /// <summary>Keeps a copy of the running machine and stops it (Pause, the Wait natives).</summary>
    internal ScriptVm SuspendRunningVm()
    {
        var vm = new ScriptVm(Vm);
        SavedVms.Append(vm);
        Vm.Stop = true;
        return vm;
    }

    /// <summary>
    /// UpdateWaypointFollowing: once within the thing's radius of the point (in the XY plane), on to the next point;
    /// at the path's end the thing stops and FollowWaypointWait's machine resumes, or Arrived runs. Otherwise again next
    /// frame. Retail 00538470: dx = wx - tx, dy = wy - ty, fsqrt(dy*dy + dx*dx), every operation at 24 bits.
    /// </summary>
    public void UpdateWaypointFollowing()
    {
        FVector w = Waypoint!.Pos;
        FVector t = DefaultThing.Pos;
        float dx = w.X - t.X;
        float dy = w.Y - t.Y;
        float dist = (float)X87.Sqrt((dy * dy) + (dx * dx));

        float radius = 2.0f;
        if (DefaultThing.IsA(ThingType.Unit))
        {
            radius = ScriptThingCalls.Unknown94((Unit)DefaultThing);
        }
        else if (DefaultThing.IsA(ThingType.Unknown20000000))
        {
            radius = 4.0f;
        }

        if (dist < radius)
        {
            var next = (Waypoint?)Waypoint.Next.ToRead;
            if (ReferenceEquals(next, Waypoint))
            {
                _runtime.LogMessage("ERROR: Waypoint points to previous");
                Waypoint = null;
            }
            else
            {
                Waypoint = next;
            }

            if (Waypoint is null)
            {
                FollowingWaypoints = false;
                ScriptThingCalls.Stop(_runtime, DefaultThing);
                if (WaitingForWaypoints)
                {
                    WaitingForWaypoints = false;
                    ScriptVm saved = WaypointVm!;
                    WaypointVm = null;
                    Resume(saved);
                    return;
                }

                Arrived(ArrivedValue);
                return;
            }

            DefaultThing.GoToPoint(Waypoint.Pos);
        }

        _runtime.Context.Events.AddEvent(MessageFollowWaypoint, this, EventManager.NextFrame);
    }

    /// <summary>
    /// ~IScript: the code (its event functions and values), then the saved machines and their values, then the
    /// script's own readers (scheduled resumes and waypoint updates find no one).
    /// </summary>
    public void Delete()
    {
        if (Deleted)
        {
            return;
        }

        Deleted = true;
        Code?.Delete();
        Code = null;
        for (ScriptVm? vm = SavedVms.First(); vm is not null; vm = SavedVms.Next())
        {
            DeleteVm(vm);
        }

        SavedVms.RemoveAll();
        Destroy();
    }
}
