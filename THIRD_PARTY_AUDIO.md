# Third-party audio

Only the twelve files listed below are included in the game. Both source packs are **CC0 1.0**; no paid assets are used.

## Music

**Happy Clappy Loop** by **OwlishMedia**. Source: <https://opengameart.org/content/happy-clappy-loop>.

- Original download: <https://opengameart.org/sites/default/files/HappyClappyLoop.wav>
- License: CC0 1.0, <https://creativecommons.org/publicdomain/zero/1.0/>
- Source SHA-256: `1ca66b4e4d41e14a4ccfcc3b758e061cf283b9d5e332c4ea8e00f94f4deb6554`
- Shipping file: `Assets/Audio/Resources/Audio/HappyClappyLoop.ogg`
- Conversion: FFmpeg, Vorbis quality 3, stereo 44.1 kHz. The original **769,132 sample frames / 17.440635 seconds** are preserved with no trimming or compositional changes. A 5 ms linear fade at each edge reduces the measured loop-boundary discontinuity.

The author's source page identifies the piece as a seamless cheerful piano game loop and labels it CC0. Attribution is voluntary and retained here.

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

The bundled source audio totals **386,042 bytes** (about 377 KiB), excluding metadata/license files. The music stays compressed in memory. The eleven short effects import as mono, preloaded clips at 22.05 kHz for immediate feedback. The actual WebGL download delta should be measured on the built compressed data file, since Unity imports/re-encodes assets.

Music starts after the first Play action, at base mix level 0.25. Effects use base level 0.75 and retain saved volume/mute settings. Collection and stair pitch still rise with their existing combo/progress. Procedural audio remains a missing-asset fallback only.

`Assets/Audio/audio-manifest.json` records durations, source mapping, byte sizes and SHA-256 checksums. Asset verification is not a listening test; loop audibility and feedback balance must also be checked in the browser.
