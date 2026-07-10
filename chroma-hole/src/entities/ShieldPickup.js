/**
 * ShieldPickup - a rare, once-per-level collectible. Swallowing it doesn't
 * grow the hole or add score; it arms a one-hit shield that negates the
 * next wrong-color touch or spike hit (no shrink, penalty fully absorbed).
 * GameScene destroys it via consume() once collected.
 */
import Phaser from 'phaser';

export default class ShieldPickup extends Phaser.GameObjects.Container {
  constructor(scene, x, y) {
    super(scene, x, y);
    this.radius = 26;
    this.phase = Math.random() * Math.PI * 2;

    this.glow = scene.make.image({ key: 'glow', add: false })
      .setTint(0x4d96ff).setAlpha(0.4).setDisplaySize(120, 120);
    this.ring = scene.make.image({ key: 'ring', add: false })
      .setTint(0x4d96ff).setDisplaySize(64, 64);
    this.icon = scene.make.image({ key: 'icon-shield', add: false })
      .setTint(0x4d96ff).setDisplaySize(38, 38);
    this.add([this.glow, this.ring, this.icon]);
    this.setDepth(6);
    scene.add.existing(this);

    this.collected = false;
    this.setScale(0);
    scene.tweens.add({ targets: this, scale: 1, duration: 340, ease: 'Back.easeOut' });
  }

  update(dt, time) {
    if (this.collected) return;
    this.y += Math.sin(time * 0.0025 + this.phase) * 0.4; // gentle bob
    this.ring.rotation += dt * 0.8;
    this.glow.setAlpha(0.32 + Math.sin(time * 0.005 + this.phase) * 0.14);
  }

  /** Pop-and-fade collection animation; scene should stop colliding after this. */
  consume() {
    this.collected = true;
    this.scene.tweens.add({
      targets: this, scale: 1.5, alpha: 0, duration: 260, ease: 'Quad.easeOut',
      onComplete: () => this.destroy(),
    });
  }
}
