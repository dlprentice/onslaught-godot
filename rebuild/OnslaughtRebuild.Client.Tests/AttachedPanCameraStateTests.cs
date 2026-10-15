// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Client.Tests;

public sealed class AttachedPanCameraStateTests
{
    private const int PanDurationTicks = 120;
    private const int HandoffLeadTicks = 1;

    [Fact]
    public void EndOfFrameUpdate_ShadowsTheCurrentPanPoseBeforeSamplingTheNextPose()
    {
        var state = new AttachedPanCameraState(PanDurationTicks, HandoffLeadTicks);

        AttachedPanCameraSnapshot first = state.AdvanceAtEndOfEventFrame(
            Frame(eventFrame: 0, panElapsedTicks: 0, positionX: 0f));
        AttachedPanCameraSnapshot second = state.AdvanceAtEndOfEventFrame(
            Frame(eventFrame: 1, panElapsedTicks: 1, positionX: 1f));

        Assert.Equal(first.CurrentPanPose, second.PreviousPanPose);
        Assert.NotEqual(second.PreviousPanPose, second.CurrentPanPose);
        Assert.Equal(
            AttachedPanCameraUpdatePhase.EndOfEventFrame,
            second.UpdatePhase);
        Assert.True(second.PanUpdateScheduled);
        Assert.Equal(new Level100ActorId(7), second.CurrentFrame.AttachedThing?.ThingId);
        Assert.Equal(1f, second.CurrentFrame.AttachedThing?.Pose.Position.X);
    }

    [Fact]
    public void Handoff_CarriesAttachedPoseZoomAndHudAtTheCallerSuppliedBoundary()
    {
        var state = new AttachedPanCameraState(PanDurationTicks, HandoffLeadTicks);
        state.AdvanceAtEndOfEventFrame(
            Frame(eventFrame: 118, panElapsedTicks: 118, positionX: 2f));
        AttachedPanCameraSnapshot boundary = state.AdvanceAtEndOfEventFrame(
            Frame(
                eventFrame: 119,
                panElapsedTicks: 119,
                positionX: 3f,
                zoomPermille: 800));

        AttachedPanCameraViewSnapshot before = state.Sample(0.5f);
        AttachedPanCameraViewSnapshot atBoundary = state.Sample(1f);

        Assert.Equal(PanDurationTicks, boundary.PanDurationTicks);
        Assert.Equal(119, boundary.ControlViewHandoffTick);
        Assert.True(before.OpeningPanActive);
        Assert.False(before.HudVisible);
        Assert.Equal(1f, before.Zoom);
        Assert.False(atBoundary.OpeningPanActive);
        Assert.True(atBoundary.HudVisible);
        Assert.Equal(0.8f, atBoundary.Zoom);
        Assert.Equal(new Level100ActorId(7), atBoundary.AttachedThingId);
        Assert.Equal(3f, atBoundary.Pose.Position.X);
        Assert.False(boundary.PanUpdateScheduled);
    }

    [Fact]
    public void CallerSuppliedDuration_DecidesTheHandoffInsteadOfASourceDefault()
    {
        var state = new AttachedPanCameraState(
            panDurationTicks: 60,
            controlViewHandoffLeadTicks: 1);
        state.AdvanceAtEndOfEventFrame(
            Frame(eventFrame: 58, panElapsedTicks: 58));
        AttachedPanCameraSnapshot boundary = state.AdvanceAtEndOfEventFrame(
            Frame(eventFrame: 59, panElapsedTicks: 59));

        Assert.Equal(60, boundary.PanDurationTicks);
        Assert.Equal(59, boundary.ControlViewHandoffTick);
        Assert.True(state.Sample(0.5f).OpeningPanActive);
        Assert.False(state.Sample(1f).OpeningPanActive);
    }

    [Fact]
    public void PanPose_StopsMutatingAfterItsLastScheduledUpdate()
    {
        var state = new AttachedPanCameraState(PanDurationTicks, HandoffLeadTicks);
        state.AdvanceAtEndOfEventFrame(
            Frame(eventFrame: 118, panElapsedTicks: 118, positionX: 2f));
        AttachedPanCameraSnapshot handoff = state.AdvanceAtEndOfEventFrame(
            Frame(eventFrame: 119, panElapsedTicks: 119, positionX: 3f));
        AttachedPanCameraSnapshot attached = state.AdvanceAtEndOfEventFrame(
            Frame(eventFrame: 120, panElapsedTicks: 120, positionX: 100f));

        Assert.False(handoff.PanUpdateScheduled);
        Assert.Equal(handoff.CurrentPanPose, attached.PreviousPanPose);
        Assert.Equal(handoff.CurrentPanPose, attached.CurrentPanPose);
    }

    [Fact]
    public void MissingAttachment_HoldsPanPoseAndFallsBackToAnEmptyAttachedView()
    {
        var state = new AttachedPanCameraState(PanDurationTicks, HandoffLeadTicks);
        AttachedPanCameraSnapshot attached = state.AdvanceAtEndOfEventFrame(
            Frame(eventFrame: 10, panElapsedTicks: 10, positionX: 4f));
        AttachedPanCameraSnapshot missing = state.AdvanceAtEndOfEventFrame(
            Frame(eventFrame: 11, panElapsedTicks: 11, attached: false));

        Assert.Equal(attached.CurrentPanPose, missing.PreviousPanPose);
        Assert.Equal(attached.CurrentPanPose, missing.CurrentPanPose);
        Assert.Null(missing.CurrentFrame.AttachedThing);
        Assert.True(missing.PanUpdateScheduled);

        state.AdvanceAtEndOfEventFrame(
            Frame(
                eventFrame: 119,
                panElapsedTicks: 119,
                zoomPermille: 400,
                attached: false));
        AttachedPanCameraViewSnapshot view = state.Sample(1f);

        Assert.Null(view.AttachedThingId);
        Assert.False(view.HudVisible);
        Assert.Equal(1f, view.Zoom);
        Assert.Equal(ClientCameraPose.Identity, view.Pose);
    }

    [Fact]
    public void PanElapsedRegression_ResetsOldAndCurrentPoseWithoutSmearingAcrossRuns()
    {
        var state = new AttachedPanCameraState(PanDurationTicks, HandoffLeadTicks);
        state.AdvanceAtEndOfEventFrame(
            Frame(eventFrame: 50, panElapsedTicks: 50, positionX: 5f));
        AttachedPanCameraSnapshot reset = state.AdvanceAtEndOfEventFrame(
            Frame(eventFrame: 51, panElapsedTicks: 0, positionX: 100f));

        Assert.Equal(1, reset.ResetGeneration);
        Assert.Equal(reset.CurrentFrame, reset.PreviousFrame);
        Assert.Equal(reset.CurrentPanPose, reset.PreviousPanPose);
        Assert.Equal(reset.CurrentPanPose, state.Sample(0f).Pose);
    }

    [Fact]
    public void AttachedView_TeleportSnapsToTheCurrentThingPose()
    {
        var state = new AttachedPanCameraState(PanDurationTicks, HandoffLeadTicks);
        state.AdvanceAtEndOfEventFrame(
            Frame(eventFrame: 119, panElapsedTicks: 119, positionX: 0f));
        state.AdvanceAtEndOfEventFrame(
            Frame(eventFrame: 120, panElapsedTicks: 120, positionX: 20f));

        AttachedPanCameraViewSnapshot halfway = state.Sample(0.5f);

        Assert.False(halfway.OpeningPanActive);
        Assert.Equal(20f, halfway.Pose.Position.X);
    }

    [Fact]
    public void WorldSnapshotAdapter_ReobservingARenderOnlyPairIsIdempotent()
    {
        var session = new InteractiveSession(
            0x4F4E534Cu,
            Level100TestActorDefinitions.Create());
        FrameAdvanceResult pair = session.AdvanceFrameTicks(500_000);
        var state = new AttachedPanCameraState(PanDurationTicks, HandoffLeadTicks);

        AttachedPanCameraSnapshot first = state.Advance(
            pair.PreviousSnapshot,
            pair.CurrentSnapshot);
        AttachedPanCameraSnapshot second = state.Advance(
            pair.PreviousSnapshot,
            pair.CurrentSnapshot);

        Assert.Equal(0, second.ResetGeneration);
        Assert.Equal(first, second);
        Assert.Equal(first.ComputeHash(), second.ComputeHash());
        Assert.Equal(
            pair.CurrentSnapshot.Level100Actors.Actors.Single(
                actor => actor.Name == "Player 1").ActorId,
            second.CurrentFrame.AttachedThing?.ThingId);
    }

    [Fact]
    public void SameEventFrames_ProduceTheSameCanonicalCameraHashes()
    {
        IReadOnlyList<string> first = ReplayHashes();
        IReadOnlyList<string> second = ReplayHashes();

        Assert.Equal(first, second);
        Assert.All(first, hash => Assert.Matches("^[0-9a-f]{64}$", hash));

        var changed = new AttachedPanCameraState(PanDurationTicks, HandoffLeadTicks);
        changed.AdvanceAtEndOfEventFrame(Frame(0, 0));
        string changedHash = changed.AdvanceAtEndOfEventFrame(
            Frame(1, 1, zoomPermille: 900)).ComputeHash();

        Assert.NotEqual(first[1], changedHash);
    }

    [Fact]
    public void FirstFlightWorldView_ConsumesTheClientCameraSnapshotSeam()
    {
        string source = File.ReadAllText(Path.Combine(
            LocateGodotDirectory(),
            "FirstFlightWorldView.cs"));

        Assert.Contains(
            "private readonly AttachedPanCameraState _cameraState = new(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "_cameraState.Advance(previous, current);",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "AttachedPanCameraViewSnapshot cameraSnapshot =\n            _cameraState.Sample(interpolationAlpha);",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ShowHud = cameraSnapshot.HudVisible;",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "OpeningPanActive = cameraSnapshot.OpeningPanActive;",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "public bool OpeningPanActive => !ShowHud;",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "EvaluateRetailOpeningSpline(",
            source,
            StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ReplayHashes()
    {
        var state = new AttachedPanCameraState(PanDurationTicks, HandoffLeadTicks);
        return new[]
        {
            state.AdvanceAtEndOfEventFrame(Frame(0, 0)).ComputeHash(),
            state.AdvanceAtEndOfEventFrame(Frame(1, 1)).ComputeHash(),
            state.AdvanceAtEndOfEventFrame(Frame(2, 2, positionX: 1f)).ComputeHash(),
            state.AdvanceAtEndOfEventFrame(Frame(119, 119, positionX: 2f, zoomPermille: 700))
                .ComputeHash(),
        };
    }

    private static AttachedPanCameraFrame Frame(
        int eventFrame,
        int panElapsedTicks,
        float positionX = 0f,
        int zoomPermille = 1_000,
        bool attached = true) =>
        new(
            eventFrame,
            panElapsedTicks,
            zoomPermille,
            attached
                ? new AttachedCameraThingSnapshot(
                    new Level100ActorId(7),
                    new ClientCameraPose(
                        new Level100RenderVector3(positionX, 0f, 0f),
                        new Level100RenderVector3(0f, 0f, -1f),
                        new Level100RenderVector3(0f, 1f, 0f)),
                    new Level100RenderVector3(1f, 0f, 0f))
                : null);

    private static string LocateGodotDirectory()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "OnslaughtRebuild.Godot");
            if (File.Exists(Path.Combine(candidate, "FirstFlightWorldView.cs")))
            {
                return candidate;
            }
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate OnslaughtRebuild.Godot above {AppContext.BaseDirectory}.");
    }
}
