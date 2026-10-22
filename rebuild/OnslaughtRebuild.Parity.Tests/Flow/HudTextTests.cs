// SPDX-License-Identifier: GPL-3.0-only
// CHud::Render00485d50 00485d50 (the weapon's name and the warnings) and Render00486940 00486940 (the ammunition count),
// onslaught-decomp src/Hud.cpp; the native port's fe-lose back buffers show the same warning at the same Presents (PARITY.md).

using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Draw;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Render;
using OnslaughtRebuild.Parity.Tests;
using OnslaughtRebuild.Parity.Things;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Flow;

/// <summary>
/// The HUD's text through the level's canvas, and the parts the tutorial highlights: on the front-end loss route the walker overheats its weapon from about
/// Present 5100, and the HUD writes "weapon overheating" centred on the second warning row, a shadow then the text, fading
/// out over the five seconds after each overheat; before the first one it writes nothing (the weapon is an energy weapon, so no
/// ammunition count, and no weapon change, so no name).
/// </summary>
public sealed class HudTextTests
{
    private const string Tape = "portrefs/tapes/fe-level100-lose-quit-7300f.tape";

    private sealed class StopRun : Exception;

    [RetailGoldenFact(Tape)]
    public void TheOverheatingWarningIsWrittenCentredOnTheSecondRow()
    {
        var textures = new FrontEndTextures(GameData.Read);
        var run = new GameRun(GameData.Read, id => new RetailPcText(GameData.Read("BEA.exe").Bytes, 0)[id],
            new RetailChainTests.InstalledFilms(), true, LevelSession.VirtualVoiceFor(GameData.Read))
        {
            Tape = RetailTape.Load(RetailGolden.File(Tape)!),
        };
        var frames = new Dictionary<long, CanvasFrame>();
        run.LevelDraw = () => new FrontEndDraw(textures) { Canvas = new RecordingCanvas(f => frames[f.Present] = f) };
        float overheated = 0;
        run.OnPresented = () =>
        {
            if (run.Clock.PresentCount == 5300 && run.Level?.Game.GetPlayer(0)?.BattleEngine is BattleEngine be)
            {
                overheated = be.WeaponOverheatedTime;
                Assert.Equal(0, be.ChangedWeaponTime.CompareTo(-20.0f));
                Assert.True(be.StoreHeat[be.GetCurrentWeapon()!.Data.AmmoStore]);
            }

            if (run.Clock.PresentCount >= 5300)
            {
                throw new StopRun();
            }
        };
        try
        {
            run.Run();
        }
        catch (StopRun)
        {
        }

        static CanvasQuad[] Text(CanvasFrame frame) => [.. frame.Quads.Where(q => q.Texture.Kind == CanvasTextureKind.NormalFont)];
        Assert.Empty(Text(frames[4900]));
        Assert.True(overheated > 0);

        string warning = run.FrontEnd.Texts!.ById(0x227a52ff);
        int width = run.FrontEnd.NormalFont!.GetTextExtent(warning).Width;
        CanvasQuad[] quads = Text(frames[5300]);
        Assert.Equal(2 * warning.Length, quads.Length);
        CanvasQuad shadow = quads[0], text = quads[warning.Length];
        Assert.Equal(MathF.Floor(320f - width * 0.5f) - 0.5f, text.V0.X);
        Assert.Equal(360f - 0.5f, text.V0.Y);
        Assert.Equal(text.V0.X + 1f, shadow.V0.X);
        // 0xff3020 doubled: the guns panel's sprites, drawn just before while the weapon part shows, leave the stage at 2x.
        Assert.Equal(0xff6040u, text.V0.Colour & 0xffffff);
        Assert.InRange(text.V0.Colour >> 24, 1u, 0xfeu);
        Assert.False(text.ZEnable);
    }

    /// <summary>
    /// Level 100's script highlights the compass (P1668 to P1883), the radar (P1884 to P2072) and the current weapon
    /// (P3424 to P3689) in turn; the port's back buffers flash each at the same sampled Presents (PARITY.md).
    /// </summary>
    [RetailGoldenFact(Tape)]
    public void TheTutorialHighlightsTheCompassTheRadarAndTheWeaponInTurn()
    {
        var run = new GameRun(GameData.Read, id => new RetailPcText(GameData.Read("BEA.exe").Bytes, 0)[id],
            new RetailChainTests.InstalledFilms(), true, LevelSession.VirtualVoiceFor(GameData.Read))
        {
            Tape = RetailTape.Load(RetailGolden.File(Tape)!),
        };
        var flashing = new Dictionary<int, (long First, long Last)>();
        run.OnPresented = () =>
        {
            if (run.Level is { } level)
            {
                int[] state = level.Context.HudParts.State;
                for (int part = 0; part < state.Length; part++)
                {
                    if (state[part] == HudParts.Flashing)
                    {
                        long p = run.Clock.PresentCount;
                        flashing[part] = flashing.TryGetValue(part, out var span) ? (span.First, p) : (p, p);
                    }
                }
            }

            if (run.Clock.PresentCount >= 4000)
            {
                throw new StopRun();
            }
        };
        try
        {
            run.Run();
        }
        catch (StopRun)
        {
        }

        Assert.Equal([HudParts.Compass, HudParts.Radar, HudParts.CurrentWeapon], flashing.Keys.Order());
        Assert.Equal((1668L, 1883L), flashing[HudParts.Compass]);
        Assert.Equal((1884L, 2073L), flashing[HudParts.Radar]);
        Assert.Equal((3424L, 3690L), flashing[HudParts.CurrentWeapon]);
    }

    /// <summary>
    /// On the tutorial pilot's c17 route (the Core equals the port on every probe field through P22150) the walker's
    /// current weapon becomes the twin vulcan cannon, a store weapon, at P8533, and the HUD writes its store's count from
    /// then on, plain (not doubled). The frozen port's back buffers (ae04185f, every 25 Presents) show none at P8500 and
    /// P8525 and exactly these counts at these Presents.
    /// </summary>
    [RetailGoldenFact("portrefs/l100-c17-port-20261021/c17-fire-port-delivered.tape",
        "portrefs/l100-trainer-port-20261021/sound-seeds-admission-root-c.json")]
    public void TheAmmunitionCountIsThePortsOnTheC17Route()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read),
            soundOptions: TrainerRouteTests.SoundProfile(
                RetailGolden.File("portrefs/l100-trainer-port-20261021/sound-seeds-admission-root-c.json")!));
        session.Tape = RetailTape.Load(RetailGolden.File("portrefs/l100-c17-port-20261021/c17-fire-port-delivered.tape")!);
        var port = new Dictionary<long, string?>
        {
            [8500] = null, [8525] = null, [8550] = "2000", [8600] = "2000", [9000] = "2000", [9300] = "2000", [9325] = "1988",
            [9350] = "1968", [9375] = "1946", [9400] = "1928", [9425] = "1908", [9450] = "1886", [9475] = "1868", [9500] = "1854",
        };
        var ours = new Dictionary<long, string?>();
        while (session.Clock.PresentCount < 9500)
        {
            session.AdvanceToNextPresent();
            if (port.ContainsKey(session.Clock.PresentCount))
            {
                HudText? count = session.Context.Particles!.World.HudTexts.SingleOrDefault(t => t.Literal is not null);
                ours[session.Clock.PresentCount] = count?.Literal;
                if (count is not null)
                {
                    Assert.Equal((567f, 340f, 0xff30ff30u, 5f, false), (count.X, count.Y, count.Colour, count.Reveal, count.Modulate2x));
                }
            }
        }

        Assert.Equal(port, ours);
    }

    [Fact]
    public void PartsFlashOnTheSecondAndFourthQuarterOfEachSecond()
    {
        var parts = new HudParts();
        Assert.True(parts.IsComponentVisible(HudParts.CurrentWeapon));
        parts.State[HudParts.CurrentWeapon] = HudParts.Flashing;
        parts.Update(10.10f, false);
        Assert.False(parts.IsComponentVisible(HudParts.CurrentWeapon));
        parts.Update(10.30f, false);
        Assert.True(parts.IsComponentVisible(HudParts.CurrentWeapon));
        parts.Update(10.55f, false);
        Assert.False(parts.IsComponentVisible(HudParts.CurrentWeapon));
        parts.Update(10.80f, true);
        Assert.True(parts.IsComponentVisible(HudParts.CurrentWeapon));
        Assert.False(parts.IsComponentVisible(HudParts.BattleLineMap));
        parts.Update(10.80f, false);
        Assert.True(parts.IsComponentVisible(HudParts.BattleLineMap));
    }
}
