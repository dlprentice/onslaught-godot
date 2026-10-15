// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/rtmesh.cpp (CRTMesh: Init makes a pose for its mesh; GetRTEmitter 004dd160). Not in
// Stuart's release.

using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>CRTMesh: a thing's mesh and the pose cache its emitter lookups go through.</summary>
public sealed class RenderThing
{
    private readonly IRenderSubject _subject;
    private readonly ThingContext _context;

    /// <summary>A render thing; <paramref name="mesh"/> is null for kinds whose mesh the simulation never queries.</summary>
    public RenderThing(ThingContext context, MeshData? mesh, IRenderSubject subject)
    {
        _context = context;
        _subject = subject;
        MeshOrNull = mesh;
        Pose = mesh is null ? null : new MeshPose(mesh.NumParts);
    }

    public MeshData? MeshOrNull { get; }

    public MeshData Mesh => MeshOrNull ?? throw new InvalidOperationException("this render thing has no mesh loaded");

    public MeshPose? Pose { get; }

    /// <summary>GetRTEmitter(name, n, pos, ori, TRUE, FALSE): the emitter's position in the thing's own space.</summary>
    public FVector EmitterPos(string name, int n) =>
        MeshPose.GetRTEmitter(Mesh, Pose, _subject, name, n, _context.RenderFrameNumber).Pos;

    /// <summary>The number of emitters of that name the Init loops count: from 1 up to the first at the origin.</summary>
    public int CountEmitters(string name)
    {
        int n = 1;
        while (!EmitterPos(name, n).Equals(new FVector(0, 0, 0)))
        {
            n++;
        }

        return n - 1;
    }
}
