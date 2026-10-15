// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.TestSupport;

internal static class Level100TestActorDefinitions
{
    // Artificial envelope for testing older hash formats in isolation. This
    // deliberately removes the optional aircraft extension; it is not an
    // alternate playable world or an admitted registry-restore snapshot.
    internal static WorldSnapshot LegacyHashEnvelope(WorldSnapshot state) => state with
    {
        Level100Actors = state.Level100Actors with
        {
            BaseStates = state.Level100Actors.BaseStates.Select(item => item.State.RetailPlane is null ? item : item with
            {
                State = item.State with
                {
                    RetailPlane = null, RetailPoses = null, RetailMotion = null,
                    Flags = item.State.Flags & ~ThingActorFlags.InMapWho,
                },
            }).ToArray(),
        },
        Level100ActorMechanics = state.Level100ActorMechanics with
        {
            PlaneEvents = null,
            // The unit callbacks' state is the schema-52 extension; their
            // events went with the event pool.
            UnitCallbacks = null,
            Actors = state.Level100ActorMechanics.Actors.Select(actor => actor with
                { PlaneGuide = null, PlaneSpawnerExit = null }).ToArray(),
        },
        // The Battle Engine's refresh events went with the event pool above;
        // its targeting state is the schema-49 extension.
        Level100BattleEngineTargeting = Level100BattleEngineTargetingSnapshot.Initial,
        Level100PlayerStores = Level100PlayerStoresSnapshot.Initial,
        Level100BattleEngineShake = Level100BattleEngineShakeSnapshot.Initial,
    };

    private static readonly Lazy<Level100ActorDefinitionSet> s_materialized = new(LoadMaterialized);

    internal static Level100ActorDefinitionSet LoadMaterialized() =>
        Level100ActorDefinitionManifest.Decode(File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory, "Assets", "Level100", "StaticWorld",
            "level100-static-world.json")));

    internal static Level100ActorDefinitionSet LoadMaterializedWorld110() =>
        Level100ActorDefinitionManifest.DecodeWorld110(File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory, "Assets", "Level110", "level110-static-world.json")));

    internal static Level100ActorDefinitionSet Create()
    {
        var actors = new List<Level100ActorDefinition>();

        void Add(
            string identity,
            string name,
            string? definition,
            string? script,
            string? mesh,
            Level100ActorPoseSnapshot pose,
            Level100MissionTargetGroup group = Level100MissionTargetGroup.None,
            int ordinal = 0,
            Level100MissionTrigger? trigger = null,
            int health = 0,
            bool active = true,
            bool isStatic = true,
            uint thingTypeMask = 0)
        {
            // These creation inputs are now consumed as raw retail state.
            // Keep fixture identity/script policy, but do not invent zero
            // transforms for the authored air units or the Plane's immutable
            // spawn owner.
            Level100ActorDefinition? released = name is "Airfield" or "Air Trainer" or "Transporter"
                ? s_materialized.Value.Actors.Single(item => item.Name == name)
                : null;
            actors.Add(new Level100ActorDefinition(
                actors.Count,
                identity,
                name,
                definition,
                script,
                mesh,
                thingTypeMask,
                isStatic,
                active,
                released?.InitialHealth ?? health,
                released?.AuthoredTransform ?? AuthoredTransform(),
                released?.InitialPose ?? pose,
                group,
                ordinal,
                trigger));
        }

        Add("test:control-tower", "Control Tower", "Control Tower", "Facilities", "fb_control_tower", Pose(-13_290, -760, 5_603));
        Add("test:tank-factory", "Tank Factory", "Forseti Pulse Tank Factory", "TankFactory", "fb_tank_factory", Pose(10_125, 0, 22_375, 1_789_434));
        Add("test:health-pad", "Health Pad", "Forseti Repair Pad", "Facilities", "fb_health_pad", Pose(-58_438, 0, 10_500));
        Add("test:turret-03", "Turret 03", "SAT Turret", "Turret", "ft_sam", Pose(-36_188, 0, 18_000));
        Add("test:turret-01", "Turret 01", "Blaster Turret", "Turret", "ft_blaster", Pose(-49_188, 0, 23_250));
        Add("test:turret-02", "Turret 02", "Blaster Turret", "Turret", "ft_blaster", Pose(-17_313, 0, -3_250, 3_141_593));
        Add("test:turret-04", "Turret 04", "Pulse Turret", "Turret", "ft_pulse", Pose(-63_188, 0, 38_750));
        Add("test:research", "Forseti Research Building 1", "Forseti Research Building", "Facilities", "fb_research", Pose(-36_557, -760, -628, 2_470_636));
        Add("test:radar", "Radar Station", "Forseti Radar Station", "Facilities", "FB_radar_station", Pose(-102_563, 0, -23_500, -1_570_796));
        Add("test:airfield", "Airfield", "Forseti Light Fighter Airfield", "Hangar", "fb_aircraft_factory", Pose(47_188, 0, 15_125, 2_094_395));
        Add("test:hangar", "Hangar", "Hangar", null, "fb_hangar", Pose(26_636, 0, 41_727, -2_443_461));

        AddTrigger(Level100MissionTrigger.TargetZone1, "Target Zone 1", "TargetZone1");
        AddTrigger(Level100MissionTrigger.FiringRange, "Firing Range", "FiringRange");
        AddTrigger(Level100MissionTrigger.TargetZone2, "Target Zone 2", "TargetZone2");
        AddTrigger(Level100MissionTrigger.TargetZone3, "Target Zone 3", "TargetZone3");
        AddTrigger(Level100MissionTrigger.TargetZone4, "Target Zone 4", "TargetZone4");

        AddTarget(2, "Target Tank 2", "Target Tank", "StaticTarget", "m_f_pulsetank_training.msh.aya", SimulationConstants.Level100TargetTank2Position, -2_153_579, SimulationConstants.Level100TargetTankLife, true);
        AddTarget(3, "Target Tank 3", "Target Tank", "StaticTarget", "m_f_pulsetank_training.msh.aya", SimulationConstants.Level100TargetTank3Position, 2_404_331, SimulationConstants.Level100TargetTankLife, true);
        AddTarget(4, "Target Warehouse", "Warehouse", "StaticTarget", "m_m_warehouse.msh.aya", SimulationConstants.Level100TargetWarehousePosition, -1_970_861, SimulationConstants.Level100TargetWarehouseLife, true);
        Add("test:player", "Player 1", "Battle Engine", null, "m_f_be1.msh.aya", Pose(0, 0, 0), health: SimulationConstants.MaximumHull, isStatic: false, thingTypeMask: Level100ReleasedThingTypeMasks.BattleEngine);
        Add("test:transporter", "Transporter", "U-17 Highside Transporter", null, "m_f_lifter.msh.aya", Pose(0, 0, 0), isStatic: false);
        Add("test:air-trainer", "Air Trainer", "Air Trainer", null, "m_FA_F24_training.msh.aya", Pose(0, 0, 0), isStatic: false);

        var spawns = new List<Level100SpawnDefinition>();
        AddSpawn("test:tank-factory", "Target Tank", "SpawnerA", "TargetTank1", "m_f_pulsetank_training.msh.aya", Level100MissionTargetGroup.StaticTargets, 1, 4);
        AddSpawn("test:tank-factory", "Target Truck", "SpawnerA", "TargetTruck1", "m_f_truck_training.msh.aya", Level100MissionTargetGroup.TargetTrucks, 1, 3);
        AddSpawn("test:tank-factory", "Target Truck", "SpawnerA", "TargetTruck2", "m_f_truck_training.msh.aya", Level100MissionTargetGroup.TargetTrucks, 2, 3);
        AddSpawn("test:tank-factory", "Target Truck", "SpawnerA", "TargetTruck3", "m_f_truck_training.msh.aya", Level100MissionTargetGroup.TargetTrucks, 3, 3);
        AddSpawn("test:tank-factory", "Target Tank", "SpawnerA", "TargetTank2", "m_f_pulsetank_training.msh.aya", Level100MissionTargetGroup.MovingTargets, 0, 6);
        AddSpawn("test:tank-factory", "Target Truck", "SpawnerA", "TargetTank2", "m_f_truck_training.msh.aya", Level100MissionTargetGroup.MovingTargets, 0, 6);
        AddSpawn("test:airfield", "Air Trainer", "SpawnerB", "AirTrainer", "m_FA_F24_training.msh.aya", Level100MissionTargetGroup.AirTrainer, 1, 1);
        AddSpawn("test:airfield", "Target Drone", "SpawnerB", "AirborneDrone1", "m_FA_F24_training.msh.aya", Level100MissionTargetGroup.AirborneTargets1, 0, 3);
        AddSpawn("test:airfield", "Target Drone", "SpawnerA", "AirborneDrone2", "m_FA_F24_training.msh.aya", Level100MissionTargetGroup.AirborneTargets2, 0, 6);
        AddSpawn("test:airfield", "Target Drone", "SpawnerB", "AirborneDrone2", "m_FA_F24_training.msh.aya", Level100MissionTargetGroup.AirborneTargets2, 0, 6);
        return new Level100ActorDefinitionSet(
            actors,
            spawns,
            WaypointPaths(),
            MotionDefinitions(),
            safeSides: s_materialized.Value.SafeSides);

        void AddTrigger(Level100MissionTrigger trigger, string name, string script)
        {
            SimVector2 position = Level100MissionTiming.TriggerPosition(trigger);
            Add(
                $"test:trigger:{trigger}",
                name,
                "General Volume",
                script,
                null,
                Pose(position.X, 0, position.Z),
                trigger: trigger,
                active: false);
        }

        void AddTarget(
            int ordinal,
            string name,
            string definition,
            string script,
            string mesh,
            SimVector2 position,
            int yaw,
            int health,
            bool isStatic) => Add(
                $"test:target-{ordinal}",
                name,
                definition,
                script,
                mesh,
                Pose(position.X, 0, position.Z, yaw),
                Level100MissionTargetGroup.StaticTargets,
                ordinal,
                health: health,
                isStatic: isStatic);

        void AddSpawn(
            string ownerIdentity,
            string definition,
            string spawner,
            string script,
            string? mesh,
            Level100MissionTargetGroup group,
            int fixedOrdinal,
            int maximum)
        {
            Level100SpawnDefinition? released = ownerIdentity == "test:airfield"
                ? s_materialized.Value.Spawns.Single(item =>
                    item.DefinitionName == definition && item.SpawnerName == spawner && item.ScriptName == script)
                : null;
            spawns.Add(new Level100SpawnDefinition(
                spawns.Count,
                $"test:spawn:{ownerIdentity}:{definition}:{spawner}:{script}",
                ownerIdentity,
                definition,
                spawner,
                script,
                mesh,
                0,
                true,
                released?.InitialHealth ?? (definition switch
                {
                    "Target Tank" => SimulationConstants.Level100TargetTankLife,
                    "Target Truck" => SimulationConstants.Level100TrainingTruckLife,
                    _ => 0,
                }),
                released?.InitialPose ?? TankFactorySpawnerPose(),
                released?.AuthoredEmitterTransform ?? new Level100SpawnerTransform(
                        new Level100FloatVector3Bits(
                            1_042_393_533,
                            1_088_031_702,
                            -1_107_199_288),
                        IdentityBasis()),
                group,
                fixedOrdinal,
                maximum,
                released?.SpawnerExitWaypoints));
        }
    }

    private static Level100ActorPoseSnapshot Pose(int x, int y, int z, int yaw = 0) => new(
        new SimVector3(x, y, z),
        IdentityBasis(),
        SimVector3.Zero,
        SimVector3.Zero);

    private static Level100AuthoredTransform AuthoredTransform() => new(
        new Level100FloatVector3Bits(0, 0, 0),
        new Level100FloatVector3Bits(0, 0, 0),
        IdentityBasis());

    private static Level100ActorPoseSnapshot TankFactorySpawnerPose() => new(
        new SimVector3(3_439, -126, 21_051),
        new Level100FloatBasis3Bits(
            -1_101_128_975, 0, -1_082_529_832,
            0, 1_065_353_216, 0,
            1_064_953_816, 0, -1_101_128_975),
        SimVector3.Zero,
        SimVector3.Zero);

    private static Level100FloatBasis3Bits IdentityBasis() => new(
        FloatBits(1f), 0, 0,
        0, FloatBits(1f), 0,
        0, 0, FloatBits(1f));

    private static int FloatBits(float value) => BitConverter.SingleToInt32Bits(value);

    // Every route below is the released one, transcribed point for point from
    // the materialized manifest `level100-static-world.json` (`waypointPaths`),
    // which the Client tests load directly.
    //
    // They used to be synthetic single points 500-620 m out at exactly 45
    // degrees, with a comment claiming that kept Core-only tests independent of
    // retail route data. It did not: it made whole released beats unreachable
    // in Core, because every actor that follows one of these paths drives off
    // the authored map and parks in deep water where no walker can follow it.
    // Measured before this correction, with beat 3 completing for the first
    // time: the three beat-4 Target Trucks stopped at (197322, -11060, 213103),
    // (187162, -11060, 203960) and (176998, -11060, 194815) - 170-197 m from
    // the player and 11 m below the water plane. Beat 4 could not complete, and
    // beats 5, 6, 8 and 10 are behind it. This is the same class of fixture
    // defect that `Target Tank Path 1` carried until it was corrected on
    // 2026-07-26, and nothing pins the synthetic values.
    //
    // Every field is the manifest's, including the retail `nodeIndex` and the
    // retail `retailComponentsFloatBits`. The previous corrections renumbered
    // nodes 0..N-1 and rebuilt the float bits from the rounded millimetres,
    // which produces *different bits* - harmless today because nothing in the
    // movement path reads either field, and wrong tomorrow because both are
    // part of the hashed definition set.
    //
    // RE-TRANSCRIBED 2026-07-27 from schema v14, after `58d9ce57` found the
    // materializer had been resolving these node indices against the 121-entry
    // navigation graph instead of the 30 RLWD thingType-18 marker records. The
    // values below were the WRONG TABLE's coordinates: 30 nodes collapsed onto
    // 11 distinct positions with 19 aliased pairs, and every altitude read
    // +10000. The corrected table has 30 distinct positions, no aliasing, and
    // splits the altitudes {0: 26, -15000: 2, -20000: 2} - the two non-zero
    // pairs landing exactly on the two ambient AIRCRAFT routes, which is the
    // corroboration that this is the right table rather than another plausible
    // one.
    //
    // `Transporter Path` no longer repeats its leading node. That "released
    // duplicate" was the aliasing artefact: nodes 44 and 22 are 116.26 m apart
    // horizontally and 117.97 m apart in three dimensions, and node 44 is the
    // one that also carried the +178.7 m error the correction commit measured.
    // `TransporterArrival_UsesTheStrictReleasedClassRadius` used to assert the
    // duplicate AS RETAIL TRUTH and no longer does.
    //
    // `Level100WaypointFixtureTests` cross-checks every row below against the
    // materialized manifest, which is what would have caught this drift the day
    // it appeared.
    // The second argument of each row is the manifest's
    // `targetChainNodeIndices` and the third its `isClosed`; the points are in
    // the manifest's (file) order. ExactPath turns them into retail's list and
    // each node's own target (waypoint-paths.md).
    private static IReadOnlyList<Level100WaypointPathDefinition> WaypointPaths() =>
    [
        ExactPath("Flyby Path", [41, 42, 43], false,
            (43, 34_813, 0, -99_750, 1_134_673_920, 1_125_089_280, -2_147_483_648, 0),
            (42, -24_313, -15_000, 47_125, 1_132_736_512, 1_133_588_480, -1_049_624_576, 0),
            (41, 82_813, -15_000, 49_500, 1_136_246_784, 1_133_666_304, -1_049_624_576, 0)),
        ExactPath("Target Truck Path 3", [33, 34, 35, 36], false,
            (36, -45_938, 0, 85_500, 1_131_593_728, 1_134_845_952, -2_147_483_648, 0),
            (35, -65_938, 0, 76_000, 1_130_283_008, 1_134_534_656, -2_147_483_648, 0),
            (34, -68_688, 0, 53_000, 1_130_102_784, 1_133_780_992, -2_147_483_648, 0),
            (33, -23_438, 0, 20_500, 1_132_765_184, 1_132_716_032, -2_147_483_648, 0)),
        ExactPath("Target Truck Path 2", [29, 30, 31, 32], false,
            (32, -51_438, 0, 93_250, 1_131_233_280, 1_135_099_904, -2_147_483_648, 0),
            (31, -64_688, 0, 74_500, 1_130_364_928, 1_134_485_504, -2_147_483_648, 0),
            (30, -69_188, 0, 52_750, 1_130_070_016, 1_133_772_800, -2_147_483_648, 0),
            (29, -34_938, 0, 29_000, 1_132_314_624, 1_132_994_560, -2_147_483_648, 0)),
        // One of the two paths whose serialized order and chain agree.
        ExactPath("Target Truck Path 1", [25, 26, 27, 28], false,
            (25, -39_938, 0, 31_750, 1_131_986_944, 1_133_084_672, -2_147_483_648, 0),
            (26, -66_938, 0, 53_500, 1_130_217_472, 1_133_797_376, -2_147_483_648, 0),
            (27, -63_438, 0, 75_250, 1_130_446_848, 1_134_510_080, -2_147_483_648, 0),
            (28, -41_688, 0, 96_250, 1_131_872_256, 1_135_198_208, -2_147_483_648, 0)),
        ExactPath("Transporter Path", [22, 23, 44], false,
            (44, 68_313, 0, 28_750, 1_135_771_648, 1_132_986_368, 0, 0),
            (22, -47_688, -20_000, 36_500, 1_131_479_040, 1_133_240_320, -1_046_478_848, 0),
            (23, -20_188, -20_000, 23_250, 1_132_871_680, 1_132_806_144, -1_046_478_848, 0)),
        // One of the two chains that closes on its own head - and note the
        // chain is not a rotation of the serialized list, it is a different
        // order entirely.
        ExactPath("Target Tank Path 2", [38, 37, 10, 24, 8], true,
            (38, -43_438, 0, 39_625, 1_131_757_568, 1_133_342_720, -2_147_483_648, 0),
            (37, -30_813, 0, 29_375, 1_132_523_520, 1_133_006_848, -2_147_483_648, 0),
            (8, -49_375, 0, 30_000, 1_131_368_448, 1_133_027_328, -2_147_483_648, 0),
            (10, -18_563, 0, 26_375, 1_132_924_928, 1_132_908_544, -2_147_483_648, 0),
            (24, -23_563, 0, 18_125, 1_132_761_088, 1_132_638_208, 0, 0)),
        // `Target Tank Path 1` was ALSO the synthetic triple (610000, 610000)..
        // (612000, 612000) before 2026-07-26 - 610 m away at exactly 45 degrees
        // - so `Target Tank #23`, the fourth beat-3 static target, drove away
        // from the firing range forever and beat 3 could never complete. That is
        // the G2 gap recorded in LEVEL100-TUTORIAL-BEATS-2026-07-26.md. It is
        // the same class of defect as the aliasing above: a fixture that was
        // never checked against the manifest.
        ExactPath("Target Tank Path 1", [6, 7, 18], false,
            (18, -68_688, 0, 80_000, 1_130_102_784, 1_134_665_728, 0, 0),
            (6, -25_438, 0, 20_500, 1_132_699_648, 1_132_716_032, -2_147_483_648, 0),
            (7, -70_125, 0, 51_563, 1_130_008_576, 1_133_733_888, -2_147_483_648, 0)),
        // The other agreeing path, and the other closed one.
        ExactPath("Drone Path 1", [1, 2, 3, 4], true,
            (1, -23_438, 0, -84_750, 1_132_765_184, 1_126_072_320, -2_147_483_648, 0),
            (2, -82_438, 0, -81_500, 1_129_201_664, 1_126_285_312, -2_147_483_648, 0),
            (3, -73_188, 0, -43_750, 1_129_807_872, 1_128_759_296, -2_147_483_648, 0),
            (4, -20_688, 0, -54_250, 1_132_855_296, 1_128_071_168, -2_147_483_648, 0)),
    ];

    // The air units' rows are the materialized ones: their flight scalars
    // come only from the unit records.
    private static IReadOnlyList<Level100ActorMotionDefinition>
        MotionDefinitions() =>
    [
        GroundMotion(0, "Target Tank"),
        GroundMotion(1, "Target Truck"),
        s_materialized.Value.GetMotionDefinition("Air Trainer"),
        s_materialized.Value.GetMotionDefinition("Target Drone"),
        s_materialized.Value.GetMotionDefinition("U-17 Highside Transporter"),
    ];

    private static Level100ActorMotionDefinition GroundMotion(
        int authoredOrder,
        string definitionName) => new(
            authoredOrder,
            definitionName,
            Level100ActorMotionClass.GroundVehicle,
            3,
            2,
            0x005E297C,
            2_000,
            0x40600000,
            0x3D567750,
            4,
            100);

    /// <summary>
    /// One released waypoint path, every field taken verbatim from the
    /// materialized manifest: node index, millimetre position, and the retail
    /// float components. Nothing here is reconstructed or renumbered.
    /// </summary>
    /// <param name="chain">
    /// The manifest's <c>targetChainNodeIndices</c>: each node's target is the
    /// next entry.
    /// </param>
    /// <param name="isClosed">
    /// The manifest's <c>isClosed</c>: the last node targets the first.
    /// </param>
    private static Level100WaypointPathDefinition ExactPath(
        string name,
        int[] chain,
        bool isClosed,
        params (int Node, int X, int Y, int Z, int B0, int B1, int B2, int B3)[] points) =>
        Level100WaypointPathDefinition.FromFileOrder(
            name,
            points.Select(point =>
            {
                int index = Array.IndexOf(chain, point.Node);
                if (index < 0)
                {
                    throw new InvalidOperationException($"Path '{name}' node {point.Node} is off its chain.");
                }
                int? target = index + 1 < chain.Length ? chain[index + 1] : isClosed ? chain[0] : null;
                return new Level100WaypointPointDefinition(
                    point.Node,
                    new SimVector3(point.X, point.Y, point.Z),
                    new Level100FloatVector4Bits(point.B0, point.B1, point.B2, point.B3),
                    target);
            }));
}
