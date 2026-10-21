// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Things;
using ParityGame = OnslaughtRebuild.Parity.Game.Game;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>CPlayer::SetIsGod (004d3020) and AssignBattleEngine's god branch (004d3080) on level 100's player.</summary>
public sealed class GodModeTests
{
    [GameDataFact]
    public void GodModeMakesTheBattleEngineInvulnerableWithFullInfiniteEnergyAndCountsTheCheat()
    {
        (ThingContext _, ParityGame game) = LevelLoadTests.Level100Run();
        Player player = game.GetPlayer(0)!;
        var engine = (BattleEngine)player.BattleEngine!;
        float max = engine.Configuration!.Energy;
        Assert.False(player.IsGod);
        Assert.True(engine.Vulnerable);

        engine.Energy = max / 4;
        player.SetIsGod(true);
        Assert.True(player.IsGod);
        Assert.Equal(1, player.Career!.Record.IsGod(0));
        Assert.False(engine.Vulnerable);
        Assert.True(engine.InfinateEnergy);
        Assert.Equal(max, engine.Energy);
        Assert.Equal(1, player.Stats[(int)PlayerStat.Cheated]);

        float life = engine.Life;
        engine.Damage(5, null);
        Assert.Equal(life, engine.Life);

        // Turning it off restores vulnerability and the drain; the energy is filled again and the cheat stays counted.
        engine.Energy = max / 2;
        player.SetIsGod(false);
        Assert.False(player.IsGod);
        Assert.Equal(0, player.Career!.Record.IsGod(0));
        Assert.True(engine.Vulnerable);
        Assert.False(engine.InfinateEnergy);
        Assert.Equal(max, engine.Energy);
        Assert.Equal(1, player.Stats[(int)PlayerStat.Cheated]);
    }

    [GameDataFact]
    public void APlayerReadsTheCareersGodFlagAndAppliesItToItsBattleEngine()
    {
        (ThingContext context, ParityGame game) = LevelLoadTests.Level100Run();
        Player player = game.GetPlayer(0)!;
        var engine = (BattleEngine)player.BattleEngine!;
        player.Career!.Record.SetIsGod(0, 1);

        var godly = new Player(1, context) { Career = player.Career };
        godly.ReadCareerGod();
        Assert.True(godly.IsGod);
        engine.Energy = 1;
        godly.AssignBattleEngine(engine);
        Assert.False(engine.Vulnerable);
        Assert.True(engine.InfinateEnergy);
        Assert.Equal(engine.Configuration!.Energy, engine.Energy);
        Assert.Equal(0, godly.Stats[(int)PlayerStat.Cheated]);
    }
}
