// SPDX-License-Identifier: GPL-3.0-or-later
// A segmented unit's destructable segments and their motion controller. Adapted from bea-decomp
// src/DestructableSegmentsController.cpp (the constructor 00443fc0, Init 00444660, CreateSegment 004449c0, ProcessNode
// 00444c10, GetDamageStage 00444f00, CanUse 00444f20, GetCoreFall 00445010), DestroyableSegment.cpp/.h (the segments'
// constructors, SetLife, GetTotalHealth 00442900, CanBreak, CDestroyableSwapSegment::GetDamageStage 00443830) and
// MCDestructableSegments.cpp (GetPartTransform 00494ce0, Unknown6 00494fa0, Unknown7 00494ff0, Unknown9).
// The damage, breaking, rubble and cascade paths are not ported: a unit that is damaged throws where they would run.

using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;

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
public class DestroyableSegment(DestructableSegments controller, int part, DestroyableSegment? parent, float size, SegmentType type)
{
    public int Part { get; } = part;
    public float Health = 1.0f;
    public float MaxHealth = 1.0f;
    public float LastDamageTime = -1.0f;
    public float LastDamageAmount;
    public bool Vulnerable = true;
    public DestroyableSegment? Parent { get; } = parent;
    public List<DestroyableSegment> Children { get; } = [];
    public float Size { get; } = size;
    public bool Destroyed;
    public SegmentType Type { get; } = type;
    protected DestructableSegments Controller { get; } = controller;

    public virtual int GetDamageStage() => 0;

    public virtual bool CanBreak() => Parent is { Destroyed: false } p && p.CanBreak();

    public virtual void SetLife(float life, float total) => MaxHealth = Health = Size / total * life;

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
}

/// <summary>CDestroyableSwapSegment: a part with damage levels, drawn by stage.</summary>
public sealed class DestroyableSwapSegment(DestructableSegments controller, int part, DestroyableSegment? parent, float size, int stages)
    : DestroyableSegment(controller, part, parent, size, SegmentType.Swap)
{
    public int NumStages { get; } = stages;

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

/// <summary>CDestroyableExtraUseMeshSegment: an "x1" part; with cores it starts all but broken.</summary>
public sealed class DestroyableExtraUseMeshSegment(DestructableSegments controller, int part, DestroyableSegment? parent, float size)
    : DestroyableSegment(controller, part, parent, size, SegmentType.ExtraUseMesh)
{
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
                return new DestroyableSegment(this, part.PartNumber, parent, size, SegmentType.Extra);
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
                parent.Children.Add(segment);
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

        if (!Unknown24 && segment is DestroyableCoreSegment { Number: 1 })
        {
            throw new NotSupportedException("CDestructableSegmentsController::CanUse: the primary core's cascade test is not ported");
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
            throw new NotSupportedException("CDestructableSegmentsMotionController::GetPartTransform: a falling core's lean is not ported");
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
