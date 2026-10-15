// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/actor.h and src/actor.cpp (CActor::Init 004011e0, LowFidelityMove (inlined into
// HandleEvent), Move 004015e0, AddMoveEvent, HandleEvent 004019e0, IsOnGround 00401f70, IsInWater 00401fa0, IsOnObject
// 00401fd0, DeclareOnGround 00402000, DeclareInWater 00402010), in Stuart's release.

using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>ActorEvent.</summary>
public static class ActorEvent
{
    public const int Move = 3000;
    public const int LfMove = 3001;
}

/// <summary>CActor: a complex thing that moves, with a velocity and a full or light move each frame.</summary>
public abstract class Actor(ThingContext context, int classId) : ComplexThing(context, classId), IRenderSubject
{
    public FVector Velocity;
    public FVector OldPos;
    public FMatrix OldOrientation;
    public float LastTimeOnGround;
    public float LastTimeInWater;
    public float LastTimeOnObject;
    public float LastMoveTime;

    /// <summary>mDoFullMoveCount: light moves left before the next full move.</summary>
    public int DoFullMoveCount;

    public override void SetThingType(uint t) => base.SetThingType(t | ThingType.Actor);

    /// <summary>
    /// CActor::Init: the times, velocity, previous position and orientation, then CComplexThing::Init; then one draw for
    /// the full-move phase and the first move event (next frame).
    /// </summary>
    public override void Init(InitThing init)
    {
        LastTimeOnGround = -100.0f;
        LastTimeInWater = -100.0f;
        LastTimeOnObject = -100.0f;
        LastMoveTime = Context.Events.Time;
        Velocity = init.Velocity;
        OldPos = init.Pos;
        if (init.OrientationType == OrientationType.EulerAngles)
        {
            // CActor::Init's inline copy (00401255..00401316, pristine 74154bfa) stores and keeps the same values as
            // CComplexThing::Init's (cos/sin yaw, cos roll and cos pitch to floats; sin roll and sin pitch on the stack)
            // in the same order, so FromYawPitchRoll's sequence applies.
            OldOrientation = FMatrix.FromYawPitchRoll(init.Yaw, init.Pitch, init.Roll);
        }
        else if (init.OrientationType == OrientationType.DirectionCosineMatrix)
        {
            OldOrientation = init.Orientation;
        }

        base.Init(init);

        int r = X87.Fist(GetMoveMultiplier());
        DoFullMoveCount = Context.Random.Random() % r;
        AddMoveEvent(null);
    }

    /// <summary>CActor::AddMoveEvent: a light move until the count runs out, then a full one, each next frame.</summary>
    public void AddMoveEvent(Event? reuse)
    {
        if ((Flags & ThingFlags.DeclaredShutdown) == 0)
        {
            DoFullMoveCount--;
            int ev = ActorEvent.LfMove;
            if (DoFullMoveCount <= 0)
            {
                ev = ActorEvent.Move;
                DoFullMoveCount = X87.Fist(GetMoveMultiplier());
            }

            Context.Events.AddEvent(ev, this, EventManager.NextFrame, EventPriority.StartOfFrame, null, reuse as ScheduledEvent);
        }
    }

    /// <summary>CActor::HandleEvent (004019e0): a full or light move, then the next move event.</summary>
    public override void HandleEvent(Event e)
    {
        switch (e.EventNum)
        {
            case ActorEvent.Move:
                Move();
                AddMoveEvent(e);
                break;
            case ActorEvent.LfMove:
                LowFidelityMove();
                AddMoveEvent(e);
                break;
            default:
                base.HandleEvent(e);
                break;
        }
    }

    /// <summary>IsOnGround (slot 67): on the ground within the last 0.15 s.</summary>
    public virtual bool IsOnGround() => Context.Events.Time - LastTimeOnGround < 0.15f;

    /// <summary>IsInWater: in the water within the last 0.25 s.</summary>
    public bool IsInWater() => Context.Events.Time - LastTimeInWater < 0.25f;

    /// <summary>IsOnObject: on an object within the last 0.15 s.</summary>
    public bool IsOnObject() => Context.Events.Time - LastTimeOnObject < 0.15f;

    /// <summary>DeclareOnGround (virtual: the dropship overrides it).</summary>
    public virtual void DeclareOnGround() => LastTimeOnGround = Context.Events.Time;

    public virtual void DeclareInWater() => LastTimeInWater = Context.Events.Time;

    /// <summary>CActor::DeclareOnObject (00402020).</summary>
    public virtual void DeclareOnObject(Thing? onObject) => LastTimeOnObject = Context.Events.Time;

    /// <summary>
    /// CActor::Move: the old place, the velocity added, then down to the ground (sliding along it with TF_SLIDE, else on
    /// the ground with a bounce: |vZ| > Gravity() + 0.05 reverses vZ scaled by BounceFactor(), otherwise vZ is 0) and the
    /// water test; then the map-who entry is updated FROM THE OLD POSITION (retail passes mOldPos, so a thing's sector
    /// lags its move by one). The slide's dot product is compiled (n.Z*v.Z + n.Y*v.Y) + n.X*v.X (00401760..00401778).
    /// The developer-mode speed warning is off.
    /// </summary>
    public virtual void Move() => ActorMove();

    /// <summary>CActor::Move itself, for a class whose Move calls it past its parents' (CBattleEngine's).</summary>
    protected void ActorMove()
    {
        LastMoveTime = Context.Events.Time;
        OldPos = Pos;
        OldOrientation = Orientation;
        bool inMap = MapWhoEntry.SectorIn.Layer != -1;

        Pos.X += Velocity.X;
        Pos.Y += Velocity.Y;
        Pos.Z += Velocity.Z;

        float waterHeight = Context.WaterLevel - COfGHeight();
        if (ClipToGround())
        {
            float alt = Context.Map.Collide(Pos) - COfGHeight();
            if (alt <= Pos.Z)
            {
                Pos.Z = alt;
                if ((Flags & ThingFlags.Slide) != 0)
                {
                    FVector n = Context.Map.Normal(Pos);
                    float backoff = (n.Z * Velocity.Z) + (n.Y * Velocity.Y) + (n.X * Velocity.X);
                    Velocity.X -= n.X * backoff;
                    Velocity.Y -= n.Y * backoff;
                    Velocity.Z -= n.Z * backoff;
                }
                else
                {
                    DeclareOnGround();
                    if (MathF.Abs(Velocity.Z) > Gravity() + 0.05f)
                    {
                        Velocity.Z = -(BounceFactor() * Velocity.Z);
                    }
                    else
                    {
                        Velocity.Z = 0;
                    }
                }
            }

            if (waterHeight <= Pos.Z)
            {
                DeclareInWater();
            }
        }

        if (inMap && MapWhoEntry.UpdateEntry(Context.MapWho, OldPos) && HasCollisionSeekingThing)
        {
            Context.Skip("CCollisionSeekingThing::MovedSector");
        }
    }

    /// <summary>
    /// LowFidelityMove (inlined into HandleEvent): the old position, the velocity added, down to the ground (marking the
    /// time on the ground). No bounce, no water test, and the map-who entry is not updated.
    /// </summary>
    public void LowFidelityMove()
    {
        OldPos = Pos;
        Pos.X += Velocity.X;
        Pos.Y += Velocity.Y;
        Pos.Z += Velocity.Z;
        if (ClipToGround())
        {
            float alt = Context.Map.Collide(Pos) - COfGHeight();
            if (alt <= Pos.Z)
            {
                Pos.Z = alt;
                LastTimeOnGround = Context.Events.Time;
            }
        }
    }

    /// <summary>CActor::StickToGround (00402030): grounded, with no move left over.</summary>
    public override void StickToGround()
    {
        base.StickToGround();
        OldPos = Pos;
    }

    // IRenderSubject: what the mesh pose code asks of the thing.
    int IRenderSubject.RealAnimIndex => GetRealAnimIndex();

    float IRenderSubject.RenderFrame => GetRenderFrame();

    FVector IRenderSubject.RenderPos => Pos;

    FMatrix IRenderSubject.RenderOrientation => Orientation;

    bool IRenderSubject.HasMotionController => false;

    bool IRenderSubject.RenderUseHierarchy(int partNumber) => true;
}
