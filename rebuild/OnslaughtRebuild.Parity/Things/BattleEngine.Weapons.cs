// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/BattleEngine.cpp: ChangeWeapon 00409f70 and CountActiveWeapons 0040dc90,
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

    /// <summary>LoseWeaponCharge: the current part's weapon loses its charge.</summary>
    public void LoseWeaponCharge()
    {
        if (State == BattleEngineState.Walker)
        {
            WalkerPart!.LoseWeaponCharge();
        }
        else if (State == BattleEngineState.Jet)
        {
            JetPart!.LoseWeaponCharge();
        }
    }

    /// <summary>
    /// AugmentWeapon (0040de40): with the primary weapon's store holding heat or ammunition, the weapon is augmented (a
    /// current primary weapon losing its charge first, zooming out if its zoom mode changed), the augmentation full, and
    /// the HUD's "weapon augmented" sample played.
    /// </summary>
    public void AugmentWeapon()
    {
        Weapon primary = WalkerPart!.PrimaryWeapon!;
        int store = primary.Data.AmmoStore;
        if (StoreHeat[store] || StoreValue[store] > 0)
        {
            if (ReferenceEquals(primary, GetCurrentWeapon()))
            {
                int oldZoomMode = GetCurrentWeapon()!.Data.ZoomMode;
                SlowMovement = false;
                LoseWeaponCharge();
                if (oldZoomMode != GetCurrentWeapon()!.Data.ZoomMode)
                {
                    DesiredZoom = 1.0f;
                }
            }

            AugActiveTime = Context.Events.Time;
            AugValue = MaxAugValue;
            AugActive = true;
            PlayHudSample("hud_weapon_augmented");
            AugmentedTime = Context.Events.Time;
        }
    }

    /// <summary>UnaugmentWeapon: the augmented weapon, if current, loses its charge (zooming out on a mode change); the augmentation ends.</summary>
    public void UnaugmentWeapon()
    {
        if (ReferenceEquals(WalkerPart!.AugWeapon, GetCurrentWeapon()))
        {
            int oldZoomMode = GetCurrentWeapon()!.Data.ZoomMode;
            SlowMovement = false;
            LoseWeaponCharge();
            if (oldZoomMode != GetCurrentWeapon()!.Data.ZoomMode)
            {
                DesiredZoom = 1.0f;
            }
        }

        AugValue = 0.0f;
        AugActive = false;
    }
}
