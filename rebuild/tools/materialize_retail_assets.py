#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Materialize the exact retail inputs consumed by the current rebuild slice.

The current source tree and release packages do not include these files. This
bounded recipe reads a user-provided Battle Engine Aquila installation,
verifies the supported Steam data, and writes only the known frontend,
Level 100, later-world admission, and Aquila assets to local paths.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import shutil
import stat
import struct
import subprocess
import sys
import tempfile
import zlib
from pathlib import Path
from typing import NamedTuple


ROOT = Path(__file__).resolve().parents[2]
GODOT_ASSETS = Path("rebuild/OnslaughtRebuild.Godot/Assets")
CORE_ASSETS = Path("rebuild/OnslaughtRebuild.Core/Assets")
WINDOWS_DEFAULT_WORK_ROOT = ROOT / "local-lab/rebuild-godot"
REPARSE_ATTRIBUTE = 0x400


class _WorldLevelHeader(NamedTuple):
    """Exact RLWD preamble variant before the compiled script objects."""

    version: int
    header_words: tuple[int, int]
    names: tuple[str, ...]
    trailing_words: tuple[int, int, int, int, int]


class _WorldAdmission(NamedTuple):
    """One later world's hash-pinned materialization recipe."""

    world_number: int
    archive: str
    archive_sha256: str
    script_root: Path
    script_objects: tuple[tuple[str, int, str], ...]
    heightfield: Path
    hfld_size: int
    hfld_sha256: str
    level_header: _WorldLevelHeader


class _WorldInitialObject(NamedTuple):
    """Exact inert projection of one serialized world initial-object seed."""

    ordinal: int
    record_offset: int
    record_bytes: int
    record_sha256: str
    thing_type: int
    position_bits: tuple[int, int, int]
    orientation_bits: tuple[int, int, int]
    mesh_number: int
    allegiance: int
    target: int
    script: str
    name: str
    spawn_script: str
    active: int
    attach_scripts: int
    tail: _WorldInitialObjectTail

    @property
    def plane_mode(self) -> int | None:
        """Compatibility view used by the already-admitted start check."""

        return (
            self.tail.plane_mode
            if isinstance(self.tail, _WorldStartSeedTail)
            else None
        )

    @property
    def player_number(self) -> int | None:
        """Compatibility view used by the already-admitted start check."""

        return (
            self.tail.player_number
            if isinstance(self.tail, _WorldStartSeedTail)
            else None
        )


class _WorldUnitSeedTail(NamedTuple):
    definition_name: str
    trailer: int


class _WorldStartSeedTail(NamedTuple):
    plane_mode: int
    player_number: int


class _WorldWaypointSeedTail(NamedTuple):
    pass


class _WorldSpawnerSeedTail(NamedTuple):
    amount: int
    delay_bits: int
    squad_delay_bits: int
    initial_delay_bits: int
    squad_size: int
    spawn_unit: str
    spawner_spawn_script: str


class _WorldScriptSeedTail(NamedTuple):
    pass


class _WorldSquadSeedTail(NamedTuple):
    amount: int
    mode: int
    definition_name: str
    trailer: int


class _WorldVolumeSeedTail(NamedTuple):
    radius_bits: int


_WorldInitialObjectTail = (
    _WorldUnitSeedTail
    | _WorldStartSeedTail
    | _WorldWaypointSeedTail
    | _WorldSpawnerSeedTail
    | _WorldScriptSeedTail
    | _WorldSquadSeedTail
    | _WorldVolumeSeedTail
)


class _PhysicsRecord(NamedTuple):
    """One ordered ``default physics.dat`` definition record."""

    record_type: int
    name: str
    fields: tuple[tuple[int, bytes], ...]


class _PhysicsFields:
    """Multiplicity-preserving access to one definition's ordered fields."""

    def __init__(self, fields: tuple[tuple[int, bytes], ...]):
        self._fields = fields

    def __contains__(self, field_id: object) -> bool:
        return any(candidate == field_id for candidate, _ in self._fields)

    def __getitem__(self, field_id: int) -> bytes:
        matches = self.values(field_id)
        if len(matches) != 1:
            raise RuntimeError(
                f"expected one physics field {field_id}, found {len(matches)}"
            )
        return matches[0]

    def get(self, field_id: int, default: bytes | None = None) -> bytes | None:
        matches = self.values(field_id)
        if not matches:
            return default
        if len(matches) != 1:
            raise RuntimeError(
                f"expected at most one physics field {field_id}, "
                f"found {len(matches)}"
            )
        return matches[0]

    def values(self, field_id: int) -> tuple[bytes, ...]:
        return tuple(
            value for candidate, value in self._fields if candidate == field_id
        )


COMMON_WORLD_LEVEL_HEADER = _WorldLevelHeader(
    50,
    (3, 41),
    ("Aquila Prototype",),
    (0, 0, 0, 0, 1),
)
WORLD200_LEVEL_HEADER = _WorldLevelHeader(
    50,
    (3, 41),
    ("Aquila Prototype",),
    (0, 0, 0, 0, 2),
)
WORLD300_LEVEL_HEADER = _WorldLevelHeader(
    50,
    (3, 47),
    ("Standard", "Laser", "Blaster"),
    (1, 1, 1, 0, 3),
)

LEVEL_ARCHIVE = "data/resources/100_res_PC.aya"
LEVEL_ARCHIVE_SHA256 = "ed6350c0e214d00ab1bf6a7bd137fba3e77d0afe19a6dc4c0607f56ac037496a"
LEVEL100_SCRIPT_ROOT = CORE_ASSETS / "Level100/Scripts"
LEVEL100_SCRIPT_OBJECTS = (
    ("AirborneDrone1", 920, "7209f0752e4715d1b3dbd9d102cda997f65b3d4ff38a37a5c382d4bb5f364f28"),
    ("AirborneDrone2", 952, "6491cb323adb7758176c0afce12ec8d84e025323bdc4014f1f452c881102ed17"),
    ("AirTrainer", 625, "9549f63b5bd964dfafb1cd002685144311db2e53b01d79b8a1be47d68f0da9d7"),
    ("BattleEngine", 990, "b68d9e176051f6fec4fb84d344c51998047cf635a40dd56cc87c30ec1156ab26"),
    ("Facilities", 564, "3f19d9cb84a06a898aea98cf2236301f4b81739c874a018093ca8f1e3a9b6ed2"),
    ("FiringRange", 913, "8b05629d4a350005322333c1442e068f3f50707e65ab810c665c82f34bd7a7c9"),
    ("Flyby", 394, "20039756aba7f0b79a5f9bccc3c45f6b17a0e6ec27e05b406f4c40c3254e1444"),
    ("Hangar", 3419, "f5d42f5c58e9874d97965971b7e2a7cdede12a039eaf3ab699965da61568d073"),
    ("LevelScript", 20586, "73eb349b9c4b5c5d7294b2183cd4d4aebe024c5d3c8cda9be685bd1463ed6fb1"),
    ("Setup", 2402, "986e2b60afa62df15c9ad52fc04538a8eab8473a4eab46fc1844e67d7a85d1e0"),
    ("StaticTarget", 1011, "6670ab8cf964b037fb29f4feff19790a1e15c9fc45504e71fc259eadda9e8a54"),
    ("StaticTarget2", 1016, "28281145def2cdd0576b4ab9c6cb9e47f36c37f59397b3a941f46b15f334b8c3"),
    ("TankFactory", 3790, "44577134d213c3e8362fcedfe8344cbc3ac1346623f89408a16d32d685703892"),
    ("TargetTank1", 1278, "50839be04b6d72e29ecf6b1519c43baa9f826cda3d3c1bd0ebe56eedc292d305"),
    ("TargetTank2", 739, "331a5ecdf7122014d3eb70827f30b348ba2d8c80a58971c4764d040b4da7dc09"),
    ("TargetTruck1", 1299, "cca2f36f70d0751e62cf5a66e4fc23a12a2f77d98f9eb8579f28d8625e72ebbb"),
    ("TargetTruck2", 1299, "7c00c14b9de87873f21848cd28696a331cf0b5902548988030ef3192c438a2b6"),
    ("TargetTruck3", 1299, "8b864edfa626bbb9f45e06c6cc73b71d6b68c3ae41469fc4cd61a453d0386c52"),
    ("TargetZone1", 969, "50269961be899b4f50025300ff1af8bb220437b36f9e8bce1cd2806e7617d56c"),
    ("TargetZone2", 1092, "8a4b727dcf0f9e249c7c1ab0155326004356f0ad5dd84f226148db794a884af3"),
    ("TargetZone3", 1118, "222c44814482a69ff716b785d01a0bc7b508f19fc60774f2067db5d8bd1ca90d"),
    ("TargetZone4", 1092, "026a70263ede8daaed00d324dbe13160bcb4882a10e10f7bfe384f62896b2e8f"),
    ("Transporter", 408, "15cf230e674d80bb92fecefc4f16c708d3c969c02aa1384c4142de47dbf35a5f"),
    ("Turret", 835, "462614eaa5222bd3be759d4b8d0217d695c3afbbf36e334fbdc256a906797c71"),
    ("Weather", 540, "369ca18aba315a853404779b931d45bed0d692d5bad38536a41c4d4706650ce4"),
)
LEVEL100_SCRIPT_SHA256 = {name: sha256 for name, _, sha256 in LEVEL100_SCRIPT_OBJECTS}

# World 110 — the second node of the released career graph (Stuart
# Career.cpp level_structure[1]). Measured 2026-08-22 straight from the
# retail install with the same readers as Level 100: the compressed
# archive hash below pins the whole input; the script objects were walked
# out of its RLWD with _skip_level100_script_object (the compiled-object
# body framing is shared) and the HFLD envelope extracted the same way as
# CHUNKS[0]. Nothing here is derived from the Level 100 payloads.
WORLD110_ARCHIVE = "data/resources/110_res_PC.aya"
WORLD110_ARCHIVE_SHA256 = (
    "4e041c758b9d41ba18311b1fadeacb95fc31af51320861480b97033bc24e3c2b"
)
WORLD110_RLWD_SIZE = 76_600
WORLD110_RLWD_SHA256 = (
    "fb56249deac8faf0033f4d4b67688ff72e12d922291c880d75b10599fc739837"
)
WORLD110_INITIAL_OBJECT_HEADER = (2, 0, 40)
WORLD110_INITIAL_OBJECT_HEADER_OFFSET = 15_709
WORLD110_INITIAL_OBJECT_FIRST_RECORD_OFFSET = 15_719
WORLD110_TREE_GROUP_HEADER_OFFSET = 18_327
WORLD110_TREE_GROUP_HEADER = (0, 2)
WORLD110_INITIAL_OBJECT_TYPE_COUNTS = {
    8: 10,
    15: 1,
    18: 19,
    19: 1,
    27: 3,
    28: 5,
    36: 1,
}
WORLD110_PLAYER_START_ORDINAL = 1
WORLD110_PLAYER_START_RECORD_OFFSET = 15_781
WORLD110_PLAYER_START_RECORD_BYTES = 59
WORLD110_PLAYER_START_RECORD_SHA256 = (
    "850de203b32b967064f3a9bacca24bebd783af68760a8b4c056ea242a2b47dfc"
)
WORLD110_PLAYER_START_POSITION_BITS = (0x43846000, 0x43816800, 0x80000000)
WORLD110_PLAYER_START_ORIENTATION_BITS = (0xBF04FD8B, 0x00000000, 0x00000000)
WORLD110_INITIAL_OBJECT_SEEDS_SCHEMA = (
    "onslaught.world110-initial-object-seeds.v1"
)
LEVEL110_INITIAL_OBJECT_SEEDS = (
    CORE_ASSETS / "Level110/level110-initial-object-seeds.json"
)
# Canonical compact JSON produced from the exact archive/RLWD/table identities
# above. This pin is populated by the reviewed all-40 admission and is checked
# before either staging or publication.
WORLD110_INITIAL_OBJECT_SEEDS_SHA256 = (
    "51e51f5e1d3f7bce52ce99297711b1f299494271af3129828959e726aed04e5a"
)
LEVEL110_INITIAL_ACTORS = CORE_ASSETS / "Level110/level110-initial-actors.json"
WORLD110_INITIAL_ACTORS_SCHEMA = "onslaught.world110-initial-actors.v7"
WORLD110_INITIAL_ACTORS_SHA256 = (
    "ace932aaf6277b1ebe1a7b5408024fc638eb1d70bb0be5731ae878eab8292ba4"
)
WORLD110_LANDING_CRAFT_MESH = "data/resources/meshes/m_m_dropship.msh.aya"
WORLD110_LANDING_CRAFT_MESH_SHA256 = (
    "f586cc84f577e441eba425d5c95dbca3e057d063229bf5c2999227157704424b"
)
WORLD110_INITIAL_MESHES = (
    ("fb_control_tower.msh", "data/resources/meshes/m_fb_control_tower.msh.aya",
     "86af67e09dc2fd21c7023acd53ebcb4171f3bf396f836da85ecfdda516588d91", 39),
    ("fb_tank_factory.msh", "data/resources/meshes/m_fb_tank_factory.msh.aya",
     "a507afda7b5c6b6b8bed275d442a53b28043bb9d5b65f9ea5bd6f5ff754bf6de", 29),
    ("fb_health_pad.msh", "data/resources/meshes/m_fb_health_pad.msh.aya",
     "4ec6cb1d589c866acfa292232ca4f850967faea899c2f082329bff78e647ab44", 20),
    ("ft_sam.msh", "data/resources/meshes/m_ft_sam.msh.aya",
     "9a82f27454863c19c05a8cdedcc99cc05300aed75b8e54467a980c94bf5ba4a2", 16),
    ("iceberg1.msh", "data/resources/meshes/m_iceberg1.msh.aya",
     "fdbbd6d91748a7130b3a0137e417fb5e984266e046f6731c6b9beb4ac222c7c5", 1),
    ("iceberg2.msh", "data/resources/meshes/m_iceberg2.msh.aya",
     "4824acec58949d555fed44caaaff238b29ee7ef44e1b4d23469bce50224b5d1e", 1),
    ("iceberg3.msh", "data/resources/meshes/m_iceberg3.msh.aya",
     "6148c199d29e39e7bfa971eabdf8c837a787c6bcf004def3a3fd032f7a1f3530", 1),
    ("iceberg4.msh", "data/resources/meshes/m_iceberg4.msh.aya",
     "3add5f0cb80a954e029016a1f9a9825888f3e88b0d049260216e3a1231408347", 1),
)
LEVEL110_STATIC_WORLD = CORE_ASSETS / "Level110/level110-static-world.json"
WORLD110_STATIC_WORLD_SHA256 = (
    "f5e411a1717c3d278e8e314c049bff54c3081744c3a7e512a9f8857b9584fa58"
)
LEVEL110_PLAYER_INPUTS = CORE_ASSETS / "Level110/level110-player-inputs.json"
WORLD110_PLAYER_INPUTS_SHA256 = (
    "3bcd5eac3bf17474f60e67d3f4aa135dd239de9a896d63f64f23c494fe339c7d"
)
BATTLE_ENGINE_CONFIGURATIONS = "data/battle engine configurations.dat"
LEVEL110_SCRIPT_ROOT = CORE_ASSETS / "Level110/Scripts"
LEVEL110_SCRIPT_OBJECTS = (
    ("beacon", 200, "8e75c80c01e8def1841c51a9234cc14d33590d4a9a031e087db0325762c35be7"),
    ("Lander", 1331, "faa88a9341d3043826c44f0a483ec707d2cbb0b834b00061a8efa903244d91a8"),
    ("Lander2", 1142, "2a3ca550f3965d4d5b1508287cf1c4b71b9638732ce3422d4a415cc7adfa6da0"),
    ("Lander3", 381, "f004ee4da79b59f410aab8129d358bbe956c8529e99e417ca8d32bd5425295f6"),
    ("LevelScript", 5110, "f5c157ba2c6a9acbee78d895a25be82252951b93bdfdd8886a79ecd7bfe222aa"),
    ("MuspellFighter", 428, "eb436aa1a01d989910f6bb89ebfebc66d0987ff2959d3c6b32e1936666f9e8f4"),
    ("MuspellFighter1", 429, "b3104aa1e827488eadb280a91cf5196a5ff033e2d298eb2e26dc692a3694c7bc"),
    ("MuspellFighter2", 429, "606e4df72110f95fcb132871b84ee35c151d918a11e568b4cdda17fbb379a7e4"),
    ("Scout", 603, "a9d861d83ff26bb1ad793426832491939c243250b412c84336a2c6edff6908f2"),
    ("Setup", 1203, "c17c5a3b14e4f03394beacd21949943da52b74f558e4a4bb6557ec50296816bb"),
    ("Victory", 1600, "281aae6c239801748e649662917ecdbfcccb648130c87f6318cd4dbbbb7c061e"),
    ("VitalBuilding", 2415, "712bac0b6b8177fcbafc3ceaf4d0768375e22cce465513125f06f712ca23dea5"),
    ("Weather", 249, "42ff6dc591660f0f08faecad2d144cffc863dd95e41bc4397fde29553c6b02bc"),
)
LEVEL110_HEIGHTFIELD = CORE_ASSETS / "Level110/level110-heightfield.hfld.bin"
WORLD110_HFLD_SIZE = 668_660
WORLD110_HFLD_SHA256 = (
    "fd4d076a2926fbc473b7d364703bdbc0c8a0f7a638b0ab71b6f319374da033c2"
)
# World 200 — the third node of the released career graph (Stuart
# Career.cpp level_structure[2], the Episode-1 continuation). Measured
# 2026-08-22 from the retail install with the same readers as worlds 100
# and 110. Two structural divergences from the first two nodes are
# MEASURED, not assumed: (1) the level-world header's post-zeros word is
# 2, not the 1 both earlier worlds carry; (2) the BSWD is world 200's
# own 80,232-byte base world, not the byte-identical island world 110
# shares with Level 100. The HFLD payload length still matches the shared
# envelope law exactly (668,652), and the whole-image owner scan places it
# in ERES just like worlds 100 and 110, so the Core loader admits it unchanged.
WORLD200_ARCHIVE = "data/resources/200_res_PC.aya"
WORLD200_ARCHIVE_SHA256 = (
    "99dbd433013822f7d83863fe9607448f4a80db69de347e01920e688c5022bb77"
)
LEVEL200_SCRIPT_ROOT = CORE_ASSETS / "Level200/Scripts"
LEVEL200_SCRIPT_OBJECTS = (
    ("EnemyLander", 1220, "2a2549d34e4fc580184cac46cf8eea3148dcb3592f57af0bcad7278b8985f637"),
    ("EnemyLander2", 1333, "2b7bc41b9264ee8488301e8113994d1a3e8d4e51362f79f2f5a7b8c9c2c48036"),
    ("EnergyMonitor", 793, "6fcbf190f980891cd15dba84f90c3addd213dc9ec3f0f42e9b6868a1d8125815"),
    ("FighterAttack", 1221, "d26b78d8ec15c21bf359a8e76573f35a435430b5e22267b82d70d18fcd060511"),
    ("Hangar", 811, "ac616a60b792422f9061e212f46656c60cd28cf62b48e88997aa51f2c5772483"),
    ("HealthMonitor", 791, "17c00d84b473964cffdceacd5037102a62d05eaf8f9ba4b246f4b4cfa22eaf3a"),
    ("LandingCraftAlpha", 2222, "63804faa4af57c6dfbaed9f1a3175c00874d9a08f47b03771d77369d9e366457"),
    ("LandingCraftBeta", 2220, "c75d713729cc50d03f32f350cd6bfa96a28d5b77fe29e0ecc407ddedecbba3cd"),
    ("LandingCraftDelta", 2876, "a48a36bf69ec3296e6d54ae5ca6704c676d3296f6fa2370cc48564265725f00b"),
    ("LandingCraftGamma", 2308, "f2ff4fe3408915c1a8124139216023771f8cca8d3ee21da935f5ba03b09146ef"),
    ("LevelScript", 10300, "ecba1e75c50563aa53821dd0c8e24e8ce932a6ed1e48aca9748e666859919737"),
    ("Tatiana", 825, "439f99498e4d2ec4c6553d78e0517c05f924eb9b29e2daafae57bef35bf6ce36"),
    ("VitalBuilding", 569, "956c17bdd26adcf74e0ed16eda55019b1c7fec2ac8ec583c46e9c1c078c193ee"),
    ("WestAttacker", 392, "395c13f8f29427c09d0bcf5eb3bb1e28249b6cb933f39f7e24db49b27d236fad"),
)
LEVEL200_HEIGHTFIELD = CORE_ASSETS / "Level200/level200-heightfield.hfld.bin"
WORLD200_HFLD_SIZE = 668_660
WORLD200_HFLD_SHA256 = (
    "1b8eb8584be552383f10b08c75d9f10e91708343f0e5ee085d5130d369f6b945"
)
# World 300 — the next admitted main-episode payload. Its script objects
# retain the measured compiled-object framing, but its version-50 RLWD
# preamble is NOT the worlds-100/110/200 preamble: header words (3, 47,
# 300), three names (Standard/Laser/Blaster), and trailing words
# (1, 1, 1, 0, 3). The exact variant is data above rather than another
# optional argument grafted onto the earlier one-name law. After the eight
# scripts the actor header is (10, 0, 36). HFLD remains the 668,660-byte
# framed envelope inside ERES; BSWD is world 300's own 77,113-byte payload
# inside WRES (SHA-256 3c153d55…0dfc).
WORLD300_ARCHIVE = "data/resources/300_res_PC.aya"
WORLD300_ARCHIVE_SHA256 = (
    "7293bcbe3cbb6c88b2e19a287cb53de132a8bcadd6c2736b0da7483726c9efe4"
)
LEVEL300_SCRIPT_ROOT = CORE_ASSETS / "Level300/Scripts"
LEVEL300_SCRIPT_OBJECTS = (
    ("dropship", 399, "a76ac1d6f6bb9b7f360b9c95fc5a22b5f7acdb179e81b567f8b6c2e96cd37ce2"),
    ("ForsetiUnit", 1079, "fd7503a2fb3e2b5effae99266387d05b63d08fa2e90ea7b5de731a92aae564e9"),
    ("Level300script", 11293, "eeec860d3fc946e1aec578f99b086d29c4fcf25967b8e32738ef3bbb0aeb88e5"),
    ("messages", 2028, "638158eefba79f0af19667be9864464002b34c3646b8b39f2725e90aedaaa5b5"),
    ("oforce", 374, "186962832481999c34bb0761a4dbc406d9fa333e362259ed9ea80c5d2a83f8bf"),
    ("outpostsphere", 2896, "5f06d5ded0ca5005898726955ef056761e1964aac7827edc3c2e9f7b0eb51691"),
    ("Tank", 249, "432f3fe2a26a544e5c1cbd97d378e28f673c9d09bf1b64feb6984821ef30d81f"),
    ("TankFactory", 577, "16e49cdfd7405ad41a055b1f73ae57daf90bc7135384576536c3051594e05252"),
)
LEVEL300_HEIGHTFIELD = CORE_ASSETS / "Level300/level300-heightfield.hfld.bin"
WORLD300_HFLD_SIZE = 668_660
WORLD300_HFLD_SHA256 = (
    "68a181f9ec3099a0be52bf4a063350a35e43c20664f2e07572bdb44d472acb1a"
)

LATER_WORLD_ADMISSIONS = (
    _WorldAdmission(
        110,
        WORLD110_ARCHIVE,
        WORLD110_ARCHIVE_SHA256,
        LEVEL110_SCRIPT_ROOT,
        LEVEL110_SCRIPT_OBJECTS,
        LEVEL110_HEIGHTFIELD,
        WORLD110_HFLD_SIZE,
        WORLD110_HFLD_SHA256,
        COMMON_WORLD_LEVEL_HEADER,
    ),
    _WorldAdmission(
        200,
        WORLD200_ARCHIVE,
        WORLD200_ARCHIVE_SHA256,
        LEVEL200_SCRIPT_ROOT,
        LEVEL200_SCRIPT_OBJECTS,
        LEVEL200_HEIGHTFIELD,
        WORLD200_HFLD_SIZE,
        WORLD200_HFLD_SHA256,
        WORLD200_LEVEL_HEADER,
    ),
    _WorldAdmission(
        300,
        WORLD300_ARCHIVE,
        WORLD300_ARCHIVE_SHA256,
        LEVEL300_SCRIPT_ROOT,
        LEVEL300_SCRIPT_OBJECTS,
        LEVEL300_HEIGHTFIELD,
        WORLD300_HFLD_SIZE,
        WORLD300_HFLD_SHA256,
        WORLD300_LEVEL_HEADER,
    ),
)
BASE_ARCHIVE = "data/resources/base_res_PC.aya"
BASE_ARCHIVE_SHA256 = "0ee8530874425cac759834872f5941bc4be086c40ce6b70553b5c6b539802883"
SOUND_BANK = "data/sounds/sounds_english_pc.xap"
SOUND_BANK_SHA256 = "658c15e3bab844d65dd3c07c4ac880f16f741c0ea116f48c603449bbd4dda8b7"
PHYSICS_DEFINITIONS = "data/default physics.dat"
PHYSICS_DEFINITIONS_SHA256 = (
    "e1fb3dedbeb29b4b4151da2c8cbbdc940b716b1a2321e1d6a9ba1542c74ada14"
)
# ``CPhysicsScriptStatements__CreateStatementType12`` at 0x0043DDC0 creates
# the serialized behaviour leaf. ``CUnitBehaviour__ApplyToUnitData`` at
# 0x00433010 then calls leaf vtable slot 1 and stores this released selector at
# definition +0xE0. Type 2 is the intentional Jeep/selector-11 discontinuity;
# type 12 resumes the otherwise sequential selector values.
UNIT_BEHAVIOR_SELECTOR_BY_SERIALIZED_TYPE: tuple[int, ...] = (
    0, 11, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 13,
    14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25,
)
ENGLISH_LANGUAGE_TABLE = "data/language/english.dat"
ENGLISH_LANGUAGE_TABLE_SHA256 = (
    "789ecff619d077092769df281c540d138a25fcc74d70023466a604888e59371a"
)
LEVEL100_MISSION_DATA_ROOT = GODOT_ASSETS / "Level100/MissionData"
LEVEL100_LEVEL_SCRIPT = LEVEL100_MISSION_DATA_ROOT / "LevelScript.msl"
LEVEL100_LEVEL_SCRIPT_SHA256 = (
    "d51f8864564b5bde872092ec822df5af49daac16563f500719135f1a8c6c04a4"
)
LEVEL100_ENGLISH_SOURCE = LEVEL100_MISSION_DATA_ROOT / "English.txt"
LEVEL100_ENGLISH_SOURCE_SHA256 = (
    "ee48f3bed1c3c872ccc975146318aa0b5da3df88bff6b0a60671f0d23f9ce478"
)
LEVEL100_TEXT_STF = LEVEL100_MISSION_DATA_ROOT / "text.stf"
LEVEL100_TEXT_STF_SHA256 = (
    "fd318d6c2304eb8ffcfa718357c1715aadad69915f39851b19f442d8263b56ae"
)
LEVEL100_ENGLISH_DAT = LEVEL100_MISSION_DATA_ROOT / "english.dat"
LEVEL100_ENGLISH_DAT_SHA256 = ENGLISH_LANGUAGE_TABLE_SHA256
LEVEL100_HUD_MANIFEST = LEVEL100_MISSION_DATA_ROOT / "level100-hud-events.json"
LEVEL100_HUD_MANIFEST_SHA256 = (
    "af0b389df897ab8f1404edf35280f3eac10e53ad0c064457f4df97b6ff4335d1"
)
FRONTEND_LOCALIZATION = GODOT_ASSETS / "Frontend/english.json"
FRONTEND_LOCALIZATION_SHA256 = (
    "b27d7b1b3f8cd8aa22b664cacf7c87a8b0907c7dea4c4f07dff8da763dbb70f3"
)
# The released 43-node career graph (Stuart Career.cpp:24-70), world column
# in node order — the same table RetailWorldCatalog pins. It orders the
# briefing-slot groups below and cross-checks the level-name rows.
CAREER_WORLD_ORDER: tuple[int, ...] = (
    100, 110, 200, 211, 212, 221, 222, 231, 232,
    300, 311, 312, 321, 322, 331, 332,
    400, 411, 412, 421, 422, 431, 432,
    500, 511, 512, 521, 522, 523, 524,
    600, 611, 612, 621, 622,
    700, 710, 720, 731, 732, 741, 742, 800,
)
# Per-world frontend strings decoded from the same English language table:
# the selector's per-node name rows ("1.00 - Training Level", "1.10 -
# Blackout", …) and each career world's two briefing body slots. Measured
# 2026-08-22; see _frontend_world_strings_bytes for the slot law.
FRONTEND_WORLD_STRINGS = GODOT_ASSETS / "Frontend/english-worlds.json"
FRONTEND_WORLD_STRINGS_SHA256 = (
    "ffe3d3f88e07d5f29d21d26ef07bf056d153b622416110250dc1c78bf2c35408"
)
# CFEPMain underlay: data/video/FEBack128.vid (BIKi 128² @30fps) decoded to a
# lean rgb24 strip at the SHIPPED rate for Godot Control draw (stretched to the
# 640×480 stage).
#
# It used to decode at fps=15 / 286 frames — exactly half the shipped rate. That
# was recorded as a defect on 2026-07-25 and left in place because nothing drew
# the strip. It is now drawn (the main-menu underlay), and a half-rate strip
# cannot be at the right phase at ANY instant, so the decimation is removed here
# rather than compensated for downstream.
#
# The `-vf fps=N` argument is deliberately absent: passing a frame-rate filter is
# what produced the decimation, and RetailStartupSequenceTests already asserts
# the startup decode passes no `-r` / `fps=` / `-vf` rate argument for the same
# reason. `format=rgb24` is a pixel-format conversion, not a rate filter.
FEBACK_SOURCE = "data/video/FEBack128.vid"
FEBACK_SOURCE_SHA256 = (
    "c251f4be8ab7f2ac5d4f6b952ca44d0cf5aadd7552ad61725420009a6f0e79ba"
)
FEBACK_STRIP = GODOT_ASSETS / "Frontend/Backgrounds/fe-back-128x128x30.rgb"
FEBACK_STRIP_SHA256 = (
    "6ba092b1b43959db8eb73f6d0b9434addbdcf5ddc030e10d8d35c11208001265"
)
FEBACK_STRIP_WIDTH = 128
FEBACK_STRIP_HEIGHT = 128
FEBACK_STRIP_FPS = 30
FEBACK_STRIP_FRAME_COUNT = 572
FEBACK_STRIP_FRAME_BYTES = FEBACK_STRIP_WIDTH * FEBACK_STRIP_HEIGHT * 3
FEBACK_STRIP_BYTES = FEBACK_STRIP_FRAME_BYTES * FEBACK_STRIP_FRAME_COUNT
ROOT_TERRAIN_TEXTURE = (
    GODOT_ASSETS / "Level100/Source/level100-root-terrain.rgb565.bin"
)
# Exact initial root texture produced by the released compositor reconstructed
# below. The materializer refuses to publish if those pixels do not reproduce.
ROOT_TERRAIN_TEXTURE_SHA256 = (
    "6eb202f450926097930bedca440f0163a1886572981e3c69b4edf9289a68ae2b"
)
TERRAIN_HIERARCHY_SOURCE = (
    GODOT_ASSETS / "Level100/Source/level100-terrain-hierarchy.bin"
)
# Compact runtime source for the five released 512x512 logical landscape
# caches. This contains only the exact indexed MAPT levels and the Level 100
# mixer/lighting/shadow records needed to rebuild camera-local cache tiles.
TERRAIN_HIERARCHY_SOURCE_SHA256 = (
    "541eacd0aa75fae8befb8a3e1505ea52ae6b1f6c1367c15c65d7dd23b7cfe977"
)

LANDSCAPE_MAP_TEXTURES = (
    # Logical cache level, source width, framed MAPT size, exact framed hash.
    (0, 16, 7_788, "04aa2a1630e427a2916c5d3a4ba2be676d5b575022f559de1753d2101727b711"),
    (1, 32, 12_396, "cf7ed8810bd323d404c88e66bcc6789dc30b1179b4270551cd04423022c2ef77"),
    (2, 64, 30_828, "ec9d2f0dbaff2189610a98aa4aac5845463bff55db4b9919b1ee1b94facd269f"),
    (3, 128, 104_556, "dc29acbb0941515080e55c2679de84fc4604f59eed4294a28f471dfeed9b5edb"),
    (4, 256, 399_468, "c21576ae7ea75fa800ab4117c1479aeb70359a1acc84edd9508895eb339612f1"),
)
STATIC_WORLD_ROOT = GODOT_ASSETS / "Level100/StaticWorld"
STATIC_WORLD_MANIFEST = STATIC_WORLD_ROOT / "level100-static-world.json"
STATIC_WORLD_ANIMATION = STATIC_WORLD_ROOT / "level100-static-world-animation.json"
# The released engine drives hierarchy playback from its 20 Hz base update; the
# mesh format itself stores no rate. See OnslaughtRebuild.Core/SimulationConstants.cs
# and FirstFlightWorldView.RetailAquilaAnimationHz.
STATIC_WORLD_ANIMATION_HZ = 20
STATIC_WORLD_ANIMATION_SHA256 = (
    "b826dc2d1f62069e3285f4f8cddeafb84511519b32065cf6c028615498afa9c3"
)
# Exact released virtual-frame counts (CMSP+0xBC) of every Level 100 static mesh
# that carries more than one authored hierarchy pose. These are the playback
# lengths; nothing here is resampled.
STATIC_WORLD_ANIMATED_MESHES = {
    "FB_Docks": 26,
    "FB_Solar_Pod": 11,
    "FB_radar_station": 26,
    "ft_blaster": 41,
    "ft_pulse": 101,
    "ft_sam": 21,
}
# Reproducibility pin for the v14 manifest: authored waypoint coordinates,
# up-positive Core positions, and signed retail-to-Core bases. The 2026-09-08
# Q*B*inverse(Q) correction changes 237 +0 words to -0 across 44 actors and
# 10 spawns. The subsequent Trainer-life correction changes only the authored
# Flyby and AirTrainer spawn initialHealth from 0 to 3000, from physics field3.
# Keep this pin aligned with Level100ActorDefinitionManifest.ExpectedManifestSha256.
STATIC_WORLD_MANIFEST_SHA256 = "350ac9c4fdcccecbbea9f6c0d3cb7099ee915df8971123e6a030bbf574168561"
STATIC_WORLD_SOURCE_AGGREGATE_SHA256 = (
    "67015b3f37422e18116b84b6245958509e847f09d27f696145ae88fb88fb3f2c"
)
LEVEL100_CONTACT_ASSET = (
    CORE_ASSETS / "Level100/level100-contact-owners.json"
)
LEVEL100_CONTACT_ASSET_SHA256 = (
    "f793060abf3cd958de26a63100c5362a72bf606b5d5ce9e0b2e67a7698b72681"
)
# Retained historical identity for the 24 contact-source meshes. This generator
# emits the literal; its recipe is not recomputed here. The complete generated
# asset hash is the current integrity gate.
LEVEL100_CONTACT_SOURCE_AGGREGATE_SHA256 = (
    "8d85c9bfbe366c815e00d3900d8d29b71a33bef7a60cddfce9ed6ac558e06b4c"
)
# `Target Drone` and `Air Trainer` both carry CUnitMesh `fa_f24_training.msh`,
# so one retention and one conversion serve both. It used to be read and
# hash-verified in place instead of retained, because nothing drew it; task #114
# gives it a renderer, so it is now an ordinary `DIRECT_ASSETS` row below and
# the in-place special case that stood in for it is gone.
PINE_IMPOSTER_TEXTURE = (
    "data/resources/dxtntextures/Imposters_100(0)A1R5G5B5.aya",
    "7368ba0c586221ff1b1572cee8f84de2bf6db426c005a73a10bad54a938ad882",
)
# Retail's AUTHORED default, read from the pristine specimen
# local-lab/safe-copy-bea-pristine/BEA.exe.original.backup (sha256
# 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750).
# It is deliberately NOT read from defaultoptions.bea, which is per-machine run
# state; see GOAL.md's defaults rule and task #137.
#
# The "Geometry detail:" setter at VA 0x004DD6B0 dispatches its state argument
# to exactly three arms, each a `mov dword [0x006321A0], imm32`
# (opcode bytes C7 05 A0 21 63 00):
#     0x004DD737  00 00 20 41 = 10.0  Low     (bias 3.0, scale 0.1)
#     0x004DD6FF  00 00 F0 41 = 30.0  Medium  (bias 1.0, scale 1.0)
#     0x004DD6CA  00 00 8C 42 = 70.0  High    (bias 0.3, scale 2.0)
# The image's own static initialisers pick out exactly one of those arms:
# .data 0x006321A0 (file 0x2321A0) = 00 00 F0 41 = 30.0, LOD bias 0x00631E88
# (file 0x231E88) = 1.0, quality scale 0x00630E0C (file 0x230E0C) = 1.0. That is
# arm 1 and only arm 1, so the released out-of-box Geometry detail is Medium and
# the authored mesh-quality distance is 30.0.
#
# 70.0 was one workstation's "High" choice, not a shipped value. This machine's
# defaultoptions.bea holds 70.0/0.3/2.0 at file offsets 0x26CA/0x26CE/0x26D2
# while proof_defaultoptions.bea from the same install holds 30.0/1.0/1.0 at the
# identical offsets, and no .bea appears anywhere in INSTALL.LOG's 5,773
# installed-file lines. OptionsTail +0x0C is bound to 0x006321A0 by the
# serializer itself: 0x00420E0F `A3 A0 21 63 00` on load, 0x00420BA0
# `8B 0D A0 21 63 00` on save.
PINE_MESH_QUALITY_DISTANCE = 30.0
# Steam's fast standing view is address-derived, but the exact runtime owner
# sequence and phase are unresolved. This reconstruction-owned phase makes the
# four-view assignment deterministic without inferring a retail heap identity.
PINE_FAST_STANDING_VIEW_PHASE = 0
WATER_SURFACE_RESOURCE = STATIC_WORLD_ROOT / "Source/level100-water-surface.surf.bin"
WATER_SURFACE_SHA256 = "c3177354fed3eb5a94dc72debf2465c32ab1d931de79e5e88ac431043d3e917d"
WATER_TEXTURES = (
    (
        "water-reflection-00",
        "data/resources/dxtntextures/mixers%reflection00.tga(0)X8R8G8B8.aya",
        "41117238976776b114b8af4d1e4fbccd3afb90245f46f59b353e83663cac7b6e",
    ),
    (
        "water-caustic-00",
        "data/resources/dxtntextures/mixers%caustic00.tga(0)A8R8G8B8.aya",
        "7f34ee7d90ca483893c3ed8b0bf01bdf07b9a0b0f4a48f9df5fefd961d796f0a",
    ),
    (
        "water-waves",
        "data/resources/dxtntextures/mixers%waves.tga(0)R5G6B5.aya",
        "6ec848d1f9801be12f3a6591d6a4f5d5ecf1fc9f21d1a4242e1d681d826ab078",
    ),
    (
        "water-sun-blob",
        "data/resources/textures/mustbe_sunblob.tga(0)A8R8G8B8.aya",
        "5d97f24f514383c928c58c7f333bf489888b6a402004213ffbaaaad2ef30a53e",
    ),
    (
        "water-sun-reflection",
        "data/resources/textures/mustbe_sunreflect.tga(0)A8R8G8B8.aya",
        "a65940d6cdfe93f8b8820efb883fd33166aec63863ed894673466f3f58527ab4",
    ),
)


# Released physics-definition name -> exact loose mesh selected by Level 100.
STATIC_MESH_BY_DEFINITION = {
    "Control Tower": "fb_control_tower",
    "Forseti Pulse Tank Factory": "fb_tank_factory",
    "Forseti Repair Pad": "fb_health_pad",
    "SAT Turret": "ft_sam",
    "Iceberg 1": "iceberg1",
    "Iceberg 2": "iceberg2",
    "Iceberg 3": "iceberg3",
    "Iceberg 4": "iceberg4",
    "Blaster Turret": "ft_blaster",
    "Pulse Turret": "ft_pulse",
    "Forseti Research Building": "fb_research",
    "Forseti Building 1": "FB_House_Type_A",
    "Forseti Building 2": "FB_House_Type_B",
    "Forseti Building 3": "FB_House_Type_C",
    "Forseti Solar Pod": "FB_Solar_Pod",
    "Forseti Radar Station": "FB_radar_station",
    "Forseti Light Fighter Airfield": "fb_aircraft_factory",
    "Forseti Docks": "FB_Docks",
    "Hangar": "fb_hangar",
    "Forseti Tall Building 1": "F_buildtall1",
    "Forseti Tall Building 3": "F_buildtall3",
    "Forseti City Building 1": "f-city1",
    "Forseti City Building 2": "f-city2",
    "Forseti City Building 3": "f-city3",
}
PINE_MESH_KEYS = tuple(f"pinesnow{variant}" for variant in range(4))
STATIC_MESH_KEYS = tuple(dict.fromkeys(STATIC_MESH_BY_DEFINITION.values())) + PINE_MESH_KEYS
PINE_MESH_SHA256 = (
    "d428a6ed49d460a5729c357618c3837e399e7d265ae6127899cd1911eb7a0481",
    "51984637f702b0215f0bd841977d8c9602201fb9f8392f7b110e285cde1ed102",
    "d717735ac741f7267b746546feebece60e7515048c0fdf46586c79e85c4087eb",
    "f9f5031421b5c9913a2f8865cabe31feb25e692e0c33934e1a537ab675793b3a",
)


# Destination, released-install source, exact supported SHA-256.
DIRECT_ASSETS = (
    (GODOT_ASSETS / "Aquila/Source/m_cockpit2.msh.aya", "data/resources/meshes/m_cockpit2.msh.aya", "008b9292c59a5564ba3696f65d5bd51030d3e57250bc792d9d2b7f01292cdd4a"),
    (GODOT_ASSETS / "Aquila/Source/m_f_be1.msh.aya", "data/resources/meshes/m_f_be1.msh.aya", "d4c8fa752229af4111b31efa5ff5928c892736faa6a807915412767f3cd3c6b2"),
    (GODOT_ASSETS / "Aquila/Source/m_f_be2.msh.aya", "data/resources/meshes/m_f_be2.msh.aya", "35aada1313c3cbb796ba75db071321035f7005096da7c148a7514944f4772b4c"),
    (GODOT_ASSETS / "Aquila/Textures/be-tex-a.texture.aya", "data/resources/dxtntextures/meshtex%BE_texA.tga(0)A1R5G5B5.aya", "86f9f54ae97ba4e3782c65909d1d93b86566228b1132829ebb93816eb5a4705b"),
    (GODOT_ASSETS / "Aquila/Textures/be-tex-b.texture.aya", "data/resources/dxtntextures/meshtex%BE_texB.tga(0)A1R5G5B5.aya", "ea01431a4023abd517daf5a27066eb7edf706100fb3991566726fb4530490b60"),
    (GODOT_ASSETS / "Aquila/Textures/bluegun-light.texture.aya", "data/resources/dxtntextures/meshtex%A8_bluegunlight_LIT.tga(0)A8R8G8B8.aya", "85858e7809a974b74f3db5a169e081fc9dd506558f1ca99fa47c7832d8552fc5"),
    (GODOT_ASSETS / "Aquila/Textures/cockpit.texture.aya", "data/resources/dxtntextures/meshtex%cockpit.tga(0)A1R5G5B5.aya", "c62d0c668226f056db7455c8a5a8fa7d55ab7621ade1e58392d6aaad3c00f0cc"),
    (LEVEL100_LEVEL_SCRIPT, "data/MissionScripts/level100/LevelScript.msl", LEVEL100_LEVEL_SCRIPT_SHA256),
    (LEVEL100_ENGLISH_SOURCE, "data/MissionScripts/level100/English.txt", LEVEL100_ENGLISH_SOURCE_SHA256),
    (LEVEL100_TEXT_STF, "data/MissionScripts/text/text.stf", LEVEL100_TEXT_STF_SHA256),
    (LEVEL100_ENGLISH_DAT, ENGLISH_LANGUAGE_TABLE, LEVEL100_ENGLISH_DAT_SHA256),
    (GODOT_ASSETS / "Hud/bar-line.texture.aya", "data/resources/dxtntextures/hud%v2%BarLine.tga(0)X8R8G8B8.aya", "16796e3a8acfec3529e03c29afbefbe28c92ffccd5b05574f992e8f31976704d"),
    (GODOT_ASSETS / "Hud/battleline-marker.texture.aya", "data/resources/dxtntextures/hud%marker.tga(0)A8R8G8B8.aya", "ab14538237bdd38486add4f5e9f38cfca0069496ceee5faf1268e50eab319be7"),
    (GODOT_ASSETS / "Hud/battleline-outline.texture.aya", "data/resources/dxtntextures/hud%v2%BattleLineOutline.tga(0)X8R8G8B8.aya", "b1c097b29dd81e2a0493f72a157ccd5ad249b5abf758224c75df4f93973d0405"),
    (GODOT_ASSETS / "Hud/circle-darkener.texture.aya", "data/resources/dxtntextures/hud%v2%CircleDarkener.tga(0)A8R8G8B8.aya", "7bd18594757165dcdd8dadb618ea99eb500ed105dbe2d6a6f66bbcbc31c323a3"),
    (GODOT_ASSETS / "Hud/circle-mask.texture.aya", "data/resources/dxtntextures/hud%v2%CircleMask.tga(0)A8R8G8B8.aya", "14d809f9b45f5153f82fa1f80152690b554710d83f91b8cbe203de5cf18a9dfa"),
    (GODOT_ASSETS / "Hud/compass-objective-marker.texture.aya", "data/resources/dxtntextures/hud%v2%CompassObjectiveMarker.tga(0)A8R8G8B8.aya", "e24fca83de34646a7328c313e7b89ac02c6bc4b04a69a74bf3ee81b3d57283df"),
    (GODOT_ASSETS / "Hud/crosshair-dot.texture.aya", "data/resources/dxtntextures/hud%v3%hud_crosshair_dot.tga(0)A8R8G8B8.aya", "19e1b35b885a36230e5a1d47a9910164b0ca177746649a15c146cefca29651dd"),
    (GODOT_ASSETS / "Hud/crosshair-enemy.texture.aya", "data/resources/dxtntextures/hud%Crosshair Enemy.tga(0)A8R8G8B8.aya", "6a5c0d6dc22ea911ba783e630b5f623ab027ff45da094c5816263f7b87db98ea"),
    (GODOT_ASSETS / "Hud/crosshair-friend.texture.aya", "data/resources/dxtntextures/hud%Crosshair Friend.tga(0)A8R8G8B8.aya", "75b827ac9560846ad85904032249f707d84a74a432d3b732116f0e89a3621895"),
    (GODOT_ASSETS / "Hud/crosshair-outline.texture.aya", "data/resources/dxtntextures/hud%Crosshair Outline.tga(0)A8R8G8B8.aya", "1e27b9771f3d0416c07eff9aede6863ca6f081386c86259f635e3d9a02ade5b2"),
    (GODOT_ASSETS / "Hud/crosshair-predictor.texture.aya", "data/resources/dxtntextures/hud%Crosshair Predictor.tga(0)A8R8G8B8.aya", "519b30e4edee809d3fe660ccb866feeea2ace4485b75373c98293afb6e730aa5"),
    (GODOT_ASSETS / "Hud/crosshair-primary.texture.aya", "data/resources/dxtntextures/hud%v3%hud_crosshair_primary.tga(0)A8R8G8B8.aya", "310dae2f7dd976f6cc724604737726885aff96ab6bc507e41f90dca60d134b17"),
    (GODOT_ASSETS / "Hud/crosshair-secondary.texture.aya", "data/resources/dxtntextures/hud%v3%hud_crosshair_secondary.tga(0)A8R8G8B8.aya", "7b078344e64d1e78ef64a8e21bdd3787e059b628c6a442634e9d13ba7d3a0487"),
    (GODOT_ASSETS / "Hud/damage-flash.texture.aya", "data/resources/dxtntextures/hud%v2%DamageFlash.tga(0)X8R8G8B8.aya", "8a25ec2e0ba8e66d86217684125d2e245dae3eddeb327ee2a7b144e2b45391c0"),
    (GODOT_ASSETS / "Hud/dial.raw", "data/Dial.raw", "2c57b657b92cd8bd73ca8c8986e8ce60aaffb065fdde09940053a2dd6d59671c"),
    # [hud+0x1d4] in CHud__LoadTextures (0x00481650), from the string at
    # 0x0062ceb0 - the sprite CHud__RenderBattleline draws when no message is
    # active and CInfluenceMapManager is empty. 64x64 DXT2.
    (GODOT_ASSETS / "Hud/forseti-icon.texture.aya", "data/resources/dxtntextures/hud%ForsetiIcon.tga(0)A8R8G8B8.aya", "0093b1a1a1e89327b90cef93f06882dded060a3023510ca0f49e6ebbf3666da1"),
    (GODOT_ASSETS / "Hud/font-13ps.texture.aya", "data/resources/textures/mustbe_Font13PS.tga(0)A8R8G8B8.aya", "7acc088b75e729cbdc2782e239a7d18ba0ec409e1bc890109aa1020f5ee81dc0"),
    (GODOT_ASSETS / "Hud/font-22.texture.aya", "data/resources/textures/mustbe_font22.512.tga(0)A8R8G8B8.aya", "acc8dbdd60839c0f9686250025672cf9c370530114bc44910df09be487b247c6"),
    (GODOT_ASSETS / "Hud/guns-darken.texture.aya", "data/resources/dxtntextures/hud%v2%GunsDarken.tga(0)A8R8G8B8.aya", "0054e526a0980c5f89fc1271e7d34cfb551fc2567f6259f0be11d63100ab12b1"),
    (GODOT_ASSETS / "Hud/guns-front.texture.aya", "data/resources/dxtntextures/hud%v2%GunsFront.tga(0)X8R8G8B8.aya", "4ce73b693e83c01812ebca6ddd64edcfbebd4f85720ab7f5b87e425c9ece1a06"),
    (GODOT_ASSETS / "Hud/guns-outline.texture.aya", "data/resources/dxtntextures/hud%v2%GunsOutline.tga(0)X8R8G8B8.aya", "e80dc5fb51a2c29dc696d85b3bea036c427261d1bbe8f1db14a531fba996cffd"),
    (GODOT_ASSETS / "Hud/guns-side.texture.aya", "data/resources/dxtntextures/hud%v2%GunsSide.tga(0)X8R8G8B8.aya", "52d46b47d26d6c779fe8a7e0e77dc13f6bcf91849869ea9ec81c3fa190164594"),
    (GODOT_ASSETS / "Hud/guns-top.texture.aya", "data/resources/dxtntextures/hud%v2%GunsTop.tga(0)X8R8G8B8.aya", "d8b09af12efad59e9a25b91930bf9b8215f028c4e766eacc815c073a5e63304f"),
    (GODOT_ASSETS / "Hud/kramer-portrait-oo.texture.aya", "data/resources/dxtntextures/MessageBox%FC_Kramer_oo.tga(0)A8R8G8B8.aya", "f4887e775c5ebcac182b5caabae0c11ffe3fab8aa32ff14ddcd9ce33d1bd50b3"),
    (GODOT_ASSETS / "Hud/kramer-portrait-ee.texture.aya", "data/resources/dxtntextures/MessageBox%FC_Kramer_ee.tga(0)A8R8G8B8.aya", "0b34a213a25e0ebc48a74f8cf73cf27bbb817f4cdffbc79cd294a7d2858b9bde"),
    (GODOT_ASSETS / "Hud/kramer-portrait-mm.texture.aya", "data/resources/dxtntextures/MessageBox%FC_Kramer_mm.tga(0)A8R8G8B8.aya", "69ed961309997f300be094b6ba899d182b91c145fbf07f80fc2e0f8c8d149689"),
    (GODOT_ASSETS / "Hud/kramer-portrait.texture.aya", "data/resources/dxtntextures/MessageBox%FC_Kramer_aa.tga(0)A8R8G8B8.aya", "c6fe448e1d88e52c8abc0ad96c386b774b8d979ab07eb6ff2a4cbf069e99106b"),
    (GODOT_ASSETS / "Hud/message-noise.texture.aya", "data/resources/dxtntextures/MessageBox%noisebig.tga(0)X8R8G8B8.aya", "f5c43c330394db9eb7c1e782f3f30fe847de01d7ce9335d2c7f9fd24babb1825"),
    (GODOT_ASSETS / "Hud/objective-inner-centre.texture.aya", "data/resources/dxtntextures/hud%v2%ObjectiveInnerCentre.tga(0)A8R8G8B8.aya", "fc42774e8c4f4534b65009807bfdb333443a9f5202c6a2c59dff0dddbed4f55b"),
    (GODOT_ASSETS / "Hud/objective-inner-left.texture.aya", "data/resources/dxtntextures/hud%v2%ObjectiveInnerLeft.tga(0)A8R8G8B8.aya", "70030aace505e8e3d7f56dde0b9c6a929f3d2c61912fc03ac816746b6d8a96bd"),
    (GODOT_ASSETS / "Hud/objective-inner-right.texture.aya", "data/resources/dxtntextures/hud%v2%ObjectiveInnerRight.tga(0)A8R8G8B8.aya", "7c85b0293fc7a524978a21e7cdc06b1dd3308e9a595aadca054b51cd9a6aa113"),
    (GODOT_ASSETS / "Hud/objective-left.texture.aya", "data/resources/dxtntextures/hud%v2%ObjectiveLeft.tga(0)A8R8G8B8.aya", "0ae835780d1af6c01f0272a50afda141abca70eaa5c23d74e7fc3968b6d9194f"),
    (GODOT_ASSETS / "Hud/objective-right.texture.aya", "data/resources/dxtntextures/hud%v2%ObjectiveRight.tga(0)A8R8G8B8.aya", "581f10446db76ece7aa7044b4c02f0431a79a7d606225d5c69a412c17f85078b"),
    (GODOT_ASSETS / "Hud/radar-outline.texture.aya", "data/resources/dxtntextures/hud%v2%RadarOutline.tga(0)X8R8G8B8.aya", "507d465248f7321f2332413b2c6f461f3b3c45d87c52d86c38c43104043d7dc7"),
    (GODOT_ASSETS / "Hud/radio-north.texture.aya", "data/resources/dxtntextures/hud%v2%RadioNorth.tga(0)A8R8G8B8.aya", "e5dfd8db4dd73e9aeeffbb009fca68d889572c996987bce365b0b5b4d0a7ed85"),
    (GODOT_ASSETS / "Hud/radio-view.texture.aya", "data/resources/dxtntextures/hud%v2%RadioView.tga(0)A8R8G8B8.aya", "888d5a70ab812e23f75db76ab2ed71cd2cce04191ee282d525c86e337cc01778"),
    (GODOT_ASSETS / "Hud/scanner-blob-medium.texture.aya", "data/resources/dxtntextures/hud%ScannerBlobMedium.tga(0)A8R8G8B8.aya", "b7f9d3d0b6a2c80933673514b8d032589b1de2e4c60bcc362df39e6244d71853"),
    (GODOT_ASSETS / "Hud/scanner-blob-large.texture.aya", "data/resources/dxtntextures/hud%ScannerBlobLarge.tga(0)A8R8G8B8.aya", "791327844e9700103afad1572de3b4426e48088ed958033b227e256ffbed4152"),
    (GODOT_ASSETS / "Hud/scanner-blob-repair-pad.texture.aya", "data/resources/dxtntextures/hud%ScannerBlobRepairPad.tga(0)A8R8G8B8.aya", "bddf99cbdd816a5ecd32e04e01a1f2db356202b64015426e3aff446268ec6469"),
    (GODOT_ASSETS / "Hud/scanner-blob-small.texture.aya", "data/resources/dxtntextures/hud%ScannerBlobSmall.tga(0)A8R8G8B8.aya", "d7e9d287536f23e67bf35f678ec75d1a349353ae1d9d00b87ce09f6bd03641e4"),
    (GODOT_ASSETS / "Hud/screen-marker.texture.aya", "data/resources/dxtntextures/hud%v3%ScreenMarker.tga(0)X8R8G8B8.aya", "c5ec9c34386546a327a0e7188156e9a6b0259feda301320c111b1b7062c30a49"),
    (GODOT_ASSETS / "Hud/tatiana-portrait-oo.texture.aya", "data/resources/dxtntextures/MessageBox%tat_oo.tga(0)A8R8G8B8.aya", "39f40088069a8c68584a5a0cda9e5ae7d4e4e5a248a12f0d0a240b8d3668621e"),
    (GODOT_ASSETS / "Hud/tatiana-portrait-ee.texture.aya", "data/resources/dxtntextures/MessageBox%tat_ee.tga(0)A8R8G8B8.aya", "4a4a17b72bbafae2b324e3a0a1c847226a288feaa7c4273c45aa2de8aea3f99a"),
    (GODOT_ASSETS / "Hud/tatiana-portrait-mm.texture.aya", "data/resources/dxtntextures/MessageBox%tat_mm.tga(0)A8R8G8B8.aya", "802d8e22d8d304e12589a547f22ac2f2d5771b96aea47306b3b2bbf752730de5"),
    (GODOT_ASSETS / "Hud/tatiana-portrait.texture.aya", "data/resources/dxtntextures/MessageBox%tat_aa.tga(0)A8R8G8B8.aya", "34d451a6fc31e399b99032230413a60f146b41a0fea65e61561a37d8ec757cfd"),
    (GODOT_ASSETS / "Hud/technician-portrait-oo.texture.aya", "data/resources/dxtntextures/MessageBox%technic_oo.tga(0)A8R8G8B8.aya", "b28a3818b8ef37decfd8779d7acae74c657b5d510edd2332587864f2a1e58a2c"),
    (GODOT_ASSETS / "Hud/technician-portrait-ee.texture.aya", "data/resources/dxtntextures/MessageBox%technic_ee.tga(0)A8R8G8B8.aya", "05326c603e8c9224c5bab488a32ab9e9e19ca5b3fb424bc700af97ae71c2527f"),
    (GODOT_ASSETS / "Hud/technician-portrait-mm.texture.aya", "data/resources/dxtntextures/MessageBox%technic_mm.tga(0)A8R8G8B8.aya", "263a2c107d6463a717ddef20cc113cadfb585bd8ecbb1db479f049843dcf3636"),
    (GODOT_ASSETS / "Hud/technician-portrait.texture.aya", "data/resources/dxtntextures/MessageBox%technic_aa.tga(0)A8R8G8B8.aya", "c4c1b11f4ddfb960afc1c1d2a04020fadf997795eccf651c07314141652f9603"),
    (GODOT_ASSETS / "Hud/target-sighted.texture.aya", "data/resources/dxtntextures/hud%v3%hud_target_sighted.tga(0)A8R8G8B8.aya", "6caafcdc20228617b86bd7f4010e718e1d681ee52b0e904d73e6f16369bcaa88"),
    (GODOT_ASSETS / "Hud/threat-flash.texture.aya", "data/resources/dxtntextures/hud%v2%ThreatFlash.tga(0)X8R8G8B8.aya", "43f5fb70d318f4969f43147c225a399d233edcea577592f14876ac542bd57f50"),
    (GODOT_ASSETS / "Hud/weapon-fill.texture.aya", "data/resources/dxtntextures/hud%v2%WeaponFill.tga(0)A8R8G8B8.aya", "e639910d70ae10b044423cd5025c300c61cb8a9b5765890fd1a011c7d4499c0d"),
    (GODOT_ASSETS / "Hud/weapon-plasma-cannon.texture.aya", "data/resources/dxtntextures/hud%Weapon Plasma Cannon.tga(0)A8R8G8B8.aya", "d5165347be203c17f302250ae20e83ae5b9525938b15ca8c6aceb0d4392f06df"),
    (GODOT_ASSETS / "Hud/weapon-vulcan-cannon.texture.aya", "data/resources/dxtntextures/hud%Weapon Vulcan Cannon.tga(0)A8R8G8B8.aya", "20d3c6d31b2815af0b0a7de5aa993164d911ac527205b40892f19f07ee34f3b8"),
    (GODOT_ASSETS / "Hud/weapon-outline.texture.aya", "data/resources/dxtntextures/hud%v2%WeaponOutline.tga(0)X8R8G8B8.aya", "2e2da786db82c8fd76de36d8d71fe744ddddd364247467cba1bfb9a95e52d62b"),
    (GODOT_ASSETS / "Hud/offscreen-arrow.texture.aya", "data/resources/dxtntextures/hud%v3%offscreenarrow.tga(0)A8R8G8B8.aya", "8bbf5f0898a7796668e8a1a2bc6393c077b14beb7623585856709bdd937d35d2"),
    (GODOT_ASSETS / "PauseMenu/blank.texture.aya", "data/resources/dxtntextures/FrontEnd%v2%FE_Blank.tga(0)A8R8G8B8.aya", "01d55e16e994dec02191e6f62adcb5a82a1f0ca3b5629e489c9e5c82884fe7e9"),
    (GODOT_ASSETS / "PauseMenu/circle-01.texture.aya", "data/resources/dxtntextures/pausemenu%pause_circle01.tga(0)A8R8G8B8.aya", "01622d0e442aef1ccd5d5971cad5b4c06d2b15d1980dd07a1ddf95a7bc7472c7"),
    (GODOT_ASSETS / "PauseMenu/circle-02.texture.aya", "data/resources/dxtntextures/pausemenu%pause_circle02.tga(0)A8R8G8B8.aya", "4cd5e553a8a093d3a895f3639136fb6548267371c28914b00d3ccbb3c153516f"),
    # CMessageLog__RenderPanelFrame (0x004B9010) corner cell: one 32x32 DXT2
    # quarter curve, opaque toward the lower-left, drawn at the measured 32.0
    # corner size (_DAT_005DB2B8) and flipped per corner.
    (GODOT_ASSETS / "PauseMenu/endcurve.texture.aya", "data/resources/dxtntextures/MessageLog%endcurve.tga(0)A8R8G8B8.aya", "843defb41f22782e01c037d36a1b03b3ab8ba6c81624b9ad6e0b6e61e2568635"),
    (GODOT_ASSETS / "Level100/Sky/cube25-cent.texture.aya", "data/resources/dxtntextures/cubes%cube25_cent.tga(0)X8R8G8B8.aya", "1aad6cc8f85b6bb7ccbb8d2c7b0e6aa31722a9adbde5a3f19b248430ca83469e"),
    (GODOT_ASSETS / "Level100/Sky/cube25-down.texture.aya", "data/resources/dxtntextures/cubes%cube25_down.tga(0)X8R8G8B8.aya", "4770829ba631e93fbc33db2012754da75a06bfccc2fb2b36875e92032e22d19d"),
    (GODOT_ASSETS / "Level100/Sky/cube25-left.texture.aya", "data/resources/dxtntextures/cubes%cube25_left.tga(0)X8R8G8B8.aya", "d7cbce30e51473ddc89ed0c44326e598dac4d2682f64ef20c19237afd2cebe14"),
    (GODOT_ASSETS / "Level100/Sky/cube25-right.texture.aya", "data/resources/dxtntextures/cubes%cube25_right.tga(0)X8R8G8B8.aya", "830c9b965c76a4023c2415b7c8924ca32590562c850cc84e92c003e173263d11"),
    (GODOT_ASSETS / "Level100/Sky/cube25-up.texture.aya", "data/resources/dxtntextures/cubes%cube25_up.tga(0)X8R8G8B8.aya", "419e2424bcfd698058d72111ffa7d84fdc9022e03815db7c0da28403f4925f3c"),
    (GODOT_ASSETS / "Level100/Source/m_f_pulsetank_training.msh.aya", "data/resources/meshes/m_f_pulsetank_training.msh.aya", "9b2cfdceb86ed700ed924051fbff13c32dc30bd8f8b948ea1cf8aa9fbfe8b97b"),
    (GODOT_ASSETS / "Level100/Source/m_f_truck_training.msh.aya", "data/resources/meshes/m_f_truck_training.msh.aya", "3bd92ce93d0619b7c4b0dd158680641fbab6cd88580a68c6ef34e5f22f7596c5"),
    (GODOT_ASSETS / "Level100/Source/m_m_warehouse.msh.aya", "data/resources/meshes/m_m_warehouse.msh.aya", "61fe5465bd7affedf749ad784209be02b2e4dd28631e70386c3810302b5f6f15"),
    # Task #114, the two ambient aircraft. `m_FA_F24_training.msh.aya` is the
    # Air Trainer AND the nine Target Drones: `default physics.dat` records #601
    # and #660 both name `fa_f24_training.msh`, so this single retention serves
    # every airborne unit in the level. `m_f_lifter.msh.aya` is the U-17
    # Highside Transporter (record #636). Retail constructs both aircraft at
    # level load from the level world - MEASURED, `CreateThingByType` creations
    # 28 and 29 out of `CWorld__LoadWorld` in the 2026-07-28 TTD trace.
    (GODOT_ASSETS / "Level100/Source/m_FA_F24_training.msh.aya", "data/resources/meshes/m_FA_F24_training.msh.aya", "48876552ae836750221241719f333fb9b5221f78f1ab8bc03d5950cdbf4e6ec5"),
    (GODOT_ASSETS / "Level100/Source/m_f_lifter.msh.aya", "data/resources/meshes/m_f_lifter.msh.aya", "ced8fdd223e6592b37586395197d86843afec68456efde4c8211b8cd75593df3"),
    # The authored particle sets. `ParticleSetFile` parses them and
    # `ParticleEffectResolver` resolves named effects out of them, so descriptor
    # values stop being hand-copied constants. `MainSet.par` is the one the game
    # loads in-level and is pinned by rebuild/PROVENANCE.md; the client reads
    # `Sun Sprite` out of it for the in-level sun.
    #
    # All three are retained rather than `MainSet.par` alone because the
    # decoder's falsifiability check is a byte-identical re-emission of the
    # WHOLE shipped corpus - 1,479 descriptors over three files. A decoder
    # proven on one file is a decoder proven on one file.
    (GODOT_ASSETS / "Level100/ParticleSets/MainSet.par", "data/ParticleSets/MainSet.par", "a51fe4419b55e1af132e31c6b3cd8133c937745d8f4ab691eb5a0d81017ded06"),
    (GODOT_ASSETS / "Level100/ParticleSets/Frontend.par", "data/ParticleSets/Frontend.par", "01a4c73d7cfc666b4a367736fabd1d91bf3459ed1c538b6ca77f70c069cf8bc6"),
    (GODOT_ASSETS / "Level100/ParticleSets/ModelViewer.par", "data/ParticleSets/ModelViewer.par", "32d85d1f0400f46a45078d49c695967cde60ed572053059fd6246227162115a9"),
    # `Sun Sprite` authors `Blend_Mode 0`, which
    # ParticleEffectResolver.BlendModeSelectsShippedTextureFormat measures as
    # the alpha-less additive copy - and `(0)R5G6B5` is the only format
    # `sun3.tga` ships in at all, so the selection is unambiguous here.
    (GODOT_ASSETS / "Level100/Textures/particle-sun3-additive.texture.aya", "data/resources/dxtntextures/Particle%sun3.tga(0)R5G6B5.aya", "22a6b691ace6d6cc744d33cc10776e70fe08de5b8e6cb25fa4f4bcf61cbe922d"),
    (GODOT_ASSETS / "Level100/Textures/effect-flash-medium.texture.aya", "data/resources/dxtntextures/Particle%sun2.tga(0)R5G6B5.aya", "d7fbfcb4edb2167fedc0a467d4501c9bbc2f6a2852c7873daec3953e6f518f5c"),
    # NOTE 2026-08-13: the format suffix was A4R4G4B4 here from 2026-08-10 until
    # today, copied from the alparticle4 line above. The pinned SHA-256 was always
    # correct and always described the R5G6B5 file; only the name was wrong, so
    # this entry could never resolve and blocked the whole rebuild gate before a
    # single test ran. The engine picks the format per source texture - alparticle4
    # has alpha and alparticle5 does not - so neighbouring particles legitimately
    # differ. Do not "correct" this suffix to match its neighbours again.
    (GODOT_ASSETS / "Level100/Textures/particle-alparticle5-additive.texture.aya", "data/resources/dxtntextures/Particle%alparticle5.tga(0)R5G6B5.aya", "5004b8c6a688b82605f870e60d4ed32a32203b4371f1aec72155fef1619a5fa0"),
    (GODOT_ASSETS / "Level100/Textures/mech-pulse-medium-energy-trail.texture.aya", "data/resources/dxtntextures/Particle%Energy Trail.tga(0)R5G6B5.aya", "64eddc6b147c67886f41ef4d2bcc2a0606b453b01e4d93b9962f10cc07aba92e"),
    (GODOT_ASSETS / "Level100/Textures/mech-pulse-medium-halo.texture.aya", "data/resources/dxtntextures/Particle%Halo.tga(0)R5G6B5.aya", "cde6efc90dc7958c5bda425a04486e277beb85a7f1c33fb9074f369e92d58edb"),
    (GODOT_ASSETS / "Level100/Textures/pulse-bolt-blue-spark.texture.aya", "data/resources/dxtntextures/Particle%Blue Spark 2.tga(0)A4R4G4B4.aya", "b3730b1e9d7713910e0de4bd0cb0dcfefcb9ceb8f6402d50681a524adc0dcb08"),
    (GODOT_ASSETS / "Level100/Textures/pulse-bolt-blue-trail.texture.aya", "data/resources/dxtntextures/Particle%Blue Trail.tga(0)R5G6B5.aya", "2b4bc5cf8902d7ea8452f1068ac8f11514c8238a733ca33aad7d6d0667688a63"),
    (GODOT_ASSETS / "Level100/Textures/vulcan-bullet-trail.texture.aya", "data/resources/dxtntextures/Particle%Bullet.tga(0)R5G6B5.aya", "42da4484967f48958e71c9529435306c284fbbc36fc1f7a6ca2befc1eac2f01c"),
    (GODOT_ASSETS / "Level100/Textures/pulse-impact-animated-blob.texture.aya", "data/resources/dxtntextures/Particle%alparticle4.tga(0)A4R4G4B4.aya", "74085b280199e20b765640cfc3e417e6da0fcbfb25384e129858a32f5deb995d"),
    (GODOT_ASSETS / "Level100/Textures/pulse-impact-shockwave.texture.aya", "data/resources/dxtntextures/Particle%1telep.tga(0)R5G6B5.aya", "e92efc3f5adfa347e6b50f1e3d20af4c6800d76853a2126d71237dfefeea9f10"),
    (GODOT_ASSETS / "Level100/Textures/vulcan-impact-spark.texture.aya", "data/resources/dxtntextures/Particle%alparticle2.tga(0)R5G6B5.aya", "95c15d4269ffea56e7be13ac7fb64a71a999cce2b9417cb73ce9c7313cef4389"),
    (GODOT_ASSETS / "Level100/Textures/target-tank-explosion-animated.texture.aya", "data/resources/dxtntextures/Particle%alparticle6.tga(0)R5G6B5.aya", "3c8fc30ad4923c56c3735caab5661a3f176eb661eaa678093870f51de4204c9e"),
    (GODOT_ASSETS / "Level100/Textures/target-tank-explosion-fireball.texture.aya", "data/resources/dxtntextures/Particle%fireball.tga(0)A4R4G4B4.aya", "e6c166669e351632a90b41c74782967923c78fc8be644a1e8948d356806b23ed"),
    (GODOT_ASSETS / "Level100/Textures/target-tank.texture.aya", "data/resources/dxtntextures/meshtex%f_pulsetank_training.tga(0)A1R5G5B5.aya", "97ddd1e18e45b19e249e91e881d773d80d36768a2cd48f6549a769c2559a7b7e"),
    (GODOT_ASSETS / "Level100/Textures/target-truck.texture.aya", "data/resources/dxtntextures/meshtex%f_truck_training.tga(0)A1R5G5B5.aya", "ab4125242321de4963c51c9b22f63c951a33c22e874d8f039fa2c61a109f5e81"),
    (GODOT_ASSETS / "Level100/Textures/target-warehouse-m001.texture.aya", "data/resources/dxtntextures/meshtex%M_001.tga(0)A1R5G5B5.aya", "689b184ab8a5d03f33b69e5c35edcfdfdec12aa9b4b31f7c74ce5209f6236a49"),
    (GODOT_ASSETS / "Level100/Textures/target-warehouse-m002.texture.aya", "data/resources/dxtntextures/meshtex%M_002.tga(0)A1R5G5B5.aya", "8fabadbe1c5af067a740cf05debd1c952c628fd5fa3ea92b8202094704b8a20d"),
    # The U-17 Highside Transporter's two base textures, the only NEW texture
    # retentions task #114 needs. MEASURED from the mesh's own MSHT/TEXB table:
    # `m_f_lifter.msh.aya` names four texture records - f_lifter02, Chrome3,
    # f_lifter01, Chrome3 - and its two material groups reference base slots 0
    # and 2 with reflection slot 1. `meshtex\Chrome3.tga` is already
    # materialized by the static-world pass as
    # StaticWorld/Textures/meshtex-chrome3.texture.aya, and the Air Trainer /
    # Target Drone mesh needs no new texture at all: its table is
    # (f_pulsetank_training, Chrome3), byte-identical to the Target Tank's.
    (GODOT_ASSETS / "Level100/Textures/transporter-lifter01.texture.aya", "data/resources/dxtntextures/meshtex%f_lifter01.tga(0)A1R5G5B5.aya", "556f8fab2e9d708412d085c81f07dbfe800a9ee415c4e7ccf1aaad56bd2135e6"),
    (GODOT_ASSETS / "Level100/Textures/transporter-lifter02.texture.aya", "data/resources/dxtntextures/meshtex%f_lifter02.tga(0)A1R5G5B5.aya", "fa03f88a6132819fd0ac33266cdfb2b7423bf20f7b6c9aba05d370807efba21e"),
    (GODOT_ASSETS / "Level100/Textures/material-overlay-a8trust5.texture.aya", "data/resources/dxtntextures/meshtex%a8trust5.tga(0)A8R8G8B8.aya", "4ccde973f9741c110a82f350e102f1a12c566ff3d3b1b4f5426f2bbf536be843"),
    (GODOT_ASSETS / "Level100/Textures/terrain-cloud-shadow.texture.aya", "data/resources/dxtntextures/clouds%shadow.tga(0)A8R8G8B8.aya", "fc7441887e494e4b18f2b16179ed42c17801b128d71e29d653a4e8b792869519"),
    (GODOT_ASSETS / "Level100/Textures/terrain-detail-00.texture.aya", "data/resources/dxtntextures/mixers%detail00.tga(0)R5G6B5.aya", "7c9c22169d13ed8b7d6ad69286bdb59cc88f9ae3bfb6a9d3a0503d320386bfef"),
    (GODOT_ASSETS / "Level100/Music/tutorial-track-03.ogg", "data/Music/BEA_04(Master).ogg", "32d3e338964d74f50d0094536c585375f1e14aa2bae6087487803f3529eaf360"),
    (GODOT_ASSETS / "Level100/TutorialAudio/hud_01.ogg", "data/sounds/english/MessageBox/hud_01.ogg", "bae30243a2b5fe3dae718181ac5b05d766f93d5e25b042fe1b04c71fc9347909"),
    (GODOT_ASSETS / "Level100/TutorialAudio/hud_02.ogg", "data/sounds/english/MessageBox/hud_02.ogg", "43ae0c306b7935a21d415338348508eabf3a61f8799c0fd0873c89919fb84a35"),
    (GODOT_ASSETS / "Level100/TutorialAudio/hud_05.ogg", "data/sounds/english/MessageBox/hud_05.ogg", "66256d87557946647a51a2e8d49e044bc55ae370c4ad1c8e950b1d884ec082eb"),
    (GODOT_ASSETS / "Level100/TutorialAudio/hud_06.ogg", "data/sounds/english/MessageBox/hud_06.ogg", "4ed80a12fa7d2ad07a044f95f94d52455413962b75e7689101df6907711f3235"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_01.ogg", "data/sounds/english/MessageBox/tutorial_01.ogg", "48e40b07a77b5776f817ed8d8ffe1eff1a978b10480cab92019077e7b66784a8"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_02.ogg", "data/sounds/english/MessageBox/tutorial_02.ogg", "fa0533de72b8d7702b83b709ba631bc8f7a42a5183babcb147ae653a5d7a2904"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_03.ogg", "data/sounds/english/MessageBox/tutorial_03.ogg", "8e3bbd3f680099f7664f473f73837bf3e6d09474b4426677dd6bf27b31177dc2"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_13_mod.ogg", "data/sounds/english/MessageBox/tutorial_13_mod.ogg", "7eee9087f86c00abe4feab115b20e4e2f27a8e6d1adc7318b1602446a7493e65"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_message_log.ogg", "data/sounds/english/MessageBox/tutorial_message_log.ogg", "7a03ff8f3faa4be4b729e7619055379c62921e2eaeb67fc9711dac0dfe273f8b"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_open_fire.ogg", "data/sounds/english/MessageBox/tutorial_open_fire.ogg", "04a1a65b45f75f4d1e85b0fab6970125584efbabe3609d7413e60b569a26d20c"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_open_fire_2.ogg", "data/sounds/english/MessageBox/tutorial_open_fire_2.ogg", "122782139a31fbd777a734e0979f4f0ab8a7308d1154d7215ce2af13d56e3237"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_pulse_cannon.ogg", "data/sounds/english/MessageBox/tutorial_pulse_cannon.ogg", "2fda4a38b4737e03647c03bac38bfb36e7e6ff16b279007c04616c23857c25f8"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_pulse_cannon_2.ogg", "data/sounds/english/MessageBox/tutorial_pulse_cannon_2.ogg", "f4eca49f26f61f0369c0d8b770300596695f8a62ec12269a4c9d1cb3f61b13e0"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_scanner.ogg", "data/sounds/english/MessageBox/tutorial_scanner.ogg", "7a9535b1187b6e1ff276cebc3906ec2102e5d166f381ee674113b4f09c2b3bd2"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_technician_01.ogg", "data/sounds/english/MessageBox/tutorial_technician_01.ogg", "4792371453b4402454b922a481eb0968a099efb13981ff1918aa6177fb6ae151"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_vulcan_cannon.ogg", "data/sounds/english/MessageBox/tutorial_vulcan_cannon.ogg", "7f483d8f3c876c8e9e8bd52b0369f1c54c39c83c174c0a43d5fb8674c069172c"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_vulcan_cannon_2.ogg", "data/sounds/english/MessageBox/tutorial_vulcan_cannon_2.ogg", "6f872ca07fcc4f49ffb0cb2536a460411139fe1d77d263198c0f423fcefa9d90"),
    (GODOT_ASSETS / "Level100/TutorialAudio/hud_03.ogg", "data/sounds/english/MessageBox/hud_03.ogg", "3d58ab5abf9715ee4b3c657bf3ca7c0b91c2985382a7d72c948513045669c31e"),
    (GODOT_ASSETS / "Level100/TutorialAudio/hud_04.ogg", "data/sounds/english/MessageBox/hud_04.ogg", "3b96aa4cf15202d2977d32a04e73715dc1e940e00596d1614acd47262c86e77d"),
    (GODOT_ASSETS / "Level100/TutorialAudio/hud_07.ogg", "data/sounds/english/MessageBox/hud_07.ogg", "8d75f5981abb9fc494eb77f0a721b38b8eb95083be9282e67561e4bf18e9a8d4"),
    (GODOT_ASSETS / "Level100/TutorialAudio/hud_08.ogg", "data/sounds/english/MessageBox/hud_08.ogg", "06e0c641803b25fe157e8fd27420e11be5972df3b0dcd80681926577c9b0c361"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_04.ogg", "data/sounds/english/MessageBox/tutorial_04.ogg", "78c04e99ff5ae90fb224fa2dffae6b3101123d32fe73e3c9ee6b37ec4d6482c6"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_05.ogg", "data/sounds/english/MessageBox/tutorial_05.ogg", "bd047bd04eeefeeca91ed1ae3244eb65f8fe26de33f81ba18bd9505bc4494c58"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_06.ogg", "data/sounds/english/MessageBox/tutorial_06.ogg", "d61f834ac1efe27ff6982d25a401cfc48dd7bfaf03ee38f3bdfadb0d819f11c1"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_07.ogg", "data/sounds/english/MessageBox/tutorial_07.ogg", "1d01bb7c56bc582944161b415cfb18518abbeb211067a0db62f80d70c587af7e"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_08.ogg", "data/sounds/english/MessageBox/tutorial_08.ogg", "a7888025d9db2094df63145e7d58b53be5054abf28c678f727155f6ed282c205"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_09.ogg", "data/sounds/english/MessageBox/tutorial_09.ogg", "d306048bb7d1680c4b5e3fd8b7a01f966725c1c4d7e4f9a09b9e9e25dcae7557"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_10.ogg", "data/sounds/english/MessageBox/tutorial_10.ogg", "89a881c253c9cbc5227e84e942b0c8c3361b5b6bd2694b61522bd97af320f5fb"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_11.ogg", "data/sounds/english/MessageBox/tutorial_11.ogg", "c46293ff014967f62d90f870cfe8a8fc228fcc7a0b5acb7cbe3d203341a39984"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_12.ogg", "data/sounds/english/MessageBox/tutorial_12.ogg", "e6cbf2cb4dabcfacec0479dee1eeb33ed62e1d3b46bf238b638d75aa55b7a425"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_aborted.ogg", "data/sounds/english/MessageBox/tutorial_aborted.ogg", "320b8a1e4619ddeecf94afd8862e8b90c7ebaba3fffd5b8976a3d6160497c1b5"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_broke_1.ogg", "data/sounds/english/MessageBox/tutorial_broke_1.ogg", "30e2e6652508c7b9bf13beb2f19cd0ed66ce474d677e946ceeebd30a905d6696"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_broke_2.ogg", "data/sounds/english/MessageBox/tutorial_broke_2.ogg", "8e6af2bd4038b89c72e45c8e86a5b5e32fba12f50728b82a26223918f6d96315"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_broke_3.ogg", "data/sounds/english/MessageBox/tutorial_broke_3.ogg", "57c3c737699b02d96cd2b9cb2e6b5e8d2f513c92b51e3b13b7728f9127bc8d8f"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_dodge_2.ogg", "data/sounds/english/MessageBox/tutorial_dodge_2.ogg", "d88eff763474bcff488ebf913b1d8b45900452f34331ef6db334c589768d66b2"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_dodge_3.ogg", "data/sounds/english/MessageBox/tutorial_dodge_3.ogg", "7796dd70479a91c48d027594bbfc1bb6c0206336006657c13c2220185be5c0a8"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_dodge_bad.ogg", "data/sounds/english/MessageBox/tutorial_dodge_bad.ogg", "b551c989f66c3c30e048a73f230dbabcf19e588cd93a95b37b5a2654569bcdd5"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_dodge_good.ogg", "data/sounds/english/MessageBox/tutorial_dodge_good.ogg", "329d8269262c2f4c25b95a4373ad932f53a09b26003238c9d6aa3d613e98669d"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_dodge_mod.ogg", "data/sounds/english/MessageBox/tutorial_dodge_mod.ogg", "c74568f49114f77cc42426cf029be35434bf8058f2d05b1d4b6adb73b124c3a5"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_help_player.ogg", "data/sounds/english/MessageBox/tutorial_help_player.ogg", "287998779062d08030350bfca83e625ac55d12bb75775d4cc59b1f9e3afb7b5c"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_landing.ogg", "data/sounds/english/MessageBox/tutorial_landing.ogg", "07428ce3bfc31603e472ba83affd1d355b0c3cabec24d8ad87e3fe30cc646176"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_strafe.ogg", "data/sounds/english/MessageBox/tutorial_strafe.ogg", "f6da37701ffd0ddc9cf2780c80674305459c552fea3d1f383b65ebc76565db70"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_throttle_mod.ogg", "data/sounds/english/MessageBox/tutorial_throttle_mod.ogg", "29d3791d61286318fdedb4bdc88c267488f647d65996a581fb3da91b2d2e2ef4"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_technician_02.ogg", "data/sounds/english/MessageBox/tutorial_technician_02.ogg", "196032dddadbe1cbfe3315635991c02e62b05cac083a90ad4dfaaebbab8eb955"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_technician_03.ogg", "data/sounds/english/MessageBox/tutorial_technician_03.ogg", "952d91a0a416a20487d790debcf82dde357935946fd7e4b69678cbd350d8681c"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_movement.ogg", "data/sounds/english/MessageBox/tutorial_movement.ogg", "79f7dda58130d4b8e506683f7cf65c9213dffca074885df06aaaa857b0c28340"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_weapon.ogg", "data/sounds/english/MessageBox/tutorial_weapon.ogg", "e1afe902725a81d9900a1d5bee65d7fdf47b61ab59246de7ecade89f2a9e7b00"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_overheat.ogg", "data/sounds/english/MessageBox/tutorial_overheat.ogg", "34a084751bafa8320fa0d8b34ed17b6f8fc3f38630b0caf6528a3287a39b8683"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_ammo.ogg", "data/sounds/english/MessageBox/tutorial_ammo.ogg", "1a7474ffd1aa2c88a56b60d4f27a77a3fea348abf24a17a9f6e3251308af8769"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_water.ogg", "data/sounds/english/MessageBox/tutorial_water.ogg", "99dc92041fb49bd4b06d0e875b3ded4155e2f4a8ba4aed9b40596ce0012d1151"),
    (GODOT_ASSETS / "Level100/TutorialAudio/tutorial_zoom.ogg", "data/sounds/english/MessageBox/tutorial_zoom.ogg", "d0aadac588017f5a410358494603f7e052890c93d6b52ddbc61cbe171cb6940b"),
)


# Small exact released frontend surface used by the startup -> Level 100 path.
FRONTEND_ASSETS = (
    # CFrontEnd::Init (0x004662a0) plays MUS_FRONTEND, which CMusic::PlaySelection
    # (0x004bb8c0) resolves to zero-based track 8 of the alphabetical data\music
    # *.ogg playlist, i.e. BEA_09(Master).ogg. See Level100AudioCatalog.FrontendMusic.
    (GODOT_ASSETS / "Frontend/Music/frontend-track-08.ogg", "data/Music/BEA_09(Master).ogg", "4f6166f655e62dec6993643a8a860bdea0abb7d853ad443f5d03e95368be93a1"),
    (GODOT_ASSETS / "Frontend/Backgrounds/click-to-start.texture.aya", "data/resources/dxtntextures/FrontEnd%v2%fe_splash1.tga(0)A8R8G8B8.aya", "46ab45168875b5b686e3534b3f66ab65b5a5b5512f697e5a98b03dd12708731a"),
    (GODOT_ASSETS / "Frontend/Backgrounds/rock.texture.aya", "data/resources/dxtntextures/FrontEnd%v2%FE_Rock_Background.tga(0)A8R8G8B8.aya", "89213b441332f060acdb3e55aa28c290fa0e530983c16a57b8ce1a7413e9e86d"),
    # 0x0089d7bc — click-to-start sliding pair (LostToys, not BarL/C/R).
    (GODOT_ASSETS / "Frontend/click-slide.texture.aya", "data/resources/dxtntextures/FrontEnd%LostToys.tga(0)A8R8G8B8.aya", "ab1b3654842335983e7170f233137731fea5a25e8632a1f94cfcadccf758040b"),
    # 0x0089d7f0 — main-menu right chrome (Forseti writing large).
    (GODOT_ASSETS / "Frontend/forseti-writing-large.texture.aya", "data/resources/dxtntextures/FrontEnd%v2%FE_Forseti_Writing_large.tga(0)A8R8G8B8.aya", "6bc5671a482817e4b5702e348433c66d1e87178d068c6da36500273885b004c9"),
    # 0x0089d7fc — additive logo glow bars (FE_Reflection_map).
    (GODOT_ASSETS / "Frontend/reflection-map.texture.aya", "data/resources/dxtntextures/FrontEnd%v2%FE_Reflection_map.tga(0)A8R8G8B8.aya", "e480261fbba5cfafb646d52f217bc11983bae1285de16d6f80a9de6c017f0121"),
    # Language selector. data/language holds exactly five .dat sets
    # (english/french/german/italian/spanish), matching Career.h NUM_LANGUAGES 5,
    # and the released texture set carries exactly five matching flags.
    (GODOT_ASSETS / "Frontend/Flags/flag-uk.texture.aya", "data/resources/dxtntextures/FrontEnd%v2%Flag_UK.tga(0)A8R8G8B8.aya", "efbdba2a567b771f48b5314a941a2196bd83c75363fbcf5db91050ab9765d7e9"),
    (GODOT_ASSETS / "Frontend/Flags/flag-fr.texture.aya", "data/resources/dxtntextures/FrontEnd%v2%Flag_FR.tga(0)A8R8G8B8.aya", "1fa4179014b9b0c20f1bbd2336b8680a9f58452b46bdaced042fe685668d7452"),
    (GODOT_ASSETS / "Frontend/Flags/flag-gr.texture.aya", "data/resources/dxtntextures/FrontEnd%v2%Flag_GR.tga(0)A8R8G8B8.aya", "55ed2bb99b8572b5bda2ec3a89e9a4897462533ff46c7c6e0b66aa2fd20a23af"),
    (GODOT_ASSETS / "Frontend/Flags/flag-it.texture.aya", "data/resources/dxtntextures/FrontEnd%v2%Flag_IT.tga(0)A8R8G8B8.aya", "b397a80418458a253ea83dd54a03bd4fa035843bd629511c846cd28cdcf01432"),
    (GODOT_ASSETS / "Frontend/Flags/flag-sp.texture.aya", "data/resources/dxtntextures/FrontEnd%v2%Flag_SP.tga(0)A8R8G8B8.aya", "75a99eca5236dc940032d874f5f039a6c42307cc20534572cea80e2b8631bf1b"),
    # Left/right chevrons flanking the flag.
    (GODOT_ASSETS / "Frontend/fe-arrow.texture.aya", "data/resources/dxtntextures/FrontEnd%v2%FE_Arrow.tga(0)A8R8G8B8.aya", "ecf729f9402512b5fce21cd53d2a239a4b0991230dddc089c4af59325105ab82"),
    (GODOT_ASSETS / "Frontend/title-logo.texture.aya", "data/resources/dxtntextures/FrontEnd%v3%FE_BEA_Title2.tga(0)A8R8G8B8.aya", "5ae9b300836d27bd13462a53e3455b649bb46bf8f48c8c326fd8f4f0c18c7ec7"),
    (GODOT_ASSETS / "Frontend/title-bracket-01.texture.aya", "data/resources/dxtntextures/FrontEnd%v3%FE_BEA_title_bracket01.tga(0)A8R8G8B8.aya", "679b5fa6220b3eb54aeef1d970890c35be5df264530226f5d08b22a63ad75064"),
    (GODOT_ASSETS / "Frontend/title-bracket-02.texture.aya", "data/resources/dxtntextures/FrontEnd%v3%FE_BEA_title_bracket02.tga(0)A8R8G8B8.aya", "79f05e8c64b6e25f038c5b7c37ddadfd31ee9376e92fc5da505b6c427ed9c74f"),
    (GODOT_ASSETS / "Frontend/title-text-box.texture.aya", "data/resources/dxtntextures/FrontEnd%v3%FE_BEA_title_text_box.tga(0)A8R8G8B8.aya", "c007742e1fe9b93e988d198f8a2a4e741e546843fd36218d9015ab2ee6627b9c"),
    (GODOT_ASSETS / "Frontend/symbol-bracket-01.texture.aya", "data/resources/dxtntextures/FrontEnd%v3%FE_BEA_title_symbol_bracket01.tga(0)A8R8G8B8.aya", "3243e641e9ad45cd8b80c4abebaa1e6f73b5ed774e0b4dba1afbcbbaf81a49a8"),
    (GODOT_ASSETS / "Frontend/symbol-bracket-02.texture.aya", "data/resources/dxtntextures/FrontEnd%v3%FE_BEA_title_symbol_bracket02.tga(0)A8R8G8B8.aya", "92739af94bec154d898afb5e59432694a789bb3f2c37242eb65272684daeb687"),
    (GODOT_ASSETS / "Frontend/Icons/new-game.texture.aya", "data/resources/dxtntextures/FrontEnd%v3%FE_BEA_title_nav_symbol_newgame01.tga(0)A8R8G8B8.aya", "d3ff62fbc8193e15bf250c82088f5088b17c667277dbb5fff92f2980cc3deb70"),
    (GODOT_ASSETS / "Frontend/Icons/continue-game.texture.aya", "data/resources/dxtntextures/FrontEnd%v3%FE_BEA_title_nav_symbol_continuegame01.tga(0)A8R8.aya", "83c9fa4d7e786ae4353d1f639c75b007bc0c65f1412b447d68967d9e5b4cca0e"),
    (GODOT_ASSETS / "Frontend/Icons/load-game.texture.aya", "data/resources/dxtntextures/FrontEnd%v3%FE_BEA_title_nav_symbol_loadgame01.tga(0)A8R8G8B8.aya", "9d1bb0d9efc450fc2bce244e01a2975468f07cf785bf6854a5bc9495fffdc001"),
    (GODOT_ASSETS / "Frontend/Icons/multiplayer.texture.aya", "data/resources/dxtntextures/FrontEnd%v3%FE_BEA_title_nav_symbol_multiplayer01.tga(0)A8R8G.aya", "8a7d7dba563b153b314e04daaad4ffa2d0969b65a0603de043027eaf5b4df031"),
    (GODOT_ASSETS / "Frontend/Icons/goodies.texture.aya", "data/resources/dxtntextures/FrontEnd%v3%FE_BEA_title_nav_symbol_goodies01.tga(0)A8R8G8B8.aya", "efa9ec1d2317e3cdf2ed9a90cc8b6cb391e6ed1099740ddaeb2c808b49f33358"),
    (GODOT_ASSETS / "Frontend/Icons/options.texture.aya", "data/resources/dxtntextures/FrontEnd%v3%FE_BEA_title_nav_symbol_options01.tga(0)A8R8G8B8.aya", "0824d66acec9dad5037be8bfc2b863201f94404d21795eac4fad82d8c4da2aba"),
    (GODOT_ASSETS / "Frontend/Icons/quit.texture.aya", "data/resources/dxtntextures/FrontEnd%v3%FE_BEA_title_nav_symbol_quitgame01.tga(0)A8R8G8B8.aya", "7096f573ff30302b5d5dad8f56ebd633e51f2bd70613d5349b974dada17b7a93"),
    (GODOT_ASSETS / "Frontend/level-bracket-01.texture.aya", "data/resources/dxtntextures/FrontEnd%v3%FE_select_level_bracket01.tga(0)A8R8G8B8.aya", "560db1621169c1b5787fc9c4691f4bede1af292674f84d4d43be11ca05166aa5"),
    (GODOT_ASSETS / "Frontend/level-bracket-02.texture.aya", "data/resources/dxtntextures/FrontEnd%v3%FE_select_level_bracket02.tga(0)A8R8G8B8.aya", "7ad21e2a6e64f61998f7a43e92fe92d69ac013b169fd8107b648b1fa69877b27"),
    (GODOT_ASSETS / "Frontend/level-ring-01.texture.aya", "data/resources/dxtntextures/FrontEnd%v3%FE_select_level_ring_bracket01.tga(0)A8R8G8B8.aya", "687eaf0945b701b622bdebde805e88cac394734a4b4420155379993ef9f74e1c"),
    (GODOT_ASSETS / "Frontend/level-ring-02.texture.aya", "data/resources/dxtntextures/FrontEnd%v3%FE_select_level_ring_bracket02.tga(0)A8R8G8B8.aya", "620900d34c153e722b6d78a9fbecab2d69b8e81abcdbda084b0f90eb96142dff"),
    # CFEPDebriefing::Render 0x00456DD0. CFrontEnd__LoadSharedResources binds
    # RankingS/A/B/C/D/E at FRONTEND+0x60..+0x74; the page's entry ring is the
    # separate v2 FE_metal_ring_trans_from_levsel2 surface at FRONTEND+0xb0.
    (GODOT_ASSETS / "Frontend/Debriefing/metal-ring-transition.texture.aya", "data/resources/dxtntextures/FrontEnd%v2%FE_metal_ring_trans_from_levsel2.tga(0)A8R8G8B8.aya", "8b9b79189981de706fc72de9552bdc398ada96d892c0e4e33706c77a907da869"),
    (GODOT_ASSETS / "Frontend/Debriefing/ranking-a.texture.aya", "data/resources/dxtntextures/FrontEnd%RankingA.tga(0)A8R8G8B8.aya", "88765906f295b8002dc254f9805b05d5f5589d87a08f297b37d90953ba57d625"),
    (GODOT_ASSETS / "Frontend/Debriefing/ranking-b.texture.aya", "data/resources/dxtntextures/FrontEnd%RankingB.tga(0)A8R8G8B8.aya", "5f03cdf1e79e679e706a0b6ab644ad6961447dace35695dc93d7c3928ebf3d50"),
    (GODOT_ASSETS / "Frontend/Debriefing/ranking-c.texture.aya", "data/resources/dxtntextures/FrontEnd%RankingC.tga(0)A8R8G8B8.aya", "364b5140a18ebec99bdf4e4ce7c99e3619ff743d4cbe888c0fa5d84e96407a06"),
    (GODOT_ASSETS / "Frontend/Debriefing/ranking-d.texture.aya", "data/resources/dxtntextures/FrontEnd%RankingD.tga(0)A8R8G8B8.aya", "894521839162a5d2d8d73feba39435cf0590b0863d65b67daa5c4a4420485bd0"),
    (GODOT_ASSETS / "Frontend/Debriefing/ranking-e.texture.aya", "data/resources/dxtntextures/FrontEnd%RankingE.tga(0)A8R8G8B8.aya", "c844f938cfcf56a2f3908bb69313ec86153f97e6993f42172c2c8376897e911a"),
    (GODOT_ASSETS / "Frontend/Debriefing/ranking-s.texture.aya", "data/resources/dxtntextures/FrontEnd%RankingS.tga(0)A8R8G8B8.aya", "aab0817f798ff8379cf6e9e0f86ae466832ad644170e8c14b6f7a50a6a12de9f"),
    (GODOT_ASSETS / "Frontend/title-font.texture.aya", "data/resources/textures/mustbe_TitleFont.tga(0)A8R8G8B8.aya", "1941e28a5665665fb7f8f733e7a4854c60def33e1d4f1cb9caa979bc204d0707"),
    (GODOT_ASSETS / "Frontend/system-font.texture.aya", "data/resources/textures/mustbe_SystemFont(0)A8R8G8B8.aya", "475edc8c9b95e3d3619e9b78e168dfcda8575042b728d96de1598cb8917967eb"),
    (GODOT_ASSETS / "Frontend/loading-screen.texture.aya", "data/resources/dxtntextures/LoadingScreen.tga(0)X8R8G8B8.aya", "e4ad32fee41a31477e97d4f6f0b280f33c360756e3aba27bf23746038443fc2c"),
    # CFrontEnd::DrawBar (FrontEnd.cpp:1073) and CFrontEnd__LoadSharedResources
    # (0x004687e0) name FrontEnd\BarL/BarC/BarR.tga. These are header-bar white
    # masks, not CConsole__RenderLoadingScreen (0x0042C810). Safe-copy hashes
    # from local-lab/safe-copy-bea-pristine/data/resources/dxtntextures/.
    (GODOT_ASSETS / "Frontend/bar-l.texture.aya", "data/resources/dxtntextures/FrontEnd%BarL.tga(0)A8R8G8B8.aya", "fbd28ca720ebe91cb8f58a9f5be5e4e9ee5c013fc42052fd1bec6b41dfd094bd"),
    (GODOT_ASSETS / "Frontend/bar-c.texture.aya", "data/resources/dxtntextures/FrontEnd%BarC.tga(0)A8R8G8B8.aya", "347828edf9f97dd3463ce7374e167e57f8bd837113cbfad71cb8cbc6bcde68a5"),
    (GODOT_ASSETS / "Frontend/bar-r.texture.aya", "data/resources/dxtntextures/FrontEnd%BarR.tga(0)A8R8G8B8.aya", "9995d4a41ff140d3d33004086e82f940db946c0db45635b5c853662ace6c6199"),
    # The mouse cursor sprite. Retail closes every interactive frontend frame
    # with a 32x32 quad of this texture at the pointer position, measured on 14
    # draws across 13 pages and 4 launches in the 2026-07-27 d3d9 sweep
    # (local-lab/D3D9-FULL-SWEEP-2026-07-27.md).
    #
    # The name is read from the pristine specimen
    # local-lab/safe-copy-bea-pristine/BEA.exe.original.backup (sha256
    # 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750): VA
    # 0x00640058 / file 0x240058 holds "mouse.tga\0", the string the cursor
    # renderer at 0x00523A70 hands to CTexture::FindTexture, and VA 0x00640054
    # holds FF 00 00 00, which that renderer packs into the 0xFFFFFFFF diffuse
    # the sweep measured.
    #
    # The file is DXT2 on disk (128x128, mipMapCount 8) even though the sweep
    # measured D3DFMT_A8R8G8B8 at runtime: the texture loader special-cases this
    # one filename and forces the create-format. Godot decodes the DXT2 the file
    # actually holds, so the loader asks for Dxt2.
    (GODOT_ASSETS / "Frontend/mouse-cursor.texture.aya", "data/resources/dxtntextures/mouse.tga(0)A8R8G8B8.aya", "366021def699de220ad018c40250eefaccaab356c6c5d93fe0aa1b7f5302354c"),
)


FRONTEND_TEXT_IDS = {
    "newGame": 0x00E39AD3,
    "continueGame": 0x1B749178,
    "loadGame": 0x01BE0271,
    "multiplayer": 0x0794B06E,
    "goodies": 0x007C113D,
    "options": 0x008251DC,
    "quit": 0x00141D4A,
    "selectLevel": 0x0E89F10A,
    "level100": 0x28672C3F,
    "loading": 0x003848A7,
}


CHUNKS = (
    (CORE_ASSETS / "Level100/level100-heightfield.hfld.bin", b"HFLD", 668660, "7a4c7c5b9400e2c8d2325cecb5c44701cd8a6e6f8609cbc8bc31d449c0620f5d"),
)


# Destination, source AYA destination above, hierarchy frame, exact OBJ SHA-256.
MESHES = (
    (GODOT_ASSETS / "Level100/level100-target-tank.obj", GODOT_ASSETS / "Level100/Source/m_f_pulsetank_training.msh.aya", None, "88bae82b7d5080e307cbe49aa39c06c21efbcc7de806e236b5cb663aa7d5ba63"),
    (GODOT_ASSETS / "Level100/level100-target-truck.obj", GODOT_ASSETS / "Level100/Source/m_f_truck_training.msh.aya", None, "39eb4f7723725422ee348ca6052c437be2e59aac38ddac0ca4559213fc4e0113"),
    (GODOT_ASSETS / "Level100/level100-target-warehouse.obj", GODOT_ASSETS / "Level100/Source/m_m_warehouse.msh.aya", None, "dd605f3778d465a5e25a99b43c7721dec37d509732e65db3e6c71534f6b4a9ac"),
    # Task #114. Rest pose, no hierarchy frame, exactly as the three rows above.
    # `level100-air-trainer.obj` is drawn for BOTH the Air Trainer and the
    # Target Drone; it emits a single material group,
    # `layers-00000000-ffffffff-00000001-ffffffff-ffffffff-ffffffff`, which is
    # the Target Tank's group verbatim. The transporter emits two groups,
    # `...-00000000-...` and `...-00000002-...`.
    (GODOT_ASSETS / "Level100/level100-air-trainer.obj", GODOT_ASSETS / "Level100/Source/m_FA_F24_training.msh.aya", None, "1d8ee300e9349df27a889af85e597778a2422634d73ced5d284c7140dd9b885f"),
    (GODOT_ASSETS / "Level100/level100-transporter.obj", GODOT_ASSETS / "Level100/Source/m_f_lifter.msh.aya", None, "a6a39ea17e811b4d6f18fcc02b8eb753e6fd29643e8359ed068bb26fd76de787"),
)


# Destination, zero-based XAP record, exact record name, exact WAV SHA-256.
SOUNDS = (
    (GODOT_ASSETS / "Aquila/SoundEffects/strafe.wav", 20, "Battle Engine\\N_BE_dash", "8f22e6201b2fe8ac003d7d07c635f26a1b99277791f5262447f0400c747ecab1"),
    (GODOT_ASSETS / "Aquila/SoundEffects/energy-critical.wav", 21, "Battle Engine\\N_BE_energy_critical", "600776048d576d88839cf134372e38d739671de30ad27fbec880c0ce3a09cf18"),
    (GODOT_ASSETS / "Aquila/SoundEffects/energy-low.wav", 22, "Battle Engine\\N_BE_energy_low", "6f1434fbae6a2373a7c5fda5d921a5c61bb3006ab2a8eeb47245e1f11d55c70d"),
    (GODOT_ASSETS / "Aquila/SoundEffects/engine-inflight.wav", 23, "Battle Engine\\N_BE_engine_inflight", "0e6eb03aa2c2991c0e59c3483956b1c608a79700e0de179c855491cff548ac04"),
    (GODOT_ASSETS / "Aquila/SoundEffects/engine-land.wav", 24, "Battle Engine\\N_BE_engine_land", "07a03b5af4980d0d2e7c16bb5de833365483237cb4c83ff83b78d9aab61fbb36"),
    (GODOT_ASSETS / "Aquila/SoundEffects/engine-takeoff.wav", 25, "Battle Engine\\N_BE_engine_takeoff", "3698a4419c000ab982cbc92c6553ac2639272fb85930df52a26528b232f00798"),
    (GODOT_ASSETS / "Aquila/SoundEffects/target-locked.wav", 29, "Battle Engine\\N_BE_homing_missile_lock", "32bef3c615361b472c5b36d8952b6ca0d03bda74cdf8f9617d1e99b17d08d6d6"),
    (GODOT_ASSETS / "Aquila/SoundEffects/target-acquired.wav", 30, "Battle Engine\\N_BE_homing_missile_target", "3b0542a1df0541a3e19ec108d4c2e198556bfd406aba2f386e85b803ca04d5bc"),
    (GODOT_ASSETS / "Aquila/SoundEffects/hydraulics.wav", 31, "Battle Engine\\N_BE_hydraulics_02", "f02c11c62abb998ea35d38ede180aaf9b4ccaeaaae8f76e017e239a65570bf21"),
    (GODOT_ASSETS / "Aquila/SoundEffects/incoming-missile.wav", 32, "Battle Engine\\N_BE_incoming_missile", "7f00ce0cf622fdc65ca0ae3f06c7e68f093453db35486552b56715a71b9dcd0d"),
    (GODOT_ASSETS / "Aquila/SoundEffects/micro-missile-fire.wav", 33, "Battle Engine\\N_BE_micro_missiles_fire", "1c381c693cccfae6c8759a5bf417587af4f43845ccff827e25ea388df9a34f20"),
    (GODOT_ASSETS / "Aquila/SoundEffects/vulcan-cannon-fire.wav", 40, "Battle Engine\\N_BE_vulcan_cannon_fire", "bed47939fe924bf5d9a5ba273f9f61d6269813749ea43aca7db8c2bc1152078d"),
    (GODOT_ASSETS / "Frontend/SoundEffects/back.wav", 41, "Front End\\N_FE_back", "133b78e813c6b393be4dba1d263f69513958b0ab827d6603f952d6e0a82ba02b"),
    (GODOT_ASSETS / "Frontend/SoundEffects/move.wav", 42, "Front End\\N_FE_move", "76b2458e9c5854daf7237ea81b4f288ae09963bc10e7651e81e858fdb68ce83b"),
    (GODOT_ASSETS / "Frontend/SoundEffects/select.wav", 43, "Front End\\N_FE_select", "f84144c80405fe9f745b8cf4bd352d7fa4f8c0a8ba481c770c2c7c0a9053ade1"),
    (GODOT_ASSETS / "Level100/SoundEffects/terminal-ammunition-depleted.wav", 44, "HUD\\N_HUD_Ammunition_Depleted", "f1bd9e787faa1d32c149340b16af3d485cd6ad6b46ed09c30c889fc16b5a8da1"),
    (GODOT_ASSETS / "Level100/SoundEffects/terminal-armour-low.wav", 46, "HUD\\N_HUD_Armour_Low", "6da6b88abc77e7e281d338cf45ef35dd2d8aa99cca578418f6b6f28115d60d61"),
    (GODOT_ASSETS / "Level100/SoundEffects/terminal-energy-low.wav", 51, "HUD\\N_HUD_Energy_Low", "e7bf31d24623f3a37a6d578a52afae975a1b6d95dc9bb07a0dd1e1ed9c913f6d"),
    (GODOT_ASSETS / "Level100/SoundEffects/terminal-hostile-environment.wav", 55, "HUD\\N_HUD_Hostile_Environment", "3f841b810aac4a67221319a0d28f877c5bf1555130aeec337234e3f304ddf32f"),
    (GODOT_ASSETS / "Level100/SoundEffects/terminal-incoming-missile.wav", 56, "HUD\\N_HUD_Incoming_Missile", "5bf1fb9cfaef17fc0d62a16d3b37e07c85a5f9fa9f78d7135dc4ee75759ecdf6"),
    (GODOT_ASSETS / "Level100/SoundEffects/terminal-incoming-warhead.wav", 57, "HUD\\N_HUD_Incoming_Warhead", "711297354e61f9a94ad66172646caebc7683c9a67066b3ff7793996dd57e918e"),
    (GODOT_ASSETS / "Level100/SoundEffects/terminal-micro-missiles-selected.wav", 58, "HUD\\N_HUD_Micro_Missiles", "811d18c02399fbe05df649adb42732df9c6c4c3e85f4d9acafa1ed3751db04c1"),
    (GODOT_ASSETS / "Level100/SoundEffects/terminal-pulse-cannon-selected.wav", 60, "HUD\\N_HUD_Pulse_Cannon", "99a29b4bead7cd484547adbc3954a6f805638e6fcdf95cf70e90b6d5183734da"),
    (GODOT_ASSETS / "Level100/SoundEffects/terminal-vulcan-cannon-selected.wav", 70, "HUD\\N_HUD_Vulcan_Cannon", "a742431a169bba0a92f0241556a79d704626d56e1dc14bb43f41079aa2ce4990"),
    (GODOT_ASSETS / "Level100/SoundEffects/terminal-weapon-overheating.wav", 73, "HUD\\N_HUD_Weapon_Overheating", "f3f9e967708177f52667c872b152362284e0a8ba55b9e6a1137a5b981f44a34d"),
    (GODOT_ASSETS / "Level100/SoundEffects/component-explosion.wav", 93, "Impact\\N_I_explosion2", "c840a5ea48f975ecdb03e82646c369d164282e1ac10b03164923271da1067dec"),
    (GODOT_ASSETS / "Level100/SoundEffects/transport-explosion-large.wav", 94, "Impact\\N_I_explosion_big", "4ce37c52a010e7903a5e7d4073e0647ca790fbfe839facbba97ad4d7fe2de2fd"),
    (GODOT_ASSETS / "Level100/SoundEffects/explosion-large-debris.wav", 95, "Impact\\N_I_explosion_big_debris", "e5daefb4f5db5738b0cd8cd2619868d090ca4a604642bc05d302c1afb54980fe"),
    (GODOT_ASSETS / "Level100/SoundEffects/pulse-cannon-fire.wav", 35, "Battle Engine\\N_BE_pulse_cannon_fire", "710ff06db55bc694efb8ff7d3a5ab658125e7ca0fe6b4733a805da98b22b0277"),
    (GODOT_ASSETS / "Level100/SoundEffects/target-tank-explosion-medium.wav", 102, "Impact\\N_I_explosion_medium", "7228ae049cb0a9877e63671a65e51829443017b2c4981df90a9c64d2f38b6d9c"),
    (GODOT_ASSETS / "Level100/SoundEffects/pulse-impact-small.wav", 105, "Impact\\N_I_explosion_small", "3296b13938928f54847a29e17307e7875e9933f8fd6381bf0dfcd260cd6fc131"),
    (GODOT_ASSETS / "Frontend/SoundEffects/back.wav", 41, "Front End\\N_FE_back", "133b78e813c6b393be4dba1d263f69513958b0ab827d6603f952d6e0a82ba02b"),
    (GODOT_ASSETS / "Frontend/SoundEffects/move.wav", 42, "Front End\\N_FE_move", "76b2458e9c5854daf7237ea81b4f288ae09963bc10e7651e81e858fdb68ce83b"),
    (GODOT_ASSETS / "Frontend/SoundEffects/select.wav", 43, "Front End\\N_FE_select", "f84144c80405fe9f745b8cf4bd352d7fa4f8c0a8ba481c770c2c7c0a9053ade1"),
    (GODOT_ASSETS / "Level100/SoundEffects/facility-explosion-medium.wav", 103, "Impact\\N_I_explosion_medium_ricochet", "f86d23cfa18025ba9d1283e0b10d5bc1f939161465a924140044d3c0a6095d3a"),
    (GODOT_ASSETS / "Level100/SoundEffects/explosion-small.wav", 106, "Impact\\N_I_explosion_small_debris", "28f89761970629118b989b41b5dda3253fecb431ec479b64d056248bc3e5c1dc"),
    (GODOT_ASSETS / "Level100/SoundEffects/aquila-explosion-huge.wav", 107, "Impact\\N_I_explosion_vbig", "23a85aa8c5543f5be15f7d8b2279859560f5d417e9475f015a69522db3c8aed8"),
    (GODOT_ASSETS / "Level100/SoundEffects/explosion-huge-ground-debris.wav", 108, "Impact\\N_I_explosion_vbig_debris", "5205f38b925ec77a04aaed0501387176d49f0a69b11479127168936e8ebfcdec"),
    (GODOT_ASSETS / "Level100/SoundEffects/trainer-flyby.wav", 119, "Vehicles\\N_V_F_fighter_flyby", "838eac239fc8b53ef89471d433978d7e4aee0b3f96cea5d3281f60e2cfa6d2ce"),
    (GODOT_ASSETS / "Level100/SoundEffects/transport-flyby.wav", 126, "Vehicles\\N_V_bomber_flyby", "6317e8056a8b4f657aefc1319fb8ef512927846dde8a287498b2c2e852108a1c"),
    (GODOT_ASSETS / "Level100/SoundEffects/drone-vulcan-fire.wav", 150, "Weapons\\N_WP_blaster_02", "5e9227999084bd4fb06558b498c86f968c79b8df87b06d4621336bbc994b577b"),
    (GODOT_ASSETS / "Level100/SoundEffects/repair-charging.wav", 7, "Atmospheres\\N_A_health_pod_charging", "9ac76f06602d5432188901de75daaa2bb87dd12f46f7df88d6eda73bf31632ba"),
    (GODOT_ASSETS / "Level100/SoundEffects/repair-full.wav", 8, "Atmospheres\\N_A_health_pod_full", "2725b5298e6fa84c0d96deefdf3d1fe7fefb854ce8512a70441d1f95c395bb1f"),
    (GODOT_ASSETS / "Level100/SoundEffects/repair-idle.wav", 9, "Atmospheres\\N_A_health_pod_on", "ec3f2af86c5281d42923c3ae00fb66222e4e358a5f986fd26c7a43c34406f7d4"),
)


IMA_STEP_TABLE = (
    7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31,
    34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130,
    143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449,
    494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411,
    1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660,
    4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493,
    10442, 11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385,
    24623, 27086, 29794, 32767,
)
IMA_INDEX_TABLE = (-1, -1, -1, -1, 2, 4, 6, 8)


def _sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _read_exact(path: Path, expected_hash: str) -> bytes:
    try:
        data = path.read_bytes()
    except OSError as error:
        raise RuntimeError(f"required retail input is unavailable: {path}") from error
    actual = _sha256(data)
    if actual != expected_hash:
        raise RuntimeError(f"unsupported retail input: {path} (SHA-256 {actual})")
    return data


def _physics_record_stream(data: bytes) -> tuple[_PhysicsRecord, ...]:
    if len(data) < 6 or struct.unpack_from("<H", data, 0)[0] != 0x12:
        raise RuntimeError("unsupported physics-definition framing")
    offset = 2
    records: list[_PhysicsRecord] = []
    while struct.unpack_from("<i", data, offset)[0] != -1:
        record_type, _declared_size = struct.unpack_from("<II", data, offset)
        offset += 8
        name_end = data.index(0, offset)
        name = data[offset:name_end].decode("ascii")
        offset = name_end + 1
        fields: list[tuple[int, bytes]] = []
        while True:
            field_id, size = struct.unpack_from("<II", data, offset)
            offset += 8
            value = data[offset : offset + size]
            offset += size
            marker = struct.unpack_from("<i", data, offset)[0]
            offset += 4
            fields.append((field_id, value))
            if marker == -1:
                break
            if marker != 0:
                raise RuntimeError(
                    f"physics record has an invalid continuation: {name}"
                )
        records.append(_PhysicsRecord(record_type, name, tuple(fields)))
    if offset + 4 != len(data) or len(records) != 777:
        raise RuntimeError("physics-definition record count changed")
    return tuple(records)


def _physics_records(
    data: bytes,
) -> dict[tuple[int, str], tuple[_PhysicsRecord, ...]]:
    """Index the ordered stream without collapsing legitimate duplicate names."""

    records: dict[tuple[int, str], list[_PhysicsRecord]] = {}
    for record in _physics_record_stream(data):
        records.setdefault((record.record_type, record.name), []).append(
            record
        )
    return {key: tuple(values) for key, values in records.items()}


def _physics_record(
    records: dict[tuple[int, str], tuple[_PhysicsRecord, ...]],
    record_type: int,
    name: str,
) -> _PhysicsFields:
    matches = records.get((record_type, name), ())
    if len(matches) != 1:
        raise RuntimeError(
            f"expected one physics record type {record_type} named {name!r}, "
            f"found {len(matches)}"
        )
    return _PhysicsFields(matches[0].fields)


def _unit_behavior_selector(fields: _PhysicsFields | dict[int, bytes]) -> int:
    value = fields.get(8)
    if value is None or len(value) != 4:
        raise RuntimeError("Unit behaviour field 8 is not one dword")
    serialized_type = struct.unpack("<I", value)[0]
    if not 1 <= serialized_type <= len(UNIT_BEHAVIOR_SELECTOR_BY_SERIALIZED_TYPE):
        raise RuntimeError(
            f"unsupported Unit behaviour serialized type {serialized_type}"
        )
    return UNIT_BEHAVIOR_SELECTOR_BY_SERIALIZED_TYPE[serialized_type - 1]


def _definition_string(
    fields: _PhysicsFields | dict[int, bytes],
    field_id: int,
) -> str:
    value = fields[field_id]
    if not value or value[-1] != 0:
        raise RuntimeError(f"physics string field {field_id} is not terminated")
    return value[:-1].decode("ascii")


# The profile constructor's MinAltitude default, 4.0 (`mov [ebx+0x15c],
# 0x40800000` at 0x0042f0e9); no released air profile sets field 42.
AIR_UNIT_DEFAULT_MINIMUM_ALTITUDE_BITS = 0x40800000


def _air_unit_motion_fields(
    fields: _PhysicsFields, definition_name: str, mesh_radius_bits: int | None,
) -> dict[str, object]:
    """An air unit's flight scalars from its unit record: id 2
    CUnitAirVelocity (+0xb4), id 6 CUnitAirTurnRate (+0xb8), id 56 CUnitBig
    (+0x124) and MinAltitude (+0x15c, the constructor default). A Big unit's
    air step damps by 0.95 while its render radius (the mesh's +0x164,
    CRTMesh +0x20) times 0.2 reaches the water (0x00402fa0)."""
    if 42 in fields or 2 not in fields or 6 not in fields:
        raise RuntimeError(f"{definition_name} air-unit fields changed")
    big = struct.unpack("<i", fields[56])[0] != 0 if 56 in fields else False
    if big and mesh_radius_bits is None:
        raise RuntimeError(f"{definition_name} is Big but has no render radius")
    return {
        "airTurnRateFloatBits": struct.unpack("<i", fields[6])[0],
        "airVelocityFloatBits": struct.unpack("<i", fields[2])[0],
        "big": big,
        "meshRadiusFloatBits": mesh_radius_bits if big else None,
        "minimumAltitudeFloatBits": AIR_UNIT_DEFAULT_MINIMUM_ALTITUDE_BITS,
    }


def _cmsh_radius_bits(inflated_mesh: bytes) -> int:
    """The mesh's radius word, CMesh +0x164 (the CMSH payload starts at 8)."""
    if inflated_mesh[:4] != b"CMSH":
        raise RuntimeError("mesh is not a CMSH stream")
    return struct.unpack_from("<i", inflated_mesh, 8 + 0x164)[0]


def _level100_actor_motion_definitions(
    physics: dict[tuple[int, str], tuple[_PhysicsRecord, ...]],
    aircraft_mesh,
    transporter_radius_bits: int,
) -> list[dict[str, object]]:
    # Released PC IScript waypoint completion, `0x00538470`, computes horizontal
    # distance and arrives only when `distance < radius`. For `mThingType &
    # THING_TYPE_UNIT` it calls vtable slot 94 (`+0x178`). The exact Level 100
    # class tables and targets are:
    #
    #   CGroundVehicle 0x005e297c -> 0x00405e60 -> 2.0f
    #   CPlane         0x005e1930 -> 0x0050e8e0 -> 5.0f
    #   CDropship      0x005e1dd8 -> 0x0050ead0 -> 8.0f
    #
    # These are class behavior, not fields in `default physics.dat`; the prior
    # field census correctly found no arrival-radius id. Core stores positions
    # in millimetres, so the exact released values become 2_000 / 5_000 / 8_000.
    # See reverse-engineering/binary-analysis/functions/IScript.cpp.md.
    rows: list[dict[str, object]] = []
    for definition_name, expected_life_bits in (
        ("Target Tank", 0x40C00000),
        ("Target Truck", 0x40400000),
    ):
        fields = _physics_record(physics, 1, definition_name)
        if (
            fields.get(1) != struct.pack("<I", 0x40600000)
            or fields.get(3) != struct.pack("<I", expected_life_bits)
            or fields.get(5) != struct.pack("<I", 0x3D567750)
            or fields.get(8) != struct.pack("<i", 3)
        ):
            raise RuntimeError(
                f"Level 100 {definition_name} ground-motion fields changed"
            )
        rows.append(
            {
                "arrivalRadiusMillimeters": 2_000,
                "authoredOrder": len(rows),
                "behaviorInternalId": _unit_behavior_selector(fields),
                "behaviorSerializedType": 3,
                "definitionName": definition_name,
                "fullGuideBaseTicks": 4,
                "coreGroundOriginOffsetMillimeters": 100,
                "maximumSpeedFloatBits": struct.unpack("<i", fields[1])[0],
                "maximumTurnRadiansPerBaseTickFloatBits": struct.unpack(
                    "<i", fields[5]
                )[0],
                "motionClass": "GroundVehicle",
                "steamClassVtableAddress": 0x005E297C,
            }
        )

    # The manifest's ground-motion fields stay null for the air units; their
    # flight scalars come from _air_unit_motion_fields. Ordered weapon
    # uses/model poses are admitted below.
    #
    # id 2 = CUnitAirVelocity (unit record +0xb4), id 6 = CUnitAirTurnRate
    # (+0xb8), id 23 = CUnitMaxTargetRange (+0x158); see
    # reverse-engineering/binary-analysis/physics-round-value-ids-2026-07-25.md
    # for the RTTI-resolved value-id map.
    for definition_name, air_velocity_bits, max_target_range_bits in (
        ("Air Trainer", 0x41133333, 0x43960000),   # 9.2 u/s, 300.0
        ("Target Drone", 0x40B00000, 0x43FA0000),  # 5.5 u/s, 500.0
    ):
        fields = _physics_record(physics, 1, definition_name)
        if (
            fields.get(8) != struct.pack("<i", 9)
            or fields.get(2) != struct.pack("<I", air_velocity_bits)
            or fields.get(6) != struct.pack("<I", 0x3D32B8C2)
            or fields.get(23) != struct.pack("<I", max_target_range_bits)
        ):
            raise RuntimeError(
                f"Level 100 {definition_name} plane motion fields changed"
            )
        rows.append(
            {
                "arrivalRadiusMillimeters": 5_000,
                "authoredOrder": len(rows),
                "behaviorInternalId": _unit_behavior_selector(fields),
                "behaviorSerializedType": 9,
                "definitionName": definition_name,
                "fullGuideBaseTicks": None,
                "coreGroundOriginOffsetMillimeters": None,
                "maximumSpeedFloatBits": None,
                "maximumTurnRadiansPerBaseTickFloatBits": None,
                "motionClass": "Plane",
                "steamClassVtableAddress": 0x005E1930,
                "weaponMounts": _aircraft_weapon_mounts(aircraft_mesh, fields),
                **_air_unit_motion_fields(fields, definition_name, None),
            }
        )

    transporter_name = "U-17 Highside Transporter"
    transporter_fields = _physics_record(physics, 1, transporter_name)
    if (
        transporter_fields.get(8) != struct.pack("<i", 12)
        or transporter_fields.get(2) != struct.pack("<I", 0x40A00000)
        or transporter_fields.get(6) != struct.pack("<I", 0x3BE4C388)
        or transporter_fields.get(56) != struct.pack("<i", 1)
    ):
        raise RuntimeError("Level 100 transporter dropship fields changed")
    rows.append(
        {
            "arrivalRadiusMillimeters": 8_000,
            "authoredOrder": len(rows),
            "behaviorInternalId": _unit_behavior_selector(transporter_fields),
            "behaviorSerializedType": 12,
            "definitionName": transporter_name,
            "fullGuideBaseTicks": None,
            "coreGroundOriginOffsetMillimeters": None,
            "maximumSpeedFloatBits": None,
            "maximumTurnRadiansPerBaseTickFloatBits": None,
            "motionClass": "Dropship",
            "steamClassVtableAddress": 0x005E1DD8,
            **_air_unit_motion_fields(transporter_fields, transporter_name, transporter_radius_bits),
        }
    )
    return rows


def _materialize_level100_hud_manifest(stage: Path) -> str:
    level_script_path = stage / LEVEL100_LEVEL_SCRIPT
    english_source_path = stage / LEVEL100_ENGLISH_SOURCE
    text_stf_path = stage / LEVEL100_TEXT_STF
    english_dat_path = stage / LEVEL100_ENGLISH_DAT

    level_script = level_script_path.read_text(encoding="windows-1252")
    english_source = english_source_path.read_text(encoding="windows-1252")
    text_stf = text_stf_path.read_text(encoding="windows-1252")

    speaker_by_token = {
        "P_TATIANA": "Tatiana",
        "P_TECHNICIAN": "Technician",
        "P_KRAMER": "Kramer",
    }
    play_events: list[dict[str, object]] = []
    current_highlight = "None"
    for line in level_script.splitlines():
        highlight_match = re.search(r"\bHighlightHudPart\s*\(\s*(\w+)\s*\)", line)
        if highlight_match:
            current_highlight = highlight_match.group(1)

        play_match = re.search(
            r"\bPlayCharMessage(Wait)?\s*\(\s*(\w+)\s*,\s*(\w+)",
            line,
        )
        if play_match:
            wait_token, speaker_token, symbol = play_match.groups()
            if speaker_token not in speaker_by_token:
                raise RuntimeError(
                    f"Level 100 script uses an unsupported HUD speaker: {speaker_token}"
                )
            play_events.append(
                {
                    "highlightSymbol": current_highlight,
                    "speaker": speaker_by_token[speaker_token],
                    "symbol": symbol,
                    "waitsForCompletion": wait_token is not None,
                }
            )

        unhighlight_match = re.search(
            r"\bUnHighlightHudPart\s*\(\s*(\w+)\s*\)",
            line,
        )
        if unhighlight_match:
            if current_highlight != unhighlight_match.group(1):
                raise RuntimeError("Level 100 script unhighlights a different HUD part")
            current_highlight = "None"

    if current_highlight != "None":
        raise RuntimeError("Level 100 script leaves a HUD highlight active")
    play_symbols = {str(event["symbol"]) for event in play_events}
    if len(play_events) != 45 or len(play_symbols) != 43:
        raise RuntimeError(
            "Level 100 PlayCharMessage event identity changed: "
            f"{len(play_events)} calls/{len(play_symbols)} messages"
        )

    english_message_symbols: list[str] = []
    seen_english_message_symbols: set[str] = set()
    for match in re.finditer(
        r"^\[([^\]]+)\]\s*//\s*\w+\s*$",
        english_source,
        re.MULTILINE,
    ):
        symbol = match.group(1)
        if symbol in seen_english_message_symbols:
            raise RuntimeError(f"Level 100 English source repeats a message: {symbol}")
        seen_english_message_symbols.add(symbol)
        english_message_symbols.append(symbol)
    if len(english_message_symbols) != 51:
        raise RuntimeError(
            "Level 100 English callback-message count changed: "
            f"{len(english_message_symbols)}"
        )

    modified_symbols = {
        symbol.removesuffix("_MOD"): symbol
        for symbol in play_symbols
        if symbol.endswith("_MOD")
    }
    message_symbols = [
        modified_symbols.get(symbol, symbol) for symbol in english_message_symbols
    ]
    if len(message_symbols) != 51 or len(set(message_symbols)) != 51:
        raise RuntimeError(
            f"Level 100 released HUD-message union changed: {len(message_symbols)}"
        )
    if not play_symbols.issubset(message_symbols):
        raise RuntimeError("Level 100 English callback order omits a played message")

    symbol_to_signed_id: dict[str, int] = {}
    for line in text_stf.splitlines():
        match = re.match(r"^#define\s+(\S+)\s+(-?\d+)\s*$", line.strip())
        if not match:
            continue
        symbol, raw_value = match.groups()
        value = int(raw_value, 10)
        if symbol in symbol_to_signed_id and symbol_to_signed_id[symbol] != value:
            raise RuntimeError(f"text.stf changes a symbol ID: {symbol}")
        symbol_to_signed_id[symbol] = value

    tools_root = ROOT / "tools"
    if str(tools_root) not in sys.path:
        sys.path.insert(0, str(tools_root))
    from language_dat_decode import entry_record, parse_lang_dat

    language = parse_lang_dat(english_dat_path)
    english_dat = english_dat_path.read_bytes()
    entries_by_id = {entry.text_id: entry for entry in language.entries}

    def native_record(symbol: str) -> tuple[int, dict[str, object]]:
        try:
            signed_id = symbol_to_signed_id[symbol]
            entry = entries_by_id[signed_id & 0xFFFFFFFF]
        except KeyError as error:
            raise RuntimeError(
                f"released text data is missing Level 100 symbol: {symbol}"
            ) from error
        return signed_id, entry_record(language, english_dat, entry)

    messages: list[dict[str, object]] = []
    for symbol in message_symbols:
        signed_id, native = native_record(symbol)
        audio = native["audio"]
        text = native["text"]
        if not isinstance(audio, str) or not audio or Path(audio).name != audio:
            raise RuntimeError(
                f"released Level 100 message has invalid audio identity: {symbol}"
            )
        if not isinstance(text, str) or not text:
            raise RuntimeError(
                f"released Level 100 message has no native text: {symbol}"
            )
        audio_file = f"{audio.lower()}.ogg"
        if not (
            stage / GODOT_ASSETS / "Level100/TutorialAudio" / audio_file
        ).is_file():
            raise RuntimeError(
                f"released Level 100 message audio is not materialized: {audio_file}"
            )
        messages.append(
            {
                "audioFile": audio_file,
                "symbol": symbol,
                "text": text,
                "textId": signed_id,
            }
        )

    native_play_events: list[dict[str, object]] = []
    for event_index, event in enumerate(play_events):
        symbol = str(event["symbol"])
        signed_id, _ = native_record(symbol)
        native_play_events.append(
            {
                "eventIndex": event_index,
                "highlightSymbol": event["highlightSymbol"],
                "speaker": event["speaker"],
                "symbol": symbol,
                "textId": signed_id,
                "waitsForCompletion": event["waitsForCompletion"],
            }
        )

    help_symbols = re.findall(r"\bAddHelpMessage\s*\(\s*(\w+)\s*\)", level_script)
    if len(help_symbols) != 6 or len(set(help_symbols)) != 6:
        raise RuntimeError(
            f"Level 100 help-message count changed: {len(help_symbols)}"
        )
    help_messages: list[dict[str, object]] = []
    for symbol in help_symbols:
        signed_id, native = native_record(symbol)
        text = native["text"]
        if not isinstance(text, str) or not text:
            raise RuntimeError(
                f"released Level 100 help message has no native text: {symbol}"
            )
        help_messages.append(
            {"symbol": symbol, "text": text, "textId": signed_id}
        )

    terminal_strings: dict[str, dict[str, object]] = {}
    for key, symbol in (
        ("victory", "FETX_VICTORY"),
        ("defeat", "FETX_DEFEAT"),
        ("missionComplete", "IG_MISSION_COMPLETE"),
        ("retry", "GI_RETRY"),
        ("back", "GI_BACK"),
        ("tutorialBroken", "LOSE_TUTORIAL_BROKE"),
        ("playerDeath", "GAME_OVER_DEATH"),
        ("water", "GAME_OVER_WATER"),
    ):
        signed_id, native = native_record(symbol)
        text = native["text"]
        if not isinstance(text, str) or not text:
            raise RuntimeError(
                f"released terminal string has no native text: {symbol}"
            )
        terminal_strings[key] = {
            "symbol": symbol,
            "text": text,
            "textId": signed_id,
        }

    payload = {
        "help": help_messages,
        "messages": messages,
        "playCharEvents": native_play_events,
        "schemaVersion": "onslaught.level100-hud-events.v4",
        "sources": {
            "englishDatSha256": _sha256(english_dat),
            "englishSourceSha256": _sha256(english_source_path.read_bytes()),
            "levelScriptSha256": _sha256(level_script_path.read_bytes()),
            "textStfSha256": _sha256(text_stf_path.read_bytes()),
        },
        "terminalStrings": terminal_strings,
    }
    data = (
        json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    ).encode("utf-8")
    manifest_hash = _sha256(data)
    target = stage / LEVEL100_HUD_MANIFEST
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(data)
    return manifest_hash


def _fixed_outputs() -> tuple[tuple[Path, str], ...]:
    direct = tuple(
        (path, expected) for path, _, expected in DIRECT_ASSETS + FRONTEND_ASSETS
    )
    chunks = tuple((path, expected) for path, _, _, expected in CHUNKS)
    later_worlds: list[tuple[Path, str]] = []
    for admission in LATER_WORLD_ADMISSIONS:
        later_worlds.extend(
            (
                admission.script_root
                / f"level{admission.world_number}-{name}.mso.bin",
                expected,
            )
            for name, _, expected in admission.script_objects
        )
        later_worlds.append((admission.heightfield, admission.hfld_sha256))
    derived = (
        (ROOT_TERRAIN_TEXTURE, ROOT_TERRAIN_TEXTURE_SHA256),
        (TERRAIN_HIERARCHY_SOURCE, TERRAIN_HIERARCHY_SOURCE_SHA256),
        *(
            (LEVEL100_SCRIPT_ROOT / f"level100-{name}.mso.bin", expected)
            for name, _, expected in LEVEL100_SCRIPT_OBJECTS
        ),
        *later_worlds,
        (LEVEL110_INITIAL_OBJECT_SEEDS, WORLD110_INITIAL_OBJECT_SEEDS_SHA256),
        (LEVEL110_INITIAL_ACTORS, WORLD110_INITIAL_ACTORS_SHA256),
        (LEVEL110_STATIC_WORLD, WORLD110_STATIC_WORLD_SHA256),
        (LEVEL110_PLAYER_INPUTS, WORLD110_PLAYER_INPUTS_SHA256),
        (FRONTEND_LOCALIZATION, FRONTEND_LOCALIZATION_SHA256),
        (FRONTEND_WORLD_STRINGS, FRONTEND_WORLD_STRINGS_SHA256),
        (FEBACK_STRIP, FEBACK_STRIP_SHA256),
        (LEVEL100_HUD_MANIFEST, LEVEL100_HUD_MANIFEST_SHA256),
    )
    meshes = tuple((path, expected) for path, _, _, expected in MESHES)
    sounds = tuple((path, expected) for path, _, _, expected in SOUNDS)
    return direct + chunks + derived + meshes + sounds


def _resource_relative(resource_path: str) -> Path:
    prefix = "res://Assets/Level100/StaticWorld/"
    if not resource_path.startswith(prefix):
        raise RuntimeError(f"static-world resource escaped its owner: {resource_path}")
    suffix = resource_path[len("res://") :]
    relative = Path("rebuild/OnslaughtRebuild.Godot") / Path(suffix)
    if ".." in relative.parts:
        raise RuntimeError(f"static-world resource has an invalid path: {resource_path}")
    return relative


def _static_world_outputs(root: Path) -> tuple[tuple[Path, str], ...]:
    manifest_path = root / STATIC_WORLD_MANIFEST
    manifest_bytes = manifest_path.read_bytes()
    manifest_hash = _sha256(manifest_bytes)
    if manifest_hash != STATIC_WORLD_MANIFEST_SHA256:
        raise RuntimeError(
            f"static-world manifest does not reproduce exactly (SHA-256 {manifest_hash})"
        )
    manifest = json.loads(manifest_bytes)
    if (
        manifest.get("schema") != "onslaught.level100-static-world.v15"
        or manifest.get("sourceArchiveSha256") != LEVEL_ARCHIVE_SHA256
        or manifest.get("physicsSourceSha256") != PHYSICS_DEFINITIONS_SHA256
        or manifest.get("sourceAggregateSha256") != STATIC_WORLD_SOURCE_AGGREGATE_SHA256
        or manifest.get("unitRecordCount") != 35
        or manifest.get("visibleObjectCount") != 33
        or manifest.get("suppressedFernCount") != 753
        or manifest.get("pineInstanceCount") != 1481
        or len(manifest.get("objects", ())) != 33
        or len(manifest.get("actorDefinitions", ())) != 44
        or len(manifest.get("spawnDefinitions", ())) != 10
        or [
            (
                row.get("definitionName"),
                row.get("motionClass"),
                row.get("behaviorInternalId"),
                row.get("steamClassVtableAddress"),
                row.get("arrivalRadiusMillimeters"),
            )
            for row in manifest.get("motionDefinitions", ())
        ]
        != [
            ("Target Tank", "GroundVehicle", 2, 0x005E297C, 2_000),
            ("Target Truck", "GroundVehicle", 2, 0x005E297C, 2_000),
            ("Air Trainer", "Plane", 8, 0x005E1930, 5_000),
            ("Target Drone", "Plane", 8, 0x005E1930, 5_000),
            ("U-17 Highside Transporter", "Dropship", 12, 0x005E1DD8, 8_000),
        ]
        or [
            path.get("name") for path in manifest.get("waypointPaths", ())
        ]
        != [
            "Flyby Path",
            "Target Truck Path 3",
            "Target Truck Path 2",
            "Target Truck Path 1",
            "Transporter Path",
            "Target Tank Path 2",
            "Target Tank Path 1",
            "Drone Path 1",
        ]
        # The authored `target` chains, and the two that close on themselves.
        # These are the leaves the 2026-07-27 waypoint correction added; pinning
        # them here is what stops a future decode regression from silently
        # re-flattening the routes.
        or [
            (
                path.get("name"),
                tuple(path.get("targetChainNodeIndices", ())),
                path.get("isClosed"),
            )
            for path in manifest.get("waypointPaths", ())
        ]
        != [
            ("Flyby Path", (41, 42, 43), False),
            ("Target Truck Path 3", (33, 34, 35, 36), False),
            ("Target Truck Path 2", (29, 30, 31, 32), False),
            ("Target Truck Path 1", (25, 26, 27, 28), False),
            ("Transporter Path", (22, 23, 44), False),
            ("Target Tank Path 2", (38, 37, 10, 24, 8), True),
            ("Target Tank Path 1", (6, 7, 18), False),
            ("Drone Path 1", (1, 2, 3, 4), True),
        ]
        or len(manifest.get("pines", ())) != 1481
        or len(manifest.get("meshes", {})) != 28
        or len(manifest.get("textures", {})) != 34
        or manifest.get("pineBillboards", {}).get("texture")
        != "pine-imposters-100"
        or manifest.get("pineBillboards", {}).get("meshQualityDistance")
        != PINE_MESH_QUALITY_DISTANCE
        or manifest.get("pineBillboards", {}).get("fastStandingViewPhase")
        != PINE_FAST_STANDING_VIEW_PHASE
        or len(manifest.get("pineBillboards", {}).get("variants", ())) != 4
        or any(
            len(variant.get("centerOffset", ())) != 3
            or len(variant.get("views", ())) != 6
            for variant in manifest.get("pineBillboards", {}).get("variants", ())
        )
        or manifest.get("textures", {})
        .get("meshtex-a8-fb-hangermorebits-lit", {})
        .get("blendTextureAlpha")
        is not True
        or sum(
            item.get("blendTextureAlpha") is True
            for item in manifest.get("textures", {}).values()
        )
        != 1
        or manifest.get("water", {}).get("surfaceSha256") != WATER_SURFACE_SHA256
    ):
        raise RuntimeError("static-world manifest has unexpected identity or counts")

    outputs: list[tuple[Path, str]] = [
        (STATIC_WORLD_MANIFEST, STATIC_WORLD_MANIFEST_SHA256)
    ]
    for collection in (manifest["meshes"], manifest["textures"]):
        for item in collection.values():
            outputs.append((_resource_relative(item["resourcePath"]), item["sha256"]))
    outputs.append(
        (
            _resource_relative(manifest["water"]["surfaceResourcePath"]),
            manifest["water"]["surfaceSha256"],
        )
    )
    outputs.append((LEVEL100_CONTACT_ASSET, LEVEL100_CONTACT_ASSET_SHA256))
    outputs.append((STATIC_WORLD_ANIMATION, STATIC_WORLD_ANIMATION_SHA256))
    if len(outputs) != 66 or len({path for path, _ in outputs}) != len(outputs):
        raise RuntimeError("static-world manifest has duplicate or missing outputs")
    return tuple(outputs)


def _all_outputs(root: Path) -> tuple[tuple[Path, str], ...]:
    return _fixed_outputs() + _static_world_outputs(root)


def _outputs_ready() -> bool:
    try:
        for relative, expected in _all_outputs(ROOT):
            path = ROOT / relative
            if _sha256(path.read_bytes()) != expected:
                return False
        return True
    except (KeyError, OSError, RuntimeError, TypeError, ValueError):
        return False


def _reuse_canonical_assets() -> int:
    """Read verified canonical inputs through file links; never copy or publish there.

    Link files, not directories: Godot's adjacent .import files and all derived
    resources must belong to the current checkout. Validate the complete input
    set and every destination before creating the first link.
    """
    canonical = _canonical_repository_root()
    configured_lab = os.environ.get("BEA_LOCAL_LAB")
    if configured_lab and Path(configured_lab).resolve() != (canonical / "local-lab").resolve():
        raise RuntimeError("BEA_LOCAL_LAB must name this worktree's canonical local-lab")
    # The static-world manifest lists further outputs. A checkout that holds
    # the exact pinned manifest as its own file lists them from it, since the
    # canonical checkout may not have republished it yet; every listed output
    # is still verified below.
    own_manifest = ROOT / STATIC_WORLD_MANIFEST
    listing_root = ROOT if (not own_manifest.is_symlink() and own_manifest.is_file()
                            and _sha256(own_manifest.read_bytes()) == STATIC_WORLD_MANIFEST_SHA256) else canonical
    outputs = tuple(dict.fromkeys(_all_outputs(listing_root)))
    pending: list[tuple[Path, Path]] = []
    for relative, expected in outputs:
        source = canonical / relative
        destination = ROOT / relative
        if ROOT.resolve() != canonical.resolve():
            # A directory link could redirect Godot imports or later
            # publications into the canonical corpus even when the leaf link
            # is correct.
            parent = destination.parent
            while parent != ROOT:
                if parent.is_symlink():
                    raise RuntimeError(f"asset destination traverses a directory link: {parent}")
                parent = parent.parent
        if (not destination.is_symlink() and destination.is_file()
                and _sha256(destination.read_bytes()) == expected):
            # This checkout already holds the exact pinned bytes as its own
            # file (an output the canonical checkout has not published, or has
            # not yet republished for this checkout's materializer): keep it.
            continue
        if not source.exists() and not source.is_symlink():
            raise RuntimeError(f"canonical input is missing: {relative}")
        if source.is_symlink() or _sha256(source.read_bytes()) != expected:
            raise RuntimeError(f"canonical input is not the current exact regular file: {relative}")
        if ROOT.resolve() == canonical.resolve():
            continue
        if destination.is_symlink():
            if destination.resolve(strict=True) != source.resolve(strict=True):
                raise RuntimeError(f"asset destination points to another input: {relative}")
        elif destination.exists():
            if not destination.is_file() or _sha256(destination.read_bytes()) != expected:
                raise RuntimeError(f"refusing to replace existing asset destination: {relative}")
        else:
            pending.append((source, destination))
    for source, destination in pending:
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.symlink_to(source)
    return len(outputs)


def _steam_roots() -> list[Path]:
    roots: list[Path] = []
    if sys.platform.startswith("linux"):
        roots.extend((Path.home() / ".local/share/Steam", Path.home() / ".steam/steam"))
    else:
        try:
            import winreg

            for hive, key_name, value_name in (
                (winreg.HKEY_CURRENT_USER, r"Software\Valve\Steam", "SteamPath"),
                (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
            ):
                try:
                    with winreg.OpenKey(hive, key_name) as key:
                        roots.append(Path(winreg.QueryValueEx(key, value_name)[0]))
                except OSError:
                    pass
        except ImportError:
            pass

        roots.extend((Path(r"C:\Program Files (x86)\Steam"), Path(r"C:\Program Files\Steam")))
    libraries: list[Path] = []
    for root in roots:
        libraries.append(root)
        vdf = root / "steamapps/libraryfolders.vdf"
        try:
            text = vdf.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        libraries.extend(
            Path(re.sub(r'\\(["\\])', r'\1', value))
            for value in re.findall(r'"path"\s+"((?:\\.|[^"\\])*)"', text)
        )
    if sys.platform.startswith("linux"):
        # Steam's compatibility root is commonly a symlink to the first root.
        # Resolve identities without changing Steam's configuration or contents.
        return list(dict.fromkeys(library.resolve() for library in libraries))
    return libraries


def _game_candidates(explicit: Path | None) -> list[Path]:
    if explicit is not None:
        return [explicit]

    candidates: list[Path] = []
    for library in _steam_roots():
        candidates.append(library / "steamapps/common/Battle Engine Aquila")
    if not sys.platform.startswith("linux"):
        candidates.extend(
            Path(path)
            for path in (
                r"D:\Steam\steamapps\common\Battle Engine Aquila",
                r"D:\SteamLibrary\steamapps\common\Battle Engine Aquila",
                r"E:\Steam\steamapps\common\Battle Engine Aquila",
                r"E:\SteamLibrary\steamapps\common\Battle Engine Aquila",
            )
        )
    unique: list[Path] = []
    seen: set[str] = set()
    for candidate in candidates:
        key = os.path.normcase(os.path.abspath(candidate))
        if key not in seen:
            seen.add(key)
            unique.append(Path(key))
    return unique


def _resolve_game_root(explicit: Path | None) -> Path:
    for candidate in _game_candidates(explicit):
        if (
            (candidate / "BEA.exe").is_file()
            and (candidate / LEVEL_ARCHIVE).is_file()
            and (candidate / BASE_ARCHIVE).is_file()
            and (candidate / SOUND_BANK).is_file()
        ):
            return candidate
    if explicit is not None:
        raise RuntimeError(f"not a complete Battle Engine Aquila installation: {explicit}")
    raise RuntimeError("Battle Engine Aquila was not detected; pass --game-root with your retail installation")


def _is_link_or_reparse(info: os.stat_result) -> bool:
    return stat.S_ISLNK(info.st_mode) or bool(
        getattr(info, "st_file_attributes", 0) & REPARSE_ATTRIBUTE
    )


def _plain_existing_directory(path: Path, label: str) -> Path:
    """Resolve one existing directory without crossing links/reparse points."""

    current = Path(path.anchor)
    for component in path.parts[1:]:
        current /= component
        try:
            info = current.lstat()
        except OSError as error:
            raise RuntimeError(f"{label} is absent: {current}") from error
        if _is_link_or_reparse(info):
            raise RuntimeError(f"{label} traverses a link or reparse point: {current}")
        if not stat.S_ISDIR(info.st_mode):
            raise RuntimeError(f"{label} has a non-directory component: {current}")
    return path.resolve(strict=True)


def _paths_overlap(left: Path, right: Path) -> bool:
    return (
        left == right
        or left.is_relative_to(right)
        or right.is_relative_to(left)
    )


def _canonical_repository_root() -> Path:
    """Resolve the primary checkout from this checkout's Git common dir."""

    git_environment = {
        key: value
        for key, value in os.environ.items()
        if not key.startswith("GIT_")
    }
    try:
        result = subprocess.run(
            (
                "git",
                "-C",
                str(ROOT),
                "rev-parse",
                "--path-format=absolute",
                "--git-common-dir",
            ),
            check=True,
            capture_output=True,
            text=True,
            env=git_environment,
        )
    except (OSError, subprocess.CalledProcessError) as error:
        raise RuntimeError(
            "could not resolve the canonical repository checkout"
        ) from error

    common_text = result.stdout.strip()
    if not common_text:
        raise RuntimeError("Git returned an empty common-directory path")
    common_candidate = Path(common_text)
    if not common_candidate.is_absolute():
        common_candidate = ROOT / common_candidate
    common = _plain_existing_directory(
        Path(os.path.abspath(common_candidate)),
        "Git common directory",
    )
    if common.name != ".git":
        raise RuntimeError(
            f"Git common directory is not a canonical .git owner: {common}"
        )

    canonical = _plain_existing_directory(
        common.parent,
        "canonical repository root",
    )
    marker = canonical / ".git"
    try:
        marker_info = marker.lstat()
    except OSError as error:
        raise RuntimeError(
            f"canonical repository Git directory is absent: {marker}"
        ) from error
    if _is_link_or_reparse(marker_info) or not stat.S_ISDIR(marker_info.st_mode):
        raise RuntimeError(
            f"canonical repository Git directory is not plain: {marker}"
        )
    if marker.resolve(strict=True) != common:
        raise RuntimeError(
            "Git common directory does not belong to the canonical repository root"
        )
    return canonical


def _resolve_work_root(
    explicit: Path | None,
    *,
    game_root: Path | None = None,
) -> Path:
    """Select the temporary asset-build owner without touching source trees.

    The historical Windows launchers stage below ``ROOT/local-lab`` and retain
    that default. Linux callers must explicitly select a plain descendant of
    the primary checkout's ignored ``local-lab`` owner. A linked worktree's
    similarly spelled path is deliberately not an authority.
    """

    if explicit is None:
        if os.name == "nt":
            return WINDOWS_DEFAULT_WORK_ROOT
        raise RuntimeError(
            "non-Windows retail asset materialization requires an explicit "
            "absolute --work-root below the canonical repository local-lab"
        )

    expanded = explicit.expanduser()
    if not expanded.is_absolute():
        raise RuntimeError("--work-root must be an absolute path")
    if ".." in expanded.parts:
        raise RuntimeError("--work-root must not contain parent traversal")

    candidate = Path(os.path.abspath(expanded))
    repository = ROOT.resolve(strict=True)
    canonical_lab: Path | None = None
    if os.name != "nt":
        canonical_repository = _canonical_repository_root()
        canonical_lab = _plain_existing_directory(
            canonical_repository / "local-lab",
            "canonical repository local-lab",
        )
        if candidate == canonical_lab:
            raise RuntimeError(
                "--work-root must be a descendant of the canonical repository "
                "local-lab, not the local-lab owner itself"
            )
        if not candidate.is_relative_to(canonical_lab):
            if _paths_overlap(candidate, canonical_repository) or _paths_overlap(
                candidate, repository
            ):
                raise RuntimeError(
                    "--work-root must not use tracked or arbitrary repository paths; "
                    "Linux staging is allowed only below the canonical repository "
                    "local-lab"
                )
            raise RuntimeError(
                "Linux --work-root must be a plain descendant of the exact "
                f"canonical repository local-lab: {canonical_lab}"
            )
    elif _paths_overlap(candidate, repository):
        # Preserve the historical explicit Windows scratch-root policy. Its
        # implicit launcher default returns above before reaching this branch.
        raise RuntimeError("--work-root must be disjoint from the repository")

    parent = _plain_existing_directory(candidate.parent, "--work-root parent")
    selected = parent / candidate.name
    if selected.exists() or selected.is_symlink():
        info = selected.lstat()
        if _is_link_or_reparse(info):
            raise RuntimeError("--work-root is a link or reparse point")
        if not stat.S_ISDIR(info.st_mode):
            raise RuntimeError("--work-root is not a plain directory")
        selected = selected.resolve(strict=True)

    if canonical_lab is not None and (
        selected == canonical_lab or not selected.is_relative_to(canonical_lab)
    ):
        raise RuntimeError(
            "--work-root resolved outside the canonical repository local-lab"
        )
    if canonical_lab is None and _paths_overlap(selected, repository):
        raise RuntimeError("--work-root must be disjoint from the repository")
    if game_root is not None:
        retail = game_root.resolve(strict=True)
        if _paths_overlap(selected, retail):
            raise RuntimeError(
                "--work-root must be disjoint from the retail installation"
            )
    return selected


def _host_default_work_root_request() -> Path | None:
    """Return the explicit, platform-appropriate launcher staging request.

    Windows launchers retain their historical implicit ignored path. Linux
    launchers deliberately name the canonical checkout's real local-lab owner;
    the request still passes through ``_resolve_work_root`` and all of its
    link, overlap, and ownership checks before anything is written.
    """

    if os.name == "nt":
        return None
    return _canonical_repository_root() / "local-lab/rebuild-godot"


def _extract_chunk(raw: bytes, tag: bytes, expected_size: int, expected_hash: str) -> bytes:
    matches: list[bytes] = []
    cursor = 0
    while True:
        offset = raw.find(tag, cursor)
        if offset < 0:
            break
        cursor = offset + 1
        if offset + 8 > len(raw):
            continue
        declared = struct.unpack_from("<I", raw, offset + 4)[0]
        end = offset + 8 + declared
        if end > len(raw) or end - offset != expected_size:
            continue
        candidate = raw[offset:end]
        if _sha256(candidate) == expected_hash:
            matches.append(candidate)
    if len(matches) != 1:
        raise RuntimeError(f"expected one exact {tag.decode('ascii')} chunk, found {len(matches)}")
    return matches[0]


def _chunk_records(data: bytes) -> list[tuple[bytes, bytes]]:
    records: list[tuple[bytes, bytes]] = []
    offset = 0
    while offset < len(data):
        if offset + 8 > len(data):
            raise RuntimeError("retail data contains a truncated chunk")
        size = struct.unpack_from("<I", data, offset + 4)[0]
        end = offset + 8 + size
        if end > len(data):
            raise RuntimeError("retail data contains an overrun chunk")
        records.append((data[offset : offset + 4], data[offset + 8 : end]))
        offset = end
    return records


def _chunk_payload(data: bytes, wanted_tag: bytes) -> bytes:
    matches = [payload for tag, payload in _chunk_records(data) if tag == wanted_tag]
    if len(matches) != 1:
        raise RuntimeError(
            f"expected one {wanted_tag.decode('ascii')} world chunk, found {len(matches)}"
        )
    return matches[0]


class _WorldReader:
    def __init__(self, data: bytes) -> None:
        self.data = data
        self.position = 0

    def _take(self, size: int) -> bytes:
        end = self.position + size
        if size < 0 or end > len(self.data):
            raise RuntimeError("Level 100 base-world data is truncated")
        value = self.data[self.position : end]
        self.position = end
        return value

    def uint8(self) -> int:
        return self._take(1)[0]

    def uint16(self) -> int:
        return struct.unpack("<H", self._take(2))[0]

    def int32(self) -> int:
        return struct.unpack("<i", self._take(4))[0]

    def single(self) -> float:
        value = struct.unpack("<f", self._take(4))[0]
        if not math.isfinite(value):
            raise RuntimeError("Level 100 base-world data contains a non-finite value")
        return value

    def string8(self) -> str:
        return self._take(self.uint8()).decode("ascii")

    def string32(self) -> str:
        size = self.int32()
        if size < 0 or size > 1_048_576:
            raise RuntimeError("Level 100 world data has an invalid string length")
        return self._take(size).decode("ascii")

    def c_string(self) -> str:
        end = self.data.find(b"\0", self.position)
        if end < 0:
            raise RuntimeError("Level 100 base-world data has an unterminated string")
        value = self.data[self.position : end].decode("ascii")
        self.position = end + 1
        return value


class _WorldTreeGroup(NamedTuple):
    name: str
    header_offset: int
    records_offset: int
    records_sha256: str
    placements: tuple[tuple[int, int, int], ...]


class _WorldTreeTable(NamedTuple):
    header_offset: int
    end_offset: int
    groups: tuple[_WorldTreeGroup, ...]


def _read_explicit_tree_groups(reader: _WorldReader) -> _WorldTreeTable:
    """The supported two-group table, retaining float words and record order.

    World-specific names/counts and whether a group is instantiated belong to
    the caller. Equal placements are distinct serialized records.
    """
    header_offset = reader.position
    if reader.uint16() != 0 or reader.int32() != 2:
        raise RuntimeError("world tree groups are not the supported explicit layout")
    groups: list[_WorldTreeGroup] = []
    for _ in range(2):
        group_offset = reader.position
        name = reader.string8()
        count = reader.int32()
        if count < 0 or count > 4096 or any(group.name == name for group in groups):
            raise RuntimeError("world has invalid explicit tree metadata")
        records_offset = reader.position
        placements = []
        for _ in range(count):
            raw = reader._take(12)
            x_bits, y_bits, variant = struct.unpack("<iii", raw)
            if not all(math.isfinite(value) for value in struct.unpack("<2f", raw[:8])):
                raise RuntimeError("world tree placement contains a non-finite value")
            if variant not in range(4):
                raise RuntimeError("world has an unsupported tree variant")
            placements.append((x_bits, y_bits, variant))
        groups.append(_WorldTreeGroup(name, group_offset, records_offset,
            _sha256(reader.data[records_offset:reader.position]), tuple(placements)))
    return _WorldTreeTable(header_offset, reader.position, tuple(groups))


def _require_snow_tree_groups(table: _WorldTreeTable) -> None:
    if [(group.name, len(group.placements)) for group in table.groups] != [
        ("fernsnow", 753), ("pinesnow", 1481)
    ]:
        raise RuntimeError("world snow tree groups changed")


def _parse_static_world_inputs(
    raw_level: bytes, *, preserve_serialized_names: bool = False,
    safe_sides: list[dict[str, object]] | None = None,
) -> tuple[list[dict[str, object]], _WorldTreeTable]:
    bswd = _chunk_payload(_chunk_payload(_chunk_payload(raw_level, b"WRES"), b"WRLD"), b"BSWD")
    reader = _WorldReader(bswd)
    if (
        reader.uint16() != 50
        or tuple(reader.int32() for _ in range(3)) != (3, 42, 1)
        or reader.int32() != 1
        or reader.string8() != "Paladin Prototype"
        or tuple(reader.int32() for _ in range(4)) != (1, 0, 0, 0)
        or tuple(reader.int32() for _ in range(4)) != (-1, 0, 1, 0)
        or reader.uint16() != 35
    ):
        raise RuntimeError("Level 100 base-world header is not the supported version-50 layout")

    objects: list[dict[str, object]] = []
    for ordinal in range(35):
        thing_type = reader.int32()
        position = [reader.single() for _ in range(3)]
        yaw_pitch_roll = [reader.single() for _ in range(3)]
        mesh_number = reader.int32()
        allegiance = reader.int32()
        target = reader.int32()
        script = reader.c_string()
        name = reader.c_string()
        spawn_script = reader.c_string()
        active = reader.int32()
        attach_scripts = reader.int32()

        definition = ""
        if thing_type in (8, 35):
            definition = reader.string8()
            reader.int32()
        elif thing_type != 37:
            raise RuntimeError(f"unsupported Level 100 base-world thing type {thing_type}")

        if thing_type == 37:
            # A CSafeSide: no actor, but a retreat target
            # (_safe_side_record; 0x004fd910 walks their list).
            if safe_sides is not None:
                safe_sides.append(_safe_side_record("base", ordinal, position, allegiance))
            continue
        mesh_key = STATIC_MESH_BY_DEFINITION.get(definition)
        if mesh_key is None:
            raise RuntimeError(f"unmapped Level 100 physics definition: {definition}")
        objects.append(
            {
                "definition": definition,
                "active": active != 0,
                "allegiance": allegiance,
                "attachScripts": attach_scripts != 0,
                "mesh": mesh_key,
                "meshNumber": mesh_number,
                "name": name if preserve_serialized_names else name or definition,
                "ordinal": ordinal,
                "retailOrientation": yaw_pitch_roll,
                "retailPosition": position,
                "script": script,
                "spawnScript": spawn_script,
                "target": target,
                "thingType": thing_type,
                "yaw": yaw_pitch_roll[0],
            }
        )
        if preserve_serialized_names:
            objects[-1]["activeWord"] = active
            objects[-1]["attachScriptsToUnitsWord"] = attach_scripts

    trees = _read_explicit_tree_groups(reader)
    _require_snow_tree_groups(trees)
    if len(objects) != 33 or reader.position != 29_549:
        raise RuntimeError("Level 100 base-world object/tree counts do not reproduce")
    return objects, trees


def _safe_side_record(world: str, row: int, position: list[float], allegiance: int) -> dict[str, object]:
    """One CSafeSide (type 37): Init 0x004de190 prepends it to the list at
    0x00855160 and keeps the row's allegiance (+0x7c, read by slot 66
    0x004bfc10); a unit's retreat point (0x004fd910) is the nearest one of its
    own side. Rows are listed in construction order."""
    return {
        "allegiance": allegiance,
        "retailPositionFloatBits": [struct.unpack("<i", struct.pack("<f", value))[0] for value in position],
        "row": row,
        "world": world,
    }


def _level100_safe_sides(
    raw_level: bytes, level_actors: list[dict[str, object]],
) -> list[dict[str, object]]:
    """Level 100's CSafeSides in construction order: the base world's rows,
    then the level world's (level100-construction-order.md)."""
    safe_sides: list[dict[str, object]] = []
    _parse_static_world_inputs(raw_level, safe_sides=safe_sides)
    return safe_sides + [
        _safe_side_record("level", int(actor["ordinal"]), list(actor["retailPosition"]), int(actor["allegiance"]))
        for actor in level_actors if actor["thingType"] == 37
    ]


def _parse_static_world(
    raw_level: bytes, *, preserve_serialized_names: bool = False,
) -> tuple[list[dict[str, object]], list[list[float | int]], int]:
    """Existing render/shadow projection; no second interpretation of records."""
    objects, trees = _parse_static_world_inputs(
        raw_level, preserve_serialized_names=preserve_serialized_names)
    ferns, pines = trees.groups
    placements = [[*struct.unpack("<2f", struct.pack("<2i", x, y)), variant]
                  for x, y, variant in pines.placements]
    return objects, placements, len(ferns.placements)


LEVEL100_SCRIPT_NAMES = tuple(item[0] for item in LEVEL100_SCRIPT_OBJECTS)


def _skip_level100_script_value(reader: _WorldReader) -> None:
    value_type = reader.int32()
    if value_type == 0:
        return
    if value_type in (1, 2, 4, 5):
        reader.int32()
        return
    if value_type == 3:
        reader.string32()
        return
    if value_type == 6:
        for _ in range(3):
            reader.int32()
        return
    raise RuntimeError(f"Level 100 script object has unsupported value type {value_type}")


def _skip_level100_script_object(reader: _WorldReader) -> tuple[str, int]:
    name = reader.string32()
    instruction_count = reader.int32()
    if instruction_count < 0 or instruction_count > 20_000:
        raise RuntimeError("Level 100 script object has an invalid instruction count")
    reader._take(instruction_count * 8)
    reader._take(13 * 4)
    symbol_count = reader.int32()
    if symbol_count < 0 or symbol_count > 4_096:
        raise RuntimeError("Level 100 script object has an invalid symbol count")
    for ordinal in range(symbol_count):
        reader.string32()
        _skip_level100_script_value(reader)
        reader.int32()
        if reader.int32() != ordinal or reader.int32() != 1:
            raise RuntimeError("Level 100 script object has invalid symbol metadata")
    if reader.int32() != symbol_count:
        raise RuntimeError("Level 100 script object has an invalid symbol trailer")
    event_count = reader.int32()
    if event_count < 0 or event_count > 256:
        raise RuntimeError("Level 100 script object has an invalid event count")
    for _ in range(event_count):
        reader.int32()
        if reader.int32() != 1:
            raise RuntimeError("Level 100 script event parameter count changed")
        reader.int32()
    if reader.int32() != 0 or reader.int32() not in (0, 1):
        raise RuntimeError("Level 100 script object has an invalid trailer")
    payload_end = reader.position
    if reader._take(10) != b"end_script":
        raise RuntimeError("Level 100 script object end marker changed")
    return name, payload_end


def _parse_world_scripts(
    raw_level: bytes,
    world_number: int,
    script_objects: tuple[tuple[str, int, str], ...],
    level_header: _WorldLevelHeader = COMMON_WORLD_LEVEL_HEADER,
) -> dict[str, bytes]:
    rlwd = _chunk_payload(_chunk_payload(_chunk_payload(raw_level, b"WRES"), b"WRLD"), b"RLWD")
    reader = _WorldReader(rlwd)
    version = reader.uint16()
    header_words = tuple(reader.int32() for _ in range(3))
    name_count = reader.int32()
    names = tuple(reader.string8() for _ in range(name_count))
    trailing_words = tuple(reader.int32() for _ in range(5))
    script_count = reader.int32()
    if (
        version != level_header.version
        or header_words != (*level_header.header_words, world_number)
        or names != level_header.names
        or trailing_words != level_header.trailing_words
        or script_count != len(script_objects)
    ):
        raise RuntimeError(
            f"world {world_number} level-world header changed "
            f"(version={version}, words={header_words}, names={names}, "
            f"trailing={trailing_words}, scripts={script_count})"
        )
    scripts: dict[str, bytes] = {}
    for expected_name, expected_size, expected_hash in script_objects:
        start = reader.position
        name, payload_end = _skip_level100_script_object(reader)
        payload = rlwd[start:payload_end]
        if (
            name != expected_name
            or len(payload) != expected_size
            or _sha256(payload) != expected_hash
            or name in scripts
        ):
            raise RuntimeError(
                f"world {world_number} compiled script {expected_name} changed "
                f"(name={name!r}, size={len(payload)}, SHA-256={_sha256(payload)})"
            )
        scripts[name] = payload
    return scripts


def _world_single_bits(reader: _WorldReader, world_number: int) -> int:
    raw = reader._take(4)
    value = struct.unpack("<f", raw)[0]
    if not math.isfinite(value):
        raise RuntimeError(
            f"world {world_number} initial-object data contains a non-finite value"
        )
    return struct.unpack("<I", raw)[0]


def _signed_int32_bits(bits: int) -> int:
    """Preserve one IEEE-754 word in JSON's signed Core ``int`` shape."""

    return struct.unpack("<i", struct.pack("<I", bits))[0]


def _parse_world_initial_objects(
    reader: _WorldReader,
    world_number: int,
    expected_header: tuple[int, int, int],
    expected_type_counts: dict[int, int],
) -> tuple[_WorldInitialObject, ...]:
    """Walk one exact later-world object table and land on its tree header.

    This is deliberately separate from the Level 100 actor/waypoint parser:
    later worlds add tails that Level 100 never uses, and widening that parser
    would weaken its existing level-specific assertions.
    """

    header = (reader.int32(), reader.int32(), reader.uint16())
    if header != expected_header:
        raise RuntimeError(
            f"world {world_number} initial-object header changed ({header})"
        )

    objects: list[_WorldInitialObject] = []
    for ordinal in range(header[2]):
        record_offset = reader.position
        thing_type = reader.int32()
        position_bits = tuple(
            _world_single_bits(reader, world_number) for _ in range(3)
        )
        orientation_bits = tuple(
            _world_single_bits(reader, world_number) for _ in range(3)
        )
        mesh_number = reader.int32()
        allegiance = reader.int32()
        target = reader.int32()
        script = reader.c_string()
        name = reader.c_string()
        spawn_script = reader.c_string()
        active = reader.int32()
        attach_scripts = reader.int32()
        if active not in (0, 1) or attach_scripts not in (0, 1):
            raise RuntimeError(
                f"world {world_number} initial object {ordinal} has a non-boolean flag"
            )

        if thing_type == 8:
            definition_name = reader.string8()
            trailer = reader.int32()
            if trailer != -1:
                raise RuntimeError(
                    f"world {world_number} type-8 definition trailer changed"
                )
            tail: _WorldInitialObjectTail = _WorldUnitSeedTail(
                definition_name,
                trailer,
            )
        elif thing_type == 15:
            plane_mode = reader.int32()
            player_number = reader.int32()
            if plane_mode not in (0, 1) or player_number not in (1, 2):
                raise RuntimeError(
                    f"world {world_number} type-15 start tail is invalid"
                )
            tail = _WorldStartSeedTail(plane_mode, player_number)
        elif thing_type == 18:
            tail = _WorldWaypointSeedTail()
        elif thing_type == 19:
            amount = reader.int32()
            delay_bits = _world_single_bits(reader, world_number)
            squad_delay_bits = _world_single_bits(reader, world_number)
            initial_delay_bits = _world_single_bits(reader, world_number)
            squad_size = reader.int32()
            spawn_unit = reader.c_string()
            spawner_spawn_script = reader.c_string()
            if amount <= 0 or amount > 10_000 or squad_size <= 0 or squad_size > 10_000:
                raise RuntimeError(
                    f"world {world_number} type-19 spawner cardinality is invalid"
                )
            tail = _WorldSpawnerSeedTail(
                amount,
                delay_bits,
                squad_delay_bits,
                initial_delay_bits,
                squad_size,
                spawn_unit,
                spawner_spawn_script,
            )
        elif thing_type == 27:
            tail = _WorldScriptSeedTail()
        elif thing_type == 28:
            amount = reader.int32()
            mode = reader.int32()
            definition_name = reader.string8()
            trailer = reader.int32()
            if trailer != -1:
                raise RuntimeError(
                    f"world {world_number} type-28 definition trailer changed"
                )
            if amount <= 0 or amount > 10_000 or mode < 0 or mode > 3:
                raise RuntimeError(
                    f"world {world_number} type-28 squad configuration is invalid"
                )
            tail = _WorldSquadSeedTail(
                amount,
                mode,
                definition_name,
                trailer,
            )
        elif thing_type == 36:
            tail = _WorldVolumeSeedTail(
                _world_single_bits(reader, world_number)
            )
        else:
            raise RuntimeError(
                f"world {world_number} initial object {ordinal} has unsupported "
                f"thing type {thing_type}"
            )

        record = reader.data[record_offset : reader.position]
        objects.append(
            _WorldInitialObject(
                ordinal,
                record_offset,
                len(record),
                _sha256(record),
                thing_type,
                position_bits,
                orientation_bits,
                mesh_number,
                allegiance,
                target,
                script,
                name,
                spawn_script,
                active,
                attach_scripts,
                tail,
            )
        )

    actual_type_counts = {
        thing_type: sum(item.thing_type == thing_type for item in objects)
        for thing_type in sorted({item.thing_type for item in objects})
    }
    if actual_type_counts != expected_type_counts:
        raise RuntimeError(
            f"world {world_number} initial-object type census changed "
            f"({actual_type_counts})"
        )
    if (reader.uint16(), reader.int32()) != WORLD110_TREE_GROUP_HEADER:
        raise RuntimeError(
            f"world {world_number} initial objects did not land on the tree groups"
        )
    return tuple(objects)


def _admit_world110_player_start(
    objects: tuple[_WorldInitialObject, ...],
) -> _WorldInitialObject:
    starts = tuple(item for item in objects if item.thing_type == 15)
    if len(starts) != 1:
        raise RuntimeError(
            f"world 110 requires exactly one authored start, found {len(starts)}"
        )
    start = starts[0]
    if (
        start.ordinal != WORLD110_PLAYER_START_ORDINAL
        or start.record_offset != WORLD110_PLAYER_START_RECORD_OFFSET
        or start.record_bytes != WORLD110_PLAYER_START_RECORD_BYTES
        or start.record_sha256 != WORLD110_PLAYER_START_RECORD_SHA256
        or start.position_bits != WORLD110_PLAYER_START_POSITION_BITS
        or start.orientation_bits != WORLD110_PLAYER_START_ORIENTATION_BITS
        or start.mesh_number != 0
        or start.allegiance != 0
        or start.target != -1
        or start.script != ""
        or start.name != ""
        or start.spawn_script != ""
        or start.active != 1
        or start.attach_scripts != 0
        or start.plane_mode != 0
        or start.player_number != 1
    ):
        raise RuntimeError("world 110 authored player-start record changed")
    return start


def _parse_world110_initial_object_seeds(
    raw_world: bytes,
) -> tuple[_WorldInitialObject, ...]:
    """Re-derive all 40 exact world-110 serialized constructor inputs."""

    rlwd = _chunk_payload(
        _chunk_payload(_chunk_payload(raw_world, b"WRES"), b"WRLD"),
        b"RLWD",
    )
    if len(rlwd) != WORLD110_RLWD_SIZE or _sha256(rlwd) != WORLD110_RLWD_SHA256:
        raise RuntimeError(
            "world 110 RLWD identity changed "
            f"(size={len(rlwd)}, SHA-256={_sha256(rlwd)})"
        )

    # Validate and skip the exact preamble/script table before interpreting any
    # initial-object bytes. _parse_world_scripts independently binds every
    # compiled object; the second reader retains the resulting byte offset.
    _parse_world_scripts(
        raw_world,
        110,
        LEVEL110_SCRIPT_OBJECTS,
        COMMON_WORLD_LEVEL_HEADER,
    )
    reader = _WorldReader(rlwd)
    reader.uint16()
    for _ in range(3):
        reader.int32()
    for _ in range(reader.int32()):
        reader.string8()
    for _ in range(5):
        reader.int32()
    script_count = reader.int32()
    if script_count != len(LEVEL110_SCRIPT_OBJECTS):
        raise RuntimeError("world 110 script count changed before initial objects")
    for _ in LEVEL110_SCRIPT_OBJECTS:
        _skip_level100_script_object(reader)

    if reader.position != WORLD110_INITIAL_OBJECT_HEADER_OFFSET:
        raise RuntimeError(
            "world 110 initial-object table offset changed "
            f"({reader.position})"
        )

    objects = _parse_world_initial_objects(
        reader,
        110,
        WORLD110_INITIAL_OBJECT_HEADER,
        WORLD110_INITIAL_OBJECT_TYPE_COUNTS,
    )
    if reader.position - 6 != WORLD110_TREE_GROUP_HEADER_OFFSET:
        raise RuntimeError(
            "world 110 tree-group boundary changed "
            f"({reader.position - 6})"
        )
    _admit_world110_player_start(objects)
    return objects


def _parse_world110_player_start(raw_world: bytes) -> _WorldInitialObject:
    """Filter the already-admitted all-40 table to its sole CStart row."""

    return _admit_world110_player_start(
        _parse_world110_initial_object_seeds(raw_world)
    )


def _world110_initial_object_tail_document(
    tail: _WorldInitialObjectTail,
) -> dict[str, object]:
    if isinstance(tail, _WorldUnitSeedTail):
        return {
            "definitionName": tail.definition_name,
            "kind": "unit",
            "trailer": tail.trailer,
        }
    if isinstance(tail, _WorldStartSeedTail):
        return {
            "kind": "start",
            "planeModeWord": tail.plane_mode,
            "playerNumber": tail.player_number,
        }
    if isinstance(tail, _WorldWaypointSeedTail):
        return {"kind": "waypoint"}
    if isinstance(tail, _WorldSpawnerSeedTail):
        return {
            "amount": tail.amount,
            "delayBits": _signed_int32_bits(tail.delay_bits),
            "initialDelayBits": _signed_int32_bits(tail.initial_delay_bits),
            "kind": "spawner",
            "spawnUnit": tail.spawn_unit,
            "spawnerSpawnScript": tail.spawner_spawn_script,
            "squadDelayBits": _signed_int32_bits(tail.squad_delay_bits),
            "squadSize": tail.squad_size,
        }
    if isinstance(tail, _WorldScriptSeedTail):
        return {"kind": "script"}
    if isinstance(tail, _WorldSquadSeedTail):
        return {
            "amount": tail.amount,
            "definitionName": tail.definition_name,
            "kind": "squad",
            "mode": tail.mode,
            "trailer": tail.trailer,
        }
    if isinstance(tail, _WorldVolumeSeedTail):
        return {
            "kind": "volume",
            "radiusBits": _signed_int32_bits(tail.radius_bits),
        }
    raise RuntimeError("world 110 initial object has an unsupported retained tail")


def _world110_initial_object_seed_bytes(
    objects: tuple[_WorldInitialObject, ...],
) -> bytes:
    """Render the exact deterministic local evidence consumed by Core."""

    if len(objects) != WORLD110_INITIAL_OBJECT_HEADER[2]:
        raise RuntimeError("world 110 initial-object seed count changed")
    expected_offset = WORLD110_INITIAL_OBJECT_FIRST_RECORD_OFFSET
    for ordinal, item in enumerate(objects):
        if item.ordinal != ordinal or item.record_offset != expected_offset:
            raise RuntimeError(
                f"world 110 initial-object serialized order changed at {ordinal}"
            )
        expected_offset += item.record_bytes
    if expected_offset != WORLD110_TREE_GROUP_HEADER_OFFSET:
        raise RuntimeError("world 110 initial-object table extent changed")
    _admit_world110_player_start(objects)

    rows: list[dict[str, object]] = []
    for item in objects:
        rows.append(
            {
                "activeWord": item.active,
                "allegiance": item.allegiance,
                "attachScriptsToUnitsWord": item.attach_scripts,
                "meshNumber": item.mesh_number,
                "name": item.name,
                "objectIdentity": f"wres:rlwd:{item.ordinal:04d}",
                "ordinal": item.ordinal,
                "orientationXBits": _signed_int32_bits(item.orientation_bits[0]),
                "orientationYBits": _signed_int32_bits(item.orientation_bits[1]),
                "orientationZBits": _signed_int32_bits(item.orientation_bits[2]),
                "positionXBits": _signed_int32_bits(item.position_bits[0]),
                "positionYBits": _signed_int32_bits(item.position_bits[1]),
                "positionZBits": _signed_int32_bits(item.position_bits[2]),
                "recordOffset": item.record_offset,
                "script": item.script,
                "serializedByteLength": item.record_bytes,
                "serializedSha256": item.record_sha256,
                "spawnScript": item.spawn_script,
                "tail": _world110_initial_object_tail_document(item.tail),
                "target": item.target,
                "thingType": item.thing_type,
            }
        )

    document = {
        "archive": {
            "relativePath": WORLD110_ARCHIVE,
            "sha256": WORLD110_ARCHIVE_SHA256,
        },
        "census": [
            {"count": count, "thingType": thing_type}
            for thing_type, count in WORLD110_INITIAL_OBJECT_TYPE_COUNTS.items()
        ],
        "headerA": WORLD110_INITIAL_OBJECT_HEADER[0],
        "headerB": WORLD110_INITIAL_OBJECT_HEADER[1],
        "initialObjectCount": WORLD110_INITIAL_OBJECT_HEADER[2],
        "rlwdByteLength": WORLD110_RLWD_SIZE,
        "rlwdInitialObjectHeaderOffset": WORLD110_INITIAL_OBJECT_HEADER_OFFSET,
        "rlwdSha256": WORLD110_RLWD_SHA256,
        "rows": rows,
        "schema": WORLD110_INITIAL_OBJECT_SEEDS_SCHEMA,
        "treeGroupHeaderA": WORLD110_TREE_GROUP_HEADER[0],
        "treeGroupHeaderB": WORLD110_TREE_GROUP_HEADER[1],
        "treeGroupHeaderOffset": WORLD110_TREE_GROUP_HEADER_OFFSET,
        "worldNumber": 110,
    }
    return (
        json.dumps(document, sort_keys=True, separators=(",", ":")) + "\n"
    ).encode("utf-8")


def _parse_level_world_scripts(
    raw_level: bytes,
) -> tuple[_WorldReader, dict[str, bytes]]:
    rlwd = _chunk_payload(_chunk_payload(_chunk_payload(raw_level, b"WRES"), b"WRLD"), b"RLWD")
    scripts = _parse_world_scripts(raw_level, 100, LEVEL100_SCRIPT_OBJECTS)
    reader = _WorldReader(rlwd)
    reader.uint16()
    for _ in range(3):
        reader.int32()
    reader.int32()
    reader.string8()
    for _ in range(4):
        reader.int32()
    reader.int32()
    reader.int32()
    for expected_name in LEVEL100_SCRIPT_NAMES:
        _skip_level100_script_object(reader)
    return reader, scripts


def _waypoint_target_chain(
    markers: dict[int, dict[str, object]],
    path_name: str,
    node_indices: list[int],
) -> tuple[list[int], bool]:
    """Order one named path's markers by their own `target` next-node pointers.

    Every thingType-18 marker record carries a `target` ordinal, and within a
    single named path those pointers form exactly one chain over exactly that
    path's members. Two of the eight chains close on themselves; the serialized
    index list cannot express that, so the chain is the only field in the
    shipped data that can encode a loop at all.
    """
    members = list(node_indices)
    member_set = set(members)
    if len(member_set) != len(members):
        raise RuntimeError(f"Level 100 waypoint path {path_name!r} repeats a marker")
    targets = {node: int(markers[node]["target"]) for node in members}
    successors = {target for target in targets.values() if target in member_set}
    heads = [node for node in members if node not in successors]
    if len(heads) > 1:
        raise RuntimeError(
            f"Level 100 waypoint path {path_name!r} target pointers form "
            f"{len(heads)} chains rather than one"
        )
    is_closed = not heads
    head = members[0] if is_closed else heads[0]
    chain = [head]
    visited = {head}
    current = head
    while True:
        following = targets[current]
        if following not in member_set:
            if following != -1:
                raise RuntimeError(
                    f"Level 100 waypoint path {path_name!r} marker {current} "
                    f"targets {following}, which is outside its own path"
                )
            break
        if following in visited:
            if not is_closed or following != head:
                raise RuntimeError(
                    f"Level 100 waypoint path {path_name!r} target pointers "
                    "close on a node other than the chain head"
                )
            break
        chain.append(following)
        visited.add(following)
        current = following
    if sorted(chain) != sorted(members):
        raise RuntimeError(
            f"Level 100 waypoint path {path_name!r} target chain does not cover "
            "its own serialized markers"
        )
    return chain, is_closed


def _parse_level_world_actors_and_waypoints(
    raw_level: bytes,
) -> tuple[list[dict[str, object]], list[dict[str, object]]]:
    reader, _ = _parse_level_world_scripts(raw_level)
    if reader.int32() != 1 or reader.int32() != 0 or reader.uint16() != 45:
        raise RuntimeError("Level 100 initial-actor header changed")

    actors: list[dict[str, object]] = []
    for ordinal in range(45):
        thing_type = reader.int32()
        position = [reader.single() for _ in range(3)]
        orientation = [reader.single() for _ in range(3)]
        mesh_number = reader.int32()
        allegiance = reader.int32()
        target = reader.int32()
        script = reader.c_string()
        name = reader.c_string()
        spawn_script = reader.c_string()
        active = reader.int32()
        attach_scripts = reader.int32()
        definition = ""
        radius: float | None = None
        if thing_type in (8, 35):
            definition = reader.string8()
            if reader.int32() != -1:
                raise RuntimeError("Level 100 level-world unit definition trailer changed")
        elif thing_type == 15:
            reader.int32()
            reader.int32()
        elif thing_type == 36:
            radius = reader.single()
        elif thing_type not in (18, 27, 37):
            raise RuntimeError(f"unsupported Level 100 level-world thing type {thing_type}")
        actors.append(
            {
                "active": active != 0,
                "allegiance": allegiance,
                "attachScripts": attach_scripts != 0,
                "definition": definition,
                "meshNumber": mesh_number,
                "name": name,
                "ordinal": ordinal,
                "radius": radius,
                "retailOrientation": orientation,
                "retailPosition": position,
                "script": script,
                "spawnScript": spawn_script,
                "target": target,
                "thingType": thing_type,
            }
        )
    _require_snow_tree_groups(_read_explicit_tree_groups(reader))

    # The 121-entry table is the navigation/occupancy graph, NOT the array the
    # eight named paths index. It is still read and range-validated in full,
    # because the 198-edge count landing exactly at `+ 121 * 16` is what proves
    # this offset and stride; only its use as a path-coordinate source is gone.
    # Its 121 entries hold just 11 distinct XY tuples physically repeated 11
    # times (period 176 bytes), every one with z = 10.0 and w = 0.0 - which is
    # why reading paths out of it flattened every node to Y = +10000 mm and
    # aliased whole routes onto each other.
    if reader.uint16() != 1 or reader.int32() != 121:
        raise RuntimeError("Level 100 waypoint node table changed")
    navigation_nodes = [
        tuple(reader.single() for _ in range(4))
        for _ in range(121)
    ]

    edge_count = reader.int32()
    if edge_count != 198:
        raise RuntimeError("Level 100 waypoint edge table changed")
    for _ in range(edge_count):
        start = reader.int32()
        end = reader.int32()
        enabled = reader.int32()
        if start not in range(len(navigation_nodes)) or end not in range(len(navigation_nodes)):
            raise RuntimeError("Level 100 waypoint edge refers to an invalid node")
        if enabled != 1:
            raise RuntimeError("Level 100 waypoint edge state changed")

    # A named path's uint16 entries are RLWD *initial-actor ordinals* of the 30
    # thingType-18 marker records, not indices into the navigation graph above.
    # Three independent legs establish this, all measured from the shipped
    # archive (local-lab/AMBIENT-LIFE-INVENTORY-2026-07-27.md section 1.4):
    #   1. the union of all eight index lists is exactly the 30 thingType-18
    #      ordinals, each used exactly once - which the guard below re-checks
    #      on every materialization rather than trusting the note;
    #   2. every marker's own `target` field is a next-node pointer, and those
    #      pointers form exactly the eight chains the path lists name;
    #   3. only under this reading do the altitudes make sense - `Flyby Path`'s
    #      markers sit at the Air Trainer's own z = -15 and `Transporter Path`'s
    #      at z = -20, against a uniform z = +10 from the navigation graph.
    waypoint_markers = {
        int(actor["ordinal"]): actor
        for actor in actors
        if actor["thingType"] == 18
    }
    if len(waypoint_markers) != 30:
        raise RuntimeError(
            f"Level 100 authored waypoint marker count changed ({len(waypoint_markers)})"
        )
    path_count = reader.uint16()
    if path_count != 8:
        raise RuntimeError("Level 100 named waypoint path count changed")
    waypoint_paths: list[dict[str, object]] = []
    for _ in range(path_count):
        name = reader.string8()
        node_count = reader.int32()
        if node_count <= 0 or node_count > len(waypoint_markers):
            raise RuntimeError(f"Level 100 waypoint path {name!r} has an invalid node count")
        points: list[dict[str, object]] = []
        for _ in range(node_count):
            node_index = reader.uint16()
            marker = waypoint_markers.get(node_index)
            if marker is None:
                raise RuntimeError(
                    f"Level 100 waypoint path {name!r} refers to initial-actor "
                    f"ordinal {node_index}, which is not an authored waypoint marker"
                )
            components = marker["retailPosition"]
            points.append(
                {
                    "positionMillimeters": [
                        _round_away_from_zero(
                            (components[0] - LEVEL100_PLAYER_START_X) * 1_000.0
                        ),
                        _round_away_from_zero(components[2] * 1_000.0),
                        _round_away_from_zero(
                            (components[1] - LEVEL100_PLAYER_START_Y) * 1_000.0
                        ),
                    ],
                    "nodeIndex": node_index,
                    # The marker record carries three position floats, against
                    # the navigation graph's four. `Level100FloatVector4Bits` is
                    # a Core-owned shape this tool cannot narrow, so the fourth
                    # slot is the float bits of 0.0f: a structural filler, not a
                    # retail value. The graph's own w was 0.0 for all 121
                    # entries, so nothing measured is lost by the substitution.
                    "retailComponentsFloatBits": [
                        _float_bits(components[0]),
                        _float_bits(components[1]),
                        _float_bits(components[2]),
                        _float_bits(0.0),
                    ],
                }
            )
        # Serialized order and `target`-chain order agree for `Target Truck
        # Path 1` and `Drone Path 1` and disagree - reversed or rotated - for
        # the other six. Which one retail traverses is NOT settled here, so both
        # are recorded and `points` deliberately keeps the serialized order the
        # consumer already reads. `isClosed` is the loop fact the serialized
        # list cannot carry; consuming it is a separate mechanics change.
        chain, is_closed = _waypoint_target_chain(
            waypoint_markers,
            name,
            [int(point["nodeIndex"]) for point in points],
        )
        waypoint_paths.append(
            {
                "name": name,
                "points": points,
                "targetChainNodeIndices": chain,
                "isClosed": is_closed,
            }
        )

    # The guard that would have caught the defect this replaced: the eight paths
    # must partition the 30 authored markers, and no two emitted points may
    # share a position. Under the navigation-graph reading both failed - the
    # eight paths drew 30 indices from an 11-position table, so `Flyby Path` and
    # `Target Truck Path 2` resolved to the same first three points.
    used = [
        int(point["nodeIndex"])
        for path in waypoint_paths
        for point in path["points"]
    ]
    if len(used) != len(set(used)) or set(used) != set(waypoint_markers):
        raise RuntimeError(
            "Level 100 named waypoint paths no longer partition the 30 authored "
            "waypoint markers exactly once each"
        )
    emitted = [
        tuple(point["positionMillimeters"])
        for path in waypoint_paths
        for point in path["points"]
    ]
    if len(set(emitted)) != len(emitted):
        raise RuntimeError("Level 100 waypoint paths resolved two markers to one position")

    expected_paths = {
        "Flyby Path": (43, 42, 41),
        "Target Truck Path 3": (36, 35, 34, 33),
        "Target Truck Path 2": (32, 31, 30, 29),
        "Target Truck Path 1": (25, 26, 27, 28),
        "Transporter Path": (44, 22, 23),
        "Target Tank Path 2": (38, 37, 8, 10, 24),
        "Target Tank Path 1": (18, 6, 7),
        "Drone Path 1": (1, 2, 3, 4),
    }
    actual_paths = {
        str(path["name"]): tuple(
            int(point["nodeIndex"]) for point in path["points"]
        )
        for path in waypoint_paths
    }
    if actual_paths != expected_paths:
        raise RuntimeError("Level 100 named waypoint paths changed")
    return actors, waypoint_paths


def _slug(value: str) -> str:
    result = re.sub(r"[^a-z0-9]+", "-", value.lower()).strip("-")
    if not result:
        raise RuntimeError("retail asset name did not produce a safe local name")
    return result


def _texture_key(texture_ref: str) -> str:
    name = texture_ref.replace("/", "\\").rsplit("\\", 1)[-1]
    if name.lower().endswith(".tga"):
        name = name[:-4]
    return _slug(name)


def _material_surface_name(texture_indices: tuple[int, int, int, int, int, int]) -> str:
    return "layers-" + "-".join(f"{value:08x}" for value in texture_indices)


def _texture_pass_metadata(raw: bytes) -> tuple[float, float, float, float, float]:
    if len(raw) != 20:
        raise RuntimeError("mesh texture metadata has an unexpected length")
    values = struct.unpack("<5f", raw)
    opacity, offset_u, offset_v, scale_u, scale_v = values
    if (
        not all(math.isfinite(value) for value in values)
        or opacity < 0.0
        or opacity > 1.0
        or scale_u < 0.0
        or scale_v < 0.0
        or scale_u > 100.0
        or scale_v > 100.0
    ):
        raise RuntimeError("mesh texture metadata is outside the supported retail envelope")
    return opacity, offset_u, offset_v, scale_u, scale_v


def _texture_blend_alpha_flags(raw_level: bytes) -> dict[str, bool]:
    """Read the released CTexture +0xB4 mode from Level 100's DXTX records."""
    marker = b"DXTX\x6c\x01\x00\x00CTEX\x58\x01\x00\x00"
    flags: dict[str, bool] = {}
    record_count = 0
    offset = 0
    while True:
        offset = raw_level.find(marker, offset)
        if offset < 0:
            break
        payload_start = offset + len(marker)
        payload_end = payload_start + 344
        if payload_end > len(raw_level):
            raise RuntimeError("Level 100 has a truncated CTexture record")
        payload = raw_level[payload_start:payload_end]
        name_bytes = payload[8:72].split(b"\0", 1)[0]
        try:
            name = name_bytes.decode("ascii")
        except UnicodeDecodeError as error:
            raise RuntimeError("Level 100 has a non-ASCII CTexture name") from error
        normalized = name.replace("/", "\\").strip().lower()
        raw_flag = struct.unpack_from("<I", payload, 0xB4)[0]
        if not normalized or raw_flag not in (0, 1):
            raise RuntimeError("Level 100 has an unsupported CTexture record")
        flag = raw_flag == 1
        previous = flags.setdefault(normalized, flag)
        if previous != flag:
            raise RuntimeError(f"Level 100 CTexture mode disagrees for {name}")
        record_count += 1
        offset = payload_end

    if (
        record_count != 273
        or len(flags) != 265
        or {name for name, enabled in flags.items() if enabled}
        != {"meshtex\\a8_fb_hangermorebits_lit.tga"}
    ):
        raise RuntimeError("Level 100 CTexture modes do not match the released archive")
    return flags


def _dds_metadata(source: bytes, inflate_aya_bytes) -> tuple[int, int, str]:
    dds = inflate_aya_bytes(source)
    if len(dds) < 128 or dds[:4] != b"DDS ":
        raise RuntimeError("static-world texture is not an AYA-wrapped DDS image")
    height = struct.unpack_from("<I", dds, 12)[0]
    width = struct.unpack_from("<I", dds, 16)[0]
    fourcc = dds[84:88]
    compression = {b"DXT1": "Dxt1", b"DXT2": "Dxt2"}.get(fourcc)
    if (
        compression is None
        and fourcc == b"\0\0\0\0"
        and struct.unpack_from("<I", dds, 80)[0] == 0x41
        and struct.unpack_from("<I", dds, 88)[0] == 32
        and struct.unpack_from("<IIII", dds, 92)
        == (0x00FF0000, 0x0000FF00, 0x000000FF, 0xFF000000)
    ):
        compression = "Rgba8"
    if compression is None or width not in (64, 128, 256, 512) or height != width:
        raise RuntimeError("static-world texture has unsupported dimensions or compression")
    return width, height, compression


def _source_aggregate(source_data: dict[Path, bytes]) -> str:
    rows = [
        f"{path.as_posix()}|{len(data)}|{_sha256(data)}"
        for path, data in source_data.items()
    ]
    rows.sort(key=str.casefold)
    return _sha256(("\n".join(rows) + "\n").encode("utf-8"))


PINE_CENTER_BITS = {
    0: (0xBCCC7F20, 0x39BA4000, 0xBF6303AA),
    1: (0x3D8FAD60, 0xBDA96080, 0xBF696408),
    2: (0x3C9B2D60, 0xBDF5D470, 0xBF6A0AB4),
    3: (0x3D429CA0, 0x3CD68540, 0xBF506532),
}


def _pine_imposter_views(raw_level: bytes) -> list[list[list[float]]]:
    outer = _chunk_payload(raw_level, b"IMPS")
    if len(outer) != 10_208:
        raise RuntimeError("Level 100 has an unexpected outer IMPS envelope")
    payload = _chunk_payload(outer, b"IMPS")
    if len(payload) != 10_200 or payload.count(b"Imposters_100\0") != 1:
        raise RuntimeError("Level 100 does not select the expected imposter atlas")

    views: list[list[list[float]]] = []
    for variant in range(4):
        name = f"pinesnow{variant}.MSH".encode("ascii")
        name_offset = payload.find(name)
        if name_offset < 0 or payload.find(name, name_offset + 1) >= 0:
            raise RuntimeError(f"Level 100 has an ambiguous {name.decode()} imposter")
        record_offset = payload.rfind(b"IMPT", 0, name_offset)
        if record_offset < 0:
            raise RuntimeError(f"Level 100 has no IMPT owner for {name.decode()}")
        record_size = struct.unpack_from("<I", payload, record_offset + 4)[0]
        record_end = record_offset + 8 + record_size
        if record_size != 248 or not (record_offset < name_offset < record_end):
            raise RuntimeError(f"Level 100 has an invalid {name.decode()} IMPT record")
        view_offset = payload.find(b"VIEW", name_offset, record_end)
        if (
            view_offset < 0
            or payload.find(b"VIEW", view_offset + 1, record_end) >= 0
            or struct.unpack_from("<I", payload, view_offset + 4)[0] != 144
            or view_offset + 152 != record_end
        ):
            raise RuntimeError(f"Level 100 has invalid {name.decode()} VIEW data")
        view_payload = payload[view_offset + 8 : record_end]
        all_views = [
            list(struct.unpack_from("<6f", view_payload, index * 24))
            for index in range(6)
        ]
        if any(
            len(view) != 6
            or not all(math.isfinite(value) for value in view)
            or not (0.0 <= view[0] < view[1] <= 1.0)
            or not (0.0 <= view[2] < view[3] <= 1.0)
            or view[4] <= 0.0
            or view[5] <= 0.0
            for view in all_views
        ):
            raise RuntimeError(f"Level 100 {name.decode()} imposter views are invalid")
        views.append(all_views)
    return views


def _pine_global_bbox_words(source: bytes, inflate_aya, variant: int) -> list[int]:
    inflated = inflate_aya(source)
    outer_offset = len(inflated) - 56
    inner_offset = outer_offset + 8
    if (
        outer_offset < 0
        or inflated[outer_offset : outer_offset + 4] != b"BBOX"
        or struct.unpack_from("<I", inflated, outer_offset + 4)[0] != 48
        or inflated[inner_offset : inner_offset + 4] != b"BBOX"
        or struct.unpack_from("<I", inflated, inner_offset + 4)[0] != 40
        or inner_offset + 48 != len(inflated)
    ):
        raise RuntimeError(f"pinesnow{variant} has no exact final global BBOX")
    center_bits = struct.unpack_from("<3I", inflated, inner_offset + 8)
    if center_bits != PINE_CENTER_BITS[variant]:
        raise RuntimeError(f"pinesnow{variant} global BBOX center changed")
    words = list(struct.unpack_from("<10i", inflated, inner_offset + 8))
    values = struct.unpack_from("<10f", inflated, inner_offset + 8)
    if words[8] != 1 or any(not math.isfinite(values[i]) for i in (0, 1, 2, 4, 5, 6, 9)) or any(values[i] <= 0 for i in (4, 5, 6, 9)):
        raise RuntimeError(f"pinesnow{variant} global BBOX is invalid")
    return words


def _pine_global_center(source: bytes, inflate_aya, variant: int) -> list[float]:
    # Retain the unchanged Level100 rendering projection from the one reader.
    words = _pine_global_bbox_words(source, inflate_aya, variant)
    return list(struct.unpack("<3f", struct.pack("<3i", *words[:3])))


def _read_pine_meshes(game_root: Path) -> tuple[bytes, ...]:
    from aya_archive_inventory import build_asset_resolver
    resolver = build_asset_resolver(game_root / "data/resources")
    result = []
    for variant, key in enumerate(PINE_MESH_KEYS):
        matches = resolver.mesh_index.get(f"{key}.msh", [])
        if len(matches) != 1:
            raise RuntimeError(f"expected one exact loose mesh for {key}, found {len(matches)}")
        result.append(_read_exact(Path(matches[0]), PINE_MESH_SHA256[variant]))
    return tuple(result)


def _world110_tree_mesh_inputs(pine_meshes: tuple[bytes, ...]) -> list[dict[str, object]]:
    from cmsh_static_preview import inflate_aya
    if len(pine_meshes) != 4:
        raise RuntimeError("world 110 requires four pine meshes")
    result = []
    for variant, source in enumerate(pine_meshes):
        if _sha256(source) != PINE_MESH_SHA256[variant]:
            raise RuntimeError(f"world 110 pine mesh identity changed: {variant}")
        inflated = inflate_aya(source)
        result.append({
            "variant": variant,
            "meshName": f"pinesnow{variant}.MSH",
            "sourceSha256": PINE_MESH_SHA256[variant],
            # CMSH payload starts at8; CRTTree::Init copies mesh+0x164.
            "meshRadiusFloatBits": struct.unpack_from("<i", inflated, 8 + 0x164)[0],
            "globalBoundingBoxWords": _pine_global_bbox_words(source, inflate_aya, variant),
        })
    return result


LEVEL100_PLAYER_START_X = 288.6875
LEVEL100_PLAYER_START_Y = 243.25
# Retail Z is DOWN-positive and its zero is not Core's zero. Core's vertical
# axis is up and its datum is the authored player-start plane at retail
# Z = -10.0, pinned as
# `Level100Terrain.PlayerStartReferenceElevationMillimeters = -10_000` and used
# by `SampleGroundElevationMillimetersAtFixed`, which returns exactly
# `-10_000 - retailHeightMillimetres`. The Godot world-object path
# (`Level100StaticWorldAsset.Load`) and this file's own
# `authoredElevationMillimeters` both spell the same conversion
# `-10.0 - retailZ`. Anything writing a Core vertical must go through
# `_core_elevation_millimeters`.
LEVEL100_PLAYER_START_ELEVATION = -10.0

LEVEL100_SETUP_SCRIPT_BINDINGS = {
    "Tank Factory": "TankFactory",
    "Airfield": "Hangar",
    "Turret 01": "Turret",
    "Turret 02": "Turret",
    "Turret 03": "Turret",
    "Turret 04": "Turret",
    "Radar Station": "Facilities",
    "Forseti Research Building 1": "Facilities",
    "Health Pad": "Facilities",
    "Control Tower": "Facilities",
}


def _round_away_from_zero(value: float) -> int:
    if not math.isfinite(value):
        raise RuntimeError("Level 100 actor data contains a non-finite value")
    return math.floor(value + 0.5) if value >= 0 else math.ceil(value - 0.5)


def _float_bits(value: float) -> int:
    return struct.unpack("<i", struct.pack("<f", value))[0]


def _normalize_angle(value: float) -> float:
    while value > math.pi:
        value -= math.tau
    while value < -math.pi:
        value += math.tau
    return value


def _core_elevation_millimeters(retail_z: float) -> int:
    """Authored retail Z (down-positive) to Core Y (up-positive, player-plane)."""
    return _round_away_from_zero(
        (LEVEL100_PLAYER_START_ELEVATION - retail_z) * 1_000.0
    )


def _actor_pose(
    retail_position: list[float] | tuple[float, float, float],
    retail_orientation: list[float] | tuple[float, float, float],
    retail_basis: list[list[float]] | tuple[tuple[float, float, float], ...] | None = None,
) -> dict[str, list[int]]:
    basis = retail_basis or _retail_basis(retail_orientation)
    core_basis = _change_basis_retail_to_core(basis)
    return {
        "angularVelocityMicroRadiansPerTick": [0, 0, 0],
        "basisFloatBits": [
            _float_bits(component) for row in core_basis for component in row
        ],
        "linearVelocityMillimetersPerTick": [0, 0, 0],
        "positionMillimeters": [
            _round_away_from_zero((retail_position[0] - LEVEL100_PLAYER_START_X) * 1_000.0),
            # THE AUTHORED VERTICAL, in Core's own datum. This used to write
            # the raw authored Z-down value into a Core Y that every consumer
            # reads as up, which was wrong for every actor whose authored Z is
            # non-zero (14 of the 44).
            #
            # `_core_elevation_millimeters` is the whole producer half. Retail's
            # `CThing::Init` (0x004F34A0), read from the pristine specimen
            # local-lab/safe-copy-bea-pristine/BEA.exe.original.backup, sha256
            # 74154BFAE14DDC8ECB87A0766F5BC381C7B7F1AB334ED7A753040EDA1E1E7750,
            # copies the authored position in and then applies two clamps and
            # nothing else - `pos[2] = min(pos[2], SampleGroundHeight(x, y))`
            # and `pos[2] = min(pos[2], *(float*)0x006FBDFC)`, the water level.
            # The authored value is ABSOLUTE: only ever pulled toward a surface,
            # never offset, never terrain-relative, and there is no per-class
            # additive term at Init. Z is down-positive there, so both minima
            # are MAXIMA in Core's up-positive space.
            #
            # THE SECOND HALF IS NOT OPTIONAL AND LANDED WITH THIS ONE:
            # `Level100ActorRegistry.SeatOnGround` applies that support clamp.
            # It used to apply it only to the `GroundVehicle` motion class, so
            # this line alone would move every retail-Z-0 static from an
            # accidentally near-ground Core Y of 0 to a genuinely-authored but
            # unclamped -10000 - 10 m under the terrain - and leave 32 of the 33
            # static instances disagreeing with the elevation the renderer draws
            # them at. See local-lab/TARGET-CONTACT-GEOMETRY-2026-07-26.md
            # section 4 and local-lab/TTD-DATUM-ORIENTATION-2026-07-31.md.
            _core_elevation_millimeters(retail_position[2]),
            _round_away_from_zero((retail_position[1] - LEVEL100_PLAYER_START_Y) * 1_000.0),
        ],
    }


def _retail_basis(
    retail_orientation: list[float] | tuple[float, float, float],
) -> list[list[float]]:
    yaw, pitch, roll = retail_orientation
    # Every authored Level 100 actor uses yaw only. Preserve that released
    # contract explicitly rather than inventing an unobserved Euler order.
    if pitch != 0.0 or roll != 0.0:
        raise RuntimeError("Level 100 authored actor uses an unresolved pitch/roll basis")
    cosine = math.cos(yaw)
    sine = math.sin(yaw)
    return [[cosine, -sine, 0.0], [sine, cosine, 0.0], [0.0, 0.0, 1.0]]


def _matrix_multiply(left, right) -> list[list[float]]:
    return [
        [sum(left[row][axis] * right[axis][column] for axis in range(3))
         for column in range(3)]
        for row in range(3)
    ]


def _matrix_vector(matrix, vector) -> list[float]:
    return [sum(matrix[row][axis] * vector[axis] for axis in range(3)) for row in range(3)]


def _change_basis_retail_to_core(retail_basis) -> list[list[float]]:
    # Match position/velocity conversion Q(x,y,z)=(x,-z,y): Q*B*inverse(Q).
    # Direct selection/negation also preserves the float words' signed zeros.
    b = retail_basis
    return [[b[0][0], -b[0][2], b[0][1]],
            [-b[2][0], b[2][2], -b[2][1]],
            [b[1][0], -b[1][2], b[1][1]]]


def _authored_transform(position, orientation) -> dict[str, list[int]]:
    basis = _retail_basis(orientation)
    return {
        "retailBasisFloatBits": [
            _float_bits(component) for row in basis for component in row
        ],
        "retailEulerFloatBits": [_float_bits(component) for component in orientation],
        "retailPositionFloatBits": [_float_bits(component) for component in position],
    }


def _mesh_emitters(parsed) -> dict[str, dict[str, object]]:
    result: dict[str, dict[str, object]] = {}
    for binding in parsed.emitter_bindings():
        name, part_index = binding.name, binding.part_ordinal
        if not name.startswith("Spawner"):
            continue
        if part_index is None:
            raise RuntimeError(f"Level 100 emitter {name} has no mesh part")
        if name in result:
            raise RuntimeError(f"Level 100 mesh has duplicate emitter {name}")
        transform = parsed.parts[part_index].transform
        result[name] = {
            "basis": transform.rows,
            "partOrdinal": part_index,
            "position": transform.position,
        }
    return result


def _airfield_exit_waypoints(parsed) -> dict[str, list[dict[str, object]]]:
    """Selected constant model-space exit poses, distinct from launch emitters.

    Unit attachment tags 15/16 query WaypointA/B case-insensitively and retain
    the exact CEMT selector. The selected mesh has one point per exit, at 1.
    These are CPOS/CORI model inputs; the Core owner applies its seated pose.
    This does not flatten an animated or controller-dependent attachment.
    """
    if any(sibling.tag in (b"PMSH", b"PMS2") for sibling in parsed.siblings):
        raise RuntimeError("Airfield exit lookup requires the admitted single mesh")
    parts = parsed.file_parts()
    result = {"SpawnerA": [], "SpawnerB": []}
    for binding in parsed.emitter_bindings():
        if binding.name.casefold() not in ("waypointa", "waypointb"):
            continue
        if binding.part_ordinal is None:
            raise RuntimeError("Airfield exit waypoint has no mesh part")
        if binding.selector != 1:
            raise RuntimeError("Airfield exit waypoint selector changed")
        key = "Spawner" + binding.name[-1].upper()
        if result[key]:
            raise RuntimeError("Airfield exit waypoint binding is ambiguous")
        part = parts[binding.part_ordinal]
        ancestor_index, visited = binding.part_ordinal, set()
        while True:
            ancestor = parts[ancestor_index]
            if (ancestor_index in visited or ancestor.track is None
                    or ancestor.track.frame_map != (0,) * 101
                    or len(ancestor.track.hierarchy) != 1):
                raise RuntimeError("Airfield exit hierarchy is not the admitted constant pose")
            visited.add(ancestor_index)
            if ancestor.parent is None:
                break
            ancestor_index = ancestor.parent
        track = part.track
        # 004b1129..004b1169 resolves the two cache owners independently.
        # Selected exit points own CPOS but inherit CORI through +0x120.
        orientation_owner = part
        while struct.unpack_from("<I", orientation_owner.raw_cmsp, 0x120)[0]:
            if orientation_owner.parent is None:
                raise RuntimeError("Airfield exit orientation cache has no owner")
            orientation_owner = parts[orientation_owner.parent]
        if (struct.unpack_from("<I", part.raw_cmsp, 0x11c)[0]
                or len(track.cached_position_bytes) != 16
                or len(orientation_owner.track.cached_orientation_bytes) != 48):
            raise RuntimeError("Airfield exit model-pose cache changed")
        position = list(struct.unpack("<4i", track.cached_position_bytes)[:3])
        padded_basis = struct.unpack("<12i", orientation_owner.track.cached_orientation_bytes)
        basis = [padded_basis[index] for index in (0, 1, 2, 4, 5, 6, 8, 9, 10)]
        if (position != [_float_bits(value) for value in part.transform.position]
                or basis != [_float_bits(value) for row in part.transform.rows for value in row]):
            raise RuntimeError("Airfield exit model transform disagrees with its cache")
        result[key].append({
            "selector": binding.selector,
            "modelTransform": {"localPositionFloatBits": position,
                               "localBasisFloatBits": basis},
        })
    if any(len(points) != 1 for points in result.values()):
        raise RuntimeError("Airfield exit waypoint set is incomplete")
    return result


def _unit_construction_use(raw: bytes) -> dict[str, object]:
    """One ordered Unit field 7/18 tuple; preserve its raw creation flags."""
    try:
        definition_end = raw.index(0)
        tag_end = raw.index(0, definition_end + 1)
        if not definition_end or tag_end == definition_end + 1 or len(raw) != tag_end + 5:
            raise ValueError("invalid field length")
        return {
            "definitionName": raw[:definition_end].decode("ascii"),
            "tagName": raw[definition_end + 1:tag_end].decode("ascii"),
            "rawCreationFlags": struct.unpack_from("<I", raw, tag_end + 1)[0],
        }
    except (ValueError, UnicodeError) as error:
        raise RuntimeError("Unit construction use layout changed") from error


def _aircraft_weapon_mounts(parsed, fields: _PhysicsFields) -> list[dict[str, object]]:
    """Selected constant model inputs, not a replacement for live muzzle queries.

    Unit 004f8858 supplies selector1; 004fc6e0 maps GunA/B tags and
    004aa820 matches case-insensitive names plus the exact selector. The
    selected mesh also has GunA/2 before GunA/1. Its Trail animation does not
    change the admitted gun/root tracks. Renderer/profile cache state and
    Actor interpolation remain runtime inputs (CComplexThing.cpp.md).
    """
    if (_definition_string(fields, 9).casefold() != "fa_f24_training.msh"
            or any(sibling.tag in (b"PMSH", b"PMS2") for sibling in parsed.siblings)):
        raise RuntimeError("Aircraft weapon lookup requires the admitted single mesh")
    parts = parsed.file_parts()
    bindings = parsed.emitter_bindings()
    result = []
    for raw in fields.values(7):
        use = _unit_construction_use(raw)
        if use["tagName"] not in ("GunA", "GunB"):
            raise RuntimeError("Aircraft weapon attachment tag is not admitted")
        matches = [binding for binding in bindings
                   if binding.name.casefold() == use["tagName"].casefold()
                   and binding.selector == 1]
        if len(matches) != 1:
            raise RuntimeError("Aircraft weapon selector1 binding is missing or ambiguous")
        binding = matches[0]
        if binding.part_ordinal is None:
            raise RuntimeError("Aircraft weapon binding has no mesh part")
        part = parts[binding.part_ordinal]
        ancestor_index, visited = binding.part_ordinal, set()
        while True:
            ancestor = parts[ancestor_index]
            if (ancestor_index in visited or ancestor.track is None
                    or ancestor.track.frame_map != (0,) * 64
                    or len(ancestor.track.hierarchy) != 1):
                raise RuntimeError("Aircraft weapon hierarchy is not the admitted constant pose")
            visited.add(ancestor_index)
            if ancestor.parent is None:
                break
            ancestor_index = ancestor.parent
        track = part.track
        if (part.part_type != 5 or len(part.raw_cmsp) < 0x124
                or struct.unpack_from("<3I", part.raw_cmsp, 0x118) != (1, 0, 0)
                or len(track.cached_position_bytes) != 16
                or len(track.cached_orientation_bytes) != 48):
            raise RuntimeError("Aircraft weapon model-pose cache changed")
        position = list(struct.unpack("<4i", track.cached_position_bytes)[:3])
        padded_basis = struct.unpack("<12i", track.cached_orientation_bytes)
        basis = [padded_basis[index] for index in (0, 1, 2, 4, 5, 6, 8, 9, 10)]
        if (position != [_float_bits(value) for value in part.transform.position]
                or basis != [_float_bits(value) for row in part.transform.rows for value in row]):
            raise RuntimeError("Aircraft weapon model transform disagrees with its cache")
        result.append({
            "use": use,
            "selector": binding.selector,
            "modelTransform": {"localPositionFloatBits": position,
                               "localBasisFloatBits": basis},
        })
    return result


def _spawn_pose(
    owner: dict[str, object],
    emitter: dict[str, object],
) -> dict[str, list[int]]:
    owner_position = owner["retailPosition"]
    owner_orientation = owner["retailOrientation"]
    local_position = emitter["position"]
    local_basis = emitter["basis"]
    if not isinstance(owner_position, list) or not isinstance(owner_orientation, list):
        raise RuntimeError("Level 100 spawn owner has invalid authored transform data")
    owner_basis = _retail_basis(owner_orientation)
    world_basis = _matrix_multiply(owner_basis, local_basis)
    translated = _matrix_vector(owner_basis, local_position)
    world_position = [
        owner_position[axis] + translated[axis] for axis in range(3)
    ]
    world_yaw = math.atan2(world_basis[1][0], world_basis[0][0])
    return _actor_pose(
        world_position,
        [_normalize_angle(world_yaw), 0.0, 0.0],
        world_basis,
    )


def _world110_player_input_bytes(raw_world: bytes, configuration_data: bytes) -> bytes:
    """Real RLWD configuration names and the admitted shipped configuration fields.

    CWorld::LoadWorldHeader skips BSWD names and loads the RLWD name table.
    The existing source-bounded decoder owns file framing and unknown bytes.
    """
    sys.path.insert(0, str(ROOT / "tools"))
    import battle_engine_config_decode as configuration_decoder

    configurations = configuration_decoder.parse_exact_baseline(configuration_data)
    _parse_world110_initial_object_seeds(raw_world)
    rlwd = _chunk_payload(
        _chunk_payload(_chunk_payload(raw_world, b"WRES"), b"WRLD"), b"RLWD"
    )
    reader = _WorldReader(rlwd)
    reader.uint16()
    for _ in range(3):
        reader.int32()
    names = [reader.string8() for _ in range(reader.int32())]
    records = []
    for record in configurations.records:
        records.append({
            "name": record.configuration_name,
            "lifeBits": _signed_int32_bits(record.field("mLife").u32_bits),
            "energyBits": _signed_int32_bits(record.field("mEnergy").u32_bits),
            "storeHeat": [_signed_int32_bits(record.field(f"mStoreHeat[{n}]").u32_bits)
                          for n in range(configuration_decoder.STORE_COUNT)],
            "storeValueBits": [_signed_int32_bits(record.field(f"mStoreValue[{n}]").u32_bits)
                               for n in range(configuration_decoder.STORE_COUNT)],
        })
    return (json.dumps({
        "schema": "onslaught.world110-player-inputs.v1",
        "worldNumber": 110,
        "rlwdSha256": WORLD110_RLWD_SHA256,
        "configurationSourceSha256": configuration_decoder.BASELINE_SHA256,
        "configurationNames": names,
        "records": records,
    }, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")


def _world110_component_attachment(rows, physics, mesh_data: bytes) -> dict[str, object]:
    """The four first Component queries, not completed child initialization.

    Unit 0x004fc4e0 uses a shared local-pose cache for these fresh profiles.
    Their base Init changes no authored origin before the query. Complex Init
    0x004f4008..0x004f40cc supplies the yaw-only basis, including signed zeros.
    The three stored sine/cosine pairs were checked with native x87 arithmetic
    under explicit 53-bit, round-to-nearest control; this is not game execution.
    See the existing CUnit__UpdateTransform evidence owner for the bounds.
    """
    from cmsh_static_preview import inflate_aya, parse_cmsh_stream

    if _sha256(mesh_data) != WORLD110_LANDING_CRAFT_MESH_SHA256:
        raise RuntimeError("world 110 landing-craft mesh identity changed")
    inflated = inflate_aya(mesh_data)
    parsed = parse_cmsh_stream(inflated)
    matches = [binding for binding in parsed.emitter_bindings()
               if binding.name == "Component" and binding.selector == 1]
    if len(matches) != 1 or matches[0].part_ordinal != 31:
        raise RuntimeError("world 110 Component/1 mesh binding changed")
    parts = parsed.file_parts()
    for index, parent in ((31, 1), (1, 0), (0, None)):
        part = parts[index]
        if (part.parent != parent or part.track is None
                or part.track.frame_map != (0,) * 36 or len(part.track.hierarchy) != 1):
            raise RuntimeError("world 110 Component hierarchy is not the admitted constant pose")
    track = parts[31].track
    if len(track.cached_position_bytes) != 16 or len(track.cached_orientation_bytes) != 48:
        raise RuntimeError("world 110 Component model-pose cache changed")
    position = list(struct.unpack("<4i", track.cached_position_bytes)[:3])
    padded_basis = struct.unpack("<12i", track.cached_orientation_bytes)
    basis = [padded_basis[index] for index in (0, 1, 2, 4, 5, 6, 8, 9, 10)]
    if (position != [_float_bits(value) for value in parts[31].transform.position]
            or basis != [_float_bits(value) for row in parts[31].transform.rows for value in row]):
        raise RuntimeError("world 110 Component model pose disagrees with its cache")

    uses = []
    for row in rows:
        if row["serializedThingType"] != 8:
            continue
        actor = row["actor"]
        fields = _physics_record(physics, 1, actor["definitionName"])
        for raw in fields.values(27):
            name_end = raw.index(0)
            if len(raw) != name_end + 5:
                raise RuntimeError("world 110 component use layout changed")
            name = raw[:name_end].decode("ascii")
            selector = struct.unpack_from("<i", raw, name_end + 1)[0]
            if name != "Dropship Gun Turret" or selector != 1:
                raise RuntimeError("world 110 component use is outside the admitted profile")
            _physics_record(physics, 7, name)
            yaw_bits, pitch_bits, roll_bits = actor["authoredTransform"]["retailEulerFloatBits"]
            if pitch_bits != 0 or roll_bits != 0 or yaw_bits not in (0, 0x40490FDB, 0x40782696):
                raise RuntimeError("world 110 landing-craft Init Euler inputs changed")
            yaw = struct.unpack("<f", struct.pack("<i", yaw_bits))[0]
            c, s = _f32(math.cos(yaw)), _f32(math.sin(yaw))
            # Preserve the finite yaw-only specialization of the actual stores;
            # a mathematical identity matrix loses the negative-zero words.
            parent_basis = [c, -s, _f32(0.0 * c + 0.0 * s),
                            s, c, _f32(0.0 * s - 0.0 * c), -0.0, 0.0, 1.0]
            uses.append({
                "parentDefinitionIdentity": actor["definitionIdentity"],
                "componentDefinitionName": name,
                "parentInitBasisFloatBits": [_float_bits(value) for value in parent_basis],
            })
    if [use["parentDefinitionIdentity"] for use in uses] != [
            "wres:rlwd:0008", "wres:rlwd:0012", "wres:rlwd:0013", "wres:rlwd:0020"]:
        raise RuntimeError("world 110 landing-craft Component use order changed")
    return {
        "meshSourceSha256": WORLD110_LANDING_CRAFT_MESH_SHA256,
        "meshInflatedSha256": _sha256(inflated),
        "emitterTag": 20,
        "selector": 1,
        "partOrdinal": 31,
        "localPositionFloatBits": position,
        "localBasisFloatBits": basis,
        "uses": uses,
    }


def _world110_initial_mesh(source: bytes, index: int) -> dict[str, object]:
    """Original mesh metadata and complete CPOS/CORI word arrays.

    Cached positions use four words per stored pose; orientations use twelve.
    Retain stale fourth words and every cached pose rather than flattening an
    animated part. Collision geometry is bound by source hash, not exported.
    """
    from cmsh_static_preview import inflate_aya, parse_cmsh_stream
    mesh_name, _, source_hash, part_count = WORLD110_INITIAL_MESHES[index]
    if _sha256(source) != source_hash:
        raise RuntimeError(f"world 110 initial mesh identity changed: {mesh_name}")
    parsed = parse_cmsh_stream(inflate_aya(source))
    parts = parsed.file_parts()
    if len(parts) != part_count or struct.unpack_from("<I", parsed.raw_header, 0x14)[0] != (4 if index == 3 else 0):
        raise RuntimeError("initial mesh part/animation profile changed")
    boxes = [sibling.raw_payload for sibling in parsed.siblings if sibling.tag == b"BBOX"]
    if len(boxes) != 1 or len(boxes[0]) != 48 or boxes[0][:8] != b"BBOX\x28\0\0\0":
        raise RuntimeError("initial mesh global bounding box changed")
    animation_chunks = [sibling.raw_payload for sibling in parsed.siblings if sibling.tag == b"CAMD"]
    animation_count = struct.unpack_from("<I", parsed.raw_header, 0x14)[0]
    if len(animation_chunks) != int(animation_count > 0):
        raise RuntimeError("world 110 animation table count changed")
    animation_data = animation_chunks[0] if animation_chunks else b""
    if len(animation_data) != animation_count * 36:
        raise RuntimeError("world 110 animation table layout changed")
    animations = []
    for ordinal in range(animation_count):
        record = animation_data[ordinal * 36:(ordinal + 1) * 36]
        mode, start, end, delta, increment = struct.unpack_from("<5i", record, 16)
        animations.append({"sourceOrdinal": ordinal, "name": record[:16].split(b"\0", 1)[0].decode("ascii"),
                           "modeId": mode, "startFrame": start, "endFrame": end,
                           "frameDelta": delta, "incrementFloatBits": increment})
    frame_counts = {len(part.track.frame_map) for part in parts if part.track is not None}
    if len(frame_counts) != 1 or any(part.track is None for part in parts):
        raise RuntimeError("world 110 initial mesh frame profile changed")
    return {
        "frameCount": frame_counts.pop(),
        "animations": animations,
        "numberOfAnimations": struct.unpack_from("<I", parsed.raw_header, 0x14)[0],
        "meshName": mesh_name,
        "sourceSha256": source_hash,
        "meshRadiusFloatBits": struct.unpack_from("<i", parsed.raw_header, 0x164)[0],
        "globalBoundingBoxWords": list(struct.unpack_from("<10i", boxes[0], 8)),
        "parts": [{
            "cachedPositionWords": list(struct.unpack(f"<{len(part.track.cached_position_bytes) // 4}i",
                                                       part.track.cached_position_bytes)),
            "cachedOrientationWords": list(struct.unpack(f"<{len(part.track.cached_orientation_bytes) // 4}i",
                                                          part.track.cached_orientation_bytes)),
            "name": part.raw_cmsp[0xdc:0xfc].split(b"\0", 1)[0].decode("ascii"),
            "type": part.part_type,
            "reference": part.reference, "parent": part.parent,
            "children": part.children, "nmic": part.nmic,
            "numNmic": struct.unpack_from("<i", part.raw_cmsp, 0xa0)[0],
            "isNmic": struct.unpack_from("<i", part.raw_cmsp, 0xa4)[0],
            "halfExtentFloatBits": [_float_bits(value) for value in part.bounding_box.half_extents],
        } for part in parts],
        "emitters": [{"name": binding.name, "selector": binding.selector,
                      "partOrdinal": binding.part_ordinal}
                     for binding in parsed.emitter_bindings()],
    }


def _world110_initial_unit_uses(rows, physics) -> list[dict[str, object]]:
    """Ordered raw Unit field 7/18 tuples for the first four admitted Units."""
    result = []
    for index, row in enumerate(rows[:4]):
        actor = row["actor"]
        if (actor["definitionIdentity"] != f"wres:bswd:{index:04d}"
                or actor["meshBinding"].removesuffix(".msh") + ".msh" != WORLD110_INITIAL_MESHES[index][0]
                or row["serializedThingType"] != 8):
            raise RuntimeError("world 110 initial Unit profile changed")
        fields = _physics_record(physics, 1, actor["definitionName"])
        record = {"actorDefinitionIdentity": actor["definitionIdentity"]}
        for field_id, key in ((7, "weaponUses"), (18, "spawnerUses")):
            record[key] = [_unit_construction_use(raw) for raw in fields.values(field_id)]
        result.append(record)
    return result


def _world110_initial_unit_definitions(uses, physics_data: bytes, physics):
    """Bounded authored profiles and released type-2 constructor defaults.

    CWeaponStatement__Create 0x0042f5f0 supplies absent fields; 0x00434610
    resolves slot names in type-3 source order and 0x004349c0 applies ID 7.
    This admits Repair Pad and SAT Launcher, not a generic weapon parser.
    """
    stream = _physics_record_stream(physics_data)
    weapon_records = [record for record in stream if record.record_type == 2]
    mode_records = [record for record in stream if record.record_type == 3]
    weapon_names = list(dict.fromkeys(use["definitionName"]
        for record in uses for use in record["weaponUses"]))
    spawner_names = list(dict.fromkeys(use["definitionName"]
        for record in uses for use in record["spawnerUses"]))
    if weapon_names != ["Repair Pad", "SAT Launcher"] or spawner_names != ["Sabre Factory Spawner"]:
        raise RuntimeError("world 110 initial Unit definition admission changed")

    def raw_fields(record):
        return [{"fieldId": field_id, "rawHex": raw.hex()} for field_id, raw in record.fields]

    weapons = []
    for name in weapon_names:
        weapon = physics[(2, name)][0]
        if [field_id for field_id, _ in weapon.fields] != ([1, 7] if name == "Repair Pad" else [1]):
            raise RuntimeError("world 110 initial weapon field profile changed")
        fields = _physics_record(physics, 2, weapon.name)
        slots = [-1] * 5
        for raw in fields.values(1):
            if len(raw) < 6 or raw[-1] != 0 or b"\0" in raw[4:-1]:
                raise RuntimeError("world 110 weapon charge slot layout changed")
            slot = struct.unpack_from("<i", raw)[0]
            if not 0 <= slot < 5:
                raise RuntimeError("world 110 weapon charge slot index changed")
            name = raw[4:-1].decode("ascii")
            slots[slot] = next((index for index, mode in enumerate(mode_records)
                                if mode.name == name), -1)
            if slots[slot] == -1:
                raise RuntimeError("world 110 weapon charge mode is absent")
        if slots[0] < 0:
            raise RuntimeError("world 110 weapon initial mode is absent")
        mode = mode_records[slots[0]]
        adjust_aim = struct.unpack("<f", fields.get(7, struct.pack("<f", 1.0)))[0]
        weapons.append({
            "definitionName": weapon.name,
            "typeOrdinal": weapon_records.index(weapon),
            "chargeRateFloatBits": 0x40000000,
            "chargeSlots": slots,
            "consumptionFloatBits": 0x3f800000,
            "ammoStore": 0, "zoomMode": 0, "adjustAimWord": int(adjust_aim != 0),
            "fields": raw_fields(weapon),
            "selectedMode": {"definitionName": mode.name, "typeOrdinal": slots[0],
                             "fields": raw_fields(mode)},
        })
    spawners = []
    for name in spawner_names:
        fields = _physics_record(physics, 5, name)
        target = _definition_string(fields, 1)
        spawners.append({
            "definitionName": name, "targetUnitDefinitionName": target,
            "targetUnitBehaviourSelector": _unit_behavior_selector(_physics_record(physics, 1, target)),
            "fields": raw_fields(physics[(5, name)][0]),
        })
    return weapons, spawners


def _world110_ground_unit_definitions(physics):
    """SAT's absent movement overrides retain the 0x0042efd0 zero defaults."""
    record = physics[(1, "SAT Turret")][0]
    if [field_id for field_id, _ in record.fields] != [11, 9, 8, 47, 3, 23, 7, 21, 10]:
        raise RuntimeError("world 110 SAT ground Unit field profile changed")
    return [{
        "definitionName": record.name, "fieldC8FloatBits": 0, "fieldD0FloatBits": 0,
        "fields": [{"fieldId": field_id, "rawHex": raw.hex()} for field_id, raw in record.fields],
    }]


def _world110_feature_definitions(rows, physics):
    """The four authored iceberg profiles used by BSWD objects 4 through 9."""
    features = rows[4:10]
    if [row["actor"]["definitionName"] for row in features] != [
            "Iceberg 1", "Iceberg 2", "Iceberg 3", "Iceberg 4", "Iceberg 2", "Iceberg 4"]:
        raise RuntimeError("world 110 initial Feature order changed")
    definitions = []
    for index in range(4):
        name = f"Iceberg {index + 1}"
        fields = _physics_record(physics, 8, name)
        record = physics[(8, name)][0]
        mesh_name = _definition_string(fields, 2)
        if ([field_id for field_id, _ in record.fields] != [2, 4]
                or mesh_name != WORLD110_INITIAL_MESHES[index + 4][0]
                or fields[4] != struct.pack("<f", 1.0)):
            raise RuntimeError("world 110 iceberg Feature profile changed")
        definitions.append({
            "definitionName": name, "meshName": mesh_name,
            "invincibleWord": 1, "profileWord18": 0,
            "fields": [{"fieldId": field_id, "rawHex": raw.hex()} for field_id, raw in record.fields],
        })
    return definitions


def _world110_initial_actor_bytes(
    raw_world: bytes, physics_data: bytes, landing_craft_mesh: bytes,
    pine_meshes: tuple[bytes, ...], initial_meshes: tuple[bytes, ...],
) -> bytes:
    """Authored BSWD/type-8 RLWD actors, before unresolved class initialization.

    Reuses the existing Core coordinate datum and exact yaw-only conversion.
    This is a reconstruction frame, not World 110's player-start identity.
    No start, squad, spawner, path or script-object constructor is substituted.
    """
    if _sha256(physics_data) != PHYSICS_DEFINITIONS_SHA256:
        raise RuntimeError("world 110 physics source identity changed")
    bswd = _chunk_payload(
        _chunk_payload(_chunk_payload(raw_world, b"WRES"), b"WRLD"), b"BSWD"
    )
    if len(bswd) != 54_669 or _sha256(bswd) != (
        "04c5a3838548a2c50819f46dc1f1746f7c20ec4aa34678bd23c8bcd2186010f4"
    ):
        raise RuntimeError("world 110 shared base-world identity changed")
    base_objects, base_trees = _parse_static_world_inputs(
        raw_world, preserve_serialized_names=True
    )
    seeds = _parse_world110_initial_object_seeds(raw_world)
    rlwd = _chunk_payload(
        _chunk_payload(_chunk_payload(raw_world, b"WRES"), b"WRLD"), b"RLWD")
    level_reader = _WorldReader(rlwd)
    level_reader.position = WORLD110_TREE_GROUP_HEADER_OFFSET
    level_trees = _read_explicit_tree_groups(level_reader)
    _require_snow_tree_groups(level_trees)
    if base_trees.header_offset != 2709 or level_trees.end_offset != 45167:
        raise RuntimeError("world 110 explicit tree boundaries changed")
    tree_tables = []
    for chunk, payload, table in (("BSWD", bswd, base_trees), ("RLWD", rlwd, level_trees)):
        tree_tables.append({
            "sourceChunk": chunk,
            "sourceSha256": _sha256(payload),
            "headerOffset": table.header_offset,
            "endOffset": table.end_offset,
            "groups": [{
                "name": group.name,
                "headerOffset": group.header_offset,
                "recordsOffset": group.records_offset,
                "recordsSha256": group.records_sha256,
                # LoadWorld reads both tables. A non-base load with a base
                # present skips its trees; the base load also skips fern/bush
                # groups. These exact tables therefore create only BSWD pines.
                "callsTreeInit": chunk == "BSWD" and group.name == "pinesnow",
                "placements": group.placements,
            } for group in table.groups],
        })
    physics = _physics_records(physics_data)
    inputs = [(f"wres:bswd:{item['ordinal']:04d}", item, True)
              for item in base_objects]
    for seed in seeds:
        if seed.thing_type != 8:
            continue
        inputs.append((f"wres:rlwd:{seed.ordinal:04d}", {
            "definition": seed.tail.definition_name,
            "thingType": seed.thing_type,
            "name": seed.name,
            "script": seed.script,
            "active": seed.active != 0,
            "activeWord": seed.active,
            "allegiance": seed.allegiance,
            "target": seed.target,
            "meshNumber": seed.mesh_number,
            "spawnScript": seed.spawn_script,
            "attachScriptsToUnitsWord": seed.attach_scripts,
            "retailPosition": struct.unpack("<3f", struct.pack("<3I", *seed.position_bits)),
            "retailOrientation": struct.unpack("<3f", struct.pack("<3I", *seed.orientation_bits)),
        }, False))

    rows = []
    for identity, item, is_static in inputs:
        definition = item["definition"]
        is_unit = item["thingType"] == 8
        fields = _physics_record(physics, 1, definition) if is_unit else None
        life = struct.unpack("<f", fields[3])[0] if fields is not None else None
        if life is not None and (not math.isfinite(life) or life < 0):
            raise RuntimeError(f"world 110 Unit life is invalid: {definition}")
        rows.append({
            "serializedThingType": item["thingType"],
            "activeWord": item["activeWord"],
            "allegiance": item["allegiance"],
            "target": item["target"],
            "meshNumber": item["meshNumber"],
            "spawnScript": item["spawnScript"],
            "attachScriptsToUnitsWord": item["attachScriptsToUnitsWord"],
            "serializedBehaviourType": struct.unpack("<I", fields[8])[0] if fields else None,
            "internalBehaviourSelector": _unit_behavior_selector(fields) if fields else None,
            "lifeFloatBits": _float_bits(life) if life is not None else None,
            "actor": {
                "authoredOrder": len(rows),
                "definitionIdentity": identity,
                "definitionName": definition,
                "name": item["name"],
                "scriptName": item["script"] or None,
                "meshBinding": _definition_string(fields, 9) if fields else item["mesh"],
                "isStatic": is_static,
                "active": item["active"],
                # Existing Core health convention is thousandths of Unit life.
                # Type 35 has no Unit life field; zero is not an invulnerability claim.
                "initialHealth": _round_away_from_zero(life * 1000) if life is not None else 0,
                "authoredTransform": _authored_transform(item["retailPosition"], item["retailOrientation"]),
                "initialPose": _actor_pose(item["retailPosition"], item["retailOrientation"]),
            },
        })
    if len(rows) != 43:
        raise RuntimeError("world 110 direct initial actor count changed")
    if len(initial_meshes) != len(WORLD110_INITIAL_MESHES):
        raise RuntimeError("world 110 initial mesh count changed")
    unit_uses = _world110_initial_unit_uses(rows, physics)
    weapons, spawners = _world110_initial_unit_definitions(unit_uses, physics_data, physics)
    return (json.dumps({
        "schema": WORLD110_INITIAL_ACTORS_SCHEMA,
        "worldNumber": 110,
        "archiveSha256": WORLD110_ARCHIVE_SHA256,
        "baseWorldSha256": _sha256(bswd),
        "initialObjectSeedsSha256": WORLD110_INITIAL_OBJECT_SEEDS_SHA256,
        "physicsSourceSha256": PHYSICS_DEFINITIONS_SHA256,
        "rows": rows,
        "treeTables": tree_tables,
        "treeMeshes": _world110_tree_mesh_inputs(pine_meshes),
        "initialMeshes": [_world110_initial_mesh(source, index)
                           for index, source in enumerate(initial_meshes)],
        "initialUnitUses": unit_uses,
        "weaponDefinitions": weapons,
        "spawnerDefinitions": spawners,
        "featureDefinitions": _world110_feature_definitions(rows, physics),
        "groundUnitDefinitions": _world110_ground_unit_definitions(physics),
        "componentAttachment": _world110_component_attachment(rows, physics, landing_craft_mesh),
    }, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")


WORLD110_STATIC_WORLD_SCHEMA = "onslaught.world110-static-world.v2"

# World 110's level rows as the RE lane's construction contract classes them
# (reverse-engineering/game-mechanics/world-110-construction-order.md,
# "Level-world rows"). The script carriers and the waypoints have no body;
# every other row is a thing the level constructs.
WORLD110_SCRIPT_CARRIERS = {0: "LevelScript", 2: "Setup", 39: "Weather"}
WORLD110_LANDING_CRAFT = {
    8: ("Muspell Light Landing Craft", "Lander"),
    12: ("Muspell Light Landing Craft", "Lander2"),
    13: ("Muspell Light Landing Empty", "Lander3"),
    20: ("Muspell Light Landing Craft", "Lander"),
}
WORLD110_SQUADS = {
    14: ("Light Gun Tank", 5, ""),
    16: ("Light Gun Tank", 5, ""),
    17: ("AV-14B Sabre Pulse Tank", 3, ""),
    18: ("Light Gun Tank", 5, ""),
    19: ("AV-14B Sabre Pulse Tank", 4, "Scout"),
}
WORLD110_FIGHTERS = {
    25: "Muspell Fighter",
    34: "Muspell Light Fighter",
    35: "Muspell Light Fighter",
    36: "Muspell Light Fighter",
    37: "Muspell Light Fighter",
    38: "Muspell Light Fighter",
}
WORLD110_NAMED_PATHS = {
    "Fighter Path 2": (6, 7, 5),
    "Fighter Path 1": (25, 33, 32, 31, 30, 29, 28, 27, 26),
    "Transport Path 1": (4, 3),
    "Lander Path 1": (23, 22, 21, 15, 11, 10, 8, 24),
}


def _seed_float(bits: int) -> float:
    return struct.unpack("<f", struct.pack("<i", _signed_int32_bits(bits)))[0]


def _unit_mesh_binding(fields: _PhysicsFields) -> str:
    """Level 100's naming: "m_" + the record's mesh (field 9) lower-cased + ".aya"."""
    mesh = fields.get(9)
    if mesh is None or 0 not in mesh:
        raise RuntimeError("unit mesh field changed")
    return "m_" + mesh[: mesh.index(0)].decode("ascii").lower() + ".aya"


def _unit_life_millis(fields: _PhysicsFields) -> int:
    # Unit Init copies profile+c0 into life+f8 before Actor Init (004f8b29).
    life = fields.get(3)
    if life is None:
        raise RuntimeError("unit life field changed")
    return _round_away_from_zero(struct.unpack("<f", life)[0] * 1000)


def _parse_world110_paths_and_settings(
    raw_world: bytes, seeds: tuple[_WorldInitialObject, ...],
) -> tuple[list[dict[str, object]], dict[str, int]]:
    """The level world's named paths and its settings words, after its trees.

    The RLWD continues past the level world's tree tables with the
    navigation graph (a uint16 1, 187 four-float nodes and 306 enabled edges),
    then the named paths: a uint16 count and, per path, a string8 name, an
    int32 count and that many uint16 row ordinals. Only the CWaypoint rows
    (thing type 18) are path nodes; the loader drops the others
    (waypoint-paths.md, "Loading"), and each node keeps its row's target. The
    settings follow; words 3 and 4 are the pre-run and pan lengths
    (0x0050d2b8, 0x0050d2c5; world-110-construction-order.md, "Load order").
    """
    rlwd = _chunk_payload(_chunk_payload(_chunk_payload(raw_world, b"WRES"), b"WRLD"), b"RLWD")
    reader = _WorldReader(rlwd)
    reader.position = WORLD110_TREE_GROUP_HEADER_OFFSET
    _require_snow_tree_groups(_read_explicit_tree_groups(reader))
    if reader.uint16() != 1 or reader.int32() != 187:
        raise RuntimeError("world 110 navigation node table changed")
    nodes = [tuple(reader.single() for _ in range(4)) for _ in range(187)]
    if reader.int32() != 306:
        raise RuntimeError("world 110 navigation edge table changed")
    for _ in range(306):
        start, end, enabled = reader.int32(), reader.int32(), reader.int32()
        if start not in range(len(nodes)) or end not in range(len(nodes)) or enabled != 1:
            raise RuntimeError("world 110 navigation edge changed")
    by_ordinal = {item.ordinal: item for item in seeds}
    if reader.uint16() != len(WORLD110_NAMED_PATHS):
        raise RuntimeError("world 110 named path count changed")
    paths: list[dict[str, object]] = []
    for _ in WORLD110_NAMED_PATHS:
        name = reader.string8()
        count = reader.int32()
        indices = tuple(reader.uint16() for _ in range(count))
        if WORLD110_NAMED_PATHS.get(name) != indices:
            raise RuntimeError(f"world 110 named path {name!r} changed")
        points = []
        dropped = []
        for index in indices:
            row = by_ordinal[index]
            if row.thing_type != 18:
                dropped.append(index)
                continue
            if row.target != -1 and by_ordinal[row.target].thing_type != 18:
                raise RuntimeError(f"world 110 waypoint {index} targets a non-waypoint")
            position = [_seed_float(bits) for bits in row.position_bits]
            points.append({
                "nodeIndex": index,
                "positionMillimeters": [
                    _round_away_from_zero((position[0] - LEVEL100_PLAYER_START_X) * 1_000.0),
                    _round_away_from_zero(position[2] * 1_000.0),
                    _round_away_from_zero((position[1] - LEVEL100_PLAYER_START_Y) * 1_000.0),
                ],
                # As Level 100's manifest fills a marker's three components:
                # the fourth slot is the float bits of 0.0f.
                "retailComponentsFloatBits": [
                    *(_signed_int32_bits(bits) for bits in row.position_bits),
                    _float_bits(0.0),
                ],
                "targetNodeIndex": None if row.target == -1 else row.target,
            })
        paths.append({"droppedNodeIndices": dropped, "name": name, "points": points})
    words = [reader.int32() for _ in range(5)]
    if words[3] != 0 or words[4] != _float_bits(2.0):
        raise RuntimeError("world 110 pre-run or pan length changed")
    return paths, {"panLengthFloatBits": words[4], "preRunWordBits": words[3]}


def _world110_static_world_bytes(raw_world: bytes, physics_data: bytes, landing_craft_mesh: bytes) -> bytes:
    """World 110's level for the Simulation, in Level 100's manifest shape.

    The shared base world's 33 objects and 1,481 pines as Level 100's manifest
    reads them, with each building's life from its unit record; then the level
    rows in file order: the Start (as Player 1), the inactive spawner, the four
    landing craft each followed by its turret child, the volume, the five
    type-28 squads each followed by its members, and the six fighters. The squads, the turret
    children, the named paths, each unit type's motion and the settings words
    follow. Squad members start on their squad's point; placing them on their
    formation slots is the squad's own work at runtime (0x004e9600,
    0x004e8730). Names of rows the file leaves unnamed follow Level 100's
    manifest ("Level Actor NN"); member and turret-child names are labels.
    """
    if _sha256(physics_data) != PHYSICS_DEFINITIONS_SHA256:
        raise RuntimeError("world 110 physics source identity changed")
    if _sha256(landing_craft_mesh) != WORLD110_LANDING_CRAFT_MESH_SHA256:
        raise RuntimeError("world 110 landing-craft mesh identity changed")
    from cmsh_static_preview import inflate_aya
    landing_craft_radius_bits = _cmsh_radius_bits(inflate_aya(landing_craft_mesh))
    physics = _physics_records(physics_data)
    # The level world has no CSafeSide; the shared base world's are its list.
    safe_sides: list[dict[str, object]] = []
    base_objects, base_trees = _parse_static_world_inputs(raw_world, safe_sides=safe_sides)
    seeds = _parse_world110_initial_object_seeds(raw_world)
    paths, settings = _parse_world110_paths_and_settings(raw_world, seeds)

    actors: list[dict[str, object]] = []

    def add(identity, name, definition, script, mesh, static, active, health, allegiance,
            position, orientation, thing_type_mask=0):
        actors.append({
            "active": active,
            "allegiance": allegiance,
            "authoredOrder": len(actors),
            "authoredTransform": _authored_transform(position, orientation),
            "definitionIdentity": identity,
            "definitionName": definition,
            "initialHealth": health,
            "initialPose": _actor_pose(position, orientation),
            "isStatic": static,
            "meshBinding": mesh,
            "name": name,
            "scriptName": script or None,
            "targetGroup": "None",
            "targetOrdinal": 0,
            "thingTypeMask": thing_type_mask,
            "trigger": None,
        })

    for item in base_objects:
        definition = str(item["definition"])
        health = (_unit_life_millis(_physics_record(physics, 1, definition))
                  if int(item["thingType"]) == 8 else 0)
        add(f"wres:bswd:{int(item['ordinal']):04d}", str(item["name"]), definition,
            str(item["script"]), str(item["mesh"]), True, bool(item["active"]), health,
            int(item["allegiance"]), item["retailPosition"], item["retailOrientation"])

    turret = _physics_record(physics, 7, "Dropship Gun Turret")
    turret_mesh = turret.get(3)
    if turret_mesh != b"hiveturret.msh\0":
        raise RuntimeError("world 110 Dropship Gun Turret mesh changed")
    squads: list[dict[str, object]] = []
    components: list[dict[str, object]] = []
    motion_names: list[str] = []
    for seed in seeds:
        ordinal = seed.ordinal
        identity = f"wres:rlwd:{ordinal:04d}"
        position = [_seed_float(bits) for bits in seed.position_bits]
        orientation = [_seed_float(bits) for bits in seed.orientation_bits]
        active = seed.active != 0
        if ordinal in WORLD110_SCRIPT_CARRIERS:
            if seed.thing_type != 27 or seed.script != WORLD110_SCRIPT_CARRIERS[ordinal]:
                raise RuntimeError(f"world 110 script carrier {ordinal} changed")
        elif seed.thing_type == 18:
            continue
        elif seed.thing_type == 15:
            if ordinal != 1:
                raise RuntimeError("world 110 Start row changed")
            add(identity, "Player 1", "BattleEngine", "", "m_f_be1.msh.aya", False, True, 0,
                seed.allegiance, position, orientation, thing_type_mask=8)
        elif seed.thing_type == 19:
            tail = seed.tail
            if (ordinal != 5 or active or seed.name != "Fighter Second Wave"
                    or tail.spawn_unit != "Muspell Fighter" or seed.spawn_script != "MuspellFighter2"):
                raise RuntimeError("world 110 spawner row changed")
            add(identity, seed.name, "Level Actor Type 19", "", None, True, False, 0,
                seed.allegiance, position, orientation)
        elif ordinal in WORLD110_LANDING_CRAFT:
            definition, script = WORLD110_LANDING_CRAFT[ordinal]
            if seed.thing_type != 8 or seed.tail.definition_name != definition or seed.script != script:
                raise RuntimeError(f"world 110 landing craft {ordinal} changed")
            fields = _physics_record(physics, 1, definition)
            if struct.unpack("<i", fields[8])[0] != 12:
                raise RuntimeError(f"world 110 {definition} behaviour changed")
            add(identity, f"Level Actor {ordinal:02d}", definition, script, _unit_mesh_binding(fields),
                False, active, _unit_life_millis(fields), seed.allegiance, position, orientation)
            child = f"{identity}:turret"
            # Built inside the craft's construction, after its Actor draw
            # (world-110-construction-order.md, "Landing craft and their turrets").
            # The component record's life field is not established; 0 here.
            add(child, f"Level Actor {ordinal:02d} Turret", "Dropship Gun Turret", "",
                "m_hiveturret.msh.aya", False, active, 0, seed.allegiance, position, orientation)
            components.append({"childIdentity": child, "definitionName": "Dropship Gun Turret",
                               "parentIdentity": identity})
            motion_names.append(definition)
        elif seed.thing_type == 36:
            if ordinal != 9 or seed.script or _seed_float(seed.tail.radius_bits) != 50.0:
                raise RuntimeError("world 110 volume changed")
            add(identity, f"Level Actor {ordinal:02d}", "General Volume", "", None, True, active, 0,
                seed.allegiance, position, orientation)
        elif ordinal in WORLD110_SQUADS:
            definition, amount, script = WORLD110_SQUADS[ordinal]
            tail = seed.tail
            if (seed.thing_type != 28 or tail.definition_name != definition or tail.amount != amount
                    or tail.mode != 0 or seed.script != script):
                raise RuntimeError(f"world 110 squad row {ordinal} changed")
            fields = _physics_record(physics, 1, definition)
            if struct.unpack("<i", fields[8])[0] != 3:
                raise RuntimeError(f"world 110 {definition} behaviour changed")
            # The squad is itself a thing: CreateSquad builds it, its members
            # are built inside its Init, and it keeps its own script
            # (world-110-construction-order.md, "Type-28 squads").
            add(identity, f"Level Actor {ordinal:02d}", "Level Actor Type 28", script, None, False, active,
                0, seed.allegiance, position, orientation)
            members = []
            for member in range(amount):
                member_identity = f"{identity}:{member}"
                # CSquad::Init clears a member's script and name (0x004e6049-0x004e6076).
                add(member_identity, f"Level Actor {ordinal:02d} Member {member + 1}", definition, "",
                    _unit_mesh_binding(fields), False, active, _unit_life_millis(fields),
                    seed.allegiance, position, orientation)
                members.append(member_identity)
            squads.append({
                "active": active,
                "allegiance": seed.allegiance,
                "authoredTransform": _authored_transform(position, orientation),
                "definitionIdentity": identity,
                "definitionName": definition,
                "memberIdentities": members,
                "mode": tail.mode,
                "name": f"Level Actor {ordinal:02d}",
                "scriptName": script or None,
            })
            motion_names.append(definition)
        elif ordinal in WORLD110_FIGHTERS:
            definition = WORLD110_FIGHTERS[ordinal]
            if seed.thing_type != 8 or seed.tail.definition_name != definition or seed.script:
                raise RuntimeError(f"world 110 fighter {ordinal} changed")
            fields = _physics_record(physics, 1, definition)
            if struct.unpack("<i", fields[8])[0] != 9:
                raise RuntimeError(f"world 110 {definition} behaviour changed")
            add(identity, f"Level Actor {ordinal:02d}", definition, "", _unit_mesh_binding(fields),
                False, active, _unit_life_millis(fields), seed.allegiance, position, orientation)
            motion_names.append(definition)
        else:
            raise RuntimeError(f"world 110 row {ordinal} (type {seed.thing_type}) is not admitted")

    # The same class dispatch as Level 100's motion rows
    # (_level100_actor_motion_definitions): the ground fields come from the
    # unit record; the air classes keep them null.
    motion: list[dict[str, object]] = []
    for definition in dict.fromkeys(motion_names):
        fields = _physics_record(physics, 1, definition)
        behaviour = struct.unpack("<i", fields[8])[0]
        row: dict[str, object] = {
            "authoredOrder": len(motion),
            "behaviorInternalId": _unit_behavior_selector(fields),
            "behaviorSerializedType": behaviour,
            "coreGroundOriginOffsetMillimeters": None,
            "definitionName": definition,
            "fullGuideBaseTicks": None,
            "maximumSpeedFloatBits": None,
            "maximumTurnRadiansPerBaseTickFloatBits": None,
        }
        if behaviour == 3:
            row.update({"arrivalRadiusMillimeters": 2_000, "coreGroundOriginOffsetMillimeters": 100,
                        "fullGuideBaseTicks": 4, "maximumSpeedFloatBits": struct.unpack("<i", fields[1])[0],
                        "maximumTurnRadiansPerBaseTickFloatBits": struct.unpack("<i", fields[5])[0],
                        "motionClass": "GroundVehicle", "steamClassVtableAddress": 0x005E297C})
        elif behaviour in (9, 12):
            # id 2 CUnitAirVelocity (+0xb4) and id 6 CUnitAirTurnRate (+0xb8)
            # (factory jump table 0x00432908), CUnitBig and MinAltitude.
            row.update(_air_unit_motion_fields(
                fields, definition, landing_craft_radius_bits if behaviour == 12 else None))
            if behaviour == 9:
                row.update({"arrivalRadiusMillimeters": 5_000, "motionClass": "Plane",
                            "steamClassVtableAddress": 0x005E1930})
            else:
                row.update({"arrivalRadiusMillimeters": 8_000, "motionClass": "Dropship",
                            "steamClassVtableAddress": 0x005E1DD8})
        else:
            raise RuntimeError(f"world 110 {definition} behaviour {behaviour} is not admitted")
        motion.append(row)

    ferns, pines = base_trees.groups
    if (len(actors) != 33 + 1 + 1 + 4 * 2 + 1 + 5 + 22 + 6 or len(squads) != 5
            or len(components) != 4 or len(pines.placements) != 1_481):
        raise RuntimeError("world 110 static world counts changed")
    document = {
        "actorDefinitions": actors,
        "components": components,
        "motionDefinitions": motion,
        "physicsSourceSha256": PHYSICS_DEFINITIONS_SHA256,
        "pineInstanceCount": len(pines.placements),
        "pines": [[*struct.unpack("<2f", struct.pack("<2i", x, y)), variant]
                  for x, y, variant in pines.placements],
        "safeSides": safe_sides,
        "schema": WORLD110_STATIC_WORLD_SCHEMA,
        "settings": settings,
        "sourceArchiveSha256": WORLD110_ARCHIVE_SHA256,
        "squads": squads,
        "suppressedFernCount": len(ferns.placements),
        "waypointPaths": paths,
        "worldNumber": 110,
    }
    return (json.dumps(document, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")


def _build_actor_definition_set(
    objects: list[dict[str, object]],
    level_actors: list[dict[str, object]],
    emitters_by_mesh: dict[str, dict[str, dict[str, object]]],
    physics: dict[tuple[int, str], tuple[_PhysicsRecord, ...]],
    airfield_exit_waypoints: dict[str, list[dict[str, object]]],
) -> tuple[list[dict[str, object]], list[dict[str, object]]]:
    actor_definitions: list[dict[str, object]] = []
    trainer_life = _physics_record(physics, 1, "Air Trainer").get(3)
    if trainer_life != struct.pack("<I", 0x40400000):
        raise RuntimeError("Level 100 Air Trainer life field changed")
    # Unit Init copies profile+c0 into life+f8 before Actor Init (004f8b29).
    trainer_health = _round_away_from_zero(struct.unpack("<f", trainer_life)[0] * 1000)

    def add_actor(
        *,
        identity: str,
        name: str,
        definition_name: str | None,
        script_name: str | None,
        mesh_binding: str | None,
        is_static: bool,
        active: bool,
        health: int,
        authored_transform: dict[str, list[int]],
        pose: dict[str, list[int]],
        thing_type_mask: int = 0,
        target_group: str = "None",
        target_ordinal: int = 0,
        trigger: str | None = None,
    ) -> None:
        actor_definitions.append(
            {
                "active": active,
                "authoredOrder": len(actor_definitions),
                "authoredTransform": authored_transform,
                "definitionIdentity": identity,
                "definitionName": definition_name,
                "initialHealth": health,
                "initialPose": pose,
                "isStatic": is_static,
                "meshBinding": mesh_binding,
                "name": name,
                "scriptName": script_name,
                "thingTypeMask": thing_type_mask,
                "targetGroup": target_group,
                "targetOrdinal": target_ordinal,
                "trigger": trigger,
            }
        )

    static_by_name: dict[str, dict[str, object]] = {}
    for item in objects:
        name = str(item["name"])
        if name in LEVEL100_SETUP_SCRIPT_BINDINGS and name in static_by_name:
            raise RuntimeError(f"Level 100 has duplicate mission actor name {name}")
        static_by_name[name] = item
        add_actor(
            identity=f"wres:bswd:{int(item['ordinal']):04d}",
            name=name,
            definition_name=str(item["definition"]),
            # The released Setup program assigns these scripts at runtime.
            script_name=str(item["script"]) or None,
            mesh_binding=str(item["mesh"]),
            is_static=True,
            active=bool(item["active"]),
            health=0,
            authored_transform=_authored_transform(
                item["retailPosition"], item["retailOrientation"]
            ),
            pose=_actor_pose(item["retailPosition"], item["retailOrientation"]),
        )

    trigger_by_name = {
        "Target Zone 1": "TargetZone1",
        "Firing Range": "FiringRange",
        "Target Zone 2": "TargetZone2",
        "Target Zone 3": "TargetZone3",
        "Target Zone 4": "TargetZone4",
    }
    target_by_record = {
        9: ("Target Tank 2", "m_f_pulsetank_training.msh.aya", "StaticTargets", 2, 6_000),
        11: ("Target Warehouse", "m_m_warehouse.msh.aya", "StaticTargets", 4, 50_000),
        12: ("Target Tank 3", "m_f_pulsetank_training.msh.aya", "StaticTargets", 3, 6_000),
        # The authored actor is the distinct Flyby instance. Airfield later spawns
        # the AirTrainer mission actor through SpawnerB.
        40: ("Air Trainer", "m_FA_F24_training.msh.aya", "None", 0, trainer_health),
    }
    physical_actor_ordinals = {
        0, 9, 11, 12, 13, 14, 15, 16, 19, 21, 40,
    }
    for item in level_actors:
        ordinal = int(item["ordinal"])
        if ordinal not in physical_actor_ordinals:
            continue
        serialized_name = str(item["name"])
        if ordinal == 0:
            name = "Player 1"
            definition_name = "BattleEngine"
            mesh_binding = "m_f_be1.msh.aya"
        elif ordinal in target_by_record:
            name, mesh_binding, _, _, _ = target_by_record[ordinal]
            definition_name = str(item["definition"])
        elif serialized_name:
            name = serialized_name
            definition_name = str(item["definition"]) or {
                36: "General Volume",
            }.get(int(item["thingType"]), f"Level Actor Type {item['thingType']}")
            mesh_binding = None
        else:
            name = {21: "Transporter"}.get(ordinal, f"Level Actor {ordinal:02d}")
            definition_name = str(item["definition"]) or {
                15: "BattleEngine",
            }.get(int(item["thingType"]), f"Level Actor Type {item['thingType']}")
            mesh_binding = None

        if ordinal == 21:
            mesh_binding = "m_f_lifter.msh.aya"

        target_group = "None"
        target_ordinal = 0
        health = 0
        if ordinal in target_by_record:
            _, _, target_group, target_ordinal, health = target_by_record[ordinal]
        trigger = trigger_by_name.get(serialized_name)
        if trigger is not None:
            if item["thingType"] != 36 or item["radius"] != 5.0:
                raise RuntimeError(f"Level 100 trigger {serialized_name} changed")

        add_actor(
            identity=f"wres:rlwd:{ordinal:04d}",
            name=name,
            definition_name=definition_name,
            script_name=str(item["script"]) or None,
            mesh_binding=mesh_binding,
            is_static=int(item["thingType"]) not in (8, 15),
            active=bool(item["active"]),
            health=health,
            authored_transform=_authored_transform(
                item["retailPosition"], item["retailOrientation"]
            ),
            pose=_actor_pose(item["retailPosition"], item["retailOrientation"]),
            thing_type_mask=8 if ordinal == 0 else 0,
            target_group=target_group,
            target_ordinal=target_ordinal,
            trigger=trigger,
        )

    if len(actor_definitions) != 44:
        raise RuntimeError(
            f"Level 100 actor definition set has {len(actor_definitions)} actors instead of 44"
        )

    owner_identity = {
        "Tank Factory": "wres:bswd:0001",
        "Airfield": "wres:bswd:0023",
    }
    spawn_rows = (
        ("Tank Factory", "Target Tank", "SpawnerA", "TargetTank1", "m_f_pulsetank_training.msh.aya", "StaticTargets", 1, 4),
        ("Tank Factory", "Target Truck", "SpawnerA", "TargetTruck1", "m_f_truck_training.msh.aya", "TargetTrucks", 1, 3),
        ("Tank Factory", "Target Truck", "SpawnerA", "TargetTruck2", "m_f_truck_training.msh.aya", "TargetTrucks", 2, 3),
        ("Tank Factory", "Target Truck", "SpawnerA", "TargetTruck3", "m_f_truck_training.msh.aya", "TargetTrucks", 3, 3),
        ("Tank Factory", "Target Tank", "SpawnerA", "TargetTank2", "m_f_pulsetank_training.msh.aya", "MovingTargets", 0, 6),
        ("Tank Factory", "Target Truck", "SpawnerA", "TargetTank2", "m_f_truck_training.msh.aya", "MovingTargets", 0, 6),
        ("Airfield", "Air Trainer", "SpawnerB", "AirTrainer", "m_FA_F24_training.msh.aya", "AirTrainer", 1, 1),
        ("Airfield", "Target Drone", "SpawnerB", "AirborneDrone1", "m_FA_F24_training.msh.aya", "AirborneTargets1", 0, 3),
        ("Airfield", "Target Drone", "SpawnerA", "AirborneDrone2", "m_FA_F24_training.msh.aya", "AirborneTargets2", 0, 6),
        ("Airfield", "Target Drone", "SpawnerB", "AirborneDrone2", "m_FA_F24_training.msh.aya", "AirborneTargets2", 0, 6),
    )
    spawn_definitions: list[dict[str, object]] = []
    for (
        owner_name,
        definition_name,
        spawner_name,
        script_name,
        mesh_binding,
        target_group,
        fixed_ordinal,
        maximum_group_actors,
    ) in spawn_rows:
        owner = static_by_name[owner_name]
        emitter = emitters_by_mesh[str(owner["mesh"])][spawner_name]
        local_position = emitter["position"]
        local_basis = emitter["basis"]
        spawn_definitions.append(
            {
                "active": True,
                "authoredEmitterTransform": {
                    "localBasisFloatBits": [
                        _float_bits(component)
                        for row in local_basis
                        for component in row
                    ],
                    "localPositionFloatBits": [
                        _float_bits(component) for component in local_position
                    ],
                },
                "authoredOrder": len(spawn_definitions),
                "definitionIdentity": (
                    f"spawn:{owner_name}:{definition_name}:{spawner_name}:{script_name}"
                ),
                "definitionName": definition_name,
                "fixedTargetOrdinal": fixed_ordinal,
                "initialHealth": (
                    6_000
                    if definition_name == "Target Tank"
                    else 3_000
                    if definition_name == "Target Truck"
                    else trainer_health
                    if definition_name == "Air Trainer"
                    else 0
                ),
                "initialPose": _spawn_pose(owner, emitter),
                "maximumGroupActors": maximum_group_actors,
                "meshBinding": mesh_binding,
                "ownerDefinitionIdentity": owner_identity[owner_name],
                "scriptName": script_name,
                "spawnerName": spawner_name,
                **({"spawnerExitWaypoints": airfield_exit_waypoints[spawner_name]}
                   if owner_name == "Airfield" else {}),
                "targetGroup": target_group,
                "thingTypeMask": 0,
            }
        )
    return actor_definitions, spawn_definitions


def _round_scaled_away(value: float, scale: int) -> int:
    scaled = value * scale
    return math.floor(scaled + 0.5) if scaled >= 0 else math.ceil(scaled - 0.5)


def _contact_part_float_geometry(part) -> dict[str, object]:
    """Original file records, not the resolved preview or selected runtime pose.

    BBOX words 3/7 (vector padding) and word 8 are retained opaque.
    Position/orientation arrays retain their opaque fourth words as well.
    """
    from cmsh_static_preview import _orientation_bytes, _position_bytes

    def words(data: bytes) -> list[int]:
        return list(struct.unpack(f"<{len(data) // 4}I", data))

    box = part.bounding_box
    track = part.track
    return {
        "sourceId": struct.unpack_from("<I", part.raw_cmsp, 0x88)[0],
        "sourceType": part.part_type,
        "reference": part.reference,
        "parent": part.parent,
        "children": list(part.children),
        "nmic": part.nmic,
        "numNmicWord": struct.unpack_from("<I", part.raw_cmsp, 0xa0)[0],
        "isNmicWord": struct.unpack_from("<I", part.raw_cmsp, 0xa4)[0],
        "boundingBoxWords": words(struct.pack("<3fI3fIIf", *box.center,
            box.pad_words[0], *box.half_extents, box.pad_words[1], box.valid, box.radius)),
        "cmspTransformWords": words(part.raw_cmsp[:0x80]),
        "cmsp118Word": struct.unpack_from("<I", part.raw_cmsp, 0x118)[0],
        "positionCacheInheritanceWord": struct.unpack_from("<I", part.raw_cmsp, 0x11c)[0],
        "orientationCacheInheritanceWord": struct.unpack_from("<I", part.raw_cmsp, 0x120)[0],
        "frameMap": None if track is None else list(track.frame_map),
        "hierarchyPositionWords": None if track is None else
            [words(_position_bytes(pose)) for pose in track.hierarchy],
        "hierarchyOrientationWords": None if track is None else
            [words(_orientation_bytes(pose)) for pose in track.hierarchy],
        "cachedPositionWords": None if track is None or not track.cached_position_bytes
            else words(track.cached_position_bytes),
        "cachedOrientationWords": None if track is None or not track.cached_orientation_bytes
            else words(track.cached_orientation_bytes),
    }


def _contact_part_records(parsed) -> list[dict[str, object]]:
    records: list[dict[str, object]] = []
    for index, part in enumerate(parsed.parts):
        rows = part.transform.rows
        local = part.bounding_box.center
        if (
            part.bounding_box.valid not in {0, 1}
            or not all(
                math.isfinite(value)
                for value in (
                    *local,
                    *part.bounding_box.half_extents,
                    part.bounding_box.radius,
                )
            )
            or any(value < 0 for value in part.bounding_box.half_extents)
            or part.bounding_box.radius < 0
        ):
            raise RuntimeError(f"mesh part {part.name} has an invalid collision BBOX")
        center = [
            sum(rows[axis][component] * local[component] for component in range(3))
            + part.transform.position[axis]
            for axis in range(3)
        ]
        vertices: list[int] = []
        for vertex in part.vertices:
            position = vertex.position
            transformed = [
                sum(rows[axis][component] * position[component] for component in range(3))
                + part.transform.position[axis]
                for axis in range(3)
            ]
            if not all(math.isfinite(value) for value in transformed):
                raise RuntimeError(
                    f"mesh part {part.name} has a non-finite contact vertex"
                )
            vertices.extend(
                _round_scaled_away(value, 1_000) for value in transformed
            )

        triangles: list[int] = []
        for group in part.groups:
            indices = group.indices
            for ordinal in range(len(indices) - 2):
                if ordinal % 2 == 0:
                    a, b, c = indices[ordinal : ordinal + 3]
                else:
                    b, a, c = indices[ordinal : ordinal + 3]
                if len({a, b, c}) == 3:
                    triangles.extend((a, b, c))

        half_extents = [
            math.ceil(value * 1_000.0)
            for value in part.bounding_box.half_extents
        ]
        segment_value = max(part.bounding_box.half_extents)
        records.append(
            {
                "floatGeometry": _contact_part_float_geometry(parsed.file_parts()[index]),
                "centerMillimeters": [
                    _round_scaled_away(value, 1_000) for value in center
                ],
                "collidable": (
                    part.bounding_box.valid == 1
                    and all(value > 0 for value in half_extents)
                    and bool(part.vertices)
                ),
                "halfExtentsMillimeters": half_extents,
                "index": index,
                "name": part.name,
                "orientationPartsPerMillion": [
                    _round_scaled_away(value, 1_000_000)
                    for row in rows
                    for value in row
                ],
                "parent": -1 if part.parent is None else part.parent,
                "reference": -1 if part.reference is None else part.reference,
                "segmentValueBits": struct.unpack(
                    "<I", struct.pack("<f", segment_value)
                )[0],
                "triangles": triangles,
                "type": part.part_type,
                "verticesMillimeters": vertices,
            }
        )
    return records


def _level100_definition_float_geometry(parsed, serialized_behavior: int) -> dict[str, object]:
    """Untransformed CMSH inputs, not rounded world-instance sphere values."""
    boxes = [sibling.raw_payload for sibling in parsed.siblings if sibling.tag == b"BBOX"]
    if len(boxes) != 1 or len(boxes[0]) != 48 or boxes[0][:8] != b"BBOX\x28\0\0\0":
        raise RuntimeError("Level 100 target global bounding box changed")
    words = struct.unpack_from("<10I", boxes[0], 8)
    radius = struct.unpack_from("<I", parsed.raw_header, 0x164)[0]
    if serialized_behavior not in (3, 8, 9):
        raise RuntimeError("unadmitted Level 100 primary radius class")
    values = [struct.unpack("<f", struct.pack("<I", word))[0]
              for word in (*words[:3], words[9], radius)]
    if not all(math.isfinite(value) for value in values) or min(values[3:]) <= 0:
        raise RuntimeError("invalid Level 100 target float geometry")
    return {
        "boundingBoxOriginFloatBits": list(words[:3]),
        "boundingBoxRadiusFloatBits": words[9],
        "meshRenderRadiusFloatBits": radius,
        # Pristine47c8e0 (GroundVehicle slot35): float store, then multiply
        # by float word3f4ccccd at5d85f8. Plane/Building use the base sphere.
        "primaryRadiusScaleFloatBits": 0x3f4ccccd if serialized_behavior == 3 else 0x3f800000,
    }


def _level100_contact_asset(
    objects: list[dict[str, object]],
    mesh_inputs: dict[str, tuple[Path, bytes, dict]],
    parse_cmsh_stream,
    inflate_aya,
    game_root: Path,
) -> bytes:
    contact_mesh_keys = set(STATIC_MESH_BY_DEFINITION.values())
    parsed_static = {
        mesh_key: parse_cmsh_stream(inflate_aya(source[1]))
        for mesh_key, source in mesh_inputs.items()
        if mesh_key in contact_mesh_keys
    }
    instances: list[dict[str, object]] = []
    for world_object in sorted(objects, key=lambda item: int(item["ordinal"])):
        position = world_object["retailPosition"]
        yaw_pitch_roll = world_object["retailOrientation"]
        instances.append(
            {
                # Kept, unlike the removed clearance below, because it is a
                # true record of the released authored elevation in Core's own
                # up-positive player-plane datum. It is the same conversion
                # `_actor_pose` uses, so the two cannot drift apart.
                "authoredElevationMillimeters": _core_elevation_millimeters(
                    float(position[2])
                ),
                # `baseClearanceMillimeters` (a `-min(vertexZ)` lift, plus a
                # hand-written `SAT Turret` exception) used to live here. It is
                # removed rather than zeroed because it has no retail
                # counterpart at all: `CThing::Init` @0x004F34A0 writes the
                # height-field sample straight into the pivot
                # (`FSTP [ESP+0x14]` @0x004F3529, then `CALL [EAX+0x50]`) and
                # then clamps the pivot to the water level
                # (`FSTP [ESI+0x24]` @0x004F3559). No mesh extent is read on
                # that path. The renderer stopped applying the lift for world
                # objects (`Level100StaticWorldAsset.Load` seats them at
                # `max(PlayerStartElevation - retailZ, terrain, water)`), so
                # keeping the field here made collision and render elevation
                # disagree by each mesh's own clearance - 3.2318 on FB_Docks.
                # No consumer reads it.
                "definition": world_object["definition"],
                "id": world_object["ordinal"],
                "name": world_object["name"],
                "positionMillimeters": [
                    _round_scaled_away(float(position[0]) - 288.6875, 1_000),
                    _round_scaled_away(float(position[1]) - 243.25, 1_000),
                ],
                "retailPositionBits": [
                    struct.unpack("<I", struct.pack("<f", float(value)))[0]
                    for value in position
                ],
                "retailYawPitchRollBits": [
                    struct.unpack("<I", struct.pack("<f", float(value)))[0]
                    for value in yaw_pitch_roll
                ],
                "rootMode": "authored-terrain-water",
            }
        )

    definitions = [
        {
            "definition": definition,
            "mesh": mesh_key,
            "parts": _contact_part_records(parsed_static[mesh_key]),
        }
        for definition, mesh_key in STATIC_MESH_BY_DEFINITION.items()
    ]

    physics = _physics_records(
        _read_exact(game_root / PHYSICS_DEFINITIONS, PHYSICS_DEFINITIONS_SHA256)
    )
    direct_hashes = {
        Path(source_name).name.casefold(): expected_hash
        for _, source_name, expected_hash in DIRECT_ASSETS
    }
    # The RETAINED file name is authoritative for the `mesh` field emitted
    # below, because `default physics.dat` and the archive disagree on case:
    # record #660 spells `fa_f24_training.msh` while the shipped file is
    # `m_FA_F24_training.msh.aya`. Resolving through this map keeps
    # level100-contact-owners.json byte-identical now that the drone mesh is an
    # ordinary retention rather than the removed in-place special case.
    direct_names = {
        Path(source_name).name.casefold(): Path(source_name).name
        for _, source_name, _ in DIRECT_ASSETS
    }
    target_definitions: list[dict[str, object]] = []
    target_source_hashes: dict[str, str] = {}
    # Every Level 100 definition a released script can destroy needs contact
    # geometry here, or its actors are unhittable by any weapon at any range.
    #
    # `Target Truck` is the same released destruction class as `Target Tank`:
    # `default physics.dat` (sha256 e1fb3ded...ada14) carries
    # `Unit / Target Truck` at record offset 0x24d9e (name string 0x24da6) with
    # exactly the tank's field set {1,3,5,8,9,10,11,23,46,48} and exactly its
    # values apart from two - field 3 `maximumLife` 3.0 (0x40400000) against the
    # tank's 6.0 (0x40C00000), and field 9 mesh `f_truck_training.msh` against
    # `f_pulsetank_training.msh`. Field 8 (behaviour class 3, GroundVehicle) and
    # field 10 (`Tank Explosion Medium`) are identical, so the truck reuses the
    # tank's whole-body life model and its destruction record verbatim.
    #
    # `Target Drone` is here as of 2026-07-26. It is NOT the truck's case: its
    # `Unit` record @0x24e76 carries behaviour class 9 (`CFighterBehaviourType`
    # by RTTI, the class whose vtable is 0x005e1930), field 3 `CUnitLife` 1.0
    # (0x3F800000), and field 10 destruction record `Drone Explosion` @0x5df9
    # whose field 6 `CExplosionWaterEffect` is `Water Explosion Small` while
    # fields 2/5/7 are all `Drone Explosion Effect`. That divergence used to
    # trip the single-descriptor guard below; the schema now carries the water
    # variant separately instead of collapsing it, so the guard can stay strict
    # on the three variants that genuinely agree.
    for definition in ("Target Tank", "Target Truck", "Warehouse", "Target Drone"):
        fields = _physics_record(physics, 1, definition)
        mesh_name = _definition_string(fields, 9)
        if not mesh_name.casefold().endswith(".msh"):
            mesh_name += ".msh"
        source_name = f"m_{mesh_name}.aya"
        expected_hash = direct_hashes.get(source_name.casefold())
        if expected_hash is not None:
            source_name = direct_names[source_name.casefold()]
        if expected_hash is None:
            raise RuntimeError(
                f"Level 100 target definition has no retained mesh: {definition}"
            )
        source = _read_exact(
            game_root / "data/resources/meshes" / source_name,
            expected_hash,
        )
        parsed = parse_cmsh_stream(inflate_aya(source))
        destruction_fields = _physics_record(
            physics,
            6,
            _definition_string(fields, 10),
        )
        particle_descriptor = _definition_string(destruction_fields, 2)
        if any(
            _definition_string(destruction_fields, field_id) != particle_descriptor
            for field_id in (5, 7)
        ):
            raise RuntimeError(
                f"Level 100 target destruction variants diverged: {definition}"
            )
        target_definitions.append(
            {
                "definition": definition,
                "destructionParticleDescriptor": particle_descriptor,
                "destructionWaterParticleDescriptor": _definition_string(
                    destruction_fields, 6
                ),
                "destructionPhysicsDefinition": _definition_string(fields, 10),
                "destructionSoundDescriptor": _definition_string(
                    destruction_fields, 10
                ),
                "maximumLifeBits": struct.unpack("<I", fields[3])[0],
                "mesh": source_name,
                "parts": _contact_part_records(parsed),
                "floatGeometry": _level100_definition_float_geometry(
                    parsed, struct.unpack("<i", fields[8])[0]),
            }
        )
        target_source_hashes[definition] = expected_hash

    round_fields = _physics_record(physics, 4, "Mech Pulse Bolt Medium")
    hit_fields = _physics_record(
        physics,
        6,
        _definition_string(round_fields, 9),
    )
    hit_particle_descriptor = _definition_string(hit_fields, 2)
    if (
        round_fields[12] != struct.pack("<I", 0x3D8F5C29)
        or any(
            _definition_string(hit_fields, field_id) != hit_particle_descriptor
            for field_id in (5, 6, 7)
        )
    ):
        raise RuntimeError("Level 100 medium pulse contact contract changed")

    vulcan_round_names = ("Mech Bullet", "Mech Air Bullet")
    vulcan_hit_name = "Mech Bullet Hit"
    vulcan_hit_fields = _physics_record(physics, 6, vulcan_hit_name)
    if (
        any(
            _definition_string(_physics_record(physics, 4, name), 9) !=
            vulcan_hit_name
            for name in vulcan_round_names
        )
        or _definition_string(vulcan_hit_fields, 5) !=
            "Bullet Ground Hit Effect"
        or _definition_string(vulcan_hit_fields, 7) !=
            "Mech Bullet Hit Unit Effect"
        or 10 in vulcan_hit_fields
    ):
        raise RuntimeError("Level 100 Vulcan impact contract changed")

    document = {
        "definitionCount": len(definitions),
        "definitions": definitions,
        "instanceCount": len(instances),
        "instances": instances,
        "partCount": (
            sum(len(row["parts"]) for row in definitions)
            + sum(len(row["parts"]) for row in target_definitions)
        ),
        "pulseRound": {
            "impactParticleDescriptor": hit_particle_descriptor,
            "impactPhysicsDefinition": _definition_string(round_fields, 9),
            "impactSoundDescriptor": _definition_string(hit_fields, 10),
            "radiusMillimeters": _round_scaled_away(
                struct.unpack("<f", round_fields[12])[0], 1_000
            ),
        },
        "schema": "onslaught.level100-contact-owners.v7",
        "staticMeshCount": len(parsed_static),
        "staticSourceAggregateSha256": (
            LEVEL100_CONTACT_SOURCE_AGGREGATE_SHA256
        ),
        "targetDefinitionCount": len(target_definitions),
        "targetDefinitions": target_definitions,
        "targetSourceSha256": target_source_hashes,
    }
    return (
        json.dumps(document, sort_keys=True, separators=(",", ":")) + "\n"
    ).encode("utf-8")


def _materialize_static_world(
    game_root: Path,
    raw_level: bytes,
    stage: Path,
) -> tuple[tuple[Path, str], ...]:
    tools_root = ROOT / "tools"
    rebuild_tools = ROOT / "rebuild/tools"
    sys.path.insert(0, str(tools_root))
    sys.path.insert(0, str(rebuild_tools))
    from aya_archive_inventory import build_asset_resolver, inflate_aya_bytes
    from cmsh_static_preview import (
        build_rigid_transform_tracks,
        convert_aya_bytes,
        inflate_aya,
        parse_cmsh_stream,
    )

    objects, pines, fern_count = _parse_static_world(raw_level)
    level_actors, waypoint_paths = _parse_level_world_actors_and_waypoints(raw_level)
    safe_sides = _level100_safe_sides(raw_level, level_actors)
    texture_blend_flags = _texture_blend_alpha_flags(raw_level)
    resolver = build_asset_resolver(game_root / "data/resources")
    source_data: dict[Path, bytes] = {}
    texture_blend_by_source: dict[Path, bool] = {}
    mesh_inputs: dict[
        str,
        tuple[
            Path,
            bytes,
            dict[str, tuple[tuple[Path, float, float, float, float, float] | None, ...]],
        ],
    ] = {}
    emitters_by_mesh: dict[str, dict[str, dict[str, object]]] = {}

    for mesh_key in STATIC_MESH_KEYS:
        matches = resolver.mesh_index.get(f"{mesh_key}.msh".lower(), [])
        if len(matches) != 1:
            raise RuntimeError(f"expected one exact loose mesh for {mesh_key}, found {len(matches)}")
        source_path = Path(matches[0])
        relative = source_path.relative_to(game_root)
        if mesh_key in PINE_MESH_KEYS:
            data = _read_exact(
                source_path,
                PINE_MESH_SHA256[PINE_MESH_KEYS.index(mesh_key)],
            )
        else:
            data = source_path.read_bytes()
        inflated = inflate_aya(data)
        parsed = parse_cmsh_stream(inflated)
        if mesh_key in ("fb_tank_factory", "fb_aircraft_factory"):
            emitters_by_mesh[mesh_key] = _mesh_emitters(parsed)
        signatures = sorted(
            {group.raw_texr_u32 for part in parsed.parts for group in part.groups}
        )
        referenced_indices = sorted(
            {
                texture_index
                for signature in signatures
                for texture_index in signature
                if texture_index != 0xFFFFFFFF
            }
        )
        texture_source_by_index: dict[int, Path] = {}
        for texture_index in referenced_indices:
            if texture_index >= len(parsed.textures):
                raise RuntimeError(f"{mesh_key} has an unresolved material texture")
            texture_ref = parsed.textures[texture_index].name
            normalized_ref = texture_ref.lstrip("?").replace("/", "\\").strip().lower()
            texture_matches = resolver.texture_index.get(normalized_ref, [])
            if len(texture_matches) != 1:
                raise RuntimeError(
                    f"expected one exact texture for {mesh_key} {texture_ref}, found {len(texture_matches)}"
                )
            texture_path = Path(texture_matches[0])
            texture_relative = texture_path.relative_to(game_root)
            texture_data = texture_path.read_bytes()
            source_data.setdefault(texture_relative, texture_data)
            if source_data[texture_relative] != texture_data:
                raise RuntimeError("static-world texture changed during materialization")
            if normalized_ref not in texture_blend_flags:
                raise RuntimeError(
                    f"Level 100 has no CTexture mode for {texture_ref}"
                )
            blend_flag = texture_blend_flags[normalized_ref]
            previous_blend = texture_blend_by_source.setdefault(
                texture_relative,
                blend_flag,
            )
            if previous_blend != blend_flag:
                raise RuntimeError(
                    f"Level 100 CTexture mode disagrees for {texture_ref}"
                )
            texture_source_by_index[texture_index] = texture_relative

        materials: dict[
            str,
            tuple[tuple[Path, float, float, float, float, float] | None, ...],
        ] = {}
        for signature in signatures:
            if signature[0] == 0xFFFFFFFF:
                raise RuntimeError(f"{mesh_key} has no base material texture")
            layers: list[tuple[Path, float, float, float, float, float] | None] = []
            for texture_index in signature:
                if texture_index == 0xFFFFFFFF:
                    layers.append(None)
                    continue
                metadata = _texture_pass_metadata(
                    parsed.textures[texture_index].raw_texb_metadata
                )
                layers.append((texture_source_by_index[texture_index], *metadata))
            materials[_material_surface_name(signature)] = tuple(layers)
        source_data[relative] = data
        mesh_inputs[mesh_key] = (relative, data, materials)

    if (
        set(emitters_by_mesh) != {"fb_tank_factory", "fb_aircraft_factory"}
        or set(emitters_by_mesh["fb_tank_factory"]) != {"SpawnerA", "SpawnerB"}
        or set(emitters_by_mesh["fb_aircraft_factory"]) != {"SpawnerA", "SpawnerB"}
    ):
        raise RuntimeError("Level 100 authored spawner emitters changed")
    physics = _physics_records(
        _read_exact(
            game_root / PHYSICS_DEFINITIONS,
            PHYSICS_DEFINITIONS_SHA256,
        )
    )
    actor_definitions, spawn_definitions = _build_actor_definition_set(
        objects,
        level_actors,
        emitters_by_mesh,
        physics,
        _airfield_exit_waypoints(parse_cmsh_stream(inflate_aya(mesh_inputs["fb_aircraft_factory"][1]))),
    )
    _, aircraft_source, aircraft_hash = next(
        item for item in DIRECT_ASSETS
        if item[1] == "data/resources/meshes/m_FA_F24_training.msh.aya")
    aircraft_mesh = parse_cmsh_stream(inflate_aya(
        _read_exact(game_root / aircraft_source, aircraft_hash)))
    _, transporter_source, transporter_hash = next(
        item for item in DIRECT_ASSETS
        if item[1] == "data/resources/meshes/m_f_lifter.msh.aya")
    motion_definitions = _level100_actor_motion_definitions(
        physics, aircraft_mesh,
        _cmsh_radius_bits(inflate_aya(_read_exact(game_root / transporter_source, transporter_hash))))

    pine_views = _pine_imposter_views(raw_level)
    pine_centers = [
        _pine_global_center(mesh_inputs[mesh_key][1], inflate_aya, variant)
        for variant, mesh_key in enumerate(PINE_MESH_KEYS)
    ]
    pine_atlas_source = Path(PINE_IMPOSTER_TEXTURE[0])
    pine_atlas_data = _read_exact(
        game_root / pine_atlas_source,
        PINE_IMPOSTER_TEXTURE[1],
    )
    source_data[pine_atlas_source] = pine_atlas_data

    water_sources: dict[str, Path] = {}
    for key, source_name, expected_hash in WATER_TEXTURES:
        source = Path(source_name)
        source_data[source] = _read_exact(game_root / source, expected_hash)
        water_sources[key] = source
    if len(source_data) != 62:
        raise RuntimeError(f"static-world source set has {len(source_data)} files instead of 62")
    aggregate = _source_aggregate(source_data)
    if aggregate != STATIC_WORLD_SOURCE_AGGREGATE_SHA256:
        raise RuntimeError(f"unsupported static-world source set (aggregate SHA-256 {aggregate})")

    texture_records: dict[str, dict[str, object]] = {}
    texture_key_by_source: dict[Path, str] = {}
    outputs: list[tuple[Path, str]] = []
    texture_sources = {
        layer[0]
        for _, _, materials in mesh_inputs.values()
        for layers in materials.values()
        for layer in layers
        if layer is not None
    }
    texture_sources.update(water_sources.values())
    for source in sorted(texture_sources, key=lambda value: value.as_posix().casefold()):
        water_key = next(
            (key for key, value in water_sources.items() if value == source),
            None,
        )
        key = water_key or _texture_key(source.name.split("(0)", 1)[0])
        if key in texture_records:
            raise RuntimeError(f"static-world texture key is not unique: {key}")
        data = source_data[source]
        width, height, compression = _dds_metadata(data, inflate_aya_bytes)
        destination = STATIC_WORLD_ROOT / "Textures" / f"{key}.texture.aya"
        resource_path = f"res://Assets/Level100/StaticWorld/Textures/{key}.texture.aya"
        target = stage / destination
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        output_hash = _sha256(data)
        outputs.append((destination, output_hash))
        texture_key_by_source[source] = key
        texture_records[key] = {
            "blendTextureAlpha": texture_blend_by_source.get(source, False),
            "compression": compression,
            "height": height,
            "resourcePath": resource_path,
            "sha256": output_hash,
            "sourcePath": source.as_posix(),
            "width": width,
        }

    pine_atlas_key = "pine-imposters-100"
    pine_atlas_destination = (
        STATIC_WORLD_ROOT / "Textures/pine-imposters-100.texture.aya"
    )
    pine_atlas_resource_path = (
        "res://Assets/Level100/StaticWorld/Textures/"
        "pine-imposters-100.texture.aya"
    )
    pine_atlas_target = stage / pine_atlas_destination
    pine_atlas_target.parent.mkdir(parents=True, exist_ok=True)
    pine_atlas_target.write_bytes(pine_atlas_data)
    outputs.append((pine_atlas_destination, PINE_IMPOSTER_TEXTURE[1]))
    texture_records[pine_atlas_key] = {
        "blendTextureAlpha": False,
        "compression": "Dxt2",
        "height": 256,
        "resourcePath": pine_atlas_resource_path,
        "sha256": PINE_IMPOSTER_TEXTURE[1],
        "sourcePath": pine_atlas_source.as_posix(),
        "width": 1024,
    }

    mesh_records: dict[str, dict[str, object]] = {}
    animation_records: dict[str, dict[str, object]] = {}
    for mesh_key in STATIC_MESH_KEYS:
        source, data, materials = mesh_inputs[mesh_key]
        obj = convert_aya_bytes(
            data,
            include_vertex_attributes=True,
            include_material_layer_groups=True,
            include_vertex_colors=True,
        )
        vertex_z = [
            float(line.split()[3])
            for line in obj.decode("utf-8").splitlines()
            if line.startswith("v ")
        ]
        if not vertex_z or not all(math.isfinite(value) for value in vertex_z):
            raise RuntimeError(f"static-world mesh {mesh_key} has invalid converted bounds")
        slug = _slug(mesh_key)
        destination = STATIC_WORLD_ROOT / "Meshes" / f"{slug}.obj"
        resource_path = f"res://Assets/Level100/StaticWorld/Meshes/{slug}.obj"
        target = stage / destination
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(obj)
        output_hash = _sha256(obj)
        outputs.append((destination, output_hash))

        # Released per-part rigid transform tracks (HORI/HPOS selected through
        # VHFM). The flattened OBJ above is the rest pose; these are the exact
        # per-virtual-frame deltas that carry it, with no resampling.
        tracks = build_rigid_transform_tracks(parse_cmsh_stream(inflate_aya(data)))
        emitted_vertices = sum(
            1 for line in obj.decode("utf-8").splitlines() if line.startswith("v ")
        )
        covered = sum(int(part["objVertexCount"]) for part in tracks["parts"])
        if covered != emitted_vertices:
            raise RuntimeError(
                f"static-world mesh {mesh_key} track vertex ranges do not cover the emitted OBJ"
            )
        if any("frames" in part for part in tracks["parts"]):
            animation_records[mesh_key] = {
                "cachedOrientationDelta": tracks["cachedOrientationDelta"],
                "cachedPositionDelta": tracks["cachedPositionDelta"],
                "frameMaps": tracks["frameMaps"],
                "hierarchyFrameCount": tracks["hierarchyFrameCount"],
                "objVertexCount": emitted_vertices,
                "parts": tracks["parts"],
                "resourcePath": resource_path,
                "sha256": output_hash,
                "virtualFrameCount": tracks["virtualFrameCount"],
            }
        mesh_records[mesh_key] = {
            "baseClearance": -min(vertex_z),
            "materials": {
                surface: {
                    "layers": [
                        None
                        if layer is None
                        else {
                            "offset": [layer[2], layer[3]],
                            "opacity": layer[1],
                            "scale": [layer[4], layer[5]],
                            "texture": texture_key_by_source[layer[0]],
                        }
                        for layer in layers
                    ]
                }
                for surface, layers in sorted(materials.items())
            },
            "resourcePath": resource_path,
            "sha256": output_hash,
            "sourcePath": source.as_posix(),
        }

    hfld = _extract_chunk(
        raw_level,
        b"HFLD",
        668660,
        "7a4c7c5b9400e2c8d2325cecb5c44701cd8a6e6f8609cbc8bc31d449c0620f5d",
    )
    water_level_bits = struct.unpack_from("<I", hfld, 16 + 0x1034)[0]
    water_texture_index = hfld[16 + 0x1095]
    if water_level_bits != 0xC10D70A4 or water_texture_index != 0:
        raise RuntimeError("Level 100 water selection does not match the released HFLD")

    water_surface = _extract_chunk(
        raw_level,
        b"SURF",
        18_572,
        WATER_SURFACE_SHA256,
    )
    water_surface_target = stage / WATER_SURFACE_RESOURCE
    water_surface_target.parent.mkdir(parents=True, exist_ok=True)
    water_surface_target.write_bytes(water_surface)
    outputs.append((WATER_SURFACE_RESOURCE, WATER_SURFACE_SHA256))

    manifest = {
        "actorDefinitionProvenance": {
            "baseWorld": "100_res_PC.aya WRES/WRLD/BSWD v50 records 0..34",
            "compiledScripts": "100_res_PC.aya WRES/WRLD/RLWD ordered Level 100 object code",
            "levelWorld": "100_res_PC.aya WRES/WRLD/RLWD v50 initial actors 0..44",
            "motionDefinitions": "exact default physics.dat Unit fields plus canonical Steam class/radius dispatch",
            "weaponMounts": {
                "mesh": aircraft_source,
                "meshSha256": aircraft_hash,
                "inputs": "ordered Unit field7 uses; GunA/B selector1 meaningful CPOS/CORI model words; live cache/interpolation remains runtime state",
            },
            "spawnerTransforms": "exact CEMT named emitter part transforms from the released Tank Factory and Airfield meshes",
            "startupSpawn": "TankFactory compiled initializer plus exact SpawnerA CEMT transform; no settled target pose is pre-seeded",
        },
        "actorDefinitions": actor_definitions,
        "motionDefinitions": motion_definitions,
        "meshes": mesh_records,
        "objects": objects,
        "pineBillboards": {
            "fastStandingViewPhase": PINE_FAST_STANDING_VIEW_PHASE,
            "meshQualityDistance": PINE_MESH_QUALITY_DISTANCE,
            "texture": pine_atlas_key,
            "variants": [
                {
                    "centerOffset": pine_centers[variant],
                    "views": pine_views[variant],
                }
                for variant in range(4)
            ],
        },
        "pineInstanceCount": len(pines),
        "pines": pines,
        "safeSides": safe_sides,
        "schema": "onslaught.level100-static-world.v15",
        "sourceAggregateSha256": aggregate,
        "sourceArchiveSha256": LEVEL_ARCHIVE_SHA256,
        "physicsSourceSha256": PHYSICS_DEFINITIONS_SHA256,
        "suppressedFernCount": fern_count,
        "textures": texture_records,
        "unitRecordCount": 35,
        "visibleObjectCount": len(objects),
        "spawnDefinitions": spawn_definitions,
        "waypointPaths": waypoint_paths,
        "water": {
            "causticTexture": "water-caustic-00",
            "level": struct.unpack("<f", struct.pack("<I", water_level_bits))[0],
            "reflectionTexture": "water-reflection-00",
            "sunBlobTexture": "water-sun-blob",
            "sunReflectionTexture": "water-sun-reflection",
            "surfaceResourcePath": (
                "res://Assets/Level100/StaticWorld/Source/"
                "level100-water-surface.surf.bin"
            ),
            "surfaceSha256": WATER_SURFACE_SHA256,
            "textureIndex": water_texture_index,
            "wavesTexture": "water-waves",
        },
    }
    manifest_bytes = (
        json.dumps(manifest, sort_keys=True, separators=(",", ":")) + "\n"
    ).encode("utf-8")
    manifest_hash = _sha256(manifest_bytes)
    if manifest_hash != STATIC_WORLD_MANIFEST_SHA256:
        raise RuntimeError(
            f"static-world manifest did not reproduce exactly (SHA-256 {manifest_hash})"
        )
    manifest_target = stage / STATIC_WORLD_MANIFEST
    manifest_target.parent.mkdir(parents=True, exist_ok=True)
    manifest_target.write_bytes(manifest_bytes)
    outputs.append((STATIC_WORLD_MANIFEST, manifest_hash))

    if sorted(animation_records) != sorted(STATIC_WORLD_ANIMATED_MESHES):
        raise RuntimeError(
            "static-world animated mesh set changed: "
            f"{sorted(animation_records)}"
        )
    for mesh_key, expected_frames in STATIC_WORLD_ANIMATED_MESHES.items():
        if animation_records[mesh_key]["virtualFrameCount"] != expected_frames:
            raise RuntimeError(
                f"static-world mesh {mesh_key} virtual frame count changed: "
                f"{animation_records[mesh_key]['virtualFrameCount']}"
            )
    animation = {
        "framesPerSecond": STATIC_WORLD_ANIMATION_HZ,
        "meshes": animation_records,
        "provenance": {
            "cache": (
                "CPOS/CORI are the released model-space composition cache, one record "
                "per virtual frame; every composed frame here was checked against them "
                "and they carry no independent data"
            ),
            "rate": (
                "the released 20 Hz base update drives hierarchy playback; the mesh "
                "format stores no rate"
            ),
            "space": (
                "delta transforms apply directly to the emitted OBJ rest vertices, in "
                "OBJ space (released model space with Z negated)"
            ),
            "tracks": (
                "CMSH MESP HORI (hFrames x 48) and HPOS (hFrames x 16) selected per "
                "virtual frame by VHFM, composed along PRNT root-to-leaf"
            ),
        },
        "schema": "onslaught.level100-static-world-animation.v1",
        "sourceAggregateSha256": aggregate,
    }
    animation_bytes = (
        json.dumps(animation, sort_keys=True, separators=(",", ":")) + "\n"
    ).encode("utf-8")
    animation_hash = _sha256(animation_bytes)
    if animation_hash != STATIC_WORLD_ANIMATION_SHA256:
        raise RuntimeError(
            "static-world animation manifest did not reproduce exactly "
            f"(SHA-256 {animation_hash})"
        )
    animation_target = stage / STATIC_WORLD_ANIMATION
    animation_target.parent.mkdir(parents=True, exist_ok=True)
    animation_target.write_bytes(animation_bytes)
    outputs.append((STATIC_WORLD_ANIMATION, animation_hash))

    contact_asset = _level100_contact_asset(
        objects,
        mesh_inputs,
        parse_cmsh_stream,
        inflate_aya,
        game_root,
    )
    contact_hash = _sha256(contact_asset)
    if contact_hash != LEVEL100_CONTACT_ASSET_SHA256:
        raise RuntimeError(
            "Level 100 contact owners did not reproduce exactly "
            f"(SHA-256 {contact_hash})"
        )
    contact_target = stage / LEVEL100_CONTACT_ASSET
    contact_target.parent.mkdir(parents=True, exist_ok=True)
    contact_target.write_bytes(contact_asset)
    outputs.append((LEVEL100_CONTACT_ASSET, contact_hash))
    return tuple(outputs)


def _xap_records(data: bytes) -> list[tuple[str, int, bytes]]:
    if len(data) < 8 or data[:4] != b"PCXP":
        raise RuntimeError("unsupported English XAP framing")
    count = struct.unpack_from("<I", data, 4)[0]
    if count != 164:
        raise RuntimeError(f"unsupported English XAP record count: {count}")
    records: list[tuple[str, int, bytes]] = []
    offset = 8
    for index in range(count):
        if offset + 72 > len(data) or data[offset : offset + 4] != b"PSMP":
            raise RuntimeError(f"unsupported PSMP framing at record {index}")
        name = data[offset + 4 : offset + 68].split(b"\0", 1)[0].decode("ascii")
        decoded_size = struct.unpack_from("<I", data, offset + 68)[0]
        packed_size = (decoded_size + 3) // 4
        end = offset + 72 + packed_size
        if end > len(data):
            raise RuntimeError(f"truncated PSMP record {index}")
        records.append((name, decoded_size, data[offset + 72 : end]))
        offset = end
    if offset != len(data):
        raise RuntimeError("unexpected trailing English XAP data")
    return records


def _decode_ima_high_nibble_first(packed: bytes, decoded_size: int) -> bytes:
    predictor = 0
    step_index = 0
    output = bytearray()
    for value in packed:
        for nibble in (value >> 4, value & 0x0F):
            step = IMA_STEP_TABLE[step_index]
            delta = step >> 3
            if nibble & 1:
                delta += step >> 2
            if nibble & 2:
                delta += step >> 1
            if nibble & 4:
                delta += step
            predictor += -delta if nibble & 8 else delta
            predictor = max(-32768, min(32767, predictor))
            step_index = max(0, min(88, step_index + IMA_INDEX_TABLE[nibble & 7]))
            output.extend(struct.pack("<h", predictor))
    if len(output) < decoded_size:
        raise RuntimeError("decoded PSMP output is shorter than declared")
    return bytes(output[:decoded_size])


def _pcm_wav(pcm: bytes) -> bytes:
    return (
        b"RIFF"
        + struct.pack("<I", 36 + len(pcm))
        + b"WAVEfmt "
        + struct.pack("<IHHIIHH", 16, 1, 1, 44100, 88200, 2, 16)
        + b"data"
        + struct.pack("<I", len(pcm))
        + pcm
    )


def _find_ffmpeg() -> Path:
    which = shutil.which("ffmpeg")
    if which:
        return Path(which)
    local_app_data = os.environ.get("LOCALAPPDATA")
    if local_app_data:
        packages = Path(local_app_data) / "Microsoft" / "WinGet" / "Packages"
        if packages.is_dir():
            matches = sorted(packages.glob("**/ffmpeg.exe"))
            if matches:
                return matches[0]
    raise RuntimeError(
        "ffmpeg is required to decode FEBack128.vid into the frontend underlay strip"
    )


def _materialize_feback_strip(game_root: Path, stage: Path) -> None:
    """Decode CFEPMain's FEBack128.vid into the exact rgb24 underlay strip."""
    _read_exact(game_root / FEBACK_SOURCE, FEBACK_SOURCE_SHA256)
    ffmpeg = _find_ffmpeg()
    target = stage / FEBACK_STRIP
    target.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="feback-") as temporary:
        temporary_strip = Path(temporary) / FEBACK_STRIP.name
        completed = subprocess.run(
            [
                str(ffmpeg),
                "-y",
                "-hide_banner",
                "-loglevel",
                "error",
                "-i",
                str(game_root / FEBACK_SOURCE),
                "-vf",
                "format=rgb24",
                "-f",
                "rawvideo",
                str(temporary_strip),
            ],
            check=False,
            capture_output=True,
            text=True,
        )
        if completed.returncode != 0:
            raise RuntimeError(
                "ffmpeg failed to decode FEBack128.vid: "
                + (completed.stderr or completed.stdout or "no output")
            )
        data = temporary_strip.read_bytes()
    if len(data) != FEBACK_STRIP_BYTES:
        raise RuntimeError(
            f"FEBack strip length {len(data)} != expected {FEBACK_STRIP_BYTES}"
        )
    actual = _sha256(data)
    if actual != FEBACK_STRIP_SHA256:
        raise RuntimeError(
            "FEBack strip did not reproduce exactly "
            f"(SHA-256 {actual})"
        )
    target.write_bytes(data)


def _frontend_localization_bytes(data: bytes) -> bytes:
    if (
        len(data) < 16
        or struct.unpack_from("<I", data, 0)[0] != 0xFFFFFFBB
        or struct.unpack_from("<I", data, 4)[0] != 3
    ):
        raise RuntimeError("unsupported English language-table framing")

    count = struct.unpack_from("<I", data, 8)[0]
    if count != 2571:
        raise RuntimeError(f"unsupported English language-table count: {count}")

    entries_offset = 12
    text_pool_offset = entries_offset + count * 12 + 4
    if text_pool_offset > len(data):
        raise RuntimeError("truncated English language-table entries")

    offsets: dict[int, int] = {}
    wanted_ids = set(FRONTEND_TEXT_IDS.values())
    for index in range(count):
        entry_offset = entries_offset + index * 12
        text_id, text_offset_words = struct.unpack_from("<II", data, entry_offset)
        if text_id in wanted_ids:
            if text_id in offsets:
                raise RuntimeError(f"duplicate frontend text ID 0x{text_id:08X}")
            offsets[text_id] = text_offset_words

    if set(offsets) != wanted_ids:
        missing = wanted_ids - set(offsets)
        raise RuntimeError(
            "English language table is missing frontend text IDs: "
            + ", ".join(f"0x{text_id:08X}" for text_id in sorted(missing))
        )

    strings: dict[str, str] = {}
    for key, text_id in FRONTEND_TEXT_IDS.items():
        position = text_pool_offset + offsets[text_id] * 2
        code_units: list[int] = []
        for _ in range(8192):
            if position + 2 > len(data):
                raise RuntimeError(f"truncated frontend text ID 0x{text_id:08X}")
            code_unit = struct.unpack_from("<H", data, position)[0]
            position += 2
            if code_unit == 0:
                break
            code_units.append(code_unit)
        else:
            raise RuntimeError(f"unterminated frontend text ID 0x{text_id:08X}")
        strings[key] = struct.pack(f"<{len(code_units)}H", *code_units).decode("utf-16le")

    document = {
        "culture": "en",
        "schema": "onslaught.frontend-strings.v1",
        "sourceSha256": ENGLISH_LANGUAGE_TABLE_SHA256,
        "strings": strings,
    }
    output = (
        json.dumps(document, sort_keys=True, separators=(",", ":")) + "\n"
    ).encode("utf-8")
    actual = _sha256(output)
    if actual != FRONTEND_LOCALIZATION_SHA256:
        raise RuntimeError(
            "frontend localization did not reproduce exactly "
            f"(SHA-256 {actual})"
        )
    return output


def _frontend_world_strings_bytes(data: bytes) -> bytes:
    """Decode the per-world selector names and briefing slots.

    The language table's text IDs are a hash-scrambled space (no derivation
    is pinned here and none is invented), but the TEXT POOL preserves the
    authoring order, and that order carries two regular structures:

    1. The level-name rows: N.NN-titled strings ("1.00 - Training Level",
       "1.10 - Blackout", …) whose NN parts are exactly the career worlds of
       RetailWorldCatalog. They are matched by title pattern, not by ID.
    2. Briefing pages in career order: consecutive pool slots in groups of
       nine — two body paragraphs followed by seven reserved empties —
       beginning at world 100 ("Tatiana will take you through …" +
       "Listen to her advice …", byte-identical to the transcription already
       drawn on FEP_BRIEFING). Group k belongs to the k-th node of
       s_levelStructure that owns a briefing video under data/video/
       briefings/ (PC_<world>_exact.vid). Worlds without authored copy keep
       empty slots; the decoder emits those as empty strings rather than
       inventing text.

    Everything is emitted with the same exact-reproduction discipline as the
    menu table: this function's whole document is hash-pinned by
    FRONTEND_WORLD_STRINGS_SHA256.
    """
    if (
        len(data) < 16
        or struct.unpack_from("<I", data, 0)[0] != 0xFFFFFFBB
        or struct.unpack_from("<I", data, 4)[0] != 3
    ):
        raise RuntimeError("unsupported English language-table framing")

    count = struct.unpack_from("<I", data, 8)[0]
    entries_offset = 12
    text_pool_offset = entries_offset + count * 12 + 4

    def read_string(text_offset_words: int) -> str:
        position = text_pool_offset + text_offset_words * 2
        code_units: list[int] = []
        for _ in range(8192):
            if position + 2 > len(data):
                raise RuntimeError("truncated English language-table text pool")
            code_unit = struct.unpack_from("<H", data, position)[0]
            position += 2
            if code_unit == 0:
                break
            code_units.append(code_unit)
        return struct.pack(f"<{len(code_units)}H", *code_units).decode("utf-16le")

    pool_entries: list[tuple[int, str]] = []
    for index in range(count):
        entry_offset = entries_offset + index * 12
        text_id, text_offset_words = struct.unpack_from("<II", data, entry_offset)
        pool_entries.append((text_offset_words, read_string(text_offset_words)))
    pool_entries.sort(key=lambda entry: entry[0])

    # Level-name rows, keyed by their world number.
    name_pattern = re.compile(r"^(\d{1,2})\.(\d{2}) - .+$")
    level_names: dict[str, str] = {}
    for _, text in pool_entries:
        match = name_pattern.match(text)
        if match is None:
            continue
        world_number = int(match.group(1)) * 100 + int(match.group(2))
        key = f"{world_number}"
        if key in level_names:
            raise RuntimeError(f"duplicate level-name row for world {world_number}")
        level_names[key] = text

    missing_catalog_names = [
        world
        for world in CAREER_WORLD_ORDER
        if f"{world}" not in level_names
    ]
    if missing_catalog_names:
        raise RuntimeError(
            "English table lacks level-name rows for career worlds: "
            + ", ".join(str(world) for world in missing_catalog_names)
        )

    # Briefing groups: walk the pool from world 100's Tatiana paragraph,
    # located by its exact released text. Each group is nine slots; the
    # first two carry the page.
    tatiana_index = next(
        (
            position
            for position, (_, text) in enumerate(pool_entries)
            if text.startswith("Tatiana will take you through the basics")
        ),
        None,
    )
    if tatiana_index is None:
        raise RuntimeError("English table lost the world-100 briefing marker")

    briefings: dict[str, list[str]] = {}
    cursor = tatiana_index
    for group_index, world in enumerate(CAREER_WORLD_ORDER):
        if cursor + 9 > len(pool_entries):
            raise RuntimeError(
                f"English table ends before briefing group {group_index} "
                f"(world {world})"
            )
        group = pool_entries[cursor : cursor + 9]
        primary = group[0][1]
        secondary = group[1][1]
        tertiary = group[2][1]
        reserved = [text for _, text in group[3:]]
        if group_index == 0 and not primary.startswith("Tatiana will"):
            raise RuntimeError("world-100 briefing slot moved")
        # MEASURED 2026-08-22: exactly four worlds carry a third paragraph,
        # the two Tara-rescue epilogue variants of episode six; every other
        # group's third slot and every group's slots four through nine are
        # empty. Emitting the third slot only where retail authored one.
        if tertiary and world not in (611, 612, 621, 622):
            raise RuntimeError(
                f"briefing group {group_index} (world {world}) has an "
                "unexpected third paragraph"
            )
        if not tertiary and world in (611, 612, 621, 622):
            raise RuntimeError(
                f"briefing group {group_index} (world {world}) lost its "
                "measured third paragraph"
            )
        briefings[f"{world}"] = (
            [primary, secondary] if not tertiary
            else [primary, secondary, tertiary]
        )
        if any(reserved):
            raise RuntimeError(
                f"briefing group {group_index} (world {world}) has non-empty "
                "reserved slots; the nine-slot law does not hold"
            )
        cursor += 9

    known_pair = briefings["100"]
    if known_pair[1] != "Listen to her advice and try to keep Colonel Kramer happy.":
        raise RuntimeError(
            "the world-100 second briefing slot is no longer the Kramer line"
        )

    document = {
        "culture": "en",
        "schema": "onslaught.frontend-world-strings.v1",
        "sourceSha256": ENGLISH_LANGUAGE_TABLE_SHA256,
        "levelNames": level_names,
        "briefings": briefings,
    }
    output = (
        json.dumps(document, sort_keys=True, separators=(",", ":")) + "\n"
    ).encode("utf-8")
    actual = _sha256(output)
    if actual != FRONTEND_WORLD_STRINGS_SHA256:
        raise RuntimeError(
            "frontend world strings did not reproduce exactly "
            f"(SHA-256 {actual})"
        )
    return output


def _require_chunk(source: bytes, offset: int, tag: bytes, payload_size: int) -> int:
    if (
        offset < 0
        or offset + 8 > len(source)
        or source[offset : offset + 4] != tag
        or struct.unpack_from("<I", source, offset + 4)[0] != payload_size
        or offset + 8 + payload_size > len(source)
    ):
        raise RuntimeError(f"Level 100 has invalid {tag.decode('ascii')} framing")
    return offset + 8


def _parse_map_texture(
    raw_level: bytes,
    width: int,
    framed_size: int,
    expected_hash: str,
) -> tuple[bytes, tuple[int, ...]]:
    source = _extract_chunk(raw_level, b"MAPT", framed_size, expected_hash)
    _require_chunk(source, 0, b"MAPT", len(source) - 8)
    cmtx = _require_chunk(source, 8, b"CMTX", 76)
    if (
        struct.unpack_from("<I", source, cmtx)[0] != 0xDEAD
        or struct.unpack_from("<I", source, cmtx + 8)[0] != 0xDEAD
        or struct.unpack_from("<iii", source, cmtx + 0x10)
        != (width * width, 6, width)
    ):
        raise RuntimeError(f"Level 100 {width}x{width} MAPT metadata changed")

    data_header = cmtx + 76
    data_size = width * width * 6
    data_offset = _require_chunk(source, data_header, b"DATA", data_size)
    palette_header = data_offset + data_size
    palette_offset = _require_chunk(source, palette_header, b"PALT", 6_144)
    if palette_offset + 6_144 != len(source):
        raise RuntimeError("Level 100 root MAPT has trailing data")
    return source[data_offset : data_offset + data_size], struct.unpack_from(
        "<1536I", source, palette_offset
    )


def _parse_root_map_texture(raw_level: bytes) -> tuple[bytes, tuple[int, ...]]:
    _, width, framed_size, expected_hash = LANDSCAPE_MAP_TEXTURES[0]
    return _parse_map_texture(raw_level, width, framed_size, expected_hash)


def _parse_landscape_map_textures(
    raw_level: bytes,
) -> tuple[tuple[int, bytes, tuple[int, ...]], ...]:
    result: list[tuple[int, bytes, tuple[int, ...]]] = []
    for logical_level, width, framed_size, expected_hash in LANDSCAPE_MAP_TEXTURES:
        data, palettes = _parse_map_texture(
            raw_level,
            width,
            framed_size,
            expected_hash,
        )
        if logical_level != int(math.log2(width // 16)):
            raise RuntimeError("landscape MAPT logical-level ordering changed")
        result.append((width, data, palettes))
    return tuple(result)


def _parse_root_mixer_map(
    raw_level: bytes,
) -> tuple[list[tuple[tuple[int, ...], bytes]], bytes]:
    source = _extract_chunk(
        raw_level,
        b"MMAP",
        877_597,
        "45045d248e27366080614c1ad26fc9e711bc9656f4f79210eac63d2a20938361",
    )
    _require_chunk(source, 0, b"MMAP", len(source) - 8)
    cells: list[tuple[tuple[int, ...], bytes]] = []
    position = 8
    for cell_index in range(64 * 64):
        if position + 8 > len(source) or source[position : position + 4] != b"MCEL":
            raise RuntimeError(f"Level 100 MMAP cell {cell_index} is not framed")
        cell_size = struct.unpack_from("<I", source, position + 4)[0]
        cell_payload = _require_chunk(
            source,
            position,
            b"MCEL",
            cell_size,
        )
        record = _require_chunk(source, cell_payload, b"CMCL", 20)
        layer_count = struct.unpack_from("<i", source, record)[0]
        if (
            layer_count not in range(1, 6)
            or struct.unpack_from("<I", source, record + 4)[0] != 0xDEAD
        ):
            raise RuntimeError(f"Level 100 MMAP cell {cell_index} is invalid")
        material_ids = struct.unpack_from(f"<{layer_count}H", source, record + 8)
        if any(material_id >= 6 for material_id in material_ids):
            raise RuntimeError(f"Level 100 MMAP cell {cell_index} selects an invalid material")
        weights_header = record + 20
        weights_size = layer_count * 81
        weights = _require_chunk(source, weights_header, b"MXRS", weights_size)
        if cell_size != 36 + weights_size:
            raise RuntimeError(f"Level 100 MMAP cell {cell_index} has an invalid envelope")
        cells.append((material_ids, source[weights : weights + weights_size]))
        position += 8 + cell_size

    shade_offset = _require_chunk(source, position, b"MSHD", 512 * 512)
    if shade_offset + (512 * 512) != len(source):
        raise RuntimeError("Level 100 MMAP has trailing data")
    shade = source[shade_offset:]
    if any(value > 63 for value in shade):
        raise RuntimeError("Level 100 MMAP contains an invalid shade index")
    return cells, shade


def _static_shadow_cells(
    raw_level: bytes,
) -> tuple[bytes, tuple[bytes | None, ...]]:
    source = _extract_chunk(
        raw_level,
        b"SSHD",
        483_608,
        "d64b2d1503a59ab8aec15bcc92a3736bf23b6104ed0b557d1710f06dd74a5c0a",
    )
    shds = _chunk_payload(source[8:], b"SHDS")
    children = _chunk_records(shds)
    if (
        len(children) != 2
        or children[0][0] != b"DATA"
        or len(children[0][1]) != 4
        or children[1][0] != b"SDAT"
    ):
        raise RuntimeError("Level 100 SSHD has an unexpected envelope")
    owner_count = struct.unpack_from("<I", children[0][1])[0]
    owners = _chunk_records(children[1][1])
    if owner_count != 30 or len(owners) != owner_count or any(tag != b"CSSD" for tag, _ in owners):
        raise RuntimeError("Level 100 SSHD owner count changed")

    grid: list[bytearray | None] = [None] * (64 * 64)
    part_count = 0
    active_part_count = 0
    populated_cell_count = 0
    for _, owner in owners:
        if len(owner) < 24:
            raise RuntimeError("Level 100 CSSD is truncated")
        expected_parts = struct.unpack_from("<I", owner, 16)[0]
        parts = _chunk_records(owner[24:])
        if len(parts) != expected_parts or any(tag != b"SSPT" for tag, _ in parts):
            raise RuntimeError("Level 100 CSSD part count changed")
        part_count += len(parts)
        for _, part in parts:
            if len(part) < 20:
                raise RuntimeError("Level 100 SSPT is truncated")
            min_x, min_y, width, height, has_map = struct.unpack_from("<iiiii", part)
            if has_map not in (0, 1):
                raise RuntimeError(
                    "Level 100 SSPT metadata is invalid: "
                    f"{min_x},{min_y},{width},{height},{has_map}"
                )
            if has_map == 0:
                if len(part) != 20:
                    raise RuntimeError("Level 100 empty SSPT has trailing data")
                continue
            if width <= 0 or height <= 0:
                raise RuntimeError("Level 100 mapped SSPT has an invalid extent")
            active_part_count += 1
            smap = _chunk_payload(part[20:], b"SMAP")
            cursor = 0
            for local_y in range(height):
                for local_x in range(width):
                    if cursor + 4 > len(smap):
                        raise RuntimeError("Level 100 SMAP is truncated")
                    present = struct.unpack_from("<I", smap, cursor)[0]
                    cursor += 4
                    if present not in (0, 1):
                        raise RuntimeError("Level 100 SMAP has an invalid cell flag")
                    if present == 0:
                        continue
                    if cursor + 512 > len(smap):
                        raise RuntimeError("Level 100 SMAP bitmap is truncated")
                    bits = smap[cursor : cursor + 512]
                    cursor += 512
                    cell_x = min_x + local_x
                    cell_y = min_y + local_y
                    if not (0 <= cell_x < 64 and 0 <= cell_y < 64):
                        raise RuntimeError("Level 100 SMAP cell escaped the terrain")
                    grid_index = (cell_x * 64) + cell_y
                    destination = grid[grid_index]
                    if destination is None:
                        destination = bytearray(512)
                        grid[grid_index] = destination
                    for index, value in enumerate(bits):
                        destination[index] |= value
                    populated_cell_count += 1
            if cursor != len(smap):
                raise RuntimeError("Level 100 SMAP has trailing data")

    if (part_count, active_part_count, populated_cell_count) != (335, 275, 910):
        raise RuntimeError("Level 100 SSHD coverage changed")
    result = bytearray(512 * 512)
    for cell_y in range(64):
        for cell_x in range(64):
            bits = grid[(cell_x * 64) + cell_y]
            if bits is None:
                continue
            for local_y in range(8):
                source_y = local_y * 8
                for local_x in range(8):
                    source_x = local_x * 8
                    source_bit = (source_y * 64) + source_x
                    if bits[source_bit >> 3] & (1 << (source_bit & 7)):
                        target_x = (cell_x * 8) + local_x
                        target_y = (cell_y * 8) + local_y
                        result[(target_y * 512) + target_x] = 1
    if (
        sum(result) != 5_051
        or _sha256(result)
        != "0c67274b69599bf3832e6bc9e988a6436151c1a495aef720081c724f4111c03b"
    ):
        raise RuntimeError("Level 100 root static-shadow mask did not reproduce")
    return bytes(result), tuple(
        bytes(cell) if cell is not None else None for cell in grid
    )


def _static_shadow_mask(raw_level: bytes) -> bytes:
    return _static_shadow_cells(raw_level)[0]


def _lighting_gradient(sun_color: int, ambient_color: int) -> list[tuple[int, int, int]]:
    red_base = (((ambient_color >> 16) & 0xFF) << 8) // (((sun_color >> 16) & 0xFE) + 1)
    green_base = (ambient_color & 0xFF00) // (((sun_color >> 8) & 0xFE) + 1)
    blue_base = ((ambient_color & 0xFF) << 8) // ((sun_color & 0xFE) + 1)
    red = red_base << 8
    green = green_base << 8
    blue = blue_base << 8
    result: list[tuple[int, int, int]] = []
    for _ in range(64):
        red_value = min(((red >> 8) << 16) * 2, 0x00F80000) & 0x00F80000
        green_value = min(((green >> 8) << 11) * 2, 0x0007E000) & 0x0007E000
        blue_value = min(((blue >> 3) & 0xFFFFFFE0) * 2, 0x00001F00) & 0x00001F00
        result.append((red_value, green_value, blue_value))
        red += (255 - red_base) * 4
        green += (255 - green_base) * 4
        blue += (255 - blue_base) * 4
    return result


def _signed32(value: int) -> int:
    value &= 0xFFFFFFFF
    return value - 0x100000000 if value & 0x80000000 else value


def _f32(value: float) -> float:
    return struct.unpack("<f", struct.pack("<f", value))[0]


def _pine_shadow_source(
    raw_level: bytes,
    raw_base: bytes,
) -> tuple[bytes, tuple[tuple[int, int, int], ...]]:
    dmkr = _extract_chunk(
        raw_base,
        b"DMKR",
        5_473,
        "c9c9d88dbf4248e967616fdd0d4a59b8528251e13daf16b14b974582e94fb7f1",
    )[8:]
    if len(dmkr) != 5_465 or struct.unpack_from("<I", dmkr)[0] != 6:
        raise RuntimeError("base DMKR metadata changed")
    alpha = dmkr[4:]
    _, pines, _ = _parse_static_world(raw_level)
    views = _pine_imposter_views(raw_level)
    descriptors: list[tuple[int, int, int]] = []
    last_x = 0.0
    last_y = 0.0
    for x_value, y_value, variant_value in pines:
        variant = int(variant_value)
        x = _f32(_f32(float(x_value)) - _f32(0.4))
        y = _f32(_f32(float(y_value)) + _f32(0.1))
        delta_x = _f32(x - last_x)
        delta_y = _f32(y - last_y)
        distance_squared = _f32(_f32(delta_x * delta_x) + _f32(delta_y * delta_y))
        if distance_squared < _f32(0.01):
            continue
        last_x = x
        last_y = y
        metric = max(views[variant][0][4], views[variant][0][5])
        bucket = min(int(round(_f32(_f32(metric) * _f32(6.0)))), 6)
        high_size = 1 << bucket
        center_x = int(round(_f32(x * _f32(16.0))))
        center_y = int(round(_f32(y * _f32(16.0))))
        top_x = center_x - (high_size >> 1)
        top_y = center_y - (high_size >> 1)
        cell_x = top_x >> 7
        cell_y = top_y >> 7
        if not (0 <= cell_x < 64 and 0 <= cell_y < 64):
            continue
        level = bucket - 4
        if level <= 0:
            continue
        descriptors.append((top_x >> 4, top_y >> 4, level))
    return alpha, tuple(descriptors)


def _apply_pine_shadows(
    pixels: list[int],
    raw_level: bytes,
    raw_base: bytes,
) -> None:
    alpha, descriptors = _pine_shadow_source(raw_level, raw_base)
    level_offsets = (0, 1, 5, 21, 85, 341, 1_365)

    for top_x, top_y, level in reversed(descriptors):
        dimension = 1 << level
        source_offset = level_offsets[level]
        for source_y in range(dimension):
            target_y = top_y + source_y
            if not 0 <= target_y < 512:
                continue
            for source_x in range(dimension):
                target_x = top_x + source_x
                if not 0 <= target_x < 512:
                    continue
                amount = alpha[source_offset + (source_y * dimension) + source_x]
                if amount >= 32:
                    continue
                target_index = (target_y * 512) + target_x
                destination = pixels[target_index]
                pair = ((destination << 16) | destination) & 0x07E0F81F
                scaled = ((pair * amount) >> 5) & 0x07E0F81F
                pixels[target_index] = ((scaled >> 16) + scaled) & 0xFFFF


def _render_root_terrain(raw_level: bytes, raw_base: bytes, height_field: bytes) -> bytes:
    map_data, palettes = _parse_root_map_texture(raw_level)
    cells, shade = _parse_root_mixer_map(raw_level)
    static_shadow = _static_shadow_mask(raw_level)
    chfd = _require_chunk(height_field, 8, b"CHFD", 5_084)
    if height_field[chfd + 0x1030] != 10 or height_field[chfd + 0x1094] != 0:
        raise RuntimeError("Level 100 no longer selects mixer set 10 and detail texture 00")
    sun_color = struct.unpack_from("<I", height_field, chfd + 0x107C)[0]
    ambient_color = struct.unpack_from("<I", height_field, chfd + 0x108C)[0]
    gradient = _lighting_gradient(sun_color, ambient_color)
    pixels = [0] * (512 * 512)

    for y in range(512):
        for x in range(512):
            tile_x = x // 8
            tile_y = y // 8
            local_x = x & 7
            local_y = y & 7
            cell_index = (tile_y * 64) + tile_x
            material_ids, weights = cells[cell_index]
            source_x = local_x + (8 if cell_index & 1 else 0)
            source_y = local_y + (8 if cell_index & 0x40 else 0)
            texel = (source_y * 16) + source_x
            palette_index = map_data[texel]
            color = palettes[palette_index]
            for layer, material_id in enumerate(material_ids):
                palette_index = map_data[(material_id * 256) + texel]
                candidate = palettes[(material_id * 256) + palette_index]
                weight = struct.unpack("b", bytes((weights[(layer * 81) + (local_y * 9) + local_x],)))[0]
                candidate = (candidate + ((weight << 24) & 0xFFFFFFFF)) & 0xFFFFFFFF
                difference = _signed32(candidate - color)
                if difference > 0x1FFFFFFF:
                    color = candidate
                elif difference >= 0:
                    blend = difference >> 26
                    color = (
                        (((color & 0x00F8F8FF) * (7 - blend) +
                          (candidate & 0x00F8F8FF) * blend) >> 3)
                        + (candidate & 0xFF000000)
                    ) & 0xFFFFFFFF

            red = color & 0xFF
            green = (color >> 8) & 0xFF
            blue = (color >> 16) & 0xFF
            # The released blit at 0x0047eff0 bilinearly interpolates the 2x2
            # shade corners in 8.8 fixed point and pre-increments both
            # accumulators, so its last sub-texel lands exactly on the far
            # corner. The root map is lod_shift 0 (one destination texel per
            # shade unit, dst_stride 0x200), where that reduces algebraically
            # to the far corner itself. Measured: zero mismatches over 33,600
            # island texels - see
            # reverse-engineering/binary-analysis/terrain-shade-bilinear-decode-2026-07-26.md
            # This is the exact level-0 case of Level100TerrainCompositor's
            # general interpolation, not a simplification of it.
            shade_x = min(x + 1, 511)
            shade_y = min(y + 1, 511)
            shade_index = shade[(shade_y * 512) + shade_x]
            shade_index >>= static_shadow[(y * 512) + x]
            light_red, light_green, light_blue = gradient[shade_index]
            pixels[(y * 512) + x] = (
                ((green * light_green & 0x07E00000) +
                 (blue * light_blue & 0x001F0000) +
                 (red * light_red & 0xF8000000)) >> 16
            ) & 0xFFFF

    _apply_pine_shadows(pixels, raw_level, raw_base)
    return struct.pack("<262144H", *pixels)


def _render_terrain_hierarchy_source(raw_level: bytes, raw_base: bytes) -> bytes:
    maps = _parse_landscape_map_textures(raw_level)
    cells, shade = _parse_root_mixer_map(raw_level)
    _, shadow_cells = _static_shadow_cells(raw_level)
    pine_alpha, pine_descriptors = _pine_shadow_source(raw_level, raw_base)
    if (
        len(cells) != 4_096
        or len(shade) != 512 * 512
        or sum(shadow is not None for shadow in shadow_cells) != 211
        or len(pine_alpha) != 5_461
        or len(pine_descriptors) != 1_481
    ):
        raise RuntimeError("Level 100 terrain hierarchy source counts changed")

    output = bytearray(b"LTH1")
    output.extend(struct.pack("<II", 1, len(maps)))
    for width, data, palettes in maps:
        output.extend(struct.pack("<II", width, len(data)))
        output.extend(data)
        output.extend(struct.pack("<I", len(palettes)))
        output.extend(struct.pack(f"<{len(palettes)}I", *palettes))

    output.extend(struct.pack("<I", len(cells)))
    for material_ids, weights in cells:
        output.append(len(material_ids))
        output.extend(bytes(material_ids))
        output.extend(weights)

    output.extend(struct.pack("<I", len(shade)))
    output.extend(shade)

    populated_shadows: list[tuple[int, bytes]] = []
    for tile_y in range(64):
        for tile_x in range(64):
            shadow = shadow_cells[(tile_x * 64) + tile_y]
            if shadow is not None:
                populated_shadows.append(((tile_y * 64) + tile_x, shadow))
    output.extend(struct.pack("<I", len(populated_shadows)))
    for tile_index, shadow in populated_shadows:
        output.extend(struct.pack("<H", tile_index))
        output.extend(shadow)

    output.extend(struct.pack("<I", len(pine_alpha)))
    output.extend(pine_alpha)
    output.extend(struct.pack("<I", len(pine_descriptors)))
    for top_x, top_y, level in pine_descriptors:
        output.extend(struct.pack("<hhB", top_x, top_y, level))
    return bytes(output)


def _materialize(game_root: Path, stage: Path) -> tuple[tuple[Path, str], ...]:
    source_data: dict[Path, bytes] = {}
    for destination, source, expected in DIRECT_ASSETS + FRONTEND_ASSETS:
        data = _read_exact(game_root / source, expected)
        source_data[destination] = data
        target = stage / destination
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)

    english_table = _read_exact(
        game_root / ENGLISH_LANGUAGE_TABLE,
        ENGLISH_LANGUAGE_TABLE_SHA256,
    )
    frontend_localization = _frontend_localization_bytes(english_table)
    frontend_localization_target = stage / FRONTEND_LOCALIZATION
    frontend_localization_target.parent.mkdir(parents=True, exist_ok=True)
    frontend_localization_target.write_bytes(frontend_localization)
    frontend_world_strings = _frontend_world_strings_bytes(english_table)
    frontend_world_strings_target = stage / FRONTEND_WORLD_STRINGS
    frontend_world_strings_target.parent.mkdir(parents=True, exist_ok=True)
    frontend_world_strings_target.write_bytes(frontend_world_strings)
    _materialize_feback_strip(game_root, stage)
    hud_manifest_hash = _materialize_level100_hud_manifest(stage)
    if hud_manifest_hash != LEVEL100_HUD_MANIFEST_SHA256:
        raise RuntimeError(
            "Level 100 HUD manifest did not reproduce exactly "
            f"(SHA-256 {hud_manifest_hash})"
        )

    level_archive = _read_exact(game_root / LEVEL_ARCHIVE, LEVEL_ARCHIVE_SHA256)
    tools_root = ROOT / "tools"
    sys.path.insert(0, str(tools_root))
    from aya_archive_inventory import inflate_aya_bytes

    raw_level = inflate_aya_bytes(level_archive)
    _, scripts = _parse_level_world_scripts(raw_level)
    for name, payload in scripts.items():
        script_target = stage / LEVEL100_SCRIPT_ROOT / f"level100-{name}.mso.bin"
        script_target.parent.mkdir(parents=True, exist_ok=True)
        script_target.write_bytes(payload)

    # Later admitted worlds share one materialization owner. Their exact RLWD
    # preamble variants are data in LATER_WORLD_ADMISSIONS; adding a world must
    # not copy another extraction block or weaken its header assertions.
    for admission in LATER_WORLD_ADMISSIONS:
        archive = _read_exact(
            game_root / admission.archive,
            admission.archive_sha256,
        )
        raw_world = inflate_aya_bytes(archive)
        if admission.world_number == 110:
            objects = _parse_world110_initial_object_seeds(raw_world)
            seed_data = _world110_initial_object_seed_bytes(objects)
            seed_hash = _sha256(seed_data)
            if seed_hash != WORLD110_INITIAL_OBJECT_SEEDS_SHA256:
                raise RuntimeError(
                    "world 110 initial-object seed artifact did not reproduce "
                    f"exactly (SHA-256 {seed_hash})"
                )
            seed_target = stage / LEVEL110_INITIAL_OBJECT_SEEDS
            seed_target.parent.mkdir(parents=True, exist_ok=True)
            seed_target.write_bytes(seed_data)
            actor_data = _world110_initial_actor_bytes(
                raw_world,
                _read_exact(game_root / PHYSICS_DEFINITIONS, PHYSICS_DEFINITIONS_SHA256),
                _read_exact(game_root / WORLD110_LANDING_CRAFT_MESH,
                            WORLD110_LANDING_CRAFT_MESH_SHA256),
                _read_pine_meshes(game_root),
                tuple(_read_exact(game_root / path, digest)
                      for _, path, digest, _ in WORLD110_INITIAL_MESHES),
            )
            actor_hash = _sha256(actor_data)
            if actor_hash != WORLD110_INITIAL_ACTORS_SHA256:
                raise RuntimeError(
                    "world 110 initial actors did not reproduce exactly "
                    f"(SHA-256 {actor_hash})"
                )
            (stage / LEVEL110_INITIAL_ACTORS).write_bytes(actor_data)
            static_data = _world110_static_world_bytes(
                raw_world,
                _read_exact(game_root / PHYSICS_DEFINITIONS, PHYSICS_DEFINITIONS_SHA256),
                _read_exact(game_root / WORLD110_LANDING_CRAFT_MESH, WORLD110_LANDING_CRAFT_MESH_SHA256),
            )
            static_hash = _sha256(static_data)
            if static_hash != WORLD110_STATIC_WORLD_SHA256:
                raise RuntimeError(
                    "world 110 static world did not reproduce exactly "
                    f"(SHA-256 {static_hash})"
                )
            (stage / LEVEL110_STATIC_WORLD).write_bytes(static_data)
            player_data = _world110_player_input_bytes(
                raw_world, (game_root / BATTLE_ENGINE_CONFIGURATIONS).read_bytes()
            )
            player_hash = _sha256(player_data)
            if player_hash != WORLD110_PLAYER_INPUTS_SHA256:
                raise RuntimeError(
                    "world 110 player inputs did not reproduce exactly "
                    f"(SHA-256 {player_hash})"
                )
            (stage / LEVEL110_PLAYER_INPUTS).write_bytes(player_data)
        scripts = _parse_world_scripts(
            raw_world,
            admission.world_number,
            admission.script_objects,
            admission.level_header,
        )
        for name, payload in scripts.items():
            script_target = (
                stage
                / admission.script_root
                / f"level{admission.world_number}-{name}.mso.bin"
            )
            script_target.parent.mkdir(parents=True, exist_ok=True)
            script_target.write_bytes(payload)
        heightfield = _extract_chunk(
            raw_world,
            b"HFLD",
            admission.hfld_size,
            admission.hfld_sha256,
        )
        heightfield_target = stage / admission.heightfield
        heightfield_target.parent.mkdir(parents=True, exist_ok=True)
        heightfield_target.write_bytes(heightfield)

    chunk_data: dict[bytes, bytes] = {}
    for destination, tag, expected_size, expected in CHUNKS:
        data = _extract_chunk(raw_level, tag, expected_size, expected)
        chunk_data[tag] = data
        target = stage / destination
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)

    base_archive = _read_exact(game_root / BASE_ARCHIVE, BASE_ARCHIVE_SHA256)
    raw_base = inflate_aya_bytes(base_archive)
    root_terrain = _render_root_terrain(raw_level, raw_base, chunk_data[b"HFLD"])
    root_terrain_hash = _sha256(root_terrain)
    if root_terrain_hash != ROOT_TERRAIN_TEXTURE_SHA256:
        raise RuntimeError(
            "Level 100 root terrain did not reproduce exactly "
            f"(SHA-256 {root_terrain_hash})"
        )
    root_terrain_target = stage / ROOT_TERRAIN_TEXTURE
    root_terrain_target.parent.mkdir(parents=True, exist_ok=True)
    root_terrain_target.write_bytes(root_terrain)

    terrain_hierarchy = _render_terrain_hierarchy_source(raw_level, raw_base)
    terrain_hierarchy_hash = _sha256(terrain_hierarchy)
    if terrain_hierarchy_hash != TERRAIN_HIERARCHY_SOURCE_SHA256:
        raise RuntimeError(
            "Level 100 terrain hierarchy did not reproduce exactly "
            f"(SHA-256 {terrain_hierarchy_hash})"
        )
    terrain_hierarchy_target = stage / TERRAIN_HIERARCHY_SOURCE
    terrain_hierarchy_target.parent.mkdir(parents=True, exist_ok=True)
    terrain_hierarchy_target.write_bytes(terrain_hierarchy)

    rebuild_tools = ROOT / "rebuild/tools"
    sys.path.insert(0, str(rebuild_tools))
    from cmsh_static_preview import convert_aya_bytes

    for destination, source_destination, frame, expected in MESHES:
        data = convert_aya_bytes(
            source_data[source_destination],
            include_vertex_attributes=True,
            include_material_layer_groups=True,
            include_vertex_colors=True,
            hierarchy_frame=frame,
        )
        if _sha256(data) != expected:
            raise RuntimeError(f"derived mesh did not reproduce exactly: {destination}")
        target = stage / destination
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)

    bank = _read_exact(game_root / SOUND_BANK, SOUND_BANK_SHA256)
    records = _xap_records(bank)
    for destination, record_index, expected_name, expected in SOUNDS:
        name, decoded_size, packed = records[record_index]
        if name != expected_name:
            raise RuntimeError(f"unexpected PSMP name at record {record_index}: {name}")
        data = _pcm_wav(_decode_ima_high_nibble_first(packed, decoded_size))
        if _sha256(data) != expected:
            raise RuntimeError(f"decoded sound did not reproduce exactly: {destination}")
        target = stage / destination
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)

    static_world_outputs = _materialize_static_world(game_root, raw_level, stage)
    all_outputs = _fixed_outputs() + static_world_outputs
    for relative, expected in all_outputs:
        if _sha256((stage / relative).read_bytes()) != expected:
            raise RuntimeError(f"staged retail asset failed final verification: {relative}")
    return all_outputs


# --- retail cold-start media -------------------------------------------------
#
# The intro sequencer in pristine BEA.exe (0x004efce3-0x004efee9) loads
# data/textures/splash.tga and then plays "ltlogo" and "openingfmv" through the
# format string "data\\video\\%s.vid". The gated
# Play("TWIMTBP_GefFX_640x480_Audio") between them is not materialized, but NOT
# because it is dead code — that first reading was wrong. Its gate [0x83d404] is
# the value field of a CVar named "TWIMTBP" constructed at 0x83d3f8 (0x004efae0)
# and written object-relative by CVar::Init's `mov [eax+0xc], ecx`, which an
# absolute-address scan cannot see; the shipped cardid.txt sets
# `Tweak:TWIMTBP 1` for exactly one device, Vendor:10DE Device:0330 GeForce FX
# 5900 Ultra. It is omitted because no modern adapter matches that entry and the
# measured startup has no 5 s slot for it.
#
# WHERE THIS WRITES, AND WHY IT IS NOT WITH THE OTHER ASSETS.
# Everything above lands under rebuild/OnslaughtRebuild.Godot/Assets, i.e.
# inside res://. That is already a latent hazard for the FEBack strip: a
# .gitignore entry stops a git commit but does NOT stop a Godot export from
# packing an ignored file into the PCK. Startup media is two decoded retail
# movies, so it goes to a cache OUTSIDE the project tree where packing it is
# structurally impossible. It is therefore NOT part of _all_outputs() and does
# not participate in the exact-file count.
#
# FORMAT. One lossless PNG per video frame, not a raw RGB24 strip. Measured on
# the reference machine: LTLogo 229 frames = 24 MB and OpeningFMV 2054 frames =
# 244 MB, against 98.9 MB and 887 MB for RGB24 strips of the same footage. PNG
# is lossless, so these ARE the Bink decode and can serve as their own parity
# oracle; and because frames are separately addressable the player holds one
# frame resident instead of 2,054 textures.
#
# THE THIRD CLIP IS NOT A STARTUP CLIP. data/video/cutscenes/01.vid is Level
# 100's intro cutscene, played by CGame::RunIntroFMV
# (references/Onslaught/game.cpp:1122-1152) from CGame::RestartLoopRunLevel
# (game.cpp:1336-1345) — after the level loads and before the first gameplay
# frame, not during startup. It is decoded here because it is the same decode
# contract, the same cache, the same 480x300 at 25 fps, and the same
# startup-media.json the client already reads; a second recipe for the same job
# is how the half-rate FEBack strip happened. Which clip: the campaign FMV table
# at file offset 0x23FD70 (VA 0x0063FD70) of the pristine specimen
# local-lab/safe-copy-bea-pristine/BEA.exe.original.backup (sha256 74154bfa…)
# holds [level][intro][outroA][outroB] records, and row 0 is (100, 1, 2, -1).
# game.cpp:1130 formats that id as "cutscenes\\%02d".
#
# Clip 02 — the row's outro — is deliberately NOT decoded. RunOutroFMV plays it
# only on (mQuit == QT_QUIT_TO_FRONTEND) && (mFinalState == GAME_STATE_LEVEL_WON)
# (game.cpp:1166), and nothing in the reconstruction produces that signal yet.
# Decoding 62 MB of frames nothing can play would be retention without a
# consumer.
#
# AUDIO, AND WHICH OF THE FIVE TRACKS IS ENGLISH.
# cutscenes/01.vid carries FIVE binkaudio_rdft 44.1 kHz stereo tracks — one per
# shipped language — and for two days nothing was decoded because guessing which
# one is English was refused. It is now read out of the shipped binary. All file
# offsets below are VA - 0x400000 in the pristine specimen
# local-lab/safe-copy-bea-pristine/BEA.exe.original.backup, sha256 74154bfa….
#
#   1. CText::Init's jump table at 0x004f2498 selects the language string. Its
#      five arms load, IN THIS ORDER BY CASE: 0 -> 0x00632D74 "english",
#      1 -> 0x00632D7C "french", 2 -> 0x00632D94 "german",
#      3 -> 0x00632D84 "spanish", 4 -> 0x00632D8C "italian". The arms load OUT
#      of address order, so reading the string table in address order gives the
#      WRONG enum; the arm targets are the evidence.
#   2. g_LanguageIndex is g_Text + 0x1c = 0x0083d97c.
#   3. CFMV::PlayFullscreenWithLoadingGate (0x00465640) forwards
#      `localise ? g_LanguageIndex : 0` into vtable slot +0x2c.
#   4. Five hops later CBinkOpenThread::VFunc_0 (0x00541140) passes that value
#      VERBATIM to BINKW32::BinkSetSoundTrack at 0x0054116d — no +1, no remap.
#   5. Corroboration: localise=FALSE forces 0, CLIParams.cpp:64 defaults
#      mLanguage=LANG_ENGLISH, and the American-English path pins the index to 0
#      as well, so American and English share track 0.
#
# So ENGLISH IS BINK TRACK 0. The one link that is a property of THIS FILE
# rather than of the binary is that a Bink *track ID* and ffmpeg's `-map 0:a:N`
# *ordinal* coincide, and _bink_audio_tracks() below checks exactly that against
# the shipped header rather than assuming it.
#
# No language selector is built. Tracks 1-4 are identified but nothing in the
# reconstruction selects a language, and shipping a surface with one consumer
# and no evidence behind its wiring is how dead settings accumulate. LTLogo.vid
# and OpeningFMV.vid each carry exactly ONE audio track. Their track-0 headers
# and 44.1 kHz stereo streams were read back from the Linux Steam installation
# on 2026-09-06; they use the same measured decode and playback path.
STARTUP_MEDIA_SCHEMA = "onslaught-startup-media.v4"
STARTUP_FRAME_SET_DOMAIN = b"onslaught-startup-frame-set.v1\0"
STARTUP_MEDIA_CLIPS = (
    ("LostToysLogo", "data/video/LTLogo.vid", "lost-toys-logo", 480, 300, 25, 229),
    ("OpeningMontage", "data/video/OpeningFMV.vid", "opening-montage", 480, 300, 25, 2054),
    ("Level100IntroCutscene", "data/video/cutscenes/01.vid",
     "level100-intro-cutscene", 480, 300, 25, 3095),
)

# cue -> (bink track, output name, sample rate, channels, bits per sample).
# The sample rate and channel count are read back off the shipped Bink header
# before the decode runs, exactly as the video geometry is; they are stated here
# so a mismatch is an error rather than a silently different output.
STARTUP_MEDIA_AUDIO = {
    "LostToysLogo": (0, "audio-track00.wav", 44100, 2, 16),
    "OpeningMontage": (0, "audio-track00.wav", 44100, 2, 16),
    "Level100IntroCutscene": (0, "voice-track00.wav", 44100, 2, 16),
}

# binkaudio's frame length at 44.1 kHz. ffmpeg's decoder uses
# frame_len_bits = 11 for sample rates >= 44100 (libavcodec/binkaudio.c), so the
# decoder emits whole 2048-sample frames and the last one overruns the video by
# whatever it takes to fill. That overhang is the ONLY slack allowed between the
# audio and the video length: anything larger means the two are not the same
# clip, which is the cheapest possible falsification of the chain above.
BINK_AUDIO_FRAME_SAMPLES = 2048

STARTUP_MEDIA_SPLASH_SOURCE = "data/textures/splash.tga"


def _default_startup_media_root() -> Path:
    configured = os.environ.get("ONSLAUGHT_STARTUP_MEDIA")
    if configured:
        return Path(configured)
    if os.name == "nt":
        local = os.environ.get("LOCALAPPDATA")
        if local:
            return Path(local) / "OnslaughtToolkit" / "startup-media"
        return ROOT / "local-lab/startup-media"

    # A linked worktree does not own a second ignored lab. Route the implicit
    # Linux cache through the same canonical-owner, plain-directory, and
    # link/reparse checks as rebuild staging before startup media can be read
    # or written.
    return _resolve_work_root(
        _canonical_repository_root() / "local-lab/startup-media"
    )


def _png_dimensions(path: Path) -> tuple[int, int] | None:
    """Validate a complete PNG chunk stream and return its IHDR dimensions."""
    png_signature = bytes((0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A))
    try:
        with path.open("rb") as stream:
            file_size = path.stat().st_size
            if stream.read(8) != png_signature:
                return None

            dimensions: tuple[int, int] | None = None
            rgb8_noninterlaced = False
            idat = bytearray()
            first_chunk = True
            for _ in range(4096):
                chunk_header = stream.read(8)
                if len(chunk_header) != 8:
                    return None
                length = int.from_bytes(chunk_header[:4], "big")
                kind = chunk_header[4:8]
                if stream.tell() + length + 4 > file_size:
                    return None

                payload = stream.read(length)
                stored_crc = stream.read(4)
                if len(payload) != length or len(stored_crc) != 4:
                    return None
                actual_crc = zlib.crc32(kind)
                actual_crc = zlib.crc32(payload, actual_crc) & 0xFFFFFFFF
                if actual_crc != int.from_bytes(stored_crc, "big"):
                    return None

                if kind == b"IHDR":
                    if not first_chunk or length != 13:
                        return None
                    dimensions = (
                        int.from_bytes(payload[:4], "big"),
                        int.from_bytes(payload[4:8], "big"),
                    )
                    rgb8_noninterlaced = payload[8:] == bytes((8, 2, 0, 0, 0))
                elif kind == b"IDAT":
                    idat.extend(payload)
                if kind == b"IEND":
                    if (
                        length != 0
                        or dimensions is None
                        or not rgb8_noninterlaced
                        or not idat
                        or stream.tell() != file_size
                    ):
                        return None
                    width, height = dimensions
                    row_bytes = 1 + width * 3
                    expected_bytes = height * row_bytes
                    decoder = zlib.decompressobj()
                    decoded = decoder.decompress(idat, expected_bytes + 1)
                    if len(decoded) <= expected_bytes and not decoder.unconsumed_tail:
                        decoded += decoder.flush(expected_bytes + 1 - len(decoded))
                    if (
                        not decoder.eof
                        or decoder.unused_data
                        or decoder.unconsumed_tail
                        or len(decoded) != expected_bytes
                        or any(decoded[row * row_bytes] > 4 for row in range(height))
                    ):
                        return None
                    return dimensions
                first_chunk = False
    except (OSError, zlib.error):
        return None
    return None


def _bink_audio_tracks(source: Path) -> list[tuple[int, int]]:
    """Read the shipped Bink header's audio table as [(track id, sample rate)].

    This exists for ONE assertion. `BinkSetSoundTrack` takes a track ID and
    `ffmpeg -map 0:a:N` takes an ordinal within the file; ffmpeg's bink demuxer
    creates the streams in on-disk order and then assigns `st->id` from the
    track-ID table, so the two coincide only while that table is the identity.
    For cutscenes/01.vid it reads {0,1,2,3,4} and they do — but that is a
    property of the file, so it is measured here rather than assumed.

    Layout, from the header of the shipped files themselves:
      0x00 'BIK' + revision      0x28 track count (u32)
      0x08 frame count (u32)     0x2C track count x u32 max decoded size
      0x14 width, 0x18 height    +    track count x (u16 rate, u16 flags)
      0x1C fps num / 0x20 den    +    track count x u32 track id
    """
    with source.open("rb") as stream:
        header = stream.read(0x2C)
        if len(header) != 0x2C or header[:3] != b"BIK":
            raise RuntimeError(f"{source} is not a Bink file")
        (count,) = struct.unpack_from("<I", header, 0x28)
        if count > 256:
            raise RuntimeError(f"{source} declares an implausible {count} audio tracks")
        if count == 0:
            return []
        stream.seek(0x2C + (count * 4))
        rates = stream.read(count * 4)
        ids = stream.read(count * 4)
    if len(rates) != count * 4 or len(ids) != count * 4:
        raise RuntimeError(f"{source} has a truncated Bink audio header")
    return [
        (
            struct.unpack_from("<I", ids, index * 4)[0],
            struct.unpack_from("<H", rates, index * 4)[0],
        )
        for index in range(count)
    ]


def _wav_pcm_format(path: Path) -> tuple[int, int, int, int] | None:
    """Validate a canonical PCM WAV and return (rate, channels, bits, frames).

    Deliberately strict about the 44-byte canonical header rather than walking
    arbitrary chunks: the decode below runs ffmpeg with `-fflags +bitexact`
    precisely so no LIST/INFO chunk carrying the encoder version lands in the
    file. Accepting one here would let a build-dependent string into a receipt
    that is supposed to identify retail bytes.
    """
    try:
        size = path.stat().st_size
        with path.open("rb") as stream:
            header = stream.read(44)
    except OSError:
        return None
    if len(header) != 44 or header[:4] != b"RIFF" or header[8:12] != b"WAVE":
        return None
    if header[12:16] != b"fmt " or header[36:40] != b"data":
        return None
    (riff_size,) = struct.unpack_from("<I", header, 4)
    (format_size,) = struct.unpack_from("<I", header, 16)
    audio_format, channels, rate, byte_rate, block_align, bits = struct.unpack_from(
        "<HHIIHH", header, 20
    )
    (data_size,) = struct.unpack_from("<I", header, 40)
    if (
        format_size != 16
        or audio_format != 1
        or channels <= 0
        or rate <= 0
        or bits <= 0
        or bits % 8 != 0
        or block_align != channels * (bits // 8)
        or byte_rate != rate * block_align
        or riff_size != size - 8
        or data_size != size - 44
        or data_size == 0
        or data_size % block_align != 0
    ):
        return None
    return rate, channels, bits, data_size // block_align


def _startup_frame_set_sha256(paths: list[Path]) -> str:
    """Hash one ordered frame set without inflating every PNG on each launch."""
    digest = hashlib.sha256()
    digest.update(STARTUP_FRAME_SET_DOMAIN)
    for path in paths:
        name = path.name.encode("ascii")
        size = path.stat().st_size
        digest.update(name)
        digest.update(b"\0")
        digest.update(str(size).encode("ascii"))
        digest.update(b"\0")
        with path.open("rb") as stream:
            while chunk := stream.read(1024 * 1024):
                digest.update(chunk)
    return digest.hexdigest()


def _startup_media_ready(game_root: Path, media_root: Path) -> bool:
    """Return true for a complete, structurally valid cache of this retail data."""
    try:
        manifest_path = media_root / "startup-media.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        if not isinstance(manifest, dict):
            return False
        if manifest.get("schema") != STARTUP_MEDIA_SCHEMA:
            return False

        clips = manifest.get("clips")
        stills = manifest.get("stills")
        if not isinstance(clips, dict) or not isinstance(stills, dict):
            return False

        expected_cues = {cue for cue, *_ in STARTUP_MEDIA_CLIPS}
        if set(clips) != expected_cues:
            return False

        for cue, relative, folder, width, height, fps, frames in STARTUP_MEDIA_CLIPS:
            entry = clips.get(cue)
            if not isinstance(entry, dict):
                return False

            source = game_root / relative
            expected_format = f"{folder}/f{{0:D5}}.png"
            if (
                not source.is_file()
                or entry.get("source") != relative
                or entry.get("sourceSha256") != _sha256(source.read_bytes())
                or entry.get("width") != width
                or entry.get("height") != height
                or entry.get("fpsNumerator") != fps
                or entry.get("fpsDenominator") != 1
                or entry.get("frameCount") != frames
                or entry.get("framePathFormat") != expected_format
            ):
                return False

            destination = media_root / folder
            expected_names = [f"f{index:05d}.png" for index in range(1, frames + 1)]
            actual = sorted(path.name for path in destination.glob("f*.png"))
            if actual != expected_names:
                return False
            frame_paths = [destination / name for name in expected_names]
            if entry.get("framesSha256") != _startup_frame_set_sha256(frame_paths):
                return False
            # Generation validates every decoded frame before publishing the
            # digest above. Re-inflating all 5,378 PNGs on each launch added
            # roughly 24 seconds; the ordered digest still checks every byte.
            edge_names = {expected_names[0], expected_names[-1]}
            if any(
                _png_dimensions(destination / name) != (width, height)
                for name in edge_names
            ):
                return False

            audio_spec = STARTUP_MEDIA_AUDIO.get(cue)
            if audio_spec is not None:
                track, name, rate, channels, bits = audio_spec
                audio = entry.get("audio")
                audio_path = destination / name
                if not isinstance(audio, dict):
                    return False
                if (
                    audio.get("track") != track
                    or audio.get("path") != f"{folder}/{name}"
                    or audio.get("sampleRate") != rate
                    or audio.get("channels") != channels
                    or audio.get("bitsPerSample") != bits
                    or not audio_path.is_file()
                    or audio.get("outputSha256") != _sha256(audio_path.read_bytes())
                ):
                    return False
                measured = _wav_pcm_format(audio_path)
                if measured is None:
                    return False
                if (rate, channels, bits) != measured[:3]:
                    return False
                if audio.get("sampleFrameCount") != measured[3]:
                    return False
            elif "audio" in entry:
                return False

        splash = stills.get("Splash")
        splash_source = game_root / STARTUP_MEDIA_SPLASH_SOURCE
        splash_path = media_root / "splash.png"
        if (
            not isinstance(splash, dict)
            or not splash_source.is_file()
            or splash.get("source") != STARTUP_MEDIA_SPLASH_SOURCE
            or splash.get("sourceSha256") != _sha256(splash_source.read_bytes())
            or splash.get("path") != "splash.png"
            or not splash_path.is_file()
            or splash.get("outputSha256") != _sha256(splash_path.read_bytes())
            or _png_dimensions(splash_path) != (512, 512)
        ):
            return False

        return True
    except (OSError, UnicodeError, ValueError, TypeError, KeyError, json.JSONDecodeError):
        return False


def _ffprobe_stream(source: Path) -> dict[str, str]:
    completed = subprocess.run(
        [
            "ffprobe", "-hide_banner", "-v", "error",
            "-select_streams", "v:0",
            "-show_entries", "stream=width,height,r_frame_rate,duration",
            "-of", "default=nw=1",
            str(source),
        ],
        check=True,
        capture_output=True,
        text=True,
    )
    fields: dict[str, str] = {}
    for line in completed.stdout.splitlines():
        if "=" in line:
            key, _, value = line.partition("=")
            fields[key.strip()] = value.strip()
    return fields


def _materialize_clip_audio(
    source: Path,
    relative: str,
    folder: str,
    destination: Path,
    frames: int,
    fps: int,
    audio_spec: tuple[int, str, int, int, int],
) -> dict[str, object]:
    """Decode ONE Bink audio track to lossless PCM beside that clip's frames.

    Lossless for the same reason the frames are PNG and not JPEG: this IS the
    Bink decode, so it can serve as its own parity oracle. 123.80 s of 44.1 kHz
    stereo s16 is 21.8 MB against 436.8 MB of frames for the same clip.
    """
    track, name, rate, channels, bits = audio_spec

    # Prove the ordinal ffmpeg is about to be handed is the track ID the game
    # hands BinkSetSoundTrack. Everything else about "English is track 0" is a
    # property of BEA.exe; this is the one part that is a property of the .vid.
    tracks = _bink_audio_tracks(source)
    if track >= len(tracks):
        raise RuntimeError(
            f"{relative} carries {len(tracks)} audio tracks, so track {track} does not exist"
        )
    track_id, track_rate = tracks[track]
    if track_id != track:
        raise RuntimeError(
            f"{relative} audio ordinal {track} has Bink track id {track_id}; the "
            "track-ID table is not the identity, so `-map 0:a:N` no longer "
            "selects the track BinkSetSoundTrack(N) would play"
        )
    if track_rate != rate:
        raise RuntimeError(
            f"{relative} audio track {track} is {track_rate} Hz, expected {rate} Hz"
        )

    output = destination / name
    # -fflags +bitexact on BOTH sides so the wav muxer omits the LIST/INFO chunk
    # naming the ffmpeg build. Without it the receipt hashes the encoder version
    # as well as the retail audio.
    subprocess.run(
        [
            "ffmpeg", "-hide_banner", "-loglevel", "error", "-y",
            "-fflags", "+bitexact",
            "-i", str(source),
            "-map", f"0:a:{track}",
            "-vn",
            "-acodec", "pcm_s16le",
            "-flags", "+bitexact",
            "-fflags", "+bitexact",
            str(output),
        ],
        check=True,
    )

    measured = _wav_pcm_format(output)
    if measured is None:
        raise RuntimeError(f"{relative} audio track {track} produced an invalid PCM WAV")
    measured_rate, measured_channels, measured_bits, sample_frames = measured
    if (measured_rate, measured_channels, measured_bits) != (rate, channels, bits):
        raise RuntimeError(
            f"{relative} audio track {track} decoded to {measured_rate} Hz "
            f"{measured_channels}ch {measured_bits}-bit, expected {rate} Hz "
            f"{channels}ch {bits}-bit"
        )

    # THE LENGTH LAW. The audio must cover the whole video and overrun it by
    # less than one binkaudio frame. A mismatch here is the cheapest possible
    # falsification that this track belongs to this clip at all.
    video_sample_frames = frames * rate // fps
    overhang = sample_frames - video_sample_frames
    if not 0 <= overhang < BINK_AUDIO_FRAME_SAMPLES:
        raise RuntimeError(
            f"{relative} audio track {track} is {sample_frames} sample frames "
            f"against {video_sample_frames} for {frames} video frames at {fps} fps "
            f"({overhang:+d}); expected 0..{BINK_AUDIO_FRAME_SAMPLES - 1}"
        )

    return {
        "track": track,
        "path": f"{folder}/{name}",
        "sampleRate": rate,
        "channels": channels,
        "bitsPerSample": bits,
        "sampleFrameCount": sample_frames,
        "outputSha256": _sha256(output.read_bytes()),
    }


def _materialize_startup_media(game_root: Path, media_root: Path) -> Path:
    media_root.mkdir(parents=True, exist_ok=True)
    index: dict[str, object] = {
        "schema": STARTUP_MEDIA_SCHEMA,
        "generatedBy": "rebuild/tools/materialize_retail_assets.py --startup-media",
        "gameRoot": str(game_root),
        "note": (
            "Decoded from the user's own retail installation. Never commit, "
            "never place under res://, never redistribute."
        ),
        "clips": {},
        "stills": {},
    }

    for cue, relative, folder, width, height, fps, frames in STARTUP_MEDIA_CLIPS:
        source = game_root / relative
        if not source.is_file():
            raise RuntimeError(f"startup media source missing: {source}")

        # Read the shipped header back rather than trusting the constants above.
        # A decode whose frame count silently disagrees with the source is
        # exactly the class of defect that produced the half-rate FEBack strip.
        probe = _ffprobe_stream(source)
        if int(probe["width"]) != width or int(probe["height"]) != height:
            raise RuntimeError(
                f"{relative} is {probe['width']}x{probe['height']}, expected {width}x{height}"
            )
        if probe["r_frame_rate"] != f"{fps}/1":
            raise RuntimeError(
                f"{relative} reports frame rate {probe['r_frame_rate']}, expected {fps}/1"
            )
        expected_duration = frames / fps
        if abs(float(probe["duration"]) - expected_duration) > 0.02:
            raise RuntimeError(
                f"{relative} reports duration {probe['duration']}s, "
                f"expected {expected_duration:.3f}s for {frames} frames at {fps} fps"
            )

        destination = media_root / folder
        if destination.exists():
            shutil.rmtree(destination)
        destination.mkdir(parents=True)

        # No -r, no fps filter, no -vf: any rate argument here is how a decode
        # silently drops every second frame while preserving total duration.
        subprocess.run(
            [
                "ffmpeg", "-hide_banner", "-loglevel", "error", "-y",
                "-i", str(source),
                "-pix_fmt", "rgb24",
                str(destination / "f%05d.png"),
            ],
            check=True,
        )

        written = sorted(destination.glob("f*.png"))
        if len(written) != frames:
            raise RuntimeError(
                f"{relative} decoded to {len(written)} frames, expected {frames}"
            )
        malformed = next(
            (
                frame
                for frame in written
                if _png_dimensions(frame) != (width, height)
            ),
            None,
        )
        if malformed is not None:
            raise RuntimeError(
                f"{relative} produced an invalid {width}x{height} PNG: {malformed}"
            )

        entry: dict[str, object] = {
            "source": relative,
            "sourceSha256": _sha256(source.read_bytes()),
            "width": width,
            "height": height,
            "fpsNumerator": fps,
            "fpsDenominator": 1,
            "frameCount": frames,
            "framePathFormat": f"{folder}/f{{0:D5}}.png",
            "framesSha256": _startup_frame_set_sha256(written),
        }

        audio_spec = STARTUP_MEDIA_AUDIO.get(cue)
        if audio_spec is not None:
            entry["audio"] = _materialize_clip_audio(
                source, relative, folder, destination, frames, fps, audio_spec
            )

        index["clips"][cue] = entry  # type: ignore[index]

    splash_source = game_root / STARTUP_MEDIA_SPLASH_SOURCE
    if not splash_source.is_file():
        raise RuntimeError(f"startup media source missing: {splash_source}")
    subprocess.run(
        [
            "ffmpeg", "-hide_banner", "-loglevel", "error", "-y",
            "-i", str(splash_source),
            str(media_root / "splash.png"),
        ],
        check=True,
    )
    splash_output = media_root / "splash.png"
    if _png_dimensions(splash_output) != (512, 512):
        raise RuntimeError(
            f"{STARTUP_MEDIA_SPLASH_SOURCE} produced an invalid 512x512 PNG: "
            f"{splash_output}"
        )
    index["stills"]["Splash"] = {  # type: ignore[index]
        "source": STARTUP_MEDIA_SPLASH_SOURCE,
        "sourceSha256": _sha256(splash_source.read_bytes()),
        "path": splash_output.name,
        "outputSha256": _sha256(splash_output.read_bytes()),
    }

    manifest = media_root / "startup-media.json"
    manifest.write_text(json.dumps(index, indent=2), encoding="utf-8")
    return manifest


def _publish(stage: Path, outputs: tuple[tuple[Path, str], ...]) -> None:
    for relative, expected in outputs:
        source = stage / relative
        destination = ROOT / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        temporary = destination.with_name(f".{destination.name}.materializing")
        try:
            shutil.copyfile(source, temporary)
            if _sha256(temporary.read_bytes()) != expected:
                raise RuntimeError(f"publication verification failed: {relative}")
            os.replace(temporary, destination)
        finally:
            temporary.unlink(missing_ok=True)


def main() -> int:
    parser = argparse.ArgumentParser(description="Materialize the exact retail assets used by the rebuild")
    parser.add_argument("--game-root", type=Path, help="Battle Engine Aquila retail installation root")
    work_root_group = parser.add_mutually_exclusive_group()
    work_root_group.add_argument(
        "--work-root",
        type=Path,
        help=(
            "temporary asset-staging directory; on Linux this must be a plain "
            "descendant of the canonical checkout's ignored local-lab; always "
            "rejected when it overlaps the retail installation"
        ),
    )
    work_root_group.add_argument(
        "--host-default-work-root",
        action="store_true",
        help=(
            "select the verified launcher staging owner (canonical checkout "
            "local-lab/rebuild-godot on Linux; the historical ignored default "
            "on Windows)"
        ),
    )
    parser.add_argument("--force", action="store_true", help="reverify source data and regenerate every asset")
    parser.add_argument("--reuse-canonical-assets", action="store_true",
                        help="verify and link canonical materialized inputs read-only; no corpus copy")
    parser.add_argument(
        "--startup-media",
        action="store_true",
        help=(
            "decode the retail movies (LTLogo.vid, OpeningFMV.vid, splash.tga and "
            "the Level 100 intro cutscene cutscenes/01.vid, including its English "
            "Bink audio track 0) into a cache OUTSIDE res://, and do nothing else"
        ),
    )
    parser.add_argument(
        "--startup-media-root",
        type=Path,
        help=(
            "where to write the cold-start media cache (default: "
            "ONSLAUGHT_STARTUP_MEDIA when set; otherwise %%LOCALAPPDATA%%/"
            "OnslaughtToolkit/startup-media on Windows or the canonical "
            "checkout's local-lab/startup-media on Linux)"
        ),
    )
    args = parser.parse_args()

    # Child worktrees consume committed-contract inputs. A stale canonical
    # conversion is an explicit conflict, never permission to replace it.
    if args.reuse_canonical_assets or (not args.startup_media and (ROOT / ".git").is_file()):
        if args.force or args.startup_media:
            print("canonical reuse cannot force regeneration or write startup media", file=sys.stderr)
            return 2
        try:
            count = _reuse_canonical_assets()
        except (OSError, RuntimeError, ValueError, KeyError) as error:
            print(f"canonical asset reuse failed: {error}", file=sys.stderr)
            return 2
        print(f"canonical retail inputs verified: {count} files; checkout-local file links ready")
        return 0

    requested_work_root = (
        _host_default_work_root_request()
        if args.host_default_work_root
        else args.work_root
    )

    if args.startup_media:
        if requested_work_root is not None or args.host_default_work_root:
            print(
                "startup media materialization failed: --work-root and "
                "--host-default-work-root apply only to rebuild-asset staging; "
                "use --startup-media-root for this mode",
                file=sys.stderr,
            )
            return 2
        try:
            game_root = _resolve_game_root(args.game_root)
            media_root = args.startup_media_root or _default_startup_media_root()
            if not args.force and _startup_media_ready(game_root, media_root):
                print(f"startup media ready: {media_root / 'startup-media.json'}")
                return 0
            manifest = _materialize_startup_media(game_root, media_root)
        except (OSError, RuntimeError, UnicodeError, ValueError, KeyError,
                subprocess.SubprocessError) as error:
            print(f"startup media materialization failed: {error}", file=sys.stderr)
            return 2
        print(f"startup media materialized: {manifest}")
        return 0

    try:
        work_root = _resolve_work_root(requested_work_root)
    except (OSError, RuntimeError, ValueError) as error:
        print(f"retail asset materialization failed: {error}", file=sys.stderr)
        return 2

    if not args.force and args.game_root is None and _outputs_ready():
        print(f"retail rebuild assets ready: {len(_all_outputs(ROOT))} exact files")
        return 0

    try:
        game_root = _resolve_game_root(args.game_root)
        work_root = _resolve_work_root(requested_work_root, game_root=game_root)
        work_root.mkdir(parents=requested_work_root is None, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix="materialize-retail-", dir=work_root) as temporary:
            stage = Path(temporary)
            outputs = _materialize(game_root, stage)
            _publish(stage, outputs)
        if not _outputs_ready():
            raise RuntimeError("published retail assets failed final verification")
    except (OSError, RuntimeError, UnicodeError, ValueError) as error:
        print(f"retail asset materialization failed: {error}", file=sys.stderr)
        return 2

    print(f"retail rebuild assets materialized: {len(_all_outputs(ROOT))} exact files")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
