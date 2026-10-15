// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Source-first tests for the bounded two-viewpoint state in
/// <c>references/Onslaught/engine.h:13-16,66-154</c> and
/// <c>engine.cpp:304-338</c>. Retail inline ABI/field layout remains unknown.
/// </summary>
public sealed class EngineViewpointStateTests
{
    private const float Level100NearPlane = 0.1f;
    private const float Level100FarPlane = 700f;

    [Fact]
    public void Initialization_HasExactlyTwoEmptySlotsAndTheMeasuredLevel100DepthRange()
    {
        var state = new EngineViewpointState(
            Level100NearPlane,
            Level100FarPlane);

        Assert.Equal(2, state.SlotCount);
        Assert.Equal(0, state.SelectedSlot);
        Assert.Equal(EngineViewpointSlotState.Empty, state.GetSlot(0));
        Assert.Equal(EngineViewpointSlotState.Empty, state.GetSlot(1));
        Assert.Null(state.SelectedSnapshot.CurrentViewport);
        Assert.Equal(
            0x3DCCCCCDu,
            BitConverter.SingleToUInt32Bits(state.SelectedSnapshot.NearPlane));
        Assert.Equal(
            0x442F0000u,
            BitConverter.SingleToUInt32Bits(state.SelectedSnapshot.FarPlane));
    }

    [Fact]
    public void Selection_AcceptsBothSlotsAndRejectsEveryOutOfRangeIndex()
    {
        var state = Create();

        state.SelectSlot(1);
        Assert.Equal(1, state.SelectedSlot);
        state.SelectSlot(0);
        Assert.Equal(0, state.SelectedSlot);

        Assert.Throws<ArgumentOutOfRangeException>(() => state.SelectSlot(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.SelectSlot(2));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => state.UpdateSlot(-1, EngineViewpointSlotState.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => state.UpdateSlot(2, EngineViewpointSlotState.Empty));
    }

    [Fact]
    public void Selection_CopiesTheViewportByValueUntilTheSlotIsExplicitlyReselected()
    {
        var state = Create();
        var firstViewport = new EngineViewportValue(
            Width: 640,
            Height: 480,
            X: 0,
            Y: 0,
            MinDepth: 0f,
            MaxDepth: 1f);
        var updatedViewport = firstViewport with { Width = 800, X = 12 };

        state.UpdateSlot(
            0,
            new EngineViewpointSlotState("level-100.camera", 7, firstViewport));
        state.SelectSlot(0);
        state.UpdateSlot(
            0,
            new EngineViewpointSlotState("level-100.camera", 7, updatedViewport));

        Assert.Equal(firstViewport, state.SelectedSnapshot.CurrentViewport);
        Assert.Equal(updatedViewport, state.GetSlot(0).Viewport);

        state.SelectSlot(0);
        Assert.Equal(updatedViewport, state.SelectedSnapshot.CurrentViewport);
    }

    [Fact]
    public void SlotUpdates_PreserveIdentityOrderAndDoNotLeakAcrossTheSelectedSlot()
    {
        var state = Create();
        var slot0 = new EngineViewpointSlotState(
            "level-100.attached-pan",
            7,
            new EngineViewportValue(640, 480, 0, 0, 0f, 1f));
        var slot1 = new EngineViewpointSlotState(
            "second.camera",
            19,
            new EngineViewportValue(320, 240, 320, 0, 0f, 1f));

        state.UpdateSlot(0, slot0);
        state.UpdateSlot(1, slot1);
        state.SelectSlot(1);
        EngineViewpointSnapshot selected = state.SelectedSnapshot;

        state.UpdateSlot(
            0,
            slot0 with { CameraIdentity = "level-100.replacement" });

        Assert.Equal(1, selected.SelectedSlot);
        Assert.Equal(slot1, selected.SelectedSlotState);
        Assert.Equal(slot1, state.SelectedSnapshot.SelectedSlotState);
        Assert.Equal("level-100.replacement", state.GetSlot(0).CameraIdentity);
    }

    [Fact]
    public void MissingIdentifiers_AreExplicitAndClearStaleIdentity()
    {
        var state = Create();
        state.UpdateSlot(
            0,
            new EngineViewpointSlotState(
                "level-100.attached-pan",
                7,
                null));
        state.SelectSlot(0);

        state.UpdateSlot(0, EngineViewpointSlotState.Empty);
        state.SelectSlot(0);

        Assert.Null(state.SelectedSnapshot.SelectedSlotState.CameraIdentity);
        Assert.Null(state.SelectedSnapshot.SelectedSlotState.PlayerThingIdentity);
        Assert.Null(state.SelectedSnapshot.SelectedSlotState.Viewport);
        Assert.Null(state.SelectedSnapshot.CurrentViewport);
    }

    [Fact]
    public void Reset_RestoresTheDeterministicInitializationWithoutChangingDepthPolicy()
    {
        var state = Create();
        state.UpdateSlot(
            1,
            new EngineViewpointSlotState(
                "second.camera",
                19,
                new EngineViewportValue(320, 240, 320, 0, 0f, 1f)));
        state.SelectSlot(1);

        state.Reset();

        Assert.Equal(0, state.SelectedSlot);
        Assert.Equal(EngineViewpointSlotState.Empty, state.GetSlot(0));
        Assert.Equal(EngineViewpointSlotState.Empty, state.GetSlot(1));
        Assert.Null(state.SelectedSnapshot.CurrentViewport);
        Assert.Equal(Level100NearPlane, state.SelectedSnapshot.NearPlane);
        Assert.Equal(Level100FarPlane, state.SelectedSnapshot.FarPlane);
        Assert.Equal(Create().ComputeHash(), state.ComputeHash());
    }

    [Fact]
    public void CanonicalHash_IsRepeatableAndSensitiveToOrderIdentityAndCurrentViewportCopy()
    {
        string first = ReplayHash(reverseUpdateOrder: false, reselectAfterUpdate: true);
        string second = ReplayHash(reverseUpdateOrder: false, reselectAfterUpdate: true);
        string reversed = ReplayHash(reverseUpdateOrder: true, reselectAfterUpdate: true);
        string staleCopy = ReplayHash(reverseUpdateOrder: false, reselectAfterUpdate: false);

        Assert.Matches("^[0-9a-f]{64}$", first);
        Assert.Equal(first, second);
        Assert.NotEqual(first, reversed);
        Assert.NotEqual(first, staleCopy);
    }

    private static EngineViewpointState Create() =>
        new(Level100NearPlane, Level100FarPlane);

    private static string ReplayHash(
        bool reverseUpdateOrder,
        bool reselectAfterUpdate)
    {
        var state = Create();
        var first = new EngineViewpointSlotState(
            "level-100.attached-pan",
            7,
            new EngineViewportValue(640, 480, 0, 0, 0f, 1f));
        var second = new EngineViewpointSlotState(
            "second.camera",
            19,
            new EngineViewportValue(320, 240, 320, 0, 0f, 1f));

        if (reverseUpdateOrder)
        {
            state.UpdateSlot(1, first);
            state.UpdateSlot(0, second);
        }
        else
        {
            state.UpdateSlot(0, first);
            state.UpdateSlot(1, second);
        }

        state.SelectSlot(0);
        state.UpdateSlot(
            0,
            state.GetSlot(0) with
            {
                Viewport = new EngineViewportValue(800, 600, 0, 0, 0f, 1f),
            });
        if (reselectAfterUpdate)
        {
            state.SelectSlot(0);
        }

        return state.ComputeHash();
    }
}
