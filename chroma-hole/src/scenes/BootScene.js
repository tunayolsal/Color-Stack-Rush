/**
 * BootScene - builds EVERY texture in the game procedurally (canvas 2D
 * gradients + Phaser Graphics; zero external image files), shows the splash
 * logo, waits for the Google Font, then auto-advances to the menu.
 */
import Phaser from 'phaser';
import { GAME_W, GAME_H, COLORS, PALETTE, INK, CREAM, FONT } from '../constants.js';

export default class BootScene extends Phaser.Scene {
  constructor() { super('BootScene'); }

  create() {
    this.buildTextures();
    this.showSplash();
  }

  /* ================= splash ================= */

  showSplash() {
    this.add.rectangle(GAME_W / 2, GAME_H / 2, GAME_W, GAME_H, INK);

    // logo: a mini hole built from the freshly generated textures
    const cx = GAME_W / 2, cy = 520;
    const glow = this.add.image(cx, cy, 'glow').setTint(COLORS.red).setAlpha(0.55).setScale(1.7);
    this.add.image(cx, cy, 'rim').setTint(COLORS.red).setScale(1.06);
    this.add.image(cx, cy, 'hole-core');
    const swirl = this.add.image(cx, cy, 'swirl').setTint(COLORS.red).setAlpha(0.5).setScale(0.8);
    this.tweens.add({ targets: swirl, rotation: Math.PI * 2, duration: 3000, repeat: -1 });

    // cycle the logo color through the palette
    let ci = 0;
    const rim = this.children.list.find((c) => c.texture && c.texture.key === 'rim');
    this.time.addEvent({
      delay: 420, repeat: -1,
      callback: () => {
        ci = (ci + 1) % PALETTE.length;
        rim.setTint(PALETTE[ci]);
        glow.setTint(PALETTE[ci]);
        swirl.setTint(PALETTE[ci]);
      },
    });

    const mkTitle = () => {
      const letters = 'CHROMA';
      const colors = [0xffffff, 0xffffff, 0xffffff, 0xffffff, 0xffffff, 0xffffff];
      letters.split('').forEach((ch, i) => {
        this.add.text(GAME_W / 2 - 150 + i * 60, 760, ch, {
          fontFamily: FONT, fontSize: '72px', fontStyle: '700',
          color: '#F7F3E9',
        }).setOrigin(0.5).setResolution(2).setTint(colors[i]);
      });
      'HOLE'.split('').forEach((ch, i) => {
        this.add.text(GAME_W / 2 - 105 + i * 70, 850, ch, {
          fontFamily: FONT, fontSize: '84px', fontStyle: '700',
          color: '#FFFFFF',
        }).setOrigin(0.5).setResolution(2).setTint(PALETTE[i % PALETTE.length]);
      });
    };

    // Wait for the webfont (with a hard timeout) before drawing text,
    // then hop to the menu.
    const fontsReady = (document.fonts && document.fonts.load)
      ? Promise.all([
        document.fonts.load('700 84px Fredoka'),
        document.fonts.load('600 40px Fredoka'),
        document.fonts.load('400 32px Fredoka'),
      ]).catch(() => {})
      : Promise.resolve();

    Promise.race([fontsReady, new Promise((r) => setTimeout(r, 2200))]).then(() => {
      if (!this.scene.isActive('BootScene')) return;
      mkTitle();
      this.time.delayedCall(1100, () => {
        this.cameras.main.fadeOut(260, 26, 26, 46);
        this.time.delayedCall(280, () => this.scene.start('MenuScene'));
      });
    });
  }

  /* ================= texture factory ================= */

  buildTextures() {
    const t = this.textures;
    const canvasTex = (key, size, draw) => {
      if (t.exists(key)) return;
      const c = document.createElement('canvas');
      c.width = size; c.height = size;
      draw(c.getContext('2d'), size);
      t.addCanvas(key, c);
    };
    const g = this.make.graphics({ x: 0, y: 0, add: false });
    const gfxTex = (key, w, h, draw) => {
      if (t.exists(key)) return;
      g.clear();
      draw(g);
      g.generateTexture(key, w, h);
    };

    /* --- soft radial glow (white, tintable) --- */
    canvasTex('glow', 256, (ctx, s) => {
      const grd = ctx.createRadialGradient(s / 2, s / 2, 0, s / 2, s / 2, s / 2);
      grd.addColorStop(0, 'rgba(255,255,255,1)');
      grd.addColorStop(0.5, 'rgba(255,255,255,0.35)');
      grd.addColorStop(1, 'rgba(255,255,255,0)');
      ctx.fillStyle = grd;
      ctx.fillRect(0, 0, s, s);
    });

    /* --- hole core: dark void disc with a soft edge --- */
    canvasTex('hole-core', 256, (ctx, s) => {
      const grd = ctx.createRadialGradient(s / 2, s / 2, 0, s / 2, s / 2, s / 2);
      grd.addColorStop(0, '#1A1A2E');
      grd.addColorStop(0.8, '#1A1A2E');
      grd.addColorStop(0.94, 'rgba(26,26,46,0.85)');
      grd.addColorStop(1, 'rgba(26,26,46,0)');
      ctx.fillStyle = grd;
      ctx.fillRect(0, 0, s, s);
    });

    /* --- rim: annulus gradient (dark center -> colored edge feel when tinted) --- */
    canvasTex('rim', 256, (ctx, s) => {
      const grd = ctx.createRadialGradient(s / 2, s / 2, 0, s / 2, s / 2, s / 2);
      grd.addColorStop(0.55, 'rgba(255,255,255,0)');
      grd.addColorStop(0.74, 'rgba(255,255,255,0.9)');
      grd.addColorStop(0.86, 'rgba(255,255,255,0.95)');
      grd.addColorStop(1, 'rgba(255,255,255,0)');
      ctx.fillStyle = grd;
      ctx.fillRect(0, 0, s, s);
    });

    /* --- swirl: two-arm spiral, rotated at runtime inside the core --- */
    canvasTex('swirl', 256, (ctx, s) => {
      ctx.strokeStyle = 'rgba(255,255,255,0.85)';
      ctx.lineWidth = 9;
      ctx.lineCap = 'round';
      for (const arm of [0, Math.PI]) {
        ctx.beginPath();
        let first = true;
        for (let a = 0; a < Math.PI * 2.2; a += 0.07) {
          const r = 10 + (a / (Math.PI * 2.2)) * 108;
          const x = s / 2 + Math.cos(a + arm) * r;
          const y = s / 2 + Math.sin(a + arm) * r;
          if (first) { ctx.moveTo(x, y); first = false; } else ctx.lineTo(x, y);
        }
        ctx.stroke();
      }
    });

    /* --- basic shapes --- */
    gfxTex('disc', 256, 256, (gr) => { gr.fillStyle(0xffffff, 1); gr.fillCircle(128, 128, 128); });
    gfxTex('ring', 256, 256, (gr) => { gr.lineStyle(16, 0xffffff, 1); gr.strokeCircle(128, 128, 118); });
    gfxTex('ripple', 256, 256, (gr) => { gr.lineStyle(10, 0xffffff, 1); gr.strokeCircle(128, 128, 118); });

    canvasTex('particle', 48, (ctx, s) => {
      const grd = ctx.createRadialGradient(s / 2, s / 2, 0, s / 2, s / 2, s / 2);
      grd.addColorStop(0, 'rgba(255,255,255,1)');
      grd.addColorStop(0.45, 'rgba(255,255,255,0.9)');
      grd.addColorStop(1, 'rgba(255,255,255,0)');
      ctx.fillStyle = grd;
      ctx.fillRect(0, 0, s, s);
    });

    canvasTex('spark', 64, (ctx) => {
      ctx.fillStyle = '#FFFFFF';
      ctx.beginPath();
      ctx.moveTo(32, 2); ctx.lineTo(38, 26); ctx.lineTo(62, 32); ctx.lineTo(38, 38);
      ctx.lineTo(32, 62); ctx.lineTo(26, 38); ctx.lineTo(2, 32); ctx.lineTo(26, 26);
      ctx.closePath(); ctx.fill();
    });

    canvasTex('shard', 48, (ctx) => {
      ctx.fillStyle = '#FFFFFF';
      ctx.beginPath();
      ctx.moveTo(8, 40); ctx.lineTo(22, 6); ctx.lineTo(40, 34);
      ctx.closePath(); ctx.fill();
    });

    /* --- blob shapes (abstract, white, tinted at runtime; radius ~40) --- */
    canvasTex('blob-round', 96, (ctx, s) => {
      const grd = ctx.createRadialGradient(s / 2, s / 2, 5, s / 2, s / 2, 40);
      grd.addColorStop(0, '#FFFFFF');
      grd.addColorStop(0.82, '#FFFFFF');
      grd.addColorStop(1, '#E3E3E3');
      ctx.fillStyle = grd;
      ctx.beginPath(); ctx.arc(s / 2, s / 2, 40, 0, Math.PI * 2); ctx.fill();
    });

    canvasTex('blob-blobby', 96, (ctx, s) => {
      const cx = s / 2, cy = s / 2, N = 10;
      const pts = [];
      for (let i = 0; i < N; i++) {
        const a = (i / N) * Math.PI * 2;
        const r = 38 * (1 + 0.16 * Math.sin(i * 3 + 1.7));
        pts.push([cx + Math.cos(a) * r, cy + Math.sin(a) * r]);
      }
      ctx.fillStyle = '#FFFFFF';
      ctx.beginPath();
      for (let i = 0; i < N; i++) {
        const [x1, y1] = pts[i];
        const [x2, y2] = pts[(i + 1) % N];
        const mx = (x1 + x2) / 2, my = (y1 + y2) / 2;
        if (i === 0) ctx.moveTo(mx, my);
        else ctx.quadraticCurveTo(x1, y1, mx, my);
        if (i === N - 1) ctx.quadraticCurveTo(x2, y2, (pts[0][0] + x2) / 2, (pts[0][1] + y2) / 2);
      }
      ctx.closePath(); ctx.fill();
    });

    canvasTex('blob-crystal', 96, (ctx, s) => {
      const cx = s / 2, cy = s / 2, N = 6;
      ctx.fillStyle = '#FFFFFF';
      ctx.beginPath();
      for (let i = 0; i < N; i++) {
        const a = (i / N) * Math.PI * 2 - Math.PI / 2;
        const r = i % 2 === 0 ? 42 : 32;
        const x = cx + Math.cos(a) * r, y = cy + Math.sin(a) * r;
        if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
      }
      ctx.closePath(); ctx.fill();
      // baked facet lines (dark, unaffected by tint direction)
      ctx.strokeStyle = 'rgba(0,0,0,0.12)';
      ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(cx, cy - 42); ctx.lineTo(cx, cy + 32);
      ctx.moveTo(cx - 30, cy - 12); ctx.lineTo(cx + 30, cy - 12); ctx.stroke();
    });

    /* --- goal capsule bodies (radius ~120 in 288px canvas) --- */
    canvasTex('capsule-hex', 288, (ctx, s) => {
      const cx = s / 2, cy = s / 2, R = 118, cr = 26;
      const pts = [];
      for (let i = 0; i < 6; i++) {
        const a = (i / 6) * Math.PI * 2 - Math.PI / 2;
        pts.push([cx + Math.cos(a) * R, cy + Math.sin(a) * R]);
      }
      ctx.fillStyle = '#FFFFFF';
      ctx.beginPath();
      for (let i = 0; i < 6; i++) {
        const [x1, y1] = pts[i];
        const [x2, y2] = pts[(i + 1) % 6];
        if (i === 0) ctx.moveTo((x1 + pts[5][0]) / 2, (y1 + pts[5][1]) / 2);
        ctx.arcTo(x1, y1, (x1 + x2) / 2, (y1 + y2) / 2, cr);
        ctx.lineTo((x1 + x2) / 2, (y1 + y2) / 2);
      }
      ctx.closePath(); ctx.fill();
      // inner inset shading
      ctx.fillStyle = 'rgba(0,0,0,0.1)';
      ctx.beginPath();
      for (let i = 0; i < 6; i++) {
        const a = (i / 6) * Math.PI * 2 - Math.PI / 2;
        const x = cx + Math.cos(a) * R * 0.62, y = cy + Math.sin(a) * R * 0.62;
        if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
      }
      ctx.closePath(); ctx.fill();
    });

    canvasTex('capsule-gem', 288, (ctx, s) => {
      const cx = s / 2;
      ctx.fillStyle = '#FFFFFF';
      ctx.beginPath();
      ctx.moveTo(cx - 62, 62); ctx.lineTo(cx + 62, 62);
      ctx.lineTo(cx + 112, 128); ctx.lineTo(cx, 250); ctx.lineTo(cx - 112, 128);
      ctx.closePath(); ctx.fill();
      ctx.strokeStyle = 'rgba(0,0,0,0.12)';
      ctx.lineWidth = 5;
      ctx.beginPath();
      ctx.moveTo(cx - 112, 128); ctx.lineTo(cx + 112, 128);
      ctx.moveTo(cx - 62, 62); ctx.lineTo(cx - 36, 128); ctx.lineTo(cx, 250);
      ctx.moveTo(cx + 62, 62); ctx.lineTo(cx + 36, 128); ctx.lineTo(cx, 250);
      ctx.stroke();
    });

    /* --- decorative rings --- */
    canvasTex('deco-ring', 288, (ctx, s) => {
      ctx.strokeStyle = '#FFFFFF';
      ctx.lineWidth = 10;
      ctx.lineCap = 'round';
      const R = 126;
      for (let i = 0; i < 12; i++) {
        const a0 = (i / 12) * Math.PI * 2;
        ctx.beginPath();
        ctx.arc(s / 2, s / 2, R, a0, a0 + Math.PI / 18);
        ctx.stroke();
      }
    });

    canvasTex('pattern-dots', 288, (ctx, s) => {
      ctx.fillStyle = '#FFFFFF';
      const R = 126;
      for (let i = 0; i < 10; i++) {
        const a = (i / 10) * Math.PI * 2;
        ctx.beginPath();
        ctx.arc(s / 2 + Math.cos(a) * R, s / 2 + Math.sin(a) * R, 9, 0, Math.PI * 2);
        ctx.fill();
      }
    });

    /* --- star --- */
    canvasTex('star', 128, (ctx, s) => {
      const cx = s / 2, cy = s / 2;
      ctx.fillStyle = '#FFFFFF';
      ctx.beginPath();
      for (let i = 0; i < 10; i++) {
        const a = (i / 10) * Math.PI * 2 - Math.PI / 2;
        const r = i % 2 === 0 ? 58 : 26;
        const x = cx + Math.cos(a) * r, y = cy + Math.sin(a) * r;
        if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
      }
      ctx.closePath(); ctx.fill();
    });

    /* --- coin (pre-colored, not tinted) --- */
    canvasTex('coin', 96, (ctx, s) => {
      const cx = s / 2;
      ctx.fillStyle = '#FFD93D';
      ctx.beginPath(); ctx.arc(cx, cx, 42, 0, Math.PI * 2); ctx.fill();
      ctx.strokeStyle = '#E0A82E';
      ctx.lineWidth = 8;
      ctx.beginPath(); ctx.arc(cx, cx, 32, 0, Math.PI * 2); ctx.stroke();
      ctx.fillStyle = '#FFE98A';
      ctx.beginPath(); ctx.arc(cx - 12, cx - 14, 9, 0, Math.PI * 2); ctx.fill();
    });

    /* --- UI panels / buttons (white, tinted at use site) --- */
    gfxTex('btn-lg', 460, 130, (gr) => { gr.fillStyle(0xffffff, 1); gr.fillRoundedRect(0, 0, 460, 130, 40); });
    gfxTex('btn-sq', 120, 120, (gr) => { gr.fillStyle(0xffffff, 1); gr.fillRoundedRect(0, 0, 120, 120, 32); });
    gfxTex('pill', 260, 76, (gr) => { gr.fillStyle(0xffffff, 1); gr.fillRoundedRect(0, 0, 260, 76, 38); });
    gfxTex('panel', 560, 700, (gr) => { gr.fillStyle(0xffffff, 1); gr.fillRoundedRect(0, 0, 560, 700, 44); });
    gfxTex('card', 208, 230, (gr) => { gr.fillStyle(0xffffff, 1); gr.fillRoundedRect(0, 0, 208, 230, 26); });

    /* --- icons (96px, white strokes/fills, tinted at use site) --- */
    const icon = (key, draw) => canvasTex(key, 96, (ctx) => {
      ctx.strokeStyle = '#FFFFFF';
      ctx.fillStyle = '#FFFFFF';
      ctx.lineWidth = 10;
      ctx.lineCap = 'round';
      ctx.lineJoin = 'round';
      draw(ctx);
    });

    icon('icon-play', (ctx) => {
      ctx.beginPath(); ctx.moveTo(32, 22); ctx.lineTo(78, 48); ctx.lineTo(32, 74);
      ctx.closePath(); ctx.fill();
    });
    icon('icon-pause', (ctx) => {
      ctx.fillRect(28, 24, 15, 48);
      ctx.fillRect(56, 24, 15, 48);
    });
    icon('icon-gear', (ctx) => {
      for (let i = 0; i < 8; i++) {
        ctx.save();
        ctx.translate(48, 48);
        ctx.rotate((i * Math.PI) / 4);
        ctx.fillRect(-7, -42, 14, 18);
        ctx.restore();
      }
      ctx.beginPath(); ctx.arc(48, 48, 27, 0, Math.PI * 2); ctx.fill();
      ctx.globalCompositeOperation = 'destination-out';
      ctx.beginPath(); ctx.arc(48, 48, 12, 0, Math.PI * 2); ctx.fill();
      ctx.globalCompositeOperation = 'source-over';
    });
    icon('icon-cart', (ctx) => {
      ctx.beginPath();
      ctx.moveTo(14, 22); ctx.lineTo(28, 22); ctx.lineTo(38, 56); ctx.lineTo(72, 56);
      ctx.lineTo(80, 32); ctx.lineTo(32, 32);
      ctx.stroke();
      ctx.beginPath(); ctx.arc(42, 72, 8, 0, Math.PI * 2); ctx.fill();
      ctx.beginPath(); ctx.arc(68, 72, 8, 0, Math.PI * 2); ctx.fill();
    });
    icon('icon-home', (ctx) => {
      ctx.beginPath(); ctx.moveTo(48, 18); ctx.lineTo(18, 46); ctx.lineTo(78, 46);
      ctx.closePath(); ctx.fill();
      ctx.fillRect(27, 46, 42, 30);
    });
    icon('icon-retry', (ctx) => {
      ctx.beginPath(); ctx.arc(48, 50, 25, -0.4, Math.PI * 1.55); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(80, 34); ctx.lineTo(70, 52); ctx.lineTo(56, 36);
      ctx.closePath(); ctx.fill();
    });
    icon('icon-sound', (ctx) => {
      ctx.beginPath();
      ctx.moveTo(20, 38); ctx.lineTo(36, 38); ctx.lineTo(54, 22); ctx.lineTo(54, 74);
      ctx.lineTo(36, 58); ctx.lineTo(20, 58);
      ctx.closePath(); ctx.fill();
      ctx.beginPath(); ctx.arc(56, 48, 14, -0.9, 0.9); ctx.stroke();
      ctx.beginPath(); ctx.arc(56, 48, 24, -0.9, 0.9); ctx.stroke();
    });
    icon('icon-mute', (ctx) => {
      ctx.beginPath();
      ctx.moveTo(16, 38); ctx.lineTo(32, 38); ctx.lineTo(50, 22); ctx.lineTo(50, 74);
      ctx.lineTo(32, 58); ctx.lineTo(16, 58);
      ctx.closePath(); ctx.fill();
      ctx.beginPath(); ctx.moveTo(62, 38); ctx.lineTo(82, 58); ctx.moveTo(82, 38); ctx.lineTo(62, 58); ctx.stroke();
    });
    icon('icon-music', (ctx) => {
      ctx.beginPath(); ctx.ellipse(34, 68, 12, 9, -0.3, 0, Math.PI * 2); ctx.fill();
      ctx.beginPath(); ctx.ellipse(68, 60, 12, 9, -0.3, 0, Math.PI * 2); ctx.fill();
      ctx.lineWidth = 8;
      ctx.beginPath(); ctx.moveTo(44, 66); ctx.lineTo(44, 24); ctx.lineTo(78, 18); ctx.lineTo(78, 58); ctx.stroke();
    });
    icon('icon-ad', (ctx) => {
      ctx.lineWidth = 8;
      ctx.strokeRect(14, 28, 68, 44);
      ctx.beginPath(); ctx.moveTo(42, 40); ctx.lineTo(62, 50); ctx.lineTo(42, 60);
      ctx.closePath(); ctx.fill();
    });
    icon('icon-lock', (ctx) => {
      ctx.fillRect(26, 44, 44, 32);
      ctx.lineWidth = 9;
      ctx.beginPath(); ctx.arc(48, 44, 15, Math.PI, Math.PI * 2); ctx.stroke();
    });
    icon('icon-check', (ctx) => {
      ctx.lineWidth = 13;
      ctx.beginPath(); ctx.moveTo(22, 50); ctx.lineTo(42, 68); ctx.lineTo(76, 28); ctx.stroke();
    });
    icon('icon-close', (ctx) => {
      ctx.lineWidth = 13;
      ctx.beginPath(); ctx.moveTo(28, 28); ctx.lineTo(68, 68); ctx.moveTo(68, 28); ctx.lineTo(28, 68); ctx.stroke();
    });
    icon('icon-clock', (ctx) => {
      ctx.lineWidth = 9;
      ctx.beginPath(); ctx.arc(48, 48, 30, 0, Math.PI * 2); ctx.stroke();
      ctx.lineWidth = 8;
      ctx.beginPath(); ctx.moveTo(48, 48); ctx.lineTo(48, 28); ctx.moveTo(48, 48); ctx.lineTo(62, 56); ctx.stroke();
    });
    icon('icon-arrow', (ctx) => {
      ctx.lineWidth = 12;
      ctx.beginPath(); ctx.moveTo(48, 74); ctx.lineTo(48, 30); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(30, 44); ctx.lineTo(48, 24); ctx.lineTo(66, 44); ctx.stroke();
    });
    icon('icon-crown', (ctx) => {
      ctx.beginPath();
      ctx.moveTo(20, 68); ctx.lineTo(18, 34); ctx.lineTo(36, 50); ctx.lineTo(48, 26);
      ctx.lineTo(60, 50); ctx.lineTo(78, 34); ctx.lineTo(76, 68);
      ctx.closePath(); ctx.fill();
    });
    icon('icon-trophy', (ctx) => {
      ctx.beginPath();
      ctx.moveTo(28, 20); ctx.lineTo(68, 20); ctx.lineTo(64, 50);
      ctx.quadraticCurveTo(60, 62, 48, 62);
      ctx.quadraticCurveTo(36, 62, 32, 50);
      ctx.closePath(); ctx.fill();
      ctx.lineWidth = 7;
      ctx.beginPath(); ctx.arc(25, 31, 10, Math.PI * 0.5, Math.PI * 1.5, false); ctx.stroke();
      ctx.beginPath(); ctx.arc(71, 31, 10, -Math.PI * 0.5, Math.PI * 0.5, false); ctx.stroke();
      ctx.fillRect(43, 60, 10, 10);
      ctx.fillRect(31, 70, 34, 8);
    });
    icon('icon-gift', (ctx) => {
      ctx.fillRect(24, 42, 48, 34);   // box
      ctx.fillRect(20, 30, 56, 12);   // lid
      ctx.lineWidth = 7;
      ctx.beginPath(); ctx.arc(39, 21, 8, 0, Math.PI * 2); ctx.stroke(); // bow loops
      ctx.beginPath(); ctx.arc(57, 21, 8, 0, Math.PI * 2); ctx.stroke();
      // punch the ribbon slot so the tinted background shows through
      ctx.globalCompositeOperation = 'destination-out';
      ctx.fillRect(44, 30, 8, 46);
      ctx.globalCompositeOperation = 'source-over';
    });
    icon('icon-share', (ctx) => {
      const nodes = [[70, 24], [70, 72], [26, 48]];
      ctx.lineWidth = 7;
      ctx.beginPath();
      ctx.moveTo(nodes[2][0], nodes[2][1]); ctx.lineTo(nodes[0][0], nodes[0][1]);
      ctx.moveTo(nodes[2][0], nodes[2][1]); ctx.lineTo(nodes[1][0], nodes[1][1]);
      ctx.stroke();
      nodes.forEach(([x, y]) => { ctx.beginPath(); ctx.arc(x, y, 11, 0, Math.PI * 2); ctx.fill(); });
    });
    icon('icon-shield', (ctx) => {
      ctx.beginPath();
      ctx.moveTo(48, 14);
      ctx.lineTo(80, 26);
      ctx.lineTo(80, 50);
      ctx.quadraticCurveTo(80, 76, 48, 88);
      ctx.quadraticCurveTo(16, 76, 16, 50);
      ctx.lineTo(16, 26);
      ctx.closePath();
      ctx.fill();
      ctx.globalCompositeOperation = 'destination-out';
      ctx.lineWidth = 9;
      ctx.beginPath(); ctx.moveTo(32, 48); ctx.lineTo(44, 60); ctx.lineTo(66, 34); ctx.stroke();
      ctx.globalCompositeOperation = 'source-over';
    });

    /* --- spike hazard: white thorn ball, tinted ink at use site --- */
    canvasTex('spike', 96, (ctx, s) => {
      const cx = s / 2, cy = s / 2, N = 10;
      ctx.fillStyle = '#FFFFFF';
      ctx.beginPath();
      for (let i = 0; i < N * 2; i++) {
        const a = (i / (N * 2)) * Math.PI * 2 - Math.PI / 2;
        const r = i % 2 === 0 ? 44 : 22;
        const x = cx + Math.cos(a) * r, y = cy + Math.sin(a) * r;
        if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
      }
      ctx.closePath(); ctx.fill();
      ctx.fillStyle = 'rgba(0,0,0,0.18)';
      ctx.beginPath(); ctx.arc(cx, cy, 15, 0, Math.PI * 2); ctx.fill();
    });

    g.destroy();
  }
}
