/**
 * ReviewManager - Google Play's in-app review prompt (no server, no account
 * needed; Play Console handles rate-limiting on its end too). Asked at most
 * ONCE per install, only after a genuinely good moment (a 3-star clear past
 * the early levels) so it doesn't interrupt someone who's struggling.
 */
import { Capacitor } from '@capacitor/core';
import { InAppReview } from '@capacitor-community/in-app-review';
import Save from './SaveManager.js';

const IS_NATIVE = Capacitor.isNativePlatform();

class ReviewManager {
  /** Call after a level clear; only actually prompts when conditions are met. */
  async maybeRequest({ level, stars }) {
    if (!Save.shouldAskReview()) return;
    if (level < 5 || stars < 3) return; // wait for a confident, happy moment
    Save.markReviewAsked(); // mark first so a crash/skip can't loop-retry forever
    if (!IS_NATIVE) {
      console.log('[Review] requestReview skipped (web build - Play Store review flow needs a device).');
      return;
    }
    try {
      await InAppReview.requestReview();
    } catch (e) {
      console.warn('[Review] requestReview failed', e);
    }
  }
}

export default new ReviewManager();
