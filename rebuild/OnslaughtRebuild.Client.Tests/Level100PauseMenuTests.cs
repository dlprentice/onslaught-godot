// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.Client.Tests;

public sealed class Level100PauseMenuTests
{
    [Fact]
    public void RootUsesRetainedOrderAndDisablesRowsWithoutIntegratedOwners()
    {
        var menu = new Level100PauseMenu();

        menu.Open();

        Assert.Equal(
            [
                "Continue",
                "Message Log",
                "Briefing",
                "Controller Options",
                "Sound Options",
                "Video Options",
                "Retry",
                "Quit",
            ],
            menu.Entries.Select(entry => entry.Label));
        Assert.All(menu.Entries.Skip(1).Take(5), entry => Assert.False(entry.IsEnabled));
        Assert.DoesNotContain(menu.Entries, entry =>
            entry.Label.Contains("God", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NavigationSkipsEveryCapabilityDisabledRootRow()
    {
        var menu = new Level100PauseMenu();
        menu.Open();

        Assert.True(menu.MoveSelection(1));
        Assert.Equal(Level100PauseEntryId.Retry, menu.Entries[menu.SelectedIndex].Id);
        Assert.True(menu.MoveSelection(-1));
        Assert.Equal(Level100PauseEntryId.Continue, menu.Entries[menu.SelectedIndex].Id);
    }

    [Fact]
    public void ChildCancelReturnsRootAndRootCancelResumes()
    {
        var menu = new Level100PauseMenu();
        menu.Open();
        menu.Hover(6);
        menu.ActivateSelected();

        Assert.Equal(Level100PausePage.ConfirmRetry, menu.Page);
        Assert.Equal(Level100PauseAction.None, menu.Cancel());
        Assert.Equal(Level100PausePage.Root, menu.Page);
        Assert.Equal(Level100PauseEntryId.Retry, menu.Entries[menu.SelectedIndex].Id);

        Assert.Equal(Level100PauseAction.Resume, menu.Cancel());
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void RetryAndQuitConfirmationsStartOnSafeNo()
    {
        var menu = new Level100PauseMenu();
        menu.Open();
        menu.Hover(6);
        menu.ActivateSelected();

        Assert.Equal(Level100PauseEntryId.No, menu.Entries[menu.SelectedIndex].Id);
        menu.MoveSelection(1);
        Assert.Equal(Level100PauseAction.RetryLevel, menu.ActivateSelected());

        menu.Open();
        menu.Hover(7);
        menu.ActivateSelected();
        Assert.Equal(Level100PauseEntryId.No, menu.Entries[menu.SelectedIndex].Id);
        menu.MoveSelection(1);
        Assert.Equal(Level100PauseAction.ReturnToFrontend, menu.ActivateSelected());
    }

    [Fact]
    public void GodotPauseIntegrationUsesExistingInputAudioCursorAndAssetOwners()
    {
        string sourceRoot = Path.Combine(AppContext.BaseDirectory, "godot-pause-source");
        string game = File.ReadAllText(Path.Combine(sourceRoot, "FirstFlightGame.cs"));
        string view = File.ReadAllText(Path.Combine(sourceRoot, "FirstFlightPauseMenu.cs"));
        string audio = File.ReadAllText(Path.Combine(sourceRoot, "Level100Audio.cs"));
        string materializer = File.ReadAllText(
            Path.Combine(sourceRoot, "materialize_retail_assets.py"));

        string input = ExtractMethod(game, "public override void _Input(InputEvent inputEvent)");
        Assert.Contains("Key.Escape", input, StringComparison.Ordinal);
        Assert.Contains("JoyButton.Start", input, StringComparison.Ordinal);
        Assert.Contains("_pauseView.InputReady", input, StringComparison.Ordinal);
        Assert.Contains("OpenAuthenticPauseMenu();", input, StringComparison.Ordinal);

        string open = ExtractMethod(game, "private void OpenAuthenticPauseMenu()");
        AssertOccursInOrder(
            open,
            "_session.SetAuthenticMenuPaused(true);",
            "_audio.SetGameplayPaused(true);",
            "_pauseView.Open();",
            "UpdateGameplayCursorMode();");
        Assert.DoesNotContain("GameplayPauseRequested", game, StringComparison.Ordinal);

        string activate = ExtractMethod(game, "private void ActivatePauseSelection()");
        Assert.Contains(
            "action is not Level100PauseAction.RetryLevel",
            activate,
            StringComparison.Ordinal);
        Assert.Contains(
            "not Level100PauseAction.ReturnToFrontend",
            activate,
            StringComparison.Ordinal);

        string handle = ExtractMethod(
            game,
            "private void HandlePauseAction(Level100PauseAction action)");
        AssertOccursInOrder(
            handle,
            "case Level100PauseAction.RetryLevel:",
            "CompletePauseExitAudio();",
            "CloseAuthenticPauseForLifecycle();",
            "RestartLevel100();",
            "case Level100PauseAction.ReturnToFrontend:",
            "CompletePauseExitAudio();",
            "CloseAuthenticPauseForLifecycle();",
            "LeaveLevel100ForMainMenu();");

        string complete = ExtractMethod(game, "private void CompletePauseExitAudio()");
        Assert.Equal(
            1,
            CountOccurrences(complete, "_audio.StopForLevelExit(playFrontendSelect: true);"));
        Assert.Equal(
            1,
            CountOccurrences(
                complete,
                "RaiseFrontendAudioCueRequested(RetailFrontendAudioCue.Select);"));
        Assert.Equal(
            1,
            CountOccurrences(game, "FrontendAudioCueRequested?.Invoke(cue);"));

        string stopForExit = ExtractMethod(
            audio,
            "public void StopForLevelExit(bool playFrontendSelect)");
        AssertOccursInOrder(
            stopForExit,
            "StopLevel100Audio();",
            "PlayFrontendCue(\"Select\");");

        string destroy = ExtractMethod(game, "private void DestroyLevel100World()");
        AssertOccursInOrder(
            destroy,
            "if (!_pauseExitAudioCompleted)",
            "_audio.StopLevel100Audio();",
            "_pauseExitAudioCompleted = false;");

        string cursor = ExtractMethod(game, "private void UpdateGameplayCursorMode()");
        Assert.Contains("_session.IsPaused", cursor, StringComparison.Ordinal);
        Assert.Contains("RetailFrontendCursorMode.Visible", cursor, StringComparison.Ordinal);
        Assert.Contains("RetailFrontendCursorMode.Captured", cursor, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(game, "Input.MouseMode ="));

        string[] pauseAssets =
        [
            "blank.texture.aya",
            "circle-01.texture.aya",
            "circle-02.texture.aya",
        ];
        foreach (string asset in pauseAssets)
        {
            Assert.Contains(
                $"res://Assets/PauseMenu/{asset}",
                view,
                StringComparison.Ordinal);
            Assert.Equal(1, CountOccurrences(materializer, $"PauseMenu/{asset}"));
        }
        Assert.Contains(
            "res://Assets/Hud/font-22.texture.aya",
            view,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "res://Assets/PauseMenu/font-22.texture.aya",
            view,
            StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(materializer, "Hud/font-22.texture.aya"));
        Assert.DoesNotContain(
            "PauseMenu/font-22.texture.aya",
            materializer,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ConfirmationDrawsTheRetailPanelFrameOverTheStillDrawnRootList()
    {
        string view = ReadPauseView();
        string draw = ExtractMethod(view, "private void ApplyVisualState()");

        // CPauseMenu__Render renders the active range (index this+0x24, still 0
        // while a Retry/Quit prompt is up) and then the prompt hanging off
        // this+0x08. The root list must therefore stay drawn, and the prompt
        // must be the range that carries the panel flag.
        AssertOccursInOrder(draw,
            "_rootRange.Visible = transitionSeconds >= FadeSeconds;",
            "_confirmationRange.Visible = _rootRange.Visible && confirmation;",
            "ApplyMenuRange(_rootTitle, _rootRows, \"PAUSED\", _model.RootEntries,",
            "confirmation ? _model.UnderlyingRootSelection : _model.SelectedIndex);",
            "if (confirmation)",
            "ApplyMenuRange(_confirmationTitle, _confirmationRows, \"Are you sure?\", _model.Entries,",
            "ArrangePanelFrame(\"Are you sure?\", _model.Entries, _model.Page);");
        Assert.Equal(1, CountOccurrences(draw, "ArrangePanelFrame("));
    }

    [Fact]
    public void PanelFrameUsesTheMeasuredRetailGeometryAndTint()
    {
        string view = ReadPauseView();
        string panel = ExtractMethod(view, "private void ArrangePanelFrame(");

        // Sizing pass in CMenuItemRange__Render: (max(title, widest item) +
        // 0x10) * 1.1 wide, (0x20 + summed item heights) * 1.1 tall, centred on
        // the range origin using the pre-round size, clamped to 0x40 after.
        Assert.Contains(
            "(widest + PanelWidthPadding) * PanelSizeFactor",
            panel,
            StringComparison.Ordinal);
        Assert.Contains(
            "(PanelTitleBand + itemHeights) * PanelSizeFactor",
            panel,
            StringComparison.Ordinal);
        AssertOccursInOrder(
            panel,
            "float left = MathF.Round(320f - (rawWidth * 0.5f));",
            "float top = MathF.Round(GetRangeCenterY(page) - (rawHeight * 0.5f));",
            "Math.Max(PanelMinimumSize, MathF.Round(rawWidth))",
            "Math.Max(PanelMinimumSize, MathF.Round(rawHeight))");

        Assert.Contains("private const float PanelSizeFactor = 1.1f;", view, StringComparison.Ordinal);
        Assert.Contains("private const float PanelTitleBand = 32f;", view, StringComparison.Ordinal);
        Assert.Contains("private const float PanelWidthPadding = 16f;", view, StringComparison.Ordinal);
        Assert.Contains("private const float PanelMinimumSize = 64f;", view, StringComparison.Ordinal);
        Assert.Contains("private const float PanelCornerSize = 32f;", view, StringComparison.Ordinal);

        // Keep the previous renderer's ROUND(1.2 * 160.0) over RGB 0 and
        // its cited lab executable identity. This migration does not remeasure it.
        Assert.Contains(
            "PanelTint = new(0f, 0f, 0f, 192f / 255f)",
            view,
            StringComparison.Ordinal);
        Assert.Contains(
            "e1436ef7e0ad9ccbddd43aaaca952f6e84d4b1a282835cead745efcfc32fadf4",
            view,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SurfaceAddsNoUnevidencedLegibilityTreatment()
    {
        string view = ReadPauseView();

        // The frame is the only occluder retail draws for the prompt. Nothing
        // here may reach for a scrim, blur, outline or dim of the root list,
        // and the root list must not be hidden either -- retail keeps drawing
        // it because CPauseMenu__Render never changes the active range index
        // when the prompt opens.
        foreach (string banned in new[] { "Scrim", "Dim", "Blur", "Outline", "Vignette" })
        {
            Assert.DoesNotContain(banned, view, StringComparison.OrdinalIgnoreCase);
        }

        // PAUSED stays at the retail title colour 0xff505050 with no shadow:
        // CMenuItemRange__Render packs exactly that ARGB for its single title
        // CDXFont__DrawText call. Faintness is retail, not a defect.
        Assert.Contains("TitleColor = RetailColor(0xff505050)", view, StringComparison.Ordinal);
        string range = ExtractMethod(view, "private static void ApplyMenuRange(");
        AssertOccursInOrder(range, "titleControl.TextColor = TitleColor;", "titleControl.Shadow = false;");
    }

    private static string ReadPauseView() => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "FirstFlightPauseMenu.cs"));

    private static string ExtractMethod(string source, string signature)
    {
        int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Missing method signature: {signature}");
        int openingBrace = source.IndexOf('{', signatureIndex);
        Assert.True(openingBrace >= 0, $"Missing method body: {signature}");

        int depth = 0;
        for (int index = openingBrace; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}' && --depth == 0)
            {
                return source[(openingBrace + 1)..index];
            }
        }

        throw new InvalidOperationException($"Unterminated method body: {signature}");
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    private static void AssertOccursInOrder(string source, params string[] values)
    {
        int index = 0;
        foreach (string value in values)
        {
            index = source.IndexOf(value, index, StringComparison.Ordinal);
            Assert.True(index >= 0, $"Missing ordered source fragment: {value}");
            index += value.Length;
        }
    }
}
