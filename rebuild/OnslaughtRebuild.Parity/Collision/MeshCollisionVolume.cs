// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/MeshCollisionVolume.cpp and MeshCollisionVolume.h (CMeshCollisionVolume: CollideWithCylinder
// 004abe50, InitBoxTriangles 004ac000, TestSweptSphereAgainstBox 004ac140, TestSweptSphereAgainstPart 004ac4a0,
// CollideWithSphere 004ac6e0, ResolveContact 004acf30, CachePartTransform 004ad600, CollideWithLine 004ad830) and
// src/MeshPart.cpp (GetFirstPoly 004ae110 and its siblings), reconstructed; not in Stuart's release.

using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Collision;

/// <summary>What the mesh volume asks of its render thing: the mesh, the pose the transforms come from, and the time.</summary>
public interface IMeshCollisionSource
{
    /// <summary>GetRTMesh: null when the render thing has none.</summary>
    MeshData? Mesh { get; }

    /// <summary>The render thing's pose cache (CMeshPart::EvaluatePose reads it).</summary>
    MeshPose? Pose { get; }

    /// <summary>The thing the render thing draws (mThing).</summary>
    IRenderSubject Subject { get; }

    /// <summary>EVENT_MANAGER.GetTime().</summary>
    float Time { get; }

    /// <summary>The render frame number EvaluatePose is given.</summary>
    int RenderFrameNumber { get; }

    /// <summary>The simulation's poly bucket statics.</summary>
    PolyBucketStatics PolyBuckets { get; }
}

/// <summary>
/// CMeshCollisionVolume: a collision volume made of a render thing's mesh. Each collision part (type 1, at its current
/// damage level) is tested in its own frame, from transforms cached per part and refreshed when stale (never, once
/// cached, when the transforms are fixed). Motion controllers are not ported: a thing that has one is refused.
/// </summary>
public sealed class MeshCollisionVolume(IMeshCollisionSource? source, bool useFixedTransforms, bool ignoreAnimation)
{
    /// <summary>A part's cached transform (0x74 bytes): the orientation, its transpose, the position and its time (-1: none).</summary>
    public sealed class PartTransform
    {
        public FMatrix Orientation;
        public FMatrix Inverse;
        public FVector Pos;
        public float Frame;
    }

    // The unit cube's corners and the eight triangles of the box test (InitBoxTriangles).
    private static readonly FVector[] Corners =
    [
        new(-1.0f, -1.0f, -1.0f), new(-1.0f, 1.0f, -1.0f), new(1.0f, 1.0f, -1.0f), new(1.0f, -1.0f, -1.0f),
        new(-1.0f, -1.0f, 1.0f), new(-1.0f, 1.0f, 1.0f), new(1.0f, 1.0f, 1.0f), new(1.0f, -1.0f, 1.0f),
    ];

    private static readonly int[,] BoxTriangles =
    {
        { 4, 0, 3 }, { 3, 7, 4 }, { 5, 2, 1 }, { 2, 5, 6 }, { 1, 0, 4 }, { 4, 5, 1 }, { 2, 7, 3 }, { 7, 2, 6 },
    };

    /// <summary>mRenderThing (+0x14).</summary>
    public IMeshCollisionSource? Source { get; private set; } = source;

    /// <summary>mUnknown18: test parts by their boxes for the next sphere test only (mode 1); cleared by each test.</summary>
    public int Unknown18;

    /// <summary>mUnknown1C: take the transforms without animation (GetWorldTransforms at frame 0).</summary>
    public bool IgnoreAnimation { get; } = ignoreAnimation;

    /// <summary>mUnknown20: once cached, the transforms are never refreshed.</summary>
    public bool UseFixedTransforms { get; } = useFixedTransforms;

    /// <summary>mTransforms: one per mesh part, allocated by the first cache.</summary>
    public PartTransform[]? Transforms { get; private set; }

    /// <summary>Sets the cached transforms as they stand (a test harness gives them directly).</summary>
    public void SetTransforms(PartTransform[]? transforms) => Transforms = transforms;

    /// <summary>"Error: Can't find mesh part in" messages.</summary>
    public int MissingPartMessages { get; private set; }

    /// <summary>SetRenderThing: a new render thing drops the cached transforms.</summary>
    public void SetRenderThing(IMeshCollisionSource? source)
    {
        Transforms = null;
        Source = source;
    }

    private static FVector Sub(FVector a, FVector b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    private static FVector Add(FVector a, FVector b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    private static FVector Scale(FVector v, float s) => new(v.X * s, v.Y * s, v.Z * s);

    /// <summary>CachePartTransform: the part's transform now, and its transpose.</summary>
    public void CachePartTransform(MeshData mesh, int part, float frame)
    {
        if (Transforms is null)
        {
            Transforms = new PartTransform[mesh.NumParts];
            for (int i = 0; i < mesh.NumParts; i++)
            {
                Transforms[i] = new PartTransform { Frame = -1.0f };
            }
        }

        MeshPart? p = part < mesh.Parts.Count ? mesh.Parts[part] : null;
        if (p is null)
        {
            MissingPartMessages++;
            return;
        }

        FVector pos;
        FMatrix ori;
        if (!IgnoreAnimation)
        {
            MeshPose.EvaluatePose(mesh, Source!.Pose, Source.Subject, p, Source.RenderFrameNumber, out pos, out ori, false, false);
        }
        else
        {
            if (Source!.Subject.HasMotionController)
            {
                throw new NotSupportedException("motion controllers are not ported yet");
            }

            p.GetWorldTransforms(0, 0, 0, Source.Subject, out ori, out pos, local: false, uncached: false);
        }

        PartTransform t = Transforms[part];
        t.Orientation = ori;
        t.Inverse = ori.Transpose();
        t.Pos = pos;
        t.Frame = frame;
    }

    /// <summary>The part's transform, cached again when it has none yet or (unless fixed) it is older than now.</summary>
    private PartTransform GetPartTransform(MeshData mesh, int i)
    {
        float now = Source!.Time;
        if (Transforms is null || Transforms[i].Frame < 0)
        {
            CachePartTransform(mesh, i, now);
        }

        if (!UseFixedTransforms && Transforms![i].Frame < now)
        {
            CachePartTransform(mesh, i, now);
        }

        return Transforms![i];
    }

    /// <summary>The part tested for mesh part i, or null to skip it (not a collision part, hidden, or destroyed).</summary>
    private MeshPart? CollisionPart(MeshPart part)
    {
        if (part.GetMeshDataPart() is not { PartType: 1 })
        {
            return null;
        }

        if (Source!.Subject.HasMotionController)
        {
            // mc->Unknown6 (flags; 1 hides the part) and Unknown7 (the damage level: that many steps along the chain).
            throw new NotSupportedException("motion controllers are not ported yet");
        }

        return part;
    }

    /// <summary>
    /// TestSweptSphereAgainstBox: the sphere around the whole move must overlap the part's bounding box; then the box
    /// itself (how far outside it) or, when the report asks for triangles, the box's eight triangles.
    /// </summary>
    public static bool TestSweptSphereAgainstBox(MeshPart? partIndex, BoundingBox box, FVector pos, FVector move, float radius,
        CollisionReport report)
    {
        bool hit = false;
        FVector end = Add(pos, move);
        FVector d = Sub(end, pos);
        float r = ((float)X87.Sqrt((d.Z * d.Z) + (d.Y * d.Y) + (d.X * d.X)) * 0.5f) + radius;
        FVector centre = Add(Scale(Sub(end, pos), 0.5f), pos);
        FVector extent = box.Axes;
        centre = Sub(centre, box.Origin);
        if (centre.X - r > extent.X || -extent.X > centre.X + r
            || centre.Y - r > extent.Y || -extent.Y > centre.Y + r
            || centre.Z - r > extent.Z || -extent.Z > centre.Z + r)
        {
            return false;
        }

        if (report.TestTriangles == 0)
        {
            float dist = Geometry.DistanceOutsideBox(centre, extent) - r;
            if (dist > 0)
            {
                return false;
            }

            report.Distance = dist;
            report.Part = partIndex;
            report.UnknownA8 = true;
            report.Unknown94 = true;
            return true;
        }

        for (int i = 0; i < 8; i++)
        {
            FVector a = Corners[BoxTriangles[i, 0]];
            FVector b = Corners[BoxTriangles[i, 1]];
            FVector c = Corners[BoxTriangles[i, 2]];
            a = Add(new FVector(a.X * extent.X, a.Y * extent.Y, a.Z * extent.Z), box.Origin);
            b = Add(new FVector(b.X * extent.X, b.Y * extent.Y, b.Z * extent.Z), box.Origin);
            c = Add(new FVector(c.X * extent.X, c.Y * extent.Y, c.Z * extent.Z), box.Origin);
            if (Geometry.TestSweptSphereAgainstTriangle(a, b, c, pos, move, radius, report))
            {
                hit = true;
            }
        }

        if (hit)
        {
            report.Part = partIndex;
        }

        return hit;
    }

    /// <summary>TestSweptSphereAgainstPart: the same swept sphere against the part's own triangles, from its poly bucket.</summary>
    public static bool TestSweptSphereAgainstPart(PolyBucketStatics statics, MeshPart? partIndex, MeshPolyBucket? bucket,
        FVector pos, FVector move, float radius, CollisionReport report)
    {
        bool hit = false;
        FVector end = Add(pos, move);
        FVector centre = Scale(Add(end, pos), 0.5f);
        FVector d = Sub(end, pos);
        float r = ((float)X87.Sqrt((d.Z * d.Z) + (d.Y * d.Y) + (d.X * d.X)) * 0.5f) + radius;
        if (bucket is null)
        {
            return false;
        }

        for (int t = bucket.StartSearch(statics, centre, r); t != -1; t = bucket.GetNextPoly(statics))
        {
            PolyTriangle tri = bucket.TriangleVertices(t);
            if (Geometry.TestSweptSphereAgainstTriangle(bucket.Unpack(tri.A), bucket.Unpack(tri.B), bucket.Unpack(tri.C), pos,
                move, radius, report))
            {
                hit = true;
                report.Part = partIndex;
            }
        }

        return hit;
    }

    /// <summary>CollideWithCylinder: as the sphere about the cylinder's centre of its larger radius and height.</summary>
    public bool CollideWithCylinder(ref CollisionMove move, float radius, float height, CollisionReport report) =>
        CollideWithSphere(ref move, radius <= height ? height : radius, report);

    /// <summary>
    /// CollideWithSphere: the sphere swept along the move against each collision part, in up to six passes. A pass's
    /// contacts are counted (up to six) when only boxes are wanted; otherwise the move is resolved against the contact
    /// (a slide, or a stop) and tested again while the passes keep finding one.
    /// </summary>
    public bool CollideWithSphere(ref CollisionMove move, float radius, CollisionReport report)
    {
        int passes = 0;
        int mode = Unknown18;
        Unknown18 = 0;
        if (Source?.Mesh is not { } mesh)
        {
            return false;
        }

        report.NumContacts = 0;
        bool hit = false;
        report.UnknownA8 = true;
        FVector start = Sub(move.Pos, move.Move);
        FVector pos = start;
        FVector dir = move.Move;
        FVector travel = move.Move;
        while (passes < 6)
        {
            if (!report.UnknownA8)
            {
                break;
            }

            report.UnknownA8 = false;
            report.Stuck = false;
            for (int i = 0; i < mesh.NumParts; i++)
            {
                if (CollisionPart(mesh.Parts[i]) is not { } part)
                {
                    continue;
                }

                PartTransform t = GetPartTransform(mesh, i);
                FVector lstart = t.Inverse.Transform(Sub(start, t.Pos));
                FVector lmove = t.Inverse.Transform(travel);
                MeshPart collision = part.GetMeshDataPart()!;
                if (mode == 1)
                {
                    if (TestSweptSphereAgainstBox(part, collision.BoundingBox!, lstart, lmove, radius, report))
                    {
                        hit = true;
                    }
                }
                else if (TestSweptSphereAgainstPart(Source.PolyBuckets, part, collision.PolyBucket, lstart, lmove, radius, report))
                {
                    hit = true;
                }

                if (!hit)
                {
                    continue;
                }

                if (report.UnknownD0 != 0)
                {
                    return true;
                }

                if (report.TestTriangles == 0)
                {
                    report.ContactParts[report.NumContacts] = report.Part!.PartNumber;
                    report.ContactDistances[report.NumContacts] = report.Distance;
                    report.ContactPos = pos;
                    report.NumContacts++;
                    hit = false;
                    if (report.NumContacts >= 6)
                    {
                        return true;
                    }
                }
            }

            if (report.UnknownA8 || report.Stuck)
            {
                if (report.TestTriangles == 0)
                {
                    return true;
                }

                report.UnknownA8 = true;
                report.ContactParts[report.NumContacts] = report.Part!.PartNumber;
                report.ContactDistances[report.NumContacts] = report.Distance;
                PartTransform t = GetPartTransform(mesh, report.Part.PartNumber);
                report.ContactPos = Add(ContactToWorld(t.Orientation, report.ContactPos), t.Pos);
                if (!ResolveContact(report, start, travel, radius, out start, out travel)
                    || Geometry.DotOutOfLine(dir, travel) < 0)
                {
                    move.Pos = pos;
                    move.Move = new FVector(0, 0, 0);
                    move.Stopped = true;
                    return true;
                }

                report.NumContacts++;
            }

            passes++;
        }

        if (hit)
        {
            if (report.Stuck)
            {
                move.Pos = pos;
                move.Stopped = true;
                return true;
            }

            move.Pos = Add(travel, start);
            move.Move = travel;
            move.Stopped = true;
        }

        return hit;
    }

    /// <summary>
    /// ResolveContact: move to the contact, then slide along the planes recorded so far. Two planes that both block the
    /// slide leave only the direction along their intersection; three or more stop the move (false). The radius is
    /// unused, as in retail. Sums as compiled: the travel's length (Y*Y + Z*Z) + X*X; the stuck arm's Normalise
    /// (Z*Z + Y*Y) + X*X (004acfd8); the later Normalise calls (X*X + Z*Z) + Y*Y; the steep-contact dot (Y + X) + Z;
    /// in the plane loop, n.travel (Y + X) + Z and the slide's test (Z + Y) + X (004ad459..004ad50d); the two-plane
    /// direction through the out-of-line cross product, Normalise and dot (00411a60, 00406d50, 0040d180).
    /// </summary>
    public static bool ResolveContact(CollisionReport report, FVector pos, FVector travel, float radius, out FVector start,
        out FVector move)
    {
        FVector correction = new(0, 0, 0);
        FVector direction = travel;
        FVector normal = default;
        FVector advance;
        bool stuck = false;
        move = travel;
        float length = (float)X87.Sqrt((travel.Y * travel.Y) + (travel.Z * travel.Z) + (travel.X * travel.X));
        if (length != 0)
        {
            length = 1.0f / length;
            direction = Scale(direction, length);
        }

        if (report.Stuck)
        {
            normal = Sub(pos, report.ContactPos);
            normal = NormaliseZYX(normal);
            stuck = true;
            correction = Scale(normal, report.ContactDistances[report.NumContacts]);
            advance = correction;
        }
        else
        {
            float distance = report.ContactDistances[report.NumContacts];
            advance = Scale(direction, distance);
            if (report.ContactDistances[report.NumContacts] > 0.01)
            {
                correction = Scale(direction, distance * 0.9f);
            }
        }

        start = Add(pos, correction);
        if (report.TestTriangles == 1)
        {
            move = new FVector(0, 0, 0);
            return true;
        }

        if (!stuck)
        {
            normal = Sub(Add(pos, advance), report.ContactPos);
        }

        normal = NormaliseXZY(normal);
        FVector slide;
        if (report.TriangleNormal.Z > -0.89f && report.TriangleNormal.Z < -0.01f)
        {
            float dot = (normal.Y * travel.Y) + (normal.X * travel.X) + (normal.Z * travel.Z);
            slide = Sub(travel, Scale(normal, dot));
            if (slide.Z <= 0)
            {
                normal.Z = 0;
                normal = NormaliseXZY(normal);
            }
        }

        report.Normals[report.NumContacts] = normal;
        int numPlanes = report.NumContacts + 1;
        int i;
        slide = default;
        for (i = 0; i < numPlanes; i++)
        {
            FVector n = report.Normals[i];
            slide = Sub(travel, Scale(n, (n.Y * travel.Y) + (n.X * travel.X) + (n.Z * travel.Z)));
            int j;
            for (j = 0; j < numPlanes; j++)
            {
                if (j != i && !SameVector(report.Normals[i], report.Normals[j]) && DotZYX(slide, report.Normals[j]) < 0)
                {
                    break;
                }
            }

            if (j == numPlanes)
            {
                break;
            }
        }

        if (i != numPlanes)
        {
            move = slide;
            return true;
        }

        if (numPlanes != 2)
        {
            return false;
        }

        FVector along = Geometry.NormaliseOutOfLine(FVector.Cross(report.Normals[0], report.Normals[1]));
        float d = Geometry.DotOutOfLine(along, travel);
        move = Scale(along, d);
        return true;
    }

    /// <summary>
    /// The sphere contact's orientation product (004acbeb..004acc4b): row 0 sums (Z + Y) + X as FMatrix's operator
    /// does elsewhere, rows 1 and 2 (X + Z) + Y.
    /// </summary>
    private static FVector ContactToWorld(FMatrix o, FVector c) => new(
        ((o.Row0.Z * c.Z) + (o.Row0.Y * c.Y)) + (o.Row0.X * c.X),
        ((o.Row1.X * c.X) + (o.Row1.Z * c.Z)) + (o.Row1.Y * c.Y),
        ((o.Row2.X * c.X) + (o.Row2.Z * c.Z)) + (o.Row2.Y * c.Y));

    /// <summary>The inline dot product as the plane loop's test compiles it: (Z + Y) + X.</summary>
    private static float DotZYX(FVector a, FVector b) => (a.Z * b.Z) + (a.Y * b.Y) + (a.X * b.X);

    /// <summary>FVector::operator== (float compares: -0 equals 0).</summary>
    private static bool SameVector(FVector a, FVector b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;

    private static FVector NormaliseZYX(FVector v)
    {
        float m = (float)X87.Sqrt((v.Z * v.Z) + (v.Y * v.Y) + (v.X * v.X));
        if (m == 0)
        {
            return v;
        }

        m = 1.0f / m;
        return Scale(v, m);
    }

    private static FVector NormaliseXZY(FVector v)
    {
        float m = (float)X87.Sqrt((v.X * v.X) + (v.Z * v.Z) + (v.Y * v.Y));
        if (m == 0)
        {
            return v;
        }

        m = 1.0f / m;
        return Scale(v, m);
    }

    /// <summary>
    /// CollideWithLine: the line, placed at the move's position, in each collision part's frame; the nearest triangle
    /// hit is kept (the walk of a part stops after the cell of its first hit). The contact and its normal go back to
    /// the world; a response moves the line's movement to the contact.
    /// </summary>
    public bool CollideWithLine(ref CollisionMove move, FVector lineStart, FVector lineEnd, CollisionReport report)
    {
        Unknown18 = 0;
        if (Source?.Mesh is not { } mesh)
        {
            return false;
        }

        bool hit = false;
        FVector start = Add(move.Pos, lineStart);
        FVector end = Add(move.Pos, lineEnd);
        report.UnknownA8 = false;
        for (int i = 0; i < mesh.NumParts; i++)
        {
            bool partHit = false;
            if (CollisionPart(mesh.Parts[i]) is not { } part)
            {
                continue;
            }

            PartTransform t = GetPartTransform(mesh, i);
            FVector localStart = t.Inverse.Transform(Sub(start, t.Pos));
            FVector localEnd = t.Inverse.Transform(Sub(end, t.Pos));
            MeshPart collision = part.GetMeshDataPart()!;
            MeshPolyBucket? bucket = collision.PolyBucket;
            if (bucket is null)
            {
                continue;
            }

            for (int tri = bucket.StartLineSearch(Source.PolyBuckets, localStart, localEnd); tri != -1;
                 tri = bucket.GetNextPolyLine(Source.PolyBuckets, partHit))
            {
                PolyTriangle v = bucket.TriangleVertices(tri);
                if (Geometry.TestSegmentAgainstTriangle(bucket.Unpack(v.A), bucket.Unpack(v.B), bucket.Unpack(v.C), localStart,
                    localEnd, report))
                {
                    if (report.UnknownD0 != 0)
                    {
                        return true;
                    }

                    hit = true;
                    partHit = true;
                    report.Part = part;
                }
            }
        }

        if (hit)
        {
            MeshPart hitPart = report.Part!;
            report.ContactParts[0] = hitPart.PartNumber;
            report.ContactDistances[0] = report.Distance;
            report.NumContacts = 1;
            PartTransform t = GetPartTransform(mesh, hitPart.PartNumber);
            FMatrix ori = t.Orientation;
            report.ContactPos = Add(ori.Transform(report.ContactPos), t.Pos);
            report.Normals[0] = ori.Transform(report.TriangleNormal);
            if (report.TestTriangles != 0)
            {
                move.Pos = report.ContactPos;
                move.Stopped = true;
            }
        }

        return hit;
    }
}
