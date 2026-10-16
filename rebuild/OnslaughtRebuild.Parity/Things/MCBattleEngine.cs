// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/MCMech.cpp (CMCMech: the constructor 004983b0, HandleEvent 00498870, ProcessMovement
// 004988b0) and src/MCBattleEngine.h (CMCBattleEngine, built inline by CBattleEngine::Init 004053c7..0040540d).
// Reconstructed; not in Stuart's release.

using OnslaughtRebuild.Parity.Events;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>
/// CMCBattleEngine: the walker's motion controller (a CMCMech). It rebuilds the legs' bones once a frame from its own
/// event 3000, which it schedules for the next frame when made and re-adds (re-using the event) each time it runs. The
/// bones feed drawing only: the battle engine's simulated state never reads them (it matches retail without them), so
/// the pose itself is the host's to compute.
/// </summary>
public sealed class MCBattleEngine : Monitor
{
    public const int ProcessEvent = 3000;

    private readonly ThingContext _context;

    public MCBattleEngine(ThingContext context)
    {
        _context = context;
        _context.Events.AddEvent(ProcessEvent, this, EventManager.NextFrame);
    }

    /// <summary>mProcessCount: the bone rebuilds so far.</summary>
    public int ProcessCount { get; private set; }

    public override void HandleEvent(Event e)
    {
        if (e.EventNum == ProcessEvent)
        {
            ProcessMovement();
            _context.Events.AddEvent(ProcessEvent, this, EventManager.NextFrame, EventPriority.StartOfFrame, null, e as ScheduledEvent);
        }
    }

    /// <summary>
    /// ProcessMovement(FALSE, FALSE): skipped when nothing asked for a transform since the last rebuild, the game is
    /// panning or later, and five rebuilds have run; otherwise the bones are rebuilt (drawing only, not modelled here).
    /// </summary>
    private void ProcessMovement()
    {
        ProcessCount++;
        _context.Skip("CMCMech::ProcessMovement's bones (UpdateBoneHierarchyRecursive: drawing only)");
    }
}
