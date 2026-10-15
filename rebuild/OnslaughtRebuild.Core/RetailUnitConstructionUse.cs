// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// One weapon or spawner use from a unit's physics record: the used
/// definition, its attachment tag and the record's raw creation flags.
/// </summary>
public sealed record RetailUnitConstructionUse(string DefinitionName, string TagName, uint RawCreationFlags);
