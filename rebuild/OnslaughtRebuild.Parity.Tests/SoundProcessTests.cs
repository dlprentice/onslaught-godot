// SPDX-License-Identifier: GPL-3.0-only
// Authored integration controls for the explicitly admitted virtual sound process. No retail pool defaults implied.
using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Draw;
using OnslaughtRebuild.Parity.Flow.Menus;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Script;
using OnslaughtRebuild.Parity.Sound;
using OnslaughtRebuild.Parity.Things;
using ParityGame = OnslaughtRebuild.Parity.Game.Game;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class SoundProcessTests
{
    private sealed class Text : IGameText
    {
        public string? GetString(uint id) => "message";
        public string? GetSoundName(uint id) => "voice";
        public string LanguageName => "english";
    }

    private static byte[] VoiceFile(int frames = 3472)
    {
        byte[] bytes = new byte[40]; "OggS"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(6), frames);
        bytes[26] = 1; bytes[27] = 12; bytes[28] = 1; bytes[29] = (byte)'v'; bytes[39] = 1;
        return bytes; // authored header-only stream: this test does not decode or play PCM.
    }

    private static VirtualSoundProcessOptions Options(int channels = 12) => new(new(1, channels, true, false),
        Enumerable.Repeat(new SoundEventSeed(-1, default), 256).ToArray(), (_, _) => null, _ => false, _ => null);

    private sealed class Fixture
    {
        public PlatformClock Clock { get; } = new();
        public SoundEffects Sounds { get; } = new();
        public SoundProcess Process { get; }
        public ThingContext Context { get; }
        public VirtualVoice Voice { get; }
        public ScriptGameServices Services { get; }
        public MessageBox Box { get; }
        public Fixture(int channels = 12)
        {
            var pool = new SPtrSetPool(); pool.Init(128);
            var events = new EventManager(); events.Init();
            Context = new(pool, events, new SeedUniformRandomNumberStream(123456), null!, new PhysicsRegistry());
            Context.Game = new ParityGame(Clock, events); Context.SoundEffects = Sounds;
            Process = new(Sounds, Clock, Options(channels)); Process.BindLevel(Context);
            Voice = new(Clock, _ => ("authored", VoiceFile())); Voice.BindSoundProcess(Process);
            Box = new(Context, new Text(), Voice);
            var runtime = ScriptRuntime.Of(Context); runtime.MessageBox = Box;
            Services = new(NullGameServices.Instance) { Runtime = runtime };
            Clock.PresentDevice(); Clock.GetSysTimeFloat(); Process.InitialiseAfterFirstPresent(.8f);
        }
        public ScriptMessage Play()
        {
            var message = new ScriptMessage("name", "text", 1, 0, null, null, 0);
            Voice.StartLoad("authored.ogg"); Voice.PlayLoaded(message); return message;
        }
        public void Presents(int n) { for (int i = 0; i < n; i++) Clock.Present(); }
    }

    [Fact]
    public void ConstructionAndRejectedConfigurationDoNotMoveTheClockOrigin()
    {
        var clock = new PlatformClock(); var sounds = new SoundEffects();
        Assert.Throws<ArgumentException>(() => new SoundProcess(sounds, clock, Options() with { Seeds = [] }));
        var process = new SoundProcess(sounds, clock, Options());
        Assert.Same(process.SampleLookup, sounds.SampleLookup);
        Assert.Throws<InvalidOperationException>(() => process.InitialiseAfterFirstPresent(.8f));
        clock.PresentDevice(); Assert.Equal(0, clock.GetSysTimeFloat());
        process.InitialiseAfterFirstPresent(.8f);
        Assert.Equal(0, sounds.LastProcessTime); Assert.Equal(.8f, sounds.MasterVolume);
        Assert.Throws<InvalidOperationException>(() => process.InitialiseAfterFirstPresent(.8f));
        clock.Present(); Assert.Equal(0x3d088880u, BitConverter.SingleToUInt32Bits(clock.GetSysTimeFloat()));
    }

    [Fact]
    public void ConfiguredVoiceUsesSharedEventAndCompletesOnlyAtManagerStatusBoundary()
    {
        var f = new Fixture(); ScriptMessage message = f.Play();
        var e = Assert.Single(f.Sounds.Events);
        Assert.Same(message, e.Owner); Assert.Equal(.42f, e.MasterVolume);
        Assert.Equal(SoundTracking.None, e.Tracking); Assert.True(e.InformOwner); Assert.True(e.IgnoreOwnerPosition);
        Assert.Throws<InvalidOperationException>(f.Voice.UpdateStatus);
        f.Presents(3); Assert.False(message.FinishedPlaying); Assert.True(e.Playing);
        f.Services.AfterUpdate(); Assert.True(message.FinishedPlaying); Assert.Empty(f.Sounds.Events);
        Assert.Equal((0L, 9L), (f.Voice.Trace[0].PlayedAt, f.Voice.Trace[0].StoppedAt));
    }

    [Fact]
    public void PauseSuspendsActualVoiceBufferAndResumeRetainsItsPosition()
    {
        var f = new Fixture(); ScriptMessage message = f.Play();
        f.Presents(1); f.Services.SetAllSamplesPaused(true);
        uint cursor = f.Process.Backend.InspectChannel(0)!.Value.PlayCursor;
        f.Presents(12); f.Services.AfterUpdate();
        Assert.False(message.FinishedPlaying); Assert.True(Assert.Single(f.Sounds.Events).Paused);
        Assert.Equal(cursor, f.Process.Backend.InspectChannel(0)!.Value.PlayCursor);
        f.Services.SetAllSamplesPaused(false); f.Presents(1); f.Services.AfterUpdate();
        Assert.False(message.FinishedPlaying);
        f.Presents(1); f.Services.AfterUpdate(); Assert.True(message.FinishedPlaying);
    }

    [Fact]
    public void ChannelLessVoiceUsesNativeEventLengthInsteadOfTheOldDurationTimer()
    {
        var f = new Fixture(channels: 0); ScriptMessage message = f.Play();
        Assert.Equal(-1, Assert.Single(f.Sounds.Events).Channel);
        f.Presents(3); f.Services.AfterUpdate(); Assert.False(message.FinishedPlaying); // old isolated voice would finish.
        f.Presents(2); f.Services.AfterUpdate(); Assert.True(message.FinishedPlaying);
    }

    [Fact]
    public void MessageBoxDeletionReleasesPendingAndPlayingOwnersWithoutResettingPool()
    {
        var f = new Fixture(); ScriptMessage playing = f.Play(); f.Box.AddMessage(playing);
        var e = Assert.Single(f.Sounds.Events); int slot = e.Slot;
        f.Box.Delete(); Assert.True(playing.FinishedPlaying); Assert.Null(e.Owner); Assert.False(e.Playing);
        Assert.Equal(1, f.Sounds.EventCount); // native deletion is deferred until UpdateStatus.
        Assert.Equal(0, playing.DeletionCount); Assert.Equal(0, f.Box.Queue.Size);
        f.Box.Delete(); f.Services.AfterUpdate(); Assert.Empty(f.Sounds.Events);
        var replacement = new VirtualVoice(f.Clock, _ => ("authored", VoiceFile())); replacement.BindSoundProcess(f.Process);
        replacement.StartLoad("next.ogg"); replacement.PlayLoaded(null);
        Assert.Equal(slot, Assert.Single(f.Sounds.Events).Slot);
    }

    [Fact]
    public void DestroyedMessageNeverReceivesStaleCompletionAndLevelBindingRejectsOverlap()
    {
        var f = new Fixture(); ScriptMessage message = f.Play(); message.Delete();
        f.Presents(3); f.Services.AfterUpdate(); Assert.False(message.FinishedPlaying);
        var pool = new SPtrSetPool(); pool.Init(64); var events = new EventManager(); events.Init();
        var next = new ThingContext(pool, events, new SeedUniformRandomNumberStream(123456), null!, new PhysicsRegistry())
            { SoundEffects = f.Sounds, Game = new ParityGame(f.Clock, events) };
        Assert.Throws<InvalidOperationException>(() => f.Process.BindLevel(next));
        f.Process.ReleaseLevel(f.Context); f.Process.BindLevel(next);
        Assert.Same(f.Sounds, next.SoundEffects); Assert.True(f.Process.Initialised);
    }

    [Fact]
    public void FrontEndStatusRunsOnceBeforePagesAndNotForEachRenderAttempt()
    {
        var f = new Fixture(); f.Process.ReleaseLevel(f.Context);
        var front = Flow.FrontEndTests.NewFrontEnd(f.Clock); front.SoundProcess = f.Process;
        Assert.True(front.Begin(FrontEndPage.Main));
        var sample = new SampleMetadata("fade", 88200);
        // No actual buffer is available in this authored profile, but the channel/event/fade owner still runs.
        var e = f.Sounds.PlaySample(sample, null, new SoundPlayback(Fade: .1f, Repeat: true))!;
        int renders = 0; front.BeginScene = () => ++renders == 2;
        front.Step();
        Assert.Equal(2, renders); Assert.Equal(.1f, e.SubVolume);
    }

    [Fact]
    public void FrontEndVolumeRefreshCanCompleteAnElapsedVoiceWithoutAnExtraStatusTick()
    {
        var f = new Fixture(); ScriptMessage message = f.Play();
        var front = Flow.FrontEndTests.NewFrontEnd(f.Clock); front.SoundProcess = f.Process;
        f.Presents(3);
        float lastTime = f.Sounds.LastProcessTime;
        ((IMenuHost)front).SetSoundVolume(.5f);
        Assert.True(message.FinishedPlaying);
        Assert.Equal(.5f, front.Career.Record.SoundVolume);
        Assert.Equal(.5f, f.Sounds.MasterVolume);
        Assert.Equal(lastTime, f.Sounds.LastProcessTime); // gain polling is not SOUND.UpdateStatus.
    }

    private static void CheckMenuVolume(PauseMenu menu, IMenuHost host, SoundEffects sounds)
    {
        var scale = Assert.IsType<ScaleMenuItem>(menu.Lists[3].Items[0]);
        var sample = new SampleMetadata("authored-loop", 100000);
        // Repeat admits this authored event during pre-run; it exercises gain/admission without requiring PCM.
        var e = sounds.PlaySample(sample, null, new SoundPlayback(Volume: 1, Repeat: true))!;
        int initialGain = e.CurrentVolume;
        scale.Value = 0; menu.MenuItemChanged(scale);
        Assert.Equal(0, host.Career.Record.SoundVolume); Assert.Equal(0, sounds.MasterVolume);
        Assert.True(e.CurrentVolume < initialGain);
        Assert.Null(sounds.PlaySample(sample, null, new SoundPlayback(Repeat: true)));
        int mutedGain = e.CurrentVolume;
        scale.Value = 5; menu.MenuItemChanged(scale);
        Assert.Equal(.5f, host.Career.Record.SoundVolume); Assert.Equal(.5f, sounds.MasterVolume);
        Assert.True(e.CurrentVolume > mutedGain);
        Assert.NotNull(sounds.PlaySample(sample, null, new SoundPlayback(Repeat: true)));
    }

    [GameDataFact]
    public void FrontEndOptionsSliderChangesTheSharedGainAndMuteAdmission()
    {
        var run = new GameRun(GameData.Read, null, new Flow.RetailChainTests.InstalledFilms(), skipFmv: true,
            soundOptions: Options());
        run.Clock.PresentDevice(); run.Clock.GetSysTimeFloat(); run.SoundProcess!.InitialiseAfterFirstPresent(1);
        var optionsPage = Assert.IsType<OnslaughtRebuild.Parity.Flow.Pages.OptionsPage>(run.FrontEnd[FrontEndPage.Options]);
        optionsPage.Init(); optionsPage.TransitionNotification(FrontEndPage.Main);
        CheckMenuVolume(optionsPage.Menu!, run.FrontEnd, run.Sounds);
    }

    [GameDataFact]
    public void LevelPauseSliderChangesTheSharedGainAndMuteAdmission()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, soundOptions: Options());
        session.AdvanceToNextPresent();
        CheckMenuVolume(session.Input.PauseMenu!, session.Input.PauseMenuHost!, session.SoundProcess!.Effects);
    }

    private sealed class StopRun : Exception;

    [GameDataFact]
    public void StandaloneRestartRetainsOwnerAndClockAndReleasesOldVoiceBeforeRebinding()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, soundOptions: Options());
        SoundProcess process = session.SoundProcess!;
        Assert.False(process.Initialised); Assert.Equal(0, session.Clock.PresentCount);
        session.AdvanceToNextPresent(); Assert.Equal(3, session.Clock.PresentCount);
        Assert.True(process.Initialised); Assert.Equal(0, process.Effects.LastProcessTime);
        var box = ScriptRuntime.Of(session.Context).MessageBox!;
        var voice = Assert.IsType<VirtualVoice>(box.Voice);
        // An authored voice uses the real manager to exercise release, not a mission event or native acceptance tape.
        var authored = new VirtualVoice(session.Clock, _ => ("authored", VoiceFile(100000)));
        authored.BindSoundProcess(process);
        var owner = new ScriptMessage("owner", "text", 1, 0, null, null, 0);
        authored.StartLoad("authored.ogg"); authored.PlayLoaded(owner);
        var frames = new List<CanvasFrame>();
        session.Draw = new FrontEndDraw(new FrontEndTextures(GameData.Read))
        {
            Canvas = new RecordingCanvas(frames.Add),
        };
        session.Game.SetQuit(QuitType.RestartLevel);
        for (int i = 0; !session.Ended && i < 10; i++) session.AdvanceToNextPresent();
        Assert.True(session.Ended); Assert.True(owner.FinishedPlaying);
        Assert.DoesNotContain(process.Effects.Events, e => e.Playing && ReferenceEquals(e.Owner, owner));
        long present = session.Clock.PresentCount; float lastTime = process.Effects.LastProcessTime;
        LevelSession next = session.Restart();
        Assert.Same(session.Draw, next.Draw);
        Assert.Same(process, next.SoundProcess); Assert.Same(session.Context.SoundEffects, next.Context.SoundEffects);
        Assert.Same(session.Clock, next.Clock); Assert.True(next.Clock.PresentCount >= present);
        Assert.Equal(lastTime, process.Effects.LastProcessTime); // no fresh initialization/status at reload.
        Assert.Equal(1, process.Effects.GameSoundsMasterVolume);
        Assert.NotSame(voice, ScriptRuntime.Of(next.Context).MessageBox!.Voice);
        frames.Clear();
        next.AdvanceToNextPresent(); // the reload's 100% screen
        next.AdvanceToNextPresent(); // the first world render after Retry
        Assert.Contains(frames, frame => frame.Present == next.PresentCount);
    }

    [GameDataFact]
    public void FullBootConfiguresOnlyAfterFirstPresentOnTheSameClock()
    {
        var run = new GameRun(GameData.Read, null, new Flow.RetailChainTests.InstalledFilms(), skipFmv: false,
            soundOptions: Options());
        Assert.False(run.Sounds.EventKernelConfigured);
        run.OnPresented = () =>
        {
            if (run.Clock.PresentCount == 1) Assert.False(run.Sounds.EventKernelConfigured);
            if (run.Clock.PresentCount == 2)
            {
                Assert.True(run.Sounds.EventKernelConfigured); Assert.Equal(0, run.Sounds.LastProcessTime);
                Assert.Same(run.SoundProcess, run.FrontEnd.SoundProcess); throw new StopRun();
            }
        };
        Assert.Throws<StopRun>(() => run.Run());
    }
}
