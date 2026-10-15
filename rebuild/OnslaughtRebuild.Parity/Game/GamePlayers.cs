// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/Player.cpp and src/player.h (CPlayer: the constructor 004d2780, Init 004d28a0, GotoFPView
// 004d28c0, AssignBattleEngine 004d3080), src/Camera.cpp and src/Camera.h (CCamera, CThingCamera::GetPos 00418cd0) and
// src/game.cpp (CGame::LoadLevel 0046cdf0's players, PostLoadProcess 0046d040's start positions, SetCurrentCamera
// 004705e0). Player.cpp and Camera.cpp are in Stuart's release.

using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Game;

/// <summary>CCamera: where a player's view is (only what the simulation reads).</summary>
public abstract class Camera
{
    public abstract FVector GetPos();
}

/// <summary>
/// CThingCamera: the view from a thing. A battle engine's is its position; any other thing's is raised by 1.5 times
/// its bounding box's Y axis and lowered by its Z axis (bounding boxes are not ported, so that case throws).
/// </summary>
public sealed class ThingCamera : Camera
{
    private readonly ActiveReader<Thing> _forThing = new();

    public ThingCamera(Thing? forThing) => _forThing.SetReader(forThing);

    public Thing? ForThing => _forThing.ToRead;

    public override FVector GetPos()
    {
        Thing? thing = _forThing.ToRead;
        if (thing is null)
        {
            return new FVector(0, 0, 0);
        }

        if (!thing.IsA(ThingType.BattleEngine))
        {
            throw new NotSupportedException("CThingCamera::GetPos of a thing other than a battle engine (bounding boxes are not ported)");
        }

        return thing.Pos;
    }
}

/// <summary>CPlayer: a player's number (from 1), battle engine and view.</summary>
public sealed class Player(int number)
{
    private readonly ActiveReader<Thing> _battleEngine = new();

    public int Number { get; } = number;

    /// <summary>GetBattleEngine.</summary>
    public Thing? BattleEngine => _battleEngine.ToRead;

    /// <summary>mCurrentViewMode: the first-person view from the start.</summary>
    public bool InFirstPersonView { get; private set; } = true;

    /// <summary>
    /// CPlayer::AssignBattleEngine: the battle engine is read; it learns its player, and a god player's is made
    /// invulnerable with infinite energy (the career's god flag is read as off: the wiped career).
    /// </summary>
    public void AssignBattleEngine(Thing? battleEngine, ThingContext context)
    {
        _battleEngine.SetReader(battleEngine);
        context.Skip("CBattleEngine::SetPlayer");
    }

    /// <summary>CPlayer::Init: the first-person view (the sign-in timeout clock is the platform's).</summary>
    public void Init(Game game) => GotoFPView(game);

    /// <summary>CPlayer::GotoFPView: a thing camera on the battle engine, if there is one.</summary>
    public void GotoFPView(Game game)
    {
        if (BattleEngine is null)
        {
            return;
        }

        InFirstPersonView = true;
        game.SetCurrentCamera(Number - 1, new ThingCamera(BattleEngine));
    }
}

public sealed partial class Game
{
    private readonly Player?[] _player = new Player?[2];
    private readonly Camera?[] _currentCamera = new Camera?[2];

    /// <summary>GetPlayer(n): mPlayer[n], or null.</summary>
    public Player? GetPlayer(int number) => number is >= 0 and < 2 ? _player[number] : null;

    /// <summary>GetCurrentCamera(n): mCurrentCamera[n], or null.</summary>
    public Camera? GetCurrentCamera(int number) => number is >= 0 and < 2 ? _currentCamera[number] : null;

    /// <summary>SetCurrentCamera (004705e0): the player's camera (the old one is released).</summary>
    public void SetCurrentCamera(int number, Camera? camera) => _currentCamera[number] = camera;

    /// <summary>
    /// CGame::LoadLevel after WORLD.Load (game.cpp:716): mPlayers is 2 for a multiplayer world, else 1, and each player
    /// is made (the controllers are the platform's).
    /// </summary>
    public void CreatePlayers(bool multiplayerWorld)
    {
        Players = multiplayerWorld ? 2 : 1;
        for (int i = 0; i < Players; i++)
        {
            _player[i] = new Player(i + 1);
        }
    }

    /// <summary>
    /// CGame::PostLoadProcess's start positions (game.cpp:783): each player takes the battle engine of the last start with
    /// its number, then Init (its first-person camera). A player without a start would get a default one at (256, 256);
    /// that path is not ported.
    /// </summary>
    public void AssignPlayersToStarts(ThingContext context)
    {
        for (int i = 0; i < Players; i++)
        {
            Player player = _player[i]!;
            bool found = false;
            for (Thing? t = context.World.StartNB.First(); t is not null; t = context.World.StartNB.Next())
            {
                if (t is Start start && start.PlayerNumber == player.Number)
                {
                    found = true;
                    player.AssignBattleEngine(start.PlayerObject.ToRead, context);
                }
            }

            if (!found)
            {
                throw new NotSupportedException("CGame::PostLoadProcess: a default start for a player without one");
            }

            player.Init(this);
        }
    }
}
