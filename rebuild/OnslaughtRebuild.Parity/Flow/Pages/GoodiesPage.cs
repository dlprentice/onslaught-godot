// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/FEPGoodies.cpp (CFEPGoodies: get_goodie_number 0045cb80, get_goodie_state,
// set_goodie_state, ResetGoodyState, Init 0045c7a0, ButtonPressed 0045cde0, Process 0045d7e0), in Stuart's release with the
// PC build's changes.

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Flow.Draw;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Platform;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary>
/// The goodies page (<c>CFEPGoodies</c>): a grid of the career's goodies, four rows (biographies, races and the
/// developers' items; units; films; concept art), the cursor moved with the menu buttons. Selecting a goodie the career
/// has (new or old) opens its viewer and marks it old; Back saves the options and leaves. What decides the simulation is
/// here: the cursor, the sounds (each a C runtime draw), the career's goodie states and the save on the way out. The
/// A diagnostic caller can supply a parsed mesh-resource and the completion of its load for goodie 8; timing is an
/// explicit input because the PC loader is asynchronous. Mesh drawing, its camera effects, other viewer kinds and
/// manual mesh controls are not ported.
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

    /// <summary>mSelectFade and mXPos; both affect the rendered grid's mouse handling and selection.</summary>
    public float SelectFade { get; private set; }
    public float XPos { get; private set; }

    /// <summary>The loaded mesh, null while loading or after resources are freed.</summary>
    public GoodieMeshResource? MeshResource { get; private set; }

    /// <summary>
    /// An explicit diagnostic input: start the PC's asynchronous mesh load. The default is unsupported, including on
    /// interactive hosts; supplying this does not supply texture rendering. Ready must follow the caller's observed or
    /// authored schedule, not the machine's wall clock.
    /// </summary>
    public Func<int, IGoodieMeshLoad>? StartMeshLoad { get; set; }
    private IGoodieMeshLoad? _meshLoad;
    private bool _meshLoading;
    private float _lastProcessTime;
    private float _textScroll;

    /// <summary>mCurrentYaw, useful to the diagnostic viewer; no visual equivalence is asserted.</summary>
    public float Yaw { get; private set; }

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
        SelectFade = 1f;
        ImageZoom = 0f;
        DisplayGoody = false;
        _meshLoading = false;
        _lastProcessTime = 0f;
        _textScroll = -50f;
        Yaw = 0f;
    }

    /// <summary>Init (0045c7a0): the cursor home, the viewer closed, the selection timer read.</summary>
    public override bool Init()
    {
        Column = 0;
        Row = 0;
        XPos = 0f;
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
                    if (GoodieX(Column) < 90f) XPos -= 45f;
                    break;
                case FrontEndButtons.MenuRight:
                    Column = Math.Min(Column + 1, Columns - 1);
                    while (GoodieNumber(Column, Row) == -1) Column--;
                    frontEnd.PlaySound(FrontEndSound.Move);
                    if (GoodieX(Column) > 570f) XPos += 45f;
                    break;
                case FrontEndButtons.MenuSelect:
                    frontEnd.PlaySound(FrontEndSound.Select);
                    ResetGoodyState();
                    if (GoodieState(Column, Row) is CareerGoodieState.New or CareerGoodieState.Old)
                    {
                        int number = GoodieNumber(Column, Row);
                        if (number != 8 || StartMeshLoad is null)
                            throw new NotSupportedException($"the goodie viewer (goodie {number}) is not ported");
                        DisplayGoody = true;
                        _meshLoad = StartMeshLoad(number) ?? throw new InvalidOperationException("the mesh loader returned no request");
                        _meshLoading = true;
                        frontEnd.Career.Record.SetGoodieState(number, CareerGoodieState.Old);
                    }

                    break;
                case FrontEndButtons.MenuBack:
                    frontEnd.PlaySound(FrontEndSound.Back);
                    FreeMesh();
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
        else if (ImageZoom >= 1f && button is 64 or Buttons.CameraPitchUp or Buttons.CameraPitchDown or
                 Buttons.CameraMoveForward or Buttons.CameraMoveBackwards or Buttons.CameraStrafeLeft or Buttons.CameraStrafeRight)
        {
            throw new NotSupportedException("manual goodie mesh controls are not ported");
        }
    }

    /// <summary>TransitionNotification (0045ffa0): the start time read; the viewer closed.</summary>
    public override void TransitionNotification(FrontEndPage from)
    {
        frontEnd.Clock.GetSysTimeFloat(); // mStartTime
        ResetGoodyState();
    }

    /// <summary>
    /// Process (0045d7e0): a supplied mesh load is polled, the viewer fades and the mesh is freed after it closes.
    /// Grid edge-slide and the active page's right-mouse back also change input-visible state.
    /// </summary>
    public override void Process(FrontEndPageState state)
    {
        if (DisplayGoody)
        {
            if (_meshLoading && _meshLoad is { Ready: true } load)
            {
                frontEnd.Clock.GetSysTimeFloat(); // LoadingGoodyPoll start
                frontEnd.Clock.GetSysTimeFloat(); // ReadResources start
                GoodieMeshResource resource = load.Read();
                if (resource.Number != GoodieNumber(Column, Row)) throw new InvalidDataException("loader returned a different goodie");
                frontEnd.Clock.GetSysTimeFloat(); // ReadResources end
                MeshResource = resource;
                _meshLoading = false;
                frontEnd.Clock.GetSysTimeFloat(); // LoadingGoodyPoll end
            }
            SelectFade = SmoothResize(SelectFade, 0f, .1f, .001f);
            if (MeshResource is not null)
            {
                if (ImageZoom < 1f) ImageZoom = SmoothResize(ImageZoom, 1f, .3f, .005f);
                float now = frontEnd.Clock.GetSysTimeFloat();
                double delta = X87.Sub(now, _lastProcessTime);
                _lastProcessTime = now;
                Yaw = (float)X87.Add(Yaw, X87.Mul(8f, delta));
                _textScroll = (float)X87.Add(_textScroll, X87.Div(frontEnd.Timing.FrameStep, .1f));
                // Text height is set by the unported renderer and is non-negative. Before this threshold its value
                // cannot affect the reset decision; past it the caller must not infer a text-scroll result.
                if (_textScroll > 10f) throw new NotSupportedException("goodie text height is not ported");
                if (ImageZoom >= 1f && frontEnd.Platform.Mouse.Held[0])
                    throw new NotSupportedException("manual goodie mesh controls are not ported");
            }
        }
        else
        {
            SelectFade = SmoothResize(SelectFade, 1f, .1f, .001f);
            ImageZoom = SmoothResize(ImageZoom, 0f, .4f, .005f);
            if (SelectFade == 1f || ImageZoom == 0f)
            {
                FreeMesh();
                XPos = MouseEdgeSlide.Apply(frontEnd, state, XPos, 5100f);
            }
        }

        if (state == FrontEndPageState.Active && frontEnd.Platform.Mouse.Buttons[2].Down)
        {
            frontEnd.Platform.Mouse.Buttons[2].Down = false;
            ButtonPressed(FrontEndButtons.MenuBack, 1f);
        }
    }

    public override void Shutdown() => FreeMesh();

    private void FreeMesh()
    {
        // Retail's cache can still own a worker/buffer after the viewer is closed; cancellation and reuse of that
        // asynchronous request have not been modelled. Do not turn an unfinished request into a clean release.
        if (_meshLoading) throw new NotSupportedException("closing a pending goodie mesh load is not ported");
        _meshLoad?.Release();
        _meshLoad = null;
        _meshLoading = false;
        MeshResource = null;
    }

    private static float SmoothResize(float value, float target, float factor, float epsilon)
    {
        value = (float)X87.Add(value, X87.Mul(X87.Sub(target, value), factor));
        return Math.Abs(X87.Sub(target, value)) < epsilon ? target : value;
    }

    private float GoodieX(int column) => 148f - XPos + column * 45f;

    /// <summary>
    /// Render's stateful grid selection, mouse hover/click, and clock reads; the visual sprites and mesh are unported.
    /// </summary>
    public override void Render(float transition, FrontEndPage dest)
    {
        if (SelectFade > 0f)
        {
            int alpha = Math.Min(FrontEndDraw.MakeAlpha(SelectFade),
                X87.Fist(X87.Mul(FrontEndDraw.RangeTransition(transition, .75f, 1f), 255f)));
            for (int x = 0; x < Columns; x++)
            {
                float cx = GoodieX(x);
                if (cx > 620f) break;
                if (cx < 10f) continue;
                if (cx > 570f && Column >= x) Column = x - 1;
                else if (cx < 90f && Column <= x)
                {
                    Column = x + 1;
                    while (GoodieNumber(Column, Row) == -1) Row = (Row + 1) & 3;
                }
                if (alpha <= 0) continue;
                for (int y = 0; y < Rows; y++)
                {
                    if (GoodieNumber(x, y) == -1) continue;
                    float cy = 252.5f + y * 45f;
                    if (!DisplayGoody && cx >= 90f && cx <= 570f)
                    {
                        if (frontEnd.GetCursorStateInRect(cx - 22.5f, cy - 20f, cx + 22.5f, cy + 20f))
                            (Column, Row) = (x, y);
                        if (frontEnd.MouseClickedInRect(cx - 22.5f, cy - 20f, cx + 22.5f, cy + 20f))
                        {
                            (Column, Row) = (x, y);
                            ButtonPressed(FrontEndButtons.MenuSelect, 1f);
                        }
                    }
                    if (GoodieState(x, y) == CareerGoodieState.New) frontEnd.Clock.GetSysTimeFloat();
                }
            }
        }
        if (GoodieState(Column, Row) != CareerGoodieState.Unknown)
        {
            frontEnd.Clock.GetSysTimeFloat();
            frontEnd.Clock.GetSysTimeFloat();
        }
        if (MeshResource is not null && ImageZoom >= .8f) frontEnd.Clock.GetSysTimeFloat();
        frontEnd.RenderHelpArrow(1, transition);
    }
}

/// <summary>A diagnostic mesh load: Ready is a deterministic external input; Read supplies the parsed resource.</summary>
public interface IGoodieMeshLoad
{
    bool Ready { get; }
    GoodieMeshResource Read();
    void Release();
}
