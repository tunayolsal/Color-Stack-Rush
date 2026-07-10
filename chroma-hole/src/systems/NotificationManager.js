/**
 * NotificationManager - local-only "come back" reminders (no server, no
 * Firebase). Uses @capacitor/local-notifications, which only does anything
 * on native builds; on web this is a silent no-op.
 *
 * Single reminder strategy: whenever the menu loads and today's daily gift
 * is still unclaimed, (re)schedule ONE notification for ~20 hours from now
 * nudging the player back for it. Any previously scheduled reminder is
 * cancelled first, so there's never more than one pending at a time.
 */
import { Capacitor } from '@capacitor/core';
import { LocalNotifications } from '@capacitor/local-notifications';

const IS_NATIVE = Capacitor.isNativePlatform();
const REMINDER_ID = 1001;

class NotificationManager {
  constructor() {
    this.permissionAsked = false;
  }

  async _ensurePermission() {
    if (!IS_NATIVE) return false;
    try {
      const cur = await LocalNotifications.checkPermissions();
      if (cur.display === 'granted') return true;
      if (this.permissionAsked) return false; // don't nag every menu visit
      this.permissionAsked = true;
      const res = await LocalNotifications.requestPermissions();
      return res.display === 'granted';
    } catch (e) {
      console.warn('[Notifications] permission check failed', e);
      return false;
    }
  }

  /** Call whenever the menu loads. Silently does nothing on web. */
  async scheduleDailyReminder() {
    if (!IS_NATIVE) {
      console.log('[Notifications] scheduleDailyReminder skipped (web build has no native notifications).');
      return;
    }
    try {
      await LocalNotifications.cancel({ notifications: [{ id: REMINDER_ID }] });
      const ok = await this._ensurePermission();
      if (!ok) return;
      await LocalNotifications.schedule({
        notifications: [{
          id: REMINDER_ID,
          title: 'Chroma Hole',
          body: 'Your daily gift is waiting! 🎁 Come grow your hole.',
          schedule: { at: new Date(Date.now() + 20 * 3600 * 1000) },
          smallIcon: 'ic_launcher',
        }],
      });
    } catch (e) {
      console.warn('[Notifications] scheduling failed', e);
    }
  }

  /** Call once the gift has been claimed - no need to remind again today. */
  async cancelDailyReminder() {
    if (!IS_NATIVE) return;
    try {
      await LocalNotifications.cancel({ notifications: [{ id: REMINDER_ID }] });
    } catch (e) {
      console.warn('[Notifications] cancel failed', e);
    }
  }
}

export default new NotificationManager();
