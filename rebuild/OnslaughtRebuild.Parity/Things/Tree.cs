// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/tree.h and src/tree.cpp (CTree::Init 004f6080, SetThingType, CFallingTree's constructor
// 004f5f60, Damage 004f68e0, CreateFallingTree 004f69b0, Hit 004f6aa0, UpdateFallingTree 004f6b80, HandleEvent 004f7050). Float forms read
// from the pristine executable (74154bfa...). Not in Stuart's release.

using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.World;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Particles;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>CTree: a tree (one draw for its variation, one more when its type has no variant number).</summary>
public sealed class Tree(ThingContext context) : Thing(context, Oid.Tree)
{
    /// <summary>CTree::Damage (004f68e0): a standing tree loses life, and at none falls away from what damaged it.</summary>
    public override void Damage(float amount, Thing? byThis, bool damageShields = true, int meshPart = -1)
    {
        if (Falling is not null || TreeLife == 0)
        {
            return;
        }

        TreeLife -= amount;
        if (TreeLife <= 0)
        {
            TreeLife = 0;
            FVector dir = Pos - byThis!.Pos;
            dir.Normalise();
            CreateFallingTree(dir);
        }
    }

    public int Variation;
    public float TreeLife;
    public byte[] MeshName = [];

    public override void SetThingType(uint t) => base.SetThingType(t | ThingType.Tree | ThingType.Stationary);

    /// <summary>TREE_FALL: the falling tree's next step.</summary>
    public const int FallEvent = 3000;

    /// <summary>TREE_COLLIDE: half a second into its fall it collides with everything.</summary>
    public const int CollideEvent = 3001;

    /// <summary>
    /// sOrientations: the 32 yaws of n * (PI / 16) (a float constant), as CTree::Init's MakeRotationYawF stores them:
    /// the angle, its cosine and its sine each a float.
    /// </summary>
    private static readonly FMatrix[] Orientations = MakeOrientations();

    private static FMatrix[] MakeOrientations()
    {
        var o = new FMatrix[32];
        float pi16 = BitConverter.Int32BitsToSingle(0x3e490fdb);
        for (int n = 0; n < 32; n++)
        {
            float yaw = n * pi16;
            float c = (float)X87.Cos(yaw);
            float sn = (float)X87.Sin(yaw);
            o[n] = new FMatrix(new FVector(c, -sn, 0.0f), new FVector(sn, c, 0.0f), new FVector(0.0f, 0.0f, 1.0f));
        }

        return o;
    }

    /// <summary>mFalling: the fall, once it has started.</summary>
    public FallingTree? Falling;

    /// <summary>
    /// CTree::Hit (004f6aa0): a standing tree hit by something that can destroy trees (an explosion only within its
    /// current radius) falls away from it.
    /// </summary>
    public override void Hit(Thing other, Collision.CollisionReport? report)
    {
        if (!other.IsA(ThingType.CanDestroyTrees) || Falling is not null)
        {
            return;
        }

        FVector dir = Pos - other.Pos;
        // 004f6ae9..004f6b13: sqrt((Y² + Z²) + X²), retained on the x87 stack. A bit test of ah against 41
        // admits less/equal and unordered; using !(magnitude <= radius) would reject the latter.
        if (other.IsA(ThingType.Explosion) && HitMagnitude(dir) > ((Explosion)other).CurrentRadius)
        {
            return;
        }

        // 004f6b15..004f6b64 recomputes the retained norm, skips zero OR unordered, and keeps the
        // reciprocal unspilled until each component store. FVector.Normalise spills a different sum.
        double magnitude = HitMagnitude(dir);
        if (magnitude != 0 && !double.IsNaN(magnitude))
        {
            double reciprocal = X87.Div(1, magnitude);
            dir = new FVector((float)X87.Mul(dir.X, reciprocal), (float)X87.Mul(dir.Y, reciprocal),
                (float)X87.Mul(dir.Z, reciprocal));
        }
        CreateFallingTree(dir);
    }

    private static double HitMagnitude(FVector dir) =>
        X87.Sqrt(X87.Add(X87.Add(X87.Mul(dir.Y, dir.Y), X87.Mul(dir.Z, dir.Z)), X87.Mul(dir.X, dir.X)));

    /// <summary>CreateFallingTree (004f69b0): it falls away along dir, colliding with everything half a second later.</summary>
    public void CreateFallingTree(FVector dir)
    {
        if (Falling is not null)
        {
            return;
        }

        Falling = new FallingTree(Orientations[Variation], GetRadius() * 2, dir) { StartTime = -1.0f };
        Context.Events.AddEventIn(0.5f, CollideEvent, this);
        UpdateFallingTree();
    }

    /// <summary>
    /// UpdateFallingTree (004f6b80): while it turns, a trace from just below its base to its top (plus 0.5) against the
    /// ground; on a hit it bounces back (the angle loses half the speed, the speed is scaled by -0.2) and it stops once
    /// the speed is under 0.01. Otherwise the angle advances, the speed grows by 0.005 and the start orientation turns by
    /// the angle about the fall's axis; stopped, it dies five seconds later. The ground-hit particle effect (speed above
    /// 0.05, taller than 3) is drawing only.
    /// </summary>
    public void UpdateFallingTree()
    {
        bool carryOn = true;
        if (Falling is not { } f)
        {
            return;
        }

        f.Unknown8C = f.StartOrientation;
        if (f.Speed > 0)
        {
            FVector pos = Pos;
            FVector start = pos;
            start.Z -= 0.2f;
            float h = -f.Height;
            FMatrix o = f.StartOrientation;
            var d = new FVector(o.Row0.Z * h, o.Row1.Z * h, o.Row2.Z * h);
            var end = new FVector(d.X + pos.X, d.Y + pos.Y, d.Z + pos.Z + 0.5f);
            bool hit = Context.Map.LineHits(start, end, out _, includeWater: false);
            if (hit)
            {
                if (f.Speed > 0.05f && f.Height > 3.0f)
                {
                    // The ground hit's effect, through a local link (released at once: the effect plays on), at the tree,
                    // turned to the fall's azimuth.
                    if (Context.Particles is { } particles)
                    {
                        var link = new ParticleEffectLink(particles.Manager);
                        particles.Manager.CreateEffect(particles.Manager.Set.GetPD("Tree Ground Hit Effect"), link, new FVector(0, 0, 0), false, false);
                        link.SetPos(Pos);
                        link.SetOrientation(FMatrix.FromYawPitchRoll(WeaponMaths.Azimuth(Context, f.Dir), 0, 0));
                        link.Release();
                    }
                    else
                    {
                        Context.Skip("CTree::UpdateFallingTree: the \"Tree Ground Hit Effect\" particle effect");
                    }
                }

                f.Angle -= f.Speed * 0.5f;
                f.Speed *= -0.2f;
            }

            // fabs(speed) < 0.01 compares with a double constant.
            if (Math.Abs(f.Speed) < 0.01 && hit)
            {
                carryOn = false;
            }
        }

        if (carryOn)
        {
            f.Angle = f.Speed + f.Angle;
            f.Speed += 0.005f;

            // As compiled (004f6e2b..004f6f35): the sine is stored as a float; 1 - cos is taken from fcos unrounded.
            float a = f.Angle;
            float s = (float)X87.Sin(a);
            float t = (float)X87.Sub(1.0, X87.Cos(a));
            FVector ax = f.Axis;
            float xx = ax.X * ax.X, yy = ax.Y * ax.Y, zz = ax.Z * ax.Z;
            float xy = ax.Y * ax.X, xz = ax.Z * ax.X, yz = ax.Z * ax.Y;
            float xs = ax.X * s, ys = ax.Y * s, zs = ax.Z * s;
            float xyt = xy * t;
            float xzt = xz * t;
            float yzt = yz * t;
            var r = new FMatrix(
                new FVector(1.0f - ((zz + yy) * t), xyt + zs, xzt - ys),
                new FVector(xyt - zs, 1.0f - ((zz + xx) * t), yzt + xs),
                new FVector(xzt + ys, yzt - xs, 1.0f - ((yy + xx) * t)));
            f.StartOrientation = r.MultiplyOutOfLine(f.Orientation);
            Context.Events.AddEvent(FallEvent, this, Events.EventManager.NextFrame);
        }
        else
        {
            f.StartTime = Context.Events.Time;
            Context.Events.AddEventIn(5.0f, ThingEvent.StartDieProcess, this);
        }
    }

    /// <summary>HandleEvent (004f7050): the fall's steps; half a second in, it collides with everything.</summary>
    public override void HandleEvent(Events.Event e)
    {
        switch (e.EventNum)
        {
            case FallEvent:
                UpdateFallingTree();
                break;
            case CollideEvent:
                Seeker!.SetCollisionMask(0xffffffff);
                break;
            default:
                base.HandleEvent(e);
                break;
        }
    }

    /// <summary>
    /// CTree::InitCollisionSeekingThing (004f6480): a tree that seeks collision is a thin cylinder (0.2 by half its
    /// radius), approximate at most, and does not seek stationary things (other trees among them).
    /// </summary>
    public override void InitCollisionSeekingThing(InitCollision init)
    {
        if (init.NotSeekCollisionWith != ThingType.Everything)
        {
            init.Shape = new Collision.CylinderVolume(0.2f, GetRadius() * 0.5f);
            init.NotSeekCollisionWith = ThingType.Stationary;
            init.MaxCollision = CollisionLevel.ApproxGeometryShapes;
            base.InitCollisionSeekingThing(init);
        }
    }

    public override void Init(InitThing init)
    {
        Variation = X87.Fist((float)(Context.Random.Random() % 65536) * (1.0f / 2048.0f));   // fistp: rounds to nearest (004f61d2)
        init.Pos.Z = Context.Map.Collide(init.Pos);
        init.Orientation = FMatrix.Identity;
        init.OrientationType = OrientationType.DirectionCosineMatrix;
        var name = new List<byte>(init.TreeType);
        if (!init.TreeTypeComplete)
        {
            name.AddRange(System.Text.Encoding.ASCII.GetBytes((Context.Random.Random() % 4).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        name.AddRange(".MSH"u8.ToArray());
        MeshName = name.ToArray();
        // CreateRenderThing(2): a CRTTree, with the mesh of that name (CMesh::GetMesh, ignoring case); its radius and
        // bounding box size the tree's map-who entry. Its imposter and slope are rendering only.
        string meshName = System.Text.Encoding.ASCII.GetString(MeshName);
        if (!Context.Meshes.TryGetValue(meshName, out Mesh.MeshData? mesh))
        {
            throw new InvalidOperationException($"tree mesh '{meshName}' is not in the level resource file");
        }

        RenderThing = new RenderThing(Context, mesh, NoSubject.Instance);
        string text = System.Text.Encoding.ASCII.GetString(MeshName);
        if (text.StartsWith("fern", StringComparison.OrdinalIgnoreCase) || text.StartsWith("bush", StringComparison.OrdinalIgnoreCase))
        {
            init.InitCst.NotSeekCollisionWith = ThingType.Everything;
        }

        base.Init(init);
        TreeLife = 1.5f;
    }
}

/// <summary>A render subject for things whose pose the simulation never evaluates (trees).</summary>
internal sealed class NoSubject : Mesh.IRenderSubject
{
    public static readonly NoSubject Instance = new();

    public int RealAnimIndex => -1;

    public float RenderFrame => 0;

    public FVector RenderPos => default;

    public FMatrix RenderOrientation => FMatrix.Identity;

    public bool HasMotionController => false;

    public bool RenderUseHierarchy(int partNumber) => true;
}

/// <summary>CFallingTree: a tree's fall away from what hit it.</summary>
public sealed class FallingTree
{
    /// <summary>The constructor (004f5f60): the axis is dir ^ (0, 0, -1), normalised; the speed starts at 0.02.</summary>
    public FallingTree(FMatrix ori, float height, FVector dir)
    {
        StartOrientation = ori;
        Angle = 0;
        Speed = 0;
        Orientation = ori;
        Unknown8C = ori;
        Height = height;
        Dir = dir;
        Axis = FVector.Cross(dir, new FVector(0, 0, -1.0f));
        Axis.Normalise();
        Speed = 0.02f;
    }

    public float Height;
    public float Angle;
    public float Speed;
    public FVector Dir;

    /// <summary>mAxis: the fall's axis, normalised.</summary>
    public FVector Axis;

    public FMatrix Orientation;
    public FMatrix StartOrientation;

    /// <summary>mUnknown8C: the orientation before this step (drawn between it and the start orientation).</summary>
    public FMatrix Unknown8C;

    /// <summary>mStartTime: -1 until it lands, then the time it stopped (it fades over five seconds).</summary>
    public float StartTime;
}
