// SPDX-License-Identifier: GPL-3.0-only

using Godot;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Flow.Draw;

namespace OnslaughtRebuild.Host;

/// <summary>
/// The front end's pixels for <see cref="FrontEndRaster"/>, from the installation: each front-end texture's frame 0 by
/// the paths <see cref="FrontEndTextures.FramePaths"/> lists, and the two fonts' pages (the files the front end's fonts
/// are read from); DXTn images are decompressed. Loaded on first use and kept.
/// </summary>
public sealed class FrontEndImages(Func<string, (string Name, byte[] Bytes)> read)
{
    private const string SmallFontFile = @"data\resources\textures\mustbe_Font13PS.tga(0)A8R8G8B8.aya";
    private const string NormalFontFile = @"data\resources\textures\mustbe_font22.512.tga(0)A8R8G8B8.aya";

    private readonly Dictionary<QuadTexture, RasterTexture?> _cache = [];

    public RasterTexture? Get(QuadTexture texture)
    {
        if (!_cache.TryGetValue(texture, out RasterTexture? image))
        {
            image = texture.Kind switch
            {
                CanvasTextureKind.FrontEnd => FirstOf(FrontEndTextures.FramePaths(FrontEndTextures.Names[texture.Index])),
                CanvasTextureKind.SmallFont => FirstOf([SmallFontFile]),
                CanvasTextureKind.NormalFont => FirstOf([NormalFontFile]),
                CanvasTextureKind.Cursor => FirstOf(FrontEndTextures.FramePaths(FrontEndTextures.CursorName)),
                CanvasTextureKind.Named => FirstOf(FrontEndTextures.FramePaths(texture.Name!, texture.Format)),
                _ => RasterTexture.White,
            };
            _cache[texture] = image;
        }

        return image;
    }

    private RasterTexture? FirstOf(string[] paths)
    {
        foreach (string path in paths)
        {
            (string Name, byte[] Bytes) found;
            try
            {
                found = read(path);
            }
            catch (FileNotFoundException)
            {
                continue;
            }

            MemBuffer mb = MemBuffer.FromFile(found.Name, found.Bytes);
            var dds = new byte[mb.Length];
            mb.Read(dds);
            var image = new Image();
            if (image.LoadDdsFromBuffer(dds) != Error.Ok)
            {
                continue;
            }

            if (image.IsCompressed())
            {
                image.Decompress();
            }

            if (image.HasMipmaps())
            {
                image.ClearMipmaps();
            }

            image.Convert(Image.Format.Rgba8);
            byte[] rgba = image.GetData();
            int w = image.GetWidth(), h = image.GetHeight();
            var pixels = new uint[w * h];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = ((uint)rgba[i * 4 + 3] << 24) | ((uint)rgba[i * 4] << 16) | ((uint)rgba[i * 4 + 1] << 8) | rgba[i * 4 + 2];
            }

            return new RasterTexture(w, h, pixels);
        }

        return null;
    }
}
