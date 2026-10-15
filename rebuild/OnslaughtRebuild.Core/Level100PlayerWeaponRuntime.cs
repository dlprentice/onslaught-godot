// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// The released Level 100 player's configured weapon slots, active flags,
/// current selection and the Aquila's six ammunition and heat stores. Charge
/// is the Pulse Cannon Pod accumulator advanced by held
/// <see cref="SimActions.ChargeWeapon"/>. Each weapon retains its own float
/// ready time. Selections are base slots: Walker slot zero may resolve to the
/// augmented weapon at runtime.
/// </summary>
/// <remarks>
/// Stores follow the RE lane's contract
/// <c>reverse-engineering/game-mechanics/battle-engine-weapon-stores.md</c>
/// and the pinned source (<c>BattleEngine.cpp:303-312, :1708-1718</c>,
/// <c>BattleEngineJetPart.cpp:659-797</c>,
/// <c>BattleEngineWalkerPart.cpp:519-760</c>): ammo stores start full and
/// heat stores empty; a burst event spends once, before its volley; heat
/// stores cool by 1 on every Move and clear overheat below three quarters of
/// capacity.
/// </remarks>
internal sealed class Level100PlayerWeaponRuntime
{
    private bool _pulseCannonActive;
    private bool _twinVulcanActive;
    private bool _mechVulcanActive;
    private bool _missilePodActive;
    private int _walkerSelection;
    private int _jetSelection;
    private RetailWeaponChargeTable _pulseCharge = Level100PulseCannonCharge.CreatePod();
    private int _pulseModeLevel;
    private float _twinVulcanReadyAt;
    private float _mechVulcanReadyAt;
    private RetailWeaponChargeTable _missilePod = Level100MissilePod.CreateCharge();
    private int _podModeLevel;
    private bool _podHasMode;
    private int _podBurstCount;
    private int _podSequenceCounter;
    private int _podAngleCounter;
    private readonly RetailWeaponStores _stores = new();
    private bool _shieldsRecharging;
    private float _ammoDepletedTime;
    private float _weaponOverheatedTime;

    // CWeapon constructor's +0x64 store at 0x00505e6e, also used by the admitted Unit
    // attachment constructor. Configured Level 100 weapons have a mode.
    private const float InitialReadyAt = -200f;
    // Both released Vulcan modes carry CWeaponReloadTime 0x3d4ccccd.
    private const float VulcanReloadTime = 0.05f;
    // BattleEngine.cpp:329-330: mAmmoDepletedTime and mWeaponOverheatedTime.
    private const float InitialCueTime = -99999.0f;
    // kWeaponCoolRate is the integer 1 at 0x00622f08 and 0x006236a4.
    private const float CoolRate = 1.0f;

    /// <summary>
    /// Aquila Prototype's six (heat, capacity) store pairs, read at
    /// <c>data/battle engine configurations.dat</c> offset <c>0x35f</c>
    /// (SHA-256 <c>58722b12…</c>, record 3 @<c>0x2d2</c>, version 12):
    /// ammo 2000, ammo 100, heat 150, ammo 200, heat 100, heat 100.
    /// </summary>
    internal static ReadOnlySpan<int> AquilaStoreHeat => [0, 0, 1, 0, 1, 1];

    internal static ReadOnlySpan<float> AquilaStoreCapacity =>
        [2000.0f, 100.0f, 150.0f, 200.0f, 100.0f, 100.0f];

    internal Level100PlayerWeaponRuntime() => ResetConfiguration();

    internal uint PulseCannonChargeBits =>
        BitConverter.SingleToUInt32Bits(_pulseCharge.Charge);

    /// <summary>The Pulse Cannon Pod's charge-selected mode level.</summary>
    internal int PulseChargeLevel => RetailWeaponCharge.ModeLevel(_pulseCharge);

    /// <summary>The walker part's +0x14: false after a heat charge or shot this update.</summary>
    internal bool ShieldsRecharging => _shieldsRecharging;

    internal Level100PlayerWeaponStateSnapshot Snapshot => new(
        PulseCannonChargeBits,
        BitConverter.SingleToUInt32Bits(_pulseCharge.ReadyAtTime),
        BitConverter.SingleToUInt32Bits(_twinVulcanReadyAt),
        BitConverter.SingleToUInt32Bits(_mechVulcanReadyAt),
        BitConverter.SingleToUInt32Bits(_missilePod.ReadyAtTime));

    /// <summary>
    /// The Missile Pod's charge (<c>+0x60</c>), Fire level (<c>+0x68</c>),
    /// whether a mode has been set (<c>+0xa0</c>), burst counter
    /// (<c>+0x6c</c>) and launch-sequence and launch-angle counters
    /// (<c>+0x70</c>, <c>+0x74</c>).
    /// </summary>
    internal Level100MissilePodSnapshot PodSnapshot => new(
        BitConverter.SingleToUInt32Bits(_missilePod.Charge),
        _podModeLevel,
        _podHasMode,
        _podBurstCount,
        _podSequenceCounter,
        _podAngleCounter);

    internal Level100PlayerStoresSnapshot StoresSnapshot => new(
        BitConverter.SingleToUInt32Bits(_stores.StoreValue[0]),
        BitConverter.SingleToUInt32Bits(_stores.StoreValue[1]),
        BitConverter.SingleToUInt32Bits(_stores.StoreValue[2]),
        BitConverter.SingleToUInt32Bits(_stores.StoreValue[3]),
        BitConverter.SingleToUInt32Bits(_stores.StoreValue[4]),
        BitConverter.SingleToUInt32Bits(_stores.StoreValue[5]),
        OverheatMask(),
        _shieldsRecharging,
        BitConverter.SingleToUInt32Bits(_ammoDepletedTime),
        BitConverter.SingleToUInt32Bits(_weaponOverheatedTime),
        _pulseModeLevel);

    internal Level100MissionWeapon WalkerSelectedWeapon =>
        WeaponAt(VehicleMode.Walker, _walkerSelection);

    internal Level100MissionWeapon JetSelectedWeapon =>
        WeaponAt(VehicleMode.Jet, _jetSelection);

    internal void ResetConfiguration()
    {
        _pulseCannonActive = true;
        _twinVulcanActive = true;
        _mechVulcanActive = true;
        _missilePodActive = true;
        _walkerSelection = 0;
        _jetSelection = 0;
        _pulseCharge = Level100PulseCannonCharge.CreatePod();
        _pulseCharge.ReadyAtTime = InitialReadyAt;
        _pulseModeLevel = 0;
        _twinVulcanReadyAt = InitialReadyAt;
        _mechVulcanReadyAt = InitialReadyAt;
        _missilePod = Level100MissilePod.CreateCharge();
        _missilePod.ReadyAtTime = InitialReadyAt;
        _podModeLevel = 0;
        _podHasMode = false;
        _podBurstCount = 0;
        // The CWeapon constructor 0x00505e00 stores -1 in +0x70 and +0x74
        // (0x00505e7b/0x00505e7e), so the first round uses the first slot.
        _podSequenceCounter = -1;
        _podAngleCounter = -1;
        for (int store = 0; store < RetailWeaponStores.StoreCount; store++)
        {
            _stores.StoreOverheat[store] = 0;
            _stores.StoreHeat[store] = AquilaStoreHeat[store];
            _stores.ConfigurationStoreValue[store] = AquilaStoreCapacity[store];
            _stores.StoreValue[store] = AquilaStoreHeat[store] == 0
                ? AquilaStoreCapacity[store]
                : 0.0f;
        }
        _shieldsRecharging = true;
        _ammoDepletedTime = InitialCueTime;
        _weaponOverheatedTime = InitialCueTime;
    }

    /// <summary>
    /// The held-input arm of Walker <c>0x00413CF0</c> and Jet
    /// <c>0x00411BF0</c> (<c>BattleEngineWalkerPart.cpp:519-557</c>). Returns a
    /// Fire request for a weapon that cannot charge, and for a charge that
    /// finds its heat store full; the caller uses the same Fire wrapper as a
    /// release.
    /// </summary>
    internal bool AdvanceCharge(VehicleMode mode, VehicleTransition transition, float now)
    {
        Level100MissionWeapon selected = GetCurrentWeapon(mode);
        if (transition != VehicleTransition.None || !IsActive(selected) ||
            !ReadyToFire(selected, now))
        {
            return false;
        }

        if (selected is Level100MissionWeapon.MechTwinVulcanCannon or
            Level100MissionWeapon.MechVulcanCannon)
        {
            return true;
        }

        RetailWeaponChargeTable charge = selected == Level100MissionWeapon.MissilePod
            ? _missilePod
            : _pulseCharge;
        (int store, float consumption) = StoreOf(selected);
        bool heat = _stores.StoreHeat[store] != 0;
        if (!(heat || _stores.StoreValue[store] > 0.0f) ||
            RetailWeaponCharge.FullyCharged(charge) ||
            _stores.StoreOverheat[store] != 0)
        {
            return false;
        }

        RetailWeaponCharge.Charge(charge);
        if (mode == VehicleMode.Walker)
        {
            _shieldsRecharging = false;
        }

        if (!heat)
        {
            return false;
        }

        // The store's overheat flag was clear to reach this point, so the
        // source's second test is its capacity alone.
        if (_stores.StoreValue[store] < _stores.ConfigurationStoreValue[store])
        {
            _stores.StoreValue[store] = (float)RetailFloat24.Add(_stores.StoreValue[store], consumption);
            return false;
        }

        _stores.StoreOverheat[store] = 1;
        WeaponOverheated(now);
        return true;
    }

    /// <summary>
    /// Shared Fire <c>0x00506010</c> samples charge, clears +0x60, selects its
    /// mode level (+0x68), then checks now &gt; +0x64. A refused Pulse release
    /// still loses its charge and keeps its new mode level.
    /// </summary>
    internal bool TryPrepareFire(VehicleMode mode, VehicleTransition transition,
        float now, out Level100ProjectileKind pulseRound)
    {
        pulseRound = Level100ProjectileKind.MechPulseBoltMedium;
        Level100MissionWeapon selected = GetCurrentWeapon(mode);
        if (transition != VehicleTransition.None || !IsActive(selected))
        {
            return false;
        }

        if (selected == Level100MissionWeapon.PulseCannonPod)
        {
            pulseRound = Level100PulseCannonCharge.SelectFireRound(_pulseCharge);
            _pulseModeLevel = RetailWeaponCharge.ModeLevel(_pulseCharge);
            RetailWeaponCharge.LoseCharge(_pulseCharge);
        }
        else if (selected == Level100MissionWeapon.MissilePod)
        {
            // Fire rewrites the level and mode before its reload check, so a
            // second press during a salvo switches the rest of that burst to
            // the launcher (the RE lane's stores contract, 0x00506952).
            _podModeLevel = RetailWeaponCharge.ModeLevel(_missilePod);
            _podHasMode = true;
            RetailWeaponCharge.LoseCharge(_missilePod);
        }
        return ReadyToFire(selected, now);
    }

    internal bool ReadyToFire(Level100MissionWeapon weapon, float now) =>
        RetailWeaponCharge.ReadyTimeElapsed(now, weapon switch
        {
            Level100MissionWeapon.PulseCannonPod => _pulseCharge.ReadyAtTime,
            Level100MissionWeapon.MechTwinVulcanCannon => _twinVulcanReadyAt,
            Level100MissionWeapon.MechVulcanCannon => _mechVulcanReadyAt,
            Level100MissionWeapon.MissilePod => _missilePod.ReadyAtTime,
            _ => throw new ArgumentOutOfRangeException(nameof(weapon)),
        });

    /// <summary>
    /// Fire's <c>fld now / fadd mode+0x38 / fstp weapon+0x64</c> at
    /// <c>0x0050611A..0x00506132</c>. One float store follows the addition.
    /// </summary>
    internal void StampReadyAt(Level100MissionWeapon weapon, float now,
        Level100ProjectileKind pulseRound = Level100ProjectileKind.MechPulseBoltMedium)
    {
        float reload = weapon switch
        {
            Level100MissionWeapon.PulseCannonPod => pulseRound switch
            {
                Level100ProjectileKind.MechPulseBoltMedium => Level100PulseCannonCharge.ReloadTime,
                Level100ProjectileKind.MechPulseBoltLarge => Level100PulseCannonCharge.ChargedReloadTime,
                _ => throw new ArgumentOutOfRangeException(nameof(pulseRound)),
            },
            Level100MissionWeapon.MechTwinVulcanCannon or
                Level100MissionWeapon.MechVulcanCannon => VulcanReloadTime,
            Level100MissionWeapon.MissilePod => PodMode.ReloadTime,
            _ => throw new ArgumentOutOfRangeException(nameof(weapon)),
        };
        float readyAt = (float)((double)now + (double)reload);
        switch (weapon)
        {
            case Level100MissionWeapon.PulseCannonPod:
                _pulseCharge.ReadyAtTime = readyAt;
                break;
            case Level100MissionWeapon.MechTwinVulcanCannon:
                _twinVulcanReadyAt = readyAt;
                break;
            case Level100MissionWeapon.MechVulcanCannon:
                _mechVulcanReadyAt = readyAt;
                break;
            case Level100MissionWeapon.MissilePod:
                _missilePod.ReadyAtTime = readyAt;
                break;
        }
    }

    /// <summary>The pod's mode as Fire last set it, through <c>+0x68</c>.</summary>
    internal Level100MissilePodMode PodMode => Level100MissilePod.Mode(_podModeLevel);

    /// <summary>The pod's burst counter, <c>+0x6c</c>.</summary>
    internal int PodBurstCount
    {
        get => _podBurstCount;
        set => _podBurstCount = value;
    }

    /// <summary>
    /// <c>CWeapon::IsFiring</c> (<c>0x0050a290</c>): a mode is set, the burst
    /// counter is nonzero and below the mode's burst size. Burst-size-1
    /// weapons never count, so of Level 100's weapons only the pod can.
    /// </summary>
    internal bool PodIsFiring =>
        _podHasMode && _podBurstCount != 0 && _podBurstCount < PodMode.BurstSize;

    /// <summary>
    /// The lock parameters the current charge selects: the getters round the
    /// live charge, divide by 100 and take that level's mode, so a charging
    /// pod reads the salvo's parameters from 104 onward.
    /// </summary>
    internal Level100LockParameters LockParameters(Level100MissionWeapon weapon)
    {
        if (weapon != Level100MissionWeapon.MissilePod)
        {
            return Level100LockParameters.RecordDefaults;
        }

        Level100MissilePodMode mode = Level100MissilePod.Mode(RetailWeaponCharge.ModeLevel(_missilePod));
        return new Level100LockParameters(
            mode.MaxLocks, mode.LockTime, mode.LockDeflection, mode.LockRange, mode.LockUnitMask);
    }

    /// <summary>
    /// The spawner's launch-sequence and launch-angle counters (<c>+0x70</c>,
    /// <c>+0x74</c>): each advances before the round and wraps to 0 when it is
    /// not below its list's count.
    /// </summary>
    internal (int Emitter, int AngleSlot) AdvancePodLaunchCounters()
    {
        _podSequenceCounter++;
        if (_podSequenceCounter >= Level100MissilePod.LaunchSlots)
        {
            _podSequenceCounter = 0;
        }
        _podAngleCounter++;
        if (_podAngleCounter >= Level100MissilePod.LaunchSlots)
        {
            _podAngleCounter = 0;
        }
        return (Level100MissilePod.LaunchSequenceEmitters[_podSequenceCounter], _podAngleCounter);
    }

    /// <summary>
    /// The weapon record's <c>CWeaponAmmoStore</c> and <c>CWeaponConsumption</c>
    /// (<c>data/default physics.dat</c>, SHA-256 <c>e1fb3ded…</c>): Pulse Cannon
    /// Pod @<c>0x17463</c> store 2, 4.0; Mech Twin Vulcan Cannon @<c>0x171b4</c>
    /// store 0, 2.0; Mech Vulcan Cannon @<c>0x170c5</c> store 0, 1.0; Missile
    /// Pod @<c>0x17746</c> store 3, 1.0.
    /// </summary>
    internal static (int Store, float Consumption) StoreOf(Level100MissionWeapon weapon) => weapon switch
    {
        Level100MissionWeapon.PulseCannonPod => (2, 4.0f),
        Level100MissionWeapon.MechTwinVulcanCannon => (0, 2.0f),
        Level100MissionWeapon.MechVulcanCannon => (0, 1.0f),
        Level100MissionWeapon.MissilePod => (3, 1.0f),
        _ => throw new ArgumentOutOfRangeException(nameof(weapon)),
    };

    /// <summary>
    /// The owning part's <c>WeaponFired</c>, run once per burst event before
    /// the volley (jet <c>0x00412050</c>, walker <c>0x004140d0</c>). A refusal
    /// creates no round and takes no draw.
    /// </summary>
    internal bool WeaponFired(Level100MissionWeapon weapon, bool jetPart, float now)
    {
        (int store, float consumption) = StoreOf(weapon);
        if (_stores.StoreHeat[store] != 0)
        {
            // Charged() is weapon +0x68 > 0: a charged heat shot costs nothing.
            if (weapon == Level100MissionWeapon.PulseCannonPod && _pulseModeLevel > 0)
            {
                return true;
            }

            if (_stores.StoreValue[store] < _stores.ConfigurationStoreValue[store] &&
                _stores.StoreOverheat[store] == 0)
            {
                // The jet subtracts kWeaponCoolRate here; the walker adds the
                // full consumption and stops its shields recharging.
                _stores.StoreValue[store] = (float)RetailFloat24.Add(
                    _stores.StoreValue[store],
                    jetPart ? RetailFloat24.Subtract(consumption, CoolRate) : consumption);
                if (!jetPart)
                {
                    _shieldsRecharging = false;
                }
                return true;
            }

            _stores.StoreOverheat[store] = 1;
            WeaponOverheated(now);
            return false;
        }

        if (_stores.StoreValue[store] > 0.0f)
        {
            float value = (float)RetailFloat24.Subtract(_stores.StoreValue[store], consumption);
            _stores.StoreValue[store] = value < 0.0f ? 0.0f : value;
            return true;
        }

        if (_ammoDepletedTime < (float)RetailFloat24.Subtract(now, 8.0f))
        {
            _ammoDepletedTime = now;
        }
        return false;
    }

    /// <summary>
    /// <c>CanWeaponFire</c> (jet <c>0x00412570</c>, walker <c>0x00414630</c>,
    /// which also requires the weapon active).
    /// </summary>
    internal bool CanWeaponFire(Level100MissionWeapon weapon, bool walkerPart)
    {
        if (walkerPart && !IsActive(weapon))
        {
            return false;
        }

        (int store, _) = StoreOf(weapon);
        return _stores.StoreHeat[store] != 0
            ? _stores.StoreValue[store] < _stores.ConfigurationStoreValue[store] &&
                _stores.StoreOverheat[store] == 0
            : _stores.StoreValue[store] > 0.0f;
    }

    /// <summary>
    /// The store loop at the end of <c>CBattleEngine::Move</c>
    /// (<c>0x004095d6-0x00409633</c>, <c>BattleEngine.cpp:1708-1718</c>).
    /// </summary>
    internal void CoolStores()
    {
        for (int store = 0; store < RetailWeaponStores.StoreCount; store++)
        {
            if (_stores.StoreHeat[store] == 0)
            {
                continue;
            }

            float value = (float)RetailFloat24.Subtract(_stores.StoreValue[store], CoolRate);
            if (value < 0.0f)
            {
                value = 0.0f;
            }
            else if (value < (float)RetailFloat24.Multiply(_stores.ConfigurationStoreValue[store], 0.75f))
            {
                _stores.StoreOverheat[store] = 0;
            }
            _stores.StoreValue[store] = value;
        }
    }

    /// <summary>The walker part's Move ends with <c>mShieldsRecharging=TRUE</c>.</summary>
    internal void ResumeShieldsRecharging() => _shieldsRecharging = true;

    /// <summary>
    /// <c>WeaponOverheated</c> (<c>0x0040f110</c>) only stamps the cue time
    /// when the previous stamp is more than 4 s old.
    /// </summary>
    private void WeaponOverheated(float now)
    {
        if (_weaponOverheatedTime < (float)RetailFloat24.Subtract(now, 4.0f))
        {
            _weaponOverheatedTime = now;
        }
    }

    /// <summary>
    /// The common pre-direction work in retail <c>CBattleEngine::Morph</c>
    /// calls <c>LoseWeaponCharge</c> on both vehicle parts. Each part clears
    /// only its currently selected weapon: the Pulse Cannon Pod on the walker
    /// and the Missile Pod on the jet are Level 100's charge accumulators.
    /// </summary>
    internal void LoseCurrentWeaponChargesForMorph()
    {
        if (WalkerSelectedWeapon == Level100MissionWeapon.PulseCannonPod)
        {
            RetailWeaponCharge.LoseCharge(_pulseCharge);
        }
        if (JetSelectedWeapon == Level100MissionWeapon.MissilePod)
        {
            RetailWeaponCharge.LoseCharge(_missilePod);
        }
    }

    internal Level100MissionWeapon GetCurrentWeapon(VehicleMode mode) => mode switch
    {
        VehicleMode.Walker => WalkerSelectedWeapon,
        VehicleMode.Jet => JetSelectedWeapon,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    internal bool IsActive(Level100MissionWeapon weapon) => weapon switch
    {
        Level100MissionWeapon.PulseCannonPod => _pulseCannonActive,
        Level100MissionWeapon.MechTwinVulcanCannon => _twinVulcanActive,
        Level100MissionWeapon.MechVulcanCannon => _mechVulcanActive,
        Level100MissionWeapon.MissilePod => _missilePodActive,
        _ => throw new ArgumentOutOfRangeException(nameof(weapon)),
    };

    internal int CountActiveWeapons(VehicleMode mode) => mode switch
    {
        VehicleMode.Walker =>
            (_pulseCannonActive ? 1 : 0) + (_twinVulcanActive ? 1 : 0),
        VehicleMode.Jet =>
            (_mechVulcanActive ? 1 : 0) + (_missilePodActive ? 1 : 0),
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    internal bool SelectNextActive(VehicleMode mode)
    {
        if (CountActiveWeapons(mode) <= 1)
        {
            return false;
        }

        return TrySelectNextActive(mode);
    }

    internal void SetActive(Level100MissionWeapon weapon, bool active)
    {
        switch (weapon)
        {
            case Level100MissionWeapon.PulseCannonPod:
                _pulseCannonActive = active;
                ReselectIfDisabled(VehicleMode.Walker, weapon, active);
                break;
            case Level100MissionWeapon.MechTwinVulcanCannon:
                _twinVulcanActive = active;
                ReselectIfDisabled(VehicleMode.Walker, weapon, active);
                break;
            case Level100MissionWeapon.MechVulcanCannon:
                _mechVulcanActive = active;
                ReselectIfDisabled(VehicleMode.Jet, weapon, active);
                break;
            case Level100MissionWeapon.MissilePod:
                _missilePodActive = active;
                ReselectIfDisabled(VehicleMode.Jet, weapon, active);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(weapon));
        }
    }

    private void ReselectIfDisabled(
        VehicleMode mode,
        Level100MissionWeapon weapon,
        bool active)
    {
        if (!active && GetCurrentWeapon(mode) == weapon)
        {
            _ = TrySelectNextActive(mode);
        }
    }

    /// <summary>
    /// The parts' <c>ChangeWeapon</c> walk
    /// (<c>BattleEngineWalkerPart.cpp:560-596</c>, jet <c>0x00411f3a-0x00411f49</c>):
    /// the next active weapon whose store is a heat store or holds at least
    /// its consumption.
    /// </summary>
    private bool TrySelectNextActive(VehicleMode mode)
    {
        int current = mode == VehicleMode.Walker
            ? _walkerSelection
            : _jetSelection;
        int candidate = (current + 1) % 2;
        while (candidate != current)
        {
            Level100MissionWeapon weapon = WeaponAt(mode, candidate);
            (int store, float consumption) = StoreOf(weapon);
            if (IsActive(weapon) &&
                (_stores.StoreHeat[store] != 0 || _stores.StoreValue[store] >= consumption))
            {
                if (mode == VehicleMode.Walker)
                {
                    _walkerSelection = candidate;
                }
                else
                {
                    _jetSelection = candidate;
                }

                // ChangeWeapon's aftermath: LoseCharge on the newly selected
                // weapon (0x00411f96). Level 100 accumulates charge on the two
                // pods; resetting a table matches the store of +0.0f.
                if (weapon == Level100MissionWeapon.PulseCannonPod)
                {
                    RetailWeaponCharge.LoseCharge(_pulseCharge);
                }
                else if (weapon == Level100MissionWeapon.MissilePod)
                {
                    RetailWeaponCharge.LoseCharge(_missilePod);
                }
                return true;
            }

            candidate = (candidate + 1) % 2;
        }

        return false;
    }

    private int OverheatMask()
    {
        int mask = 0;
        for (int store = 0; store < RetailWeaponStores.StoreCount; store++)
        {
            if (_stores.StoreOverheat[store] != 0)
            {
                mask |= 1 << store;
            }
        }
        return mask;
    }

    private static Level100MissionWeapon WeaponAt(VehicleMode mode, int index) =>
        (mode, index) switch
        {
            (VehicleMode.Walker, 0) => Level100MissionWeapon.PulseCannonPod,
            (VehicleMode.Walker, 1) => Level100MissionWeapon.MechTwinVulcanCannon,
            (VehicleMode.Jet, 0) => Level100MissionWeapon.MechVulcanCannon,
            (VehicleMode.Jet, 1) => Level100MissionWeapon.MissilePod,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
}
