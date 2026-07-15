# Color Stack Rush

A hypercasual **"roll forward, swipe to match the color"** mobile game, built solo in
**Unity 6 (6000.3.3f1)** with **C#**. The entire game — meshes, materials, particles, SFX and
music — is **generated procedurally from code and Unity primitives**; there are no external
model, texture or audio assets and no prefab files. All runtime objects are constructed in code.

> Source lives under [`Assets/ColorStackRush/`](Assets/ColorStackRush/).

## Gameplay

A ball auto-runs down a track and accelerates. A glowing ring beneath it shows the currently
required color, which rotates every 15–20s. Matching-color blocks grow your stack (health) and
score; wrong-color blocks and obstacles shrink it; an empty stack ends the run. Crossing the
finish gate converts your remaining stack into escalating multiplier bonus points on the
"multiplier stairs."

One finger, no rules to read. Coins, unlockable skins, power-ups and a daily-reward streak keep
the loop going.

## Engineering highlights

- **Event-driven architecture** — systems communicate only through a static `GameEvents` hub
  (`PlayerDied`, `BlockCollected`, `ScorePopup`, `CoinCollected`, `PowerUpStarted`, …). Audio,
  camera shake, floating text and UI *react* to events instead of holding direct references,
  keeping systems decoupled.
- **Single entry point** — `GameBootstrapper.Awake()` (`[DefaultExecutionOrder(-100)]`) builds
  the whole runtime scene graph in dependency order: managers → player → world → camera rig → UI.
- **Performance-first** — object pooling for everything spawned repeatedly (blocks, coins,
  obstacles, ground tiles, floating text), a shared `MaterialCache` to avoid per-frame
  allocations, and event/coroutine-driven logic instead of blanket `Update()` calls.
- **Full game systems, all in code** — score/combo, currency, shop, power-ups (magnet, double
  coins, shield, slow motion, lucky box), daily rewards, and a complete UI stack (HUD, main menu,
  pause, game over, victory, shop, settings) built by a `UIBuilder`/`UIFactory`.
- **Persistence** — JSON save via `SaveManager` (coins, high score, level, unlocked/selected
  skins, audio/haptics settings, daily-reward streak).
- **Procedural audio** — SFX and music synthesized at runtime (`SfxSynth`), so the build ships
  with zero audio files.
- Uses Unity's **new Input System** (swipe/drag), not the legacy `Input` class.

## Tech stack

`Unity 6` · `C#` · new Input System · object pooling · event bus · procedural generation ·
JSON persistence · IL2CPP + ARM64 (Android) · portrait mobile (1080×1920, 60 fps)

## Project layout

```
Assets/ColorStackRush/Scripts/
  Player/        SwipeInput, PlayerController, PlayerStack, PlayerCollision, PlayerVisuals
  Managers/      GameBootstrapper, GameManager, GameEvents, ColorManager, SpawnManager,
                 ScoreManager, CurrencyManager, PowerUpManager, ShopManager, DailyRewardManager
  UI/            UIBuilder, UIFactory, UIManager, HUD/MainMenu/Pause/GameOver/Victory/Shop/
                 DailyReward/Settings panels, FloatingTextManager
  Obstacles/     Obstacle, FinishTrigger
  Collectibles/  Collectible, CollectibleBlock, Coin, PowerUpPickup
  Camera/        CameraFollow, CameraShake
  Audio/         AudioManager, SfxSynth
  SaveSystem/    SaveData, SaveManager
  Utilities/     ObjectPool, Juice, MaterialCache, ParticleFactory, Primitives, ColorPalette, …
  Editor/        SceneSetupTool (Tools → Color Stack Rush menu)
```

## Running

Open in Unity `6000.3.3f1`, open `Assets/Scenes/SampleScene.unity`, then
**Tools → Color Stack Rush → Setup Scene** and press Play. In the Editor, drag with the left
mouse button or use `A`/`D` / arrow keys; on device, swipe left/right.
