// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>The Battle Engine's shake offsets and their cosine phase, as float bits.</summary>
public readonly record struct Level100BattleEngineShakeSnapshot(
    uint YawBits,
    uint PitchBits,
    uint RollBits,
    uint PhaseBits)
{
    public static Level100BattleEngineShakeSnapshot Initial => default;
}

/// <summary>
/// <c>CBattleEngine::AddShockShake</c> and the shake tail of
/// <c>UpdateRotation</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>AddShockShake</c> (<c>0x00407940</c>, <c>BattleEngine.cpp:1094-1113</c>):
/// return when the amount is below 0.001 (a double compare against
/// <c>0x005d8bc8</c>); cap it at 0.75; take three shared draws, each
/// <c>(r mod 32) / (16 / amount) − amount</c>, into yaw <c>+0x4b8</c>, pitch
/// <c>+0x4bc</c> and roll <c>+0x4c0</c>; zero the phase <c>+0x4c4</c>. The
/// cockpit's own shake uses the CRT <c>rand</c> stream and is presentation.
/// Its two gameplay callers are <c>RecoilWeapon</c> (<c>0x0040c340</c>, once per
/// spawned round of a Battle Engine weapon, with the mode's
/// <c>CWeaponPower</c>) and <c>CBattleEngine::Damage</c>
/// (<c>0x0040ab75-0x0040abc2</c>: the life lost times 0.125, halved while
/// shields remain, capped at 0.25). Evidence: the RE lane's burst-spawner
/// contract, <c>reverse-engineering/contracts/render-platform/ProjectileBurst__SpawnFromCurrentPreset__005069f0.md</c>.
/// </para>
/// <para>
/// <c>UpdateRotation</c> (<c>BattleEngine.cpp:1222-1233</c>, called on every
/// Move) builds <c>mOrientation</c> from the current angles plus
/// <c>cos(phase) × shake</c>, then, while any offset exceeds 0.001 in
/// magnitude, multiplies all three by 0.8 and advances the phase by 0.8.
/// Core does not yet apply the offsets to the Battle Engine's orientation;
/// the draws, values and decay are exact. Arithmetic follows the PC24 control
/// word the aircraft paths observe.
/// </para>
/// </remarks>
internal sealed class RetailBattleEngineShake
{
    private float _yaw;
    private float _pitch;
    private float _roll;
    private float _phase;

    internal Level100BattleEngineShakeSnapshot Snapshot => new(
        BitConverter.SingleToUInt32Bits(_yaw),
        BitConverter.SingleToUInt32Bits(_pitch),
        BitConverter.SingleToUInt32Bits(_roll),
        BitConverter.SingleToUInt32Bits(_phase));

    internal void Reset()
    {
        _yaw = 0.0f;
        _pitch = 0.0f;
        _roll = 0.0f;
        _phase = 0.0f;
    }

    /// <summary><c>AddShockShake(amount)</c>; <paramref name="nextDraw"/> is the shared stream.</summary>
    internal void Add(float amount, Func<int> nextDraw)
    {
        ArgumentNullException.ThrowIfNull(nextDraw);
        if ((double)amount < 0.001)
        {
            return;
        }

        if (amount > 0.75f)
        {
            amount = 0.75f;
        }

        float scale = (float)RetailFloat24.Divide(16.0, amount);
        _yaw = Offset(nextDraw(), scale, amount);
        _pitch = Offset(nextDraw(), scale, amount);
        _roll = Offset(nextDraw(), scale, amount);
        _phase = 0.0f;

        // and 0x8000001f with the sign fix-up is MSVC's signed % 32.
        static float Offset(int draw, float scale, float amount) => (float)RetailFloat24.Subtract(
            RetailFloat24.Divide(draw % 32, scale),
            amount);
    }

    /// <summary>The decay at the end of <c>UpdateRotation</c>.</summary>
    internal void Decay()
    {
        if (!(MathF.Abs(_yaw) > 0.001f || MathF.Abs(_pitch) > 0.001f || MathF.Abs(_roll) > 0.001f))
        {
            return;
        }

        _yaw = (float)RetailFloat24.Multiply(_yaw, 0.8f);
        _pitch = (float)RetailFloat24.Multiply(_pitch, 0.8f);
        _roll = (float)RetailFloat24.Multiply(_roll, 0.8f);
        _phase = (float)RetailFloat24.Add(_phase, 0.8f);
    }

    /// <summary>
    /// <c>CBattleEngine::Damage</c>'s shake amount from the life lost and
    /// whether shields remain after the hit (<c>BattleEngine.cpp:2222-2228</c>).
    /// </summary>
    internal static float DamageAmount(int lifeLostMilli, bool shieldsRemain)
    {
        float diff = (float)RetailFloat24.Multiply(lifeLostMilli / 1000.0f, 0.125f);
        if (shieldsRemain)
        {
            diff = (float)RetailFloat24.Divide(diff, 2.0f);
        }

        return diff > 0.25f ? 0.25f : diff;
    }
}
