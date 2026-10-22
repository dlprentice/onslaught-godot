// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Parity;

/// <summary>
/// The baseline before the parity core exists: it counts Presents and accepts input but reads no probe, so a
/// comparison against a retail trace reports every probe as missing. It measures the harness, not the game.
/// </summary>
public sealed class UnimplementedSimulation : IParitySimulation
{
    public long PresentCount { get; private set; }

    public void AdvanceToNextPresent() => PresentCount++;

    public void DeliverKey(RetailKeyEvent key)
    {
    }

    public void DeliverMouse(RetailMouseEvent mouse)
    {
    }

    public void ReadProbes(long frame, ProbeTrace trace)
    {
    }
}
