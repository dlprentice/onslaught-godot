// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Physics;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class BattleEngineDataTests
{
    [GameDataFact]
    public void TheConfigurationsLoadToTheEndOfTheirFile()
    {
        (string name, byte[] bytes) = GameData.Read(@"data\battle engine configurations.dat");
        MemBuffer file = MemBuffer.FromFile(name, bytes);
        BattleEngineDataManager m = BattleEngineDataManager.Load(file);

        Assert.Equal((uint)bytes.Length, file.Position);
        Assert.NotEmpty(m.Configurations);
        BattleEngineData? aquila = m.GetConfiguration("Aquila Prototype");
        Assert.NotNull(aquila);
        Assert.NotNull(aquila.CockpitText);
        Assert.True(aquila.Life > 0 && aquila.Energy > 0);
        Assert.Same(aquila, m.ForWorld(["Aquila Prototype"u8.ToArray()], 0));
    }
}
