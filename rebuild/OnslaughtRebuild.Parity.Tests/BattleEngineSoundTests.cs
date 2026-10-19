// SPDX-License-Identifier: GPL-3.0-only
// Incoming gate00409b3f..00409b99 and HUD PlayEffect arguments in pristine74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
using System.Text;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Sound;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class BattleEngineSoundTests
{
    private sealed class Fixture
    {
        public long Blocks;
        public ThingContext Context { get; }
        public BattleEngine Engine { get; }
        public SoundEffects Sound { get; }
        public VirtualSoundEventBackend Backend { get; }
        public Fixture(string effect = "BE Incoming Missile", bool configured = true)
        {
            var pool = new SPtrSetPool(); pool.Init(128);
            var events = new EventManager(); events.Init();
            Context = new(pool, events, new SeedUniformRandomNumberStream(123456), null!, new PhysicsRegistry());
            Sound = SoundEffects.Load(Encoding.Latin1.GetBytes($"103\n1\n{effect}\nsample\nunused\n100\n0\n0\n0\n0\n\n"));
            Context.SoundEffects = Sound;
            var sample = new SampleMetadata("sample", 6944); Sound.SampleLookup = (_, _) => sample;
            Backend = new(Sound, new(1, 12, true, false), () => Blocks, _ => true,
                () => new(default, default, FMatrix.Identity), _ => null);
            // Authored isolated fixture seeds; this is not a claimed runtime heap profile.
            if (configured) Sound.ConfigureEventKernel(Backend, () => 0, () => false,
                Enumerable.Repeat(new SoundEventSeed(-1, default), 256).ToArray(), Backend.Profile.SoundRate);
            Engine = new(Context) { Life = 20, Energy = 8, Pos = new(3000, 0, 0) };
        }
        public void Warn() { Engine.PlayIncomingMissileSound = true; Engine.HandleSounds(); }
    }

    [Fact]
    public void WarningRetainsOwnerButIgnoresPositionAndRearmsOnlyAfterActualCompletionPoll()
    {
        var f = new Fixture(); f.Warn();
        var e = Assert.Single(f.Sound.Events);
        Assert.Same(f.Engine, e.Owner); Assert.True(e.IgnoreOwnerPosition);
        Assert.Equal(default, e.Position); Assert.Equal(SoundTracking.FollowDontDie, e.Tracking);
        Assert.Equal(SoundKind.Game, e.Kind); Assert.Equal(.45f, e.MasterVolume);
        Assert.False(e.Looping); Assert.False(f.Engine.PlayIncomingMissileSound);
        Assert.Equal(1, f.Context.Crt.Calls);
        f.Blocks = 7; f.Sound.UpdateStatus(); f.Warn(); Assert.Equal(1, f.Context.Crt.Calls);
        f.Blocks = 8; f.Warn(); Assert.Equal(1, f.Context.Crt.Calls); // no implicit device poll in IsEffectPlaying
        f.Sound.UpdateStatus(); Assert.Empty(f.Sound.Events);
        f.Warn(); Assert.Equal(2, f.Context.Crt.Calls); Assert.Single(f.Sound.Events);
    }

    [Fact]
    public void RejectedMasterMutedWarningStillConsumesEffectDrawAndClearsRadarFlag()
    {
        var f = new Fixture(); f.Sound.SetMasterVolume(0); f.Warn();
        Assert.Empty(f.Sound.Events); Assert.Equal(1, f.Context.Crt.Calls);
        Assert.False(f.Engine.PlayIncomingMissileSound);
    }

    [Fact]
    public void MissingWarningEffectClearsFlagWithoutDrawButLegacyLifetimeStillRefuses()
    {
        var missing = new Fixture("unrelated"); missing.Warn();
        Assert.Empty(missing.Sound.Events); Assert.Equal(0, missing.Context.Crt.Calls);
        Assert.False(missing.Engine.PlayIncomingMissileSound);
        var legacy = new Fixture(configured: false);
        Assert.Throws<NotSupportedException>(legacy.Warn);
        Assert.True(legacy.Engine.PlayIncomingMissileSound); Assert.Equal(0, legacy.Context.Crt.Calls);
    }

    [Fact]
    public void HumQueriesTheSameOwnerBeforeStartingAnotherHudEvent()
    {
        var f = new Fixture("BE On 02"); f.Engine.HandleSounds(); f.Engine.HandleSounds();
        Assert.Single(f.Sound.Events); Assert.Equal(1, f.Context.Crt.Calls);
        f.Blocks = 8; f.Sound.UpdateStatus(); f.Engine.HandleSounds();
        Assert.Single(f.Sound.Events); Assert.Equal(2, f.Context.Crt.Calls);
    }
}
