// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>The keys <c>RetailFrontendFlow.HandleKey</c> routes.</summary>
internal enum RetailFrontendKey
{
    Up,
    Down,
    Left,
    Right,
    Enter,
    Space,
    Escape,
    Backspace,
}

/// <summary>
/// A headless stand-in for the Godot node that owns the released frontend, so
/// the page flow can be driven by keystrokes and clicks without an engine.
///
/// <para><b>Read this before citing a run that used it.</b> The page graph it
/// drives is the product's own — <see cref="RetailFrontendSession"/>, which is
/// plain C# in <c>OnslaughtRebuild.Client</c> and carries every page decision.
/// What is TRANSCRIBED here, and therefore is a second copy, is the BINDING
/// LAYER: which key and which 640x480 design-stage rectangle reaches which
/// session call. That layer lives in
/// <c>rebuild/OnslaughtRebuild.Godot/RetailFrontendFlow.cs</c> as private
/// methods on a <c>Godot.Control</c>, and it cannot be executed without an
/// engine: <c>_Input</c> pattern-matches <c>InputEvent</c> subclasses (:816-824),
/// every hit test goes through <c>ToDesignPosition</c> which reads
/// <c>Control.Size</c> (:2910-2914), <c>Confirm</c> calls
/// <c>GetTree().Quit</c> (:2449), and reaching any of it at all requires
/// <c>Initialize()</c> to have loaded ~24 <c>Texture2D</c> fields (:557-571).</para>
///
/// <para><b>Nothing in the repository exercises that layer today.</b> The
/// native smoke and the frontend capture rig both drive the flow through
/// <c>RetailFrontendFlow.ConfirmForSmoke()</c> (:602-605), the internal escape
/// hatch that calls the private <c>Confirm()</c> directly, so no test
/// synthesizes an <c>InputEvent</c> and no key binding or hit rectangle in this
/// file has ever been checked against the code it was copied from by anything
/// but a human reading both. Each constant below carries the line it came
/// from.</para>
/// </summary>
internal sealed class RetailFrontendHarness
{
    /// <summary>
    /// The reveal that runs when click-to-start hands over to the main menu.
    /// <c>RetailFrontendFlow.MainMenuEntryTransitionFrames</c> (:480) is 50, and
    /// <c>_Input</c> drops every event while it is running (:811-814).
    /// </summary>
    internal const int MainMenuEntryTransitionFrames = 50;

    // RetailFrontendFlow.cs:20-23.
    private const double MenuColumnX = 219d;
    private const double MenuStartY = 304d;
    private const double MenuPitch = 20d;
    private const double MenuHitHalfWidth = 120d;

    private readonly RetailFrontendSession _session = new();

    private RetailFrontendScreen _lastDrawnScreen = RetailFrontendScreen.ClickToStart;
    private int _mainTransitionTime;
    private int _mainTransitionCount;
    private int _loadingFrames;
    private bool _loadRequestRaised;
    private bool _level100Ready;

    /// <summary>Raised where <c>RetailFrontendFlow</c> raises
    /// <c>Level100LoadRequested</c> (:736).</summary>
    internal event Action? Level100LoadRequested;

    internal RetailFrontendScreen Screen => _session.Screen;

    internal int SelectedMainIndex => _session.SelectedMainIndex;

    internal IReadOnlyList<RetailFrontendMenuItem> Items => _session.Items;

    /// <summary><c>RetailFrontendFlow.MarkLevel100Ready</c> (:573-582).</summary>
    internal void MarkLevel100Ready() => _level100Ready = true;

    /// <summary>
    /// One frontend frame. Mirrors the two things
    /// <c>RetailFrontendFlow._Process</c> does that the input path depends on:
    /// the main-menu entry reveal (:676-698) and the Loading page's two-frame
    /// gate on the load request (:725-743).
    /// </summary>
    internal void Process()
    {
        if (_session.Screen == RetailFrontendScreen.MainMenu &&
            _lastDrawnScreen == RetailFrontendScreen.ClickToStart)
        {
            _mainTransitionCount = 0;
            _mainTransitionTime = MainMenuEntryTransitionFrames;
        }

        if (_mainTransitionTime > 0)
        {
            _mainTransitionCount++;
            if (_mainTransitionCount >= _mainTransitionTime)
            {
                _mainTransitionTime = 0;
            }
        }

        if (_session.Screen == RetailFrontendScreen.Loading)
        {
            _loadingFrames++;
            if (!_loadRequestRaised && _loadingFrames >= 2)
            {
                if (!_session.ConsumeLevel100LaunchRequest())
                {
                    throw new InvalidOperationException(
                        "The Level 100 launch edge was lost.");
                }

                _loadRequestRaised = true;
                Level100LoadRequested?.Invoke();
            }

            if (_level100Ready)
            {
                _session.CompleteLevel100Load();
            }
        }

        _lastDrawnScreen = _session.Screen;
    }

    /// <summary><c>RetailFrontendFlow.HandleKey</c> (:2376-2435), behind the two
    /// <c>_Input</c> gates at :798-801 and :811-814.</summary>
    internal bool SendKey(RetailFrontendKey key)
    {
        if (!AcceptsInput())
        {
            return false;
        }

        switch (key)
        {
            case RetailFrontendKey.Backspace:
                return _session.Screen == RetailFrontendScreen.DevSelect &&
                    _session.RemoveGameNameCharacter();

            case RetailFrontendKey.Up:
            case RetailFrontendKey.Left:
                _session.MovePrevious();
                return true;

            case RetailFrontendKey.Down:
            case RetailFrontendKey.Right:
                _session.MoveNext();
                return true;

            case RetailFrontendKey.Enter:
            case RetailFrontendKey.Space:
                Confirm();
                return true;

            case RetailFrontendKey.Escape:
                RetailFrontendSignal back = _session.Back();
                HandleNavigationSignal(back);
                return back != RetailFrontendSignal.None;

            default:
                return false;
        }
    }

    /// <summary>One printable character into the FEP_DEVSELECT name field
    /// (<c>RetailFrontendFlow</c> :2391-2396).</summary>
    internal bool SendCharacter(char character) =>
        AcceptsInput() &&
        _session.Screen == RetailFrontendScreen.DevSelect &&
        _session.AppendGameNameCharacter(character, 0); // Explicit unmeasured short-name test extent.

    /// <summary>
    /// A left mouse button press at a 640x480 design-stage point.
    /// <c>RetailFrontendFlow.HandlePointerConfirm</c> (:2269-2374).
    /// </summary>
    internal bool SendLeftClick(double x, double y)
    {
        if (!AcceptsInput())
        {
            return false;
        }

        switch (_session.Screen)
        {
            case RetailFrontendScreen.ClickToStart:
                // :2274-2276 - a click anywhere on the page confirms. Retail's
                // own handler at 0x0051B6B0 accepts full-window mouse input.
                Confirm();
                return true;

            case RetailFrontendScreen.MainMenu:
                int index = MainMenuIndexAt(x, y);
                if (index < 0 || !_session.Items[index].IsAvailable)
                {
                    return false;
                }

                _session.SelectMainIndex(index);
                Confirm();
                return true;

            case RetailFrontendScreen.QuitConfirm:
                int choice = QuitConfirmIndexAt(x, y);
                if (choice < 0)
                {
                    return false;
                }

                _session.SelectQuitConfirmIndex(choice);
                Confirm();
                return true;

            case RetailFrontendScreen.DevSelect:
                // :2304-2324 - back chevron, forward chevron, or the name field.
                if (HasPoint(0, 430, 46, 48, x, y))
                {
                    return Back();
                }

                if (HasPoint(595, 430, 45, 48, x, y) ||
                    HasPoint(128, 408, 403, 44, x, y))
                {
                    Confirm();
                    return true;
                }

                return false;

            case RetailFrontendScreen.LevelSelect:
                // :2326-2346. The second rectangle is the ring drawn on
                // LevelNodes[0], which is the only level node this page
                // hit-tests at all.
                if (HasPoint(0, 430, 48, 48, x, y))
                {
                    return Back();
                }

                if (HasPoint(595, 430, 45, 48, x, y))
                {
                    Confirm();
                    return true;
                }

                if (HasPoint(120, 265, 60, 60, x, y))
                {
                    _ = _session.SelectWorld(100);
                    Confirm();
                    return true;
                }

                if (HasPoint(180, 265, 60, 60, x, y))
                {
                    if (!_session.SelectWorld(110))
                    {
                        return false;
                    }

                    Confirm();
                    return true;
                }

                return false;

            case RetailFrontendScreen.MissionBriefing:
            case RetailFrontendScreen.SelectConfiguration:
                // :2348-2369 - both pages share one case; only the chevrons.
                if (HasPoint(0, 430, 48, 48, x, y))
                {
                    return Back();
                }

                if (HasPoint(595, 430, 45, 48, x, y))
                {
                    Confirm();
                    return true;
                }

                return false;

            default:
                return false;
        }
    }

    /// <summary>
    /// The design-stage point of a main-menu row, for a click that has to land
    /// on a named entry rather than on a coordinate someone typed.
    /// </summary>
    internal static (double X, double Y) MainMenuRowPoint(int index) =>
        (MenuColumnX, MenuStartY + (MenuPitch * index));

    /// <summary>The centre of the level node the LevelSelect page hit-tests.</summary>
    internal static (double X, double Y) LevelNodePoint() => (150d, 295d);

    private bool AcceptsInput() =>
        _session.Screen is not (RetailFrontendScreen.Loading or RetailFrontendScreen.Gameplay) &&
        _mainTransitionTime <= 0;

    private void Confirm() => HandleNavigationSignal(_session.Confirm());

    private bool Back()
    {
        RetailFrontendSignal signal = _session.Back();
        HandleNavigationSignal(signal);
        return signal != RetailFrontendSignal.None;
    }

    /// <summary><c>RetailFrontendFlow.HandleNavigationSignal</c> (:2466-2475).</summary>
    private void HandleNavigationSignal(RetailFrontendSignal signal)
    {
        if (signal != RetailFrontendSignal.LevelLaunchRequested)
        {
            return;
        }

        _loadRequestRaised = false;
        _level100Ready = false;
        _loadingFrames = 0;
    }

    private int MainMenuIndexAt(double x, double y)
    {
        if (!HasPoint(
                MenuColumnX - MenuHitHalfWidth,
                MenuStartY - (MenuPitch * 0.5d),
                MenuHitHalfWidth * 2d,
                MenuPitch * _session.Items.Count,
                x,
                y))
        {
            return -1;
        }

        int index = (int)((y - MenuStartY + (MenuPitch * 0.5d)) / MenuPitch);
        return Math.Clamp(index, 0, _session.Items.Count - 1);
    }

    private static int QuitConfirmIndexAt(double x, double y)
    {
        if (HasPoint(160, 240, 120, 36, x, y))
        {
            return 0;
        }

        return HasPoint(360, 240, 120, 36, x, y) ? 1 : -1;
    }

    /// <summary>Godot's <c>Rect2.HasPoint</c>: closed at the origin, open at the
    /// far edge.</summary>
    private static bool HasPoint(
        double left,
        double top,
        double width,
        double height,
        double x,
        double y) =>
        x >= left && x < left + width && y >= top && y < top + height;
}
