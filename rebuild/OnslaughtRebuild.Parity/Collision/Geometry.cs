// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/Geometry.cpp (ClipSegmentAgainstAABB3D 00478160, TestSweptSphereAgainstTriangle 00478510,
// TestSegmentAgainstTriangle 00478c20, PointInsideTrianglePrism 00479020, SelectClosestPointOnTriangleEdges 00479200,
// RaySphereEntryDistance 00479630, DistanceOutsideBox 00479770) and src/maths.h (FVector's out-of-line members: dot
// 0040d180, Magnitude 004026b0, MagnitudeSq 00477ba0, Normalise 00406d50), reconstructed; not in Stuart's release.
// Every float operation rounds to 24 bits; the order of each sum of three terms is the one compiled at its site, read
// from the Steam BEA.exe (= the pristine specimen 74154bfa) and checked by running the retail functions (the retail
// oracle, rebuild/tools/retail-oracle).

using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Collision;

/// <summary>Retail's triangle, segment, sphere and box tests.</summary>
public static class Geometry
{
    /// <summary>FVector::operator*(const FVector &amp;) out of line (0040d180): (Z + Y) + X.</summary>
    public static float DotOutOfLine(FVector a, FVector b) => (a.Z * b.Z) + (a.Y * b.Y) + (a.X * b.X);

    /// <summary>FVector::MagnitudeSq out of line (00477ba0): (X*X + Y*Y) + Z*Z.</summary>
    public static float MagnitudeSqOutOfLine(FVector v) => (v.X * v.X) + (v.Y * v.Y) + (v.Z * v.Z);

    /// <summary>FVector::Magnitude out of line (004026b0): fsqrt((X*X + Y*Y) + Z*Z).</summary>
    public static float MagnitudeOutOfLine(FVector v) => (float)X87.Sqrt((v.X * v.X) + (v.Y * v.Y) + (v.Z * v.Z));

    /// <summary>FVector::Normalise out of line (00406d50): times the reciprocal of (X*X + Y*Y) + Z*Z's root, unless 0.</summary>
    public static FVector NormaliseOutOfLine(FVector v)
    {
        float m = MagnitudeOutOfLine(v);
        if (m == 0.0f)
        {
            return v;
        }

        float r = 1.0f / m;
        return new FVector(v.X * r, v.Y * r, v.Z * r);
    }

    private static FVector Sub(FVector a, FVector b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static FVector Add(FVector a, FVector b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    private static FVector Scale(FVector v, float s) => new(v.X * s, v.Y * s, v.Z * s);

    private static FVector Cross(FVector v, FVector a) =>
        new((v.Y * a.Z) - (v.Z * a.Y), (v.Z * a.X) - (a.Z * v.X), (a.Y * v.X) - (v.Y * a.X));

    /// <summary>The outcode of a point against the bounds (min X, min Y, max X, max Y, min Z, max Z).</summary>
    private static int OutCode(float x, float y, float z, float[] b)
    {
        int code = 0;
        if (y > b[3])
        {
            code |= 1;
        }
        else if (y < b[1])
        {
            code |= 2;
        }

        if (x > b[2])
        {
            code |= 4;
        }
        else if (x < b[0])
        {
            code |= 8;
        }

        if (z > b[5])
        {
            code |= 16;
        }
        else if (z < b[4])
        {
            code |= 32;
        }

        return code;
    }

    /// <summary>
    /// ClipSegmentAgainstAABB3D (00478160): Cohen-Sutherland in three dimensions; the ends are moved onto the box. False
    /// when the segment misses it.
    /// </summary>
    public static bool ClipSegmentAgainstAABB3D(ref FVector start, ref FVector end, float[] bounds)
    {
        int first = OutCode(start.X, start.Y, start.Z, bounds);
        int second = OutCode(end.X, end.Y, end.Z, bounds);
        while ((first | second) != 0)
        {
            if ((first & second) != 0)
            {
                return false;
            }

            int code = first != 0 ? first : second;
            float x, y, z;
            if ((code & 1) != 0)
            {
                float fraction = (bounds[3] - start.Y) / (end.Y - start.Y);
                x = start.X + ((end.X - start.X) * fraction);
                z = start.Z + ((end.Z - start.Z) * fraction);
                y = bounds[3];
            }
            else if ((code & 2) != 0)
            {
                float fraction = (bounds[1] - start.Y) / (end.Y - start.Y);
                x = start.X + ((end.X - start.X) * fraction);
                z = start.Z + ((end.Z - start.Z) * fraction);
                y = bounds[1];
            }
            else if ((code & 4) != 0)
            {
                x = bounds[2];
                float fraction = (bounds[2] - start.X) / (end.X - start.X);
                y = start.Y + ((end.Y - start.Y) * fraction);
                z = start.Z + ((end.Z - start.Z) * fraction);
            }
            else if ((code & 8) != 0)
            {
                x = bounds[0];
                float fraction = (bounds[0] - start.X) / (end.X - start.X);
                y = start.Y + ((end.Y - start.Y) * fraction);
                z = start.Z + ((end.Z - start.Z) * fraction);
            }
            else if ((code & 16) != 0)
            {
                z = bounds[5];
                float fraction = (bounds[5] - start.Z) / (end.Z - start.Z);
                x = start.X + ((end.X - start.X) * fraction);
                y = start.Y + ((end.Y - start.Y) * fraction);
            }
            else
            {
                z = bounds[4];
                float fraction = (bounds[4] - start.Z) / (end.Z - start.Z);
                x = start.X + ((end.X - start.X) * fraction);
                y = start.Y + ((end.Y - start.Y) * fraction);
            }

            if (code == first)
            {
                start = new FVector(x, y, z);
                first = OutCode(start.X, start.Y, start.Z, bounds);
            }
            else
            {
                end = new FVector(x, y, z);
                second = OutCode(end.X, end.Y, end.Z, bounds);
            }
        }

        return true;
    }

    /// <summary>
    /// The plane-distance calculation both swept-sphere paths expand: through the out-of-line dot, d = (-normal) . point,
    /// then (normal . start) + d over normal . direction, negated; -1 when the direction is parallel to the plane.
    /// </summary>
    private static float TrianglePlaneDistance(FVector normal, FVector point, FVector start, FVector direction)
    {
        float distance = DotOutOfLine(new FVector(-normal.X, -normal.Y, -normal.Z), point);
        distance = DotOutOfLine(normal, start) + distance;
        float denominator = DotOutOfLine(normal, direction);
        if (denominator == 0)
        {
            return -1;
        }

        return -(distance / denominator);
    }

    /// <summary>The inline dots at 00478510: (Y + Z) + X.</summary>
    private static float DotYZX(FVector n, FVector v) => (n.Y * v.Y) + (n.Z * v.Z) + (n.X * v.X);

    /// <summary>
    /// TestSweptSphereAgainstTriangle (00478510): a sphere of the radius at pos moving by move against the triangle: the
    /// distance along the move to the contact, the contact point and the normal into the report (when nearer than one it
    /// already has, or overlapping, which marks it stuck).
    /// </summary>
    public static bool TestSweptSphereAgainstTriangle(FVector a, FVector b, FVector c, FVector pos, FVector move,
        float radius, CollisionReport report)
    {
        bool overlapping = false;
        bool valid = true;
        FVector normal = Cross(Sub(b, a), Sub(c, a));
        float m = (float)X87.Sqrt((normal.Z * normal.Z) + (normal.Y * normal.Y) + (normal.X * normal.X));
        if (m != 0.0f)
        {
            float r = 1.0f / m;
            normal = new FVector(normal.X * r, normal.Y * r, normal.Z * r);
        }

        if (DotYZX(normal, move) >= 0.01f)
        {
            return false;
        }

        FVector fromStart = Sub(a, pos);
        if (DotYZX(normal, fromStart) >= 0.01f)
        {
            return false;
        }

        FVector front = Sub(pos, Scale(normal, radius));
        FVector fromFront = Sub(a, front);
        float distance;
        FVector projected;
        if (DotYZX(normal, fromFront) > 0.0001f)
        {
            distance = TrianglePlaneDistance(normal, a, front, normal);
            projected = Add(front, Scale(normal, distance));
        }
        else
        {
            FVector direction = move;
            float dm = (float)X87.Sqrt((direction.X * direction.X) + (direction.Z * direction.Z) + (direction.Y * direction.Y));
            if (dm != 0.0f)
            {
                float r = 1.0f / dm;
                direction = new FVector(direction.X * r, direction.Y * r, direction.Z * r);
            }

            distance = TrianglePlaneDistance(normal, a, front, direction);
            projected = Add(front, Scale(direction, distance));
            if (distance * distance > MagnitudeSqOutOfLine(move))
            {
                return false;
            }
        }

        if (Math.Abs(distance) < 0.0001)
        {
            distance = 0;
        }

        float contactDistance = distance;
        FVector contact = projected;
        if (!PointInsideTrianglePrism(projected, a, b, c, normal))
        {
            contact = SelectClosestPointOnTriangleEdges(a, b, c, projected);
            FVector rayStart = Sub(contact, pos);
            FVector rayEnd = Sub(Sub(contact, move), pos);
            contactDistance = RaySphereEntryDistance(rayStart, rayEnd, radius);
        }

        if (MagnitudeOutOfLine(Sub(contact, pos)) - radius < -0.0001f)
        {
            report.Stuck = true;
            overlapping = true;
        }
        else if (contactDistance < -0.0001 || MagnitudeOutOfLine(move) < contactDistance)
        {
            valid = false;
        }

        if (valid && report.UnknownA8 && contactDistance >= report.Distance && !overlapping)
        {
            valid = false;
        }

        if (!valid)
        {
            return false;
        }

        report.Distance = contactDistance;
        report.UnknownA8 = true;
        report.Unknown94 = true;
        report.ContactPos = contact;
        report.TriangleNormal = normal;
        return true;
    }

    /// <summary>
    /// TestSegmentAgainstTriangle (00478c20): the segment against the triangle's front face: the fraction along it, the
    /// contact and the normal into the report (when nearer than one it already has). Orders as compiled: the normal's
    /// square sum (X*X + Y*Y) + Z*Z, the facing test (X + Z) + Y, the approach X + (Y + Z), the plane (-X*aX - Y*aY) -
    /// Z*aZ and the hit ((plane + X) + Z) + Y.
    /// </summary>
    public static bool TestSegmentAgainstTriangle(FVector a, FVector b, FVector c, FVector start, FVector end,
        CollisionReport? report)
    {
        var normal = new FVector(
            ((b.Y - a.Y) * (c.Z - a.Z)) - ((b.Z - a.Z) * (c.Y - a.Y)),
            ((b.Z - a.Z) * (c.X - a.X)) - ((b.X - a.X) * (c.Z - a.Z)),
            ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X)));
        float m = (float)X87.Sqrt((normal.X * normal.X) + (normal.Y * normal.Y) + (normal.Z * normal.Z));
        if (m != 0.0f)
        {
            m = 1.0f / m;
            normal = new FVector(normal.X * m, normal.Y * m, normal.Z * m);
        }

        float dx = end.X - start.X;
        float dy = end.Y - start.Y;
        float dz = end.Z - start.Z;
        float facing = (normal.X * dx) + (normal.Z * dz) + (normal.Y * dy);
        if (facing > 0.0)
        {
            return false;
        }

        float approach = (normal.X * dx) + ((normal.Y * dy) + (normal.Z * dz));
        if (MathF.Abs(approach) < 0.005f)
        {
            return false;
        }

        float plane = (-normal.X * a.X) - (normal.Y * a.Y) - (normal.Z * a.Z);
        float hit = plane + (normal.X * start.X) + (normal.Z * start.Z) + (normal.Y * start.Y);
        float distance = -(hit / approach);
        if (distance < 0 || distance > 1)
        {
            return false;
        }

        if (report is not null && report.UnknownA8 && distance > report.Distance)
        {
            return false;
        }

        var contact = new FVector(start.X + (dx * distance), start.Y + (dy * distance), start.Z + (dz * distance));
        if (!PointInsideTrianglePrism(contact, a, b, c, normal))
        {
            return false;
        }

        if (report is not null)
        {
            report.Distance = distance;
            report.UnknownA8 = true;
            report.Unknown94 = true;
            report.ContactPos = contact;
            report.TriangleNormal = normal;
        }

        return true;
    }

    /// <summary>
    /// PointInsideTrianglePrism (00479020): the point is inside the triangle's prism when each edge's cross product with
    /// the point's offset faces along the normal: the first two dots (Y + Z) + X, the third the out-of-line dot.
    /// </summary>
    public static bool PointInsideTrianglePrism(FVector point, FVector a, FVector b, FVector c, FVector normal)
    {
        FVector crossa = Cross(Sub(a, point), Sub(b, a));
        if ((normal.Y * crossa.Y) + (normal.Z * crossa.Z) + (normal.X * crossa.X) < 0.0)
        {
            return false;
        }

        FVector crossb = Cross(Sub(b, point), Sub(c, b));
        if ((normal.Y * crossb.Y) + (normal.Z * crossb.Z) + (normal.X * crossb.X) < 0.0)
        {
            return false;
        }

        FVector crossc = Cross(Sub(c, point), Sub(a, c));
        return !(DotOutOfLine(normal, crossc) < 0.0);
    }

    /// <summary>The closest point to a point on the segment (expanded three times into the edge selection, out-of-line operators).</summary>
    private static FVector ClosestPointOnSegment(FVector start, FVector end, FVector point)
    {
        FVector offset = Sub(point, start);
        FVector direction = Sub(end, start);
        float lengthSq = MagnitudeSqOutOfLine(direction);
        direction = NormaliseOutOfLine(direction);
        float projection = DotOutOfLine(direction, offset);
        if (projection < 0)
        {
            return start;
        }

        if (projection * projection > lengthSq)
        {
            return end;
        }

        return Add(start, Scale(direction, projection));
    }

    /// <summary>
    /// SelectClosestPointOnTriangleEdges (00479200): the nearest of the edges' closest points (B to A, A to C, C to B;
    /// a tie keeps the earlier edge). The second and first squared distances are inline, (Z*Z + Y*Y) + X*X; the third
    /// is the out-of-line MagnitudeSq.
    /// </summary>
    public static FVector SelectClosestPointOnTriangleEdges(FVector a, FVector b, FVector c, FVector point)
    {
        FVector first = ClosestPointOnSegment(b, a, point);
        FVector second = ClosestPointOnSegment(a, c, point);
        FVector third = ClosestPointOnSegment(c, b, point);
        FVector delta = Sub(point, first);
        FVector toSecond = Sub(point, second);
        float secondDistanceSq = (toSecond.Z * toSecond.Z) + (toSecond.Y * toSecond.Y) + (toSecond.X * toSecond.X);
        float thirdDistanceSq = MagnitudeSqOutOfLine(Sub(point, third));
        float bestDistanceSq = (delta.Z * delta.Z) + (delta.Y * delta.Y) + (delta.X * delta.X);
        FVector result = first;
        if (secondDistanceSq < bestDistanceSq)
        {
            result = second;
            bestDistanceSq = secondDistanceSq;
        }

        if (thirdDistanceSq < bestDistanceSq)
        {
            result = third;
        }

        return result;
    }

    /// <summary>
    /// RaySphereEntryDistance (00479630): how far along the ray from start towards end the sphere of the radius about the
    /// origin is entered; -1 for a miss; inside, the radius less the start's distance. Orders as compiled (all inline):
    /// the direction's square sum (Y*Y + Z*Z) + X*X, the projection (Z + Y) + X, the centre's square sum and the start's
    /// magnitude (Z*Z + Y*Y) + X*X.
    /// </summary>
    public static float RaySphereEntryDistance(FVector start, FVector end, float radius)
    {
        FVector direction = Sub(end, start);
        float m = (float)X87.Sqrt((direction.Y * direction.Y) + (direction.Z * direction.Z) + (direction.X * direction.X));
        if (m != 0.0f)
        {
            float r = 1.0f / m;
            direction = new FVector(direction.X * r, direction.Y * r, direction.Z * r);
        }

        var toCentre = new FVector(-start.X, -start.Y, -start.Z);
        float projection = (toCentre.Z * direction.Z) + (toCentre.Y * direction.Y) + (toCentre.X * direction.X);
        float centreSq = (toCentre.Z * toCentre.Z) + (toCentre.Y * toCentre.Y) + (toCentre.X * toCentre.X);
        float discriminant = (radius * radius) - (centreSq - (projection * projection));
        if (discriminant < 0)
        {
            return -1;
        }

        float root = (float)X87.Sqrt(discriminant);
        float distance = projection - root;
        if (distance < 0)
        {
            if (root + projection > 0)
            {
                return radius - (float)X87.Sqrt((start.Z * start.Z) + (start.Y * start.Y) + (start.X * start.X));
            }

            return -1;
        }

        return distance;
    }

    /// <summary>
    /// DistanceOutsideBox (00479770): how far a point is outside a box of the half-extents about the origin (0 inside).
    /// Retail's three-axes case adds z twice instead of squaring it (004798a0..004798a7).
    /// </summary>
    public static float DistanceOutsideBox(FVector pos, FVector extent)
    {
        int outside = 0;
        float x = MathF.Abs(pos.X) - MathF.Abs(extent.X);
        if (x > 0)
        {
            outside |= 1;
        }

        float y = MathF.Abs(pos.Y) - MathF.Abs(extent.Y);
        if (y > 0)
        {
            outside |= 2;
        }

        float z = MathF.Abs(pos.Z) - MathF.Abs(extent.Z);
        if (z > 0)
        {
            outside |= 4;
        }

        return outside switch
        {
            0 => 0,
            1 => x,
            2 => y,
            4 => z,
            3 => (float)X87.Sqrt((x * x) + (y * y)),
            5 => (float)X87.Sqrt((x * x) + (z * z)),
            6 => (float)X87.Sqrt((y * y) + (z * z)),
            _ => (float)X87.Sqrt((x * x) + (y * y) + (z + z)),
        };
    }
}
