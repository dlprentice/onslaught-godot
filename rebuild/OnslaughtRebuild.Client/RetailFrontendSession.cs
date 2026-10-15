// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Client;

/// <summary>
/// Presentation-owned state for the bounded released-frontend path into Level 100.
/// Gameplay state remains exclusively owned by <see cref="InteractiveSession"/>.
/// </summary>
public sealed class RetailFrontendSession
{
    // Steam-drawn order (CFEPMain__Update / icons): New, Continue, Load,
    // Multiplayer, Goodies, Options, Quit. DoAction pages for sel 3/4/5 are
    // crossed (Options/MP/Goodies) — route by index when those pages land.
    // Index 6: Quit label; DoAction opens FEMessBox with Localization 0xe4.
    private static readonly RetailFrontendMenuItem[] MainMenuItems =
    [
        // IsAvailable models RETAIL's availability, which drives the draw colour -
        // it is not a statement about which pages this reconstruction implements.
        // Measured from the pristine 640x480 main-menu capture: only Continue Game
        // is drawn dim (no career in progress); Load Game, Multiplayer, Goodies and
        // Options are drawn in the normal bright colour exactly like Quit.
        // Multiplayer and Goodies still fall through to None. Load Game now
        // enters the injected read-only career-list mode below.
        new(RetailFrontendMenuItemKind.NewGame, IsAvailable: true),
        new(RetailFrontendMenuItemKind.ContinueGame, IsAvailable: false),
        new(RetailFrontendMenuItemKind.LoadGame, IsAvailable: true),
        new(RetailFrontendMenuItemKind.Multiplayer, IsAvailable: true),
        new(RetailFrontendMenuItemKind.Goodies, IsAvailable: true),
        new(RetailFrontendMenuItemKind.Options, IsAvailable: true),
        new(RetailFrontendMenuItemKind.Quit, IsAvailable: true),
    ];


    // Level 100's WorldHeaders record names exactly one configuration, so its
    // page-list index is 0; Aquila Prototype is separately catalog record 3 in
    // data/battle engine configurations.dat. The
    // shorter display strings are independently visible in the pristine
    // SELECT CONFIGURATION frame. Keeping both prevents UI text from being
    // mistaken for the underlying weapon-record names.
    private static readonly RetailFrontendBattleEngineConfiguration[] Level100Configurations =
    [
        new(
            CatalogRecordIndex: 3,
            AuthoredName: "Aquila Prototype",
            DisplayName: "BE:A Unit-00 'Prototype'",
            WalkerPrimary: new("Pulse Cannon Pod", "Pulse Cannon"),
            WalkerSecondary: new("Mech Twin Vulcan Cannon", "Vulcan Cannon"),
            JetPrimary: new("Mech Vulcan Cannon", "Vulcan Cannon"),
            JetSecondary: new("Missile Pod", "Micro Missiles")),
    ];

    /// <summary>
    /// Default game name offered by the page. Retail's pristine 640x480
    /// FEP_DEVSELECT capture shows "BEA 1" pre-filled and highlighted in the
    /// name field with no career selected
    /// (local-lab/retail-reference-pristine/choose-game-name/choose-game-name-640x480.png).
    /// </summary>
    public const string DefaultGameName = "BEA 1";

    /// <summary>520cc0 checks the existing UTF-16 length before editing.</summary>
    public const int MaxGameNameLength = 31;
    public const int GameNameWidthLimit = 384;
    public int GameNameCursor { get; private set; } = DefaultGameName.Length;
    public bool GameNameIsFresh { get; private set; } = true;

    private bool _level100LaunchPending;
    private RetailCareerDescriptor? _activeLoadedCareer;
    private RetailCareerDescriptor? _selectedCareerLoadRequest;
    private readonly IReadOnlyList<RetailCareerDescriptor> _careerDescriptors;
    private readonly IReadOnlyList<string> _careerNames;
    private string _gameName = DefaultGameName;
    private int _selectedConfigurationIndex;

    /// <summary>
    /// Creates the presentation state over caller-supplied, already-read career
    /// descriptors. Storage discovery and byte reads remain outside Client.
    /// Input order is retained as the storage/page order.
    /// </summary>
    public RetailFrontendSession(IEnumerable<RetailCareerDescriptor>? careerDescriptors = null)
    {
        RetailCareerDescriptor[] descriptors = careerDescriptors?.ToArray() ?? [];
        _careerDescriptors = Array.AsReadOnly(descriptors);
        _careerNames = Array.AsReadOnly(descriptors.Select(descriptor => descriptor.Name).ToArray());
    }

    /// <summary>
    /// The career world the level selector has chosen and the launch request
    /// will carry. Bounded by <see cref="RetailWorldCatalog"/> admission: the
    /// released selector only offers worlds an incoming completed link has
    /// unlocked, so this value is always constructible-by-law even when the
    /// reconstruction cannot yet build that world's content.
    /// </summary>
    public int SelectedWorldNumber { get; private set; } = RetailWorldCatalog.RootWorldNumber;

    /// <summary>
    /// The released selector name row of <see cref="SelectedWorldNumber"/>
    /// ("1.00 - Training Level", "1.10 - Blackout", …), from the pinned
    /// English language table (see
    /// <see cref="RetailFrontendWorldStrings"/>). The level-select name band
    /// and the briefing page both draw the SELECTED node's row; the band
    /// following the selection is measured-consistent with retail (PARITY.md,
    /// 2026-08-22) rather than source-proven — FEPLevelSelect.cpp is not in
    /// the source drop.
    /// </summary>
    public string SelectedLevelName =>
        RetailFrontendWorldStrings.LevelName(SelectedWorldNumber)
            ?? string.Empty;

    /// <summary>
    /// The released MISSION BRIEFING body paragraphs for
    /// <see cref="SelectedWorldNumber"/>, in authored order, empty when the
    /// language table carries no copy for that world. Callers must not fall
    /// back to another world's text.
    /// </summary>
    public IReadOnlyList<string> SelectedBriefingBody =>
        RetailFrontendWorldStrings.Briefing(SelectedWorldNumber);

    public RetailFrontendScreen Screen { get; private set; } = RetailFrontendScreen.ClickToStart;

    /// <summary>
    /// The active post-level page projection. Retail stores the source fields
    /// in <c>END_LEVEL_DATA</c>; Client keeps only the values its debriefing
    /// presenter consumes.
    /// </summary>
    public RetailDebriefingProjection? Debriefing { get; private set; }

    public int SelectedMainIndex { get; private set; }

    /// <summary>
    /// Active frontend language. data/language ships exactly five sets and
    /// Career.h defines NUM_LANGUAGES 5; CFrontEnd::Init caches all five and
    /// CFrontEnd::SetLanguage swaps the active one.
    /// </summary>
    public RetailFrontendLanguage Language { get; private set; } = RetailFrontendLanguage.English;

    /// <summary>Quit-confirm choice: 0 = No (safe default), 1 = Yes.</summary>
    public int SelectedQuitConfirmIndex { get; private set; }

    public RetailFrontendMenuItem SelectedMainItem => MainMenuItems[SelectedMainIndex];

    public IReadOnlyList<RetailFrontendMenuItem> Items => MainMenuItems;

    /// <summary>Number of exact released Level-100 configuration rows.</summary>
    public int ConfigurationCount => Level100Configurations.Length;

    public int SelectedConfigurationIndex => _selectedConfigurationIndex;

    public RetailFrontendBattleEngineConfiguration SelectedConfiguration =>
        Level100Configurations[_selectedConfigurationIndex];

    public RetailFrontendMenuItemKind? UnavailableSelection { get; private set; }

    /// <summary>Injected career descriptors in deterministic page order.</summary>
    public IReadOnlyList<RetailCareerDescriptor> CareerDescriptors => _careerDescriptors;

    /// <summary>Career rows drawn in the FEP_DEVSELECT list panel.</summary>
    public IReadOnlyList<string> CareerNames => _careerNames;

    public RetailFrontendCareerPageMode CareerPageMode { get; private set; } =
        RetailFrontendCareerPageMode.New;

    /// <summary>Highlighted career row, or -1 when no row is highlighted.</summary>
    public int SelectedCareerIndex { get; private set; } = -1;

    /// <summary>Editable contents of the FEP_DEVSELECT name field.</summary>
    public string GameName => _gameName;

    /// <summary>
    /// Moves the FEP_DEVSELECT highlight onto a career row, tracking the
    /// selection into the name field. Observed in the user's live retail frame:
    /// selecting a list entry replaces the field text with that entry.
    /// </summary>
    public bool SelectCareerIndex(int index)
    {
        if (Screen != RetailFrontendScreen.DevSelect ||
            index < 0 ||
            index >= CareerNames.Count ||
            index == SelectedCareerIndex)
        {
            return false;
        }

        SelectedCareerIndex = index;
        _gameName = CareerNames[index];
        GameNameCursor = Math.Min(_gameName.Length, MaxGameNameLength);
        GameNameIsFresh = true;
        return true;
    }

    // Charset/remap inputs 5db5fc/5db738; font0 is CDXBitmapFont.
    private const string ExtendedNameGlyphs = "áàâäçéèêëíìîïóòôöœñúùûüÁÀÄÂÇÉÈÍÌÑÓÒÖÜÚÙŒ¡¿©®™«»ºª\u001fßÊËÎÏÔÛ┐┌";

    public static int GameNameGlyphIndex(char character)
    {
        character = character switch { '\u2019' => '\'', '\u2013' => '-', _ => character };
        if ("$@[\\]^_{|}~\u007f".Contains(character)) return -1;
        if (character is >= ' ' and <= '\u007f') return character - ' ';
        int index = ExtendedNameGlyphs.IndexOf(character);
        return index < 0 || character == '\u001f' ? -1 : index + 96;
    }

    /// <summary>Bitmap-font remap. Font0 sets+168; Font13PS leaves it zero. Both leave+16c zero.</summary>
    public static int GameNameRenderGlyphIndex(char character, bool swapInvertedPunctuation)
    {
        char mapped = swapInvertedPunctuation
            ? character switch { '¡' => '¿', '¿' => '¡', _ => character }
            : character;
        int glyph = GameNameGlyphIndex(mapped);
        return glyph < 0 ? 145 : glyph;
    }

    /// <summary>Physical character event; width is the current font0 extent, including trailing spacing.</summary>
    public bool AppendGameNameCharacter(char character, int currentTextWidth)
    {
        if (Screen != RetailFrontendScreen.DevSelect ||
            CareerPageMode != RetailFrontendCareerPageMode.New ||
            GameNameGlyphIndex(character) < 0 || _gameName.Length >= MaxGameNameLength)
            return false;
        if (GameNameIsFresh)
        {
            _gameName = string.Empty;
            GameNameCursor = 0;
            GameNameIsFresh = false;
            currentTextWidth = 0;
        }
        if (currentTextWidth >= GameNameWidthLimit) return false;
        _gameName = _gameName.Insert(GameNameCursor, character.ToString());
        GameNameCursor++;
        return true;
    }

    public bool MoveGameNameCursor(bool right)
    {
        if (Screen != RetailFrontendScreen.DevSelect || CareerPageMode != RetailFrontendCareerPageMode.New)
            return false;
        bool changed = GameNameIsFresh;
        GameNameIsFresh = false;
        int next = Math.Clamp(GameNameCursor + (right ? 1 : -1), 0, Math.Min(_gameName.Length, 31));
        changed |= next != GameNameCursor;
        GameNameCursor = next;
        return changed;
    }

    public bool RemoveGameNameCharacter()
    {
        if (Screen != RetailFrontendScreen.DevSelect || CareerPageMode != RetailFrontendCareerPageMode.New)
            return false;
        if (GameNameIsFresh)
        {
            _gameName = string.Empty;
            GameNameCursor = 0;
            GameNameIsFresh = false;
            return true;
        }
        if (GameNameCursor == 0) return false;
        _gameName = _gameName.Remove(--GameNameCursor, 1);
        return true;
    }

    private void ResetGameName()
    {
        _gameName = DefaultGameName;
        GameNameCursor = _gameName.Length;
        GameNameIsFresh = true;
    }

    private void SeedGameName()
    {
        int suffix = 1;
        // 51fff0 checks candidates1..4095; candidate4096 is the unchecked cap.
        while (suffix < 4096 && CareerNames.Contains($"BEA {suffix}", StringComparer.Ordinal)) suffix++;
        _gameName = $"BEA {suffix}";
        GameNameCursor = Math.Min(_gameName.Length, MaxGameNameLength);
        GameNameIsFresh = true;
    }

    public bool MovePrevious()
    {
        if (Screen == RetailFrontendScreen.QuitConfirm)
        {
            if (SelectedQuitConfirmIndex == 0)
            {
                return false;
            }

            SelectedQuitConfirmIndex = 0;
            return true;
        }

        if (Screen == RetailFrontendScreen.DevSelect)
        {
            return SelectCareerIndex(SelectedCareerIndex - 1);
        }

        if (Screen == RetailFrontendScreen.SelectConfiguration)
        {
            return SelectConfigurationIndex(_selectedConfigurationIndex - 1);
        }

        if (Screen != RetailFrontendScreen.MainMenu || SelectedMainIndex == 0)
        {
            return false;
        }

        SelectedMainIndex--;
        UnavailableSelection = null;
        return true;
    }

    public bool MoveNext()
    {
        if (Screen == RetailFrontendScreen.QuitConfirm)
        {
            if (SelectedQuitConfirmIndex == 1)
            {
                return false;
            }

            SelectedQuitConfirmIndex = 1;
            return true;
        }

        if (Screen == RetailFrontendScreen.DevSelect)
        {
            return SelectCareerIndex(SelectedCareerIndex + 1);
        }

        if (Screen == RetailFrontendScreen.SelectConfiguration)
        {
            return SelectConfigurationIndex(_selectedConfigurationIndex + 1);
        }

        if (Screen != RetailFrontendScreen.MainMenu || SelectedMainIndex == MainMenuItems.Length - 1)
        {
            return false;
        }

        SelectedMainIndex++;
        UnavailableSelection = null;
        return true;
    }

    public bool SelectMainIndex(int index)
    {
        if (Screen != RetailFrontendScreen.MainMenu ||
            index < 0 ||
            index >= MainMenuItems.Length ||
            index == SelectedMainIndex)
        {
            return false;
        }

        SelectedMainIndex = index;
        UnavailableSelection = null;
        return true;
    }

    public bool SelectQuitConfirmIndex(int index)
    {
        if (Screen != RetailFrontendScreen.QuitConfirm ||
            index is < 0 or > 1 ||
            index == SelectedQuitConfirmIndex)
        {
            return false;
        }

        SelectedQuitConfirmIndex = index;
        return true;
    }

    public bool SelectConfigurationIndex(int index)
    {
        if (Screen != RetailFrontendScreen.SelectConfiguration ||
            index < 0 ||
            index >= Level100Configurations.Length ||
            index == _selectedConfigurationIndex)
        {
            return false;
        }

        _selectedConfigurationIndex = index;
        return true;
    }

    /// <summary>
    /// Selects the world the level selector highlights.
    ///
    /// <para>Admission is the released law, not a reconstruction preference:
    /// <see cref="RetailWorldCatalog.IsWorldSelectable"/> accepts a world only
    /// when a completed incoming link unlocks it (or it is the root). The
    /// selector's default is retail's cold-career default, the root world.
    /// </para>
    /// </summary>
    public bool SelectWorld(int worldNumber)
    {
        bool selectable = _activeLoadedCareer is not null
            ? _activeLoadedCareer.Career.IsWorldSelectable(worldNumber)
            : RetailWorldCatalog.IsWorldSelectable(Career, worldNumber);
        if (Screen != RetailFrontendScreen.LevelSelect ||
            worldNumber == SelectedWorldNumber ||
            !selectable)
        {
            return false;
        }

        SelectedWorldNumber = worldNumber;
        return true;
    }

    /// <summary>
    /// The career graph the selection is admitted against. The Level 100
    /// mission owns the only live <see cref="RetailCareerCampaign"/>, but the
    /// selector must answer before any world is constructed — retail's own
    /// selector reads the loaded career, which on a cold start is the cold
    /// slice. This is that cold slice, shared and read-only in practice.
    /// </summary>
    public RetailCareerCampaign Career { get; } =
        RetailCareerReCalcLinks.CreateColdTrainingSlice();

    public RetailFrontendSignal Confirm()
    {
        UnavailableSelection = null;

        switch (Screen)
        {
            case RetailFrontendScreen.ClickToStart:
                Screen = RetailFrontendScreen.MainMenu;
                SelectedMainIndex = 0;
                return RetailFrontendSignal.PageChanged;

            case RetailFrontendScreen.MainMenu:
                if (!SelectedMainItem.IsAvailable)
                {
                    UnavailableSelection = SelectedMainItem.Kind;
                    return RetailFrontendSignal.Unavailable;
                }

                if (SelectedMainItem.Kind == RetailFrontendMenuItemKind.NewGame)
                {
                    // Retail's New Game entry goes to FEP_DEVSELECT, not level
                    // select: CFrontEnd::Init drives the "new career" entry with
                    // SetPage(FEP_DEVSELECT, 0) (references/Onslaught/FrontEnd.cpp:182),
                    // and the pristine 640x480 capture of the page that follows
                    // New Game is the "CHOOSE GAME NAME" screen.
                    Screen = RetailFrontendScreen.DevSelect;
                    CareerPageMode = RetailFrontendCareerPageMode.New;
                    _activeLoadedCareer = null;
                    _selectedCareerLoadRequest = null;
                    SelectedWorldNumber = RetailWorldCatalog.RootWorldNumber;
                    SelectedCareerIndex = -1;
                    SeedGameName();
                    return RetailFrontendSignal.PageChanged;
                }

                if (SelectedMainItem.Kind == RetailFrontendMenuItemKind.LoadGame)
                {
                    // CFEPLoadGame::Init clears mSaveGameNumber to -1
                    // (FEPLoadGame.cpp:12-20). The caller has already supplied
                    // descriptors from an explicit, app-safe read boundary;
                    // Client owns only their deterministic page order.
                    Screen = RetailFrontendScreen.DevSelect;
                    CareerPageMode = RetailFrontendCareerPageMode.Load;
                    _activeLoadedCareer = null;
                    _selectedCareerLoadRequest = null;
                    SelectedCareerIndex = -1;
                    ResetGameName();
                    return RetailFrontendSignal.PageChanged;
                }

                if (SelectedMainItem.Kind == RetailFrontendMenuItemKind.Quit)
                {
                    Screen = RetailFrontendScreen.QuitConfirm;
                    SelectedQuitConfirmIndex = 0;
                    return RetailFrontendSignal.PageChanged;
                }

                if (SelectedMainItem.Kind == RetailFrontendMenuItemKind.Options)
                {
                    // CFEPMain__DoAction case 5 -> SetPage(0x11, 0x46), UNGATED -
                    // unlike Continue/Load, which test mCareerInProgress.
                    Screen = RetailFrontendScreen.Options;
                    return RetailFrontendSignal.PageChanged;
                }

                return RetailFrontendSignal.None;

            case RetailFrontendScreen.QuitConfirm:
                if (SelectedQuitConfirmIndex == 0)
                {
                    Screen = RetailFrontendScreen.MainMenu;
                    return RetailFrontendSignal.PageChanged;
                }

                return RetailFrontendSignal.ExitRequested;

            case RetailFrontendScreen.DevSelect:
                if (CareerPageMode == RetailFrontendCareerPageMode.Load)
                {
                    if (SelectedCareerIndex < 0)
                    {
                        UnavailableSelection = RetailFrontendMenuItemKind.LoadGame;
                        return RetailFrontendSignal.Unavailable;
                    }

                    _activeLoadedCareer = _careerDescriptors[SelectedCareerIndex];
                    _selectedCareerLoadRequest = _activeLoadedCareer;
                    SelectedWorldNumber = _activeLoadedCareer.Career.SuggestedWorldNumber;
                    Screen = RetailFrontendScreen.LevelSelect;
                    return RetailFrontendSignal.CareerLoadRequested;
                }

                Screen = RetailFrontendScreen.LevelSelect;
                return RetailFrontendSignal.PageChanged;

            case RetailFrontendScreen.LevelSelect:
                // Retail does NOT go from level select to loading. The pristine
                // 640x480 capture chain taken 2026-07-25 records
                // SELECT LEVEL -> MISSION BRIEFING -> SELECT CONFIGURATION ->
                // LOADING (local-lab/STARTUP-FLOW-FINDINGS-2026-07-25.md), with a
                // reference frame for each of the three intermediate pages.
                Screen = RetailFrontendScreen.MissionBriefing;
                return RetailFrontendSignal.PageChanged;

            case RetailFrontendScreen.Debriefing:
                Debriefing = null;
                Screen = RetailFrontendScreen.LevelSelect;
                return RetailFrontendSignal.PageChanged;

            case RetailFrontendScreen.MissionBriefing:
                _selectedConfigurationIndex = 0;
                Screen = RetailFrontendScreen.SelectConfiguration;
                return RetailFrontendSignal.PageChanged;

            case RetailFrontendScreen.SelectConfiguration:
                Screen = RetailFrontendScreen.Loading;
                _level100LaunchPending = true;
                return RetailFrontendSignal.LevelLaunchRequested;

            default:
                return RetailFrontendSignal.None;
        }
    }

    public RetailFrontendSignal Back()
    {
        UnavailableSelection = null;

        if (Screen == RetailFrontendScreen.QuitConfirm)
        {
            Screen = RetailFrontendScreen.MainMenu;
            return RetailFrontendSignal.PageChanged;
        }

        if (Screen == RetailFrontendScreen.DevSelect)
        {
            Screen = RetailFrontendScreen.MainMenu;
            CareerPageMode = RetailFrontendCareerPageMode.New;
            _activeLoadedCareer = null;
            _selectedCareerLoadRequest = null;
            SelectedCareerIndex = -1;
            ResetGameName();
            return RetailFrontendSignal.PageChanged;
        }

        if (Screen == RetailFrontendScreen.Options)
        {
            // Leaving the frontend Options page is where retail writes
            // defaultoptions.bea (CFEPOptions__SaveDefaultOptions 0x0051F500).
            // This lane persists nothing, so the write has no counterpart here.
            Screen = RetailFrontendScreen.MainMenu;
            return RetailFrontendSignal.PageChanged;
        }

        if (Screen == RetailFrontendScreen.SelectConfiguration)
        {
            Screen = RetailFrontendScreen.MissionBriefing;
            return RetailFrontendSignal.PageChanged;
        }

        if (Screen == RetailFrontendScreen.MissionBriefing)
        {
            Screen = RetailFrontendScreen.LevelSelect;
            return RetailFrontendSignal.PageChanged;
        }

        if (Screen == RetailFrontendScreen.Debriefing)
        {
            Debriefing = null;
            Screen = RetailFrontendScreen.LevelSelect;
            return RetailFrontendSignal.PageChanged;
        }

        if (Screen != RetailFrontendScreen.LevelSelect)
        {
            return RetailFrontendSignal.None;
        }

        Screen = RetailFrontendScreen.DevSelect;
        return RetailFrontendSignal.PageChanged;
    }

    public bool ConsumeLevel100LaunchRequest()
    {
        if (!_level100LaunchPending)
        {
            return false;
        }

        _level100LaunchPending = false;
        return true;
    }

    /// <summary>
    /// Consumes the selected descriptor once. This mirrors the source page's
    /// copy of <c>mSaveGameNumber</c> and <c>mSaveGameName</c> before its storage
    /// transaction (<c>FEPLoadGame.cpp:128-153</c>) without putting filesystem
    /// work in Client.
    /// </summary>
    public RetailCareerDescriptor? ConsumeSelectedCareerLoadRequest()
    {
        RetailCareerDescriptor? request = _selectedCareerLoadRequest;
        _selectedCareerLoadRequest = null;
        return request;
    }

    /// <summary>The world the pending launch request will construct.</summary>
    public int ConsumeLaunchWorldNumber => SelectedWorldNumber;

    /// <summary>
    /// Whether the host can currently play <see cref="SelectedWorldNumber"/>.
    /// World 110 is selectable after a Won update, and Core and
    /// <see cref="InteractiveSession"/> construct it with its carry-over
    /// (<see cref="SelectedWorldLostBaseRows"/>), but the Godot host has no
    /// World 110 presentation yet, so only world 100 is played.
    /// </summary>
    public bool SelectedWorldIsConstructible =>
        SelectedWorldNumber == RetailWorldCatalog.RootWorldNumber;

    /// <summary>
    /// The base-world rows the selected world's load skips: the career's
    /// carry-over from the levels before it
    /// (<see cref="RetailCareerNodeTable.LostBaseRows"/>).
    /// </summary>
    public IReadOnlyList<int> SelectedWorldLostBaseRows =>
        Career.Nodes.LostBaseRows(SelectedWorldNumber, RetailFillOutEndLevelData.Level100BaseWorldThingCount);

    public void CompleteLevel100Load()
    {
        if (Screen != RetailFrontendScreen.Loading || _level100LaunchPending)
        {
            throw new InvalidOperationException(
                "Level 100 can complete only after its pending launch request is consumed.");
        }

        // Reaching gameplay without BeginLevel100IntroCutscene means the
        // released intro was suppressed or unavailable. Either way the first
        // round has been consumed; an in-level Retry must not replay it.
        Level100IntroCutscenePending = false;
        Screen = RetailFrontendScreen.Gameplay;
    }

    /// <summary>
    /// Whether retail would still play this level's intro cutscene.
    ///
    /// This is <c>CGame::mFirstTimeRound</c>. <c>CGame::GetIntroFMV</c> opens
    /// with <c>if (!mFirstTimeRound) return -1;</c>
    /// (<c>references/Onslaught/game.cpp:1105-1106</c>), and the restart loop
    /// sets the flag TRUE when the level is entered
    /// (<c>game.cpp:1607</c>) and FALSE at the bottom of each iteration
    /// (<c>game.cpp:1691</c>). So the cutscene plays on entering the level from
    /// the frontend and NOT on an in-level Retry — which is exactly the
    /// distinction between <see cref="RestartLevel100"/> and
    /// <see cref="ReturnToMainMenu"/> here.
    /// </summary>
    public bool Level100IntroCutscenePending { get; private set; } = true;

    /// <summary>
    /// Enters the intro cutscene once the level is loaded. Retail reaches this
    /// point with the loading screen already dismissed
    /// (<c>CONSOLE.SetLoading(FALSE)</c>, <c>game.cpp:1339</c>).
    ///
    /// The caller owns the decision that a cutscene is actually AVAILABLE —
    /// whether the clip was decoded, and whether retail's own
    /// <c>CLIPARAMS.mSkipFMV</c> gate applies. This method owns only the
    /// once-per-entry rule.
    /// </summary>
    public void BeginLevel100IntroCutscene()
    {
        if (Screen != RetailFrontendScreen.Loading || _level100LaunchPending)
        {
            throw new InvalidOperationException(
                "The intro cutscene can begin only after Level 100 has loaded.");
        }

        if (!Level100IntroCutscenePending)
        {
            throw new InvalidOperationException(
                "Retail plays a level's intro cutscene only on the first time round.");
        }

        Level100IntroCutscenePending = false;
        Screen = RetailFrontendScreen.IntroCutscene;
    }

    /// <summary>
    /// Hands the screen from the cutscene to gameplay, whether it played out or
    /// the player aborted it. Retail treats both identically: <c>PlayFullscreen</c>
    /// returns and the load resumes (<c>game.cpp:1342-1345</c>).
    /// </summary>
    public void CompleteLevel100IntroCutscene()
    {
        if (Screen != RetailFrontendScreen.IntroCutscene)
        {
            throw new InvalidOperationException(
                "There is no intro cutscene to complete.");
        }

        Screen = RetailFrontendScreen.Gameplay;
    }

    /// <summary>
    /// Restarts the bounded Level 100 run from a pause owned by the gameplay
    /// presenter. Pause is intentionally not a second frontend state machine:
    /// it leaves this lifecycle in Gameplay.
    /// </summary>
    public RetailFrontendSignal RestartLevel100()
    {
        RequireLevel100Transition(nameof(RestartLevel100));
        Screen = RetailFrontendScreen.Loading;
        _level100LaunchPending = true;
        return RetailFrontendSignal.LevelLaunchRequested;
    }

    /// <summary>
    /// Leaves a gameplay-owned pause for the existing startup/main-menu shell.
    /// </summary>
    public RetailFrontendSignal LeaveLevel100ForMainMenu()
    {
        RequireLevel100Transition(nameof(LeaveLevel100ForMainMenu));
        ReturnToMainMenu();
        return RetailFrontendSignal.ReturnToMainMenuRequested;
    }

    /// <summary>
    /// The post-Won frontend re-entry. Retail's PC <c>CFrontEnd::Init</c>
    /// applies <c>CCareer::Update</c> before page initialization and lands on
    /// <c>FEP_DEBRIEFING</c> (<c>FrontEnd.cpp:67,233-279</c>). The page's
    /// TransitionNotification then reads-and-clears the two goodie latches.
    /// This keeps that ordering: the selector is already unlocked while the
    /// debriefing page is visible, but it cannot be selected until the player
    /// leaves the page. <c>SetCurrentLevelToHighestAvailable</c> is not in the
    /// source drop and is not invented here: the highlight stays on the root.
    /// </summary>
    /// <summary>
    /// A mission's <c>SetSlotSave</c>: <c>CCareer::SetSlot</c> on the career
    /// at once (<c>0x00533900</c>), before any win.
    /// </summary>
    public void SaveSlot(Level100TutorialSlotSaved saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        RetailSetSlotSave.PersistCareerSlot(Career.Slots, saved.Slot, saved.Value);
    }

    /// <param name="baseThingsLeft">
    /// FillOut's base-world survivor list from the level's end state
    /// (<see cref="RetailFillOutEndLevelData.BaseThingsLeft"/>); a first play
    /// that loses no row when omitted.
    /// </param>
    public bool TryAcceptWonHandoff(
        Level100MissionOutcome outcome,
        Level100MissionTerminalState terminalState,
        IReadOnlyList<int>? baseThingsLeft = null)
    {
        if (Screen != RetailFrontendScreen.Gameplay ||
            Level100WonCareerHandoff.TryApply(Career, outcome, terminalState, baseThingsLeft) is not { } snapshot)
        {
            return false;
        }

        Debriefing = RetailDebriefingProjection.From(
            snapshot,
            Career.Counters.GetAndResetGoodieNewCount(),
            Career.Counters.GetAndResetFirstGoodie());
        _level100LaunchPending = false;
        _selectedConfigurationIndex = 0;
        Level100IntroCutscenePending = true;
        Screen = RetailFrontendScreen.Debriefing;
        return true;
    }

    /// <summary>
    /// Backs out of Loading when the selected world is admitted by career
    /// law but this reconstruction cannot yet construct it. The launch
    /// request must already have been consumed.
    /// </summary>
    public bool ReturnUnconstructibleLaunchToLevelSelect()
    {
        if (Screen != RetailFrontendScreen.Loading || _level100LaunchPending)
        {
            return false;
        }

        Level100IntroCutscenePending = true;
        Screen = RetailFrontendScreen.LevelSelect;
        return true;
    }

    private void RequireLevel100Transition(string operation)
    {
        if (Screen != RetailFrontendScreen.Gameplay)
        {
            throw new InvalidOperationException(
                $"{operation} requires an active Level 100 lifecycle.");
        }
    }

    private void ReturnToMainMenu()
    {
        SelectedMainIndex = 0;
        SelectedQuitConfirmIndex = 0;
        UnavailableSelection = null;
        SelectedCareerIndex = -1;
        CareerPageMode = RetailFrontendCareerPageMode.New;
        _activeLoadedCareer = null;
        _selectedCareerLoadRequest = null;
        _selectedConfigurationIndex = 0;
        Debriefing = null;
        ResetGameName();
        _level100LaunchPending = false;
        // Leaving the level ends CGame's restart loop. The next entry runs
        // RestartLoopRunLevel afresh, which sets mFirstTimeRound TRUE again
        // (references/Onslaught/game.cpp:1607), so the intro cutscene is armed
        // once more. RestartLevel100 deliberately does NOT do this: an in-level
        // Retry stays inside the same loop, where the flag is already FALSE.
        Level100IntroCutscenePending = true;
        Screen = RetailFrontendScreen.MainMenu;
    }
}

public sealed record RetailFrontendWeaponConfiguration(
    string AuthoredName,
    string DisplayName);

public sealed record RetailFrontendBattleEngineConfiguration(
    int CatalogRecordIndex,
    string AuthoredName,
    string DisplayName,
    RetailFrontendWeaponConfiguration WalkerPrimary,
    RetailFrontendWeaponConfiguration WalkerSecondary,
    RetailFrontendWeaponConfiguration JetPrimary,
    RetailFrontendWeaponConfiguration JetSecondary);

/// <summary>
/// Frontend identity supplied alongside an already-read career. Stuart's
/// <c>CFEPLoadGame</c> carries both <c>mSaveGameNumber</c> and
/// <c>mSaveGameName</c> (<c>FEPLoadGame.h:32-35</c>); neither lives in the
/// serialized <c>CCareer</c> bytes.
/// </summary>
public sealed record RetailCareerDescriptor(
    int? SlotNumber,
    string Name,
    RetailCareerSave Career);

public enum RetailFrontendCareerPageMode
{
    New,
    Load,
}

public enum RetailFrontendScreen
{
    ClickToStart,
    MainMenu,
    QuitConfirm,

    /// <summary>
    /// Retail FEP_DEVSELECT — the "CHOOSE GAME NAME" page New Game enters
    /// (references/Onslaught/FrontEnd.cpp:120/182/782). The same composed list
    /// surface carries injected read-only Load Game descriptors; no discovery,
    /// save write, or career persistence occurs here.
    /// </summary>
    DevSelect,

    /// <summary>
    /// Retail FEP_OPTIONS (page 0x11, vtable 0x005DB8A8), reached from the main
    /// menu's Options entry. CFEPOptions uses the pause-menu initializer and
    /// render implementation, lazily retaining a frontend-specific context and
    /// resetting its root session on each entry. The in-game pause menu is a
    /// distinct runtime instance of the same widget implementation.
    ///
    /// Reference frames: local-lab/retail-captures-options-pause-2026-07-27/
    /// fep-options-{root,controller,video,sound}-640x480.png.
    /// </summary>
    Options,

    /// <summary>
    /// Retail FEP_DEBRIEFING, the post-level objective summary and grade page.
    /// The settled Render contract is recovered from pristine PC
    /// <c>0x00456DD0..0x00457CED</c>; the partial source drop does not include
    /// <c>FEPDebriefing.cpp</c>.
    /// </summary>
    Debriefing,
    LevelSelect,

    /// <summary>
    /// Retail's MISSION BRIEFING page, reached from SELECT LEVEL. Reference
    /// frame: local-lab/retail-reference-pristine/mission-briefing/
    /// 05-mission-briefing-640x480.png.
    /// </summary>
    MissionBriefing,

    /// <summary>
    /// Retail's SELECT CONFIGURATION page, between briefing and loading. It was
    /// first observed on 2026-07-25 and the reconstruction had never modelled
    /// it. Reference frame: local-lab/retail-reference-pristine/
    /// select-configuration/06-select-configuration-640x480.png.
    /// </summary>
    SelectConfiguration,
    Loading,

    /// <summary>
    /// Retail's level intro cutscene, between the loading screen and the first
    /// gameplay frame.
    ///
    /// <c>CGame::RestartLoopRunLevel</c> runs it AFTER the level has loaded:
    /// <c>references/Onslaught/game.cpp:1336-1345</c> reads
    /// <c>if (GetIntroFMV()!=-1) { SetLoadingFraction(1.f); SetLoading(FALSE);
    /// RunIntroFMV(); SetLoading(TRUE); ... }</c> — the loading screen is driven
    /// to 100 %, turned OFF, the movie plays over black, and only then does the
    /// remaining load resume. That is why this is a state between Loading and
    /// Gameplay rather than an overlay on either.
    ///
    /// For Level 100 the clip is <c>data/video/cutscenes/01.vid</c>, 123.80 s.
    /// See <c>RetailStartupCue.Level100IntroCutscene</c> for the byte evidence.
    /// </summary>
    IntroCutscene,
    Gameplay,
}

public enum RetailFrontendSignal
{
    None,
    PageChanged,
    Unavailable,
    CareerLoadRequested,

    /// <summary>
    /// The Select-Configuration confirm edge. Carries
    /// <see cref="RetailFrontendSession.SelectedWorldNumber"/> as the world to
    /// construct. Named for the released page flow (any career level), not for
    /// the one world the reconstruction currently builds.
    /// </summary>
    LevelLaunchRequested,
    ReturnToMainMenuRequested,
    ExitRequested,
}

public enum RetailFrontendAudioCue
{
    Move,
    Select,
    Back,
}

/// <summary>
/// The five released frontend languages, ordered to match data/language and the
/// released Flag_UK / Flag_FR / Flag_GR / Flag_IT / Flag_SP texture set.
/// </summary>
public enum RetailFrontendLanguage
{
    English,
    French,
    German,
    Italian,
    Spanish,
}

public enum RetailFrontendCursorMode
{
    Custom,
    Visible,
    Hidden,
    Captured,
}

public enum RetailFrontendMenuItemKind
{
    NewGame,
    ContinueGame,
    LoadGame,
    Multiplayer,
    Goodies,
    Options,
    Quit,
}

public sealed record RetailFrontendMenuItem(
    RetailFrontendMenuItemKind Kind,
    bool IsAvailable);
