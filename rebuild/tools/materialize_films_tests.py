# SPDX-License-Identifier: GPL-3.0-only
"""Tests for materialize_films.py that need no retail data: the Bink header fields and retail's colour conversion."""
import struct
import tempfile
import unittest
from pathlib import Path

import numpy as np

import materialize_films as mf


class BinkHeaderTests(unittest.TestCase):
    def test_reads_frames_size_and_rate(self):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "x.vid"
            p.write_bytes(b"BIKi" + struct.pack("<10I", 1000, 229, 25136, 229, 480, 300, 25, 1, 0, 1))
            self.assertEqual(mf.bink_header(p), {"frames": 229, "width": 480, "height": 300, "rate_num": 25,
                                                 "rate_den": 1})


class ColourTests(unittest.TestCase):
    def test_retail_tables(self):
        # luma below 16 is black; mid grey stays grey; the terms truncate toward zero
        y = np.array([[0, 15, 16, 128]], dtype=np.uint8)
        u = np.array([[128, 128]], dtype=np.uint8)
        v = np.array([[128, 128]], dtype=np.uint8)
        px = np.frombuffer(mf.to_bgra(y, u, v, None), dtype=np.uint8).reshape(-1, 4)
        self.assertEqual(px[:3, :3].tolist(), [[0, 0, 0]] * 3)
        self.assertEqual(px[3, :3].tolist(), [130, 130, 130])		# ((128-16)*76309)>>16
        self.assertEqual(px[:, 3].tolist(), [0, 0, 0, 0])			# no alpha plane: retail writes 0
        self.assertEqual(int(mf.TABLE_RV[0]), -204)				# 104597 * -128 truncates toward zero (floor: -205)

    def test_chroma_is_nearest_neighbour(self):
        y = np.full((2, 4), 128, dtype=np.uint8)
        u = np.array([[128, 255]], dtype=np.uint8)
        v = np.array([[128, 128]], dtype=np.uint8)
        px = np.frombuffer(mf.to_bgra(y, u, v, None), dtype=np.uint8).reshape(2, 4, 4)
        self.assertEqual(px[0, 0, 0], px[1, 1, 0])			# columns 0-1 share chroma 0
        self.assertNotEqual(px[0, 1, 0], px[0, 2, 0])			# column 2 takes chroma 1


if __name__ == "__main__":
    unittest.main()
