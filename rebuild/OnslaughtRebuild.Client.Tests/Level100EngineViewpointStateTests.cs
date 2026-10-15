// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Client.Tests;

public sealed class Level100EngineViewpointStateTests
{
    private const int PanDurationTicks = 120;
    private const int HandoffLeadTicks = 1;
    private const float Level100NearPlane = 0.1f;
    private const float Level100FarPlane = 700f;

    [Fact]
    public void Initialization_SelectsTheLevel100CameraWithoutInventingNativeViewportValues()
    {
        Level100EngineViewpointState state = CreateEngine();
        EngineViewpointSnapshot selected = state.SelectedSnapshot;

        Assert.Equal(2, state.SlotCount);
        Assert.Equal(0, selected.SelectedSlot);
        Assert.Equal(
            Level100EngineViewpointState.AttachedPanCameraIdentity,
            selected.SelectedSlotState.CameraIdentity);
        Assert.Null(selected.SelectedSlotState.PlayerThingIdentity);
        Assert.Null(selected.SelectedSlotState.Viewport);
        Assert.Null(selected.CurrentViewport);
        Assert.Equal(
            0x3DCCCCCDu,
            BitConverter.SingleToUInt32Bits(selected.NearPlane));
        Assert.Equal(
            0x442F0000u,
            BitConverter.SingleToUInt32Bits(selected.FarPlane));
    }

    [Fact]
    public void Bind_CarriesTheAttachedThingIdentityAcrossTheSixSecondHandoff()
    {
        var camera = new AttachedPanCameraState(
            PanDurationTicks,
            HandoffLeadTicks);
        Level100EngineViewpointState engine = CreateEngine();
        camera.AdvanceAtEndOfEventFrame(
            Frame(eventFrame: 118, panElapsedTicks: 118, thingId: 7));
        camera.AdvanceAtEndOfEventFrame(
            Frame(eventFrame: 119, panElapsedTicks: 119, thingId: 7));

        AttachedPanCameraViewSnapshot before = camera.Sample(0.5f);
        EngineViewpointSnapshot beforeSelected = engine.Bind(before);
        AttachedPanCameraViewSnapshot atHandoff = camera.Sample(1f);
        EngineViewpointSnapshot handoffSelected = engine.Bind(atHandoff);

        Assert.True(before.OpeningPanActive);
        Assert.False(before.HudVisible);
        Assert.False(atHandoff.OpeningPanActive);
        Assert.True(atHandoff.HudVisible);
        Assert.Equal(7, beforeSelected.SelectedSlotState.PlayerThingIdentity);
        Assert.Equal(7, handoffSelected.SelectedSlotState.PlayerThingIdentity);
        Assert.Equal(
            Level100EngineViewpointState.AttachedPanCameraIdentity,
            handoffSelected.SelectedSlotState.CameraIdentity);
        Assert.Equal(0.1f, handoffSelected.NearPlane);
        Assert.Equal(700f, handoffSelected.FarPlane);
    }

    [Fact]
    public void Bind_MissingAttachedThingClearsThePriorIdentityInsteadOfLeakingIt()
    {
        Level100EngineViewpointState engine = CreateEngine();
        engine.Bind(new AttachedPanCameraViewSnapshot(
            new Level100ActorId(7),
            ClientCameraPose.Identity,
            1f,
            HudVisible: true,
            OpeningPanActive: false));

        EngineViewpointSnapshot missing = engine.Bind(
            new AttachedPanCameraViewSnapshot(
                null,
                ClientCameraPose.Identity,
                1f,
                HudVisible: false,
                OpeningPanActive: false));

        Assert.Null(missing.SelectedSlotState.PlayerThingIdentity);
        Assert.Equal(
            Level100EngineViewpointState.AttachedPanCameraIdentity,
            missing.SelectedSlotState.CameraIdentity);
    }

    [Fact]
    public void Bind_ReplaysToTheSameCanonicalHashAndChangesWithThingIdentity()
    {
        string first = ReplayHash(7);
        string second = ReplayHash(7);
        string changed = ReplayHash(19);

        Assert.Matches("^[0-9a-f]{64}$", first);
        Assert.Equal(first, second);
        Assert.NotEqual(first, changed);
    }

    [Fact]
    public void AdapterAssembly_DoesNotAcquireGodotOrNativeViewportDependencies()
    {
        string[] references = typeof(Level100EngineViewpointState)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            references,
            name => name.Contains("Godot", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            references,
            name => name.Contains("Direct3D", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FirstFlightWorldView_ConsumesTheSelectedByValueEngineSnapshot()
    {
        string source = File.ReadAllText(Path.Combine(
            LocateGodotDirectory(),
            "FirstFlightWorldView.cs"));

        Assert.Contains(
            "private readonly Level100EngineViewpointState _engineViewpointState = new(\n        RetailNearPlane,\n        RetailFarPlane);",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "private const float RetailNearPlane = 0.1f;",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "private const float RetailFarPlane = 700f;",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "EngineViewpointSnapshot selectedViewpoint =\n            _engineViewpointState.Bind(cameraSnapshot);",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Near = selectedViewpoint.NearPlane,",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Far = selectedViewpoint.FarPlane,",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "2f * selectedViewpoint.NearPlane * RetailTanVerticalHalfFov * cameraSnapshot.Zoom;",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Near = 0.1f,", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Far = 700f,", source, StringComparison.Ordinal);
    }

    private static string ReplayHash(int thingId)
    {
        Level100EngineViewpointState engine = CreateEngine();
        engine.Bind(new AttachedPanCameraViewSnapshot(
            new Level100ActorId(thingId),
            ClientCameraPose.Identity,
            1f,
            HudVisible: true,
            OpeningPanActive: false));
        return engine.ComputeHash();
    }

    private static Level100EngineViewpointState CreateEngine() =>
        new(Level100NearPlane, Level100FarPlane);

    private static AttachedPanCameraFrame Frame(
        int eventFrame,
        int panElapsedTicks,
        int thingId) =>
        new(
            eventFrame,
            panElapsedTicks,
            1_000,
            new AttachedCameraThingSnapshot(
                new Level100ActorId(thingId),
                ClientCameraPose.Identity,
                new Level100RenderVector3(1f, 0f, 0f)));

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
