# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

A Unity 6 (`6000.3.3f1`) project. The actual game — **Color Stack Rush**, a
hypercasual "roll forward, swipe to match color" mobile game — lives entirely
under `Assets/ColorStackRush/`. It is built **100% from code and Unity
primitives**: no external models, textures, or audio files. Everything
(meshes, materials, particles, SFX/music) is generated procedurally at
runtime by `GameBootstrapper`.

Two unrelated JS projects (`chroma-hole/`, `hook-rush` — the latter is a
sibling directory *outside* this repo, referenced only via
`.claude/launch.json`) are gitignored here and being moved to their own
repos (see the "Remove chroma-hole and nimbalyst-local" commit) — don't
treat them as part of this project's source tree.

## Running the game

Build and test CLI instructions are in README.md. ReleaseBuilder generates the Android release scene; Unity Test Runner hosts EditMode and PlayMode tests.

1. Open the project in Unity `6000.3.3f1` (Unity Hub will prompt to install
   the matching editor version from `ProjectSettings/ProjectVersion.txt`).
2. Open `Assets/Scenes/SampleScene.unity`.
3. Menu bar → **Tools → Color Stack Rush → Setup Scene**, then press Play.
   (Alternative: add a `GameBootstrapper` component to any empty GameObject
   in any scene — it builds the entire runtime hierarchy in `Awake()`.)
4. **Tools → Color Stack Rush → Delete Save Data** resets the JSON save at
   `Application.persistentDataPath/colorstackrush_save.json`.

Controls: swipe/drag left-right on device; in the Editor, drag with LMB or
use `A`/`D`/arrow keys.

## Architecture

`GameBootstrapper.Awake()` ([Assets/ColorStackRush/Scripts/Managers/GameBootstrapper.cs](Assets/ColorStackRush/Scripts/Managers/GameBootstrapper.cs))
is the single entry point and constructs the whole runtime scene graph in
dependency order — managers first, then player, world, camera rig, and UI
last (so UI can safely query manager state on build). `[DefaultExecutionOrder(-100)]`
ensures this runs before any other `Start()`.

```
Scripts/
  Player/        SwipeInput, PlayerController, PlayerStack, PlayerCollision, PlayerVisuals
  Managers/      GameBootstrapper, GameManager, GameEvents, ColorManager, SpawnManager,
                 ScoreManager, CurrencyManager, PowerUpManager, ShopManager, DailyRewardManager
  UI/            UIBuilder, UIFactory, UIManager, HUDPanel, MainMenuPanel, PausePanel,
                 GameOverPanel, VictoryPanel, ShopPanel, DailyRewardPanel, SettingsPanel,
                 FloatingTextManager
  Obstacles/     Obstacle, FinishTrigger
  Collectibles/  Collectible (base), CollectibleBlock, Coin, PowerUpPickup
  Camera/        CameraFollow, CameraShake
  Audio/         AudioManager, SfxSynth (procedural SFX + music)
  SaveSystem/    SaveData, SaveManager (JSON @ persistentDataPath)
  Utilities/     ObjectPool, Juice, MaterialCache, ParticleFactory, Primitives,
                 WorldText, ColorPalette, GameEnums, HapticsManager
  Editor/        SceneSetupTool (the Tools → Color Stack Rush menu)
```

**Everything is code-built, nothing is a prefab asset.** `SpawnManager.BuildTemplates()`
constructs inactive template GameObjects (Block, Coin, Wall, Spinner, Slider,
PowerUp, GroundTile) from primitives that `ObjectPool` then clones — this is
the pattern to follow when adding a new spawnable type.

**Communication is entirely through the static `GameEvents` hub**
([Assets/ColorStackRush/Scripts/Managers/GameEvents.cs](Assets/ColorStackRush/Scripts/Managers/GameEvents.cs)) —
systems never hold direct references to each other. Audio, camera shake,
floating text, and UI all *react* to events (`PlayerDied`, `BlockCollected`,
`ScorePopup`, `CoinCollected`, `PowerUpStarted`, etc.) rather than being
called directly. When adding a new cross-system signal, add it here rather
than wiring a direct reference. Subscribers must unsubscribe in
`OnDisable`/`OnDestroy`.

Core gameplay loop: the ball auto-runs and accelerates; a glowing ring under
it (driven by `ColorManager`) shows the currently-required color, which
changes at planned distance boundaries with an empty region and advance warning; matching-color blocks grow the stack (= health, capped at 32) and
score, wrong-color blocks or obstacles shrink it; stack at zero ends the run.
Crossing the finish gate converts remaining stack into escalating
additive bonus points (+10, +20, ...) on the finish stairs. Endless has no finish gate. RunConfig and RunResult keep campaign and endless records separate.

**Design conventions already established in this codebase — follow them for
new code:**
- Object pooling for anything spawned repeatedly (blocks, coins, obstacles,
  ground tiles, floating text, stack cubes) — never `Instantiate`/`Destroy`
  in the hot path.
- No per-frame allocations in gameplay code; use `MaterialCache` for shared/
  batched materials instead of creating new ones.
- `Update()` only on objects that need continuous motion; everything else is
  event-driven (via `GameEvents`) or coroutine-driven.
- Single-responsibility MonoBehaviours with `[SerializeField]` tunables
  (there is no central constants file like a web project would have — tuning
  knobs live as serialized fields on the relevant manager/controller, see the
  Tuning Cheat-Sheet below).
- Uses Unity's new Input System (`SwipeInput`, `InputSystem_Actions.inputactions`)
  — not the legacy `Input` class.

## Tuning cheat-sheet

All tunables are `[SerializeField]` fields on these components (edit defaults
in code, or on the instance at runtime in the Editor):
- Difficulty: `PlayerController` (baseSpeed/acceleration), `RunConfig` (length/speed cap), `TrackPlanner` (patterns and introductions). `SpawnManager` handles pooling and streaming.
- Health: `PlayerStack.startBlocks`, `PlayerCollision.obstacleDamage`.
- Combo: `ScoreManager` (comboPerMultiplier, maxMultiplier, pointsPerBlock).
- Color pressure: `TrackPlanner.ColorBand/WarningDistance/TransitionAfter`, with `ColorManager` following distance.
- Power-ups: `PowerUpManager` durations (Magnet, Double Coins, Shield, Slow Motion, Lucky Box).
- Feel: `CameraShake`, `CameraFollow`, `Juice` call sites.

## Save data

JSON at `Application.persistentDataPath/colorstackrush_save.json` via
`SaveManager`/`SaveData`: coins, high score, level, unlocked/selected skins,
volume/mute/haptics settings, daily-reward streak and last claim date. Schema v2 also holds per-level best scores/stars, endless records, tutorial, reduced motion and quality. SaveStore migrates old JSON and retains a temporary-write/backup recovery path. Tests must use isolated directories.

## Git workflow

This repo is pushed to `github.com/tunayolsal/Color-Stack-Rush`. As you do work,
commit regularly with clean, descriptive commit messages and push to GitHub
so progress is never lost — don't let uncommitted work pile up locally.

## Mobile build notes

- Portrait orientation; UI authored at 1080×1920, scales both ways.
- `QualityProfile` targets 60 FPS (high) or 30 FPS (low), vSync off; real device measurements remain necessary.
- Android release: set Scripting Backend to IL2CPP + ARM64 in Player Settings.
- Haptics via `Handheld.Vibrate()` — swap `HapticsManager` internals for a
  richer plugin if needed.
