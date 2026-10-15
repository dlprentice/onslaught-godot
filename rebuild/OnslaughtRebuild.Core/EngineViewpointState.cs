// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;

namespace OnslaughtRebuild.Core;

/// <summary>
/// The source viewport's deterministic value fields. A missing nullable value at
/// the slot boundary means the viewport has not been configured; Core does not
/// derive it from a host window.
/// </summary>
public readonly record struct EngineViewportValue(
    int Width,
    int Height,
    int X,
    int Y,
    float MinDepth,
    float MaxDepth);

/// <summary>
/// One deterministic viewpoint slot. Identities are adapter-owned stable values,
/// not native camera, player, or renderer objects.
/// </summary>
public readonly record struct EngineViewpointSlotState(
    string? CameraIdentity,
    int? PlayerThingIdentity,
    EngineViewportValue? Viewport)
{
    public static EngineViewpointSlotState Empty => new(null, null, null);
}

/// <summary>
/// A by-value presentation handoff for the selected slot and the separately
/// copied current viewport.
/// </summary>
public readonly record struct EngineViewpointSnapshot(
    int SelectedSlot,
    EngineViewpointSlotState SelectedSlotState,
    EngineViewportValue? CurrentViewport,
    float NearPlane,
    float FarPlane);

/// <summary>
/// Pure two-slot engine/viewpoint state derived from the pinned source shape in
/// <c>references/Onslaught/engine.h:13-16,66-154</c> and
/// <c>engine.cpp:304-338</c>.
/// </summary>
/// <remarks>
/// <para>
/// Source fixes <c>VIEWPOINTS</c> to two, keeps camera/player/viewport state per
/// slot, and copies the selected slot's viewport by value into a distinct
/// current viewport. Updating a slot therefore does not alias or silently
/// refresh the current viewport; <see cref="SelectSlot"/> performs that explicit
/// copy.
/// </para>
/// <para>
/// Near/far values are caller-supplied because released Level 100 uses measured
/// <c>0.1/700</c>. This owner deliberately has no default that could revive the
/// source base far value <c>256</c>. Retail inline ABI and field layout for this
/// source-present state remain UNKNOWN; no retail equality is claimed.
/// </para>
/// <para>
/// Native camera/viewport objects remain Client/Godot concerns. This type has
/// no Godot geometry, GPU, D3D, COM, HWND, filesystem, clock, process, or network
/// dependency.
/// </para>
/// </remarks>
public sealed class EngineViewpointState
{
    private const int ViewpointCount = 2;
    private const int HashSchemaVersion = 1;

    private readonly EngineViewpointSlotState[] _slots =
        [EngineViewpointSlotState.Empty, EngineViewpointSlotState.Empty];
    private readonly float _nearPlane;
    private readonly float _farPlane;
    private int _selectedSlot;
    private EngineViewportValue? _currentViewport;

    public EngineViewpointState(float nearPlane, float farPlane)
    {
        _nearPlane = nearPlane;
        _farPlane = farPlane;
    }

    public int SlotCount => ViewpointCount;

    public int SelectedSlot => _selectedSlot;

    public EngineViewpointSnapshot SelectedSnapshot => new(
        _selectedSlot,
        _slots[_selectedSlot],
        _currentViewport,
        _nearPlane,
        _farPlane);

    public EngineViewpointSlotState GetSlot(int slot)
    {
        ValidateSlotIndex(slot);
        return _slots[slot];
    }

    public void UpdateSlot(int slot, EngineViewpointSlotState state)
    {
        ValidateSlotIndex(slot);
        _slots[slot] = state;
    }

    public void SelectSlot(int slot)
    {
        ValidateSlotIndex(slot);
        _selectedSlot = slot;
        _currentViewport = _slots[slot].Viewport;
    }

    public void Reset()
    {
        _slots[0] = EngineViewpointSlotState.Empty;
        _slots[1] = EngineViewpointSlotState.Empty;
        _selectedSlot = 0;
        _currentViewport = null;
    }

    /// <summary>
    /// Canonical hash over both ordered slots, selected index, copied viewport,
    /// and depth range.
    /// </summary>
    public string ComputeHash()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(HashSchemaVersion);
            writer.Write(ViewpointCount);
            writer.Write(BitConverter.SingleToInt32Bits(_nearPlane));
            writer.Write(BitConverter.SingleToInt32Bits(_farPlane));
            writer.Write(_selectedSlot);
            WriteSlot(writer, _slots[0]);
            WriteSlot(writer, _slots[1]);
            WriteViewport(writer, _currentViewport);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()))
            .ToLowerInvariant();
    }

    private static void ValidateSlotIndex(int slot)
    {
        if (slot is < 0 or >= ViewpointCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }

    private static void WriteSlot(
        BinaryWriter writer,
        EngineViewpointSlotState slot)
    {
        writer.Write(slot.CameraIdentity is not null);
        if (slot.CameraIdentity is not null)
        {
            writer.Write(slot.CameraIdentity);
        }

        writer.Write(slot.PlayerThingIdentity.HasValue);
        if (slot.PlayerThingIdentity.HasValue)
        {
            writer.Write(slot.PlayerThingIdentity.Value);
        }

        WriteViewport(writer, slot.Viewport);
    }

    private static void WriteViewport(
        BinaryWriter writer,
        EngineViewportValue? viewport)
    {
        writer.Write(viewport.HasValue);
        if (viewport is not { } value)
        {
            return;
        }

        writer.Write(value.Width);
        writer.Write(value.Height);
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(BitConverter.SingleToInt32Bits(value.MinDepth));
        writer.Write(BitConverter.SingleToInt32Bits(value.MaxDepth));
    }
}
