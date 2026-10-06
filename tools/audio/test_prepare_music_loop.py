import struct
import tempfile
import unittest
from pathlib import Path
import wave

from prepare_music_loop import prepare


class MusicLoopPreparationTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory(prefix="csr-wav-loop-")
        self.source = Path(self.folder.name) / "source.wav"
        self.output = Path(self.folder.name) / "loop.wav"

    def tearDown(self):
        self.folder.cleanup()

    def source_wav(self, width, channels, samples):
        with wave.open(str(self.source), "wb") as wav:
            wav.setsampwidth(width)
            wav.setnchannels(channels)
            wav.setframerate(44100)
            wav.writeframes(samples)

    def test_stereo_pcm16_preserves_all_samples_and_writes_inclusive_loop(self):
        samples = struct.pack("<8h", 120, -95, 950, -1240, 1800, -50, -50, 20)
        self.source_wav(2, 2, samples)
        result = prepare(self.source, self.output)
        with wave.open(str(self.output), "rb") as wav:
            self.assertEqual(wav.getnframes(), 2052)
            self.assertEqual(wav.readframes(2048), b"\0" * 8192)
            self.assertEqual(wav.readframes(4), samples)
        blob = self.output.read_bytes()
        smpl = blob.index(b"smpl") + 8
        self.assertEqual(struct.unpack_from("<I", blob, smpl + 28)[0], 1)
        self.assertEqual(struct.unpack_from("<6I", blob, smpl + 36), (0, 0, 2048, 2051, 0, 0))
        self.assertTrue(result["musicalSamplesPreserved"])

    def test_unsigned_pcm8_guard_and_exclusive_selected_range(self):
        self.source_wav(1, 1, bytes((100, 120, 140, 160, 180)))
        result = prepare(self.source, self.output, 1024, 1, 4)
        with wave.open(str(self.output), "rb") as wav:
            self.assertEqual(wav.readframes(1024), b"\x80" * 1024)
            self.assertEqual(wav.readframes(3), bytes((120, 140, 160)))
        self.assertEqual(result["loopEndFrameInclusive"], 1026)
        self.assertEqual(result["selectedSourceFrames"], [1, 4])

    def test_invalid_guard_range_and_same_output_do_not_replace_source(self):
        self.source_wav(2, 1, struct.pack("<3h", 10, 20, 30))
        original = self.source.read_bytes()
        with self.assertRaises(ValueError):
            prepare(self.source, self.output, 1023)
        with self.assertRaises(ValueError):
            prepare(self.source, self.output, start_frame=3)
        with self.assertRaises(ValueError):
            prepare(self.source, self.source)
        self.assertEqual(self.source.read_bytes(), original)
        self.assertFalse(self.output.exists())


if __name__ == "__main__":
    unittest.main()
