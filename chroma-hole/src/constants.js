// Shared game-wide constants: reference resolution, palette, font.

export const GAME_W = 720;
export const GAME_H = 1280;

// Gameplay palette (from the art spec)
export const COLORS = {
  red: 0xff5c5c,
  blue: 0x4d96ff,
  yellow: 0xffd93d,
  green: 0x6bcb77,
  purple: 0xb983ff,
};

export const COLOR_KEYS = Object.keys(COLORS);
export const PALETTE = Object.values(COLORS);

export const CREAM = 0xf7f3e9; // background
export const INK = 0x1a1a2e;   // hole "void" color / dark UI
export const INK_CSS = '#1A1A2E';
export const CREAM_CSS = '#F7F3E9';

export const FONT = 'Fredoka, "Baloo 2", "Trebuchet MS", sans-serif';

// Hole tuning (exact values from the spec)
export const HOLE_START_RADIUS = 28;
export const HOLE_MIN_RADIUS = 15;
export const HOLE_MAX_RADIUS = 140;
export const HOLE_FOLLOW = 0.18; // per-frame lerp factor at 60fps

export function colorOf(key) {
  return COLORS[key] ?? 0xffffff;
}

export function cssColor(hex) {
  return '#' + hex.toString(16).padStart(6, '0');
}
