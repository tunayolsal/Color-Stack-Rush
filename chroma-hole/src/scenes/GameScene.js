/**
 * GameScene - the core loop.
 * Drag the hole, swallow matching blobs to grow, repaint at pools, then
 * consume the goal capsule while big enough + correctly colored.
 * Win/fail/pause are in-scene overlays; blobs are object-pooled.
 */
import Phaser from 'phaser';
import {
  GAME_W, GAME_H, COLORS, INK, CREAM, PALETTE,
  HOLE_START_RADIUS, HOLE_MIN_RADIUS, HOLE_MAX_RADIUS, cssColor,
} from '../constants.js';
import { Share } from '@capacitor/share';
import Save from '../systems/SaveManager.js';
import Audio from '../systems/AudioManager.js';
import Monetization from '../systems/MonetizationManager.js';
import Review from '../systems/ReviewManager.js';
import ParticleManager from '../systems/ParticleManager.js';
import { getLevelConfig, SIZE_DEFS, pickSizeIndex } from '../systems/LevelManager.js';
import { checkAchievements } from '../systems/Achievements.js';
import Hole from '../entities/Hole.js';
import Blob, { GOLDEN_DEF } from '../entities/Blob.js';
import PaintPool from '../entities/PaintPool.js';
import GoalCapsule from '../entities/GoalCapsule.js';
import Spike from '../entities/Spike.js';
import ShieldPickup from '../entities/ShieldPickup.js';
import Gate from '../entities/Gate.js';
import HUD from '../ui/HUD.js';
import { iconButton, textButton, mkText } from '../ui/UIFactory.js';
import skins from '../data/skins.json';

const POOL_LAYOUTS = {
  2: [[120, 780], [600, 780]],
  3: [[120, 700], [600, 700], [360, 1060]],
  4: [[110, 560], [610, 560], [110, 1070], [610, 1070]],
  5: [[110, 520], [610, 520], [110, 1090], [610, 1090], [360, 810]],
};

const CAPSULE_POS = { x: 360, y: 265 };
const HOLE_START = { x: 360, y: 960 };

export default class GameScene extends Phaser.Scene {
  constructor() { super('GameScene'); }

  init(data) {
    this.levelNum = (data && data.level) || Save.data.currentLevel || 1;
  }

  create() {
    this.cameras.main.setBackgroundColor(CREAM);
    this.cfg = getLevelConfig(this.levelNum);
    this.stateName = 'playing'; // playing | paused | complete | failed
    this.score = 0;
    this.overflow = 0;
    this.mult = 1;
    this.stats = { correct: 0, wrong: 0 };
    this.timeLeft = this.cfg.timeLimit;
    this.timeBonusUsed = false;
    this.reviveUsed = false;
    this.penaltyCooldown = 0;
    this.capsuleNudgeCd = 0;
    this.spawnAccum = 0;
    this.lastTickSec = -1;
    this.floatPool = [];
    this.combo = 0;
    this.comboTimer = 0;
    this.shield = false;
    this.isFlawless = false;

    this._buildBackdrop();

    this.particles = new ParticleManager(this);

    // paint pools - one per active color
    const layout = POOL_LAYOUTS[this.cfg.activeColors.length] || POOL_LAYOUTS[4];
    this.pools = this.cfg.activeColors.map((colorKey, i) =>
      new PaintPool(this, layout[i][0], layout[i][1], colorKey, {
        moving: this.cfg.movingPools, index: i,
      }));

    this.capsule = new GoalCapsule(this, CAPSULE_POS.x, CAPSULE_POS.y, this.cfg);

    // spike hazards (levels 20+); on mega levels some orbit the capsule
    this.spikes = [];
    const orbitN = Math.min(this.cfg.orbitSpikes || 0, this.cfg.spikeCount || 0);
    for (let i = 0; i < orbitN; i++) {
      this.spikes.push(new Spike(this, this.capsule.x, this.capsule.y, {
        orbit: {
          cx: this.capsule.x, cy: this.capsule.y,
          r: this.capsule.bodyRadius + 78, speed: 0.9,
          angle: (i / orbitN) * Math.PI * 2,
        },
      }));
    }
    for (let i = orbitN; i < (this.cfg.spikeCount || 0); i++) {
      const pos = this._findOpenSpot();
      this.spikes.push(new Spike(this, pos.x, pos.y, { drift: this.cfg.spikesDrift }));
    }

    // timed gates (level 26+) - true physical barriers, never the only path
    this.gates = [];
    for (let i = 0; i < (this.cfg.gateCount || 0); i++) {
      this.gates.push(new Gate(this, 360, 700 + (i % 2) * 220, 220));
    }

    // one free shield pickup per level (level 3+), no respawn once collected
    this.shieldPickup = null;
    if (this.cfg.hasShield) {
      const pos = this._findOpenSpot();
      this.shieldPickup = new ShieldPickup(this, pos.x, pos.y);
    }

    const skin = skins.find((s) => s.id === Save.data.activeSkin) || skins[0];
    this.hole = new Hole(this, HOLE_START.x, HOLE_START.y, skin);
    this.hole.setColorKey(this.cfg.activeColors[0], { silent: true });
    this.trail = this.particles.createTrail(this.hole, skin);
    this._retintTrail();

    // pooled blobs
    this.blobPool = Array.from({ length: 34 }, () => new Blob(this));
    const initial = Math.round(this.cfg.maxObjects * 0.75);
    for (let i = 0; i < initial; i++) this._spawnBlob();

    this.hud = new HUD(this, {
      levelNum: this.levelNum,
      timeLimit: this.cfg.timeLimit,
      targetRadius: this.cfg.targetRadius,
      requiredColor: this.cfg.requiredColor,
      onPause: () => this.pauseGame(),
      onTimeBonus: () => this.useTimeBonus(),
    });
    this.hud.setColor(this.hole.colorKey);

    // input: hole follows the pointer while pressed (touch or mouse)
    this.input.on('pointerdown', (p) => {
      if (this.stateName !== 'playing') return;
      const hits = this.input.hitTestPointer(p);
      if (hits.length > 0) return; // tapped a UI element
      this.hole.setTarget(p.worldX, p.worldY);
      this._dismissHint();
    });
    this.input.on('pointermove', (p) => {
      if (this.stateName !== 'playing' || !p.isDown) return;
      this.hole.setTarget(p.worldX, p.worldY);
    });

    this._showIntroBanner();
    if (this.levelNum === 1 && !Save.data.sawDragHint) this._showDragHint();

    Audio.startMusic();
  }

  /* ================= per-frame ================= */

  update(time, delta) {
    const dt = Math.min(delta, 50) / 1000;

    if (this.stateName !== 'playing') {
      this.hole.update(dt, false);
      if (this.stateName === 'complete' || this.stateName === 'failed') {
        this.capsule.update(dt, this.hole);
        // let in-flight suction animations finish behind the overlay
        for (const b of this.blobPool) {
          if (b.alive && b.state === 'swallowing') b.update(dt, this);
        }
      }
      return;
    }

    this.hole.update(dt, true);
    this.pools.forEach((p) => p.update(dt, time));
    this.capsule.update(dt, this.hole);
    this.penaltyCooldown -= dt;
    this.capsuleNudgeCd -= dt;

    // combo decay window
    if (this.combo > 0) {
      this.comboTimer -= dt;
      if (this.comboTimer <= 0) {
        this.combo = 0;
        this.hud.setCombo(0);
      }
    }

    // spike hazards
    for (const sp of this.spikes) {
      sp.update(dt, time);
      const dSp = Phaser.Math.Distance.Between(this.hole.x, this.hole.y, sp.x, sp.y);
      if (dSp < this.hole.radius + sp.radius * 0.6) this._spikeHit(sp);
    }
    if (this.stateName !== 'playing') return; // a spike hit may have collapsed the hole

    // timed gates - true physical barriers, pushed out rather than damaged
    for (const gate of this.gates) {
      gate.update(dt);
      if (gate.closed) this._resolveGateCollision(gate);
    }

    // shield pickup
    if (this.shieldPickup && !this.shieldPickup.collected) {
      this.shieldPickup.update(dt, time);
      const dSh = Phaser.Math.Distance.Between(this.hole.x, this.hole.y, this.shieldPickup.x, this.shieldPickup.y);
      if (dSh < this.hole.radius + this.shieldPickup.radius * 0.6) {
        this.shieldPickup.consume();
        this.shield = true;
        this.hud.setShield(true);
        Audio.shieldUp();
        this.particles.colorSplash(this.shieldPickup.x, this.shieldPickup.y, 0x4d96ff);
      }
    }

    if (this.trail) {
      this.trail.emitting = this.hole.speed > 150 && !this.hole.dead;
    }

    // paint pool pickup
    for (const pool of this.pools) {
      if (pool.colorKey === this.hole.colorKey) continue;
      const d = Phaser.Math.Distance.Between(this.hole.x, this.hole.y, pool.x, pool.y);
      if (d < pool.radius) {
        this.hole.setColorKey(pool.colorKey);
        this.hud.setColor(pool.colorKey);
        this._retintTrail();
        this.particles.colorSplash(pool.x, pool.y, COLORS[pool.colorKey]);
        Audio.whoosh();
      }
    }

    // timer
    this.timeLeft -= dt;
    this.hud.setTime(this.timeLeft);
    if (this.timeLeft <= 5.05 && !this.timeBonusUsed && this.timeLeft > 0) {
      this.hud.showBonus();
      const sec = Math.ceil(this.timeLeft);
      if (sec !== this.lastTickSec) { this.lastTickSec = sec; Audio.tick(); }
    }
    if (this.timeLeft <= 0) { this.fail('time'); return; }

    // spawning (rate-based, capped by density)
    this.spawnAccum += dt * this.cfg.spawnRate;
    while (this.spawnAccum >= 1) {
      this.spawnAccum -= 1;
      if (this._aliveBlobs() < this.cfg.maxObjects) this._spawnBlob();
    }

    // blob updates + swallow / wrong-touch checks
    for (const b of this.blobPool) {
      if (!b.alive) continue;
      b.update(dt, this);
      if (b.state !== 'idle') continue;
      let d = Phaser.Math.Distance.Between(this.hole.x, this.hole.y, b.x, b.y);
      const isMatch = b.isGolden || b.colorKey === this.hole.colorKey;
      // matching-color (and golden) blobs get funneled toward the vortex (agar.io magnet)
      if (isMatch && d > 1) {
        const suctionR = this.hole.radius + 120;
        if (d < suctionR) {
          const pull = Math.pow(1 - d / suctionR, 1.6) * 640 * dt;
          b.x += ((this.hole.x - b.x) / d) * pull;
          b.y += ((this.hole.y - b.y) / d) * pull;
          d = Phaser.Math.Distance.Between(this.hole.x, this.hole.y, b.x, b.y);
        }
      }
      if (d < this.hole.radius + b.radius * 0.5) {
        if (isMatch) this._swallow(b);
        else this._wrongTouch(b);
        if (this.stateName !== 'playing') return;
      }
    }

    // goal capsule contact
    const dCap = Phaser.Math.Distance.Between(this.hole.x, this.hole.y, this.capsule.x, this.capsule.y);
    if (this.capsule.ready && dCap < this.hole.radius + this.capsule.bodyRadius * 0.6) {
      this.win();
      return;
    }
    if (!this.capsule.ready && dCap < this.hole.radius + this.capsule.bodyRadius * 0.55 && this.capsuleNudgeCd <= 0) {
      this.capsuleNudgeCd = 1.4;
      this.capsule.nudge();
    }

    this.hud.setProgress(this.hole.radius, HOLE_START_RADIUS, this.hole.colorKey, this.capsule.ready);
  }

  /* ================= gameplay events ================= */

  _swallow(b) {
    const def = b.isGolden ? GOLDEN_DEF : SIZE_DEFS[b.sizeIndex];

    // combo: consecutive swallows within the window stack a score bonus
    this.combo += 1;
    this.comboTimer = 2.5;
    Save.recordCombo(this.combo);
    const comboBonus = 1 + Math.min(this.combo - 1, 10) * 0.1; // up to +100%

    const gained = Math.round(def.score * this.mult * comboBonus);
    this.score += gained;
    this.stats.correct += 1;
    Save.addSwallowed();
    this._growHole(def.grow);
    if (b.isGolden) Audio.goldenChime();
    else Audio.pop(b.sizeIndex, this.combo);
    if (this.combo >= 2) {
      Audio.comboUp(this.combo);
      this.hud.setCombo(this.combo);
    }
    if (this.combo % 5 === 0) {
      // combo milestone: camera punch + gold sparks above the hole
      this._zoomPunch();
      this.particles.swallowBurst(this.hole.x, this.hole.y - this.hole.radius, 0xffc93c, 2);
    }
    this._floatText(b.x, b.y - 20, `+${gained}`, b.isGolden ? '#FFC93C' : cssColor(COLORS[b.colorKey]));
    b.startSwallow(this.hole); // spiral suction; color splash fires as it sinks in
    this.hud.setScore(this.score);
  }

  _growHole(px) {
    if (this.hole.radius < HOLE_MAX_RADIUS) {
      const room = HOLE_MAX_RADIUS - this.hole.radius;
      const applied = Math.min(room, px);
      this.hole.setRadius(this.hole.radius + applied, { pop: true });
      this.overflow += px - applied;
    } else {
      this.overflow += px;
    }
    this.mult = 1 + this.overflow * 0.02; // growth past the visual cap becomes score multiplier
    this.hud.setMult(this.mult);
  }

  /** Shared penalty core: 12% shrink + shake + flash + combo reset. */
  _applyPenalty(px, py) {
    this.penaltyCooldown = 0.6;
    this.stats.wrong += 1;
    this.combo = 0;
    this.hud.setCombo(0);
    const newR = this.hole.setRadius(this.hole.radius * 0.88); // -12% (spec)
    this.cameras.main.shake(120, 0.01);
    this.cameras.main.flash(150, 255, 70, 70);
    Audio.buzz();
    this.particles.wrongBurst(px, py);
    if (newR < HOLE_MIN_RADIUS) this.fail('collapse');
  }

  _wrongTouch(b) {
    if (this.penaltyCooldown > 0) return;
    if (this._tryShieldBlock(this.hole.x, this.hole.y)) { b.knockbackFrom(this.hole); return; }
    this._applyPenalty(this.hole.x, this.hole.y);
    b.knockbackFrom(this.hole);
  }

  _spikeHit(sp) {
    if (this.penaltyCooldown > 0) return;
    sp.hitPulse();
    if (this._tryShieldBlock(sp.x, sp.y)) return;
    this._applyPenalty(sp.x, sp.y);
  }

  /** Consumes the shield (if armed) to fully negate a penalty. Doesn't count against flawless. */
  _tryShieldBlock(x, y) {
    if (!this.shield) return false;
    this.shield = false;
    this.hud.shieldBreak();
    Audio.shieldBlock();
    this.particles.colorSplash(x, y, 0x4d96ff);
    this.penaltyCooldown = 0.3; // brief grace so losing the shield isn't instantly followed by a real hit
    return true;
  }

  /** Circle-vs-AABB push-out so a closed gate is a true wall, never damage. */
  _resolveGateCollision(gate) {
    const r = gate.getRect();
    const cx = Phaser.Math.Clamp(this.hole.x, r.x, r.x + r.w);
    const cy = Phaser.Math.Clamp(this.hole.y, r.y, r.y + r.h);
    const dx = this.hole.x - cx;
    const dy = this.hole.y - cy;
    const dist = Math.hypot(dx, dy);
    if (dist > 0.001 && dist < this.hole.radius) {
      const push = this.hole.radius - dist;
      this.hole.x += (dx / dist) * push;
      this.hole.y += (dy / dist) * push;
    } else if (dist <= 0.001) {
      this.hole.y -= this.hole.radius; // rare exact-center case - just push up
    }
  }

  _zoomPunch() {
    const cam = this.cameras.main;
    this.tweens.add({
      targets: cam, zoom: 1.035, duration: 90, yoyo: true, ease: 'Quad.easeOut',
      onComplete: () => cam.setZoom(1),
    });
  }

  win() {
    if (this.stateName !== 'playing') return;
    this.stateName = 'complete';
    this.hud.hideBonus();
    if (this.trail) this.trail.emitting = false;
    Audio.fanfare();

    const timeUsed = this.cfg.timeLimit - Math.max(0, this.timeLeft);
    const touches = this.stats.correct + this.stats.wrong;
    const accuracy = touches === 0 ? 1 : this.stats.correct / touches;
    let stars = 1;
    if (accuracy >= 0.75) stars += 1;
    if (timeUsed <= this.cfg.timeLimit * 0.7) stars += 1;
    this.isFlawless = this.stats.wrong === 0;
    const coins = this.cfg.coinBase + stars * 10 + (this.isFlawless ? 25 : 0);
    this.isNewBest = Save.recordScore(this.levelNum, this.score);
    Save.completeLevel(this.levelNum, stars, coins);
    checkAchievements(this);
    Review.maybeRequest({ level: this.levelNum, stars });

    // the spike guardians (and any leftover hazards/pickups) fade with the win
    this.spikes.forEach((sp) => this.tweens.add({
      targets: sp, alpha: 0, scale: 0, duration: 420, ease: 'Back.easeIn',
    }));
    this.gates.forEach((g) => this.tweens.add({ targets: g, alpha: 0, duration: 300 }));
    if (this.shieldPickup && !this.shieldPickup.collected) {
      this.tweens.add({ targets: this.shieldPickup, alpha: 0, scale: 0, duration: 300 });
    }

    // suck the capsule into the hole; the big payoff fires the moment it sinks in
    this.capsule.consume(this.hole, () => {
      this.particles.capsuleBurst(this.hole.x, this.hole.y, COLORS[this.cfg.requiredColor]);
      Audio.pop(2);
      this.cameras.main.shake(90, 0.004);
      this.tweens.add({ targets: this.hole, pulse: 0.3, duration: 150, yoyo: true, onComplete: () => { this.hole.pulse = 0; } });
    });

    this.time.delayedCall(1250, () => {
      Monetization.maybeShowInterstitial(() => this._showWinOverlay(stars, coins), this.levelNum);
    });
  }

  fail(reason) {
    if (this.stateName !== 'playing') return;
    this.stateName = 'failed';
    this.hud.hideBonus();
    this.hud.setCombo(0);
    if (this.trail) this.trail.emitting = false;
    checkAchievements(this); // combo/swallow trophies still count on a fail
    Audio.collapse();
    if (reason === 'collapse') {
      this.hole.collapse();
      this.particles.wrongBurst(this.hole.x, this.hole.y);
    }
    this.time.delayedCall(700, () => this._showFailOverlay(reason));
  }

  /* ================= rewarded / pause ================= */

  useTimeBonus() {
    if (this.timeBonusUsed || this.stateName !== 'playing') return;
    this.timeBonusUsed = true;
    this.hud.hideBonus();
    Monetization.showRewardedAd((ok) => {
      if (ok && this.stateName === 'playing') {
        this.timeLeft += 10;
        this.hud.setTime(this.timeLeft);
        this._floatText(this.hole.x, this.hole.y - this.hole.radius - 30, '+10s', '#6BCB77');
      }
    });
  }

  pauseGame() {
    if (this.stateName !== 'playing') return;
    this.stateName = 'paused';
    const ov = this._overlayBase(520);
    mkText(this, 360, 400, `LV ${this.levelNum}`, 40, '#1A1A2E', '700').setDepth(301).setAlpha(0.7).setName('__ov');
    const resume = textButton(this, 360, 520, {
      label: 'RESUME', icon: 'icon-play', color: 0x6bcb77,
      onClick: () => { this._destroyOverlay(); this.stateName = 'playing'; },
    });
    const retry = textButton(this, 360, 660, {
      label: 'RETRY', icon: 'icon-retry', color: 0xffd93d, labelColor: '#1A1A2E',
      onClick: () => this.scene.restart({ level: this.levelNum }),
    });
    retry.list[3].setTint(0x1a1a2e); // icon tint to ink on the yellow button
    const home = iconButton(this, 260, 800, { icon: 'icon-home', onClick: () => this.scene.start('MenuScene') });
    const sound = iconButton(this, 380, 800, {
      icon: Save.data.settings.sound ? 'icon-sound' : 'icon-mute',
      iconTint: Save.data.settings.sound ? 0x1a1a2e : 0xff5c5c,
      onClick: () => {
        Audio.setSound(!Save.data.settings.sound);
        sound.iconImage.setTexture(Save.data.settings.sound ? 'icon-sound' : 'icon-mute');
        sound.iconImage.setTint(Save.data.settings.sound ? 0x1a1a2e : 0xff5c5c);
      },
    });
    const music = iconButton(this, 500, 800, {
      icon: 'icon-music',
      iconTint: Save.data.settings.music ? 0x1a1a2e : 0xff5c5c,
      onClick: () => {
        Audio.setMusic(!Save.data.settings.music);
        music.iconImage.setTint(Save.data.settings.music ? 0x1a1a2e : 0xff5c5c);
      },
    });
    [resume, retry, home, sound, music].forEach((el) => { el.setDepth(301); el.setName('__ov'); });
  }

  /* ================= overlays ================= */

  _overlayBase(panelH = 640) {
    const dim = this.add.rectangle(360, 640, GAME_W, GAME_H, INK, 0.55)
      .setDepth(300).setInteractive().setName('__ov');
    const panel = this.add.image(360, 620, 'panel').setDisplaySize(560, panelH)
      .setTint(0xfffdf7).setDepth(300).setName('__ov');
    panel.setScale(panel.scaleX * 0.85, panel.scaleY * 0.85).setAlpha(0);
    this.tweens.add({
      targets: panel,
      scaleX: 560 / panel.width, scaleY: panelH / panel.height,
      alpha: 1, duration: 240, ease: 'Back.easeOut',
    });
    return { dim, panel };
  }

  _destroyOverlay() {
    this.children.list
      .filter((c) => c.name === '__ov')
      .forEach((c) => c.destroy());
  }

  _showWinOverlay(stars, coins) {
    if (this.stateName !== 'complete') return;
    this._overlayBase(830);
    this.particles.confetti();

    mkText(this, 360, 300, `LEVEL ${this.levelNum}`, 40, '#1A1A2E', '700').setDepth(301).setName('__ov');
    const badge = this.add.image(360, 244, 'icon-check').setDisplaySize(56, 56)
      .setTint(0x6bcb77).setDepth(301).setName('__ov');
    this.tweens.add({ targets: badge, scale: badge.scale * 1.15, duration: 400, yoyo: true, repeat: -1 });

    // score + personal-best / flawless ribbons
    mkText(this, 360, 348, `SCORE ${this.score}`, 26, '#1A1A2E', '600').setAlpha(0.6).setDepth(301).setName('__ov');
    const bothRibbons = this.isNewBest && this.isFlawless;
    if (this.isNewBest) {
      const ribbon = mkText(this, bothRibbons ? 250 : 360, 380, 'NEW BEST!', 22, '#FF5C5C', '700').setDepth(301).setName('__ov').setScale(0);
      this.tweens.add({ targets: ribbon, scale: 1, delay: 700, duration: 260, ease: 'Back.easeOut' });
    }
    if (this.isFlawless) {
      const ribbon = mkText(this, bothRibbons ? 470 : 360, 380, 'FLAWLESS!', 22, '#6BCB77', '700').setDepth(301).setName('__ov').setScale(0);
      this.tweens.add({ targets: ribbon, scale: 1, delay: 850, duration: 260, ease: 'Back.easeOut' });
    }

    // stars
    for (let i = 0; i < 3; i++) {
      const earned = i < stars;
      const s = this.add.image(360 + (i - 1) * 110, 460, 'star')
        .setDisplaySize(92, 92)
        .setTint(earned ? 0xffc93c : 0xd8d3c8)
        .setDepth(301).setName('__ov').setScale(0);
      this.tweens.add({
        targets: s, scale: 92 / 128, delay: 200 + i * 180, duration: 260, ease: 'Back.easeOut',
        onStart: () => { if (earned) Audio.star(); },
      });
    }

    // coins earned (count-up)
    this.add.image(280, 555, 'coin').setDisplaySize(52, 52).setDepth(301).setName('__ov');
    const coinText = mkText(this, 360, 555, '+0', 44, '#1A1A2E', '700').setDepth(301).setName('__ov');
    this.tweens.addCounter({
      from: 0, to: coins, duration: 550, delay: 500,
      onStart: () => Audio.coin(),
      onUpdate: (tw) => coinText.setText(`+${Math.round(tw.getValue())}`),
    });

    // watch-ad-for-bonus-coins button - one free rewarded bonus per level clear
    const BONUS_COINS = 50;
    const adBtn = textButton(this, 360, 650, {
      label: `+${BONUS_COINS} COINS`, icon: 'icon-ad', color: 0x4d96ff, fontSize: 34, w: 400, h: 92,
      onClick: () => {
        adBtn.disableInteractive();
        Monetization.showRewardedAd((ok) => {
          if (!ok || !adBtn.active) return;
          Save.addCoins(BONUS_COINS);
          Audio.coin();
          this.particles.confetti();
          adBtn.labelText.setText('CLAIMED!');
          adBtn.list[2].setTint(0x6bcb77); // icon image, tinted green as a "done" cue
          this._floatText(360, 650, `+${BONUS_COINS}`, '#6BCB77');
        });
      },
    }).setDepth(301).setName('__ov');

    textButton(this, 360, 765, {
      label: 'NEXT', icon: 'icon-play', color: 0x6bcb77, fontSize: 48,
      onClick: () => this.scene.restart({ level: this.levelNum + 1 }),
    }).setDepth(301).setName('__ov');

    iconButton(this, 290, 890, { icon: 'icon-home', size: 88, onClick: () => this.scene.start('MenuScene') })
      .setDepth(301).setName('__ov');
    iconButton(this, 430, 890, {
      icon: 'icon-share', size: 88, iconTint: 0x4d96ff,
      onClick: () => this._shareScore(stars),
    }).setDepth(301).setName('__ov');
  }

  /** Native share sheet with a fallback to clipboard-copy on desktop web. */
  _shareScore(stars) {
    Audio.click();
    const text = `I scored ${this.score} points on Level ${this.levelNum} in Chroma Hole (${'★'.repeat(stars)}${'☆'.repeat(3 - stars)})! 🕳️`;
    Share.share({ title: 'Chroma Hole', text, dialogTitle: 'Share your score' }).catch(() => {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text)
          .then(() => this._floatText(360, 890, 'Copied!', '#4D96FF'))
          .catch(() => this._floatText(360, 890, 'Share unavailable', '#FF5C5C'));
      }
    });
  }

  _showFailOverlay(reason) {
    if (this.stateName !== 'failed') return;
    this._overlayBase(620);

    const sad = this.add.image(360, 350, 'disc').setDisplaySize(110, 110)
      .setTint(0xff5c5c).setDepth(301).setName('__ov');
    this.add.image(360, 350, reason === 'time' ? 'icon-clock' : 'icon-close')
      .setDisplaySize(60, 60).setTint(0xffffff).setDepth(302).setName('__ov');
    this.tweens.add({ targets: sad, angle: 8, duration: 90, yoyo: true, repeat: 3 });

    mkText(this, 360, 448, `LEVEL ${this.levelNum}`, 40, '#1A1A2E', '700').setDepth(301).setAlpha(0.75).setName('__ov');

    if (!this.reviveUsed) {
      textButton(this, 360, 570, {
        label: reason === 'time' ? '+10s' : 'REVIVE', icon: 'icon-ad', color: 0x6bcb77, fontSize: 42,
        onClick: () => this._rewardedContinue(reason),
      }).setDepth(301).setName('__ov');
    }

    textButton(this, 360, 710, {
      label: 'RETRY', icon: 'icon-retry', color: 0xffd93d, labelColor: '#1A1A2E',
      onClick: () => this.scene.restart({ level: this.levelNum }),
    }).setDepth(301).setName('__ov').list[3].setTint(0x1a1a2e);

    iconButton(this, 360, 840, { icon: 'icon-home', size: 92, onClick: () => this.scene.start('MenuScene') })
      .setDepth(301).setName('__ov');
  }

  _rewardedContinue(reason) {
    Monetization.showRewardedAd((ok) => {
      if (!ok || this.stateName !== 'failed') return;
      this.reviveUsed = true;
      this._destroyOverlay();
      if (reason === 'time') {
        this.timeLeft += 10;
      } else {
        this.hole.revive(Math.max(34, HOLE_START_RADIUS));
        this.timeLeft = Math.max(this.timeLeft, 8);
      }
      this.lastTickSec = -1;
      this.hud.setTime(this.timeLeft);
      this.stateName = 'playing';
    });
  }

  /* ================= spawning ================= */

  _aliveBlobs() {
    let n = 0;
    for (const b of this.blobPool) if (b.alive) n += 1;
    return n;
  }

  _spawnBlob() {
    const free = this.blobPool.find((b) => !b.alive);
    if (!free) return;
    const sizeIndex = pickSizeIndex(this.cfg.sizeWeights);
    const def = SIZE_DEFS[sizeIndex];
    const golden = Math.random() < (this.cfg.goldenChance || 0);
    const colorKey = Math.random() < this.cfg.correctBias
      ? this.hole.colorKey
      : Phaser.Utils.Array.GetRandom(this.cfg.activeColors);

    let x = 360, y = 700;
    for (let tries = 0; tries < 14; tries++) {
      x = Phaser.Math.Between(48, GAME_W - 48);
      y = Phaser.Math.Between(200, 1230);
      const dCap = Phaser.Math.Distance.Between(x, y, this.capsule.x, this.capsule.y);
      if (dCap < this.capsule.bodyRadius + 90) continue;
      const dHole = Phaser.Math.Distance.Between(x, y, this.hole.x, this.hole.y);
      if (dHole < this.hole.radius + 150) continue;
      if (this.pools.some((p) => Phaser.Math.Distance.Between(x, y, p.x, p.y) < p.radius + 46)) continue;
      if (this.spikes.some((sp) => Phaser.Math.Distance.Between(x, y, sp.x, sp.y) < 80)) continue;
      if (this.gates.some((g) => Phaser.Math.Distance.Between(x, y, g.cx, g.cy) < g.span / 2 + 60)) continue;
      break;
    }

    const moving = this.cfg.movingObjects && Math.random() < this.cfg.moveChance;
    free.spawn({ x, y, colorKey, sizeIndex, def, moving, golden });
  }

  /** A spot for a static spike or pickup, clear of pools/capsule/hole start/other hazards. */
  _findOpenSpot() {
    let x = 360, y = 760;
    for (let tries = 0; tries < 20; tries++) {
      x = Phaser.Math.Between(90, GAME_W - 90);
      y = Phaser.Math.Between(430, 1150);
      if (Phaser.Math.Distance.Between(x, y, this.capsule.x, this.capsule.y) < 230) continue;
      if (Phaser.Math.Distance.Between(x, y, HOLE_START.x, HOLE_START.y) < 230) continue;
      if (this.pools.some((p) => Phaser.Math.Distance.Between(x, y, p.baseX, p.baseY) < p.radius + 90)) continue;
      if (this.spikes.some((sp) => Phaser.Math.Distance.Between(x, y, sp.x, sp.y) < 140)) continue;
      if (this.shieldPickup && Phaser.Math.Distance.Between(x, y, this.shieldPickup.x, this.shieldPickup.y) < 140) continue;
      break;
    }
    return { x, y };
  }

  /* ================= presentation helpers ================= */

  _buildBackdrop() {
    // Subtle corner tint blobs so the cream field isn't sterile
    const spots = [
      [90, 250, COLORS.blue], [640, 420, COLORS.yellow],
      [110, 1050, COLORS.purple], [620, 1150, COLORS.green],
    ];
    spots.forEach(([x, y, c], i) => {
      const im = this.add.image(x, y, 'glow').setTint(c).setAlpha(0.10).setDisplaySize(420, 420).setDepth(0);
      this.tweens.add({
        targets: im, y: y - 24, duration: 2600 + i * 420, yoyo: true, repeat: -1, ease: 'Sine.easeInOut',
      });
    });
  }

  _floatText(x, y, str, colorCss) {
    let t = this.floatPool.find((ft) => !ft.visible);
    if (!t) {
      if (this.floatPool.length >= 10) {
        t = this.floatPool[0];
      } else {
        t = mkText(this, 0, 0, '', 30, '#FFFFFF', '700').setDepth(60);
        this.floatPool.push(t);
      }
    }
    t.setVisible(true).setAlpha(1).setScale(1).setPosition(x, y).setText(str).setColor(colorCss);
    t.setStroke('#1A1A2E', 4);
    this.tweens.add({
      targets: t, y: y - 70, alpha: 0, duration: 620, ease: 'Quad.easeOut',
      onComplete: () => t.setVisible(false),
    });
  }

  _showIntroBanner() {
    const c = this.add.container(-420, 340).setDepth(150);
    const bg = this.add.image(0, 0, 'pill').setDisplaySize(430, 96).setTint(INK).setAlpha(0.9);
    const label = mkText(this, -70, 0, `LEVEL ${this.levelNum}`, 44, '#FFFFFF', '700');
    const cap = this.add.image(90, 0, this.cfg.mega ? 'capsule-gem' : 'capsule-hex')
      .setDisplaySize(52, 52).setTint(COLORS[this.cfg.requiredColor]);
    const arrow = this.add.image(148, 0, 'icon-arrow').setDisplaySize(34, 34).setTint(0xffffff);
    c.add([bg, label, cap, arrow]);
    this.tweens.chain({
      targets: c,
      tweens: [
        { x: 360, duration: 360, ease: 'Back.easeOut' },
        { x: 360, duration: 720 },
        { x: 1150, duration: 320, ease: 'Back.easeIn', onComplete: () => c.destroy() },
      ],
    });
  }

  _showDragHint() {
    this.hint = this.add.container(this.hole.x, this.hole.y + 10).setDepth(90);
    const ring = this.add.image(0, 0, 'ring').setDisplaySize(120, 120).setTint(INK).setAlpha(0.6);
    const dot = this.add.image(0, 0, 'disc').setDisplaySize(30, 30).setTint(INK).setAlpha(0.5);
    const label = mkText(this, 0, 96, 'DRAG', 34, '#1A1A2E', '700').setAlpha(0.6);
    this.hint.add([ring, dot, label]);
    this.tweens.add({ targets: ring, scale: ring.scale * 1.35, alpha: 0.15, duration: 800, repeat: -1 });
    this.tweens.add({ targets: this.hint, x: this.hole.x + 130, duration: 900, yoyo: true, repeat: -1, ease: 'Sine.easeInOut' });
  }

  _dismissHint() {
    if (this.hint) {
      Save.markDragHintSeen();
      this.hint.destroy();
      this.hint = null;
    }
  }

  _retintTrail() {
    if (!this.trail) return;
    const skin = this.hole.skin;
    let tint = this.hole.colorHex;
    if (skin.glow && skin.glow.startsWith && skin.glow.startsWith('#')) {
      tint = parseInt(skin.glow.slice(1), 16);
    }
    this.trail.setParticleTint(tint);
  }
}
