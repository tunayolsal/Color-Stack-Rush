/**
 * PaintPool - circular color pool. Passing the hole over it instantly
 * recolors the hole (no color mixing, per spec). Pools ripple with
 * concentric rings; on levels 31+ they patrol slowly (alternating axis).
 */
import Phaser from 'phaser';
import { COLORS } from '../constants.js';

const RIPPLE_TEXTURE_R = 118;

export default class PaintPool extends Phaser.GameObjects.Container {
  constructor(scene, x, y, colorKey, { moving = false, index = 0 } = {}) {
    super(scene, x, y);
    this.baseX = x;
    this.baseY = y;
    this.colorKey = colorKey;
    this.radius = 52;
    this.moving = moving;
    this.axis = index % 2 === 0 ? 'x' : 'y';
    this.phase = index * 1.7;
    this.rippleT = 0;

    const hex = COLORS[colorKey];
    const dark = Phaser.Display.Color.ValueToColor(hex).darken(14).color;

    this.glow = scene.make.image({ key: 'glow', add: false })
      .setTint(hex).setAlpha(0.3).setDisplaySize(this.radius * 3.4, this.radius * 3.4);
    this.base = scene.make.image({ key: 'disc', add: false })
      .setTint(hex).setDisplaySize(this.radius * 2, this.radius * 2).setAlpha(0.95);
    this.inner = scene.make.image({ key: 'disc', add: false })
      .setTint(dark).setDisplaySize(this.radius * 1.45, this.radius * 1.45).setAlpha(0.9);
    this.r1 = scene.make.image({ key: 'ripple', add: false }).setTint(0xffffff);
    this.r2 = scene.make.image({ key: 'ripple', add: false }).setTint(0xffffff);

    this.add([this.glow, this.base, this.inner, this.r1, this.r2]);
    this.setDepth(2);
    scene.add.existing(this);
  }

  update(dt, time) {
    this.rippleT += dt;
    const period = 1.6;
    [[this.r1, 0], [this.r2, 0.8]].forEach(([ring, off]) => {
      const p = ((this.rippleT + off) % period) / period;
      const desired = this.radius * (0.45 + p * 0.95);
      ring.setScale(desired / RIPPLE_TEXTURE_R);
      ring.setAlpha((1 - p) * 0.55);
    });
    if (this.moving) {
      const wobble = Math.sin(time * 0.0006 + this.phase) * 80;
      if (this.axis === 'x') {
        this.x = Phaser.Math.Clamp(this.baseX + wobble, 70, 650);
      } else {
        this.y = Phaser.Math.Clamp(this.baseY + wobble, 380, 1180);
      }
    }
  }
}
