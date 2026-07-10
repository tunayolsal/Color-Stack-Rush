# Color Stack Rush

A complete, production-quality hypercasual mobile game built entirely from
Unity primitives and code — **zero external assets required**.

Roll forward, swipe to steer, collect blocks matching your ring color to grow
your stack (your health!), dodge coral obstacles, and cash in your stack on the
multiplier stairs at the finish line.

---

## Quick Start (2 steps)

1. Open `Assets/Scenes/SampleScene.unity`.
2. In the menu bar run **Tools → Color Stack Rush → Setup Scene**, then press **Play**.

That's it. The `GameBootstrapper` component constructs the entire game at
startup: managers, player, camera rig, lighting, pooled world objects, UI,
sounds and music (all synthesized procedurally).

> Manual alternative: create an empty GameObject in any scene and add the
> `GameBootstrapper` component to it.

**Controls**
- Device: swipe / drag left-right.
- Editor: drag with the left mouse button, or use `A`/`D` / arrow keys.

---

## Gameplay Rules

- The ball auto-runs forward, slowly accelerating.
- The glowing **ring under the ball** (and the HUD indicator) shows the color to collect.
- Correct color → +1 stack block, +points × combo multiplier.
- Wrong color → −1 stack block, combo reset.
- Obstacle hit → −2 stack blocks, screen shake, brief invincibility.
- **Stack = health.** Stack at zero → game over.
- The active color changes every 15–20 seconds (listen for the chime).
- Cross the finish gate → the ball climbs the **multiplier stairs**, converting
  each remaining stack block into escalating bonus points (x1, x2, x3 …).
- Each level is longer than the last. Coins buy 10 unlockable ball skins.

**Power-ups:** Magnet (M), Double Coins (2X), Shield (SH), Slow Motion (SL),
Lucky Box (?).

---

## Runtime Scene Hierarchy (built by GameBootstrapper)

```
ColorStackRush            ← GameBootstrapper (the only authored object)
[Managers]                ← AudioManager, GameManager, ColorManager, ScoreManager,
                            PowerUpManager, ShopManager, DailyRewardManager, SwipeInput
Player                    ← Rigidbody(kinematic) + trigger SphereCollider
 ├─ Ball                  ← rolling sphere (skin color)
 ├─ ColorRing             ← flat cylinder tinted with the active color
 └─ ShieldOrb             ← transparent bubble (shield power-up)
StackBlocks               ← pooled trail cubes (the "snake" / health)
[World]                   ← SpawnManager + Templates + all pooled instances
CameraRig                 ← CameraFollow
 └─ Shaker                ← CameraShake
     └─ Main Camera
[UI]                      ← UIBuilder, UIManager, FloatingTextManager
 └─ Canvas                ← HUD, MainMenu, Pause, GameOver, Victory, Shop, Daily, Settings
EventSystem               ← InputSystemUIInputModule
[FX_Burst], [FX_Confetti] ← shared particle systems
Finish                    ← gate + multiplier stairs (rebuilt per level)
```

## "Prefabs" (code-built pool templates)

No prefab assets exist; `SpawnManager.BuildTemplates()` constructs inactive
template objects that the pools clone:

| Template  | Visual                                | Components |
|-----------|----------------------------------------|------------|
| Block     | 0.8 cube, palette color                | BoxCollider (trigger), `CollectibleBlock` |
| Coin      | flattened cylinder, gold emissive      | SphereCollider (trigger), `Coin` |
| Wall      | 1.2×1.2 coral cube segment             | BoxCollider (trigger), `Obstacle(Wall)` |
| Spinner   | pole + rotating 4.4-unit bar           | BoxCollider on bar, `Obstacle(Spinner)` |
| Slider    | 1.5 cube sliding across the road       | BoxCollider (trigger), `Obstacle(Slider)` |
| PowerUp   | emissive sphere + letter label         | SphereCollider (trigger), `PowerUpPickup` |
| GroundTile| 30-unit road slab + pastel edge rails  | none (decorative) |

## Script Architecture

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
  Editor/        SceneSetupTool
```

**Design principles used**
- Event-driven: systems communicate via the static `GameEvents` hub — audio,
  camera shake, floating text and UI all *react* to gameplay instead of being called.
- Object pooling everywhere (blocks, coins, obstacles, ground, floating text, stack cubes).
- No per-frame allocations in gameplay; shared cached materials for batching.
- Update() only where motion demands it; everything else is event/coroutine driven.
- Single-responsibility scripts, inspector-tunable `[SerializeField]` values.

## Save System

JSON file at `Application.persistentDataPath/colorstackrush_save.json`:
coins, high score, level, unlocked + selected skins, volume/mute/haptics
settings, daily-reward streak and last claim date.
Delete via **Tools → Color Stack Rush → Delete Save Data** or the in-game
Settings → Reset Progress.

## Mobile Build Notes

- Portrait orientation recommended (UI is authored at 1080×1920, scales both ways).
- Target 60 FPS is set in code (`Application.targetFrameRate = 60`, vSync off).
- Input uses the **new Input System** (project is already configured for it).
- Haptics use `Handheld.Vibrate()` on device; swap `HapticsManager` internals
  for a richer plugin if desired.
- Android: set Scripting Backend IL2CPP + ARM64 in Player Settings for release.

## Tuning Cheat-Sheet

All key numbers are `[SerializeField]` fields (visible once components exist at
runtime, or edit defaults in code):

- Difficulty: `PlayerController` (baseSpeed/acceleration/maxSpeed), `SpawnManager` (chances, chunk length, level length).
- Health: `PlayerStack.startBlocks`, `PlayerCollision.obstacleDamage`.
- Combo: `ScoreManager` (comboPerMultiplier, maxMultiplier, pointsPerBlock).
- Color pressure: `ColorManager` (minInterval/maxInterval).
- Power-ups: `PowerUpManager` durations.
- Feel: `CameraShake`, `CameraFollow`, `Juice` call sites.
