/**
 * AdMob configuration - THE ONLY FILE YOU NEED TO EDIT with real IDs.
 *
 * Real Chroma Hole AdMob account is wired in below. TEST_MODE is off, so
 * these ad units will serve real (non-test) ads on a signed release build.
 *
 * If you ever need to go back to safe test ads (e.g. handing a debug build
 * to someone else, or testing on a new device before it's allow-listed),
 * flip ADMOB_TEST_MODE back to true - that also auto-swaps in Google's
 * public test ad unit IDs so you never accidentally serve real ads during
 * testing (which can get an AdMob account flagged for invalid traffic).
 */
export const ADMOB_TEST_MODE = false;

const REAL_AD_UNITS = {
  interstitial: 'ca-app-pub-8811964130270609/9430823956',
  rewarded: 'ca-app-pub-8811964130270609/8463181048',
};

const TEST_AD_UNITS = {
  interstitial: 'ca-app-pub-3940256099942544/1033173712',
  rewarded: 'ca-app-pub-3940256099942544/5224354917',
};

export const AD_UNITS = ADMOB_TEST_MODE ? TEST_AD_UNITS : REAL_AD_UNITS;

// Real AdMob App ID (ca-app-pub-8811964130270609~6411732770) lives in
// android/app/src/main/AndroidManifest.xml - Android reads it at
// manifest-merge time, not from JS.
