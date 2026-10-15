// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

public sealed class Level100DestructionContactTests
{
    [Fact]
    public void PassiveBoundsPreserveRetailThreeAxisDistanceQuirk()
    {
        // Synthetic discriminator: conventional sqrt(1+1+9) rejects radius3;
        // shipped FADD gives sqrt(1+1+6), which accepts. No runtime shot claim.
        Assert.True(Level100ContactMechanics.TryPassiveSphereBounds(
            FloatVector(2, 2, 4), default, FloatBits(3), default, FloatVector(1, 1, 1), out int distance));
        Assert.Equal(unchecked((int)0xbe2fb0d0), distance); // PC24 sqrt rounds before subtraction
        Assert.True(Math.Sqrt(11) > 3);
    }

    [Theory]
    [InlineData(0, 0, 0, 1, -1)]
    [InlineData(2, 0, 0, 1, 0)]
    [InlineData(0, -2, 0, 1, 0)]
    [InlineData(0, 0, 2, 1, 0)]
    [InlineData(4, 5, 0, 5, 0)]
    [InlineData(4, 0, 5, 5, 0)]
    [InlineData(0, 4, 5, 5, 0)]
    public void PassiveBoundsIncludeFaceAndTwoAxisTangency(float x, float y, float z, float radius, float expected)
    {
        Assert.True(Level100ContactMechanics.TryPassiveSphereBounds(
            FloatVector(x, y, z), default, FloatBits(radius), default, FloatVector(1, 1, 1), out int distance));
        Assert.Equal(FloatBits(expected), distance);
    }

    [Fact]
    public void PassiveBoundsAxisGateRejectsBeforeThreeAxisShortcut()
    {
        Assert.False(Level100ContactMechanics.TryPassiveSphereBounds(
            FloatVector(2, 2, 4.5f), default, FloatBits(3), default, FloatVector(1, 1, 1), out _));
    }

    [Fact]
    public void PassiveBoundsDisplacementRoundsRegisterXAndSpilledYEndpoints()
    {
        var origin = FloatVector(16_777_216, 16_777_216, 0);
        Assert.True(Level100ContactMechanics.TryPassiveSphereBounds(
            origin, FloatVector(1, 0, 0), 0, origin, default, out int xDistance));
        Assert.True(Level100ContactMechanics.TryPassiveSphereBounds(
            origin, FloatVector(0, 1, 0), 0, origin, default, out int yDistance));
        Assert.Equal(0, xDistance);
        Assert.Equal(0, yDistance);
    }

    [Fact]
    public void PassiveBoundsRoundsSquareRootBeforeRadiusComparison()
    {
        // F32 sqrt(8) is slightly below the exact root. PC24 rounds FSQRT
        // to that same word, so tangency is accepted; a 53-bit root rejects.
        Assert.True(Level100ContactMechanics.TryPassiveSphereBounds(
            FloatVector(3, 3, 0), default, 0x403504f3, default, FloatVector(1, 1, 1), out int distance));
        Assert.Equal(0, distance);
    }

    private static int FloatBits(float value) => BitConverter.SingleToInt32Bits(value);
    private static Level100FloatVector3Bits FloatVector(float x, float y, float z) =>
        new(FloatBits(x), FloatBits(y), FloatBits(z));

    [Fact]
    public void WarehouseReportKeepsFirstSixContactsBeforeACloserSeventh()
    {
        var definition = Level100ContactCatalog.Instance.GetDefinition("Warehouse");
        var poses = new RetailUnitAttachmentPose[definition.PartCount];
        Array.Fill(poses, BoundsPose(default));
        for (int i = 0; i < 6; i++) poses[i] = BoundsPose(FloatVector(100, 0, 0));
        var seventhBox = definition.Parts[6].FloatGeometry.BoundingBoxWords.Span;
        poses[6] = BoundsPose(FloatVector(-WordFloat(seventhBox[0]), -WordFloat(seventhBox[1]), -WordFloat(seventhBox[2])));
        var activity = Enumerable.Repeat((byte)1, definition.PartCount).ToArray();
        Span<Level100PartBoundsContact> report = stackalloc Level100PartBoundsContact[6];
        int count = Level100ContactMechanics.CollectWarehouseSphereBounds(definition, poses, activity,
            default, default, FloatBits(1_000), report);
        Assert.Equal(6, count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, report.ToArray().Select(row => row.PartIndex));
        Assert.Equal(0u, new Level100DestructionState(42, definition).GetCurrentSegmentHealthBits(0));

        Array.Clear(activity);
        activity[6] = 1;
        Span<Level100PartBoundsContact> later = stackalloc Level100PartBoundsContact[6];
        Assert.Equal(1, Level100ContactMechanics.CollectWarehouseSphereBounds(definition, poses, activity,
            default, default, FloatBits(1_000), later));
        Assert.Equal(6, later[0].PartIndex);
        float closer = BitConverter.Int32BitsToSingle(later[0].SignedDistanceFloatBits);
        Assert.All(report.ToArray(), row => Assert.True(BitConverter.Int32BitsToSingle(row.SignedDistanceFloatBits) > closer));
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, report.ToArray().Select(row => row.PartIndex));
    }

    [Fact]
    public void WarehouseReferenceKeepsItsPoseAndIdentityWithSourceBounds()
    {
        var definition = Level100ContactCatalog.Instance.GetDefinition("Warehouse");
        Assert.Equal(14, definition.Parts[15].FloatGeometry.Reference);
        var emptyBox = definition.Parts[15].FloatGeometry.BoundingBoxWords.Span;
        foreach (int word in new[] { 0, 1, 2, 4, 5, 6, 8, 9 }) Assert.Equal(0u, emptyBox[word]);
        // Padding words 3/7 are retained opaque; empty geometry does not zero them.
        var box = definition.Parts[14].FloatGeometry.BoundingBoxWords.Span;
        Assert.True(WordFloat(box[4]) > 0);
        var inside = FloatVector(WordFloat(box[0]) + WordFloat(box[4]) * 0.5f, WordFloat(box[1]), WordFloat(box[2]));
        var poses = new RetailUnitAttachmentPose[definition.PartCount];
        Array.Fill(poses, BoundsPose(FloatVector(1_000_000, 1_000_000, 1_000_000)));
        poses[15] = BoundsPose(default);
        var activity = new byte[definition.PartCount];
        activity[15] = 1; // Referenced context 14 is inactive and has a different pose.
        Span<Level100PartBoundsContact> report = stackalloc Level100PartBoundsContact[6];
        Assert.Equal(1, Level100ContactMechanics.CollectWarehouseSphereBounds(definition, poses, activity,
            inside, default, 0, report));
        Assert.Equal(new Level100PartBoundsContact(15, 0), report[0]);
    }

    private static float WordFloat(uint bits) => BitConverter.Int32BitsToSingle(unchecked((int)bits));
    private static RetailUnitAttachmentPose BoundsPose(Level100FloatVector3Bits position) =>
        new(position, new(0x3f800000, 0, 0, 0, 0x3f800000, 0, 0, 0, 0x3f800000));

    [Fact]
    public void WarehouseRetainsOriginalPartRecordsWithoutSelectingRuntimePose()
    {
        var parts = Level100ContactCatalog.Instance.GetDefinition("Warehouse").Parts;
        Assert.Equal(28, parts.Count);
        Assert.Equal(22, parts.Count(part => part.FloatGeometry.SourceType == 1));
        Assert.Equal(6, parts.Count(part => part.FloatGeometry.SourceType == 6));
        Assert.Equal(new[] { 0, 8, 16, 17, 19, 20, 22, 24, 25, 27 },
            parts.Where(part => part.FloatGeometry.CachedOrientationWords.HasValue)
                .Select(part => part.Index));
        foreach (var part in parts)
        {
            var raw = part.FloatGeometry;
            Assert.Equal((uint)part.Index, raw.SourceId);
            Assert.Equal(0u, raw.NumNmicWord);
            Assert.Equal(0u, raw.IsNmicWord);
            Assert.Equal(1u, raw.Cmsp118Word);
            Assert.Equal(0u, raw.PositionCacheInheritanceWord);
            Assert.Equal(raw.CachedOrientationWords.HasValue ? 0u : 1u,
                raw.OrientationCacheInheritanceWord);
            Assert.Equal(101, raw.FrameMap!.Value.Length);
            Assert.All(raw.FrameMap.Value.ToArray(), frame => Assert.Equal(0, frame));
            Assert.Single(raw.HierarchyPositionWords!);
            Assert.Single(raw.HierarchyOrientationWords!);
            Assert.Equal(4, raw.CachedPositionWords!.Value.Length);
            Assert.Equal(10, raw.BoundingBoxWords.Length);
            Assert.Equal(32, raw.CmspTransformWords.Length);
        }
        // REFR keeps its empty original BBOX alongside the existing resolved
        // preview. Neither HORI nor a parent's CORI substitutes for absent CORI.
        var referenced = parts[27];
        Assert.Equal(19, referenced.FloatGeometry.Reference);
        Assert.Equal(0u, referenced.FloatGeometry.BoundingBoxWords.Span[8]);
        Assert.True(referenced.HalfExtents.X > 0);
        Assert.Null(parts[1].FloatGeometry.CachedOrientationWords);
        Assert.NotNull(parts[1].FloatGeometry.HierarchyOrientationWords);
        Assert.Equal(7u, parts[0].FloatGeometry.CachedPositionWords!.Value.Span[3]);
        Assert.Equal(0u, parts[0].FloatGeometry.HierarchyPositionWords![0].Span[3]);
    }

    [Theory]
    [InlineData("Target Tank", 0x3d30cb70u, 0xbe24c554u, 0xbec4d063u, 0x3fab28c8u, 0x3fc487a7u, 0x3f4ccccdu)]
    [InlineData("Target Truck", 0xbb8d5d80u, 0xbd4d6660u, 0xbef307e4u, 0x3fcfd61cu, 0x3fe83ed3u, 0x3f4ccccdu)]
    [InlineData("Target Drone", 0xbba22500u, 0x3ea56398u, 0xbcd78710u, 0x3fc26850u, 0x3fe9f832u, 0x3f800000u)]
    [InlineData("Warehouse", 0x3cf5e900u, 0x400739e3u, 0xbff19379u, 0x40f2bef5u, 0x41088ddeu, 0x3f800000u)]
    public void CatalogRetainsRawFloatGeometryApartFromQuantizedParts(string name,
        uint x, uint y, uint z, uint bboxRadius, uint renderRadius, uint primaryScale)
    {
        Assert.Equal(new Level100ContactFloatGeometry(x, y, z, bboxRadius, renderRadius, primaryScale),
            Level100ContactCatalog.Instance.GetDefinition(name).FloatGeometry);
    }

    [Fact]
    public void CatalogRetainsExactLevel100DefinitionsWithoutCreatingActors()
    {
        Level100ContactCatalog catalog = Level100ContactCatalog.Instance;

        Assert.Equal(28, catalog.Definitions.Count);
        Assert.Equal(70, catalog.PulseRound.RadiusMillimeters);
        Assert.Equal("Mech Pulse Hit Medium", catalog.PulseRound.ImpactPhysicsDefinition);
        Assert.Equal(
            "Mech Pulse Bolt Explosion Medium",
            catalog.PulseRound.ImpactParticleDescriptor);
        Assert.Equal("Explosion Small", catalog.PulseRound.ImpactSoundDescriptor);

        Level100ContactDefinition tank = catalog.GetDefinition("Target Tank");
        Assert.Equal(Level100DefinitionKind.TargetTank, tank.Kind);
        Assert.Equal(0x40C00000u, tank.MaximumLifeBits);
        Assert.Equal(7, tank.PartCount);
        Assert.Equal("Tank Explosion Medium", tank.DestructionPhysicsDefinition);
        Assert.Equal("Tank Explosion Medium", tank.DestructionParticleDescriptor);
        Assert.Equal("Explosion Medium", tank.DestructionSoundDescriptor);

        // Target Truck decodes from the shipped `m_f_truck_training.msh.aya`
        // (sha256 3bd92ce9...96c5): one collidable CMSH part `Mesh01` with
        // 306 vertices and 432 triangles, half-extents (525, 1510, 287) mm
        // about centre (-4, -50, -475). Life 3.0 (0x40400000) and the
        // destruction record are read from `Unit / Target Truck` at 0x24d9e in
        // `default physics.dat`. No part of this volume is authored here.
        Level100ContactDefinition truck = catalog.GetDefinition("Target Truck");
        Assert.Equal(Level100DefinitionKind.TargetTank, truck.Kind);
        Assert.Equal(0x40400000u, truck.MaximumLifeBits);
        Assert.Equal("m_f_truck_training.msh.aya", truck.Mesh);
        Assert.Equal(1, truck.PartCount);
        Assert.Equal("Mesh01", truck.Parts[0].Name);
        Assert.True(truck.Parts[0].Collidable);
        Assert.Equal(306 * 3, truck.Parts[0].VerticesMillimeters.Length);
        Assert.Equal(432 * 3, truck.Parts[0].Triangles.Length);
        Assert.Equal(
            new Level100Vector3(525, 1_510, 287),
            truck.Parts[0].HalfExtents);
        Assert.Equal(new Level100Vector3(-4, -50, -475), truck.Parts[0].Center);
        Assert.Equal("Tank Explosion Medium", truck.DestructionPhysicsDefinition);
        Assert.Equal("Tank Explosion Medium", truck.DestructionParticleDescriptor);
        Assert.Equal("Explosion Medium", truck.DestructionSoundDescriptor);

        // Target Drone decodes from the shipped `m_FA_F24_training.msh.aya`
        // (sha256 48876552...6ec5): 12 CMSH parts of which exactly one,
        // `Object03`, is collidable, with half-extents (519, 1416, 185) mm
        // about centre (-5, 323, -26). Life 1.0 (0x3F800000) and the
        // destruction record `Drone Explosion` (@0x5df9) come from
        // `Unit / Target Drone` at record 0x24e76 in `default physics.dat`.
        // The mesh names three of its non-collidable parts `GUNA`, `GUNA` and
        // `GUNB`, which is the mesh's own corroboration of the record's two
        // `CUnitUse` weapon slots. Nothing here is authored.
        Level100ContactDefinition drone = catalog.GetDefinition("Target Drone");
        Assert.Equal(Level100DefinitionKind.TargetDrone, drone.Kind);
        Assert.Equal(0x3F800000u, drone.MaximumLifeBits);
        Assert.Equal("m_FA_F24_training.msh.aya", drone.Mesh);
        Assert.Equal(12, drone.PartCount);
        Assert.Equal("Object03", drone.Parts[0].Name);
        Assert.True(drone.Parts[0].Collidable);
        Assert.Single(drone.Parts, part => part.Collidable);
        Assert.Equal(
            new Level100Vector3(519, 1_416, 185),
            drone.Parts[0].HalfExtents);
        Assert.Equal(new Level100Vector3(-5, 323, -26), drone.Parts[0].Center);
        Assert.Equal("Drone Explosion", drone.DestructionPhysicsDefinition);
        Assert.Equal(
            "Drone Explosion Effect",
            drone.DestructionParticleDescriptor);
        // The divergence that used to make this definition unrepresentable:
        // `Drone Explosion` field 6 (CExplosionWaterEffect) is a different
        // descriptor from fields 2/5/7. The schema now carries it.
        Assert.Equal(
            "Water Explosion Small",
            drone.DestructionWaterParticleDescriptor);
        Assert.Equal("Explosion Small", drone.DestructionSoundDescriptor);
        Assert.Equal(
            "Tank Explosion Medium",
            truck.DestructionWaterParticleDescriptor);

        Level100ContactDefinition warehouse = catalog.GetDefinition("Warehouse");
        Assert.Equal(Level100DefinitionKind.Warehouse, warehouse.Kind);
        Assert.Equal(0x42480000u, warehouse.MaximumLifeBits);
        Assert.Equal(28, warehouse.PartCount);
        Assert.Equal(-1, warehouse.Parts[0].Parent);
    }

    [Fact]
    public void SweptSphereUsesMeshNarrowphaseAfterBboxBroadphase()
    {
        Level100ContactDefinition tank =
            Level100ContactCatalog.Instance.GetDefinition("Target Tank");
        var actors = new[]
        {
            new Level100ContactActor(
                41,
                active: true,
                Level100Transform3.Identity,
                Level100Vector3.Zero,
                tank),
        };

        Assert.True(Level100ContactMechanics.TrySweepPulse(
            new Level100Vector3(0, 0, -2_000),
            new Level100Vector3(0, 0, 1_000),
            actors,
            out Level100ContactHit hit));
        Assert.Equal(41, hit.ActorId);
        Assert.Equal(Level100ContactSurfaceKind.Mesh, hit.SurfaceKind);
        Assert.InRange(hit.PartIndex, 0, 2);
        Assert.InRange(hit.TimePartsPerMillion, 1, 999_999);

        var destruction = new Level100DestructionState(41, tank);
        var destructionEvents = new Level100DestructionEvent[
            Level100DestructionState.MaximumEventsPerHit];
        destruction.ApplyPulseHit(
            hit,
            destructionEvents);
        Assert.Equal(0x40866666u, destruction.CurrentLifeBits);

        var disabled = new byte[tank.PartCount];
        actors[0] = new Level100ContactActor(
            41,
            active: true,
            Level100Transform3.Identity,
            Level100Vector3.Zero,
            tank,
            partActivity: disabled);
        Assert.False(Level100ContactMechanics.TrySweepPulse(
            new Level100Vector3(0, 0, -2_000),
            new Level100Vector3(0, 0, 1_000),
            actors,
            out _));

        actors[0] = new Level100ContactActor(
            41,
            active: true,
            Level100Transform3.Identity,
            Level100Vector3.Zero,
            tank);

        // This ray traverses the tank-body BBOX corner but no retained
        // triangle. A broadphase-only implementation reports a false hit.
        Assert.False(Level100ContactMechanics.TrySweepPulse(
            new Level100Vector3(800, 730, -2_000),
            new Level100Vector3(800, 730, 1_000),
            actors,
            out _));
    }

    [Fact]
    public void DownwardPulseUsesRetainedTerrainSamplerWithoutMarching()
    {
        Assert.True(Level100ContactMechanics.TrySweepPulseAgainstTerrain(
            new Level100Vector3(0, 0, -2_000),
            new Level100Vector3(0, 0, 2_000),
            out Level100ContactHit hit));

        Assert.Equal(Level100ContactSurfaceKind.Terrain, hit.SurfaceKind);
        Assert.Equal(-211, hit.SurfacePoint.Z);
        Assert.Equal(-281, hit.ImpactCenter.Z);
        Assert.Equal(429_625, hit.TimePartsPerMillion);
        Assert.True(hit.NormalPartsPerMillion.Z < 0);
    }

    [Fact]
    public void TerrainSweepFindsSamplerRidgeBetweenClearEndpoints()
    {
        var start = new Level100Vector3(16_000, 94_000, -7_140);
        var end = new Level100Vector3(17_000, 94_000, -7_140);

        Assert.False(Level100ContactMechanics.TrySweepPulseAgainstTerrain(
            start,
            start,
            out _));
        Assert.False(Level100ContactMechanics.TrySweepPulseAgainstTerrain(
            end,
            end,
            out _));
        Assert.True(Level100ContactMechanics.TrySweepPulseAgainstTerrain(
            start,
            end,
            out Level100ContactHit hit));
        Assert.Equal(Level100ContactSurfaceKind.Terrain, hit.SurfaceKind);
        Assert.InRange(hit.TimePartsPerMillion, 1, 999_999);
    }

    [Fact]
    public void ExternalActorContractRejectsInvalidIdentityBasisAndPart()
    {
        Level100ContactDefinition tank =
            Level100ContactCatalog.Instance.GetDefinition("Target Tank");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Level100ContactActor(
                0,
                active: true,
                Level100Transform3.Identity,
                Level100Vector3.Zero,
                tank));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Level100ContactActor(
                1,
                active: true,
                default,
                Level100Vector3.Zero,
                tank));

        var state = new Level100DestructionState(1, tank);
        var events = new Level100DestructionEvent[
            Level100DestructionState.MaximumEventsPerHit];
        Assert.Throws<ArgumentOutOfRangeException>(() => state.ApplyPulseHit(
            Hit(1, tank.PartCount),
            events));
    }

    [Fact]
    public void WarehouseUsesExtentWeightsAndDetachesALeafSegment()
    {
        Level100ContactDefinition warehouse =
            Level100ContactCatalog.Instance.GetDefinition("Warehouse");
        var state = new Level100DestructionState(101, warehouse);
        var events = new Level100DestructionEvent[
            Level100DestructionState.MaximumEventsPerHit];

        Assert.Equal(0u, state.GetInitialSegmentHealthBits(0));
        Assert.Equal(0x4196959Eu, state.GetInitialSegmentHealthBits(1));
        Assert.Equal(0x3ED1CE4Fu, state.GetInitialSegmentHealthBits(19));

        Level100ContactHit chimneyHit = Hit(101, 19);
        int count = state.ApplyPulseHit(
            chimneyHit,
            events);

        Assert.Equal(3, count);
        Assert.Equal(
            new[]
            {
                Level100DestructionEventKind.PulseImpact,
                Level100DestructionEventKind.SegmentDamaged,
                Level100DestructionEventKind.SegmentDetached,
            },
            events.AsSpan(0, count).ToArray().Select(item => item.Kind));
        Assert.Equal(0, state.ContactPartActivity.Span[19]);
        Assert.Equal(1, state.ContactPartActivity.Span[20]);
        Assert.False(state.Terminal);
    }

    [Fact]
    public void WarehouseTerminalIsCoreChildOrThirtyPercentNotSyntheticHull()
    {
        Level100ContactDefinition warehouse =
            Level100ContactCatalog.Instance.GetDefinition("Warehouse");
        var state = new Level100DestructionState(102, warehouse);
        var events = new Level100DestructionEvent[
            Level100DestructionState.MaximumEventsPerHit];
        Level100ContactHit coreHit = Hit(102, 1);

        for (int hitIndex = 0; hitIndex < 10; hitIndex++)
        {
            state.ApplyPulseHit(
                coreHit,
                events);
            Assert.False(state.Terminal);
            Assert.Equal(1, state.ContactPartActivity.Span[1]);
        }

        int terminalCount = state.ApplyPulseHit(
            coreHit,
            events);

        Assert.True(state.Terminal);
        Assert.Equal(0, state.ContactPartActivity.Span[1]);
        Assert.Equal(0, state.ContactPartActivity.Span[2]);
        Assert.Contains(
            events.AsSpan(0, terminalCount).ToArray(),
            item => item.Kind == Level100DestructionEventKind.Terminal &&
                item.EffectKind ==
                    Level100DestructionEffectKind.FacilityDestroyed);
    }

    [Fact]
    public void WarehouseLethalCoreBreaksImmediateChildrenInSegmentListOrder()
    {
        var warehouse = Level100ContactCatalog.Instance.GetDefinition("Warehouse");
        var state = new Level100DestructionState(102, warehouse);
        var before = state.CaptureSnapshot();
        var events = new Level100DestructionEvent[32];
        Level100ContactHit hit = Hit(102, 1);
        int count = state.ApplyRoundHit(hit, state.GetCurrentSegmentHealthBits(1),
            Level100DestructionEffectKind.PulseImpact, events);

        Assert.Equal(25, count);
        Level100DestructionEvent[] detached = events[..count]
            .Where(item => item.Kind == Level100DestructionEventKind.SegmentDetached).ToArray();
        // Native construction pushes authored children onto the head of the
        // segment list. These are immediate children; chimney events are queued.
        Assert.Equal(new[] { 1, 26, 23, 21, 18, 17, 16, 15, 14, 13, 12,
            11, 10, 9, 8, 7, 6, 5, 4, 3, 2 }, detached.Select(item => item.PartIndex));
        Assert.All(detached, item =>
        {
            Assert.Equal(Level100DestructionEffectKind.None, item.EffectKind);
            Assert.Equal(hit.SurfacePoint, item.Position); // cause anchor, not debris origin
            Assert.Equal(0u, state.GetCurrentSegmentHealthBits(item.PartIndex));
            Assert.Equal(0, state.ContactPartActivity.Span[item.PartIndex]);
        });
        Assert.Equal(1, Assert.Single(events[..count],
            item => item.Kind == Level100DestructionEventKind.SegmentDamaged).PartIndex);
        int[] surviving = [0, 19, 20, 22, 24, 25, 27];
        Assert.Equal(surviving, Enumerable.Range(0, warehouse.PartCount)
            .Where(index => state.ContactPartActivity.Span[index] != 0));
        foreach (int index in surviving)
            Assert.Equal(before.CurrentHealthBits.Span[index], state.GetCurrentSegmentHealthBits(index));
    }

    [Fact]
    public void WarehouseExtraBreakDoesNotImmediatelyPropagateToChimneys()
    {
        var warehouse = Level100ContactCatalog.Instance.GetDefinition("Warehouse");
        var state = new Level100DestructionState(102, warehouse);
        var before = state.CaptureSnapshot();
        var events = new Level100DestructionEvent[Level100DestructionState.MaximumEventsPerHit];
        int count = state.ApplyRoundHit(Hit(102, 18), state.GetCurrentSegmentHealthBits(18),
            Level100DestructionEffectKind.PulseImpact, events);
        Assert.Equal(18, Assert.Single(events[..count],
            item => item.Kind == Level100DestructionEventKind.SegmentDetached).PartIndex);
        foreach (int index in new[] { 19, 20 })
        {
            Assert.Equal(1, state.ContactPartActivity.Span[index]);
            Assert.Equal(before.CurrentHealthBits.Span[index], state.GetCurrentSegmentHealthBits(index));
        }
    }

    [Fact]
    public void WarehouseCascadeRejectsShortEventBufferBeforeChangingState()
    {
        var warehouse = Level100ContactCatalog.Instance.GetDefinition("Warehouse");
        var state = new Level100DestructionState(102, warehouse);
        var before = state.CaptureSnapshot();
        var events = new Level100DestructionEvent[31];
        Assert.Throws<ArgumentException>(() => state.ApplyRoundHit(Hit(102, 1),
            state.GetCurrentSegmentHealthBits(1), Level100DestructionEffectKind.PulseImpact, events));
        var after = state.CaptureSnapshot();
        Assert.Equal(before.CurrentHealthBits.ToArray(), after.CurrentHealthBits.ToArray());
        Assert.Equal(before.PartActivity.ToArray(), after.PartActivity.ToArray());
        Assert.Equal(before.Terminal, after.Terminal);
        Assert.Equal(before.BelowHalfReported, after.BelowHalfReported);
        Assert.All(events, item => Assert.Equal(default, item));
    }

    [Fact]
    public void WarehouseCascadeSnapshotRestoresHealthEligibilityAndHash()
    {
        var warehouse = Level100ContactCatalog.Instance.GetDefinition("Warehouse");
        var state = new Level100DestructionState(102, warehouse);
        var events = new Level100DestructionEvent[32];
        int count = state.ApplyRoundHit(Hit(102, 1), state.GetCurrentSegmentHealthBits(1),
            Level100DestructionEffectKind.PulseImpact, events);
        var snapshot = state.CaptureSnapshot();
        var restored = new Level100DestructionState(102, warehouse);
        restored.Restore(snapshot);
        Assert.Equal(0, restored.ContactPartActivity.Span[2]);
        Assert.Equal(1, restored.ContactPartActivity.Span[0]); // zero health is still eligible
        Assert.Equal(snapshot.CurrentHealthBits.ToArray(), restored.CaptureSnapshot().CurrentHealthBits.ToArray());
        Assert.Equal(snapshot.PartActivity.ToArray(), restored.ContactPartActivity.ToArray());
        WorldSnapshot original = new Simulation(0x100u, Level100TestActorDefinitions.Create()).Snapshot with
        {
            Level100Destruction = new([snapshot]),
            Level100DestructionEvents = events[..count],
        };
        Assert.Equal(StateHasher.ComputeHex(original), StateHasher.ComputeHex(original with
        {
            Level100Destruction = new([restored.CaptureSnapshot()]),
        }));
        foreach (bool changeHealth in new[] { true, false })
        {
            uint[] health = snapshot.CurrentHealthBits.ToArray();
            byte[] activity = snapshot.PartActivity.ToArray();
            if (changeHealth) health[2] = 0x3f800000;
            else activity[2] = 1;
            // Change one collateral field only, retaining terminal state and
            // every event; neither array may disappear from canonical hashing.
            var altered = new Level100DestructionSnapshot(snapshot.ActorId, snapshot.DefinitionName,
                snapshot.CurrentLifeBits, snapshot.Terminal, snapshot.BelowHalfReported,
                snapshot.InitialHealthBits.ToArray(), health, activity);
            Assert.NotEqual(StateHasher.ComputeHex(original), StateHasher.ComputeHex(original with
            {
                Level100Destruction = new([altered]),
            }));
        }
    }

    [Fact]
    public void WarehouseBelowHalfEventSumsRemainingLeavesInNativeListOrder()
    {
        var warehouse = Level100ContactCatalog.Instance.GetDefinition("Warehouse");
        var state = new Level100DestructionState(102, warehouse);
        var events = new Level100DestructionEvent[Level100DestructionState.MaximumEventsPerHit];
        int[] leafHits = [17, 9, 14, 15, 7, 13, 19, 3, 25, 12, 10, 16, 4, 27, 8, 11, 5];
        for (int index = 0; index < leafHits.Length; index++)
        {
            int part = leafHits[index];
            Assert.True(warehouse.Parts[part].FloatGeometry.Children.IsEmpty);
            int count = state.ApplyRoundHit(Hit(102, part), state.GetCurrentSegmentHealthBits(part),
                Level100DestructionEffectKind.PulseImpact, events);
            Assert.False(state.Terminal);
            Assert.Equal(1, state.ContactPartActivity.Span[1]);
            if (index < leafHits.Length - 1)
                Assert.DoesNotContain(events[..count], item =>
                    item.Kind == Level100DestructionEventKind.ActiveSubtreeBelowHalf);
            else
                Assert.Equal(0x4200da7du, Assert.Single(events[..count], item =>
                    item.Kind == Level100DestructionEventKind.ActiveSubtreeBelowHalf).RemainingHealthBits);
        }
    }

    [Fact]
    public void WarehouseDoesNotTerminateAtExactNativeThirtyPercentBoundary()
    {
        var warehouse = Level100ContactCatalog.Instance.GetDefinition("Warehouse");
        var state = new Level100DestructionState(102, warehouse);
        var initial = new uint[warehouse.PartCount];
        var health = new uint[warehouse.PartCount];
        var activity = new byte[warehouse.PartCount];
        // Synthetic boundary state: retain the real cached initial total
        // 0x42821ded, but put precisely its PC24 * binary64(0.3) threshold
        // in the surviving core. This is not an authored retail damage route.
        initial[1] = 0x419c23e9;
        initial[2] = 0x423629e6;
        health[1] = 0x3f800000;
        activity[0] = activity[1] = 1;
        state.Restore(new Level100DestructionSnapshot(102, warehouse.Name,
            warehouse.MaximumLifeBits, false, false, initial, health, activity));
        var events = new Level100DestructionEvent[Level100DestructionState.MaximumEventsPerHit];
        int count = state.ApplyRoundHit(Hit(102, 1), 0,
            Level100DestructionEffectKind.PulseImpact, events);
        Assert.False(state.Terminal); // strict less-than; equality survives
        Assert.DoesNotContain(events[..count], item => item.Kind == Level100DestructionEventKind.Terminal);
    }

    [Fact]
    public void WarehouseAlsoTerminatesBelowThirtyPercentWithCoreIntact()
    {
        Level100ContactDefinition warehouse =
            Level100ContactCatalog.Instance.GetDefinition("Warehouse");
        var state = new Level100DestructionState(103, warehouse);
        var events = new Level100DestructionEvent[
            Level100DestructionState.MaximumEventsPerHit];

        for (int partIndex = 2; partIndex < warehouse.PartCount && !state.Terminal; partIndex++)
        {
            Level100ContactHit hit = Hit(103, partIndex);
            while (state.ContactPartActivity.Span[partIndex] != 0 && !state.Terminal)
            {
                state.ApplyPulseHit(
                    hit,
                    events);
            }
        }

        Assert.True(state.Terminal);
        Assert.Equal(1, state.ContactPartActivity.Span[1]);
        Assert.NotEqual(0u, state.GetCurrentSegmentHealthBits(1));
    }

    [Fact]
    public void TargetTankEmitsTypedImpactAndTerminalEffects()
    {
        Level100ContactDefinition tank =
            Level100ContactCatalog.Instance.GetDefinition("Target Tank");
        var state = new Level100DestructionState(201, tank);
        var events = new Level100DestructionEvent[
            Level100DestructionState.MaximumEventsPerHit];
        Level100ContactHit hit = Hit(201, 0);

        for (int hitIndex = 0; hitIndex < 3; hitIndex++)
        {
            state.ApplyPulseHit(hit, events);
            Assert.False(state.Terminal);
        }
        int count = state.ApplyPulseHit(hit, events);

        Assert.True(state.Terminal);
        Assert.Equal(0xBF99999Au, state.CurrentLifeBits);
        Assert.Equal(
            new[] { 0xBE4CCCD4u, 0xBF99999Au },
            events.AsSpan(0, count)
                .ToArray()
                .Where(item =>
                    item.Kind == Level100DestructionEventKind.SegmentDamaged)
                .Select(item => item.RemainingHealthBits));
        Assert.Contains(
            events.AsSpan(0, count).ToArray(),
            item => item.Kind == Level100DestructionEventKind.PulseImpact &&
                item.EffectKind == Level100DestructionEffectKind.PulseImpact);
        Assert.Contains(
            events.AsSpan(0, count).ToArray(),
            item => item.Kind == Level100DestructionEventKind.Terminal &&
                item.EffectKind ==
                    Level100DestructionEffectKind.TargetDestroyed);
    }

    [Fact]
    public void PulseHitPreservesDirectThenExplosionDamageOrder()
    {
        // Retail CRound::Hit 0x004D8AE0 first sends the configured 0.8
        // CRoundDamage through target slot 40. Its mode-3 impact path then
        // creates the configured immediate-radius explosion, whose Hit at
        // 0x0044BF10 sends 1.0 through the same receiver. The old Core path
        // collapsed those calls into one synthetic 1.8 subtraction and lost
        // the observable intermediate life state.
        Level100ContactDefinition tank =
            Level100ContactCatalog.Instance.GetDefinition("Target Tank");
        var state = new Level100DestructionState(204, tank);
        var events = new Level100DestructionEvent[
            Level100DestructionState.MaximumEventsPerHit];

        int count = state.ApplyPulseHit(Hit(204, 0), events);

        Assert.Equal(0x40866666u, state.CurrentLifeBits);
        Assert.Equal(
            new[] { 0x40A66666u, 0x40866666u },
            events.AsSpan(0, count)
                .ToArray()
                .Where(item =>
                    item.Kind == Level100DestructionEventKind.SegmentDamaged)
                .Select(item => item.RemainingHealthBits));
    }

    [Fact]
    public void PulseHitDestroysTargetDroneOnlyAfterExplosionStage()
    {
        Level100ContactDefinition drone =
            Level100ContactCatalog.Instance.GetDefinition("Target Drone");
        var state = new Level100DestructionState(205, drone);
        var events = new Level100DestructionEvent[
            Level100DestructionState.MaximumEventsPerHit];

        int count = state.ApplyPulseHit(Hit(205, 0), events);

        Assert.True(state.Terminal);
        Assert.Equal(0xBF4CCCCDu, state.CurrentLifeBits);
        Assert.Equal(
            new[] { 0x3E4CCCCCu, 0xBF4CCCCDu },
            events.AsSpan(0, count)
                .ToArray()
                .Where(item =>
                    item.Kind == Level100DestructionEventKind.SegmentDamaged)
                .Select(item => item.RemainingHealthBits));
        Assert.Contains(
            events.AsSpan(0, count).ToArray(),
            item => item.Kind == Level100DestructionEventKind.Terminal &&
                item.EffectKind ==
                    Level100DestructionEffectKind.DroneDestroyed);
    }

    [Fact]
    public void WholeBodyLifeRequiresStrictlyNegativeRemainingLifeToBecomeTerminal()
    {
        Level100ContactDefinition tank =
            Level100ContactCatalog.Instance.GetDefinition("Target Tank");
        var state = new Level100DestructionState(202, tank);
        var events = new Level100DestructionEvent[
            Level100DestructionState.MaximumEventsPerHit];
        Level100ContactHit hit = Hit(202, 0);
        Assert.Equal(0x40C00000u, tank.MaximumLifeBits);

        int zeroCount = state.ApplyRoundHit(
            hit,
            tank.MaximumLifeBits,
            Level100DestructionEffectKind.PulseImpact,
            events);

        Assert.Equal(0x00000000u, state.CurrentLifeBits);
        Assert.False(state.Terminal);
        Assert.DoesNotContain(
            events.AsSpan(0, zeroCount).ToArray(),
            item => item.Kind == Level100DestructionEventKind.Terminal);

        int negativeCount = state.ApplyRoundHit(
            hit,
            0x3F800000u,
            Level100DestructionEffectKind.PulseImpact,
            events);

        Assert.Equal(0xBF800000u, state.CurrentLifeBits);
        Assert.True(state.Terminal);
        Assert.Contains(
            events.AsSpan(0, negativeCount).ToArray(),
            item => item.Kind == Level100DestructionEventKind.Terminal);
    }

    [Fact]
    public void ApplyDamageTraceVectorPreservesExactOverkillBits()
    {
        // Existing-trace reproof a0bd86d8... observes CUnit__ApplyDamage at
        // 0x004F9A90 store life 0x3BA3D70B -> 0xC479FFAE for amount
        // 0x447A0000 (1000.0f). The actor's shield was already zero, so this
        // pins the whole-body life arithmetic and terminal edge only; it does
        // not claim positive-shield absorption.
        Level100ContactDefinition tank =
            Level100ContactCatalog.Instance.GetDefinition("Target Tank");
        var state = new Level100DestructionState(203, tank);
        Level100DestructionSnapshot baseline = state.CaptureSnapshot();
        state.Restore(new Level100DestructionSnapshot(
            baseline.ActorId,
            baseline.DefinitionName,
            currentLifeBits: 0x3BA3D70Bu,
            terminal: false,
            belowHalfReported: false,
            baseline.InitialHealthBits.ToArray(),
            baseline.CurrentHealthBits.ToArray(),
            baseline.PartActivity.ToArray()));
        var events = new Level100DestructionEvent[
            Level100DestructionState.MaximumEventsPerHit];

        int count = state.ApplyRoundHit(
            Hit(203, 0),
            damageBits: 0x447A0000u,
            Level100DestructionEffectKind.PulseImpact,
            events);

        Assert.Equal(0xC479FFAEu, state.CurrentLifeBits);
        Assert.True(state.Terminal);
        Assert.Contains(
            events.AsSpan(0, count).ToArray(),
            item => item.Kind == Level100DestructionEventKind.SegmentDamaged &&
                item.RemainingHealthBits == 0xC479FFAEu);
        Assert.Contains(
            events.AsSpan(0, count).ToArray(),
            item => item.Kind == Level100DestructionEventKind.Terminal);
    }

    [Fact]
    public void RuntimeConsumesRegistryPoseAndReportsReleasedLifecycleFacts()
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var registry = new Level100ActorRegistry(definitions);
        var runtime = new Level100DestructionRuntime(registry);
        Level100ActorId actorId = Assert.IsType<Level100ActorId>(
            registry.GetThingRef("Target Tank 2"));
        Level100ActorSnapshot authored = registry.GetActor(actorId);
        var pose = new Level100ActorPoseSnapshot(
            new SimVector3(1_000, 3_000, 2_000),
            IdentityFloatBasis(),
            new SimVector3(100, 0, 0),
            new SimVector3(0, 25_000, 0));
        registry.SetPose(actorId, pose);

        // Contact-local sweep is (0,0,-2000)->(0,0,1000). Core's vertical
        // axis is up, so the target remains above terrain while the released
        // contact path receives its native Z-down coordinates.
        var start = new SimVector3(1_000, 5_000, 2_000);
        var end = new SimVector3(1_000, 2_000, 2_000);
        for (int hitIndex = 0; hitIndex < 4; hitIndex++)
        {
            Assert.True(runtime.TryApplyPulseSweep(start, end, out Level100ContactHit hit));
            Assert.Equal(actorId.Value, hit.ActorId);
            Assert.Equal(Level100ContactSurfaceKind.Mesh, hit.SurfaceKind);
        }

        Level100ActorSnapshot destroyed = registry.GetActor(actorId);
        Assert.Equal(authored.DefinitionIdentity, destroyed.DefinitionIdentity);
        Assert.Equal(authored.MeshBinding, destroyed.MeshBinding);
        Assert.Equal(pose, destroyed.Pose);
        Assert.Equal(0, destroyed.Health);
        // Ground vehicles notify the script immediately but retain their
        // physical actor until the queued SHUTDOWN reaches its frame bucket.
        Assert.True(destroyed.Active);
        Assert.NotEqual(Level100ActorLifecycle.Destroyed, destroyed.Lifecycle);
        Assert.True(registry.GetBaseState(actorId).IsDying);
        Assert.False(registry.GetBaseState(actorId).IsShuttingDown);
        Assert.Equal(
            new[]
            {
                Level100ActorFactKind.Hit,
                Level100ActorFactKind.Hit,
                Level100ActorFactKind.Hit,
                Level100ActorFactKind.Hit,
                Level100ActorFactKind.StartedDying,
                Level100ActorFactKind.Died,
            },
            registry.Snapshot.PendingFacts.Select(fact => fact.Kind));

        Level100DestructionSnapshot component = runtime.Snapshot.Actors.Single(
            item => item.ActorId == actorId.Value);
        Assert.True(component.Terminal);
        Assert.Contains(
            runtime.Events,
            item => item.ActorId == actorId.Value &&
                item.Kind == Level100DestructionEventKind.Terminal);

        WorldSnapshot envelope = new Simulation(0x100u, definitions).Snapshot;
        Assert.NotEqual(
            StateHasher.ComputeHex(envelope),
            StateHasher.ComputeHex(envelope with
            {
                Level100Destruction = runtime.Snapshot,
                Level100DestructionEvents = runtime.Events,
            }));
    }

    [Fact]
    public void SpawnerLossRetainsPlaneHealthAndPartsWithoutDeclaringShutdown()
    {
        var definitions = Level100TestActorDefinitions.LoadMaterialized();
        var registry = new Level100ActorRegistry(definitions);
        var id = Assert.Single(registry.SpawnThing(registry.GetThingRef("Airfield")!.Value,
            "Target Drone", "SpawnerB", 1, "AirborneDrone1"));
        var runtime = new Level100DestructionRuntime(registry);
        registry.DrainFacts();
        var before = runtime.Snapshot.Actors.Single(actor => actor.ActorId == id.Value);
        int health = registry.GetActor(id).Health;

        runtime.StartPlaneDeathAfterSpawnerLoss(id);
        Assert.Equal(Level100ActorFactKind.StartedDying, Assert.Single(registry.DrainFacts()).Kind);
        runtime.StartPlaneDeathAfterSpawnerLoss(id);
        Assert.Empty(registry.DrainFacts());
        Assert.Equal(Level100ActorLifecycle.StartedDying, registry.GetActor(id).Lifecycle);
        Assert.True(registry.GetBaseState(id).IsDying);
        Assert.False(registry.GetBaseState(id).IsShuttingDown);
        Assert.Equal(health, registry.GetActor(id).Health);
        runtime.BeginTick(); // Production registry/destruction consistency check.
        var restoredRegistry = new Level100ActorRegistry(definitions, registry.Snapshot);
        var restored = new Level100DestructionRuntime(restoredRegistry, runtime.Snapshot);
        restored.BeginTick();
        var after = restored.Snapshot.Actors.Single(actor => actor.ActorId == id.Value);
        Assert.False(after.Terminal);
        Assert.Equal(before.CurrentLifeBits, after.CurrentLifeBits);
        Assert.Equal(before.CurrentHealthBits.ToArray(), after.CurrentHealthBits.ToArray());
        Assert.Equal(before.PartActivity.ToArray(), after.PartActivity.ToArray());
        Assert.Empty(restored.Snapshot.PendingShutdowns);
        Assert.Throws<InvalidOperationException>(() =>
            runtime.StartPlaneDeathAfterSpawnerLoss(registry.GetThingRef("Player 1")!.Value));
    }

    [Theory]
    [InlineData("Target Tank")]
    [InlineData("Target Truck")]
    public void DyingGroundTargetStillLosesLifeWithoutASecondDeath(string definitionName)
    {
        // Component calls supply damage; this is not a recorded player shot.
        var state = new Level100DestructionState(203,
            Level100ContactCatalog.Instance.GetDefinition(definitionName));
        var events = new Level100DestructionEvent[Level100DestructionState.MaximumEventsPerHit];
        state.ApplyRoundHit(Hit(203, 0), 0x41000000,
            Level100DestructionEffectKind.PulseImpact, events);
        Assert.True(state.Terminal);
        float life = state.CurrentLife;
        int count = state.ApplyRoundHit(Hit(203, 0), 0x3f800000,
            Level100DestructionEffectKind.PulseImpact, events);
        Assert.Equal(life - 1f, state.CurrentLife);
        Assert.DoesNotContain(events.Take(count), item =>
            item.Kind == Level100DestructionEventKind.Terminal);
    }

    [Theory]
    [InlineData(13u)]
    [InlineData(198u)]
    [InlineData(3999u)]
    public void GroundShutdownUsesRingBucketAfterTenAdvancesEvenBeforeStoredDueTime(uint frame)
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var registry = new Level100ActorRegistry(definitions);
        var runtime = new Level100DestructionRuntime(registry);
        Level100ActorId id = registry.GetThingRef("Target Tank 2")!.Value;
        PositionGroundTarget(registry, id);
        var scheduler = new RetailEventScheduler();
        for (uint i = 0; i < frame; i++) scheduler.Update();
        RetailEventAdmission admission = scheduler.AddEventTimeFromNow(0.5f, 2000, id.Value);

        Assert.True(runtime.TryApplyRoundSweep(GroundStart, GroundEnd, 200, 0x41000000,
            Level100DestructionEffectKind.PulseImpact, out var hit, frame));
        Assert.Equal(id.Value, hit.ActorId);
        Level100GroundShutdownSnapshot pending = Assert.Single(runtime.Snapshot.PendingShutdowns);
        Assert.Equal(frame + 10, pending.DeliveryFrame);
        Assert.Equal(admission.DueTimeBits, pending.DueTimeBits);
        registry.DrainFacts();
        Assert.True(runtime.TryApplyRoundSweep(GroundStart, GroundEnd, 200, 0x3f800000,
            Level100DestructionEffectKind.PulseImpact, out hit, frame + 1));
        Assert.Equal(id.Value, hit.ActorId);
        Assert.Equal(pending, Assert.Single(runtime.Snapshot.PendingShutdowns));
        Assert.DoesNotContain(registry.DrainFacts(), fact => fact.Kind is
            Level100ActorFactKind.StartedDying or Level100ActorFactKind.Died);

        // BeginTick clears presentation events even during a paused UI update;
        // it must not move the scheduler or consume the pending shutdown.
        for (int i = 0; i < 30; i++) runtime.BeginTick();
        Assert.Equal(pending, Assert.Single(runtime.Snapshot.PendingShutdowns));
        for (int i = 1; i < 10; i++)
        {
            Assert.Empty(scheduler.Update());
            runtime.FlushStartOfFrame(frame + (uint)i);
            Assert.True(registry.GetActor(id).Active);
        }
        WorldSnapshot envelope = new Simulation(0x100u, definitions).Snapshot;
        var before = envelope with { Level100Actors = registry.Snapshot };
        Assert.True(OnslaughtRebuild.Client.Level100TargetPresentation.Project(
            before.Targets.Single(target => target.ActorId == id)).Visible);

        Assert.Single(scheduler.Update());
        if (frame == 13)
        {
            Assert.Equal(0x3f933334u, pending.DueTimeBits);
            Assert.Equal(0x3f933333u, BitConverter.SingleToUInt32Bits(scheduler.Time));
            Assert.True(scheduler.Time < admission.DueTime);
        }
        runtime.FlushStartOfFrame(frame + 10);
        Assert.Empty(runtime.Snapshot.PendingShutdowns);
        Assert.False(registry.GetActor(id).Active);
        Assert.Equal(Level100ActorLifecycle.Destroyed, registry.GetActor(id).Lifecycle);
        Assert.False(registry.GetBaseState(id).IsShuttingDown);
        Assert.Empty(registry.DrainFacts()); // no duplicate died notification
        Assert.False(OnslaughtRebuild.Client.Level100TargetPresentation.Project(
            (before with { Level100Actors = registry.Snapshot }).Targets.Single(target => target.ActorId == id)).Visible);
    }

    [Fact]
    public void GroundDeathDeletesScriptImmediatelyAndRestoresPendingPhysicalShutdown()
    {
        var definitions = Level100TestActorDefinitions.Create();
        var registry = new Level100ActorRegistry(definitions);
        var runtime = new Level100DestructionRuntime(registry);
        Level100ActorId id = registry.GetThingRef("Target Tank 2")!.Value;
        Level100ActorId player = registry.GetThingRef("Player 1")!.Value;
        var scripts = new Level100ActorScriptRuntime(registry, player);
        scripts.InitializeReleasedScripts();
        PositionGroundTarget(registry, id);
        registry.Activate(id);
        registry.SetObjective(id, true);
        Assert.True(runtime.TryApplyRoundSweep(GroundStart, GroundEnd, 200, 0x41000000,
            Level100DestructionEffectKind.PulseImpact, out var hit, 13));
        Assert.Equal(id.Value, hit.ActorId);
        Assert.True(registry.GetActor(id).IsObjective); // native shutdown has not cleared it
        foreach (var fact in registry.DrainFacts()) scripts.DispatchFact(fact);
        Assert.False(registry.GetActor(id).IsObjective); // the actual Died script clears it
        Assert.True(registry.GetActor(id).Active);
        Assert.DoesNotContain(scripts.Snapshot.Instances, item => item.ActorId == id);

        var restoredRegistry = new Level100ActorRegistry(definitions, registry.Snapshot);
        var restored = new Level100DestructionRuntime(restoredRegistry, runtime.Snapshot);
        var restoredScripts = new Level100ActorScriptRuntime(restoredRegistry, player, scripts.Snapshot);
        Assert.DoesNotContain(restoredScripts.Snapshot.Instances, item => item.ActorId == id);
        WorldSnapshot envelope = new Simulation(0x100u, definitions).Snapshot with
        {
            Level100Actors = registry.Snapshot,
            Level100Destruction = runtime.Snapshot,
            RetailEventFrameCount = 13,
        };
        // This component-only envelope supplies no aircraft event clock.
        envelope = Level100TestActorDefinitions.LegacyHashEnvelope(envelope);
        string expectedHash = StateHasher.ComputeHex(envelope);
        Assert.Equal(expectedHash, StateHasher.ComputeHex(Level100TestActorDefinitions.LegacyHashEnvelope(envelope with
        {
            Level100Actors = restoredRegistry.Snapshot,
            Level100Destruction = restored.Snapshot,
        })));
        var pending = Assert.Single(runtime.Snapshot.PendingShutdowns);
        foreach (var changed in new[]
        {
            pending with { AdmissionFrame = 14 },
            pending with { DeliveryFrame = 24 },
            pending with { DueTimeBits = pending.DueTimeBits + 1 },
        })
        {
            var invalid = runtime.Snapshot with { PendingShutdowns = [changed] };
            Assert.NotEqual(expectedHash, StateHasher.ComputeHex(envelope with { Level100Destruction = invalid }));
            Assert.Throws<ArgumentException>(() => new Level100DestructionRuntime(restoredRegistry, invalid));
        }
        Assert.Throws<InvalidDataException>(() => new Level100DestructionRuntime(restoredRegistry,
            runtime.Snapshot with { PendingShutdowns = [] }));
        restored.FlushStartOfFrame(22);
        Assert.True(restoredRegistry.GetActor(id).Active);
        restored.FlushStartOfFrame(23);
        Assert.False(restoredRegistry.GetActor(id).Active);
    }

    /// <summary>
    /// A dying round's step (the RE lane's "No hit while dying"): the contact
    /// still reaches the struck thing's Hit, so its script hears it, but the
    /// round's own Hit returns before damage, the impact explosion and the
    /// round's death.
    /// </summary>
    [Fact]
    public void DyingRoundContact_ReportsTheHitButLeavesTheTargetUnharmed()
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var registry = new Level100ActorRegistry(definitions);
        var runtime = new Level100DestructionRuntime(registry);
        Level100ActorId id = registry.GetThingRef("Target Tank 2")!.Value;
        PositionGroundTarget(registry, id);
        registry.Activate(id);
        registry.DrainFacts();
        runtime.BeginTick();
        int health = registry.GetActor(id).Health;

        Assert.True(runtime.TryReportDyingRoundContact(GroundStart, GroundEnd, 200));
        Level100ActorFactSnapshot fact = Assert.Single(registry.DrainFacts());
        Assert.Equal(Level100ActorFactKind.Hit, fact.Kind);
        Assert.Equal(id, fact.ActorId);
        Assert.Equal(Level100ReleasedThingTypeMasks.Ammunition, fact.OtherThingTypeMask);
        Assert.Equal(health, registry.GetActor(id).Health);
        Assert.Empty(runtime.Events);
        Assert.Empty(runtime.Snapshot.PendingShutdowns);

        // The same step from a live round damages the tank and bursts on it.
        Assert.True(runtime.TryApplyRoundSweep(GroundStart, GroundEnd, 200, 0x41000000,
            Level100DestructionEffectKind.PulseImpact, out _, 0));
        Assert.True(registry.GetActor(id).Health < health);
        Assert.NotEmpty(runtime.Events);
    }

    private static readonly SimVector3 GroundStart = new(1_000, 5_000, 2_000);
    private static readonly SimVector3 GroundEnd = new(1_000, 2_000, 2_000);

    private static void PositionGroundTarget(Level100ActorRegistry registry, Level100ActorId id) =>
        registry.SetPose(id, new Level100ActorPoseSnapshot(new SimVector3(1_000, 3_000, 2_000),
            IdentityFloatBasis(), SimVector3.Zero, SimVector3.Zero));

    [Fact]
    public void EqualTimeGroundShutdownsPreserveInsertionOrderThroughRestoreAndHash()
    {
        var definitions = Level100TestActorDefinitions.Create();
        var registry = new Level100ActorRegistry(definitions);
        var runtime = new Level100DestructionRuntime(registry);
        var ids = registry.Snapshot.Actors.Where(actor => actor.DefinitionName == "Target Tank")
            .OrderByDescending(actor => actor.ActorId.Value).Take(2).Select(actor => actor.ActorId).ToArray();
        Assert.Equal(2, ids.Length);
        foreach (var id in ids)
        {
            PositionGroundTarget(registry, id);
            registry.Activate(id);
            Assert.True(runtime.TryApplyRoundSweep(GroundStart, GroundEnd, 200, 0x41000000,
                Level100DestructionEffectKind.PulseImpact, out var hit, 198));
            Assert.Equal(id.Value, hit.ActorId);
            // Keep the next ray clear without changing activity/lifetime.
            registry.SetPose(id, registry.GetPose(id) with { PositionMillimeters = new(90_000, 3_000, 90_000) });
        }
        Assert.Equal(ids.Select(id => id.Value), runtime.Snapshot.PendingShutdowns.Select(item => item.ActorId));
        var restoredRegistry = new Level100ActorRegistry(definitions, registry.Snapshot);
        var restored = new Level100DestructionRuntime(restoredRegistry, runtime.Snapshot);
        Assert.Equal(runtime.Snapshot.PendingShutdowns, restored.Snapshot.PendingShutdowns);
        WorldSnapshot envelope = new Simulation(0x100u, definitions).Snapshot with
        {
            Level100Actors = registry.Snapshot,
            Level100Destruction = runtime.Snapshot,
            RetailEventFrameCount = 198,
        };
        envelope = Level100TestActorDefinitions.LegacyHashEnvelope(envelope);
        Assert.NotEqual(StateHasher.ComputeHex(envelope), StateHasher.ComputeHex(envelope with
        {
            Level100Destruction = runtime.Snapshot with
            { PendingShutdowns = runtime.Snapshot.PendingShutdowns.Reverse().ToArray() },
        }));
        restored.FlushStartOfFrame(208);
        Assert.Empty(restored.Snapshot.PendingShutdowns);
        Assert.All(ids, id => Assert.False(restoredRegistry.GetActor(id).Active));
        // A fresh runtime after reset has no inherited timers.
        Assert.Empty(new Level100DestructionRuntime(new Level100ActorRegistry(definitions)).Snapshot.PendingShutdowns);
    }

    [Fact]
    public void NearerStaticGeometryOccludesDestructibleActorWithoutDamage()
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var registry = new Level100ActorRegistry(definitions);
        var runtime = new Level100DestructionRuntime(registry);
        Level100ActorId facilityId = Assert.IsType<Level100ActorId>(
            registry.GetThingRef("Control Tower"));
        Level100ActorId targetId = Assert.IsType<Level100ActorId>(
            registry.GetThingRef("Target Tank 2"));

        registry.SetPose(
            facilityId,
            new Level100ActorPoseSnapshot(
                new SimVector3(0, 4_000, 0),
                IdentityFloatBasis(),
                SimVector3.Zero,
                SimVector3.Zero));
        registry.SetPose(
            targetId,
            new Level100ActorPoseSnapshot(
                new SimVector3(0, 2_000, 0),
                IdentityFloatBasis(),
                SimVector3.Zero,
                SimVector3.Zero));
        Level100ActorSnapshot facilityBefore = registry.GetActor(facilityId);
        Level100ActorSnapshot targetBefore = registry.GetActor(targetId);

        Assert.True(runtime.TryApplyPulseSweep(
            new SimVector3(0, 6_000, 0),
            new SimVector3(0, 1_000, 0),
            out Level100ContactHit hit));

        Assert.Equal(facilityId.Value, hit.ActorId);
        Assert.Equal(Level100ContactSurfaceKind.Mesh, hit.SurfaceKind);
        Assert.Equal(facilityBefore, registry.GetActor(facilityId));
        Assert.Equal(targetBefore, registry.GetActor(targetId));
        // The occluding static takes no damage, but it does now observe the
        // released THING_TYPE_AMMUNITION contact: `CThing::Hit` dispatches to
        // the attached script whether or not the thing is destructible, and
        // Facilities.msl / TankFactory.msl / Turret.msl all define
        // `hit(otherThing)`. Without this fact `Hit Friendly Building` and
        // `Broke Tutorial` can never be posted. This assertion was
        // `Assert.Empty` while that route was missing.
        Level100ActorFactSnapshot hitFact =
            Assert.Single(registry.Snapshot.PendingFacts);
        Assert.Equal(Level100ActorFactKind.Hit, hitFact.Kind);
        Assert.Equal(facilityId, hitFact.ActorId);
        Assert.Equal(
            Level100ReleasedThingTypeMasks.Ammunition,
            hitFact.OtherThingTypeMask);
        Level100DestructionEvent impact = Assert.Single(runtime.Events);
        Assert.Equal(Level100DestructionEventKind.PulseImpact, impact.Kind);
        Assert.Equal(Level100DestructionEffectKind.PulseImpact, impact.EffectKind);
        Assert.Equal(facilityId.Value, impact.ActorId);
    }

    [Fact]
    public void TerrainContactEmitsImpactWithoutRegistryMutation()
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var registry = new Level100ActorRegistry(definitions);
        var runtime = new Level100DestructionRuntime(registry);
        Level100ActorRegistrySnapshot before = registry.Snapshot;

        Assert.True(runtime.TryApplyPulseSweep(
            new SimVector3(0, 2_000, 0),
            new SimVector3(0, -2_000, 0),
            out Level100ContactHit hit));

        Assert.Equal(0, hit.ActorId);
        Assert.Equal(Level100ContactSurfaceKind.Terrain, hit.SurfaceKind);
        Level100DestructionEvent impact = Assert.Single(runtime.Events);
        Assert.Equal(Level100DestructionEventKind.PulseImpact, impact.Kind);
        Assert.Equal(Level100DestructionEffectKind.PulseImpact, impact.EffectKind);
        Assert.Equal(0, impact.ActorId);
        Assert.Equal(-1, impact.PartIndex);
        Assert.Equal(hit.SurfacePoint, impact.Position);

        Level100ActorRegistrySnapshot after = registry.Snapshot;
        Assert.Equal(before.NextActorId, after.NextActorId);
        Assert.Equal(before.NextFactSequence, after.NextFactSequence);
        Assert.Equal(before.Actors.ToArray(), after.Actors.ToArray());
        Assert.Equal(before.PendingFacts.ToArray(), after.PendingFacts.ToArray());

        var vulcanRuntime = new Level100DestructionRuntime(
            new Level100ActorRegistry(definitions));
        Assert.True(vulcanRuntime.TryApplyRoundSweep(
            new SimVector3(0, 2_000, 0),
            new SimVector3(0, -2_000, 0),
            Level100ContactMechanics.PulseRadiusMillimeters,
            Level100DestructionState.MechBulletDamageBits,
            Level100DestructionEffectKind.VulcanImpact,
            out _));
        Level100DestructionEvent vulcanImpact =
            Assert.Single(vulcanRuntime.Events);
        Assert.Equal(
            Level100DestructionEventKind.VulcanImpact,
            vulcanImpact.Kind);
        Assert.Equal(
            Level100DestructionEffectKind.VulcanImpact,
            vulcanImpact.EffectKind);
    }

    [Fact]
    public void DamagedAndTerminalRegistryDestructionSnapshotsRestoreAndHash()
    {
        AssertRegistryDestructionRoundTrip(hitCount: 1, expectedHealth: 4_200);
        AssertRegistryDestructionRoundTrip(hitCount: 4, expectedHealth: 0);
    }

    [Fact]
    public void MeshContactHotPathDoesNotAllocatePerQuery()
    {
        Level100ContactDefinition tank =
            Level100ContactCatalog.Instance.GetDefinition("Target Tank");
        var actors = new[]
        {
            new Level100ContactActor(
                401,
                active: true,
                Level100Transform3.Identity,
                Level100Vector3.Zero,
                tank),
        };
        Level100Vector3 start = new(0, 0, -2_000);
        Level100Vector3 end = new(0, 0, 1_000);
        Assert.True(Level100ContactMechanics.TrySweepPulse(
            start,
            end,
            actors,
            out _));

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 100; index++)
        {
            Assert.True(Level100ContactMechanics.TrySweepPulse(
                start,
                end,
                actors,
                out _));
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    private static Level100ContactHit Hit(int actorId, int partIndex) =>
        new(
            actorId,
            partIndex,
            Level100ContactSurfaceKind.Mesh,
            500_000,
            new Level100Vector3(0, 0, 0),
            new Level100Vector3(0, 0, 70),
            new Level100Vector3(0, 0, -1_000_000));

    private static void AssertRegistryDestructionRoundTrip(
        int hitCount,
        int expectedHealth)
    {
        Level100ActorDefinitionSet definitions =
            Level100TestActorDefinitions.Create();
        var registry = new Level100ActorRegistry(definitions);
        var runtime = new Level100DestructionRuntime(registry);
        Level100ActorId actorId = Assert.IsType<Level100ActorId>(
            registry.GetThingRef("Target Tank 2"));
        registry.SetPose(
            actorId,
            new Level100ActorPoseSnapshot(
                new SimVector3(1_000, 3_000, 2_000),
                IdentityFloatBasis(),
                SimVector3.Zero,
                SimVector3.Zero));

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Assert.True(runtime.TryApplyPulseSweep(
                new SimVector3(1_000, 5_000, 2_000),
                new SimVector3(1_000, 2_000, 2_000),
                out _));
        }

        Level100ActorSnapshot actor = registry.GetActor(actorId);
        Assert.Equal(expectedHealth, actor.Health);
        Assert.Equal(
            hitCount == 4
                ? Level100ActorLifecycle.DiedAwaitingShutdown
                : Level100ActorLifecycle.Alive,
            actor.Lifecycle);
        Assert.True(actor.Active);

        Level100ActorRegistrySnapshot registrySnapshot = registry.Snapshot;
        Level100DestructionRuntimeSnapshot destructionSnapshot = runtime.Snapshot;
        IReadOnlyList<Level100DestructionEvent> events = runtime.Events;
        var restoredRegistry = new Level100ActorRegistry(
            definitions,
            registrySnapshot);
        var restoredRuntime = new Level100DestructionRuntime(
            restoredRegistry,
            destructionSnapshot);
        Assert.Equal(expectedHealth, restoredRegistry.GetActor(actorId).Health);
        Assert.Equal(
            destructionSnapshot.Actors.Single(item =>
                item.ActorId == actorId.Value).Terminal,
            restoredRuntime.Snapshot.Actors.Single(item =>
                item.ActorId == actorId.Value).Terminal);

        WorldSnapshot envelope = new Simulation(0x100u, definitions).Snapshot;
        WorldSnapshot original = envelope with
        {
            Level100Actors = registrySnapshot,
            Level100Destruction = destructionSnapshot,
            Level100DestructionEvents = events,
        };
        WorldSnapshot restored = original with
        {
            Level100Actors = restoredRegistry.Snapshot,
            Level100Destruction = restoredRuntime.Snapshot,
        };
        Assert.Equal(
            StateHasher.ComputeHex(original),
            StateHasher.ComputeHex(restored));

        Level100ActorRegistrySnapshot mismatchedSnapshot =
            registrySnapshot with
            {
                Actors = registrySnapshot.Actors
                    .Select(item => item.ActorId == actorId
                        ? item with { Health = item.Health + 1 }
                        : item)
                    .ToArray(),
            };
        if (hitCount == 4)
        {
            // A death-notified ground actor must project zero registry life.
            Assert.Throws<ArgumentException>(() => new Level100ActorRegistry(
                definitions, mismatchedSnapshot));
            return;
        }
        var mismatchedRegistry = new Level100ActorRegistry(
            definitions,
            mismatchedSnapshot);
        Assert.Throws<InvalidDataException>(() =>
            new Level100DestructionRuntime(
                mismatchedRegistry,
                destructionSnapshot));
    }

    private static Level100FloatBasis3Bits IdentityFloatBasis() => new(
        BitConverter.SingleToInt32Bits(1f), 0, 0,
        0, BitConverter.SingleToInt32Bits(1f), 0,
        0, 0, BitConverter.SingleToInt32Bits(1f));
}
