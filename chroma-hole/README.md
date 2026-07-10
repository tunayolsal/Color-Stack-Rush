# Chroma Hole

A color-matching "hole" hypercasual game. Drag the chroma hole around, swallow
blobs that match your current color to grow, repaint yourself in paint pools,
then devour the goal capsule while big enough **and** wearing the right color.

**v1.1 additions:** a combo system (consecutive swallows stack a score bonus,
escalating pitch, and a HUD pill; every 5th combo triggers a camera punch),
spike hazards from level 20+ (static/drifting, or orbiting the capsule as
"guardians" on mega levels — same 12% penalty as a wrong color, gated by the
same cooldown), 10 coin-rewarding achievements (trophy button on the menu,
toast on unlock), and a 7-day streak-based daily gift (gift button on the
menu, resets if a day is skipped).

**v1.2 additions (growth/monetization pass):** coin IAP packs (Shop → COINS
tab, 4 tiers, still routed through the `purchaseProduct` stub - see IAP TODO
below), no interstitial ads before level 5, a native share sheet on the win
screen (falls back to clipboard-copy on desktop web), a per-level personal
best score with a "NEW BEST!" ribbon, a one-time Play Store in-app review
prompt (fires after a confident 3-star clear past level 5), a local "come
back for your gift" reminder notification (device-only, no Firebase/server),
and a limited-time "Summer" skin. (An earlier "Endless Mode" menu button that
jumped straight to level 41 was removed - it let players skip all 40 story
levels; the level-41+ loop is still there, just reached by actually playing
through, per `LevelManager.getLevelConfig`'s existing endless-band fallback.)

**v1.3 additions (gameplay depth pass):** golden blobs (6% spawn chance, all
levels - fixed 80pt/+6px reward, bypasses color-matching, still comboable),
a one-time shield pickup per level from level 3+ (fully absorbs the next
wrong-color/spike hit - no shrink, doesn't break a flawless run), timed gates
from level 26+ (true physical barriers that cycle open → red-flash warning →
closed, pushing the hole back out rather than damaging it; always leaves room
to go around), and a FLAWLESS bonus (+25 coins, badge on the win screen) for
clearing a level
with zero wrong touches - shield-absorbed hits don't count against it.

- **Stack:** Phaser 3 + Vite + vanilla ES modules (no TypeScript, no asset files —
  every sprite is generated procedurally at boot, all audio is synthesized with
  the Web Audio API).
- **Target:** mobile-first portrait (720x1280 reference, `Phaser.Scale.FIT`),
  fully playable with mouse on desktop.

## Run locally

```bash
npm install
npm run dev      # http://localhost:5173
```

## Production build

```bash
npm run build    # outputs dist/ with relative paths (base:'./')
npm run preview  # serve the build locally
```

The `dist/` folder is self-contained and can be uploaded directly to an HTML5
game portal (Poki, CrazyGames, itch.io...). The only external request is the
Google Fonts CSS for Fredoka; if a portal requires zero external requests,
self-host the font (download the woff2, add a local `@font-face` in
`index.html`) — the game falls back to system fonts automatically if the font
fails to load.

## Packaging for Android (already done once in this repo)

The `android/` folder is already set up: Capacitor added, portrait locked
(`android:screenOrientation="portrait"` in `AndroidManifest.xml`), icons +
splash generated from `store-assets/icon-1024.png`, and AdMob wired in.
To rebuild after code changes:

```bash
npm run build
npx cap sync android
cd android
JAVA_HOME="/c/Program Files/Java/jdk-21" ./gradlew.bat bundleRelease
# output: android/app/build/outputs/bundle/release/app-release.aab
```

Capacitor 8's native deps require **JDK 21** to compile (JDK 17 is not
enough) - install it if `JAVA_HOME` above doesn't exist on your machine.

### Release signing

A release keystore already exists at `keystore/chroma-hole-release.keystore`
with credentials in `keystore/KEYSTORE_CREDENTIALS_DO_NOT_COMMIT.txt` (both
git-ignored). `android/app/build.gradle` reads `keystore.properties` (repo
root, git-ignored) to sign release builds automatically - if that file is
missing, release builds are simply unsigned.

**Back up the keystore file and its password somewhere durable (password
manager, offline drive).** Losing it means you can never publish an update to
the same Play Store listing again - only a brand new listing.

### iOS

Packaging for iOS requires a Mac (Xcode only runs on macOS) - not covered in
this repo yet. When ready: `npm i -D @capacitor/ios && npx cap add ios`, then
follow the same portrait-lock / icon steps in Xcode, or use a cloud Mac CI
(e.g. Codemagic) if you don't own a Mac.

## Ads: AdMob is already wired up (test ads by default)

`src/systems/MonetizationManager.js` calls the real
[`@capacitor-community/admob`](https://github.com/capacitor-community/admob)
plugin on native builds (Android/iOS). In a browser (`npm run dev`, or any
non-native Capacitor context) it automatically falls back to a console-log
stub, since the native ad SDK has nothing to talk to there — this is expected
and not a bug.

**The real Chroma Hole AdMob account is already wired in** (`ADMOB_TEST_MODE =
false` in [`src/adConfig.js`](src/adConfig.js)), so release builds serve real
ads, not test ads:
- App ID: `ca-app-pub-8811964130270609~6411732770` (in
  `android/app/src/main/AndroidManifest.xml`)
- Interstitial unit: `ca-app-pub-8811964130270609/9430823956`
- Rewarded unit: `ca-app-pub-8811964130270609/8463181048`

Don't tap your own real ads repeatedly while testing on a device - AdMob can
flag an account for "invalid traffic" from self-clicks. If you need to test
interactively, flip `ADMOB_TEST_MODE` back to `true` in `adConfig.js` first
(this auto-swaps in Google's public test ad units, clearly labeled "Test Ad"),
rebuild, test freely, then flip it back to `false` before shipping.

A freshly created AdMob app/account can take a little while (minutes to a
day) before it reliably fills real ad requests - seeing "no fill" errors in
logcat right after setup is normal and resolves on its own.

`showInterstitialAd`/`maybeShowInterstitial` fire every 3rd level completion;
`showRewardedAd` powers the "+10s" bonus and the fail-screen revive/continue.

IAP (`purchaseProduct` / `restorePurchases`, used by the premium "Prism" skin
and Settings) is still a console-logging stub — integrate RevenueCat / Google
Play Billing separately when you're ready to sell it for real.

## Replacing procedural art / audio with real assets

- **Art:** every texture is created in
  [`src/scenes/BootScene.js`](src/scenes/BootScene.js) `buildTextures()`. To use
  real images, `this.load.image('key', 'url')` in a `preload()` and delete the
  matching generator — all game code only references texture keys.
- **Audio:** all SFX + the looping music motif are synthesized in
  [`src/systems/AudioManager.js`](src/systems/AudioManager.js).
  `TODO(AUDIO)` marks where to load a real music file instead of the
  step-sequenced motif.

## Levels

`src/data/levels.json` holds 40 data-driven levels produced by the difficulty
curve in [`scripts/generateLevels.js`](scripts/generateLevels.js)
(`npm run gen:levels` regenerates deterministically). Levels beyond 40 loop the
hardest band (endless). Curve summary: 2→5 colors, dense→sparse objects,
45s→28s timers, moving objects from level 16, moving paint pools + lower
correct-color bias from 31, mega capsules every 10th level.

## Phase 2 — .io Arena Mode (NOT implemented)

**Phase 2 requires a separate backend project.** The realtime arena mode
(multiple players on one map; bigger + correctly-colored holes swallow rivals)
needs Node.js + Socket.io or Colyseus, deployed separately (Render/Railway),
plus client prediction. Only empty skeletons ship in this repo:
[`src/scenes/ArenaScene.js`](src/scenes/ArenaScene.js) and
[`src/systems/NetworkManager.js`](src/systems/NetworkManager.js). Nothing in
the game calls them.

## Store assets

- `store-assets/icon-1024.svg` — procedural source of the app icon;
  `icon-1024.png` is exported from the live game's canvas.
- `store-assets/screenshots/` — captured from the running game via the dev-only
  `/__screenshot` endpoint in `vite.config.js` (`window.__snap('name.png')` in
  the browser console while `npm run dev` is running).
- `STORE_LISTING.md` — name/subtitle/description/keywords.

## Assumptions taken (spec left them open)

1. **Coins** come from level completion only (`20 + 10 x stars`); score is
   per-level bragging rights and does not convert to coins.
2. **Stars:** 1 for completing; +1 if accuracy ≥ 75% (correct vs wrong
   touches); +1 if finished within 70% of the time limit.
3. **Blob visuals** are drawn ~1.7x their collision radius for readability;
   collision math uses the spec's exact radii (8/14/20) and formula.
4. **Skin glow** decorates the outer halo/pattern ring only — the inner rim
   always shows the current gameplay color so readability never suffers.
5. The hole starts each level with the level's **first active color**; the
   correct-color spawn bias references the hole's *current* color.
6. **Interstitial** every 3rd completed level; **rewarded** continue: +10s on
   timeout, revive at radius 34 on collapse — each once per level attempt.
7. Time limits use 28–45s (inside the spec's 25–45 window) for fairness at the
   sparse late-game densities.
8. UI language is English, icon-first, minimal words (global audience).
9. Added `src/entities/PaintPool.js` and `src/ui/UIFactory.js` beyond the spec's
   file list (kept the mandated structure otherwise).
10. Wrong-color penalty has a 0.6s cooldown and knocks the blob away so one
    overlap can't drain the hole in successive frames.
11. Privacy policy is live at https://tunayolsal.github.io/chroma-hole-privacy/
    (source: `legal/index.html`, hosted on GitHub Pages) and linked from the
    Settings screen via `@capacitor/browser`; premium skin purchase still
    auto-succeeds in the IAP stub.
