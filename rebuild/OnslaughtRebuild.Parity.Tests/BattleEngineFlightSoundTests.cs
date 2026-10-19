// SPDX-License-Identifier: GPL-3.0-only
// Pristine PC 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750:
// Morph0040a580, Forward00412d80, Backward00412f70, StrafeLeft00413160 and StrafeRight00413360.
// Matched source: bea-decomp src/BattleEngine.cpp and BattleEngineWalkerPart.cpp. These caller tests use the
// actual shared kernel and virtual backend with authored sample metadata/seeds. They are not audio acceptance.
using System.Buffers.Binary;
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

public sealed class BattleEngineFlightSoundTests
{
    private sealed class Fixture
    {
        public BattleEngine Engine { get; }
        public SoundEffects Sounds { get; }
        public Dictionary<string, SampleMetadata> Samples { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Lookups { get; } = [];
        public Action? OnLookup;
        public float SoundTime;

        public Fixture(bool authoredLoop = false)
        {
            string[] names = ["BE Engines(takeoff)", "BE Engines(in-flight)", "BE Engines(land)", "BE Strafe L/R"];
            var text = new StringBuilder("103\n4\n");
            foreach (string name in names)
            {
                string sample = name + ".wav";
                Samples.Add(sample, new(sample, 88200));
                text.Append($"{name}\n{sample}\n\n75\n0\n0\n{(name == names[1] && authoredLoop ? 1 : 0)}\n0\n\n");
            }
            Sounds = SoundEffects.Load(Encoding.Latin1.GetBytes(text.ToString()));
            var pool = new SPtrSetPool(); pool.Init(64);
            var events = new EventManager(); events.Init();
            for (int i = 0; i < 320; i++) events.AdvanceTime();
            var raw = new byte[24 + HeightField.RecordSize + HeightField.SampleCount * 2];
            BinaryPrimitives.WriteSingleLittleEndian(raw.AsSpan(16 + 0x102c), 1);
            BinaryPrimitives.WriteSingleLittleEndian(raw.AsSpan(16 + 0x1034), 100);
            var map = HeightField.Load(new ChunkReader(MemBuffer.FromFile("flat-map", raw)));
            var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(1), map, new PhysicsRegistry())
                { SoundEffects = Sounds };
            Engine = new(context)
            {
                Configuration = new BattleEngineData { GroundVelocity = 4.37f, MinTransformEnergy = 1 },
                State = BattleEngineState.Walker, Energy = 8, LastTimeOnGround = events.Time,
                Pos = new(0, 0, -10), ZoomOutTime = 9,
            };
            Engine.WalkerPart = new(Engine);
            Engine.JetPart = new(Engine);
            Engine.WalkerPart.PrimaryWeapon = new Weapon(context, new WeaponData("primary"u8.ToArray()), 0) { ChargeValue = .8f };
            Engine.WalkerPart.AugWeapon = new Weapon(context, new WeaponData("aug"u8.ToArray()), 0) { ChargeValue = .8f };
            Sounds.SampleLookup = (name, language) =>
            {
                Assert.False(language); Lookups.Add(name); OnLookup?.Invoke();
                return Samples.GetValueOrDefault(name);
            };
            var backend = new VirtualSoundEventBackend(Sounds, new(1, 12, true, false), () => 0, _ => true,
                () => new SoundCameraState(Engine.Pos, Engine.Pos, FMatrix.Identity),
                owner => owner is Thing t ? new SoundOwnerSpatialState(t.Pos, default) : null);
            Sounds.ConfigureEventKernel(backend, () => SoundTime, () => false,
                Enumerable.Repeat(new SoundEventSeed(-1, default), 256).ToArray(), backend.Profile.SoundRate);
            backend.PrepareStatus();
        }

        public SoundEffect Inflight => Sounds.Find("BE Engines(in-flight)")!;
        public SampleMetadata InflightSample => Samples[Inflight.SampleName];
        public void Tick() { SoundTime += .05f; Sounds.UpdateStatus(); }
    }

    private static void AssertPlayback(SoundEffects.SoundEvent e, BattleEngine engine, SoundTracking tracking, bool repeat, float fade)
    {
        Assert.Same(engine, e.Owner); Assert.Equal(tracking, e.Tracking);
        Assert.True(e.Playing); Assert.False(e.Paused); Assert.Equal(repeat, e.Looping);
        Assert.Equal(.75f, e.MasterVolume); Assert.Equal(fade, e.Fade);
        Assert.Equal(0, e.StartPoint); Assert.Equal(-1, e.EndPoint);
        Assert.Equal(1, e.Pitch); Assert.Equal(1, e.DesiredPitch);
        Assert.False(e.InformOwner); Assert.False(e.IgnoreOwnerPosition); Assert.Equal(SoundKind.Game, e.Kind);
        Assert.InRange(e.Channel, 0, 11);
    }

    [Fact]
    public void TakeoffAdmitsOwnedOneShotThenRampingRepeatWithExactArgumentsAndTwoDraws()
    {
        var f = new Fixture();
        f.OnLookup = () => Assert.Equal(0, f.Engine.WalkerPart!.PrimaryWeapon!.ChargeValue);
        f.Engine.Morph();
        Assert.Equal(BattleEngineState.MorphingIntoJet, f.Engine.State);
        Assert.Equal(new[] { "BE Engines(takeoff).wav", "BE Engines(in-flight).wav" }, f.Lookups);
        Assert.Equal(2, f.Engine.Context.Crt.Calls);
        Assert.Equal(2, f.Sounds.EventCount);
        var takeoff = f.Sounds.FindPlayingSample("BE Engines(takeoff).wav", f.Engine)!;
        var inflight = f.Sounds.FindPlayingSample(f.Inflight.SampleName, f.Engine)!;
        AssertPlayback(takeoff, f.Engine, SoundTracking.FollowDontDie, false, 0);
        AssertPlayback(inflight, f.Engine, SoundTracking.FollowDontDie, true, .02f);
        Assert.Equal(0, inflight.SubVolume); Assert.Equal(1, inflight.FadeDestination);
        f.Tick(); Assert.Equal(.02f, inflight.SubVolume);
        f.Engine.Morph(); // the transition guard must not choose or admit more effects
        Assert.Equal(2, f.Engine.Context.Crt.Calls); Assert.Equal(2, f.Sounds.EventCount);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void RejectedMorphsConsumeNoRandomDrawAndAdmitNothing(int guard)
    {
        var f = new Fixture();
        switch (guard)
        {
            case 0: f.Engine.FlightModeActive = false; break;
            case 1: f.Engine.State = BattleEngineState.MorphingIntoWalker; break;
            case 2: f.Engine.WalkerPart!.DoingDashCount = 1; break;
            case 3: f.Engine.JetPart!.DoingLoop = true; break;
            case 4: f.Engine.Energy = .99f; break;
        }
        f.Engine.Morph();
        Assert.Empty(f.Lookups); Assert.Empty(f.Sounds.Events); Assert.Equal(0, f.Engine.Context.Crt.Calls);
    }

    [Fact]
    public void LandingFadesExistingLoopBeforeAdmittingLandingAndTakeoffRampsItBackWithoutAnotherLoopDraw()
    {
        var f = new Fixture(); f.Engine.Morph(); f.Tick(); f.Tick();
        var loop = f.Sounds.FindPlayingSample(f.Inflight.SampleName, f.Engine)!;
        float volume = loop.SubVolume;
        f.OnLookup = () => { Assert.Equal(0, loop.FadeDestination); Assert.Equal(-.02f, loop.Fade); };
        f.Engine.State = BattleEngineState.Jet; // authored caller state; no claim of elapsed transition acceptance
        f.Engine.Morph();
        Assert.Equal(BattleEngineState.MorphingIntoWalker, f.Engine.State);
        Assert.Equal(3, f.Engine.Context.Crt.Calls); Assert.Equal(volume, loop.SubVolume);
        AssertPlayback(f.Sounds.FindPlayingSample("BE Engines(land).wav", f.Engine)!, f.Engine,
            SoundTracking.FollowDontDie, false, 0);
        f.OnLookup = null;
        f.Engine.State = BattleEngineState.Walker; f.Engine.Morph();
        Assert.Equal(4, f.Engine.Context.Crt.Calls); // takeoff only, no duplicate inflight draw
        Assert.Equal(1, loop.FadeDestination); Assert.Equal(.02f, loop.Fade);
        Assert.Single(f.Sounds.Events, e => ReferenceEquals(e.Sample, f.InflightSample));
    }

    [Theory]
    [InlineData(false, 2)] [InlineData(true, 3)]
    public void InflightOnceRejectsOwnerlessSameSampleUnlessAuthoredLoopingClearsIt(bool authoredLoop, int count)
    {
        var f = new Fixture(authoredLoop);
        var ownerless = f.Sounds.PlaySample(f.InflightSample, null, new SoundPlayback(Repeat: true));
        Assert.NotNull(ownerless);
        Assert.False(f.Sounds.IsEffectPlaying(f.Inflight, f.Engine));
        f.Engine.Morph();
        Assert.Equal(2, f.Engine.Context.Crt.Calls); // effect selection precedes the once admission test
        Assert.Equal(count, f.Sounds.EventCount);
        Assert.Equal(authoredLoop, f.Sounds.FindPlayingSample(f.Inflight.SampleName, f.Engine) is not null);
    }

    [Fact]
    public void PlayingChainedVariantSuppressesAdmissionButDoesNotFadeAnAbsentFirstName()
    {
        var f = new Fixture();
        f.Inflight.Chained = new SoundEffect("variant", 0, "variant.wav");
        var variant = f.Sounds.PlaySample(new SampleMetadata("variant.wav", 88200), f.Engine,
            new SoundPlayback(Volume: .4f, Repeat: true, Fade: -.02f))!;
        Assert.True(f.Sounds.IsEffectPlaying(f.Inflight, f.Engine));
        f.Engine.Morph();
        Assert.Equal(1, f.Engine.Context.Crt.Calls);
        Assert.Equal(2, f.Sounds.EventCount);
        Assert.Equal(0, variant.FadeDestination); Assert.Equal(-.02f, variant.Fade);
        Assert.Null(f.Sounds.FindPlayingSample(f.Inflight.SampleName, f.Engine));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void AllDirectionalDashesPlayMainOwnedFollowAndDieBeforeChargeLossAndOnlyOnce(int direction)
    {
        var f = new Fixture(); BattleEngine be = f.Engine; var w = be.WalkerPart!;
        float start = be.Context.Events.Time - .15f;
        switch (direction)
        {
            case 0: w.LastStartHardBackwardTime = start; break;
            case 1: w.LastStartHardForwardTime = start; break;
            case 2: w.LastStartHardRightTime = start; break;
            case 3: w.LastStartHardLeftTime = start; break;
        }
        f.OnLookup = () => { Assert.Equal(.8f, w.PrimaryWeapon!.ChargeValue); Assert.Equal(9, be.ZoomOutTime); };
        void Invoke()
        {
            switch (direction)
            {
                case 0: w.Forward(-1); break; case 1: w.Backward(1); break;
                case 2: w.StrafeLeft(-1); break; case 3: w.StrafeRight(1); break;
            }
        }
        Invoke();
        Assert.Equal(1, be.Context.Crt.Calls); Assert.Single(f.Sounds.Events);
        AssertPlayback(f.Sounds.Events.Single(), be, SoundTracking.FollowAndDie, false, 0);
        Assert.Equal(0, w.PrimaryWeapon!.ChargeValue); Assert.Equal(0, be.ZoomOutTime);
        Assert.Equal(15, w.DoingDashCount);
        f.OnLookup = () => Assert.Fail("Repeated held dash must not select another effect.");
        w.PrimaryWeapon.ChargeValue = .6f;
        Invoke();
        Assert.Equal(1, be.Context.Crt.Calls); Assert.Single(f.Sounds.Events); Assert.Equal(.6f, w.PrimaryWeapon.ChargeValue);
    }
    [GameDataFact]
    public void LoadedLevel100ObjectsUseInstalledEffectsAndSharedBackendForMorph()
    {
        // Actual LevelSession consumer with installed data, but an authored backend/clock profile and direct
        // method calls. This does not advance the tutorial or establish player/audio acceptance.
        var session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        var be = Assert.IsType<BattleEngine>(session.Game.GetPlayer(0)!.BattleEngine);
        SoundEffects sounds = session.Context.SoundEffects!;
        Assert.Empty(sounds.Events);
        var bank = SampleBankMetadata.Read(GameData.Read(@"data\sounds\sounds_english_pc.xap").Bytes);
        sounds.SampleLookup = (name, _) => bank.Find(name); // explicit English profile
        var backend = new VirtualSoundEventBackend(sounds, new(1, 12, true, false), () => 0, _ => true,
            () => new SoundCameraState(be.Pos, be.Pos, FMatrix.Identity),
            owner => owner is Thing t ? new SoundOwnerSpatialState(t.Pos, default) : null);
        sounds.ConfigureEventKernel(backend, () => 0, () => false,
            Enumerable.Repeat(new SoundEventSeed(-1, default), 256).ToArray(), backend.Profile.SoundRate);
        backend.PrepareStatus();
        be.EnableFlightMode(); be.Energy = be.Configuration!.Energy;
        long before = session.Context.Crt.Calls;
        be.Morph();
        Assert.Equal(BattleEngineState.MorphingIntoJet, be.State);
        Assert.Equal(before + 2, session.Context.Crt.Calls);
        var takeoff = sounds.FindPlayingSample(sounds.Find("BE Engines(takeoff)")!.SampleName, be)!;
        var inflight = sounds.FindPlayingSample(sounds.Find("BE Engines(in-flight)")!.SampleName, be)!;
        Assert.Same(bank.Find(takeoff.Sample.Name), takeoff.Sample);
        Assert.Same(bank.Find(inflight.Sample.Name), inflight.Sample);
        Assert.False(takeoff.Looping); Assert.True(inflight.Looping);
        // Original PlayEffect004e1981..004e1999, PC24: FILD(40) * 1 * float(.01), not literal .4.
        Assert.Equal(0x3eccccccu, BitConverter.SingleToUInt32Bits(takeoff.MasterVolume));
        Assert.Equal(.5f, inflight.MasterVolume);
        Assert.Equal(.02f, inflight.Fade); Assert.Equal(0, inflight.SubVolume);
        Assert.Equal(SoundTracking.FollowDontDie, takeoff.Tracking);
        Assert.Equal(SoundTracking.FollowDontDie, inflight.Tracking);
        Assert.Equal(2, sounds.EventCount);
    }

}
