// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/FEPLevelSelect.cpp and src/FEPLevelSelect.h (reconstructed from the retail program; not
// in Stuart's release), with FEMouseEdgeSlide from src/FEPGoodies.cpp.

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary>
/// The level select (<c>CFEPLevelSelect</c>): the campaign's worlds as a grid of columns (a new column at each world
/// ending in 0 or 1), scrolled sideways. Select on an unlocked world goes to its briefing; the mouse picks a world by
/// hovering and clicking, and scrolls the grid at the screen's edges.
/// </summary>
/// <remarks>
/// Not modelled: the auto-configuration test's two automatic selections (the switch is off), and the render's drawing
/// (grades, links, glows, names). Its two clock reads (the name and episode text's ages) are drawing inputs.
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
        // The links between levels (mLinks) are drawn, never read, so they are left out.
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
        Pos = MouseEdgeSlide(state, Pos, 1100f);
    }

    /// <summary>
    /// <c>FEMouseEdgeSlide</c> (0x0045d730): with the cursor above y 400 and right of x 540 (or left of x 100), the
    /// position moves by the cube of the distance; nothing while the message box is interactive or the page is not active.
    /// </summary>
    private float MouseEdgeSlide(FrontEndPageState state, float pos, float max)
    {
        if (_frontEnd.MessageBox.IsInteractive || state != FrontEndPageState.Active) return pos;
        Platform.Mouse mouse = _frontEnd.Platform.Mouse;
        float speed = 0f;
        if (mouse.X > 540 && mouse.Y < 400) speed = mouse.X - 540;
        else if (mouse.X < 100 && mouse.Y < 400) speed = mouse.X - 100;
        float step = _frontEnd.Timing.FrameStep;
        // x87: left to right, each product rounded to 24 bits, then the sum stored.
        pos = (float)X87.Add(pos, X87.Mul(X87.Mul(X87.Mul(X87.Mul(step, speed), speed), speed), 0.002f));
        if (pos < 0f) pos = 0f;
        if (pos > max) pos = max;
        return pos;
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

    /// <summary>
    /// The state-changing part of <c>CFEPLevelSelect::Render</c> (0x00460b40): walking the columns from x 148 - pos in
    /// steps of 60, a cursor column that has left the scroll band moves back into it; on screen, a click on an unlocked
    /// world (once the transition is complete) selects it, and hovering over one moves the cursor to it. Then the help
    /// line's select and back arrows.
    /// </summary>
    public override void Render(float transition, FrontEndPage dest)
    {
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
                    }
                    bool unlocked = IsWorldUnlocked(_levels[x, y]);
                    if (onscreen && _frontEnd.GetCursorStateInRect(left, top, right, bottom) && unlocked)
                    {
                        Column = x;
                        Row = y;
                    }
                    gy = (float)X87.Sub(gy, 60f);
                }
            }
            gx = (float)X87.Add(gx, 60f);
        }
        _frontEnd.RenderHelpArrow(5, transition);
        _frontEnd.RenderHelpArrow(1, transition);
    }
}
