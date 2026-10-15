// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Task #114. Retail's Level 100 authors two ambient aircraft into the level
/// world - a U-17 Highside Transporter on script <c>Transporter</c> at RLWD
/// ordinal 21 and an Air Trainer on script <c>Flyby</c> at ordinal 40 - and
/// both were constructed and scripted by Core while being invisible, because
/// <see cref="WorldSnapshot.Targets"/> admitted only two mission target groups.
///
/// <para>These tests pin the two halves of the fix that can be checked without
/// an engine: the render projection now carries them, and carrying them does
/// NOT make them targetable. Every assertion runs against the hash-pinned
/// materialized manifest rather than a hand-built fixture, so a manifest that
/// stops shipping the aircraft fails here.</para>
/// </summary>
public sealed class Level100AmbientAircraftTests
{
    private const long OneCoreStepTicks = 500_000;
    private const uint Seed = 0x4F4E534Cu;

    /// <summary>
    /// The ambient U-17 Highside Transporter, MEASURED from the shipped level
    /// world: RLWD initial-actor ordinal 21, thingType 8, authored retail
    /// position (218.5, 297.5, -15.0) which is Core millimetres
    /// (-70188, +5000, 54250).
    /// </summary>
    /// <remarks>
    /// The vertical was -15000 until 2026-08-01, when task #154 corrected the
    /// producer to convert the authored retail Z into Core's datum instead of
    /// writing the down-positive number into an up-positive field:
    /// <c>(-10.0 - (-15.0)) * 1000 = +5000</c>. THIS IS THE PAYOFF OF #114 -
    /// the aircraft was being drawn 15,997 mm under the terrain at its own pad,
    /// which is the whole reason the second ambient aircraft was never seen.
    /// </remarks>
    private static readonly SimVector3 s_authoredTransporterPosition =
        new(-70_188, 5_000, 54_250);

    /// <summary>
    /// The ambient Air Trainer, RLWD ordinal 40, authored retail position
    /// (265.5, 392.5, -15.0) = Core (-23188, +5000, 149250). Same datum
    /// correction as above.
    /// </summary>
    private static readonly SimVector3 s_authoredAirTrainerPosition =
        new(-23_188, 5_000, 149_250);

    /// <summary>
    /// The render projection carries EXACTLY the two ambient aircraft out of
    /// the <see cref="Level100MissionTargetGroup.None"/> band - not the 33
    /// base-world structures, not the five General Volume trigger spheres, and
    /// not the player.
    ///
    /// <para>This is the assertion that makes the widening safe. MEASURED from
    /// the pinned manifest: 41 of the 44 authored actor definitions carry group
    /// <c>None</c> and 36 of those carry a non-null mesh binding, so a
    /// "has a mesh binding" predicate would admit 36 actors, 34 of which have
    /// no target mesh registered and would each hit
    /// <c>FirstFlightWorldView.AddLevel100Target</c>'s throw. Only three of the
    /// 36 are non-static, and one of those is the player.</para>
    /// </summary>
    [Fact]
    public void TheRenderProjectionCarriesExactlyTheTwoAmbientAircraftFromTheNoGroupBand()
    {
        Level100ActorDefinitionSet definitions = LoadMaterializedActorDefinitions();

        // The premise, re-derived from the manifest rather than assumed. If any
        // of these three counts move, the narrow predicate is no longer the
        // right one and the assertions below would be passing for the wrong
        // reason.
        Assert.Equal(44, definitions.Actors.Count);
        Assert.Equal(
            41,
            definitions.Actors.Count(actor =>
                actor.TargetGroup == Level100MissionTargetGroup.None));
        Assert.Equal(
            36,
            definitions.Actors.Count(actor =>
                actor.TargetGroup == Level100MissionTargetGroup.None &&
                actor.MeshBinding is not null));

        var session = new InteractiveSession(Seed, definitions);
        WorldSnapshot snapshot = session.CurrentSnapshot;

        Dictionary<int, Level100ActorSnapshot> actorsById =
            snapshot.Level100Actors.Actors.ToDictionary(
                actor => actor.ActorId.Value);

        TargetSnapshot[] ungrouped = snapshot.Targets
            .Where(target =>
                actorsById[target.ActorId.Value].TargetGroup ==
                Level100MissionTargetGroup.None)
            .ToArray();

        Assert.Equal(2, ungrouped.Length);

        TargetSnapshot transporter = Assert.Single(
            ungrouped,
            target => target.DefinitionName == "U-17 Highside Transporter");
        TargetSnapshot airTrainer = Assert.Single(
            ungrouped,
            target => target.DefinitionName == "Air Trainer");

        Assert.Equal(
            Level100TargetPresentation.TransporterBinding,
            Level100TargetPresentation.Project(transporter).Binding);
        Assert.Equal(
            Level100TargetPresentation.AirTrainerBinding,
            Level100TargetPresentation.Project(airTrainer).Binding);

        // Seated on their measured authored poses at the load. Since
        // 2026-08-01 those poses are in Core's own vertical datum (task #154),
        // and the released CThing::Init support clamp that
        // Level100ActorRegistry now applies to every class LEAVES BOTH OF THEM
        // WHERE THEY ARE: +5000 is above the terrain sample at each aircraft's
        // own X/Z (+997 under the Transporter, -11160 under the Air Trainer)
        // and above the -1160 water plane, so max(authored, terrain, water) is
        // the authored value. That is the measured behaviour of the released
        // CDropship too - it takes both clamps and neither moves it.
        Level100ActorRegistrySnapshot load = new Level100ActorRegistry(definitions).Snapshot;
        Assert.Equal(
            s_authoredTransporterPosition,
            load.Actors.Single(actor => actor.DefinitionName == "U-17 Highside Transporter").Pose.PositionMillimeters);
        Assert.Equal(
            s_authoredAirTrainerPosition,
            load.Actors.Single(actor => actor.DefinitionName == "Air Trainer").Pose.PositionMillimeters);

        // The first tick follows the three-second pre-run, in which the Air
        // Trainer is already flying; both are drawn exactly as Core reports them.
        Assert.Equal(
            actorsById[transporter.ActorId.Value].Pose.PositionMillimeters,
            transporter.Pose.PositionMillimeters);
        Assert.Equal(
            actorsById[airTrainer.ActorId.Value].Pose.PositionMillimeters,
            airTrainer.Pose.PositionMillimeters);
        Assert.True(transporter.IsActive);
        Assert.True(airTrainer.IsActive);

        // The ungrouped band is 100 + canonical actor id, which cannot collide
        // with the grouped bands 1..23 that the firing-range logic reads.
        Assert.Equal(100 + transporter.ActorId.Value, transporter.Id);
        Assert.Equal(100 + airTrainer.ActorId.Value, airTrainer.Id);
        Assert.All(snapshot.Targets, target => Assert.True(
            target.Id >= 1,
            $"presentation id {target.Id} is outside every authored band"));
        Assert.Equal(
            snapshot.Targets.Count,
            snapshot.Targets.Select(target => target.Id).Distinct().Count());

        // The three things the narrow None arm exists to keep OUT.
        Assert.DoesNotContain(
            snapshot.Targets,
            target => target.DefinitionName == "BattleEngine");
        Assert.DoesNotContain(
            snapshot.Targets,
            target => actorsById[target.ActorId.Value].Trigger.HasValue);
        Assert.DoesNotContain(
            snapshot.Targets,
            target => actorsById[target.ActorId.Value].IsStatic);
    }

    /// <summary>
    /// Reaching the RENDERER does not make an actor TARGETABLE. Retail already
    /// draws that line in the shipped data: both ambient aircraft are authored
    /// with mission target group <c>None</c> and ordinal 0, so they are not
    /// members of any of the six mission target groups the tutorial's firing,
    /// objective and completion logic reads.
    ///
    /// <para>The scanner assertion is the positive control. Retail DOES show
    /// both aircraft as contacts - <c>CHud__RenderTacticalRadarContacts</c>
    /// walks the global unit list, not a target group - so a test that found
    /// them nowhere in the HUD would be proving the wrong thing.</para>
    /// </summary>
    [Fact]
    public void TheAmbientAircraftReachTheRendererWithoutBecomingTargetable()
    {
        var session = new InteractiveSession(
            Seed,
            LoadMaterializedActorDefinitions());
        WorldSnapshot snapshot = session.CurrentSnapshot;

        Level100ActorSnapshot[] ambient = snapshot.Level100Actors.Actors
            .Where(actor =>
                actor.DefinitionName is
                    "U-17 Highside Transporter" or "Air Trainer")
            .ToArray();
        Assert.Equal(2, ambient.Length);

        foreach (Level100ActorSnapshot actor in ambient)
        {
            Assert.Equal(Level100MissionTargetGroup.None, actor.TargetGroup);
            Assert.Equal(0, actor.TargetOrdinal);
            Assert.False(actor.IsObjective);
            // The Trainer's positive profile life does not make it a mission target.
            Assert.Equal(actor.DefinitionName == "Air Trainer" ? 3_000 : 0, actor.Health);
        }

        int[] ambientIds = ambient
            .Select(actor => actor.ActorId.Value)
            .ToArray();

        // Not a firing-range target. Level100FiringRangeTargetsActive is the
        // one piece of mission logic that reads this projection at all, and it
        // tests ids 1..4.
        Assert.DoesNotContain(
            snapshot.Targets,
            target => ambientIds.Contains(target.ActorId.Value) &&
                target.Id is >= 1 and <= 4);
        Assert.False(snapshot.Level100FiringRangeTargetsActive);

        // Not a HUD objective.
        var presentation = new Level100HudPresentationState();
        Level100HudSnapshot hud = presentation.Project(
            snapshot,
            new Level100MessagePlaybackState(null, null, 0d, 0d, false, false));
        Assert.DoesNotContain(
            hud.Objectives,
            objective => ambientIds.Contains(objective.ActorId.Value));

        // ...but they ARE scanner contacts, which is what retail draws.
        Assert.Equal(
            2,
            hud.Contacts.Count(contact =>
                ambientIds.Contains(contact.Id)));
    }

    /// <summary>
    /// The Air Trainer and the Transporter both fly: the Air Trainer its
    /// authored route as a <c>Plane</c>, the U-17 its "Transporter Path" as a
    /// <c>CDropship</c> (<c>Level100ActorDropshipRuntime</c>). The renderer
    /// draws what Core reports, so this test states the expected on-screen
    /// movement rather than leaving it to a screenshot.
    /// </summary>
    [Fact]
    public void TheAirTrainerAndTheTransporterBothFly()
    {
        var session = new InteractiveSession(
            Seed,
            LoadMaterializedActorDefinitions());
        for (int tick = 0; tick < 600; tick++)
        {
            session.AdvanceFrameTicks(OneCoreStepTicks);
        }

        TargetSnapshot transporter = Assert.Single(
            session.CurrentSnapshot.Targets,
            target => target.DefinitionName == "U-17 Highside Transporter");
        TargetSnapshot airTrainer = Assert.Single(
            session.CurrentSnapshot.Targets,
            target => target.DefinitionName == "Air Trainer" &&
                target.Id >= 100);

        Assert.NotEqual(
            s_authoredTransporterPosition,
            transporter.Pose.PositionMillimeters);
        Assert.NotEqual(
            s_authoredAirTrainerPosition,
            airTrainer.Pose.PositionMillimeters);
    }

    /// <summary>
    /// Every world actor the projection can admit has a registered visual
    /// binding.
    ///
    /// <para>This is the exact guard for the crash class:
    /// <c>FirstFlightWorldView.AddLevel100Target</c> THROWS on an unregistered
    /// binding rather than skipping it, so one missing entry kills the client on
    /// the first frame that admits the actor. The eligible set is derived from
    /// the pinned manifest through the same rule the Core projection applies,
    /// and compared for set equality in both directions - a missing binding and
    /// a dead one both fail.</para>
    /// </summary>
    [Fact]
    public void EveryRenderableWorldActorHasARegisteredVisualBinding()
    {
        Level100ActorDefinitionSet definitions = LoadMaterializedActorDefinitions();

        HashSet<Level100TargetVisualBinding> eligible = definitions.Actors
            .Where(actor =>
                actor.MeshBinding is not null &&
                actor.DefinitionName is not null &&
                IsRenderedGroup(
                    actor.TargetGroup,
                    actor.IsStatic,
                    actor.Name))
            .Select(actor => new Level100TargetVisualBinding(
                actor.DefinitionName!,
                actor.MeshBinding!))
            .Concat(definitions.Spawns
                .Where(spawn =>
                    spawn.MeshBinding is not null &&
                    IsRenderedGroup(
                        spawn.TargetGroup,
                        isStatic: false,
                        name: spawn.DefinitionName))
                .Select(spawn => new Level100TargetVisualBinding(
                    spawn.DefinitionName,
                    spawn.MeshBinding!)))
            .ToHashSet();

        Assert.Equal(
            eligible,
            Level100TargetPresentation.RenderedBindings.ToHashSet());
    }

    /// <summary>
    /// The player carries a mesh binding and is never a rendered target: the
    /// Aquila walker/jet asset draws it, and admitting it here would draw it
    /// twice.
    /// </summary>
    [Fact]
    public void ThePlayerIsNotARenderedTarget()
    {
        var session = new InteractiveSession(
            Seed,
            LoadMaterializedActorDefinitions());
        WorldSnapshot snapshot = session.CurrentSnapshot;

        Level100ActorSnapshot player = Assert.Single(
            snapshot.Level100Actors.Actors,
            actor => actor.Name == "Player 1");
        Assert.Equal("m_f_be1.msh.aya", player.MeshBinding);
        Assert.False(player.IsStatic);
        Assert.Equal(Level100MissionTargetGroup.None, player.TargetGroup);

        Assert.DoesNotContain(
            snapshot.Targets,
            target => target.ActorId.Value == player.ActorId.Value);
        Assert.DoesNotContain(
            Level100TargetPresentation.RenderedBindings,
            binding => binding.MeshBinding == "m_f_be1.msh.aya");
    }

    /// <summary>
    /// The rule <see cref="WorldSnapshot.Targets"/> applies, restated over the
    /// manifest so the eligible-binding derivation is an independent path
    /// rather than a call into the code under test.
    /// </summary>
    private static bool IsRenderedGroup(
        Level100MissionTargetGroup group,
        bool isStatic,
        string name) => group switch
        {
            Level100MissionTargetGroup.StaticTargets or
            Level100MissionTargetGroup.TargetTrucks or
            Level100MissionTargetGroup.MovingTargets or
            Level100MissionTargetGroup.AirborneTargets1 or
            Level100MissionTargetGroup.AirborneTargets2 or
            Level100MissionTargetGroup.AirTrainer => true,
            Level100MissionTargetGroup.None =>
                !isStatic && !StringComparer.Ordinal.Equals(name, "Player 1"),
            _ => false,
        };

    private static Level100ActorDefinitionSet LoadMaterializedActorDefinitions()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Level100",
            "StaticWorld",
            "level100-static-world.json");
        return Level100ActorDefinitionManifest.Decode(File.ReadAllBytes(path));
    }
}
