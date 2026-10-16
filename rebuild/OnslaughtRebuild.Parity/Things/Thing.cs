// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/thing.h and src/thing.cpp (CThing: the constructor 004f33e0, Init 004f34a0, Shutdown
// 004f3600, HandleEvent 004f3730, AddShutdownEvent 004f3760, StartDieProcess 004f37a0, SetObjective 004f3970,
// GetCentrePos 004f3ac0, StickToGround 004f3c50, GetRenderThingFrameIncrement 004f3c80, MoveTo 004f3cb0, Teleport
// 004f3ce0; CComplexThing: GetOrientation, SetAnimMode 004f44a0, GetRenderFrame 004f45c0, GetRealAnimIndex 004014d0,
// the constructor 004f3e10, Init 004f3fd0, SetName 004f4120, Shutdown
// 004f41b0, SetScript 004f4230, HandleEvent 004f4300, AddShutdownEvent 004f43d0, StartDieProcess 004f4430, Hit
// 004f4480), in Stuart's release.

using System.Text;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Collision;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.World;
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

/// <summary>EAIState.</summary>
public enum AIState
{
    On = 0,
    Off,
    Normal,
    Defensive,
    Onf,
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
public abstract class Thing : Monitor, IOccupancyUnit
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

    /// <summary>mMapWhoEntry: the thing's place in the map-who grid.</summary>
    public MapWhoEntry MapWhoEntry => _mapWhoEntry ??= new MapWhoEntry(this);

    private MapWhoEntry? _mapWhoEntry;

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
            MapWhoEntry.Init(Context.MapWho, Pos, init.ForceRadius);
            if (MapWhoEntry.SectorIn.Layer < MapWho.UseBigListFor)
            {
                Context.World.BigThings.Append(this);
                Flags |= ThingFlags.IsBigThing;
            }

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
            HasCollisionSeekingThing = true;
            if (init.MaxCollision == CollisionLevel.Mesh)
            {
                CollisionMeshVolume = new MeshCollisionVolume(RenderThing, init.UseFixedMeshTransforms, init.IgnoreAnimationCollision);
            }

            Seeker = new CollisionSeekingThing(this, init);
        }
    }

    /// <summary>SetThingType: each class ORs its bits and calls its base.</summary>
    public virtual void SetThingType(uint t) => ThingTypeBits = t | ThingType.Thing;

    public virtual bool ClipToGround() => true;

    /// <summary>Gravity (slot 45, 004014b0).</summary>
    public virtual float Gravity() => 0.01f;

    /// <summary>ObeyGravity (slot 46).</summary>
    public virtual bool ObeyGravity() => true;

    /// <summary>BounceFactor (slot 47).</summary>
    public virtual float BounceFactor() => 0;

    /// <summary>COfGHeight (slot 48): the height of the centre of gravity above the ground.</summary>
    public virtual float COfGHeight() => 0;

    /// <summary>mCollisionSeekingThing != NULL (the collision-seeking things are not ported; this records where one would be).</summary>
    public bool HasCollisionSeekingThing;

    /// <summary>mCollisionSeekingThing (as GetCSPT reads it: always a persistent one here).</summary>
    public CollisionSeekingThing? Seeker;

    /// <summary>GetLocalLastFrameMovement (slot 25): none for a thing that does not move.</summary>
    public virtual FVector GetLocalLastFrameMovement() => new(0.0f, 0.0f, 0.0f);

    /// <summary>SetVelocity (slot 28): nothing for a thing without a velocity.</summary>
    public virtual void SetVelocity(FVector v)
    {
    }

    /// <summary>
    /// GetMotionController() != NULL, for the collision test's dying-building rule: the parity core models no motion
    /// controllers (a building's destructible segments would make one).
    /// </summary>
    public virtual bool HasMotionControllerForCollision() => false;

    /// <summary>
    /// The collision-seeking thing's mesh volume (CCollisionSeekingThing::Init makes one when the maximum collision
    /// level is the mesh): the thing's render thing, its fixed-transforms and ignore-animation flags.
    /// </summary>
    public MeshCollisionVolume? CollisionMeshVolume;

    public bool HasCollisionMeshVolume() => CollisionMeshVolume is not null;

    /// <summary>GetRenderImposterBias (IRenderableThing 23): scales the distance at which the mesh gives way to its imposter.</summary>
    public virtual float GetRenderImposterBias() => 1.0f;

    /// <summary>GetVelocity (slot 27): zero for a plain thing.</summary>
    public virtual FVector GetVelocity() => new(0, 0, 0);

    /// <summary>GetOrientation (virtual): the identity for a plain thing.</summary>
    public virtual FMatrix GetOrientation() => FMatrix.Identity;

    /// <summary>
    /// The occupancy builders' collision query (NavigationMap.cpp's ShadowHits): a line from 100 below the cell's
    /// ground point up to it, then a unit sphere there, against the mesh volume, with a report reset to stop at the
    /// first hit.
    /// </summary>
    public bool ShadowHits(FVector cellPos)
    {
        MeshCollisionVolume shape = CollisionMeshVolume
            ?? throw new InvalidOperationException($"{this} is on the occupancy grids without a collision mesh volume (retail reads NULL)");
        var report = new CollisionReport { NumContacts = 0, UnknownA8 = false, Stuck = false, TestTriangles = 0, UnknownD0 = 1 };
        var lineMove = new CollisionMove { Pos = cellPos };
        if (shape.CollideWithLine(ref lineMove, new FVector(0, 0, -100.0f), new FVector(0, 0, 0), report))
        {
            return true;
        }

        var sphereMove = new CollisionMove { Pos = cellPos };
        return shape.CollideWithSphere(ref sphereMove, 1.0f, report);
    }

    /// <summary>CThing::GetCentrePos: a building's bounding-box centre, a ground battle engine's raised point, the box's height otherwise.</summary>
    public FVector GetCentrePos()
    {
        BoundingBox? box = RenderThing?.MeshOrNull?.BoundingBox;
        if (IsA(ThingType.Building) || IsA(ThingType.Component))
        {
            if (box is not null)
            {
                return Pos + GetOrientation().Transform(box.Origin);
            }
        }
        else if (IsA(ThingType.BattleEngine))
        {
            if (IsA(ThingType.GroundUnit))
            {
                FVector p = Pos;
                p.Z += 1.9f * 0.4f;
                return p;
            }
        }
        else if (box is not null)
        {
            return new FVector(Pos.X, Pos.Y, Pos.Z + box.Origin.Z);
        }

        return Pos;
    }


    public virtual bool CanGoUnderWater() => false;

    public virtual float GetMoveMultiplier() => 1.0f;

    /// <summary>GetMaxVelocity (slot 15).</summary>
    public virtual float GetMaxVelocity() => 0.0f;

    /// <summary>GetRadius (slot 16, 00401440): the render thing's radius (CRTMesh: its mesh's), 0 without one.</summary>
    public virtual float GetRadius() => RenderThing is null ? 0.0f : RenderThing.Mesh.Radius;

    /// <summary>GetBoundingRadius (slot 17, 004f3940): the mesh's bounding box radius when there is one, else GetRadius.</summary>
    public virtual float GetBoundingRadius()
    {
        if (RenderThing is not null && RenderThing.Mesh.BoundingBox is { } box)
        {
            return box.Radius;
        }

        return GetRadius();
    }

    /// <summary>TeleportOrientation (slot 21): nothing for a plain thing.</summary>
    public virtual void TeleportOrientation(FMatrix orientation)
    {
    }

    /// <summary>
    /// CThing::SetObjective: a thing marked as an objective joins the world's objective list (at its head) once, and
    /// leaves it when unmarked.
    /// </summary>
    public void SetObjective(bool value)
    {
        if (value)
        {
            if ((Flags & ThingFlags.MarkedObjective) == 0)
            {
                Context.World.ObjectiveThings.Add(this);
                Flags |= ThingFlags.MarkedObjective;
            }
        }
        else if ((Flags & ThingFlags.MarkedObjective) != 0)
        {
            Context.World.ObjectiveThings.Remove(this);
            Flags &= ~ThingFlags.MarkedObjective;
        }
    }

    /// <summary>IsObjective (slot 26): FALSE for a plain thing.</summary>
    public virtual bool IsObjective() => false;

    /// <summary>MakeVisible (slot 32, 00401460).</summary>
    public virtual void MakeVisible() => Flags &= ~ThingFlags.Invisible;

    /// <summary>MakeInvisible (slot 33, 00401470).</summary>
    public virtual void MakeInvisible() => Flags |= ThingFlags.Invisible;

    /// <summary>GetAIState (slot 53): AI_ON.</summary>
    public virtual AIState GetAIState() => AIState.On;

    /// <summary>SetAIState (slot 54): nothing.</summary>
    public virtual void SetAIState(AIState state)
    {
    }

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

    /// <summary>Hit (slot 39): nothing for a plain thing.</summary>
    public virtual void Hit(Thing other, Collision.CollisionReport? report)
    {
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

        // `delete this`: the destructors, most derived first, then the embedded map-who entry's takes the thing off the
        // grid.
        Deleted();
        _mapWhoEntry?.RemoveFromMap(Context.MapWho);
    }

    /// <summary>
    /// The destructors' work at CThing::Shutdown's `delete this`: each class's override does its own, then calls the
    /// base's (the order of retail's destructor chain).
    /// </summary>
    protected virtual void Deleted()
    {
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

/// <summary>CComplexThing: a thing with an orientation, a name, a mission script, an animation and a motion controller.</summary>
public abstract class ComplexThing(ThingContext context, int classId) : Thing(context, classId)
{
    /// <summary>mOrientation.</summary>
    public FMatrix Orientation = FMatrix.Identity;

    /// <summary>mName (null when the thing has none).</summary>
    public byte[]? Name;

    /// <summary>mMissionScript.</summary>
    public IScript? Script;

    /// <summary>The mesh animation mode SetAnimMode chose (CAnimation's), -1 for none.</summary>
    public int AnimMode = -1;

    public override void SetThingType(uint t) => base.SetThingType(t | ThingType.ComplexThing);

    /// <summary>CComplexThing::IsObjective (004014e0): whether the marked-objective flag is set.</summary>
    public override bool IsObjective() => (Flags & ThingFlags.MarkedObjective) != 0;

    /// <summary>CComplexThing::GetOrientation: mOrientation.</summary>
    public override FMatrix GetOrientation() => Orientation;

    /// <summary>CComplexThing::TeleportOrientation (004f4460).</summary>
    public override void TeleportOrientation(FMatrix orientation) => Orientation = orientation;

    /// <summary>CComplexThing::GoToPoint (00401520): nothing.</summary>
    public virtual void GoToPoint(FVector point, bool inOverride = false)
    {
    }

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
        if (Script is not null)
        {
            Script.Delete();
            Script = null;
        }

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
                Script = IScript.Create(this, code, Context);
                Context.Events.AddEvent(ThingEvent.InitScript, this, EventManager.NextFrame, EventPriority.StartOfFrame);
            }
            else
            {
                Context.Skip($"FATAL ERROR: Cant find script '{Encoding.ASCII.GetString(scriptName)}'");
            }
        }
    }

    /// <summary>mAnimation: made by the first SetAnimMode.</summary>
    public Animation? Animation;

    /// <summary>CComplexThing::SetAnimMode: the animation clock (made on first use) takes the mode.</summary>
    public virtual bool SetAnimMode(int mode, bool resetFrame, bool forceLooped)
    {
        AnimMode = mode;
        Animation ??= new Animation(this);
        return Animation.SetAnimMode(mode, resetFrame, forceLooped);
    }

    /// <summary>CComplexThing::GetRenderFrame: the animation's render frame, 0 without one.</summary>
    public float GetRenderFrame() => Animation?.GetRenderFrame() ?? 0.0f;

    /// <summary>GetRealAnimIndex (004014d0): the animation's index in the mesh's table, -1 without one.</summary>
    public int GetRealAnimIndex() => Animation?.AnimIndex ?? -1;

    /// <summary>
    /// CThing::GetRenderThingFrameIncrement: the render thing's rate for the mode and the mode's index in its table
    /// (CRTMesh::GetFrameIncrement through the mesh; the index stays as it was without a mesh).
    /// </summary>
    public float GetRenderThingFrameIncrement(int mode, ref int realIndex)
    {
        if (RenderThing?.MeshOrNull is not { } mesh)
        {
            return 0.0f;
        }

        float rate = mesh.GetFrameIncrement(mode, out int index);
        realIndex = index;
        return rate;
    }

    /// <summary>The mesh's animation mode by name (the mesh's GetAnimModeByName), or -1.</summary>
    public int AnimModeByName(string name) => RenderThing?.MeshOrNull?.GetAnimModeByName(name) ?? -1;

    /// <summary>
    /// CComplexThing::HandleEvent: SHUTDOWN tells the script first (while the game is at most playing), INIT_SCRIPT
    /// runs the script's Init (and, for a non-unit, READY_SCRIPT follows next frame), READY_SCRIPT its Ready.
    /// </summary>
    public override void HandleEvent(Event e)
    {
        switch (e.EventNum)
        {
            case ThingEvent.Shutdown:
                if ((Context.Game?.State ?? Game.GameState.Playing) <= Game.GameState.Playing && Script is not null)
                {
                    Script.Shutdown();
                }

                base.HandleEvent(e);
                break;
            case ThingEvent.InitScript:
                Script?.Init();
                if (!IsA(ThingType.Unit))
                {
                    Context.Events.AddEvent(ThingEvent.ReadyScript, this, EventManager.NextFrame);
                }

                break;
            case ThingEvent.ReadyScript:
                Script?.Ready();
                break;
            default:
                base.HandleEvent(e);
                break;
        }
    }

    /// <summary>
    /// CComplexThing::Shutdown: out of the named things, no longer an objective, then CThing's; its `delete this`
    /// runs ~CComplexThing (004f3f00), which deletes the script.
    /// </summary>
    public override void Shutdown()
    {
        if (Name is not null)
        {
            Context.World.NamedThings.Remove(this);
            Name = null;
        }

        SetObjective(false);
        base.Shutdown();
    }

    /// <summary>~CComplexThing (004f3f00): the script is deleted.</summary>
    protected override void Deleted()
    {
        if (Script is not null)
        {
            Script.Delete();
            Script = null;
        }

        base.Deleted();
    }

    /// <summary>CComplexThing::AddShutdownEvent: the script's Died, and the script goes; then CThing's.</summary>
    public override void AddShutdownEvent()
    {
        if ((Flags & ThingFlags.DeclaredShutdown) == 0 && Script is not null)
        {
            Script.Died();
            Script.Delete();
            Script = null;
        }

        base.AddShutdownEvent();
    }

    /// <summary>CComplexThing::StartDieProcess: CThing's, and the script hears it started dying.</summary>
    public override bool StartDieProcess()
    {
        if (base.StartDieProcess())
        {
            Script?.StartedDying();
            return true;
        }

        return false;
    }

    /// <summary>CComplexThing::Hit (004f4480): the script hears of a complex thing hitting this one.</summary>
    public override void Hit(Thing other, Collision.CollisionReport? report)
    {
        if (Script is not null && other.IsA(ThingType.ComplexThing))
        {
            Script.Hit((ComplexThing)other);
        }
    }

    /// <summary>CComplexThing::FinishedPlayingCurrentAnimation (004f45a0): PlayAnimationWait's script carries on.</summary>
    public bool FinishedPlayingCurrentAnimation()
    {
        Script?.FinishedPlayingAnim();
        return true;
    }
}
