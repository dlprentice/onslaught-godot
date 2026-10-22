// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/MissionScript/DataType.h and DataType.cpp (CDataType::Create 0052ec60; CIntDataType
// 0052d0a0..0052d380 and CBoolDataType 0052e420..0052e490, emitted in AsmInstruction.obj; CFloatDataType,
// CStringDataType 0052f690..0052f790, CThingPtrDataType and CPositionDataType 0052f820..0052f920), reconstructed from
// the retail executable; not in Stuart's release.

using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Script;

/// <summary>
/// CDataType: a mission-script value. The virtual slots keep retail's defaults: arithmetic gives null, comparisons give
/// false, <see cref="IsTrue"/> gives true (so a float, string, thing or position is always true), and the getters give
/// zero, null or the zero vector. <see cref="GetFloat"/> returns what the slot leaves in st(0): an int's value comes
/// back exactly (a bare fild, 0052d370), so the caller's operation rounds once.
/// </summary>
public abstract class ScriptData
{
    public virtual ScriptData? Add(ScriptData other) => null;

    public virtual ScriptData? Subtract(ScriptData other) => null;

    public virtual ScriptData? Multiply(ScriptData other) => null;

    public virtual ScriptData? Divide(ScriptData other) => null;

    public virtual void Assign(ScriptData other)
    {
    }

    public virtual bool IsEqual(ScriptData other) => false;

    public virtual bool NotEquals(ScriptData other) => false;

    public virtual bool LessThan(ScriptData other) => false;

    public virtual bool GreaterThan(ScriptData other) => false;

    public virtual bool LessOrEqual(ScriptData other) => false;

    public virtual bool GreaterOrEqual(ScriptData other) => false;

    public virtual int GetInt() => 0;

    /// <summary>The value as the x87 register holds it on return (exact; not yet rounded to a float).</summary>
    public virtual double GetFloat() => 0.0;

    /// <summary>The string, or null (retail's NULL, which strcmp would fault on).</summary>
    public virtual byte[]? GetString() => null;

    public virtual bool IsTrue() => true;

    public virtual Thing? GetThing() => null;

    public virtual FVector GetPosition() => new(0.0f, 0.0f, 0.0f);

    public abstract ScriptData Clone();

    public abstract DataType Type { get; }

    /// <summary>
    /// <c>delete value</c>: a thing pointer leaves its thing's reader list (~CActiveReader); the rest hold nothing.
    /// Every place retail deletes a value calls this.
    /// </summary>
    public virtual void Delete()
    {
    }

    /// <summary>
    /// CDataType::Create: a value as the object code stores it. A thing pointer's stored id is read and dropped; the
    /// value starts with no thing.
    /// </summary>
    public static ScriptData Create(ScriptValue stored) => stored.Type switch
    {
        DataType.Int => new IntData(stored.Int),
        DataType.Float => new FloatData(stored.Float),
        DataType.String => new StringData(stored.String!),
        DataType.Bool => new BoolData(unchecked((byte)stored.Int)),
        DataType.ThingPtr => new ThingPtrData(null),
        DataType.Position => new PositionData(stored.Position),
        _ => throw new InvalidDataException($"unknown data type to spawn ({(int)stored.Type})"),
    };

    /// <summary>strcmp(a, b) == 0 on the bytes before each string's terminator.</summary>
    internal static bool SameString(byte[]? a, byte[]? b)
    {
        if (a is null || b is null)
        {
            throw new NullReferenceException("strcmp on a NULL script string");
        }

        return CString(a).SequenceEqual(CString(b));
    }

    internal static ReadOnlySpan<byte> CString(byte[] s)
    {
        int end = Array.IndexOf(s, (byte)0);
        return end < 0 ? s : s.AsSpan(0, end);
    }
}

/// <summary>CIntDataType: 32-bit arithmetic that wraps (imul, idiv), comparisons against the other's GetInt.</summary>
public sealed class IntData(int value) : ScriptData
{
    public int Value = value;

    public override ScriptData Add(ScriptData other) => new IntData(unchecked(Value + other.GetInt()));

    public override ScriptData Subtract(ScriptData other) => new IntData(unchecked(Value - other.GetInt()));

    public override ScriptData Multiply(ScriptData other) => new IntData(unchecked(Value * other.GetInt()));

    /// <summary>idiv: a zero divisor or int.MinValue / -1 faults in retail (DivideByZeroException / OverflowException here).</summary>
    public override ScriptData Divide(ScriptData other)
    {
        int divisor = other.GetInt();
        return new IntData(Value / divisor);
    }

    public override bool IsEqual(ScriptData other) => Value == other.GetInt();

    public override bool NotEquals(ScriptData other) => Value != other.GetInt();

    public override void Assign(ScriptData other) => Value = other.GetInt();

    public override bool LessThan(ScriptData other) => Value < other.GetInt();

    public override bool GreaterThan(ScriptData other) => Value > other.GetInt();

    public override bool LessOrEqual(ScriptData other) => Value <= other.GetInt();

    public override bool GreaterOrEqual(ScriptData other) => Value >= other.GetInt();

    public override ScriptData Clone() => new IntData(Value);

    public override int GetInt() => Value;

    public override double GetFloat() => Value;

    public override bool IsTrue() => Value != 0;

    public override DataType Type => DataType.Int;

    public override string ToString() => $"Int {Value}";
}

/// <summary>
/// CBoolDataType: the one-byte bool (the stored form reads a dword over it, keeping the low byte). Equals compares the
/// byte with the other's IsTrue, so only 0 and 1 compare as retail's true and false do.
/// </summary>
public sealed class BoolData(byte value) : ScriptData
{
    public byte Value = value;

    public BoolData(bool value)
        : this(value ? (byte)1 : (byte)0)
    {
    }

    public override bool IsEqual(ScriptData other) => Value == (other.IsTrue() ? 1 : 0);

    public override bool NotEquals(ScriptData other) => Value != (other.IsTrue() ? 1 : 0);

    public override void Assign(ScriptData other) => Value = other.IsTrue() ? (byte)1 : (byte)0;

    public override bool IsTrue() => Value != 0;

    public override ScriptData Clone() => new BoolData(Value);

    public override DataType Type => DataType.Bool;

    public override string ToString() => $"Bool {Value}";
}

/// <summary>
/// CFloatDataType: each operation rounds once at 24 bits with the other's value exactly as GetFloat leaves it; GetInt
/// is fistp under /QIfist (round to nearest even). It has no IsTrue, so 0.0 is true.
/// </summary>
public sealed class FloatData(float value) : ScriptData
{
    public float Value = value;

    public override ScriptData Add(ScriptData other) => new FloatData((float)X87.Add(Value, other.GetFloat()));

    public override ScriptData Subtract(ScriptData other) => new FloatData((float)X87.Sub(Value, other.GetFloat()));

    public override ScriptData Multiply(ScriptData other) => new FloatData((float)X87.Mul(Value, other.GetFloat()));

    public override ScriptData Divide(ScriptData other) => new FloatData((float)X87.Div(Value, other.GetFloat()));

    public override bool IsEqual(ScriptData other) => Value == other.GetFloat();

    public override bool NotEquals(ScriptData other) => Value != other.GetFloat();

    public override void Assign(ScriptData other) => Value = (float)other.GetFloat();

    public override bool LessThan(ScriptData other) => Value < other.GetFloat();

    public override bool GreaterThan(ScriptData other) => Value > other.GetFloat();

    public override bool LessOrEqual(ScriptData other) => Value <= other.GetFloat();

    public override bool GreaterOrEqual(ScriptData other) => Value >= other.GetFloat();

    public override ScriptData Clone() => new FloatData(Value);

    public override int GetInt() => X87.Fist(Value);

    public override double GetFloat() => Value;

    public override DataType Type => DataType.Float;

    public override string ToString() => $"Float {Value:R}";
}

/// <summary>CStringDataType: a copy of the text up to its terminator; Equals is strcmp.</summary>
public sealed class StringData : ScriptData
{
    public StringData(byte[] text)
    {
        Value = CString(text).ToArray();
    }

    public byte[] Value { get; }

    public override ScriptData Clone() => new StringData(Value);

    public override bool IsEqual(ScriptData other) => SameString(Value, other.GetString());

    /// <summary>Matches (slot 20).</summary>
    public bool Matches(byte[] text) => SameString(Value, text);

    public override bool NotEquals(ScriptData other) => !IsEqual(other);

    public override byte[] GetString() => Value;

    public override DataType Type => DataType.String;

    public override string ToString() => '"' + System.Text.Encoding.Latin1.GetString(Value) + '"';
}

/// <summary>CThingPtrDataType: an active reader of a thing, so the value becomes null when the thing goes.</summary>
public sealed class ThingPtrData : ScriptData
{
    private readonly ActiveReader<Thing> _thing = new();

    public ThingPtrData(Thing? thing) => _thing.SetReader(thing);

    public override bool NotEquals(ScriptData other) => !IsEqual(other);

    public override void Assign(ScriptData other) => _thing.SetReader(other.GetThing());

    public override bool IsEqual(ScriptData other) => ReferenceEquals(_thing.ToRead, other.GetThing());

    public override ScriptData Clone() => new ThingPtrData(_thing.ToRead);

    public override Thing? GetThing() => _thing.ToRead;

    public override DataType Type => DataType.ThingPtr;

    public override void Delete() => _thing.Release();

    public override string ToString() => $"Thing {_thing.ToRead}";
}

/// <summary>CPositionDataType: a vector; Multiply scales each component by the other's value (one rounding each).</summary>
public sealed class PositionData(FVector value) : ScriptData
{
    public FVector Value = value;

    public override ScriptData Add(ScriptData other) => new PositionData(Value + other.GetPosition());

    public override ScriptData Subtract(ScriptData other) => new PositionData(Value - other.GetPosition());

    public override ScriptData Multiply(ScriptData other)
    {
        double scale = other.GetFloat();
        return new PositionData(new FVector(
            (float)X87.Mul(Value.X, scale), (float)X87.Mul(Value.Y, scale), (float)X87.Mul(Value.Z, scale)));
    }

    public override void Assign(ScriptData other) => Value = other.GetPosition();

    public override ScriptData Clone() => new PositionData(Value);

    public override FVector GetPosition() => Value;

    public override DataType Type => DataType.Position;

    public override string ToString() => $"Position {Value}";
}
