// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/GeneralVolume.h (CCollisionMove, CCollisionReport), reconstructed; not in Stuart's release.

using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;

namespace OnslaughtRebuild.Parity.Collision;

/// <summary>CCollisionMove: the move a collision test sweeps and the position it ends at; stopped when it is cut short.</summary>
public struct CollisionMove
{
    public FVector Move;
    public FVector Pos;
    public bool Stopped;
}

/// <summary>CCollisionReport: what a collision test found (up to six contacts) and the state the tests carry between calls.</summary>
public sealed class CollisionReport
{
    /// <summary>mUnknown00 (zeroed by the constructor).</summary>
    public int Unknown00;

    /// <summary>mNormals: each contact's normal.</summary>
    public FVector[] Normals { get; } = new FVector[6];

    /// <summary>mContactParts: each contact's part number.</summary>
    public int[] ContactParts { get; } = new int[6];

    public int NumContacts;

    public FVector ContactPos;

    /// <summary>mUnknown94: set on a hit.</summary>
    public bool Unknown94;

    /// <summary>mRelativeMove: the other movement less this one.</summary>
    public FVector RelativeMove;

    /// <summary>mUnknownA8: set on a hit; a later test must beat the distance found.</summary>
    public bool UnknownA8;

    public float[] ContactDistances { get; } = new float[6];

    /// <summary>mDistance: the nearest hit's distance (or how far inside a box).</summary>
    public float Distance;

    /// <summary>mStuck: the sphere already overlaps.</summary>
    public bool Stuck;

    /// <summary>mTestTriangles: the minimum collision response; zero also skips the detailed tests.</summary>
    public int TestTriangles;

    /// <summary>mUnknownD0: stop at the first hit.</summary>
    public int UnknownD0;

    /// <summary>mTriangleNormal: the hit triangle's unit normal.</summary>
    public FVector TriangleNormal;

    /// <summary>mPart: the part tested last.</summary>
    public MeshPart? Part;
}
