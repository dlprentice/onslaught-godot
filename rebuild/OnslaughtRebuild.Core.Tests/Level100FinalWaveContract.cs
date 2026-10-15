// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// The released final drone wave's two outcomes, from the shipped script
/// (<c>data/MissionScripts/level100/LevelScript.msl</c>, SHA-256
/// <c>d51f8864…</c>) as the RE lane's contract
/// <c>reverse-engineering/game-mechanics/level100-final-drone-wave.md</c> reads
/// it. Six kills count <c>numTargets</c> to zero and complete objective 4
/// (<c>:284-295</c>). Otherwise the health poll's abort (<c>:351-364</c>) sets
/// <c>numTargets</c> to zero with objective 4 left failed from <c>init()</c>
/// (<c>:38</c>), and every surviving drone's <c>AirborneDrone2.msl</c> answers
/// with <c>SetAIState(AI_OFF)</c> and <c>SetAllegiance(FRIENDLY)</c>. Target
/// Zone 4 still ends in <c>LevelWon()</c> on both branches. Which branch a run
/// takes depends on its combat, so neither is a driver invariant.
/// </summary>
internal static class Level100FinalWaveContract
{
    private const int AiOff = 1;
    private const int FriendlyAllegiance = 0;

    internal static void AssertReleasedBranch(WorldSnapshot final)
    {
        Assert.Equal(Level100MissionOutcome.Won, final.Level100Mission.Outcome);
        Level100ActorSnapshot[] wave = final.Level100Actors.Actors
            .Where(actor => actor.TargetGroup == Level100MissionTargetGroup.AirborneTargets2)
            .ToArray();
        int kills = wave.Count(actor => actor.Lifecycle == Level100ActorLifecycle.Destroyed);
        Level100PrimaryObjectiveStatus objective4 = final.Level100Mission.PrimaryObjectives
            .Single(objective => objective.Objective == 4).Status;

        if (!final.Level100Mission.Aborted)
        {
            Assert.Equal(6, kills);
            Assert.Equal(Level100PrimaryObjectiveStatus.Complete, objective4);
            return;
        }

        Assert.InRange(kills, 0, 5);
        Assert.Equal(Level100PrimaryObjectiveStatus.Failed, objective4);
        foreach (Level100ActorSnapshot drone in wave.Where(actor =>
                     actor.Active && actor.Lifecycle == Level100ActorLifecycle.Alive))
        {
            Level100ActorCommandIntentSnapshot intent = Assert.Single(
                final.Level100ActorMechanics.Actors,
                item => item.ActorId == drone.ActorId);
            Assert.Equal(AiOff, intent.AiState);
            Assert.True(intent.HasAllegianceOverride);
            Assert.Equal(FriendlyAllegiance, intent.Allegiance);
        }
    }
}
