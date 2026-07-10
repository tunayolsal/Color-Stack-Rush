/**
 * Achievements - 10 coin-rewarding trophies checked against SaveManager data.
 * `checkAchievements(scene)` is called after meaningful events (level end,
 * shop purchase, menu open); newly unlocked ones slide a toast in from the
 * top and grant their coin reward. The full list is browsable from the
 * trophy button on the menu.
 */
import Save from './SaveManager.js';
import Audio from './AudioManager.js';
import { mkText } from '../ui/UIFactory.js';

const sumStars = (d) => Object.values(d.stars).reduce((a, b) => a + b, 0);

export const ACHIEVEMENTS = [
  {
    id: 'first_gulp', name: 'First Gulp', desc: 'Swallow your first blob',
    icon: 'blob-round', coins: 10,
    check: (d) => d.totalSwallowed >= 1,
  },
  {
    id: 'hungry', name: 'Hungry', desc: 'Swallow 100 blobs',
    icon: 'blob-blobby', coins: 25,
    check: (d) => d.totalSwallowed >= 100,
    progress: (d) => [Math.min(d.totalSwallowed, 100), 100],
  },
  {
    id: 'glutton', name: 'Glutton', desc: 'Swallow 500 blobs',
    icon: 'blob-crystal', coins: 60,
    check: (d) => d.totalSwallowed >= 500,
    progress: (d) => [Math.min(d.totalSwallowed, 500), 500],
  },
  {
    id: 'combo_5', name: 'Snack Chain', desc: 'Reach a 5x combo',
    icon: 'spark', coins: 20,
    check: (d) => d.bestCombo >= 5,
  },
  {
    id: 'combo_10', name: 'Feeding Frenzy', desc: 'Reach a 10x combo',
    icon: 'spark', coins: 50,
    check: (d) => d.bestCombo >= 10,
  },
  {
    id: 'lv_5', name: 'Warming Up', desc: 'Reach level 5',
    icon: 'capsule-hex', coins: 15,
    check: (d) => d.currentLevel >= 5,
  },
  {
    id: 'lv_11', name: 'Mega Muncher', desc: 'Beat the first mega capsule',
    icon: 'capsule-gem', coins: 30,
    check: (d) => d.currentLevel >= 11,
  },
  {
    id: 'lv_20', name: 'Halfway Hole', desc: 'Reach level 20',
    icon: 'capsule-gem', coins: 50,
    check: (d) => d.currentLevel >= 20,
  },
  {
    id: 'stars_30', name: 'Star Collector', desc: 'Earn 30 stars',
    icon: 'star', coins: 60,
    check: (d) => sumStars(d) >= 30,
    progress: (d) => [Math.min(sumStars(d), 30), 30],
  },
  {
    id: 'fashion', name: 'Fresh Look', desc: 'Own 3 hole skins',
    icon: 'icon-cart', coins: 25,
    check: (d) => d.unlockedSkins.length >= 3,
    progress: (d) => [Math.min(d.unlockedSkins.length, 3), 3],
  },
];

/**
 * Unlock every newly-earned achievement, grant coins, queue toasts.
 * Returns the array of newly unlocked achievement defs.
 */
export function checkAchievements(scene) {
  const newly = [];
  for (const a of ACHIEVEMENTS) {
    if (!Save.hasAchievement(a.id) && a.check(Save.data)) {
      Save.unlockAchievement(a.id);
      Save.addCoins(a.coins);
      newly.push(a);
    }
  }
  newly.forEach((a, i) => {
    scene.time.delayedCall(i * 1500, () => {
      if (scene.scene.isActive()) showAchievementToast(scene, a);
    });
  });
  return newly;
}

/** Gold-badged toast sliding in from the top edge. */
export function showAchievementToast(scene, a) {
  Audio.achievement();
  const c = scene.add.container(360, -70).setDepth(650);
  const bg = scene.add.image(0, 0, 'pill').setDisplaySize(440, 94).setTint(0x1a1a2e).setAlpha(0.94);
  const badge = scene.add.image(-158, 0, 'disc').setDisplaySize(62, 62).setTint(0xffc93c);
  const ic = scene.add.image(-158, 0, a.icon).setDisplaySize(36, 36).setTint(0x1a1a2e);
  const name = mkText(scene, -112, -16, a.name.toUpperCase(), 26, '#FFFFFF', '700').setOrigin(0, 0.5);
  const coin = scene.add.image(-99, 20, 'coin').setDisplaySize(26, 26);
  const sub = mkText(scene, -80, 20, `+${a.coins}`, 22, '#FFD93D', '600').setOrigin(0, 0.5);
  c.add([bg, badge, ic, name, coin, sub]);
  scene.tweens.chain({
    targets: c,
    tweens: [
      { y: 96, duration: 320, ease: 'Back.easeOut' },
      { y: 96, duration: 1650 },
      { y: -84, duration: 260, ease: 'Back.easeIn', onComplete: () => c.destroy() },
    ],
  });
}
