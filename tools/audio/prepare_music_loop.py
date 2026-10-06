"""Wrap an inspected PCM WAV with Unity's documented WebGL loop guard.

Musical samples are copied byte for byte; this script does not choose a musical
cut, normalize, fade, stretch, or crossfade the recording. Inspect/listen to the
source first, and optionally supply an exact, exclusive frame range.

Unity reference:
https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-audio.html#loop-issues
"""

import argparse
import hashlib
import json
from pathlib import Path
import struct
import wave


def chunk(name, payload):
    return name + struct.pack("<I", len(payload)) + payload + (b"\0" if len(payload) % 2 else b"")


def prepare(input_path, output_path, guard_frames=2048, start_frame=0, end_frame=None):
    input_path = Path(input_path)
    output_path = Path(output_path)
    if input_path.resolve() == output_path.resolve():
        raise ValueError("Use a separate output file; the inspected source is never overwritten.")
    if guard_frames < 1024:
        raise ValueError("Unity's WebGL AAC workaround requires at least 1,024 guard frames.")

    with wave.open(str(input_path), "rb") as source:
        if source.getcomptype() != "NONE":
            raise ValueError("Input must be an uncompressed integer PCM WAV.")
        channels, width, rate, total_frames, _, _ = source.getparams()
        end_frame = total_frames if end_frame is None else end_frame
        if not 0 <= start_frame < end_frame <= total_frames:
            raise ValueError("Frame range must satisfy 0 <= start < end <= source frames.")
        source.setpos(start_frame)
        musical = source.readframes(end_frame - start_frame)

    frame_bytes = channels * width
    musical_frames = end_frame - start_frame
    if len(musical) != musical_frames * frame_bytes:
        raise ValueError("Source PCM data is truncated.")
    # Eight-bit PCM is unsigned; all wider integer PCM uses signed zero.
    silence_byte = b"\x80" if width == 1 else b"\0"
    guard = silence_byte * (guard_frames * frame_bytes)
    loop_start = guard_frames
    loop_end = guard_frames + musical_frames - 1  # RIFF smpl end is inclusive.
    pcm = guard + musical
    fmt = struct.pack("<HHIIHH", 1, channels, rate, rate * frame_bytes, frame_bytes, width * 8)
    smpl = struct.pack("<9I", 0, 0, round(1_000_000_000 / rate), 60, 0, 0, 0, 1, 0)
    smpl += struct.pack("<6I", 0, 0, loop_start, loop_end, 0, 0)
    body = b"WAVE" + chunk(b"fmt ", fmt) + chunk(b"smpl", smpl) + chunk(b"data", pcm)
    output = b"RIFF" + struct.pack("<I", len(body)) + body
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_bytes(output)
    # Compare the committed output's region, not only the intermediate buffer.
    with wave.open(str(output_path), "rb") as prepared:
        prepared.setpos(loop_start)
        if prepared.readframes(musical_frames) != musical:
            raise RuntimeError("Prepared output failed byte-for-byte musical sample verification.")
    return {
        "input": str(input_path),
        "output": str(output_path),
        "sampleRate": rate,
        "channels": channels,
        "sampleWidthBytes": width,
        "sourceFrames": total_frames,
        "selectedSourceFrames": [start_frame, end_frame],
        "guardFrames": guard_frames,
        "loopStartFrame": loop_start,
        "loopEndFrameInclusive": loop_end,
        "musicalFrames": musical_frames,
        "musicalSeconds": musical_frames / rate,
        "outputFrames": guard_frames + musical_frames,
        "outputBytes": len(output),
        "musicalSamplesPreserved": True,
        "sha256": hashlib.sha256(output).hexdigest(),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--guard-frames", type=int, default=2048)
    parser.add_argument("--start-frame", type=int, default=0)
    parser.add_argument("--end-frame", type=int, help="Exclusive end frame; default is the full source.")
    args = parser.parse_args()
    try:
        report = prepare(args.input, args.output, args.guard_frames, args.start_frame, args.end_frame)
    except (ValueError, wave.Error) as error:
        parser.error(str(error))
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
