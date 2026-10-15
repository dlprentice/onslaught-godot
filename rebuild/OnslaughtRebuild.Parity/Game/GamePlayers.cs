// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/Player.cpp and src/player.h (CPlayer: the constructor 004d2780, Init 004d28a0, GotoFPView
// 004d28c0, GotoControlView 004d2a50, GotoPanView 004d2c10, HandleEvent 004d2fe0, AssignBattleEngine 004d3080),
// src/Camera.cpp and src/Camera.h (CCamera, CThingCamera::GetPos 00418cd0) and
// src/game.cpp (CGame::LoadLevel 0046cdf0's players, PostLoadProcess 0046d040's start positions, SetCurrentCamera
// 004705e0). Player.cpp and Camera.cpp are in Stuart's release.

using System.Runtime.CompilerServices;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Things;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Game;

/// <summary>
/// CCamera: where a player's view is (only what the simulation reads). A monitor, as CPanCamera is (its update events
/// are aimed at it).
/// </summary>
public abstract class Camera : Monitor
{
    public abstract FVector GetPos();

    /// <summary>delete camera (SetCurrentCamera releasing the old one).</summary>
    public virtual void Release() => Destroy();
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
public sealed class Player(int number, ThingContext context) : Monitor
{
    /// <summary>GOTO_CONTROL_VIEW: the pan is over.</summary>
    public const int GotoControlViewEvent = 4000;

    private readonly ActiveReader<Thing> _battleEngine = new();

    public int Number { get; } = number;

    /// <summary>GetBattleEngine.</summary>
    public Thing? BattleEngine => _battleEngine.ToRead;

    /// <summary>mCurrentViewMode (PLAYER_FP_VIEW from the constructor).</summary>
    public PlayerView CurrentView { get; private set; } = PlayerView.FirstPerson;

    /// <summary>mPreferedControlView (PLAYER_FP_VIEW from the constructor).</summary>
    public PlayerView PreferredControlView { get; set; } = PlayerView.FirstPerson;

    /// <summary>
    /// CPlayer::AssignBattleEngine: the battle engine is read; it learns its player, and a god player's is made
    /// invulnerable with infinite energy (the career's god flag is read as off: the wiped career).
    /// </summary>
    public void AssignBattleEngine(Thing? battleEngine)
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

        CurrentView = PlayerView.FirstPerson;
        game.SetCurrentCamera(Number - 1, new ThingCamera(BattleEngine));
    }

    /// <summary>CPlayer::GotoControlView: the preferred view (first person; the third-person camera is not ported).</summary>
    public void GotoControlView(Game game)
    {
        if (PreferredControlView == PlayerView.FirstPerson)
        {
            GotoFPView(game);
        }

        if (PreferredControlView == PlayerView.ThirdPerson)
        {
            throw new NotSupportedException("CPlayer::Goto3rdPersonView (CThing3rdPersonCamera is not ported)");
        }
    }

    /// <summary>
    /// CPlayer::GotoPanView: a pan camera around the battle engine along a spline through points in its frame (half a
    /// pan on the levels that start standing on something), and GOTO_CONTROL_VIEW 0.05 s before the pan ends. Each
    /// point is the matrix times a constant vector with the zero terms folded: one or two products per row summed
    /// (004d2d1c..004d2f0e).
    /// </summary>
    public void GotoPanView(Game game, float forTime)
    {
        if (BattleEngine is not ComplexThing be)
        {
            return;
        }

        CurrentView = PlayerView.Pan;
        float panInterval = forTime;
        var points = new SPtrSet<StrongBox<FVector>>(context.Pool);
        FMatrix o = be.Orientation;
        if (game.CurrentLevel is 231 or 232 or 331 or 221 or 222 or 524 or 523 or 332)
        {
            points.Append(new StrongBox<FVector>(new FVector(o.Row0.X * 5.0f, o.Row1.X * 5.0f, o.Row2.X * 5.0f)));
        }
        else
        {
            points.Append(new StrongBox<FVector>(new FVector(
                (o.Row0.Y * 10.0f) + (o.Row0.Z * -4.3f), (o.Row1.Y * 10.0f) + (o.Row1.Z * -4.3f),
                (o.Row2.Y * 10.0f) + (o.Row2.Z * -4.3f))));
            points.Append(new StrongBox<FVector>(new FVector(
                (o.Row0.X * 5.0f) + (o.Row0.Z * 1.3f), (o.Row1.X * 5.0f) + (o.Row1.Z * 1.3f),
                (o.Row2.X * 5.0f) + (o.Row2.Z * 1.3f))));
        }

        points.Append(new StrongBox<FVector>(new FVector(
            (o.Row0.Y * -9.0f) + (o.Row0.Z * -1.3f), (o.Row1.Y * -9.0f) + (o.Row1.Z * -1.3f),
            (o.Row2.Y * -9.0f) + (o.Row2.Z * -1.3f))));
        points.Append(new StrongBox<FVector>(new FVector(o.Row0.Y * -2.5f, o.Row1.Y * -2.5f, o.Row2.Y * -2.5f)));

        var spline = new BSpline(points);
        game.SetCurrentCamera(Number - 1, new PanCamera(context, be, spline, panInterval));
        if (panInterval > 0.0f)
        {
            panInterval -= 0.05f;
        }

        context.Events.AddEventIn(panInterval, GotoControlViewEvent, this);
    }

    /// <summary>CPlayer::HandleEvent: at GOTO_CONTROL_VIEW, a panning player with a battle engine goes to its control view.</summary>
    public override void HandleEvent(Event e)
    {
        if (e.EventNum == GotoControlViewEvent && BattleEngine is not null && CurrentView == PlayerView.Pan)
        {
            GotoControlView(context.Game!);
        }
    }
}

/// <summary>EPlayerCameraView.</summary>
public enum PlayerView
{
    FirstPerson,
    ThirdPerson,
    Pan,
}

public sealed partial class Game
{
    private readonly Player?[] _player = new Player?[2];
    private readonly Camera?[] _currentCamera = new Camera?[2];
    private readonly Camera?[] _oldCamera = new Camera?[2];

    /// <summary>GetPlayer(n): mPlayer[n], or null.</summary>
    public Player? GetPlayer(int number) => number is >= 0 and < 2 ? _player[number] : null;

    /// <summary>GetCurrentCamera(n): mCurrentCamera[n], or null.</summary>
    public Camera? GetCurrentCamera(int number) => number is >= 0 and < 2 ? _currentCamera[number] : null;

    /// <summary>
    /// SetCurrentCamera (004705e0): the player's camera, the old one released; with the free camera on, the camera to
    /// return to changes instead.
    /// </summary>
    public void SetCurrentCamera(int number, Camera camera, bool releaseOldCamera = true)
    {
        Camera?[] slot = number == 0 && FreeCameraOn ? _oldCamera : _currentCamera;
        if (releaseOldCamera)
        {
            slot[number]?.Release();
        }

        slot[number] = camera;
    }

    /// <summary>
    /// CGame::LoadLevel after WORLD.Load (game.cpp:716): mPlayers is 2 for a multiplayer world, else 1, and each player
    /// is made (the controllers are the platform's).
    /// </summary>
    public void CreatePlayers(ThingContext context, bool multiplayerWorld)
    {
        Players = multiplayerWorld ? 2 : 1;
        for (int i = 0; i < Players; i++)
        {
            _player[i] = new Player(i + 1, context);
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
                    player.AssignBattleEngine(start.PlayerObject.ToRead);
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
