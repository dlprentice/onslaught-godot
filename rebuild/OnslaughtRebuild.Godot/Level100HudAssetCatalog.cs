// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.GodotClient;

public sealed record Level100HudMessageDefinition(
    int MessageId,
    string Symbol,
    string AudioFile,
    string Text);

public sealed record Level100HudHelpDefinition(
    Level100HudHelpPrompt Prompt,
    string Symbol,
    string Text);

public sealed record Level100HudTerminalStrings(
    string Victory,
    string Defeat,
    string MissionComplete,
    string Retry,
    string Back,
    string TutorialBroken,
    string PlayerDeath,
    string Water)
{
    public string GetFailureReason(Level100MissionFailureReason reason) => reason switch
    {
        Level100MissionFailureReason.TutorialBroken => TutorialBroken,
        Level100MissionFailureReason.PlayerDeath => PlayerDeath,
        Level100MissionFailureReason.WaterLoss => Water,
        _ => throw new InvalidDataException(
            $"Level 100 loss has no released failure string for {reason}."),
    };
}

public readonly record struct Level100MessagePlaybackSnapshot(
    bool IsAvailable,
    int? ActiveMessageId,
    bool Playing,
    double PositionSeconds,
    double LengthSeconds,
    int? PortraitPoseIndex)
{
    public static Level100MessagePlaybackSnapshot Unavailable { get; } = new(
        IsAvailable: false,
        ActiveMessageId: null,
        Playing: false,
        PositionSeconds: 0d,
        LengthSeconds: 0d,
        PortraitPoseIndex: null);
}

public sealed class Level100HudAssetCatalog
{
    private const string ResourcePath =
        "res://Assets/Level100/MissionData/level100-hud-events.json";
    private const string ExpectedSha256 =
        "af0b389df897ab8f1404edf35280f3eac10e53ad0c064457f4df97b6ff4335d1";
    private const string ExpectedSchema = "onslaught.level100-hud-events.v4";
    private const string ExpectedLevelScriptSha256 =
        "d51f8864564b5bde872092ec822df5af49daac16563f500719135f1a8c6c04a4";
    private const string ExpectedEnglishSourceSha256 =
        "ee48f3bed1c3c872ccc975146318aa0b5da3df88bff6b0a60671f0d23f9ce478";
    private const string ExpectedTextStfSha256 =
        "fd318d6c2304eb8ffcfa718357c1715aadad69915f39851b19f442d8263b56ae";
    private const string ExpectedEnglishDatSha256 =
        "789ecff619d077092769df281c540d138a25fcc74d70023466a604888e59371a";

    private readonly IReadOnlyDictionary<int, Level100HudMessageDefinition>
        _messages;
    private readonly IReadOnlyDictionary<Level100HudHelpPrompt, Level100HudHelpDefinition>
        _help;

    private Level100HudAssetCatalog(
        IReadOnlyDictionary<int, Level100HudMessageDefinition> messages,
        IReadOnlyDictionary<Level100HudHelpPrompt, Level100HudHelpDefinition> help,
        Level100HudTerminalStrings terminalStrings)
    {
        _messages = messages;
        _help = help;
        TerminalStrings = terminalStrings;
    }

    public Level100HudTerminalStrings TerminalStrings { get; }

    public static Level100HudAssetCatalog Load()
    {
        byte[] data = Godot.FileAccess.GetFileAsBytes(ResourcePath);
        if (data.Length == 0)
        {
            throw new InvalidDataException(
                $"Released Level 100 HUD event manifest is missing: {ResourcePath}");
        }

        string actualHash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        if (!string.Equals(actualHash, ExpectedSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Released Level 100 HUD event manifest has unexpected SHA-256: {actualHash}");
        }

        HudManifest manifest = JsonSerializer.Deserialize<HudManifest>(data) ??
            throw new InvalidDataException("Released Level 100 HUD event manifest is invalid JSON.");
        ValidateIdentity(manifest);

        Dictionary<int, Level100HudMessageDefinition> messages =
            manifest.Messages.ToDictionary(
                row => row.TextId,
                row => new Level100HudMessageDefinition(
                    row.TextId,
                    RequireText(row.Symbol, "message symbol"),
                    RequireAudioFile(row.AudioFile),
                    RequireText(row.Text, $"native text for {row.Symbol}")));
        HashSet<int> releasedMessageIds = messages.Keys.ToHashSet();
        HashSet<int> audioMessageIds = Level100AudioCatalog.CharacterMessages
            .Select(message => message.MessageId)
            .ToHashSet();
        if (messages.Count != 51 || !releasedMessageIds.SetEquals(audioMessageIds))
        {
            throw new InvalidDataException(
                "Released Level 100 HUD events do not exactly cover the audio message IDs.");
        }

        for (int index = 0; index < manifest.PlayCharEvents.Length; index++)
        {
            HudPlayCharEvent playEvent = manifest.PlayCharEvents[index];
            int messageId = playEvent.TextId;
            string symbol = RequireText(playEvent.Symbol, "PlayCharMessage symbol");
            if (playEvent.EventIndex != index ||
                !messages.TryGetValue(messageId, out Level100HudMessageDefinition? definition) ||
                !string.Equals(symbol, definition.Symbol, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Released Level 100 PlayCharMessage events have unexpected order or identity.");
            }
            _ = ParseSpeaker(playEvent.Speaker);
            _ = ParseHighlight(playEvent.HighlightSymbol);
        }

        Dictionary<Level100HudHelpPrompt, Level100HudHelpDefinition> help =
            manifest.Help.ToDictionary(
                row => (Level100HudHelpPrompt)row.TextId,
                row => new Level100HudHelpDefinition(
                    (Level100HudHelpPrompt)row.TextId,
                    RequireText(row.Symbol, "help symbol"),
                    RequireText(row.Text, $"native help text for {row.Symbol}")));
        HashSet<int> releasedHelpIds = help.Keys.Select(prompt => (int)prompt).ToHashSet();
        HashSet<int> coreHelpIds = Enum.GetValues<Level100HudHelpPrompt>()
            .Select(prompt => (int)prompt)
            .ToHashSet();
        if (help.Count != 6 || !releasedHelpIds.SetEquals(coreHelpIds))
        {
            throw new InvalidDataException(
                "Released Level 100 HUD events do not exactly cover the Core help IDs.");
        }

        Level100HudTerminalStrings terminalStrings = new(
            RequireTerminalString(
                manifest.TerminalStrings.Victory,
                8_959_659,
                "FETX_VICTORY"),
            RequireTerminalString(
                manifest.TerminalStrings.Defeat,
                4_141_956,
                "FETX_DEFEAT"),
            RequireTerminalString(
                manifest.TerminalStrings.MissionComplete,
                1_036_010_335,
                "IG_MISSION_COMPLETE"),
            RequireTerminalString(manifest.TerminalStrings.Retry, 830_889, "GI_RETRY"),
            RequireTerminalString(manifest.TerminalStrings.Back, 457_178, "GI_BACK"),
            RequireTerminalString(
                manifest.TerminalStrings.TutorialBroken,
                1_110_345_999,
                "LOSE_TUTORIAL_BROKE"),
            RequireTerminalString(
                manifest.TerminalStrings.PlayerDeath,
                54_406_750,
                "GAME_OVER_DEATH"),
            RequireTerminalString(
                manifest.TerminalStrings.Water,
                57_310_275,
                "GAME_OVER_WATER"));

        return new Level100HudAssetCatalog(messages, help, terminalStrings);
    }

    public bool TryGet(
        int messageId,
        out Level100HudMessageDefinition? definition) =>
        _messages.TryGetValue(messageId, out definition);

    public Level100HudMessageDefinition GetRequired(int messageId) =>
        _messages.TryGetValue(messageId, out Level100HudMessageDefinition? definition)
            ? definition
            : throw new InvalidDataException(
                $"Mission delivered an unknown Level 100 message ID: {messageId}");

    public Level100HudHelpDefinition GetRequired(Level100HudHelpPrompt prompt) =>
        _help.TryGetValue(prompt, out Level100HudHelpDefinition? definition)
            ? definition
            : throw new InvalidDataException(
                $"Core delivered an unknown Level 100 help ID: {(int)prompt}");

    private static void ValidateIdentity(HudManifest manifest)
    {
        if (!string.Equals(manifest.SchemaVersion, ExpectedSchema, StringComparison.Ordinal) ||
            manifest.Messages.Length != 51 ||
            manifest.PlayCharEvents.Length != 45 ||
            manifest.Help.Length != 6 ||
            !string.Equals(
                manifest.Sources.LevelScriptSha256,
                ExpectedLevelScriptSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                manifest.Sources.EnglishSourceSha256,
                ExpectedEnglishSourceSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                manifest.Sources.TextStfSha256,
                ExpectedTextStfSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                manifest.Sources.EnglishDatSha256,
                ExpectedEnglishDatSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Released Level 100 HUD event manifest has unexpected identity or counts.");
        }
    }

    private static string RequireText(string? value, string field) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidDataException($"Released Level 100 HUD event has no {field}.");

    private static string RequireAudioFile(string? value)
    {
        string audioFile = RequireText(value, "audio file");
        if (!string.Equals(Path.GetFileName(audioFile), audioFile, StringComparison.Ordinal) ||
            !audioFile.EndsWith(".ogg", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Released Level 100 HUD event has an invalid audio file: {audioFile}");
        }
        return audioFile;
    }

    private static string RequireTerminalString(
        HudTerminalString row,
        int expectedTextId,
        string expectedSymbol)
    {
        if (row.TextId != expectedTextId ||
            !string.Equals(row.Symbol, expectedSymbol, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Released terminal string {expectedSymbol} has an unexpected identity.");
        }

        return RequireText(row.Text, $"native terminal text for {expectedSymbol}");
    }

    private static Level100HudSpeaker ParseSpeaker(string? value) => value switch
    {
        "Tatiana" => Level100HudSpeaker.Tatiana,
        "Technician" => Level100HudSpeaker.Technician,
        "Kramer" => Level100HudSpeaker.Kramer,
        _ => throw new InvalidDataException(
            $"Released Level 100 HUD event has an unknown speaker: {value}"),
    };

    private static Level100HudPart? ParseHighlight(string? value) => value switch
    {
        "None" => null,
        "HUD_COMPASS" => Level100HudPart.Compass,
        "HUD_RADAR" => Level100HudPart.Radar,
        "HUD_ENERGY_BAR" => Level100HudPart.Energy,
        "HUD_HEALTH_BAR" => Level100HudPart.Health,
        "HUD_CURRENT_WEAPON" => Level100HudPart.CurrentWeapon,
        "HUD_BATTLE_LINE_MAP" => Level100HudPart.BattleLine,
        _ => throw new InvalidDataException(
            $"Released Level 100 HUD event has an unknown highlight owner: {value}"),
    };

    private sealed class HudManifest
    {
        [JsonPropertyName("schemaVersion")]
        public string? SchemaVersion { get; init; }

        [JsonPropertyName("sources")]
        public HudSources Sources { get; init; } = new();

        [JsonPropertyName("messages")]
        public HudMessage[] Messages { get; init; } = [];

        [JsonPropertyName("playCharEvents")]
        public HudPlayCharEvent[] PlayCharEvents { get; init; } = [];

        [JsonPropertyName("help")]
        public HudHelp[] Help { get; init; } = [];

        [JsonPropertyName("terminalStrings")]
        public HudTerminalStrings TerminalStrings { get; init; } = new();
    }

    private sealed class HudSources
    {
        [JsonPropertyName("levelScriptSha256")]
        public string? LevelScriptSha256 { get; init; }

        [JsonPropertyName("englishSourceSha256")]
        public string? EnglishSourceSha256 { get; init; }

        [JsonPropertyName("textStfSha256")]
        public string? TextStfSha256 { get; init; }

        [JsonPropertyName("englishDatSha256")]
        public string? EnglishDatSha256 { get; init; }
    }

    private sealed class HudMessage
    {
        [JsonPropertyName("textId")]
        public int TextId { get; init; }

        [JsonPropertyName("symbol")]
        public string? Symbol { get; init; }

        [JsonPropertyName("audioFile")]
        public string? AudioFile { get; init; }

        [JsonPropertyName("text")]
        public string? Text { get; init; }

    }

    private sealed class HudPlayCharEvent
    {
        [JsonPropertyName("eventIndex")]
        public int EventIndex { get; init; }

        [JsonPropertyName("textId")]
        public int TextId { get; init; }

        [JsonPropertyName("symbol")]
        public string? Symbol { get; init; }

        [JsonPropertyName("speaker")]
        public string? Speaker { get; init; }

        [JsonPropertyName("highlightSymbol")]
        public string? HighlightSymbol { get; init; }

        [JsonPropertyName("waitsForCompletion")]
        public bool WaitsForCompletion { get; init; }
    }

    private sealed class HudHelp
    {
        [JsonPropertyName("textId")]
        public int TextId { get; init; }

        [JsonPropertyName("symbol")]
        public string? Symbol { get; init; }

        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }

    private sealed class HudTerminalStrings
    {
        [JsonPropertyName("victory")]
        public HudTerminalString Victory { get; init; } = new();

        [JsonPropertyName("defeat")]
        public HudTerminalString Defeat { get; init; } = new();

        [JsonPropertyName("missionComplete")]
        public HudTerminalString MissionComplete { get; init; } = new();

        [JsonPropertyName("retry")]
        public HudTerminalString Retry { get; init; } = new();

        [JsonPropertyName("back")]
        public HudTerminalString Back { get; init; } = new();

        [JsonPropertyName("tutorialBroken")]
        public HudTerminalString TutorialBroken { get; init; } = new();

        [JsonPropertyName("playerDeath")]
        public HudTerminalString PlayerDeath { get; init; } = new();

        [JsonPropertyName("water")]
        public HudTerminalString Water { get; init; } = new();
    }

    private sealed class HudTerminalString
    {
        [JsonPropertyName("textId")]
        public int TextId { get; init; }

        [JsonPropertyName("symbol")]
        public string? Symbol { get; init; }

        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }
}
