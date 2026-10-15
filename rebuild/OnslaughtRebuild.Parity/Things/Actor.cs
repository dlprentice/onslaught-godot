// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/actor.h and src/actor.cpp (CActor::Init 004011e0, AddMoveEvent, HandleEvent 004019e0),
// in Stuart's release.

using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;

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
            // CActor::Init's inline copy of the rotation (00401255..0040131a) is not yet checked against
            // CComplexThing::Init's register use; it feeds the previous orientation only.
            OldOrientation = FMatrix.FromYawPitchRoll(init.Yaw, init.Pitch, init.Roll);
        }
        else if (init.OrientationType == OrientationType.DirectionCosineMatrix)
        {
            OldOrientation = init.Orientation;
        }

        base.Init(init);

        int r = (int)GetMoveMultiplier();
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
                DoFullMoveCount = (int)GetMoveMultiplier();
            }

            Context.Events.AddEvent(ev, this, EventManager.NextFrame, EventPriority.StartOfFrame, null, reuse as ScheduledEvent);
        }
    }

    // IRenderSubject: what the mesh pose code asks of the thing (the rest pose until animation timing is ported).
    int IRenderSubject.RealAnimIndex => -1;

    float IRenderSubject.RenderFrame => 0;

    FVector IRenderSubject.RenderPos => Pos;

    FMatrix IRenderSubject.RenderOrientation => Orientation;

    bool IRenderSubject.HasMotionController => false;

    bool IRenderSubject.RenderUseHierarchy(int partNumber) => true;
}
