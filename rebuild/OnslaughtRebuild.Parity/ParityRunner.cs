// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Parity;

/// <summary>
/// A simulation the parity harness can drive the way the retail harness drives the game: Present by Present.
/// </summary>
public interface IParitySimulation
{
    /// <summary>The number of Present calls that have returned since start; 0 before the first.</summary>
    long PresentCount { get; }

    /// <summary>Runs the game until its next Present call returns, so <see cref="PresentCount"/> rises by one.</summary>
    void AdvanceToNextPresent();

    /// <summary>Delivers a key event as retail's window procedure receives it (WM_KEYDOWN/WM_KEYUP).</summary>
    void DeliverKey(RetailKeyEvent key);

    /// <summary>
    /// Delivers a mouse event: its movement is added to the pending DirectInput delta (consumed by the first
    /// device-state read after this frame's events) and its buttons become the new button level.
    /// </summary>
    void DeliverMouse(RetailMouseEvent mouse);

    /// <summary>Writes the current value of every probe this simulation can read into <paramref name="trace"/>.</summary>
    void ReadProbes(long frame, ProbeTrace trace);
}

/// <summary>Plays a retail tape into a parity simulation and records its probe trace.</summary>
public static class ParityRunner
{
    /// <summary>
    /// Advances <paramref name="simulation"/> Present by Present up to <paramref name="maxFrames"/>. After each
    /// Present F returns it reads the probes when F is a multiple of <paramref name="sampleEvery"/>, then delivers the
    /// tape's events for frame F, in that order, as the retail harness does.
    /// </summary>
    public static ProbeTrace Run(IParitySimulation simulation, RetailTape tape, long maxFrames, int sampleEvery)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(tape);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFrames, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleEvery, 1);
        if (simulation.PresentCount != 0)
        {
            throw new InvalidOperationException("The simulation must start before its first Present.");
        }

        var trace = new ProbeTrace();
        while (simulation.PresentCount < maxFrames)
        {
            long before = simulation.PresentCount;
            simulation.AdvanceToNextPresent();
            long frame = simulation.PresentCount;
            if (frame != before + 1)
            {
                throw new InvalidOperationException($"AdvanceToNextPresent moved from Present {before} to {frame}.");
            }

            if (frame % sampleEvery == 0)
            {
                simulation.ReadProbes(frame, trace);
            }

            foreach (RetailTapeEvent e in tape.EventsAt(frame))
            {
                switch (e)
                {
                    case RetailKeyEvent key:
                        simulation.DeliverKey(key);
                        break;
                    case RetailMouseEvent mouse:
                        simulation.DeliverMouse(mouse);
                        break;
                }
            }
        }

        return trace;
    }
}
