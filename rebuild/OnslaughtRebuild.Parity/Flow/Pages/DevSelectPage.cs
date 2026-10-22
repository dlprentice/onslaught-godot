// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/FEPDevSelect.cpp (reconstructed from the retail program; not in Stuart's release):
// RenderPreCommon (folded with CFEPDirectory's, 0051ae50), Render 00458ee0 and RenderDevice 004595b0.

using OnslaughtRebuild.Parity.Flow.Draw;
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

    /// <summary><c>CFEPDevSelect::RenderPreCommon</c> (0x0051ae50, shared with the directory page): the standard video background.</summary>
    public override void RenderPreCommon(float transition, FrontEndPage dest) =>
        frontEnd.DrawStandardVideoBackground(transition, 0x3fffffff, dest);

    /// <summary>
    /// <c>CFEPDevSelect::Render</c> (0x00458ee0). Its state-changing part: the help line's arrows. (The "no device" box
    /// it creates and kills needs a card number of -2, which the PC never has.) With <see cref="FrontEnd.Draw"/> it also
    /// draws: the sliding text, borders and mask; the devices (the PC's one card, the hard disk being unavailable) with
    /// the selected one pulsing; the "available blocks" label; the small logo; the title bar; and the help text.
    /// </summary>
    public override void Render(float transition, FrontEndPage dest)
    {
        FrontEndDraw? draw = frontEnd.Draw;
        Text.FrontEndTexts? texts = frontEnd.Texts;

        frontEnd.DrawSlidingTextBordersAndMask(transition, dest);
        if (transition > 0f) RenderDevices(draw, transition, dest);

        if (Card != -2 || frontEnd.MessageBox.BeingDisplayed) frontEnd.RenderHelpArrow(5, transition);
        if (frontEnd.SuccessPage == FrontEndPage.Debriefing) frontEnd.RenderHelpArrow(3, transition);
        else frontEnd.RenderHelpArrow(frontEnd.SaveMode ? 2 : 1, transition);

        float tr = (transition - 0.75f) * 4f;
        tr = tr < 0f ? 0f : tr > 1f ? 1f : tr;
        frontEnd.RenderSmallBEALogo(tr);

        // The title and the help text are read whether or not they are drawn (each read checks the X key); retail reads the
        // help text twice, once to measure it and once to draw it.
        if (texts is null) return;
        Text.FrontEndText title;
        string help;
        if (frontEnd.SaveMode)
        {
            title = frontEnd.Career.Record.CareerInProgress != 0 ? Text.FrontEndText.SaveGame2 : Text.FrontEndText.SaveNewGame2;
            help = "FETX_SELECTSAVLOC";
        }
        else
        {
            title = Text.FrontEndText.LoadGame2;
            help = "FETX_SELECTLOADLOC";
        }
        frontEnd.DrawTitleBar(texts.GetText(title), transition, dest);

        int ta = FrontEndDraw.MakeAlpha(tr);
        string measured = texts.GetTextByName(help);
        string helpText = texts.GetTextByName(help);
        if (draw is null || frontEnd.SmallFont is not { } small) return;
        int cx = small.GetTextExtent(measured).Width;
        draw.DrawTextDynamic(small, CanvasTextureKind.SmallFont, 395f - cx * 0.5f, 100f, 0.01f, 1f, 1f, FrontEndDraw.BlendAlpha2(0xff3f3f5f, ta),
            helpText, 1000f);
    }

    /// <summary>
    /// The devices: the hard disk (never on PC) and the cards two to a column, then the "available blocks" label, which
    /// retail reads twice (to measure it and to draw it) whether or not anything is drawn; each read checks the X key.
    /// The hard disk's name and free space, shown while it is selected, are left out (the PC has no hard disk device),
    /// and the selected card's name and sizes are only measured.
    /// </summary>
    private void RenderDevices(FrontEndDraw? draw, float transition, FrontEndPage dest)
    {
        string? measured = frontEnd.Texts?.GetTextByName("FETX_DS_AVAILABLE_BLOCKS_XBOX");
        string? blocks = frontEnd.Texts?.GetTextByName("FETX_DS_AVAILABLE_BLOCKS_XBOX");
        if (draw is null) return;

        int alpha = FrontEndDraw.MakeAlpha(dest == FrontEndPage.VirtualKeyboard
            ? FrontEndDraw.RangeTransition(transition, 0.5f, 1f)
            : FrontEndDraw.RangeTransition(transition, 0.75f, 1f));

        if (Card0.IsHddAvailable)
        {
            (_, _, int free, int total) = Card0.GetCardInfo(-1);
            RenderDevice(draw, 200f, 120f, (float)total - free, total, 0.89f, 1f, alpha, State == 0, true, true, true);
        }

        for (int i = 0; i < Card0.NumCards; i++)
        {
            (bool present, bool formatted, int free, int total) = Card0.GetCardInfo(i);
            if (!present || !formatted)
            {
                free = 0;
                total = 0;
            }
            RenderDevice(draw, i / 2 * 96f + (i & 1) * 48f + 152f, 280f, (float)total - free, total, 0.89f, 0.5f, alpha,
                State == 1 && Cursor == i, false, formatted, present);
        }

        if (frontEnd.SmallFont is { } small && measured is not null && blocks is not null)
        {
            int cx = small.GetTextExtent(measured).Width;
            draw.DrawTextWithShadow(small, new QuadTexture(CanvasTextureKind.SmallFont), 410f - cx - 10f + 100f, 340f,
                FrontEndDraw.BlendAlpha2(0xff7f7f00, alpha), blocks, 0, 0.79f);
        }
    }

    /// <summary>
    /// <c>CFEPDevSelect::RenderDevice</c> (0x004595b0): the device's panel (pulsing green when selected, red when a save
    /// will not fit) and white box under its picture, and its usage bar.
    /// </summary>
    private void RenderDevice(FrontEndDraw draw, float x, float y, float used, float total, float z, float scale, int alpha, bool selected,
        bool hdd, bool formatted, bool present)
    {
        FrontEndTexture tex = hdd ? FrontEndTexture.XbXbox : formatted ? FrontEndTexture.XbMu : FrontEndTexture.XbMuCorrupt;
        if (draw.Textures.Size(tex) is not { } size) return;
        float w = size.Width * scale;
        float h = size.Height * scale;

        uint col;
        if (selected)
        {
            // SINT(...): fistp under /QIfist.
            uint v = unchecked((uint)(0xbf - X87.Fist(MathF.Sin(frontEnd.Counter * 3.14f * (1f / 30f)) * -64f)));
            col = unchecked(0x6f000000 + (v & ~1u) * 128 + v);
            col = unchecked(((col >> 24) * (uint)alpha & 0xffffff00) * 0x10000 | (col & 0x00ffffff));
        }
        else col = FrontEndDraw.BlendAlpha2(0x6f000000, alpha);

        int saveSize = Card0.GetSaveSize(OnslaughtRebuild.Parity.Career.CareerSaveGame.Size);
        if (total - used < saveSize && frontEnd.SaveMode && formatted) col = (col & 0xff000000) + ((col & 0xff) << 16);

        float bottom = y + h;
        draw.DrawPanel(x, y, x + w, bottom + 16f, z, col);
        draw.DrawBox(x, y, x + w, bottom + 16f, FrontEndDraw.BlendAlpha2(0xffffffff, alpha), 2f, z);

        if (!present) return;
        draw.DrawColouredSprite(x, y, 0.79f, tex, FrontEndDraw.BlendAlpha2(0xffffffff, alpha), scale, scale);
        if (total != 0f)
            draw.DrawBarGraph(x + w * 0.05f, bottom + 4f, x + w * 0.95f, bottom + 12f, used, total, z, FrontEndDraw.BlendAlpha2(0xff004000, alpha),
                FrontEndDraw.BlendAlpha2(0xffff0000, alpha));
    }

    /// <summary><c>CFEPDevSelect::SetCurrentCard</c> (0x00459810).</summary>
    public void SetCurrentCard(int card)
    {
        State = 1;
        Card = card;
        Cursor = card;
    }
}
