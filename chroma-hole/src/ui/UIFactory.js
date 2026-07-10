/**
 * UIFactory - tiny helpers for consistent, juicy buttons/text/toasts.
 * All backgrounds use the white rounded-rect textures generated in BootScene
 * and are tinted per use.
 */
import Save from '../systems/SaveManager.js';
import Audio from '../systems/AudioManager.js';
import { FONT, INK_CSS } from '../constants.js';

export function mkText(scene, x, y, str, size = 32, color = INK_CSS, weight = '600') {
  return scene.add.text(x, y, str, {
    fontFamily: FONT,
    fontSize: `${size}px`,
    color,
    fontStyle: weight,
  }).setOrigin(0.5).setResolution(2);
}

function pressify(container, onClick, baseScale = 1) {
  container.on('pointerover', () => container.setScale(baseScale * 1.05));
  container.on('pointerout', () => container.setScale(baseScale));
  container.on('pointerdown', () => container.setScale(baseScale * 0.93));
  container.on('pointerup', () => {
    container.setScale(baseScale);
    Audio.click();
    if (onClick) onClick();
  });
}

/** Square icon button (pause, settings, back...). */
export function iconButton(scene, x, y, { icon, bg = 0xffffff, iconTint = 0x1a1a2e, size = 104, onClick, bgAlpha = 1 }) {
  const c = scene.add.container(x, y);
  const shadow = scene.add.image(4, 6, 'btn-sq').setDisplaySize(size, size).setTint(0x1a1a2e).setAlpha(0.12);
  const bgImg = scene.add.image(0, 0, 'btn-sq').setDisplaySize(size, size).setTint(bg).setAlpha(bgAlpha);
  const ic = scene.add.image(0, 0, icon).setDisplaySize(size * 0.52, size * 0.52).setTint(iconTint);
  c.add([shadow, bgImg, ic]);
  c.setSize(size, size);
  c.setInteractive({ useHandCursor: true });
  pressify(c, onClick);
  c.iconImage = ic;
  c.bgImage = bgImg;
  return c;
}

/** Large rounded text button, optionally with a leading icon. */
export function textButton(scene, x, y, { w = 420, h = 118, label, icon, color = 0x6bcb77, labelColor = '#FFFFFF', fontSize = 44, onClick }) {
  const c = scene.add.container(x, y);
  const shadow = scene.add.image(5, 8, 'btn-lg').setDisplaySize(w, h).setTint(0x1a1a2e).setAlpha(0.14);
  const bg = scene.add.image(0, 0, 'btn-lg').setDisplaySize(w, h).setTint(color);
  c.add([shadow, bg]);
  let textX = 0;
  if (icon) {
    const ic = scene.add.image(0, 0, icon).setDisplaySize(h * 0.42, h * 0.42).setTint(0xffffff);
    const t = mkText(scene, 0, 0, label, fontSize, labelColor);
    const total = ic.displayWidth + 18 + t.width;
    ic.setX(-total / 2 + ic.displayWidth / 2);
    t.setX(ic.x + ic.displayWidth / 2 + 18 + t.width / 2);
    c.add([ic, t]);
    c.labelText = t;
  } else {
    const t = mkText(scene, textX, 0, label, fontSize, labelColor);
    c.add(t);
    c.labelText = t;
  }
  c.setSize(w, h);
  c.setInteractive({ useHandCursor: true });
  pressify(c, onClick);
  return c;
}

/** Coin pill (icon + amount). Call .refresh() after coins change. */
export function coinBadge(scene, x, y) {
  const c = scene.add.container(x, y).setDepth(50);
  const bg = scene.add.image(0, 0, 'pill').setDisplaySize(190, 64).setTint(0xffffff).setAlpha(0.92);
  const coin = scene.add.image(-58, 0, 'coin').setDisplaySize(44, 44);
  const t = mkText(scene, 16, 0, String(Save.coins), 34);
  c.add([bg, coin, t]);
  c.refresh = () => t.setText(String(Save.coins));
  return c;
}

/** Transient bottom toast for feedback (kept minimal / mostly for stubs). */
export function toast(scene, msg) {
  const c = scene.add.container(360, 1210).setDepth(600).setAlpha(0);
  const w = Math.max(280, msg.length * 15 + 70);
  const bg = scene.add.image(0, 0, 'pill').setDisplaySize(w, 62).setTint(0x1a1a2e).setAlpha(0.92);
  const t = mkText(scene, 0, 0, msg, 25, '#FFFFFF', '500');
  c.add([bg, t]);
  scene.tweens.add({ targets: c, y: 1150, alpha: 1, duration: 220, ease: 'Quad.easeOut' });
  scene.time.delayedCall(1500, () => {
    scene.tweens.add({ targets: c, alpha: 0, y: 1120, duration: 260, onComplete: () => c.destroy() });
  });
  return c;
}
