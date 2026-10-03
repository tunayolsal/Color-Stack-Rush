# Color Stack Rush

A portrait Android runner made entirely with Unity primitives and C# generated meshes, UI, particles, music and sound effects. Unity version: **6000.3.3f1**.

Drag left and right to collect the active color and geometric symbol. Matching blocks score points and grow a trail with a maximum of 32 blocks. Further matches still score. Wrong colors and obstacles remove blocks; an empty trail ends the run. Cosmetics only change appearance.

## Modes

- **Campaign:** 18 repeatable levels, unlocked in order. Levels 1–6 introduce collection, wall gaps, sliders, color transitions, spinners and combinations. Levels 7–12 use Sunset; levels 13–18 use Midnight. Completing a level earns one star, climbing eight stairs earns two and fourteen earns three. Level scores and stars retain their best values.
- **Endless:** available immediately, without a finish gate. Speed rises to a maximum of 18 and later segments introduce more patterns. Score and distance records are separate from campaign progress.

Eight patterns are planned from a run seed. Campaign seeds are fixed. Gameplay generation uses its own hash so audio and visual random effects cannot change the course. Color changes happen at distance boundaries, with an advance warning and an empty transition region. The validator reserves a reachable corridor including each moving obstacle's swept bounds and substitutes a collection segment for invalid content.

The HUD includes active/next color, symbols, trail size, progress and power-up timers. The first level teaches dragging and matching; Settings can replay it. UI uses `Screen.safeArea`. Settings also offer reduced camera motion and 30/60 FPS quality targets.

## Open and play

1. Open this project in Unity Hub using **6000.3.3f1**.
2. Open `Assets/Scenes/SampleScene.unity` and press Play. **Tools → Color Stack Rush → Setup Scene** can prepare another empty scene.
3. Drag with the left mouse button or use A/D/arrow keys. On Android, drag with one finger. A gesture beginning over a button does not steer, and another finger cannot inherit an existing gesture.

## Build

Install Android Build Support including SDK/NDK and OpenJDK for this editor and activate its Unity license. Then choose **Tools → Color Stack Rush → Build Android APK**. It generates the release scene and builds an IL2CPP ARM64 APK at `Builds/Android/ColorStackRush.apk`.

Batch example (replace the paths with your local paths):

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.3f1/Editor/Unity.exe' `
  -batchmode -quit -projectPath 'C:/projects/Color-Stack-Rush' `
  -buildTarget Android -executeMethod ReleaseBuilder.BuildAndroid `
  -outputPath 'C:/builds/ColorStackRush.apk' -logFile 'C:/builds/android.log'
```

Application identity is preserved to retain existing save locations. APKs use Unity's local debug signing; store publishing and release keystore setup are outside this change.

## Tests

Use **Window → General → Test Runner** for EditMode and PlayMode tests. Batch invocation:

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.3f1/Editor/Unity.exe' `
  -batchmode -projectPath 'C:/projects/Color-Stack-Rush' `
  -runTests -testPlatform EditMode `
  -testResults 'C:/builds/editmode.xml' -logFile 'C:/builds/editmode.log'
# Repeat using -testPlatform PlayMode. Do not add -quit to a test invocation.
```

Tests cover save migration and backup recovery, separate mode records, deterministic and reachable generation across 1,000 seeds, pool double release, terminal sequence cancellation, immediate death lock, background saving, pause/input isolation, UI gestures and multiple fingers. A PlayMode course bot drives all 18 levels through real input and physics. Add `-snapshotPath C:/builds/previews` to produce screenshots and the campaign results CSV during this test. Test saves use temporary directories and do not reset the player's save.

Real Android performance, thermal stability, notch behavior and five-player acceptance testing still require the process in [DEVICE_QA.md](DEVICE_QA.md). A target FPS setting or an Editor test is not a device performance measurement.

## Architecture and saves

`GameBootstrapper` creates the runtime hierarchy. `GameManager.StartRun(RunConfig)` owns run/state transitions; `RunResult` is emitted once through `GameEvents.RunCompleted`. Movement uses a kinematic rigidbody in `FixedUpdate`. `TrackPlanner` and `TrackValidator` plan/validate reusable buffers; `SpawnManager` streams pooled objects.

Save schema version 2 keeps existing coins, cosmetics, settings and the legacy general high score. It adds campaign scores/stars, endless records, tutorial status, reduced motion and quality. The old general record is preserved separately. Writes flush a temporary file and retain a last good `.bak`; a corrupt primary loads the backup. Coins are earned immediately and saved when backgrounded; the result screen does not grant them again. Shop purchases persist wallet debit, unlock and selection together. Daily claims require a strictly later device date.

Game source and tests are under `Assets/ColorStackRush/`; batch release entry points are under `Assets/Editor/ReleaseBuilder.cs`. No ads, account, server, online leaderboard or store integration is included.
