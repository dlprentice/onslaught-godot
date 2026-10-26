// SPDX-License-Identifier: GPL-3.0-only
// Source contract: CBattleEngine::NeedsRearm0040c5e0, Rearm0040ac50 and CRepairPadAI::CanService004d6e00,
// onslaught-decomp BattleEngine.cpp and RepairPadAI.cpp. The loaded caller case uses installed L100 definitions;
// direct shot-payment, pad-selection and rearm calls do not claim completion of the tutorial's player route.
// The original NeedsRearm body (66 bytes, SHA256 9ec25db8a03b616394764394bb61676a0d4e6c5bbec4e9b1311fa11c63daf00e)
// branches on x87 C0, so either NaN operand requests rearming; C# source-spelling '<' would lose that behavior.
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class BattleEngineRearmTests
{
    private static BattleEngine Engine()
    {
        var pool = new SPtrSetPool(); pool.Init(64);
        var events = new EventManager(); events.Init();
        var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(1), null!, new PhysicsRegistry());
        var be = new BattleEngine(context) { Configuration = new BattleEngineData() };
        for (int i = 0; i < BattleEngine.Stores; ++i) be.StoreValue[i] = be.Configuration.StoreValue[i];
        return be;
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void AnyDepletedAmmunitionStoreRequestsRearmWithoutMutatingIt(int store)
    {
        var be = Engine();
        be.StoreValue[store] = MathF.BitDecrement(be.Configuration!.StoreValue[store]);
        float[] values = [.. be.StoreValue]; bool[] heat = [.. be.StoreHeat];
        long random = be.Context.Crt.Calls; int events = be.Context.Events.TotalEvents;
        Assert.True(be.NeedsRearm());
        Assert.Equal(values, be.StoreValue); Assert.Equal(heat, be.StoreHeat);
        Assert.Equal(random, be.Context.Crt.Calls); Assert.Equal(events, be.Context.Events.TotalEvents);
        Assert.DoesNotContain(be.Context.NotPorted.Keys, name => name.Contains("NeedsRearm", StringComparison.Ordinal));
        be.StoreValue[store] = be.Configuration.StoreValue[store]; Assert.False(be.NeedsRearm());
        be.StoreValue[store] = MathF.BitIncrement(be.Configuration.StoreValue[store]); Assert.False(be.NeedsRearm());
    }

    [Fact]
    public void HeatStoresNeverReadConfigurationOrRequestAmmunition()
    {
        var be = Engine(); Array.Fill(be.StoreHeat, true); Array.Fill(be.StoreValue, float.NaN);
        be.Configuration = null; // native's heat guard precedes its configuration dereference
        Assert.False(be.NeedsRearm());
    }

    [Theory]
    [InlineData(float.NaN, 1000f, true)]
    [InlineData(1000f, float.NaN, true)]
    [InlineData(float.NaN, float.NaN, true)]
    [InlineData(float.NegativeInfinity, 1000f, true)]
    [InlineData(float.PositiveInfinity, 1000f, false)]
    [InlineData(1000f, float.PositiveInfinity, true)]
    [InlineData(1000f, float.NegativeInfinity, false)]
    [InlineData(-1f, 0f, true)]
    [InlineData(-0f, 0f, false)]
    public void TheOriginalC0ComparisonAdmitsLessOrUnorderedButNotEquality(float value, float capacity, bool expected)
    {
        var be = Engine(); be.StoreValue[5] = value; be.Configuration!.StoreValue[5] = capacity;
        Assert.Equal(expected, be.NeedsRearm());
        be.StoreHeat[5] = true; Assert.False(be.NeedsRearm());
    }

    [GameDataFact]
    public void LoadedPadSelectsFullHealthEngineAfterRealWeaponPaymentAndReleasesItAfterRearm()
    {
        var session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        var be = Assert.IsType<BattleEngine>(session.Game.GetPlayer(0)!.BattleEngine);
        var pad = Assert.Single(session.Context.World.Things.ToList().OfType<Building>(),
            building => building.UnitData!.NameText == "Forseti Repair Pad");
        var ai = Assert.IsType<RepairPadAI>(pad.AI);
        Assert.True(be.FullHealth()); Assert.False(be.NeedsRearm()); Assert.False(ai.CanService(be));
        // Exercise the installed weapon's actual payment method rather than inventing a store deficit.
        Weapon? ammunition = be.WalkerPart!.Weapons.FirstOrDefault(weapon =>
            !be.StoreHeat[weapon.Data.AmmoStore] && weapon.Data.Consumption > 0 && be.Configuration!.StoreValue[weapon.Data.AmmoStore] > 0);
        bool walker = ammunition is not null;
        ammunition ??= be.JetPart!.Weapons.FirstOrDefault(weapon =>
            !be.StoreHeat[weapon.Data.AmmoStore] && weapon.Data.Consumption > 0 && be.Configuration!.StoreValue[weapon.Data.AmmoStore] > 0);
        Assert.NotNull(ammunition);
        int store = ammunition.Data.AmmoStore; float before = be.StoreValue[store];
        Assert.True(walker ? be.WalkerPart.WeaponFired(ammunition) : be.JetPart!.WeaponFired(ammunition));
        Assert.Equal(MathF.Max(0, before - ammunition.Data.Consumption), be.StoreValue[store]);
        Assert.True(be.FullHealth()); Assert.True(be.NeedsRearm()); Assert.True(ai.CanService(be));
        // Put the genuine loaded engine inside the native pad's horizontal/vertical selection bounds.
        be.Teleport(pad.Pos + new OnslaughtRebuild.Parity.Maths.FVector(0, 0, -1));
        Assert.Same(be, ai.Unknown11()); Assert.Same(be, ai.Target.ToRead);
        Assert.True(ai.CanFireAtTarget); Assert.True(ai.TargetInReach);
        float[] heatValues = [.. be.StoreValue];
        be.Rearm(1f); // the existing native Rearm saturates ammunition stores and leaves heat stores alone
        for (int i = 0; i < BattleEngine.Stores; ++i)
            Assert.Equal(be.StoreHeat[i] ? heatValues[i] : be.Configuration!.StoreValue[i], be.StoreValue[i]);
        Assert.True(be.FullHealth()); Assert.False(be.NeedsRearm()); Assert.False(ai.CanService(be));
        Assert.Null(ai.Unknown11()); Assert.Null(ai.Target.ToRead);
        Assert.False(ai.CanFireAtTarget); Assert.False(ai.TargetInReach);
    }
}
