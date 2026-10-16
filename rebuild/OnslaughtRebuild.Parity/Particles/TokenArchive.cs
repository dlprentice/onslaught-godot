// SPDX-License-Identifier: GPL-3.0-or-later
// The particle set file's reader: CTokenArchive (bea-decomp src/TokenArchive.cpp: ReadToken 004f57b0, SetFixup 004f5b70,
// SetFloatRef 004f5b80, HandleRelocation 004f5ba0). The token names are the file format's own keys.

using System.Globalization;
using System.Text;

namespace OnslaughtRebuild.Parity.Particles;

/// <summary>The archive's tokens (EParticleSystemToken), in their numbering.</summary>
public enum Pst
{
    None = -1,
    FileHeader,
    FileVersion,
    NumParticleDescriptors,
    ParticleDescriptorType,
    ParticleDescriptorName,
    Separator,
    Radius,
    FinalRadius,
    Gravity,
    Bounce,
    FadeCol,
    Texture,
    Modifier,
    BlendMode,
    TextureNumber,
    AxisAligned,
    ColourRange,
    AnimType,
    EndFrame,
    AnimSpeed,
    TextureSize,
    VelocityDamp,
    LifePct,
    RandomStartFrame,
    Length,
    TwoD,
    EmitPerTurn,
    Life,
    ParticleDescriptor,
    InitialVelocityX,
    InitialVelocityY,
    InitialVelocityZ,
    VelocityRandomness,
    TransmitLife,
    TransmitFoR,
    TransmitVelocity,
    InterpolatedEmission,
    Shape,
    PassNumParticles,
    OutwardVelocity,
    Mover,
    ParticleDescriptor0,
    ParticleDescriptor1,
    ParticleDescriptor2,
    ParticleDescriptor3,
    Probability0,
    Probability1,
    Probability2,
    Probability3,
    StartRed,
    StartGreen,
    StartBlue,
    EndRed,
    EndGreen,
    EndBlue,
    TransitionRed,
    TransitionGreen,
    TransitionBlue,
    UseEnd,
    UseTransition,
    TransitionPoint,
    NumEntries,
    Time,
    Type,
    RingAxis,
    Hemisphere,
    NumParticles,
    Hollow,
    RandomSX,
    RandomSY,
    RandomSZ,
    Width,
    NumPoints,
    StartWidth,
    WiggleFactor,
    DisperseRate,
    TaperStart,
    WidthWithLife,
    FadePoint,
    UseSegmentLength,
    SegmentLength,
    ManualWiggleEnabled,
    WiggleLength,
    YawLength,
    Flat,
    GravityPC,
    Yaw,
    Pitch,
    Roll,
    YawFunction,
    PitchFunction,
    RollFunction,
    ParamA,
    ParamB,
    ParamC,
    ParamD,
    ParamFunction,
    Clip,
    ValueType,
    GameturnScale,
    OffsetGameturn,
    Mesh,
    AngularMomentum,
    ImpactSpawnee,
    AutoCentre,
    Initial,
    Death,
    TileU,
    TileV,
    ScrollU,
    ScrollV,
    CylinderNumPtsAxial,
    CylinderNumPtsRadial,
    CylinderRadius,
    CylinderRadius2,
    CylinderLength,
    ColourRange2,
    SphereNumPtsAx,
    SphereNumPtsRad,
    SphereRadiusTime,
    SphereLatitudeStart,
    SphereLatitudeEnd,
    SphereLongitudeStart,
    SphereLongitudeEnd,
}

/// <summary>A float that may be scaled by a function descriptor (CFloatRef: value, and the function).</summary>
public sealed class FloatRef
{
    public float Value;
    public ParticleDescriptor? Ref;

    public FloatRef(float value) => Value = value;
}

/// <summary>
/// CTokenArchive: reads the set file line by line (each line's first word is the token, the second its value); references
/// to descriptors by name are collected and resolved once every descriptor has been read.
/// </summary>
public sealed class TokenArchive
{
    // The keys in token order; the header's (token 0) is matched by its prefix, as the file's first line.
    private static readonly string[] Names =
    [
        "", "File_Version", "Num_Particle_Descriptors", "Particle_Descriptor_Type", "Particle_Descriptor_Name",
        "*****************************************************************", "Radius", "Final_Radius", "Gravity", "Bounce",
        "Fade_Col", "Texture", "Modifier", "Blend_Mode", "Texture_Number", "Axis_Aligned", "Colour_Range", "Anim_Type",
        "End_Frame", "Anim_Speed", "Texture_Size", "Velocity_Damp", "Life_Pct", "Random_Start_Frame", "Length", "2D",
        "Emit_Per_Turn", "Life", "Particle_Descriptor", "Initial_Velocity_X", "Initial_Velocity_Y", "Initial_Velocity_Z",
        "Velocity_Randomness", "Transmit_Life", "Transmit_FoR", "Transmit_Velocity", "Interpolated_Emission", "Shape",
        "Pass_Num_Particles", "Outward_Velocity", "Mover", "Particle_Descriptor_0", "Particle_Descriptor_1",
        "Particle_Descriptor_2", "Particle_Descriptor_3", "Probability_0", "Probability_1", "Probability_2", "Probability_3",
        "Start_Red", "Start_Green", "Start_Blue", "End_Red", "End_Green", "End_Blue", "Transition_Red", "Transition_Green",
        "Transition_Blue", "Use_End", "Use_Transition", "Transition_Point", "Num_Entries", "Time", "Type", "Ring_Axis",
        "Hemisphere", "Num_Particles", "Hollow", "RandomSX", "RandomSY", "RandomSZ", "Width", "Num_Points", "Start_Width",
        "Wiggle_Factor", "Disperse_Rate", "Taper_Start", "Width_With_Life", "Fade_Point", "Use_Segment_Length",
        "SegmentLength", "Manual_Wiggle_Enabled", "Wiggle_Length", "Yaw_Length", "Flat", "GravityPC", "Yaw", "Pitch", "Roll",
        "Yaw_Function", "Pitch_Function", "Roll_Function", "Param_A", "Param_B", "Param_C", "Param_D", "Param_Function",
        "Clip", "Value_Type", "Gameturn_Scale", "Offset_Gameturn", "Mesh", "Angular_Momentum", "Impact_Spawnee",
        "Auto_Centre", "Initial", "Death", "Tile_U", "Tile_V", "Scroll_U", "Scroll_V", "Cylinder_NumPtsAxial",
        "Cylinder_NumPtsRadial", "Cylinder_Radius", "Cylinder_Radius2", "Cylinder_Length", "Colour_Range2",
        "Sphere_NumPtsAx", "Sphere_NumPtsRad", "Sphere_RadiusTime", "Sphere_Latitude_Start", "Sphere_Latitude_End",
        "Sphere_Longitude_Start", "Sphere_Longitude_End",
    ];

    private const string HeaderPrefix = "ParticleSystemEd_File_";

    private readonly string[] _lines;
    private int _next;
    // mFixups: one target per name slot; a later SetFixup or SetFloatRef with the same index replaces it.
    private readonly Dictionary<int, Action<ParticleDescriptor?>> _fixups = [];
    private readonly List<string> _names = [];

    public TokenArchive(byte[] file)
    {
        // CDXMemBuffer::ReadLine: each line without its terminator.
        _lines = Encoding.Latin1.GetString(file).Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
    }

    /// <summary>
    /// One line's token; for a value token, its value (ival, fval) or name (sval); FALSE where retail's is. As in retail,
    /// each output is written only by the kinds of token that set it, so the caller's locals keep earlier values: a plain
    /// float leaves ival as the last name's slot (CPDEmitter::Load then passes that slot to SetFloatRef for
    /// Velocity_Randomness, replacing the slot's earlier target).
    /// </summary>
    public bool ReadToken(out Pst token, ref int ival, ref float fval, ref string sval)
    {
        string line = _next < _lines.Length ? _lines[_next++] : "";
        string[] words = line.Split((char[]?)null, 3, StringSplitOptions.RemoveEmptyEntries);
        int read = Math.Min(words.Length, 2);
        string tokenText = words.Length > 0 ? words[0] : "";
        string valueText = words.Length > 1 ? words[1] : "";
        token = FindToken(tokenText, _next == 1);
        // The rest of the line after the token and one separator (strcpy(sval, sLine + strlen(sToken) + 1)).
        string afterToken = line.Length > tokenText.Length + 1 ? line[(tokenText.Length + 1)..] : "";
        switch (token)
        {
            case Pst.None:
                return false;
            case Pst.NumParticleDescriptors or Pst.ParticleDescriptorType or Pst.Gravity or Pst.Bounce or Pst.FadeCol
                or Pst.BlendMode or Pst.TextureNumber or Pst.AxisAligned or Pst.AnimType or Pst.EndFrame or Pst.TextureSize
                or Pst.RandomStartFrame or Pst.TwoD or Pst.Life or Pst.TransmitLife or Pst.TransmitFoR
                or Pst.InterpolatedEmission or Pst.PassNumParticles or Pst.Probability0 or Pst.Probability1
                or Pst.Probability2 or Pst.Probability3 or Pst.UseEnd or Pst.UseTransition or Pst.NumEntries or Pst.Time
                or Pst.Type or Pst.RingAxis or Pst.Hemisphere or Pst.NumParticles or Pst.Hollow or Pst.NumPoints
                or Pst.TaperStart or Pst.WidthWithLife or Pst.FadePoint or Pst.UseSegmentLength or Pst.ManualWiggleEnabled
                or Pst.Flat or Pst.ParamFunction or Pst.Clip or Pst.ValueType or Pst.OffsetGameturn or Pst.AutoCentre
                or Pst.CylinderNumPtsAxial or Pst.CylinderNumPtsRadial or Pst.SphereNumPtsAx or Pst.SphereNumPtsRad:
                if (read >= 2)
                {
                    ival = ScanI(valueText);
                    return true;
                }

                return false;
            case Pst.FileHeader or Pst.Separator:
                return true;
            case Pst.FileVersion or Pst.FinalRadius or Pst.AnimSpeed or Pst.VelocityDamp or Pst.LifePct
                or Pst.VelocityRandomness or Pst.TransitionPoint or Pst.RandomSX or Pst.RandomSY or Pst.RandomSZ or Pst.Width
                or Pst.StartWidth or Pst.WiggleFactor or Pst.DisperseRate or Pst.SegmentLength or Pst.Yaw or Pst.Pitch
                or Pst.Roll or Pst.AngularMomentum:
                if (read >= 2)
                {
                    fval = ScanF(valueText);
                    return true;
                }

                return false;
            case Pst.ParticleDescriptorName or Pst.Texture or Pst.Mesh:
                if (read >= 2)
                {
                    sval = afterToken;
                    return true;
                }

                return false;
            case Pst.Modifier or Pst.ColourRange or Pst.ParticleDescriptor or Pst.Shape or Pst.Mover or Pst.ParticleDescriptor0
                or Pst.ParticleDescriptor1 or Pst.ParticleDescriptor2 or Pst.ParticleDescriptor3 or Pst.YawFunction
                or Pst.PitchFunction or Pst.RollFunction or Pst.ImpactSpawnee or Pst.Initial or Pst.Death or Pst.ColourRange2:
                if (read >= 2)
                {
                    ival = _names.Count;
                    _names.Add(afterToken);
                    return true;
                }

                return false;
            default:
            {
                // A float with an optional function's name after it; a colour without one is read as 0..255.
                fval = ScanF(valueText);
                ival = _names.Count;
                int restAt = tokenText.Length + valueText.Length + 1;
                string rest = line.Length > restAt ? line[(restAt + 1)..] : "";
                if (line.Length <= restAt)
                {
                    rest = "";
                    if (token is >= Pst.StartRed and <= Pst.TransitionBlue)
                    {
                        fval /= 255.0f;
                    }
                }

                _names.Add(rest);
                return true;
            }
        }
    }

    /// <summary>SetFixup: the name read at <paramref name="index"/> resolves into <paramref name="set"/>.</summary>
    public void SetFixup(int index, Action<ParticleDescriptor?> set) => _fixups[index] = set;

    /// <summary>SetFloatRef: the value now, the function by name once resolved.</summary>
    public void SetFloatRef(float value, int index, FloatRef r)
    {
        r.Value = value;
        _fixups[index] = d => r.Ref = d;
    }

    /// <summary>
    /// HandleRelocation: each collected name, in order, looked up among the set's descriptors (ignoring case; none for no
    /// match) and stored where its slot's target is. A slot whose target was never set would write through an unset
    /// pointer in retail; none occurs in the shipped sets.
    /// </summary>
    public void HandleRelocation(ParticleSet set)
    {
        for (int i = 0; i < _names.Count; i++)
        {
            if (!_fixups.TryGetValue(i, out Action<ParticleDescriptor?>? setter))
            {
                throw new NotSupportedException($"particle set name slot {i} ('{_names[i]}') has no target (retail writes through an unset pointer)");
            }

            setter(set.Lookup(_names[i]));
        }

        _fixups.Clear();
        _names.Clear();
    }

    private static Pst FindToken(string text, bool firstLine)
    {
        if (firstLine && text.StartsWith(HeaderPrefix, StringComparison.Ordinal))
        {
            return Pst.FileHeader;
        }

        for (int t = 1; t < Names.Length; t++)
        {
            if (text == Names[t])
            {
                return (Pst)t;
            }
        }

        return Pst.None;
    }

    /// <summary>sscanf's %i: decimal, 0x hexadecimal or 0 octal, with a sign; the longest valid prefix.</summary>
    private static int ScanI(string s)
    {
        int i = 0;
        bool negative = false;
        if (i < s.Length && (s[i] == '+' || s[i] == '-'))
        {
            negative = s[i] == '-';
            i++;
        }

        int radix = 10;
        if (i + 1 < s.Length && s[i] == '0' && (s[i + 1] == 'x' || s[i + 1] == 'X'))
        {
            radix = 16;
            i += 2;
        }
        else if (i < s.Length && s[i] == '0')
        {
            radix = 8;
        }

        long v = 0;
        for (; i < s.Length; i++)
        {
            int d = char.IsAsciiDigit(s[i]) ? s[i] - '0' : char.IsAsciiLetter(s[i]) ? char.ToLowerInvariant(s[i]) - 'a' + 10 : 99;
            if (d >= radix)
            {
                break;
            }

            v = unchecked((v * radix) + d);
        }

        return unchecked((int)(negative ? -v : v));
    }

    /// <summary>sscanf's %f on the file's plain decimal values.</summary>
    private static float ScanF(string s) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : 0.0f;
}
