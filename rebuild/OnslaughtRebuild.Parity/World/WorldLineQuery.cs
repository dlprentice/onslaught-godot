// SPDX-License-Identifier: GPL-3.0-only
// CWorld::FindFirstThingToHitLine, adapted from bea-decomp src/world.cpp (0050b030).

using OnslaughtRebuild.Parity.Collision;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.World;

public enum LineHitType
{
    Nothing = 0,
    Ground = 1,
    Thing = 2,
}

/// <summary>CWorldLineColReport: the thing hit, its part, what was hit and the distance (-1 when nothing).</summary>
public sealed class WorldLineReport
{
    public Thing? HitThing;
    public int Part = -1;
    public LineHitType HitType = LineHitType.Nothing;
    public float DistToImpact = -1.0f;
}

public static class WorldLineQuery
{

    /// <summary>
    /// FindFirstThingToHitLine: the ground along the line (CMap's sampled trace), then each thing in the sectors the line
    /// crosses whose seeker collides with lines (flags 0xc0), not ignored, not invisible, not of an ignored type and not
    /// dying (buildings excepted): its approximate shape tested against the line, then, at mesh level, its mesh. The nearest
    /// in front of the ground wins; <paramref name="firstOnly"/> returns at the first accepted thing, setting only the type.
    /// </summary>
    public static LineHitType FindFirstThingToHitLine(ThingContext context, FVector start, FVector end, Thing? ignore,
        WorldLineReport output, bool firstOnly = false, bool meshCollision = true, int level = 2, uint ignoreTypes = 0,
        bool mapFlag = false, uint allowedTypes = uint.MaxValue)
    {
        output.HitType = LineHitType.Nothing;
        var report = new CollisionReport { NumContacts = 0, UnknownA8 = false, Stuck = false, TestTriangles = 0, UnknownD0 = 0 };
        int part = -1;
        float groundDistance = 99999.0f;
        bool hitGround = context.Map.LineHits(start, end, out FVector ground, mapFlag);
        if (hitGround)
        {
            // (Y*Y + Z*Z) + X*X, as VC6 compiles this Magnitude too (retail's distance at C's P136, a ground hit).
            FVector toGround = start - ground;
            groundDistance = MathF.Sqrt(((toGround.Y * toGround.Y) + (toGround.Z * toGround.Z)) + (toGround.X * toGround.X));
            output.HitType = LineHitType.Ground;
            output.DistToImpact = groundDistance;
        }

        var line = new LineVolume(start, end);
        float nearest = groundDistance;
        Thing? hit = null;
        for (MapWhoEntry? entry = context.MapWho.GetFirstEntryWithinLine(start, end); entry is not null;
             entry = context.MapWho.GetNextEntryWithinLine())
        {
            Thing thing = entry.Thing;
            if (thing == ignore || (thing.Flags & ThingFlags.Invisible) != 0 || (thing.ThingTypeBits & ignoreTypes) != 0
                || (thing.ThingTypeBits & allowedTypes) == 0)
            {
                continue;
            }

            CollisionSeekingThing? seeker = thing.Seeker;
            if (seeker is null || (seeker.CollisionFlags & 0xc0) == 0)
            {
                continue;
            }

            if (thing.IsDying && !thing.IsA(ThingType.Building))
            {
                continue;
            }

            FVector position = seeker.Shape.Pos + thing.Pos;
            if (!seeker.Shape.TestAtOffset(line, -position, report))
            {
                continue;
            }

            // (Y*Y + Z*Z) + X*X, as VC6 compiles the function's other two Magnitudes.
            FVector toThing = position - start;
            float distance = MathF.Sqrt(((toThing.Y * toThing.Y) + (toThing.Z * toThing.Z)) + (toThing.X * toThing.X)) - thing.GetBoundingRadius();
            if ((hitGround && !(distance < groundDistance)) || (hit is not null && !(distance < nearest)))
            {
                continue;
            }

            report.ContactParts[0] = -1;
            bool accept = true;
            if ((level == 2 || ((seeker.CollisionFlags & 0xc) == 8 && meshCollision)) && seeker.MeshVolume is { } volume)
            {
                var own = new CollisionMove { Stopped = false, Pos = volume.Pos + thing.Pos };
                var incoming = new CollisionMove { Stopped = false, Pos = new FVector(0, 0, 0) };
                accept = false;
                if (volume.CollideWithLine(ref own, ref incoming, line, report))
                {
                    // VC6 compiles this Magnitude as (Y*Y + Z*Z) + X*X (its product rank order): retail's distance on C's
                    // level 100 tape at P68, a building's mesh, is that sum's root, one ulp from the written order's.
                    FVector toContact = report.ContactPos - start;
                    distance = MathF.Sqrt(((toContact.Y * toContact.Y) + (toContact.Z * toContact.Z)) + (toContact.X * toContact.X));
                    if ((!hitGround || groundDistance >= distance) && distance <= nearest)
                    {
                        accept = true;
                    }
                }
            }

            if (accept)
            {
                if (firstOnly)
                {
                    output.HitType = LineHitType.Thing;
                    return LineHitType.Thing;
                }

                part = report.ContactParts[0];
                hit = thing;
                nearest = distance;
            }
        }

        if (hit is not null)
        {
            output.HitThing = hit;
            output.Part = part;
            output.HitType = LineHitType.Thing;
            output.DistToImpact = nearest;
        }

        return output.HitType;
    }
}
