// SPDX-License-Identifier: GPL-3.0-only
// Pristine PC SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750:
// KillSamplesForThing004e1130 exact owner+playing gates; Unit.Shutdown004f95d0 calls it before other teardown.
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Sound;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class UnitSoundLifetimeTests
{
    private static ThingContext Context()
    {
        var pool = new SPtrSetPool(); pool.Init(128);
        var events = new EventManager(); events.Init();
        var c = new ThingContext(pool, events, new SeedUniformRandomNumberStream(123456), null!, new PhysicsRegistry());
        c.SoundEffects = SoundEffects.Load("103\n0\n"u8.ToArray());
        return c;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BattleEngineShutdownStopsOnlyItsPlayingLoopsIncludingSilentPausedFades(bool active)
    {
        ThingContext c = Context(); SoundEffects sounds = c.SoundEffects!;
        var be = new BattleEngine(c) { Active = active }; var other = new BattleEngine(c);
        var shared = new SoundEffect("engine", 0, "same sample", looping: true);
        var different = new SoundEffect("warning", 0, "other sample", looping: false);
        var own = sounds.PlayRepeatingEffect(shared, be, c.Crt)!;
        var unrelated = sounds.PlayRepeatingEffect(shared, other, c.Crt)!;
        var silent = sounds.PlayRepeatingEffect(different, be, c.Crt, fade: .1f)!;
        var alreadyStopped = sounds.PlayRepeatingEffect(new SoundEffect("stopped", 0), be, c.Crt)!;
        sounds.StopRepeatingSample(alreadyStopped.Effect, be);
        sounds.AdvanceLoopStatus(paused: true);
        Assert.Equal(0, silent.SubVolume); Assert.Null(alreadyStopped.Owner);
        long draws = c.Crt.Calls; uint rng = c.Crt.HoldRand;

        be.Shutdown();

        Assert.Same(unrelated, Assert.Single(sounds.RepeatingEffects)); Assert.Same(other, unrelated.Owner);
        Assert.Null(own.Owner); Assert.Null(silent.Owner);
        Assert.False(sounds.IsRepeatingEffectPlaying(shared, be));
        Assert.False(sounds.IsRepeatingEffectPlaying(different, be));
        Assert.True(sounds.IsRepeatingEffectPlaying(shared, other));
        Assert.Equal(draws, c.Crt.Calls); Assert.Equal(rng, c.Crt.HoldRand);
        sounds.KillSamplesForThing(be); // no old event is revived or stopped a second time
        Assert.Same(unrelated, Assert.Single(sounds.RepeatingEffects)); Assert.Equal(draws, c.Crt.Calls);
    }

    [Fact]
    public void ExplicitStopAndCompletedFadeReleaseTheirOwnerWhileExactZeroRemainsPlaying()
    {
        ThingContext c = Context(); SoundEffects sounds = c.SoundEffects!; object owner = new();
        var effect = new SoundEffect("fade", 0);
        var fading = sounds.PlayRepeatingEffect(effect, owner, c.Crt)!;
        sounds.FadeRepeatingSample(effect, owner, 0, .5f);
        sounds.AdvanceLoopStatus(false); sounds.AdvanceLoopStatus(false);
        Assert.Equal(0, fading.SubVolume); Assert.Same(owner, fading.Owner); Assert.Single(sounds.RepeatingEffects);
        long draws = c.Crt.Calls;
        sounds.AdvanceLoopStatus(false);
        Assert.Empty(sounds.RepeatingEffects); Assert.Null(fading.Owner); Assert.Equal(draws, c.Crt.Calls);
    }
}
