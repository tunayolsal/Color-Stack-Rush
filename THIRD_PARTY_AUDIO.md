# Third-party audio

Only the twelve files listed below are included in the game. Both source packs are **CC0 1.0**; no paid assets are used.

## Music

**Space Ranger — seamless loop** by **Nostromo**. Source: <https://opengameart.org/content/music-loop-strong-downtempo-seamless>.

- Original download: <https://opengameart.org/sites/default/files/space_ranger_seamless_loop.wav>
- License: CC0 1.0, <https://creativecommons.org/publicdomain/zero/1.0/>
- Source SHA-256: `b7af8f7ca80958bcc79cb49faac128cd85093e484b8a42e661c2cda623a6521c`
- Shipping file: `Assets/Audio/Resources/Audio/ArcadeGrooveLoop.wav`
- The original **3,175,200 stereo PCM16 sample frames / 72 seconds / 44.1 kHz** are preserved byte for byte. No fades, time stretching, or intro/outro silence are added to the musical region.
- Preparation: `tools/audio/prepare_music_loop.py` adds a 2,048-frame silent encoder guard and a forward WAV `smpl` loop. Loop start is frame 2,048; inclusive end is frame 3,177,247. The guard lies outside the repeating region.
- This follows [Unity 6.3's documented WebGL AAC loop workaround](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-audio.html#loop-issues). The importer preserves the WAV sample rate so the loop frame indices remain accurate.
- Shipping SHA-256: `ce6bfc9c7372977d95b309cfe5a13919dfa69bbc99043b98316f9c92030c71e9`

The author's source page identifies this version as a seamless downtempo music loop and labels it CC0. Attribution is voluntary and retained here. The previous Happy Clappy track is no longer included in Resources or the public build.

## Sound effects

**Interface Sounds 1.0** by **Kenney**. Source: <https://kenney.nl/assets/interface-sounds>.

- Original download: <https://kenney.nl/media/pages/assets/interface-sounds/fa43c1dd4d-1677589452/kenney_interface-sounds.zip>
- License: CC0 1.0, <https://creativecommons.org/publicdomain/zero/1.0/>
- Download SHA-256: `f2193d072726d6758a5f7871b2dcc54dcce0d5c35c6f0a62f92549b327c81232`
- Original license notice: `Assets/Audio/Kenney-License.txt`
- Selected source files are renamed for their in-game roles; their Ogg source bytes are unchanged.

| In-game role / shipping filename | Kenney source filename |
| --- | --- |
| Collect | pluck_001.ogg |
| Wrong | error_001.ogg |
| Hit | drop_004.ogg |
| Coin | glass_002.ogg |
| Button | click_001.ogg |
| PowerUp | maximize_001.ogg |
| Stair | select_002.ogg |
| Win | confirmation_002.ogg |
| Lose | close_002.ogg |
| ColorChange | switch_001.ogg |
| Buy | confirmation_004.ogg |

## Runtime and import

The bundled source audio totals **12,805,456 bytes** (about 12.21 MiB), excluding metadata/license files. Music uses **Decompress On Load** so WebGL's AudioBufferSourceNode honors the exact loop boundaries. The music's decoded Float32 budget is about 24.24 MiB at 44.1 kHz. CompressedInMemory in the shipped Unity framework uses HTMLMediaElement looping, which ignores these boundaries and would repeat the silent guard. The same decompressed default is retained for native targets, making the precision policy explicit in the committed importer metadata. The eleven short effects import as mono, preloaded clips at 22.05 kHz for immediate feedback. Measure the actual WebGL download on the compressed build, rather than equating the WAV source size to its download size.

On iOS Safari, uncompressed Web Audio follows the device's Silent Mode switch, as documented by Unity; this browser-only precision choice prioritizes the requested loop continuity.

Music starts after the first Play action, at base mix level 0.25. Native AudioSource looping handles the wrap; results, retries, new levels and volume changes do not restart it. Effects use base level 0.75 and retain saved volume/mute settings. Collection and stair pitch still rise with their existing combo/progress. Procedural audio remains a missing-asset fallback only.

`Assets/Audio/audio-manifest.json` records durations, source mapping, byte sizes and SHA-256 checksums. Asset verification is not a listening test; loop audibility and feedback balance must also be checked in the browser.
