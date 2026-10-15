// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// Shared CThing flags and type composition, without CActor pose, motion or
/// contact fields. Source: thing.cpp:28-37,183-204,423-435,742-745 and
/// thing.h:41-52,268-275 at the pinned Onslaught source revision.
/// </summary>
internal sealed class ThingBaseState(uint lineage, uint specificTypeMask,
    ThingActorFlags flags = ThingActorFlags.None)
{
    public ThingActorFlags Flags { get; private set; } = flags;
    public uint TypeMask { get; private set; } = lineage | specificTypeMask;

    public void SetThingType(uint specificTypeMask) => TypeMask = lineage | specificTypeMask;
    public void AddType(uint mask) => TypeMask |= mask;
    public void AddFlags(ThingActorFlags mask) => Flags |= mask;
    public void MakeInvisible() => Flags |= ThingActorFlags.Invisible;
    public void MakeVisible() => Flags &= ~ThingActorFlags.Invisible;

    public bool DeclareShutdown()
    {
        if ((Flags & ThingActorFlags.DeclaredShutdown) != 0) return false;
        Flags |= ThingActorFlags.DeclaredShutdown;
        return true;
    }

    public bool StartDieProcess()
    {
        if (!MarkDying()) return false;
        DeclareShutdown();
        return true;
    }

    public bool MarkDying()
    {
        if ((Flags & ThingActorFlags.Dying) != 0) return false;
        Flags |= ThingActorFlags.Dying;
        return true;
    }
}
