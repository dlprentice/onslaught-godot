// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/thing.h and src/thing.cpp (CThing: the constructor 004f33e0, Init 004f34a0, Shutdown
// 004f3600, HandleEvent 004f3730, AddShutdownEvent 004f3760, StartDieProcess 004f37a0, StickToGround 004f3c50, MoveTo
// 004f3cb0, Teleport 004f3ce0; CComplexThing: the constructor 004f3e10, Init 004f3fd0, SetName 004f4120, Shutdown
// 004f41b0, SetScript 004f4230, HandleEvent 004f4300, AddShutdownEvent 004f43d0, StartDieProcess 004f4430), in
// Stuart's release.

using System.Text;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Script;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>EThingEvent.</summary>
public static class ThingEvent
{
    public const int Shutdown = 2000;
    public const int InitScript = 2001;
    public const int StartDieProcess = 2002;
    public const int ReadyScript = 2003;
}

/// <summary>THING_FLAGS.</summary>
[Flags]
public enum ThingFlags : short
{
    None = 0,
    DeclaredShutdown = 1,
    InMapWho = 2,
    Dying = 4,
    DontRender = 8,
    Invisible = 16,
    MarkedObjective = 32,
    IsBigThing = 64,
    Slide = 128,
    RemovedUnitType = 256,
}

/// <summary>
/// CThing: anything in the world. Each subclass's Init sets its fields and calls its base's Init, as retail's
/// SUPERTYPE::Init chain does, so draws and event schedules happen in retail's order.
/// </summary>
public abstract class Thing : Monitor
{
    protected Thing(ThingContext context, int classId)
    {
        Context = context;
        ClassId = classId;
        ThingTypeBits = ThingType.None;
        Flags = ThingFlags.InMapWho;
        ThingNumber = unchecked((short)context.NextThingNumber++);
    }

    public ThingContext Context { get; }

    /// <summary>_GetClassID: the OID_ constant.</summary>
    public int ClassId { get; }

    /// <summary>mPos.</summary>
    public FVector Pos;

    /// <summary>mFlags.</summary>
    public ThingFlags Flags;

    /// <summary>mThingNumber: sThingCount at construction.</summary>
    public short ThingNumber { get; }

    /// <summary>mThingType.</summary>
    public uint ThingTypeBits;

    /// <summary>mRenderThing: the mesh render thing, when the thing has one.</summary>
    public RenderThing? RenderThing;

    public bool IsA(uint type) => (type & ThingTypeBits) != 0;

    public bool IsDying => (Flags & ThingFlags.Dying) != 0;

    /// <summary>CThing::Init.</summary>
    public virtual void Init(InitThing init)
    {
        if (RenderThing is null)
        {
            InitRenderThing(init);
        }

        SetThingType(0);
        Pos = init.Pos;
        if (ClipToGround())
        {
            float groundHeight = Context.Map.Collide(Pos);
            if (Pos.Z > groundHeight)
            {
                FVector newPos = Pos;
                newPos.Z = groundHeight;
                Teleport(newPos);
            }
        }

        if (!CanGoUnderWater())
        {
            float waterHeight = Context.WaterLevel;
            if (Pos.Z > waterHeight)
            {
                Pos.Z = waterHeight;
            }
        }

        if ((Flags & ThingFlags.InMapWho) != 0)
        {
            // mMapWhoEntry.Init, the big-things test on its sector layer and InitCollisionSeekingThing.
            Context.Skip("CThing::Init map-who entry and big-things list");
            InitCollisionSeekingThing(init.InitCst);
        }

        if (RenderThing is not null)
        {
            ThingTypeBits |= ThingType.Renderable;
        }

        if (init.Active == 0)
        {
            Deactivate();
        }

        Context.World.Things.Add(this);
    }

    /// <summary>CThing::InitRenderThing: CreateAndGetRenderThing(class id, this).</summary>
    public virtual void InitRenderThing(InitThing init) => Context.Skip("CThing::InitRenderThing (CreateAndGetRenderThing)");

    /// <summary>CThing::InitCollisionSeekingThing.</summary>
    public virtual void InitCollisionSeekingThing(InitCollision init)
    {
        if (init.NotSeekCollisionWith != ThingType.Everything)
        {
            if (RenderThing is null && init.MaxCollision == CollisionLevel.Mesh)
            {
                init.MaxCollision = CollisionLevel.ApproxGeometryShapes;
            }

            init.ForThing = this;
            Context.Skip("CCSPersistentThing::Init");
        }
    }

    /// <summary>SetThingType: each class ORs its bits and calls its base.</summary>
    public virtual void SetThingType(uint t) => ThingTypeBits = t | ThingType.Thing;

    public virtual bool ClipToGround() => true;

    public virtual bool CanGoUnderWater() => false;

    public virtual float GetMoveMultiplier() => 1.0f;

    public virtual void Activate()
    {
    }

    public virtual void Deactivate()
    {
    }

    /// <summary>CThing::UpdatePosition: the render thing follows.</summary>
    public void UpdatePosition()
    {
    }

    public virtual void StickToGround()
    {
        Pos.Z = Context.Map.Collide(Pos);
        UpdatePosition();
    }

    public virtual void MoveTo(FVector pos)
    {
        Pos = pos;
        UpdatePosition();
    }

    public virtual void Teleport(FVector pos)
    {
        Pos = pos;
        MoveTo(pos);
    }

    /// <summary>CThing::AddShutdownEvent.</summary>
    public virtual void AddShutdownEvent()
    {
        if ((Flags & ThingFlags.DeclaredShutdown) == 0)
        {
            Flags |= ThingFlags.DeclaredShutdown;
            Context.Events.AddEvent(ThingEvent.Shutdown, this, EventManager.NextFrame, EventPriority.StartOfFrame);
        }
    }

    /// <summary>CThing::StartDieProcess.</summary>
    public virtual bool StartDieProcess()
    {
        if (!IsDying)
        {
            Flags |= ThingFlags.Dying;
            AddShutdownEvent();
            return true;
        }

        return false;
    }

    /// <summary>CThing::Shutdown: out of the world's lists, then the monitor's readers are told.</summary>
    public override void Shutdown()
    {
        Context.World.Things.Remove(this);
        if ((Flags & ThingFlags.IsBigThing) != 0)
        {
            Context.World.BigThings.Remove(this);
        }

        base.Shutdown();
    }

    /// <summary>CThing::HandleEvent.</summary>
    public override void HandleEvent(Event e)
    {
        switch (e.EventNum)
        {
            case ThingEvent.Shutdown:
                Shutdown();
                break;
            case ThingEvent.StartDieProcess:
                StartDieProcess();
                break;
            default:
                base.HandleEvent(e);
                break;
        }
    }

    /// <summary>CThing::IncreaseThingCounter: nothing for most things.</summary>
    public virtual void IncreaseThingCounter(InitThing init)
    {
    }

    public override string ToString() => $"{GetType().Name} #{ThingNumber} at {Pos}";
}

/// <summary>A compiled mission script attached to a thing (IScript); its VM is M4c's.</summary>
public sealed class MissionScript(ComplexThing owner, ScriptObjectCode code)
{
    public ComplexThing Owner { get; } = owner;

    public ScriptObjectCode Code { get; } = code;
}

/// <summary>CComplexThing: a thing with an orientation, a name, a mission script, an animation and a motion controller.</summary>
public abstract class ComplexThing(ThingContext context, int classId) : Thing(context, classId)
{
    /// <summary>mOrientation.</summary>
    public FMatrix Orientation = FMatrix.Identity;

    /// <summary>mName (null when the thing has none).</summary>
    public byte[]? Name;

    /// <summary>mMissionScript.</summary>
    public MissionScript? Script;

    /// <summary>The mesh animation mode SetAnimMode chose (CAnimation's), -1 for none.</summary>
    public int AnimMode = -1;

    public override void SetThingType(uint t) => base.SetThingType(t | ThingType.ComplexThing);

    /// <summary>CComplexThing::Init: name, script, orientation, then CThing::Init.</summary>
    public override void Init(InitThing init)
    {
        SetName(init.Name);
        SetScript(init.Script);
        if (init.OrientationType == OrientationType.EulerAngles)
        {
            Orientation = FMatrix.FromYawPitchRoll(init.Yaw, init.Pitch, init.Roll);
        }
        else if (init.OrientationType == OrientationType.DirectionCosineMatrix)
        {
            Orientation = init.Orientation;
        }

        base.Init(init);
    }

    /// <summary>CComplexThing::SetName: a named thing joins the named-things list (Add: at its head).</summary>
    public void SetName(byte[] name)
    {
        if (Name is not null)
        {
            Context.World.NamedThings.Remove(this);
            Name = null;
        }

        if (name.Length != 0)
        {
            Name = name;
            Context.World.NamedThings.Add(this);
        }
    }

    /// <summary>
    /// CComplexThing::SetScript: a copy of the world's script of that name (WORLD.GetCopyOfScript), whose INIT_SCRIPT
    /// event is due next frame.
    /// </summary>
    public void SetScript(byte[] scriptName)
    {
        Script = null;
        if (scriptName.Length != 0)
        {
            ScriptObjectCode? code = null;
            foreach ((byte[] name, ScriptObjectCode c) in Context.Scripts)
            {
                if (name.AsSpan().SequenceEqual(scriptName))
                {
                    code = c;
                    break;
                }
            }

            if (code is not null)
            {
                Script = new MissionScript(this, code);
                Context.Events.AddEvent(ThingEvent.InitScript, this, EventManager.NextFrame, EventPriority.StartOfFrame);
            }
            else
            {
                Context.Skip($"FATAL ERROR: Cant find script '{Encoding.ASCII.GetString(scriptName)}'");
            }
        }
    }

    /// <summary>
    /// SetAnimMode: the mesh animation by index, reset to its start. (CAnimation's timing is ported with the
    /// animations; here only the chosen mode is kept.)
    /// </summary>
    public virtual bool SetAnimMode(int mode, bool resetFrame, bool forceLooped)
    {
        AnimMode = mode;
        return true;
    }

    /// <summary>The mesh's animation mode by name (the mesh's GetAnimModeByName), or -1.</summary>
    public int AnimModeByName(string name) => RenderThing?.MeshOrNull?.GetAnimModeByName(name) ?? -1;

    /// <summary>CComplexThing::HandleEvent: the script's init and ready events.</summary>
    public override void HandleEvent(Event e)
    {
        switch (e.EventNum)
        {
            case ThingEvent.InitScript:
                Context.Skip("IScript::Init");
                if (!IsA(ThingType.Unit))
                {
                    Context.Events.AddEvent(ThingEvent.ReadyScript, this, EventManager.NextFrame);
                }

                break;
            case ThingEvent.ReadyScript:
                Context.Skip("IScript::Ready");
                break;
            default:
                base.HandleEvent(e);
                break;
        }
    }

    /// <summary>CComplexThing::Shutdown.</summary>
    public override void Shutdown()
    {
        if (Name is not null)
        {
            Context.World.NamedThings.Remove(this);
            Name = null;
        }

        Context.Skip("CThing::SetObjective(FALSE)");
        base.Shutdown();
    }

    /// <summary>CComplexThing::AddShutdownEvent: the script's Died, then CThing's.</summary>
    public override void AddShutdownEvent()
    {
        if ((Flags & ThingFlags.DeclaredShutdown) == 0 && Script is not null)
        {
            Context.Skip("IScript::Died");
            Script = null;
        }

        base.AddShutdownEvent();
    }
}
