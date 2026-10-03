# Android and player acceptance checks

These checks require physical devices and new players. Record actual results; leave unexecuted checks pending.

## Deliveries

1. Controls/state/pooling/save hardening: verify pause/resume, quick retry, single result, saved balances/skins/settings.
2. First six levels and endless: verify deterministic retries, tutorials and color warnings, safe routes and separate records.
3. Remaining twelve levels and polish: verify all themes, unlocks/stars, safe area, quality and reduced motion.

The current source combines all three implementation stages. Build one APK from a tested commit with `ReleaseBuilder.BuildAndroid`, record its SHA and attach the matching test XML. Earlier stage APKs must come from their own tested commits rather than renamed copies of the final APK.

## Device matrix

Use one low-end and one mid-range ARM64 Android phone, including a tall/notched screen. Record model, SoC, RAM, Android version, APK SHA, quality preset and Unity Profiler capture location. Test while offline.

| Check | Low-end | Mid-range |
| --- | --- | --- |
| Cold launch, audio and procedural shader visibility | Pending | Pending |
| Drag, first-finger ownership and gesture starting over Pause | Pending | Pending |
| Pause/resume and focus loss while dragging: no jump | Pending | Pending |
| Ten rapid retries during death/finish: no stale result | Pending | Pending |
| Background after coin pickup; relaunch preserves wallet | Pending | Pending |
| Import v1 save; coins, skins and settings retained | Pending | Pending |
| Corrupt primary; last good backup loads | Pending | Pending |
| Levels 1–18 unlock, stars and records persist | Pending | Pending |
| Color warnings remain at least two seconds at maximum speed | Pending | Pending |
| Daily claim on same/older date is refused | Pending | Pending |
| All screens and buttons inside safe area | Pending | Pending |
| Reduced camera motion and quality presets work | Pending | Pending |
| 30-minute endless run: no crash or sustained memory growth | Pending | Pending |

After warm-up, use Unity Profiler on the phone to measure median and worst frame times, CPU/GPU, GC allocations and total/managed memory every five minutes. The targets are 30 FPS low-end and 60 FPS mid-range, with no per-frame allocations in steering, physics collision processing and track generation. Inspect allocation call stacks before treating a target as met. Editor screenshots and accelerated simulated runs do not replace this measurement. Keep generated UI text/effect allocation separate in the capture so remaining costs are visible.

If an earlier Android installation exists, install an update without uninstalling first. Keep the same application ID and signing key for this check. A signature mismatch requires building with the earlier signing key; do not erase its data to make the upgrade test pass.

## New-player acceptance

Recruit five players who have not played the game. Do not explain steering or matching externally. Observe their first run, then allow them to choose the next action.

| Player | Learned drag + color rule unaided | Chose to retry | Confusing moment |
| --- | --- | --- | --- |
| 1 | Pending | Pending | |
| 2 | Pending | Pending | |
| 3 | Pending | Pending | |
| 4 | Pending | Pending | |
| 5 | Pending | Pending | |

Accept at least four learning both rules and at least three choosing another run. If these thresholds fail, tune tutorials, controls and difficulty before adding content. Record campaign run duration excluding the stairs; target 25–60 seconds.
