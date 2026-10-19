// SPDX-License-Identifier: GPL-3.0-only
// Original HandleEvent4002 branch0040c1ea..0040c263, pristine74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
// Boundary results independently reproduced by unchanged original code with observed sound-service calls.
using System.Text;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Sound;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class BattleEngineIncomingVoiceTests
{
    private sealed class Fixture
    {
        public ThingContext Context { get; }
        public BattleEngine Engine { get; }
        public SoundEffects Sound { get; }
        public bool Prerunning;
        public Fixture(string effect = "hud\\hud_incoming_missile")
        {
            var pool = new SPtrSetPool(); pool.Init(128);
            var events = new EventManager(); events.Init();
            Context = new(pool, events, new SeedUniformRandomNumberStream(123456), null!, new PhysicsRegistry());
            Sound = SoundEffects.Load(Encoding.Latin1.GetBytes($"103\n1\n{effect}\nsample\nunused\n100\n0\n0\n0\n0\n\n"));
            Context.SoundEffects = Sound;
            var sample = new SampleMetadata("sample", 100000); Sound.SampleLookup = (_, _) => sample;
            var backend = new VirtualSoundEventBackend(Sound, new(1, 12, true, false), () => 0, _ => true,
                () => new(default, default, FMatrix.Identity), _ => null);
            // Authored isolated seeds; no assumption about the runtime heap's actual initial channel values.
            Sound.ConfigureEventKernel(backend, () => 0, () => Prerunning,
                Enumerable.Repeat(new SoundEventSeed(-1, default), 256).ToArray(), backend.Profile.SoundRate);
            Engine = new(Context) { LastIncomingMissileVoice = -20, Pos = new(3000, 0, 0) };
        }
        public void Warn() => Engine.HandleEvent(new Event(UnitEvent.IncomingMissileWarning));
        public void Time(uint bits) => typeof(EventManager).GetProperty(nameof(EventManager.Time))!
            .SetValue(Context.Events, BitConverter.UInt32BitsToSingle(bits)); // authored scalar oracle input, no gameplay claim.
    }

    [Theory]
    [InlineData(0xc1a00000u, 0u, 1, 0x40000000u)]
    [InlineData(0u, 0u, 0, 0u)]
    [InlineData(0x80000000u, 0u, 0, 0x80000000u)]
    [InlineData(0u, 0x3f800000u, 1, 0x40400000u)]
    [InlineData(0x3f800000u, 0u, 0, 0x3f800000u)]
    [InlineData(0x3f7fffffu, 0x3f800000u, 1, 0x40400000u)]
    [InlineData(0x3f800001u, 0x3f800000u, 0, 0x3f800001u)]
    [InlineData(0x7fc00000u, 0u, 1, 0x40000000u)]
    [InlineData(0u, 0x7fc00000u, 1, 0x7fc00000u)]
    [InlineData(0xff800000u, 0u, 1, 0x40000000u)]
    [InlineData(0u, 0x7f800000u, 1, 0x7f800000u)]
    [InlineData(0x7f800000u, 0x7f800000u, 0, 0x7f800000u)]
    [InlineData(0u, 0x4c000000u, 1, 0x4c000000u)]
    [InlineData(0xbf800000u, 0xbf000000u, 1, 0x3fc00000u)]
    public void OriginalWarningBranchAndRoundedTimestamp(uint last, uint time, int calls, uint after)
    {
        var f = new Fixture(); f.Engine.LastIncomingMissileVoice = BitConverter.UInt32BitsToSingle(last); f.Time(time);
        f.Warn();
        Assert.Equal(calls, f.Context.Crt.Calls);
        Assert.Equal(after, BitConverter.SingleToUInt32Bits(f.Engine.LastIncomingMissileVoice));
        Assert.Equal(calls, f.Sound.EventCount);
        if (calls == 0) return;
        var e = Assert.Single(f.Sound.Events);
        Assert.Same(f.Engine, e.Owner); Assert.Equal(.45f, e.MasterVolume);
        Assert.Equal(SoundTracking.FollowDontDie, e.Tracking); Assert.True(e.IgnoreOwnerPosition);
        Assert.Equal(SoundKind.Game, e.Kind); Assert.False(e.Looping); Assert.False(e.InformOwner);
        Assert.Equal(1, e.Pitch); Assert.Equal(0, e.StartPoint); Assert.Equal(-1, e.EndPoint);
    }

    [Fact]
    public void RealSchedulerSuppressesRepeatedWarningUntilStrictlyAfterDeadline()
    {
        var f = new Fixture(); var events = f.Context.Events;
        events.AddEvent(UnitEvent.IncomingMissileWarning, f.Engine, EventManager.NextFrame);
        events.AddEvent(UnitEvent.IncomingMissileWarning, f.Engine, EventManager.NextFrame);
        events.Update();
        Assert.Equal(2u, events.TotalEventsProcessed); Assert.Equal(1, f.Context.Crt.Calls);
        float deadline = f.Engine.LastIncomingMissileVoice;
        while (events.Time < deadline) events.AdvanceTime();
        Assert.Equal(deadline, events.Time); f.Warn(); Assert.Equal(1, f.Context.Crt.Calls);
        events.AdvanceTime(); f.Warn(); Assert.Equal(2, f.Context.Crt.Calls);
        // Existing playback does not suppress this spoken warning: only the two-second timestamp does.
        Assert.Equal(2, f.Sound.EventCount); Assert.All(f.Sound.Events, e => Assert.True(e.Playing));
        Assert.False(f.Engine.PlayIncomingMissileSound);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MutedOrPrerunningWarningStillDrawsAndAdvancesItsDeadline(bool muted)
    {
        var f = new Fixture();
        if (muted) f.Sound.SetMasterVolume(0); else f.Prerunning = true;
        f.Warn(); Assert.Equal(1, f.Context.Crt.Calls); Assert.Empty(f.Sound.Events);
        Assert.Equal(2, f.Engine.LastIncomingMissileVoice);
        f.Warn(); Assert.Equal(1, f.Context.Crt.Calls);
    }

    [Fact]
    public void MissingEffectHasNoDrawButStillAdvancesTheWarningDeadline()
    {
        var f = new Fixture("unrelated"); f.Warn();
        Assert.Equal(0, f.Context.Crt.Calls); Assert.Empty(f.Sound.Events);
        Assert.Equal(2, f.Engine.LastIncomingMissileVoice);
    }

    [GameDataFact]
    public void LoadedBattleEngineStartsWithTheNativeMinusTwentyDeadline()
    {
        var session = LevelSession.Load(100, GameData.Read);
        Assert.Equal(-20, Assert.IsType<BattleEngine>(session.Game.GetPlayer(0)!.BattleEngine).LastIncomingMissileVoice);
    }
}
