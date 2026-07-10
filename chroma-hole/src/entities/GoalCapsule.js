/**
 * GoalCapsule - the level objective. Swallowable only when the hole is
 * BOTH big enough (targetRadius) AND the required color. Every 10th level
 * uses the flashier "mega" gem variant with an extra counter-rotating ring.
 * A small icon badge above it communicates the requirement without text:
 * [color dot + grow arrow] -> [checkmark] when ready.
 */
import Phaser from 'phaser';
import { COLORS, INK } from '../constants.js';
import Audio from '../systems/AudioManager.js';

const BODY_TEXTURE_R = 120; // radius baked into capsule textures
const RING_TEXTURE_R = 126;

export default class GoalCapsule extends Phaser.GameObjects.Container {
  constructor(scene, x, y, cfg) {
    super(scene, x, y);
    this.requiredColor = cfg.requiredColor;
    this.targetRadius = cfg.targetRadius;
    this.mega = !!cfg.mega;
    this.bodyRadius = this.mega ? 84 : 62;
    this.ready = false;
    this.consuming = false;
    this.pt = Math.random() * 10;

    const hex = COLORS[this.requiredColor];
    const mk = (key) => scene.make.image({ key, add: false });

    this.glow = mk('glow').setTint(hex).setAlpha(0.4)
      .setDisplaySize(this.bodyRadius * 3.6, this.bodyRadius * 3.6);
    this.ring1 = mk('deco-ring').setTint(hex).setAlpha(0.75)
      .setScale((this.bodyRadius * 1.45) / RING_TEXTURE_R);
    this.body = mk(this.mega ? 'capsule-gem' : 'capsule-hex').setTint(hex);
    this.baseBodyScale = this.bodyRadius / BODY_TEXTURE_R;
    this.body.setScale(this.baseBodyScale);

    const children = [this.glow, this.ring1, this.body];
    if (this.mega) {
      this.ring2 = mk('pattern-dots').setTint(0xffc93c).setAlpha(0.9)
        .setScale((this.bodyRadius * 1.75) / RING_TEXTURE_R);
      children.splice(1, 0, this.ring2);
    }

    // requirement badge (icons only, no text)
    const by = -(this.bodyRadius + 52);
    this.badgeBg = mk('pill').setTint(INK).setAlpha(0.82).setDisplaySize(128, 52).setPosition(0, by);
    this.badgeDot = mk('disc').setTint(hex).setDisplaySize(28, 28).setPosition(-26, by);
    this.badgeArrow = mk('icon-arrow').setTint(0xffffff).setDisplaySize(30, 30).setPosition(16, by);
    this.badgeCheck = mk('icon-check').setTint(0x6bcb77).setDisplaySize(34, 34).setPosition(16, by).setVisible(false);
    children.push(this.badgeBg, this.badgeDot, this.badgeArrow, this.badgeCheck);

    this.add(children);
    this.setDepth(6);
    scene.add.existing(this);
  }

  update(dt, hole) {
    if (this.consuming) {
      this._updateConsume(dt);
      return;
    }
    this.pt += dt;
    this.ring1.rotation += dt * 0.55;
    if (this.ring2) this.ring2.rotation -= dt * 0.85;

    const wasReady = this.ready;
    this.ready = hole.radius >= this.targetRadius && hole.colorKey === this.requiredColor && !hole.dead;
    if (this.ready !== wasReady) {
      this.badgeArrow.setVisible(!this.ready);
      this.badgeCheck.setVisible(this.ready);
      if (this.ready) {
        Audio.ready();
        this.scene.tweens.add({ targets: this.body, scale: this.baseBodyScale * 1.25, duration: 130, yoyo: true, ease: 'Quad.easeOut' });
      }
    }

    const amp = this.ready ? 0.09 : 0.035;
    const speed = this.ready ? 6 : 3;
    if (!this.scene.tweens.isTweening(this.body)) {
      this.body.setScale(this.baseBodyScale * (1 + Math.sin(this.pt * speed) * amp));
    }
    this.glow.setAlpha(this.ready ? 0.7 + Math.sin(this.pt * 6) * 0.15 : 0.4);
  }

  /** Small "not yet" shake when touched without meeting the requirements. */
  nudge() {
    Audio.deny();
    this.scene.tweens.add({ targets: this, x: this.x + 7, duration: 45, yoyo: true, repeat: 3 });
    this.scene.tweens.add({ targets: this.badgeBg, alpha: 1, duration: 90, yoyo: true });
  }

  /**
   * Win animation - agar.io-style suction into the hole:
   * a short tremble ("grabbed" by the vortex), then an accelerating inward
   * spiral around the hole's live position, spinning faster and shrinking
   * as it crosses the rim, with a color streak trailing behind.
   * Calls onConsumed() the moment it fully disappears into the void.
   */
  consume(hole, onConsumed) {
    this.consuming = true;
    this.done = false;
    this.holeRef = hole;
    this.onConsumed = onConsumed;
    this.ct = 0;
    this._lastTrail = -1;
    const dx = this.x - hole.x;
    const dy = this.y - hole.y;
    this.cAngle = Math.atan2(dy, dx);
    this.cDist = Math.max(1, Math.hypot(dx, dy));
    this.cStartDist = this.cDist;
    this.scene.tweens.add({
      targets: [this.badgeBg, this.badgeDot, this.badgeArrow, this.badgeCheck],
      alpha: 0, duration: 150,
    });
  }

  _updateConsume(dt) {
    if (this.done) return;
    const h = this.holeRef;
    this.ct += dt;

    // slow "grab" ramping into a violent pull
    const speed = 60 + this.ct * this.ct * 3600;
    this.cDist = Math.max(0, this.cDist - speed * dt);
    this.cAngle += dt * (1.4 + (1 - this.cDist / this.cStartDist) * 5.5);

    // early tremble while the vortex takes hold
    const trem = Math.max(0, 1 - this.ct / 0.35) * 5;
    this.x = h.x + Math.cos(this.cAngle) * this.cDist + Math.sin(this.ct * 70) * trem;
    this.y = h.y + Math.sin(this.cAngle) * this.cDist + Math.cos(this.ct * 57) * trem;
    this.rotation += dt * (3 + this.ct * 20);

    // full size outside the rim, sinks to nothing at the center
    const inside = Phaser.Math.Clamp(this.cDist / (h.radius + this.bodyRadius * 0.5), 0, 1);
    this.setScale(Math.max(0.02, inside));

    // color streak trailing into the hole
    const tick = Math.floor(this.ct * 30);
    if (tick !== this._lastTrail && this.scene.particles) {
      this._lastTrail = tick;
      this.scene.particles.burst.setParticleTint(COLORS[this.requiredColor]);
      this.scene.particles.burst.explode(2, this.x, this.y);
    }

    if (this.cDist <= Math.max(6, h.radius * 0.1)) {
      this.done = true;
      this.setVisible(false);
      if (this.onConsumed) this.onConsumed();
    }
  }
}
