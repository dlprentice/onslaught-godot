// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/game.cpp (CGame::DrawGameStuff 004714c0: the end-of-level screen, and
// draw_end_level_darkener) and src/MissionObjective.h.

using OnslaughtRebuild.Parity.Flow.Draw;
using OnslaughtRebuild.Parity.Text;

namespace OnslaughtRebuild.Parity.Game;

/// <summary>The process's statics the game's render keeps across levels (<c>DrawGameStuff</c>'s <c>lLastTime</c>).</summary>
public sealed class GameRenderStatics
{
    /// <summary><c>lLastTime</c>: the clock at the end screen's last render, from 0.</summary>
    public float WinLoseLastTime { get; set; }
}

/// <summary>
/// The end-of-level screen <c>CGame::DrawGameStuff</c> draws once a level is decided (the state past playing and short of
/// quit, the end count at 0.049 or more): the screen darkens towards alpha 0xA0 (0xFF when fading out) by 16 a render (1
/// when fading out), stepping as many 0.05 s frames as the clock says passed; then the result, and for a lost or won level
/// the lost reason, the primary and secondary objectives with their results. Its alpha is the game's
/// (<see cref="Game.WinLoseScreenAlpha"/>): at 255 the game's render draws nothing behind it. The alpha and the clock read
/// happen whether or not anything is drawn.
/// </summary>
public static class EndLevelOverlay
{
    public static void Render(Game game, Clock.PlatformClock clock, GameRenderStatics statics, FrontEndDraw? draw,
        BitmapFont? small, BitmapFont? normal, FrontEndTexts? texts)
    {
        if (!(game.State > GameState.Playing && game.State < GameState.Quit && game.EndLevelCount >= 0.049f)) return;
        int step = 16, max = 0xa0;
        if (game.FadeOut)
        {
            step = 1;
            max = 0xff;
        }

        float time = clock.GetSysTimeFloat();
        if (time > statics.WinLoseLastTime)
        {
            float delta = time - statics.WinLoseLastTime;
            if (delta < 5f)
            {
                int frames = (int)(delta / 0.05f);
                if (frames > 1) step *= frames;
            }
        }

        statics.WinLoseLastTime = time;
        game.WinLoseScreenAlpha += step;
        if (game.WinLoseScreenAlpha > max) game.WinLoseScreenAlpha = max;
        if (draw is null || small is null || normal is null || texts is null) return;

        uint alpha = (uint)game.WinLoseScreenAlpha << 24;
        string Db(string name) => texts.ById(texts.Ids[name]);
        void Shadowed(BitmapFont font, float x, float y, uint colour, string text) =>
            draw.DrawTextWithShadow(font, new QuadTexture(ReferenceEquals(font, normal) ? CanvasTextureKind.NormalFont : CanvasTextureKind.SmallFont),
                x, y, colour, text, 0, 0f);
        void Centred(string text, float y) => Shadowed(normal, 320f - normal.GetTextExtent(text).Width * 0.5f, y, 0xffffffff, text);
        void Darken() => draw.DrawNamedSprite(@"hud\v2\white.tga", -10f, -10f, 0.001f, alpha, 680f / 64f, 520f / 64f, format: "X8R8G8B8");

        switch (game.State)
        {
            case GameState.Player1Won:
                Darken();
                Centred(Db("FETX_VICTORY"), 50f);
                Centred(Db("FETX_DEFEAT"), 245f);
                break;
            case GameState.Player2Won:
                Darken();
                Centred(Db("FETX_DEFEAT"), 50f);
                Centred(Db("FETX_VICTORY"), 245f);
                break;
            case GameState.GameDrawn:
                Darken();
                // Retail measures the draw text but draws defeat and victory at those widths.
                float drawWidth = normal.GetTextExtent(Db("JCL_DRAW")).Width;
                Shadowed(normal, 320f - drawWidth * 0.5f, 50f, 0xffffffff, Db("FETX_DEFEAT"));
                Shadowed(normal, 320f - drawWidth * 0.5f, 245f, 0xffffffff, Db("FETX_VICTORY"));
                break;
        }

        if (game.State is not (GameState.LevelLost or GameState.LevelWon)) return;
        Darken();
        Centred(Db(game.State == GameState.LevelWon ? "FETX_VICTORY" : "FETX_DEFEAT"), 50f);
        const int x = 65;
        int y = 90;
        if (game.State == GameState.LevelLost)
        {
            string reason = game.LevelLostReason == 0 ? "(ERROR) unknown game over reason" : texts.ById(unchecked((uint)game.LevelLostReason));
            foreach (string line in small.WordWrap(reason, 500f))
            {
                Shadowed(small, x, y, 0xffffffff, line);
                y += 16;
            }

            y += 20;
        }

        y = Objectives(game.PrimaryObjectives, "FETX_PRIMARY_OBJECTIVES", y, out bool foundPrimary);
        if (foundPrimary) y += 8;
        Objectives(game.SecondaryObjectives, "FETX_SECONDARY_OBJECTIVES", y, out _);

        int Objectives(MissionObjective[] objectives, string heading, int top, out bool found)
        {
            found = false;
            foreach (MissionObjective objective in objectives)
            {
                if (objective.State == MissionObjectiveState.NotDefined) continue;
                if (!found)
                {
                    Shadowed(small, x, top, 0xffffa040, Db(heading));
                    top += 16;
                    found = true;
                }

                string text = texts.ById(unchecked((uint)objective.StringId));
                Shadowed(small, x, top, 0xffffffff, text);
                if (small.GetTextExtent(text).Width > 322) top += 16;
                bool complete = objective.State == MissionObjectiveState.Complete;
                Shadowed(small, x + 330, top, complete ? 0xff10ff1fu : 0xffff1f1fu, Db(complete ? "FETX_COMPLETE" : "FETX_INCOMPLETE"));
                top += 16;
            }

            return top;
        }
    }
}
