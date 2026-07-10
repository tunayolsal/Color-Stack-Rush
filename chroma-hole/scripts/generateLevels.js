/**
 * Difficulty-curve generator for levels.json.
 * Run with: npm run gen:levels
 *
 * The curve (deterministic, no RNG so re-runs are stable):
 *  - Levels 1-5   : 2 colors, dense objects, loose timers        (onboarding)
 *  - Levels 6-15  : 3 colors, medium density                     (core loop)
 *  - Levels 16-30 : 4 colors, moving objects appear, tighter time
 *  - Levels 31-40 : 4-5 colors, lower correct-color bias, moving paint pools
 *  - Every 10th level is a "mega" capsule milestone.
 */
import fs from 'node:fs';

const COLOR_KEYS = ['red', 'blue', 'yellow', 'green', 'purple'];

function buildLevel(n) {
  const t = Math.min(1, (n - 1) / 39); // 0..1 across the 40-level arc

  const colorCount = n <= 5 ? 2 : n <= 15 ? 3 : n <= 30 ? 4 : (n % 2 === 0 ? 5 : 4);
  const start = (n * 3) % COLOR_KEYS.length;
  const activeColors = Array.from({ length: colorCount }, (_, i) => COLOR_KEYS[(start + i) % COLOR_KEYS.length]);
  const requiredColor = activeColors[(n * 7 + 3) % colorCount];

  const wiggle = [0, -2, 2][n % 3];
  const timeLimit = Math.max(28, Math.min(45, Math.round(45 - 17 * t + wiggle)));

  const mega = n % 10 === 0;
  const targetRadius = Math.round(50 + 42 * t) + (mega ? 8 : 0);

  const spawnRate = Math.round((0.9 + 0.9 * t) * 100) / 100; // objects per second
  const objectDensity = n <= 5 ? 'dense' : n <= 30 ? 'medium' : 'sparse';
  const movingObjects = n >= 16;
  const moveChance = movingObjects ? Math.round(Math.min(0.65, 0.25 + (n - 16) * 0.016) * 100) / 100 : 0;
  const correctBias = Math.round(Math.max(0.35, 0.58 - 0.28 * t) * 100) / 100;
  const movingPools = n >= 31;

  return {
    level: n,
    activeColors,
    requiredColor,
    targetRadius,
    timeLimit,
    spawnRate,
    objectDensity,
    movingObjects,
    moveChance,
    correctBias,
    movingPools,
    mega,
  };
}

const levels = Array.from({ length: 40 }, (_, i) => buildLevel(i + 1));
const out = new URL('../src/data/levels.json', import.meta.url);
fs.writeFileSync(out, JSON.stringify(levels, null, 2) + '\n');
console.log(`Generated ${levels.length} levels -> src/data/levels.json`);
