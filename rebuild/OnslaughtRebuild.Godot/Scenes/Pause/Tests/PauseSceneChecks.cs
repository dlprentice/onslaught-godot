// SPDX-License-Identifier: GPL-3.0-or-later

using Godot;
using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// Focused headless check of the production pause view, built in code. Run this
/// check scene with --headless; it never starts a game session.
/// </summary>
public sealed partial class PauseSceneChecks : Node
{
    private int _checks;
    private string? _captureDirectory;

    public override async void _Ready()
    {
        try
        {
            string? captureOption = OS.GetCmdlineUserArgs().FirstOrDefault(
                value => value.StartsWith("--pause-scene-capture-dir=", StringComparison.Ordinal));
            if (captureOption is not null)
            {
                _captureDirectory = captureOption["--pause-scene-capture-dir=".Length..];
                if (!Path.IsPathFullyQualified(_captureDirectory) || !Directory.Exists(_captureDirectory))
                    throw new InvalidOperationException("Capture directory must be an existing absolute task-owned path.");
            }
            Input.MouseModeEnum pointerBefore = Input.MouseMode;
            var model = new Level100PauseMenu();
            var view = FirstFlightPauseMenu.Create(model);
            var surface = view.GetNode<Control>("Surface");
            var native = view.GetNode<Control>("Surface/Native");
            var root = native.GetNode<Control>("RootRange");
            var prompt = native.GetNode<Control>("ConfirmationRange");
            var rootRows = root.GetNode<Control>("Rows").GetChildren().Cast<RetailBitmapLabel>().ToArray();
            var promptRows = prompt.GetNode<Control>("Rows").GetChildren().Cast<RetailBitmapLabel>().ToArray();
            var frame = prompt.GetNode<Control>("Frame");
            Check(rootRows.Length == 8 && promptRows.Length == 2, "Rows exist before _Ready.");
            Check(frame.GetChildCount() == 9, "The confirmation frame has nine authored texture controls.");
            Check(root.GetNodeOrNull("Frame") is null, "Only the confirmation has a panel frame.");
            for (int i = 0; i < rootRows.Length; i++)
            {
                Check(rootRows[i].Text == model.RootEntries[i].Label, "Authored root order/text matches the model.");
                Check(rootRows[i].Position == new Vector2(0, 175 + 20 * i) &&
                    rootRows[i].Size == new Vector2(640, 20), "Retained root row rectangles.");
            }
            Check(root.GetNode<Control>("Title").Position.Y == 145f, "Retained root title origin.");
            Check(prompt.GetNode<Control>("Title").Position.Y == 285f &&
                promptRows[0].Position.Y == 315f && promptRows[1].Position.Y == 335f,
                "Retained confirmation title and row origins.");
            var viewport = new SubViewport
            {
                Size = new Vector2I(640, 480), Disable3D = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            };
            AddChild(viewport);
            viewport.AddChild(view);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(!surface.Visible && !view.InputReady && !model.IsOpen, "The standalone view starts inactive.");
            Check(view.GetNode<TextureRect>("Surface/Overlay").Texture is not null, "Production private atlas loaded.");
            Check(Input.MouseMode == pointerBefore, "View initialization leaves pointer ownership alone.");
            model.Open();
            view.Open();
            Check(!view.InputReady && !root.Visible, "Opening blocks input and rows during the fade.");
            view.AdvanceAnimation(double.NaN);
            view.AdvanceAnimation(double.PositiveInfinity);
            view.AdvanceAnimation(-1d);
            Check(!view.InputReady, "Invalid time deltas cannot complete opening.");
            view.AdvanceAnimation(0.2d);
            var overlay = view.GetNode<TextureRect>("Surface/Overlay");
            var circle01 = native.GetNode<TextureRect>("Circle01");
            var circle02 = native.GetNode<TextureRect>("Circle02");
            Near(overlay.SelfModulate.A, 96f / 255f, "Half-fade overlay alpha.");
            Near(circle01.Scale.X, 1.2f, "Circle growth completes at 0.2 seconds.");
            Near(circle01.Rotation, 0f, "Circle rotation starts after growth.");
            Check(!view.InputReady, "Input is still gated at 0.2 seconds.");
            view.AdvanceAnimation(0.2d);
            Check(view.InputReady && root.Visible && !prompt.Visible, "Root opens at 0.4 seconds.");
            Near(overlay.SelfModulate.A, 192f / 255f, "Retained overlay alpha ceiling.");
            Near(circle01.Rotation, -0.2f, "First circle rotation.");
            Near(circle02.Rotation, 0.2f, "Second circle rotation.");
            await Capture(viewport, "pause-root.png");
            Check(view.TryPointAt(new Vector2(0, 175), out bool moved) && !moved, "Inclusive left/top row boundary.");
            Check(view.TryPointAt(new Vector2(640, 194.999f), out moved) && !moved, "Inclusive right and interior row edge.");
            Check(!view.TryPointAt(new Vector2(640.001f, 175), out _), "Outside the native horizontal region is rejected.");
            Check(!view.TryPointAt(new Vector2(320, 195), out _), "Disabled row at the previous row's exclusive end.");
            Check(view.TryPointAt(new Vector2(320, 295), out moved) && moved, "Retry owns its authored hit rectangle.");
            Check(model.SelectedIndex == 6, "Hover selects the existing model owner.");
            model.ActivateSelected();
            view.Refresh();
            Check(root.Visible && prompt.Visible, "Confirmation preserves the root list underneath.");
            Check(rootRows[6].TextColor == new Color(1f, 204f / 255f, 0f, 1f), "Underlying root selection is preserved.");
            var title = prompt.GetNode<RetailBitmapLabel>("Title");
            Check(!title.Shadow && promptRows.All(row => row.Shadow), "Title and item shadow policy is retained.");
            Check(title.TextColor == new Color(80f / 255f, 80f / 255f, 80f / 255f, 1f), "Retained title colour.");
            float rawWidth = (Math.Max(title.Measure("Are you sure?"),
                Math.Max(promptRows[0].Measure("No"), promptRows[1].Measure("Yes"))) + 16f) * 1.1f;
            float width = Math.Max(64f, MathF.Round(rawWidth));
            float left = MathF.Round(320f - rawWidth * 0.5f);
            var topLeft = frame.GetNode<TextureRect>("CornerTopLeft");
            var bottomRight = frame.GetNode<TextureRect>("CornerBottomRight");
            Check(topLeft.Position == new Vector2(left, 280f) && topLeft.Size == new Vector2(32f, 32f),
                "Panel positions before minimum-size clamping, native corner size.");
            Check(bottomRight.Position == new Vector2(left + width - 32f, 327f), "Measured confirmation frame extent.");
            Check(topLeft.FlipH && !topLeft.FlipV && !bottomRight.FlipH && bottomRight.FlipV,
                "Corner UV mirroring faces the panel interior.");
            Check(topLeft.SelfModulate == new Color(0f, 0f, 0f, 192f / 255f), "Panel tint is separate from root rows.");
            await Capture(viewport, "pause-confirmation.png");
            Check(view.TryPointAt(new Vector2(320, 335), out moved) && moved && model.SelectedIndex == 1,
                "Confirmation row boundary selects Yes in the existing model.");
            view.Close();
            Check(!view.InputReady && view.IsClosing, "Closing immediately blocks input.");
            view.AdvanceAnimation(0.1d);
            Check(!root.Visible && !prompt.Visible, "Rows disappear on the retained closing transition.");
            view.AdvanceAnimation(0.3d);
            Check(!surface.Visible && !view.IsClosing && !view.InputReady, "Closing ends after 0.4 seconds.");
            model.Reset();
            model.Open();
            view.Open();
            view.AdvanceAnimation(0.4d);
            viewport.Size = new Vector2I(1280, 720);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Near(native.Scale.X, 1.5f, "Native UI scales by viewport height.");
            Near(native.Position.X, 160f, "Native UI stays horizontally centered.");
            Check(view.TryPointAt(new Vector2(640, 442.5f), out moved) && moved && model.SelectedIndex == 6,
                "Widescreen hit conversion still selects Retry.");
            Check(Input.MouseMode == pointerBefore, "Pause transitions leave pointer changes to the game owner.");
            GD.Print($"PAUSE_SCENE_CHECKS: {_checks} passed; code-built view, assets, timing, rectangles, frame, hit edges and widescreen conversion.");
            viewport.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private async Task Capture(SubViewport viewport, string filename)
    {
        if (_captureDirectory is null)
            return;
        string destination = Path.Combine(_captureDirectory, filename);
        if (File.Exists(destination))
            throw new InvalidOperationException("Refusing to overwrite a previous scene capture.");
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Error saved = viewport.GetTexture().GetImage().SavePng(destination);
        Check(saved == Error.Ok, $"Saved task-owned capture {filename}.");
    }

    private void Check(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException(description);
        _checks++;
    }

    private void Near(float actual, float expected, string description) =>
        Check(Math.Abs(actual - expected) < 0.00001f, description + $" Expected {expected}, got {actual}.");
}
