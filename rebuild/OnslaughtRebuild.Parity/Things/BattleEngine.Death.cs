// SPDX-License-Identifier: GPL-3.0-only
// Adapted from matched bea-decomp BattleEngine.cpp: StartDieProcess0040bfd0, Explode0040dfb0,
// dying Move00408416, Shutdown00405f80 and destructor00405a40. Pristine PC SHA-256:
// 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Particles;

namespace OnslaughtRebuild.Parity.Things;

public sealed partial class BattleEngine
{
    private ParticleEffectLink? _deathSmoke;

    /// <summary>mSmoke's current frame of reference; observing it does not advance the particles.</summary>
    public FoR? DeathSmoke => _deathSmoke?.Effect;

    /// <summary>
    /// StartDieProcess0040bfd0: the game hears the death before the script. Unlike CUnit's death, this neither
    /// adjusts unit counters nor schedules shutdown: the falling wreck's later contact does that.
    /// </summary>
    internal bool StartBattleEngineDeath()
    {
        if (IsDying) return false;
        if (Player is not Game.Player player || Context.Game is not { } game)
            throw new NotSupportedException("CBattleEngine::StartDieProcess requires its controlling player and game");
        if (!ReferenceEquals(game.RequirePlayerDeathSupported(player.Number), this))
            throw new InvalidOperationException("CBattleEngine::StartDieProcess requires the player's current battle engine");
        Context.Skip("CBattleEngine::StartDieProcess: controller SetVibration(0), no physical rumble device");
        Flags |= ThingFlags.Dying;
        game.DeclarePlayerDead(player.Number);
        if (Script is not null)
        {
            Script.Died();
            Script?.Delete();
            Script = null;
        }
        Explode();
        if (Context.Particles is { } particles)
        {
            _deathSmoke ??= new ParticleEffectLink(particles.Manager);
            particles.Manager.AddParticle(particles.Manager.Set.GetPD("Oily Smoke Effect"), _deathSmoke);
            _deathSmoke.SetPos(Pos);
        }
        return true;
    }

    /// <summary>
    /// Explode0040dfb0: configuration-named explosion, attached radius, ground before water, and the init object's
    /// default AllowVolumetric=false. This deliberately does not call CUnit's differently configured explosion.
    /// </summary>
    public void Explode()
    {
        byte[]? name = Configuration?.Explosion;
        int index = name is null ? -1 : Context.Physics.GetExplosionIndex(name);
        Explosion? explosion = Explosion.Spawn(Context, index);
        if (explosion is null) return;
        explosion.Init(new InitThing
        {
            Behaviour = Context.Physics.GetExplosion(name!),
            AttachedTo = this,
            Allegiance = Allegiance,
            UseAttachedRadius = true,
            ColType = IsOnGround() ? 1 : IsInWater() ? 2 : 0,
            Pos = Pos,
        });
    }

    /// <summary>
    /// The dying arm of Move, before the shared velocity/ground/gravity tail. The retained x87 square root is used
    /// for Z; X/Y reload its single-precision spill (0040842a..004084a1). No live part movement runs in this arm.
    /// </summary>
    internal void MoveDyingBattleEngine()
    {
        _deathSmoke?.SetPos(Pos);
        if (State != BattleEngineState.Jet) return;
        Pitchvel = .01f;
        double magnitude = X87.Sqrt(X87.Add(X87.Add(X87.Mul(Velocity.X, Velocity.X),
            X87.Mul(Velocity.Y, Velocity.Y)), X87.Mul(Velocity.Z, Velocity.Z)));
        float storedMagnitude = (float)magnitude;
        Velocity = new FVector((float)X87.Mul(storedMagnitude, Orientation.Row0.Y),
            (float)X87.Mul(storedMagnitude, Orientation.Row1.Y),
            (float)X87.Mul(magnitude, Orientation.Row2.Y));
    }

    /// <summary>Shutdown00405f80: remove flashes in head order, stop rumble and smoke, then CUnit shutdown.</summary>
    public override void Shutdown()
    {
        while (_damageFlashes?.First() is { } flash) _damageFlashes.Remove(flash);
        if (Player is Game.Player)
            Context.Skip("CBattleEngine::Shutdown: controller SetVibration(0), no physical rumble device");
        _deathSmoke?.Kill();
        base.Shutdown();
    }

    /// <summary>
    /// The implemented BE-owned parts of destructor00405a40, after equipment destruction and before base deletion.
    /// Destroying monitors invalidates their queued events; merely nulling the owner fields would leave them live.
    /// </summary>
    internal void DeleteBattleEngineParts()
    {
        CurrentWeapon = null;
        if (Cockpit?.RenderThing is { } cockpitRender) Context.Particles?.World.ReleaseRenderThing(cockpitRender);
        Cockpit?.Destroy();
        Cockpit = null;
        if (WalkerPart is { } walker)
        {
            foreach (Weapon weapon in walker.Weapons) weapon.Release();
            walker.Weapons.Clear();
            walker.PrimaryWeapon?.Release();
            walker.PrimaryWeapon = null;
            walker.AugWeapon?.Release();
            walker.AugWeapon = null;
            WalkerPart = null;
        }
        if (JetPart is { } jet)
        {
            foreach (Weapon weapon in jet.Weapons) weapon.Release();
            jet.Weapons.Clear();
            JetPart = null;
        }
        if (OtherMeshRenderThing is { } otherRender) Context.Particles?.World.ReleaseRenderThing(otherRender);
        OtherMeshRenderThing = null;
        DeleteMotionController(_backupMotionController);
        _backupMotionController = null;
        _deathSmoke?.Release();
        _deathSmoke = null;
        // Embedded readers are destroyed in reverse declaration order (00405c41..00405d45).
        HadAutoAimTarget.Release();
        Player = null;
        AutoAimTarget.Release();
        CurrentUnitOverCrosshairRegardlessOfRange.Release();
        CurrentUnitOverCrosshair.Release();
        StandingOnThing.Release();
    }

    /// <summary>CComplexThing's attached controller follows its script/animation; CThing owns current-mesh cleanup.</summary>
    protected override void DeleteAttachedController()
    {
        DeleteMotionController(MotionController);
        MotionController = null;
    }

    private void DeleteMotionController(MCBattleEngine? controller)
    {
        if (controller is null) return;
        controller.SetAttached(false);
        controller.Destroy();
        Context.MotionControllers.Remove(controller);
    }
}
