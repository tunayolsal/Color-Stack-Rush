/**
 * Blob - a swallowable abstract shape. Instances are pooled by GameScene:
 * `alive === false` means "free slot". All animation is manual (no tweens)
 * so recycling is allocation-free.
 */
import Phaser from 'phaser';
import { COLORS, GAME_W } from '../constants.js';

const SHAPE_KEYS = ['blob-round', 'blob-blobby', 'blob-crystal'];
const TEXTURE_R = 40; // radius baked into blob textures
const BOUNDS = { x0: 36, x1: GAME_W - 36, y0: 180, y1: 1240 };

// Golden blobs are a fixed, generous reward regardless of size roll - players
// learn to recognize and detour for "the shiny one".
export const GOLDEN_DEF = { r: 15, score: 80, grow: 6 };

export default class Blob extends Phaser.GameObjects.Container {
  constructor(scene) {
    super(scene, 0, 0);
    this.shape = scene.make.image({ key: 'blob-round', add: false });
    this.hl = scene.make.image({ key: 'particle', add: false })
      .setAlpha(0.5).setDisplaySize(26, 26).setPosition(-13, -14);
    this.glow = scene.make.image({ key: 'glow', add: false })
      .setTint(0xffc93c).setAlpha(0).setDisplaySize(110, 110); // golden halo, hidden unless golden
    this.add([this.glow, this.shape, this.hl]);
    this.setDepth(5);
    scene.add.existing(this);

    this.alive = false;
    this.state = 'dead';
    this.phase = Math.random() * Math.PI * 2;
    this.isGolden = false;
    this.setVisible(false);
  }

  spawn({ x, y, colorKey, sizeIndex, def, moving, speedScale = 1, golden = false }) {
    this.alive = true;
    this.state = 'appearing';
    this.t = 0;
    this.colorKey = colorKey;
    this.sizeIndex = sizeIndex;
    this.isGolden = golden;
    const useDef = golden ? GOLDEN_DEF : def;
    this.radius = useDef.r;         // gameplay/collision radius (spec values)
    this.visualR = useDef.r * 1.7;  // drawn a bit larger for readability
    this.setPosition(x, y).setVisible(true);
    this.shape.setTexture(golden ? 'blob-crystal' : SHAPE_KEYS[Math.floor(Math.random() * SHAPE_KEYS.length)]);
    this.shape.setTint(golden ? 0xffc93c : COLORS[colorKey]);
    this.glow.setAlpha(golden ? 0.55 : 0);
    if (moving) {
      const a = Math.random() * Math.PI * 2;
      const spd = (40 + Math.random() * 60) * speedScale;
      this.vx = Math.cos(a) * spd;
      this.vy = Math.sin(a) * spd;
    } else {
      this.vx = 0;
      this.vy = 0;
    }
    this.kx = 0; // knockback velocity
    this.ky = 0;
    this.setScale(0.001);
  }

  /** Begin the agar.io-style suction: spiral into the hole while shrinking. */
  startSwallow(hole) {
    this.state = 'swallowing';
    this.t = 0;
    const dx = this.x - hole.x;
    const dy = this.y - hole.y;
    this.swAngle = Math.atan2(dy, dx);
    this.swDist = Math.max(1, Math.hypot(dx, dy));
    this.swStartDist = this.swDist;
    this.swSpin = (Math.random() < 0.5 ? -1 : 1) * (4 + Math.random() * 3);
  }

  knockbackFrom(hole) {
    const a = Math.atan2(this.y - hole.y, this.x - hole.x);
    this.kx = Math.cos(a) * 460;
    this.ky = Math.sin(a) * 460;
  }

  kill() {
    this.alive = false;
    this.state = 'dead';
    this.setVisible(false);
  }

  update(dt, scene) {
    if (!this.alive) return;

    if (this.state === 'appearing') {
      this.t += dt * 4;
      if (this.t >= 1) { this.t = 1; this.state = 'idle'; }
    } else if (this.state === 'swallowing') {
      // Vortex suction: accelerate along an inward spiral around the hole's
      // live position, stretching toward the center and thinning out as the
      // blob crosses the rim - it visibly disappears INTO the void.
      const h = scene.hole;
      this.t += dt;
      const prog = 1 - this.swDist / this.swStartDist;
      this.swDist = Math.max(0, this.swDist - (150 + this.t * 3000) * dt);
      this.swAngle += this.swSpin * dt * (1 + prog * 2.4);
      this.x = h.x + Math.cos(this.swAngle) * this.swDist;
      this.y = h.y + Math.sin(this.swAngle) * this.swDist;
      const inside = Phaser.Math.Clamp(this.swDist / Math.max(h.radius, 24), 0, 1);
      const s = (this.visualR / TEXTURE_R) * (0.25 + 0.75 * inside);
      this.rotation = this.swAngle; // local X axis = radial direction
      this.setScale(s * (1 + 0.55 * (1 - inside)), s * Math.max(0.15, inside));
      if (this.swDist <= Math.max(6, h.radius * 0.12)) {
        // color splash right where it sinks into the void
        scene.particles.swallowBurst(this.x, this.y, COLORS[this.colorKey], this.sizeIndex);
        this.kill();
      }
      return;
    }

    if (this.state === 'idle') {
      this.x += (this.vx + this.kx) * dt;
      this.y += (this.vy + this.ky) * dt;
      const decay = Math.max(0, 1 - 5 * dt);
      this.kx *= decay;
      this.ky *= decay;
      if (this.x < BOUNDS.x0) { this.x = BOUNDS.x0; this.vx = Math.abs(this.vx); }
      if (this.x > BOUNDS.x1) { this.x = BOUNDS.x1; this.vx = -Math.abs(this.vx); }
      if (this.y < BOUNDS.y0) { this.y = BOUNDS.y0; this.vy = Math.abs(this.vy); }
      if (this.y > BOUNDS.y1) { this.y = BOUNDS.y1; this.vy = -Math.abs(this.vy); }
    }

    // idle pulse + slight rocking, all sine-based (no tweens on pooled objects)
    const now = scene.time.now;
    const pulse = 1 + Math.sin(now * 0.004 + this.phase) * (this.isGolden ? 0.14 : 0.07);
    const appear = this.state === 'appearing' ? this.t * (2 - this.t) : 1;
    this.setScale((this.visualR / TEXTURE_R) * pulse * appear);
    this.rotation = Math.sin(now * 0.001 + this.phase) * 0.15;
    if (this.isGolden) this.glow.setAlpha(0.4 + Math.sin(now * 0.006 + this.phase) * 0.2);
  }
}
