# Color Stack Rush runtime

See the [project README](../../README.md) for current gameplay, save format, build commands and automated tests, and [WEB_QA.md](../../WEB_QA.md) for browser, physical-device and player acceptance checks.

Everything is built by `Scripts/Managers/GameBootstrapper.cs` using procedural toy meshes, materials and audio with selected CC0 sprites and environment models in Assets/Art. Open `Assets/Scenes/SampleScene.unity` in Unity **6000.3.3f1** and press Play. An empty scene with a GameBootstrapper component also works.

The runtime hierarchy contains managers, a kinematic rigidbody player with a sphere trigger, a pooled trail, a pooled streamed world, camera rig and safe-area Canvas. `RunConfig` identifies a long level ID, content version and seed. `LevelCatalog.Get(long)` calculates finite levels without a fixed cap. `TrackPlanner` fills reusable segment buffers; `TrackValidator` reserves a safe route; `SpawnManager` owns templates/pools and creates a finish gate for each level.

Runtime systems subscribe to `GameEvents` and unsubscribe on disable. `GameManager` owns Playing/Paused/Dying/Finish/terminal state transitions and cancels old terminal sequences when restarting or returning to the menu. Input only accumulates while Playing. `RunCompleted` publishes once after durable progression succeeds, while earned coins are already in the wallet.

Code modules:

| Folder | Responsibility |
| --- | --- |
| Scripts/Player | Input, fixed-step steering, trail, collision and player visuals |
| Scripts/Managers | Run lifecycle, deterministic planning/streaming, color, score, powers, shop/daily rewards |
| Scripts/UI | Procedural screens, safe area, tutorial, HUD and results |
| Scripts/SaveSystem | Version 3 sparse records, migration, backup writes and acknowledged IndexedDB synchronization |
| Scripts/Utilities | Object pool, toy meshes, CC0 art loading, materials, effects, quality and themes |
| Scripts/Audio | Synthesized music and event-specific feedback |
| Scripts/Camera | Follow, bounded shake and reduced motion |
| Tests/EditMode | Determinism/safety, saves, purchases and pool invariants |
| Tests/PlayMode | Real input, physics, state cancellation, level progression and UI regression checks |

Controls: one-finger drag on phones; mouse drag or A/D/arrows on computers. Template colliders must agree with the swept bounds in TrackPlanner whenever obstacle sizes or amplitudes change. Preserve the application identity and script GUIDs when modifying build or scene setup, so existing installations and scenes keep their data/references.
