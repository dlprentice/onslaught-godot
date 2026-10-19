// SPDX-License-Identifier: GPL-3.0-only
// Call-site evidence: pristine 74154bfa, CFeature 0044cb99/0044cbe0 and CUnit::Unknown87 004fc170.
// The shared virtual sound backend exercises lifetime and selection; no audible/device acceptance is claimed.
using System.Text;
using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Sound;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class FeaturePostFireSoundTests
{
    private sealed class TestUnit(ThingContext context) : Unit(context);
    private sealed class TestGroundUnit(ThingContext context) : GroundUnit(context);

    // A zero-part, radius-one authored mesh keeps Feature.Init on its real collision/occupancy path.
    private static MeshData FeatureMesh()
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        void Chunk(string tag, byte[] bytes) { writer.Write(ChunkReader.MakeId(tag)); writer.Write(bytes.Length); writer.Write(bytes); }
        Chunk("MESH", []); Chunk("NAME", new byte[309]);
        byte[] record = new byte[MeshData.RecordSize]; BinaryPrimitives.WriteSingleLittleEndian(record.AsSpan(0x164), 1);
        Chunk("CMSH", record); Chunk("CMST", []); Chunk("BBOX", []); Chunk("DATA", new byte[40]);
        return MeshData.Deserialize(new ChunkReader(MemBuffer.FromFile("authored-feature-mesh", stream.ToArray())),
            _ => throw new InvalidOperationException("Authored mesh has no external assets"))!;
    }

    private static (ThingContext Context, SampleMetadata Sample) MakeContext(bool prerun = false, bool configured = true,
        bool authoredLoop = false)
    {
        var pool = new SPtrSetPool(); pool.Init(256);
        var events = new EventManager(); events.Init();
        var map = HeightField.Load(new ChunkReader(MemBuffer.FromFile("feature-sound-flat-map",
            new byte[24 + HeightField.RecordSize + HeightField.SampleCount * 2])));
        map.InitQuickCollisionMap();
        var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(123), map, new PhysicsRegistry());
        context.Meshes.Add("fixture", FeatureMesh());
        context.SoundEffects = SoundEffects.Load(Encoding.Latin1.GetBytes(
            $"103\n1\neffect\nsample\nunused\n50\n50\n0\n{(authoredLoop ? 1 : 0)}\n0\n\n"));
        var sample = new SampleMetadata("sample", 44100);
        context.SoundEffects.SampleLookup = (name, _) => name == sample.Name ? sample : null;
        if (configured)
        {
            var backend = new VirtualSoundEventBackend(context.SoundEffects, new(1, 12, true, false), () => 0,
                _ => true, () => null, owner => owner is Thing t ? new(t.Pos, default) : null);
            context.SoundEffects.ConfigureEventKernel(backend, () => events.Time, () => prerun,
                Enumerable.Repeat(new SoundEventSeed(-1, default), 256).ToArray(), 1);
        }
        return (context, sample);
    }

    private static void InitFeature(Feature feature, bool sound = true) => feature.Init(new InitThing
    {
        Pos = new(3, 4, -1), Allegiance = 2,
        FeatureData = new FeatureData("feature"u8.ToArray())
            { MeshName = "fixture"u8.ToArray(), Life = 75, SoundName = sound ? "effect"u8.ToArray() : null },
    });

    private static Weapon Equip(ThingContext context, Unit owner, float reload = .25f, bool sound = true)
    {
        var mode = new WeaponModeData("mode"u8.ToArray())
        {
            Round = new RoundData("round"u8.ToArray()) { Speed = 1, Radius = .1f, LifeSpan = 10, Seek = 2 },
            PostFireSoundName = sound ? "effect"u8.ToArray() : null, PostFireDelay = reload, VolleySize = 0,
        };
        context.Physics.WeaponModes.Add(mode);
        var data = new WeaponData("weapon"u8.ToArray()); data.ChargeModes[0] = context.Physics.WeaponModes.Count - 1;
        var weapon = new Weapon(context, data, 0); weapon.InitialiseMode();
        weapon.Init(new InitEquipment { AttachedTo = owner, Number = -1 }); owner.CurrentWeapon = weapon;
        return weapon;
    }

    private static void Common(SoundEffects.SoundEvent e, Thing owner, SoundTracking tracking, bool repeat)
    {
        Assert.Same(owner, e.Owner); Assert.Equal(.35f, e.MasterVolume); Assert.Equal(tracking, e.Tracking);
        Assert.Equal(repeat, e.Looping); Assert.Equal(0, e.Fade); Assert.Equal(0, e.StartPoint); Assert.Equal(-1, e.EndPoint);
        Assert.Equal(1, e.Pitch); Assert.False(e.InformOwner); Assert.False(e.IgnoreOwnerPosition); Assert.Equal(SoundKind.Game, e.Kind);
    }

    [Fact]
    public void FeatureLoopStartsAfterItsStateAndOccupancyEvenDuringPrerun()
    {
        var (context, sample) = MakeContext(prerun: true); var feature = new Feature(context);
        context.SoundEffects!.SampleLookup = (name, _) =>
        {
            Assert.Equal("sample", name); Assert.Equal(75, feature.Life); Assert.Equal(2, feature.Allegiance);
            Assert.True(feature.AboveWater); Assert.True(context.Occupancy.Units.Contains(feature));
            Assert.True(context.World.Things.Contains(feature)); return sample;
        };
        InitFeature(feature);
        var e = Assert.Single(context.SoundEffects.Events); Common(e, feature, SoundTracking.FollowAndDie, true);
        Assert.Same(sample, e.Sample); Assert.Equal(feature.Pos, e.Position); Assert.Equal(1, context.Crt.Calls);
        feature.Pos = new(6, 8, -1); context.SoundEffects.UpdateStatus(); Assert.Equal(feature.Pos, e.Position);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void FeatureOnceUsesSampleOwnerIdentityUnlessAuthoredLoopDisablesIt(bool authoredLoop, int count)
    {
        var (context, sample) = MakeContext(authoredLoop: authoredLoop); var feature = new Feature(context);
        context.SoundEffects!.PlaySample(sample, feature, new SoundPlayback(Repeat: true));
        InitFeature(feature);
        Assert.Equal(count, context.SoundEffects.Events.Count()); Assert.Equal(1, context.Crt.Calls);
    }

    [Fact]
    public void FeatureShutdownStopsOnlyItsOwnEventsImmediatelyWhileFrozenBeforeMonitorRelease()
    {
        var (context, _) = MakeContext(); var first = new Feature(context); var second = new Feature(context);
        InitFeature(first); InitFeature(second);
        var stopped = Assert.Single(context.SoundEffects!.Events, e => ReferenceEquals(e.Owner, first));
        var retained = Assert.Single(context.SoundEffects.Events, e => ReferenceEquals(e.Owner, second));
        var reader = new ActiveReader<Feature>(); reader.SetReader(first);
        context.SoundEffects.SoundsFrozen = true;
        first.Shutdown(); // No UpdateStatus is needed to stop this owner.
        Assert.False(stopped.Playing); Assert.Null(stopped.Owner); Assert.Null(reader.ToRead);
        Assert.True(retained.Playing); Assert.Same(second, retained.Owner);
        Assert.False(context.Occupancy.Units.Contains(first)); Assert.True(context.Occupancy.Units.Contains(second));
        Assert.False(context.World.Things.Contains(first)); Assert.Null(first.RenderThing);
        Assert.Equal(2, context.Crt.Calls);
        Assert.Equal(1, context.NotPorted["CStaticShadows::UpdateVisibility(feature, remove: true)"]);
    }

    [Fact]
    public void MutedFeatureStillSelectsItsSampleAndFeatureWithoutSoundDoesNot()
    {
        var (context, _) = MakeContext(); context.SoundEffects!.SetMasterVolume(0);
        InitFeature(new Feature(context)); Assert.Empty(context.SoundEffects.Events); Assert.Equal(1, context.Crt.Calls);
        InitFeature(new Feature(context), sound: false); Assert.Equal(1, context.Crt.Calls);
    }

    [Fact]
    public void ActualFireCallsPostFireBeforeClearingShotAndSettingCooldown()
    {
        var (context, sample) = MakeContext(); context.Events.AdvanceTime();
        var unit = new TestUnit(context) { Pos = new(1, 2, -3), Unknown1E8 = 1, Unknown1EC = 1, Unknown168 = 7, Unknown16C = 99 };
        var weapon = Equip(context, unit); weapon.NextFireTime = -1;
        context.SoundEffects!.SampleLookup = (name, _) =>
        {
            Assert.Equal("sample", name); Assert.Equal(1, unit.Unknown1E8); Assert.Equal(1, unit.Unknown1EC);
            Assert.Equal(7, unit.Unknown168); Assert.Equal(99, unit.Unknown16C); return sample;
        };
        Assert.True(unit.Unknown74());
        var e = Assert.Single(context.SoundEffects.Events); Common(e, unit, SoundTracking.InitialPosition, false);
        Assert.Equal(0, unit.Unknown1E8); Assert.Equal(0, unit.Unknown1EC); Assert.Equal(2, unit.Unknown168);
        Assert.Equal(context.Events.Time + .25f, unit.Unknown16C); Assert.Equal(1, context.Crt.Calls);
        unit.Pos = new(7, 8, -2); context.SoundEffects.UpdateStatus(); Assert.Equal(new(1, 2, -3), e.Position);
        Assert.Equal(1, context.NotPorted["CUnit::SpawnComponentEffects (post-fire)"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PostFireMuteAndPrerunDoNotSuppressSelectionOrStateChanges(bool prerun)
    {
        var (context, _) = MakeContext(prerun: prerun); if (!prerun) context.SoundEffects!.SetMasterVolume(0);
        var unit = new TestUnit(context) { Unknown1E8 = 1 }; Equip(context, unit);
        unit.Unknown87(); Assert.Equal(1, context.Crt.Calls); Assert.Empty(context.SoundEffects!.Events);
        Assert.Equal(0, unit.Unknown1E8); Assert.Equal(2, unit.Unknown168); Assert.Equal(.25f, unit.Unknown16C);
    }

    [Fact]
    public void PostFireDoesNotUseOnceAndAbsentWeaponOrSoundDoesNotDraw()
    {
        var (context, _) = MakeContext(); var unit = new TestUnit(context); var weapon = Equip(context, unit);
        unit.Unknown87(); unit.Unknown87(); Assert.Equal(2, context.SoundEffects!.Events.Count()); Assert.Equal(2, context.Crt.Calls);
        weapon.Mode!.PostFireSoundName = null; unit.Unknown87(); Assert.Equal(2, context.Crt.Calls);
        unit.CurrentWeapon = null; unit.Unknown1E8 = 1; unit.Unknown16C = 99;
        unit.Unknown87(); Assert.Equal(2, context.Crt.Calls); Assert.Equal(0, unit.Unknown1E8);
        Assert.Equal(0, unit.Unknown168); Assert.Equal(99, unit.Unknown16C);
    }

    [Theory]
    [InlineData(0x00000000u, 0)]
    [InlineData(0x80000000u, 0)]
    [InlineData(0x7fc00001u, 0)]
    [InlineData(0x7f800001u, 0)]
    [InlineData(0xffc00001u, 0)]
    [InlineData(0x3e800000u, 2)]
    [InlineData(0xbe800000u, 2)]
    [InlineData(0x7f800000u, 2)]
    [InlineData(0xff800000u, 2)]
    public void PostFireReloadUsesNativeOrderedNonzeroGate(uint reloadBits, int expectedState)
    {
        var (context, _) = MakeContext(); var unit = new TestUnit(context) { Unknown1E8 = 1, Unknown168 = 7, Unknown16C = 99 };
        float reload = BitConverter.UInt32BitsToSingle(reloadBits); Equip(context, unit, reload, sound: false);
        unit.Unknown87(); Assert.Equal(expectedState, unit.Unknown168); Assert.Equal(0, unit.Unknown1E8);
        Assert.Equal(expectedState == 0 ? 99 : reload, unit.Unknown16C); Assert.Equal(0, context.Crt.Calls);
    }

    [Fact]
    public void UnconfiguredFixturesKeepTheirExplicitSoundBoundary()
    {
        var (context, _) = MakeContext(configured: false); InitFeature(new Feature(context));
        var unit = new TestUnit(context); Equip(context, unit); unit.Unknown87();
        Assert.Equal(0, context.Crt.Calls); Assert.Empty(context.SoundEffects!.Events);
        Assert.Contains("SOUND.PlayEffect (a feature's sound)", context.NotPorted.Keys);
        Assert.Contains("CUnit::Unknown87: the mode's post-fire sound", context.NotPorted.Keys);
    }

    [Fact]
    public void GroundUnitOverrideStillRefusesBeforeSoundOrStateMutation()
    {
        var (context, _) = MakeContext(); var unit = new TestGroundUnit(context) { Unknown1E8 = 1, Unknown168 = 7 };
        Equip(context, unit); Assert.Throws<NotSupportedException>(unit.Unknown87);
        Assert.Equal(0, context.Crt.Calls); Assert.Empty(context.SoundEffects!.Events);
        Assert.Equal(1, unit.Unknown1E8); Assert.Equal(7, unit.Unknown168);
    }
}
