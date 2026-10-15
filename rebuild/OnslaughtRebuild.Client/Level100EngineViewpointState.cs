// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Client;

/// <summary>
/// Binds the existing Level 100 attached/pan camera snapshot to the selected
/// slot in Core's two-viewpoint engine-state envelope.
/// </summary>
/// <remarks>
/// <para>
/// Source shape: <c>references/Onslaught/engine.h:13-16,66-154</c> and
/// <c>engine.cpp:304-338</c>. Slot zero is the only Level 100 presentation slot
/// currently consumed. Slot one remains explicit deterministic empty state; this
/// adapter does not create split-screen rendering or a second camera lifecycle.
/// </para>
/// <para>
/// The measured product exception remains <c>near=0.1</c>, <c>far=700</c>.
/// Viewport geometry stays unconfigured until a separately proved deterministic
/// value exists; it is never derived from the host window here. Godot translates
/// the selected value snapshot into its one native camera.
/// </para>
/// </remarks>
public sealed class Level100EngineViewpointState
{
    public const string AttachedPanCameraIdentity = "level-100.attached-pan";

    private const int Level100Slot = 0;
    private readonly EngineViewpointState _state;

    public Level100EngineViewpointState(float nearPlane, float farPlane)
    {
        _state = new EngineViewpointState(nearPlane, farPlane);
        _state.UpdateSlot(
            Level100Slot,
            new EngineViewpointSlotState(
                AttachedPanCameraIdentity,
                null,
                null));
        _state.SelectSlot(Level100Slot);
    }

    public int SlotCount => _state.SlotCount;

    public EngineViewpointSnapshot SelectedSnapshot => _state.SelectedSnapshot;

    /// <summary>
    /// Updates the selected Level 100 slot from the already sampled attached/pan
    /// lifecycle, then explicitly reselects it so the current viewport follows
    /// the source's by-value copy boundary.
    /// </summary>
    public EngineViewpointSnapshot Bind(AttachedPanCameraViewSnapshot camera)
    {
        EngineViewpointSlotState slot = _state.GetSlot(Level100Slot);
        _state.UpdateSlot(
            Level100Slot,
            slot with
            {
                CameraIdentity = AttachedPanCameraIdentity,
                PlayerThingIdentity = camera.AttachedThingId?.Value,
            });
        _state.SelectSlot(Level100Slot);
        return _state.SelectedSnapshot;
    }

    public string ComputeHash() => _state.ComputeHash();
}
