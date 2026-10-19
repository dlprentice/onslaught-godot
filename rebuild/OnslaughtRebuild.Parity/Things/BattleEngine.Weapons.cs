// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/BattleEngine.cpp: ChangeWeapon 00409f70 and CountActiveWeapons 0040dc90,
// plus BattleEngineWalkerPart.cpp: CountActiveWeapons 00414b70 and GetWeaponIconName 004145f0.

using System.Text;

namespace OnslaughtRebuild.Parity.Things;

public sealed partial class BattleEngine
{
    /// <summary>mChangedWeaponTime: constructor -20; every admitted ChangeWeapon records the event time.</summary>
    public float ChangedWeaponTime = -20.0f;

    /// <summary>CountActiveWeapons counts active entries and the primary, without ammunition or aug substitution.</summary>
    public int CountActiveWeapons()
    {
        if (State == BattleEngineState.Jet)
            return JetPart!.Weapons.Count(weapon => weapon.WeaponActive);
        BattleEngineWalkerPart walker = WalkerPart!;
        int count = walker.Weapons.Count(weapon => weapon.WeaponActive);
        if (walker.PrimaryWeapon is { WeaponActive: true }) count++;
        return count;
    }

    /// <summary>
    /// ChangeWeapon: with more than one active weapon, restore full movement and ask the current part to select;
    /// then record the time and announce the resulting weapon. A store can prevent selection even though this
    /// wrapper's active count admits the request: the time and announcement still occur in that case.
    /// </summary>
    public void ChangeWeapon()
    {
        if (CountActiveWeapons() <= 1) return;
        SlowMovement = false;
        if (State == BattleEngineState.Walker) WalkerPart!.ChangeWeapon();
        else if (State == BattleEngineState.Jet)
            JetPart!.ChangeWeapon();

        ChangedWeaponTime = Context.Events.Time;
        byte[]? icon = GetCurrentWeapon()?.Data.IconName;
        if (icon is null) return;
        // The retail comparison starts seven bytes into the icon name ("weapon\\"). No localization lookup.
        string name = Encoding.ASCII.GetString(icon.AsSpan(7));
        string? sample = name switch
        {
            "Vulcan Cannon" => "hud_Vulcan_Cannon",
            "Grenade" => "hud_Grenade_launcher",
            "Torpedo" => "hud_Torpedo_launcher",
            "Blaster" => "hud_Blaster",
            "Flux Pod" => "hud_Flux_Missile",
            "Micro Missile" => "hud_Micro_Missiles",
            "Spread Bomb" => "hud_Spread_Bomb",
            "Rail Gun" => AugActive ? "hud_Rail_Gun_augmented" : "hud_Rail_Gun",
            "Beam Laser" => AugActive ? "hud_beam_laser_augmented" : "hud_beam_laser",
            "Plasma Cannon" => AugActive ? "hud_Pulse_Cannon_augmented" : "hud_Pulse_Cannon",
            _ => null,
        };
        if (sample is not null) PlayHudSample(sample);
    }
}
