/**
 * Gate - a horizontal barrier that cycles OPEN -> WARN -> CLOSED -> OPEN.
 * Unlike Spike (touch = damage), a closed gate is a true physical wall: the
 * hole is pushed back out, never damaged. A brief red warning flash before
 * it slams shut telegraphs the timing so crossing it is a fair skill check,
 * not a cheap surprise. Always leaves room to go around - never the only path.
 */
import Phaser from 'phaser';

const OPEN_DUR = 1.8;
const WARN_DUR = 0.4;
const CLOSE_DUR = 1.6;
const SNAP_DUR = 0.22; // visual slide time for opening/closing

export default class Gate extends Phaser.GameObjects.Container {
  constructor(scene, cx, cy, length = 200) {
    super(scene, cx, cy);
    this.cx = cx;
    this.cy = cy;
    this.span = length; // named `span`, not `length` - Container already has a read-only .length getter
    this.thickness = 30;
    this.phase = 'open'; // open | warn | closed
    this.timer = Math.random() * OPEN_DUR; // desync multiple gates
    this.closeAmount = 0; // 0 = fully open/retracted, 1 = fully closed/met at center

    const barW = length / 2 - 6;
    this.barL = scene.make.image({ key: 'card', add: false }).setDisplaySize(barW, this.thickness).setTint(0x1a1a2e);
    this.barR = scene.make.image({ key: 'card', add: false }).setDisplaySize(barW, this.thickness).setTint(0x1a1a2e);
    this.glowL = scene.make.image({ key: 'glow', add: false }).setDisplaySize(90, 90).setTint(0xff5c5c).setAlpha(0);
    this.glowR = scene.make.image({ key: 'glow', add: false }).setDisplaySize(90, 90).setTint(0xff5c5c).setAlpha(0);
    this.add([this.glowL, this.glowR, this.barL, this.barR]);
    this.setDepth(3);
    scene.add.existing(this);
    this._layout();
  }

  get closed() { return this.phase === 'closed'; }

  /** Collision rect, only meaningful while `closed` is true. */
  getRect() {
    return { x: this.cx - this.span / 2, y: this.cy - this.thickness / 2, w: this.span, h: this.thickness };
  }

  _layout() {
    const barW = this.span / 2 - 6;
    const gap = (1 - this.closeAmount) * (this.span / 2 - 10);
    this.barL.setX(-barW / 2 - gap).setY(0);
    this.barR.setX(barW / 2 + gap).setY(0);
    this.glowL.setPosition(this.barL.x, 0);
    this.glowR.setPosition(this.barR.x, 0);
  }

  update(dt) {
    this.timer -= dt;
    if (this.phase === 'open' && this.timer <= 0) {
      this.phase = 'warn';
      this.timer = WARN_DUR;
    } else if (this.phase === 'warn' && this.timer <= 0) {
      this.phase = 'closed';
      this.timer = CLOSE_DUR;
    } else if (this.phase === 'closed' && this.timer <= 0) {
      this.phase = 'open';
      this.timer = OPEN_DUR;
    }

    const targetClose = this.phase === 'closed' ? 1 : this.phase === 'warn' ? Math.max(0, 1 - this.timer / WARN_DUR) * 0.15 : 0;
    this.closeAmount += (targetClose - this.closeAmount) * Math.min(1, dt / SNAP_DUR);
    if (this.phase === 'closed') this.closeAmount = Math.min(1, this.closeAmount + dt / SNAP_DUR);
    if (this.phase === 'open' && this.timer > OPEN_DUR - SNAP_DUR) this.closeAmount = Math.max(0, this.closeAmount - dt / SNAP_DUR * 3);
    this._layout();

    const warnFlash = this.phase === 'warn' ? (0.4 + Math.sin(this.timer * 40) * 0.35) : this.phase === 'closed' ? 0.2 : 0;
    this.glowL.setAlpha(warnFlash);
    this.glowR.setAlpha(warnFlash);
    const barTint = this.phase === 'closed' ? 0x2a1a1a : 0x1a1a2e;
    this.barL.setTint(barTint);
    this.barR.setTint(barTint);
  }
}
