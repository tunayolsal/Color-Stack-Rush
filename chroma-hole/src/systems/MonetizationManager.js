/**
 * MonetizationManager - real AdMob integration (native builds) with an
 * automatic console-log stub fallback (web/dev preview, where the native
 * AdMob plugin has nothing to talk to).
 *
 * Ad unit IDs live in src/adConfig.js - that is the ONLY file you need to
 * touch to go from Google's test ads to your real AdMob account. See the
 * TODO(ADMOB) comments there and in AndroidManifest.xml.
 *
 * IAP (purchaseProduct/restorePurchases) is still a stub - integrate
 * RevenueCat / Google Play Billing separately when you add real purchases.
 */
import { Capacitor } from '@capacitor/core';
import { AdMob } from '@capacitor-community/admob';
import { ADMOB_TEST_MODE, AD_UNITS } from '../adConfig.js';

const IS_NATIVE = Capacitor.isNativePlatform();

class MonetizationManager {
  constructor() {
    this.levelsSinceInterstitial = 0;
    this.interstitialEvery = 3; // show an interstitial every 3rd level completion
    this.initialized = false;
    this.interstitialLoading = false;
    this.rewardedLoading = false;
    if (IS_NATIVE) this._init();
  }

  async _init() {
    try {
      await AdMob.initialize({
        testingDevices: [],
        initializeForTesting: ADMOB_TEST_MODE,
      });
      this.initialized = true;
      this._preloadInterstitial();
      this._preloadRewarded();
    } catch (e) {
      console.warn('[Monetization] AdMob init failed, ads disabled this session.', e);
    }
  }

  async _preloadInterstitial() {
    if (!IS_NATIVE || this.interstitialLoading) return;
    this.interstitialLoading = true;
    try {
      await AdMob.prepareInterstitial({ adId: AD_UNITS.interstitial });
    } catch (e) {
      console.warn('[Monetization] Interstitial preload failed', e);
    } finally {
      this.interstitialLoading = false;
    }
  }

  async _preloadRewarded() {
    if (!IS_NATIVE || this.rewardedLoading) return;
    this.rewardedLoading = true;
    try {
      await AdMob.prepareRewardVideoAd({ adId: AD_UNITS.rewarded });
    } catch (e) {
      console.warn('[Monetization] Rewarded preload failed', e);
    } finally {
      this.rewardedLoading = false;
    }
  }

  /** Fire-and-forget fullscreen ad. Calls onDone() when the ad is closed (or fails/skipped). */
  async showInterstitialAd(onDone) {
    if (!IS_NATIVE) {
      console.log('[Monetization] Interstitial ad requested (web stub - AdMob only runs on device).');
      setTimeout(() => { if (onDone) onDone(); }, 300);
      return;
    }
    try {
      await AdMob.showInterstitial();
    } catch (e) {
      console.warn('[Monetization] Interstitial show failed (likely not loaded yet)', e);
    } finally {
      if (onDone) onDone();
      this._preloadInterstitial(); // warm up the next one
    }
  }

  /**
   * Counts level completions and only actually shows an interstitial every
   * `interstitialEvery` completions. Always calls onDone().
   * `justCompletedLevel` gates a "no ads yet" grace period for brand-new
   * players (first 5 levels) so the very first session isn't interrupted.
   */
  maybeShowInterstitial(onDone, justCompletedLevel = 99) {
    if (justCompletedLevel < 5) {
      if (onDone) onDone();
      return;
    }
    this.levelsSinceInterstitial += 1;
    if (this.levelsSinceInterstitial >= this.interstitialEvery) {
      this.levelsSinceInterstitial = 0;
      this.showInterstitialAd(onDone);
    } else if (onDone) {
      onDone();
    }
  }

  /** Rewarded ad. onComplete(true) if the user earned the reward. */
  async showRewardedAd(onComplete) {
    if (!IS_NATIVE) {
      console.log('[Monetization] Rewarded ad requested (web stub - AdMob only runs on device) -> auto-granting reward.');
      setTimeout(() => { if (onComplete) onComplete(true); }, 500);
      return;
    }
    let earned = false;
    try {
      // showRewardVideoAd() resolves with the reward item iff the user
      // watched to completion; it rejects/never resolves on skip or failure.
      const reward = await AdMob.showRewardVideoAd();
      earned = !!reward;
    } catch (e) {
      console.warn('[Monetization] Rewarded show failed (likely not loaded, or skipped)', e);
    } finally {
      if (onComplete) onComplete(earned);
      this._preloadRewarded(); // warm up the next one
    }
  }

  /** IAP purchase. Resolves { success, productId }. */
  purchaseProduct(productId) {
    // TODO(IAP): integrate RevenueCat / StoreKit / Google Play Billing via Capacitor.
    console.log(`[Monetization] purchaseProduct("${productId}") requested (stub) -> auto-success. TODO: plug real IAP here.`);
    return new Promise((resolve) => {
      setTimeout(() => resolve({ success: true, productId }), 500);
    });
  }

  /** Restore purchases stub. Resolves { restored: [] }. */
  restorePurchases() {
    // TODO(IAP): query the store for owned non-consumables and re-unlock them.
    console.log('[Monetization] restorePurchases() requested (stub) -> nothing to restore.');
    return Promise.resolve({ restored: [] });
  }
}

export default new MonetizationManager();
