/**
 * ArenaScene - PHASE 2 SKELETON, intentionally empty.
 *
 * Planned: realtime ".io" mode where multiple players share one map and
 * bigger, correctly-colored holes can swallow rival holes. Requires the
 * separate backend described in NetworkManager.js / README.md.
 * This scene is registered but never started by the shipped game.
 */
import Phaser from 'phaser';

export default class ArenaScene extends Phaser.Scene {
  constructor() { super('ArenaScene'); }

  create() {
    console.warn('[Arena] Phase 2 placeholder scene - returning to menu.');
    this.scene.start('MenuScene');
  }
}
