// Converte os SVG de saida/ em PNG no tamanho exato de cada peça.
// Uso: node marketing/identidade/render.js   (precisa do Playwright com Chromium)
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

(async () => {
  const dir = path.join(__dirname, 'saida');
  const so = process.argv.slice(2);
  const browser = await chromium.launch();
  for (const f of fs.readdirSync(dir).filter((n) => n.endsWith('.svg'))) {
    if (so.length && !so.some((s) => f.includes(s))) continue;
    const svg = fs.readFileSync(path.join(dir, f), 'utf8');
    const m = svg.match(/viewBox="0 0 (\d+) (\d+)"/);
    const [w, h] = [Number(m[1]), Number(m[2])];
    const page = await browser.newPage({ viewport: { width: w, height: h } });
    await page.setContent(`<body style="margin:0;background:transparent">${svg}</body>`);
    await page.screenshot({ path: path.join(dir, f.replace('.svg', '.png')), omitBackground: true });
    await page.close();
  }
  await browser.close();
})();
