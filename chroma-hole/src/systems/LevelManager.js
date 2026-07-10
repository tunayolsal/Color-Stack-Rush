/**
 * LevelManager - reads levels.json and turns a level number into a full
 * runtime config (object counts, size weights, etc. derived from the
 * difficulty curve). Levels beyond 40 loop the hardest band so the game
 * is effectively endless.
 */
import levels from '../data/levels.json';

const DENSITY_COUNTS = { dense: 26, medium: 18, sparse: 12 };

// Object size tiers - exact values from the spec.
export const SIZE_DEFS = [
  { r: 8, score: 10, grow: 2 },
  { r: 14, score: 25, grow: 4 },
  { r: 20, score: 50, grow: 7 },
];

export function getLevelConfig(n) {
  let def = levels.find((l) => l.level === n);
  if (!def) {
    // Endless mode: repeat the 31-40 band with the requested level number.
    const base = levels[30 + ((n - 31) % 10)] || levels[levels.length - 1];
    def = { ...base, level: n };
  }
  const t = Math.min(1, (def.level - 1) / 39);
  const lv = def.level;
  return {
    ...def,
    maxObjects: DENSITY_COUNTS[def.objectDensity] || 18,
    // Later levels skew towards bigger (higher value, rarer) objects.
    sizeWeights: [0.5 - 0.2 * t, 0.35 + 0.05 * t, 0.15 + 0.15 * t],
    coinBase: 20,
    // Spike hazards (derived here rather than stored in levels.json so the
    // json keeps the spec's shape): none before 20, then ramping up.
    // On mega levels two of them orbit the goal capsule as guardians.
    spikeCount: lv < 20 ? 0 : lv < 30 ? 2 : lv < 40 ? 3 : 4,
    orbitSpikes: def.mega && lv >= 20 ? 2 : 0,
    spikesDrift: lv >= 30,
    // Golden blob: flat chance on every spawn, all levels.
    goldenChance: 0.06,
    // One free shield pickup per level from level 3 on (no respawn).
    hasShield: lv >= 3,
    // Timed gates: level 26+, a single choke-point that telegraphs before closing.
    gateCount: lv >= 26 ? 1 : 0,
  };
}

/** Weighted pick of a size tier index (0=small 1=medium 2=large). */
export function pickSizeIndex(weights) {
  const total = weights[0] + weights[1] + weights[2];
  let r = Math.random() * total;
  for (let i = 0; i < 3; i++) {
    if (r < weights[i]) return i;
    r -= weights[i];
  }
  return 0;
}

export function levelCount() {
  return levels.length;
}
