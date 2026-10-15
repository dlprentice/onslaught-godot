// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/FEPDevSelect.cpp (reconstructed from the retail program; not in Stuart's release).

using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary>
/// The storage-device page (<c>CFEPDevSelect</c>). On PC there is one device, always present, formatted and roomy, so
/// opening it goes straight on: to the name keyboard when saving, to the save list when loading. While the page is
/// active it presses select itself every frame.
/// </summary>
public sealed class DevSelectPage(FrontEnd frontEnd) : FrontEndPageBase
{
    /// <summary><c>mCard</c>: the chosen device, -1 for the hard disk, -2 for none.</summary>
    public int Card { get; private set; }
    public int Cursor { get; private set; }

    /// <summary><c>mState</c>: 0 the hard disk row, 1 the cards row, -1 nothing.</summary>
    public int State { get; private set; }

    public float StartTime { get; private set; }
    public bool CardsChanged { get; private set; }

    private MemoryCard Card0 => frontEnd.MemoryCard;

    /// <summary><c>CFEPDevSelect::Init</c> (0x00458620): with an explicit save pending, the first device is chosen.</summary>
    public override bool Init()
    {
        if (Card0.Update()) CardsChanged = true;
        if (frontEnd.AutoSave == AutoSaveMode.Not) ChooseFirstDevice();
        return true;
    }

    private void ChooseFirstDevice()
    {
        if (Card0.IsHddAvailable)
        {
            Cursor = 0;
            State = 0;
            Card = -1;
            return;
        }
        int found = -1;
        for (int i = 0; i < Card0.NumCards && found == -1; i++)
        {
            if (Card0.GetCardInfo(i).Present) found = i;
        }
        if (found == -1)
        {
            Cursor = -1;
            State = -1;
            Card = -2;
            return;
        }
        State = 1;
        Cursor = found;
        Card = found;
    }

    /// <summary>
    /// <c>CFEPDevSelect::UseDevice</c> (0x00458710): saving goes to the name keyboard and loading to the save list.
    /// Returns false only when the device has gone (never on PC). A card too full for the save (never on PC, which
    /// reports 0x7fffffff bytes free, unless it holds more than 4,095 saves) asks to delete saves; that path is not ported.
    /// </summary>
    public bool UseDevice()
    {
        if (Card == -2) return true;
        frontEnd.MemoryCardNumber = Card;
        (bool present, bool formatted, int bytesFree, _) = Card0.GetCardInfo(Card);
        int numSaves = Card0.NumSaves;
        if (!present) throw new InvalidOperationException("The PC's save folder is always present.");

        if (frontEnd.SaveMode)
        {
            if (formatted)
            {
                int saveSize = Card0.GetSaveSize(OnslaughtRebuild.Parity.Career.CareerSaveGame.Size);
                if (bytesFree < saveSize || numSaves > 0xfff)
                    throw new NotSupportedException("A full save folder (more than 4,095 saves) is not ported.");
            }
            frontEnd.SetPage(FrontEndPage.VirtualKeyboard, 70);
            return true;
        }

        if (!formatted) throw new InvalidOperationException("The PC's save folder is always formatted.");
        frontEnd.SetPage(FrontEndPage.Directory, 70);
        return true;
    }

    /// <summary><c>CFEPDevSelect::Open</c> (0x004589f0).</summary>
    public void Open()
    {
        CheckDevice();
        UseDevice();
    }

    /// <summary><c>CFEPDevSelect::CheckDevice</c> (0x00458ce0): a device that has gone is chosen again.</summary>
    public void CheckDevice()
    {
        if (Card0.Update()) CardsChanged = true;
        bool present = Card >= 0 && Card0.GetCardInfo(Card).Present;
        if (Card == -1 && Card0.IsHddAvailable) return;
        if (present) return;
        ChooseFirstDevice();
        FrontEndMessageBox box = frontEnd.MessageBox;
        if (box.BeingDisplayed)
        {
            int q = box.Question;
            if (q >= MessageBoxQuestion.DeleteSaves && (q <= MessageBoxQuestion.DeleteSave || q == MessageBoxQuestion.Format)) box.Kill();
        }
    }

    /// <summary><c>CFEPDevSelect::ButtonPressed</c> (0x00458a10).</summary>
    public override void ButtonPressed(int button, float value)
    {
        switch (button)
        {
            case FrontEndButtons.MenuUp:
                if (State == 1 && Card0.IsHddAvailable)
                {
                    frontEnd.PlaySound(FrontEndSound.Move);
                    State = 0;
                    Card = -1;
                }
                break;
            case FrontEndButtons.MenuDown:
                if (State != 0) break;
                if (Card0.GetCardInfo(Cursor).Present)
                {
                    State = 1;
                    Card = Cursor;
                    frontEnd.PlaySound(FrontEndSound.Move);
                    break;
                }
                int found = -1;
                for (int i = 0; i < Card0.NumCards && found == -1; i++)
                {
                    if (Card0.GetCardInfo(i).Present) found = i;
                }
                if (found == -1)
                {
                    State = 0;
                    Card = -1;
                    break;
                }
                State = 1;
                Cursor = found;
                Card = found;
                frontEnd.PlaySound(FrontEndSound.Move);
                break;
            case FrontEndButtons.MenuLeft:
                if (State == 1)
                {
                    for (int i = Cursor - 1; i >= 0; i--)
                    {
                        if (!Card0.GetCardInfo(i).Present) continue;
                        Cursor = i;
                        Card = i;
                        frontEnd.PlaySound(FrontEndSound.Move);
                        break;
                    }
                }
                break;
            case FrontEndButtons.MenuRight:
                if (State == 1)
                {
                    for (int i = Cursor + 1; i < Card0.NumCards; i++)
                    {
                        if (!Card0.GetCardInfo(i).Present) continue;
                        Cursor = i;
                        Card = i;
                        frontEnd.PlaySound(FrontEndSound.Move);
                        break;
                    }
                }
                break;
            case FrontEndButtons.MenuSelect:
                if (!UseDevice()) frontEnd.PlaySound(FrontEndSound.Select);
                break;
            case FrontEndButtons.MenuBack:
                if (frontEnd.SuccessPage != FrontEndPage.Debriefing)
                {
                    frontEnd.SetPage(FrontEndPage.Main, 70);
                    frontEnd.PlaySound(FrontEndSound.Back);
                }
                break;
            case FrontEndButtons.MenuSkip:
                if (frontEnd.SaveMode)
                {
                    frontEnd.PlaySound(FrontEndSound.Select);
                    frontEnd.ShowMessage(320f, 240f, 300f, t => t.GetText(Text.FrontEndText.NoAutoSave), 0, 10, MessageBoxOption.YesNo, MessageBoxQuestion.NoAutoSave);
                }
                break;
        }
    }

    /// <summary>
    /// <c>CFEPDevSelect::Process</c> (0x00458e00): the device is checked, the questions answered (playing without
    /// saving turns autosave to pretend and marks the career in progress), and while active the page presses select.
    /// </summary>
    public override void Process(FrontEndPageState state)
    {
        if (state == FrontEndPageState.Inactive) return;
        CheckDevice();
        FrontEndMessageBox box = frontEnd.MessageBox;
        if (box.BeingDisplayed)
        {
            switch (box.Question)
            {
                case MessageBoxQuestion.DeleteSaves:
                    switch (box.GetFinalChoice())
                    {
                        case MessageBoxQuestion.No:
                            if (frontEnd.ActivePage != FrontEndPage.Main) frontEnd.SetPage(FrontEndPage.Main, 50);
                            break;
                        case MessageBoxQuestion.Yes:
                            frontEnd.SetPage(FrontEndPage.Directory, 0);
                            break;
                    }
                    break;
                case 5:
                    box.GetFinalChoice();
                    break;
                case MessageBoxQuestion.NoAutoSave:
                    if (box.GetFinalChoice() == MessageBoxQuestion.Yes)
                    {
                        frontEnd.SetPage(frontEnd.SuccessPage, frontEnd.SuccessTransitionTime);
                        frontEnd.AutoSave = AutoSaveMode.Pretend;
                        frontEnd.Career.Record.CareerInProgress = 1;
                    }
                    break;
            }
        }
        if (state == FrontEndPageState.Active) ButtonPressed(FrontEndButtons.MenuSelect, 1f);
    }

    /// <summary><c>CFEPDevSelect::TransitionNotification</c> (0x00459580).</summary>
    public override void TransitionNotification(FrontEndPage from)
    {
        CheckDevice();
        StartTime = (float)X87.Add(frontEnd.Clock.GetSysTimeFloat(), 2.0);
    }

    /// <summary>
    /// The state-changing part of <c>CFEPDevSelect::Render</c> (0x00458ee0): the help line's arrows. (The "no device"
    /// box it creates and kills needs a card number of -2, which the PC never has.)
    /// </summary>
    public override void Render(float transition, FrontEndPage dest)
    {
        if (Card != -2 || frontEnd.MessageBox.BeingDisplayed) frontEnd.RenderHelpArrow(5, transition);
        if (frontEnd.SuccessPage == FrontEndPage.Debriefing) frontEnd.RenderHelpArrow(3, transition);
        else frontEnd.RenderHelpArrow(frontEnd.SaveMode ? 2 : 1, transition);
    }

    /// <summary><c>CFEPDevSelect::SetCurrentCard</c> (0x00459810).</summary>
    public void SetCurrentCard(int card)
    {
        State = 1;
        Card = card;
        Cursor = card;
    }
}
