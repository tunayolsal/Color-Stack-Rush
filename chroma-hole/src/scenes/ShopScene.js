/**
 * ShopScene - two tabs: cosmetic hole SKINS (3x4 grid) and COINS (IAP packs).
 * Skin card states: ACTIVE (green ring) / owned (tap to apply) /
 * affordable (coin price) / premium (IAP stub) / limited (seasonal ribbon).
 * Coin packs are still an IAP stub (see MonetizationManager.purchaseProduct)
 * until real Play Billing is wired in.
 */
import Phaser from 'phaser';
import { CREAM, INK } from '../constants.js';
import Save from '../systems/SaveManager.js';
import Audio from '../systems/AudioManager.js';
import Monetization from '../systems/MonetizationManager.js';
import { iconButton, textButton, coinBadge, mkText, toast } from '../ui/UIFactory.js';
import skins from '../data/skins.json';
import coinPacks from '../data/coinPacks.json';

export default class ShopScene extends Phaser.Scene {
  constructor() { super('ShopScene'); }

  create() {
    this.cameras.main.setBackgroundColor(CREAM);

    iconButton(this, 56, 60, { icon: 'icon-close', size: 84, onClick: () => this.scene.start('MenuScene') });
    this.add.image(300, 60, 'icon-cart').setDisplaySize(44, 44).setTint(INK);
    mkText(this, 384, 60, 'SHOP', 44, '#1A1A2E', '700');
    this.coins = coinBadge(this, 590, 60);

    // tabs
    this.tabSkinsBtn = textButton(this, 220, 142, {
      label: 'SKINS', color: 0x6bcb77, fontSize: 30, w: 260, h: 72, onClick: () => this._setTab('skins'),
    });
    this.tabCoinsBtn = textButton(this, 500, 142, {
      label: 'COINS', color: 0xe4ddcc, labelColor: '#1A1A2E', fontSize: 30, w: 260, h: 72, onClick: () => this._setTab('coins'),
    });

    this.skinsLayer = this.add.container(0, 0);
    this.coinsLayer = this.add.container(0, 0).setVisible(false);

    // 3 columns; row spacing/card size auto-shrink a touch once a skin count
    // needs a 5th row, so everything still fits on one non-scrolling screen.
    this.cards = [];
    const rows = Math.ceil(skins.length / 3);
    const rowH = rows >= 5 ? 218 : 262;
    this.cardSize = rows >= 5 ? { w: 204, h: 204 } : { w: 208, h: 236 };
    skins.forEach((skin, i) => {
      const col = i % 3;
      const row = Math.floor(i / 3);
      this._makeSkinCard(skin, 136 + col * 224, 280 + row * rowH);
    });

    coinPacks.forEach((pack, i) => this._makeCoinCard(pack, 360, 300 + i * 176));

    this.refresh();
  }

  _setTab(tab) {
    Audio.click();
    const onSkins = tab === 'skins';
    this.skinsLayer.setVisible(onSkins);
    this.coinsLayer.setVisible(!onSkins);
    this.tabSkinsBtn.list[1].setTint(onSkins ? 0x6bcb77 : 0xe4ddcc);
    this.tabSkinsBtn.labelText.setColor(onSkins ? '#FFFFFF' : '#1A1A2E');
    this.tabCoinsBtn.list[1].setTint(!onSkins ? 0x6bcb77 : 0xe4ddcc);
    this.tabCoinsBtn.labelText.setColor(!onSkins ? '#FFFFFF' : '#1A1A2E');
  }

  /* ---------------- skins tab ---------------- */

  _makeSkinCard(skin, x, y) {
    const { w, h } = this.cardSize;
    const s = h / 236; // scale factor for the inner layout, relative to the original 208x236 design
    const c = this.add.container(x, y);
    const shadow = this.add.image(4, 6, 'card').setDisplaySize(w, h).setTint(INK).setAlpha(0.1);
    const activeRing = this.add.image(0, 0, 'card').setDisplaySize(w + 14, h + 14).setTint(0x6bcb77).setVisible(false);
    const bg = this.add.image(0, 0, 'card').setDisplaySize(w, h).setTint(0xffffff);

    // mini hole preview
    let glowTint = 0x1a1a2e;
    if (skin.glow && skin.glow.startsWith && skin.glow.startsWith('#')) glowTint = parseInt(skin.glow.slice(1), 16);
    const py = -46 * s;
    const glow = this.add.image(0, py, 'glow').setDisplaySize(150 * s, 150 * s).setTint(glowTint).setAlpha(0.65);
    const rim = this.add.image(0, py, 'rim').setDisplaySize(96 * s, 96 * s).setTint(glowTint === 0x1a1a2e ? 0xff5c5c : glowTint);
    const core = this.add.image(0, py, 'hole-core').setDisplaySize(90 * s, 90 * s);
    const parts = [shadow, activeRing, bg, glow, rim, core];

    if (skin.pattern !== 'none') {
      const p = this.add.image(0, py, skin.pattern === 'dots' ? 'pattern-dots' : 'deco-ring')
        .setDisplaySize(118 * s, 118 * s).setTint(glowTint === 0x1a1a2e ? 0xffffff : glowTint).setAlpha(0.9);
      this.tweens.add({ targets: p, rotation: Math.PI * 2, duration: 6000, repeat: -1 });
      parts.push(p);
    }
    if (skin.glow === 'rainbow') {
      this.tweens.addCounter({
        from: 0, to: 1, duration: 3600, repeat: -1,
        onUpdate: (tw) => {
          const col = Phaser.Display.Color.HSVToRGB(tw.getValue(), 0.6, 1).color;
          glow.setTint(col); rim.setTint(col);
        },
      });
    }

    const name = mkText(this, 0, 34 * s, skin.name.toUpperCase(), 22 * s, '#1A1A2E', '700').setAlpha(0.8);
    parts.push(name);

    if (skin.limited) {
      const ribbonBg = this.add.image(-72 * s, -98 * s, 'pill').setDisplaySize(112 * s, 34 * s).setTint(0xff5c5c);
      const ribbonTx = mkText(this, -72 * s, -98 * s, 'LIMITED', 15 * s, '#FFFFFF', '700');
      parts.push(ribbonBg, ribbonTx);
    }

    // status row (filled in refresh())
    const statusIcon = this.add.image(-44 * s, 82 * s, 'coin').setDisplaySize(34 * s, 34 * s);
    const statusText = mkText(this, 12 * s, 82 * s, '', 24 * s, '#1A1A2E', '700');
    parts.push(statusIcon, statusText);

    c.add(parts);
    c.setSize(w, h);
    c.setInteractive({ useHandCursor: true });
    c.on('pointerdown', () => c.setScale(0.95));
    c.on('pointerout', () => c.setScale(1));
    c.on('pointerup', () => { c.setScale(1); this._onCardTap(skin); });

    this.skinsLayer.add(c);
    this.cards.push({ skin, activeRing, statusIcon, statusText });
  }

  _onCardTap(skin) {
    Audio.click();
    if (Save.hasSkin(skin.id)) {
      Save.setActiveSkin(skin.id);
      Audio.ready();
      this.refresh();
      return;
    }
    if (skin.premium) {
      Monetization.purchaseProduct(skin.productId).then((res) => {
        if (res.success) {
          Save.unlockSkin(skin.id);
          Save.setActiveSkin(skin.id);
          Audio.fanfare();
          toast(this, `${skin.name} unlocked!`);
          this.refresh();
        } else {
          toast(this, 'Purchase failed');
        }
      });
      return;
    }
    if (Save.spendCoins(skin.price)) {
      Save.unlockSkin(skin.id);
      Save.setActiveSkin(skin.id);
      Audio.fanfare();
      toast(this, `${skin.name} unlocked!`);
    } else {
      Audio.deny();
      toast(this, 'Not enough coins');
    }
    this.refresh();
  }

  /* ---------------- coins tab ---------------- */

  _makeCoinCard(pack, x, y) {
    const c = this.add.container(x, y);
    const shadow = this.add.image(4, 6, 'btn-lg').setDisplaySize(560, 152).setTint(INK).setAlpha(0.1);
    const bg = this.add.image(0, 0, 'btn-lg').setDisplaySize(560, 152).setTint(0xffffff);
    const coinIcon = this.add.image(-220, 0, 'coin').setDisplaySize(76, 76);
    const amount = mkText(this, -130, -18, `${pack.coins.toLocaleString()}`, 36, '#1A1A2E', '700').setOrigin(0, 0.5);
    const parts = [shadow, bg, coinIcon, amount];
    if (pack.bonus) {
      const bonus = mkText(this, -130, 20, `+${pack.bonus} BONUS`, 20, '#6BCB77', '700').setOrigin(0, 0.5);
      parts.push(bonus);
    }
    if (pack.bestValue) {
      const ribbonBg = this.add.image(-180, -68, 'pill').setDisplaySize(150, 34).setTint(0xffc93c);
      const ribbonTx = mkText(this, -180, -68, 'BEST VALUE', 15, '#1A1A2E', '700');
      parts.push(ribbonBg, ribbonTx);
    }

    const buyBtn = this.add.container(210, 0);
    const buyBg = this.add.image(0, 0, 'btn-lg').setDisplaySize(180, 84).setTint(0x4d96ff);
    const buyTx = mkText(this, 0, 0, pack.priceLabel, 28, '#FFFFFF', '700');
    buyBtn.add([buyBg, buyTx]);
    parts.push(buyBtn);

    c.add(parts);
    c.setSize(560, 152);
    c.setInteractive({ useHandCursor: true });
    c.on('pointerdown', () => c.setScale(0.97));
    c.on('pointerout', () => c.setScale(1));
    c.on('pointerup', () => { c.setScale(1); this._onCoinPackTap(pack); });

    this.coinsLayer.add(c);
  }

  _onCoinPackTap(pack) {
    Audio.click();
    Monetization.purchaseProduct(pack.productId).then((res) => {
      if (res.success) {
        Save.addCoins(pack.coins + (pack.bonus || 0));
        Audio.coin();
        Audio.fanfare();
        toast(this, `+${pack.coins + (pack.bonus || 0)} coins!`);
        this.refresh();
      } else {
        toast(this, 'Purchase failed');
      }
    });
  }

  refresh() {
    this.coins.refresh();
    this.cards.forEach(({ skin, activeRing, statusIcon, statusText }) => {
      const owned = Save.hasSkin(skin.id);
      const active = Save.data.activeSkin === skin.id;
      activeRing.setVisible(active);
      if (active) {
        statusIcon.setTexture('icon-check').setTint(0x6bcb77).setDisplaySize(30, 30).setX(-34);
        statusText.setText('ON').setColor('#6BCB77').setX(10);
      } else if (owned) {
        statusIcon.setTexture('icon-check').setTint(0xb9b3a5).setDisplaySize(30, 30).setX(-40);
        statusText.setText('USE').setColor('#1A1A2E').setX(14);
      } else if (skin.premium) {
        statusIcon.setTexture('icon-crown').setTint(0xffc93c).setDisplaySize(32, 32).setX(-52);
        statusText.setText(skin.priceLabel).setColor('#1A1A2E').setX(20);
      } else {
        statusIcon.setTexture('coin').clearTint().setDisplaySize(32, 32).setX(-44);
        statusText.setText(String(skin.price)).setColor('#1A1A2E').setX(12);
      }
    });
  }
}
