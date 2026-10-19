// SPDX-License-Identifier: GPL-3.0-only
// Retail 74154bfa: CBattleEngine::ChangeWeapon 00409f70 and walker CountActiveWeapons 00414b70.
// These reconstruction checks exercise the documented guards and the ordinary PC controller route.
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class ChangeWeaponTests
{
    private static (LevelSession Session, BattleEngine Engine, BattleEngineWalkerPart Walker) Loaded()
    {
        var session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        BattleEngine engine = Assert.IsType<BattleEngine>(session.Game.GetPlayer(0)!.BattleEngine);
        BattleEngineWalkerPart walker = engine.WalkerPart!;
        Assert.NotEmpty(walker.Weapons);
        return (session, engine, walker);
    }

    [GameDataFact]
    public void OneActiveWeaponReturnsBeforeMovementTimeChargeAndSoundChanges()
    {
        var (session, engine, walker) = Loaded();
        foreach (var w in walker.Weapons) w.WeaponActive = false;
        walker.PrimaryWeapon!.WeaponActive = true;
        walker.PrimaryWeapon.ChargeValue = 123.0f;
        engine.SlowMovement = true;
        engine.ChangedWeaponTime = -17.0f;
        long calls = session.Context.Crt.Calls;
        engine.ChangeWeapon();
        Assert.Equal(1, engine.CountActiveWeapons());
        Assert.Equal(0, walker.CurrentWeapon);
        Assert.True(engine.SlowMovement);
        Assert.Equal(-17.0f, engine.ChangedWeaponTime);
        Assert.Equal(123.0f, walker.PrimaryWeapon.ChargeValue);
        Assert.Equal(calls, session.Context.Crt.Calls);
    }

    [GameDataFact]
    public void EmptySecondaryStillAdmitsWrapperButDoesNotSelectIt()
    {
        var (session, engine, walker) = Loaded();
        foreach (var w in walker.Weapons) w.WeaponActive = false;
        walker.PrimaryWeapon!.WeaponActive = true;
        Weapon secondary = walker.Weapons[0];
        secondary.WeaponActive = true;
        Assert.True(secondary.Data.Consumption > 0);
        engine.StoreHeat[secondary.Data.AmmoStore] = false;
        engine.StoreValue[secondary.Data.AmmoStore] = 0;
        engine.SlowMovement = true;
        walker.PrimaryWeapon.ChargeValue = 123.0f;
        Assert.Equal(2, engine.CountActiveWeapons());
        long calls = session.Context.Crt.Calls;
        engine.ChangeWeapon();
        Assert.Equal(0, walker.CurrentWeapon);
        Assert.False(engine.SlowMovement);
        Assert.Equal(session.Context.Events.Time, engine.ChangedWeaponTime);
        Assert.Equal(123.0f, walker.PrimaryWeapon.ChargeValue);
        Assert.Equal(calls + 1, session.Context.Crt.Calls); // announces the still-current primary
    }

    [GameDataFact]
    public void SecondarySelectionLosesNewChargePreservesOldChargeAndDrawsHudSound()
    {
        var (session, engine, walker) = Loaded();
        foreach (var w in walker.Weapons) w.WeaponActive = false;
        walker.PrimaryWeapon!.WeaponActive = true;
        walker.PrimaryWeapon.ChargeValue = 77.0f;
        Weapon secondary = walker.Weapons[0];
        secondary.WeaponActive = true;
        secondary.ChargeValue = 123.0f;
        engine.StoreHeat[secondary.Data.AmmoStore] = true;
        engine.SlowMovement = true;
        long calls = session.Context.Crt.Calls;
        engine.ChangeWeapon();
        Assert.Same(secondary, engine.GetCurrentWeapon());
        Assert.Equal(1, walker.CurrentWeapon);
        Assert.False(engine.SlowMovement);
        Assert.Equal(0.0f, secondary.ChargeValue);
        Assert.Equal(77.0f, walker.PrimaryWeapon.ChargeValue);
        Assert.Equal(session.Context.Events.Time, engine.ChangedWeaponTime);
        Assert.Equal(calls + 1, session.Context.Crt.Calls);
    }

    [GameDataFact]
    public void OrdinaryRightMouseReleaseSelectsOnceAndHoldingDoesNotCycle()
    {
        var (session, engine, walker) = Loaded();
        // Let the tutorial finish its initial power-up script before exercising a player's weapon input.
        while (session.PresentCount < 3000) session.AdvanceToNextPresent();
        Assert.Equal(GameState.Playing, session.Game.State);
        Assert.True(engine.PoweredUp);
        foreach (var w in walker.Weapons) w.WeaponActive = true;
        walker.PrimaryWeapon!.WeaponActive = true;
        Array.Fill(engine.StoreHeat, true);
        session.Input.Mouse.AddMouseDelta(0, 0, 0, 0x02); // right mouse; retail's MouseButtonOnce reads release
        for (int i = 0; i < 8; i++) session.AdvanceToNextPresent();
        Assert.Equal(0, walker.CurrentWeapon);
        Assert.Equal(-20.0f, engine.ChangedWeaponTime);
        session.Input.Mouse.AddMouseDelta(0, 0, 0, 0);
        long deadline = session.PresentCount + 8;
        while (walker.CurrentWeapon == 0 && session.PresentCount < deadline) session.AdvanceToNextPresent();
        Assert.Equal(1, walker.CurrentWeapon);
        float changedAt = engine.ChangedWeaponTime;
        for (int i = 0; i < 8; i++) session.AdvanceToNextPresent();
        Assert.Equal(changedAt, engine.ChangedWeaponTime);
        session.Input.Mouse.AddMouseDelta(0, 0, 0, 0x02);
        for (int i = 0; i < 20; i++) session.AdvanceToNextPresent();
        Assert.Equal(1, walker.CurrentWeapon);
        Assert.Equal(changedAt, engine.ChangedWeaponTime);
        session.Input.Mouse.AddMouseDelta(0, 0, 0, 0);
        deadline = session.PresentCount + 8;
        while (engine.ChangedWeaponTime == changedAt && session.PresentCount < deadline) session.AdvanceToNextPresent();
        Assert.NotEqual(changedAt, engine.ChangedWeaponTime);
        Assert.Equal((1 + 1) % walker.CountWeapons(), walker.CurrentWeapon);
    }
}
