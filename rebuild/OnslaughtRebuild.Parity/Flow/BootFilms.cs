// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/DXFMV.cpp (CDXFMV::Play 0053f190), src/DXFrontEndVideo.cpp (CDXFrontEndVideo: Open
// 005412e0 and InitVideo 00541430 as Play calls them, synchronously; NextFrame 00541d30; Render 00541790's frame copy),
// src/fmv.cpp (CFMV::PlayFullscreen 00465640, ReceiveButtonAction 004656e0) and src/System.cpp (CSystem::Init 004efb10,
// its film block). Reconstructed; not in Stuart's release.

using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Platform;

namespace OnslaughtRebuild.Parity.Flow;

/// <summary>A film file's Bink header, as Play needs it: its frame count and rate.</summary>
public readonly record struct FilmHeader(int Frames, int RateNumerator, int RateDenominator);

/// <summary>The films on disk: <c>data\video\&lt;name&gt;.vid</c>'s Bink header, or null when the file does not open.</summary>
public interface IFilmFiles
{
    FilmHeader? Probe(string name);
}

/// <summary>
/// BinkWait's answer: whether the film's next frame is due. Play sleeps a millisecond while it is not. The Presents do
/// not depend on it (a wait never presents); only the time spent waiting does.
/// </summary>
public interface IFilmPacing
{
    /// <summary>A film was opened (Bink starts its clock).</summary>
    void Opened(FilmHeader film);

    /// <summary>BinkWait(bink) == 0: the frame <paramref name="frameNum"/> (1-based, Bink's FrameNum) is due.</summary>
    bool FrameDue(int frameNum);

    /// <summary>Sleep(1) on the main thread.</summary>
    void Sleep();
}

/// <summary>
/// The pacing of a contributor's retail replay harness: retail's binkw32 follows each film's sound track, which plays on Wine's
/// real-time mixer, so every frame comes due by real time and the virtual clock moves only with the Presents (no spin
/// escape fired during the boot films). Modelled as every frame due at once: exact for the Presents, which a wait never
/// makes; the waits themselves are not modelled.
/// </summary>
public sealed class HarnessFilmPacing : IFilmPacing
{
    public void Opened(FilmHeader film)
    {
    }

    public bool FrameDue(int frameNum) => true;

    public void Sleep()
    {
    }
}

/// <summary>
/// CFMV and its DirectX player (CDXFMV::Play): a film full screen, one Present before it, one for each frame shown and
/// one after. Play opens the film synchronously, which decodes frame 1. Each loop takes input (a mouse button skips),
/// lets the controller act (a skip-cutscene button, or select when the film is skippable, quits), then asks for the
/// next frame: not due, it sleeps a millisecond; due, the next frame is decoded and Bink's frame number advances. When
/// that number reaches the frame count the frame just decoded is still drawn and the film ends, so of N frames N - 1 are
/// shown: a film costs N + 1 Presents.
/// </summary>
public sealed class FilmPlayer : IButtonReceiver
{
    private readonly PlatformClock _clock;
    private readonly PlatformInput _input;
    private readonly ControllerContext _controls;
    private readonly IFilmFiles _files;
    private readonly IFilmPacing _pacing;

    public FilmPlayer(PlatformClock clock, PlatformInput input, ControllerContext controls, IFilmFiles files, IFilmPacing pacing)
    {
        _clock = clock;
        _input = input;
        _controls = controls;
        _files = files;
        _pacing = pacing;
    }

    /// <summary>CFMV::mQuit: the film was skipped.</summary>
    public bool Quit { get; private set; }

    /// <summary>CFMV::mSkippable: select skips too (attract mode, or a film played skippable).</summary>
    public bool Skippable { get; private set; }

    /// <summary>CLIPARAMS.mAttractMode, which a skip clears.</summary>
    public bool AttractMode { get; set; }

    /// <summary>The films played, with their frames shown and whether each was skipped, for comparison with retail.</summary>
    public List<(string Name, int FramesShown, bool Skipped)> Trace { get; } = [];

    /// <summary>The Presents a film loop makes (a Flip is one Present).</summary>
    private void Flip() => _clock.Present();

    /// <summary>CFMV::PlayFullscreen: skippable when asked or in attract mode; true when the player skipped it.</summary>
    public bool PlayFullscreen(string name, bool skippable)
    {
        Skippable = skippable || AttractMode;
        return Play(name);
    }

    /// <summary>CSystem::Run's attract films: <c>FMV.PlayFullscreen(name, TRUE, ...)</c>, skippable.</summary>
    public bool PlayAttractFilm(string name) => PlayFullscreen(name, true);

    /// <summary>CDXFMV::Play: false when the file does not exist (nothing is shown); otherwise whether it was skipped.</summary>
    public bool Play(string name)
    {
        if (_files.Probe(name) is not { } film)
        {
            return false;
        }

        // Open (synchronous) and InitVideo: frame 1 decoded, Bink's frame number now 2.
        _pacing.Opened(film);
        int frameNum = 2;
        bool decoded = true;
        bool copied = false;
        var controller = new Controller(0, 1, this);

        // ClearScreen, Flip, ClearScreen; the controller's state flushed.
        Flip();
        controller.Flush(_controls);
        Quit = false;
        int shown = 0;
        while (!Quit)
        {
            _input.Mouse.ResetTransientState();
            _input.Process(_clock.QueryPerformanceCounter());
            if (_input.Mouse.Buttons[0].Down || _input.Mouse.Buttons[1].Down || _input.Mouse.Buttons[2].Down)
            {
                _input.Mouse.ResetTransientState();
                Quit = true;
                AttractMode = false;
            }

            controller.Flush(_controls);

            // NextFrame(true): with the frame copied, wait until the next is due, then decode it.
            int result = 0;
            if (copied)
            {
                if (!_pacing.FrameDue(frameNum))
                {
                    _pacing.Sleep();
                    continue;
                }

                copied = false;
                if (!decoded)
                {
                    frameNum++;
                    decoded = true;
                }
            }

            if (frameNum == film.Frames)
            {
                result = 1;
            }

            // Render: the decoded frame is copied to the texture and drawn; the scene is flipped.
            if (!copied)
            {
                copied = true;
                decoded = false;
            }

            Flip();
            shown++;
            if (result != 0)
            {
                break;
            }
        }

        // CloseVideo, Shutdown, ClearScreen, Flip.
        Flip();
        Trace.Add((name, shown, Quit));
        return Quit;
    }

    /// <summary>CFMV::ReceiveButtonAction: the skip-cutscene button, or select (0x42) when skippable, ends the film.</summary>
    public void ReceiveButtonAction(Controller from, int button, float value)
    {
        if (button == Buttons.SkipCutscene || (button == 0x42 && Skippable))
        {
            Quit = true;
            AttractMode = false;
        }
    }

    bool IButtonReceiver.CanBeControlledWhenInPause => true;

    ControlType IButtonReceiver.ControlType => (ControlType)0;
}

/// <summary>
/// CSystem::Init's films: none with a level given (-level), straight to the device select or -skipfmv. Otherwise, in
/// attract mode the logo and then, unless it was skipped, the vendor film when its tweak is on, then, unless skipped, the
/// opening film; on a PAL machine (retail's default) the logo, then (outside the auto-configuration test) the vendor film
/// when its tweak is on and the opening film; on an NTSC one four films the PC release does not ship. The splash over
/// the opening film's last frames is drawing only.
/// </summary>
public static class BootFilms
{
    /// <summary>
    /// The loading-screen Presents between the films and <c>CFrontEnd::Init</c>.
    /// Observed in the port's full-boot trace: one loading Present after the films (Present 2287). The other
    /// non-film Present is the device's first Present, before CSystem::Init; with -skipfmv these are Presents 1 and 2.
    /// </summary>
    public const int PresentsAfterFilms = 1;

    public sealed record Settings(bool LevelGiven, bool StraightToDeviceSelect, bool SkipFmv, bool Attract, bool Pal = true,
        bool AutoConfigTest = false, bool VendorFilm = false);

    public static void Play(FilmPlayer player, Settings s)
    {
        if (s.LevelGiven || s.StraightToDeviceSelect || s.SkipFmv)
        {
            return;
        }

        player.AttractMode = s.Attract;
        if (s.Attract)
        {
            if (!player.PlayFullscreen("ltlogo", true))
            {
                if (!s.VendorFilm || !player.PlayFullscreen("TWIMTBP_GefFX_640x480_Audio", true))
                {
                    player.PlayFullscreen("openingfmv", true);
                }
            }
        }
        else if (!s.Pal)
        {
            player.PlayFullscreen("copyright", false);
            player.PlayFullscreen("atari", false);
            player.PlayFullscreen("lostlogo", false);
            player.PlayFullscreen("introntsc", false);
        }
        else
        {
            player.PlayFullscreen("ltlogo", false);
            if (!s.AutoConfigTest)
            {
                if (s.VendorFilm)
                {
                    player.PlayFullscreen("TWIMTBP_GefFX_640x480_Audio", false);
                }

                player.PlayFullscreen("openingfmv", false);
            }
        }
    }
}
