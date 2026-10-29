// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class BattleEngineLaunchTests
{
    private static (BattleEngine Engine, Weapon Weapon) Fixture()
    {
        var pool = new SPtrSetPool();
        pool.Init(64);
        var events = new EventManager();
        events.Init();
        var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(1), null!, new PhysicsRegistry());
        var engine = new BattleEngine(context) { Pos = new FVector(1, 2, 3) };
        var weapon = new Weapon(context, new WeaponData("authored"u8.ToArray()) { AdjustAim = 1 }, 0)
        {
            AttachedTo = engine,
            Number = -1,
            Mode = new WeaponModeData("authored"u8.ToArray()) { Round = new RoundData("authored"u8.ToArray()) },
        };
        return (engine, weapon);
    }

    private static uint[] Bits(FMatrix matrix) =>
    [
        BitConverter.SingleToUInt32Bits(matrix.Row0.X), BitConverter.SingleToUInt32Bits(matrix.Row0.Y), BitConverter.SingleToUInt32Bits(matrix.Row0.Z),
        BitConverter.SingleToUInt32Bits(matrix.Row1.X), BitConverter.SingleToUInt32Bits(matrix.Row1.Y), BitConverter.SingleToUInt32Bits(matrix.Row1.Z),
        BitConverter.SingleToUInt32Bits(matrix.Row2.X), BitConverter.SingleToUInt32Bits(matrix.Row2.Y), BitConverter.SingleToUInt32Bits(matrix.Row2.Z),
    ];

    [Fact]
    public void NoHitAimUsesTheOutOfLineProductRatherThanTheGenericProduct()
    {
        (BattleEngine engine, Weapon weapon) = Fixture();
        engine.Wlcr.HitType = LineHitType.Nothing;

        // Authored rotations exercise actual forward-weapon admission and include cases where the two products differ.
        float[] angles = [-0.91f, -0.33f, 0.17f, 0.61f];
        int distinct = 0;
        foreach (float yaw in angles)
        foreach (float pitch in angles)
        foreach (float roll in angles)
        foreach (float offset in angles)
        {
            engine.Orientation = FMatrix.FromYawPitchRoll(yaw, pitch, roll);
            engine.AutoAimYawOffset = offset;
            engine.AutoAimPitchOffset = pitch * 0.23f;
            FMatrix aim = FMatrix.MakeRotationOutOfLine(engine.AutoAimYawOffset, engine.AutoAimPitchOffset, 0);
            uint[] expected = Bits(engine.Orientation.MultiplyOutOfLine(aim));
            if (expected.SequenceEqual(Bits(engine.Orientation.MultiplyLeftToRight(aim)))) continue;
            distinct++;
            engine.GetLaunchPosition(weapon, 1, out FVector pos, out FMatrix orientation, true);
            Assert.Equal(engine.Pos, pos);
            Assert.Equal(expected, Bits(orientation));
        }

        Assert.True(distinct > 0, "Authored inputs must distinguish the two product orders.");
    }

    [Fact]
    public void HitAimKeepsItsExistingTargetDirection()
    {
        (BattleEngine engine, Weapon weapon) = Fixture();
        engine.Orientation = FMatrix.FromYawPitchRoll(.17f, .23f, .31f);
        engine.AutoAimYawOffset = .11f;
        engine.AutoAimPitchOffset = -.23f;
        engine.Wlcr.HitType = LineHitType.Ground;
        engine.Wlcr.DistToImpact = 10.0f;
        FMatrix aim = FMatrix.MakeRotationOutOfLine(engine.AutoAimYawOffset, engine.AutoAimPitchOffset, 0);
        FVector target = FMatrix.Identity.MultiplyOutOfLine(aim).Transform(new FVector(0, 200, 0));
        FVector hit = target * (engine.Wlcr.DistToImpact / target.Magnitude());
        uint[] expected = Bits(BattleEngine.AimAtHit(hit - engine.Pos));
        Assert.False(expected.SequenceEqual(Bits(engine.Orientation.MultiplyOutOfLine(aim))));
        engine.GetLaunchPosition(weapon, 1, out FVector pos, out FMatrix orientation, true);
        Assert.Equal(engine.Pos, pos);
        Assert.Equal(expected, Bits(orientation));
    }

    [Fact]
    public void BallisticAimStillReportsTheUnsupportedSearch()
    {
        (BattleEngine engine, Weapon weapon) = Fixture();
        weapon.Mode!.Round!.Gravity = 1.0f;
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            engine.GetLaunchPosition(weapon, 1, out _, out _, true));
        Assert.Contains("ballistic weapon", error.Message);
    }

    [Fact]
    public void DisabledAimOrUnrequestedOrientationKeepsThePrimaryOrientation()
    {
        (BattleEngine engine, Weapon weapon) = Fixture();
        engine.Orientation = FMatrix.FromYawPitchRoll(.17f, .23f, .31f);
        engine.AutoAimYawOffset = .11f;
        engine.AutoAimPitchOffset = -.23f;
        weapon.Data.AdjustAim = 0;
        engine.GetLaunchPosition(weapon, 1, out _, out FMatrix noAim, true);
        Assert.Equal(Bits(engine.Orientation), Bits(noAim));
        weapon.Data.AdjustAim = 1;
        engine.GetLaunchPosition(weapon, 1, out _, out FMatrix notRequested, false);
        Assert.Equal(Bits(engine.Orientation), Bits(notRequested));
    }

    [Fact]
    public void AimOutsideTheForwardAngleFenceKeepsThePrimaryOrientation()
    {
        (BattleEngine engine, Weapon weapon) = Fixture();
        // A deliberately non-unit authored basis exercises the fence without a cockpit emitter.
        engine.Orientation = new FMatrix(new FVector(1, 0, 0), new FVector(0, .5f, 0), new FVector(0, 0, 1));
        engine.AutoAimYawOffset = .11f;
        engine.AutoAimPitchOffset = -.23f;
        engine.Wlcr.HitType = LineHitType.Nothing;
        FMatrix aim = FMatrix.MakeRotationOutOfLine(engine.AutoAimYawOffset, engine.AutoAimPitchOffset, 0);
        Assert.False(Bits(engine.Orientation).SequenceEqual(Bits(engine.Orientation.MultiplyOutOfLine(aim))));

        engine.GetLaunchPosition(weapon, 1, out _, out FMatrix orientation, true);
        Assert.Equal(Bits(engine.Orientation), Bits(orientation));
    }
}
