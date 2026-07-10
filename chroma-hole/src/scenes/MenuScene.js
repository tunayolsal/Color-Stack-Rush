/**
 * MenuScene - Play / Shop / Trophies / Settings + coin display,
 * daily gift popup (streak-based) and achievements browser.
 * Shows an animated hole preview wearing the active skin.
 */
import Phaser from 'phaser';
import { GAME_W, GAME_H, COLORS, CREAM, PALETTE, INK, FONT } from '../constants.js';
import Save from '../systems/SaveManager.js';
import Audio from '../systems/AudioManager.js';
import Notifications from '../systems/NotificationManager.js';
import { ACHIEVEMENTS, checkAchievements } from '../systems/Achievements.js';
import { textButton, iconButton, coinBadge, mkText } from '../ui/UIFactory.js';
import skins from '../data/skins.json';

export default class MenuScene extends Phaser.Scene {
  constructor() { super('MenuScene'); }

  create() {
    this.cameras.main.setBackgroundColor(CREAM);
    this.cameras.main.fadeIn(240, 26, 26, 46);

    // floating decorative blobs
    const shapes = ['blob-round', 'blob-blobby', 'blob-crystal'];
    for (let i = 0; i < 7; i++) {
      const b = this.add.image(
        Phaser.Math.Between(60, GAME_W - 60),
        Phaser.Math.Between(120, GAME_H - 80),
        shapes[i % 3],
      ).setTint(PALETTE[i % PALETTE.length]).setAlpha(0.16)
        .setDisplaySize(70 + (i % 3) * 40, 70 + (i % 3) * 40);
      this.tweens.add({
        targets: b, y: b.y - Phaser.Math.Between(20, 50), angle: Phaser.Math.Between(-14, 14),
        duration: Phaser.Math.Between(2200, 3600), yoyo: true, repeat: -1, ease: 'Sine.easeInOut',
      });
    }

    // logo hole with the active skin's glow
    const skin = skins.find((s) => s.id === Save.data.activeSkin) || skins[0];
    const cy = 330;
    let glowTint = COLORS.red;
    if (skin.glow && skin.glow.startsWith && skin.glow.startsWith('#')) glowTint = parseInt(skin.glow.slice(1), 16);
    const glow = this.add.image(360, cy, 'glow').setTint(glowTint).setAlpha(0.5).setScale(1.9);
    const rim = this.add.image(360, cy, 'rim').setTint(COLORS.red).setScale(1.06);
    this.add.image(360, cy, 'hole-core');
    const swirl = this.add.image(360, cy, 'swirl').setTint(COLORS.red).setAlpha(0.5).setScale(0.8);
    this.tweens.add({ targets: swirl, rotation: Math.PI * 2, duration: 4000, repeat: -1 });
    if (skin.glow === 'rainbow' || skin.glow === 'match') {
      this.tweens.addCounter({
        from: 0, to: 1, duration: 4200, repeat: -1,
        onUpdate: (tw) => {
          const c = Phaser.Display.Color.HSVToRGB(tw.getValue(), 0.55, 1).color;
          glow.setTint(c); rim.setTint(c); swirl.setTint(c);
        },
      });
    }

    // title
    mkText(this, 360, 560, 'CHROMA', 88, '#1A1A2E', '700');
    'HOLE'.split('').forEach((ch, i) => {
      this.add.text(360 - 108 + i * 72, 655, ch, {
        fontFamily: FONT, fontSize: '88px', fontStyle: '700', color: '#FFFFFF',
      }).setOrigin(0.5).setResolution(2).setTint(PALETTE[i % PALETTE.length])
        .setStroke('#1A1A2E', 8);
    });

    const level = Save.data.currentLevel;
    const play = textButton(this, 360, 850, {
      label: 'PLAY', icon: 'icon-play', color: 0x6bcb77, w: 460, h: 132, fontSize: 54,
      onClick: () => {
        this.scene.start('GameScene', { level });
      },
    });
    this.tweens.add({ targets: play, scale: 1.03, duration: 700, yoyo: true, repeat: -1, ease: 'Sine.easeInOut' });
    mkText(this, 360, 936, `LEVEL ${level}`, 30, '#1A1A2E', '600').setAlpha(0.55);

    iconButton(this, 200, 1050, { icon: 'icon-cart', size: 112, onClick: () => this.scene.start('ShopScene') });
    iconButton(this, 360, 1050, { icon: 'icon-trophy', size: 112, onClick: () => this._showAchievements() });
    iconButton(this, 520, 1050, { icon: 'icon-gear', size: 112, onClick: () => this.scene.start('SettingsScene') });

    this.coins = coinBadge(this, 590, 64);

    // total stars
    this.add.image(96, 64, 'star').setDisplaySize(42, 42).setTint(0xffc93c);
    this.starsText = mkText(this, 152, 64, String(Save.totalStars()), 34, '#1A1A2E', '700');

    // daily gift button (top-right, red dot + pulse while claimable)
    this.giftBtn = iconButton(this, 640, 168, {
      icon: 'icon-gift', size: 96, iconTint: 0xb983ff,
      onClick: () => this._showDailyGift(),
    });
    this.giftDot = this.add.image(676, 132, 'disc').setDisplaySize(26, 26).setTint(0xff5c5c).setDepth(60);
    this._syncGiftState();

    // pick up any trophies earned since the last visit (e.g. shop unlocks)
    checkAchievements(this);

    // local-only "come back for your gift" reminder (no-op on web, see NotificationManager)
    if (Save.canClaimDaily()) Notifications.scheduleDailyReminder();

    Audio.startMusic();
  }

  _syncGiftState() {
    const claimable = Save.canClaimDaily();
    this.giftDot.setVisible(claimable);
    if (this.giftPulse) { this.giftPulse.stop(); this.giftPulse = null; this.giftBtn.setScale(1); }
    if (claimable) {
      this.giftPulse = this.tweens.add({
        targets: this.giftBtn, scale: 1.1, duration: 480, yoyo: true, repeat: -1, ease: 'Sine.easeInOut',
      });
    }
  }

  /* ================= popups ================= */

  _popupBase(panelH) {
    const dim = this.add.rectangle(360, 640, GAME_W, GAME_H, INK, 0.55)
      .setDepth(700).setInteractive().setName('__pop');
    const panel = this.add.image(360, 640, 'panel').setDisplaySize(600, panelH)
      .setTint(0xfffdf7).setDepth(700).setName('__pop');
    panel.setAlpha(0).setScale(panel.scaleX * 0.86, panel.scaleY * 0.86);
    this.tweens.add({
      targets: panel,
      scaleX: 600 / panel.width, scaleY: panelH / panel.height,
      alpha: 1, duration: 220, ease: 'Back.easeOut',
    });
    const top = 640 - panelH / 2;
    iconButton(this, 600, top + 64, { icon: 'icon-close', size: 76, onClick: () => this._closePopup() })
      .setDepth(702).setName('__pop');
    return top;
  }

  _closePopup() {
    this.children.list.filter((c) => c.name === '__pop').forEach((c) => c.destroy());
  }

  /* ---------------- daily gift ---------------- */

  _showDailyGift() {
    this._closePopup();
    const top = this._popupBase(660);

    this.add.image(266, top + 64, 'icon-gift').setDisplaySize(52, 52).setTint(0xb983ff)
      .setDepth(701).setName('__pop');
    mkText(this, 380, top + 64, 'DAILY GIFT', 40, '#1A1A2E', '700').setDepth(701).setName('__pop');

    const canClaim = Save.canClaimDaily();
    const nextStreak = Math.max(1, Save.dailyNextStreak());
    const reward = Save.dailyReward();

    // streak pips: 7 days, filled up to the streak being claimed today
    for (let i = 1; i <= 7; i++) {
      const x = 360 + (i - 4) * 74;
      const y = top + 190;
      const done = i < nextStreak || (!canClaim && i <= nextStreak);
      const today = canClaim && i === Math.min(nextStreak, 7);
      const disc = this.add.image(x, y, 'disc').setDisplaySize(56, 56)
        .setTint(done ? 0xffc93c : today ? 0x6bcb77 : 0xe4ddcc)
        .setDepth(701).setName('__pop');
      mkText(this, x, y, String(i), 26, done || today ? '#FFFFFF' : '#B9B3A5', '700')
        .setDepth(702).setName('__pop');
      if (today) {
        this.tweens.add({ targets: disc, scale: disc.scale * 1.15, duration: 420, yoyo: true, repeat: -1 });
      }
    }
    mkText(this, 360, top + 258, `STREAK ${Save.data.daily.streak || 0}`, 24, '#1A1A2E', '600')
      .setAlpha(0.5).setDepth(701).setName('__pop');

    // reward row
    this.add.image(300, top + 340, 'coin').setDisplaySize(58, 58).setDepth(701).setName('__pop');
    mkText(this, 386, top + 340, `+${reward}`, 52, '#1A1A2E', '700').setDepth(701).setName('__pop');

    if (canClaim) {
      const btn = textButton(this, 360, top + 480, {
        label: 'CLAIM', icon: 'icon-gift', color: 0x6bcb77, fontSize: 44, w: 380, h: 108,
        onClick: () => {
          const res = Save.claimDaily();
          if (!res) return;
          Audio.gift();
          Audio.coin();
          btn.disableInteractive();
          btn.labelText.setText('CLAIMED!');
          // coins fly to the wallet badge
          for (let i = 0; i < 6; i++) {
            const cn = this.add.image(360, top + 480, 'coin').setDisplaySize(36, 36).setDepth(710);
            this.tweens.add({
              targets: cn, x: 590, y: 64, delay: i * 70, duration: 480, ease: 'Cubic.easeIn',
              onComplete: () => { cn.destroy(); this.coins.refresh(); },
            });
          }
          this._syncGiftState();
          Notifications.cancelDailyReminder();
          this.time.delayedCall(900, () => this._closePopup());
        },
      }).setDepth(701).setName('__pop');
    } else {
      const pill = this.add.image(360, top + 480, 'pill').setDisplaySize(420, 96)
        .setTint(0xe4ddcc).setDepth(701).setName('__pop');
      mkText(this, 360, top + 480, 'COME BACK TOMORROW', 26, '#8A8474', '700')
        .setDepth(702).setName('__pop');
    }
  }

  /* ---------------- achievements browser ---------------- */

  _showAchievements() {
    this._closePopup();
    const top = this._popupBase(1030);

    this.add.image(232, top + 64, 'icon-trophy').setDisplaySize(50, 50).setTint(0xffc93c)
      .setDepth(701).setName('__pop');
    mkText(this, 382, top + 64, 'TROPHIES', 40, '#1A1A2E', '700').setDepth(701).setName('__pop');

    const unlockedCount = Save.data.achievements.length;
    mkText(this, 360, top + 112, `${unlockedCount} / ${ACHIEVEMENTS.length}`, 24, '#1A1A2E', '600')
      .setAlpha(0.5).setDepth(701).setName('__pop');

    ACHIEVEMENTS.forEach((a, i) => {
      const y = top + 182 + i * 80;
      const unlocked = Save.hasAchievement(a.id);

      const badge = this.add.image(140, y, 'disc').setDisplaySize(58, 58)
        .setTint(unlocked ? 0xffc93c : 0xe4ddcc).setDepth(701).setName('__pop');
      this.add.image(140, y, a.icon).setDisplaySize(34, 34)
        .setTint(unlocked ? 0x1a1a2e : 0xb9b3a5).setDepth(702).setName('__pop');

      mkText(this, 190, y - 14, a.name.toUpperCase(), 25, unlocked ? '#1A1A2E' : '#8A8474', '700')
        .setOrigin(0, 0.5).setDepth(701).setName('__pop');
      let desc = a.desc;
      if (!unlocked && a.progress) {
        const [cur, goal] = a.progress(Save.data);
        desc += `  (${cur}/${goal})`;
      }
      mkText(this, 190, y + 15, desc, 18, '#1A1A2E', '500')
        .setOrigin(0, 0.5).setAlpha(0.55).setDepth(701).setName('__pop');

      if (unlocked) {
        this.add.image(566, y, 'icon-check').setDisplaySize(38, 38).setTint(0x6bcb77)
          .setDepth(701).setName('__pop');
      } else {
        this.add.image(536, y, 'coin').setDisplaySize(28, 28).setDepth(701).setName('__pop');
        mkText(this, 578, y, `${a.coins}`, 22, '#1A1A2E', '700').setDepth(701).setName('__pop');
      }
    });
  }
}
