// SPDX-License-Identifier: GPL-3.0-only
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class BattleEngineZoomTests
{
    private static (BattleEngine Engine, WeaponData Data, Weapon Weapon) Fixture(int mode)
    {
        var pool = new SPtrSetPool();
        pool.Init(64);
        var events = new EventManager();
        events.Init();
        var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(1), null!, new PhysicsRegistry());
        var data = new WeaponData("authored"u8.ToArray()) { ZoomMode = mode };
        var engine = new BattleEngine(context) { Configuration = new BattleEngineData(), State = BattleEngineState.Walker };
        var weapon = new Weapon(context, data, 0);
        engine.WalkerPart = new BattleEngineWalkerPart(engine) { PrimaryWeapon = weapon };
        return (engine, data, weapon);
    }

    [Fact]
    public void NoWeaponOrUnknownModeMovesByOneTenthAndSavesThePreviousZoom()
    {
        var (engine, _, weapon) = Fixture(0);
        engine.Zoom = 0.8f;
        engine.DesiredZoom = 0.2f;
        engine.WalkerPart!.PrimaryWeapon = null;
        engine.UpdateWeaponZoom(0);
        Assert.Equal(0.8f, engine.OldZoom);
        Assert.Equal(0x3f333333u, BitConverter.SingleToUInt32Bits(engine.Zoom));
        Assert.Equal(0.2f, engine.DesiredZoom);

        engine.Zoom = 0.52f;
        engine.DesiredZoom = 0.5f;
        engine.WalkerPart.PrimaryWeapon = weapon;
        engine.UpdateWeaponZoom(0);
        Assert.Equal(0.52f, engine.OldZoom);
        Assert.Equal(0.5f, engine.Zoom); // the remaining difference is strictly below 0.1
        Assert.Equal(0.5f, engine.DesiredZoom);
    }

    [Fact]
    public void NormalModeRaisesAnUndersizedTargetButNotTheExactMinimum()
    {
        var (engine, _, _) = Fixture(1);
        engine.Zoom = 1.0f;
        engine.DesiredZoom = 0.2f;
        engine.UpdateWeaponZoom(0);
        Assert.Equal(0x3e99999au, BitConverter.SingleToUInt32Bits(engine.DesiredZoom));
        Assert.Equal(0x3f666666u, BitConverter.SingleToUInt32Bits(engine.Zoom));

        engine.DesiredZoom = 0.4f;
        engine.UpdateWeaponZoom(0);
        Assert.Equal(0.4f, engine.DesiredZoom);
    }

    [Fact]
    public void ChargeModeUsesTheHighestAvailableChargeLevelAndNoModeMeansZeroCharge()
    {
        var (engine, data, weapon) = Fixture(2);
        data.ChargeModes[4] = 0; // maximum charge is 400, regardless of the registry entry's identity
        weapon.ChargeValue = 200;
        engine.Zoom = 1.0f;
        engine.DesiredZoom = 1.0f;
        engine.UpdateWeaponZoom(0);
        Assert.Equal(0x3f19999au, BitConverter.SingleToUInt32Bits(engine.DesiredZoom)); // half charge: 0.6
        Assert.Equal(0x3f666666u, BitConverter.SingleToUInt32Bits(engine.Zoom));

        data.ChargeModes[4] = -1;
        weapon.ChargeValue = 200;
        engine.DesiredZoom = 0.5f;
        engine.ZoomOutTime = -1.0f;
        engine.UpdateWeaponZoom(0);
        Assert.Equal(1.0f, engine.DesiredZoom); // GetCharge's zero-denominator arm returns zero
    }

    [Fact]
    public void ChargeZoomRoundsEachOriginalPc24OperationRatherThanTheEntireExpressionOnce()
    {
        var (engine, data, weapon) = Fixture(2);
        data.ChargeModes[1] = 0; // maximum charge 100
        weapon.ChargeValue = 10;
        engine.Zoom = 1.0f;
        engine.DesiredZoom = 1.0f;
        engine.UpdateWeaponZoom(0);
        Assert.Equal(0x3f6b851eu, BitConverter.SingleToUInt32Bits(engine.DesiredZoom));
        float oneDoubleExpression = (float)((1.0 - (10.0 / 100.0)) * (double)0.8f + (double)0.2f);
        Assert.Equal(0x3f6b851fu, BitConverter.SingleToUInt32Bits(oneDoubleExpression));
    }

    [Fact]
    public void FullyChargedZoomHoldsUntilStrictlyAfterTheHalfSecondDeadline()
    {
        var (engine, data, weapon) = Fixture(2);
        data.ChargeModes[4] = 0;
        weapon.ChargeValue = 400;
        engine.Zoom = 1.0f;
        engine.DesiredZoom = 1.0f;
        engine.UpdateWeaponZoom(2.0f);
        Assert.Equal(0.2f, engine.DesiredZoom);
        Assert.Equal(2.5f, engine.ZoomOutTime);
        Assert.Equal(0x3f666666u, BitConverter.SingleToUInt32Bits(engine.Zoom));

        weapon.ChargeValue = 0;
        engine.UpdateWeaponZoom(2.5f);
        Assert.Equal(0.2f, engine.DesiredZoom);
        Assert.Equal(0x3f4cccccu, BitConverter.SingleToUInt32Bits(engine.Zoom));
        engine.UpdateWeaponZoom(MathF.BitIncrement(2.5f));
        Assert.Equal(1.0f, engine.DesiredZoom);
        Assert.Equal(0x3f666666u, BitConverter.SingleToUInt32Bits(engine.Zoom));
    }
}
