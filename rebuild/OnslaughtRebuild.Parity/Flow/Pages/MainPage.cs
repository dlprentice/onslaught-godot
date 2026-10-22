// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/FEPMain.cpp (reconstructed from the retail program; not in Stuart's release).

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Flow.Draw;
using OnslaughtRebuild.Parity.Crt;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Particles;

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
                FrontEndParticles particles = frontEnd.Particles ?? throw new InvalidOperationException(
                    "completed-career fireworks need the front-end particle resources");
                particles.Camera = (new FVector(0, -20, 0), FMatrix.Identity);
                CrtRandStream rand = particles.Manager.Rand;
                if (rand.Rand() % 50 == 0)
                {
                    // VC6 evaluates the vector's arguments right to left: Z, then X. Effect initialisation follows.
                    float z = ((rand.Rand() & 255) - 128) * 0.0390625f - 5f;
                    float x = ((rand.Rand() & 255) - 128) * 0.078125f;
                    particles.Manager.CreateEffect(particles.Manager.Set.GetPD("Firework Explosion Blue Effect"), null,
                        new FVector(x, 0, z), false, false);
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
    /// <c>CFEPMain::RenderPreCommon</c> (0x00462b70): the background video, centred, growing in from a larger size as the page
    /// arrives (not from the intro) and brightening.
    /// </summary>
    public override void RenderPreCommon(float transition, FrontEndPage dest)
    {
        if (frontEnd.Draw is not { } draw) return;
        float t = FrontEndDraw.RangeTransition(transition, 0.5f, 1f);
        if (dest == FrontEndPage.Intro) t = 1f;
        float u = 2f - t;
        float scale = (u * u * u - 1f) * 0.87f + 1f;
        float v = 1f - u;
        uint colour = unchecked((uint)(X87.Fist(X87.Mul(1f - v * v, 255f)) * 0x3f0000)) | 0xffffff;
        draw.VideoWithoutFilm(FrontEndDraw.WindowWidth / 2f, FrontEndDraw.WindowHeight / 2f, 0.96f,
            FrontEndDraw.WindowWidth * scale * 0.0087890625f, FrontEndDraw.WindowHeight * scale * 0.0079101566f, colour, true);
    }

    /// <summary>
    /// <c>CFEPMain::Render</c> (0x00462d40). Its state-changing part: the language row (from y 268; its arrows click left
    /// and right) and then each action, 20 apart from 36 below it, centred on x 219 at the small font's size of its text;
    /// once the page is nine tenths in, hovering an available action selects it and clicking it presses select. That part
    /// runs only with the host's small font and texts (<see cref="FrontEnd.SmallFont"/>). With <see cref="FrontEnd.Draw"/>
    /// it also draws: the Forseti strip scrolling behind, the actions (the selected one on a bar), the language row's flag
    /// and arrows, the title's brackets and symbol (assembling as the intro hands over), the selected action's symbol, the
    /// version, the title with its passing shine, and a white flash as the page arrives.
    /// </summary>
    public override void Render(float transition, FrontEndPage dest)
    {
        FrontEndDraw? draw = frontEnd.Draw;
        float counter = frontEnd.Counter;
        if (draw is not null)
        {
            float strip = 175f - (float)((double)(counter * 0.3f) % 350.0);
            uint stripCol = FrontEndDraw.BlendAlpha2(0x3fffffff, X87.Fist(X87.Mul(FrontEndDraw.RangeTransition(transition, 0.75f, 1f), 255f)));
            for (int k = 0; k < 3; k++)
            {
                draw.DrawColouredSprite(458f, strip + 350f * k, 0.9f, FrontEndTexture.ForsetiText, stripCol, 1f, 1f, SpriteAlign.Centre);
            }
        }

        if (frontEnd.SmallFont is { } font && frontEnd.Texts is { } texts)
        {
            float y = 268f;
            int start = -1;
            if (AmericanText)
            {
                start = 0;
                y = 304f;
            }
            int alpha = FrontEndDraw.MakeAlpha(FrontEndDraw.RangeTransition(transition, 0.75f, 1f));
            for (int i = start; i < NumActions; i++)
            {
                if (i != -1)
                {
                    string text = texts.GetText(ActionText(i));
                    (int cx, int cy) = font.GetTextExtent(text);
                    if (i == Selected)
                    {
                        alpha = FrontEndDraw.MakeAlpha(FrontEndDraw.RangeTransition(transition, 0.8f, 1f));
                        draw?.DrawColouredSprite(219f, y, 0.33f, FrontEndTexture.BeaTitleTextBox, FrontEndDraw.BlendAlpha2(0x7f000000, alpha),
                            (cx + 30f) * (1f / 256f), 1f, SpriteAlign.Centre);
                    }
                    uint c = IsActionAvailable(i) ? 0xff4f4f4f : 0x7f1f1f1f;
                    if (i == Selected) c = 0xffff6f3f;
                    // x87: the halved width is subtracted from 219 and stored; the half height divides as an int.
                    float tx = (float)X87.Sub(219f, X87.Mul(cx, 0.5f));
                    float ty = (float)X87.Sub(y, cy / 2);
                    c = FrontEndDraw.BlendAlpha2(FrontEndDraw.BlendAlpha2(c, alpha), 255);
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
                    draw?.DrawTextDynamic(font, CanvasTextureKind.SmallFont, tx, ty, 0.32f, 1f, 1f, c, text, 1000f);
                    y = (float)X87.Add(y, 20f);
                }
                else
                {
                    uint c = 0xff7f7f7f;
                    if (Selected == -1)
                        c = unchecked(0xffdfdfdf - (uint)(X87.Fist(MathF.Sin(counter * 3.14f * (1f / 30f)) * -32f) * 0x10101));
                    c = FrontEndDraw.BlendAlpha2(FrontEndDraw.BlendAlpha2(c, alpha), 255);
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
                    if (draw is not null) DrawLanguageRow(draw, y, c, alpha, counter);
                    y = (float)X87.Add(y, 36f);
                }
            }
        }

        if (draw is not null) DrawTitle(draw, transition, dest, counter);
    }

    // The language row's flag between its arrows (each arrow flashing larger after a press).
    private void DrawLanguageRow(FrontEndDraw draw, float y, uint c, int alpha, float counter)
    {
        draw.DrawColouredSprite(219f, y, 0.32f, FrontEndTexture.FlagUk + frontEnd.Language, c, 0.65f, 0.25f, SpriteAlign.Centre);
        bool shown = X87.Fist(counter) % 64 < 50;
        void Arrow(float x, float flash, float yaw)
        {
            if (!shown && !(flash > 0f)) return;
            uint ac = FrontEndDraw.BlendAlpha2(0xffffffff, alpha);
            if (Selected != -1) ac = FrontEndDraw.BlendAlpha2(ac, 64);
            draw.DrawColouredSprite(x, y, 0.32f, FrontEndTexture.HelpArrow, FrontEndDraw.BlendAlpha2(ac, 255), 0.8f, 0.8f, SpriteAlign.Centre, SpriteType.None, 1f, yaw);
            uint fc = FrontEndDraw.BlendAlpha2(0xffffffff, X87.Fist(flash * 255f));
            fc = FrontEndDraw.BlendAlpha2(fc, alpha);
            float scale = (1f - flash + 1f) * 0.8f;
            draw.DrawColouredSprite(x, y, 0.32f, FrontEndTexture.HelpArrow, FrontEndDraw.BlendAlpha2(fc, 255), scale, scale, SpriteAlign.Centre, SpriteType.None, 1f, yaw);
        }
        Arrow(154f, LeftArrowFlash, -3.14159265358979f);
        Arrow(284f, RightArrowFlash, 0f);
    }

    // Everything after the menu rows: the title's brackets and symbol, the selected action's symbol, the version, the
    // title and its shine, and the white flash.
    private void DrawTitle(FrontEndDraw draw, float transition, FrontEndPage dest, float counter)
    {
        float shx = FrontEndDraw.ShadowOffsetX(counter), shy = FrontEndDraw.ShadowOffsetY(counter);

        // The title's brackets: they close in as the intro hands over, and fly apart as the page leaves.
        int a = 255;
        float scale = 1.25f, yaw = 0f;
        if (transition < 1f)
        {
            if (dest == FrontEndPage.Intro)
            {
                if (transition < 0.2f)
                {
                    float f = FrontEndDraw.RangeTransition(transition, 0f, 0.2f);
                    a = FrontEndDraw.MakeAlpha(f);
                    scale = f;
                    yaw = -((1f - f) * 0.3f);
                }
                else if (transition < 0.4f) scale = 1f;
                else if (transition < 0.6f) scale = FrontEndDraw.RangeTransition(transition, 0.4f, 0.6f) * 0.25f + 1f;
            }
            else
            {
                float f = 1f - FrontEndDraw.RangeTransition(transition, 0.5f, 1f);
                a = FrontEndDraw.MakeAlpha(1f - f);
                scale = f * 10f + 1.25f;
                yaw = f * -0.5f;
            }
        }
        // Retail adds the x shadow offset to both coordinates here.
        draw.DrawColouredSprite(224f + shx, 349f + shx, 0.35f, FrontEndTexture.BeaTitleBracket01, FrontEndDraw.BlendAlpha2(0x3f000000, a),
            scale * 1.05f, scale * 1.05f, SpriteAlign.Centre, SpriteType.None, 1f, yaw);
        draw.DrawColouredSprite(219f, 344f, 0.3f, FrontEndTexture.BeaTitleBracket01, FrontEndDraw.BlendAlpha2(0xffffffff, a),
            scale, scale, SpriteAlign.Centre, SpriteType.None, 1f, yaw);

        a = 255;
        yaw = 0f;
        if (transition < 1f && dest == FrontEndPage.Intro)
        {
            bool drawBracket2 = true;
            if (transition < 0.2f)
            {
                float f = FrontEndDraw.RangeTransition(transition, 0f, 0.2f);
                a = FrontEndDraw.MakeAlpha(f);
                scale = f;
                yaw = (1f - f) * 0.3f;
            }
            else if (transition < 0.6f) scale = 1f;
            else if (transition < 0.8f)
            {
                float f = 1f - FrontEndDraw.RangeTransition(transition, 0.6f, 0.8f);
                a = FrontEndDraw.MakeAlpha(f);
                scale = f;
                yaw = -((1f - f) * 0.3f);
            }
            else drawBracket2 = false;
            if (drawBracket2)
            {
                draw.DrawColouredSprite(224f + shx, 349f + shx, 0.35f, FrontEndTexture.BeaTitleBracket02, FrontEndDraw.BlendAlpha2(0x3f000000, a),
                    scale * 1.05f, scale * 1.05f, SpriteAlign.Centre, SpriteType.None, 1f, yaw);
                draw.DrawColouredSprite(219f, 344f, 0.3f, FrontEndTexture.BeaTitleBracket02, FrontEndDraw.BlendAlpha2(0xffffffff, a),
                    scale, scale, SpriteAlign.Centre, SpriteType.None, 1f, yaw);
            }
        }

        // The title's symbol, the same way a little later.
        a = 255;
        scale = 1.25f;
        yaw = 0f;
        if (transition < 1f)
        {
            if (dest == FrontEndPage.Intro)
            {
                if (transition < 0.1f) a = 0;
                else if (transition < 0.3f)
                {
                    float f = FrontEndDraw.RangeTransition(transition, 0.1f, 0.3f);
                    a = FrontEndDraw.MakeAlpha(f);
                    scale = f;
                    yaw = -(f * 0.3f);
                }
                else if (transition < 0.5f) scale = 1f;
                else if (transition < 0.7f) scale = FrontEndDraw.RangeTransition(transition, 0.5f, 0.7f) * 0.25f + 1f;
            }
            else
            {
                float f = 1f - FrontEndDraw.RangeTransition(transition, 0.75f, 1f);
                a = FrontEndDraw.MakeAlpha(1f - f);
                scale = f * 1.4f + 1.25f;
                yaw = -f;
            }
        }
        draw.DrawColouredSprite(462f + shx, 365f + shy, 0.35f, FrontEndTexture.BeaTitleSymbolBracket01, FrontEndDraw.BlendAlpha2(0x3f000000, a),
            scale * 1.05f, scale * 1.05f, SpriteAlign.Centre, SpriteType.None, 1f, yaw);
        draw.DrawColouredSprite(457f, 355f, 0.3f, FrontEndTexture.BeaTitleSymbolBracket01, FrontEndDraw.BlendAlpha2(0xffffffff, a),
            scale, scale, SpriteAlign.Centre, SpriteType.None, 1f, yaw);

        a = 255;
        yaw = 0f;
        if (transition < 1f && dest == FrontEndPage.Intro && !(transition < 0.1f))
        {
            bool drawSymbolBracket2 = true;
            if (transition < 0.3f)
            {
                float f = FrontEndDraw.RangeTransition(transition, 0.1f, 0.3f);
                a = FrontEndDraw.MakeAlpha(f);
                scale = f;
                yaw = -(f * 0.3f);
            }
            else if (transition < 0.7f) scale = 1f;
            else if (transition < 0.9f)
            {
                float f = 1f - FrontEndDraw.RangeTransition(transition, 0.7f, 0.9f);
                a = FrontEndDraw.MakeAlpha(f);
                scale = f;
                yaw = -(f * 0.3f);
            }
            else drawSymbolBracket2 = false;
            if (drawSymbolBracket2)
            {
                draw.DrawColouredSprite(462f + shx, 365f + shy, 0.35f, FrontEndTexture.BeaTitleSymbolBracket02, FrontEndDraw.BlendAlpha2(0x3f000000, a),
                    scale * 1.05f, scale * 1.05f, SpriteAlign.Centre, SpriteType.None, 1f, yaw);
                draw.DrawColouredSprite(457f, 355f, 0.3f, FrontEndTexture.BeaTitleSymbolBracket02, FrontEndDraw.BlendAlpha2(0xffffffff, a),
                    scale, scale, SpriteAlign.Centre, SpriteType.None, 1f, yaw);
            }
        }

        // The selected action's symbol.
        FrontEndTexture? symbol = Selected switch
        {
            -1 => FrontEndTexture.BeaTitleNavSymbolLanguage01,
            0 => FrontEndTexture.BeaTitleNavSymbolNewgame01,
            1 => FrontEndTexture.BeaTitleNavSymbolContinuegame01,
            2 => FrontEndTexture.BeaTitleNavSymbolLoadgame01,
            8 => FrontEndTexture.BeaTitleNavSymbolSavegame01,
            3 => FrontEndTexture.BeaTitleNavSymbolMultiplayer01,
            4 => FrontEndTexture.BeaTitleNavSymbolGoodies01,
            5 => FrontEndTexture.BeaTitleNavSymbolOptions01,
            6 => FrontEndTexture.BeaTitleNavSymbolQuitgame01,
            _ => null,
        };
        if (symbol is { } tex)
        {
            a = FrontEndDraw.MakeAlpha(FrontEndDraw.RangeTransition(transition, 0.8f, 1f));
            draw.DrawColouredSprite(462f + shx, 365f + shy, 0.34f, tex, FrontEndDraw.BlendAlpha2(0x3f000000, a), 1.05f, 1.05f, SpriteAlign.Centre);
            draw.DrawColouredSprite(457f, 355f, 0.1f, tex, FrontEndDraw.BlendAlpha2(0xffffffff, a), 1f, 1f, SpriteAlign.Centre);
        }

        // The version, bottom left (drawn with gInFrontEnd clear: its own font colours are not affected by it).
        if (frontEnd.SmallFont is { } font)
        {
            float v = FrontEndDraw.RangeTransition(transition, 0.75f, 1f);
            string version = $"V{frontEnd.VersionMajor % 10}.{frontEnd.VersionMinor:00}";
            draw.DrawTextDynamic(font, CanvasTextureKind.SmallFont, 0f, FrontEndDraw.WindowHeight - 16, 0.01f, 1f, 1f,
                ((uint)X87.Fist(v * 255f) << 24) | 0x102025, version, 1000f);
        }

        // The title, and the shine that passes over it.
        a = X87.Fist(FrontEndDraw.RangeTransition(transition, 0.75f, 1f) * 255f);
        if (dest == FrontEndPage.Intro) a = 255;
        draw.DrawColouredSprite(325f + shx, 140f + shy, 0.1f, FrontEndTexture.BeaTitle2, FrontEndDraw.BlendAlpha2(0x3f000000, a), 1.05f, 1.05f, SpriteAlign.Centre);
        draw.ZAlways = true;
        draw.DrawColouredSprite(320f, 130f, 0.999f, FrontEndTexture.BeaTitle2, FrontEndDraw.BlendAlpha2(0xffafcfff, a), 1f, 1f, SpriteAlign.Centre);
        draw.ZAlways = false;
        float sx = 256f - (float)((double)(counter * 0.6f) % 512.0) + 65f;
        draw.Blend = CanvasBlend.Additive;
        uint shine = 0xff000000 + ((uint)(a * 127) >> 8) * 0x10101;
        draw.DrawColouredSprite(sx, 120f, 0.998f, FrontEndTexture.LogoShine, shine, 1f, 2f, SpriteAlign.Centre);
        draw.DrawColouredSprite(sx + 512f, 120f, 0.998f, FrontEndTexture.LogoShine, shine, 1f, 2f, SpriteAlign.Centre);
        draw.Blend = CanvasBlend.Modulate;

        // A white flash as the page arrives.
        float d = MathF.Abs(transition - 0.75f);
        if (d < 0.15f && dest != FrontEndPage.Intro)
        {
            float s = FrontEndDraw.WindowWidth * (1f / 32f);
            draw.DrawColouredSprite(0f, 0f, 0.001f, FrontEndTexture.White, FrontEndDraw.BlendAlpha2(0xffffffff, X87.Fist((1f - d * (1f / 0.15f)) * 128f)), s, s);
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
