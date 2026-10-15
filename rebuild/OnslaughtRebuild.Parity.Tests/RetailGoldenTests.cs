// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using OnslaughtRebuild.Parity.Things;
using Xunit.Abstractions;
using ParityGame = OnslaughtRebuild.Parity.Game.Game;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// A test against a contributor's retail goldens: retail BEA.exe recorded under the replay harness (golden-640: virtual time
/// 1/30 s per Present, probes read after each Present returns; two retail processes identical). They are retail
/// recordings kept out of Git, found through ONSLAUGHT_RETAIL_GOLDEN; skipped, visibly, without them or the game data.
/// </summary>
public sealed class RetailGoldenFactAttribute : FactAttribute
{
    public RetailGoldenFactAttribute(string file)
    {
        if (RetailGolden.File(file) is null)
        {
            Skip = $"retail golden '{file}' not found (set ONSLAUGHT_RETAIL_GOLDEN)";
        }
        else if (GameData.Root is null)
        {
            Skip = "game installation not found (set ONSLAUGHT_GAME_DIR)";
        }
    }
}

internal static class RetailGolden
{
    public static string? File(string name)
    {
        string? root = Environment.GetEnvironmentVariable("ONSLAUGHT_RETAIL_GOLDEN");
        if (string.IsNullOrEmpty(root))
        {
            return null;
        }

        string path = Path.Combine(root, name);
        return System.IO.File.Exists(path) ? path : null;
    }
}

public sealed class RetailGoldenTests(ITestOutputHelper output)
{
    /// <summary>The Presents our level-100 run matches so far (raised as it reaches further; never lowered).</summary>
    private const int RandomStreamMatchedFloor = 187;

    /// <summary>
    /// Level 100's random stream against retail's at every Present (level100.perpresent-3600.probes: RNG is the
    /// stream's mValue and mSeed, EVT the event manager's time). Each retail Present is matched to our state after the
    /// update that reached its event time, so the Present-to-update mapping comes from the recording itself. The
    /// comparison runs until our simulation stops (a part not ported yet) or retail's time passes ours.
    /// </summary>
    [RetailGoldenFact("level100.perpresent-3600.probes")]
    public void Level100sRandomStreamFollowsRetailsPerPresentProbes()
    {
        (ThingContext context, ParityGame game) = LevelLoadTests.Level100Run();
        var states = new Dictionary<uint, (int Update, int Value)>();
        void Record(int u) => states[BitConverter.SingleToUInt32Bits(context.Events.Time)] = (u, context.Random.Value);
        Record(0);
        int update = 0;
        string stopped = "ran to retail's last probe";
        while (update < 2000)
        {
            update++;
            try
            {
                game.Update();
            }
            catch (NotSupportedException e)
            {
                stopped = $"stopped in U{update}: {e.Message}";
                break;
            }

            Record(update);
        }

        int compared = 0;
        string? first = null;
        uint? evt = null;
        foreach (string line in System.IO.File.ReadLines(RetailGolden.File("level100.perpresent-3600.probes")!))
        {
            string[] f = line.Split(' ');
            if (f.Length != 4 || f[0] != "P" || f[3] == "null")
            {
                continue;
            }

            if (f[2] == "EVT")
            {
                evt = BitConverter.ToUInt32(Convert.FromHexString(f[3]));
                continue;
            }

            if (f[2] != "RNG" || evt is null)
            {
                continue;
            }

            if (!states.TryGetValue(evt.Value, out (int Update, int Value) ours))
            {
                stopped += $"; retail's Present {f[1]} (time {BitConverter.UInt32BitsToSingle(evt.Value):R}) is past our run";
                break;
            }

            int retail = BitConverter.ToInt32(Convert.FromHexString(f[3]), 0);
            if (retail != ours.Value)
            {
                first = $"Present {f[1]} (time {BitConverter.UInt32BitsToSingle(evt.Value):R}, our U{ours.Update}): retail {retail}, ours {ours.Value}";
                break;
            }

            compared++;
            evt = null;
        }

        output.WriteLine($"{stopped}; {compared} Presents matched; first difference: {first ?? "none"}");
        Assert.True(compared >= RandomStreamMatchedFloor);
    }
}
