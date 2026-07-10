/**
 * SettingsScene - sound/music toggles, restore purchases (stub),
 * privacy policy link, progress reset.
 */
import Phaser from 'phaser';
import { Browser } from '@capacitor/browser';
import { CREAM, INK } from '../constants.js';
import Save from '../systems/SaveManager.js';
import Audio from '../systems/AudioManager.js';
import Monetization from '../systems/MonetizationManager.js';
import { iconButton, textButton, mkText, toast } from '../ui/UIFactory.js';

const PRIVACY_POLICY_URL = 'https://tunayolsal.github.io/chroma-hole-privacy/';

export default class SettingsScene extends Phaser.Scene {
  constructor() { super('SettingsScene'); }

  create() {
    this.cameras.main.setBackgroundColor(CREAM);

    iconButton(this, 56, 60, { icon: 'icon-close', size: 84, onClick: () => this.scene.start('MenuScene') });
    this.add.image(268, 60, 'icon-gear').setDisplaySize(44, 44).setTint(INK);
    mkText(this, 396, 60, 'SETTINGS', 44, '#1A1A2E', '700');

    const panel = this.add.image(360, 560, 'panel').setDisplaySize(600, 620).setTint(0xffffff).setAlpha(0.85);

    // --- sound row ---
    this.add.image(160, 360, 'icon-sound').setDisplaySize(52, 52).setTint(INK);
    mkText(this, 300, 360, 'SOUND', 38, '#1A1A2E', '600').setOrigin(0, 0.5).setX(210);
    this.soundBtn = iconButton(this, 540, 360, {
      icon: Save.data.settings.sound ? 'icon-check' : 'icon-close',
      bg: Save.data.settings.sound ? 0x6bcb77 : 0xd8d3c8,
      iconTint: 0xffffff,
      size: 92,
      onClick: () => {
        Audio.setSound(!Save.data.settings.sound);
        this._syncToggles();
      },
    });

    // --- music row ---
    this.add.image(160, 500, 'icon-music').setDisplaySize(52, 52).setTint(INK);
    mkText(this, 300, 500, 'MUSIC', 38, '#1A1A2E', '600').setOrigin(0, 0.5).setX(210);
    this.musicBtn = iconButton(this, 540, 500, {
      icon: Save.data.settings.music ? 'icon-check' : 'icon-close',
      bg: Save.data.settings.music ? 0x6bcb77 : 0xd8d3c8,
      iconTint: 0xffffff,
      size: 92,
      onClick: () => {
        Audio.setMusic(!Save.data.settings.music);
        this._syncToggles();
      },
    });

    // --- restore purchases (stub) ---
    textButton(this, 360, 650, {
      label: 'RESTORE PURCHASES', color: 0x4d96ff, fontSize: 32, w: 480, h: 96,
      onClick: () => {
        Monetization.restorePurchases().then(() => toast(this, 'Purchases restored (stub)'));
      },
    });

    // --- privacy policy ---
    textButton(this, 360, 770, {
      label: 'PRIVACY POLICY', color: 0xb983ff, fontSize: 32, w: 480, h: 96,
      onClick: () => {
        Browser.open({ url: PRIVACY_POLICY_URL }).catch(() => {
          window.open(PRIVACY_POLICY_URL, '_blank');
        });
      },
    });

    // --- reset progress (double-tap confirm) ---
    this.resetArmed = false;
    this.resetBtn = textButton(this, 360, 890, {
      label: 'RESET PROGRESS', color: 0xffffff, labelColor: '#FF5C5C', fontSize: 28, w: 420, h: 84,
      onClick: () => {
        if (!this.resetArmed) {
          this.resetArmed = true;
          this.resetBtn.labelText.setText('TAP AGAIN TO CONFIRM');
          this.time.delayedCall(2200, () => {
            this.resetArmed = false;
            if (this.resetBtn.active) this.resetBtn.labelText.setText('RESET PROGRESS');
          });
        } else {
          Save.reset();
          toast(this, 'Progress reset');
          this.resetArmed = false;
          this.resetBtn.labelText.setText('RESET PROGRESS');
        }
      },
    });

    mkText(this, 360, 1210, 'Chroma Hole v1.0.0', 24, '#1A1A2E', '400').setAlpha(0.4);
  }

  _syncToggles() {
    const s = Save.data.settings;
    this.soundBtn.iconImage.setTexture(s.sound ? 'icon-check' : 'icon-close');
    this.soundBtn.bgImage.setTint(s.sound ? 0x6bcb77 : 0xd8d3c8);
    this.musicBtn.iconImage.setTexture(s.music ? 'icon-check' : 'icon-close');
    this.musicBtn.bgImage.setTint(s.music ? 0x6bcb77 : 0xd8d3c8);
  }
}
