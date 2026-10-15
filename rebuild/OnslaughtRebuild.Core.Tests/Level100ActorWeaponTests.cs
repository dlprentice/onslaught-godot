// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Actor-owned weapons: the released random stream, the released scatter law,
/// the released firing gate, and the <c>CRoundSeek 3</c> homing law.
///
/// Nothing in this file posts a mission event or writes a health value
/// directly. Every observation is produced by advancing
/// <see cref="Level100ActorMechanics"/> after issuing exactly the
/// <c>Attack(player)</c> command <c>AirborneDrone2.msl</c>'s <c>init()</c>
/// issues.
/// </summary>
public sealed class Level100ActorWeaponTests
{
    private readonly ITestOutputHelper _output;

    public Level100ActorWeaponTests(ITestOutputHelper output) =>
        _output = output;

    /// <summary>
    /// The released stream, reproduced. The seed is retail's own constant
    /// 123456 and the arithmetic is retail's own 32-bit Schrage step with the
    /// shipped modulus 214783647, so the sequence is not an invention: it is
    /// the shipped generator run from the shipped seed.
    ///
    /// This test also pins the two properties the shipped defect produces -
    /// the state leaves [1, m) almost immediately, and it never reaches zero
    /// (a zero state would step to m, so there is no lock-up).
    /// </summary>
    [Fact]
    public void ReleasedRandom_ReproducesTheShippedStreamFromTheShippedSeed()
    {
        var first = new Level100ReleasedRandom();
        var second = new Level100ReleasedRandom(
            SimulationConstants.Level100ReleasedRandomInitialSeed);

        int[] head = Enumerable.Range(0, 8).Select(_ => first.Next()).ToArray();
        int[] repeat = Enumerable.Range(0, 8).Select(_ => second.Next()).ToArray();
        Assert.Equal(head, repeat);
        _output.WriteLine(
            "first eight draws from seed 123456: " + string.Join(", ", head));

        // Restoring from a snapshotted seed continues the same stream, which
        // is what makes the whole thing hashable and replayable.
        var resumed = new Level100ReleasedRandom(first.Seed);
        Assert.Equal(first.Next(), resumed.Next());

        var stream = new Level100ReleasedRandom();
        bool leftNominalRange = false;
        for (int step = 0; step < 200_000; step++)
        {
            stream.Next();
            Assert.NotEqual(0, stream.Seed);
            if (stream.Seed < 1 ||
                stream.Seed > SimulationConstants.Level100ReleasedRandomModulus)
            {
                leftNominalRange = true;
            }
        }
        Assert.True(
            leftNominalRange,
            "the shipped modulus breaks the Schrage precondition, so the " +
            "state must leave [1, m); a stream that stays inside it is not " +
            "the shipped stream.");
    }

    /// <summary>
    /// The scatter sample is exactly
    /// <c>((sample % 65536) / 32768 - 1) * CWeaponInaccuracy</c>, so a
    /// 1-degree inaccuracy can never exceed 1 degree in magnitude from a
    /// non-negative draw, and a zero inaccuracy still consumes the stream.
    /// </summary>
    [Fact]
    public void ReleasedScatter_IsBoundedByTheShippedInaccuracy()
    {
        var stream = new Level100ReleasedRandom();
        int inaccuracy =
            SimulationConstants.Level100DroneVulcanInaccuracyMicroRadians;
        int maximum = 0;
        for (int draw = 0; draw < 4_096; draw++)
        {
            int sample = stream.NextSignedUnitScaled(inaccuracy);
            maximum = Math.Max(maximum, Math.Abs(sample));
        }
        _output.WriteLine(
            $"largest |scatter| over 4096 draws: {maximum} urad against the " +
            $"shipped {inaccuracy} urad CWeaponInaccuracy");
        Assert.True(maximum <= inaccuracy);

        int before = stream.Seed;
        Assert.Equal(0, stream.NextSignedUnitScaled(0));
        Assert.NotEqual(before, stream.Seed);
    }

    /// <summary>
    /// The whole released chain, driven only by the <c>Attack(player)</c>
    /// command the script issues: the drone closes to inside
    /// <c>CWeaponMaxRange</c>, passes <c>CWeaponYawTolerance</c>, fires its
    /// <c>Drone Vulcan Cannon</c> burst, and its Blasters reach the player.
    /// </summary>
    [Fact]
    public void AttackingTargetDrone_FiresItsVulcanAndReachesThePlayer()
    {
        Level100ActorDefinitionSet definitions =
            Level100TestActorDefinitions.Create();
        var registry = new Level100ActorRegistry(definitions);
        var mechanics = new Level100ActorMechanics(registry, definitions);
        Level100ActorId airfieldId = Assert.IsType<Level100ActorId>(
            registry.GetThingRef("Airfield"));
        Level100ActorId playerId = Assert.IsType<Level100ActorId>(
            registry.GetThingRef("Player 1"));
        SeatPlayerOnTheTerrain(registry, playerId);
        Level100ActorId droneId = registry.SpawnThing(
            airfieldId,
            "Target Drone",
            "SpawnerA",
            1,
            "AirborneDrone2").Single();
        mechanics.RegisterSpawnedActor(droneId);
        mechanics.ApplyCommand(new Level100ActorScriptCommand(
            1,
            0,
            droneId,
            Level100ActorScriptCommandKind.Attack,
            playerId,
            null,
            0));

        int blasterImpacts = 0;
        int missileImpacts = 0;
        int incomingDamage = 0;
        int firstImpactTick = -1;
        var impactRoundIds = new HashSet<int>();
        for (int tick = 0; tick < 30 * 180; tick++)
        {
            mechanics.AdvanceTick();
            foreach (Level100ActorRoundImpactReceipt receipt in
                     mechanics.DrainActorRoundImpactReceipts())
            {
                Level100ActorRoundImpact impact = receipt.Impact;
                Assert.Equal(playerId, impact.TargetActorId);
                Assert.Equal(droneId, impact.OwnerActorId);
                Assert.True(receipt.RoundId.HasValue);
                Assert.True(impactRoundIds.Add(receipt.RoundId.Value));
                incomingDamage += impact.IncomingDamageMilliLife;
                if (impact.Kind == Level100ActorRoundKind.Blaster)
                {
                    blasterImpacts++;
                }
                else
                {
                    missileImpacts++;
                }
                if (firstImpactTick < 0)
                {
                    firstImpactTick = tick;
                }
            }
        }

        _output.WriteLine(
            $"blaster impacts {blasterImpacts}, missile impacts " +
            $"{missileImpacts}, first at Core tick {firstImpactTick}, total " +
            $"{incomingDamage} incoming milli-life");
        Assert.True(
            blasterImpacts > 0,
            "the released Drone Vulcan Cannon must reach the player it is " +
            "told to attack.");
        Assert.True(incomingDamage > 0);
    }

    /// <summary>
    /// Released Blaster damage is <c>CRoundDamage</c> 0.2 with an explosion
    /// (<c>Small Energy Hit</c>) that carries no <c>CExplosionDamage</c>;
    /// released Forseti Missile data contains <c>CRoundDamage</c> 2.0 plus the
    /// 0.5 <c>CExplosionDamage</c> its <c>Micro Missile Hit</c> inherits from
    /// <c>Small Explosion Base</c>. Core currently emits their decoded 2.5 sum
    /// as one impact. Whether retail calls <c>Damage</c> once with that sum or
    /// separately for the round and explosion remains an explicit runtime
    /// question; the player-damage boundary test preserves the consequential
    /// near-shield-exhaustion difference.
    ///
    /// <para>The expectation is stated in RELEASED POINTS (permille of a life
    /// unit) and converted here, rather than as a hull literal. It used to be
    /// the literals 10 and 125, which silently encoded <c>MaximumHull</c> = 1,000
    /// - a value that declared the Aquila to be 1.0 released life against a
    /// shipped <c>mLife</c> of 20.0. When that unit defect was corrected these
    /// were the only two assertions that moved, precisely because they were the
    /// only ones that had baked the constant in. Deriving them means the next
    /// scale correction cannot be absorbed by editing a number until it passes.</para>
    /// </summary>
    [Theory]
    // Blaster: CRoundDamage 0.2, no CExplosionDamage -> 0.2 released points.
    [InlineData(Level100ActorRoundKind.Blaster, 200)]
    // Forseti: CRoundDamage 2.0 + inherited CExplosionDamage 0.5 -> 2.5 points.
    [InlineData(Level100ActorRoundKind.ForsetiMissile, 2_500)]
    public void ActorRoundImpact_UsesTheDecodedRecordAggregate(
        Level100ActorRoundKind kind,
        int releasedDamageMilliPoints)
    {
        // milli-points of released life -> registry milli-life. The registry's
        // unit IS milli-life (see Level100TargetTankLife), so a released point
        // is exactly 1,000 of them and this conversion is the identity on units.
        int expectedIncomingDamage = releasedDamageMilliPoints *
            SimulationConstants.MaximumHull /
            (SimulationConstants.Level100PlayerReleasedLife * 1_000);
        Level100ActorDefinitionSet definitions =
            Level100TestActorDefinitions.Create();
        var registry = new Level100ActorRegistry(definitions);
        var mechanics = new Level100ActorMechanics(registry, definitions);
        Level100ActorId airfieldId = Assert.IsType<Level100ActorId>(
            registry.GetThingRef("Airfield"));
        Level100ActorId playerId = Assert.IsType<Level100ActorId>(
            registry.GetThingRef("Player 1"));
        SeatPlayerOnTheTerrain(registry, playerId);
        Level100ActorId droneId = registry.SpawnThing(
            airfieldId,
            "Target Drone",
            "SpawnerA",
            1,
            "AirborneDrone2").Single();
        mechanics.RegisterSpawnedActor(droneId);
        mechanics.ApplyCommand(new Level100ActorScriptCommand(
            1,
            0,
            droneId,
            Level100ActorScriptCommandKind.Attack,
            playerId,
            null,
            0));

        int observed = -1;
        int launched = 0;
        int highWater = 0;
        for (int tick = 0; tick < 30 * 600 && observed < 0; tick++)
        {
            mechanics.AdvanceTick();
            Level100ActorMechanicsSnapshot snapshot = mechanics.Snapshot;
            launched = Math.Max(launched, snapshot.NextActorRoundId - 1);
            highWater = Math.Max(
                highWater,
                snapshot.ActorRounds.Count(r => r.Kind == kind));
            foreach (Level100ActorRoundImpactReceipt receipt in
                     mechanics.DrainActorRoundImpactReceipts())
            {
                Level100ActorRoundImpact impact = receipt.Impact;
                if (impact.Kind == kind)
                {
                    Assert.True(receipt.RoundId.HasValue);
                    Assert.InRange(receipt.RoundId.Value, 1, launched);
                    observed = impact.IncomingDamageMilliLife;
                }
            }
        }
        _output.WriteLine(
            $"{kind}: {launched} rounds launched overall, high-water " +
            $"{highWater} of this kind in flight");

        _output.WriteLine($"{kind} incoming milli-life: {observed}");
        Assert.Equal(expectedIncomingDamage, observed);
    }

    /// <summary>
    /// Actor rounds move on their own MOVE and life events (the RE lane's round
    /// Frames contract). A Blaster's 3 s life lands in the ring 59 buckets out,
    /// ahead of that frame's MOVE: 60 Moves. A Forseti Missile's 10 s life is
    /// 199 buckets out, past the ring, so it waits in the overflow list, which
    /// is delivered after the lanes once its time has passed: the missile takes
    /// that frame's MOVE and one more.
    /// </summary>
    [Theory]
    [InlineData(Level100ActorRoundKind.Blaster)]
    [InlineData(Level100ActorRoundKind.ForsetiMissile)]
    public void ActorRounds_MoveOnTheirOwnEventsUntilTheirLifeEnds(Level100ActorRoundKind kind)
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var registry = new Level100ActorRegistry(definitions);
        var mechanics = new Level100ActorMechanics(registry, definitions);
        Level100ActorId airfield = registry.GetThingRef("Airfield")!.Value;
        Level100ActorId player = registry.GetThingRef("Player 1")!.Value;
        // High above the level and climbing away from its target, the round
        // meets nothing, and a missile drops the target at its seek cone.
        int roundId = mechanics.QueueActorRoundForMeasurement(
            airfield, player, kind, new SimVector3(0, 400_000, 0), 0, 1_000_000);
        RetailEventSchedulerSnapshot events = mechanics.Snapshot.PlaneEvents!;
        uint launchFrame = events.FrameCount;
        int listener = Level100ActorMechanics.ActorRoundListener(roundId);
        RetailEventSlotSnapshot[] filed = events.Lanes.SelectMany(lane => lane.Handles).Concat(events.Overflow)
            .Select(handle => events.Slots.Single(slot => slot.Handle == handle))
            .Where(slot => slot.Listener == listener).ToArray();
        Assert.Equal(
            new[] { Level100ActorMechanics.RoundMoveEvent, Level100ActorMechanics.RoundLifeEvent },
            filed.Select(slot => (int)slot.EventNum).Order());
        RetailEventSlotSnapshot life = filed.Single(slot => slot.EventNum == Level100ActorMechanics.RoundLifeEvent);
        bool overflow = events.Overflow.Contains(life.Handle);
        Assert.Equal(kind == Level100ActorRoundKind.ForsetiMissile, overflow);

        int expectedMoves = 60;
        if (overflow)
        {
            float due = BitConverter.UInt32BitsToSingle(life.TimeBits);
            uint delivery = launchFrame + 1;
            while (!(due < RetailEventScheduler.TimeAtFrameCount(delivery)))
            {
                delivery++;
            }
            expectedMoves = checked((int)(delivery + 1 - launchFrame));
            Assert.InRange(expectedMoves, 201, 202);
        }

        int lastElapsed = -1;
        for (int frame = 0; frame < 400; frame++)
        {
            mechanics.AdvanceTick();
            Level100ActorRoundSnapshot? round = mechanics.Snapshot.ActorRounds.SingleOrDefault(item => item.Id == roundId);
            if (round is null)
            {
                break;
            }
            Assert.Equal(frame + 1, round.ElapsedBaseTicks);
            lastElapsed = round.ElapsedBaseTicks;
        }

        // The frame that removes the round has taken its last step.
        Assert.Equal(expectedMoves, lastElapsed + 1);
        Assert.Empty(mechanics.DrainActorRoundImpactReceipts());
    }

    /// <summary>
    /// A dying actor round's last step still meets its target but hits
    /// nothing (the RE lane's "No hit while dying"): its receipt only carries
    /// the target's script hit, and the public drain of damaging impacts stays
    /// empty. The same crossing one step earlier, while it lives, damages.
    /// </summary>
    [Theory]
    [InlineData(59, true)]
    [InlineData(58, false)]
    public void ActorRound_LastStepMeetsThePlayerWithoutDamage(int stepsBeforeTheCrossing, bool dying)
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var registry = new Level100ActorRegistry(definitions);
        var mechanics = new Level100ActorMechanics(registry, definitions);
        Level100ActorId airfield = registry.GetThingRef("Airfield")!.Value;
        Level100ActorId player = registry.GetThingRef("Player 1")!.Value;
        registry.Activate(player);
        // A level Blaster along +Z crosses the Battle Engine's cylinder centre
        // halfway through step stepsBeforeTheCrossing + 1 of its 60.
        int step = Level100ActorArmament.Round(Level100ActorRoundKind.Blaster).SpeedMillimetersPerBaseTick;
        const int Height = 100_000;
        registry.SetPose(player, new Level100ActorPoseSnapshot(
            new SimVector3(0, Height + SimulationConstants.Level100PlayerCollisionCenterBelowOriginMillimeters,
                (stepsBeforeTheCrossing * step) + (step / 2)),
            IdentityFloatBasis(), SimVector3.Zero, SimVector3.Zero));
        int roundId = mechanics.QueueActorRoundForMeasurement(
            airfield, player, Level100ActorRoundKind.Blaster, new SimVector3(0, Height, 0), 0, 0);

        var receipts = new List<Level100ActorRoundImpactReceipt>();
        int frames = 0;
        while (mechanics.Snapshot.ActorRounds.Any(item => item.Id == roundId))
        {
            mechanics.AdvanceTick();
            frames++;
            receipts.AddRange(mechanics.DrainActorRoundImpactReceipts());
            Assert.True(frames <= 60);
        }

        Assert.Equal(stepsBeforeTheCrossing + 1, frames);
        Level100ActorRoundImpactReceipt receipt = Assert.Single(receipts);
        Assert.Equal(roundId, receipt.RoundId);
        Assert.Equal(dying, receipt.ContactOnly);
    }

    private static Level100FloatBasis3Bits IdentityFloatBasis() => new(
        BitConverter.SingleToInt32Bits(1.0f), 0, 0,
        0, BitConverter.SingleToInt32Bits(1.0f), 0,
        0, 0, BitConverter.SingleToInt32Bits(1.0f));

    /// <summary>
    /// The old sphere shortcut misses this line: at closest approach it is
    /// 800 mm from the cylinder centre along the cap axis and 399 mm radially
    /// away. Retail's finite cylinder accepts it, while a 400 mm sphere around
    /// the same centre cannot.
    /// </summary>
    [Fact]
    public void ActorRoundCollision_UsesTheFiniteBattleEngineCylinder()
    {
        var battleEngine = new SimVector3(0, 0, 0);
        int cylinderCenterY = -
            SimulationConstants.Level100PlayerCollisionCenterBelowOriginMillimeters;
        var start = new SimVector3(-1_000, cylinderCenterY + 800, 399);
        var end = new SimVector3(1_000, cylinderCenterY + 800, 399);

        long oldSphereDistanceSquared =
            ((long)(cylinderCenterY + 800) * (cylinderCenterY + 800)) +
            ((long)399 * 399);
        long radius = SimulationConstants.Level100PlayerContactRadiusMillimeters;
        Assert.True(oldSphereDistanceSquared > radius * radius);
        Assert.True(Level100ActorMechanics
            .TryResolveBattleEngineCylinderContact(
                start,
                end,
                battleEngine,
                out _));
    }

    [Theory]
    [InlineData(950, 0, true)]
    [InlineData(951, 0, false)]
    [InlineData(0, 400, true)]
    [InlineData(0, 401, false)]
    public void ActorRoundCollision_PreservesInclusiveCylinderCapsAndSide(
        int axialOffset,
        int radialOffset,
        bool expected)
    {
        int cylinderCenterY = -
            SimulationConstants.Level100PlayerCollisionCenterBelowOriginMillimeters;
        var start = new SimVector3(
            -1_000,
            cylinderCenterY + axialOffset,
            radialOffset);
        var end = new SimVector3(
            1_000,
            cylinderCenterY + axialOffset,
            radialOffset);

        bool actual = Level100ActorMechanics.TryResolveBattleEngineCylinderContact(
            start,
            end,
            SimVector3.Zero,
            out _);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(-100, 575, 436, 173, true)]
    [InlineData(436, 173, -100, 575, true)]
    [InlineData(-99, 575, 437, 173, false)]
    [InlineData(437, 173, -99, 575, false)]
    public void ActorRoundCollision_PreservesExactObliqueSideTangency(
        int startX,
        int startZ,
        int endX,
        int endZ,
        bool expected)
    {
        const int cylinderCenterY = -
            SimulationConstants.Level100PlayerCollisionCenterBelowOriginMillimeters;
        var start = new SimVector3(startX, cylinderCenterY, startZ);
        var end = new SimVector3(endX, cylinderCenterY, endZ);

        bool actual = Level100ActorMechanics.TryResolveBattleEngineCylinderContact(
            start,
            end,
            SimVector3.Zero,
            out _);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(-1_000, -760, 0, 1_000, -760, 0, -400, -760, 0)]
    [InlineData(-2_000_000, -760, 0, 2_000_000, -760, 0, -400, -760, 0)]
    [InlineData(-399, -760, 0, 1_000, -760, 0, -399, -760, 0)]
    [InlineData(-1_000, -760, 400, 1_000, -760, 400, -323, -760, 400)]
    [InlineData(0, 1_240, 0, 0, -2_760, 0, 0, -360, 0)]
    [InlineData(-1_000, 440, 0, 1_000, 40, 0, 139, 212, 0)]
    [InlineData(0, -760, 0, 0, -760, 0, 0, -760, 0)]
    public void ActorRoundCollision_PreservesReleasedModeOneCandidate(
        int startX,
        int startY,
        int startZ,
        int endX,
        int endY,
        int endZ,
        int expectedX,
        int expectedY,
        int expectedZ)
    {
        var start = new SimVector3(startX, startY, startZ);
        var end = new SimVector3(endX, endY, endZ);

        Assert.True(Level100ActorMechanics.TryResolveBattleEngineCylinderContact(
            start,
            end,
            SimVector3.Zero,
            out SimVector3 selected));
        Assert.Equal(new SimVector3(expectedX, expectedY, expectedZ), selected);
    }

    /// <summary>
    /// The mathematical segment enters the cylinder side at one third, but
    /// retail mode 1 constructs both root candidates below the lower cap and
    /// rejects after its preliminary solid-cylinder overlap.
    /// </summary>
    [Fact]
    public void ActorRoundCollision_AppliesReleasedSameCapRejection()
    {
        var start = new SimVector3(-600, -1_560, 0);
        var end = new SimVector3(0, -1_760, 0);

        Assert.False(Level100ActorMechanics.TryResolveBattleEngineCylinderContact(
            start,
            end,
            SimVector3.Zero,
            out _));
    }

    [Theory]
    [InlineData(int.MaxValue, int.MinValue, int.MinValue, int.MinValue + 401)]
    [InlineData(int.MinValue, int.MaxValue, int.MaxValue, int.MaxValue - 401)]
    public void ActorRoundCollision_PreservesRepresentableFullRangeContact(
        int startX,
        int endX,
        int battleEngineX,
        int expectedX)
    {
        var start = new SimVector3(startX, -760, 0);
        var end = new SimVector3(endX, -760, 0);
        var battleEngine = new SimVector3(battleEngineX, 0, 0);

        Assert.True(Level100ActorMechanics.TryResolveBattleEngineCylinderContact(
            start,
            end,
            battleEngine,
            out SimVector3 selected));
        Assert.Equal(new SimVector3(expectedX, -760, 0), selected);
    }

    /// <summary>
    /// The state hash sees the armament. Advancing an attacking drone changes
    /// the mechanics snapshot, and the snapshot round-trips: a mechanics
    /// instance restored from it continues the identical stream and produces
    /// the identical next snapshot.
    /// </summary>
    [Fact]
    public void ActorArmament_IsCanonicalReplayState()
    {
        Level100ActorMechanicsSnapshot Advance(int ticks)
        {
            Level100ActorDefinitionSet definitions =
                Level100TestActorDefinitions.Create();
            var registry = new Level100ActorRegistry(definitions);
            var mechanics = new Level100ActorMechanics(registry, definitions);
            Level100ActorId airfieldId = Assert.IsType<Level100ActorId>(
                registry.GetThingRef("Airfield"));
            Level100ActorId playerId = Assert.IsType<Level100ActorId>(
                registry.GetThingRef("Player 1"));
            SeatPlayerOnTheTerrain(registry, playerId);
            Level100ActorId droneId = registry.SpawnThing(
                airfieldId,
                "Target Drone",
                "SpawnerA",
                1,
                "AirborneDrone2").Single();
            mechanics.RegisterSpawnedActor(droneId);
            mechanics.ApplyCommand(new Level100ActorScriptCommand(
                1,
                0,
                droneId,
                Level100ActorScriptCommandKind.Attack,
                playerId,
                null,
                0));
            for (int tick = 0; tick < ticks; tick++)
            {
                mechanics.AdvanceTick();
            }
            return mechanics.Snapshot;
        }

        Level100ActorMechanicsSnapshot idle = Advance(0);
        Assert.Empty(idle.ActorRounds);
        // Construction takes its draws before any callback runs: each row's
        // class sequence in load order (the RE lane's construction-order
        // contract), then the spawned Drone's Actor draw and CPlane::Init's
        // last draw.
        var creationRandom = new Level100ReleasedRandom();
        int draws = ConstructionDraws(Level100TestActorDefinitions.Create()) + 2;
        for (int draw = 0; draw < draws; draw++)
        {
            creationRandom.Next();
        }
        Assert.Equal(creationRandom.Seed, idle.ReleasedRandomSeed);

        Level100ActorMechanicsSnapshot flown = Advance(30 * 120);
        Level100ActorMechanicsSnapshot repeat = Advance(30 * 120);
        // Record equality would compare the two lists by reference, so this
        // compares the values the state hasher actually writes.
        Assert.Equal(flown.ReleasedRandomSeed, repeat.ReleasedRandomSeed);
        Assert.Equal(flown.NextActorRoundId, repeat.NextActorRoundId);
        Assert.Equal(flown.ActorWeapons, repeat.ActorWeapons);
        Assert.Equal(flown.ActorRounds, repeat.ActorRounds);
        Assert.NotEqual(idle.ReleasedRandomSeed, flown.ReleasedRandomSeed);
        Assert.NotEmpty(flown.ActorWeapons);
        _output.WriteLine(
            $"after 120 s: seed {flown.ReleasedRandomSeed}, " +
            $"{flown.ActorRounds.Count} rounds in flight, " +
            $"next round id {flown.NextActorRoundId}");
    }

    /// <summary>
    /// The load's draws for a definition set: one per pine, then per row
    /// Building 1 (Actor), Cannon 2 (Actor, fire control), Feature 1,
    /// SimpleBuilding 1, Battle Engine 1 (its 6002/6003 belong to the
    /// Simulation), squad-wrapped ground vehicle 5 (Actor, hover, three squad
    /// refreshes), Dropship 1 and Plane 2 (Actor, <c>+0x284</c>).
    /// </summary>
    internal static int ConstructionDraws(Level100ActorDefinitionSet definitions) =>
        definitions.BaseWorldPineCount + RowDraws(definitions.Actors) +
        (definitions.BaseWorldPineCount > 0 ? BaseWorldPassExtraDraws : 0);

    /// <summary>
    /// With the base world's pass: the influence map's two draws (after the
    /// pines and at the load's tail) and the Target Truck and Target Drone
    /// warm-ups, two draws each.
    /// </summary>
    internal const int BaseWorldPassExtraDraws = 6;

    /// <summary>The construction draws of the given rows, by class.</summary>
    internal static int RowDraws(IEnumerable<Level100ActorDefinition> rows) =>
        rows.Sum(definition => Level100ConstructionClasses.Of(definition.DefinitionName) switch
        {
            Level100ConstructionClass.Trigger => 0,
            Level100ConstructionClass.Cannon or Level100ConstructionClass.Plane => 2,
            Level100ConstructionClass.SquadGroundVehicle => 5,
            _ => 1,
        });

    /// <summary>
    /// <c>CRoundSeek 3</c> is a homing round with a launcher-supplied target.
    /// Driven here through the released <c>Forseti Drone Missile Launcher</c>
    /// against a target that is moved every tick, so a straight-flying round
    /// wearing the missile's name could not produce this result.
    /// </summary>
    [Fact]
    public void ForsetiMissile_HomesOnAMovingTarget()
    {
        Level100ActorDefinitionSet definitions =
            Level100TestActorDefinitions.Create();
        var registry = new Level100ActorRegistry(definitions);
        var mechanics = new Level100ActorMechanics(registry, definitions);
        Level100ActorId airfieldId = Assert.IsType<Level100ActorId>(
            registry.GetThingRef("Airfield"));
        Level100ActorId playerId = Assert.IsType<Level100ActorId>(
            registry.GetThingRef("Player 1"));
        SeatPlayerOnTheTerrain(registry, playerId);
        Level100ActorId droneId = registry.SpawnThing(
            airfieldId,
            "Target Drone",
            "SpawnerA",
            1,
            "AirborneDrone2").Single();
        mechanics.RegisterSpawnedActor(droneId);
        mechanics.ApplyCommand(new Level100ActorScriptCommand(
            1,
            0,
            droneId,
            Level100ActorScriptCommandKind.Attack,
            playerId,
            null,
            0));

        int missileImpacts = 0;
        int sidestep = 0;
        for (int tick = 0; tick < 30 * 600; tick++)
        {
            // Move the target laterally every base tick. A round with no
            // guidance law cannot follow this.
            Level100ActorPoseSnapshot pose = registry.GetPose(playerId);
            sidestep = (sidestep + 1) % 240;
            registry.SetPose(
                playerId,
                pose with
                {
                    PositionMillimeters = new SimVector3(
                        pose.PositionMillimeters.X + (sidestep < 120 ? 40 : -40),
                        pose.PositionMillimeters.Y,
                        pose.PositionMillimeters.Z),
                });
            mechanics.AdvanceTick();
            missileImpacts += mechanics.DrainActorRoundImpacts()
                .Count(impact =>
                    impact.Kind == Level100ActorRoundKind.ForsetiMissile);
        }

        _output.WriteLine($"Forseti Missile impacts on a moving target: {missileImpacts}");
        Assert.True(
            missileImpacts > 0,
            "CRoundSeek 3 must home; a straight round would be a different " +
            "weapon wearing the missile's name.");
    }

    /// <summary>
    /// The bare registry fixture leaves <c>Player 1</c> at its authored
    /// Y = 0, which is below the height field. <see cref="Simulation"/> does
    /// not: it seats the walker on the terrain and the flight-leg measurements
    /// record a standing origin 1,900 mm above ground. Rounds aimed at a
    /// target buried under the terrain would be spent on the terrain before
    /// reaching it, which is a fixture artefact and not a weapon result.
    /// </summary>
    private static void SeatPlayerOnTheTerrain(
        Level100ActorRegistry registry,
        Level100ActorId playerId)
    {
        Level100ActorPoseSnapshot pose = registry.GetPose(playerId);
        int ground = Level100Terrain.Instance.SampleGroundElevationMillimeters(
            new SimVector2(
                pose.PositionMillimeters.X,
                pose.PositionMillimeters.Z));
        registry.SetPose(
            playerId,
            pose with
            {
                PositionMillimeters = new SimVector3(
                    pose.PositionMillimeters.X,
                    ground + WalkerStandingOriginMillimeters,
                    pose.PositionMillimeters.Z),
            });
    }

    private const int WalkerStandingOriginMillimeters = 1_900;
}
