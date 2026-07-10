/**
 * ParticleManager - one set of pooled emitters per scene (Phaser particle
 * emitters pool their particles internally, so bursts allocate nothing
 * after warm-up). All game juice effects go through here.
 */
import { PALETTE } from '../constants.js';

export default class ParticleManager {
  constructor(scene) {
    this.scene = scene;

    this.burst = scene.add.particles(0, 0, 'particle', {
      speed: { min: 90, max: 320 },
      angle: { min: 0, max: 360 },
      scale: { start: 0.75, end: 0 },
      alpha: { start: 1, end: 0 },
      lifespan: { min: 260, max: 520 },
      emitting: false,
    }).setDepth(12);

    this.shards = scene.add.particles(0, 0, 'shard', {
      speed: { min: 120, max: 380 },
      angle: { min: 0, max: 360 },
      scale: { start: 0.7, end: 0.1 },
      alpha: { start: 1, end: 0 },
      rotate: { min: 0, max: 360 },
      lifespan: { min: 220, max: 450 },
      gravityY: 500,
      emitting: false,
    }).setDepth(12);

    this.sparks = scene.add.particles(0, 0, 'spark', {
      speed: { min: 60, max: 280 },
      angle: { min: 0, max: 360 },
      scale: { start: 0.7, end: 0 },
      alpha: { start: 1, end: 0 },
      rotate: { min: 0, max: 180 },
      lifespan: { min: 300, max: 700 },
      emitting: false,
    }).setDepth(12);

    this.rings = scene.add.particles(0, 0, 'ripple', {
      speed: 0,
      scale: { start: 0.15, end: 1.0 },
      alpha: { start: 0.8, end: 0 },
      lifespan: 450,
      emitting: false,
    }).setDepth(12);
  }

  /** Colored pop when an object is swallowed. */
  swallowBurst(x, y, tint, sizeIndex = 0) {
    this.burst.setParticleTint(tint);
    this.burst.explode(10 + sizeIndex * 5, x, y);
  }

  /** Red shard break on wrong-color touch. */
  wrongBurst(x, y) {
    this.shards.setParticleTint(0xff5c5c);
    this.shards.explode(12, x, y);
  }

  /** Ring + splash when the hole picks up a new color from a paint pool. */
  colorSplash(x, y, tint) {
    this.rings.setParticleTint(tint);
    this.rings.explode(1, x, y);
    this.burst.setParticleTint(tint);
    this.burst.explode(8, x, y);
  }

  /** Big celebration when the goal capsule is consumed. */
  capsuleBurst(x, y, tint) {
    this.burst.setParticleTint(tint);
    this.burst.explode(28, x, y);
    this.sparks.setParticleTint(0xffd93d);
    this.sparks.explode(18, x, y);
    this.rings.setParticleTint(tint);
    this.rings.explode(2, x, y);
  }

  /** Multicolor confetti for the win overlay. */
  confetti() {
    PALETTE.forEach((c, i) => {
      this.burst.setParticleTint(c);
      this.burst.explode(7, 120 + i * 120, 280);
    });
  }

  /**
   * Cosmetic trail emitter following the hole (per-skin style).
   * Returns null for the "none" style; GameScene toggles `.emitting`
   * based on hole speed and re-tints on color change.
   */
  createTrail(hole, skin) {
    if (!skin || skin.trail === 'none') return null;
    const isBubble = skin.trail === 'bubble';
    const em = this.scene.add.particles(0, 0, isBubble ? 'ripple' : 'spark', {
      follow: hole,
      frequency: 45,
      speed: { min: 10, max: 60 },
      scale: isBubble ? { start: 0.12, end: 0.3 } : { start: 0.5, end: 0 },
      alpha: { start: 0.7, end: 0 },
      lifespan: 400,
      gravityY: isBubble ? -140 : 0,
      emitting: false,
    }).setDepth(9);
    return em;
  }
}
