// Generates the SVG sources for the app drawings from the design code (original design source real.js, art.js; not in this repository).
// Output: a temp folder of SVG files; svg2xaml.py turns them into XAML (src/Earbuds.App/Art/*.xaml).
// Usage: node gen-svg.mjs <designMakeDir> <outDir>
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';

const [, , makeDir, outDir] = process.argv;
if (!makeDir || !outDir) { console.error('usage: node gen-svg.mjs <designMakeDir> <outDir>'); process.exit(2); }
new Function(readFileSync(join(makeDir, 'art.js'), 'utf8'))();
new Function(readFileSync(join(makeDir, 'real.js'), 'utf8'))();
const ART = globalThis.ART;
const { CASE } = ART.REAL;
mkdirSync(outDir, { recursive: true });
let count = 0;
const W = (name, svg) => { writeFileSync(join(outDir, name + '.svg'), svg); count++; };
const fams = { short: 'short', long: 'long', pro: 'pro', gen: 'generic' };
const place = { c: 'case', o: 'out' };

// LED overlay in scene coordinates (same values as real.js scene(): ly = ry + (bh - ry) * led).
function led(fam, kind) {
  const C = CASE[fam];
  const ly = Math.round((C.ry + (C.bh - C.ry) * C.led) * 100) / 100;
  const g = kind === 'a'
    ? { dot: [['0', '#FFE2A8'], ['.35', '#F5A524'], ['1', '#B86E00']], glow: '#F5A524', go: .5 }
    : { dot: [['0', '#B6FFB0'], ['.35', '#3BD44A'], ['1', '#1E8F2A']], glow: '#4BE05A', go: .55 };
  const stops = a => a.map(s => `<stop offset="${s[0]}" stop-color="${s[1]}"/>`).join('');
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="-20 ${ly - 20} 40 40"><defs>` +
    `<radialGradient id="d" cx=".5" cy=".5" r=".5">${stops(g.dot)}</radialGradient>` +
    `<radialGradient id="w" cx=".5" cy=".5" r=".5"><stop offset="0" stop-color="${g.glow}" stop-opacity="${g.go}"/><stop offset="1" stop-color="${g.glow}" stop-opacity="0"/></radialGradient></defs>` +
    `<ellipse cx="0" cy="${ly}" rx="9" ry="7" fill="url(#w)"/><ellipse cx="0" cy="${ly}" rx="2.7" ry="2.3" fill="url(#d)"/>` +
    `<ellipse cx="-.7" cy="${ly - .7}" rx=".8" ry=".6" fill="#fff" fill-opacity=".8"/></svg>`;
}

for (const theme of ['light', 'dark']) {
  for (const [fam, nm] of Object.entries(fams)) {
    for (const l of ['c', 'o']) for (const r of ['c', 'o']) {
      W(`scene.${nm}.${l}${r}.${theme}`, ART.scene(fam, { theme, h: 240, l: place[l], r: place[r], led: 'off', id: 's', xmlns: true }));
    }
    W(`bud.${nm}.l.${theme}`, ART.bud(fam, 'l', { theme, h: 200, id: 'b', xmlns: true }));
    W(`bud.${nm}.r.${theme}`, ART.bud(fam, 'r', { theme, h: 200, id: 'b', xmlns: true }));
  }
  W(`max.${theme}`, ART.max({ theme, h: 240, id: 'm', xmlns: true }));
}
for (const fam of ['short', 'long', 'pro']) for (const k of ['g', 'a']) W(`led.${fams[fam]}.${k}`, led(fam, k));
W('appicon', ART.appIcon(256, { xmlns: true }));
console.log('svg files:', count);
