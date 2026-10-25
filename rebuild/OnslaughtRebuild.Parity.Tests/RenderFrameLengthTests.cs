// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Game;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class RenderFrameLengthTests(ITestOutputHelper output)
{
    [GameDataFact]
    public void TheHostDivisionWouldDivergeFromTheExistingCanonicalRenderStep()
    {
        var session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        float previousDivision = -1;
        int rendered = 0, differences = 0;
        while (session.PresentCount < 2000)
        {
            session.AdvanceToNextPresent();
            if (session.Context.RenderFrameNumber == 0) continue;
            float divided = (session.Context.Events.Time + (session.Game.Loop.Fraction * 0.05f)) / 0.05f;
            float oldLength = previousDivision == -1 ? 1 : divided - previousDivision;
            previousDivision = divided;
            float canonical = session.Context.Particles!.RenderFrameLength;
            if (BitConverter.SingleToInt32Bits(oldLength) != BitConverter.SingleToInt32Bits(canonical))
            {
                if (differences++ == 0)
                    output.WriteLine($"First removed-division difference at Present {session.PresentCount}: old {BitConverter.SingleToUInt32Bits(oldLength):x8}, canonical {BitConverter.SingleToUInt32Bits(canonical):x8}");
            }
            Assert.True(float.IsFinite(canonical) && canonical >= 0);
            rendered++;
        }
        Assert.True(rendered > 1000);
        Assert.True(differences > 0, "the real render schedule must distinguish the removed Host division from the core's compiled reciprocal");
        output.WriteLine($"{rendered} actual world renders; {differences} divided steps differ from the canonical render length.");
    }
}
