/**
 * NetworkManager - PHASE 2 SKELETON, intentionally non-functional.
 *
 * The realtime ".io" arena mode (multiple players on one map, bigger +
 * correctly-colored holes swallow rival holes) requires a dedicated backend:
 *   - Node.js + Socket.io or Colyseus for authoritative state
 *   - deployed separately (Render / Railway / Fly.io)
 *   - client-side prediction + snapshot interpolation for hole movement
 *
 * Nothing in the shipped game calls into this class. See README.md,
 * section "Phase 2 - .io Arena Mode".
 */
export default class NetworkManager {
  connect(/* url */) {
    console.warn('[Network] Phase 2 stub - no backend available.');
    return Promise.reject(new Error('Arena mode is not implemented (Phase 2).'));
  }

  disconnect() {}
  send(/* type, payload */) {}
  on(/* type, handler */) {}
}
