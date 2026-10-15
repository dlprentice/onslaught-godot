// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// The natives World 110's shipped scripts add, each re-read from the pristine
/// specimen (<c>74154bfa…</c>): <c>Rand</c> (<c>0x00538230</c>: one draw on
/// the shared stream <c>0x008a9d9c</c>, then r mod n), <c>GetInitialHealth</c>
/// (<c>0x00535a30</c>), <c>SpawnersEmpty</c> (<c>0x00535a90</c> →
/// <c>0x004fd7e0</c>), <c>Land</c> (<c>0x005361d0</c> → slot 93) and
/// <c>GetNumUnits</c> (<c>0x00535590</c>).
/// </summary>
public sealed class World110ScriptNativeTests
{
    private static readonly Lazy<Level100ActorDefinitionSet> s_world110 =
        new(Level100TestActorDefinitions.LoadMaterializedWorld110);

    private static Level100ActorId Row(Level100ActorRegistry actors, string identity) =>
        actors.Snapshot.Actors.Single(actor => actor.DefinitionIdentity == identity).ActorId;

    /// <summary>
    /// VitalBuilding reads <c>GetInitialHealth</c> after its first second,
    /// then checks every two seconds. Below 60% it takes <c>Rand(3)</c> once
    /// and plays one warning, then <c>_110_RESEARCH_HIT</c>. Its source
    /// <c>switch</c> has no <c>break</c>, but the shipped bytecode ends every
    /// case with a JMP to the switch's end (instructions 36, 47 and 58; opcode
    /// <c>0x14</c>, whose executor <c>0x0052e9b0</c> sets the instruction
    /// pointer unconditionally), so the cases do not fall through.
    /// </summary>
    [Fact]
    public void VitalBuilding_TakesOneRandAndPlaysOneWarning()
    {
        IReadOnlyList<int> Warnings(int draw)
        {
            var actors = new Level100ActorRegistry(s_world110.Value);
            var scripts = new Level100ActorScriptRuntime(actors, actors.GetThingRef("Player 1")!.Value);
            int draws = 0;
            scripts.SharedRandom = () =>
            {
                draws++;
                return draw;
            };
            var messages = new List<int>();
            scripts.RequestMessage = (_, message, waits) =>
            {
                Assert.True(waits);
                messages.Add(message);
                return 1;
            };
            var inits = new List<Level100ActorId>();
            scripts.AttachReleasedScripts(inits.Add);
            scripts.RunSetupInit();
            Level100ActorId research = actors.GetThingRef("Forseti Research Building 1")!.Value;
            Assert.Equal([research], inits);
            scripts.RunScriptInit(research);

            int initial = actors.GetActor(research).Health;
            Assert.Equal(150_000, initial);
            actors.SetHealth(research, 100_000);
            for (int tick = 0; tick < 200; tick++) scripts.AdvanceTick();
            Assert.Equal((0, 0), (draws, messages.Count));

            actors.SetHealth(research, 89_999);
            for (int tick = 0; tick < 200; tick++) scripts.AdvanceTick();
            Assert.Equal(1, draws);
            return messages;
        }

        // HEALTH_LOW_1O, _2O and _3O, then _110_RESEARCH_HIT (the bytecode's constants).
        Assert.Equal([13_904_116, 264_341_189], Warnings(6));
        Assert.Equal([13_949_710, 264_341_189], Warnings(7));
        Assert.Equal([13_995_304, 264_341_189], Warnings(8));
    }

    /// <summary>
    /// Lander2 lands (a command the mechanics apply as landing state 2), then
    /// asks <c>SpawnersEmpty</c> every second. A loaded Landing Craft keeps
    /// its SpawnerA and SpawnerB, which Core does not deploy yet; the Empty
    /// craft has none, so <c>0x004fd7e0</c> returns 1 and, given Lander2, it
    /// withdraws after its first <c>Pause(1)</c>.
    /// </summary>
    [Fact]
    public void Lander2_WithdrawsOnlyWhenItsCraftHasNoSpawners()
    {
        Level100ActorDefinitionSet world = s_world110.Value;
        var definitions = new Level100ActorDefinitionSet(
            world.Actors.Select(actor => actor.DefinitionIdentity == "wres:rlwd:0013"
                ? actor with { ScriptName = "Lander2" }
                : actor),
            world.Spawns, world.WaypointPaths, world.MotionDefinitions, worldNumber: 110,
            baseWorldPineCount: world.BaseWorldPineCount, squads: world.Squads, components: world.Components);
        var actors = new Level100ActorRegistry(definitions);
        var scripts = new Level100ActorScriptRuntime(actors, actors.GetThingRef("Player 1")!.Value);
        scripts.AttachReleasedScripts(_ => { });
        Level100ActorId loaded = Row(actors, "wres:rlwd:0012");
        Level100ActorId empty = Row(actors, "wres:rlwd:0013");
        scripts.RunScriptInit(loaded);
        scripts.RunScriptInit(empty);
        Assert.Equal(
            [(loaded, Level100ActorScriptCommandKind.SetAIState, 4), (loaded, Level100ActorScriptCommandKind.Land, 0),
             (loaded, Level100ActorScriptCommandKind.SetAIState, 0),
             (empty, Level100ActorScriptCommandKind.SetAIState, 4), (empty, Level100ActorScriptCommandKind.Land, 0),
             (empty, Level100ActorScriptCommandKind.SetAIState, 0)],
            scripts.DrainCommands().Select(command => (command.ActorId!.Value, command.Kind, command.Scalar)));

        for (int tick = 0; tick < 19; tick++) scripts.AdvanceTick();
        Assert.Empty(scripts.DrainPostedEvents());
        scripts.AdvanceTick();
        Assert.Equal([(empty, "Lander Withdraws")],
            scripts.DrainPostedEvents().Select(item => (item.ActorId!.Value, item.EventName)));
        for (int tick = 0; tick < 200; tick++) scripts.AdvanceTick();
        Assert.Empty(scripts.DrainPostedEvents());
    }

    /// <summary>
    /// <c>GetNumUnits</c> counts per behaviour selector and side: the 15
    /// enemy Light Gun Tanks (rows 14, 16, 18) are selector 2 and side 1, the
    /// seven AV-14Bs (rows 17, 19) side 0; the landing craft are selector 12
    /// and the fighters 8. Any other side reads 0. A unit leaves its count
    /// when it starts to die (<c>0x004fd140</c>).
    /// </summary>
    [Fact]
    public void GetNumUnits_CountsLivingUnitsBySelectorAndSide()
    {
        var actors = new Level100ActorRegistry(s_world110.Value);
        Assert.Equal(
            (15, 7, 4, 6, 0),
            (actors.CountUnits(2, 1), actors.CountUnits(2, 0), actors.CountUnits(12, 1), actors.CountUnits(8, 1),
             actors.CountUnits(2, 2)));
        Level100ActorId tank = Row(actors, s_world110.Value.Squads[0].MemberIdentities[0]);
        Assert.True(actors.ReportStartedDying(tank));
        Assert.Equal(14, actors.CountUnits(2, 1));

        // A side-2 unit is in neither count: CUnit::Init increments only
        // sides 0 and 1, and GetNumUnits reads 0 for any other side.
        Level100ActorDefinitionSet world = s_world110.Value;
        var sideTwo = new Level100ActorRegistry(new Level100ActorDefinitionSet(
            world.Actors.Select(actor => actor.DefinitionIdentity == "wres:rlwd:0025" ? actor with { Allegiance = 2 } : actor),
            world.Spawns, world.WaypointPaths, world.MotionDefinitions, worldNumber: 110,
            baseWorldPineCount: world.BaseWorldPineCount, squads: world.Squads, components: world.Components));
        Assert.Equal((5, 0), (sideTwo.CountUnits(8, 1), sideTwo.CountUnits(8, 2)));
    }
}
