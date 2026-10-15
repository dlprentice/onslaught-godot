// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.Client.Tests;

public sealed class RetailMusicPolicyTests
{
    [Fact]
    public void InitialStateKeepsTheAuthoredRawOptionAndSourceVolumeSeeds()
    {
        var policy = new RetailMusicPolicy();

        Assert.Equal(0.9f, policy.ConfiguredVolume);
        Assert.Equal(114, policy.SetVolume);
        Assert.Equal(127, policy.CurrentVolume);
        Assert.Equal(127, policy.TargetVolume);
        Assert.False(policy.IsPlaying);
        Assert.Null(policy.CurrentTrackIdentity);
        Assert.Null(policy.QueuedTrackIdentity);
        Assert.Null(policy.Selection);
        Assert.Null(policy.SelectionTrackIdentity);
    }

    [Fact]
    public void ConfiguredVolumeKeepsTheRawFloatAndUsesToEvenRounding()
    {
        var policy = new RetailMusicPolicy();

        float evenTie = 2.5f / RetailMusicPolicy.FullVolume;
        policy.SetConfiguredVolume(evenTie);

        Assert.Equal(evenTie, policy.ConfiguredVolume);
        Assert.Equal(2, policy.SetVolume);

        float oddTie = 3.5f / RetailMusicPolicy.FullVolume;
        policy.SetConfiguredVolume(oddTie);

        Assert.Equal(oddTie, policy.ConfiguredVolume);
        Assert.Equal(4, policy.SetVolume);
    }

    [Fact]
    public void SelectionStartsOneTrackAtTheConfiguredIntegerVolume()
    {
        var policy = new RetailMusicPolicy();

        IReadOnlyList<RetailMusicAction> actions = policy.PlaySelection(
            RetailMusicSelection.Frontend,
            "frontend-track-08.ogg");

        Assert.Equal(
            [
                new RetailMusicAction(RetailMusicActionKind.SetVolume, null, 114),
                new RetailMusicAction(RetailMusicActionKind.Play, "frontend-track-08.ogg", 0),
            ],
            actions);
        Assert.True(policy.IsPlaying);
        Assert.Equal(RetailMusicPlayType.Selection, policy.PlayType);
        Assert.Equal(RetailMusicSelection.Frontend, policy.Selection);
        Assert.Equal("frontend-track-08.ogg", policy.SelectionTrackIdentity);
        Assert.Equal("frontend-track-08.ogg", policy.CurrentTrackIdentity);
        Assert.Equal(114, policy.CurrentVolume);
        Assert.Equal(114, policy.TargetVolume);
    }

    [Fact]
    public void ImmediateSelectionReplacementStopsBeforeUsingTheSameChannel()
    {
        var policy = new RetailMusicPolicy();
        policy.PlaySelection(RetailMusicSelection.Frontend, "frontend-track-08.ogg");

        IReadOnlyList<RetailMusicAction> actions = policy.PlaySelection(
            RetailMusicSelection.Tutorial,
            "tutorial-track-03.ogg");

        Assert.Equal(
            [
                new RetailMusicAction(RetailMusicActionKind.Stop, null, 0),
                new RetailMusicAction(RetailMusicActionKind.SetVolume, null, 114),
                new RetailMusicAction(RetailMusicActionKind.Play, "tutorial-track-03.ogg", 0),
            ],
            actions);
        Assert.True(policy.IsPlaying);
        Assert.Equal(RetailMusicSelection.Tutorial, policy.Selection);
        Assert.Equal("tutorial-track-03.ogg", policy.SelectionTrackIdentity);
        Assert.Equal("tutorial-track-03.ogg", policy.CurrentTrackIdentity);
        Assert.Null(policy.QueuedTrackIdentity);
    }

    [Fact]
    public void FadedSelectionQueuesThenMovesByFiveBeforeReplacement()
    {
        var policy = new RetailMusicPolicy();
        policy.PlaySelection(RetailMusicSelection.Frontend, "frontend-track-08.ogg");

        IReadOnlyList<RetailMusicAction> queued = policy.PlaySelection(
            RetailMusicSelection.Tutorial,
            "tutorial-track-03.ogg",
            fade: true);

        Assert.Empty(queued);
        Assert.Equal("frontend-track-08.ogg", policy.CurrentTrackIdentity);
        Assert.Equal("tutorial-track-03.ogg", policy.QueuedTrackIdentity);
        Assert.Equal(RetailMusicSelection.Frontend, policy.Selection);
        Assert.Equal("frontend-track-08.ogg", policy.SelectionTrackIdentity);
        Assert.Equal(0, policy.TargetVolume);

        List<int> fadeVolumes = [];
        IReadOnlyList<RetailMusicAction> replacement = [];
        for (int update = 0; update < 30 && policy.QueuedTrackIdentity is not null; update++)
        {
            IReadOnlyList<RetailMusicAction> actions = policy.AdvanceFadeStep();
            if (policy.QueuedTrackIdentity is null)
            {
                replacement = actions;
            }
            else
            {
                fadeVolumes.Add(Assert.Single(actions).Volume);
            }
        }

        Assert.Equal(
            [109, 104, 99, 94, 89, 84, 79, 74, 69, 64, 59, 54, 49, 44,
                39, 34, 29, 24, 19, 14, 9],
            fadeVolumes);
        Assert.Equal(
            [
                new RetailMusicAction(RetailMusicActionKind.SetVolume, null, 0),
                new RetailMusicAction(RetailMusicActionKind.Stop, null, 0),
                new RetailMusicAction(RetailMusicActionKind.SetVolume, null, 114),
                new RetailMusicAction(RetailMusicActionKind.Play, "tutorial-track-03.ogg", 0),
            ],
            replacement);
        Assert.Equal("tutorial-track-03.ogg", policy.CurrentTrackIdentity);
        Assert.Equal(114, policy.CurrentVolume);
        Assert.Equal(114, policy.TargetVolume);
    }

    [Fact]
    public void FinishedQueuedReplacementReplaysTheStoredSelectionIdentity()
    {
        var policy = new RetailMusicPolicy();
        policy.PlaySelection(RetailMusicSelection.Frontend, "frontend-track-08.ogg");
        policy.PlaySelection(
            RetailMusicSelection.Tutorial,
            "tutorial-track-03.ogg",
            fade: true);
        for (int update = 0; update < 22; update++)
        {
            policy.AdvanceFadeStep();
        }

        Assert.Equal(RetailMusicSelection.Frontend, policy.Selection);
        Assert.Equal("frontend-track-08.ogg", policy.SelectionTrackIdentity);
        Assert.Equal("tutorial-track-03.ogg", policy.CurrentTrackIdentity);

        IReadOnlyList<RetailMusicAction> actions = policy.HandleTrackFinished();

        Assert.Equal(
            [
                new RetailMusicAction(RetailMusicActionKind.Stop, null, 0),
                new RetailMusicAction(RetailMusicActionKind.SetVolume, null, 114),
                new RetailMusicAction(RetailMusicActionKind.Play, "frontend-track-08.ogg", 0),
            ],
            actions);
        Assert.Equal(RetailMusicSelection.Frontend, policy.Selection);
        Assert.Equal("frontend-track-08.ogg", policy.CurrentTrackIdentity);
    }

    [Fact]
    public void FinishedSelectionReplaysTheSameIdentityThroughOneChannel()
    {
        var policy = new RetailMusicPolicy();
        policy.PlaySelection(RetailMusicSelection.Tutorial, "tutorial-track-03.ogg");

        IReadOnlyList<RetailMusicAction> actions = policy.HandleTrackFinished();

        Assert.Equal(
            [
                new RetailMusicAction(RetailMusicActionKind.Stop, null, 0),
                new RetailMusicAction(RetailMusicActionKind.SetVolume, null, 114),
                new RetailMusicAction(RetailMusicActionKind.Play, "tutorial-track-03.ogg", 0),
            ],
            actions);
        Assert.True(policy.IsPlaying);
        Assert.Equal(RetailMusicPlayType.Selection, policy.PlayType);
        Assert.Equal(RetailMusicSelection.Tutorial, policy.Selection);
        Assert.Equal("tutorial-track-03.ogg", policy.CurrentTrackIdentity);
    }

    [Fact]
    public void DirectNullListRequestAssignsRandomModeBeforeUsingCallerSelection()
    {
        var policy = new RetailMusicPolicy();

        IReadOnlyList<RetailMusicAction> actions = policy.PlayFromList(
            requestedTrackIdentity: null,
            randomTrackIdentity: "random-track.ogg",
            fade: false);

        Assert.Equal(RetailMusicPlayType.Random, policy.PlayType);
        Assert.Null(policy.Selection);
        Assert.Null(policy.SelectionTrackIdentity);
        Assert.Equal("random-track.ogg", policy.CurrentTrackIdentity);
        Assert.Equal(
            [
                new RetailMusicAction(RetailMusicActionKind.SetVolume, null, 114),
                new RetailMusicAction(RetailMusicActionKind.Play, "random-track.ogg", 0),
            ],
            actions);
    }

    [Fact]
    public void ReleasedPcSelectionMetadataUsesOggAndExactFixedIndices()
    {
        Assert.Equal("ogg", RetailMusicPolicy.PlaylistExtension);
        Assert.Equal(
            8,
            RetailMusicPolicy.TrackIndex(RetailMusicSelection.Frontend));
        Assert.Equal(
            3,
            RetailMusicPolicy.TrackIndex(RetailMusicSelection.Tutorial));
    }

    [Fact]
    public void KillClearsPlaybackWhileResetRestoresTheAuthoredInitialState()
    {
        var policy = new RetailMusicPolicy();
        policy.PlaySelection(RetailMusicSelection.Frontend, "frontend-track-08.ogg");
        policy.PlaySelection(
            RetailMusicSelection.Tutorial,
            "tutorial-track-03.ogg",
            fade: true);
        policy.SetConfiguredVolume(0.8f);

        Assert.Equal(
            [new RetailMusicAction(RetailMusicActionKind.Stop, null, 0)],
            policy.Kill());
        Assert.False(policy.IsPlaying);
        Assert.Equal(0.8f, policy.ConfiguredVolume);
        Assert.Equal(102, policy.SetVolume);
        Assert.Equal(102, policy.CurrentVolume);
        Assert.Equal(102, policy.TargetVolume);
        Assert.Null(policy.CurrentTrackIdentity);
        Assert.Null(policy.QueuedTrackIdentity);
        Assert.Null(policy.Selection);
        Assert.Null(policy.SelectionTrackIdentity);
        Assert.Equal(RetailMusicPlayType.Linear, policy.PlayType);

        Assert.Empty(policy.Reset());
        Assert.Equal(0.9f, policy.ConfiguredVolume);
        Assert.Equal(114, policy.SetVolume);
        Assert.Equal(127, policy.CurrentVolume);
        Assert.Equal(127, policy.TargetVolume);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(-0.01f)]
    [InlineData(1.01f)]
    public void ConfiguredVolumeRejectsNonFiniteAndOutOfRangeValues(float value)
    {
        var policy = new RetailMusicPolicy();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            policy.SetConfiguredVolume(value));
    }

    [Fact]
    public void IdenticalActionTapesProduceTheSamePinnedStateHash()
    {
        RetailMusicPolicy first = RunReplayTape();
        RetailMusicPolicy second = RunReplayTape();

        Assert.Equal(first.Snapshot(), second.Snapshot());
        Assert.Equal(
            "93d172fa01a419f0d025f1c164b03050b82b1e93c6fc06b35568dff80b574f48",
            Hash(first.Snapshot()));
        Assert.Equal(Hash(first.Snapshot()), Hash(second.Snapshot()));
    }

    [Fact]
    public void ChangedSetVolumeRetargetsBeforeTheNextFivePointStep()
    {
        var policy = new RetailMusicPolicy();
        policy.PlaySelection(RetailMusicSelection.Frontend, "frontend-track-08.ogg");
        policy.SetConfiguredVolume(0.8f);

        Assert.Equal(5, RetailMusicPolicy.FadeStep);
        Assert.Equal(
            new RetailMusicAction(RetailMusicActionKind.SetVolume, null, 114),
            Assert.Single(policy.AdvanceFadeStep()));
        Assert.Equal(114, policy.CurrentVolume);
        Assert.Equal(102, policy.TargetVolume);

        Assert.Equal(
            new RetailMusicAction(RetailMusicActionKind.SetVolume, null, 109),
            Assert.Single(policy.AdvanceFadeStep()));
        Assert.Equal(109, policy.CurrentVolume);
        Assert.Equal(102, policy.TargetVolume);
    }

    private static RetailMusicPolicy RunReplayTape()
    {
        var policy = new RetailMusicPolicy();
        policy.PlaySelection(RetailMusicSelection.Frontend, "frontend-track-08.ogg");
        policy.PlaySelection(
            RetailMusicSelection.Tutorial,
            "tutorial-track-03.ogg",
            fade: true);
        for (int update = 0; update < 22; update++)
        {
            policy.AdvanceFadeStep();
        }
        policy.SetConfiguredVolume(0.8f);
        policy.AdvanceFadeStep();
        policy.HandleTrackFinished();
        return policy;
    }

    private static string Hash(RetailMusicPolicySnapshot snapshot)
    {
        string canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"v2|{snapshot.ConfiguredVolume:R}|{snapshot.SetVolume}|" +
            $"{snapshot.CurrentVolume}|{snapshot.TargetVolume}|{snapshot.IsPlaying}|" +
            $"{(int)snapshot.PlayType}|{snapshot.CurrentTrackIdentity}|" +
            $"{snapshot.QueuedTrackIdentity}|{(int?)snapshot.Selection}|" +
            $"{snapshot.SelectionTrackIdentity}");
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
