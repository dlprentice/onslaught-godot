// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/FEPGoodies.cpp (CFEPGoodies: get_goodie_number 0045cb80, get_goodie_state,
// set_goodie_state, ResetGoodyState, Init 0045c7a0, ButtonPressed 0045cde0, Process 0045d7e0), in Stuart's release with the
// PC build's changes.

using OnslaughtRebuild.Parity.Career;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary>
/// The goodies page (<c>CFEPGoodies</c>): a grid of the career's goodies, four rows (biographies, races and the
/// developers' items; units; films; concept art), the cursor moved with the menu buttons. Selecting a goodie the career
/// has (new or old) opens its viewer and marks it old; Back saves the options and leaves. What decides the simulation is
/// here: the cursor, the sounds (each a C runtime draw), the career's goodie states and the save on the way out. The
/// grid's scroll position and fades are drawing only; the viewer (its loading, meshes, images and films) is not ported.
/// </summary>
public sealed class GoodiesPage(FrontEnd frontEnd) : FrontEndPageBase
{
    /// <summary>NUM_GOODIES_Y: the rows.</summary>
    public const int Rows = 4;

    /// <summary>NUM_GOODIES_X_INC: the columns (123 complete; 300 goodies leave no remainder in four rows).</summary>
    public const int Columns = 123;

    /// <summary>mCX.</summary>
    public int Column { get; private set; }

    /// <summary>mCY.</summary>
    public int Row { get; private set; }

    /// <summary>mDisplayGoody: the viewer is open.</summary>
    public bool DisplayGoody { get; private set; }

    /// <summary>mImageZoom: the viewer's zoom (0 closed); the grid takes buttons only at 0 with no goodie shown.</summary>
    public float ImageZoom { get; private set; }

    /// <summary>
    /// get_goodie_number: the goodie at column <paramref name="x"/> of row <paramref name="y"/>, or -1. Row 0: the
    /// biographies 0-7, the races 66-70, the developers' items 74-77; row 1: the units 8-65; row 2: the films 201-232;
    /// row 3: the concept art 78-200.
    /// </summary>
    public static int GoodieNumber(int x, int y)
    {
        switch (y)
        {
            case 0:
                if (x < 8) return x;
                if (x < 13) return x - 8 + 66;
                return x switch { 13 => 74, 14 => 75, 15 => 76, 16 => 77, _ => -1 };
            case 1:
                return x < 58 ? x + 8 : -1;
            case 2:
                return x < 32 ? x + 201 : -1;
            case 3:
                return x < 123 ? x + 78 : -1;
            default:
                return -1;
        }
    }

    /// <summary>get_goodie_state: unknown off the grid; the PC's cheat 5 shows instructions, cheat 0 everything old.</summary>
    private CareerGoodieState GoodieState(int x, int y)
    {
        int number = GoodieNumber(x, y);
        if (number == -1) return CareerGoodieState.Unknown;
        if (SaveGamePage.CheatActive(frontEnd.SaveGameName, frontEnd.AllCheatsEnabled, 5)) return CareerGoodieState.Instructions;
        if (SaveGamePage.CheatActive(frontEnd.SaveGameName, frontEnd.AllCheatsEnabled, 0)) return CareerGoodieState.Old;
        return frontEnd.Career.Record.GoodieState(number);
    }

    /// <summary>ResetGoodyState: the viewer closed (its fades, zoom, mesh and text state are drawing only).</summary>
    private void ResetGoodyState()
    {
        ImageZoom = 0f;
        DisplayGoody = false;
    }

    /// <summary>Init (0045c7a0): the cursor home, the viewer closed, the selection timer read.</summary>
    public override bool Init()
    {
        Column = 0;
        Row = 0;
        ResetGoodyState();
        frontEnd.Clock.GetSysTimeFloat(); // mTimerGoody
        return true;
    }

    /// <summary>ButtonPressed (0045cde0).</summary>
    public override void ButtonPressed(int button, float value)
    {
        if (!DisplayGoody && ImageZoom == 0f)
        {
            int oldColumn = Column, oldRow = Row;
            switch (button)
            {
                case FrontEndButtons.MenuUp:
                {
                    int ny = Row - 1;
                    while (ny >= 0 && GoodieNumber(Column, ny) == -1) ny--;
                    if (ny != -1) Row = ny;
                    frontEnd.PlaySound(FrontEndSound.Move);
                    break;
                }
                case FrontEndButtons.MenuDown:
                {
                    int ny = Row + 1;
                    while (ny < Rows && GoodieNumber(Column, ny) == -1) ny++;
                    if (ny != Rows) Row = ny;
                    frontEnd.PlaySound(FrontEndSound.Move);
                    break;
                }
                case FrontEndButtons.MenuLeft:
                    Column = Math.Max(Column - 1, 0);
                    frontEnd.PlaySound(FrontEndSound.Move);
                    break;
                case FrontEndButtons.MenuRight:
                    Column = Math.Min(Column + 1, Columns - 1);
                    while (GoodieNumber(Column, Row) == -1) Column--;
                    frontEnd.PlaySound(FrontEndSound.Move);
                    break;
                case FrontEndButtons.MenuSelect:
                    frontEnd.PlaySound(FrontEndSound.Select);
                    ResetGoodyState();
                    if (GoodieState(Column, Row) is CareerGoodieState.New or CareerGoodieState.Old)
                    {
                        // StartLoadingGoody and the viewer, then set_goodie_state(GS_OLD).
                        throw new NotSupportedException($"the goodie viewer (goodie {GoodieNumber(Column, Row)}) is not ported");
                    }

                    break;
                case FrontEndButtons.MenuBack:
                    frontEnd.PlaySound(FrontEndSound.Back);
                    // FreeUpGoodyResources: nothing loaded. CFEPOptions::SaveDefaultOptions(false).
                    (frontEnd[FrontEndPage.Options] as OptionsPage ?? throw new InvalidOperationException("the options page is not registered"))
                        .SaveDefaultOptions(false);
                    break;
            }

            if (Column != oldColumn || Row != oldRow)
            {
                frontEnd.Clock.GetSysTimeFloat(); // mTimerGoody
            }
        }
        else if (button == FrontEndButtons.MenuBack)
        {
            frontEnd.PlaySound(FrontEndSound.Back);
            DisplayGoody = false;
        }
    }

    /// <summary>TransitionNotification (0045ffa0): the start time read; the viewer closed.</summary>
    public override void TransitionNotification(FrontEndPage from)
    {
        frontEnd.Clock.GetSysTimeFloat(); // mStartTime
        ResetGoodyState();
    }

    /// <summary>
    /// Process (0045d7e0) with the viewer closed: the zoom eases to 0 (drawing), resources freed, the mouse's edge slide
    /// (drawing: the scroll position); the right mouse button goes back on the active page.
    /// </summary>
    public override void Process(FrontEndPageState state)
    {
        if (DisplayGoody)
        {
            throw new NotSupportedException("the goodie viewer is not ported");
        }

        if (state == FrontEndPageState.Active && frontEnd.Platform.Mouse.Buttons[2].Down)
        {
            frontEnd.Platform.Mouse.Buttons[2].Down = false;
            ButtonPressed(FrontEndButtons.MenuBack, 1f);
        }
    }
}
