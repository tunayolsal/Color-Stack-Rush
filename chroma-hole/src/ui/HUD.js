/**
 * HUD - in-game heads-up display.
 * Top row: pause | level | score | timer.
 * Second row: current-color dot + hole-size progress bar toward the goal
 * capsule (marker at the end shows the required color). A rewarded "+10s"
 * pill appears during the last 5 seconds.
 */
import Phaser from 'phaser';
import { COLORS, INK, INK_CSS, FONT } from '../constants.js';
import { mkText, iconButton } from './UIFactory.js';

export default class HUD extends Phaser.GameObjects.Container {
  constructor(scene, { levelNum, timeLimit, targetRadius, requiredColor, onPause, onTimeBonus }) {
    super(scene, 0, 0);
    this.scene = scene;
    this.targetRadius = targetRadius;
    this.timeLimit = timeLimit;
    this.onTimeBonus = onTimeBonus;
    this.setDepth(100);
    scene.add.existing(this);

    this.pauseBtn = iconButton(scene, 56, 60, { icon: 'icon-pause', size: 84, onClick: onPause });
    this.pauseBtn.setDepth(101);

    this.levelText = mkText(scene, 165, 60, `LV ${levelNum}`, 34, INK_CSS, '700').setAlpha(0.85);
    this.scoreText = mkText(scene, 360, 58, '0', 54, INK_CSS, '700');
    this.clockIcon = scene.add.image(568, 60, 'icon-clock').setDisplaySize(38, 38).setTint(INK).setAlpha(0.85);
    this.timeText = mkText(scene, 636, 60, String(Math.ceil(timeLimit)), 44, INK_CSS, '700');

    // row 2: color + size progress
    this.colorRing = scene.add.image(96, 126, 'ring').setDisplaySize(52, 52).setTint(INK).setAlpha(0.25);
    this.colorDot = scene.add.image(96, 126, 'disc').setDisplaySize(40, 40);
    this.barG = scene.add.graphics().setDepth(100);
    this.marker = scene.add.image(600, 126, 'capsule-hex')
      .setDisplaySize(46, 46).setTint(COLORS[requiredColor]);
    this.markerBase = 46;

    this.multText = mkText(scene, 360, 104, '', 26, '#B983FF', '700').setVisible(false);

    this.add([this.levelText, this.scoreText, this.clockIcon, this.timeText,
      this.colorRing, this.colorDot, this.multText]);

    // combo pill (top-right, below the timer; hidden until combo >= 2)
    this.comboC = scene.add.container(596, 186).setDepth(110).setVisible(false);
    this.comboBg = scene.add.image(0, 0, 'pill').setDisplaySize(172, 64).setTint(0xffc93c);
    this.comboSpark = scene.add.image(-52, 0, 'spark').setDisplaySize(34, 34).setTint(0xffffff);
    this.comboText = mkText(scene, 16, 0, 'x2', 36, '#FFFFFF', '700');
    this.comboC.add([this.comboBg, this.comboSpark, this.comboText]);

    // shield badge (top-left, below pause; hidden unless armed)
    this.shieldBadge = scene.add.container(56, 186).setDepth(105).setVisible(false);
    const shBg = scene.add.image(0, 0, 'ring').setDisplaySize(70, 70).setTint(0x4d96ff);
    const shIcon = scene.add.image(0, 0, 'icon-shield').setDisplaySize(38, 38).setTint(0x4d96ff);
    this.shieldBadge.add([shBg, shIcon]);

    // rewarded +10s pill (hidden until the last 5 seconds)
    this.bonus = scene.add.container(360, 1160).setDepth(120).setVisible(false);
    const bbg = scene.add.image(0, 0, 'pill').setDisplaySize(240, 84).setTint(0x6bcb77);
    const bic = scene.add.image(-62, 0, 'icon-ad').setDisplaySize(46, 46).setTint(0xffffff);
    const bt = mkText(scene, 26, 0, '+10s', 40, '#FFFFFF', '700');
    this.bonus.add([bbg, bic, bt]);
    this.bonus.setSize(240, 84).setInteractive({ useHandCursor: true });
    this.bonus.on('pointerup', () => { if (this.onTimeBonus) this.onTimeBonus(); });

    this._lastTimeShown = -1;
  }

  setScore(v) {
    this.scoreText.setText(String(v));
  }

  setMult(m) {
    if (m > 1.001) {
      this.multText.setVisible(true).setText(`x${m.toFixed(2)}`);
    } else {
      this.multText.setVisible(false);
    }
  }

  setTime(secondsLeft) {
    const s = Math.max(0, Math.ceil(secondsLeft));
    if (s !== this._lastTimeShown) {
      this._lastTimeShown = s;
      this.timeText.setText(String(s));
      if (s <= 5) {
        this.timeText.setColor('#FF5C5C');
        this.scene.tweens.add({ targets: this.timeText, scale: 1.3, duration: 110, yoyo: true });
      } else {
        this.timeText.setColor(INK_CSS);
      }
    }
  }

  setColor(colorKey) {
    this.colorDot.setTint(COLORS[colorKey]);
    this.scene.tweens.add({ targets: this.colorDot, scale: this.colorDot.scale * 1.25, duration: 90, yoyo: true });
  }

  /** Show/refresh the combo pill; n < 2 fades it out. Tint escalates. */
  setCombo(n) {
    if (n < 2) {
      if (this.comboC.visible) {
        this.scene.tweens.add({
          targets: this.comboC, alpha: 0, duration: 180,
          onComplete: () => this.comboC.setVisible(false).setAlpha(1),
        });
      }
      return;
    }
    const tint = n >= 10 ? 0xb983ff : n >= 7 ? 0xff5c5c : n >= 4 ? 0xff8c42 : 0xffc93c;
    this.comboBg.setTint(tint);
    this.comboText.setText(`x${n}`);
    this.comboSpark.rotation += 0.7;
    this.comboC.setVisible(true).setAlpha(1);
    this.scene.tweens.add({
      targets: this.comboC, scale: { from: 1.28, to: 1 }, duration: 150, ease: 'Back.easeOut',
    });
  }

  /** Arms/disarms the shield badge with a little pop. */
  setShield(active) {
    if (active) {
      this.shieldBadge.setVisible(true).setScale(0);
      this.scene.tweens.add({ targets: this.shieldBadge, scale: 1, duration: 260, ease: 'Back.easeOut' });
      this.shieldPulse = this.scene.tweens.add({
        targets: this.shieldBadge, scale: 1.08, duration: 420, yoyo: true, repeat: -1, delay: 260,
      });
    } else {
      if (this.shieldPulse) { this.shieldPulse.stop(); this.shieldPulse = null; }
      this.shieldBadge.setVisible(false);
    }
  }

  /** Flash-and-shrink when the shield absorbs a hit, then hides itself. */
  shieldBreak() {
    if (this.shieldPulse) { this.shieldPulse.stop(); this.shieldPulse = null; }
    this.scene.tweens.add({
      targets: this.shieldBadge, scale: 1.5, alpha: 0, duration: 260, ease: 'Quad.easeOut',
      onComplete: () => { this.shieldBadge.setVisible(false).setAlpha(1).setScale(1); },
    });
  }

  /** Redraw the size progress bar. Called every frame (cheap). */
  setProgress(radius, startRadius, colorKey, ready) {
    const x0 = 136, y0 = 114, w = 420, h = 24;
    const p = Phaser.Math.Clamp((radius - startRadius) / (this.targetRadius - startRadius), 0, 1);
    const g = this.barG;
    g.clear();
    g.fillStyle(INK, 0.1);
    g.fillRoundedRect(x0, y0, w, h, 12);
    const fw = Math.max(24, p * w);
    g.fillStyle(COLORS[colorKey], 1);
    g.fillRoundedRect(x0, y0, fw, h, 12);
    if (ready) {
      const s = this.markerBase * (1.05 + Math.sin(this.scene.time.now * 0.012) * 0.12);
      this.marker.setDisplaySize(s, s).setAlpha(1);
    } else {
      this.marker.setDisplaySize(this.markerBase, this.markerBase).setAlpha(0.9);
    }
  }

  showBonus() {
    if (this.bonus.visible) return;
    this.bonus.setVisible(true).setScale(0);
    this.scene.tweens.add({ targets: this.bonus, scale: 1, duration: 240, ease: 'Back.easeOut' });
    this.bonusPulse = this.scene.tweens.add({
      targets: this.bonus, scale: 1.06, duration: 380, yoyo: true, repeat: -1, delay: 260,
    });
  }

  hideBonus() {
    if (this.bonusPulse) { this.bonusPulse.stop(); this.bonusPulse = null; }
    this.bonus.setVisible(false);
  }
}
