// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// World 110's base-world carry-over from Level 100 (the RE lane's World 110
/// seed contract, "Level 100 to World 110: base-world carry-over", and its
/// construction contract, "From a Level 100 win to World 110"): FillOut's
/// survivor list, the career's copy onto World 110's node, and the load that
/// skips a lost row.
/// </summary>
public sealed class World110CarryOverTests
{
    private static readonly Lazy<Level100ActorDefinitionSet> s_world110 =
        new(Level100TestActorDefinitions.LoadMaterializedWorld110);

    private static readonly Lazy<Level100ActorDefinitionSet> s_level100 =
        new(Level100TestActorDefinitions.LoadMaterialized);

    private const int BaseRows = RetailFillOutEndLevelData.Level100BaseWorldThingCount;

    private static int DrawsTo(int seed)
    {
        var random = new Level100ReleasedRandom();
        int draws = 0;
        while (random.Seed != seed)
        {
            random.Next();
            Assert.InRange(++draws, 0, 100_000);
        }

        return draws;
    }

    private static int Bits(float value) => BitConverter.SingleToInt32Bits(value);

    [Fact]
    public void FullCarryOver_BuildsTheWholeBaseWorld()
    {
        var simulation = new Simulation(123456u, s_world110.Value, worldNumber: 110, lostBaseRows: []);
        WorldSnapshot load = simulation.LoadSnapshotForMeasurement!;
        Assert.Equal(1_622, DrawsTo(load.Level100ActorMechanics.ReleasedRandomSeed));
        Assert.Empty(load.Level100ActorMechanics.LandscapeDamageStamps);
        Assert.Empty(load.Level100Actors.LostBaseRows);
    }

    /// <summary>
    /// The Tank Factory (row 1, a building) lost in Level 100. The load skips
    /// it and takes twenty draws in place of its one Actor draw, after the
    /// pines (1,481), the influence map (1) and the Control Tower (row 0, 1).
    /// Each pair stamps damage type 6 at the factory's position plus
    /// ((r mod 65536)·2⁻¹⁶ − 0.5) × 5.0, the first draw for Y
    /// (<c>0x0050d09c-0x0050d123</c>). Setup's <c>GetThingRef("Tank
    /// Factory")</c> then finds nothing, so its <c>Exists</c> check skips the
    /// activation while the turrets still activate.
    /// </summary>
    [Fact]
    public void LostBuilding_IsNotBuiltAndLeavesTwentyDrawsOfDamage()
    {
        var simulation = new Simulation(123456u, s_world110.Value, worldNumber: 110, lostBaseRows: [1]);
        WorldSnapshot load = simulation.LoadSnapshotForMeasurement!;
        Assert.Equal(1_622 - 1 + 20, DrawsTo(load.Level100ActorMechanics.ReleasedRandomSeed));
        Assert.Equal([1], load.Level100Actors.LostBaseRows);
        Assert.DoesNotContain(load.Level100Actors.Actors, actor => actor.Name == "Tank Factory");

        Level100ActorDefinition factory = s_world110.Value.Actors.Single(actor =>
            actor.DefinitionIdentity == "wres:bswd:0001");
        float x = BitConverter.Int32BitsToSingle(factory.AuthoredTransform.RetailPositionFloatBits.X);
        float y = BitConverter.Int32BitsToSingle(factory.AuthoredTransform.RetailPositionFloatBits.Y);
        var random = new Level100ReleasedRandom();
        for (int draw = 0; draw < 1_481 + 1 + 1; draw++)
        {
            random.Next();
        }

        var expected = new List<Level100LandscapeDamageStamp>();
        for (int stamp = 0; stamp < 10; stamp++)
        {
            float stampY = y + ((((random.Next() % 65536) / 65536f) - 0.5f) * 5f);
            float stampX = x + ((((random.Next() % 65536) / 65536f) - 0.5f) * 5f);
            expected.Add(new Level100LandscapeDamageStamp(Bits(stampX), Bits(stampY), 6));
        }

        Assert.Equal(expected, load.Level100ActorMechanics.LandscapeDamageStamps);

        WorldSnapshot start = simulation.Snapshot;
        Assert.DoesNotContain(start.Level100Actors.Actors, actor => actor.Name == "Tank Factory");
        Assert.All(["Turret 01", "Turret 02", "Turret 03", "Turret 04"], name =>
            Assert.True(Assert.Single(start.Level100Actors.Actors, actor => actor.Name == name).Active));
    }

    /// <summary>
    /// A lost cannon (Turret 03, row 3) is skipped too, but the cannon type
    /// setter (<c>0x0050ea20</c>, <c>0x40040220</c>) has no building bit, so
    /// its two construction draws (Actor and fire control) simply go.
    /// </summary>
    [Fact]
    public void LostCannon_IsNotBuiltAndLeavesNoDamage()
    {
        var simulation = new Simulation(123456u, s_world110.Value, worldNumber: 110, lostBaseRows: [3]);
        WorldSnapshot load = simulation.LoadSnapshotForMeasurement!;
        Assert.Equal(1_622 - 2, DrawsTo(load.Level100ActorMechanics.ReleasedRandomSeed));
        Assert.Empty(load.Level100ActorMechanics.LandscapeDamageStamps);
        Assert.DoesNotContain(load.Level100Actors.Actors, actor => actor.Name == "Turret 03");
    }

    /// <summary>
    /// FillOut reads each base row from the end state: 1 when built and not
    /// dying, else 0 (<c>0x0046d4cb-0x0046d4d1</c>). The SafeSides (rows 21
    /// and 22) are never actors in Core and never die; a row the load skipped
    /// reads 0.
    /// </summary>
    [Fact]
    public void FillOut_ReadsEachBaseRowFromTheEndState()
    {
        WorldSnapshot state = new Simulation(1u, s_level100.Value).Snapshot;
        int[] left = RetailFillOutEndLevelData.BaseThingsLeft(state, s_level100.Value);
        Assert.Equal(Enumerable.Repeat(1, BaseRows), left.Take(BaseRows));
        Assert.All(left.Skip(BaseRows), word => Assert.Equal(0, word));

        Level100ActorId tower = state.Level100Actors.Actors.Single(actor =>
            actor.DefinitionIdentity == "wres:bswd:0000").ActorId;
        WorldSnapshot dying = state with
        {
            Level100Actors = state.Level100Actors with
            {
                Actors = state.Level100Actors.Actors.Select(actor => actor.ActorId == tower
                    ? actor with { Lifecycle = Level100ActorLifecycle.StartedDying }
                    : actor).ToArray(),
            },
        };
        int[] afterDeath = RetailFillOutEndLevelData.BaseThingsLeft(dying, s_level100.Value);
        Assert.Equal((0, 1, 1, 1), (afterDeath[0], afterDeath[1], afterDeath[21], afterDeath[22]));

        var skipped = new Simulation(123456u, s_world110.Value, worldNumber: 110, lostBaseRows: [1]);
        Assert.Equal(0, RetailFillOutEndLevelData.BaseThingsLeft(skipped.Snapshot, s_world110.Value)[1]);
    }

    /// <summary>
    /// The transition: a Level 100 win's FillOut reaches World 110's node
    /// through <c>ReCalcLinks</c>, and World 110's load skips exactly the rows
    /// that node marks lost. A win that loses nothing keeps the whole base
    /// world.
    /// </summary>
    [Fact]
    public void Level100Win_CarriesItsSurvivorsIntoWorld110sLoad()
    {
        RetailCareerCampaign full = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        full.ApplyUpdate(RetailFillOutEndLevelData.ForLevel100Won(
            baseThingsLeft: RetailFillOutEndLevelData.BaseThingsLeft(
                new Simulation(1u, s_level100.Value).Snapshot, s_level100.Value)));
        Assert.Empty(full.Nodes.LostBaseRows(110, BaseRows));

        int[] left = RetailFillOutEndLevelData.FirstPlayBaseThingsLeft();
        left[1] = 0;
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        career.ApplyUpdate(RetailFillOutEndLevelData.ForLevel100Won(baseThingsLeft: left));
        IReadOnlyList<int> lost = career.Nodes.LostBaseRows(110, BaseRows);
        Assert.Equal([1], lost);
        var world110 = new Simulation(123456u, s_world110.Value, worldNumber: 110, lostBaseRows: lost);
        Assert.DoesNotContain(world110.Snapshot.Level100Actors.Actors, actor => actor.Name == "Tank Factory");

        // Levels 850-899 keep every row even when their node marks one lost,
        // and so does a world with no node.
        career.Nodes.Add(851, complete: 0).SetBaseThingExistTo(1, 0);
        Assert.Empty(career.Nodes.LostBaseRows(851, BaseRows));
        career.Nodes.Add(849, complete: 0).SetBaseThingExistTo(1, 0);
        Assert.Equal([1], career.Nodes.LostBaseRows(849, BaseRows));
        Assert.Empty(career.Nodes.LostBaseRows(12_345, BaseRows));
    }
}
