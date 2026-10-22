// SPDX-License-Identifier: GPL-3.0-only
// A segmented unit's destructable segments and their motion controller. Adapted from onslaught-decomp
// src/DestructableSegmentsController.cpp (the constructor 00443fc0, Init 00444660, CreateSegment 004449c0, ProcessNode
// 00444c10, GetDamageStage 00444f00, CanUse 00444f20, GetCoreFall 00445010, GetCentre 00445070), DestroyableSegment.cpp/.h (the segments'
// constructors, SetLife, GetTotalHealth 00442900, CanBreak, CDestroyableSwapSegment::GetDamageStage 00443830) and
// MCDestructableSegments.cpp (GetPartTransform 00494ce0, Unknown6 00494fa0, Unknown7 00494ff0, Unknown9); the damage paths
// from DestroyableSegment.cpp (SpawnPickup 00442710, GetActiveHealth 00442890, Damage 00442960, ScheduleChildBreaks
// 004429a0, ActivateAll, DamageChildren, Break 00442b20, Update 00442d40, SpawnRubble 00442f60, the core's
// AreChildrenDestroyed 004433f0, Damage 004435f0, Break 00443660 and HandleEvent 004436d0, the swap's Damage 00443780 and
// Break 00443810, the extra's Damage 00443890 and Break 004439c0, the extra-use-mesh's SpawnRubble 00443a20) and the
// controller's (Damage 00444030, GetLife 00444330, GetLifeUnknown444370/4443b0, Unknown4443f0, GetLastDamageTime/Amount).

using OnslaughtRebuild.Parity.Collision;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Particles;
using OnslaughtRebuild.Parity.Physics;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>EDestroyableSegmentType.</summary>
public enum SegmentType
{
    Core = 0,
    Swap = 1,
    Extra = 2,
    ExtraUseMesh = 3,
    Component = 4,
}

/// <summary>CDestroyableSegment: one mesh part's share of the unit's life.</summary>
public class DestroyableSegment(DestructableSegments controller, int part, DestroyableSegment? parent, float size, SegmentType type) : Monitor
{
    /// <summary>SEGMENT_BREAK: a scheduled break.</summary>
    public const int BreakEvent = 3000;

    /// <summary>SEGMENT_CORE_CASCADE: a falling core's next frame.</summary>
    public const int CascadeEvent = 3002;

    public int Part { get; } = part;
    public float Health = 1.0f;
    public float MaxHealth = 1.0f;
    public float LastDamageTime = -1.0f;
    public float LastDamageAmount;
    public bool Vulnerable = true;
    public DestroyableSegment? Parent { get; } = parent;
    public List<DestroyableSegment> Children { get; } = [];
    public float Size = size;
    public bool Destroyed;
    public SegmentType Type { get; } = type;
    protected DestructableSegments Controller { get; } = controller;

    public virtual int GetDamageStage() => 0;

    public virtual bool CanBreak() => Parent is { Destroyed: false } p && p.CanBreak();

    public virtual void SetLife(float life, float total) => MaxHealth = Health = Size / total * life;

    /// <summary>GetCoreTime (slot 7): a core's fall time, 0 for others.</summary>
    public virtual float GetCoreTime() => 0.0f;

    private ThingContext Context => Controller.Owner.Context;

    /// <summary>GetActiveHealth (00442890): its full health while it could not break and has health, and its children's.</summary>
    public float GetActiveHealth()
    {
        float health = 0;
        if (Vulnerable && !CanBreak() && Health != 0)
        {
            health = MaxHealth;
        }

        foreach (DestroyableSegment child in Children)
        {
            health += child.GetActiveHealth();
        }

        return health;
    }

    /// <summary>Damage (00442960): a vulnerable segment loses health (to 0), noting when and how much.</summary>
    public virtual void Damage(float amount, Thing? by)
    {
        if (Vulnerable)
        {
            Health -= amount;
            LastDamageTime = Context.Events.Time;
            LastDamageAmount = amount;
            if (Health < 0)
            {
                Health = 0;
            }
        }
    }

    /// <summary>
    /// ScheduleChildBreaks (004429a0): under a core the children break at once; under a swap within half a second either
    /// way, otherwise within a tenth (main-thread rand).
    /// </summary>
    public void ScheduleChildBreaks()
    {
        foreach (DestroyableSegment child in Children.ToList())
        {
            if (Type == SegmentType.Core)
            {
                child.Break();
            }
            else if (Type == SegmentType.Swap)
            {
                float time = Context.Events.Time + (0.5f + ((float)((Context.Crt.Rand() & 0xff) - 128) * (1.0f / 256.0f)));
                Context.Events.AddEvent(BreakEvent, child, time);
            }
            else
            {
                float time = Context.Events.Time + (0.1f + ((float)((Context.Crt.Rand() & 0xff) - 128) * (1.0f / 1280.0f)));
                Context.Events.AddEvent(BreakEvent, child, time);
            }
        }
    }

    /// <summary>ActivateAll (00442a80): this and every descendant vulnerable.</summary>
    public void ActivateAll()
    {
        Vulnerable = true;
        foreach (DestroyableSegment child in Children)
        {
            child.ActivateAll();
        }
    }

    /// <summary>DamageChildren (00442ac0).</summary>
    public void DamageChildren(float amount)
    {
        foreach (DestroyableSegment child in Children.ToList())
        {
            child.Damage(amount, Controller.Owner);
        }
    }

    /// <summary>
    /// Break (00442b20): destroyed, the effects on its part's children killed and the spawners mounted there removed (none
    /// are made in this core), then the children's breaks scheduled.
    /// </summary>
    public virtual void Break()
    {
        Destroyed = true;
        Health = 0;
        Controller.Changed = true;
        if (Controller.Owner.RenderThing is not { MeshOrNull: { } mesh } rt)
        {
            return;
        }

        MeshPart part = mesh.Parts[Part];
        for (int n = 0; n < part.NumChildren; n++)
        {
            MeshPart child = part.Children[n]!;
            if (rt.FindEffectLink(child) is { } link)
            {
                link.Kill();
            }
        }

        ScheduleChildBreaks();
    }

    /// <summary>
    /// Update (00442d40): an intact segment now and then (one in 60, GAME random) throws a pickup explosion from a random
    /// point of its part's box; then its children's.
    /// </summary>
    public virtual void Update()
    {
        MeshPart? part = Controller.Owner.RenderThing?.MeshOrNull?.Parts[Part];
        if (!Destroyed && part is not null && Context.Random.Random() % 60 == 1)
        {
            part.GetWorldTransforms(0, 0, 0, Controller.Owner, out FMatrix ori, out FVector pos, false, true, Context.TransformStatics);
            BoundingBox box = Controller.PartBox(part);
            float x = ((float)(Context.Random.Random() % 65536) * box.Axes.X * (2.0f / 65536.0f)) - box.Axes.X;
            float y = ((float)(Context.Random.Random() % 65536) * box.Axes.Y * (2.0f / 65536.0f)) - box.Axes.Y;
            float z = ((float)(Context.Random.Random() % 65536) * box.Axes.Z * (2.0f / 65536.0f)) - box.Axes.Z;
            FVector offset = box.Origin + new FVector(x, y, z);
            SpawnPickup(ori.Transform(offset) + pos);
        }

        foreach (DestroyableSegment child in Children.ToList())
        {
            child.Update();
        }
    }

    /// <summary>SpawnPickup (00442710): the owner's explosion at that point, on the ground, not hitting the owner (or buildings).</summary>
    public void SpawnPickup(FVector pos)
    {
        Unit owner = Controller.Owner;
        if (owner.UnitData is { Explosion: not 0 } data)
        {
            Explosion? explosion = Explosion.Spawn(Context, data.Explosion);
            var init = new InitThing
            {
                Behaviour = data.Explosion >= 0 && data.Explosion < Context.Physics.Explosions.Count ? Context.Physics.Explosions[data.Explosion] : null,
                ColType = 1,
                Allegiance = owner.Allegiance,
            };
            if (explosion is not null)
            {
                init.Pos = pos;
                if (Controller.Unknown28 == 1)
                {
                    init.InitCst.NotSeekCollisionWith = ThingType.Everything;
                }
                else
                {
                    init.InitCst.NotSeekCollisionWith |= ThingType.Building;
                }

                init.InitCst.IgnoreThing = owner;
                explosion.Init(init);
            }
        }
    }

    /// <summary>
    /// SpawnRubble (00442f60): three rubble pieces per unit of size from random points of the part's box (GAME randoms),
    /// every twelfth with a landscape mark and a pickup explosion, each flying out from the owner at up to 0.07 (GAME
    /// random) as the building's rubble mesh (a building's pick draws main-thread rand).
    /// </summary>
    public virtual void SpawnRubble()
    {
        Unit owner = Controller.Owner;
        if (owner.RenderThing?.MeshOrNull is not { } mesh || mesh.Parts[Part] is not { } part)
        {
            return;
        }

        UnitData data = owner.UnitData!;
        ParticleDescriptor? pd = Context.Particles?.Manager.Set.GetPD("Generic Mesh");
        int count = Numerics.X87.Fist(Size * 3.0f);
        part.GetWorldTransforms(0, 0, 0, owner, out FMatrix ori, out FVector partPos, false, true, Context.TransformStatics);
        BoundingBox box = Controller.PartBox(part);
        for (int n = 0; n < count; n++)
        {
            float x = ((float)(Context.Random.Random() % 65536) * box.Axes.X * (2.0f / 65536.0f)) - box.Axes.X;
            float y = ((float)(Context.Random.Random() % 65536) * box.Axes.Y * (2.0f / 65536.0f)) - box.Axes.Y;
            float z = ((float)(Context.Random.Random() % 65536) * box.Axes.Z * (2.0f / 65536.0f)) - box.Axes.Z;
            var point = new FVector(box.Origin.X + x, box.Origin.Y + y, box.Origin.Z + z);
            FVector pos = ori.Transform(point) + partPos;
            ParticleEffectLink? link = Context.Particles is { } particles ? new ParticleEffectLink(particles.Manager) : null;
            if (data.Explosion != 0 && n % 12 == 0)
            {
                Context.Skip("ENGINE.AddDamage (a landscape mark under the rubble)");
                SpawnPickup(pos);
            }

            if (link is not null)
            {
                Context.Particles!.Manager.CreateEffect(pd, link, new FVector(0, 0, 0), false, false);
                link.SetPos(pos);
            }

            FVector vel = pos - owner.Pos;
            vel.Normalise();
            vel *= ((float)(Context.Random.Random() % 65536) * (1.0f / 65536.0f)) * 0.07f;
            if (link?.Effect?.Particle is { } mp)
            {
                mp.Velocity = vel;
                if (owner is Building building && building.PickRubble() is { } rubble)
                {
                    mp.Data = rubble;
                    mp.Radius = rubble.Radius;
                }
                else if (owner is not Building)
                {
                    Context.Skip("CDestroyableSegment::SpawnRubble: \"WOOPS: You forgot to set rubble\" (a log line)");
                }
            }

            link?.Release();
        }
    }

    public override void HandleEvent(Event e)
    {
        if (e.EventNum == BreakEvent)
        {
            Break();
        }
    }

    /// <summary>GetTotalHealth: its own health unless it could break, and its children's.</summary>
    public float GetTotalHealth()
    {
        float health = 0;
        if (Vulnerable && !CanBreak())
        {
            health = Health;
        }

        foreach (DestroyableSegment child in Children)
        {
            health += child.GetTotalHealth();
        }

        return health;
    }
}

/// <summary>CDestroyableCoreSegment: a "core" part; the primary core (number 1) has no health of its own.</summary>
public sealed class DestroyableCoreSegment(DestructableSegments controller, int part, DestroyableSegment? parent, float size, int number)
    : DestroyableSegment(controller, part, parent, size, SegmentType.Core)
{
    public int Number { get; } = number;
    public float TotalTime;
    public float Time;
    public bool Cascading;

    public override bool CanBreak() => Cascading || base.CanBreak();

    public override void SetLife(float life, float total) => MaxHealth = Health = Number == 1 ? 0 : Size / total * life * 5.0f;

    public override float GetCoreTime() => Time;

    private ThingContext Ctx => Controller.Owner.Context;

    /// <summary>AreChildrenDestroyed (004433f0): every core child destroyed (true with no children).</summary>
    public bool AreChildrenDestroyed()
    {
        if (Children.Count == 0)
        {
            Ctx.Skip("CDestroyableCoreSegment: \"Warning: First core part has no children\" (a log line)");
            return true;
        }

        return Children.All(c => c.Type != SegmentType.Core || c.Destroyed);
    }

    /// <summary>Damage (004435f0): the primary core takes none; a core out of health throws its rubble and breaks.</summary>
    public override void Damage(float amount, Thing? by)
    {
        if (Number == 1)
        {
            return;
        }

        base.Damage(amount, by);
        if (Health <= 0 && !Destroyed)
        {
            SpawnRubble();
            Break();
        }
    }

    /// <summary>Break (00443660): out of health it breaks; otherwise, when it cannot yet, it starts falling (its parent's time).</summary>
    public override void Break()
    {
        if (Destroyed)
        {
            return;
        }

        if (Health == 0.0)
        {
            base.Break();
            return;
        }

        if (!CanBreak())
        {
            Cascading = true;
            Time = Parent!.GetCoreTime();
            Ctx.Events.AddEvent(CascadeEvent, this, EventManager.NextFrame);
        }
    }

    /// <summary>HandleEvent (004436d0): a falling core speeds up under the owner's gravity until it has fallen far enough, then breaks.</summary>
    public override void HandleEvent(Event e)
    {
        if (e.EventNum == CascadeEvent)
        {
            Controller.Changed = true;
            Time += Controller.Owner.Gravity();
            TotalTime += Time;
            if (TotalTime < Controller.Unknown14)
            {
                Ctx.Events.AddEvent(CascadeEvent, this, EventManager.NextFrame);
                Update();
            }
            else
            {
                Cascading = false;
                SpawnRubble();
                base.Break();
            }
        }
        else if (e.EventNum == BreakEvent)
        {
            Break();
        }
    }
}

/// <summary>CDestroyableSwapSegment: a part with damage levels, drawn by stage.</summary>
public sealed class DestroyableSwapSegment(DestructableSegments controller, int part, DestroyableSegment? parent, float size, int stages)
    : DestroyableSegment(controller, part, parent, size, SegmentType.Swap)
{
    public int NumStages { get; } = stages;

    public bool Swapped;

    /// <summary>Damage (00443780): a new damage stage throws rubble (the first schedules the children's breaks) and halves its size.</summary>
    public override void Damage(float amount, Thing? by)
    {
        int stage = GetDamageStage();
        base.Damage(amount, by);
        if (stage != GetDamageStage())
        {
            SpawnRubble();
            if (stage == 0)
            {
                ScheduleChildBreaks();
            }

            Size *= 0.5f;
            Controller.Changed = true;
        }

        if (Health == 0)
        {
            Break();
        }
    }

    /// <summary>Break (00443810): its rubble once, then broken.</summary>
    public override void Break()
    {
        if (!Swapped)
        {
            SpawnRubble();
            Swapped = true;
        }

        base.Break();
    }

    public override int GetDamageStage()
    {
        if (MaxHealth != 0)
        {
            int stage = (int)Math.Floor(((MaxHealth - Health) / MaxHealth * NumStages) + 0.5f);
            if (stage > NumStages)
            {
                stage = NumStages;
            }

            if (stage >= 0)
            {
                return stage;
            }
        }

        return 0;
    }
}

/// <summary>CDestroyableExtraSegment: an ordinary part; destroyed, it flies off away from what destroyed it.</summary>
public class DestroyableExtraSegment(DestructableSegments controller, int part, DestroyableSegment? parent, float size, SegmentType type = SegmentType.Extra)
    : DestroyableSegment(controller, part, parent, size, type)
{
    /// <summary>mDirection / mHasDirection: away from what destroyed it, at 0.03.</summary>
    public FVector Direction;

    public bool HasDirection;

    /// <summary>Damage (00443890): destroyed, it takes the destroyer's direction of travel and breaks.</summary>
    public override void Damage(float amount, Thing? by)
    {
        base.Damage(amount, by);
        if (Health <= 0 && !Destroyed)
        {
            FVector oldPos = by is Actor a ? a.OldPos : by!.Pos;
            Direction = by!.Pos - oldPos;
            Direction.Normalise();
            Direction *= 0.03f;
            HasDirection = true;
            Break();
        }
    }

    /// <summary>Break (004439c0): with cores, its rubble first.</summary>
    public override void Break()
    {
        if (!Destroyed && Controller.Cores > 0)
        {
            SpawnRubble();
        }

        base.Break();
    }
}

/// <summary>CDestroyableExtraUseMeshSegment: an "x1" part; with cores it starts all but broken, and flies off as its own mesh.</summary>
public sealed class DestroyableExtraUseMeshSegment(DestructableSegments controller, int part, DestroyableSegment? parent, float size)
    : DestroyableExtraSegment(controller, part, parent, size, SegmentType.ExtraUseMesh)
{
    /// <summary>
    /// SpawnRubble (00443a20): no ordinary pieces (its size zeroed); the part's own mesh flies off as one particle along
    /// its direction (or out from the owner at up to 0.03, a GAME random), with a pickup explosion and a random spin (three
    /// GAME randoms).
    /// </summary>
    public override void SpawnRubble()
    {
        Size = 0;
        base.SpawnRubble();
        Unit owner = Controller.Owner;
        if (owner.RenderThing?.MeshOrNull is not { } mesh || mesh.Parts[Part] is not { } part)
        {
            return;
        }

        ThingContext context = owner.Context;
        ParticleDescriptor? pd = context.Particles?.Manager.Set.GetPD("Generic Mesh");
        ParticleEffectLink? link = context.Particles is { } particles ? new ParticleEffectLink(particles.Manager) : null;
        if (link is not null)
        {
            context.Particles!.Manager.CreateEffect(pd, link, new FVector(0, 0, 0), false, false);
        }

        part.GetWorldTransforms(0, 0, 0, owner, out FMatrix ori, out FVector pos, false, true, context.TransformStatics);
        BoundingBox box = Controller.PartBox(part);
        pos = ori.Transform(box.Origin) + pos;
        FVector vel = Direction;
        if (!HasDirection)
        {
            vel = pos - owner.Pos;
            vel.Normalise();
            vel *= ((float)(context.Random.Random() % 65536) * (1.0f / 65536.0f)) * 0.03f;
        }

        if (owner.UnitData!.Explosion != 0)
        {
            SpawnPickup(pos);
        }

        if (link is { Full: true, Effect.Particle: { } mp })
        {
            link.SetPos(pos);
            link.SetOrientation(ori);
            float a = (float)(context.Random.Random() % 65536) * (1.0f / 65536.0f);
            float b = (float)(context.Random.Random() % 65536) * (1.0f / 65536.0f);
            float c = (float)(context.Random.Random() % 65536) * (1.0f / 65536.0f);
            mp.U0 = a * 0.04f;
            mp.V0 = b * 0.04f;
            mp.U1 = c * 0.04f;
            mp.Velocity = vel;
            mp.Data = mesh;
            mp.Unknown7E = (short)Part;
            mp.Radius = box.Radius;
        }

        link?.Release();
    }

    public override void SetLife(float life, float total)
    {
        if (Controller.Cores > 0)
        {
            Health = 0.001f;
        }
        else
        {
            base.SetLife(life, total);
        }
    }
}

/// <summary>
/// CDestructableSegmentsController: one segment per collision part, built from the mesh's part tree ("core"/"CORE" parts
/// are cores, parts with damage levels swap, "x1"/"X1" parts use their mesh, the rest are extras).
/// </summary>
public sealed class DestructableSegments(Unit owner, float unknown14, bool unknown24, int unknown28)
{
    public Unit Owner { get; } = owner;
    public float Unknown14 { get; } = unknown14;
    public bool Unknown24 { get; } = unknown24;
    public int Unknown28 { get; } = unknown28;
    public DestroyableCoreSegment? Core;
    public DestroyableSegment?[] Segments = [];
    public float TotalSize;

    /// <summary>mUnknown20: the cores made (the controller is kept only with one or more).</summary>
    public int Cores;

    public float TotalHealth;
    public bool Changed = true;

    /// <summary>Init (00444660): the segments from the mesh's part tree, then each one's life.</summary>
    public void Init()
    {
        if (Owner.RenderThing?.MeshOrNull is not { } mesh)
        {
            return;
        }

        Segments = new DestroyableSegment?[mesh.NumParts + 1];
        ProcessNode(mesh.Parts[0], null);
        // The attached components' segments: units with components throw at CUnit::Init (components are not ported).
        float life = Owner.UnitData?.Life ?? 100.0f;
        foreach (DestroyableSegment? s in Segments)
        {
            s?.SetLife(life, TotalSize);
        }

        if (Core is not null)
        {
            TotalHealth = Core.GetTotalHealth();
        }
    }

    /// <summary>
    /// GetCentre (00445070): the live core segment with the highest number, at its part's pose in the world (evaluated as
    /// retail does, through the render thing's pose); without one, the owner's centre position.
    /// </summary>
    public FVector GetCentre()
    {
        RenderThing rt = Owner.RenderThing!;
        MeshData mesh = rt.Mesh;
        int best = 0;
        MeshPart? bestPart = null;
        for (int n = 0; n < mesh.NumParts; n++)
        {
            MeshPart? part = n < mesh.Parts.Count ? mesh.Parts[n] : null;
            if (part is not null && part.PartNumber < Segments.Length && Segments[part.PartNumber] is DestroyableCoreSegment
                {
                    Type: SegmentType.Core, Health: > 0,
                } core && core.Number > best)
            {
                best = core.Number;
                bestPart = part;
            }
        }

        if (bestPart is not null)
        {
            var source = (IMeshCollisionSource)rt;
            MeshPose.EvaluatePose(mesh, source.Pose, source.Subject, bestPart, source.Statics, out FVector pos, out _, false, false);
            return pos;
        }

        return Owner.GetCentrePos();
    }

    private static bool Starts(string name, string lower, string upper) =>
        name.StartsWith(lower, StringComparison.Ordinal) || name.StartsWith(upper, StringComparison.Ordinal);

    private SegmentType TypeOf(MeshPart part) =>
        Starts(part.Name, "core", "CORE") ? SegmentType.Core
        : part.NumChainedParts > 0 ? SegmentType.Swap
        : Starts(part.Name, "x1", "X1") ? SegmentType.ExtraUseMesh
        : SegmentType.Extra;

    private DestroyableSegment CreateSegment(SegmentType type, MeshPart part, DestroyableSegment? parent, float size)
    {
        switch (type)
        {
            case SegmentType.Core:
                Cores++;
                return new DestroyableCoreSegment(this, part.PartNumber, parent, size, Cores);
            case SegmentType.Swap:
                return new DestroyableSwapSegment(this, part.PartNumber, parent, size, part.NumChainedParts);
            case SegmentType.ExtraUseMesh:
                return new DestroyableExtraUseMeshSegment(this, part.PartNumber, parent, size);
            default:
                return new DestroyableExtraSegment(this, part.PartNumber, parent, size);
        }
    }

    /// <summary>ProcessNode (00444c10): a collision part makes a segment under its parent's; its children follow.</summary>
    private void ProcessNode(MeshPart part, DestroyableSegment? parent)
    {
        DestroyableSegment? segment = parent;
        if (part.GetMeshDataPart() is { PartType: 1 } collision && part.IsChained == 0)
        {
            FVector axes = collision.BoundingBox!.Axes;
            float size = axes.X;
            if (axes.Y > size)
            {
                size = axes.Y;
            }

            if (axes.Z > size)
            {
                size = axes.Z;
            }

            SegmentType type = TypeOf(part);
            if (parent is null)
            {
                segment = CreateSegment(type, part, null, size);
                if (segment.Type == SegmentType.Core)
                {
                    if (Cores > 1)
                    {
                        return; // "found second start core part"
                    }

                    Core = (DestroyableCoreSegment)segment;
                }
                else
                {
                    return; // an unlinked part at the root ends the walk here
                }
            }
            else
            {
                TotalSize += size;
                segment = CreateSegment(type, part, parent, size);
                parent.Children.Insert(0, segment); // RegisterChild: SPtrSet::Add puts it at the head
            }

            Segments[part.PartNumber] = segment;
            if (part.NumChainedParts > 0)
            {
                for (MeshPart? level = part.NextMeshPartInChain; level is not null; level = level.NextMeshPartInChain)
                {
                    Segments[level.PartNumber] = segment;
                }
            }
        }

        for (int n = 0; n < part.NumChildren; n++)
        {
            ProcessNode(part.Children[n]!, segment);
        }
    }

    public int GetDamageStage(int part) => Segments[part]?.GetDamageStage() ?? 0;

    /// <summary>mUnknown2C: the unit is down to half its segments' life.</summary>
    public bool Unknown2C;

    /// <summary>A part's bounding box (the mesh data part's when the instance has none).</summary>
    public BoundingBox PartBox(MeshPart part) => part.BoundingBox ?? part.GetMeshDataPart()!.BoundingBox!;

    private bool NoneVulnerable() => !Segments.Any(s => s is { Vulnerable: true });

    /// <summary>GetLife (00444330): the core tree's active health (1 when no segment is vulnerable).</summary>
    public float GetLife() => NoneVulnerable() ? 1.0f : Core!.GetActiveHealth();

    /// <summary>GetLifeUnknown444370: the core tree's total health (1 when none is vulnerable).</summary>
    public float GetLifeTotal() => NoneVulnerable() ? 1.0f : Core!.GetTotalHealth();

    /// <summary>GetLifeUnknown4443b0: the health it started with (1 when none is vulnerable).</summary>
    public float GetLifeStart() => NoneVulnerable() ? 1.0f : TotalHealth;

    public float GetLastDamageTime(int part) => part != -1 && Segments[part] is { } s ? s.LastDamageTime : -1.0f;

    public float GetLastDamageAmount(int part) => part != -1 && Segments[part] is { } s ? s.LastDamageAmount : 0.0f;

    /// <summary>
    /// Damage (00444030): the hit part's segment takes it; with every core child gone, or the cores' active health under
    /// 30% of the start, the owner starts dying. Under half its life the unit is marked.
    /// </summary>
    public void Damage(int part, float amount, Thing? by)
    {
        if (part != -1)
        {
            DestroyableSegment? segment = Segments[part];
            if (segment is not null)
            {
                segment.Damage(amount, by);
                if (Core!.AreChildrenDestroyed() || TotalHealth * 0.3 > Core.GetActiveHealth())
                {
                    Owner.StartDieProcess();
                }
            }
            else if (Owner.RenderThing?.MeshOrNull is not null)
            {
                Owner.Context.Skip("CDestructableSegmentsController::Damage: \"WARNING: building part not found\" (a log line)");
            }
        }

        if (!Unknown2C && GetLife() < GetLifeStart() * 0.5f)
        {
            Unknown2C = true;
        }
    }

    /// <summary>Unknown4443f0 (004443f0): at the owner's death, unless already ruined, every segment vulnerable and its children destroyed.</summary>
    public void OwnerDying()
    {
        if (Core!.AreChildrenDestroyed() || TotalHealth * 0.3 > Core.GetActiveHealth())
        {
            return;
        }

        Core.ActivateAll();
        Core.DamageChildren(100000.0f);
        Unknown2C = true;
    }

    /// <summary>CanUse (00444f20): the part's segment is intact (an emitter part asks its parent's).</summary>
    public bool CanUse(int part)
    {
        DestroyableSegment? segment = Segments[part];
        if (segment is null)
        {
            if (Owner.RenderThing?.MeshOrNull is not { } mesh)
            {
                return true;
            }

            MeshPart? p = part < mesh.Parts.Count ? mesh.Parts[part] : null;
            if (p is { PartType: 5, Parent: { } pp } && Segments[pp.PartNumber] is { } ps)
            {
                return !ps.Destroyed;
            }

            return true;
        }

        if (!Unknown24 && segment is DestroyableCoreSegment { Number: 1 }
            && (Core!.AreChildrenDestroyed() || TotalHealth * 0.3 > Core.GetActiveHealth()))
        {
            return false;
        }

        return !segment.Destroyed;
    }

    /// <summary>GetCoreFall (00445010): how far a core part has fallen.</summary>
    public float GetCoreFall(int part)
    {
        if (Cores > 0 && Segments[part] is DestroyableCoreSegment core)
        {
            return core.TotalTime;
        }

        return 0.0f;
    }
}

/// <summary>CDestructableSegmentsMotionController: falling cores drop and lean; broken and chained parts are hidden.</summary>
public sealed class SegmentsMotionController(DestructableSegments segments) : IPartController
{
    public DestructableSegments Segments { get; } = segments;

    /// <summary>GetPartTransform (00494ce0): a falling core drops, and leans the further it has fallen.</summary>
    public void GetPartTransform(MeshPart part, ref FVector pos, ref FMatrix ori, bool world)
    {
        float fall = Segments.GetCoreFall(part.PartNumber);
        pos.Z += fall;
        if (fall > 0.0)
        {
            float lean = fall;
            if (lean > 2.0f)
            {
                lean = 2.0f;
            }

            ori = ori.Multiply(FMatrix.FromYawPitchRoll(0, lean / 25.0f, lean / 14.0f));
        }

        Segments.Changed = false;
    }

    /// <summary>Unknown6 (00494fa0): a chained part, or one whose segment cannot be used, is hidden.</summary>
    public void Unknown6(MeshPart part, ref int flags)
    {
        if (part.IsChained != 0)
        {
            flags |= 1;
            return;
        }

        if (!Segments.CanUse(part.PartNumber))
        {
            flags |= 1;
        }
        else
        {
            flags &= ~1;
        }
    }

    /// <summary>Unknown7 (00494ff0): the part's damage stage.</summary>
    public int Unknown7(MeshPart part) => part.IsChained != 0 ? 0 : Segments.GetDamageStage(part.PartNumber);

    /// <summary>Unknown9 (004fdc10): the animation loops.</summary>
    public bool Unknown9() => true;
}
