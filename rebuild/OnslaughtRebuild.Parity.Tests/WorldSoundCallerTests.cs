// SPDX-License-Identifier: GPL-3.0-only
// Native call arguments from pristine74154bfa:0050619c/00506a96 weapon,004e2f11 impact,
// 0044be28 explosion and004fbdad pre-fire. Tests use the shared event kernel and virtual backend;
// authored sample metadata/spatial inputs are explicit fixtures, not audio or retail-play acceptance.
using System.Text;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Sound;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class WorldSoundCallerTests
{
    private sealed class TestUnit(ThingContext c) : Unit(c)
    {
        public int Material;
        public override int GetSoundMaterial() => Material;
    }
    private static ThingContext Context(bool configured = true, bool prerun = false)
    {
        var pool = new SPtrSetPool(); pool.Init(256);
        var events = new EventManager(); events.Init();
        var raw = new byte[24 + HeightField.RecordSize + HeightField.SampleCount * 2];
        var map = HeightField.Load(new ChunkReader(MemBuffer.FromFile("sound-caller-flat-map", raw)));
        map.InitQuickCollisionMap();
        var c = new ThingContext(pool, events, new SeedUniformRandomNumberStream(123), map, new PhysicsRegistry());
        string[] names = ["launch", "prefire", "explosion", "water", "impact\\Wood", "hit 1", "hit 2", "hit 3", "hit 4", "hit 5", "hit 6"];
        var lines = new List<string> { "103", names.Length.ToString() };
        foreach (string name in names) lines.AddRange([name, name, "unused", name == "launch" ? "50" : "100", "50", "0", "0", "0", ""]);
        c.SoundEffects = SoundEffects.Load(Encoding.Latin1.GetBytes(string.Join('\n', lines)));
        if (configured)
        {
            var samples = names.ToDictionary(n => n, n => new SampleMetadata(n, 44100));
            c.SoundEffects.SampleLookup = (name, _) => samples.GetValueOrDefault(name);
            var backend = new VirtualSoundEventBackend(c.SoundEffects, new(1, 12, true, false), () => 0,
                _ => true, () => null, owner => owner is Thing t ? new(t.Pos, default) : null);
            c.SoundEffects.ConfigureEventKernel(backend, () => 0, () => prerun,
                Enumerable.Repeat(new SoundEventSeed(-1, default), 256).ToArray(), 1);
        }
        return c;
    }
    private static Weapon Weapon(ThingContext c, Unit owner, int perBurst = 0, int volley = 0)
    {
        var mode = new WeaponModeData("mode"u8.ToArray())
        {
            Round = new RoundData("round"u8.ToArray()) { Speed = 1, Gravity = .1f, Radius = .1f, LifeSpan = 10, Seek = 2 },
            LaunchSoundName = "launch"u8.ToArray(), PreFireSoundName = "prefire"u8.ToArray(),
            SoundPerBurst = perBurst, VolleySize = volley,
        };
        c.Physics.WeaponModes.Add(mode);
        var definition = new WeaponData("weapon"u8.ToArray()); definition.ChargeModes[0] = c.Physics.WeaponModes.Count - 1;
        var weapon = new Weapon(c, definition, 0); weapon.InitialiseMode();
        weapon.Init(new InitEquipment { AttachedTo = owner, Number = -1 });
        return weapon;
    }
    private static void Common(SoundEffects.SoundEvent e, object? owner, float volume, SoundTracking tracking)
    {
        Assert.Same(owner, e.Owner); Assert.Equal(volume, e.MasterVolume); Assert.Equal(tracking, e.Tracking);
        Assert.False(e.Looping); Assert.Equal(0, e.Fade); Assert.Equal(0, e.StartPoint); Assert.Equal(-1, e.EndPoint);
        Assert.Equal(1, e.Pitch); Assert.False(e.InformOwner); Assert.False(e.IgnoreOwnerPosition); Assert.Equal(SoundKind.Game, e.Kind);
    }

    [Fact]
    public void UnitLaunchUsesHalfGainAndAttachedOwnerBeforeAnyVolleyAllocation()
    {
        var c = Context(); var owner = new TestUnit(c) { Pos = new(1, 2, -3) }; var weapon = Weapon(c, owner);
        Assert.False(weapon.SpawnRound()); // zero volley still plays per-shot sound
        var e = Assert.Single(c.SoundEffects!.Events); Common(e, owner, .25f, SoundTracking.FollowDontDie);
        Assert.Equal(owner.Pos, e.Position); Assert.Equal(1, c.Crt.Calls);
        Assert.False(weapon.SpawnRound()); Assert.Equal(2, c.SoundEffects.Events.Count()); Assert.Equal(2, c.Crt.Calls);
    }

    [Fact]
    public void BattleEngineLaunchHasNoSpatialOwnerAndStillUsesHalfGain()
    {
        var c = Context(); var owner = new BattleEngine(c) { Configuration = new BattleEngineData(), State = BattleEngineState.Walker };
        owner.SetThingType(0); // Init normally establishes the native type bits before any weapon can fire
        owner.WalkerPart = new BattleEngineWalkerPart(owner); owner.JetPart = new BattleEngineJetPart(owner);
        var weapon = Weapon(c, owner); owner.WalkerPart.PrimaryWeapon = weapon; owner.StoreValue[weapon.Data.AmmoStore] = 10;
        Assert.False(weapon.SpawnRound());
        Common(Assert.Single(c.SoundEffects!.Events), null, .25f, SoundTracking.None);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    public void PerBurstSoundRequiresSuccessfulRoundAndDoesNotDoubleThePerShotCall(int volley, int expectedEvents)
    {
        var c = Context(); var owner = new TestUnit(c) { Pos = new(20, 20, -3), Orientation = FMatrix.Identity };
        var weapon = Weapon(c, owner, perBurst: 1, volley: volley);
        Assert.True(weapon.Fire());
        Assert.Equal(expectedEvents, c.SoundEffects!.Events.Count()); Assert.Equal(expectedEvents, c.Crt.Calls);
        if (expectedEvents != 0) Common(Assert.Single(c.SoundEffects.Events), owner, .25f, SoundTracking.FollowDontDie);
    }

    [Fact]
    public void ImpactKeepsFirstArgumentOwnerAndWoodSkipsOnlyTheMaterialChoiceDraw()
    {
        var c = Context(); var a = new TestUnit(c) { Material = 0x66, Pos = new(3, 4, -1) };
        var b = new TestUnit(c) { Material = 1, Pos = new(10, 2, -1) };
        Thing.PlayImpactSound(a, b);
        var e = Assert.Single(c.SoundEffects!.Events); Assert.Equal("impact\\Wood", e.Sample.Name);
        Common(e, a, .7f, SoundTracking.InitialPosition); Assert.Equal(a.Pos, e.Position); Assert.Equal(1, c.Crt.Calls);
        a.Material = 2; Thing.PlayImpactSound(a, b);
        Assert.Equal(2, c.SoundEffects.Events.Count()); Assert.Equal(3, c.Crt.Calls); // material and chain draws
        a.Material = 0; Thing.PlayImpactSound(a, b); Assert.Equal(3, c.Crt.Calls);
    }

    [Theory]
    [InlineData(0, "explosion")]
    [InlineData(1, "explosion")]
    [InlineData(2, "water")]
    [InlineData(3, "explosion")]
    [InlineData(4, null)]
    [InlineData(-1, null)]
    public void ExplosionSelectsItsCollisionSoundAndKeepsInitialPositionAfterDeletion(int type, string? sample)
    {
        var c = Context(); var explosion = new Explosion(c);
        explosion.Init(new InitThing { Pos = new(3, 4, -1), ColType = type,
            Behaviour = new ExplosionData("test"u8.ToArray()) { SoundName = "explosion"u8.ToArray(), WaterSoundName = "water"u8.ToArray() } });
        if (sample is null) { Assert.Empty(c.SoundEffects!.Events); Assert.Equal(0, c.Crt.Calls); return; }
        var e = Assert.Single(c.SoundEffects!.Events); Common(e, explosion, .7f, SoundTracking.InitialPosition);
        Assert.Equal(sample, e.Sample.Name); Assert.Equal(explosion.Pos, e.Position); FVector position = e.Position;
        explosion.Shutdown(); Assert.Null(e.Owner); Assert.True(e.Playing);
        c.SoundEffects.UpdateStatus(); Assert.True(e.Playing); Assert.Equal(position, e.Position);
    }

    [Fact]
    public void PreFireSoundSeesPreparationStateAndOnlyPlaysOnSuccessfulAdmission()
    {
        var c = Context(); var unit = new TestUnit(c) { UnitData = new UnitData("unit"u8.ToArray()), Pos = new(1, 2, -3) };
        var weapon = Weapon(c, unit); unit.CurrentWeapon = weapon; weapon.Mode!.PreFireDelay = .5f;
        var lookup = c.SoundEffects!.SampleLookup!;
        c.SoundEffects.SampleLookup = (name, language) =>
        {
            Assert.Equal("prefire", name); Assert.Equal(1, unit.Unknown168); Assert.Equal(1, unit.Unknown1EC);
            Assert.Equal(.5f, unit.Unknown16C); Assert.Equal(0, unit.Unknown1E8); return lookup(name, language);
        };
        Assert.True(unit.PrepareShot()); Common(Assert.Single(c.SoundEffects.Events), unit, .7f, SoundTracking.InitialPosition);
        Assert.False(unit.PrepareShot()); Assert.Equal(1, c.Crt.Calls);
    }

    [Fact]
    public void PrerunAndMutedSoundStillConsumeSelectionButDoNotCreateNonrepeatingEvents()
    {
        foreach (bool prerun in new[] { false, true })
        {
            var c = Context(prerun: prerun); if (!prerun) c.SoundEffects!.SetMasterVolume(0);
            var unit = new TestUnit(c); Weapon(c, unit).SpawnRound();
            Assert.Equal(1, c.Crt.Calls); Assert.Empty(c.SoundEffects!.Events);
        }
    }

    [Fact]
    public void UnconfiguredCallersPreserveTheLegacySelectionOnlyBoundary()
    {
        var c = Context(configured: false); var unit = new TestUnit(c) { UnitData = new UnitData("unit"u8.ToArray()) };
        var weapon = Weapon(c, unit); weapon.SpawnRound(); Assert.Equal(1, c.Crt.Calls); Assert.Empty(c.SoundEffects!.Events);
        unit.CurrentWeapon = weapon; Assert.True(unit.PrepareShot()); Assert.Equal(1, c.Crt.Calls);
        Assert.Contains(c.NotPorted.Keys, s => s.Contains("pre-fire sound"));
    }
}
