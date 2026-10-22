// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/FEPLoadGame.cpp (CFEPLoadGame), in Stuart's release.

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Numerics;
using Fet = OnslaughtRebuild.Parity.Text.FrontEndText;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary>
/// The load page (<c>CFEPLoadGame</c>): while active with no box up, it loads the save the list picked into the career,
/// shows "loaded" for 60 frames and goes to the success page; the save becomes the autosave's target.
/// </summary>
public sealed class LoadGamePage(FrontEnd frontEnd) : FrontEndPageBase
{
    /// <summary>Message box texts this page shows, for the host's renderer.</summary>
    public enum Text
    {
        LoadDone,
        BadSaveVersion,
        LoadInvalid,
    }

    public int CX { get; private set; }
    public int CY { get; private set; }

    /// <summary><c>mSaveGameNumber</c>: the save's index in the list, or -1.</summary>
    public int SaveGameNumber { get; set; } = -1;

    public string SaveGameName { get; set; } = "";
    public float StartTime { get; private set; }
    public Text? LastText { get; private set; }

    /// <summary><c>CFEPLoadGame::Init</c> (0x00461c40).</summary>
    public override bool Init()
    {
        CX = 0;
        CY = 0;
        SaveGameNumber = -1;
        SaveGameName = "";
        return true;
    }

    /// <summary><c>CFEPLoadGame::ButtonPressed</c> (0x00461c60).</summary>
    public override void ButtonPressed(int button, float value)
    {
        switch (button)
        {
            case FrontEndButtons.MenuUp:
                CY = Math.Max(CY - 1, 0);
                frontEnd.PlaySound(FrontEndSound.Move);
                break;
            case FrontEndButtons.MenuDown:
                CY = Math.Min(CY + 1, 0xff);
                frontEnd.PlaySound(FrontEndSound.Move);
                break;
            case FrontEndButtons.MenuLeft:
                CX = Math.Max(CX - 1, 0);
                frontEnd.PlaySound(FrontEndSound.Move);
                break;
            case FrontEndButtons.MenuRight:
                CX = Math.Min(CX + 1, 0xff);
                frontEnd.PlaySound(FrontEndSound.Move);
                break;
            case FrontEndButtons.MenuSelect:
                frontEnd.PlaySound(FrontEndSound.Select);
                break;
            case FrontEndButtons.MenuBack:
                frontEnd.PlaySound(FrontEndSound.Back);
                frontEnd.SetPage(FrontEndPage.Main, 0);
                break;
        }
    }

    /// <summary><c>CFEPLoadGame::Process</c> (0x00461d60).</summary>
    public override void Process(FrontEndPageState state)
    {
        if (state != FrontEndPageState.Inactive) frontEnd.MemoryCard.Update();
        if (state == FrontEndPageState.Active && !frontEnd.MessageBox.BeingDisplayed) StartLoad();
    }

    /// <summary>The state-changing part of <c>CFEPLoadGame::Render</c> (0x00461d90): the help arrow (continue).</summary>
    public override void Render(float transition, FrontEndPage dest)
    {
        frontEnd.DrawSlidingTextBordersAndMask(transition, dest);
        // The title is read whether or not it is drawn (each read checks the X key).
        if (frontEnd.Texts is { } texts) frontEnd.DrawTitleBar(texts.GetText(OnslaughtRebuild.Parity.Text.FrontEndText.LoadGame2), transition, dest);
        frontEnd.RenderSmallBEALogo(Draw.FrontEndDraw.RangeTransition(transition, 0.75f, 1f));
        frontEnd.RenderHelpArrow(4, transition);
    }

    /// <summary><c>CFEPLoadGame::RenderPreCommon</c>: the standard video background.</summary>
    public override void RenderPreCommon(float transition, FrontEndPage dest) =>
        frontEnd.DrawStandardVideoBackground(transition, 0x3fffffff, dest);

    /// <summary><c>CFEPLoadGame::TransitionNotification</c>.</summary>
    public override void TransitionNotification(FrontEndPage from) =>
        StartTime = (float)X87.Add(frontEnd.Clock.GetSysTimeFloat(), 2.0);

    /// <summary>
    /// <c>CFEPLoadGame::StartLoad</c> (0x00461e20). The name goes to the save page (the next save's name) and the
    /// keyboard. A whole save loads into the career to play (the volumes kept, the bindings and options not read); on
    /// success the loaded file is also written as <c>defaultoptions.bea</c> (unless a display reset is pending), and on
    /// a wrong version or a failed read the page returns to the main menu with a message.
    /// </summary>
    public void StartLoad()
    {
        if (SaveGameNumber == -1) return;
        frontEnd.SaveGameName = SaveGameName;
        frontEnd.VirtualKeyboard?.SetName(SaveGameName);
        // The PC's folder is always present and formatted, and never reports having been unplugged.
        FrontEndMessageBox box = frontEnd.MessageBox;
        if (frontEnd.MemoryCard.ReadSave(SaveGameName, CareerSaveGame.Size, out byte[] buffer) == MemoryCard.Success)
        {
            if (!CareerSaveGame.Load(frontEnd.Career, buffer, suggestNextLevel: true).Loaded)
            {
                box.Kill();
                Show(0, MessageBoxOption.AContinue, Text.BadSaveVersion);
                frontEnd.SetPage(FrontEndPage.Main, 0);
            }
            else
            {
                box.Kill();
                Show(60, MessageBoxOption.None, Text.LoadDone);
                frontEnd.SetPage(frontEnd.SuccessPage, frontEnd.SuccessTransitionTime);
                frontEnd.AutoSave = AutoSaveMode.Normal;
                if (frontEnd.Clock.DisplayReset.Pending == Platform.DisplayResetRequest.None) frontEnd.WriteDefaultOptions?.Invoke(buffer);
            }
        }
        else
        {
            box.Kill();
            Show(0, MessageBoxOption.AContinue, Text.LoadInvalid);
            frontEnd.SetPage(FrontEndPage.Main, 0);
        }
    }

    private void Show(uint timeout, MessageBoxOption option, Text text)
    {
        // Each box gets its layout from its text (FEPLoadGame.cpp's Create calls), the timed "loaded" box included: its
        // text is read (checking the X key) as the others' are.
        frontEnd.ShowMessage(320f, 240f, 400f, t => text switch
        {
            Text.LoadDone => t.GetTextByName("FETX_LOADDONE"),
            Text.BadSaveVersion => t.GetText(Fet.BadSaveVersion),
            _ => t.GetText(Fet.LoadInvalid),
        }, timeout, 10, option, 0);
        LastText = text;
    }
}
