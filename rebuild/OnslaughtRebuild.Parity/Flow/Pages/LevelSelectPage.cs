// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/FEPLevelSelect.cpp and src/FEPLevelSelect.h (reconstructed from the retail program; not
// in Stuart's release). FEMouseEdgeSlide, from Stuart's FEPGoodies.cpp, is in MouseEdgeSlide.cs. The level and episode
// names are from src/FrontEndText.cpp (CFrontEndText::GetLevelName 00469550, GetEpisodeName 00469c20, GetBriefingTextId
// 00469cf0, GetBriefingText 0046a1f0) and src/text.cpp (CText::GetFollowingString 004f2500).

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Flow.Draw;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Text;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary>
/// The level select (<c>CFEPLevelSelect</c>): the campaign's worlds as a grid of columns (a new column at each world
/// ending in 0 or 1), scrolled sideways. Select on an unlocked world goes to its briefing; the mouse picks a world by
/// hovering and clicking, and scrolls the grid at the screen's edges.
/// </summary>
/// <remarks>
/// Not modelled: the auto-configuration test's two automatic selections (the switch is off).
/// </remarks>
public sealed class LevelSelectPage : FrontEndPageBase, ILevelSelect
{
    public const int Columns = 50;
    public const int Rows = 6;

    // gLevelSelectScrollLeft and gLevelSelectScrollRight (00629390, 00629394).
    private const float ScrollLeft = 90f;
    private const float ScrollRight = 570f;

    private readonly FrontEnd _frontEnd;
    private readonly int[] _columnLength = new int[Columns];
    private readonly int[,] _levels = new int[Columns, Rows];
    private readonly float[,] _transition = new float[Columns, Rows];
    private readonly List<(int Column, int Row)>[,] _links = new List<(int Column, int Row)>[Columns, Rows];

    /// <summary>The constructor (0x00460140): the grid from the campaign's level structure.</summary>
    public LevelSelectPage(FrontEnd frontEnd)
    {
        _frontEnd = frontEnd;
        int column = -1;
        for (int i = 0; i < CareerTables.LevelStructure.GetLength(0); i++)
        {
            int level = CareerTables.LevelStructure[i, 0];
            if (level % 10 is 0 or 1) column++;
            _levels[column, _columnLength[column]] = level;
            _columnLength[column]++;
        }
        NumColumns = column + 1;

        // The lines from each level to the ones it opens (mLinks), which only the render draws.
        for (int i = 0; i < Columns; i++)
            for (int j = 0; j < Rows; j++)
                _links[i, j] = [];
        for (int i = 0; i < CareerTables.LevelStructure.GetLength(0); i++)
        {
            (int c, int r) = Find(CareerTables.LevelStructure[i, 0]);
            if (c == -1 || r == -1) continue;
            for (int k = 1; k <= 2; k++)
            {
                int next = CareerTables.LevelStructure[i, k];
                if (next == -1) continue;
                (int c2, int r2) = Find(CareerTables.LevelStructure[next, 0]);
                if (c2 != -1 && r2 != -1) _links[c, r].Add((c2, r2));
            }
        }
    }

    /// <summary>The grid cell holding a level (the last match, as the constructor's full scan finds it), or (-1, -1).</summary>
    private (int Column, int Row) Find(int level)
    {
        int c = -1, r = -1;
        for (int a = 0; a < Columns; a++)
        {
            for (int b = 0; b < Rows; b++)
            {
                if (_levels[a, b] != level) continue;
                c = a;
                r = b;
            }
        }
        return (c, r);
    }

    public int NumColumns { get; }
    public int Column { get; private set; }
    public int Row { get; private set; }
    public float Pos { get; private set; }
    public float PosDest { get; private set; }
    public float EpisodeTime { get; private set; }
    public float CursorTime { get; private set; }
    public float StartTime { get; private set; }

    public int ColumnLength(int column) => _columnLength[column];
    public int Level(int column, int row) => _levels[column, row];
    public float Transition(int column, int row) => _transition[column, row];

    private int ClampedRow()
    {
        int row = Row;
        if (row >= _columnLength[Column]) row = _columnLength[Column] - 1;
        else if (row < 0) row = 0;
        return row;
    }

    // x87: floorf of the float quotient; the products and difference are stored to floats.
    private float Slide() => (float)Math.Floor(X87.Div(360f, (float)(NumColumns - 1)));

    private float ColumnPos(float slide) => (float)X87.Sub(X87.Mul(Column, 60f), X87.Mul(Column, slide));

    /// <summary><c>FindSelectedLevel</c>: the cursor on the front end's selected level (or column 0, row 0), the grid scrolled to it.</summary>
    private void FindSelectedLevel()
    {
        Column = 0;
        Row = 0;
        for (int i = 0; i < Columns; i++)
        {
            for (int j = 0; j < Rows; j++)
            {
                if (_levels[i, j] != _frontEnd.SelectedLevel) continue;
                Column = i;
                Row = j;
                goto found;
            }
        }
    found:
        float slide = Slide();
        Pos = PosDest = ColumnPos(slide);
        EpisodeTime = _frontEnd.Clock.GetSysTimeFloat();
        CursorTime = _frontEnd.Clock.GetSysTimeFloat();
    }

    /// <summary><c>SyncToSelectedLevel</c> (0x00460310).</summary>
    public void SyncToSelectedLevel() => FindSelectedLevel();

    /// <summary><c>CFEPLevelSelect::Init</c> (0x004603d0).</summary>
    public override bool Init()
    {
        FindSelectedLevel();
        return true;
    }

    /// <summary><c>CFEPLevelSelect::Shutdown</c> (0x00460490): the cursor on the selected level, without scrolling.</summary>
    public override void Shutdown()
    {
        Column = 0;
        Row = 0;
        for (int i = 0; i < Columns; i++)
        {
            for (int j = 0; j < Rows; j++)
            {
                if (_levels[i, j] != _frontEnd.SelectedLevel) continue;
                Column = i;
                Row = j;
                return;
            }
        }
    }

    /// <summary><c>CFEPLevelSelect::ActiveNotification</c> (0x004604f0).</summary>
    public override void ActiveNotification(FrontEndPage from)
    {
        if (from is FrontEndPage.Briefing or FrontEndPage.Debriefing) _transition[Column, Row] = 1f;
    }

    /// <summary><c>CFEPLevelSelect::TransitionNotification</c> (0x00460520): only the first six columns' first three rows reset.</summary>
    public override void TransitionNotification(FrontEndPage from)
    {
        StartTime = _frontEnd.Clock.GetSysTimeFloat();
        for (int i = 0; i < 6; i++)
            for (int j = 0; j < 3; j++)
                _transition[i, j] = 0f;
        if (from is FrontEndPage.Briefing or FrontEndPage.Debriefing) _transition[Column, Row] = 1f;
    }

    /// <summary><c>CFEPLevelSelect::Process</c> (0x00460590): the glows fade, then the mouse scrolls the grid at the edges.</summary>
    public override void Process(FrontEndPageState state)
    {
        int row = ClampedRow();
        for (int i = 0; i < Columns; i++)
        {
            for (int j = 0; j < Rows; j++)
            {
                float t = state == FrontEndPageState.Active && i == Column && j == row
                    ? (float)X87.Add(_transition[i, j], 0.1f)
                    : (float)X87.Sub(_transition[i, j], 0.1f);
                if (t < 0f) t = 0f;
                if (t > 1f) t = 1f;
                _transition[i, j] = t;
            }
        }
        Pos = MouseEdgeSlide.Apply(_frontEnd, state, Pos, 1100f);
    }

    /// <summary><c>CFEPLevelSelect::ButtonPressed</c> (0x004606b0). Up and down are swapped on screen: up moves to the next row.</summary>
    public override void ButtonPressed(int button, float value)
    {
        if (button is >= FrontEndButtons.MenuUp and <= FrontEndButtons.MenuDown)
        {
            if (Row >= _columnLength[Column]) Row = _columnLength[Column] - 1;
            else if (Row < 0) Row = 0;
        }

        int oldColumn = Column;
        int oldRow = Row;
        int oldEpisode = _levels[Column, Row] / 100;

        switch (button)
        {
            case FrontEndButtons.MenuDown:
                Row = Math.Max(Row - 1, 0);
                _frontEnd.PlaySound(FrontEndSound.Move);
                break;
            case FrontEndButtons.MenuUp:
                Row++;
                if (Row >= _columnLength[Column]) Row = _columnLength[Column] - 1;
                _frontEnd.PlaySound(FrontEndSound.Move);
                break;
            case FrontEndButtons.MenuLeft:
                // Column 16 has four rows; its neighbours have two.
                if (Column == 16) Row = Row is 2 or 3 ? 1 : 0;
                Column = Math.Max(Column - 1, 0);
                _frontEnd.PlaySound(FrontEndSound.Move);
                if (Column == 16 && Row == 1) Row = 2;
                if (X87.Add(X87.Mul(Column, 60f), X87.Sub(148f, Pos)) < ScrollLeft) Pos = (float)X87.Sub(Pos, 60f);
                break;
            case FrontEndButtons.MenuRight:
                if (Column == 16) Row = Row is 2 or 3 ? 1 : 0;
                Column++;
                if (Column >= NumColumns) Column = NumColumns - 1;
                _frontEnd.PlaySound(FrontEndSound.Move);
                if (Column == 16 && Row == 1) Row = 2;
                if (X87.Add(X87.Mul(Column, 60f), X87.Sub(148f, Pos)) > ScrollRight) Pos = (float)X87.Add(Pos, 60f);
                break;
            case FrontEndButtons.MenuSelect:
            {
                int row = ClampedRow();
                if (IsWorldUnlocked(_levels[Column, row]))
                {
                    _frontEnd.SetPage(FrontEndPage.Briefing, 30);
                    _frontEnd.PlaySound(FrontEndSound.Select);
                    _frontEnd.SelectedLevel = _levels[Column, row];
                }
                break;
            }
            case FrontEndButtons.MenuBack:
                _frontEnd.PlaySound(FrontEndSound.Back);
                _frontEnd.SetPage(FrontEndPage.Main, 70);
                break;
        }

        PosDest = ColumnPos(Slide());
        if (Column != oldColumn || Row != oldRow) CursorTime = _frontEnd.Clock.GetSysTimeFloat();
        if (_levels[Column, Row] / 100 != oldEpisode) EpisodeTime = _frontEnd.Clock.GetSysTimeFloat();
    }

    /// <summary><c>SetCurrentLevelToHighestAvailable</c> (0x00460a40): the last unlocked world, scanning columns and rows backwards.</summary>
    public void SetCurrentLevelToHighestAvailable()
    {
        for (int c = NumColumns - 1; c >= 0; c--)
        {
            for (int r = _columnLength[c] - 1; r >= 0; r--)
            {
                if (!IsWorldUnlocked(_levels[c, r])) continue;
                Row = r;
                Column = c;
                Pos = PosDest = ColumnPos(Slide());
                EpisodeTime = _frontEnd.Clock.GetSysTimeFloat();
                CursorTime = _frontEnd.Clock.GetSysTimeFloat();
                _frontEnd.SelectedLevel = _levels[c, r];
                return;
            }
        }
    }

    /// <summary>
    /// <c>CFEPLevelSelect::IsWorldUnlocked</c> (0x00461a50): the TURKEY cheat opens everything; world 100 is always open;
    /// otherwise a completed link must lead to the world.
    /// </summary>
    public bool IsWorldUnlocked(int level)
    {
        if (SaveGamePage.CheatActive(_frontEnd.SaveGameName, _frontEnd.AllCheatsEnabled, 1)) return true;
        if (level == 100) return true;
        OnslaughtRebuild.Parity.Career.Career career = _frontEnd.Career;
        int node = career.GetNodeFromWorldNo(level);
        if (node == -1) return false;
        foreach (int link in career.ParentLinks(node))
        {
            if (career.Record.LinkType(link) == CareerLinkType.Complete) return true;
        }
        return false;
    }

    /// <summary><c>CFEPLevelSelect::RenderPreCommon</c> (folded with CFEPDirectory's, 0x0051ae50): the standard video background.</summary>
    public override void RenderPreCommon(float transition, FrontEndPage dest) =>
        _frontEnd.DrawStandardVideoBackground(transition, 0x3fffffff, dest);

    /// <summary>
    /// <c>CFEPLevelSelect::Render</c> (0x00460b40). Its state-changing part: walking the columns from x 148 - pos in steps
    /// of 60, a cursor column that has left the scroll band moves back into it; on screen, a click on an unlocked world
    /// (once the transition is complete) selects it, and hovering over one moves the cursor to it. Then the help line's
    /// select and back arrows. With <see cref="FrontEnd.Draw"/> it also draws: the sliding borders, the episode numbers
    /// and curves over each episode's first column, each world's mask (its grade's once it has one), blue glow (orange
    /// under the cursor), rings (growing into the briefing's ring for the chosen world as the briefing arrives) and lines
    /// to the worlds it opens, the small logo, the cursor world's name and episode, and the title bar.
    /// </summary>
    public override void Render(float transition, FrontEndPage dest)
    {
        FrontEndDraw? draw = _frontEnd.Draw;
        float t4 = transition * 4f;
        if (draw is not null) RenderEpisodes(draw, transition, dest, t4);

        // SINT under /QIfist rounds to nearest.
        int alpha = X87.Fist(X87.Mul(FrontEndDraw.RangeTransition(transition, 0.75f, 1f), 255f));
        float gx = (float)X87.Sub(148f, Pos);
        for (int x = 0; x < NumColumns; x++)
        {
            bool onscreen = !(gx < ScrollLeft) && !(gx > ScrollRight);
            if (gx < ScrollLeft && Column <= x)
            {
                Column = x + 1;
                Row = 0;
            }
            else if (gx > ScrollRight && Column >= x)
            {
                Column = x - 1;
                Row = 0;
            }
            if (!(gx < 610f)) break;

            if (!(gx < 0f))
            {
                // x87: the int row count loads exactly; the product, half and sum are stored to a float.
                float gy = (float)X87.Add(X87.Mul(X87.Mul(_columnLength[x] - 1, 60f), 0.5f), 320f);
                for (int y = 0; y < _columnLength[x]; y++)
                {
                    float left = (float)X87.Sub(gx, 20f), top = (float)X87.Sub(gy, 20f);
                    float right = (float)X87.Add(gx, 20f), bottom = (float)X87.Add(gy, 20f);
                    if (IsWorldUnlocked(_levels[x, y]))
                    {
                        if (onscreen && transition == 1f && _frontEnd.MouseClickedInRect(left, top, right, bottom))
                        {
                            Column = x;
                            Row = y;
                            ButtonPressed(FrontEndButtons.MenuSelect, 1f);
                        }
                        if (draw is not null) DrawWorld(draw, x, y, gx, gy, alpha);
                    }
                    bool unlocked = IsWorldUnlocked(_levels[x, y]);
                    if (onscreen && _frontEnd.GetCursorStateInRect(left, top, right, bottom) && unlocked)
                    {
                        Column = x;
                        Row = y;
                    }
                    if (draw is not null) DrawRingsAndLinks(draw, transition, dest, x, y, gx, gy, alpha, unlocked);
                    gy = (float)X87.Sub(gy, 60f);
                }
            }
            gx = (float)X87.Add(gx, 60f);
        }

        RenderNames(draw, transition, dest, t4);
        _frontEnd.RenderHelpArrow(5, transition);
        _frontEnd.RenderHelpArrow(1, transition);
        // The title is read whether or not it is drawn (each read checks the X key).
        if (_frontEnd.Texts is { } texts) _frontEnd.DrawTitleBar(texts.GetText(FrontEndText.SelectLevel), transition, dest);
    }

    /// <summary>
    /// The render's first part: the sliding borders, then over each episode's first column (a level ending in 00) its
    /// curve (additive, bright for the cursor's episode) and its number, fading in over the transition's last quarter.
    /// </summary>
    private void RenderEpisodes(FrontEndDraw draw, float transition, FrontEndPage dest, float t4)
    {
        _frontEnd.DrawSlidingTextBordersAndMask(transition, dest);
        float gx = (float)X87.Sub(148f, Pos);
        int ea = X87.Fist(X87.Mul(X87.Sub(t4, 3f), 255f));
        if (ea < 0) ea = 0;
        int episode = _levels[Column, ClampedRow()] / 100;
        for (int x = 0; x < NumColumns && gx < 610f; x++, gx = (float)X87.Add(gx, 60f))
        {
            if (gx < 0f || _levels[x, 0] % 100 != 0) continue;
            uint c = episode != _levels[x, 0] / 100 ? 0xff2f2f2fu : 0xff8f8f8fu;
            draw.Blend = CanvasBlend.Additive;
            draw.DrawColouredSprite(gx - 10f, 322f, 0.955f, FrontEndTexture.SelectLevelEpisodeCurve, Scale(c, (uint)ea), 1f, 1f,
                SpriteAlign.Centre, SpriteType.None, 1f, 0f);
            draw.Blend = CanvasBlend.Modulate;

            c = episode != _levels[x, 0] / 100 ? 0x3e1f4f7fu : 0xff1f4f7fu;
            if (_frontEnd.NormalFont is { } font)
            {
                draw.DrawTextDynamic(font, CanvasTextureKind.NormalFont, gx + 15f, 179f, 0.345f, 1f, 1f, FrontEndDraw.BlendAlpha2(c, ea),
                    (_levels[x, 0] / 100).ToString(System.Globalization.CultureInfo.InvariantCulture), 1000f, false, 8);
            }
        }
        draw.Blend = CanvasBlend.Modulate;
    }

    /// <summary>Each channel of the colour scaled by a / 256, the alpha kept (retail's inline at 00460c88; name ours).</summary>
    private static uint Scale(uint c, uint a) =>
        ((((((c >> 16) & 0xff) * a / 256 * 256) + (((c >> 8) & 0xff) * a / 256)) * 256) + ((c & 0xff) * a / 256)) + (c & 0xff000000);

    /// <summary>
    /// An unlocked world's mask, drawn into depth only with its edges clamped (its grade's mask once it has a ranking),
    /// then its blue glow scrolling with the counter, and the orange glow while its transition is above 0.
    /// </summary>
    private void DrawWorld(FrontEndDraw draw, int x, int y, float gx, float gy, int alpha)
    {
        uint white = FrontEndDraw.BlendAlpha2(0xffffffff, alpha);
        draw.Blend = CanvasBlend.Keep;
        float ranking = -1f;
        OnslaughtRebuild.Parity.Career.Career career = _frontEnd.Career;
        int node = career.GetNodeFromWorldNo(_levels[x, y]);
        if (node != -1) ranking = career.Record.NodeRanking(node);
        FrontEndTexture mask = FrontEndTexture.LevelSelectMask;
        if (ranking != -1f)
        {
            mask = (char)OnslaughtRebuild.Parity.Career.Career.GradeFromRanking(ranking) switch
            {
                'S' => FrontEndTexture.RankingSmallMaskS,
                'A' => FrontEndTexture.RankingSmallMaskA,
                'B' => FrontEndTexture.RankingSmallMaskB,
                'C' => FrontEndTexture.RankingSmallMaskC,
                'D' => FrontEndTexture.RankingSmallMaskD,
                _ => FrontEndTexture.RankingSmallMaskE,
            };
        }
        draw.DrawColouredSprite(gx, gy, 0.31f, mask, white, 1f, 1f, SpriteAlign.Centre, clamp: true);
        draw.Blend = CanvasBlend.Modulate;

        // x87: the sum and products are kept wide and stored once, as the call's argument.
        float spin = (float)((x * 11 + (double)(y * 7)) * 0.114f + (double)_frontEnd.Counter * 0.02f);
        draw.DrawColouredSprite(gx, gy, 0.335f, FrontEndTexture.LevelSelectGlowBlue, white, 1f, 1f, SpriteAlign.Centre, SpriteType.ScrollRight, spin);
        if (_transition[x, y] > 0f)
        {
            int a = X87.Fist(X87.Mul(FrontEndDraw.RangeTransition(_transition[x, y], 0f, 0.5f), 255f));
            draw.DrawColouredSprite(gx, gy, 0.335f, FrontEndTexture.LevelSelectGlowOrange, FrontEndDraw.BlendAlpha2(0xffffffff, a * alpha / 255),
                1f, 1f, SpriteAlign.Centre, SpriteType.ScrollRight, spin);
        }
    }

    /// <summary>
    /// A world's two ring brackets (faint while locked; turned and grown by its transition), or, for the cursor's world as
    /// the briefing or debriefing arrives, the briefing's two metal rings flying from it to (285, 225); then the lines to
    /// the worlds it opens, bright where both ends are open.
    /// </summary>
    private void DrawRingsAndLinks(FrontEndDraw draw, float transition, FrontEndPage dest, int x, int y, float gx, float gy, int alpha, bool unlocked)
    {
        uint ring = unlocked ? 0xffffffffu : 0x1fffffffu;
        if (transition != 1f && Column == x && ClampedRow() == y && dest is FrontEndPage.Briefing or FrontEndPage.Debriefing)
        {
            if (transition > 0f)
            {
                float inv = 1f - transition;
                float yaw = Pi * 0.67f * transition;
                float sc = inv * 1.5f + 0.1f;
                float px = (285f - gx) * inv + gx;
                float py = (225f - gy) * inv + gy;
                draw.DrawColouredSprite(px, py, 0.305f, FrontEndTexture.MetalRingTransFromLevsel2, ring, sc, sc, SpriteAlign.Centre, SpriteType.None, 1f, -yaw);
                draw.DrawColouredSprite(px, py, 0.305f, FrontEndTexture.MetalRingTransFromLevsel1, ring, sc * 1.75f, sc * 1.75f, SpriteAlign.Centre,
                    SpriteType.None, 1f, yaw);
            }
        }
        else if (alpha > 0)
        {
            uint col = FrontEndDraw.BlendAlpha2(ring, alpha);
            float yaw = _transition[x, y] * Pi * 0.25f;
            float sc = 1f + _transition[x, y] * 0.25f;
            draw.DrawColouredSprite(gx, gy, 0.305f, FrontEndTexture.SelectLevelRingBracket02, col, 1f, 1f, SpriteAlign.Centre, SpriteType.None, 1f, yaw);
            draw.DrawColouredSprite(gx, gy, 0.305f, FrontEndTexture.SelectLevelRingBracket01, col, sc, sc, SpriteAlign.Centre, SpriteType.None, 1f, -yaw);
        }

        foreach ((int lx, int ly) in _links[x, y])
        {
            float ex = (lx - x) * 60f + gx;
            float ey = (float)((_columnLength[lx] - 1) * 60.0 * 0.5 + 320.0 - ly * 60.0);
            uint lc = unlocked && IsWorldUnlocked(_levels[lx, ly]) ? 0xafffffffu : 0x1fffffffu;
            if (alpha > 0) draw.DrawLine(gx, gy, ex, ey, FrontEndDraw.BlendAlpha2(lc, alpha), 10f, 0.308f, 0.4f);
        }
    }

    /// <summary>
    /// The render's last drawing: the small logo (whole while the briefing or debriefing is arriving), then the cursor
    /// world's name (the unknown text while it is locked; narrower when over 450 wide) and its episode's name, sliding in
    /// from the left and revealed from when the cursor and the episode last changed. The locked world's text (which reads
    /// the X key) and the two clock reads are made whether or not anything is drawn, as retail makes them.
    /// </summary>
    private void RenderNames(FrontEndDraw? draw, float transition, FrontEndPage dest, float t4)
    {
        _frontEnd.RenderSmallBEALogo(dest is FrontEndPage.Briefing or FrontEndPage.Debriefing ? 1f : FrontEndDraw.RangeTransition(transition, 0.75f, 1f));
        if (_frontEnd.Texts is not { } texts) return;
        int shownLevel = _levels[Column, ClampedRow()];
        string shownName = IsWorldUnlocked(shownLevel) ? LevelText.LevelName(_frontEnd, texts, shownLevel) : texts.GetText(FrontEndText.GoodyUnknown);
        float cursorAge = _frontEnd.Clock.GetSysTimeFloat() - CursorTime;
        float episodeAge = _frontEnd.Clock.GetSysTimeFloat() - EpisodeTime;
        if (draw is null || _frontEnd.NormalFont is not { } font) return;
        uint ta = (uint)FrontEndDraw.MakeAlpha(t4 - 2f);
        float d = 1f - transition - 0.25f;
        if (d < 0f) d = 0f;
        float off = d * d * 3000f;
        float sx = font.GetTextExtent(shownName).Width > 450f ? 0.8f : 1f;
        float lx = 130f - off;
        draw.DrawTextDynamic(font, CanvasTextureKind.NormalFont, lx, 153f, 0.305f, sx, 1f, (ta << 24) | 0x7f6f3f, shownName, cursorAge);
        draw.DrawTextDynamic(font, CanvasTextureKind.NormalFont, lx, 125f, 0.305f, 1f, 1f, (ta << 24) | 0x7f7f7f,
            LevelText.EpisodeName(_frontEnd, texts, shownLevel / 100), episodeAge);
    }

    private const float Pi = 3.14159265358979f;
}

/// <summary>
/// The level select's and briefing's texts by level number (<c>CFrontEndText::GetLevelName</c>, <c>GetEpisodeName</c>,
/// <c>GetBriefingText</c>), read from the language file by id: none reads the X key as <c>FET.GetText</c> does.
/// </summary>
internal static class LevelText
{
    /// <summary>The levels GetLevelName and GetBriefingTextId name (201 shares 200's texts).</summary>
    private static readonly HashSet<int> Named =
    [
        100, 110, 200, 201, 211, 212, 221, 222, 231, 232, 300, 311, 312, 321, 322, 331, 332, 400, 411, 412, 421, 422, 431,
        432, 500, 511, 512, 521, 522, 523, 524, 600, 611, 612, 621, 622, 700, 710, 720, 731, 732, 741, 742, 800,
    ];

    /// <summary>A text by its name in text.stf.</summary>
    public static string ByName(FrontEnd frontEnd, FrontEndTexts texts, string name) => texts.ById(texts.Ids[name]);

    public static string LevelName(FrontEnd frontEnd, FrontEndTexts texts, int level) =>
        Named.Contains(level) ? ByName(frontEnd, texts, $"FETX_LEVEL_NAME_{(level == 201 ? 200 : level)}") : "Unnamed Level";

    /// <summary><c>CFrontEndText::GetEpisodeName</c> (0x00469c20).</summary>
    public static string EpisodeName(FrontEnd frontEnd, FrontEndTexts texts, int episode) =>
        episode is >= 1 and <= 8 ? ByName(frontEnd, texts, $"FETX_EPISODE_{episode}") : "Unnamed Episode";

    /// <summary>
    /// <c>CFrontEndText::GetBriefingText</c> (0x0046a1f0): <c>GetFollowingString(BRIEFING_level_1, n)</c>, the n-th entry
    /// after the first. Every shipped language file stores each level's nine entries in order, so that entry is
    /// <c>BRIEFING_level_(n + 1)</c>. A level without a briefing (id -1) finds no entry: the pool's first string.
    /// </summary>
    public static string BriefingText(FrontEnd frontEnd, FrontEndTexts texts, int level, int n) =>
        Named.Contains(level) ? ByName(frontEnd, texts, $"BRIEFING_{(level == 201 ? 200 : level)}_{n + 1}") : texts.ById(0xffffffff);

    /// <summary><c>CFrontEndText::GetBriefingTextColour</c> (0x0046a210).</summary>
    public const uint BriefingTextColour = 0xffffdf5f;
}
