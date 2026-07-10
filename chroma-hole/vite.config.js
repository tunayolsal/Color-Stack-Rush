import { defineConfig } from 'vite';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.dirname(fileURLToPath(import.meta.url));

/**
 * Dev-only helper endpoint: the running game can POST a canvas snapshot to
 * /__screenshot and it gets written into store-assets/. Used to generate the
 * app icon PNG and the promo screenshots straight from the live game.
 * It is registered with `apply: 'serve'` so it never ships in a build.
 */
function screenshotEndpoint() {
  return {
    name: 'screenshot-endpoint',
    apply: 'serve',
    configureServer(server) {
      server.middlewares.use('/__screenshot', (req, res) => {
        if (req.method !== 'POST') { res.statusCode = 405; return res.end('method not allowed'); }
        let body = '';
        req.on('data', (c) => { body += c; });
        req.on('end', () => {
          try {
            const { name, data } = JSON.parse(body);
            const safe = path.basename(String(name));
            if (!/^[\w.-]+\.(png|jpe?g)$/i.test(safe)) throw new Error('bad file name');
            const b64 = String(data).split(',')[1];
            const dir = safe.startsWith('icon-')
              ? path.join(ROOT, 'store-assets')
              : path.join(ROOT, 'store-assets', 'screenshots');
            fs.mkdirSync(dir, { recursive: true });
            fs.writeFileSync(path.join(dir, safe), Buffer.from(b64, 'base64'));
            res.end('ok');
          } catch (e) {
            res.statusCode = 400;
            res.end(String(e));
          }
        });
      });
    },
  };
}

export default defineConfig({
  base: './', // relative paths so the build works on any portal subdirectory
  plugins: [screenshotEndpoint()],
  server: { port: 5173, strictPort: true },
  build: { chunkSizeWarningLimit: 1600 },
});
