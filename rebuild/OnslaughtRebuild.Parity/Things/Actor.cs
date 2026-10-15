// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/actor.h and src/actor.cpp (CActor::Init 004011e0, AddMoveEvent, HandleEvent 004019e0),
// in Stuart's release.

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

    /// <summary>Move (slot 33): the full move (gravity, collision, the subclass's motion); ported with the movement.</summary>
    public virtual void Move() => Context.Skip($"{GetType().Name}::Move (the movement is not ported yet)");

    /// <summary>LowFidelityMove: the light move between full ones; ported with the movement.</summary>
    public virtual void LowFidelityMove() => Context.Skip($"{GetType().Name}::LowFidelityMove (the movement is not ported yet)");

    // IRenderSubject: what the mesh pose code asks of the thing (the rest pose until animation timing is ported).
    int IRenderSubject.RealAnimIndex => -1;

    float IRenderSubject.RenderFrame => 0;

    FVector IRenderSubject.RenderPos => Pos;

    FMatrix IRenderSubject.RenderOrientation => Orientation;

    bool IRenderSubject.HasMotionController => false;

    bool IRenderSubject.RenderUseHierarchy(int partNumber) => true;
}
