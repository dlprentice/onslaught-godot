// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/GeneralVolume.h (the volumes' double dispatch), src/Sphere.cpp and Sphere.h (CSphere:
// CollideWithCylinder 004e4d70, CollideWithSphere 004e4e00, Collide 00426320), src/Cylinder.cpp and cylinder.h
// (CCylinder: CollideWithCylinder 0043fe20, the folded slots 004059a0) and src/MeshCollisionVolume.h (Collide 004262e0,
// CollideWithCylinder 004abe50). Reconstructed (matched with order stand-ins); not in Stuart's release.

using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Parity.Collision;

/// <summary>
/// CGeneralVolume: a collision volume, at <see cref="Pos"/> from its thing. Collision is double-dispatched: Collide
/// asks the other volume to test this one, swapping the two movement records; the first movement given to a
/// CollideWith belongs to the receiving volume.
/// </summary>
public abstract class GeneralVolume
{
    /// <summary>mPos: the volume's offset from its thing.</summary>
    public FVector Pos;

    /// <summary>GetType: 0 sphere, 2 cylinder, 3 mesh.</summary>
    public abstract int Type { get; }

    /// <summary>Collide (slot 5): the other volume tests this one, the movements swapped.</summary>
    public abstract bool Collide(ref CollisionMove own, ref CollisionMove move, GeneralVolume other, CollisionReport report);

    public abstract bool CollideWithMesh(ref CollisionMove own, ref CollisionMove move, MeshVolume other, CollisionReport report);

    public abstract bool CollideWithCylinder(ref CollisionMove own, ref CollisionMove move, CylinderVolume other, CollisionReport report);

    public abstract bool CollideWithSphere(ref CollisionMove own, ref CollisionMove move, SphereVolume other, CollisionReport report);

    public abstract bool CollideWithLine(ref CollisionMove own, ref CollisionMove move, LineVolume other, CollisionReport report);

    /// <summary>
    /// TestAtOffset (004780f0, Geometry.cpp): this volume at the origin against <paramref name="other"/> at
    /// <paramref name="offset"/> (only the positions and stopped flags are set; the moves are left as they are).
    /// </summary>
    public bool TestAtOffset(GeneralVolume other, FVector offset, CollisionReport report)
    {
        var own = new CollisionMove { Stopped = false, Pos = new FVector(0, 0, 0) };
        var incoming = new CollisionMove { Stopped = false, Pos = offset };
        return Collide(ref own, ref incoming, other, report);
    }

    /// <summary>FVector's dot product (maths.h): (X*aX + Y*aY) + Z*aZ.</summary>
    protected static float Dot(FVector v, FVector a) => (v.X * a.X) + (v.Y * a.Y) + (v.Z * a.Z);

    /// <summary>MagnitudeSq (maths.h): (X*X + Y*Y) + Z*Z.</summary>
    protected static float MagnitudeSq(FVector v) => (v.X * v.X) + (v.Y * v.Y) + (v.Z * v.Z);

    /// <summary>FVector::Normalise inline: the reciprocal of the length multiplies each axis, unless the length is 0.</summary>
    protected static FVector Normalised(FVector v)
    {
        v.Normalise();
        return v;
    }
}

/// <summary>CSphere.</summary>
public sealed class SphereVolume(float radius) : GeneralVolume
{
    public float Radius { get; } = radius;

    public float RadiusSquared { get; } = radius * radius;

    public override int Type => 0;

    public override bool Collide(ref CollisionMove own, ref CollisionMove move, GeneralVolume other, CollisionReport report) =>
        other.CollideWithSphere(ref move, ref own, this, report);

    /// <summary>Slot 1 is Collide's body (folded at 00426320): the mesh tests the sphere.</summary>
    public override bool CollideWithMesh(ref CollisionMove own, ref CollisionMove move, MeshVolume other, CollisionReport report) =>
        other.CollideWithSphere(ref move, ref own, this, report);

    /// <summary>
    /// CollideWithLine (004e4b90): the line, offset by the movements' difference, starts or ends inside, or its nearest
    /// point within the segment is.
    /// </summary>
    public override bool CollideWithLine(ref CollisionMove own, ref CollisionMove move, LineVolume other, CollisionReport report)
    {
        FVector offset = move.Pos - own.Pos;
        FVector start = offset + other.Start;
        FVector end = offset + other.End;
        if (MagnitudeSq(start) < RadiusSquared || MagnitudeSq(end) < RadiusSquared)
        {
            return true;
        }

        FVector delta = start - end;
        float fraction = Dot(delta, start) / MagnitudeSq(delta);
        if (fraction >= 0.0 && fraction <= 1.0)
        {
            FVector closest = start + ((end - start) * fraction);
            return MagnitudeSq(closest) <= RadiusSquared;
        }

        return false;
    }

    /// <summary>CollideWithCylinder (004e4d70): this sphere as a cylinder of its radius each way.</summary>
    public override bool CollideWithCylinder(ref CollisionMove own, ref CollisionMove move, CylinderVolume other, CollisionReport report) =>
        new CylinderVolume(Radius, Radius).CollideWithCylinder(ref own, ref move, other, report);

    /// <summary>CollideWithSphere (004e4e00): the spheres meet; with the response on, both are rewound to the touch and slide.</summary>
    public override bool CollideWithSphere(ref CollisionMove own, ref CollisionMove move, SphereVolume other, CollisionReport report)
    {
        FVector separation = move.Pos - own.Pos;
        float combinedRadius = Radius + other.Radius;
        bool result = separation.Magnitude() - combinedRadius <= 0;
        if (report.TestTriangles == 0 || !result)
        {
            return result;
        }

        FVector initial = (move.Pos - move.Move) - (own.Pos - own.Move);
        float distanceSquared = (initial.Z * initial.Z) + (initial.Y * initial.Y) + (initial.X * initial.X);
        float radius = Radius;
        radius += other.Radius;
        FVector relativeMove = move.Move - own.Move;
        float initialDistance = MathF.Sqrt(distanceSquared);
        if (initialDistance - radius >= -0.0001f)
        {
            // The relative speed sums X*X, then Z*Z, then Y*Y (004e4fab).
            float speed = MathF.Sqrt((relativeMove.X * relativeMove.X) + (relativeMove.Z * relativeMove.Z) + (relativeMove.Y * relativeMove.Y));
            float distance = -initial.Magnitude();
            float aa = speed * speed;
            float bb = 2 * (speed * distance);
            float fraction = aa == 0
                ? 0
                : (-bb - MathF.Sqrt((bb * bb) - (4 * aa * ((distance * distance) - (combinedRadius * combinedRadius))))) / (2 * aa);
            if (fraction < 0.0)
            {
                fraction = 0;
            }

            move.Pos = move.Pos - (move.Move * (1 - fraction));
            own.Pos = own.Pos - (own.Move * (1 - fraction));
            FVector normal = move.Pos - own.Pos;
            normal.Z = 0;
            normal = Normalised(normal);

            // normal . move: the X, Z pair, then Y (004e5169).
            float dot = ((normal.X * move.Move.X) + (normal.Z * move.Move.Z)) + (normal.Y * move.Move.Y);
            if (dot < 0)
            {
                move.Move = move.Move + (normal * -dot);
            }

            move.Pos = move.Pos + move.Move;

            // normal . own move: the Y, Z pair, then X (004e5217).
            dot = ((normal.Y * own.Move.Y) + (normal.Z * own.Move.Z)) + (normal.X * own.Move.X);
            if (dot > 0)
            {
                own.Move = own.Move + (normal * -dot);
            }

            own.Pos = own.Pos + own.Move;
        }
        else
        {
            FVector normal = initial;
            float distance = normal.Magnitude();
            normal = Normalised(normal);
            float overlap = Radius + other.Radius - distance;
            if (overlap < 0.0)
            {
                return false;
            }

            float halfOverlap = overlap * 0.5f;
            FVector otherCorrection = normal * halfOverlap;
            FVector ownCorrection = -(normal * halfOverlap);
            otherCorrection.Z = 0;
            ownCorrection.Z = 0;
            move.Pos = move.Pos - move.Move;
            move.Pos = move.Pos + otherCorrection;
            own.Pos = own.Pos - own.Move;
            own.Pos = own.Pos + ownCorrection;
        }

        own.Stopped = true;
        move.Stopped = true;
        return true;
    }
}

/// <summary>CCylinder: a radius and a half height about its centre.</summary>
public sealed class CylinderVolume(float radius, float halfHeight) : GeneralVolume
{
    public float Radius { get; } = radius;

    public float RadiusSquared { get; } = radius * radius;

    public float Height { get; } = halfHeight;

    public override int Type => 2;

    /// <summary>Slots 1, 3 and 5 are one body (004059a0): the other volume tests this cylinder, the movements swapped.</summary>
    public override bool Collide(ref CollisionMove own, ref CollisionMove move, GeneralVolume other, CollisionReport report) =>
        other.CollideWithCylinder(ref move, ref own, this, report);

    public override bool CollideWithMesh(ref CollisionMove own, ref CollisionMove move, MeshVolume other, CollisionReport report) =>
        other.CollideWithCylinder(ref move, ref own, this, report);

    public override bool CollideWithSphere(ref CollisionMove own, ref CollisionMove move, SphereVolume other, CollisionReport report) =>
        other.CollideWithCylinder(ref move, ref own, this, report);

    /// <summary>
    /// CollideWithLine (Cylinder.cpp): the line, offset by the movements' difference, clear of the cylinder's height band or
    /// its radius misses; otherwise, when an end lies beyond the band or a response is asked for, the entry and exit
    /// points along it decide, and a response moves the line's movement to the entry.
    /// </summary>
    public override bool CollideWithLine(ref CollisionMove own, ref CollisionMove move, LineVolume other, CollisionReport report)
    {
        FVector offset = move.Pos - own.Pos;
        FVector start = offset + other.Start;
        FVector end = offset + other.End;
        if ((start.Z > Height && end.Z > Height) || (start.Z < -Height && end.Z < -Height))
        {
            return false;
        }

        FVector delta = start - end;
        if (MagnitudeSq(start) > RadiusSquared && MagnitudeSq(end) > RadiusSquared)
        {
            float fraction = Dot(delta, start) / MagnitudeSq(delta);
            if (fraction < 0.0 || fraction > 1.0)
            {
                return false;
            }

            FVector closest = start + ((end - start) * fraction);
            if ((closest.X * closest.X) + (closest.Y * closest.Y) > RadiusSquared)
            {
                return false;
            }
        }

        if (MathF.Abs(start.Z) > Height || MathF.Abs(end.Z) > Height || report.TestTriangles != 0)
        {
            float speed = (end - start).Magnitude();
            float distance = -start.Magnitude();
            float aa = speed * speed;
            float bb = 2 * (speed * distance);
            float root = MathF.Sqrt((bb * bb) - (4 * aa * ((distance * distance) - RadiusSquared)));
            float firstFraction = Math.Clamp((-bb - root) / (2 * aa), 0.0f, 1.0f);
            float lastFraction = Math.Clamp((-bb + root) / (2 * aa), 0.0f, 1.0f);
            FVector first = start + (-delta * firstFraction);
            FVector last = start + (-delta * lastFraction);
            if ((first.Z > Height && last.Z > Height) || (first.Z < -Height && last.Z < -Height))
            {
                return false;
            }

            if (report.TestTriangles != 0)
            {
                move.Pos = move.Pos + (first - offset);
                move.Stopped = true;
            }
        }

        return true;
    }

    /// <summary>
    /// CollideWithCylinder (0043fe20): they meet when the radial gap and the vertical gap both close. With the response
    /// on, a pair still approaching is rewound to the touch (along the axis whose gap was larger) and the approach is
    /// taken out of both moves; a pair that started overlapping is pushed apart sideways by half the (negative) gap each.
    /// </summary>
    public override bool CollideWithCylinder(ref CollisionMove own, ref CollisionMove move, CylinderVolume other, CollisionReport report)
    {
        FVector separation = move.Pos - own.Pos;
        float radius = other.Radius + Radius;
        float height = other.Height + Height;
        bool result = (float)Math.Sqrt((separation.X * separation.X) + (separation.Y * separation.Y)) - radius <= 0
            && MathF.Abs(move.Pos.Z - own.Pos.Z) <= height;
        if (report is null || report.TestTriangles == 0 || !result)
        {
            return result;
        }

        FVector ownStart = own.Pos - own.Move;
        FVector otherStart = move.Pos - move.Move;
        FVector initial = otherStart - ownStart;
        bool vertical = false;
        float verticalDistance = initial.Z - height;
        float radialLength = MathF.Sqrt((initial.X * initial.X) + (initial.Y * initial.Y));
        float radialDistance = radialLength - radius;
        float distance;
        float approach;
        if (verticalDistance > radialDistance)
        {
            distance = verticalDistance;
            vertical = true;
            FVector relative = move.Move - own.Move;
            approach = (relative.X * 0.0f) + (relative.Y * 0.0f) + (relative.Z * initial.Z);
        }
        else
        {
            distance = radialDistance;
            FVector relative = move.Move - own.Move;
            approach = (relative.X * initial.X) + (relative.Y * initial.Y) + (0.0f * initial.Z);
        }

        if (approach > 0)
        {
            return false;
        }

        FVector relativeMove = move.Move - own.Move;
        if (distance >= -0.0001f)
        {
            float initialDistance;
            float cc;
            if (vertical)
            {
                initialDistance = -initial.Z;
                cc = (initialDistance * initialDistance) - (height * height);
            }
            else
            {
                initialDistance = -radialLength;
                cc = (initialDistance * initialDistance) - (radius * radius);
            }

            float speed = MathF.Sqrt((relativeMove.X * relativeMove.X) + (relativeMove.Y * relativeMove.Y));
            float aa = speed * speed;
            float bb = 2 * speed * initialDistance;
            float fraction = aa == 0 ? 0 : (-bb - MathF.Sqrt((bb * bb) - (4 * aa * cc))) / (2 * aa);
            if (fraction < 0.0)
            {
                fraction = 0;
            }

            float back = 1 - fraction;
            move.Pos = move.Pos - (move.Move * back);
            own.Pos = own.Pos - (own.Move * back);
            FVector normal = vertical
                ? new FVector(0, 0, move.Pos.Z - own.Pos.Z)
                : new FVector(own.Pos.X - move.Pos.X, own.Pos.Y - move.Pos.Y, own.Pos.Z - own.Pos.Z);
            normal = Normalised(normal);
            float dot = (normal.X * move.Move.X) + (normal.Y * move.Move.Y) + (normal.Z * move.Move.Z);
            report.Normals[0] = normal;
            if (dot > 0)
            {
                move.Move = move.Move + (normal * -dot);
            }

            move.Pos = move.Pos + move.Move;
            dot = (normal.X * own.Move.X) + (normal.Y * own.Move.Y) + (normal.Z * own.Move.Z);
            if (dot < 0)
            {
                own.Move = own.Move + (normal * -dot);
            }

            own.Pos = own.Pos + own.Move;
        }
        else
        {
            FVector normal = Normalised(initial);
            float halfDistance = distance * 0.5f;
            FVector otherCorrection = -(normal * halfDistance);
            FVector ownCorrection = normal * halfDistance;
            otherCorrection.Z = 0;
            ownCorrection.Z = 0;
            move.Pos = move.Pos + otherCorrection;
            own.Pos = own.Pos + ownCorrection;
        }

        own.Stopped = true;
        move.Stopped = true;
        return true;
    }
}

/// <summary>CMeshCollisionVolume as a volume (its tests are <see cref="MeshCollisionVolume"/>'s).</summary>
public sealed class MeshVolume(MeshCollisionVolume mesh) : GeneralVolume
{
    public MeshCollisionVolume Mesh { get; } = mesh;

    public override int Type => 3;

    /// <summary>Collide (004262e0): the other volume's slot 1 with this mesh, the movements swapped.</summary>
    public override bool Collide(ref CollisionMove own, ref CollisionMove move, GeneralVolume other, CollisionReport report) =>
        other.CollideWithMesh(ref move, ref own, this, report);

    public override bool CollideWithMesh(ref CollisionMove own, ref CollisionMove move, MeshVolume other, CollisionReport report) =>
        throw new NotSupportedException("mesh against mesh (Unknown6 swaps one for its shape first)");

    /// <summary>CollideWithCylinder (004abe50): the sphere of the larger of the cylinder's radius and half height.</summary>
    public override bool CollideWithCylinder(ref CollisionMove own, ref CollisionMove move, CylinderVolume other, CollisionReport report) =>
        Mesh.CollideWithSphere(ref move, other.Radius <= other.Height ? other.Height : other.Radius, report);

    public override bool CollideWithSphere(ref CollisionMove own, ref CollisionMove move, SphereVolume other, CollisionReport report) =>
        Mesh.CollideWithSphere(ref move, other.Radius, report);

    /// <summary>CollideWithLine (004ad830): the line in each collision part.</summary>
    public override bool CollideWithLine(ref CollisionMove own, ref CollisionMove move, LineVolume other, CollisionReport report) =>
        Mesh.CollideWithLine(ref move, other.Start, other.End, report);
}

/// <summary>CLine: a segment from <see cref="Start"/> to <see cref="End"/> (relative to its movement's position).</summary>
public sealed class LineVolume(FVector start, FVector end) : GeneralVolume
{
    public FVector Start { get; } = start;

    public FVector End { get; } = end;

    public override int Type => 1;

    /// <summary>Collide and the CollideWith slots (004098c0): the other volume tests this line, the movements swapped.</summary>
    public override bool Collide(ref CollisionMove own, ref CollisionMove move, GeneralVolume other, CollisionReport report) =>
        other.CollideWithLine(ref move, ref own, this, report);

    public override bool CollideWithMesh(ref CollisionMove own, ref CollisionMove move, MeshVolume other, CollisionReport report) =>
        other.CollideWithLine(ref move, ref own, this, report);

    public override bool CollideWithCylinder(ref CollisionMove own, ref CollisionMove move, CylinderVolume other, CollisionReport report) =>
        other.CollideWithLine(ref move, ref own, this, report);

    public override bool CollideWithSphere(ref CollisionMove own, ref CollisionMove move, SphereVolume other, CollisionReport report) =>
        other.CollideWithLine(ref move, ref own, this, report);

    /// <summary>CollideWithLine (004098b0): two lines never meet.</summary>
    public override bool CollideWithLine(ref CollisionMove own, ref CollisionMove move, LineVolume other, CollisionReport report) => false;
}
