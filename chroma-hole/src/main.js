import Phaser from 'phaser';
import { GAME_W, GAME_H, CREAM_CSS } from './constants.js';
import Audio from './systems/AudioManager.js';
import BootScene from './scenes/BootScene.js';
import MenuScene from './scenes/MenuScene.js';
import GameScene from './scenes/GameScene.js';
import ShopScene from './scenes/ShopScene.js';
import SettingsScene from './scenes/SettingsScene.js';
import ArenaScene from './scenes/ArenaScene.js';

const game = new Phaser.Game({
  type: Phaser.AUTO,
  parent: 'game',
  width: GAME_W,
  height: GAME_H,
  backgroundColor: CREAM_CSS,
  scale: {
    mode: Phaser.Scale.FIT,
    autoCenter: Phaser.Scale.CENTER_BOTH,
  },
  render: { antialias: true },
  input: { activePointers: 2 },
  scene: [BootScene, MenuScene, GameScene, ShopScene, SettingsScene, ArenaScene],
});

// Web Audio must be unlocked by a user gesture (mobile + desktop policies).
['pointerdown', 'touchstart', 'keydown'].forEach((ev) => {
  window.addEventListener(ev, () => Audio.unlock(), { passive: true });
});

// ---- dev-only helpers (stripped from production builds by Vite) ----
if (import.meta.env.DEV) {
  window.__CH = { game };
  // Capture the live canvas and POST it to the vite screenshot endpoint
  // (see vite.config.js). Used to produce store-assets automatically.
  window.__snap = (name) => new Promise((resolve) => {
    game.renderer.snapshot((img) => {
      const c = document.createElement('canvas');
      c.width = img.width;
      c.height = img.height;
      c.getContext('2d').drawImage(img, 0, 0);
      fetch('/__screenshot', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, data: c.toDataURL('image/png') }),
      }).then((r) => resolve(r.status)).catch((e) => resolve(String(e)));
    });
  });
}
