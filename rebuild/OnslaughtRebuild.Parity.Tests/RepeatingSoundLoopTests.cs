// SPDX-License-Identifier: GPL-3.0-only
// Retail MainLoop 0046eff6 / SOUND.UpdateStatus 004e1b20, specimen 74154bfa.
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Sound;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class RepeatingSoundLoopTests
{
    [GameDataFact]
    public void FadeAdvancesPerMainLoopNotPresentAndOrdinaryPauseSuspendsIt()
    {
        var session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        while (session.PresentCount < 3000) session.AdvanceToNextPresent();
        SoundEffects sounds = session.Context.SoundEffects!;
        var effect = new SoundEffect("loop cadence fixture", 0, "loop cadence fixture");
        object owner = new();
        var playing = sounds.PlayRepeatingEffect(effect, owner, session.Context.Crt)!;
        sounds.FadeRepeatingSample(effect, owner, 0, .001f);
        float expected = 1;
        int updates = 0, repeatedRenders = 0;
        void Step()
        {
            int before = session.Game.UpdateFrameNumber;
            session.AdvanceToNextPresent();
            int delta = session.Game.UpdateFrameNumber - before;
            Assert.InRange(delta, 0, 1);
            if (delta == 0) repeatedRenders++;
            else if (!session.Game.PausedAllGameSounds) { expected -= .001f; updates++; }
            Assert.Equal(expected, playing.SubVolume);
        }

        for (int i = 0; i < 60; i++) Step();
        Assert.True(updates > 0);
        Assert.True(repeatedRenders > 0);
        session.Input.Input.PostTapeKey(0x1b, 0x01, true);
        for (int i = 0; i < 8 && !session.Game.Paused; i++) Step();
        Assert.True(session.Game.Paused);
        Assert.True(session.Game.PausedAllGameSounds);
        session.Input.Input.PostTapeKey(0x1b, 0x01, false);
        float pausedVolume = playing.SubVolume;
        for (int i = 0; i < 30; i++) Step();
        Assert.Equal(pausedVolume, playing.SubVolume);
        session.Input.Input.PostTapeKey(0x1b, 0x01, true);
        for (int i = 0; i < 8 && session.Game.Paused; i++) Step();
        Assert.False(session.Game.Paused);
        Assert.False(session.Game.PausedAllGameSounds);
        Assert.True(playing.SubVolume < pausedVolume);
        session.Input.Input.PostTapeKey(0x1b, 0x01, false);
    }
}
