// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.GodotClient;

public enum Level100EffectCue
{
    AquilaStrafe = 0,
    AquilaHydraulics = 1,
    AquilaIncomingMissile = 2,
    AquilaTargetLocked = 3,
    AquilaTargetAcquired = 4,
    PulseCannonFire = 5,
    VulcanCannonFire = 6,
    MicroMissileFire = 7,
    DroneVulcanFire = 8,
    PulseImpact = 9,
    MissileImpact = 10,
    TargetOrTrainerDestroyed = 11,
    DroneDestroyed = 12,
    FacilityDestroyed = 13,
    AquilaDestroyed = 14,
    TransportDestroyed = 15,
    ComponentDebrisDestroyed = 16,
    LargeDebrisDestroyed = 17,
    HugeGroundDebrisDestroyed = 18,
    RepairCharging = 19,
    RepairFull = 20,
}

public enum Level100TerminalCue
{
    AmmunitionDepleted = 0,
    ArmourLow = 1,
    EnergyLow = 2,
    HostileEnvironment = 3,
    IncomingMissile = 4,
    IncomingWarhead = 5,
    MicroMissilesSelected = 6,
    PulseCannonSelected = 7,
    VulcanCannonSelected = 8,
    WeaponOverheating = 9,
}

public enum AquilaWarningAudioState
{
    Normal = 0,
    EnergyLow = 1,
    HullCritical = 2,
}

public enum AquilaTransitionCue
{
    Takeoff = 0,
    InFlight = 1,
    Landing = 2,
}

public enum Level100ActorLoopCue
{
    AirTrainer = 0,
    Transport = 1,
    RepairPadIdle = 2,
}

public readonly record struct Level100MessageAudioSpec(
    int MessageId,
    string Symbol,
    string AudioStem,
    string ResourcePath);

public readonly record struct Level100AudioCueRecipe(
    string ResourcePath,
    int RetailSoundRecord,
    string RetailEffectName,
    float LinearVolume,
    int PitchVariancePercent,
    bool Looping);

public readonly record struct Level100MusicRecipe(
    string ResourcePath,
    string RetailSelection,
    int RetailTrackIndex,
    string RetailSourceName);

// Mix provenance, verified 2026-07-26 against the shipped retail table and the
// GPL-released Onslaught engine sources (see
// local-lab/LEVEL100-AUDIO-PARITY-2026-07-26.md):
//
// * RetailSoundRecord is the record index in data/sounds/sounds.sfx (a plaintext
//   "# SFX <n>" list, version 103, 170 records). RetailEffectName is that
//   record's SAMPLE-PATH field. Retail's own runtime lookup key is the record's
//   DISPLAY-NAME field instead, via CSoundManager::GetEffectByName, so these two
//   fields are provenance only and are not the retail selector.
// * CSoundManager::PlayEffect computes `volume = (volume * effect->mVolume) / 100`
//   where mVolume is the record's integer volume field and the caller's `volume`
//   defaults to DEFAULT_SOUND_VOLUME. Every LinearVolume below is that product.
// * DEFAULT_SOUND_VOLUME 0.7f, mHUDMessageVolume 0.45f and (PC branch)
//   mRadioMessageVolume 0.42f are the released constants. Battle Engine call
//   sites pass ENGINE_VOLUME 1.0f, which is why the engine, strafe and energy
//   cues carry the record volume unscaled.
// * PitchVariancePercent is the record's pitch-variance field; retail applies
//   `pitch += (rand() % variance) / 100`.
public static class Level100AudioCatalog
{
    /// <summary>
    /// <c>CBattleEngine::HostileEnvironment</c> uses a strict
    /// <c>current - previous &gt; 5.0f</c> gate, then stores the current time on
    /// both the played and suppressed paths. At the released 20 Hz clock, a
    /// 100-tick gap is still suppressed and 101 ticks is the first eligible
    /// contact.
    /// </summary>
    public const int RetailHostileEnvironmentQuietTicks =
        5 * SimulationConstants.RetailTicksPerSecond;

    public static bool ObserveHostileEnvironmentContact(
        int currentTick,
        ref int previousContactTick)
    {
        if (currentTick < previousContactTick)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentTick),
                "Hostile-environment contacts must remain tick ordered.");
        }

        bool shouldPlay =
            (long)currentTick - previousContactTick >
            RetailHostileEnvironmentQuietTicks;
        previousContactTick = currentTick;
        return shouldPlay;
    }

    public const float RetailRadioMessageVolume = 0.42f;
    public const float RetailHudMessageVolume = 0.45f;
    public const float RetailDefaultEffectVolume = 0.70f;

    // The weapon-launch call site does NOT pass DEFAULT_SOUND_VOLUME. Read from
    // the pristine specimen (local-lab/safe-copy-bea-pristine/
    // BEA.exe.original.backup, sha256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab
    // 334ed7a753040eda1e1e7750 - NOT the installed executable, which is
    // patched): ProjectileBurst__SpawnFromCurrentPreset (0x005069f0) pushes the
    // literal 0x3f000000 = 0.5f into PlayEffect's `volume` slot in BOTH of its
    // branches, at 0x00506a6b and 0x00506a8a.
    //
    //   0x00506a6a  57                 PUSH EDI            ; track = ST_NOTRACKING
    //   [retail instruction listing removed]
    //   0x00506a70  57                 PUSH EDI            ; thing = NULL
    //   ...
    //   0x00506a88  6a 03              PUSH 3              ; track = ST_FOLLOWDONTDIE
    //   [retail instruction listing removed]
    //   0x00506a8f  50                 PUSH EAX            ; thing = the unit
    //
    // EDI is zeroed at the function head (0x00506a12 `33 ff`) and is
    // callee-saved across the intervening call, so the first branch really does
    // pass 0 and NULL. Which branch runs is selected by
    // `TEST byte [thing+0x34],8` at 0x00506a50: set for the player's own Battle
    // Engine, clear for an AI unit.
    public const float RetailWeaponLaunchVolume = 0.50f;

    private static readonly ReadOnlyCollection<Level100MessageAudioSpec>
        s_characterMessages = Array.AsReadOnly(new[]
        {
            Message(292_562, "HUD_01", "hud_01"),
            Message(293_386, "HUD_02", "hud_02"),
            Message(296_682, "HUD_06", "hud_06"),
            Message(-1_575_499_396, "TUTORIAL_MESSAGE_LOG", "tutorial_message_log"),
            Message(-257_967_449, "TUTORIAL_TECHNICIAN_01", "tutorial_technician_01"),
            Message(82_987_417, "TUTORIAL_13_MOD", "tutorial_13_mod"),
            Message(4_422_830, "TUTORIAL_01", "tutorial_01"),
            Message(175_347_826, "TUTORIAL_SCANNER", "tutorial_scanner"),
            Message(4_458_134, "TUTORIAL_02", "tutorial_02"),
            Message(4_493_438, "TUTORIAL_03", "tutorial_03"),
            Message(295_858, "HUD_05", "hud_05"),
            Message(1_339_691_000, "TUTORIAL_PULSE_CANNON", "tutorial_pulse_cannon"),
            Message(669_198_996, "TUTORIAL_OPEN_FIRE", "tutorial_open_fire"),
            Message(-1_715_818_922, "TUTORIAL_PULSE_CANNON_2", "tutorial_pulse_cannon_2"),
            Message(-1_616_775_312, "TUTORIAL_VULCAN_CANNON", "tutorial_vulcan_cannon"),
            Message(-1_860_407_443, "TUTORIAL_OPEN_FIRE_2", "tutorial_open_fire_2"),
            Message(864_965_454, "TUTORIAL_VULCAN_CANNON_2", "tutorial_vulcan_cannon_2"),
            Message(294_210, "HUD_03", "hud_03"),
            Message(295_034, "HUD_04", "hud_04"),
            Message(297_506, "HUD_07", "hud_07"),
            Message(298_330, "HUD_08", "hud_08"),
            Message(4_564_046, "TUTORIAL_05", "tutorial_05"),
            Message(22_775_962, "TUTORIAL_ZOOM", "tutorial_zoom"),
            Message(667_656_903, "TUTORIAL_DODGE_MOD", "tutorial_dodge_mod"),
            Message(150_647_733, "TUTORIAL_DODGE_2", "tutorial_dodge_2"),
            Message(151_778_876, "TUTORIAL_DODGE_3", "tutorial_dodge_3"),
            Message(1_326_027_769, "TUTORIAL_DODGE_GOOD", "tutorial_dodge_good"),
            Message(623_538_785, "TUTORIAL_DODGE_BAD", "tutorial_dodge_bad"),
            Message(4_528_742, "TUTORIAL_04", "tutorial_04"),
            Message(165_861_931, "TUTORIAL_LANDING", "tutorial_landing"),
            Message(4_599_350, "TUTORIAL_06", "tutorial_06"),
            Message(1_062_059_777, "TUTORIAL_THROTTLE_MOD", "tutorial_throttle_mod"),
            Message(4_475_837, "TUTORIAL_12", "tutorial_12"),
            Message(4_705_262, "TUTORIAL_09", "tutorial_09"),
            Message(4_634_654, "TUTORIAL_07", "tutorial_07"),
            Message(80_260_569, "TUTORIAL_STRAFE", "tutorial_strafe"),
            Message(4_669_958, "TUTORIAL_08", "tutorial_08"),
            Message(4_440_532, "TUTORIAL_11", "tutorial_11"),
            Message(162_342_028, "TUTORIAL_ABORTED", "tutorial_aborted"),
            Message(150_940_633, "TUTORIAL_BROKE_1", "tutorial_broke_1"),
            Message(152_071_864, "TUTORIAL_BROKE_2", "tutorial_broke_2"),
            Message(153_203_095, "TUTORIAL_BROKE_3", "tutorial_broke_3"),
            Message(-1_455_850_811, "TUTORIAL_HELP_PLAYER", "tutorial_help_player"),
            Message(4_405_227, "TUTORIAL_10", "tutorial_10"),
            Message(-185_551_049, "TUTORIAL_TECHNICIAN_02", "tutorial_technician_02"),
            Message(-113_134_649, "TUTORIAL_TECHNICIAN_03", "tutorial_technician_03"),
            Message(361_225_970, "TUTORIAL_MOVEMENT", "tutorial_movement"),
            Message(88_347_039, "TUTORIAL_WEAPON", "tutorial_weapon"),
            Message(346_044_574, "TUTORIAL_OVERHEAT", "tutorial_overheat"),
            Message(22_391_142, "TUTORIAL_AMMO", "tutorial_ammo"),
            Message(44_677_289, "TUTORIAL_WATER", "tutorial_water"),
        });

    public static IReadOnlyList<Level100MessageAudioSpec> CharacterMessages =>
        s_characterMessages;

    public static Level100MusicRecipe TutorialMusic { get; } = new(
        "res://Assets/Level100/Music/tutorial-track-03.ogg",
        "MUS_TUTORIAL",
        3,
        "data/Music/BEA_04(Master).ogg");

    // MUS_FRONTEND resolves to track index 8, verified 2026-07-27 in all three
    // steps against pristine BEA.exe (sha256 74154bfa..., file
    // local-lab/safe-copy-bea-pristine/BEA.exe.original.backup):
    //
    // 1. INDEX 8. CMusic::PlaySelection (0x004bb8c0) case 0 computes
    //    `(-(uint)(0x0083d448 != 0) & 0xfffffff9) + 8`, i.e. Music.cpp:466-472's
    //    `PLAYABLE_DEMO ? 1 : 8`. 0x0083d448 sits past .data's raw-backed extent
    //    (raw covers VA 0x00622000-0x00661000) so it loads zero, and its only
    //    writer in the image is CLIParams::ParseCommandLine (0x00423bc0) on the
    //    "-playabledemo" argument. A normal launch therefore selects 8.
    // 2. ZERO-BASED. The GetSong walk inlined at 0x004bb8c0 starts at mFirstSong
    //    (this+0xc) and advances `index` times through mNext (song+0x104), so
    //    index 0 is the head. Matches Music.cpp:476-490.
    // 3. ALPHABETICAL. PCPlatform::InitMusicPlaylist (0x00515320) calls
    //    LoadPlaylistFromDir("data\music") with the extension token at
    //    0x00630a04, which is the asciiz string "ogg". CMusic::AddToPlayList
    //    (Music.cpp:306-362) inserts before the first stricmp-greater filename.
    //    data/Music holds exactly BEA_01..BEA_10(Master).ogg and nothing else, and
    //    stricmp order equals numeric order there, so index 8 is BEA_09.
    //
    // The same rule at index 3 yields TutorialMusic's BEA_04, which is already
    // materialized and hash-verified, so it is corroborated at a second point.
    public static Level100MusicRecipe FrontendMusic { get; } = new(
        "res://Assets/Frontend/Music/frontend-track-08.ogg",
        "MUS_FRONTEND",
        8,
        "data/Music/BEA_09(Master).ogg");

    public static Level100MessageAudioSpec GetCharacterMessage(int messageId)
    {
        foreach (Level100MessageAudioSpec message in s_characterMessages)
        {
            if (message.MessageId == messageId)
            {
                return message;
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(messageId),
            messageId,
            "The mission requested a character message outside the accepted Level 100 set.");
    }

    public static Level100AudioCueRecipe GetFrontend(string cueName) => cueName switch
    {
        "Back" => Cue(
            "res://Assets/Frontend/SoundEffects/back.wav",
            43,
            "Front End\\N_FE_back",
            RetailDefaultEffectVolume * 0.52f),
        "Move" => Cue(
            "res://Assets/Frontend/SoundEffects/move.wav",
            44,
            "Front End\\N_FE_move",
            RetailDefaultEffectVolume * 0.49f),
        "Select" => Cue(
            "res://Assets/Frontend/SoundEffects/select.wav",
            45,
            "Front End\\N_FE_select",
            RetailDefaultEffectVolume * 0.52f),
        _ => throw new ArgumentOutOfRangeException(nameof(cueName), cueName, null),
    };

    public static Level100AudioCueRecipe GetEffect(Level100EffectCue cue) => cue switch
    {
        Level100EffectCue.AquilaStrafe => Cue(
            "res://Assets/Aquila/SoundEffects/strafe.wav",
            21,
            "Battle Engine\\N_BE_dash",
            0.80f,
            pitch: 10),
        Level100EffectCue.AquilaHydraulics => Cue(
            "res://Assets/Aquila/SoundEffects/hydraulics.wav",
            32,
            "Battle Engine\\N_BE_hydraulics_02",
            RetailHudMessageVolume * 0.40f),
        Level100EffectCue.AquilaIncomingMissile => Cue(
            "res://Assets/Aquila/SoundEffects/incoming-missile.wav",
            33,
            "Battle Engine\\N_BE_incoming_missile",
            RetailHudMessageVolume * 0.80f,
            pitch: 5),
        Level100EffectCue.AquilaTargetLocked => Cue(
            "res://Assets/Aquila/SoundEffects/target-locked.wav",
            30,
            "Battle Engine\\N_BE_homing_missile_lock",
            RetailHudMessageVolume * 0.80f),
        Level100EffectCue.AquilaTargetAcquired => Cue(
            "res://Assets/Aquila/SoundEffects/target-acquired.wav",
            31,
            "Battle Engine\\N_BE_homing_missile_target",
            RetailHudMessageVolume * 0.80f),
        // `Pulse Cannon Pod` charge level 0 -> weapon mode
        // `Mech Pulse Cannon Charged` (default physics.dat @0x134eb) ->
        // CWeaponLaunchSound `BE Pulse Cannon Fire` (payload @0x13576) ->
        // sounds.sfx record 37, volume 65, pitch variance 5.
        Level100EffectCue.PulseCannonFire => Cue(
            "res://Assets/Level100/SoundEffects/pulse-cannon-fire.wav",
            37,
            "Battle Engine\\N_BE_pulse_cannon_fire",
            RetailWeaponLaunchVolume * 0.65f,
            pitch: 5),
        // Both `Mech Twin Vulcan Cannon` (@0x13368, launch sound payload
        // @0x133fe) and the jet `Mech Vulcan Cannon` (@0x1327d, payload
        // @0x13303) name `BE Vulcan Cannon` = sounds.sfx record 42, volume 75,
        // pitch variance 7.
        Level100EffectCue.VulcanCannonFire => Cue(
            "res://Assets/Aquila/SoundEffects/vulcan-cannon-fire.wav",
            42,
            "Battle Engine\\N_BE_vulcan_cannon_fire",
            RetailWeaponLaunchVolume * 0.75f,
            pitch: 7),
        Level100EffectCue.MicroMissileFire => Cue(
            "res://Assets/Aquila/SoundEffects/micro-missile-fire.wav",
            34,
            "Battle Engine\\N_BE_micro_missiles_fire",
            RetailDefaultEffectVolume * 0.80f,
            pitch: 15),
        // sounds.sfx holds three "Blaster" records; 155 ("Blaster 2") is the one
        // whose volume 60 and pitch variance 10 this recipe carries. Record 156
        // ("Blaster 1") names the same sample at volume 80 / variance 0. Which of
        // the two the Level 100 Target Drone selects is NOT yet established.
        Level100EffectCue.DroneVulcanFire => Cue(
            "res://Assets/Level100/SoundEffects/drone-vulcan-fire.wav",
            155,
            "Weapons\\N_WP_blaster_02",
            RetailDefaultEffectVolume * 0.60f,
            pitch: 10),
        Level100EffectCue.PulseImpact or Level100EffectCue.DroneDestroyed => Cue(
            "res://Assets/Level100/SoundEffects/explosion-small.wav",
            108,
            "Impact\\N_I_explosion_small_debris",
            RetailDefaultEffectVolume * 0.70f,
            pitch: 20),
        Level100EffectCue.MissileImpact or
        Level100EffectCue.TargetOrTrainerDestroyed => Cue(
            "res://Assets/Level100/SoundEffects/target-tank-explosion-medium.wav",
            104,
            "Impact\\N_I_explosion_medium",
            RetailDefaultEffectVolume * 0.70f,
            pitch: 30),
        Level100EffectCue.FacilityDestroyed => Cue(
            "res://Assets/Level100/SoundEffects/facility-explosion-medium.wav",
            105,
            "Impact\\N_I_explosion_medium_ricochet",
            RetailDefaultEffectVolume * 0.70f,
            pitch: 30),
        Level100EffectCue.AquilaDestroyed => Cue(
            "res://Assets/Level100/SoundEffects/aquila-explosion-huge.wav",
            109,
            "Impact\\N_I_explosion_vbig",
            RetailDefaultEffectVolume * 0.70f,
            pitch: 30),
        Level100EffectCue.TransportDestroyed => Cue(
            "res://Assets/Level100/SoundEffects/transport-explosion-large.wav",
            96,
            "Impact\\N_I_explosion_big",
            RetailDefaultEffectVolume * 0.70f,
            pitch: 30),
        Level100EffectCue.ComponentDebrisDestroyed => Cue(
            "res://Assets/Level100/SoundEffects/component-explosion.wav",
            95,
            "Impact\\N_I_explosion2",
            RetailDefaultEffectVolume * 0.70f,
            pitch: 30),
        Level100EffectCue.LargeDebrisDestroyed => Cue(
            "res://Assets/Level100/SoundEffects/explosion-large-debris.wav",
            97,
            "Impact\\N_I_explosion_big_debris",
            RetailDefaultEffectVolume * 0.70f,
            pitch: 30),
        Level100EffectCue.HugeGroundDebrisDestroyed => Cue(
            "res://Assets/Level100/SoundEffects/explosion-huge-ground-debris.wav",
            110,
            "Impact\\N_I_explosion_vbig_debris",
            RetailDefaultEffectVolume * 0.70f,
            pitch: 30),
        Level100EffectCue.RepairCharging => Cue(
            "res://Assets/Level100/SoundEffects/repair-charging.wav",
            7,
            "Atmospheres\\N_A_health_pod_charging",
            RetailDefaultEffectVolume * 0.80f),
        Level100EffectCue.RepairFull => Cue(
            "res://Assets/Level100/SoundEffects/repair-full.wav",
            8,
            "Atmospheres\\N_A_health_pod_full",
            RetailDefaultEffectVolume * 0.80f),
        _ => throw new ArgumentOutOfRangeException(nameof(cue)),
    };

    public static Level100AudioCueRecipe GetTerminal(Level100TerminalCue cue) => cue switch
    {
        Level100TerminalCue.AmmunitionDepleted => Terminal(
            "ammunition-depleted", 46, "HUD\\N_HUD_Ammunition_Depleted"),
        Level100TerminalCue.ArmourLow => Terminal(
            "armour-low", 48, "HUD\\N_HUD_Armour_Low"),
        Level100TerminalCue.EnergyLow => Terminal(
            "energy-low", 53, "HUD\\N_HUD_Energy_Low"),
        Level100TerminalCue.HostileEnvironment => Terminal(
            "hostile-environment", 57, "HUD\\N_HUD_Hostile_Environment"),
        Level100TerminalCue.IncomingMissile => Terminal(
            "incoming-missile", 58, "HUD\\N_HUD_Incoming_Missile"),
        Level100TerminalCue.IncomingWarhead => Terminal(
            "incoming-warhead", 59, "HUD\\N_HUD_Incoming_Warhead"),
        Level100TerminalCue.MicroMissilesSelected => Terminal(
            "micro-missiles-selected", 60, "HUD\\N_HUD_Micro_Missiles"),
        Level100TerminalCue.PulseCannonSelected => Terminal(
            "pulse-cannon-selected", 62, "HUD\\N_HUD_Pulse_Cannon"),
        Level100TerminalCue.VulcanCannonSelected => Terminal(
            "vulcan-cannon-selected", 72, "HUD\\N_HUD_Vulcan_Cannon"),
        Level100TerminalCue.WeaponOverheating => Terminal(
            "weapon-overheating", 75, "HUD\\N_HUD_Weapon_Overheating"),
        _ => throw new ArgumentOutOfRangeException(nameof(cue)),
    };

    public static Level100AudioCueRecipe GetAquilaTransition(AquilaTransitionCue cue) =>
        cue switch
        {
            AquilaTransitionCue.Takeoff => Cue(
                "res://Assets/Aquila/SoundEffects/engine-takeoff.wav",
                26,
                "Battle Engine\\N_BE_engine_takeoff",
                0.40f),
            AquilaTransitionCue.InFlight => Cue(
                "res://Assets/Aquila/SoundEffects/engine-inflight.wav",
                24,
                "Battle Engine\\N_BE_engine_inflight",
                0.50f,
                looping: true),
            AquilaTransitionCue.Landing => Cue(
                "res://Assets/Aquila/SoundEffects/engine-land.wav",
                25,
                "Battle Engine\\N_BE_engine_land",
                0.40f),
            _ => throw new ArgumentOutOfRangeException(nameof(cue)),
        };

    public static Level100AudioCueRecipe GetAquilaWarning(AquilaWarningAudioState state) =>
        state switch
        {
            AquilaWarningAudioState.EnergyLow => Cue(
                "res://Assets/Aquila/SoundEffects/energy-low.wav",
                23,
                "Battle Engine\\N_BE_energy_low",
                0.70f,
                looping: true),
            AquilaWarningAudioState.HullCritical => Cue(
                "res://Assets/Aquila/SoundEffects/energy-critical.wav",
                22,
                "Battle Engine\\N_BE_energy_critical",
                0.70f,
                looping: true),
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };

    public static Level100AudioCueRecipe GetActorLoop(Level100ActorLoopCue cue) => cue switch
    {
        Level100ActorLoopCue.AirTrainer => Cue(
            "res://Assets/Level100/SoundEffects/trainer-flyby.wav",
            121,
            "Vehicles\\N_V_F_fighter_flyby",
            RetailDefaultEffectVolume * 0.45f,
            pitch: 15,
            looping: true),
        Level100ActorLoopCue.Transport => Cue(
            "res://Assets/Level100/SoundEffects/transport-flyby.wav",
            129,
            "Vehicles\\N_V_bomber_flyby",
            RetailDefaultEffectVolume * 0.40f,
            pitch: 15,
            looping: true),
        Level100ActorLoopCue.RepairPadIdle => Cue(
            "res://Assets/Level100/SoundEffects/repair-idle.wav",
            9,
            "Atmospheres\\N_A_health_pod_on",
            RetailDefaultEffectVolume * 0.50f,
            looping: true),
        _ => throw new ArgumentOutOfRangeException(nameof(cue)),
    };

    /// <summary>
    /// Released <c>CSoundManager::SetMasterVolume</c> at <c>0x004E04C0</c>:
    /// store the supplied option float directly. The retained PC tangent curve
    /// is a source/release divergence and is not applied here.
    /// </summary>
    public static float ToRetailSoundMasterVolume(float optionValue)
    {
        ValidateAudioOption(optionValue);
        return optionValue;
    }

    /// <summary>
    /// Released <c>CMusic::SetVolume</c> at <c>0x004BBA10</c>: round the supplied
    /// option float multiplied by 127 into the integer set-volume field. The
    /// original float remains the career value outside this presentation owner.
    /// </summary>
    public static int ToRetailMusicSetVolume(float optionValue)
    {
        ValidateAudioOption(optionValue);
        return checked((int)MathF.Round(
            optionValue * 127f,
            MidpointRounding.ToEven));
    }

    private static void ValidateAudioOption(float optionValue)
    {
        if (!float.IsFinite(optionValue) || optionValue is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(optionValue),
                optionValue,
                "Audio option values must be finite and between zero and one.");
        }
    }

    // ------------------------------------------------------------------
    // The released volume -> attenuation law.
    //
    // This is NOT a linear->dB conversion. Retail computes a DirectSound
    // attenuation in MILLIBELS (hundredths of a dB) with a piecewise-linear
    // map, a saturation plateau at the top, and a second PC-only shaping stage.
    // Until 2026-07-27 this adapter called Mathf.LinearToDb instead, which has
    // neither the plateau nor the linear-in-dB knee, and which compressed the
    // whole Level 100 mix into roughly a third of retail's dynamic range.
    //
    // The chain, in order, from the pinned source:
    //
    //   references/Onslaught/SoundManager.cpp:437-442  GetVolumeForPos
    //   references/Onslaught/SoundManager.cpp:519-524  the non-looping early-out
    //   references/Onslaught/SoundManager.cpp:526-531  the two source volumes
    //   references/Onslaught/SoundManager.cpp:760-793  CSoundManager::Fade
    //   references/Onslaught/pcsoundmanager.cpp:405-412 the PC shaping + SetVolume
    // ------------------------------------------------------------------

    /// <summary>
    /// <c>FAR_SOUND</c>, <c>references/Onslaught/SoundManager.h:21</c>. The whole
    /// spatial law: linear from full at the listener to nothing at 50 units.
    /// <c>NEAR_SOUND</c> (<c>:22</c>) and <c>CEffect::mFalloff</c>
    /// (parsed at <c>SoundManager.cpp:1541</c>) are never read anywhere in the
    /// drop, so there is no per-record curve to honour.
    /// </summary>
    public const float RetailFarSoundUnits = 50f;

    /// <summary>
    /// <c>SINT vol = 127;</c>, <c>references/Onslaught/SoundManager.cpp:526</c>.
    /// This is the source volume of an <c>ST_NOTRACKING</c> event — one started
    /// with a null owner (<c>:479-480</c>). <c>CFrontEnd::PlaySound</c>
    /// (<c>references/Onslaught/FrontEnd.cpp:1609</c>) passes
    /// <c>PlayEffect(effect, NULL)</c>, so every frontend cue is one of these.
    /// </summary>
    public const int RetailUntrackedSourceVolume = 127;

    /// <summary>
    /// The ceiling of <c>GetVolumeForPos</c>
    /// (<c>references/Onslaught/SoundManager.cpp:442</c>), and therefore the
    /// source volume of a TRACKED event sitting on the listener. Retail's HUD
    /// cues are tracked on the player's own Battle Engine
    /// (<c>BattleEngine.cpp:3180-3183</c> <c>PlayHudSample</c>), and the camera is at
    /// the Battle Engine's position in the released first-person view, so they
    /// resolve here rather than to 127. Message-box samples reach the same value
    /// through <c>ignore_owner_pos</c>, which pins the event to the camera
    /// (<c>SoundManager.cpp:457</c>, <c>:1004-1011</c>).
    /// </summary>
    public const int RetailListenerSourceVolume = 100;

    /// <summary>
    /// <c>event-&gt;mSubVolume = 1;</c> for an event started with no fade,
    /// <c>references/Onslaught/SoundManager.cpp:486-487</c>. Most adapter-owned
    /// events remain unfaded; the Aquila flight loop uses the bounded signed
    /// step below.
    /// </summary>
    public const float RetailUnfadedSubVolume = 1f;

    /// <summary>
    /// The Level 100 Aquila flight loop's signed <c>FadeTo</c> step. Retail
    /// starts the loop at zero with <c>+0.02f</c>, and landing changes the same
    /// event to <c>-0.02f</c> toward zero. <c>UpdateStatus</c> advances it once
    /// per released 20 Hz update and clamps only after crossing the target, so
    /// float accumulation completes either edge on update 51 rather than 50.
    /// </summary>
    public const float RetailFlightLoopFadeStep = 0.02f;

    public static float AdvanceRetailFlightLoopSubVolume(
        float current,
        float target,
        float signedStep,
        out bool crossedTarget)
    {
        if (!float.IsFinite(current) ||
            !float.IsFinite(target) ||
            current is < 0f or > 1f ||
            target is < 0f or > 1f ||
            MathF.Abs(signedStep) != RetailFlightLoopFadeStep)
        {
            throw new ArgumentOutOfRangeException(
                nameof(current),
                "Flight-loop fades require bounded subvolumes and the exact signed retail step.");
        }

        float next = current + signedStep;
        crossedTarget = signedStep > 0f
            ? next > target
            : next < target;
        return crossedTarget ? target : next;
    }

    /// <summary>
    /// <c>CSoundManager::GetVolumeForPos</c>,
    /// <c>references/Onslaught/SoundManager.cpp:437-442</c>, verbatim:
    /// <code>
    /// float fvol = FAR_SOUND - dist;
    /// if (fvol &gt; FAR_SOUND) fvol = FAR_SOUND;
    /// if (fvol &lt; 0)        fvol = 0;
    /// SINT vol = SINT((fvol * 100) / FAR_SOUND);
    /// </code>
    /// The camera fetch above it is commented out and <c>cpos</c> is
    /// <c>(0,0,0)</c>, but <c>UpdateSoundPosition</c> has already moved the
    /// event into camera-local space (<c>SoundManager.cpp:992-993</c>), so this
    /// really is distance from the listener.
    /// </summary>
    public static int RetailSourceVolumeForDistance(float distanceUnits)
    {
        float fvol = RetailFarSoundUnits - distanceUnits;
        if (fvol > RetailFarSoundUnits)
        {
            fvol = RetailFarSoundUnits;
        }
        if (fvol < 0f)
        {
            fvol = 0f;
        }

        return (int)((fvol * 100f) / RetailFarSoundUnits);
    }

    /// <summary>
    /// <c>CSoundManager::StartSoundEvent</c>'s "SRG early out",
    /// <c>references/Onslaught/SoundManager.cpp:519-524</c>. A NON-LOOPING event
    /// starting at or beyond <c>FAR_SOUND</c> is deleted and never plays at all;
    /// a looping one is exempt and starts silent.
    /// </summary>
    public static bool RetailRefusesNonLoopingStart(float distanceUnits) =>
        distanceUnits >= RetailFarSoundUnits;

    /// <summary>
    /// <c>CSoundManager::Fade</c>,
    /// <c>references/Onslaught/SoundManager.cpp:760-793</c>, verbatim, returning
    /// DirectSound millibels. The parameter order is the source's own multiply
    /// order so the float rounding matches:
    /// <code>
    /// tv = SINT(float(v) * event-&gt;mMasterVolume * event-&gt;mSubVolume *
    ///           mMasterVolume * mGameSoundsMasterVolume);
    /// tv = tv * 200;                  //was 350...
    /// if (tv &gt; 10000) tv = 10000;
    /// tv = ((tv - 10000)/2);
    /// if (tv &lt; -10000) tv = -10000;
    /// </code>
    /// Two consequences the linear->dB form did not have. The truncation to
    /// <c>SINT</c> happens BEFORE the scale, so the output is quantised to whole
    /// 100-millibel (1 dB) steps. And the <c>10000</c> cap is a genuine
    /// PLATEAU: every combined multiplier at or above <c>0.5</c> tracked, or
    /// <c>50/127 = 0.3937</c> untracked, plays at exactly 0 dB.
    /// The floor is <c>-5000</c> mB, not silence.
    /// </summary>
    public static int RetailFadeMillibels(
        int sourceVolume,
        float eventVolume,
        float subVolume,
        float masterMix,
        float typeMasterMix)
    {
        int tv = (int)(sourceVolume * eventVolume * subVolume * masterMix * typeMasterMix);

        tv *= 200;
        if (tv > 10_000)
        {
            tv = 10_000;
        }
        tv = (tv - 10_000) / 2;
        if (tv < -10_000)
        {
            tv = -10_000;
        }

        return tv;
    }

    /// <summary>
    /// <c>CPCSoundManager::UpdateSound</c>'s shaping stage,
    /// <c>references/Onslaught/pcsoundmanager.cpp:405-410</c>, under the
    /// developer's own comment "Ensure we actually fall off to silence":
    /// <code>
    /// int vol = event-&gt;mCurrentAttenuatedVolume;
    /// if (vol &lt; -4000) vol = vol + ((vol+4000)*2);
    /// </code>
    /// It triples the slope below -40 dB. It does not in fact reach silence:
    /// <c>Fade</c>'s -5000 floor shapes to -7000 mB, i.e. -70 dB.
    /// This is applied to the ATTENUATED volume — <c>:405</c> reads
    /// <c>mCurrentAttenuatedVolume</c>, not <c>mCurrentVolume</c>.
    /// </summary>
    public static int RetailPcShapedMillibels(int millibels) =>
        millibels < -4_000
            ? millibels + ((millibels + 4_000) * 2)
            : millibels;

    /// <summary>
    /// The whole released chain, in Godot's <c>VolumeDb</c> unit. DirectSound
    /// <c>SetVolume</c> takes hundredths of a decibel
    /// (<c>references/Onslaught/pcsoundmanager.cpp:412</c>), so the conversion
    /// is a divide by 100 and nothing else.
    /// </summary>
    public static float RetailVolumeDb(
        int sourceVolume,
        float eventVolume,
        float subVolume,
        float masterMix,
        float typeMasterMix) =>
        RetailPcShapedMillibels(RetailFadeMillibels(
            sourceVolume,
            eventVolume,
            subVolume,
            masterMix,
            typeMasterMix)) / 100f;

    /// <summary>
    /// <c>CPCSoundManager::UpdateSound</c>,
    /// <c>references/Onslaught/pcsoundmanager.cpp:398-401</c>, with the
    /// developer's own comment kept because it is the whole explanation:
    /// <code>
    /// // ## SRG  clamp to 1.0 to stop stalls  (why won't it work??)
    /// if (event-&gt;mPitchMultiplier &gt; 1.0f) event-&gt;mPitchMultiplier = 1.0f ;
    /// mDSBuffer[event-&gt;mChannel]-&gt;SetFrequency((UINT)(event-&gt;mPitchMultiplier*44000));
    /// </code>
    /// This runs immediately before the single <c>SetFrequency</c> call, both at
    /// PlaySound time (<c>:268</c>) and on every later update, so there is no
    /// path on the released PC build by which a pitch above 1.0 reaches a
    /// buffer. BOTH producers only ever emit values at or above 1.0 —
    /// <c>PlayEffect</c>'s <c>pitch = 1.0f + (rand() % mPitchVariance)/100.0f</c>
    /// (<c>references/Onslaught/SoundManager.cpp:1188-1196</c>) and the jet's
    /// <c>SetPitch(event, 1.f + thruster*0.25f)</c>
    /// (<c>references/Onslaught/BattleEngine.cpp:1542</c>) — so retail PC plays
    /// every sample at a constant 44000 Hz.
    ///
    /// The producers are deliberately still reproduced at the call sites and
    /// then passed through here, rather than deleted, so a future reader sees
    /// why the answer is always 1.0 and does not "restore" the modulation.
    /// </summary>
    public static float RetailPcPitchMultiplier(float desiredPitchMultiplier) =>
        desiredPitchMultiplier > 1f ? 1f : desiredPitchMultiplier;

    private static Level100MessageAudioSpec Message(
        int messageId,
        string symbol,
        string audioStem) => new(
            messageId,
            symbol,
            audioStem,
            $"res://Assets/Level100/TutorialAudio/{audioStem}.ogg");

    private static Level100AudioCueRecipe Terminal(
        string localName,
        int record,
        string effectName) => Cue(
            $"res://Assets/Level100/SoundEffects/terminal-{localName}.wav",
            record,
            effectName,
            RetailHudMessageVolume);

    private static Level100AudioCueRecipe Cue(
        string resourcePath,
        int record,
        string effectName,
        float linearVolume,
        int pitch = 0,
        bool looping = false) => new(
            resourcePath,
            record,
            effectName,
            linearVolume,
            pitch,
            looping);
}

internal sealed class Level100CharacterMessageQueue
{
    private readonly Queue<Level100QueuedCharacterMessage> _messages = [];

    public int Count => _messages.Count;

    public void Enqueue(int speakerId, int messageId) =>
        _messages.Enqueue(new Level100QueuedCharacterMessage(
            speakerId,
            Level100AudioCatalog.GetCharacterMessage(messageId)));

    public bool TryDequeue(out Level100QueuedCharacterMessage message) =>
        _messages.TryDequeue(out message);

    public void Clear() => _messages.Clear();
}

internal readonly record struct Level100QueuedCharacterMessage(
    int SpeakerId,
    Level100MessageAudioSpec Audio);
