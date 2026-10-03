# Color Stack Rush runtime

See the [project README](../../README.md) for current gameplay, save format, build commands and automated tests, and [DEVICE_QA.md](../../DEVICE_QA.md) for physical Android and new-player acceptance checks.

Everything is built by `Scripts/Managers/GameBootstrapper.cs` using primitives and procedurally generated meshes, UI sprites, materials, particles and audio. Open `Assets/Scenes/SampleScene.unity` in Unity **6000.3.3f1** and press Play. An empty scene with a GameBootstrapper component also works.

The runtime hierarchy contains managers, a kinematic rigidbody player with a sphere trigger, a pooled trail, a pooled streamed world, camera rig and safe-area Canvas. `RunConfig` identifies mode, level and seed. `TrackPlanner` fills reusable segment buffers; `TrackValidator` reserves a safe route; `SpawnManager` owns templates/pools and creates a finish gate only for campaign.

Runtime systems subscribe to `GameEvents` and unsubscribe on disable. `GameManager` owns Playing/Paused/Dying/Finish/terminal state transitions and cancels old terminal sequences when restarting or returning to the menu. Input only accumulates while Playing. `RunCompleted` commits progression once, while earned coins are already in the wallet.

Code modules:

| Folder | Responsibility |
| --- | --- |
| Scripts/Player | Input, fixed-step steering, trail, collision and player visuals |
| Scripts/Managers | Run lifecycle, deterministic planning/streaming, color, score, powers, shop/daily rewards |
| Scripts/UI | Procedural screens, safe area, tutorial, HUD and results |
| Scripts/SaveSystem | Version 2 migration and temporary/backup JSON writes |
| Scripts/Utilities | Object pool, meshes/symbols, materials, effects, quality and themes |
| Scripts/Audio | Synthesized music and event-specific feedback |
| Scripts/Camera | Follow, bounded shake and reduced motion |
| Tests/EditMode | Determinism/safety, saves, purchases and pool invariants |
| Tests/PlayMode | Real input, physics, state cancellation, full campaign and UI regression checks |

Controls: one-finger drag on Android; mouse drag or A/D/arrows in the editor. Template colliders must agree with the swept bounds in TrackPlanner whenever obstacle sizes or amplitudes change. Preserve the application identity and script GUIDs when modifying build or scene setup, so existing installations and scenes keep their data/references.
