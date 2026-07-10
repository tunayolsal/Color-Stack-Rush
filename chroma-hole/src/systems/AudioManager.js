/**
 * AudioManager - all SFX and music are synthesized with the Web Audio API
 * (no copyrighted audio files). Respects the sound/music settings in
 * SaveManager and survives scene changes (it is a plain singleton, not a
 * Phaser object).
 *
 * TODO(AUDIO): to ship real produced music, drop an .mp3/.ogg in /public,
 * load it with an <audio> element or Phaser sound, and replace startMusic()
 * / stopMusic() below. Everything else can stay as-is.
 */
import Save from './SaveManager.js';

const NOTE = (midi) => 440 * Math.pow(2, (midi - 69) / 12);

class AudioManager {
  constructor() {
    this.ctx = null;
    this.master = null;
    this.sfxGain = null;
    this.musicGain = null;
    this.noiseBuffer = null;
    this.musicTimer = null;
    this.musicStep = 0;
    this.nextNoteTime = 0;
    this.wantMusic = true; // menus/game always want music; the setting gates it
  }

  get soundOn() { return Save.data.settings.sound; }
  get musicOn() { return Save.data.settings.music; }

  /** Create/resume the AudioContext. Must be triggered by a user gesture. */
  unlock() {
    if (!this.ctx) {
      const AC = window.AudioContext || window.webkitAudioContext;
      if (!AC) return;
      this.ctx = new AC();
      this.master = this.ctx.createGain();
      this.master.gain.value = 0.9;
      this.master.connect(this.ctx.destination);
      this.sfxGain = this.ctx.createGain();
      this.sfxGain.gain.value = 1;
      this.sfxGain.connect(this.master);
      this.musicGain = this.ctx.createGain();
      this.musicGain.gain.value = 0.8;
      this.musicGain.connect(this.master);
      // 1s of white noise reused by all noise-based SFX
      const len = this.ctx.sampleRate;
      this.noiseBuffer = this.ctx.createBuffer(1, len, this.ctx.sampleRate);
      const data = this.noiseBuffer.getChannelData(0);
      for (let i = 0; i < len; i++) data[i] = Math.random() * 2 - 1;
    }
    if (this.ctx.state === 'suspended') this.ctx.resume();
    if (this.wantMusic && this.musicOn && !this.musicTimer) this._beginMusic();
  }

  /* ---------------- low-level helpers ---------------- */

  _tone({ f0 = 440, f1 = null, type = 'sine', dur = 0.15, vol = 0.2, at = 0, dest = null }) {
    if (!this.ctx) return;
    const t0 = this.ctx.currentTime + at;
    const osc = this.ctx.createOscillator();
    const g = this.ctx.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(Math.max(20, f0), t0);
    if (f1 && f1 !== f0) osc.frequency.exponentialRampToValueAtTime(Math.max(20, f1), t0 + dur);
    g.gain.setValueAtTime(0.0001, t0);
    g.gain.exponentialRampToValueAtTime(vol, t0 + 0.008);
    g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
    osc.connect(g);
    g.connect(dest || this.sfxGain);
    osc.start(t0);
    osc.stop(t0 + dur + 0.05);
  }

  _noise({ dur = 0.3, vol = 0.2, f0 = 800, f1 = 2000, q = 1.2, type = 'bandpass', at = 0, dest = null }) {
    if (!this.ctx || !this.noiseBuffer) return;
    const t0 = this.ctx.currentTime + at;
    const src = this.ctx.createBufferSource();
    src.buffer = this.noiseBuffer;
    src.loop = true;
    const filter = this.ctx.createBiquadFilter();
    filter.type = type;
    filter.Q.value = q;
    filter.frequency.setValueAtTime(Math.max(40, f0), t0);
    if (f1 !== f0) filter.frequency.exponentialRampToValueAtTime(Math.max(40, f1), t0 + dur);
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(0.0001, t0);
    g.gain.exponentialRampToValueAtTime(vol, t0 + 0.01);
    g.gain.exponentialRampToValueAtTime(0.0001, t0 + dur);
    src.connect(filter); filter.connect(g); g.connect(dest || this.sfxGain);
    src.start(t0);
    src.stop(t0 + dur + 0.05);
  }

  _sfxAllowed() {
    if (!this.soundOn) return false;
    if (!this.ctx) return false; // not unlocked yet - silently skip
    return true;
  }

  /* ---------------- SFX ---------------- */

  click() {
    if (!this._sfxAllowed()) return;
    this._tone({ f0: 1500, f1: 1100, type: 'triangle', dur: 0.06, vol: 0.15 });
  }

  /**
   * Swallow "slurp/pop" - pitch depends on object size (0 small .. 2 large)
   * and rises with the current combo for an escalating feeding-frenzy feel.
   */
  pop(sizeIndex = 0, combo = 0) {
    if (!this._sfxAllowed()) return;
    let base = [620, 470, 340][sizeIndex] || 500;
    base *= 1 + Math.min(combo, 10) * 0.045;
    this._tone({ f0: base * 0.6, f1: base * 2.1, type: 'sine', dur: 0.13, vol: 0.28 });
    this._tone({ f0: base * 3.2, f1: base * 1.4, type: 'triangle', dur: 0.09, vol: 0.1, at: 0.015 });
    this._noise({ dur: 0.1, vol: 0.06, f0: 500, f1: 1800 });
  }

  /** Combo increment blip - rises roughly a semitone per combo step. */
  comboUp(n) {
    if (!this._sfxAllowed()) return;
    const f = 660 * Math.pow(1.059, Math.min(n, 14));
    this._tone({ f0: f, f1: f * 1.3, type: 'sine', dur: 0.09, vol: 0.13 });
    if (n % 5 === 0) {
      this._tone({ f0: f * 1.5, type: 'triangle', dur: 0.18, vol: 0.12, at: 0.05 });
    }
  }

  /** Achievement unlocked - short sparkly arpeggio. */
  achievement() {
    if (!this._sfxAllowed()) return;
    [659.25, 783.99, 987.77].forEach((f, i) => {
      this._tone({ f0: f, type: 'triangle', dur: 0.16, vol: 0.15, at: i * 0.09 });
    });
    this._noise({ dur: 0.3, vol: 0.04, f0: 3000, f1: 7000, type: 'highpass', at: 0.2 });
  }

  /** Daily gift claim - a happy two-note jingle. */
  gift() {
    if (!this._sfxAllowed()) return;
    this._tone({ f0: 587, f1: 880, type: 'triangle', dur: 0.16, vol: 0.16 });
    this._tone({ f0: 880, f1: 1174, type: 'triangle', dur: 0.22, vol: 0.14, at: 0.14 });
  }

  buzz() {
    if (!this._sfxAllowed()) return;
    this._tone({ f0: 95, type: 'square', dur: 0.18, vol: 0.2 });
    this._tone({ f0: 63, type: 'square', dur: 0.18, vol: 0.14 });
  }

  whoosh() {
    if (!this._sfxAllowed()) return;
    this._noise({ dur: 0.28, vol: 0.18, f0: 400, f1: 2600 });
    this._tone({ f0: 600, f1: 1150, type: 'sine', dur: 0.2, vol: 0.09 });
  }

  fanfare() {
    if (!this._sfxAllowed()) return;
    [523.25, 659.25, 783.99, 1046.5].forEach((f, i) => {
      this._tone({ f0: f, type: 'triangle', dur: 0.28, vol: 0.2, at: i * 0.12 });
    });
    this._tone({ f0: 1568, type: 'sine', dur: 0.45, vol: 0.12, at: 0.48 });
    this._noise({ dur: 0.5, vol: 0.05, f0: 2000, f1: 6000, at: 0.45, type: 'highpass' });
  }

  coin() {
    if (!this._sfxAllowed()) return;
    this._tone({ f0: 987, type: 'square', dur: 0.07, vol: 0.1 });
    this._tone({ f0: 1318, type: 'square', dur: 0.12, vol: 0.1, at: 0.08 });
  }

  /** Golden blob swallowed - a bright shimmering chime. */
  goldenChime() {
    if (!this._sfxAllowed()) return;
    [880, 1108.7, 1318.5, 1760].forEach((f, i) => {
      this._tone({ f0: f, type: 'triangle', dur: 0.2, vol: 0.14, at: i * 0.06 });
    });
    this._noise({ dur: 0.25, vol: 0.05, f0: 4000, f1: 8000, type: 'highpass' });
  }

  /** Shield picked up - a rising protective hum. */
  shieldUp() {
    if (!this._sfxAllowed()) return;
    this._tone({ f0: 300, f1: 700, type: 'sine', dur: 0.28, vol: 0.16 });
    this._tone({ f0: 450, f1: 900, type: 'triangle', dur: 0.2, vol: 0.1, at: 0.05 });
  }

  /** Shield absorbs a hit - a glassy shatter-block, distinct from buzz(). */
  shieldBlock() {
    if (!this._sfxAllowed()) return;
    this._tone({ f0: 1100, f1: 400, type: 'triangle', dur: 0.18, vol: 0.18 });
    this._noise({ dur: 0.15, vol: 0.12, f0: 2500, f1: 800 });
  }

  tick() {
    if (!this._sfxAllowed()) return;
    this._tone({ f0: 1250, type: 'sine', dur: 0.05, vol: 0.1 });
  }

  ready() {
    if (!this._sfxAllowed()) return;
    this._tone({ f0: 880, f1: 1320, type: 'sine', dur: 0.14, vol: 0.14 });
  }

  deny() {
    if (!this._sfxAllowed()) return;
    this._tone({ f0: 240, f1: 190, type: 'triangle', dur: 0.12, vol: 0.12 });
  }

  collapse() {
    if (!this._sfxAllowed()) return;
    this._tone({ f0: 380, f1: 65, type: 'sawtooth', dur: 0.5, vol: 0.2 });
    this._noise({ dur: 0.5, vol: 0.12, f0: 1200, f1: 150 });
  }

  star() {
    if (!this._sfxAllowed()) return;
    this._tone({ f0: 1318, f1: 1760, type: 'sine', dur: 0.12, vol: 0.14 });
  }

  /* ---------------- music ---------------- */
  // A cheerful 4-bar C-major-pentatonic loop, scheduled with a lookahead
  // step sequencer (8th notes at ~118 bpm).

  startMusic() {
    this.wantMusic = true;
    if (this.ctx && this.musicOn && !this.musicTimer) this._beginMusic();
  }

  stopMusic() {
    this.wantMusic = false;
    this._haltMusic();
  }

  _beginMusic() {
    const stepDur = 60 / 118 / 2;
    // 32 steps = 4 bars of 8th notes; null = rest (midi numbers)
    this.melody = [
      72, null, 76, null, 79, 76, null, 74,
      72, null, 76, 79, null, 81, 79, null,
      84, null, 81, 79, 81, null, 79, 76,
      74, 76, null, 72, null, null, 67, null,
    ];
    this.bass = [48, 48, 45, 45, 53, 53, 55, 55]; // one note per half bar
    this.musicStep = 0;
    this.nextNoteTime = this.ctx.currentTime + 0.1;
    this.musicTimer = setInterval(() => this._schedule(stepDur), 90);
  }

  _haltMusic() {
    if (this.musicTimer) {
      clearInterval(this.musicTimer);
      this.musicTimer = null;
    }
  }

  _schedule(stepDur) {
    if (!this.ctx || !this.musicOn) { this._haltMusic(); return; }
    while (this.nextNoteTime < this.ctx.currentTime + 0.35) {
      const s = this.musicStep % 32;
      const at = this.nextNoteTime - this.ctx.currentTime;
      const m = this.melody[s];
      if (m !== null && m !== undefined) {
        this._tone({ f0: NOTE(m), type: 'triangle', dur: 0.2, vol: 0.05, at, dest: this.musicGain });
      }
      if (s % 4 === 0) {
        const b = this.bass[Math.floor(s / 4) % this.bass.length];
        this._tone({ f0: NOTE(b), type: 'sine', dur: 0.42, vol: 0.07, at, dest: this.musicGain });
      }
      if (s % 2 === 1) {
        this._noise({ dur: 0.04, vol: 0.015, f0: 6000, f1: 8000, type: 'highpass', at, dest: this.musicGain });
      }
      this.nextNoteTime += stepDur;
      this.musicStep += 1;
    }
  }

  /* ---------------- settings ---------------- */

  setSound(on) {
    Save.setSetting('sound', on);
    if (on) this.click();
  }

  setMusic(on) {
    Save.setSetting('music', on);
    if (on) {
      if (this.ctx && this.wantMusic && !this.musicTimer) this._beginMusic();
    } else {
      this._haltMusic();
    }
  }
}

export default new AudioManager();
