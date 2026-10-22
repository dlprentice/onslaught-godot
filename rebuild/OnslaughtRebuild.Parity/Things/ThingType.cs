// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/ThingType.h and src/waypoint.h (THING_TYPE_WAYPOINT). Not in Stuart's release.

namespace OnslaughtRebuild.Parity.Things;

/// <summary>THING_TYPE_*: the bits a thing's SetThingType chain ORs together (CThing::mThingType).</summary>
public static class ThingType
{
    public const uint None = 0x00000000;
    public const uint Thing = 0x00000001;
    public const uint Actor = 0x00000002;
    public const uint Ammunition = 0x00000004;
    public const uint BattleEngine = 0x00000008;
    public const uint Unit = 0x00000010;
    public const uint Unknown20 = 0x00000020;
    public const uint Stationary = 0x00000020;
    public const uint Animal = 0x00000040;
    public const uint Building = 0x00000100;
    public const uint GroundUnit = 0x00000200;
    public const uint AirUnit = 0x00000400;
    public const uint Mech = 0x00000800;
    public const uint Waypoint = 0x00001000;
    public const uint NamedMesh = 0x00002000;
    public const uint Infantry = 0x00004000;
    public const uint Naval = 0x00008000;
    public const uint Cutscene = 0x00010000;
    public const uint Vehicle = 0x00020000;
    public const uint Emplacement = 0x00040000;
    public const uint Component = 0x00080000;
    public const uint CanBeWalkedOn = 0x00100000;
    public const uint CanDestroyTrees = 0x00200000;
    public const uint Feature = 0x00400000;
    public const uint Renderable = 0x00800000;
    public const uint Explosion = 0x01000000;
    public const uint Tree = 0x02000000;
    public const uint Unknown04000000 = 0x04000000;
    public const uint Unknown08000000 = 0x08000000;
    public const uint Unknown10000000 = 0x10000000;
    public const uint Unknown20000000 = 0x20000000;
    public const uint Unknown40000000 = 0x40000000;
    public const uint ComplexThing = 0x80000000;
    public const uint Everything = 0xffffffff;
}
