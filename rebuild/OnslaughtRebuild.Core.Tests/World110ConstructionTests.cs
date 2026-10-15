// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// World 110's construction from its materialized static world, against the RE
/// lane's contract
/// (<c>reverse-engineering/game-mechanics/world-110-construction-order.md</c>).
/// </summary>
public sealed class World110ConstructionTests
{
    private static readonly Lazy<Level100ActorDefinitionSet> s_world110 =
        new(Level100TestActorDefinitions.LoadMaterializedWorld110);

    [Fact]
    public void Registry_AdmitsEveryWorld110Row()
    {
        var registry = new Level100ActorRegistry(s_world110.Value);
        Assert.Equal(77, registry.Snapshot.Actors.Count);
    }

    [Fact]
    public void Simulation_ConstructsWorld110()
    {
        var simulation = new Simulation(123456u, s_world110.Value, worldNumber: 110);
        Assert.Equal(110, simulation.WorldNumber);
    }

    /// <summary>
    /// The load takes the contract's draws in order: the shared base world's
    /// 1,481 pines and its influence-map draw, base rows 0-34 as in Level 100
    /// (18 buildings, 4 cannons with fire control, 6 icebergs, 5 city
    /// buildings: 37), the level rows' 102 under the static target estimate,
    /// and the tail's one influence draw ("Load order", "Level-world rows").
    /// </summary>
    [Fact]
    public void Load_TakesTheContractDraws()
    {
        var simulation = new Simulation(123456u, s_world110.Value, worldNumber: 110);
        WorldSnapshot load = simulation.LoadSnapshotForMeasurement!;
        Assert.Equal(1_481 + 1 + 37 + 102 + 1, DrawsTo(load.Level100ActorMechanics.ReleasedRandomSeed));
    }

    /// <summary>
    /// The scripts' first frames (the contract's "Script inits"): LevelScript
    /// deactivates the player and plays its first message, Setup activates the
    /// Tank Factory and the four turrets and binds VitalBuilding, Weather sets
    /// the snow, the Lander scripts switch their craft to AI_ONF and row 12
    /// lands.
    /// </summary>
    [Fact]
    public void PreRun_RunsEveryWorld110ScriptInit()
    {
        var simulation = new Simulation(123456u, s_world110.Value, worldNumber: 110);
        WorldSnapshot start = simulation.Snapshot;
        Level100ActorSnapshot Named(string name) =>
            Assert.Single(start.Level100Actors.Actors, actor => actor.Name == name);
        Assert.Contains(start.Level100MissionEvents, item => item is Level100PlayerActivationChanged { Active: false });
        Assert.All(["Tank Factory", "Turret 01", "Turret 02", "Turret 03", "Turret 04"],
            name => Assert.True(Named(name).Active));
        Assert.False(Named("Airfield").Active);
        Assert.Equal("VitalBuilding", Named("Forseti Research Building 1").ScriptName);
        Assert.All(start.Level100ActorScripts.Instances, instance => Assert.True(instance.Initialized));
        // Weather, the carrier at row 39: SetSnowDensity(1).
        Assert.Contains(start.Level100ActorScripts.Instances, instance => instance.ProgramName == "Weather");
        Assert.Contains(start.Level100ActorScriptCommands, command =>
            command.Kind == Level100ActorScriptCommandKind.SetSnowDensity && command.ActorId is null);

        int AiState(string identity) => start.Level100ActorMechanics.Actors.Single(actor =>
            actor.ActorId == start.Level100Actors.Actors.Single(item => item.DefinitionIdentity == identity).ActorId).AiState;
        Assert.Equal(4, AiState("wres:rlwd:0008"));
        Assert.Equal(4, AiState("wres:rlwd:0020"));
        Assert.Equal(0, AiState("wres:rlwd:0012"));
        Assert.Equal(
            [("wres:rlwd:0012", 2)],
            start.Level100ActorMechanics.Actors.Where(actor => actor.DropshipLandingState != 0)
                .Select(actor => (start.Level100Actors.Actors.Single(item => item.ActorId == actor.ActorId).DefinitionIdentity,
                    actor.DropshipLandingState)));
    }

    /// <summary>
    /// The first bucket's order: each carrier's INIT_SCRIPT at its row (0, 2,
    /// 39), each landing craft's first in its construction, and Scout's bound
    /// by the squad after all four of its members' AI events (the contract's
    /// "Type-28 squads": <c>0x004e61b4</c> runs after the members).
    /// </summary>
    [Fact]
    public void Load_FilesEveryInitScriptAtItsRow()
    {
        var simulation = new Simulation(123456u, s_world110.Value, worldNumber: 110);
        WorldSnapshot load = simulation.LoadSnapshotForMeasurement!;
        RetailEventSchedulerSnapshot events = load.Level100ActorMechanics.PlaneEvents!;
        Dictionary<int, RetailEventSlotSnapshot> slots = events.Slots.ToDictionary(slot => slot.Handle);
        Dictionary<Level100ActorId, Level100ActorSnapshot> actors =
            load.Level100Actors.Actors.ToDictionary(actor => actor.ActorId);
        RetailEventSlotSnapshot[] lane = events.Lanes
            .Single(item => item.LaneIndex == events.CurrentBufferNum * RetailEventScheduler.PriorityLanes)
            .Handles.Select(handle => slots[handle]).ToArray();
        string[] inits = lane
            .Where(slot => slot.EventNum == Level100ActorMechanics.InitScriptEvent &&
                Level100ActorMechanics.IsScriptListener(slot.Listener))
            .Select(slot => Level100ActorMechanics.IsCarrierScriptListener(slot.Listener)
                ? $"row {Level100ActorMechanics.CarrierScriptRow(slot.Listener)}"
                : actors[Level100ActorMechanics.ScriptListenerActor(slot.Listener)].DefinitionIdentity)
            .ToArray();
        Assert.Equal(
        [
            "row 0", "row 2", "wres:rlwd:0008", "wres:rlwd:0012", "wres:rlwd:0013",
            "wres:rlwd:0019", "wres:rlwd:0020", "row 39",
        ], inits);

        Level100ActorId scout = actors.Values.Single(actor => actor.DefinitionIdentity == "wres:rlwd:0019").ActorId;
        int scoutInit = Array.FindIndex(lane, slot => slot.EventNum == Level100ActorMechanics.InitScriptEvent &&
            Level100ActorMechanics.IsScriptListener(slot.Listener) &&
            !Level100ActorMechanics.IsCarrierScriptListener(slot.Listener) &&
            Level100ActorMechanics.ScriptListenerActor(slot.Listener) == scout);
        int lastMemberEvent = s_world110.Value.Squads.Single(squad => squad.ScriptName == "Scout").MemberIdentities
            .Select(identity => actors.Values.Single(actor => actor.DefinitionIdentity == identity).ActorId)
            .Max(member => Array.FindLastIndex(lane, slot => slot.Listener == Level100ActorMechanics.UnitAiListener(member)));
        Assert.True(lastMemberEvent >= 0 && scoutInit > lastMemberEvent);
    }

    /// <summary>
    /// One landing craft and its turret child, scriptless, so that every draw
    /// and due time is predictable. The craft's script effect is applied as
    /// the command it would issue.
    /// </summary>
    private static Level100ActorMechanics OneLander(string identity, out Level100ActorId craftId)
    {
        Level100ActorDefinitionSet world = s_world110.Value;
        Level100ComponentDefinition component = world.Components.Single(item => item.ParentIdentity == identity);
        Level100ActorDefinition craft = world.Actors.Single(actor => actor.DefinitionIdentity == identity) with
        {
            AuthoredOrder = 0,
            ScriptName = null,
        };
        Level100ActorDefinition turret = world.Actors.Single(actor =>
            actor.DefinitionIdentity == component.ChildIdentity) with { AuthoredOrder = 1 };
        var definitions = new Level100ActorDefinitionSet(
            [craft, turret], [], world.WaypointPaths,
            world.MotionDefinitions.Where(motion => motion.DefinitionName == craft.DefinitionName),
            worldNumber: 110, components: [component]);
        var registry = new Level100ActorRegistry(definitions);
        craftId = registry.Snapshot.Actors.Single(actor => actor.DefinitionIdentity == identity).ActorId;
        return new Level100ActorMechanics(registry, definitions);
    }

    private static RetailEventSlotSnapshot PendingAi(Level100ActorMechanics mechanics, Level100ActorId actorId)
    {
        RetailEventSchedulerSnapshot events = mechanics.Snapshot.PlaneEvents!;
        return Assert.Single(events.Slots, slot => slot.Listener == Level100ActorMechanics.UnitAiListener(actorId) &&
            events.Lanes.SelectMany(lane => lane.Handles).Concat(events.Overflow).Contains(slot.Handle));
    }

    private static uint Bits(double value) => BitConverter.SingleToUInt32Bits((float)value);

    /// <summary>
    /// Rows 8 and 20 think in AI_ONF (the contract's delivery table): the
    /// dispatcher runs <c>CDropshipAI::Update</c> (draw <c>0x0044880f</c>),
    /// then polls (draw <c>0x004ff371</c>) and files 3003 at now + 2.0 +
    /// (r mod 65536)·2⁻¹⁵. Frame 1 delivers, in filing order, the turret
    /// child's MOVE (one draw), 4003 (one) and AI (one), then the craft's
    /// 4003 (one) and AI (two).
    /// </summary>
    [Fact]
    public void LandingCraft_InAiOnf_RunsItsUpdateThenPolls()
    {
        Level100ActorMechanics mechanics = OneLander("wres:rlwd:0008", out Level100ActorId craft);
        var random = new Level100ReleasedRandom();
        random.Next(); // the craft's Actor draw
        random.Next(); // the turret child's Actor draw
        Assert.Equal(random.Seed, mechanics.Snapshot.ReleasedRandomSeed);
        mechanics.ApplyCommand(new Level100ActorScriptCommand(
            1, 0, craft, Level100ActorScriptCommandKind.SetAIState, null, null, 4));

        mechanics.AdvanceTick();
        float now = RetailEventScheduler.TimeAtFrameCount(1);
        for (int draw = 0; draw < 5; draw++) random.Next(); // turret MOVE, 4003, AI; craft 4003; Update
        int poll = random.Next() % 65536;
        Assert.Equal(random.Seed, mechanics.Snapshot.ReleasedRandomSeed);
        RetailEventSlotSnapshot pending = PendingAi(mechanics, craft);
        Assert.Equal((3003, Bits(RetailFloat24.Add(RetailFloat24.Add(now, 2.0), RetailFloat24.Multiply(poll, 1.0 / 32768.0)))),
            ((int)pending.EventNum, pending.TimeBits));
    }

    /// <summary>
    /// Row 12 lands, then switches to AI_ON: <c>CDropshipAI</c> slot 9
    /// (<c>0x00448580</c>) does nothing in landing state 2, then draws once
    /// (<c>0x00448763</c>) and files 3000 at ((r mod 65536)·2⁻¹⁶ + 1.0) + now.
    /// </summary>
    [Fact]
    public void LandedCraft_ThinksOnItsOwnOneToTwoSecondCadence()
    {
        Level100ActorMechanics mechanics = OneLander("wres:rlwd:0012", out Level100ActorId craft);
        var random = new Level100ReleasedRandom();
        random.Next();
        random.Next();
        mechanics.ApplyCommand(new Level100ActorScriptCommand(
            1, 0, craft, Level100ActorScriptCommandKind.Land, null, null, 0));
        Assert.Equal(2, mechanics.Snapshot.Actors.Single(actor => actor.ActorId == craft).DropshipLandingState);

        mechanics.AdvanceTick();
        float now = RetailEventScheduler.TimeAtFrameCount(1);
        for (int draw = 0; draw < 4; draw++) random.Next(); // turret MOVE, 4003, AI; craft 4003
        int think = random.Next() % 65536;
        Assert.Equal(random.Seed, mechanics.Snapshot.ReleasedRandomSeed);
        RetailEventSlotSnapshot pending = PendingAi(mechanics, craft);
        Assert.Equal((3000, Bits(RetailFloat24.Add(RetailFloat24.Add(RetailFloat24.Multiply(think, 1.0 / 65536.0), 1.0), now))),
            ((int)pending.EventNum, pending.TimeBits));

        // The turret child moves every frame with one draw.
        mechanics.AdvanceTick();
        random.Next();
        Assert.Equal(random.Seed, mechanics.Snapshot.ReleasedRandomSeed);
    }

    /// <summary>
    /// A landed craft's state restores from the snapshot, and the canonical
    /// hash refuses it: World 110 has no admitted hash schema yet.
    /// </summary>
    [Fact]
    public void LandedCraft_RestoresItsLandingStateAndIsNotHashed()
    {
        var simulation = new Simulation(123456u, s_world110.Value, worldNumber: 110);
        WorldSnapshot state = simulation.Snapshot;
        var registry = new Level100ActorRegistry(s_world110.Value, state.Level100Actors);
        var restored = new Level100ActorMechanics(registry, s_world110.Value, state.Level100ActorMechanics);
        Level100ActorId landed = state.Level100Actors.Actors.Single(actor => actor.DefinitionIdentity == "wres:rlwd:0012").ActorId;
        Assert.Equal(2, restored.Snapshot.Actors.Single(actor => actor.ActorId == landed).DropshipLandingState);
        Assert.Throws<NotSupportedException>(() => StateHasher.ComputeHex(state));
    }

    /// <summary>
    /// A fighter (row 25) alone. Construction takes its Actor draw and
    /// <c>CPlane::Init</c>'s last draw (<c>0x004d1bae</c>). Frame 1 delivers
    /// its 4003 (one draw), guide 2000 and 2001 (one each) and its AI:
    /// <c>CPlaneAI</c> slot 9 (<c>0x004d21c0</c>) takes the Update's arm draw,
    /// then one draw (<c>0x004d2434</c>) and files 3000 at
    /// ((r mod 65536)·2⁻¹⁸ + 0.25) + now.
    /// </summary>
    [Fact]
    public void Fighter_ThinksOnTheCPlaneAiCadence()
    {
        Level100ActorDefinitionSet world = s_world110.Value;
        Level100ActorDefinition row = world.Actors.Single(actor => actor.DefinitionIdentity == "wres:rlwd:0025") with
        {
            AuthoredOrder = 0,
        };
        var definitions = new Level100ActorDefinitionSet(
            [row], [], world.WaypointPaths,
            [world.MotionDefinitions.Single(motion => motion.DefinitionName == row.DefinitionName) with { AuthoredOrder = 0 }],
            worldNumber: 110);
        var registry = new Level100ActorRegistry(definitions);
        Level100ActorId fighter = Assert.Single(registry.Snapshot.Actors).ActorId;
        var mechanics = new Level100ActorMechanics(registry, definitions);
        var random = new Level100ReleasedRandom();
        random.Next();
        random.Next();
        Assert.Equal(random.Seed, mechanics.Snapshot.ReleasedRandomSeed);

        mechanics.AdvanceTick();
        float now = RetailEventScheduler.TimeAtFrameCount(1);
        for (int draw = 0; draw < 4; draw++) random.Next(); // 4003, guide 2000, guide 2001, the Update's arm
        int think = random.Next() % 65536;
        Assert.Equal(random.Seed, mechanics.Snapshot.ReleasedRandomSeed);
        RetailEventSlotSnapshot pending = PendingAi(mechanics, fighter);
        Assert.Equal((3000, Bits(RetailFloat24.Add(RetailFloat24.Add(RetailFloat24.Multiply(think, 1.0 / 262144.0), 0.25), now))),
            ((int)pending.EventNum, pending.TimeBits));
    }

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

    /// <summary>
    /// The start state (the contract's "Player start"): the two-second pan
    /// ends on frame 100, where <c>StartPlayingState</c> posts "game playing".
    /// Scout answers it with "Enemy Engaged", and LevelScript's handler pauses
    /// two seconds, then activates the Airfield and the player on frame 140 and
    /// posts "Target Buildings", which VitalBuilding answers by activating the
    /// Research Building and making it the objective. The opening message
    /// waits for World 110's message gate, frame 101; Scout's follows once it
    /// clears.
    /// </summary>
    [Fact]
    public void StartState_ActivatesThePlayerTwoSecondsAfterGamePlaying()
    {
        var simulation = new Simulation(123456u, s_world110.Value, worldNumber: 110);
        WorldSnapshot state = simulation.Snapshot;
        Assert.Equal(60u, state.RetailEventFrameCount);
        Assert.Equal(SimulationConstants.OpeningPanTicks(110), state.Level100OpeningTicksRemaining);
        Level100ActorId Named(string name) =>
            state.Level100Actors.Actors.Single(actor => actor.Name == name).ActorId;
        Level100ActorId airfield = Named("Airfield");
        Level100ActorId research = Named("Forseti Research Building 1");

        var events = new List<(uint Frame, Level100MissionEvent Event)>();
        uint panEnd = 0;
        while (state.RetailEventFrameCount < 200)
        {
            bool panning = state.Level100OpeningTicksRemaining > 0;
            state = simulation.Step(SimInput.Idle);
            if (panning && state.Level100OpeningTicksRemaining == 0)
            {
                panEnd = state.RetailEventFrameCount;
            }
            uint frame = state.RetailEventFrameCount;
            events.AddRange(state.Level100MissionEvents.Select(item => (frame, item)));
        }

        Assert.Equal(100u, panEnd);
        Assert.Equal(140u, Assert.Single(events, item => item.Event is Level100PlayerActivationChanged { Active: true }).Frame);
        Assert.Equal(140u, Assert.Single(events, item => item.Event is Level100ActorCommandRequested
            { Command: Level100ActorCommand.Activate } command && command.ActorId == airfield).Frame);
        Assert.Equal(140u, Assert.Single(events, item => item.Event is Level100ActorCommandRequested
            { Command: Level100ActorCommand.Activate } command && command.ActorId == Named("Player 1")).Frame);
        Assert.Equal(140u, Assert.Single(events, item => item.Event is Level100MissionEventPosted
            { EventName: "Target Buildings" }).Frame);
        Assert.Equal(
            [(101u, 8444036), (195u, 453985879)],
            events.Where(item => item.Event is Level100MessageRequested)
                .Select(item => (item.Frame, ((Level100MessageRequested)item.Event).MessageId)));
        Assert.True(state.Level100PlayerActive);
        Assert.True(state.Level100Actors.Actors.Single(actor => actor.ActorId == research).IsObjective);
    }
}
