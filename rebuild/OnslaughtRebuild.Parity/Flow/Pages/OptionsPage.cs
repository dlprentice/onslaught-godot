// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/FEPOptions.cpp (CFEPOptions: Init 0051f4b0, ButtonPressed 0051f4e0, SaveDefaultOptions
// 0051f500, Process 0051f600, RenderPreCommon 0051f6d0, Render 0051f700, TransitionNotification 0051f7e0; reconstructed from the retail program, not in Stuart's
// release).

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Flow.Menus;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary>
/// The PC options page (<c>CFEPOptions</c>): a <see cref="PauseMenu"/> in its options form. When the menu closes the page
/// saves: the career to its slot when there is one (through the save page), and always the whole save image to
/// defaultoptions.bea.
/// </summary>
public sealed class OptionsPage(FrontEnd frontEnd) : FrontEndPageBase
{
    /// <summary>gOptionsMenu (0089bc30).</summary>
    public PauseMenu? Menu { get; private set; }

    /// <summary>mState.</summary>
    public int State { get; private set; }

    public float StartTime { get; private set; }

    /// <summary>Init (0051f4b0).</summary>
    public override bool Init()
    {
        State = 0;
        return true;
    }

    /// <summary>Shutdown (0051f4c0).</summary>
    public override void Shutdown() => Menu = null;

    /// <summary>ButtonPressed (0051f4e0): everything to the menu.</summary>
    public override void ButtonPressed(int button, float value) => Menu?.ReceiveButtonAction(button, value);

    /// <summary>IsStateAvailable: states 0, 2, 3 and 5 never; 4 with two controllers.</summary>
    private static bool IsStateAvailable(int state) => state switch
    {
        0 or 2 or 3 or 5 => false,
        4 => FrontEnd.ControllerPorts >= 2,
        _ => true,
    };

    /// <summary>Process (0051f600): once the menu closes on the active page, the credits or the save.</summary>
    public override void Process(FrontEndPageState state)
    {
        while (!IsStateAvailable(State))
        {
            State++;
        }

        if (state == FrontEndPageState.Active && Menu is { Active: false } menu)
        {
            if (menu.Result == 1)
            {
                frontEnd.SetPage(FrontEndPage.Credits, 30);
            }
            else
            {
                SaveDefaultOptions(true);
            }
        }
    }

    /// <summary>
    /// SaveDefaultOptions (0051f500): with a save name and normal autosaves, the save page writes the career to its slot
    /// (with its own success text) and returns to the main page; otherwise straight to the main page. Then CAREER.Save's
    /// image goes to defaultoptions.bea.
    /// </summary>
    public void SaveDefaultOptions(bool fromOptions)
    {
        if (frontEnd.SaveGameName.Length != 0 && frontEnd.AutoSave == AutoSaveMode.Normal)
        {
            frontEnd.SaveMode = true;
            frontEnd.SetPage(FrontEndPage.SaveGame, 0);
            frontEnd.SetSuccessPage(FrontEndPage.Main, 70);
            if (frontEnd[FrontEndPage.SaveGame] is SaveGamePage save)
            {
                save.SaveFromOptions = fromOptions;
            }
        }
        else
        {
            frontEnd.SetPage(FrontEndPage.Main, 70);
        }

        CareerOptions options = frontEnd.CurrentOptions?.Invoke() ?? CareerOptions.Read(new byte[CareerOptions.Size]);
        byte[] image = CareerSaveGame.Save(frontEnd.Career, frontEnd.Controls.Bindings.SaveConfigurable(), options);
        frontEnd.WriteDefaultOptionsFile?.Invoke(image);
    }

    /// <summary>TransitionNotification (0051f7e0): from the main page the state restarts; the menu opens without a fade.</summary>
    public override void TransitionNotification(FrontEndPage from)
    {
        if (from is FrontEndPage.DemoMain or FrontEndPage.Main)
        {
            State = 0;
        }

        StartTime = (float)Numerics.X87.Add(frontEnd.Clock.GetSysTimeFloat(), 2.0f);
        Menu ??= new PauseMenu(frontEnd, optionsPage: true);
        Menu.Activate(false);
    }

    /// <summary>RenderPreCommon (0051f6d0): the standard video background, settled when leaving for the credits or screen position.</summary>
    public override void RenderPreCommon(float transition, FrontEndPage dest)
    {
        if (dest is FrontEndPage.Credits or FrontEndPage.ScreenPos) transition = 1f;
        frontEnd.DrawStandardVideoBackground(transition, 0x3fffffff, dest);
    }

    /// <summary>
    /// Render (0051f700): the borders, the menu (with the front end's mouse), the title bar with the shown list's title
    /// or text 0x265233 (TEXT.GetStringById: no X-key read), the help line's back arrow and the small logo.
    /// </summary>
    public override void Render(float transition, FrontEndPage dest)
    {
        frontEnd.DrawSlidingTextBordersAndMask(transition, dest);
        MenuDrawing? drawing = MenuDrawing.Of(frontEnd);
        MenuTitle? title = Menu?.Render(drawing, new FrontEndMenuMouse(frontEnd));
        // The title is drawing only (no X-key read): resolved only when drawn.
        if (drawing is { Draw: not null })
            frontEnd.DrawTitleBar(title is { } t ? t.Resolve(drawing.Texts) : drawing.Texts.ById(0x265233), transition, dest);
        frontEnd.RenderHelpArrow(1, transition);
        transition = dest is FrontEndPage.Credits or FrontEndPage.ScreenPos ? 1f : Draw.FrontEndDraw.RangeTransition(transition, 0.75f, 1f);
        frontEnd.RenderSmallBEALogo(transition);
    }
}
