// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/BattleEngine.cpp: Damage0040a890, inlined AddDamageFlash0040aa03,
// and ProcessDamageFlashes at the start of Move004081c0. Pristine PC SHA-256:
// 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Things;

public sealed partial class BattleEngine
{
    /// <summary>mLastDamageTime (+0x2d4), initialized to -1 by CBattleEngine::Init.</summary>
    public float LastDamageTime = -1.0f;

    public sealed record DamageFlash(float Yaw, float StartTime);
    private SPtrSet<DamageFlash>? _damageFlashes;

    /// <summary>Newest first, as GenericSPtrSet::Add inserts at the head. A snapshot cannot mutate the owner.</summary>
    public IReadOnlyList<DamageFlash> DamageFlashes => _damageFlashes?.ToList() ?? [];

    /// <summary>
    /// Damage0040a890: no CUnit damage dispatch. Invulnerability restores only life/shields/energy, AFTER statistics,
    /// augmentation, flashes, shake and vibration. Zero still runs the common tail; negative damage repairs life then
    /// energy. StartDieProcess owns the BE-specific lethal transition; it requires the controlling player and game.
    /// </summary>
    public override void Damage(float amount, Thing? byThis, bool damageShields = true, int meshPart = -1)
    {
        float lastLife = Life, lastShields = Shields, lastEnergy = Energy;
        if (amount > 0)
        {
            // FISTP qword, then the low dword (0040a8c9..0040a8e6): nearest-even, not a C# truncating cast.
            if (Player is not Game.Player player)
                throw new NotSupportedException("CBattleEngine::Damage requires its controlling player for damage statistics");
            player.IncStat(PlayerStat.DamageTaken, X87.Fist(X87.Mul(amount, 256.0f)));
            if (Life >= 0)
            {
                if (Shields >= amount)
                {
                    // Retail multiplies by the float reciprocal and keeps this intermediate on the x87 stack.
                    double shieldDamage = damageShields
                        ? X87.Mul(X87.Mul(Configuration!.ShieldEfficiency, 0.01f), amount) : 0;
                    Shields = (float)X87.Sub(Shields, shieldDamage);
                    amount = (float)X87.Sub(amount, shieldDamage);
                    if (!AugActive && WalkerPart!.AugWeapon is not null)
                        AugValue = (float)X87.Add(shieldDamage, AugValue);
                }
                if (amount > Shields && damageShields)
                {
                    if (!AugActive) AugValue = (float)X87.Add(Shields, AugValue);
                    amount = (float)X87.Sub(amount, Shields);
                    Shields = 0;
                }
                double remainingLife = X87.Sub(Life, amount);
                Life = (float)remainingLife;
                if (!(remainingLife >= 0) && Vulnerable) StartDieProcess();
            }
            LastDamageTime = Context.Events.Time;
            if (byThis is not null) AddDamageFlash(byThis.Pos);
        }

        if (AugValue > 10.0f) AugValue = 10.0f;
        if (State == BattleEngineState.Walker) Energy = Shields;

        // C0-only x87 tests admit unordered values here. Preserve them instead of using a negative-only early-out.
        if (!(amount >= 0))
        {
            if (!(Life >= Configuration!.Life))
            {
                double difference = X87.Sub(Configuration.Life, Life);
                if (!(difference >= -amount))
                {
                    amount = (float)X87.Add(difference, amount);
                    Life = Configuration.Life;
                }
                else
                {
                    Life = (float)X87.Sub(Life, amount);
                    amount = 0;
                }
            }
            // The direct float subtraction preserves native FSUB NaN signs; X87.Sub is expressed as Add(a, -b).
            if (!(Energy >= Configuration.Energy))
                Energy = -amount > X87.Sub(Configuration.Energy, Energy)
                    ? Configuration.Energy : Energy - amount;
        }

        double shake = X87.Mul(X87.Sub(lastLife, Life), 0.125f);
        float differenceForShake = (float)shake;
        // C3 suppresses the halve for zero AND unordered shields (0040ab97).
        if (Shields != 0 && !float.IsNaN(Shields))
        {
            shake = X87.Mul(shake, 0.5f);
            differenceForShake = (float)shake;
        }
        if (shake > 0.25f) differenceForShake = 0.25f;
        // Native AddShockShake rejects unordered amounts before drawing either RNG stream.
        if (!float.IsNaN(differenceForShake)) AddShockShake(differenceForShake);
        if (differenceForShake > 0.05f) differenceForShake = 0.05f;
        Vibration = (float)X87.Add(X87.Mul(differenceForShake, 50.0f), Vibration);
        if (!Vulnerable) { Life = lastLife; Shields = lastShields; Energy = lastEnergy; }
    }

    /// <summary>
    /// Inlined AddDamageFlash: cap at 15, use attacker's CURRENT position minus our OLD position, and preserve the
    /// angle in double precision through the following 24-bit subtraction. The shared Atan2 approximation is
    /// bounded by the native flash oracle, not proven for every input. This is state, not a claim of HUD drawing.
    /// </summary>
    public void AddDamageFlash(FVector position)
    {
        _damageFlashes ??= new SPtrSet<DamageFlash>(Context.Pool);
        if (_damageFlashes.Size >= 15) return;
        float x = position.X - OldPos.X, y = position.Y - OldPos.Y;
        float yaw = (float)X87.Sub(CurrentOrientation.Yaw, -X87.Atan2(x, y));
        _damageFlashes.Add(new DamageFlash(yaw, Context.Events.Time));
    }

    /// <summary>
    /// Move004081c0 calls this BEFORE HandleEngines: remove only the FIRST expired flash in newest-first order.
    /// Exactly two seconds old survives. The caller, not this method, owns the movement schedule.
    /// </summary>
    public void ProcessDamageFlashes()
    {
        if (_damageFlashes is null) return;
        var iterator = new ListIterator<DamageFlash>(_damageFlashes);
        for (DamageFlash? flash = iterator.First(); flash is not null; flash = iterator.Next())
        {
            if (!(X87.Add(flash.StartTime, 2.0f) >= Context.Events.Time))
            {
                _damageFlashes.Remove(flash);
                break;
            }
        }
    }
}
