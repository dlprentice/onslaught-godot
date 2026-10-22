// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/collisionseekingthing.cpp and collisionseekingthing.h (CCollisionSeekingThing: Init
// 00426150, SetShape 00426370, SetCollisionMask 00426480, Unknown6 004264a0, Unknown8 00426900, Unknown10 00426920;
// CCSPersistentThing: Init 004269b0, MovedSector 00426a00, HandleEvent 00426a20) and src/HLCollisionDetector.cpp
// (CHLCollisionDetector: Init 00480a30, CheckCollision 00480c90, CheckCandidate 00480db0, ScanSector 00480e10,
// ScheduleCollisionCheck 00480ed0, MovedSector 00481060, HandleEvent 004812d0). Reconstructed; not in Stuart's release.

using OnslaughtRebuild.Parity.Collision;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Maths;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>
/// CCSPersistentThing (a CCollisionSeekingThing that keeps seeking): a thing's collision state. The flags hold the
/// desired, minimum and maximum collision levels (2 bits each), the response (bits 6-7), "seek with a mask" (0x100),
/// "test the other's box against a mesh" (0x200) and "colliding now" (0x400, set a moment after Init).
/// </summary>
public class CollisionSeekingThing : Monitor
{
    public const int StartCollidingEvent = 3000;

    public CollisionSeekingThing(Thing forThing, InitCollision init)
    {
        ForThing = forThing;
        CollisionFlags = 0;
        CollisionFlags |= (int)init.DesiredCollision;
        CollisionFlags |= (int)init.MinCollision << 2;
        CollisionFlags |= (int)init.MaxCollision << 4;
        CollisionFlags |= init.CollisionResponse << 6;
        CollisionFlags |= 0x400;
        CollisionFlags |= (init.DoObbForMeshCollision ? 1 : 0) << 9;
        IgnoreThing = init.IgnoreThing;
        NotSeekCollisionWith = init.NotSeekCollisionWith;
        float radius = forThing.GetBoundingRadius();
        Shape = init.Shape ?? new SphereVolume(radius);
        Radius = radius;
        Shape.Pos = forThing.GetCentrePos() - forThing.Pos;
        if (MaxCollisionLevel == (int)CollisionLevel.Mesh && forThing.CollisionMeshVolume is { } mesh)
        {
            MeshVolume = new MeshVolume(mesh);
        }

        // CCSPersistentThing::Init: start colliding a moment later, then the detector's first scan.
        if (init.StartCollideOnNextFrame)
        {
            CollisionFlags &= ~0x400;
            forThing.Context.Events.AddEventIn(init.TimeBeforeStart, StartCollidingEvent, this);
        }

        Detector = new CollisionDetector(this);
        Detector.Init();
    }

    public Thing ForThing { get; }

    public int CollisionFlags;

    public Thing? IgnoreThing;

    public uint NotSeekCollisionWith;

    /// <summary>mShape: the approximate volume (Unknown7).</summary>
    public GeneralVolume Shape { get; private set; }

    /// <summary>mCollisionMeshVolume.</summary>
    public MeshVolume? MeshVolume { get; }

    public float Radius { get; protected set; }

    public CollisionDetector Detector { get; }

    public int MaxCollisionLevel => (CollisionFlags & 0x30) >> 4;

    /// <summary>SetShape (Stuart's name): the volume replaced, placed at the thing's centre.</summary>
    public void SetShape(GeneralVolume shape)
    {
        Shape = shape;
        Shape.Pos = ForThing.GetCentrePos() - ForThing.Pos;
    }

    /// <summary>SetCollisionMask: the types not sought, and seeking by the mask.</summary>
    public void SetCollisionMask(uint mask)
    {
        NotSeekCollisionWith = mask;
        CollisionFlags |= 0x100;
    }

    /// <summary>Unknown8: whether this seeks collision with the other's type.</summary>
    public virtual bool Seeks(CollisionSeekingThing other) => (other.ForThing.ThingTypeBits & NotSeekCollisionWith) == 0;

    /// <summary>Unknown7: the approximate volume a collision uses (the shape).</summary>
    public virtual GeneralVolume GetVolume() => Shape;

    /// <summary>Unknown10: the sectors between the two (at the coarser layer), the larger of x and y.</summary>
    public int SectorDistance(Sector sector)
    {
        Sector other = sector;
        Sector here = ForThing.MapWhoEntry.SectorIn;
        while (other.Layer < here.Layer)
        {
            here = here.ToParent();
        }

        while (other.Layer > here.Layer)
        {
            other = other.ToParent();
        }

        return Math.Max(Math.Abs(here.X - other.X), Math.Abs(here.Y - other.Y));
    }

    public override void HandleEvent(Event e)
    {
        if (e.EventNum == StartCollidingEvent)
        {
            CollisionFlags |= 0x400;
        }
    }

    /// <summary>
    /// Unknown6 (004264a0): the two things meet. Each side's volume is its mesh when its level (its max capped, raised
    /// to its min, by the other's desired) is mesh, else its shape; two meshes test the smaller one's shape. With the
    /// response on, each side that is not stationary and was stopped moves to where the test stopped it. Both hear of
    /// the hit.
    /// </summary>
    public void Collide(CollisionSeekingThing other)
    {
        if ((CollisionFlags & 0x400) == 0)
        {
            return;
        }

        if (ReferenceEquals(IgnoreThing, other.ForThing) || ReferenceEquals(other.IgnoreThing, ForThing))
        {
            return;
        }

        if ((other.ForThing.Flags & ThingFlags.Invisible) != 0 || (ForThing.Flags & ThingFlags.Invisible) != 0)
        {
            return;
        }

        if ((other.ForThing.IsDying && other.ForThing.IsA(ThingType.Building) && !other.ForThing.HasMotionControllerForCollision())
            || (ForThing.IsDying && ForThing.IsA(ThingType.Building) && !ForThing.HasMotionControllerForCollision()))
        {
            return;
        }

        int response = Math.Min((CollisionFlags >> 6) & 3, (other.CollisionFlags >> 6) & 3);
        int level = other.CollisionFlags & 3;
        if (((CollisionFlags >> 2) & 3) > level)
        {
            level = (CollisionFlags >> 2) & 3;
        }

        if (((CollisionFlags >> 4) & 3) < level)
        {
            level = (CollisionFlags >> 4) & 3;
        }

        int otherLevel = CollisionFlags & 3;
        if (((other.CollisionFlags >> 2) & 3) > otherLevel)
        {
            otherLevel = (other.CollisionFlags >> 2) & 3;
        }

        if (((other.CollisionFlags >> 4) & 3) < otherLevel)
        {
            otherLevel = (other.CollisionFlags >> 4) & 3;
        }

        GeneralVolume volume = level <= (int)CollisionLevel.ApproxGeometryShapes ? GetVolume() : MeshVolume!;
        if (volume is MeshVolume mv && (other.CollisionFlags & 0x200) == 0x200)
        {
            mv.Mesh.Unknown18 = 1;
        }

        GeneralVolume otherVolume = otherLevel <= (int)CollisionLevel.ApproxGeometryShapes ? other.GetVolume() : other.MeshVolume!;
        if (otherVolume is MeshVolume omv && (CollisionFlags & 0x200) != 0)
        {
            omv.Mesh.Unknown18 = 1;
        }

        var move = new CollisionMove { Stopped = false, Pos = volume.Pos + ForThing.Pos, Move = ForThing.GetLocalLastFrameMovement() };
        var otherMove = new CollisionMove
        {
            Stopped = false,
            Pos = other.ForThing.Pos + otherVolume.Pos,
            Move = other.ForThing.GetLocalLastFrameMovement(),
        };
        var report = new CollisionReport
        {
            Unknown00 = 0,
            NumContacts = 0,
            UnknownA8 = false,
            Stuck = false,
            UnknownD0 = 0,
            TestTriangles = response,
            RelativeMove = otherMove.Move - move.Move,
        };
        if (level == (int)CollisionLevel.Mesh && otherLevel == (int)CollisionLevel.Mesh)
        {
            if (otherVolume.Type != 3 || volume.Type != 3)
            {
                return;
            }

            if (Radius > other.Radius)
            {
                ((MeshVolume)volume).Mesh.Unknown18 = 1;
                otherVolume = other.GetVolume();
            }
            else
            {
                ((MeshVolume)otherVolume).Mesh.Unknown18 = 1;
                volume = GetVolume();
            }
        }

        bool hit = otherVolume.Collide(ref otherMove, ref move, volume, report);
        MeshCollisionVolume.Trace?.Invoke($"collide {ForThing.GetType().Name} vs {other.ForThing.GetType().Name}: hit {hit} response {response} move pos {MeshCollisionVolume.Bits(move.Pos)} move {MeshCollisionVolume.Bits(move.Move)} stopped {move.Stopped}; other pos {MeshCollisionVolume.Bits(otherMove.Pos)} move {MeshCollisionVolume.Bits(otherMove.Move)} stopped {otherMove.Stopped}");
        if (hit && response != 0)
        {
            if (!ForThing.IsA(ThingType.Stationary) && move.Stopped)
            {
                ForThing.MoveTo(move.Pos - volume.Pos);
                ForThing.SetVelocity(move.Move);
            }

            if (!other.ForThing.IsA(ThingType.Stationary) && otherMove.Stopped)
            {
                other.ForThing.MoveTo(otherMove.Pos - otherVolume.Pos);
                other.ForThing.SetVelocity(otherMove.Move);
            }
        }

        if (hit)
        {
            Thing otherThing = other.ForThing;
            report.Unknown00 = 1;
            ForThing.Hit(otherThing, report);
            otherThing.Hit(ForThing, report);
        }
    }
}

/// <summary>
/// CHLCollisionDetector: a persistent seeker's scheduling. It scans the 3 x 3 sectors around its thing (the thing's own
/// layer's sub-trees, single sectors at the coarser layers) and schedules a check with each candidate; a check of a pair
/// still apart is rescheduled for when they could first touch at their top speeds, one that overlaps collides.
/// </summary>
public sealed class CollisionDetector(CollisionSeekingThing thing) : Monitor
{
    public const int CheckEvent = 2000;

    /// <summary>mChecksDone (static).</summary>
    public static int ChecksDone;

    /// <summary>A diagnostic hook: each check (the two seekers and what it found), for comparison with a port trace.</summary>
    public static Action<CollisionSeekingThing, CollisionSeekingThing, string>? Trace;

    private bool _checkingCollision;
    private ScheduledEvent? _event;

    private ThingContext Context => thing.ForThing.Context;

    /// <summary>Init (00480a30).</summary>
    public void Init()
    {
        _checkingCollision = false;
        _event = null;
        MapWho map = Context.MapWho;
        int firstLayer = thing.ForThing.MapWhoEntry.SectorIn.Layer;
        for (int layer = firstLayer; layer >= 0; layer--)
        {
            Sector centre = map.WorldToSector(thing.ForThing.Pos, layer);
            for (int x = centre.X - 1; x <= centre.X + 1; x++)
            {
                for (int y = centre.Y - 1; y <= centre.Y + 1; y++)
                {
                    var sector = new Sector(x, y, layer);
                    if (!MapWho.IsValidSector(sector))
                    {
                        continue;
                    }

                    MapWhoNode node = map.GetSector(sector);
                    if (firstLayer == layer)
                    {
                        ScanSector(node);
                    }
                    else
                    {
                        for (MapWhoEntry? entry = map.GetFirstEntry(node); entry is not null; entry = map.GetNextEntry())
                        {
                            CheckCandidate(entry.Thing.Seeker);
                        }
                    }
                }
            }
        }
    }

    /// <summary>CheckCollision (00480c90).</summary>
    public void CheckCollision(CollisionSeekingThing other)
    {
        ChecksDone++;
        if (other.SectorDistance(thing.ForThing.MapWhoEntry.SectorIn) > 1)
        {
            Trace?.Invoke(thing, other, "out of reach");
            _checkingCollision = true;
            return;
        }

        FVector delta = other.ForThing.GetCentrePos() - thing.ForThing.GetCentrePos();
        float radius = thing.Radius + other.Radius;
        float distanceSquared = delta.MagnitudeSq();
        Trace?.Invoke(thing, other, $"distance^2 {distanceSquared} radius {radius}");
        if (distanceSquared - (radius * radius) < 0.0)
        {
            if (((thing.CollisionFlags & 0x100) != 0 || (other.CollisionFlags & 0x100) != 0)
                && (!thing.Seeks(other) || !other.Seeks(thing)))
            {
                _checkingCollision = true;
                return;
            }

            thing.Collide(other);
        }

        _checkingCollision = true;
        ScheduleCollisionCheck(other);
    }

    /// <summary>CheckCandidate (00480db0).</summary>
    public void CheckCandidate(CollisionSeekingThing? other)
    {
        if (other is not null && !ReferenceEquals(other, thing) && thing.Seeks(other) && other.Seeks(thing))
        {
            if (_checkingCollision)
            {
                Context.Skip("CHLCollisionDetector: \"WARNING: Unexpected collision check already done\" (a log line)");
            }
            else
            {
                ScheduleCollisionCheck(other);
                _checkingCollision = false;
            }
        }
    }

    /// <summary>ScanSector (00480e10): the sector's children first (depth first), then its own entries.</summary>
    public void ScanSector(MapWhoNode node)
    {
        if (node.Children is { } children)
        {
            ScanSector(children[0]!);
            ScanSector(children[1]!);
            ScanSector(children[2]!);
            ScanSector(children[3]!);
        }

        MapWho map = Context.MapWho;
        for (MapWhoEntry? entry = map.GetFirstEntry(node); entry is not null; entry = map.GetNextEntry())
        {
            if (entry.Thing.Seeker is { } other)
            {
                CheckCandidate(other);
            }
        }
    }

    /// <summary>
    /// ScheduleCollisionCheck (00480ed0): apart, the check is due when the gap could close at the two top speeds (never,
    /// when neither moves); otherwise now, or next frame while a check is in progress.
    /// </summary>
    public void ScheduleCollisionCheck(CollisionSeekingThing other)
    {
        if (DeletionCount > 950)
        {
            return;
        }

        FVector delta = other.ForThing.GetCentrePos() - thing.ForThing.GetCentrePos();
        float distance = delta.Magnitude() - (thing.Radius + other.Radius);
        EventManager events = Context.Events;
        if (distance > 0.0f)
        {
            float otherSpeed = other.ForThing.GetMaxVelocity();
            float speed = otherSpeed + thing.ForThing.GetMaxVelocity();
            if (speed == 0.0f)
            {
                Trace?.Invoke(thing, other, "never (neither moves)");
                return;
            }

            float time = events.Time + (distance / speed);
            Trace?.Invoke(thing, other, $"schedule at {time} (distance {distance} speed {speed})");
            if (time > events.Time)
            {
                events.AddEvent(CheckEvent, this, time, EventPriority.MiddleOfFrame, other, _event);
                _event = null;
                return;
            }
        }

        if (!_checkingCollision)
        {
            CheckCollision(other);
        }
        else
        {
            events.AddEvent(CheckEvent, this, EventManager.NextFrame, EventPriority.MiddleOfFrame, other, _event);
            _event = null;
        }
    }

    /// <summary>MovedSector (00481060): the sectors newly within reach at each layer down from the new one are scanned.</summary>
    public void MovedSector(Sector from, Sector to)
    {
        Sector oldSector = from;
        Sector newSector = to;
        MapWho map = Context.MapWho;
        for (int layer = to.Layer; layer >= 0; layer--)
        {
            if (newSector != oldSector)
            {
                for (int x = -1; x < 2; x++)
                {
                    for (int y = -1; y < 2; y++)
                    {
                        var sector = new Sector(newSector.X + x, newSector.Y + y, layer);
                        if (MapWho.IsValidSector(sector)
                            && (Math.Abs(sector.X - oldSector.X) > 1 || Math.Abs(sector.Y - oldSector.Y) > 1))
                        {
                            MapWhoNode node = map.GetSector(sector);
                            if (layer == to.Layer)
                            {
                                ScanSector(node);
                            }
                            else
                            {
                                for (MapWhoEntry? entry = map.GetFirstEntry(node); entry is not null; entry = map.GetNextEntry())
                                {
                                    CheckCandidate(entry.Thing.Seeker);
                                }
                            }
                        }
                    }
                }
            }

            if (layer != 0)
            {
                newSector = newSector.ToParent();
                oldSector = oldSector.ToParent();
            }
        }
    }

    /// <summary>HandleEvent (004812d0): a scheduled check, the event kept for re-use by its rescheduling.</summary>
    public override void HandleEvent(Event e)
    {
        if (e.EventNum == CheckEvent && e is ScheduledEvent scheduled && scheduled.Data is CollisionSeekingThing other)
        {
            _event = scheduled;
            CheckCollision(other);
            _checkingCollision = false;
            _event = null;
        }
    }
}
