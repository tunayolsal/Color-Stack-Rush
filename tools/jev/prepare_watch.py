#!/usr/bin/env python3
"""Add the local-only viewing controls to an explicitly marked Jev dev build."""
import argparse
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("build", type=Path)
args = parser.parse_args()
root = args.build.resolve()
if not (root / "LOCAL_JEV_TEST_BUILD.txt").is_file():
    parser.error("Only an explicitly marked local Jev development build is supported")
path = root / "index.html"
html = path.read_text(encoding="utf-8")
if 'id="jev-watch"' in html:
    parser.error("Viewing controls already installed")
html = html.replace("matchWebGLToCanvasSize: true", "matchWebGLToCanvasSize: false")
html = html.replace("devicePixelRatio: mobile ? 1 : Math.min(window.devicePixelRatio || 1, 1.5)", "devicePixelRatio: 1")
if "matchWebGLToCanvasSize: false" not in html or "devicePixelRatio: 1" not in html:
    parser.error("The expected portrait template settings were not found")
style = """
<style>
body { --available-height:calc(100dvh - env(safe-area-inset-top) - env(safe-area-inset-bottom) - 76px);grid-template-rows:76px 1fr; }
#jev-watch { grid-row:1;width:min(100%,720px);height:76px;display:flex;align-items:center;justify-content:center;gap:12px;padding:8px 12px;font-size:13px; }
#jev-watch button { padding:10px 17px;font-size:13px;white-space:nowrap; }
#jev-watch p { margin:0;line-height:1.4; }
#game { grid-row:2;align-self:center; }
</style>
"""
bar = """
<aside id="jev-watch" aria-live="polite">
  <p><strong>Jev AI canlı test</strong><br><span id="jev-detail">Oyun hazırlanıyor. Bu yerel test sayfasıdır.</span></p>
  <button id="jev-start" disabled>Jev testini başlat</button>
  <button id="jev-resume" hidden>Devam</button>
</aside>
"""
script = """
<script>
const jevButton = document.getElementById('jev-start');
const jevDetail = document.getElementById('jev-detail');
let jevStarted = false;
document.getElementById('jev-resume').addEventListener('click', () => {
  window.colorStackRush?.unlockAudio?.();
  player?.SendMessage('[Managers]', 'ResumeGame');
});
jevButton.addEventListener('click', () => {
  if (!player || jevStarted) return;
  window.colorStackRush?.unlockAudio?.();
  player.SendMessage('[LOCAL JEV LAUNCHER]', 'StartWatchedSuite');
  jevStarted = true;
  jevButton.disabled = true;
  jevButton.textContent = 'Jev oynuyor';
  document.getElementById('jev-resume').hidden = false;
  jevDetail.textContent = '32 koşu başlıyor; oyunu Jev yönlendirecek.';
});
setInterval(async () => {
  if (player && !jevStarted) jevButton.disabled = false;
  try {
    const response = await fetch('/api/status', {cache:'no-store'});
    if (!response.ok) return;
    const report = await response.json();
    if (!jevStarted && !report.finishedRuns) return;
    const profile = {normal:'normal',extra_150ms:'+150 ms',extra_300ms:'+300 ms',one_bad_then_recover:'hata sonrası toparlanma'};
    const current = report.current;
    jevDetail.textContent = report.finished
      ? '32 koşu tamamlandı · Normal bitiş: ' + report.normalWins + '/8'
      : (current ? 'Bölüm ' + current.level + ' · ' + (profile[current.profile] || current.profile) + ' · ' : '') + (report.finishedRuns || 0) + '/32 tamamlandı';
    if (report.finished) {
      jevButton.textContent = 'Test tamamlandı';
      document.getElementById('jev-resume').hidden = true;
    }
  } catch (_) { /* Keep the current game visible if a local status poll fails. */ }
}, 1000);
</script>
"""
html = html.replace("</head>", style + "</head>")
html = html.replace("<body>", "<body>" + bar)
html = html.replace("</body>", script + "</body>")
html = html.replace("<title>Color Stack Rush · Oyna</title>", "<title>Color Stack Rush · Jev AI canlı test</title>")
path.write_text(html, encoding="utf-8")
print(f"Local-only Jev viewing controls installed in {root}")
