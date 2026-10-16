// SPDX-License-Identifier: GPL-3.0-or-later
// The landscape's texture stages from bea-decomp src/DXLandscape.cpp (CDXLandscape::RenderTerrain 00545590, method 2),
// its vertex format from src/LandscapeVB.cpp (XYZ and one texture coordinate) and the material from
// src/engine.cpp (CEngine::SetupLights: the ambient render state is the map's ambient).

using Godot;
using OnslaughtRebuild.Host.Landscape;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Host;

/// <summary>
/// The heightfield as a mesh (one vertex per sample) drawn as retail's four-stage landscape: the lit cache texture
/// (world position / 512) times the diffuse (the terrain material's 0.8 ambient reflectance of both lights' ambient;
/// CDXLandscape::Render sets the ambient render state to 0 for the terrain), doubled; times the detail mixer at the world position; times the scrolling cloud shadow (/ 256), doubled; times
/// the detail mixer turned by one radian at a quarter scale, doubled. The cache is built whole at one mip shift; retail's
/// per-tile levels of detail and the landscape's own geometry levels come later.
/// </summary>
public static class TerrainView
{
    private const string Shader = """
        shader_type spatial;
        render_mode unshaded, cull_disabled, fog_disabled;
        // Retail blends in gamma space, and this host's output is not converted from linear (the Compatibility
        // renderer here: a constant 0.5 put through the sRGB-to-linear curve read back as 54, i.e. 0.212), so ALBEDO
        // takes the gamma-space colour as it is.
        vec3 to_linear(vec3 c) { return c; }
        // D3D's EXP table fog (FOGTABLEMODE 1, the level's colour and density) blended in gamma space as retail does,
        // on the view depth; Godot's own fog would blend in linear space.
        uniform vec3 fog_colour;
        uniform float fog_density;
        vec3 fogged(vec3 c, float depth) { return mix(fog_colour, c, clamp(exp(-fog_density * depth), 0.0, 1.0)); }
        uniform sampler2D cache : filter_linear_mipmap_anisotropic, repeat_disable;
        uniform sampler2D detail : filter_linear_mipmap, repeat_enable;
        uniform sampler2D cloud : filter_linear_mipmap, repeat_enable;
        uniform vec3 diffuse;
        uniform vec2 cloud_scroll;
        varying vec2 world;
        void vertex() { world = UV; }
        void fragment() {
            vec3 c = clamp(texture(cache, world / 512.0).rgb * diffuse * 2.0, 0.0, 1.0);
            c = c * texture(detail, world).rgb;
            c = clamp(c * texture(cloud, world / 256.0 + cloud_scroll).rgb * 2.0, 0.0, 1.0);
            float cs = cos(1.0) * 0.25, sn = sin(1.0) * 0.25;
            vec2 r = vec2(world.x * cs - world.y * sn + 0.3, world.x * sn + world.y * cs + 0.3);
            c = clamp(c * texture(detail, r).rgb * 2.0, 0.0, 1.0);
            ALBEDO = to_linear(fogged(c, -VERTEX.z));
        }
        """;

    public static MeshInstance3D Build(HeightField map, TerrainTexture terrain, TextureLibrary textures, int shift, Vector2 cloudDrift)
    {
        const int Size = 512;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var heights = new float[Size + 1, Size + 1];
        for (int y = 0; y <= Size; y++)
        {
            for (int x = 0; x <= Size; x++)
            {
                heights[x, y] = map.HeightScale * map.GetHeightSample(x, y);
            }
        }

        void V(int x, int y)
        {
            st.SetUV(new Vector2(x, y));
            st.AddVertex(Host.ToGodot(new FVector(x, y, heights[x, y])));
        }

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                V(x, y);
                V(x, y + 1);
                V(x + 1, y);
                V(x + 1, y);
                V(x, y + 1);
                V(x + 1, y + 1);
            }
        }

        ushort[] texels = terrain.BuildMap(shift);
        int side = 512 << shift;
        var bytes = new byte[texels.Length * 3];
        for (int i = 0; i < texels.Length; i++)
        {
            int v = texels[i];
            bytes[i * 3] = (byte)(((v >> 11) & 31) * 255 / 31);
            bytes[(i * 3) + 1] = (byte)(((v >> 5) & 63) * 255 / 63);
            bytes[(i * 3) + 2] = (byte)((v & 31) * 255 / 31);
        }

        Image cache = Image.CreateFromData(side, side, false, Image.Format.Rgb8, bytes);
        cache.GenerateMipmaps();
        // D3D's lighting with the terrain material (ambient 0.8, no diffuse or emissive): 0.8 times the lights' ambient,
        // which ApplyLight(i, TRUE) sets to their diffuse colour (the sun and the anti-sun, each byte / 256); the ambient
        // render state is 0 while the terrain draws (CDXLandscape::Render).
        static Vector3 Bytes(uint c, float scale) => new Vector3((c >> 16) & 255, (c >> 8) & 255, c & 255) * scale;
        Vector3 diffuse = (Bytes(terrain.Sun, 1 / 256f) + Bytes(terrain.AntiSun, 1 / 256f)) * 0.8f;
        diffuse = diffuse.Clamp(Vector3.Zero, Vector3.One);
        var material = new ShaderMaterial { Shader = new Shader { Code = Shader } };
        material.SetShaderParameter("cache", ImageTexture.CreateFromImage(cache));
        string detailName = $@"mixers\detail{terrain.DetailTexture:00}.tga";
        material.SetShaderParameter("detail", textures.Get(detailName) ?? throw new InvalidOperationException($"no texture {detailName}"));
        material.SetShaderParameter("cloud", textures.Find(@"clouds\shadow.tga", 4) ?? throw new InvalidOperationException(@"no texture clouds\shadow.tga"));
        material.SetShaderParameter("diffuse", diffuse);
        material.SetShaderParameter("fog_colour", Bytes(terrain.Fog, 1 / 255f));
        material.SetShaderParameter("fog_density", terrain.FogDensity);
        material.SetShaderParameter("cloud_scroll", cloudDrift);
        return new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = material, Name = "Landscape" };
    }
}
