// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/feature.h and src/feature.cpp (CFeature::Init 0044ca30, ClipToGround 0044d1d0,
// CanGoUnderWater 00415a90, SetThingType). Not in Stuart's release.

using System.Text;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>CFeature: icebergs, rocks and other scenery that can be destroyed.</summary>
public sealed class Feature(ThingContext context) : Actor(context, Oid.Feature)
{
    public FeatureData? Data;
    public int Allegiance;
    public float Life;
    public bool AboveWater;

    public override void SetThingType(uint t) => base.SetThingType(t | ThingType.Unknown20 | 0x00500000);

    public override bool ClipToGround() => !(IsDying && !AboveWater);

    public override bool CanGoUnderWater() => IsDying;

    public override void Init(InitThing init)
    {
        init.InitCst.NotSeekCollisionWith |= ThingType.Unknown20;
        init.InitCst.MinCollision = CollisionLevel.Mesh;
        init.InitCst.MaxCollision = CollisionLevel.Mesh;
        Data = init.FeatureData;
        MeshData? mesh = null;
        if (Data?.MeshName is { } name && !Context.Meshes.TryGetValue(Encoding.ASCII.GetString(name), out mesh))
        {
            throw new InvalidOperationException($"mesh '{Encoding.ASCII.GetString(name)}' is not in the level resource file");
        }

        if (Data?.MeshName is null)
        {
            Context.Skip("CRTMesh::Init without a name (the engine's default mesh)");
        }

        RenderThing = new RenderThing(Context, mesh, this);
        base.Init(init);
        Allegiance = init.Allegiance;
        Life = Data!.Life;
        Context.Skip("CWorld::AddToOccupancy");
        AboveWater = Context.WaterLevel > Pos.Z;
        if (Data.SoundName is not null)
        {
            Context.Skip("SOUND.PlayEffect (a feature's sound)");
        }
    }
}
