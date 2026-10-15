// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using Godot;

namespace OnslaughtRebuild.GodotClient;

internal static class CuratedObjMeshLoader
{
    private const int MaximumVertices = 100_000;
    private const int MaximumTriangles = 200_000;

    public static ArrayMesh Load(
        string resourcePath,
        IReadOnlyDictionary<string, Material> materials)
    {
        ParsedObj parsed = Parse(resourcePath, materials);
        return BuildMesh(parsed, parsed.Surfaces, materials);
    }

    /// <summary>
    /// Split one curated OBJ into a mesh per released hierarchy part, plus the
    /// remainder every unlisted part shares. The released rigid tracks in
    /// <c>level100-static-world-animation.json</c> address geometry by 1-based
    /// OBJ vertex range, and those ranges are contiguous and non-overlapping, so
    /// the split needs no geometry data the OBJ does not already carry.
    ///
    /// <para>Every triangle must lie wholly inside one listed range. That is a
    /// released property of these hierarchies, not an assumption: measured over
    /// the three split meshes, 6,437 triangles straddle zero part boundaries. It
    /// is asserted rather than worked around, because a straddling triangle
    /// would mean the part ranges no longer describe the emitted OBJ.</para>
    ///
    /// <para>Each produced mesh keeps the whole vertex/normal/UV/colour array and
    /// differs only in its index array, exactly as <see cref="Load"/> already
    /// does for its material surfaces. The wider bounding box that implies is
    /// conservative for culling.</para>
    /// </summary>
    public static PartitionedObjMesh LoadPartitioned(
        string resourcePath,
        IReadOnlyDictionary<string, Material> materials,
        IReadOnlyList<ObjPartRange> partRanges)
    {
        ParsedObj parsed = Parse(resourcePath, materials);
        var remainder = new List<MaterialSurface>();
        var byPart = new Dictionary<int, List<MaterialSurface>>();

        foreach (MaterialSurface surface in parsed.Surfaces)
        {
            for (int offset = 0; offset < surface.Indices.Count; offset += 3)
            {
                int a = surface.Indices[offset];
                int b = surface.Indices[offset + 1];
                int c = surface.Indices[offset + 2];
                int owner = -1;
                foreach (ObjPartRange range in partRanges)
                {
                    // ObjVertexStart is 1-based in the released manifest.
                    int first = range.FirstVertex - 1;
                    int last = first + range.VertexCount - 1;
                    int inside =
                        (a >= first && a <= last ? 1 : 0) +
                        (b >= first && b <= last ? 1 : 0) +
                        (c >= first && c <= last ? 1 : 0);
                    if (inside == 0)
                    {
                        continue;
                    }

                    if (inside != 3 || owner >= 0)
                    {
                        throw new InvalidDataException(
                            "Curated mesh has a triangle straddling a released hierarchy part range.");
                    }

                    owner = range.Part;
                }

                List<MaterialSurface> target;
                if (owner < 0)
                {
                    target = remainder;
                }
                else if (!byPart.TryGetValue(owner, out List<MaterialSurface>? partSurfaces))
                {
                    target = [];
                    byPart.Add(owner, target);
                }
                else
                {
                    target = partSurfaces;
                }

                MaterialSurface? bucket = target.Find(item =>
                    StringComparer.Ordinal.Equals(item.Name, surface.Name));
                if (bucket is null)
                {
                    bucket = new MaterialSurface(surface.Name);
                    target.Add(bucket);
                }

                bucket.Indices.Add(a);
                bucket.Indices.Add(b);
                bucket.Indices.Add(c);
            }
        }

        var parts = new Dictionary<int, ArrayMesh>();
        foreach (ObjPartRange range in partRanges)
        {
            if (!byPart.TryGetValue(range.Part, out List<MaterialSurface>? partSurfaces))
            {
                throw new InvalidDataException(
                    "Curated mesh has a released hierarchy part range covering no triangle.");
            }

            parts.Add(range.Part, BuildMesh(parsed, partSurfaces, materials));
        }

        if (remainder.Count == 0)
        {
            throw new InvalidDataException(
                "Curated mesh has no geometry outside its released hierarchy part ranges.");
        }

        return new PartitionedObjMesh(BuildMesh(parsed, remainder, materials), parts);
    }

    private static ParsedObj Parse(
        string resourcePath,
        IReadOnlyDictionary<string, Material> materials)
    {
        string source = Godot.FileAccess.GetFileAsString(resourcePath);
        if (string.IsNullOrEmpty(source))
        {
            throw new InvalidDataException($"Curated mesh '{resourcePath}' is missing or empty.");
        }

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var textureCoordinates = new List<Vector2>();
        var colors = new List<Color>();
        var surfaces = new List<MaterialSurface>();
        var surfaceByName = new Dictionary<string, MaterialSurface>(StringComparer.Ordinal);
        MaterialSurface? activeSurface = null;

        foreach (string rawLine in source.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            string[] fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            switch (fields[0])
            {
                case "v":
                    // Retained meshes carry the retail FVF 0x152 DIFFUSE dword as the
                    // OBJ vertex-colour extension: "v x y z r g b". Meshes converted
                    // without it keep the bare three-component record.
                    if (fields.Length != 4 && fields.Length != 7)
                    {
                        throw new InvalidDataException("Curated mesh has an invalid vertex record.");
                    }
                    vertices.Add(new Vector3(ParseFloat(fields[1]), ParseFloat(fields[2]), ParseFloat(fields[3])));
                    if (fields.Length == 7)
                    {
                        colors.Add(new Color(
                            ParseUnitFloat(fields[4]),
                            ParseUnitFloat(fields[5]),
                            ParseUnitFloat(fields[6]),
                            1f));
                    }
                    if (vertices.Count > MaximumVertices)
                    {
                        throw new InvalidDataException("Curated mesh exceeds the vertex limit.");
                    }
                    break;

                case "vt":
                    RequireFieldCount(fields, 3, "texture coordinate");
                    textureCoordinates.Add(new Vector2(ParseFloat(fields[1]), ParseFloat(fields[2])));
                    break;

                case "vn":
                    RequireFieldCount(fields, 4, "normal");
                    normals.Add(new Vector3(ParseFloat(fields[1]), ParseFloat(fields[2]), ParseFloat(fields[3])));
                    break;

                case "f":
                    if (activeSurface is null)
                    {
                        throw new InvalidDataException("Curated mesh has a triangle without a material group.");
                    }
                    RequireFieldCount(fields, 4, "triangle");
                    // Retained OBJ faces use the format's conventional counter-clockwise
                    // front winding. Godot ArrayMesh defines clockwise triangles as front-facing.
                    activeSurface.Indices.Add(ParseUnifiedIndex(fields[1]));
                    activeSurface.Indices.Add(ParseUnifiedIndex(fields[3]));
                    activeSurface.Indices.Add(ParseUnifiedIndex(fields[2]));
                    if (surfaces.Sum(surface => surface.Indices.Count) / 3 > MaximumTriangles)
                    {
                        throw new InvalidDataException("Curated mesh exceeds the triangle limit.");
                    }
                    break;

                case "usemtl":
                    RequireFieldCount(fields, 2, "material");
                    if (!materials.ContainsKey(fields[1]))
                    {
                        throw new InvalidDataException($"Curated mesh references unmapped material '{fields[1]}'.");
                    }
                    if (!surfaceByName.TryGetValue(fields[1], out activeSurface))
                    {
                        activeSurface = new MaterialSurface(fields[1]);
                        surfaceByName.Add(fields[1], activeSurface);
                        surfaces.Add(activeSurface);
                    }
                    break;

                default:
                    throw new InvalidDataException($"Curated mesh contains unsupported OBJ record '{fields[0]}'.");
            }
        }

        if (vertices.Count == 0 || surfaces.Count == 0 || surfaces.Any(surface => surface.Indices.Count == 0) ||
            normals.Count != vertices.Count || textureCoordinates.Count != vertices.Count ||
            (colors.Count != 0 && colors.Count != vertices.Count) ||
            surfaces.SelectMany(surface => surface.Indices).Any(index => index < 0 || index >= vertices.Count))
        {
            throw new InvalidDataException("Curated mesh has inconsistent geometry arrays.");
        }

        return new ParsedObj(vertices, normals, textureCoordinates, colors, surfaces);
    }

    private static ArrayMesh BuildMesh(
        ParsedObj parsed,
        IReadOnlyList<MaterialSurface> surfaces,
        IReadOnlyDictionary<string, Material> materials)
    {
        List<Vector3> vertices = parsed.Vertices;
        List<Vector3> normals = parsed.Normals;
        List<Vector2> textureCoordinates = parsed.TextureCoordinates;
        List<Color> colors = parsed.Colors;

        var mesh = new ArrayMesh();
        foreach (MaterialSurface surface in surfaces)
        {
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = textureCoordinates.ToArray();
            if (colors.Count != 0)
            {
                arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
            }
            arrays[(int)Mesh.ArrayType.Index] = surface.Indices.ToArray();

            int surfaceIndex = mesh.GetSurfaceCount();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetName(surfaceIndex, surface.Name);
            mesh.SurfaceSetMaterial(surfaceIndex, materials[surface.Name]);
        }
        return mesh;
    }

    private static int ParseUnifiedIndex(string value)
    {
        string[] fields = value.Split('/');
        if (fields.Length != 3 ||
            !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out int vertex) ||
            !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int textureCoordinate) ||
            !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int normal) ||
            vertex <= 0 || vertex != textureCoordinate || vertex != normal)
        {
            throw new InvalidDataException("Curated mesh requires unified positive OBJ indices.");
        }

        return vertex - 1;
    }

    private static float ParseFloat(string value)
    {
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ||
            !float.IsFinite(result))
        {
            throw new InvalidDataException("Curated mesh contains a non-finite numeric value.");
        }

        return result;
    }

    private static float ParseUnitFloat(string value)
    {
        float result = ParseFloat(value);
        if (result is < 0f or > 1f)
        {
            throw new InvalidDataException("Curated mesh contains an out-of-range vertex colour channel.");
        }

        return result;
    }

    private static void RequireFieldCount(string[] fields, int expected, string role)
    {
        if (fields.Length != expected)
        {
            throw new InvalidDataException($"Curated mesh has an invalid {role} record.");
        }
    }

    private sealed class MaterialSurface(string name)
    {
        public string Name { get; } = name;

        public List<int> Indices { get; } = [];
    }

    private sealed record ParsedObj(
        List<Vector3> Vertices,
        List<Vector3> Normals,
        List<Vector2> TextureCoordinates,
        List<Color> Colors,
        List<MaterialSurface> Surfaces);
}

/// <summary>
/// One released hierarchy part's 1-based OBJ vertex range, as carried by
/// <c>level100-static-world-animation.json</c>.
/// </summary>
internal readonly record struct ObjPartRange(int Part, int FirstVertex, int VertexCount);

/// <summary>
/// A curated OBJ split into one mesh per released hierarchy part, plus the
/// remainder shared by every part with no rigid track.
/// </summary>
internal sealed record PartitionedObjMesh(
    ArrayMesh Remainder,
    IReadOnlyDictionary<int, ArrayMesh> Parts);
