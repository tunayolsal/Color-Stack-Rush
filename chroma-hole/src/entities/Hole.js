/**
 * Hole - the player-controlled color vortex.
 * Visuals are layered procedural textures (built once in BootScene):
 *   skinGlow (cosmetic) > rimGlow > rim > dark core > rotating swirl > pattern ring
 * The container's scale drives the gameplay radius (base texture radius 128).
 */
import Phaser from 'phaser';
import { COLORS, HOLE_START_RADIUS, HOLE_MAX_RADIUS, GAME_W, GAME_H } from '../constants.js';

const TEXTURE_R = 128; // radius baked into the boot textures

export default class Hole extends Phaser.GameObjects.Container {
  constructor(scene, x, y, skin) {
    super(scene, x, y);
    this.radius = HOLE_START_RADIUS;
    this.colorKey = 'red';
    this.targetX = x;
    this.targetY = y;
    this.speed = 0;
    this.velAngle = 0;
    this.sq = 0;      // current squash amount (smoothed)
    this.pulse = 0;   // tween-driven extra scale for feedback pops
    this.hueT = 0;    // rainbow skin phase
    this.dead = false;
    this.skin = skin || { glow: 'match', pattern: 'none', trail: 'none' };

    const mk = (key) => scene.make.image({ key, add: false });

    this.skinGlow = mk('glow').setScale(1.95).setAlpha(0.38);
    this.rimGlow = mk('glow').setScale(1.5).setAlpha(0.5);
    this.rim = mk('rim').setScale(1.06);
    this.core = mk('hole-core');
    this.swirl = mk('swirl').setScale(0.78).setAlpha(0.5);
    const patternTex = this.skin.pattern === 'dots' ? 'pattern-dots' : 'deco-ring';
    this.patternRing = mk(patternTex).setScale(1.24).setAlpha(this.skin.pattern === 'none' ? 0 : 0.85);

    this.add([this.skinGlow, this.rimGlow, this.rim, this.core, this.swirl, this.patternRing]);
    this.setDepth(10);
    scene.add.existing(this);

    this._applySkin();
    this._applyColor();
  }

  get colorHex() { return COLORS[this.colorKey]; }

  _applyColor() {
    const hex = this.colorHex;
    this.rim.setTint(hex);
    this.rimGlow.setTint(hex);
    this.swirl.setTint(hex);
    if (this.skin.glow === 'match') {
      this.skinGlow.setAlpha(0);
      if (this.skin.pattern !== 'none') this.patternRing.setTint(hex);
    }
  }

  _applySkin() {
    const g = this.skin.glow;
    if (g === 'match' || g === 'rainbow') return; // handled in _applyColor / update
    const hex = parseInt(g.slice(1), 16);
    this.skinGlow.setTint(hex);
    if (this.skin.pattern !== 'none') this.patternRing.setTint(hex);
  }

  setTarget(x, y) {
    this.targetX = x;
    this.targetY = y;
  }

  setColorKey(key, { silent = false } = {}) {
    if (key === this.colorKey && !silent) return;
    this.colorKey = key;
    this._applyColor();
    if (!silent) {
      this.scene.tweens.add({
        targets: this, pulse: 0.2, duration: 90, yoyo: true, ease: 'Quad.easeOut',
        onComplete: () => { this.pulse = 0; },
      });
    }
  }

  setRadius(r, { pop = false } = {}) {
    this.radius = Phaser.Math.Clamp(r, 2, HOLE_MAX_RADIUS);
    if (pop) {
      this.scene.tweens.add({
        targets: this, pulse: 0.12, duration: 70, yoyo: true, ease: 'Quad.easeOut',
        onComplete: () => { this.pulse = 0; },
      });
    }
    return this.radius;
  }

  /** Death animation when the hole collapses below the minimum radius. */
  collapse() {
    this.dead = true;
    this.scene.tweens.add({ targets: this, angle: 540, duration: 650, ease: 'Quad.easeIn' });
    this.scene.tweens.add({
      targets: this, radius: 2, duration: 600, ease: 'Back.easeIn',
    });
  }

  revive(radius = 34) {
    this.dead = false;
    this.setAngle(0);
    this.setRadius(radius, { pop: true });
  }

  update(dt, canMove) {
    if (!this.dead && canMove) {
      // Frame-rate-normalized version of `pos += (target - pos) * 0.18` @60fps
      const k = 1 - Math.pow(1 - 0.18, dt * 60);
      const px = this.x, py = this.y;
      let nx = px + (this.targetX - px) * k;
      let ny = py + (this.targetY - py) * k;
      const m = this.radius * 0.55 + 8;
      nx = Phaser.Math.Clamp(nx, m, GAME_W - m);
      ny = Phaser.Math.Clamp(ny, 158, GAME_H - m);
      const vx = (nx - px) / Math.max(dt, 1e-4);
      const vy = (ny - py) / Math.max(dt, 1e-4);
      this.x = nx;
      this.y = ny;
      this.speed = Math.hypot(vx, vy);
      if (this.speed > 120) this.velAngle = Math.atan2(vy, vx);
    } else if (!this.dead) {
      this.speed = 0;
    }

    // Speed-based squash/stretch along the movement direction
    const targetSq = this.dead ? 0 : Math.min(this.speed / 2600, 0.14);
    this.sq += (targetSq - this.sq) * Math.min(1, dt * 10);
    if (!this.dead) this.rotation = this.velAngle;

    const bs = (this.radius / TEXTURE_R) * (1 + this.pulse);
    this.setScale(bs * (1 + this.sq), bs * (1 - this.sq));

    // Inner texture rotation (counter-rotating layers sell the vortex)
    this.swirl.rotation += dt * 1.7;
    this.patternRing.rotation -= dt * 0.6;

    if (this.skin.glow === 'rainbow') {
      this.hueT = (this.hueT + dt * 0.22) % 1;
      const c = Phaser.Display.Color.HSVToRGB(this.hueT, 0.6, 1).color;
      this.skinGlow.setTint(c);
      if (this.skin.pattern !== 'none') this.patternRing.setTint(c);
    }
  }
}
