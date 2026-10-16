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

    /// <summary>GetOrientation: the view's axes (local Y forward, local -Z up); a renderer reads it.</summary>
    public virtual FMatrix GetOrientation() => FMatrix.Identity;

    /// <summary>GetZoom: the tangent of half the horizontal view (1 is 90 degrees across).</summary>
    public virtual float GetZoom() => 1.0f;

    /// <summary>GetShowHUD: whether the HUD draws over this camera's view (only a camera on a battle engine says so).</summary>
    public virtual bool GetShowHud() => false;

    /// <summary>GetOldPos: the previous update's position (the current one unless the camera keeps it).</summary>
    public virtual FVector GetOldPos() => GetPos();

    /// <summary>GetOldOrientation.</summary>
    public virtual FMatrix GetOldOrientation() => GetOrientation();

    /// <summary>GetOldZoom.</summary>
    public virtual float GetOldZoom() => GetZoom();

    /// <summary>
    /// CInterpolatedCamera (0041ad30): what a render draws from: the position, orientation and zoom from the camera's
    /// previous update towards its current one by GetFrameRenderFraction2, the orientation renormalised by FMatrix's
    /// Normalise (rows 0 and 1, then the cross products).
    /// </summary>
    public static (FVector Pos, FMatrix Ori, float Zoom) RenderView(Camera camera, float fraction)
    {
        FVector pos = camera.GetPos();
        FVector oldpos = camera.GetOldPos();
        FVector p = oldpos + ((pos - oldpos) * fraction);
        FMatrix ori = camera.GetOrientation();
        FMatrix oldori = camera.GetOldOrientation();
        FMatrix o = oldori.Add(ori.Add(oldori.Scale(-1.0f)).Scale(fraction)).NormaliseRows();
        float zoom = camera.GetZoom();
        float oldzoom = camera.GetOldZoom();
        return (p, o, oldzoom + ((zoom - oldzoom) * fraction));
    }

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

    /// <summary>GetShowHUD: on a battle engine.</summary>
    public override bool GetShowHud() => _forThing.ToRead?.IsA(ThingType.BattleEngine) == true;

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

    /// <summary>GetOrientation (00418d80): the thing's, or the identity without one.</summary>
    public override FMatrix GetOrientation() => _forThing.ToRead?.GetOrientation() ?? FMatrix.Identity;

    /// <summary>GetZoom (00418eb0): a battle engine's zoom, otherwise 1.</summary>
    public override float GetZoom() => _forThing.ToRead is Things.BattleEngine engine ? engine.Zoom : 1.0f;

    /// <summary>GetOldPos (00418dd0): the thing's previous position (a battle engine's as it is).</summary>
    public override FVector GetOldPos()
    {
        Thing? thing = _forThing.ToRead;
        if (thing is null)
        {
            return new FVector(0, 0, 0);
        }

        if (thing is not Things.Actor actor || !thing.IsA(ThingType.BattleEngine))
        {
            throw new NotSupportedException("CThingCamera::GetOldPos of a thing other than a battle engine (bounding boxes are not ported)");
        }

        return actor.OldPos;
    }

    /// <summary>GetOldOrientation (00418e80): the thing's previous orientation.</summary>
    public override FMatrix GetOldOrientation() =>
        _forThing.ToRead is Things.Actor actor ? actor.OldOrientation : _forThing.ToRead?.GetOrientation() ?? FMatrix.Identity;

    /// <summary>GetOldZoom (00418ed0): a battle engine's previous zoom, otherwise 1.</summary>
    public override float GetOldZoom() => _forThing.ToRead is Things.BattleEngine engine ? engine.OldZoom : 1.0f;
}

/// <summary>CPlayer: a player's number (from 1), battle engine and view.</summary>
/// <summary>EPlayerStats.</summary>
public enum PlayerStat
{
    UnitsDestroyed,
    RoundsFired,
    RoundsHit,
    Cheated,
    TimeAsJet,
    TimeAsWalker,
    DamageTaken,
}

public sealed class Player(int number, ThingContext context) : Monitor, Platform.IButtonReceiver
{
    /// <summary>mStat: the player's statistics (EPlayerStats order).</summary>
    public int[] Stats { get; } = new int[7];

    /// <summary>IncStat (player.h).</summary>
    public void IncStat(PlayerStat stat, int value = 1) => Stats[(int)stat] += value;

    /// <summary>mThingsKilled: the enemies this player killed, by kind (KillType's order).</summary>
    public int[] ThingsKilled { get; } = new int[5];

    /// <summary>KilledEnemyThing (004d30d0): each kind the unit is counts it.</summary>
    public void KilledEnemyThing(Unit thing)
    {
        if (thing.IsA(ThingType.AirUnit)) ThingsKilled[0]++;
        if (thing.IsA(ThingType.Vehicle)) ThingsKilled[1]++;
        if (thing.IsA(ThingType.Emplacement)) ThingsKilled[2]++;
        if (thing.IsA(ThingType.Infantry)) ThingsKilled[3]++;
        if (thing.IsA(ThingType.Mech)) ThingsKilled[4]++;
    }

    /// <summary>mTimeoutTime: the platform time of the last button.</summary>
    public float TimeoutTime;

    bool Platform.IButtonReceiver.CanBeControlledWhenInPause => false;

    Platform.ControlType Platform.IButtonReceiver.ControlType => Platform.ControlType.Mech;

    /// <summary>
    /// CPlayer::ReceiveButtonAction (004d3110): nothing outside play; the inactivity clock; pause, help and skipping the
    /// pan; then, with a powered battle engine that is not dying: the look axes inverted by the career (read as off: the
    /// wiped career) and shaped by tan(1.2 v) * 3 over tan(1.2) * 3 (fptan's value approximated in double); the engine's
    /// own buttons; and the walker's moves.
    /// The jet, morphing, zoom, configuration, weapons and cloak are not ported (they throw).
    /// </summary>
    public void ReceiveButtonAction(Platform.Controller from, int button, float val)
    {
        Game game = context.Game!;
        if (game.State > GameState.Playing || game.State <= GameState.PreRunning)
        {
            return;
        }

        TimeoutTime = game.Clock.GetSysTimeFloat();
        if (button == Platform.Buttons.Pause)
        {
            game.Pause(true, from);
            return;
        }

        if (button == Platform.Buttons.SkipPanning && CurrentView == PlayerView.Pan && game.State == GameState.Panning)
        {
            throw new NotSupportedException("CPlayer::ReceiveButtonAction: skipping the pan is not ported");
        }

        if (game.State < GameState.Playing)
        {
            return;
        }

        if (BattleEngine is not Things.BattleEngine be || be.IsDying || !be.PoweredUp)
        {
            return;
        }

        if (button is Platform.Buttons.MechYawRight or Platform.Buttons.MechYawLeft or Platform.Buttons.MechPitchUp
            or Platform.Buttons.MechPitchDown)
        {
            // As compiled (004d322d..004d327d): fptan inline, its result times 3 rounded once at the 24-bit control word
            // (the tangent itself is never stored); the negative arm multiplies by -1.2f and negates after; the divisor
            // is the static t2, stored as a float.
            float t1 = val > 0.0f
                ? (float)Numerics.X87.Mul(Numerics.X87.Tan(val * 1.2f), 3.0f)
                : -(float)Numerics.X87.Mul(Numerics.X87.Tan(val * -1.2f), 3.0f);
            val = t1 / LookCurveScale;
        }

        if (be.State is not (BattleEngineState.MorphingIntoWalker or BattleEngineState.MorphingIntoJet))
        {
            switch (button)
            {
                case Platform.Buttons.MechMorph:
                case Platform.Buttons.MechChangeZoomIn:
                case Platform.Buttons.MechChangeZoomOut:
                case Platform.Buttons.MechChargeGunPod:
                    be.ChargeWeapon();
                    break;
                case Platform.Buttons.MechFireGunPod:
                    be.FireWeapon();
                    break;
                case Platform.Buttons.MechChangeWeapon:
                case Platform.Buttons.MechCloak:
                    throw new NotSupportedException($"CPlayer::ReceiveButtonAction: button {button} (the battle engine's own action) is not ported");
            }
        }

        if (be.State == BattleEngineState.Walker)
        {
            BattleEngineWalkerPart walker = be.WalkerPart!;
            switch (button)
            {
                case Platform.Buttons.MechForward:
                    walker.Forward(val);
                    break;
                case Platform.Buttons.MechBackward:
                    walker.Backward(val);
                    break;
                case Platform.Buttons.MechStrafeLeft:
                    walker.StrafeLeft(val);
                    break;
                case Platform.Buttons.MechLandingJets:
                    walker.ActivateLandingJets();
                    break;
                case Platform.Buttons.MechStrafeRight:
                    walker.StrafeRight(val);
                    break;
                case Platform.Buttons.MechYawLeft:
                case Platform.Buttons.MechYawRight:
                    walker.Rotate(val);
                    break;
                case Platform.Buttons.MechPitchUp:
                case Platform.Buttons.MechPitchDown:
                    walker.Pitch(val);
                    break;
            }

            return;
        }

        if (be.State == BattleEngineState.Jet)
        {
            throw new NotSupportedException("CPlayer::ReceiveButtonAction: the jet's controls are not ported");
        }
    }

    /// <summary>
    /// The static t2 = (float)tan(1.2f) * 3.0f as compiled: the tangent of the double constant 1.2f (fldl 005de798),
    /// times 3, rounded once and stored as a float (0082b5b8).
    /// </summary>
    private static readonly float LookCurveScale = (float)Numerics.X87.Mul(Numerics.X87.Tan((double)1.2f), 3.0f);

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
        if (battleEngine is Things.BattleEngine be)
        {
            be.Player = this; // CBattleEngine::SetPlayer
        }
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
