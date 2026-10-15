#!/usr/bin/env python3
"""Focused regressions for the local retail-asset materializer."""

from __future__ import annotations

import struct
import os
import tempfile
import unittest
import zlib
import json
import subprocess
from dataclasses import replace
from pathlib import Path
from types import SimpleNamespace
from unittest import mock

import materialize_retail_assets as materializer


def _chunk(kind: bytes, payload: bytes) -> bytes:
    crc = zlib.crc32(kind)
    crc = zlib.crc32(payload, crc) & 0xFFFFFFFF
    return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", crc)


def _png(
    width: int,
    height: int,
    *,
    idat: bytes | None = None,
    last_pixel: int = 0,
) -> bytes:
    signature = bytes((0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A))
    header = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)
    pixels = bytearray(b"".join(b"\0" + bytes(width * 3) for _ in range(height)))
    pixels[-1] = last_pixel
    return (
        signature
        + _chunk(b"IHDR", header)
        + _chunk(b"IDAT", zlib.compress(pixels) if idat is None else idat)
        + _chunk(b"IEND", b"")
    )


def _physics_definition_record(
    record_type: int,
    name: str,
    fields: tuple[tuple[int, bytes], ...],
) -> bytes:
    result = bytearray(struct.pack("<II", record_type, 0))
    result.extend(name.encode("ascii") + b"\0")
    for index, (field_id, value) in enumerate(fields):
        result.extend(struct.pack("<II", field_id, len(value)))
        result.extend(value)
        result.extend(struct.pack("<i", -1 if index + 1 == len(fields) else 0))
    return bytes(result)


def _world_initial_object(
    thing_type: int,
    *,
    position_bits: tuple[int, int, int] = (0, 0, 0),
    orientation_bits: tuple[int, int, int] = (0, 0, 0),
    mesh_number: int = 0,
    allegiance: int = 0,
    target: int = -1,
    script: str = "",
    name: str = "",
    spawn_script: str = "",
    active: int = 1,
    attach_scripts: int = 0,
    definition_name: str = "X",
    trailer: int = -1,
    plane_mode: int = 0,
    player_number: int = 1,
    amount: int = 1,
    mode: int = 0,
    delay_bits: int = 0,
    squad_delay_bits: int = 0,
    initial_delay_bits: int = 0,
    squad_size: int = 1,
    spawn_unit: str = "",
    spawner_spawn_script: str = "",
    radius_bits: int = 0x3F800000,
) -> bytes:
    result = bytearray(struct.pack("<i", thing_type))
    for bits in (*position_bits, *orientation_bits):
        result.extend(struct.pack("<I", bits))
    result.extend(struct.pack("<iii", mesh_number, allegiance, target))
    for value in (script, name, spawn_script):
        result.extend(value.encode("ascii") + b"\0")
    result.extend(struct.pack("<ii", active, attach_scripts))
    if thing_type == 8:
        encoded = definition_name.encode("ascii")
        result.extend(bytes((len(encoded),)) + encoded + struct.pack("<i", trailer))
    elif thing_type == 15:
        result.extend(struct.pack("<ii", plane_mode, player_number))
    elif thing_type in (18, 27):
        pass
    elif thing_type == 19:
        result.extend(struct.pack("<iIIIi", amount, delay_bits, squad_delay_bits,
                                  initial_delay_bits, squad_size))
        result.extend(spawn_unit.encode("ascii") + b"\0")
        result.extend(spawner_spawn_script.encode("ascii") + b"\0")
    elif thing_type == 28:
        result.extend(struct.pack("<ii", amount, mode))
        encoded = definition_name.encode("ascii")
        result.extend(bytes((len(encoded),)) + encoded + struct.pack("<i", trailer))
    elif thing_type == 36:
        result.extend(struct.pack("<I", radius_bits))
    return bytes(result)


def _world110_initial_object_fixture(
    *,
    header: tuple[int, int, int] = (2, 0, 40),
    start_plane_mode: int = 0,
    start_player_number: int = 1,
    start_position_bits: tuple[int, int, int] = (0x43846000, 0x43816800, 0x80000000),
    start_orientation_bits: tuple[int, int, int] = (0xBF04FD8B, 0, 0),
    extra_start: bool = False,
    unsupported_type: bool = False,
    tree_header: tuple[int, int] = (0, 2),
) -> tuple[materializer._WorldReader, tuple[materializer._WorldInitialObject, ...]]:
    payload = bytearray(15_709)
    payload.extend(struct.pack("<iiH", *header))
    payload.extend(_world_initial_object(27, script="LevelScript"))
    payload.extend(
        _world_initial_object(
            15,
            position_bits=start_position_bits,
            orientation_bits=start_orientation_bits,
            plane_mode=start_plane_mode,
            player_number=start_player_number,
        )
    )
    remaining_types = [8] * 10 + [18] * 19 + [19] + [27] * 2 + [28] * 5 + [36]
    if extra_start:
        remaining_types[0] = 15
    if unsupported_type:
        remaining_types[0] = 99
    for thing_type in remaining_types:
        payload.extend(_world_initial_object(thing_type))
    payload.extend(struct.pack("<Hi", *tree_header))
    reader = materializer._WorldReader(bytes(payload))
    reader.position = 15_709
    objects = materializer._parse_world_initial_objects(
        reader,
        110,
        materializer.WORLD110_INITIAL_OBJECT_HEADER,
        materializer.WORLD110_INITIAL_OBJECT_TYPE_COUNTS,
    )
    return reader, objects


class Level100FloatGeometryTests(unittest.TestCase):
    def test_actor_basis_transforms_offsets_in_core_coordinate_system(self):
        # Synthetic exact quarter turns, independent of the unresolved retail
        # Euler producer. A point rotated in retail must land at the same Core
        # position as its converted offset rotated by the converted basis.
        origin = [288.6875, 243.25, -10.0]
        for label, basis, rotated_offset in (
            ("pitch", [[1, 0, 0], [0, 0, -1], [0, 1, 0]], [2, -5, 3]),
            ("roll", [[0, 0, 1], [0, 1, 0], [-1, 0, 0]], [5, 3, -2]),
        ):
            with self.subTest(rotation=label):
                pose = materializer._actor_pose(origin, [0, 0, 0], basis)
                point = materializer._actor_pose(
                    [a + b for a, b in zip(origin, rotated_offset)], [0, 0, 0])
                floats = [struct.unpack("<f", struct.pack("<i", word))[0]
                          for word in pose["basisFloatBits"]]
                core_offset = [2000, -5000, 3000]  # retail local point (2,3,5)
                actual = [sum(floats[row * 3 + col] * core_offset[col]
                              for col in range(3)) for row in range(3)]
                self.assertEqual(point["positionMillimeters"], actual)

    def test_actor_basis_preserves_signed_permutation_words(self):
        pose = materializer._actor_pose([288.6875, 243.25, -10.0], [0, 0, 0],
            [[1.0, -0.0, 0.0], [-0.0, 1.0, -0.0], [0.0, -0.0, 1.0]])
        negative_zero = -0x80000000
        self.assertEqual([0x3f800000, negative_zero, negative_zero,
                          negative_zero, 0x3f800000, 0,
                          negative_zero, 0, 0x3f800000], pose["basisFloatBits"])

    def test_original_part_words_keep_padding_and_absent_caches(self):
        from cmsh_static_preview import _Transform, _BoundingBox, _RigidTrack
        pose = _Transform(((1.0, -0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)),
                          (-0.0, 2.0, 3.0), (0x7fc01234, 17, 19), 0xffffffff)
        cmsp = bytearray(0x13c)
        struct.pack_into("<I", cmsp, 0x88, 9)
        struct.pack_into("<II", cmsp, 0xa0, 7, 0xffffffff)
        struct.pack_into("<III", cmsp, 0x118, 1, 0, 1)
        source = SimpleNamespace(raw_cmsp=bytes(cmsp), part_type=6,
            reference=3, parent=2, children=(), nmic=None,
            bounding_box=_BoundingBox((-0.0, 0.0, 0.0), (0.0, 0.0, 0.0), 0, 0.0,
                                     (0x7fc01234, 0xffffffff)),
            track=_RigidTrack((0, 0), (pose,), b"", struct.pack("<4I", 1, 2, 3, 4)))
        actual = materializer._contact_part_float_geometry(source)
        self.assertEqual((9, 6, 3, 2), tuple(actual[k] for k in
                         ("sourceId", "sourceType", "reference", "parent")))
        self.assertEqual([0x80000000, 0, 0, 0x7fc01234, 0, 0, 0, 0xffffffff, 0, 0],
                         actual["boundingBoxWords"])
        self.assertEqual([0x80000000, 0x40000000, 0x40400000, 0xffffffff],
                         actual["hierarchyPositionWords"][0])
        self.assertEqual(0x7fc01234, actual["hierarchyOrientationWords"][0][3])
        self.assertEqual([1, 2, 3, 4], actual["cachedPositionWords"])
        self.assertIsNone(actual["cachedOrientationWords"])
        self.assertEqual([0, 0], actual["frameMap"])
        self.assertEqual(1, actual["cmsp118Word"])
        self.assertEqual(7, actual["numNmicWord"])
        self.assertEqual(0xffffffff, actual["isNmicWord"])
        preview = SimpleNamespace(transform=pose,
            bounding_box=_BoundingBox((1.0, 2.0, 3.0), (4.0, 5.0, 6.0), 1, 9.0),
            vertices=(), groups=(), name="referencing part", parent=2, reference=3, part_type=6)
        record = materializer._contact_part_records(SimpleNamespace(
            parts=(preview,), file_parts=lambda: (source,)))[0]
        self.assertEqual([4000, 5000, 6000], record["halfExtentsMillimeters"])
        self.assertEqual(actual, record["floatGeometry"])
        source.track = None
        missing = materializer._contact_part_float_geometry(source)
        for key in ("frameMap", "hierarchyPositionWords", "hierarchyOrientationWords",
                    "cachedPositionWords", "cachedOrientationWords"):
            self.assertIsNone(missing[key])

    def test_stored_float_words_are_preserved_without_instance_rounding(self):
        # A synthetic parser result keeps this tools suite independent of an
        # installed game. Core's materialized-catalog test checks all four real
        # meshes separately. Adjacent float words must not be recomputed.
        origin = [0xbba22500, 0x3ea56398, 0xbcd78710]
        box = b"BBOX\x28\0\0\0" + struct.pack(
            "<10I", *origin, 0, 0, 0, 0, 0, 0, 0x3fc26850)
        header = bytearray(0x174)
        struct.pack_into("<I", header, 0x164, 0x3fe9f832)
        parsed = SimpleNamespace(
            siblings=[SimpleNamespace(tag=b"BBOX", raw_payload=box)],
            raw_header=bytes(header))
        for behavior, scale in ((3, 0x3f4ccccd), (8, 0x3f800000), (9, 0x3f800000)):
            with self.subTest(behavior=behavior):
                self.assertEqual({
                    "boundingBoxOriginFloatBits": origin,
                    "boundingBoxRadiusFloatBits": 0x3fc26850,
                    "meshRenderRadiusFloatBits": 0x3fe9f832,
                    "primaryRadiusScaleFloatBits": scale,
                }, materializer._level100_definition_float_geometry(parsed, behavior))
        with self.assertRaisesRegex(RuntimeError, "unadmitted"):
            materializer._level100_definition_float_geometry(parsed, 999)
        with self.assertRaisesRegex(RuntimeError, "bounding box"):
            materializer._level100_definition_float_geometry(
                SimpleNamespace(siblings=[], raw_header=parsed.raw_header), 3)


class ExplicitTreeGroupTests(unittest.TestCase):
    @staticmethod
    def table(first=None) -> bytes:
        rows = first if first is not None else [(0x80000000, 0, 3), (0x80000000, 0, 3)]
        payload = struct.pack("<Hi", 0, 2)
        for name, placements in ((b"pinesnow", rows), (b"fernsnow", [(0, 0x80000000, 1)])):
            payload += struct.pack("<B", len(name)) + name + struct.pack("<i", len(placements))
            payload += b"".join(struct.pack("<IIi", *row) for row in placements)
        return payload

    def test_order_duplicates_signed_zero_and_exact_boundaries_survive(self):
        payload = self.table()
        reader = materializer._WorldReader(payload + b"next section")
        table = materializer._read_explicit_tree_groups(reader)
        self.assertEqual((0, len(payload)), (table.header_offset, table.end_offset))
        self.assertEqual(["pinesnow", "fernsnow"], [group.name for group in table.groups])
        first, second = table.groups
        self.assertEqual(((-2147483648, 0, 3),) * 2, first.placements)
        self.assertEqual(((0, -2147483648, 1),), second.placements)
        self.assertEqual((6, 19, 43, 56),
                         (first.header_offset, first.records_offset,
                          second.header_offset, second.records_offset))
        self.assertEqual(materializer._sha256(payload[19:43]), first.records_sha256)
        self.assertEqual(b"next section", reader.data[reader.position:])

    def test_invalid_variant_and_nonfinite_position_are_rejected(self):
        for row, reason in (((0, 0, 4), "tree variant"),
                            ((0, 0, -1), "tree variant"),
                            ((0x7F800000, 0, 0), "non-finite")):
            with self.subTest(row=row), self.assertRaisesRegex(RuntimeError, reason):
                materializer._read_explicit_tree_groups(materializer._WorldReader(self.table([row])))

    def test_truncated_table_is_rejected(self):
        payload = self.table()
        for end in (5, 18, 30, len(payload) - 1):
            with self.subTest(end=end), self.assertRaisesRegex(RuntimeError, "truncated"):
                materializer._read_explicit_tree_groups(materializer._WorldReader(payload[:end]))


class World110InitialActorMaterializationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        # Local retail inputs are deliberately not fixtures or public payloads.
        try:
            game = materializer._resolve_game_root(None)
        except RuntimeError as error:
            raise unittest.SkipTest("World110 tests require local retail inputs") from error
        import sys
        sys.path.insert(0, str(materializer.ROOT / "tools"))
        from aya_archive_inventory import inflate_aya_bytes
        cls.raw100 = inflate_aya_bytes(materializer._read_exact(
            game / materializer.LEVEL_ARCHIVE, materializer.LEVEL_ARCHIVE_SHA256))
        cls.raw_world = inflate_aya_bytes(materializer._read_exact(
            game / materializer.WORLD110_ARCHIVE,
            materializer.WORLD110_ARCHIVE_SHA256,
        ))
        cls.physics = materializer._read_exact(
            game / materializer.PHYSICS_DEFINITIONS,
            materializer.PHYSICS_DEFINITIONS_SHA256,
        )
        cls.configurations = (game / materializer.BATTLE_ENGINE_CONFIGURATIONS).read_bytes()
        cls.pine_meshes = materializer._read_pine_meshes(game)
        cls.initial_meshes = tuple(materializer._read_exact(game / path, digest)
            for _, path, digest, _ in materializer.WORLD110_INITIAL_MESHES)
        cls.landing_craft_mesh = materializer._read_exact(
            game / materializer.WORLD110_LANDING_CRAFT_MESH,
            materializer.WORLD110_LANDING_CRAFT_MESH_SHA256)

    def test_real110_static_world_reproduces_its_pin_and_the_construction_contract(self) -> None:
        data = materializer._world110_static_world_bytes(self.raw_world, self.physics, self.landing_craft_mesh)
        self.assertEqual(materializer.WORLD110_STATIC_WORLD_SHA256, materializer._sha256(data))
        self.assertEqual(data, materializer._world110_static_world_bytes(
            self.raw_world, self.physics, self.landing_craft_mesh))
        self.assertIn((materializer.LEVEL110_STATIC_WORLD, materializer.WORLD110_STATIC_WORLD_SHA256),
                      materializer._fixed_outputs())
        document = json.loads(data)
        self.assertEqual(("onslaught.world110-static-world.v2", 110),
                         (document["schema"], document["worldNumber"]))
        actors = {actor["definitionIdentity"]: actor for actor in document["actorDefinitions"]}
        self.assertEqual(77, len(actors))
        self.assertEqual({"Level Actor Type 28"}, {actors[f"wres:rlwd:{row:04d}"]["definitionName"]
                                                   for row in (14, 16, 17, 18, 19)})
        self.assertEqual("Scout", actors["wres:rlwd:0019"]["scriptName"])
        # The shared base world reads exactly as Level 100's.
        base100, _ = materializer._parse_static_world_inputs(self.raw100)
        self.assertEqual(
            [(f"wres:bswd:{int(item['ordinal']):04d}", item["name"], item["definition"]) for item in base100],
            [(identity, actor["name"], actor["definitionName"])
             for identity, actor in actors.items() if identity.startswith("wres:bswd:")])
        self.assertEqual(150_000, actors["wres:bswd:0013"]["initialHealth"])
        self.assertEqual(("Player 1", "BattleEngine", 8),
                         (actors["wres:rlwd:0001"]["name"], actors["wres:rlwd:0001"]["definitionName"],
                          actors["wres:rlwd:0001"]["thingTypeMask"]))
        self.assertFalse(actors["wres:rlwd:0005"]["active"])
        squads = {squad["definitionIdentity"]: squad for squad in document["squads"]}
        self.assertEqual({14: 5, 16: 5, 17: 3, 18: 5, 19: 4},
                         {int(identity[-4:]): len(squad["memberIdentities"]) for identity, squad in squads.items()})
        self.assertEqual({14: 1, 16: 1, 17: 0, 18: 1, 19: 0},
                         {int(identity[-4:]): squad["allegiance"] for identity, squad in squads.items()})
        self.assertEqual("Scout", squads["wres:rlwd:0019"]["scriptName"])
        self.assertTrue(all(actors[member]["scriptName"] is None
                            for squad in squads.values() for member in squad["memberIdentities"]))
        self.assertEqual(["wres:rlwd:0008", "wres:rlwd:0012", "wres:rlwd:0013", "wres:rlwd:0020"],
                         [component["parentIdentity"] for component in document["components"]])
        paths = {path["name"]: path for path in document["waypointPaths"]}
        lander = paths["Lander Path 1"]
        self.assertEqual([8], lander["droppedNodeIndices"])
        targets = {point["nodeIndex"]: point["targetNodeIndex"] for point in lander["points"]}
        self.assertEqual((11, 15, None), (targets[10], targets[11], targets[15]))
        self.assertEqual((22, 24, None), (targets[21], targets[22], targets[24]))
        self.assertEqual([25], paths["Fighter Path 1"]["droppedNodeIndices"])
        self.assertEqual([5], paths["Fighter Path 2"]["droppedNodeIndices"])
        self.assertEqual(0x40000000, document["settings"]["panLengthFloatBits"])
        self.assertEqual({"Dropship", "GroundVehicle", "Plane"},
                         {row["motionClass"] for row in document["motionDefinitions"]})
        # The landing craft are Big, with the lander mesh's radius 13.585.
        self.assertEqual(
            {"Muspell Light Landing Craft": (0x41000000, 0x3C8EFA35, True, 0x41595C52),
             "Muspell Light Landing Empty": (0x40E00000, 0x3C8EFA35, True, 0x41595C52),
             "Muspell Fighter": (0x41200000, 0x3CE4C388, False, None),
             "Muspell Light Fighter": (0x41000000, 0x3CC82B17, False, None)},
            {row["definitionName"]: (row["airVelocityFloatBits"], row["airTurnRateFloatBits"],
                                     row["big"], row["meshRadiusFloatBits"])
             for row in document["motionDefinitions"] if row["motionClass"] != "GroundVehicle"})
        self.assertTrue(all(row["minimumAltitudeFloatBits"] == 0x40800000
                            for row in document["motionDefinitions"] if row["motionClass"] != "GroundVehicle"))
        # The level world has no CSafeSide; the base world's two are its list.
        self.assertEqual([("base", 21, 1, [235.0, 504.0, -0.0]), ("base", 22, 1, [288.0, 9.0, -0.0])],
                         [(side["world"], side["row"], side["allegiance"],
                           list(struct.unpack("<3f", struct.pack("<3i", *side["retailPositionFloatBits"]))))
                          for side in document["safeSides"]])
        self.assertEqual(1_481, document["pineInstanceCount"])

    def test_real100_safe_sides_are_the_base_worlds_then_the_levels(self) -> None:
        level_actors, _ = materializer._parse_level_world_actors_and_waypoints(self.raw100)
        self.assertEqual(
            [("base", 21, 1, [235.0, 504.0, -0.0]), ("base", 22, 1, [288.0, 9.0, -0.0]),
             ("level", 20, 0, [501.0, 296.0, -0.0]), ("level", 39, 0, [207.0, 503.5, -0.0])],
            [(side["world"], side["row"], side["allegiance"],
              list(struct.unpack("<3f", struct.pack("<3i", *side["retailPositionFloatBits"]))))
             for side in materializer._level100_safe_sides(self.raw100, level_actors)])

    def test_real110_player_inputs_use_rlwd_names_and_exact_configuration_fields(self) -> None:
        data = materializer._world110_player_input_bytes(self.raw_world, self.configurations)
        self.assertEqual(materializer.WORLD110_PLAYER_INPUTS_SHA256, materializer._sha256(data))
        document = json.loads(data)
        self.assertEqual(["Aquila Prototype"], document["configurationNames"])
        self.assertEqual(6, len(document["records"]))
        self.assertEqual("Aquila Prototype", document["records"][3]["name"])
        self.assertEqual(0x41A00000, document["records"][3]["lifeBits"])
        self.assertEqual(0x41000000, document["records"][3]["energyBits"])
        self.assertIn((materializer.LEVEL110_PLAYER_INPUTS,
                       materializer.WORLD110_PLAYER_INPUTS_SHA256), materializer._fixed_outputs())

    def test_changed_configuration_source_is_rejected(self) -> None:
        changed = bytearray(self.configurations)
        changed[8] ^= 1
        with self.assertRaises(ValueError):
            materializer._world110_player_input_bytes(self.raw_world, bytes(changed))

    def test_real110_artifact_reproduces_and_contains_its_own_unit_rows(self) -> None:
        data = materializer._world110_initial_actor_bytes(
            self.raw_world, self.physics, self.landing_craft_mesh, self.pine_meshes, self.initial_meshes)
        self.assertEqual(materializer.WORLD110_INITIAL_ACTORS_SHA256,
                         materializer._sha256(data))
        document = json.loads(data)
        self.assertEqual(110, document["worldNumber"])
        self.assertEqual(43, len(document["rows"]))
        units = [row["actor"] for row in document["rows"] if not row["actor"]["isStatic"]]
        self.assertEqual([8, 12, 13, 20, 25, 34, 35, 36, 37, 38],
                         [int(row["definitionIdentity"][-4:]) for row in units])
        self.assertEqual([""] * 10, [row["name"] for row in units])
        self.assertEqual("Muspell Light Landing Empty", units[2]["definitionName"])
        self.assertEqual(4_000, units[4]["initialHealth"])
        self.assertIn((materializer.LEVEL110_INITIAL_ACTORS,
                       materializer.WORLD110_INITIAL_ACTORS_SHA256),
                      materializer._fixed_outputs())

    def test_real110_names_do_not_change_legacy_static_world_projection(self) -> None:
        legacy, _, _ = materializer._parse_static_world(self.raw_world)
        exact, _, _ = materializer._parse_static_world(
            self.raw_world, preserve_serialized_names=True)
        self.assertEqual("Iceberg 1", legacy[4]["name"])
        self.assertEqual("", exact[4]["name"])
        self.assertEqual("Tank Factory", exact[1]["name"])
        self.assertEqual(legacy, materializer._parse_static_world(self.raw_world)[0])

    def test_control_tower_keeps_source_parts_and_duplicate_emitter_bindings(self):
        mesh = materializer._world110_initial_mesh(self.initial_meshes[0], 0)
        self.assertEqual(39, len(mesh["parts"]))
        self.assertEqual(["CORE01", "CORE02", "core4", "CORE03", "CORE05", "CORE06", "core7", "CORE08"],
                         [part["name"] for part in mesh["parts"] if part["name"].startswith(("core", "CORE"))])
        self.assertTrue(all(part["numNmic"] == part["isNmic"] == 0 for part in mesh["parts"]))
        self.assertEqual(["_Fenrir Flame Effect"] * 10, [entry["name"] for entry in mesh["emitters"]])
        self.assertEqual([0] * 10, [entry["selector"] for entry in mesh["emitters"]])
        self.assertEqual([7,26,27,28,29,34,35,36,37,38], [entry["partOrdinal"] for entry in mesh["emitters"]])
        self.assertEqual(0x416d5d81, mesh["meshRadiusFloatBits"])
        self.assertEqual([0x20202020, 0x20202020], [mesh["globalBoundingBoxWords"][i] for i in (3,7)])
        changed = bytearray(self.initial_meshes[0])
        changed[-1] ^= 1
        with self.assertRaisesRegex(RuntimeError, "initial mesh identity"):
            materializer._world110_initial_mesh(bytes(changed), 0)

    def test_three_initial_meshes_and_raw_unit_uses_preserve_source_order(self):
        document = json.loads(materializer._world110_initial_actor_bytes(
            self.raw_world, self.physics, self.landing_craft_mesh, self.pine_meshes, self.initial_meshes))
        self.assertEqual("onslaught.world110-initial-actors.v7", document["schema"])
        self.assertNotIn("controlTowerMesh", document)
        meshes = document["initialMeshes"]
        self.assertEqual(["fb_control_tower.msh", "fb_tank_factory.msh", "fb_health_pad.msh"],
                         [mesh["meshName"] for mesh in meshes[:3]])
        self.assertEqual([39, 29, 20], [len(mesh["parts"]) for mesh in meshes[:3]])
        self.assertEqual([5, 6, 8, 9, 11, 12, 13, 14, 15, 16],
                         [binding["partOrdinal"] for binding in meshes[1]["emitters"]])
        self.assertEqual(["_Health Pad Mesh FOR", "GunB"],
                         [binding["name"] for binding in meshes[2]["emitters"]])
        self.assertEqual([11, 12], [binding["partOrdinal"] for binding in meshes[2]["emitters"]])
        self.assertEqual([
            {"actorDefinitionIdentity": "wres:bswd:0000", "weaponUses": [], "spawnerUses": []},
            {"actorDefinitionIdentity": "wres:bswd:0001", "weaponUses": [], "spawnerUses": [
                {"definitionName": "Sabre Factory Spawner", "tagName": "SpawnerA",
                 "rawCreationFlags": 0x24100}]},
            {"actorDefinitionIdentity": "wres:bswd:0002", "spawnerUses": [], "weaponUses": [
                {"definitionName": "Repair Pad", "tagName": "GunB", "rawCreationFlags": 8}]},
        ], document["initialUnitUses"][:3])
        for index, source in enumerate(self.initial_meshes):
            changed = bytearray(source)
            changed[-1] ^= 1
            with self.subTest(index=index), self.assertRaisesRegex(RuntimeError, "initial mesh identity"):
                materializer._world110_initial_mesh(bytes(changed), index)
        with self.assertRaisesRegex(RuntimeError, "initial mesh count"):
            materializer._world110_initial_actor_bytes(
                self.raw_world, self.physics, self.landing_craft_mesh, self.pine_meshes,
                self.initial_meshes[:2])

    def test_building_definitions_resolve_authored_modes_and_spawner_target(self):
        document = json.loads(materializer._world110_initial_actor_bytes(
            self.raw_world, self.physics, self.landing_craft_mesh, self.pine_meshes, self.initial_meshes))
        weapon = document["weaponDefinitions"][0]
        self.assertEqual(("Repair Pad", 13), (weapon["definitionName"], weapon["typeOrdinal"]))
        self.assertEqual([11, -1, -1, -1, -1], weapon["chargeSlots"])
        self.assertEqual((0x40000000, 0x3f800000, 0, 0, 0),
                         tuple(weapon[key] for key in ("chargeRateFloatBits", "consumptionFloatBits",
                                                       "ammoStore", "zoomMode", "adjustAimWord")))
        mode = weapon["selectedMode"]
        self.assertEqual(("Repair Pad", 11), (mode["definitionName"], mode["typeOrdinal"]))
        self.assertEqual([1, 3, 2, 11, 12, 10, 13, 18, 6, 30, 34, 24],
                         [field["fieldId"] for field in mode["fields"]])
        stream = materializer._physics_record_stream(self.physics)
        actual_mode = [record for record in stream if record.record_type == 3][mode["typeOrdinal"]]
        self.assertEqual(actual_mode.fields,
                         tuple((field["fieldId"], bytes.fromhex(field["rawHex"])) for field in mode["fields"]))
        spawner, = document["spawnerDefinitions"]
        self.assertEqual("AV-14B Sabre Pulse Tank", spawner["targetUnitDefinitionName"])
        self.assertEqual(2, spawner["targetUnitBehaviourSelector"])
        self.assertEqual([1, 3, 2, 12, 11, 7], [field["fieldId"] for field in spawner["fields"]])

    def test_sat_and_features_keep_animation_modes_caches_and_actual_profiles(self):
        from cmsh_static_preview import inflate_aya, parse_cmsh_stream
        document = json.loads(materializer._world110_initial_actor_bytes(
            self.raw_world, self.physics, self.landing_craft_mesh, self.pine_meshes, self.initial_meshes))
        meshes = document["initialMeshes"]
        self.assertEqual(["ft_sam.msh", "iceberg1.msh", "iceberg2.msh", "iceberg3.msh", "iceberg4.msh"],
                         [mesh["meshName"] for mesh in meshes[3:]])
        self.assertEqual([181, 46, 101, 21, 101, 101, 101, 101],
                         [mesh["frameCount"] for mesh in meshes])
        self.assertEqual([0, 0, 0, 4, 0, 0, 0, 0], [mesh["numberOfAnimations"] for mesh in meshes])
        self.assertEqual([1000, 1001, 1002, 1003], [animation["modeId"] for animation in meshes[3]["animations"]])
        self.assertEqual({"name": "Inactive", "sourceOrdinal": 3, "modeId": 1003,
                          "startFrame": 0, "endFrame": 0, "frameDelta": 0, "incrementFloatBits": 0},
                         meshes[3]["animations"][3])
        self.assertEqual([8, 2, 6, 5, 4, 3, 7, 1],
                         [binding["selector"] for binding in meshes[3]["emitters"][:8]])
        for source, mesh in zip(self.initial_meshes, meshes):
            parsed = parse_cmsh_stream(inflate_aya(source))
            for original, part in zip(parsed.file_parts(), mesh["parts"]):
                for key, raw in (("cachedPositionWords", original.track.cached_position_bytes),
                                 ("cachedOrientationWords", original.track.cached_orientation_bytes)):
                    self.assertEqual(raw, struct.pack(f"<{len(part[key])}i", *part[key]))
        self.assertEqual((84, 252), tuple(len(meshes[3]["parts"][1][key]) for key in
                                         ("cachedPositionWords", "cachedOrientationWords")))
        self.assertEqual({"actorDefinitionIdentity": "wres:bswd:0003", "spawnerUses": [],
                          "weaponUses": [{"definitionName": "SAT Launcher", "tagName": "GunA",
                                          "rawCreationFlags": 1024}]}, document["initialUnitUses"][3])
        ground, = document["groundUnitDefinitions"]
        self.assertEqual(("SAT Turret", 0, 0),
                         (ground["definitionName"], ground["fieldC8FloatBits"], ground["fieldD0FloatBits"]))
        self.assertEqual([11, 9, 8, 47, 3, 23, 7, 21, 10],
                         [field["fieldId"] for field in ground["fields"]])
        weapon = document["weaponDefinitions"][1]
        self.assertEqual(("SAT Launcher", 88, 1), (weapon["definitionName"], weapon["typeOrdinal"], weapon["adjustAimWord"]))
        self.assertEqual([50, -1, -1, -1, -1], weapon["chargeSlots"])
        self.assertEqual(8, sum(field["fieldId"] == 8 for field in weapon["selectedMode"]["fields"]))
        self.assertEqual(["Iceberg 1", "Iceberg 2", "Iceberg 3", "Iceberg 4"],
                         [profile["definitionName"] for profile in document["featureDefinitions"]])
        self.assertTrue(all(profile["invincibleWord"] == 1 and profile["profileWord18"] == 0
                            for profile in document["featureDefinitions"]))

    def test_real110_tree_tables_retain_repeated_data_and_retail_skip_branches(self):
        document = json.loads(materializer._world110_initial_actor_bytes(
            self.raw_world, self.physics, self.landing_craft_mesh, self.pine_meshes, self.initial_meshes))
        base, level = document["treeTables"]
        self.assertEqual(["BSWD", "RLWD"], [base["sourceChunk"], level["sourceChunk"]])
        self.assertEqual((2709, 29549, 18327, 45167),
                         (base["headerOffset"], base["endOffset"],
                          level["headerOffset"], level["endOffset"]))
        self.assertEqual([False, True, False, False],
                         [group["callsTreeInit"] for table in (base, level) for group in table["groups"]])
        for index, (count, digest) in enumerate(((753,
                "c6b83ebfacf563f04294decfd1d5879726895bbd33fb23f2164b01c391117372"),
                (1481, "c4308e46dad3b687051eb9c6e4650f923133d713f92401f3db997a6fa28bae59"))):
            self.assertEqual(count, len(base["groups"][index]["placements"]))
            self.assertEqual(digest, base["groups"][index]["recordsSha256"])
            self.assertEqual(base["groups"][index]["placements"], level["groups"][index]["placements"])
        # The old rendering/shadow projection still supplies one base pine set.
        self.assertEqual(materializer._parse_static_world(self.raw100)[1:],
                         materializer._parse_static_world(self.raw_world)[1:])

    def test_tree_mesh_inputs_preserve_header_radius_and_all_global_bbox_words(self):
        from cmsh_static_preview import inflate_aya
        meshes = materializer._world110_tree_mesh_inputs(self.pine_meshes)
        self.assertEqual([0x4005575c, 0x4007f5c1, 0x400cea52, 0x40054422],
                         [mesh["meshRadiusFloatBits"] for mesh in meshes])
        for variant, (source, mesh) in enumerate(zip(self.pine_meshes, meshes)):
            raw = inflate_aya(source)
            self.assertEqual(raw[-40:], struct.pack("<10i", *mesh["globalBoundingBoxWords"]))
            self.assertEqual(f"pinesnow{variant}.MSH", mesh["meshName"])
            self.assertEqual(materializer.PINE_MESH_SHA256[variant], mesh["sourceSha256"])
            self.assertEqual(list(struct.unpack_from("<3f", raw, len(raw) - 40)),
                             materializer._pine_global_center(source, inflate_aya, variant))
            # The cylinder half-height uses header radius, not BBOX radius.
            self.assertNotEqual(mesh["meshRadiusFloatBits"], mesh["globalBoundingBoxWords"][9])
        changed = list(self.pine_meshes)
        changed[2] = changed[0]
        with self.assertRaisesRegex(RuntimeError, "pine mesh identity changed: 2"):
            materializer._world110_tree_mesh_inputs(tuple(changed))

    def test_level100_rlwd_variant_is_checked_by_the_shared_reader(self):
        tables = []
        read = materializer._read_explicit_tree_groups
        def capture(reader):
            table = read(reader)
            tables.append(table)
            return table
        with mock.patch.object(materializer, "_read_explicit_tree_groups", side_effect=capture):
            materializer._parse_level_world_actors_and_waypoints(self.raw100)
        self.assertEqual(1, len(tables))
        wrld = materializer._chunk_payload(materializer._chunk_payload(self.raw100, b"WRES"), b"WRLD")
        rlwd = materializer._chunk_payload(wrld, b"RLWD")
        changed = bytearray(rlwd)
        struct.pack_into("<i", changed, tables[0].groups[0].records_offset + 8, 4)
        self.assertEqual(1, self.raw100.count(rlwd))
        with self.assertRaisesRegex(RuntimeError, "tree variant"):
            materializer._parse_level_world_actors_and_waypoints(self.raw100.replace(rlwd, bytes(changed), 1))

    def test_real110_component_queries_keep_mesh_cache_and_constructor_zero_signs(self) -> None:
        document = json.loads(materializer._world110_initial_actor_bytes(
            self.raw_world, self.physics, self.landing_craft_mesh, self.pine_meshes, self.initial_meshes))
        attachment = document["componentAttachment"]
        self.assertEqual((20, 1, 31),
                         (attachment["emitterTag"], attachment["selector"], attachment["partOrdinal"]))
        self.assertEqual([0x3D49079B, 0x412496BD, -1067183804],
                         attachment["localPositionFloatBits"])
        self.assertEqual(["wres:rlwd:0008", "wres:rlwd:0012", "wres:rlwd:0013", "wres:rlwd:0020"],
                         [use["parentDefinitionIdentity"] for use in attachment["uses"]])
        for index, use in enumerate(attachment["uses"]):
            self.assertEqual("Dropship Gun Turret", use["componentDefinitionName"])
            basis = use["parentInitBasisFloatBits"]
            self.assertEqual(-2147483648, basis[6])
            self.assertEqual(0 if index == 2 else -2147483648, basis[2])
        # Native x87 FCOS/FSIN stores for yaw40782696 under explicit027f.
        self.assertEqual([-1086470375, -1087647576],
                         [attachment["uses"][1]["parentInitBasisFloatBits"][i] for i in (0, 3)])

    def test_changed_component_mesh_is_rejected(self) -> None:
        with self.assertRaisesRegex(RuntimeError, "mesh identity changed"):
            materializer._world110_initial_actor_bytes(self.raw_world, self.physics, b"wrong mesh",
                                                       self.pine_meshes, self.initial_meshes)

    def test_changed_physics_is_rejected_before_construction(self) -> None:
        with self.assertRaisesRegex(RuntimeError, "physics source identity"):
            materializer._world110_initial_actor_bytes(
                self.raw_world, b"wrong physics", self.landing_craft_mesh, self.pine_meshes, self.initial_meshes)


class MeshEmitterMaterializationTests(unittest.TestCase):
    def test_unbound_non_spawner_is_skipped_without_consuming_a_part(self):
        from cmsh_static_preview import MeshEmitterBinding
        parsed = SimpleNamespace(parts=(), emitter_bindings=lambda: (
            MeshEmitterBinding("Component", -1, None),))
        self.assertEqual({}, materializer._mesh_emitters(parsed))

    def test_unbound_spawner_is_not_replaced_with_a_default_pose(self):
        from cmsh_static_preview import MeshEmitterBinding
        parsed = SimpleNamespace(parts=(), emitter_bindings=lambda: (
            MeshEmitterBinding("SpawnerA", 1, None),))
        with self.assertRaisesRegex(RuntimeError, "has no mesh part"):
            materializer._mesh_emitters(parsed)


class AirfieldExitMaterializationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        try:
            game = materializer._resolve_game_root(None)
        except RuntimeError as error:
            raise unittest.SkipTest("Airfield exit tests require local retail input") from error
        from cmsh_static_preview import inflate_aya, parse_cmsh_stream
        data = materializer._read_exact(
            game / "data/resources/meshes/m_fb_aircraft_factory.msh.aya",
            "23219fc98eba73c19c83b3ae07ea92fa750d8f71362ccedfc1bfdec474629899")
        cls.parsed = parse_cmsh_stream(inflate_aya(data))

    def with_bindings(self, bindings):
        return SimpleNamespace(file_parts=self.parsed.file_parts, siblings=self.parsed.siblings,
                               emitter_bindings=lambda: tuple(bindings))

    def test_real_exit_points_keep_selectors_and_differ_from_launch_emitters(self):
        paths = materializer._airfield_exit_waypoints(self.parsed)
        launches = materializer._mesh_emitters(self.parsed)
        for key, words in (("SpawnerA", (0xbd300b66, 0xc19bb698, 0xc0c3f46a)),
                           ("SpawnerB", (0xbe164340, 0x4199149c, 0xc0c3f46a))):
            self.assertEqual([1], [point["selector"] for point in paths[key]])
            point = paths[key][0]["modelTransform"]
            self.assertEqual(list(words), [value & 0xffffffff for value in point["localPositionFloatBits"]])
            self.assertEqual([0x3f800000,0,0,0,0x3f800000,0,0,0,0x3f800000], point["localBasisFloatBits"])
            self.assertNotEqual([materializer._float_bits(value) for value in launches[key]["position"]],
                                point["localPositionFloatBits"])

    def test_case_insensitive_query_matches_the_shipped_lowercase_names(self):
        bindings = [replace(item, name=item.name.upper()) for item in self.parsed.emitter_bindings()]
        self.assertEqual(materializer._airfield_exit_waypoints(self.parsed),
                         materializer._airfield_exit_waypoints(self.with_bindings(bindings)))

    def test_changed_or_ambiguous_selector_is_not_a_list_index(self):
        bindings = self.parsed.emitter_bindings()
        chosen = next(item for item in bindings if item.name.lower() == "waypointa")
        for changed, message in (
            ([replace(item, selector=2) if item == chosen else item for item in bindings], "selector changed"),
            ([*bindings, replace(chosen, name="WaypointA")], "ambiguous"),
            ([item for item in bindings if item != chosen], "incomplete"),
            ([replace(item, part_ordinal=None) if item == chosen else item for item in bindings], "no mesh part"),
        ):
            with self.subTest(message=message), self.assertRaisesRegex(RuntimeError, message):
                materializer._airfield_exit_waypoints(self.with_bindings(changed))

    def test_changing_an_exit_ancestor_cannot_be_flattened_to_frame_zero(self):
        parts = list(self.parsed.file_parts())
        parts[13] = replace(parts[13], track=replace(parts[13].track, frame_map=(1,) * 101))
        changed = SimpleNamespace(file_parts=lambda: parts, siblings=self.parsed.siblings,
                                  emitter_bindings=self.parsed.emitter_bindings)
        with self.assertRaisesRegex(RuntimeError, "constant pose"):
            materializer._airfield_exit_waypoints(changed)

    def test_changed_cached_position_is_not_replaced_by_preview_geometry(self):
        parts = list(self.parsed.file_parts())
        raw = bytearray(parts[18].track.cached_position_bytes)
        raw[0] ^= 1
        parts[18] = replace(parts[18], track=replace(parts[18].track, cached_position_bytes=bytes(raw)))
        changed = SimpleNamespace(file_parts=lambda: parts, siblings=self.parsed.siblings,
                                  emitter_bindings=self.parsed.emitter_bindings)
        with self.assertRaisesRegex(RuntimeError, "disagrees with its cache"):
            materializer._airfield_exit_waypoints(changed)

    def test_missing_orientation_requires_the_declared_inheritance_flag(self):
        parts = list(self.parsed.file_parts())
        raw = bytearray(parts[18].raw_cmsp)
        struct.pack_into("<I", raw, 0x120, 0)
        parts[18] = replace(parts[18], raw_cmsp=bytes(raw))
        changed = SimpleNamespace(file_parts=lambda: parts, siblings=self.parsed.siblings,
                                  emitter_bindings=self.parsed.emitter_bindings)
        with self.assertRaisesRegex(RuntimeError, "model-pose cache changed"):
            materializer._airfield_exit_waypoints(changed)


class AircraftWeaponMountMaterializationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        try:
            game = materializer._resolve_game_root(None)
        except RuntimeError as error:
            raise unittest.SkipTest("Aircraft mount tests require local retail input") from error
        from cmsh_static_preview import inflate_aya, parse_cmsh_stream
        data = materializer._read_exact(
            game / "data/resources/meshes/m_FA_F24_training.msh.aya",
            "48876552ae836750221241719f333fb9b5221f78f1ab8bc03d5950cdbf4e6ec5")
        cls.parsed = parse_cmsh_stream(inflate_aya(data))
        cls.physics = materializer._physics_records(materializer._read_exact(
            game / materializer.PHYSICS_DEFINITIONS, materializer.PHYSICS_DEFINITIONS_SHA256))
        cls.fields = materializer._physics_record(cls.physics, 1, "Target Drone")
        _, lifter, lifter_hash = next(item for item in materializer.DIRECT_ASSETS
                                      if item[1] == "data/resources/meshes/m_f_lifter.msh.aya")
        cls.transporter_radius_bits = materializer._cmsh_radius_bits(
            inflate_aya(materializer._read_exact(game / lifter, lifter_hash)))

    def changed_mesh(self, *, bindings=None, parts=None):
        return SimpleNamespace(
            file_parts=self.parsed.file_parts if parts is None else lambda: parts,
            emitter_bindings=self.parsed.emitter_bindings if bindings is None else lambda: bindings,
            siblings=self.parsed.siblings)

    def test_selected_model_words_and_ordered_uses_reach_motion_definitions(self):
        rows = materializer._level100_actor_motion_definitions(
            self.physics, self.parsed, self.transporter_radius_bits)
        self.assertEqual(5, len(rows))
        # The air units' flight scalars; only the U-17 is Big, with the
        # lifter mesh's radius 7.6559.
        self.assertEqual(
            {"Air Trainer": (0x41133333, 0x3D32B8C2, False, None),
             "Target Drone": (0x40B00000, 0x3D32B8C2, False, None),
             "U-17 Highside Transporter": (0x40A00000, 0x3BE4C388, True, 0x40F4FCFE)},
            {row["definitionName"]: (row["airVelocityFloatBits"], row["airTurnRateFloatBits"],
                                     row["big"], row["meshRadiusFloatBits"])
             for row in rows if row["motionClass"] != "GroundVehicle"})
        self.assertTrue(all(row["minimumAltitudeFloatBits"] == 0x40800000
                            for row in rows if row["motionClass"] != "GroundVehicle"))
        trainer, drone = [row["weaponMounts"] for row in rows if "weaponMounts" in row]
        self.assertEqual(["Forseti Missile Trainer Launcher"], [m["use"]["definitionName"] for m in trainer])
        self.assertEqual(["Drone Vulcan Cannon", "Forseti Drone Missile Launcher"],
                         [m["use"]["definitionName"] for m in drone])
        self.assertEqual(["GunA", "GunB"], [m["use"]["tagName"] for m in drone])
        for mount, words in zip(drone, ((0xbd60ceb4, 0x3f6a3a59, 0xbd4d3bb0),
                                       (0xbb7ae95a, 0x3f5b84b4, 0x3d88c1bf))):
            self.assertEqual(1, mount["selector"])
            self.assertEqual(0x20400, mount["use"]["rawCreationFlags"])
            pose = mount["modelTransform"]
            self.assertEqual(list(words), [v & 0xffffffff for v in pose["localPositionFloatBits"]])
            self.assertEqual([0x3f800000, 0xa818719e, 0, 0x2818719e, 0x3f800000, 0, 0, 0, 0x3f800000],
                             [v & 0xffffffff for v in pose["localBasisFloatBits"]])
        self.assertEqual(trainer[0]["modelTransform"], drone[1]["modelTransform"])

    def test_exact_selector_allows_the_legitimate_earlier_guna2_and_case_variants(self):
        bindings = self.parsed.emitter_bindings()
        guns = [b for b in bindings if b.name.casefold() == "guna"]
        self.assertEqual([2, 1], [b.selector for b in guns])
        changed = [replace(b, name=b.name.upper()) for b in reversed(bindings)]
        self.assertEqual(materializer._aircraft_weapon_mounts(self.parsed, self.fields),
                         materializer._aircraft_weapon_mounts(self.changed_mesh(bindings=changed), self.fields))

    def test_missing_ambiguous_and_unbound_selector1_are_rejected(self):
        bindings = self.parsed.emitter_bindings()
        chosen = next(b for b in bindings if b.name == "GunA" and b.selector == 1)
        for changed, message in (
            ([b for b in bindings if b != chosen], "missing or ambiguous"),
            ([*bindings, replace(chosen, name="GUNA")], "missing or ambiguous"),
            ([replace(b, part_ordinal=None) if b == chosen else b for b in bindings], "no mesh part"),
        ):
            with self.subTest(message=message), self.assertRaisesRegex(RuntimeError, message):
                materializer._aircraft_weapon_mounts(self.changed_mesh(bindings=changed), self.fields)

    def test_animated_ancestor_cannot_be_flattened_to_its_first_frame(self):
        parts = list(self.parsed.file_parts())
        parts[0] = replace(parts[0], track=replace(parts[0].track, frame_map=(1,) * 64))
        with self.assertRaisesRegex(RuntimeError, "constant pose"):
            materializer._aircraft_weapon_mounts(self.changed_mesh(parts=parts), self.fields)

    def test_changed_cache_word_or_ownership_is_rejected(self):
        for offset in (None, 0x11c, 0x120):
            parts = list(self.parsed.file_parts())
            if offset is None:
                raw = bytearray(parts[3].track.cached_position_bytes)
                raw[0] ^= 1
                parts[3] = replace(parts[3], track=replace(parts[3].track, cached_position_bytes=bytes(raw)))
            else:
                raw = bytearray(parts[3].raw_cmsp)
                struct.pack_into("<I", raw, offset, 1)
                parts[3] = replace(parts[3], raw_cmsp=bytes(raw))
            with self.subTest(offset=offset), self.assertRaisesRegex(RuntimeError, "cache"):
                materializer._aircraft_weapon_mounts(self.changed_mesh(parts=parts), self.fields)

    def test_padding_is_not_promoted_into_meaningful_pose_words(self):
        parts = list(self.parsed.file_parts())
        track = parts[3].track
        position, basis = bytearray(track.cached_position_bytes), bytearray(track.cached_orientation_bytes)
        struct.pack_into("<I", position, 12, 0xdeadbeef)
        for offset in (12, 28, 44):
            struct.pack_into("<I", basis, offset, 0xffffffff)
        parts[3] = replace(parts[3], track=replace(track, cached_position_bytes=bytes(position),
                                                  cached_orientation_bytes=bytes(basis)))
        self.assertEqual(materializer._aircraft_weapon_mounts(self.parsed, self.fields),
                         materializer._aircraft_weapon_mounts(self.changed_mesh(parts=parts), self.fields))

    def test_use_order_and_unknown_flags_are_preserved(self):
        uses = list(reversed(self.fields.values(7)))
        uses[0] = uses[0][:-4] + struct.pack("<I", 0x80020401)
        fields = materializer._PhysicsFields(((9, self.fields[9]), *((7, raw) for raw in uses)))
        mounts = materializer._aircraft_weapon_mounts(self.parsed, fields)
        self.assertEqual(["GunB", "GunA"], [m["use"]["tagName"] for m in mounts])
        self.assertEqual(0x80020401, mounts[0]["use"]["rawCreationFlags"])

    def test_unit_use_requires_two_nonempty_names_and_one_flags_word(self):
        for raw in (b"name", b"\0GunA\0\0\0\0\0", b"name\0\0\0\0\0\0",
                    b"name\0GunA\0\0\0\0", b"name\0GunA\0\0\0\0\0extra"):
            with self.subTest(raw=raw), self.assertRaisesRegex(RuntimeError, "use layout"):
                materializer._unit_construction_use(raw)


class PhysicsDefinitionTests(unittest.TestCase):
    def test_ordered_stream_preserves_duplicate_records_and_fields(self) -> None:
        records = [
            _physics_definition_record(
                1,
                f"Synthetic Unit {index}",
                ((8, struct.pack("<I", 1)),),
            )
            for index in range(775)
        ]
        records.extend(
            (
                _physics_definition_record(
                    5,
                    "Duplicate Spawner",
                    ((1, b"First Unit\0"), (1, b"Second Unit\0")),
                ),
                _physics_definition_record(
                    5,
                    "Duplicate Spawner",
                    ((1, b"Third Unit\0"),),
                ),
            )
        )
        data = struct.pack("<H", 0x12) + b"".join(records) + struct.pack("<i", -1)

        stream = materializer._physics_record_stream(data)
        index = materializer._physics_records(data)

        self.assertEqual(777, len(stream))
        self.assertEqual(
            ((1, b"First Unit\0"), (1, b"Second Unit\0")),
            stream[-2].fields,
        )
        self.assertEqual(2, len(index[(5, "Duplicate Spawner")]))
        with self.assertRaisesRegex(RuntimeError, "expected one physics record"):
            materializer._physics_record(index, 5, "Duplicate Spawner")
        with self.assertRaisesRegex(RuntimeError, "expected one physics field"):
            materializer._PhysicsFields(stream[-2].fields)[1]

    def test_unit_behavior_serialized_types_map_to_released_selectors(self) -> None:
        expected = (
            0, 11, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 13,
            14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25,
        )

        self.assertEqual(
            expected,
            materializer.UNIT_BEHAVIOR_SELECTOR_BY_SERIALIZED_TYPE,
        )
        for serialized_type, selector in enumerate(expected, start=1):
            self.assertEqual(
                selector,
                materializer._unit_behavior_selector(
                    {8: struct.pack("<I", serialized_type)}
                ),
            )

    def test_unit_behavior_selector_rejects_unreleased_values(self) -> None:
        for value in (0, 26):
            with self.assertRaisesRegex(RuntimeError, "unsupported Unit behaviour"):
                materializer._unit_behavior_selector({8: struct.pack("<I", value)})
        with self.assertRaisesRegex(RuntimeError, "not one dword"):
            materializer._unit_behavior_selector({8: b"\x01"})


class LinuxSteamDiscoveryTests(unittest.TestCase):
    @staticmethod
    def _installation(root: Path) -> Path:
        for name in ("BEA.exe", materializer.LEVEL_ARCHIVE,
                     materializer.BASE_ARCHIVE, materializer.SOUND_BANK):
            path = root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.touch()
        return root

    def test_linux_default_steam_installation_is_selected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            home = Path(temporary)
            game = self._installation(home / ".local/share/Steam/steamapps/common/Battle Engine Aquila")
            with mock.patch.object(materializer.sys, "platform", "linux"), mock.patch.object(Path, "home", return_value=home):
                self.assertEqual(game, materializer._resolve_game_root(None))
                self.assertTrue(all("C:" not in str(path) for path in materializer._steam_roots()))

    def test_libraryfolders_and_steam_alias_resolve_one_library(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            home = Path(temporary)
            steam = home / ".local/share/Steam"
            (steam / "steamapps").mkdir(parents=True)
            (home / ".steam").mkdir()
            (home / ".steam/steam").symlink_to(steam, target_is_directory=True)
            library = home / "Games Library"
            game = self._installation(library / "steamapps/common/Battle Engine Aquila")
            (steam / "steamapps/libraryfolders.vdf").write_text(
                '"libraryfolders" { "0" { "path" "' + str(steam) +
                '" } "1" { "path" "' + str(library) + '" } }', encoding="utf-8"
            )
            with mock.patch.object(materializer.sys, "platform", "linux"), mock.patch.object(Path, "home", return_value=home):
                self.assertEqual([steam, library], materializer._steam_roots())
                self.assertEqual(game, materializer._resolve_game_root(None))

    def test_explicit_installation_wins_and_incomplete_override_does_not_fall_back(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            explicit = self._installation(root / "selected")
            with mock.patch.object(materializer, "_steam_roots") as discovery:
                self.assertEqual(explicit, materializer._resolve_game_root(explicit))
                with self.assertRaisesRegex(RuntimeError, "not a complete"):
                    materializer._resolve_game_root(root / "missing")
            discovery.assert_not_called()

    def test_windows_legacy_candidates_are_retained(self) -> None:
        with mock.patch.object(materializer.sys, "platform", "win32"), mock.patch.object(materializer, "_steam_roots", return_value=[]):
            candidates = materializer._game_candidates(None)
        self.assertEqual(4, len(candidates))
        self.assertTrue(any(r"D:\SteamLibrary" in str(path) for path in candidates))


class CanonicalAssetReuseTests(unittest.TestCase):
    def test_verifies_then_links_files_without_copying_or_shared_import_directories(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            canonical = Path(temporary) / "canonical"
            child = Path(temporary) / "child"
            child.mkdir()
            relative = Path("rebuild/OnslaughtRebuild.Godot/Assets/Hud/a.bin")
            source = canonical / relative
            source.parent.mkdir(parents=True)
            source.write_bytes(b"exact input")
            outputs = ((relative, materializer._sha256(b"exact input")),) * 2
            with (
                mock.patch.object(materializer, "ROOT", child),
                mock.patch.object(materializer, "_canonical_repository_root", return_value=canonical),
                mock.patch.object(materializer, "_all_outputs", return_value=outputs),
                mock.patch.dict(os.environ, {"BEA_LOCAL_LAB": str(canonical / "local-lab")}),
            ):
                self.assertEqual(1, materializer._reuse_canonical_assets())
                self.assertEqual(1, materializer._reuse_canonical_assets())
            destination = child / relative
            self.assertTrue(destination.is_symlink())
            self.assertFalse(destination.parent.is_symlink())
            self.assertEqual(source, destination.resolve())
            destination.with_suffix(".import").write_text("owned import")
            self.assertFalse(source.with_suffix(".import").exists())
            self.assertEqual(b"exact input", source.read_bytes())

    def test_stale_input_refuses_before_any_link_is_created(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            canonical = Path(temporary) / "canonical"
            child = Path(temporary) / "child"
            canonical.mkdir()
            child.mkdir()
            (canonical / "first").write_bytes(b"good")
            (canonical / "second").write_bytes(b"stale")
            with (
                mock.patch.object(materializer, "ROOT", child),
                mock.patch.object(materializer, "_canonical_repository_root", return_value=canonical),
                mock.patch.object(materializer, "_all_outputs", return_value=(
                    (Path("first"), materializer._sha256(b"good")),
                    (Path("second"), materializer._sha256(b"current")))),
                mock.patch.dict(os.environ, {"BEA_LOCAL_LAB": ""}),
            ):
                with self.assertRaisesRegex(RuntimeError, "current exact"):
                    materializer._reuse_canonical_assets()
            self.assertEqual([], list(child.iterdir()))

    def test_refuses_a_linked_destination_directory_or_conflicting_existing_file(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            canonical = Path(temporary) / "canonical"
            child = Path(temporary) / "child"
            source = canonical / "assets/input"
            source.parent.mkdir(parents=True)
            child.mkdir()
            source.write_bytes(b"good")
            with (
                mock.patch.object(materializer, "ROOT", child),
                mock.patch.object(materializer, "_canonical_repository_root", return_value=canonical),
                mock.patch.object(materializer, "_all_outputs", return_value=(
                    (Path("assets/input"), materializer._sha256(b"good")),)),
                mock.patch.dict(os.environ, {"BEA_LOCAL_LAB": ""}),
            ):
                (child / "assets").symlink_to(source.parent, target_is_directory=True)
                with self.assertRaisesRegex(RuntimeError, "directory link"):
                    materializer._reuse_canonical_assets()
                (child / "assets").unlink()
                (child / "assets").mkdir()
                destination = child / "assets/input"
                destination.write_bytes(b"unique")
                with self.assertRaisesRegex(RuntimeError, "refusing to replace"):
                    materializer._reuse_canonical_assets()
                self.assertEqual(b"unique", destination.read_bytes())


    def test_keeps_an_exact_local_output_the_canonical_checkout_has_not_published(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            canonical = Path(temporary) / "canonical"
            child = Path(temporary) / "child"
            canonical.mkdir()
            destination = child / "assets/new"
            destination.parent.mkdir(parents=True)
            destination.write_bytes(b"pinned")
            outputs = ((Path("assets/new"), materializer._sha256(b"pinned")),)
            with (
                mock.patch.object(materializer, "ROOT", child),
                mock.patch.object(materializer, "_canonical_repository_root", return_value=canonical),
                mock.patch.object(materializer, "_all_outputs", return_value=outputs),
                mock.patch.dict(os.environ, {"BEA_LOCAL_LAB": ""}),
            ):
                self.assertEqual(1, materializer._reuse_canonical_assets())
                self.assertFalse(destination.is_symlink())
                self.assertEqual(b"pinned", destination.read_bytes())
                # A canonical copy the canonical checkout has not republished
                # for this materializer does not displace the exact local one.
                (canonical / "assets").mkdir()
                (canonical / "assets/new").write_bytes(b"previous")
                self.assertEqual(1, materializer._reuse_canonical_assets())
                self.assertEqual(b"pinned", destination.read_bytes())
                (canonical / "assets/new").unlink()
                destination.write_bytes(b"other")
                with self.assertRaisesRegex(RuntimeError, "canonical input is missing"):
                    materializer._reuse_canonical_assets()
                self.assertEqual(b"other", destination.read_bytes())

    def test_lists_outputs_from_the_checkouts_own_exact_static_world_manifest(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            canonical = Path(temporary) / "canonical"
            child = Path(temporary) / "child"
            canonical.mkdir()
            manifest = child / materializer.STATIC_WORLD_MANIFEST
            manifest.parent.mkdir(parents=True)
            listed: list[Path] = []

            def outputs(root: Path) -> tuple[tuple[Path, str], ...]:
                listed.append(root)
                return ((materializer.STATIC_WORLD_MANIFEST, materializer._sha256(b"new manifest")),)

            with (
                mock.patch.object(materializer, "ROOT", child),
                mock.patch.object(materializer, "_canonical_repository_root", return_value=canonical),
                mock.patch.object(materializer, "_all_outputs", side_effect=outputs),
                mock.patch.object(materializer, "STATIC_WORLD_MANIFEST_SHA256", materializer._sha256(b"new manifest")),
                mock.patch.dict(os.environ, {"BEA_LOCAL_LAB": ""}),
            ):
                manifest.write_bytes(b"new manifest")
                self.assertEqual(1, materializer._reuse_canonical_assets())
                manifest.write_bytes(b"old manifest")
                with self.assertRaisesRegex(RuntimeError, "canonical input is missing"):
                    materializer._reuse_canonical_assets()
            self.assertEqual([child, canonical], listed)


class WorkRootRoutingTests(unittest.TestCase):
    @staticmethod
    def _canonical_lab(parent: Path) -> tuple[Path, Path]:
        repository = parent / "canonical"
        repository.mkdir()
        lab = repository / "local-lab"
        lab.mkdir()
        return repository, lab

    def test_canonical_root_is_derived_from_the_common_git_directory(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            repository = Path(temporary) / "canonical"
            common = repository / ".git"
            common.mkdir(parents=True)
            result = mock.Mock(stdout=f"{common}\n")

            with mock.patch.object(
                materializer.subprocess,
                "run",
                return_value=result,
            ) as run:
                self.assertEqual(
                    repository.resolve(),
                    materializer._canonical_repository_root(),
                )

            run.assert_called_once_with(
                (
                    "git",
                    "-C",
                    str(materializer.ROOT),
                    "rev-parse",
                    "--path-format=absolute",
                    "--git-common-dir",
                ),
                check=True,
                capture_output=True,
                text=True,
                env={
                    key: value
                    for key, value in materializer.os.environ.items()
                    if not key.startswith("GIT_")
                },
            )

    def test_canonical_root_ignores_ambient_git_routing_variables(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            parent = Path(temporary)
            canonical = parent / "canonical"
            foreign = parent / "foreign"
            for repository in (canonical, foreign):
                subprocess.run(
                    ("git", "init", "--quiet", str(repository)),
                    check=True,
                    capture_output=True,
                    text=True,
                )

            hostile = {
                "GIT_DIR": str(foreign / ".git"),
                "GIT_WORK_TREE": str(foreign),
                "GIT_COMMON_DIR": str(foreign / ".git"),
                "GIT_INDEX_FILE": str(foreign / ".git" / "index"),
            }
            with (
                mock.patch.object(materializer, "ROOT", canonical),
                mock.patch.dict(materializer.os.environ, hostile),
            ):
                self.assertEqual(
                    canonical.resolve(),
                    materializer._canonical_repository_root(),
                )

    def test_canonical_lab_root_owns_the_temporary_stage(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            parent = Path(temporary)
            repository, lab = self._canonical_lab(parent)
            game_root = parent / "retail"
            game_root.mkdir()
            work_root = lab / "materializer-work"
            observed_stage_parents: list[Path] = []

            def materialize(
                _game_root: Path, stage: Path
            ) -> tuple[tuple[Path, str], ...]:
                observed_stage_parents.append(stage.parent)
                return ()

            with (
                mock.patch.object(materializer, "ROOT", repository),
                mock.patch.object(
                    materializer.sys,
                    "argv",
                    [
                        "materialize_retail_assets.py",
                        "--force",
                        "--game-root",
                        str(game_root),
                        "--work-root",
                        str(work_root),
                    ],
                ),
                mock.patch.object(
                    materializer, "_resolve_game_root", return_value=game_root
                ),
                mock.patch.object(
                    materializer,
                    "_canonical_repository_root",
                    return_value=repository,
                ),
                mock.patch.object(
                    materializer, "_materialize", side_effect=materialize
                ),
                mock.patch.object(materializer, "_publish"),
                mock.patch.object(materializer, "_outputs_ready", return_value=True),
                mock.patch.object(materializer, "_all_outputs", return_value=()),
                mock.patch("builtins.print"),
            ):
                self.assertEqual(0, materializer.main())

            self.assertEqual([work_root.resolve()], observed_stage_parents)
            self.assertTrue(work_root.is_dir())

    def test_non_windows_requires_an_explicit_root(self) -> None:
        with mock.patch.object(materializer.os, "name", "posix"):
            with self.assertRaisesRegex(RuntimeError, "requires an explicit"):
                materializer._resolve_work_root(None)

    def test_windows_default_remains_the_historical_path(self) -> None:
        with mock.patch.object(materializer.os, "name", "nt"):
            self.assertEqual(
                materializer.WINDOWS_DEFAULT_WORK_ROOT,
                materializer._resolve_work_root(None),
            )

    def test_host_default_request_routes_linux_to_the_canonical_lab(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            repository, lab = self._canonical_lab(Path(temporary))
            with (
                mock.patch.object(materializer.os, "name", "posix"),
                mock.patch.object(
                    materializer,
                    "_canonical_repository_root",
                    return_value=repository,
                ),
            ):
                self.assertEqual(
                    lab / "rebuild-godot",
                    materializer._host_default_work_root_request(),
                )

    def test_host_default_request_preserves_the_windows_implicit_route(self) -> None:
        with mock.patch.object(materializer.os, "name", "nt"):
            self.assertIsNone(materializer._host_default_work_root_request())

    def test_host_default_flag_uses_canonical_lab_for_a_ready_linux_noop(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            repository, lab = self._canonical_lab(Path(temporary))
            work_root = lab / "rebuild-godot"
            with (
                mock.patch.object(materializer, "ROOT", repository),
                mock.patch.object(
                    materializer.sys,
                    "argv",
                    [
                        "materialize_retail_assets.py",
                        "--host-default-work-root",
                    ],
                ),
                mock.patch.object(materializer.os, "name", "posix"),
                mock.patch.object(
                    materializer,
                    "_canonical_repository_root",
                    return_value=repository,
                ),
                mock.patch.object(materializer, "_outputs_ready", return_value=True),
                mock.patch.object(materializer, "_all_outputs", return_value=()),
                mock.patch("builtins.print") as output,
            ):
                self.assertEqual(0, materializer.main())

            self.assertFalse(work_root.exists())
            output.assert_called_once_with("retail rebuild assets ready: 0 exact files")

    def test_plain_canonical_lab_descendants_are_admitted(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            repository, lab = self._canonical_lab(Path(temporary))
            existing = lab / "existing"
            existing.mkdir()
            nested_parent = lab / "nested"
            nested_parent.mkdir()

            with mock.patch.object(
                materializer,
                "_canonical_repository_root",
                return_value=repository,
            ):
                self.assertEqual(
                    existing.resolve(),
                    materializer._resolve_work_root(existing),
                )
                self.assertEqual(
                    nested_parent / "new-work-root",
                    materializer._resolve_work_root(
                        nested_parent / "new-work-root"
                    ),
                )

    def test_repository_external_and_escape_spellings_are_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            parent = Path(temporary)
            repository, lab = self._canonical_lab(parent)
            arbitrary_repository_path = repository / "rebuild"
            arbitrary_repository_path.mkdir()
            external = parent / "external"
            external.mkdir()

            with mock.patch.object(
                materializer,
                "_canonical_repository_root",
                return_value=repository,
            ):
                for rejected in (
                    repository,
                    arbitrary_repository_path,
                    repository / "local-lab-sibling" / "work",
                    materializer.ROOT / "local-lab" / "work",
                ):
                    with self.subTest(rejected=rejected):
                        with self.assertRaisesRegex(RuntimeError, "repository"):
                            materializer._resolve_work_root(rejected)

                with self.assertRaisesRegex(RuntimeError, "descendant"):
                    materializer._resolve_work_root(lab)
                with self.assertRaisesRegex(RuntimeError, "exact.*local-lab"):
                    materializer._resolve_work_root(external / "work")

    def test_relative_parent_traversal_and_missing_ancestors_are_rejected(self) -> None:
        with self.assertRaisesRegex(RuntimeError, "absolute"):
            materializer._resolve_work_root(Path("relative-work"))
        with tempfile.TemporaryDirectory() as temporary:
            repository, lab = self._canonical_lab(Path(temporary))
            with mock.patch.object(
                materializer,
                "_canonical_repository_root",
                return_value=repository,
            ):
                with self.assertRaisesRegex(RuntimeError, "parent traversal"):
                    materializer._resolve_work_root(lab / ".." / "escape")
                with self.assertRaisesRegex(RuntimeError, "parent.*absent"):
                    materializer._resolve_work_root(lab / "absent" / "work")

    def test_link_reparse_and_non_directory_ancestors_are_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            parent = Path(temporary)
            repository, lab = self._canonical_lab(parent)

            plain = lab / "plain"
            plain.mkdir()
            linked_parent = lab / "linked-parent"
            linked_parent.symlink_to(plain, target_is_directory=True)
            linked_work = lab / "linked-work"
            linked_work.symlink_to(plain, target_is_directory=True)
            non_directory = lab / "file-parent"
            non_directory.write_text("not a directory", encoding="utf-8")
            external_alias = parent / "lab-alias"
            external_alias.symlink_to(lab, target_is_directory=True)

            with mock.patch.object(
                materializer,
                "_canonical_repository_root",
                return_value=repository,
            ):
                for rejected in (
                    linked_parent / "work",
                    linked_work,
                ):
                    with self.subTest(rejected=rejected):
                        with self.assertRaisesRegex(RuntimeError, "link or reparse"):
                            materializer._resolve_work_root(rejected)
                with self.assertRaisesRegex(RuntimeError, "non-directory"):
                    materializer._resolve_work_root(non_directory / "work")
                with self.assertRaisesRegex(RuntimeError, "exact.*local-lab"):
                    materializer._resolve_work_root(external_alias / "work")

    def test_non_plain_or_missing_canonical_lab_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            parent = Path(temporary)
            repository = parent / "canonical"
            repository.mkdir()
            external = parent / "external-lab"
            external.mkdir()

            with mock.patch.object(
                materializer,
                "_canonical_repository_root",
                return_value=repository,
            ):
                with self.assertRaisesRegex(RuntimeError, "local-lab.*absent"):
                    materializer._resolve_work_root(repository / "local-lab" / "work")

                (repository / "local-lab").symlink_to(
                    external,
                    target_is_directory=True,
                )
                with self.assertRaisesRegex(RuntimeError, "link or reparse"):
                    materializer._resolve_work_root(repository / "local-lab" / "work")

    def test_retail_installation_overlap_is_rejected_inside_the_lab(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            repository, lab = self._canonical_lab(Path(temporary))
            retail = lab / "retail"
            retail.mkdir()

            with mock.patch.object(
                materializer,
                "_canonical_repository_root",
                return_value=repository,
            ):
                with self.assertRaisesRegex(RuntimeError, "retail installation"):
                    materializer._resolve_work_root(
                        retail / "materializer-work",
                        game_root=retail,
                    )


class World110PlayerStartTests(unittest.TestCase):
    def test_seed_wrapper_rewalks_the_exact_container_and_admits_all_rows(self) -> None:
        raw_world = b"world-container"
        wres = b"wres"
        wrld = b"wrld"
        rlwd = b"rlwd"
        objects = (mock.sentinel.object_row,)
        reader = mock.Mock()
        reader.position = materializer.WORLD110_INITIAL_OBJECT_HEADER_OFFSET
        reader.int32.side_effect = [0, 0, 0, 0, 0, 0, 0, 0, 0, 13]

        def parse_objects(*_args: object) -> tuple[object, ...]:
            reader.position = materializer.WORLD110_TREE_GROUP_HEADER_OFFSET + 6
            return objects

        def chunk_payload(data: bytes, kind: bytes) -> bytes:
            expected = {
                (raw_world, b"WRES"): wres,
                (wres, b"WRLD"): wrld,
                (wrld, b"RLWD"): rlwd,
            }
            return expected[(data, kind)]

        with (
            mock.patch.object(
                materializer,
                "_chunk_payload",
                side_effect=chunk_payload,
            ) as extract,
            mock.patch.object(
                materializer,
                "WORLD110_RLWD_SIZE",
                len(rlwd),
            ),
            mock.patch.object(
                materializer,
                "WORLD110_RLWD_SHA256",
                materializer._sha256(rlwd),
            ),
            mock.patch.object(materializer, "_parse_world_scripts") as scripts,
            mock.patch.object(
                materializer,
                "_WorldReader",
                return_value=reader,
            ),
            mock.patch.object(
                materializer,
                "_skip_level100_script_object",
            ) as skip_script,
            mock.patch.object(
                materializer,
                "_parse_world_initial_objects",
                side_effect=parse_objects,
            ) as parse_objects,
            mock.patch.object(
                materializer,
                "_admit_world110_player_start",
            ) as admit,
        ):
            self.assertEqual(
                objects,
                materializer._parse_world110_initial_object_seeds(raw_world),
            )

        self.assertEqual(3, extract.call_count)
        scripts.assert_called_once_with(
            raw_world,
            110,
            materializer.LEVEL110_SCRIPT_OBJECTS,
            materializer.COMMON_WORLD_LEVEL_HEADER,
        )
        self.assertEqual(len(materializer.LEVEL110_SCRIPT_OBJECTS), skip_script.call_count)
        parse_objects.assert_called_once_with(
            reader,
            110,
            materializer.WORLD110_INITIAL_OBJECT_HEADER,
            materializer.WORLD110_INITIAL_OBJECT_TYPE_COUNTS,
        )
        admit.assert_called_once_with(objects)

    def test_player_start_wrapper_filters_the_admitted_seed_table(self) -> None:
        objects = (mock.sentinel.object_row,)
        start = mock.sentinel.player_start
        with (
            mock.patch.object(
                materializer,
                "_parse_world110_initial_object_seeds",
                return_value=objects,
            ) as parse_seeds,
            mock.patch.object(
                materializer,
                "_admit_world110_player_start",
                return_value=start,
            ) as admit,
        ):
            self.assertIs(start, materializer._parse_world110_player_start(b"world"))

        parse_seeds.assert_called_once_with(b"world")
        admit.assert_called_once_with(objects)

    def test_wrapper_rejects_changed_rlwd_before_interpreting_it(self) -> None:
        with (
            mock.patch.object(
                materializer,
                "_chunk_payload",
                side_effect=(b"wres", b"wrld", b"changed-rlwd"),
            ),
            mock.patch.object(
                materializer,
                "WORLD110_RLWD_SIZE",
                len(b"changed-rlwd"),
            ),
            mock.patch.object(materializer, "_parse_world_scripts") as scripts,
        ):
            with self.assertRaisesRegex(RuntimeError, "RLWD identity changed"):
                materializer._parse_world110_player_start(b"world-container")

        scripts.assert_not_called()

    def test_exact_table_preserves_the_raw_authored_start(self) -> None:
        reader, objects = _world110_initial_object_fixture()
        start = materializer._admit_world110_player_start(objects)

        self.assertEqual(40, len(objects))
        self.assertEqual(len(reader.data), reader.position)
        self.assertEqual(1, start.ordinal)
        self.assertEqual(15_781, start.record_offset)
        self.assertEqual(59, start.record_bytes)
        self.assertEqual(
            "850de203b32b967064f3a9bacca24bebd783af68760a8b4c056ea242a2b47dfc",
            start.record_sha256,
        )
        self.assertEqual((0x43846000, 0x43816800, 0x80000000), start.position_bits)
        self.assertEqual((0xBF04FD8B, 0, 0), start.orientation_bits)
        self.assertEqual(0, start.plane_mode)
        self.assertEqual(1, start.player_number)

    def test_changed_header_is_rejected(self) -> None:
        with self.assertRaisesRegex(RuntimeError, "header changed"):
            _world110_initial_object_fixture(header=(2, 1, 40))

    def test_changed_start_and_duplicate_type_census_are_rejected(self) -> None:
        _, objects = _world110_initial_object_fixture(start_player_number=2)
        with self.assertRaisesRegex(RuntimeError, "record changed"):
            materializer._admit_world110_player_start(objects)
        with self.assertRaisesRegex(RuntimeError, "type census changed"):
            _world110_initial_object_fixture(extra_start=True)

    def test_admission_rejects_zero_or_two_player_starts(self) -> None:
        _, objects = _world110_initial_object_fixture()
        start = next(item for item in objects if item.thing_type == 15)

        without_start = tuple(item for item in objects if item.thing_type != 15)
        with self.assertRaisesRegex(RuntimeError, "exactly one.*found 0"):
            materializer._admit_world110_player_start(without_start)

        with self.assertRaisesRegex(RuntimeError, "exactly one.*found 2"):
            materializer._admit_world110_player_start(objects + (start,))

    def test_changed_plane_or_raw_pose_bit_is_rejected(self) -> None:
        _, objects = _world110_initial_object_fixture(start_plane_mode=1)
        with self.assertRaisesRegex(RuntimeError, "record changed"):
            materializer._admit_world110_player_start(objects)
        _, objects = _world110_initial_object_fixture(
            start_position_bits=(0x43846001, 0x43816800, 0x80000000)
        )
        with self.assertRaisesRegex(RuntimeError, "record changed"):
            materializer._admit_world110_player_start(objects)

    def test_unsupported_type_and_tree_misalignment_are_rejected(self) -> None:
        with self.assertRaisesRegex(RuntimeError, "unsupported thing type"):
            _world110_initial_object_fixture(unsupported_type=True)
        with self.assertRaisesRegex(RuntimeError, "tree groups"):
            _world110_initial_object_fixture(tree_header=(1, 2))

    def test_truncated_type_specific_tail_is_rejected(self) -> None:
        payload = struct.pack("<iiH", 2, 0, 1) + _world_initial_object(28)[:-2]
        reader = materializer._WorldReader(payload)
        with self.assertRaises(RuntimeError):
            materializer._parse_world_initial_objects(
                reader,
                110,
                (2, 0, 1),
                {28: 1},
            )


class World110InitialObjectSeedTests(unittest.TestCase):
    @staticmethod
    def _parse_rows(rows: list[bytes]) -> tuple[materializer._WorldInitialObject, ...]:
        payload = bytearray(struct.pack("<iiH", 2, 0, len(rows)))
        payload.extend(b"".join(rows))
        payload.extend(struct.pack("<Hi", 0, 2))
        return materializer._parse_world_initial_objects(
            materializer._WorldReader(bytes(payload)),
            110,
            (2, 0, len(rows)),
            {
                thing_type: sum(
                    struct.unpack_from("<i", row)[0] == thing_type for row in rows
                )
                for thing_type in sorted(
                    {struct.unpack_from("<i", row)[0] for row in rows}
                )
            },
        )

    def test_all_seven_closed_tails_retain_distinct_non_default_values(self) -> None:
        rows = self._parse_rows(
            [
                _world_initial_object(
                    8,
                    definition_name="Exact Unit",
                ),
                _world_initial_object(15, plane_mode=1, player_number=2),
                _world_initial_object(18),
                _world_initial_object(
                    19,
                    amount=7,
                    delay_bits=0x3F800001,
                    squad_delay_bits=0x40000002,
                    initial_delay_bits=0x40400003,
                    squad_size=4,
                    spawn_unit="Exact Spawn Unit",
                    spawner_spawn_script="SpawnerTailScript",
                    spawn_script="CommonSpawnScript",
                ),
                _world_initial_object(27, script="Carrier"),
                _world_initial_object(
                    28,
                    amount=9,
                    mode=2,
                    definition_name="Exact Squad",
                ),
                _world_initial_object(36, radius_bits=0x42480001),
            ]
        )

        self.assertEqual(
            materializer._WorldUnitSeedTail("Exact Unit", -1),
            rows[0].tail,
        )
        self.assertEqual(materializer._WorldStartSeedTail(1, 2), rows[1].tail)
        self.assertIsInstance(rows[2].tail, materializer._WorldWaypointSeedTail)
        self.assertEqual(
            materializer._WorldSpawnerSeedTail(
                7,
                0x3F800001,
                0x40000002,
                0x40400003,
                4,
                "Exact Spawn Unit",
                "SpawnerTailScript",
            ),
            rows[3].tail,
        )
        self.assertEqual("CommonSpawnScript", rows[3].spawn_script)
        self.assertIsInstance(rows[4].tail, materializer._WorldScriptSeedTail)
        self.assertEqual(
            materializer._WorldSquadSeedTail(9, 2, "Exact Squad", -1),
            rows[5].tail,
        )
        self.assertEqual(
            materializer._WorldVolumeSeedTail(0x42480001),
            rows[6].tail,
        )

    def test_non_finite_common_and_tail_words_fail_closed(self) -> None:
        cases = (
            _world_initial_object(18, position_bits=(0x7F800000, 0, 0)),
            _world_initial_object(19, delay_bits=0x7FC00000),
            _world_initial_object(36, radius_bits=0xFF800000),
        )
        for row in cases:
            with self.subTest(thing_type=struct.unpack_from("<i", row)[0]):
                with self.assertRaisesRegex(RuntimeError, "non-finite"):
                    self._parse_rows([row])

    def test_invalid_flags_sentinels_and_cardinalities_fail_closed(self) -> None:
        cases = (
            (_world_initial_object(18, active=2), "non-boolean"),
            (_world_initial_object(18, attach_scripts=-1), "non-boolean"),
            (_world_initial_object(8, trailer=0), "type-8.*trailer"),
            (_world_initial_object(28, trailer=0), "type-28.*trailer"),
            (_world_initial_object(19, amount=0), "cardinality"),
            (_world_initial_object(19, squad_size=-1), "cardinality"),
            (_world_initial_object(28, amount=-1), "configuration"),
            (_world_initial_object(28, mode=4), "configuration"),
        )
        for row, message in cases:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    self._parse_rows([row])

    def test_canonical_seed_serialization_is_byte_deterministic(self) -> None:
        _, parsed = _world110_initial_object_fixture()
        exact_lengths = (
            62, 59, 56, 51, 51, 122, 51, 51, 89, 55,
            51, 51, 90, 90, 78, 51, 78, 87, 78, 92,
            89, 51, 51, 51, 51, 71, 51, 51, 51, 51,
            51, 51, 51, 51, 77, 77, 77, 77, 77, 58,
        )
        offset = materializer.WORLD110_INITIAL_OBJECT_FIRST_RECORD_OFFSET
        normalized: list[materializer._WorldInitialObject] = []
        for item, length in zip(parsed, exact_lengths, strict=True):
            normalized.append(
                item._replace(record_offset=offset, record_bytes=length)
            )
            offset += length
        rows = tuple(normalized)

        first = materializer._world110_initial_object_seed_bytes(rows)
        second = materializer._world110_initial_object_seed_bytes(rows)

        self.assertEqual(first, second)
        self.assertTrue(first.endswith(b"\n"))
        self.assertNotIn(b" ", first)
        self.assertEqual(
            materializer.WORLD110_INITIAL_OBJECT_SEEDS_SCHEMA,
            json.loads(first)["schema"],
        )
        self.assertRegex(
            materializer.WORLD110_INITIAL_OBJECT_SEEDS_SHA256,
            r"^[0-9a-f]{64}$",
        )


class StartupMediaCacheTests(unittest.TestCase):
    @staticmethod
    def _canonical_lab(parent: Path) -> tuple[Path, Path]:
        repository = parent / "canonical"
        repository.mkdir()
        lab = repository / "local-lab"
        lab.mkdir()
        return repository, lab

    def test_linux_default_uses_the_canonical_checkout_lab(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            repository, lab = self._canonical_lab(Path(temporary))
            with (
                mock.patch.object(materializer.os, "name", "posix"),
                mock.patch.object(
                    materializer,
                    "_canonical_repository_root",
                    return_value=repository,
                ),
                mock.patch.dict(
                    materializer.os.environ,
                    {
                        "ONSLAUGHT_STARTUP_MEDIA": "",
                        "LOCALAPPDATA": "/ignored-on-linux",
                    },
                ),
            ):
                self.assertEqual(
                    lab / "startup-media",
                    materializer._default_startup_media_root(),
                )

    def test_child_worktree_default_never_uses_its_local_lab_twin(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            parent = Path(temporary)
            repository, lab = self._canonical_lab(parent)
            child = parent / "child-worktree"
            child_lab = child / "local-lab"
            child_lab.mkdir(parents=True)
            with (
                mock.patch.object(materializer, "ROOT", child),
                mock.patch.object(materializer.os, "name", "posix"),
                mock.patch.object(
                    materializer,
                    "_canonical_repository_root",
                    return_value=repository,
                ),
                mock.patch.dict(
                    materializer.os.environ,
                    {"ONSLAUGHT_STARTUP_MEDIA": ""},
                ),
            ):
                selected = materializer._default_startup_media_root()

            self.assertEqual(lab / "startup-media", selected)
            self.assertNotEqual(child_lab / "startup-media", selected)
            self.assertFalse((child_lab / "startup-media").exists())

    def test_linux_default_rejects_a_linked_canonical_cache(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            parent = Path(temporary)
            repository, lab = self._canonical_lab(parent)
            external = parent / "external-cache"
            external.mkdir()
            (lab / "startup-media").symlink_to(
                external,
                target_is_directory=True,
            )
            with (
                mock.patch.object(materializer.os, "name", "posix"),
                mock.patch.object(
                    materializer,
                    "_canonical_repository_root",
                    return_value=repository,
                ),
                mock.patch.dict(
                    materializer.os.environ,
                    {"ONSLAUGHT_STARTUP_MEDIA": ""},
                ),
            ):
                with self.assertRaisesRegex(RuntimeError, "link or reparse"):
                    materializer._default_startup_media_root()

    def test_explicit_startup_media_environment_remains_authoritative(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            configured = Path(temporary) / "explicit-cache"
            with (
                mock.patch.object(materializer.os, "name", "posix"),
                mock.patch.object(
                    materializer,
                    "_canonical_repository_root",
                ) as canonical,
                mock.patch.dict(
                    materializer.os.environ,
                    {"ONSLAUGHT_STARTUP_MEDIA": str(configured)},
                ),
            ):
                self.assertEqual(
                    configured,
                    materializer._default_startup_media_root(),
                )

            canonical.assert_not_called()

    def test_complete_rgb_png_is_accepted(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "frame.png"
            path.write_bytes(_png(2, 3))
            self.assertEqual((2, 3), materializer._png_dimensions(path))

    def test_header_only_png_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "frame.png"
            path.write_bytes(_png(2, 3)[:33])
            self.assertIsNone(materializer._png_dimensions(path))

    def test_framed_but_invalid_idat_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "frame.png"
            path.write_bytes(_png(2, 3, idat=b"\x01"))
            self.assertIsNone(materializer._png_dimensions(path))

    def test_non_object_manifest_is_not_a_ready_cache(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            media = root / "media"
            media.mkdir()
            (media / "startup-media.json").write_text("[]", encoding="utf-8")
            self.assertFalse(materializer._startup_media_ready(root, media))

    def test_legacy_v2_cache_is_not_ready(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            media = root / "media"
            media.mkdir()
            (media / "startup-media.json").write_text(
                '{"schema":"onslaught-startup-media.v2","clips":{},"stills":{}}',
                encoding="utf-8",
            )
            self.assertFalse(materializer._startup_media_ready(root, media))

    def test_ready_cache_rejects_a_corrupt_middle_frame(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            media = root / "media"
            frames = media / "clip"
            frames.mkdir(parents=True)
            source = root / "clip.vid"
            splash_source = root / "splash.tga"
            source.write_bytes(b"clip-source")
            splash_source.write_bytes(b"splash-source")
            frame_paths = []
            for frame in range(1, 4):
                path = frames / f"f{frame:05d}.png"
                path.write_bytes(_png(2, 3))
                frame_paths.append(path)
            (media / "splash.png").write_bytes(_png(512, 512))
            manifest = {
                "schema": materializer.STARTUP_MEDIA_SCHEMA,
                "clips": {
                    "Logo": {
                        "source": "clip.vid",
                        "sourceSha256": materializer._sha256(source.read_bytes()),
                        "width": 2,
                        "height": 3,
                        "fpsNumerator": 25,
                        "fpsDenominator": 1,
                        "frameCount": 3,
                        "framePathFormat": "clip/f{0:D5}.png",
                        "framesSha256": materializer._startup_frame_set_sha256(
                            frame_paths
                        ),
                    }
                },
                "stills": {
                    "Splash": {
                        "source": "splash.tga",
                        "sourceSha256": materializer._sha256(
                            splash_source.read_bytes()
                        ),
                        "path": "splash.png",
                        "outputSha256": materializer._sha256(
                            (media / "splash.png").read_bytes()
                        ),
                    }
                },
            }
            (media / "startup-media.json").write_text(
                __import__("json").dumps(manifest),
                encoding="utf-8",
            )
            with (
                mock.patch.object(
                    materializer,
                    "STARTUP_MEDIA_CLIPS",
                    (("Logo", "clip.vid", "clip", 2, 3, 25, 3),),
                ),
                mock.patch.object(
                    materializer,
                    "STARTUP_MEDIA_SPLASH_SOURCE",
                    "splash.tga",
                ),
            ):
                self.assertTrue(materializer._startup_media_ready(root, media))
                frame_paths[1].write_bytes(b"corrupt-middle-frame")
                self.assertFalse(materializer._startup_media_ready(root, media))

    def test_ready_cache_rejects_a_valid_but_different_splash(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            media = root / "media"
            media.mkdir()
            splash_source = root / "splash.tga"
            splash_source.write_bytes(b"splash-source")
            splash_path = media / "splash.png"
            splash_path.write_bytes(_png(512, 512))
            manifest = {
                "schema": materializer.STARTUP_MEDIA_SCHEMA,
                "clips": {},
                "stills": {
                    "Splash": {
                        "source": "splash.tga",
                        "sourceSha256": materializer._sha256(
                            splash_source.read_bytes()
                        ),
                        "path": "splash.png",
                        "outputSha256": materializer._sha256(
                            splash_path.read_bytes()
                        ),
                    }
                },
            }
            (media / "startup-media.json").write_text(
                __import__("json").dumps(manifest),
                encoding="utf-8",
            )
            with (
                mock.patch.object(materializer, "STARTUP_MEDIA_CLIPS", ()),
                mock.patch.object(
                    materializer,
                    "STARTUP_MEDIA_SPLASH_SOURCE",
                    "splash.tga",
                ),
            ):
                self.assertTrue(materializer._startup_media_ready(root, media))
                del manifest["stills"]["Splash"]["outputSha256"]
                (media / "startup-media.json").write_text(
                    json.dumps(manifest),
                    encoding="utf-8",
                )
                self.assertFalse(materializer._startup_media_ready(root, media))
                manifest["stills"]["Splash"]["outputSha256"] = (
                    materializer._sha256(splash_path.read_bytes())
                )
                (media / "startup-media.json").write_text(
                    json.dumps(manifest),
                    encoding="utf-8",
                )
                splash_path.write_bytes(_png(512, 512, last_pixel=1))
                self.assertFalse(materializer._startup_media_ready(root, media))

    def test_generation_rejects_a_malformed_splash_before_manifest(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            media = root / "media"
            (root / "splash.tga").write_bytes(b"splash-source")
            with (
                mock.patch.object(materializer, "STARTUP_MEDIA_CLIPS", ()),
                mock.patch.object(
                    materializer,
                    "STARTUP_MEDIA_SPLASH_SOURCE",
                    "splash.tga",
                ),
                mock.patch.object(materializer.subprocess, "run"),
                mock.patch.object(
                    materializer,
                    "_png_dimensions",
                    return_value=None,
                ),
            ):
                with self.assertRaisesRegex(RuntimeError, "invalid 512x512 PNG"):
                    materializer._materialize_startup_media(root, media)
            self.assertFalse((media / "startup-media.json").exists())

    def test_generation_receipts_exact_splash_output(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            media = root / "media"
            source = root / "splash.tga"
            source.write_bytes(b"splash-source")

            def write_splash(arguments: list[str], **_kwargs: object) -> None:
                Path(arguments[-1]).write_bytes(_png(512, 512))

            with (
                mock.patch.object(materializer, "STARTUP_MEDIA_CLIPS", ()),
                mock.patch.object(
                    materializer,
                    "STARTUP_MEDIA_SPLASH_SOURCE",
                    "splash.tga",
                ),
                mock.patch.object(
                    materializer.subprocess,
                    "run",
                    side_effect=write_splash,
                ),
            ):
                manifest_path = materializer._materialize_startup_media(root, media)

            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            splash_path = media / "splash.png"
            self.assertEqual("onslaught-startup-media.v4", manifest["schema"])
            self.assertEqual(
                materializer._sha256(splash_path.read_bytes()),
                manifest["stills"]["Splash"]["outputSha256"],
            )


def _bink_header(track_ids: list[int], sample_rate: int = 44100) -> bytes:
    """A Bink header carrying only what _bink_audio_tracks reads."""
    count = len(track_ids)
    header = bytearray(b"BIKi")
    header += struct.pack("<I", 0)          # 0x04 file size
    header += struct.pack("<I", 3095)       # 0x08 frame count
    header += struct.pack("<I", 0)          # 0x0C largest frame
    header += struct.pack("<I", 3095)       # 0x10 frame count again
    header += struct.pack("<II", 480, 300)  # 0x14 width / 0x18 height
    header += struct.pack("<II", 25, 1)     # 0x1C fps num / 0x20 den
    header += struct.pack("<I", 0)          # 0x24 video flags
    header += struct.pack("<I", count)      # 0x28 audio track count
    header += b"".join(struct.pack("<I", 0) for _ in range(count))
    header += b"".join(struct.pack("<HH", sample_rate, 0xE000) for _ in range(count))
    header += b"".join(struct.pack("<I", track) for track in track_ids)
    return bytes(header)


def _pcm_wav(sample_frames: int, rate: int = 44100, channels: int = 2) -> bytes:
    block_align = channels * 2
    data = bytes(sample_frames * block_align)
    return (
        b"RIFF"
        + struct.pack("<I", 36 + len(data))
        + b"WAVEfmt "
        + struct.pack("<IHHIIHH", 16, 1, channels, rate, rate * block_align, block_align, 16)
        + b"data"
        + struct.pack("<I", len(data))
        + data
    )


class CutsceneVoiceTrackTests(unittest.TestCase):
    """The two laws that connect "English is Bink track 0" to bytes on disk.

    Everything else in that chain is a property of BEA.exe and was read out of
    the pristine specimen. These are the parts that are properties of the .vid
    and of the decode, so they are the parts this script can check on any
    machine and the parts a wrong edit here would silently break.
    """

    # Real numbers from cutscenes/01.vid: 3095 video frames at 25 fps is
    # 5,459,580 sample frames at 44.1 kHz, and the decode produces 5,460,480 —
    # an overhang of 900, well inside one 2048-sample binkaudio frame.
    VIDEO_FRAMES = 3095
    FPS = 25
    DECODED_SAMPLE_FRAMES = 5_460_480

    def _run(self, temporary: str, track_ids: list[int], sample_frames: int):
        root = Path(temporary)
        destination = root / "clip"
        destination.mkdir(parents=True, exist_ok=True)
        source = root / "01.vid"
        source.write_bytes(_bink_header(track_ids))

        def write_audio(arguments: list[str], **_kwargs: object) -> None:
            Path(arguments[-1]).write_bytes(_pcm_wav(sample_frames))

        with mock.patch.object(
            materializer.subprocess, "run", side_effect=write_audio
        ):
            return materializer._materialize_clip_audio(
                source,
                "data/video/cutscenes/01.vid",
                "clip",
                destination,
                self.VIDEO_FRAMES,
                self.FPS,
                (0, "voice-track00.wav", 44100, 2, 16),
            )

    def test_identity_track_table_and_measured_length_are_accepted(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            entry = self._run(temporary, [0, 1, 2, 3, 4], self.DECODED_SAMPLE_FRAMES)

        self.assertEqual(0, entry["track"])
        self.assertEqual("clip/voice-track00.wav", entry["path"])
        self.assertEqual(44100, entry["sampleRate"])
        self.assertEqual(2, entry["channels"])
        self.assertEqual(self.DECODED_SAMPLE_FRAMES, entry["sampleFrameCount"])
        self.assertEqual(64, len(entry["outputSha256"]))

    def test_a_non_identity_track_table_is_refused(self) -> None:
        # BinkSetSoundTrack takes a track ID; `-map 0:a:N` takes an ordinal.
        # They coincide only while the shipped table is the identity. If it ever
        # is not, ordinal 0 stops being the track the game would have played and
        # the decode must fail rather than quietly ship another language.
        with tempfile.TemporaryDirectory() as temporary:
            with self.assertRaisesRegex(RuntimeError, "not the identity"):
                self._run(temporary, [4, 3, 2, 1, 0], self.DECODED_SAMPLE_FRAMES)

    def test_an_audio_track_that_does_not_match_the_video_length_is_refused(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            # One binkaudio frame too long.
            with self.assertRaisesRegex(RuntimeError, r"sample frames"):
                self._run(
                    temporary,
                    [0, 1, 2, 3, 4],
                    self.DECODED_SAMPLE_FRAMES + materializer.BINK_AUDIO_FRAME_SAMPLES,
                )

        with tempfile.TemporaryDirectory() as temporary:
            # Short of the video: the movie would outlive its voice.
            with self.assertRaisesRegex(RuntimeError, r"sample frames"):
                self._run(temporary, [0, 1, 2, 3, 4], self.VIDEO_FRAMES * 44100 // self.FPS - 1)


class FrontendLoadingBarAssetsTests(unittest.TestCase):
    def test_barl_barc_barr_are_hash_pinned(self) -> None:
        rows = {
            destination.as_posix(): (source, expected)
            for destination, source, expected in materializer.FRONTEND_ASSETS
        }
        self.assertEqual(
            rows["rebuild/OnslaughtRebuild.Godot/Assets/Frontend/bar-l.texture.aya"],
            (
                "data/resources/dxtntextures/FrontEnd%BarL.tga(0)A8R8G8B8.aya",
                "fbd28ca720ebe91cb8f58a9f5be5e4e9ee5c013fc42052fd1bec6b41dfd094bd",
            ),
        )
        self.assertEqual(
            rows["rebuild/OnslaughtRebuild.Godot/Assets/Frontend/bar-c.texture.aya"],
            (
                "data/resources/dxtntextures/FrontEnd%BarC.tga(0)A8R8G8B8.aya",
                "347828edf9f97dd3463ce7374e167e57f8bd837113cbfad71cb8cbc6bcde68a5",
            ),
        )
        self.assertEqual(
            rows["rebuild/OnslaughtRebuild.Godot/Assets/Frontend/bar-r.texture.aya"],
            (
                "data/resources/dxtntextures/FrontEnd%BarR.tga(0)A8R8G8B8.aya",
                "9995d4a41ff140d3d33004086e82f940db946c0db45635b5c853662ace6c6199",
            ),
        )


class FrontendDebriefingAssetsTests(unittest.TestCase):
    def test_settled_ring_and_grade_surfaces_are_hash_pinned(self) -> None:
        rows = {
            destination.as_posix(): (source, expected)
            for destination, source, expected in materializer.FRONTEND_ASSETS
        }
        expected = {
            "metal-ring-transition": (
                "FrontEnd%v2%FE_metal_ring_trans_from_levsel2.tga(0)A8R8G8B8.aya",
                "8b9b79189981de706fc72de9552bdc398ada96d892c0e4e33706c77a907da869",
            ),
            "ranking-a": (
                "FrontEnd%RankingA.tga(0)A8R8G8B8.aya",
                "88765906f295b8002dc254f9805b05d5f5589d87a08f297b37d90953ba57d625",
            ),
            "ranking-b": (
                "FrontEnd%RankingB.tga(0)A8R8G8B8.aya",
                "5f03cdf1e79e679e706a0b6ab644ad6961447dace35695dc93d7c3928ebf3d50",
            ),
            "ranking-c": (
                "FrontEnd%RankingC.tga(0)A8R8G8B8.aya",
                "364b5140a18ebec99bdf4e4ce7c99e3619ff743d4cbe888c0fa5d84e96407a06",
            ),
            "ranking-d": (
                "FrontEnd%RankingD.tga(0)A8R8G8B8.aya",
                "894521839162a5d2d8d73feba39435cf0590b0863d65b67daa5c4a4420485bd0",
            ),
            "ranking-e": (
                "FrontEnd%RankingE.tga(0)A8R8G8B8.aya",
                "c844f938cfcf56a2f3908bb69313ec86153f97e6993f42172c2c8376897e911a",
            ),
            "ranking-s": (
                "FrontEnd%RankingS.tga(0)A8R8G8B8.aya",
                "aab0817f798ff8379cf6e9e0f86ae466832ad644170e8c14b6f7a50a6a12de9f",
            ),
        }

        for name, (source, expected_hash) in expected.items():
            self.assertEqual(
                rows[
                    "rebuild/OnslaughtRebuild.Godot/Assets/Frontend/"
                    f"Debriefing/{name}.texture.aya"
                ],
                (f"data/resources/dxtntextures/{source}", expected_hash),
            )


if __name__ == "__main__":
    unittest.main()
