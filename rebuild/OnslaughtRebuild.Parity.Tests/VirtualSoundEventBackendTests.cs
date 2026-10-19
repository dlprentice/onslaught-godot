// SPDX-License-Identifier: GPL-3.0-only
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Sound;
using SoundOwner = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class VirtualSoundEventBackendTests
{
    private sealed class Fixture
    {
        public long Blocks;
        public bool SourceAvailable = true;
        public SoundCameraState? Camera;
        public SoundOwnerSpatialState? OwnerSpatial;
        public SoundEffects Sound { get; } = new();
        public VirtualSoundEventBackend Backend { get; }
        public Fixture(int rate = 1, bool continuous = true, bool flip = false)
        {
            Backend = new(Sound, new(rate, 12, continuous, flip), () => Blocks, _ => SourceAvailable,
                () => Camera, _ => OwnerSpatial);
            Sound.ConfigureEventKernel(Backend, () => 0, () => false,
                Enumerable.Repeat(new SoundEventSeed(-1, default), 256).ToArray(), Backend.Profile.SoundRate);
        }
        public SoundEffects.SoundEvent Event(uint bytes = 6944) => new(Sound, 0)
        { Sample = new("test", bytes), Channel = 0, Playing = true, Pitch = 1, EndPoint = -1, CurrentVolume = -1000 };
    }

    [Fact]
    public void RealIncomingBufferCompletesOnlyWhenManagerPollsItAtEightBlocks()
    {
        var f = new Fixture();
        var e = f.Sound.PlaySample(new("incoming", 6944), null, new SoundPlayback())!;
        f.Blocks = 7; f.Sound.UpdateStatus(); Assert.True(e.Playing);
        f.Blocks = 8; Assert.True(e.Playing); // Device clock alone does not run the event owner.
        f.Sound.UpdateStatus(); Assert.False(e.Playing); Assert.Empty(f.Sound.Events);
        Assert.False(f.Backend.HasBuffer(e.Channel));
    }

    [Fact]
    public void RestartGuardUsesOriginalBytesThenWrapsOffsetInConvertedBuffer()
    {
        var f = new Fixture(); var e = f.Event(); e.Time = .1f;
        Assert.True(f.Backend.Play(e));
        Assert.Equal(938u, f.Backend.InspectChannel(0)!.Value.PlayCursor);
        e.Time = 1;
        Assert.False(f.Backend.Play(e)); // An early return retains the duplicate and its cursor.
        Assert.Equal(938u, f.Backend.InspectChannel(0)!.Value.PlayCursor);
        Assert.True(e.Playing);
    }

    [Fact]
    public void PauseRetainsFractionalPositionResumeUsesItAndStopReleasesOnlyBuffer()
    {
        var f = new Fixture(); var e = f.Event(88200);
        Assert.True(f.Backend.Play(e)); f.Blocks = 1; f.Backend.Pause(e);
        Assert.Equal(440u, f.Backend.InspectChannel(0)!.Value.PlayCursor);
        f.Blocks = 100; f.Backend.Resume(e); f.Blocks = 101;
        Assert.Equal(882u, f.Backend.InspectChannel(0)!.Value.PlayCursor);
        f.Backend.Stop(e); Assert.Null(f.Backend.InspectChannel(0)); Assert.True(e.Playing); Assert.Equal(0, e.Channel);
    }

    [Fact]
    public void InvalidVolumeAndFrequencyRetainPreviousValuesAfterCatchUp()
    {
        var f = new Fixture(); var e = f.Event(88200); Assert.True(f.Backend.Play(e));
        e.CurrentVolume = -6000; f.Backend.Update(e); Assert.Equal(-10000, f.Backend.InspectChannel(0)!.Value.Volume);
        e.CurrentVolume = -6001; e.Pitch = 99f / 22050; f.Blocks = 1;
        Assert.False(f.Backend.Update(e)); var state = f.Backend.InspectChannel(0)!.Value;
        Assert.Equal(-10000, state.Volume); Assert.Equal(22050u, state.Frequency); Assert.Equal(440u, state.PlayCursor);
        e.Pitch = 2; f.Backend.Update(e); f.Blocks = 2;
        Assert.Equal(1322u, f.Backend.InspectChannel(0)!.Value.PlayCursor);
    }

    [Fact]
    public void PausedOrMissingBufferNeverReportsCompletionAndInitialPlayDoesNotPollCompletion()
    {
        var f = new Fixture(); var e = f.Event();
        Assert.False(f.Backend.Update(e)); Assert.True(e.Playing);
        Assert.True(f.Backend.Play(e)); f.Blocks = 8; e.Paused = true;
        Assert.False(f.Backend.Update(e)); e.Paused = false; Assert.True(f.Backend.Update(e));
        f.Backend.Stop(e); f.SourceAvailable = false; Assert.False(f.Backend.Play(e));
        Assert.False(f.Backend.HasBuffer(0)); Assert.True(e.Playing);
    }

    [Fact]
    public void NewDuplicateResetsOldPitchAndCursorWhenContinuousRateIsUnavailable()
    {
        var f = new Fixture(continuous: false); var e = f.Event(); e.Pitch = 2;
        Assert.True(f.Backend.Play(e)); f.Blocks = 1;
        Assert.Equal(22050u, f.Backend.InspectChannel(0)!.Value.Frequency);
        Assert.Equal(440u, f.Backend.InspectChannel(0)!.Value.PlayCursor);
        Assert.True(f.Backend.Play(e)); Assert.Equal(0u, f.Backend.InspectChannel(0)!.Value.PlayCursor);
    }

    [Fact]
    public void InitialPositionRetainsPositionButSpeakerFlipStillRunsOnEveryUpdate()
    {
        var f = new Fixture(flip: true) { OwnerSpatial = new(new(2, 3, 4), default) };
        var e = f.Event(); e.OwnerReader.SetReader(new SoundOwner()); e.Tracking = SoundTracking.InitialPosition;
        f.Backend.UpdatePosition(e, true); Assert.Equal(new FVector(-2, 3, 4), e.Position);
        f.OwnerSpatial = new(new(100, 100, 100), default);
        f.Backend.UpdatePosition(e, false); Assert.Equal(new FVector(2, 3, 4), e.Position);
    }

    [Fact]
    public void UnknownOwnerAndNonzeroSoundVelocityAreExplicitUnsupportedBoundaries()
    {
        var f = new Fixture(); var e = f.Event(); e.OwnerReader.SetReader(new SoundOwner()); e.Tracking = SoundTracking.FollowDontDie;
        Assert.Throws<NotSupportedException>(() => f.Backend.UpdatePosition(e, true));
        f.OwnerSpatial = new(new(2, 0, 0), new(1, 0, 0));
        Assert.Throws<NotSupportedException>(() => f.Backend.UpdatePosition(e, true));
        f.OwnerSpatial = new(new(2, 0, 0), default); f.Backend.UpdatePosition(e, true);
        Assert.Equal(new FVector(2, 0, 0), e.Position);
    }

    [Fact]
    public void DeferredPositionAppliesOnlyOnCommitAndCameraUsesActualTransform()
    {
        var f = new Fixture { Camera = new(new(1, 2, 3), default, FMatrix.Identity), OwnerSpatial = new(new(5, 7, 9), default) };
        var e = f.Event(); e.OwnerReader.SetReader(new SoundOwner()); e.Tracking = SoundTracking.FollowDontDie;
        Assert.True(f.Backend.Play(e)); Assert.Equal(new FVector(4, 5, 6), f.Backend.InspectChannel(0)!.Value.Position);
        f.OwnerSpatial = new(new(7, 9, 11), default); f.Backend.UpdatePosition(e, false); f.Backend.Update(e);
        var state = f.Backend.InspectChannel(0)!.Value;
        Assert.Equal(new FVector(4, 5, 6), state.Position); Assert.Equal(new FVector(6, 7, 8), state.PendingPosition);
        f.Backend.UpdatesDone(); Assert.Equal(new FVector(6, 7, 8), f.Backend.InspectChannel(0)!.Value.Position);
        f.Backend.PrepareStatus(); Assert.Equal(f.Camera!.Value, f.Backend.CameraSnapshot);
    }

    [RetailOracleFact("sound-event-backend")]
    public void NativeCpcOperationsUsingActualPortBuffersAgreeAcrossLifecycleSequences()
    {
        var actual = new List<string>();
        foreach ((string function, uint[] w, string expected) in RetailOracle.Cases("sound-event-backend"))
        {
            Assert.Equal("Backend", function); Assert.Equal(13 + checked((int)w[12]) * 3, w.Length);
            var f = new Fixture(unchecked((int)w[1]), w[3] != 0) { SourceAvailable = w[2] != 0 };
            var e = f.Event(w[0]); e.Playing = w[4] != 0; e.StartPoint = RetailOracle.F(w[5]); e.Time = RetailOracle.F(w[6]);
            e.EndPoint = RetailOracle.F(w[7]); e.Pitch = RetailOracle.F(w[8]); e.CurrentVolume = unchecked((int)w[9]);
            e.Looping = w[10] != 0; e.Paused = w[11] != 0;
            uint callbacks = 0, duplicates = 0; var fields = new List<uint>();
            for (int i = 13; i < w.Length; i += 3)
            {
                f.Blocks = (long)w[i] * 44100 / 1_000_000_000 / 441;
                switch (w[i + 1])
                {
                    case 0: if (f.Backend.Play(e)) { e.Playing = true; duplicates++; } break;
                    case 1: f.Backend.Pause(e); break;
                    case 2: f.Backend.Resume(e); break;
                    case 3: f.Backend.Stop(e); break;
                    case 4: if (f.Backend.Update(e)) { callbacks++; f.Sound.StopSoundEvent(e); } break;
                    case 5: e.Pitch = RetailOracle.F(w[i + 2]); break;
                    case 6: e.CurrentVolume = unchecked((int)w[i + 2]); break;
                    case 7: e.Paused = w[i + 2] != 0; break;
                    case 8: e.Time = RetailOracle.F(w[i + 2]); break;
                    case 9: e.StartPoint = RetailOracle.F(w[i + 2]); break;
                    case 10: e.EndPoint = RetailOracle.F(w[i + 2]); break;
                    case 11: e.Looping = w[i + 2] != 0; break;
                    case 12: f.SourceAvailable = w[i + 2] != 0; break;
                    case 13: break;
                    case 14: f.Backend.UpdateGlobals(); break;
                    case 15: f.Backend.UpdatesDone(); break;
                    default: throw new InvalidDataException("Unknown native backend operation.");
                }
                var c = f.Backend.InspectChannel(0);
                fields.AddRange([e.Playing ? 1u : 0u, c.HasValue ? 1u : 0u, c?.Status ?? 0, c?.PlayCursor ?? 0,
                    c?.WriteCursor ?? 0, c?.Frequency ?? 0, unchecked((uint)(c?.Volume ?? 0)), callbacks, duplicates]);
            }
            string line = "Backend " + string.Join(" ", fields.Select(v => v.ToString("x8")));
            Assert.True(line == expected, $"case {actual.Count}:\nours: {line}\nnative+port: {expected}"); actual.Add(line);
        }
        Assert.Equal(512, actual.Count); RetailOracle.WriteOurs("sound-event-backend", actual.ToArray());
    }

    [RetailOracleFact("sound-spatial-gain")]
    public void FourNativeInlineGainSitesKeepTheirIndividualDistanceAndProductOrders()
    {
        var actual = new List<string>(); var f = new Fixture(); var e = f.Event();
        foreach ((string function, uint[] w, string expected) in RetailOracle.Cases("sound-spatial-gain"))
        {
            Assert.Equal("Gain", function); e.Kind = (SoundKind)w[0]; e.Tracking = (SoundTracking)w[1];
            e.MasterVolume = RetailOracle.F(w[2]); e.SubVolume = RetailOracle.F(w[3]); f.Sound.SetMasterVolume(RetailOracle.F(w[4]));
            f.Sound.SetGameSoundsMasterVolume(RetailOracle.F(w[5])); f.Sound.MenuSoundsMasterVolume = RetailOracle.F(w[5]); e.Position = RetailOracle.V(w, 6);
            var fields = new List<uint>();
            foreach (var site in new[] { SoundVolumeSite.Start, SoundVolumeSite.Fade, SoundVolumeSite.TrackedOwner, SoundVolumeSite.MasterVolume })
            { var v = f.Backend.EvaluateVolumes(e, site); fields.Add(unchecked((uint)v.Volume)); fields.Add(unchecked((uint)v.Attenuated)); }
            string line = "Gain " + string.Join(" ", fields.Select(v => v.ToString("x8")));
            Assert.True(line == expected, $"case {actual.Count}:\nours: {line}\nretail: {expected}"); actual.Add(line);
        }
        Assert.Equal(46144, actual.Count); RetailOracle.WriteOurs("sound-spatial-gain", actual.ToArray());
    }

    [RetailOracleFact("sound-spatial-position")]
    public void NativeCameraTransformAndPanAgreeWithinTheExplicitZeroVelocityProfile()
    {
        int exact = 0, refused = 0;
        foreach ((string function, uint[] w, string expected) in RetailOracle.Cases("sound-spatial-position"))
        {
            Assert.Equal("Position", function);
            var f = new Fixture(flip: w[4] != 0)
            {
                Camera = w[19] == 0 ? null : new(RetailOracle.V(w, 20), default,
                    new FMatrix(RetailOracle.V(w, 23), RetailOracle.V(w, 26), RetailOracle.V(w, 29))),
                OwnerSpatial = new(RetailOracle.V(w, 13), RetailOracle.V(w, 16)),
            };
            var e = f.Event(); e.Tracking = (SoundTracking)w[0]; e.IgnoreOwnerPosition = w[3] != 0;
            e.Position = RetailOracle.V(w, 6); e.Velocity = RetailOracle.V(w, 9); e.Pan = unchecked((int)w[12]);
            if (w[2] != 0) e.OwnerReader.SetReader(new SoundOwner());
            bool readsOwner = (w[0] != 1 || w[1] != 0) && w[2] != 0 && w[3] == 0;
            var velocity = RetailOracle.V(w, 16);
            if (w[0] > 3 || readsOwner && (velocity.X != 0 || velocity.Y != 0 || velocity.Z != 0))
            { Assert.Throws<NotSupportedException>(() => f.Backend.UpdatePosition(e, w[1] != 0)); refused++; continue; }
            f.Backend.UpdatePosition(e, w[1] != 0);
            // Native oracle also records separate virtual camera/owner calls. The adapter's host callback supplies
            // each pair together; this comparison covers the resulting bits, not an assertion about getter cadence.
            string line = $"Position {RetailOracle.Hv(e.Position)} {RetailOracle.Hv(e.Velocity)} {unchecked((uint)e.Pan):x8}";
            Assert.Equal(string.Join(" ", expected.Split(' ').Take(8)), line); exact++;
        }
        Assert.Equal(2539, exact); Assert.Equal(1877, refused);
    }
}
