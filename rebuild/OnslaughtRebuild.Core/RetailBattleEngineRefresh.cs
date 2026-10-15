// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// The two refreshes a Battle Engine files for itself on the level event
/// manager, the range its crosshair probe accepts, and the side gate its
/// targeting uses.
/// </summary>
/// <remarks>
/// <para>
/// Evidence: pristine <c>74154bfa…</c> static reads by the RE lane,
/// <c>reverse-engineering/game-mechanics/level100-final-drone-wave.md</c>
/// ("Crosshair and auto-aim refresh"), and the source shape at
/// <c>references/Onslaught/BattleEngine.cpp:350-352, :2278-2344, :2446-2613</c>.
/// </para>
/// <para>
/// <c>CBattleEngine::Init</c> (<c>0x00404dd0</c>) takes one draw at
/// <c>0x0040586e</c> and files event 6002 (<c>CALC_UNIT_OVER_CROSSHAIR</c>) at
/// <c>0x004058b2</c>, then calls <c>HandleAutoAim(NULL)</c>, which ends with one
/// draw at <c>0x0040bf57</c> and files event 6003 (<c>HANDLE_AUTO_AIM</c>).
/// Each delivery of either event re-files it the same way with one draw
/// (6002 at <c>0x0040b091</c>). Both due times are the same x87 sequence as
/// the aircraft's 3002 callback: <c>r = NextLCGAbs; and 0x8000ffff; fild;
/// fmul scale; fadd mTime; fadd delay; fstp float</c>, with scale
/// <c>0x364ccccd</c> (0.2/65536, <c>0x005d8808</c>) and delay 0.1f
/// (<c>0x005d85c0</c>) for 6002, and scale <c>0x35cccccd</c> (0.1/65536,
/// <c>0x005d8c5c</c>) and delay 0.2f (<c>0x005d8604</c>) for 6003. Both are
/// filed at priority 0 (start of frame). The control word live on this path
/// has not been measured; Core evaluates it at the PC24 precision the
/// aircraft callbacks use, which is the same open question for both.
/// </para>
/// </remarks>
public static class RetailBattleEngineRefresh
{
    /// <summary><c>CALC_UNIT_OVER_CROSSHAIR</c>, <c>0x1772</c>.</summary>
    public const int CrosshairEvent = 6002;

    /// <summary><c>HANDLE_AUTO_AIM</c>, <c>0x1773</c>.</summary>
    public const int AutoAimEvent = 6003;

    /// <summary><c>0x005d8808</c>: 0.2/65536.</summary>
    public const uint CrosshairJitterScaleBits = 0x364ccccdu;

    /// <summary><c>0x005d85c0</c>: 0.1f.</summary>
    public const uint CrosshairDelayBits = 0x3dcccccdu;

    /// <summary><c>0x005d8c5c</c>: 0.1/65536.</summary>
    public const uint AutoAimJitterScaleBits = 0x35cccccdu;

    /// <summary><c>0x005d8604</c>: 0.2f.</summary>
    public const uint AutoAimDelayBits = 0x3e4ccccdu;

    /// <summary>
    /// The 1000-unit crosshair line (<c>BattleEngine.cpp:2304</c>) and the
    /// range the probe falls back to when the weapon reports none
    /// (<c>:2296-2297</c>).
    /// </summary>
    public const float CrosshairLineLength = 1000.0f;

    /// <summary>6002's due time for one draw at the manager's current time.</summary>
    public static float CrosshairDueTime(int draw, float now) =>
        DueTime(draw, now, CrosshairJitterScaleBits, CrosshairDelayBits);

    /// <summary>6003's due time for one draw at the manager's current time.</summary>
    public static float AutoAimDueTime(int draw, float now) =>
        DueTime(draw, now, AutoAimJitterScaleBits, AutoAimDelayBits);

    private static float DueTime(int draw, float now, uint scaleBits, uint delayBits)
    {
        // and 0x8000ffff with the sign fix-up is MSVC's signed % 65536.
        int sample = draw % 65536;
        double jitter = RetailFloat24.Multiply(sample, BitConverter.UInt32BitsToSingle(scaleBits));
        return (float)RetailFloat24.Add(
            RetailFloat24.Add(jitter, now),
            BitConverter.UInt32BitsToSingle(delayBits));
    }

    /// <summary>
    /// <c>CWeapon::GetActualMaxRange</c> (<c>0x00509c80</c>) for the Level 100
    /// player's weapon modes, as the crosshair probe reads it. A gravity-free,
    /// non-beam round that does not seek returns <c>CRoundVelocity</c> x
    /// <c>CRoundLifeSpan</c>; a seeking round returns <c>CWeaponLockRange</c>.
    /// Values from <c>data/default physics.dat</c> (<c>e1fb3ded…</c>): Mech
    /// Pulse Bolt Medium 35 x 6, Mech Pulse Bolt Large 20 x 7, Mech Bullet and
    /// Mech Air Bullet 60 x 1, and the Missile Pod's lock range 100.
    /// </summary>
    public static float ActualMaxRange(Level100MissionWeapon weapon, bool pulseChargedMode) =>
        weapon switch
        {
            Level100MissionWeapon.PulseCannonPod => pulseChargedMode ? 140.0f : 210.0f,
            Level100MissionWeapon.MechTwinVulcanCannon or
                Level100MissionWeapon.MechVulcanCannon => 60.0f,
            Level100MissionWeapon.MissilePod => 100.0f,
            _ => throw new ArgumentOutOfRangeException(nameof(weapon)),
        };

    /// <summary>
    /// The side gate <c>0x004fd3d0</c> (<c>IsTargetAlligence</c>) with the
    /// profile arm (<c>+0x128</c>) not taken: allegiance 0 and 1 oppose each
    /// other, 6 opposes both, and nothing else is a target.
    /// </summary>
    public static bool IsTargetAllegiance(int mine, int candidate) => candidate switch
    {
        0 => mine is 1 or 6,
        1 => mine is 0 or 6,
        6 => mine is 0 or 1,
        _ => false,
    };
}
