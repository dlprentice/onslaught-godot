// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// The materialized World 110 static world decodes into the rows the RE lane's
/// construction contract lists
/// (<c>reverse-engineering/game-mechanics/world-110-construction-order.md</c>,
/// "Level-world rows") and the paths as retail loads them
/// (<c>waypoint-paths.md</c>, "World 110 paths").
/// </summary>
public sealed class World110StaticWorldManifestTests
{
    private static readonly Lazy<Level100ActorDefinitionSet> s_world110 =
        new(Level100TestActorDefinitions.LoadMaterializedWorld110);

    [Fact]
    public void World110_DecodesTheBaseWorldAndTheLevelRows()
    {
        Level100ActorDefinitionSet world = s_world110.Value;
        Assert.Equal(110, world.WorldNumber);
        Assert.Equal(1_481, world.BaseWorldPineCount);
        Assert.Equal(77, world.Actors.Count);
        Assert.Equal(33, world.Actors.Count(actor => actor.DefinitionIdentity.StartsWith("wres:bswd:", StringComparison.Ordinal)));

        Level100ActorDefinition player = Assert.Single(world.Actors, actor => actor.Name == "Player 1");
        Assert.Equal(("wres:rlwd:0001", "BattleEngine", 0), (player.DefinitionIdentity, player.DefinitionName, player.Allegiance));

        // The shared base world keeps Level 100's rows; its buildings carry
        // their unit records' life.
        Level100ActorDefinition research = Assert.Single(world.Actors, actor => actor.Name == "Forseti Research Building 1");
        Assert.Equal(150_000, research.InitialHealth);

        Level100ActorDefinition spawner = Assert.Single(world.Actors, actor => actor.Name == "Fighter Second Wave");
        Assert.False(spawner.Active);

        Assert.Equal(
            ["Lander", "Lander2", "Lander3", "Lander"],
            world.Actors.Where(actor => actor.DefinitionName?.StartsWith("Muspell Light Landing", StringComparison.Ordinal) == true)
                .Select(actor => actor.ScriptName));
        Assert.All(
            world.Actors.Where(actor => actor.DefinitionName is "Muspell Fighter" or "Muspell Light Fighter"),
            fighter => Assert.Equal((1, (string?)null), (fighter.Allegiance, fighter.ScriptName)));
    }

    [Fact]
    public void World110_SquadsAndTurretChildrenFollowTheirRows()
    {
        Level100ActorDefinitionSet world = s_world110.Value;
        Assert.Equal(
            [("wres:rlwd:0014", 5, 1), ("wres:rlwd:0016", 5, 1), ("wres:rlwd:0017", 3, 0),
             ("wres:rlwd:0018", 5, 1), ("wres:rlwd:0019", 4, 0)],
            world.Squads.Select(squad => (squad.DefinitionIdentity, squad.MemberIdentities.Count, squad.Allegiance)));
        Assert.Equal("Scout", world.Squads[4].ScriptName);
        Assert.All(world.Squads, squad => Assert.Equal(
            (Level100ActorDefinitionSet.SquadDefinitionName, squad.ScriptName),
            world.Actors.Where(actor => actor.DefinitionIdentity == squad.DefinitionIdentity)
                .Select(actor => (actor.DefinitionName, actor.ScriptName)).Single()));
        Assert.All(world.Squads.Take(4), squad => Assert.Null(squad.ScriptName));

        Assert.Equal(
            ["wres:rlwd:0008", "wres:rlwd:0012", "wres:rlwd:0013", "wres:rlwd:0020"],
            world.Components.Select(component => component.ParentIdentity));
        Assert.All(world.Components, component =>
        {
            Assert.Equal("Dropship Gun Turret", component.DefinitionName);
            Assert.Equal(component.ParentIdentity + ":turret", component.ChildIdentity);
        });
    }

    [Fact]
    public void World110_PathsAreRetailListsWithTheirOwnTargets()
    {
        Level100ActorDefinitionSet world = s_world110.Value;
        Level100WaypointPathDefinition lander = world.GetWaypointPath("Lander Path 1");
        Assert.Equal([24, 10, 11, 15, 21, 22, 23], lander.Points.Select(point => point.NodeIndex));
        Assert.Equal([null, 11, 15, null, 22, 24, null], lander.Points.Select(point => point.TargetNodeIndex));
        Assert.Equal([26, 27, 28, 29, 30, 31, 32, 33],
            world.GetWaypointPath("Fighter Path 1").Points.Select(point => point.NodeIndex));
        Assert.Equal([7, 6], world.GetWaypointPath("Fighter Path 2").Points.Select(point => point.NodeIndex));
        Assert.Equal([3, 4], world.GetWaypointPath("Transport Path 1").Points.Select(point => point.NodeIndex));

        // The row 8 landing craft at retail (215, 422, -20) starts at node 10
        // (waypoint-paths.md: squared distance 725 against 7,782.5 for 11).
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        Assert.Equal(10, lander.NearestPoint(new Level100FloatVector3Bits(Bits(215f), Bits(422f), Bits(-20f)))!.NodeIndex);
        Assert.Equal(21, lander.NearestPoint(new Level100FloatVector3Bits(Bits(170f), Bits(505f), Bits(-25f)))!.NodeIndex);
    }

    [Fact]
    public void World110_IdentityCarriesSidesSquadsAndComponents()
    {
        Level100ActorDefinitionSet world = s_world110.Value;
        var withoutSquads = new Level100ActorDefinitionSet(
            world.Actors, world.Spawns, world.WaypointPaths, world.MotionDefinitions,
            worldNumber: 110, baseWorldPineCount: world.BaseWorldPineCount, components: world.Components);
        var neutral = new Level100ActorDefinitionSet(
            world.Actors.Select(actor => actor with { Allegiance = 0 }), world.Spawns, world.WaypointPaths,
            world.MotionDefinitions, worldNumber: 110, baseWorldPineCount: world.BaseWorldPineCount);
        Assert.NotEqual(world.IdentitySha256, withoutSquads.IdentitySha256);
        Assert.NotEqual(withoutSquads.IdentitySha256, neutral.IdentitySha256);
    }
}
