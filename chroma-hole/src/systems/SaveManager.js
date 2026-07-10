/**
 * SaveManager - single source of truth for persistent player data.
 * Everything is stored as one JSON blob in localStorage and written
 * eagerly on every mutation (hypercasual sessions end abruptly).
 */
const KEY = 'chroma_hole_save_v1';

class SaveManager {
  constructor() {
    this.data = this.defaults();
    this.load();
  }

  defaults() {
    return {
      coins: 0,
      currentLevel: 1,          // next level to play
      stars: {},                // { "1": 3, "2": 2, ... } best stars per level
      unlockedSkins: ['void'],
      activeSkin: 'void',
      settings: { sound: true, music: true },
      sawDragHint: false,
      totalSwallowed: 0,
      // v1.1 meta-progression
      achievements: [],         // unlocked achievement ids
      bestCombo: 0,
      totalLevelsCompleted: 0,
      lifetimeCoins: 0,         // total ever earned (not reduced by spending)
      daily: { lastClaim: '', streak: 0 }, // lastClaim = 'YYYY-MM-DD' local
      // v1.2 additions
      bestScores: {},           // { "1": 340, ... } best score per level
      askedReview: false,       // in-app review prompt is shown at most once ever
    };
  }

  load() {
    try {
      const raw = localStorage.getItem(KEY);
      if (raw) {
        const parsed = JSON.parse(raw);
        this.data = { ...this.defaults(), ...parsed };
        this.data.settings = { ...this.defaults().settings, ...(parsed.settings || {}) };
        this.data.daily = { ...this.defaults().daily, ...(parsed.daily || {}) };
      }
    } catch (e) {
      console.warn('[Save] failed to load, using defaults', e);
      this.data = this.defaults();
    }
  }

  save() {
    try {
      localStorage.setItem(KEY, JSON.stringify(this.data));
    } catch (e) {
      console.warn('[Save] failed to persist', e);
    }
  }

  get coins() { return this.data.coins; }

  addCoins(n) {
    this.data.coins += n;
    if (n > 0) this.data.lifetimeCoins += n;
    this.save();
  }

  spendCoins(n) {
    if (this.data.coins < n) return false;
    this.data.coins -= n;
    this.save();
    return true;
  }

  completeLevel(level, stars, coins) {
    const key = String(level);
    this.data.stars[key] = Math.max(this.data.stars[key] || 0, stars);
    this.data.currentLevel = Math.max(this.data.currentLevel, level + 1);
    this.data.coins += coins;
    this.data.lifetimeCoins += coins;
    this.data.totalLevelsCompleted += 1;
    this.save();
  }

  starsFor(level) { return this.data.stars[String(level)] || 0; }
  totalStars() { return Object.values(this.data.stars).reduce((a, b) => a + b, 0); }

  bestScoreFor(level) { return this.data.bestScores[String(level)] || 0; }

  /** Records a level score; returns true if it's a new personal best. */
  recordScore(level, score) {
    const key = String(level);
    const prev = this.data.bestScores[key] || 0;
    const isNew = score > prev;
    if (isNew) {
      this.data.bestScores[key] = score;
      this.save();
    }
    return isNew;
  }

  /** In-app review is asked at most once, ever, on a genuinely good moment. */
  shouldAskReview() { return !this.data.askedReview; }

  markReviewAsked() {
    this.data.askedReview = true;
    this.save();
  }

  hasSkin(id) { return this.data.unlockedSkins.includes(id); }

  unlockSkin(id) {
    if (!this.hasSkin(id)) {
      this.data.unlockedSkins.push(id);
      this.save();
    }
  }

  setActiveSkin(id) {
    this.data.activeSkin = id;
    this.save();
  }

  setSetting(key, value) {
    this.data.settings[key] = value;
    this.save();
  }

  markDragHintSeen() {
    this.data.sawDragHint = true;
    this.save();
  }

  addSwallowed(n = 1) {
    this.data.totalSwallowed += n;
    // not save-critical every frame; persisted alongside other saves
  }

  /* ---------------- achievements ---------------- */

  hasAchievement(id) { return this.data.achievements.includes(id); }

  /** Returns true only if this call newly unlocked it. */
  unlockAchievement(id) {
    if (this.hasAchievement(id)) return false;
    this.data.achievements.push(id);
    this.save();
    return true;
  }

  recordCombo(n) {
    if (n > this.data.bestCombo) {
      this.data.bestCombo = n;
      this.save();
    }
  }

  /* ---------------- daily gift ---------------- */

  _dateStr(d) {
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  canClaimDaily() {
    return this.data.daily.lastClaim !== this._dateStr(new Date());
  }

  /** The streak value the NEXT claim would have (resets if a day was skipped). */
  dailyNextStreak() {
    if (!this.canClaimDaily()) return this.data.daily.streak;
    const yesterday = this._dateStr(new Date(Date.now() - 86400000));
    return this.data.daily.lastClaim === yesterday ? this.data.daily.streak + 1 : 1;
  }

  /** Coins the next (or today's already claimed) gift is worth. */
  dailyReward() {
    const streak = Math.max(1, this.dailyNextStreak());
    return Math.min(30 + (streak - 1) * 10, 100);
  }

  claimDaily() {
    if (!this.canClaimDaily()) return null;
    const streak = this.dailyNextStreak();
    const coins = Math.min(30 + (streak - 1) * 10, 100);
    this.data.daily = { lastClaim: this._dateStr(new Date()), streak };
    this.addCoins(coins); // also saves
    return { coins, streak };
  }

  reset() {
    this.data = this.defaults();
    this.save();
  }
}

export default new SaveManager();
