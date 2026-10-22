// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Flow.Menus;
using OnslaughtRebuild.Parity.Game;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// The pause menu's Geometry detail (CTreeDetail, CRTMesh::SetQualityLevel) raised to High in level 100 against the native
/// port with patches 0029 and 0030 (l100-treedetail-port-20261021): Video Options, Geometry detail, its choice list's High
/// confirmed, the menus closed (default options saved with TREE_MESH_DISTANCE 70 and MESH_LOD_BIAS 0.3), then the fire1
/// route's walk and firing at the training targets, 600 Presents later than its own tape. The world render's meshes now take
/// the High bias, so their poses (which collision reuses) follow it. Every probe field every Present to 6600, and the saved
/// options equal to the port's.
/// </summary>
public sealed class TreeDetailRouteTests(ITestOutputHelper output)
{
    /// <summary>The Present through which every field matches the port every Present (never lowered).</summary>
    private const int MatchedThrough = 6600;

    private const string Dir = "portrefs/l100-treedetail-port-20261021";

    [RetailGoldenFact(Dir + "/port-probes-every1.txt", Dir + "/l100-treedetail.tape", Dir + "/defaultoptions-after.bea")]
    public void RaisingTheGeometryDetailMidLevelMatchesThePortEveryPresent()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        session.Tape = RetailTape.Load(RetailGolden.File(Dir + "/l100-treedetail.tape")!);
        (long matched, string? first, int compared) = TrainerRouteTests.CompareEveryPresent(session, Dir + "/port-probes-every1.txt", MatchedThrough);
        output.WriteLine($"{compared} fields compared; every field matches through Present {matched}; first difference: {first ?? "none"}");
        Assert.True(matched >= MatchedThrough, $"matched through Present {matched}, below {MatchedThrough}; {first}");
        Assert.True(compared >= 9 * (MatchedThrough - 3), $"only {compared} fields compared");
        var host = (LevelMenuHost)session.Input.PauseMenuHost!;
        Assert.Equal(2, OptionSettings.GetQualityLevel(host.Options));
        Assert.Equal(File.ReadAllBytes(RetailGolden.File(Dir + "/defaultoptions-after.bea")!), host.DefaultOptionsFile);
    }
}
