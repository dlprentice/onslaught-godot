// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using Godot;
using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.GodotClient;

// This is deliberately the Level 100 path adapter, not a reusable audio engine.
// Mission, frontend, flight, actor, destruction, HUD, and pause lanes decide
// their events and state. This node owns only exact stream selection, released
// queue/mix/pause behavior, presentation pitch/volume, and stream lifetime.
public sealed partial class Level100Audio : Node3D
{
    private const double RetailSoundUpdateSeconds =
        1d / SimulationConstants.RetailTicksPerSecond;

    // CMessageBox__TryAdvanceQueuedMessage (0x004b7b80) promotes the portrait
    // and text state, then schedules event 0xbbc with immediate 0x3e4ccccd =
    // 0.20f. CMessageBox__StartVoiceOrFallbackTextReveal runs from that later
    // event, so voice begins four released 20 Hz ticks after activation.
    private const double RetailCharacterMessageVoiceLeadSeconds = 0.2d;

    // Retail CMessageBox__AdvanceRevealAndScheduleNextTick (0x004b8020) schedules
    // the message-completion event 0xbba with the immediate float constant
    // 0x3e99999a = 0.30f. That is six ticks of the released 0.05 s event-manager
    // clock (thing.h CLOCK_TICK 0.05f; CEventManager::AdvanceTime sets
    // mTime = mFrameCount * CLOCK_TICK). The previous 6d/30d here applied that
    // six-tick count to this reconstruction's 30 Hz Core rate instead of retail's
    // 20 Hz clock and was therefore 0.2 s, a third short.
    //
    // NOT YET AT PARITY: retail starts this delay when the TEXT REVEAL completes,
    // whereas this adapter starts it from the voice stream's Finished signal.
    // Those coincide only when the voice outlasts the reveal. Correcting the
    // trigger needs the reveal clock, which lives in the HUD lane.
    private const double RetailCharacterMessageHandoffSeconds = 0.3d;

    private readonly Dictionary<string, AudioStream> _pcmStreams =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, AudioStreamWav> _loopStreams =
        new(StringComparer.Ordinal);
    private readonly Dictionary<Level100TerminalCue, AudioStream> _terminalStreams = [];
    private readonly Dictionary<string, AudioStream> _frontendStreams =
        new(StringComparer.Ordinal);
    private readonly Dictionary<int, AudioStreamOggVorbis> _voiceStreams = [];
    private readonly Level100CharacterMessageQueue _queuedCharacterMessages = new();
    private readonly List<AudioStreamPlayer3D> _gameplayOneShots = [];
    private readonly List<AudioStreamPlayer> _terminalOneShots = [];
    private readonly List<AudioStreamPlayer> _frontendOneShots = [];
    private readonly Dictionary<AudioStreamPlayer3D, float> _gameplayBaseVolumes = [];
    private readonly Dictionary<AudioStreamPlayer, float> _terminalBaseVolumes = [];
    private readonly Dictionary<AudioStreamPlayer, float> _frontendBaseVolumes = [];
    private readonly RetailMusicPolicy _musicPolicy = new();

    internal AudioPlaybackRetirement PlaybackRetirement { get; set; } = null!;

    private AudioStreamPlayer _tutorialVoice = null!;

    // Retail owns exactly one music channel: the CMusic singleton at
    // &0x00889a48, whose every PlaySelection replaces the single current song.
    // One player here keeps that law, so a frontend track and a level track can
    // never overlap.
    private AudioStreamPlayer _music = null!;
    private AudioStreamOggVorbis? _tutorialMusicStream;
    private AudioStreamOggVorbis? _frontendMusicStream;
    private double _musicUpdateAccumulatorSeconds;
    private Node3D? _aquila;
    private Level100ActorId? _aquilaActorId;
    private AudioStreamPlayer3D? _aquilaFlightLoop;
    private float _aquilaFlightLoopSubVolume;
    private float _aquilaFlightLoopTargetSubVolume;
    private float _aquilaFlightLoopFadeStep;
    private double _aquilaFlightLoopFadeAccumulatorSeconds;
    private AudioStreamPlayer3D? _aquilaWarningLoop;
    private AquilaWarningAudioState _aquilaWarningState;
    private float _aquilaWarningLoopSubVolume;
    private float _aquilaWarningLoopTargetSubVolume;
    private float _aquilaWarningLoopFadeStep;
    private double _aquilaWarningLoopFadeAccumulatorSeconds;
    private AquilaWarningAudioState _aquilaWarningLoopState;
    private AudioStreamPlayer3D? _trainerLoop;
    private AudioStreamPlayer3D? _transportLoop;
    private AudioStreamPlayer3D? _repairPadIdleLoop;
    private int? _hostileEnvironmentMissionStartTick;
    private int _lastHostileEnvironmentContactTick;
    private int? _activeCharacterSpeakerId;
    private int? _activeCharacterMessageId;
    private double _activeCharacterMessageLengthSeconds;
    private double _characterMessageVoiceLeadSecondsRemaining;
    private double _characterMessageHandoffSecondsRemaining;
    // Retail's AUTHORED out-of-box option volumes, not full scale. Corrected
    // 2026-07-27 from 1f/1f under GOAL.md's defaults rule; the reconstruction's
    // defaults are retail's defaults, and 1f was ours, not theirs.
    //
    // Two independent sources agree exactly, which is why these are stated
    // rather than fitted:
    //   references/Onslaught/Career.cpp:173-174, CCareer::CCareer --
    //       mSoundVolume=0.8f;
    //       mMusicVolume=0.9f;
    //   and the pristine specimen's own initialiser bytes
    //   (local-lab/safe-copy-bea-pristine/BEA.exe.original.backup, sha256
    //   74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750),
    //   in CCareer::StaticInitDefaults 0x0041B6A0:
    //       [retail instruction listing removed]
    //                      mov dword [0x00662AAC], 0.8f   <- sound
    //       [retail instruction listing removed]
    //                      mov dword [0x00662AB0], 0.9f   <- music
    //
    // A fresh career therefore reads 8/10 and 9/10 on the Options bars, not
    // 10/10.
    //
    // The released PC setters do NOT share the retained-source tangent curve.
    // CSoundManager::SetMasterVolume (retail 0x004E04C0) stores the supplied
    // float directly. CMusic::SetVolume (retail 0x004BBA10) rounds option*127
    // into its integer set-volume field while preserving the original career
    // float. Keep those differently typed laws visible rather than hiding them
    // behind a shared "mix" helper.
    private float _soundMasterVolume =
        Level100AudioCatalog.ToRetailSoundMasterVolume(RetailSoundOptionValue);

    /// <summary>
    /// Retail's authored cold-start sound option value, <c>CCareer::CCareer</c>
    /// (<c>references/Onslaught/Career.cpp:173</c>, <c>mSoundVolume=0.8f</c>).
    /// </summary>
    internal const float RetailSoundOptionValue = 0.8f;

    /// <summary>
    /// Retail's authored cold-start music option value, <c>CCareer::CCareer</c>
    /// (<c>references/Onslaught/Career.cpp:174</c>, <c>mMusicVolume=0.9f</c>).
    /// </summary>
    internal const float RetailMusicOptionValue = 0.9f;
    private float _gameplayMix = 1f;
    private bool _gameplayPaused;

    public bool TutorialVoicePlaying =>
        CharacterMessagePlayback is { Playing: true, Paused: false };

    // HUD owns paging and lip presentation. It can poll this presentation-only
    // state without feeding playback timing back into deterministic mission waits.
    public Level100MessagePlaybackState CharacterMessagePlayback
    {
        get
        {
            bool playing =
                _activeCharacterMessageId.HasValue &&
                GodotObject.IsInstanceValid(_tutorialVoice) &&
                _tutorialVoice.Playing;
            double position = playing
                ? Math.Clamp(
                    _tutorialVoice.GetPlaybackPosition(),
                    0d,
                    _activeCharacterMessageLengthSeconds)
                : 0d;
            return new Level100MessagePlaybackState(
                _activeCharacterSpeakerId,
                _activeCharacterMessageId,
                position,
                _activeCharacterMessageLengthSeconds,
                playing,
                playing && _tutorialVoice.StreamPaused);
        }
    }

    public bool TutorialMusicPlaying =>
        MusicPlaying(Level100AudioCatalog.TutorialMusic);

    public bool FrontendMusicPlaying =>
        MusicPlaying(Level100AudioCatalog.FrontendMusic);

    public override void _Ready()
    {
        Name = "Level100Audio";
        ProcessMode = ProcessModeEnum.Always;

        _tutorialVoice = new AudioStreamPlayer
        {
            Name = "RetailLevel100TutorialVoice",
            ProcessMode = ProcessModeEnum.Always,
            VolumeDb = MixedSoundVolumeDb(
                Level100AudioCatalog.RetailRadioMessageVolume,
                gameplay: true,
                Level100AudioCatalog.RetailListenerSourceVolume),
        };
        _tutorialVoice.Finished += BeginCharacterMessageHandoff;
        AddChild(_tutorialVoice);

        _music = new AudioStreamPlayer
        {
            Name = "RetailMusic",
            ProcessMode = ProcessModeEnum.Always,
            VolumeDb = MusicVolumeDb(_musicPolicy.CurrentVolume),
        };
        _music.Finished += HandleMusicFinished;
        AddChild(_music);
    }

    public override void _Process(double delta)
    {
        AdvanceMusicPolicy(delta);
        if (_gameplayPaused)
        {
            return;
        }

        AdvanceAquilaFlightLoopFade(delta);
        AdvanceAquilaWarningLoopFade(delta);

        // CSoundManager::UpdateStatus recomputes GetVolumeForPos and re-Fades
        // every live ST_FOLLOW* event on every update
        // (references/Onslaught/SoundManager.cpp:1361-1370), so the listener
        // walking away from a sound attenuates it. Doing this only at start
        // would leave every moving emitter - the jet loop, the Air Trainer
        // flyby, the transport - stuck at its first frame's level.
        UpdateSpatialAttenuation();

        if (!double.IsFinite(delta) || delta <= 0d)
        {
            return;
        }

        if (_characterMessageVoiceLeadSecondsRemaining > 0d)
        {
            _characterMessageVoiceLeadSecondsRemaining -= delta;
            if (_characterMessageVoiceLeadSecondsRemaining <= 0d)
            {
                _characterMessageVoiceLeadSecondsRemaining = 0d;
                StartNextCharacterMessage();
            }
            return;
        }

        if (_characterMessageHandoffSecondsRemaining <= 0d)
        {
            return;
        }

        _characterMessageHandoffSecondsRemaining -= delta;
        if (_characterMessageHandoffSecondsRemaining <= 0d)
        {
            _characterMessageHandoffSecondsRemaining = 0d;
            _characterMessageVoiceLeadSecondsRemaining =
                RetailCharacterMessageVoiceLeadSeconds;
        }
    }

    public void StartTutorialMusic() => PlayMusic(
        RetailMusicSelection.Tutorial,
        Level100AudioCatalog.TutorialMusic);

    // CFrontEnd::Init (FrontEnd.cpp:332-333, retail 0x004662a0) starts
    // MUS_FRONTEND once for the whole frontend, so one call covers click-to-start
    // through briefing.
    public void StartFrontendMusic() => PlayMusic(
        RetailMusicSelection.Frontend,
        Level100AudioCatalog.FrontendMusic);

    public void StopTutorialMusic() => StopMusic();

    public void StopFrontendMusic() => StopMusic();

    private bool MusicPlaying(Level100MusicRecipe recipe) =>
        GodotObject.IsInstanceValid(_music) &&
        _music.Playing &&
        _musicPolicy.IsPlaying &&
        StringComparer.Ordinal.Equals(
            _musicPolicy.CurrentTrackIdentity,
            recipe.ResourcePath);

    // At track end CMusic::UpdateStatus re-enters PlaySelection with fade 0
    // (Music.cpp:298-299, retail 0x004bb530). The Client policy owns that replay;
    // the Godot stream remains a non-looping decoder/device adapter.
    private void PlayMusic(
        RetailMusicSelection selection,
        Level100MusicRecipe recipe)
    {
        if (MusicPlaying(recipe))
        {
            return;
        }

        ApplyMusicActions(_musicPolicy.PlaySelection(
            selection,
            recipe.ResourcePath));
    }

    private void StopMusic()
    {
        ApplyMusicActions(_musicPolicy.Kill());
        _musicUpdateAccumulatorSeconds = 0d;
    }

    private void HandleMusicFinished()
    {
        ApplyMusicActions(_musicPolicy.HandleTrackFinished());
    }

    // The deterministic policy owns update order and integer steps. This adapter
    // only schedules those steps on the existing presentation update cadence;
    // it does not claim decoder, DirectSound, or audible timing parity.
    private void AdvanceMusicPolicy(double delta)
    {
        if (!double.IsFinite(delta) || delta <= 0d || !_musicPolicy.IsPlaying)
        {
            return;
        }

        _musicUpdateAccumulatorSeconds += delta;
        while (_musicUpdateAccumulatorSeconds >= RetailSoundUpdateSeconds &&
               _musicPolicy.IsPlaying)
        {
            _musicUpdateAccumulatorSeconds -= RetailSoundUpdateSeconds;
            ApplyMusicActions(_musicPolicy.AdvanceFadeStep());
        }
    }

    private void ApplyMusicActions(IReadOnlyList<RetailMusicAction> actions)
    {
        foreach (RetailMusicAction action in actions)
        {
            switch (action.Kind)
            {
                case RetailMusicActionKind.SetVolume:
                    _music.VolumeDb = MusicVolumeDb(action.Volume);
                    break;
                case RetailMusicActionKind.Stop:
                    _music.Stop();
                    _music.Stream = null;
                    break;
                case RetailMusicActionKind.Play:
                    _music.Stream = MusicStream(action.TrackIdentity ??
                        throw new InvalidOperationException(
                            "A music play action requires a track identity."));
                    _music.Play();
                    PlaybackRetirement.Observe(_music);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }
        }
    }

    private AudioStreamOggVorbis MusicStream(string trackIdentity)
    {
        if (StringComparer.Ordinal.Equals(
                trackIdentity,
                Level100AudioCatalog.TutorialMusic.ResourcePath))
        {
            return _tutorialMusicStream ??=
                LoadOgg(trackIdentity, looping: false);
        }
        if (StringComparer.Ordinal.Equals(
                trackIdentity,
                Level100AudioCatalog.FrontendMusic.ResourcePath))
        {
            return _frontendMusicStream ??=
                LoadOgg(trackIdentity, looping: false);
        }

        throw new ArgumentOutOfRangeException(
            nameof(trackIdentity),
            trackIdentity,
            "The Level 100 adapter received an unowned music identity.");
    }

    public void BindAquila(
        Level100ActorId actorId,
        Level100ActorRegistrySnapshot actors)
    {
        ArgumentNullException.ThrowIfNull(actors);
        if (_aquilaActorId == actorId &&
            GodotObject.IsInstanceValid(_aquila))
        {
            UpdateAquilaPose(actors);
            return;
        }

        StopAquilaFlightLoop();
        StopAquilaWarningLoop();
        _aquilaWarningState = AquilaWarningAudioState.Normal;
        ReleaseAquilaBinding();
        _aquilaActorId = actorId;
        _aquila = new Node3D
        {
            Name = $"RetailAquilaAudioActor{actorId.Value}",
        };
        AddChild(_aquila);
        UpdateAquilaPose(actors);
    }

    // The native actor registry remains the sole position owner. This anchor
    // retains no velocity, lifecycle, resource, or mission state of its own.
    public void UpdateAquilaPose(Level100ActorRegistrySnapshot actors)
    {
        ArgumentNullException.ThrowIfNull(actors);
        if (!_aquilaActorId.HasValue ||
            !GodotObject.IsInstanceValid(_aquila))
        {
            throw new InvalidOperationException(
                "The Level 100 Aquila audio owner is not bound.");
        }

        Level100ActorSnapshot actor = RequireActor(
            actors,
            _aquilaActorId.Value);
        _aquila!.Position = ToGodotWorld(actor.Pose.PositionMillimeters);
    }

    // The flight owner calls this once for each ordered mechanics event. This
    // adapter does not infer an edge from snapshots or suppress same-frame cues.
    public void PlayAquilaTransition(AquilaTransitionCue cue)
    {
        Node3D aquila = _aquila ??
            throw new InvalidOperationException("The Level 100 Aquila audio owner is not bound.");
        switch (cue)
        {
            case AquilaTransitionCue.Takeoff:
                PlayAttached(
                    aquila,
                    "RetailAquilaTakeoff",
                    Level100AudioCatalog.GetAquilaTransition(cue));
                FadeInAquilaFlightLoop(aquila);
                break;
            case AquilaTransitionCue.InFlight:
                FadeInAquilaFlightLoop(aquila);
                break;
            case AquilaTransitionCue.Landing:
                FadeOutAquilaFlightLoop();
                PlayAttached(
                    aquila,
                    "RetailAquilaLanding",
                    Level100AudioCatalog.GetAquilaTransition(cue));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(cue));
        }
    }

    public void StopAquilaFlightLoop()
    {
        StopLoop(ref _aquilaFlightLoop);
        _aquilaFlightLoopSubVolume = 0f;
        _aquilaFlightLoopTargetSubVolume = 0f;
        _aquilaFlightLoopFadeStep = 0f;
        _aquilaFlightLoopFadeAccumulatorSeconds = 0d;
    }

    public void ConsumeAquilaFlightEvents(
        IReadOnlyList<AquilaFlightEvent> events,
        int simulationTick,
        int missionTick)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (missionTick < 0 || missionTick > simulationTick + SimulationConstants.Level100PreRunTicks)
        {
            throw new ArgumentOutOfRangeException(nameof(missionTick));
        }

        // SimActions.Reset deliberately keeps Simulation.Tick monotonic while
        // replacing Level100Mission with a fresh instance, and a mission's
        // clock starts at its load, the three-second pre-run before its first
        // tick. Their difference is therefore the current mission's time-zero
        // point and reproduces CBattleEngine's constructor value of 0 without
        // adding presentation timing to Core state.
        int missionStartTick = checked(simulationTick - missionTick);
        if (_hostileEnvironmentMissionStartTick != missionStartTick)
        {
            _hostileEnvironmentMissionStartTick = missionStartTick;
            _lastHostileEnvironmentContactTick = missionStartTick;
        }

        foreach (AquilaFlightEvent flightEvent in events)
        {
            switch (flightEvent.Kind)
            {
                case AquilaFlightEvents.WalkerToJetStarted:
                    PlayAquilaTransition(AquilaTransitionCue.Takeoff);
                    break;
                case AquilaFlightEvents.JetToWalkerStarted:
                    PlayAquilaTransition(AquilaTransitionCue.Landing);
                    break;
                case AquilaFlightEvents.TransformCompleted
                    when flightEvent.Mode == VehicleMode.Jet:
                    PlayAquilaTransition(AquilaTransitionCue.InFlight);
                    break;
                case AquilaFlightEvents.TransformCompleted
                    when flightEvent.Mode == VehicleMode.Walker:
                    FadeOutAquilaFlightLoop();
                    break;
                case AquilaFlightEvents.WalkerHydraulicsRequested:
                    PlayOnAquila(Level100EffectCue.AquilaHydraulics);
                    break;
                case AquilaFlightEvents.WalkerDashRequested:
                    PlayOnAquila(Level100EffectCue.AquilaStrafe);
                    break;
                case AquilaFlightEvents.WaterSkim:
                    if (Level100AudioCatalog.ObserveHostileEnvironmentContact(
                        flightEvent.Tick,
                        ref _lastHostileEnvironmentContactTick))
                    {
                        PlayTerminalCue(Level100TerminalCue.HostileEnvironment);
                    }
                    break;
            }
        }
    }

    // The jet's Mech Vulcan Cannon cue used to be derived here from
    // AquilaFlightEvents.JetWeaponFireRequested. It is not any more: Core now
    // emits one ordered Level100WeaponFireEvent per weapon RELEASE for all
    // three player weapons, so there is a single producer of weapon-fire cues
    // instead of a walker owner and a separate jet owner. The flight event
    // itself is unchanged and still hashed - only the audio derivation moved,
    // and the jet cue, tick and emitter are identical either way.
    public void ConsumeLevel100WeaponFireEvents(
        IReadOnlyList<Level100WeaponFireEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (Level100WeaponFireEvent fireEvent in events)
        {
            // Exactly one cue per event, never one per RoundCount. The released
            // launch body issues its single PlayEffect before the volley loop -
            // see Level100WeaponFireEvent for the disassembly - so a four-round
            // Twin Vulcan volley is one report, not four overlapping ones.
            PlayOnAquila(fireEvent.Weapon switch
            {
                Level100PlayerWeapon.PulseCannonPod =>
                    Level100EffectCue.PulseCannonFire,
                // Both Vulcan weapon modes name the same released
                // CWeaponLaunchSound, `BE Vulcan Cannon` = sounds.sfx record 42.
                Level100PlayerWeapon.MechTwinVulcanCannon or
                Level100PlayerWeapon.MechVulcanCannon =>
                    Level100EffectCue.VulcanCannonFire,
                // Both pod modes name `BE Micro Missile Fire`; with
                // CWeaponSoundPerBurst 0 it plays once per burst event.
                Level100PlayerWeapon.MissilePod =>
                    Level100EffectCue.MicroMissileFire,
                _ => throw new InvalidDataException(
                    $"Core released an unknown Level 100 player weapon " +
                    $"{fireEvent.Weapon}."),
            });
        }
    }

    public void SetAquilaFlightPitch(float thrusterFraction)
    {
        if (!float.IsFinite(thrusterFraction) || thrusterFraction is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(thrusterFraction));
        }

        if (IsPlaying(_aquilaFlightLoop))
        {
            // The jet's producer is CBattleEngine's
            // `SOUND.SetPitch(event, 1.f + (GetJetPart()->GetThrusterValue() * 0.25f));`
            // (references/Onslaught/BattleEngine.cpp:1542), but the released PC
            // device layer clamps it away before the buffer ever sees it
            // (references/Onslaught/pcsoundmanager.cpp:398-401). Retail's jet
            // engine is a FLAT drone; this used to raise it by a musical third
            // with throttle, one of the most recognisable sounds in Level 100's
            // flight segment.
            _aquilaFlightLoop!.PitchScale =
                Level100AudioCatalog.RetailPcPitchMultiplier(
                    1f + (thrusterFraction * 0.25f));
        }
    }

    public void SetAquilaWarningState(AquilaWarningAudioState state)
    {
        if (_aquilaWarningState == state && IsPlaying(_aquilaWarningLoop))
        {
            return;
        }

        _aquilaWarningState = state;
        if (state == AquilaWarningAudioState.Normal)
        {
            if (IsPlaying(_aquilaWarningLoop))
            {
                _aquilaWarningLoopTargetSubVolume = 0f;
                _aquilaWarningLoopFadeStep =
                    -Level100AudioCatalog.RetailFlightLoopFadeStep;
            }
            return;
        }

        // IsEffectPlaying remains true while FadeTo is active. If the same
        // warning condition returns during its recovery tail, retail lets that
        // event finish fading and starts a fresh event only after it stops.
        // Switching to the other warning still takes the hard replacement path
        // below.
        if (IsPlaying(_aquilaWarningLoop) &&
            _aquilaWarningLoopState == state)
        {
            return;
        }

        // Retail replaces one warning with the other immediately. Only the
        // transition from a live warning back to Normal uses FadeTo(..., 0,
        // 0.02); do not smear HullCritical <-> EnergyLow together.
        StopAquilaWarningLoop();
        Node3D aquila = _aquila ??
            throw new InvalidOperationException("The Level 100 Aquila audio owner is not bound.");
        SetSpecificLoop(
            ref _aquilaWarningLoop,
            aquila,
            state == AquilaWarningAudioState.EnergyLow
                ? "RetailAquilaEnergyLowLoop"
                : "RetailAquilaHullCriticalLoop",
            Level100AudioCatalog.GetAquilaWarning(state),
            active: true);
        _aquilaWarningLoopSubVolume =
            Level100AudioCatalog.RetailUnfadedSubVolume;
        _aquilaWarningLoopTargetSubVolume =
            Level100AudioCatalog.RetailUnfadedSubVolume;
        _aquilaWarningLoopFadeStep = 0f;
        _aquilaWarningLoopFadeAccumulatorSeconds = 0d;
        _aquilaWarningLoopState = state;
    }

    public void PlayOnAquila(Level100EffectCue cue)
    {
        if (cue is not (
            Level100EffectCue.AquilaStrafe or
            Level100EffectCue.AquilaHydraulics or
            Level100EffectCue.AquilaIncomingMissile or
            Level100EffectCue.AquilaTargetLocked or
            Level100EffectCue.AquilaTargetAcquired or
            Level100EffectCue.PulseCannonFire or
            Level100EffectCue.VulcanCannonFire or
            Level100EffectCue.MicroMissileFire))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cue),
                cue,
                "This Level 100 event is not owned by the Aquila.");
        }

        Node3D aquila = _aquila ??
            throw new InvalidOperationException("The Level 100 Aquila audio owner is not bound.");
        PlayAttached(aquila, $"Retail{cue}", Level100AudioCatalog.GetEffect(cue));
    }

    public void ConsumeLevel100DestructionEvents(
        IReadOnlyList<Level100DestructionEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (Level100DestructionEvent item in events)
        {
            Level100EffectCue? cue = item.EffectKind switch
            {
                Level100DestructionEffectKind.None => null,
                Level100DestructionEffectKind.PulseImpact =>
                    Level100EffectCue.PulseImpact,
                // The released `Mech Bullet Hit` explosion record carries no
                // CExplosionSound field. Its direct visual sprite is consumed
                // by FirstFlightWorldView without inventing an impact sample.
                Level100DestructionEffectKind.VulcanImpact => null,
                // `Micro Missile Hit` carries CExplosionSound `Explosion Medium`.
                Level100DestructionEffectKind.MicroMissileImpact =>
                    Level100EffectCue.MissileImpact,
                Level100DestructionEffectKind.TargetDestroyed =>
                    Level100EffectCue.TargetOrTrainerDestroyed,
                Level100DestructionEffectKind.DroneDestroyed =>
                    Level100EffectCue.DroneDestroyed,
                Level100DestructionEffectKind.FacilityDestroyed =>
                    Level100EffectCue.FacilityDestroyed,
                _ => throw new InvalidDataException(
                    $"Core exposed unknown Level 100 destruction effect " +
                    $"{item.EffectKind}."),
            };
            if (cue.HasValue)
            {
                PlayAt(cue.Value, ToGodotWorld(item.Position));
            }
        }
    }

    private void PlayAt(Level100EffectCue cue, Vector3 worldPosition)
    {
        if (cue is not (
            Level100EffectCue.DroneVulcanFire or
            Level100EffectCue.PulseImpact or
            Level100EffectCue.MissileImpact or
            Level100EffectCue.TargetOrTrainerDestroyed or
            Level100EffectCue.DroneDestroyed or
            Level100EffectCue.FacilityDestroyed or
            Level100EffectCue.AquilaDestroyed or
            Level100EffectCue.TransportDestroyed or
            Level100EffectCue.ComponentDebrisDestroyed or
            Level100EffectCue.LargeDebrisDestroyed or
            Level100EffectCue.HugeGroundDebrisDestroyed))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cue),
                cue,
                "This Level 100 event requires its released owner.");
        }

        PlaySpatial(
            $"Retail{cue}",
            Level100AudioCatalog.GetEffect(cue),
            worldPosition,
            this);
    }

    public void PlayRepairCharging(Node3D repairPad) =>
        PlayAttached(
            repairPad,
            "RetailRepairPadCharging",
            Level100AudioCatalog.GetEffect(Level100EffectCue.RepairCharging));

    public void PlayRepairFull(Node3D repairPad) =>
        PlayAttached(
            repairPad,
            "RetailRepairPadFull",
            Level100AudioCatalog.GetEffect(Level100EffectCue.RepairFull));

    public void SetRepairPadIdle(Node3D repairPad, bool active) =>
        SetSpecificLoop(
            ref _repairPadIdleLoop,
            repairPad,
            "RetailRepairPadIdleLoop",
            Level100AudioCatalog.GetActorLoop(Level100ActorLoopCue.RepairPadIdle),
            active);

    public void SetTrainerFlying(Node3D trainer, bool active) =>
        SetSpecificLoop(
            ref _trainerLoop,
            trainer,
            "RetailAirTrainerFlybyLoop",
            Level100AudioCatalog.GetActorLoop(Level100ActorLoopCue.AirTrainer),
            active);

    // The shipped Target Drone has no engine effect, and neither its nor the
    // Air Trainer's missile launcher has a launch effect. Those states stay
    // silent instead of borrowing another Level 100 cue.

    public void SetTransportFlying(Node3D transport, bool active) =>
        SetSpecificLoop(
            ref _transportLoop,
            transport,
            "RetailTransportFlybyLoop",
            Level100AudioCatalog.GetActorLoop(Level100ActorLoopCue.Transport),
            active);

    public void PlayTerminalCue(Level100TerminalCue cue)
    {
        Level100AudioCueRecipe spec = Level100AudioCatalog.GetTerminal(cue);
        AudioStreamPlayer player = new()
        {
            Name = $"RetailTerminal{cue}",
            ProcessMode = ProcessModeEnum.Always,
            Stream = GetTerminalStream(cue, spec),
            // Retail's HUD cues are TRACKED on the player's own Battle Engine
            // (CBattleEngine::PlayHudSample, references/Onslaught/
            // BattleEngine.cpp:3180-3183, taking PlayEffect's ST_FOLLOWDONTDIE
            // default at SoundManager.h:189), and the released first-person
            // camera sits at the Battle Engine's position, so they resolve to
            // GetVolumeForPos's ceiling of 100 and not to the untracked 127.
            VolumeDb = MixedSoundVolumeDb(
                spec.LinearVolume,
                gameplay: true,
                Level100AudioCatalog.RetailListenerSourceVolume),
            PitchScale = PitchFor(spec),
        };
        player.Finished += () => ReleaseTerminalOneShot(player);
        _terminalOneShots.Add(player);
        _terminalBaseVolumes.Add(player, spec.LinearVolume);
        AddChild(player);
        player.Play();
        PlaybackRetirement.Observe(player);
        player.StreamPaused = _gameplayPaused;
    }

    // The frontend owner emits the exact Move/Select/Back identity. Cues made
    // after PauseAllSamples remain live, matching the released pause menu.
    public void PlayFrontendCue(string cueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cueName);
        Level100AudioCueRecipe spec = Level100AudioCatalog.GetFrontend(cueName);
        AudioStreamPlayer player = new()
        {
            Name = $"RetailFrontend{cueName}",
            ProcessMode = ProcessModeEnum.Always,
            Stream = GetFrontendStream(cueName, spec),
            // CFrontEnd::PlaySound passes PlayEffect(effect, NULL)
            // (references/Onslaught/FrontEnd.cpp:1609), and a null owner forces
            // ST_NOTRACKING (references/Onslaught/SoundManager.cpp:479-480), so
            // a frontend cue is one of the 127-source-volume events.
            VolumeDb = MixedSoundVolumeDb(
                spec.LinearVolume,
                gameplay: false,
                Level100AudioCatalog.RetailUntrackedSourceVolume),
            PitchScale = PitchFor(spec),
        };
        player.Finished += () => ReleaseFrontendOneShot(player);
        _frontendOneShots.Add(player);
        _frontendBaseVolumes.Add(player, spec.LinearVolume);
        AddChild(player);
        player.Play();
        PlaybackRetirement.Observe(player);
    }

    // Ordered Level100MessageRequested events enter by canonical speaker and
    // numeric message ID. Script waits and duration gates remain Core facts.
    public void QueueCharacterMessage(int speakerId, int messageId)
    {
        _queuedCharacterMessages.Enqueue(speakerId, messageId);
        if (!_tutorialVoice.Playing &&
            _characterMessageHandoffSecondsRemaining <= 0d &&
            _characterMessageVoiceLeadSecondsRemaining <= 0d)
        {
            _characterMessageVoiceLeadSecondsRemaining =
                RetailCharacterMessageVoiceLeadSeconds;
        }
    }

    public void StopCharacterMessages()
    {
        _queuedCharacterMessages.Clear();
        _activeCharacterSpeakerId = null;
        _activeCharacterMessageId = null;
        _activeCharacterMessageLengthSeconds = 0d;
        _characterMessageVoiceLeadSecondsRemaining = 0d;
        _characterMessageHandoffSecondsRemaining = 0d;
        _tutorialVoice.Stop();
        _tutorialVoice.Stream = null;
    }

    public void SetMasterSoundOption(float optionValue)
    {
        _soundMasterVolume = Level100AudioCatalog.ToRetailSoundMasterVolume(optionValue);
        ApplyMixVolumes();
    }

    public void SetMusicOption(float optionValue)
    {
        _musicPolicy.SetConfiguredVolume(optionValue);
    }

    public void SetGameplayMix(float linearMix)
    {
        ValidateLinearMix(linearMix, nameof(linearMix));
        // The result owner supplies the released current fade or duck value.
        // Audio applies it but does not schedule or advance that state.
        _gameplayMix = linearMix;
        ApplyMixVolumes();
    }

    public void SetGameplayPaused(bool paused)
    {
        if (_gameplayPaused == paused)
        {
            return;
        }

        _gameplayPaused = paused;
        _tutorialVoice.StreamPaused = paused && _tutorialVoice.Playing;
        SetPaused(_terminalOneShots, paused);
        SetPaused(_frontendOneShots, paused);
        SetPaused(_gameplayOneShots, paused);
        SetLoopPaused(_aquilaFlightLoop, paused);
        SetLoopPaused(_aquilaWarningLoop, paused);
        SetLoopPaused(_trainerLoop, paused);
        SetLoopPaused(_transportLoop, paused);
        SetLoopPaused(_repairPadIdleLoop, paused);
        // MUSIC is not a CSoundManager sample and keeps playing in retail.
    }

    public void StopGameplaySamples()
    {
        StopCharacterMessages();
        StopAndFree(_terminalOneShots);
        _terminalBaseVolumes.Clear();
        StopAndFree(_gameplayOneShots);
        _gameplayBaseVolumes.Clear();
        StopAquilaFlightLoop();
        StopAquilaWarningLoop();
        StopLoop(ref _trainerLoop);
        StopLoop(ref _transportLoop);
        StopLoop(ref _repairPadIdleLoop);
        _hostileEnvironmentMissionStartTick = null;
        _lastHostileEnvironmentContactTick = 0;
        _aquilaWarningState = AquilaWarningAudioState.Normal;
        _gameplayMix = 1f;
        _gameplayPaused = false;
    }

    // Released KillAllSamples stops every currently owned sample. A pause-menu
    // frontend cue created after this call is consequently a new live sample.
    public void StopAllSamples()
    {
        StopGameplaySamples();
        StopAndFree(_frontendOneShots);
        _frontendBaseVolumes.Clear();
    }

    public void StopLevel100Audio()
    {
        StopAllSamples();
        StopTutorialMusic();
        ReleaseAquilaBinding();
    }

    public void StopForLevelExit(bool playFrontendSelect)
    {
        StopLevel100Audio();
        if (playFrontendSelect)
        {
            PlayFrontendCue("Select");
        }
    }

    public override void _ExitTree() => StopLevel100Audio();

    private void PlayAttached(
        Node3D owner,
        string name,
        Level100AudioCueRecipe spec) =>
        PlaySpatial(name, spec, Vector3.Zero, owner);

    private void PlaySpatial(
        string name,
        Level100AudioCueRecipe spec,
        Vector3 position,
        Node parent)
    {
        if (spec.Looping)
        {
            throw new InvalidOperationException($"Looping cue '{name}' requires a specific owner.");
        }

        var player = new AudioStreamPlayer3D
        {
            Name = name,
            ProcessMode = ProcessModeEnum.Always,
            Stream = GetEventStream(spec),
            Position = position,
            // Godot's own distance model is DISABLED because retail's is not one
            // of them. The released PC build never enables DSBCAPS_CTRL3D - the
            // flag word and the whole DS3D parameter block are commented out at
            // references/Onslaught/pcsoundmanager.cpp:209 and :366-395 - so
            // there is no hardware rolloff at all. CSoundManager::GetVolumeForPos
            // computes the attenuation in code and CPCSoundManager::UpdateSound
            // writes it straight to SetVolume (:405-412).
            //
            // The invented `MaxDistance 80 / UnitSize 8` this replaces was 37 dB
            // too loud at 45 units and kept sounds audible out to 80 units that
            // retail silences at 50. Neither number appears anywhere in the
            // source. MaxDistance 0 disables Godot's culling as well, because
            // retail's cut-off is the early-out below, not a radius.
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Disabled,
            MaxDistance = 0f,
            PitchScale = PitchFor(spec),
        };
        parent.AddChild(player);

        float distanceUnits = ListenerDistance(player);
        if (Level100AudioCatalog.RetailRefusesNonLoopingStart(distanceUnits))
        {
            // CSoundManager::StartSoundEvent's "SRG early out",
            // references/Onslaught/SoundManager.cpp:519-524: a non-looping event
            // at or beyond FAR_SOUND is deleted and never plays. Every cue that
            // reaches PlaySpatial is non-looping - the guard above rejects
            // looping recipes - so the branch applies unconditionally here.
            player.QueueFree();
            return;
        }

        player.VolumeDb = SpatialVolumeDb(spec.LinearVolume, distanceUnits);
        player.Finished += () => ReleaseGameplayOneShot(player);
        _gameplayOneShots.Add(player);
        _gameplayBaseVolumes.Add(player, spec.LinearVolume);
        player.Play();
        PlaybackRetirement.Observe(player);
        player.StreamPaused = _gameplayPaused;
    }

    private void SetSpecificLoop(
        ref AudioStreamPlayer3D? player,
        Node3D owner,
        string name,
        Level100AudioCueRecipe spec,
        bool active,
        float initialSubVolume = Level100AudioCatalog.RetailUnfadedSubVolume)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!active)
        {
            StopLoop(ref player);
            return;
        }

        if (IsPlaying(player) && player!.GetParent() == owner)
        {
            return;
        }

        StopLoop(ref player);
        if (!spec.Looping)
        {
            throw new InvalidOperationException($"Non-looping cue '{name}' cannot own actor state.");
        }

        player = new AudioStreamPlayer3D
        {
            Name = name,
            ProcessMode = ProcessModeEnum.Always,
            Stream = GetLoopStream(spec),
            Position = Vector3.Zero,
            // Same released law as PlaySpatial, and see its comment for why
            // Godot's model is disabled. A LOOPING event is exempt from the
            // FAR_SOUND early-out (references/Onslaught/SoundManager.cpp:520
            // tests `!event->mLooping`), so a distant loop starts and simply
            // plays at zero source volume until the listener closes.
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Disabled,
            MaxDistance = 0f,
            PitchScale = PitchFor(spec),
        };
        _gameplayBaseVolumes.Add(player, spec.LinearVolume);
        owner.AddChild(player);
        player.VolumeDb =
            SpatialVolumeDb(
                spec.LinearVolume,
                ListenerDistance(player),
                initialSubVolume);
        player.Play();
        PlaybackRetirement.Observe(player);
        player.StreamPaused = _gameplayPaused;
    }

    private void FadeInAquilaFlightLoop(Node3D owner)
    {
        bool continuing =
            IsPlaying(_aquilaFlightLoop) &&
            _aquilaFlightLoop!.GetParent() == owner;
        Level100AudioCueRecipe spec =
            Level100AudioCatalog.GetAquilaTransition(AquilaTransitionCue.InFlight);
        SetSpecificLoop(
            ref _aquilaFlightLoop,
            owner,
            "RetailAquilaInFlightLoop",
            spec,
            active: true,
            initialSubVolume: 0f);

        if (!continuing)
        {
            _aquilaFlightLoopSubVolume = 0f;
            _aquilaFlightLoopFadeAccumulatorSeconds = 0d;
        }
        _aquilaFlightLoopTargetSubVolume =
            Level100AudioCatalog.RetailUnfadedSubVolume;
        _aquilaFlightLoopFadeStep =
            Level100AudioCatalog.RetailFlightLoopFadeStep;
        ApplyAquilaFlightLoopVolume(spec.LinearVolume);
    }

    private void FadeOutAquilaFlightLoop()
    {
        if (IsPlaying(_aquilaFlightLoop))
        {
            _aquilaFlightLoopTargetSubVolume = 0f;
            _aquilaFlightLoopFadeStep =
                -Level100AudioCatalog.RetailFlightLoopFadeStep;
        }
    }

    private void AdvanceAquilaFlightLoopFade(double delta)
    {
        if (!double.IsFinite(delta) ||
            delta <= 0d ||
            !IsPlaying(_aquilaFlightLoop) ||
            _aquilaFlightLoopFadeStep == 0f)
        {
            return;
        }

        _aquilaFlightLoopFadeAccumulatorSeconds += delta;
        while (_aquilaFlightLoopFadeAccumulatorSeconds >= RetailSoundUpdateSeconds &&
               _aquilaFlightLoopFadeStep != 0f)
        {
            _aquilaFlightLoopFadeAccumulatorSeconds -= RetailSoundUpdateSeconds;
            _aquilaFlightLoopSubVolume =
                Level100AudioCatalog.AdvanceRetailFlightLoopSubVolume(
                    _aquilaFlightLoopSubVolume,
                    _aquilaFlightLoopTargetSubVolume,
                    _aquilaFlightLoopFadeStep,
                    out bool crossedTarget);
            if (crossedTarget)
            {
                _aquilaFlightLoopFadeStep = 0f;
                _aquilaFlightLoopFadeAccumulatorSeconds = 0d;
                if (_aquilaFlightLoopTargetSubVolume == 0f)
                {
                    StopAquilaFlightLoop();
                    return;
                }
            }
        }

        if (_gameplayBaseVolumes.TryGetValue(
            _aquilaFlightLoop!,
            out float baseVolume))
        {
            ApplyAquilaFlightLoopVolume(baseVolume);
        }
    }

    private void ApplyAquilaFlightLoopVolume(float baseVolume)
    {
        if (GodotObject.IsInstanceValid(_aquilaFlightLoop) &&
            _aquilaFlightLoop!.IsInsideTree())
        {
            _aquilaFlightLoop.VolumeDb = SpatialVolumeDb(
                baseVolume,
                ListenerDistance(_aquilaFlightLoop),
                _aquilaFlightLoopSubVolume);
        }
    }

    private void AdvanceAquilaWarningLoopFade(double delta)
    {
        if (!double.IsFinite(delta) ||
            delta <= 0d ||
            !IsPlaying(_aquilaWarningLoop) ||
            _aquilaWarningLoopFadeStep == 0f)
        {
            return;
        }

        _aquilaWarningLoopFadeAccumulatorSeconds += delta;
        while (_aquilaWarningLoopFadeAccumulatorSeconds >= RetailSoundUpdateSeconds &&
               _aquilaWarningLoopFadeStep != 0f)
        {
            _aquilaWarningLoopFadeAccumulatorSeconds -= RetailSoundUpdateSeconds;
            _aquilaWarningLoopSubVolume =
                Level100AudioCatalog.AdvanceRetailFlightLoopSubVolume(
                    _aquilaWarningLoopSubVolume,
                    _aquilaWarningLoopTargetSubVolume,
                    _aquilaWarningLoopFadeStep,
                    out bool crossedTarget);
            if (crossedTarget)
            {
                _aquilaWarningLoopFadeStep = 0f;
                _aquilaWarningLoopFadeAccumulatorSeconds = 0d;
                if (_aquilaWarningLoopTargetSubVolume == 0f)
                {
                    StopAquilaWarningLoop();
                    return;
                }
            }
        }

        if (_gameplayBaseVolumes.TryGetValue(
            _aquilaWarningLoop!,
            out float baseVolume))
        {
            ApplyAquilaWarningLoopVolume(baseVolume);
        }
    }

    private void ApplyAquilaWarningLoopVolume(float baseVolume)
    {
        if (GodotObject.IsInstanceValid(_aquilaWarningLoop) &&
            _aquilaWarningLoop!.IsInsideTree())
        {
            _aquilaWarningLoop.VolumeDb = SpatialVolumeDb(
                baseVolume,
                ListenerDistance(_aquilaWarningLoop),
                _aquilaWarningLoopSubVolume);
        }
    }

    private AudioStream GetEventStream(Level100AudioCueRecipe spec)
    {
        if (!_pcmStreams.TryGetValue(spec.ResourcePath, out AudioStream? stream))
        {
            stream = LoadPcmWav(spec.ResourcePath, looping: false);
            _pcmStreams.Add(spec.ResourcePath, stream);
        }
        return stream;
    }

    private AudioStreamWav GetLoopStream(Level100AudioCueRecipe spec)
    {
        if (!_loopStreams.TryGetValue(spec.ResourcePath, out AudioStreamWav? stream))
        {
            stream = LoadPcmWav(spec.ResourcePath, looping: true);
            _loopStreams.Add(spec.ResourcePath, stream);
        }
        return stream;
    }

    private AudioStream GetTerminalStream(
        Level100TerminalCue cue,
        Level100AudioCueRecipe spec)
    {
        if (!_terminalStreams.TryGetValue(cue, out AudioStream? stream))
        {
            stream = LoadPcmWav(spec.ResourcePath, looping: false);
            _terminalStreams.Add(cue, stream);
        }
        return stream;
    }

    private AudioStream GetFrontendStream(
        string cueName,
        Level100AudioCueRecipe spec)
    {
        if (!_frontendStreams.TryGetValue(cueName, out AudioStream? stream))
        {
            stream = LoadPcmWav(spec.ResourcePath, looping: false);
            _frontendStreams.Add(cueName, stream);
        }
        return stream;
    }

    private void BeginCharacterMessageHandoff()
    {
        _activeCharacterMessageId = null;
        _activeCharacterSpeakerId = null;
        _activeCharacterMessageLengthSeconds = 0d;
        _tutorialVoice.Stream = null;
        if (_queuedCharacterMessages.Count > 0)
        {
            _characterMessageHandoffSecondsRemaining =
                RetailCharacterMessageHandoffSeconds;
        }
    }

    private void StartNextCharacterMessage()
    {
        _characterMessageVoiceLeadSecondsRemaining = 0d;
        if (!_queuedCharacterMessages.TryDequeue(
            out Level100QueuedCharacterMessage queuedMessage))
        {
            _activeCharacterSpeakerId = null;
            _activeCharacterMessageId = null;
            _activeCharacterMessageLengthSeconds = 0d;
            _tutorialVoice.Stream = null;
            return;
        }

        Level100MessageAudioSpec message = queuedMessage.Audio;
        if (!_voiceStreams.TryGetValue(message.MessageId, out AudioStreamOggVorbis? stream))
        {
            stream = LoadOgg(message.ResourcePath, looping: false);
            _voiceStreams.Add(message.MessageId, stream);
        }

        _activeCharacterSpeakerId = queuedMessage.SpeakerId;
        _activeCharacterMessageId = message.MessageId;
        _activeCharacterMessageLengthSeconds = stream.GetLength();
        _tutorialVoice.Stream = stream;
        _tutorialVoice.VolumeDb = MixedSoundVolumeDb(
            Level100AudioCatalog.RetailRadioMessageVolume,
            gameplay: true,
            Level100AudioCatalog.RetailListenerSourceVolume);
        _tutorialVoice.Play();
        PlaybackRetirement.Observe(_tutorialVoice);
        _tutorialVoice.StreamPaused = _gameplayPaused;
    }

    private void ApplyMixVolumes()
    {
        if (GodotObject.IsInstanceValid(_tutorialVoice))
        {
            _tutorialVoice.VolumeDb = MixedSoundVolumeDb(
                Level100AudioCatalog.RetailRadioMessageVolume,
                gameplay: true,
                Level100AudioCatalog.RetailListenerSourceVolume);
        }
        // Finished/stop owners remove entries, so option and fade updates can
        // iterate directly without a temporary key-copy allocation. The spatial
        // players' source volume is their live distance, which is exactly what
        // the per-update attenuation pass computes, so they share it.
        UpdateSpatialAttenuation();
        ApplyMixVolumes(
            _terminalBaseVolumes,
            gameplay: true,
            Level100AudioCatalog.RetailListenerSourceVolume);
        ApplyMixVolumes(
            _frontendBaseVolumes,
            gameplay: false,
            Level100AudioCatalog.RetailUntrackedSourceVolume);
    }

    private void ApplyMixVolumes(
        Dictionary<AudioStreamPlayer, float> baseVolumes,
        bool gameplay,
        int sourceVolume)
    {
        foreach ((AudioStreamPlayer player, float baseVolume) in baseVolumes)
        {
            if (GodotObject.IsInstanceValid(player))
            {
                player.VolumeDb =
                    MixedSoundVolumeDb(baseVolume, gameplay, sourceVolume);
            }
        }
    }

    // The released volume -> attenuation law, not a linear->dB conversion.
    //
    // This used to be a plain linear-to-decibel conversion of the product of the
    // recipe volume and the option mix, floored at -80. That was wrong in three
    // separate ways against CSoundManager::Fade
    // (references/Onslaught/SoundManager.cpp:760-793) plus the PC shaping stage
    // (references/Onslaught/pcsoundmanager.cpp:405-412): it had no saturation
    // plateau, it was logarithmic where retail is linear in dB below the knee,
    // and it compressed Level 100's dynamic range to roughly a third of
    // retail's. Every level was wrong and the RELATIVE balance was wrong: under
    // the released direct 0.8 sound master, the Pulse Cannon report computes
    // 14 dB below the jet engine loop rather than the old adapter's 3.7 dB.
    //
    // `sourceVolume` is retail's `v`, and it is NOT one value. See
    // Level100AudioCatalog.RetailUntrackedSourceVolume (127, an event with a
    // null owner) and RetailListenerSourceVolume (100, a tracked event on the
    // listener). Spatial emitters take RetailSourceVolumeForDistance instead.
    //
    // `_soundMasterVolume` is the supplied option float itself: promoted retail
    // SetMasterVolume at 0x004E04C0 stores it directly rather than applying the
    // retained-source tangent curve.
    private float MixedSoundVolumeDb(float baseVolume, bool gameplay, int sourceVolume) =>
        Level100AudioCatalog.RetailVolumeDb(
            sourceVolume,
            baseVolume,
            Level100AudioCatalog.RetailUnfadedSubVolume,
            _soundMasterVolume,
            gameplay ? _gameplayMix : 1f);

    // A spatial emitter's source volume is retail's GetVolumeForPos rather than
    // a constant: linear from 100 at the listener to 0 at FAR_SOUND = 50 units
    // (references/Onslaught/SoundManager.h:21,
    // references/Onslaught/SoundManager.cpp:437-442). Retail recomputes it on
    // every update for every ST_FOLLOW* event (SoundManager.cpp:1360-1370), so
    // _Process does the same here.
    private float SpatialVolumeDb(
        float baseVolume,
        float distanceUnits,
        float subVolume = Level100AudioCatalog.RetailUnfadedSubVolume) =>
        Level100AudioCatalog.RetailVolumeDb(
            Level100AudioCatalog.RetailSourceVolumeForDistance(distanceUnits),
            baseVolume,
            subVolume,
            _soundMasterVolume,
            _gameplayMix);

    // Retail measures from GAME.GetCamera(0)->GetPos()
    // (references/Onslaught/SoundManager.cpp:948-949). Godot routes
    // AudioStreamPlayer3D through the current Camera3D unless an
    // AudioListener3D is made current, and this client makes none, so the two
    // listeners are the same node. With no camera at all retail leaves campos
    // at ZERO_FVECTOR and the event keeps its absolute position, which puts
    // every Level 100 emitter past FAR_SOUND; that fallback is reproduced here
    // rather than special-cased.
    private Vector3 ListenerPosition() =>
        GetViewport()?.GetCamera3D() is { } camera ? camera.GlobalPosition : Vector3.Zero;

    private float ListenerDistance(Node3D emitter) =>
        emitter.GlobalPosition.DistanceTo(ListenerPosition());

    private void UpdateSpatialAttenuation()
    {
        foreach ((AudioStreamPlayer3D player, float baseVolume) in _gameplayBaseVolumes)
        {
            if (GodotObject.IsInstanceValid(player) && player.IsInsideTree())
            {
                float subVolume = ReferenceEquals(player, _aquilaFlightLoop)
                    ? _aquilaFlightLoopSubVolume
                    : ReferenceEquals(player, _aquilaWarningLoop)
                        ? _aquilaWarningLoopSubVolume
                        : Level100AudioCatalog.RetailUnfadedSubVolume;
                player.VolumeDb = SpatialVolumeDb(
                    baseVolume,
                    ListenerDistance(player),
                    subVolume);
            }
        }
    }

    // Normalizing CMusic's current 0..127 integer for Godot is an adapter
    // boundary, not a claim about DirectSound device-volume or audible parity.
    private static float MusicVolumeDb(int volume) =>
        volume <= 0
            ? -80f
            : Mathf.LinearToDb(volume / (float)RetailMusicPolicy.FullVolume);

    private static void ValidateLinearMix(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Audio mix values must be finite and between zero and one.");
        }
    }

    // CSoundManager::PlayEffect's producer
    // (references/Onslaught/SoundManager.cpp:1188-1196) passed through the
    // released PC device clamp (references/Onslaught/pcsoundmanager.cpp:398-401,
    // "## SRG  clamp to 1.0 to stop stalls  (why won't it work??)").
    //
    // The producer only ever emits values at or above 1.0, and the clamp runs
    // before the single SetFrequency call on every update, so the result is
    // ALWAYS 1.0 on the released PC build: every sample plays at a constant
    // 44000 Hz. Until 2026-07-27 this returned the producer's raw value, which
    // randomly detuned every explosion, impact and weapon report by up to +29%
    // - a wobble the released game does not have.
    //
    // The producer is kept rather than deleted so the clamp is visibly the
    // reason, and so nobody "restores" the modulation from the source's
    // SoundManager.cpp half without reaching the PC device layer.
    private static float PitchFor(Level100AudioCueRecipe spec) =>
        Level100AudioCatalog.RetailPcPitchMultiplier(
            spec.PitchVariancePercent == 0
                ? 1f
                : 1f + ((GD.Randi() % (uint)spec.PitchVariancePercent) / 100f));

    private void ReleaseGameplayOneShot(AudioStreamPlayer3D player)
    {
        _gameplayOneShots.Remove(player);
        _gameplayBaseVolumes.Remove(player);
        player.QueueFree();
    }

    private void ReleaseTerminalOneShot(AudioStreamPlayer player)
    {
        _terminalOneShots.Remove(player);
        _terminalBaseVolumes.Remove(player);
        player.QueueFree();
    }

    private void ReleaseFrontendOneShot(AudioStreamPlayer player)
    {
        _frontendOneShots.Remove(player);
        _frontendBaseVolumes.Remove(player);
        player.QueueFree();
    }

    private static void SetPaused(List<AudioStreamPlayer3D> players, bool paused)
    {
        for (int index = players.Count - 1; index >= 0; index--)
        {
            if (GodotObject.IsInstanceValid(players[index]))
            {
                players[index].StreamPaused = paused;
            }
        }
    }

    private static void SetPaused(List<AudioStreamPlayer> players, bool paused)
    {
        for (int index = players.Count - 1; index >= 0; index--)
        {
            if (GodotObject.IsInstanceValid(players[index]))
            {
                players[index].StreamPaused = paused;
            }
        }
    }

    private static void SetLoopPaused(AudioStreamPlayer3D? player, bool paused)
    {
        if (IsPlaying(player))
        {
            player!.StreamPaused = paused;
        }
    }

    private static void StopAndFree(List<AudioStreamPlayer3D> players)
    {
        foreach (AudioStreamPlayer3D player in players)
        {
            if (GodotObject.IsInstanceValid(player))
            {
                player.Stop();
                player.QueueFree();
            }
        }
        players.Clear();
    }

    private static void StopAndFree(List<AudioStreamPlayer> players)
    {
        foreach (AudioStreamPlayer player in players)
        {
            if (GodotObject.IsInstanceValid(player))
            {
                player.Stop();
                player.QueueFree();
            }
        }
        players.Clear();
    }

    private void StopLoop(ref AudioStreamPlayer3D? player)
    {
        if (player is not null)
        {
            _gameplayBaseVolumes.Remove(player);
        }
        if (GodotObject.IsInstanceValid(player))
        {
            player!.Stop();
            player.QueueFree();
        }
        player = null;
    }

    private void StopAquilaWarningLoop()
    {
        StopLoop(ref _aquilaWarningLoop);
        _aquilaWarningLoopSubVolume = 0f;
        _aquilaWarningLoopTargetSubVolume = 0f;
        _aquilaWarningLoopFadeStep = 0f;
        _aquilaWarningLoopFadeAccumulatorSeconds = 0d;
        _aquilaWarningLoopState = AquilaWarningAudioState.Normal;
    }

    private static bool IsPlaying(AudioStreamPlayer3D? player) =>
        GodotObject.IsInstanceValid(player) && player!.Playing;

    private static Level100ActorSnapshot RequireActor(
        Level100ActorRegistrySnapshot actors,
        Level100ActorId actorId)
    {
        foreach (Level100ActorSnapshot actor in actors.Actors)
        {
            if (actor.ActorId == actorId)
            {
                return actor;
            }
        }

        throw new InvalidDataException(
            $"Level 100 audio actor {actorId.Value} is absent from the native registry.");
    }

    private static Vector3 ToGodotWorld(SimVector3 position) => new(
        position.X * 0.001f,
        position.Y * 0.001f,
        -position.Z * 0.001f);

    private static Vector3 ToGodotWorld(Level100Vector3 position) => new(
        position.X * 0.001f,
        -position.Z * 0.001f,
        -position.Y * 0.001f);

    private void ReleaseAquilaBinding()
    {
        if (GodotObject.IsInstanceValid(_aquila))
        {
            _aquila!.QueueFree();
        }
        _aquila = null;
        _aquilaActorId = null;
    }

    private static AudioStreamOggVorbis LoadOgg(string resourcePath, bool looping)
    {
        byte[] source = Godot.FileAccess.GetFileAsBytes(resourcePath);
        AudioStreamOggVorbis? stream = source.Length == 0
            ? null
            : AudioStreamOggVorbis.LoadFromBuffer(source);
        if (stream is null)
        {
            throw new InvalidDataException(
                $"Released Ogg stream is missing or invalid: {resourcePath}");
        }
        stream.Loop = looping;
        return stream;
    }

    private static AudioStreamWav LoadPcmWav(string resourcePath, bool looping)
    {
        byte[] wave = Godot.FileAccess.GetFileAsBytes(resourcePath);
        if (wave.Length < 44 ||
            !wave.AsSpan(0, 4).SequenceEqual("RIFF"u8) ||
            !wave.AsSpan(8, 4).SequenceEqual("WAVE"u8) ||
            !wave.AsSpan(12, 4).SequenceEqual("fmt "u8) ||
            BinaryPrimitives.ReadUInt32LittleEndian(wave.AsSpan(16, 4)) != 16 ||
            BinaryPrimitives.ReadUInt16LittleEndian(wave.AsSpan(20, 2)) != 1 ||
            BinaryPrimitives.ReadUInt16LittleEndian(wave.AsSpan(22, 2)) != 1 ||
            BinaryPrimitives.ReadUInt32LittleEndian(wave.AsSpan(24, 4)) != 44_100 ||
            BinaryPrimitives.ReadUInt16LittleEndian(wave.AsSpan(34, 2)) != 16 ||
            !wave.AsSpan(36, 4).SequenceEqual("data"u8))
        {
            throw new InvalidDataException(
                $"Curated audio '{resourcePath}' is not 44.1 kHz mono 16-bit PCM WAV.");
        }

        uint dataLength = BinaryPrimitives.ReadUInt32LittleEndian(wave.AsSpan(40, 4));
        if (dataLength != wave.Length - 44)
        {
            throw new InvalidDataException(
                $"Curated audio '{resourcePath}' has invalid WAV framing.");
        }

        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = 44_100,
            Stereo = false,
            Data = wave.AsSpan(44).ToArray(),
            LoopMode = looping
                ? AudioStreamWav.LoopModeEnum.Forward
                : AudioStreamWav.LoopModeEnum.Disabled,
            LoopBegin = 0,
            LoopEnd = looping ? (wave.Length - 44) / sizeof(short) : 0,
        };
    }
}
