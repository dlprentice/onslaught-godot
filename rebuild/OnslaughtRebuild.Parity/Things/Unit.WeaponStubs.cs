// SPDX-License-Identifier: GPL-3.0-or-later
// TEMPORARY: the weapon side of CUnit until the weapon runtime (Things/Unit.Weapons.cs) lands; every member throws, so
// nothing runs on a guessed default. Delete this file when the weapon runtime is merged.

using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Parity.Things;

public abstract partial class Unit
{
    private bool WeaponsAreAThreat() => Weapons.Count == 0 ? false : throw NotPorted();

    private bool WeaponCanReach(Unit target) => Weapons.Count == 0 ? false : throw NotPorted();

    public void Unknown4fb840(Thing target) => throw NotPorted();

    public float GetMinRange(FVector target) => throw NotPorted();

    public float GetMaxRange(FVector target) => throw NotPorted();

    public bool Unknown4fb5a0(ISquadUnit target) => throw NotPorted();

    public bool AnyWeaponUnknown94() => throw NotPorted();

    private static NotSupportedException NotPorted() => new("the weapon runtime is not ported yet");
}
