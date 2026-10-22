// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/CPhysicsScript.cpp (CPhysicsScript::Load 0042e950) and src/CPhysicsScriptStatements.h
// (the statements' Load and the value lists' Load). Not in Stuart's release.

using System.Text;
using OnslaughtRebuild.Parity.Data;

namespace OnslaughtRebuild.Parity.Physics;

/// <summary>EPhysicsStatement: the compiled physics script's statement ids.</summary>
public enum PhysicsStatementType
{
    Unit = 1,
    Weapon,
    WeaponMode,
    Round,
    Spawner,
    Explosion,
    Component,
    Feature,
    Hazard,
}

/// <summary>One value set in a statement: its compiled id and its stored bytes, decoded by the statement's kind.</summary>
public sealed record PhysicsValue(int Type, byte[] Body);

/// <summary>A statement: a definition's kind, its name and the values set on it, in script order.</summary>
public sealed record PhysicsStatement(PhysicsStatementType Type, byte[] Name, IReadOnlyList<PhysicsValue> Values)
{
    public string NameText => Encoding.ASCII.GetString(Name);
}

/// <summary>
/// The compiled physics script (data\default physics.dat), as CPhysicsScript::Load reads it: a 16-bit version (18),
/// then statements until a type of -1. Each statement stores its type, a size, a zero-terminated name and a value list;
/// each value stores its id, its size and its body, followed by a word that is zero when another value follows.
/// </summary>
/// <remarks>
/// The statement size is not read by retail for the nine known statement types (only SkipStatement uses it, for unknown
/// ones), and it does not match the stored bytes for 444 of level data's 777 statements, so it is kept but not trusted.
/// Every value's body is kept whole; a value's own Load reads exactly its stored size (each decoder checks that).
/// </remarks>
public sealed class PhysicsScript
{
    public const int SupportedVersion = 18;

    public required IReadOnlyList<PhysicsStatement> Statements { get; init; }

    public static PhysicsScript Read(MemBuffer file)
    {
        int version = file.ReadInt16();
        if (version != SupportedVersion)
        {
            throw new InvalidDataException($"physics script version {version}, expected {SupportedVersion}");
        }

        var statements = new List<PhysicsStatement>();
        for (int type = file.ReadInt32(); type != -1; type = file.ReadInt32())
        {
            file.ReadInt32(); // the statement's size
            if (type < (int)PhysicsStatementType.Unit || type > (int)PhysicsStatementType.Hazard)
            {
                throw new InvalidDataException($"physics statement type {type} is unknown");
            }

            byte[] name = file.ReadZeroTerminated();
            var values = new List<PhysicsValue>();
            int more;
            do
            {
                int valueType = file.ReadInt32();
                int size = file.ReadInt32();
                var body = new byte[size];
                if (file.Read(body) != size)
                {
                    throw new EndOfStreamException(file.FileName);
                }

                values.Add(new PhysicsValue(valueType, body));
                more = file.ReadInt32();
            }
            while (more == 0);

            statements.Add(new PhysicsStatement((PhysicsStatementType)type, name, values));
        }

        return new PhysicsScript { Statements = statements };
    }
}
