// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/FEPVirtualKeyboard.cpp and src/FEPVirtualKeyboard.h (reconstructed from the retail
// program; not in Stuart's release).

using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Platform;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary>The normal font's metrics the name keyboard needs: a text's width in pixels, and whether it has a glyph.</summary>
public interface IFontMetrics
{
    int TextWidth(string text);
    bool CanDisplay(char character);
}

/// <summary>
/// The name keyboard (<c>CFEPVirtualKeyboard</c>): the career's name, "BEA n" by default, edited with an on-screen
/// keyboard or, on PC, the real one through a key trap. Done copies the name to the save page and goes there.
/// </summary>
/// <remarks>
/// Typed characters arrive as <c>WM_CHAR</c>, which the platform does not generate yet, and need the font's metrics
/// (<see cref="Font"/>); accepting the default name, the arrows, backspace and escape need neither. The save list drawn
/// beside the keyboard (the directory page's, picked with the mouse in its render) is not modelled.
/// </remarks>
public sealed class VirtualKeyboardPage : FrontEndPageBase, INameKeyboard
{
    public const char Left = (char)1, Right = (char)2, Backspace = (char)3, Shift = (char)4, CapsLock = (char)5;
    public const char Alphabet = (char)6, Symbols = (char)7, Accents = (char)8, Done = (char)9;

    private const int MaxLength = 31;

    // gVKTextWidth: gVKRight - gVKLeft - 4 - 4 - 10 = 530 - 128 - 18.
    private const float TextWidthLimit = 384f;

    private readonly FrontEnd _frontEnd;
    private readonly (char Token, int Width)[][][] _keys = BuildLayout();

    public VirtualKeyboardPage(FrontEnd frontEnd)
    {
        _frontEnd = frontEnd;
        TrapHandler = Trap;
    }

    /// <summary><c>m_strData</c>: the name being edited.</summary>
    public string Name { get; private set; } = "";

    /// <summary><c>m_iPos</c>: the text cursor.</summary>
    public int Position { get; private set; }

    /// <summary><c>m_Fresh</c>: the default name is untouched, so the first character replaces it.</summary>
    public bool Fresh { get; private set; }

    public int Mode { get; private set; }
    public int Row { get; private set; }
    public int Column { get; private set; }
    public float ColumnFraction { get; private set; }
    public bool ShiftOn { get; private set; }
    public bool CapsLockOn { get; private set; }

    /// <summary>The name handed to the save page (<c>mFEPSaveGame.mSaveGameName</c>) when Done is chosen.</summary>
    public string? AcceptedName { get; private set; }

    /// <summary>The font's metrics, from the host; needed only to type characters.</summary>
    public IFontMetrics? Font { get; set; }

    /// <summary><c>VKKeyTrap</c> (0x0051feb0), the key trap the page installs while it is active.</summary>
    public Func<int, int, bool> TrapHandler { get; }

    /// <summary><c>CFEPVirtualKeyboard::Init</c> (0x0051ff90), ending with <c>InitKeyboardLayout</c>'s row 1, column 1.</summary>
    public override bool Init()
    {
        ColumnFraction = 0.5f;
        CapsLockOn = false;
        ShiftOn = false;
        Position = 0;
        Mode = 0;
        Row = 1;
        Column = 1;
        return true;
    }

    /// <summary><c>MakeDefaultName</c>: "BEA 1", or the first "BEA n" (n up to 4,096) no save has, compared exactly.</summary>
    public void SetDefaultName()
    {
        Name = "BEA 1";
        for (int n = 2; n <= 0x1000; n++)
        {
            bool clash = false;
            for (int i = 0; i < _frontEnd.MemoryCard.NumSaves; i++)
            {
                if (string.Equals(_frontEnd.MemoryCard.GetSaveName(i), Name, StringComparison.Ordinal))
                {
                    clash = true;
                    break;
                }
            }
            if (!clash) break;
            Name = $"BEA {n}";
        }
        Position = Math.Min(Name.Length, MaxLength);
        Fresh = true;
    }

    public void NameUsed() => Fresh = false;

    /// <summary><c>CFEPVirtualKeyboard::TransitionNotification</c> (0x00520130): entering from the device, save list or main page.</summary>
    public override void TransitionNotification(FrontEndPage from)
    {
        if (from is not (FrontEndPage.DevSelect or FrontEndPage.Directory or FrontEndPage.Main)) return;
        if (Fresh) SetDefaultName();
        Mode = 0;
        Row = 0;
        Column = 0;
        ColumnFraction = 0.5f;
        Position = Math.Min(Name.Length, MaxLength);
    }

    /// <summary><c>CFEPVirtualKeyboard::Process</c> (0x005202d0): the key trap is installed while active and removed otherwise.</summary>
    public override void Process(FrontEndPageState state)
    {
        PlatformInput platform = _frontEnd.Platform;
        if (state == FrontEndPageState.Active)
        {
            if (platform.KeyTrap != TrapHandler) platform.KeyTrap = TrapHandler;
            // The PC's save folder is always present, so the "card removed" complaint never shows.
        }
        else if (platform.KeyTrap == TrapHandler)
        {
            platform.KeyTrap = null;
        }
    }

    /// <summary><c>CFEPVirtualKeyboard::Shutdown</c> (0x0051ffd0).</summary>
    public void Shutdown()
    {
        if (_frontEnd.Platform.KeyTrap == TrapHandler) _frontEnd.Platform.KeyTrap = null;
    }

    private bool Trap(int key, int keyEvent)
    {
        if (keyEvent == KeyTrapEvent.Down)
        {
            if (key is Dik.Up or Dik.Left) HandleKey(Left);
            else if (key is Dik.Down or Dik.Right) HandleKey(Right);
            else if (key == Dik.Escape) ButtonPressed(FrontEndButtons.MenuBack, 1f);
            else if (key is Dik.Return or Dik.NumpadEnter)
            {
                _frontEnd.PlaySound(FrontEndSound.Select);
                HandleKey(Done);
            }
            else if (key == Dik.Back) HandleKey(Backspace);
        }
        else if (keyEvent == KeyTrapEvent.Char)
        {
            if (RequireFont().CanDisplay((char)key)) HandleKey((char)key);
        }
        return false;
    }

    private IFontMetrics RequireFont() =>
        Font ?? throw new NotSupportedException("Typing a name needs the font's metrics; the host must set Font.");

    /// <summary><c>CFEPVirtualKeyboard::ButtonPressed</c> (0x00520370).</summary>
    public override void ButtonPressed(int button, float value)
    {
        switch (button)
        {
            case FrontEndButtons.MenuSelect:
                _frontEnd.PlaySound(FrontEndSound.Select);
                HandleKey(_keys[Mode][Row][Column].Token);
                break;
            case FrontEndButtons.MenuBack:
                _frontEnd.PlaySound(FrontEndSound.Back);
                _frontEnd.SetPage(FrontEndPage.Main, 70);
                break;
            case FrontEndButtons.MenuUp:
                MoveToRow(Row == 0 ? 4 : Row - 1);
                break;
            case FrontEndButtons.MenuDown:
                MoveToRow(Row == 4 ? 0 : Row + 1);
                break;
            case FrontEndButtons.MenuLeft:
                ColumnFraction = 0.5f;
                do
                {
                    if (Column == 0)
                    {
                        while (_keys[Mode][Row][Column + 1].Token != 0) Column++;
                    }
                    else
                    {
                        Column--;
                    }
                } while (IsKeyDisabled());
                break;
            case FrontEndButtons.MenuRight:
                ColumnFraction = 0.5f;
                do
                {
                    if (_keys[Mode][Row][Column + 1].Token == 0) Column = 0;
                    else Column++;
                } while (IsKeyDisabled());
                break;
        }
    }

    /// <summary><c>CFEPVirtualKeyboard::HandleKey</c> (0x00520cc0): a character is inserted at the cursor; a token is a command.</summary>
    public void HandleKey(char key)
    {
        if (key >= 0x20)
        {
            if (Name.Length >= MaxLength) return;
            if (Fresh)
            {
                Name = "";
                Position = 0;
                Fresh = false;
            }
            IFontMetrics font = RequireFont();
            if (font.TextWidth(Name) < TextWidthLimit)
            {
                char ch = key;
                if (!font.CanDisplay(key))
                {
                    ch = key switch
                    {
                        (char)0xca or (char)0xcb => 'E',
                        (char)0xce or (char)0xcf => 'I',
                        (char)0xd4 => 'O',
                        (char)0xdb => 'U',
                        _ => key,
                    };
                }
                Name = Name.Insert(Position, ch.ToString());
                Position++;
            }
            ShiftOn = false;
            return;
        }
        switch (key)
        {
            case Backspace:
                if (Fresh)
                {
                    Name = "";
                    Position = 0;
                    Fresh = false;
                    break;
                }
                if (Position > 0)
                {
                    Position--;
                    if (Position < Name.Length) Name = Name.Remove(Position, 1);
                }
                break;
            case Shift:
                ShiftOn = !ShiftOn;
                break;
            case CapsLock:
                CapsLockOn = !CapsLockOn;
                break;
            case Alphabet:
                Mode = 0;
                break;
            case Symbols:
                Mode = 1;
                break;
            case Accents:
                Mode = 2;
                break;
            case Left:
                Fresh = false;
                if (Position > 0) Position--;
                break;
            case Right:
                Fresh = false;
                if (Position < Name.Length && Position < MaxLength) Position++;
                break;
            case Done:
                AcceptedName = Name;
                _frontEnd.SaveGameName = Name;
                _frontEnd.SaveMode = true;
                _frontEnd.AutoSave = AutoSaveMode.Not;
                _frontEnd.SetPage(FrontEndPage.SaveGame, 20);
                break;
        }
    }

    /// <summary>
    /// <c>CFEPVirtualKeyboard::MoveToRow</c> (0x00520f70): the key under the same horizontal point on the new row,
    /// skipping disabled keys in the direction of travel. x87: the widths are exact; the products, sums and quotients
    /// round to 24 bits and are stored to floats.
    /// </summary>
    private void MoveToRow(int row)
    {
        while (true)
        {
            int oldRow = Row;
            float x = 0f;
            int c;
            for (c = 0; c < Column; c++) x = (float)X87.Add(x, _keys[Mode][Row][c].Width);
            float target = (float)X87.Add(x, X87.Mul(_keys[Mode][Row][c].Width, ColumnFraction));
            float left = 0f;
            Row = row;
            Column = 0;
            bool found = false;
            while (_keys[Mode][Row][Column].Token != 0)
            {
                float width = _keys[Mode][Row][Column].Width;
                float right = (float)X87.Add(left, width);
                if (target <= right)
                {
                    ColumnFraction = (float)X87.Div(X87.Sub(target, left), width);
                    found = true;
                    break;
                }
                left = right;
                Column++;
            }
            if (!found)
            {
                ColumnFraction = 0.5f;
                Column--;
            }
            if (!IsKeyDisabled()) return;
            int dir = Row - oldRow;
            if (dir != -1 && dir != 1) dir = dir < -1 ? 1 : -1;
            int next = Row + dir;
            if (next < 0) next = 4;
            else if (next >= 5) next = 0;
            row = next;
        }
    }

    /// <summary><c>CFEPVirtualKeyboard::IsKeyDisabled</c> (0x005214d0): shift and caps lock on the symbols; Done for an empty or blank name.</summary>
    private bool IsKeyDisabled()
    {
        char token = _keys[Mode][Row][Column].Token;
        if (Mode == 1 && (token == Shift || token == CapsLock)) return true;
        if (token == Done) return Name.All(ch => ch == ' ');
        return false;
    }

    /// <summary><c>InitKeyboardLayout</c> (0x00520530): three layouts of five rows; a zero token ends a row.</summary>
    private static (char, int)[][][] BuildLayout()
    {
        static (char, int)[] Row(params (int Token, int Width)[] keys) =>
            [.. keys.Select(k => ((char)k.Token, k.Width)), ((char)0, 0)];
        static (int, int)[] Run(string characters, int width = 34) => [.. characters.Select(ch => ((int)ch, width))];
        static (int, int)[] Codes(params int[] characters) => [.. characters.Select(ch => (ch, 34))];

        return
        [
            [
                Row([(9, 166), .. Run("1234567890")]),
                Row([(4, 166), .. Run("ABCDEFGHIJ")]),
                Row([(5, 166), .. Run("KLMNOPQRST")]),
                Row([(7, 166), .. Run("UVWXYZ"), (3, 136)]),
                Row((8, 166), (' ', 204), (1, 68), (2, 68)),
            ],
            [
                Row([(9, 166), .. Run("!\"#%&'()*+")]),
                Row([(4, 166), .. Run(",-./:;<=>?")]),
                Row([(5, 166), .. Codes(0xbf, 0xa1, 0xa9, 0xae, 0x2122, 0xab, 0xbb, 0xba, 0xaa)]),
                Row((6, 166), (3, 136)),
                Row((8, 166), (' ', 204), (1, 68), (2, 68)),
            ],
            [
                Row([(9, 166), .. Run("1234567890")]),
                Row([(4, 166), .. Codes(0xe1, 0xe0, 0xe2, 0xe4, 0xe7, 0xe8, 0xe9, 0xea, 0xeb, 0xed)]),
                Row([(5, 166), .. Codes(0xec, 0xee, 0xef, 0xf3, 0xf2, 0xf4, 0xf6, 0x153, 0xf1, 0xfa)]),
                Row([(6, 166), .. Codes(0xf9, 0xfb, 0xfc, 0xdf), (3, 136)]),
                Row((7, 166), (' ', 204), (1, 68), (2, 68)),
            ],
        ];
    }
}
