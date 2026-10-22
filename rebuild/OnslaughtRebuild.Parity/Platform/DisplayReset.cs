// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/Platform.cpp, CPlatform::Flip (004d2580), and src/Career.cpp,
// ReadOptions (00420d70). The retail reset releases/recreates device resources; that host work is not implemented.

namespace OnslaughtRebuild.Parity.Platform;

/// <summary>The process's gDisplayResetState (0082b5b0), shared by display Apply, options reads and career loading.</summary>
public enum DisplayResetRequest : byte
{
    None = 0,
    Apply = 1,
    LoadedOptions = 2,
}

/// <summary>
/// The deferred part of CPlatform::Flip. This models the request and its ordering, not a graphics-device reset.
/// The host must implement Reset3DEnvironment(TRUE, TRUE), including its resource/input lifetime and loading flips.
/// Without that implementation a pending reset fails explicitly after the next platform Present.
/// </summary>
public sealed class DisplayReset
{
    public DisplayResetRequest Pending { get; private set; }

    /// <summary>
    /// Reset3DEnvironment's skip-mode-dialog and show-loading-progress arguments. A successful return means the host
    /// completed the reset; a failure must throw. There is deliberately no default do-nothing implementation.
    /// </summary>
    public Action<bool, bool>? ResetEnvironment { get; set; }

    /// <summary>CAREER.Save followed by WriteDefaultOptionsFile, using the current career, bindings and display mode.</summary>
    public Action? SaveDefaultOptions { get; set; }

    /// <summary>DisplaySettingChanged writes 1, even if another request was already pending.</summary>
    public void RequestApply() => Pending = DisplayResetRequest.Apply;

    /// <summary>
    /// ReadOptions queues 2 only when a display field changed and the device already exists. This admission method
    /// does not implement live ReadOptions: the current boot reader and career-selection path do not call it.
    /// </summary>
    public void RequestAfterOptionsRead(bool displayChanged, bool deviceCreated)
    {
        if (displayChanged && deviceCreated) Pending = DisplayResetRequest.LoadedOptions;
    }

    /// <summary>
    /// After DeviceFlip returns: capture the reason and clear it before reset (which may itself Present). Only a
    /// successful state-2 reset writes default options. Retail's reset failure is fatal, with no rollback/save.
    /// </summary>
    internal void CompletePlatformFlip()
    {
        DisplayResetRequest request = Pending;
        if (request == DisplayResetRequest.None) return;
        Pending = DisplayResetRequest.None;
        (ResetEnvironment ?? throw new NotSupportedException(
            "Reset3DEnvironment(TRUE, TRUE): display/resource/input reset is not implemented by this host"))(true, true);
        if (request == DisplayResetRequest.LoadedOptions)
        {
            (SaveDefaultOptions ?? throw new InvalidOperationException(
                "a state-2 display reset requires the current career/options persistence owner"))();
        }
    }
}
