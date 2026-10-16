// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/rtmesh.cpp (CRTMesh: Init makes a pose for its mesh; GetRTEmitter 004dd160). Not in
// Stuart's release.

using OnslaughtRebuild.Parity.Collision;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>CRTMesh: a thing's mesh and the pose cache its emitter lookups go through.</summary>
public sealed class RenderThing : IMeshCollisionSource
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

    /// <summary>mNextRendered, mPrevRendered: CRTMesh's rendered-this-frame and rendered-last-frame lists.</summary>
    public RenderThing? NextRendered, PrevRendered;

    /// <summary>The mesh's effect emitters (records named '_' + descriptor), made the first time the mesh is drawn.</summary>
    public MeshEffects? Effects;

    /// <summary>FindEffect then GetEffect: the effect link of that emitter part, if it has one.</summary>
    public Particles.ParticleEffectLink? FindEffectLink(Mesh.MeshPart part)
    {
        if (Effects is not { } e)
        {
            return null;
        }

        for (int i = 0; i < e.Parts.Length; i++)
        {
            if (ReferenceEquals(e.Parts[i], part))
            {
                return e.Links[i];
            }
        }

        return null;
    }

    public MeshData Mesh => MeshOrNull ?? throw new InvalidOperationException("this render thing has no mesh loaded");

    public MeshPose? Pose { get; }

    MeshData? IMeshCollisionSource.Mesh => MeshOrNull;

    /// <summary>mThing: the thing drawn.</summary>
    public IRenderSubject Subject => _subject;

    float IMeshCollisionSource.Time => _context.Events.Time;

    int IMeshCollisionSource.RenderFrameNumber => _context.RenderFrameNumber;

    MeshTransformStatics IMeshCollisionSource.Statics => _context.TransformStatics;

    PolyBucketStatics IMeshCollisionSource.PolyBuckets => _context.PolyBuckets;

    /// <summary>GetRTEmitter(name, n, pos, ori, TRUE, FALSE): the emitter's position in the thing's own space.</summary>
    public FVector EmitterPos(string name, int n) =>
        MeshPose.GetRTEmitter(Mesh, Pose, _subject, name, n, _context.TransformStatics).Pos;

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

/// <summary>CRTMesh's effect tables: per effect emitter, its part, id, descriptor, link and whether it has started.</summary>
public sealed class MeshEffects
{
    public required Mesh.MeshPart?[] Parts;
    public required int[] Ids;
    public required Particles.ParticleDescriptor?[] Types;
    public required Particles.ParticleEffectLink?[] Links;
    public required bool[] Active;
}
