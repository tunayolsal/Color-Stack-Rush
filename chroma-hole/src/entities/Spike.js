/**
 * Spike - an untouchable ink hazard. Touching it costs the same 12% shrink
 * as a wrong-color blob (shared penalty cooldown in GameScene). Spikes are
 * never swallowable and never despawn.
 *
 * Variants:
 *  - static: sits where placed (levels 20+)
 *  - drifting: slow bounce around the field (levels 30+)
 *  - orbiting: circles the goal capsule as a "guardian" (mega levels 20+)
 *
 * Visual: dark thorn ball + pulsing red glow = universally readable danger.
 */
import Phaser from 'phaser';
import { INK, GAME_W } from '../constants.js';

export default class Spike extends Phaser.GameObjects.Container {
  constructor(scene, x, y, { orbit = null, drift = false } = {}) {
    super(scene, x, y);
    this.radius = 26; // collision radius
    this.orbit = orbit; // { cx, cy, r, speed, angle } or null
    this.phase = Math.random() * Math.PI * 2;
    if (drift && !orbit) {
      const a = Math.random() * Math.PI * 2;
      this.vx = Math.cos(a) * 32;
      this.vy = Math.sin(a) * 32;
    } else {
      this.vx = 0;
      this.vy = 0;
    }

    this.glow = scene.make.image({ key: 'glow', add: false })
      .setTint(0xff5c5c).setAlpha(0.32).setDisplaySize(116, 116);
    this.body = scene.make.image({ key: 'spike', add: false })
      .setTint(INK).setDisplaySize(64, 64);
    this.add([this.glow, this.body]);
    this.setDepth(4);
    scene.add.existing(this);

    // pop in
    this.setScale(0);
    scene.tweens.add({ targets: this, scale: 1, duration: 300, ease: 'Back.easeOut' });
  }

  update(dt, time) {
    if (this.orbit) {
      this.orbit.angle += this.orbit.speed * dt;
      this.x = this.orbit.cx + Math.cos(this.orbit.angle) * this.orbit.r;
      this.y = this.orbit.cy + Math.sin(this.orbit.angle) * this.orbit.r;
    } else if (this.vx || this.vy) {
      this.x += this.vx * dt;
      this.y += this.vy * dt;
      if (this.x < 70 || this.x > GAME_W - 70) this.vx *= -1;
      if (this.y < 400 || this.y > 1210) this.vy *= -1;
    }
    this.body.rotation += dt * 1.3;
    this.glow.setAlpha(0.26 + Math.sin(time * 0.006 + this.phase) * 0.12);
    if (!this.scene.tweens.isTweening(this.body)) {
      this.body.setScale((64 / 96) * (1 + Math.sin(time * 0.005 + this.phase) * 0.08));
    }
  }

  hitPulse() {
    this.scene.tweens.add({
      targets: this.body, scale: this.body.scale * 1.4, duration: 90, yoyo: true, ease: 'Quad.easeOut',
    });
  }
}
