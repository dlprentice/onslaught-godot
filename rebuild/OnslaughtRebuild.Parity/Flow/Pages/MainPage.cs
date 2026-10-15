// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/FEPMain.cpp (reconstructed from the retail program; not in Stuart's release).

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Crt;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary>
/// The main menu (<c>CFEPMain</c>): the language row (-1), then New Game (0), Continue (1), Load (2), Multiplayer (3),
/// Goodies (4), Options (5) and Quit (6); Save (8) is never reachable by navigation. Selecting an entry starts a new
/// career, opens a page or asks a question in the message box.
/// </summary>
public sealed class MainPage(FrontEnd frontEnd) : FrontEndPageBase
{
    private const int NumActions = 7;

    public int Selected { get; set; }
    public int CursorTarget { get; private set; }
    public float CursorPos { get; private set; }
    public float SelectFlash { get; private set; } = -1f;
    public float LeftArrowFlash { get; private set; }
    public float RightArrowFlash { get; private set; }
    public float IdleTime { get; private set; }
    public float StartTime { get; private set; }

    /// <summary>The front end's American text (<c>gAmericanText</c>): the language row is then not offered.</summary>
    public bool AmericanText { get; set; }

    /// <summary>
    /// The language whose sample bank is loaded. Retail asks <c>SOUND.LanguageChanged(FALSE)</c>; with the sound system
    /// not modelled, this compares the loaded language with the front end's (an approximation of that call).
    /// </summary>
    public int LoadedSoundLanguage { get; set; }

    /// <summary>Called when the language box has shown: the game saves the career and the options file then (see <see cref="Process"/>).</summary>
    public Action? SaveAfterLanguageChange { get; set; }

    /// <summary><c>CFEPMain::Init</c> (0x004621b0).</summary>
    public override bool Init()
    {
        SelectFlash = -1f;
        LeftArrowFlash = 0f;
        RightArrowFlash = 0f;
        return true;
    }

    /// <summary><c>CFEPMain::IsActionAvailable</c> (0x004621e0): multiplayer needs two controllers (the PC always has two).</summary>
    public bool IsActionAvailable(int action) => action switch
    {
        -1 or 0 or 2 or 4 or 5 or 6 => true,
        3 => FrontEnd.ControllerPorts >= 2,
        1 or 8 => frontEnd.Career.Record.CareerInProgress != 0,
        _ => false,
    };

    /// <summary><c>CFEPMain::ButtonPressed</c> (0x00462250).</summary>
    public override void ButtonPressed(int button, float value)
    {
        switch (button)
        {
            case FrontEndButtons.MenuUp:
            {
                int top = AmericanText ? 0 : -1;
                if (Selected > top)
                {
                    do Selected--; while (!IsActionAvailable(Selected));
                    SelectFlash = 0f;
                }
                if (Selected < top) Selected = top;
                CursorTarget = Selected;
                frontEnd.PlaySound(FrontEndSound.Move);
                IdleTime = 0f;
                break;
            }
            case FrontEndButtons.MenuDown:
                if (Selected < NumActions - 1)
                {
                    do Selected++; while (!IsActionAvailable(Selected));
                    SelectFlash = 0f;
                }
                if (Selected >= NumActions) Selected = NumActions - 1;
                CursorTarget = Selected;
                frontEnd.PlaySound(FrontEndSound.Move);
                IdleTime = 0f;
                break;
            case FrontEndButtons.MenuLeft:
                if (Selected == -1)
                {
                    int language = frontEnd.Language - 1;
                    if (language < 0) language = 4;
                    frontEnd.SetLanguage(language);
                    frontEnd.PlaySound(FrontEndSound.Move);
                    LeftArrowFlash = 1f;
                }
                IdleTime = 0f;
                break;
            case FrontEndButtons.MenuRight:
                if (Selected == -1)
                {
                    int language = frontEnd.Language + 1;
                    if (language == 5) language = 0;
                    frontEnd.SetLanguage(language);
                    frontEnd.PlaySound(FrontEndSound.Move);
                    RightArrowFlash = 1f;
                }
                IdleTime = 0f;
                break;
            case FrontEndButtons.MenuSelect:
                DoAction();
                break;
        }
    }

    /// <summary><c>CFEPMain::StartNewCareer</c>: a blank career, the default name, then the storage pages to save it.</summary>
    public void StartNewCareer()
    {
        frontEnd.Career.Blank();
        frontEnd.VirtualKeyboard?.SetDefaultName();
        frontEnd.SaveMode = true;
        frontEnd.DevSelect?.Open();
        frontEnd.SetSuccessPage(FrontEndPage.LevelSelect, 0);
        frontEnd.AutoSave = AutoSaveMode.Not;
        frontEnd.SelectedLevel = 100;
        frontEnd.LevelSelect?.SyncToSelectedLevel();
    }

    private void OpenLoad(int time)
    {
        frontEnd.SaveMode = false;
        frontEnd.DevSelect?.Open();
        frontEnd.SetSuccessPage(FrontEndPage.LevelSelect, time);
    }

    /// <summary><c>CFEPMain::DoAction</c> (0x004623e0).</summary>
    public void DoAction()
    {
        bool inProgress = frontEnd.Career.Record.CareerInProgress != 0;
        switch (Selected)
        {
            case 0:
                if (inProgress) frontEnd.ShowMessage(320f, 240f, 300f, t => t.GetText(Text.FrontEndText.AbandonCareer), 0, 10, MessageBoxOption.YesNo, MessageBoxQuestion.NewCareer);
                else StartNewCareer();
                break;
            case 1:
                frontEnd.SetPage(FrontEndPage.LevelSelect, 70);
                break;
            case 2:
                if (inProgress) frontEnd.ShowMessage(320f, 240f, 300f, t => t.GetText(Text.FrontEndText.AbandonCareer), 0, 10, MessageBoxOption.YesNo, MessageBoxQuestion.LoadCareer);
                else OpenLoad(0);
                break;
            case 8:
                frontEnd.SaveMode = true;
                if (frontEnd.AutoSave == AutoSaveMode.Normal)
                {
                    frontEnd.SetPage(FrontEndPage.SaveGame, 0);
                    frontEnd.SetSuccessPage(FrontEndPage.Main, 70);
                }
                else
                {
                    frontEnd.DevSelect?.Open();
                    frontEnd.SetSuccessPage(FrontEndPage.Main, 70);
                }
                break;
            case 3:
                frontEnd.SetPage(FrontEndPage.Multiplayer, 70);
                break;
            case 4:
                frontEnd.SetPage(FrontEndPage.Goodies, 70);
                break;
            case 5:
                frontEnd.SetPage(FrontEndPage.Options, 70);
                break;
            case 6:
                frontEnd.MessageBox.Kill();
                // Localization_GetStringById(0xe4): a PC text, from the player's executable through the host.
                frontEnd.ShowMessage(320f, 240f, 400f, t => t.PcText(0xe4), 0, 10, MessageBoxOption.YesNo, MessageBoxQuestion.Quit);
                frontEnd.MessageBox.SetOnDismiss(FrontEndPage.Main, 0);
                break;
        }
        frontEnd.PlaySound(FrontEndSound.Select);
        IdleTime = 0f;
    }

    /// <summary>
    /// <c>CFEPMain::Process</c> (0x00462640): the fireworks once the game is complete, keeping the selection on an
    /// available entry, the language box's save, the quit, new-career and load questions, the flashes, the idle return
    /// to the title page, and the cursor's easing. The PC memory card never changes and the auto-configuration test is off.
    /// </summary>
    public override void Process(FrontEndPageState state)
    {
        FrontEndMessageBox box = frontEnd.MessageBox;
        if (state == FrontEndPageState.Active)
        {
            OnslaughtRebuild.Parity.Career.Career career = frontEnd.Career;
            if (career.Record.NodeCompleteFlag(career.GetNodeFromWorldNo(800)) != 0 || career.Record.NodeCompleteFlag(career.GetNodeFromWorldNo(741)) != 0)
            {
                // The camera and the effect are presentation; the draws on the main thread's rand() are not. Creating the
                // effect draws more inside the particle system, which is its owner's to model.
                CrtRandStream rand = (frontEnd.Crt ?? throw new InvalidOperationException("The fireworks need the process's CRT state; the host must set FrontEnd.Crt.")).MainThread;
                if (rand.Rand() % 50 == 0)
                {
                    rand.Rand();
                    rand.Rand();
                    throw new NotSupportedException("The firework effect's own particle draws are not modelled yet.");
                }
            }
            while (Selected > 0 && !IsActionAvailable(Selected)) Selected--;
            while (Selected < NumActions - 1 && !IsActionAvailable(Selected)) Selected++;
        }

        if (box.BeingDisplayed && box.Question == MessageBoxQuestion.Language && box.FadeState == 1)
        {
            LoadedSoundLanguage = frontEnd.Language;
            SaveAfterLanguageChange?.Invoke();
        }

        if (box.BeingDisplayed && box.Question == MessageBoxQuestion.Quit)
        {
            switch (box.GetFinalChoice())
            {
                case MessageBoxQuestion.Yes:
                    frontEnd.Quit = FrontEnd.QuitToSystem;
                    break;
                case MessageBoxQuestion.No:
                    box.FadeCount = 0f;
                    box.FadeState = 2;
                    box.OnDismissPage = FrontEndPage.None;
                    break;
            }
        }

        if (SelectFlash != -1f)
        {
            SelectFlash = (float)X87.Add(SelectFlash, 0.05f);
            if (SelectFlash > 1f) SelectFlash = 1f;
        }
        LeftArrowFlash = (float)X87.Sub(LeftArrowFlash, 0.1f);
        if (LeftArrowFlash < 0f) LeftArrowFlash = 0f;
        RightArrowFlash = (float)X87.Sub(RightArrowFlash, 0.1f);
        if (RightArrowFlash < 0f) RightArrowFlash = 0f;

        if (box.BeingDisplayed)
        {
            if (box.Question == MessageBoxQuestion.NewCareer && box.GetFinalChoice() == MessageBoxQuestion.Yes) StartNewCareer();
            else if (box.Question == MessageBoxQuestion.LoadCareer && box.GetFinalChoice() == MessageBoxQuestion.Yes) OpenLoad(70);
        }

        if (!box.BeingDisplayed)
        {
            IdleTime = (float)X87.Add(IdleTime, X87.Mul(frontEnd.Timing.FrameStep, 50f));
            if (state == FrontEndPageState.Active)
            {
                if (!frontEnd.AttractMode)
                {
                    if (IdleTime > 165000f) frontEnd.SetPage(FrontEndPage.Intro, 0);
                }
                else
                {
                    frontEnd.SetPage(FrontEndPage.Intro, 0);
                }
            }
        }

        if (state == FrontEndPageState.Active && Selected != -1 && LoadedSoundLanguage != frontEnd.Language)
            box.Create(0, 10, false, MessageBoxOption.None, MessageBoxQuestion.Language);

        // x87: the target is an exact int load; the difference, product and sum round to 24 bits; fabsf is exact.
        float target = CursorTarget;
        CursorPos = (float)X87.Add(CursorPos, X87.Mul(X87.Sub(target, CursorPos), 0.2f));
        if (Math.Abs(X87.Sub(target, CursorPos)) < 0.05f) CursorPos = target;
    }

    private static Text.FrontEndText ActionText(int action) => action switch
    {
        0 => Text.FrontEndText.NewGame,
        1 => Text.FrontEndText.ContinueGame,
        2 => Text.FrontEndText.LoadGame,
        8 => Text.FrontEndText.SaveGame,
        3 => Text.FrontEndText.Multiplayer,
        4 => Text.FrontEndText.Goodies,
        5 => Text.FrontEndText.Options,
        _ => Text.FrontEndText.Quit,
    };

    /// <summary>
    /// The state-changing part of <c>CFEPMain::Render</c> (0x00462d40): the language row (from y 268; its arrows click
    /// left and right) and then each action, 20 apart from 36 below it, centred on x 219 at the small font's size of its
    /// text. Once the page is nine tenths in, hovering an available action selects it and clicking it presses select.
    /// Runs only with the host's small font and texts (<see cref="FrontEnd.SmallFont"/>).
    /// </summary>
    public override void Render(float transition, FrontEndPage dest)
    {
        if (frontEnd.SmallFont is not { } font || frontEnd.Texts is not { } texts) return;
        float y = 268f;
        int start = -1;
        if (AmericanText)
        {
            start = 0;
            y = 304f;
        }
        for (int i = start; i < NumActions; i++)
        {
            if (i != -1)
            {
                (int cx, int cy) = font.GetTextExtent(texts.GetText(ActionText(i)));
                // x87: the halved width is subtracted from 219 and stored; the half height divides as an int.
                float tx = (float)X87.Sub(219f, X87.Mul(cx, 0.5f));
                float ty = (float)X87.Sub(y, cy / 2);
                float right = (float)X87.Add(cx, tx), bottom = (float)X87.Add(cy, ty);
                if (transition > 0.9f && IsActionAvailable(i))
                {
                    if (frontEnd.GetCursorStateInRect(tx, ty, right, bottom)) Selected = i;
                    if (frontEnd.MouseClickedInRect(tx, ty, right, bottom))
                    {
                        Selected = i;
                        ButtonPressed(FrontEndButtons.MenuSelect, 1f);
                    }
                }
                y = (float)X87.Add(y, 20f);
            }
            else
            {
                float top = (float)X87.Sub(y, 20f), bottom = (float)X87.Add(y, 20f);
                if (transition > 0.9f && IsActionAvailable(-1) && frontEnd.GetCursorStateInRect(119f, top, 319f, bottom)) Selected = -1;
                if (frontEnd.MouseClickedInRect(119f, top, 199f, bottom))
                {
                    Selected = -1;
                    ButtonPressed(FrontEndButtons.MenuLeft, 1f);
                }
                if (frontEnd.MouseClickedInRect(239f, top, 319f, bottom))
                {
                    Selected = -1;
                    ButtonPressed(FrontEndButtons.MenuRight, 1f);
                }
                y = (float)X87.Add(y, 36f);
            }
        }
    }

    /// <summary><c>CFEPMain::TransitionNotification</c> (0x004644d0): Continue is preselected when a career is in progress.</summary>
    public override void TransitionNotification(FrontEndPage from)
    {
        SelectFlash = -1f;
        StartTime = (float)X87.Add(frontEnd.Clock.GetSysTimeFloat(), 1.0);
        if (Selected == 0 && frontEnd.Career.Record.CareerInProgress != 0) Selected = 1;
        CursorTarget = Selected;
        CursorPos = Selected;
    }

    /// <summary><c>CFEPMain::ActiveNotification</c> (0x00464520).</summary>
    public override void ActiveNotification(FrontEndPage from)
    {
        SelectFlash = 0f;
        IdleTime = 0f;
    }
}
