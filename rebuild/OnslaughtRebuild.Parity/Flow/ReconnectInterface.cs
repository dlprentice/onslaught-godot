// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/reconnectinterface.cpp and src/reconnectinterface.h (reconstructed from the retail
// program; not in Stuart's release).

namespace OnslaughtRebuild.Parity.Flow;

/// <summary><c>CReconnectInterface::EState</c>.</summary>
public enum ReconnectState
{
    Ok = 0,
    Disconnected = 1,
    Reconnected = 2,
    Resuming = 3,
}

/// <summary>
/// <c>CReconnectInterface</c> (reconnectinterface.cpp): the prompt to reconnect a removed controller. PC controllers are
/// never removed, so on PC its state only ever goes back to <see cref="ReconnectState.Ok"/>.
/// </summary>
public sealed class ReconnectInterface
{
    public ReconnectState State { get; private set; }

    /// <summary><c>Reset</c> (0x00527960).</summary>
    public void Reset(ReconnectState state) => State = state;

    /// <summary><c>Process</c> (0x00527c50): true once the player has pressed start after reconnecting.</summary>
    public bool Process()
    {
        switch (State)
        {
            case ReconnectState.Disconnected:
                State = ReconnectState.Reconnected;
                return false;
            case ReconnectState.Resuming:
                // The interface takes control only after a disconnection, which a PC controller never has.
                throw new NotSupportedException("Resuming from a controller disconnection is not ported (PC controllers never disconnect).");
            default:
                return false;
        }
    }
}
