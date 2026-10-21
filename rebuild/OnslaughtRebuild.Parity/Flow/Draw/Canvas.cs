// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Parity.Flow.Draw;

/// <summary>What a quad samples: a front-end texture (<see cref="FrontEndTexture"/>), a font's page, or the engine's default texture.</summary>
public enum CanvasTextureKind : byte
{
    FrontEnd,
    SmallFont,
    NormalFont,
    /// <summary><c>ENGINE.GetDefaultTexture()</c>, which the video quad uses while no video plays.</summary>
    Default,
    /// <summary>The mouse cursor (<c>mouse.tga</c>).</summary>
    Cursor,
    /// <summary>A texture found by its name (<c>CDXTexture::FindTexture</c>), named in <see cref="QuadTexture.Name"/>.</summary>
    Named,
}

/// <summary>
/// A quad's texture: its kind and, for a front-end texture, its <see cref="FrontEndTexture"/> index, or for a named one its
/// name as the game finds it.
/// </summary>
public readonly record struct QuadTexture(CanvasTextureKind Kind, int Index = 0, string? Name = null, string Format = FrontEndTextures.DefaultFormat)
{
    public static QuadTexture Fed(FrontEndTexture texture) => new(CanvasTextureKind.FrontEnd, (int)texture);

    /// <summary>A texture found by name, its frame file named with its format (A8R8G8B8 unless given).</summary>
    public static QuadTexture Find(string name, string format = FrontEndTextures.DefaultFormat) => new(CanvasTextureKind.Named, 0, name, format);
}

/// <summary>
/// The blend the quad draws with: the front end's modulate (source alpha over), additive (one, one), or keep (zero, one:
/// the colour stays and only depth is written, as the bracket masks draw).
/// </summary>
public enum CanvasBlend : byte
{
    Modulate,
    Additive,
    Keep,
}

/// <summary>
/// One transformed vertex as the game hands it to Direct3D (D3DFVF_XYZRHW, a diffuse colour, one texture coordinate), in the
/// window's pixels.
/// </summary>
public readonly record struct CanvasVertex(float X, float Y, float Z, float Rhw, uint Colour, float U, float V);

/// <summary>
/// A textured quad in strip order (top left, top right, bottom left, bottom right before any rotation), with the depth
/// state it is drawn under: the depth test on or off, and when on whether it is LESSEQUAL or ALWAYS.
/// </summary>
public readonly record struct CanvasQuad(QuadTexture Texture, CanvasBlend Blend, bool ZEnable, bool ZAlways,
    CanvasVertex V0, CanvasVertex V1, CanvasVertex V2, CanvasVertex V3)
{
    /// <summary>The texture's addressing clamps at its edges (the panel's) instead of wrapping.</summary>
    public bool Clamp { get; init; }
}

/// <summary>
/// Where the front end's render path draws. Pure: no state of the simulation reaches it except as arguments, nothing it
/// does reaches the simulation, and the front end's calls into it are the same whichever implementation receives them.
/// The host draws the quads of each frame in order; a frame ends at its Present.
/// </summary>
public interface IFrontEndCanvas
{
    /// <summary>Clears the frame to <paramref name="colour"/> (0x00RRGGBB) and the depth to 1 (<c>PLATFORM.ClearScreen</c>).</summary>
    void Clear(uint colour);

    void Quad(in CanvasQuad quad);

    /// <summary>The frame is presented as Present <paramref name="present"/>.</summary>
    void Present(long present);
}

/// <summary>A canvas that draws nothing: the default, and the headless runs'.</summary>
public sealed class NullCanvas : IFrontEndCanvas
{
    public static readonly NullCanvas Instance = new();

    private NullCanvas()
    {
    }

    public void Clear(uint colour)
    {
    }

    public void Quad(in CanvasQuad quad)
    {
    }

    public void Present(long present)
    {
    }
}

/// <summary>A presented frame: its Present and its quads in draw order, after a clear to <see cref="ClearColour"/> when it has one.</summary>
public sealed record CanvasFrame(long Present, uint? ClearColour, IReadOnlyList<CanvasQuad> Quads);

/// <summary>
/// Records each frame's quads and hands each presented frame, immutable, to its callback (the live host's
/// renderer, or a test). Runs on whichever thread renders the front end.
/// </summary>
public sealed class RecordingCanvas(Action<CanvasFrame> presented) : IFrontEndCanvas
{
    private List<CanvasQuad> _quads = [];
    private uint? _clear;

    public void Clear(uint colour)
    {
        _quads.Clear();
        _clear = colour;
    }

    public void Quad(in CanvasQuad quad) => _quads.Add(quad);

    public void Present(long present)
    {
        presented(new CanvasFrame(present, _clear, _quads));
        _quads = [];
        _clear = null;
    }
}
