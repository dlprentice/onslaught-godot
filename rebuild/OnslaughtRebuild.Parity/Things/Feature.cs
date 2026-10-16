// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/feature.h and src/feature.cpp (CFeature::Init 0044ca30, Move 0044cc10, ClipToGround 0044d1d0,
// CanGoUnderWater 00415a90, SetThingType). Not in Stuart's release.

using System.Text;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>CFeature: icebergs, rocks and other scenery that can be destroyed.</summary>
public sealed class Feature(ThingContext context) : Actor(context, Oid.Feature)
{
    /// <summary>CFeature::Damage (0044cd20): an invincible feature takes none; out of life it starts dying; life at most its data's.</summary>
    public override void Damage(float amount, Thing? byThis, bool damageShields = true, int meshPart = -1)
    {
        if (Data!.Invincible == 0)
        {
            Life -= amount;
            if (Life < 0 && !IsDying)
            {
                StartDieProcess();
            }

            if (Life > Data.Life)
            {
                Life = Data.Life;
            }
        }
    }

    /// <summary>CFeature::StartDieProcess (0044cd80): dying (its shutdown comes when it lands or sinks), the script told.</summary>
    public override bool StartDieProcess()
    {
        if (IsDying)
        {
            return false;
        }

        Flags |= ThingFlags.Dying;
        Script?.StartedDying();
        return true;
    }

    /// <summary>CFeature::AddShutdownEvent (0044cdb0): once, and its explosion at it with its radius.</summary>
    public override void AddShutdownEvent()
    {
        if ((Flags & ThingFlags.DeclaredShutdown) != 0)
        {
            return;
        }

        base.AddShutdownEvent();
        if (Data!.Explosion != 0 && Explosion.Spawn(Context, Data.Explosion) is { } explosion)
        {
            explosion.Init(new InitThing
            {
                Behaviour = Context.Physics.Explosions[Data.Explosion],
                AttachedTo = this,
                UseAttachedRadius = true,
                Pos = Pos,
                Allegiance = Allegiance,
            });
        }
    }

    /// <summary>CFeature::DeclareOnGround (0044d1f0): a dying feature landing shuts down.</summary>
    public override void DeclareOnGround()
    {
        base.DeclareOnGround();
        if (IsDying)
        {
            AddShutdownEvent();
        }
    }

    public override bool CanBeStaticallyShadowed => true;

    /// <summary>GetMaxVelocity (slot 15, the shared 004de700).</summary>
    public override float GetMaxVelocity() => 1.0f;


    public FeatureData? Data;
    public int Allegiance;
    public float Life;
    public bool AboveWater;

    public override void SetThingType(uint t) => base.SetThingType(t | ThingType.Unknown20 | 0x00500000);

    /// <summary>
    /// CFeature::Move (0044cc10): CActor's, then a dying feature that is not above water topples and sinks (its asin and
    /// azimuth sites and SpawnPickup are not ported, so that case throws).
    /// </summary>
    public override void Move()
    {
        base.Move();
        if (IsDying && !AboveWater)
        {
            throw new NotSupportedException("CFeature::Move's dying topple (asin, Azimuth, SpawnPickup) is not ported");
        }
    }

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
        Occupancy.AddUnit(Context.Occupancy, Context.Map, this);
        AboveWater = Context.WaterLevel > Pos.Z;
        if (Data.SoundName is not null)
        {
            Context.Skip("SOUND.PlayEffect (a feature's sound)");
        }
    }
}
