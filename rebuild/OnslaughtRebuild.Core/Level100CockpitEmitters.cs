// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// The Aquila Prototype cockpit's <c>Gun</c> emitters, from which every player
/// round starts, in the cockpit's model space (x right, y forward, z down, as
/// the Battle Engine's own axes) and micrometres.
/// </summary>
/// <remarks>
/// <para>
/// Evidence: the RE lane's aiming contract
/// (<c>reverse-engineering/game-mechanics/battle-engine-aiming.md</c>, "Gun
/// emitters", commits <c>0827d186</c> and <c>0aa1ceac</c>): the cockpit render
/// mesh is <c>cockpit2.msh</c> (SHA-256 prefix <c>008b9292</c>) in both modes;
/// an emitter's pose is its part's pose composed from HPOS/HORI at the
/// animation frame, <c>fly</c> 0 or <c>walk</c> 25. Guns 1-6 and 13-14 are the
/// same in both; Guns 9-12 hang from the animated <c>Object03</c> and
/// <c>Object01</c>. The positions were composed in double precision; the
/// runtime pose cache composes on the x87 and may differ in the last bits.
/// </para>
/// <para>
/// The weapon modes' <c>CWeaponLaunchSequence</c> pairs name the emitters:
/// <c>Mech Pulse Cannon Charged</c> and <c>… Charged 2</c> (1, 1);
/// <c>Mech Twin Vulcan Cannon</c> (1, 9) (2, 10) (3, 11) (4, 12);
/// <c>Mech Vulcan Cannon</c> (1, 13) (2, 14); both pod modes (1, 4) (2, 3)
/// (3, 5) (4, 2) (5, 6) (<c>data/default physics.dat</c>, <c>e1fb3ded…</c>).
/// Both Vulcans fire a volley exactly as long as their sequence, so their
/// sequence counter reaches the same slot at the start of every burst event
/// and needs no state of its own.
/// </para>
/// </remarks>
internal static class Level100CockpitEmitters
{
    internal readonly record struct Emitter(int XMicrometres, int YMicrometres, int ZMicrometres);

    internal const int PulseGun = 1;

    internal static ReadOnlySpan<int> TwinVulcanSequence => [9, 10, 11, 12];

    internal static ReadOnlySpan<int> MechVulcanSequence => [13, 14];

    internal static Emitter Gun(int index, bool walkPose) => index switch
    {
        1 => new(92, 84_264, -258_033),
        2 => new(-88_353, 53_973, -210_330),
        3 => new(-56_485, 68_421, -227_710),
        4 => new(359, 82_906, -247_449),
        5 => new(45_415, 70_152, -228_438),
        6 => new(87_423, 56_066, -211_729),
        9 => walkPose ? new(-234_618, 274_872, 5_708) : new(-234_618, 41_412, 5_708),
        10 => walkPose ? new(-235_592, 275_150, 55_849) : new(-235_592, 41_690, 55_849),
        11 => walkPose ? new(224_845, 274_930, 5_700) : new(224_845, 41_470, 5_700),
        12 => walkPose ? new(223_872, 275_150, 55_849) : new(223_872, 41_690, 55_849),
        13 => new(-82_423, -20_411, 185_881),
        14 => new(78_826, -20_411, 185_880),
        _ => throw new ArgumentOutOfRangeException(nameof(index), $"cockpit2.msh has no Gun {index} emitter."),
    };
}
