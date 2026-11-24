import { writeFileSync } from "node:fs";
import { join } from "node:path";
import { pathToFileURL } from "node:url";

const [output, library] = process.argv.slice(2);
if (!output || !library) throw new Error("Usage: node build-dungeon-interactions.mjs <PNG output> <pixelfx directory>");
const { Painter, createFrame, composite, hex } = await import(pathToFileURL(join(library, "src/core/index.js")));
const { encodePNG } = await import(pathToFileURL(join(library, "tools/lib/png.mjs")));

const size = 144;
const frameCount = 12;
const atlasWidth = size * 6;
const atlasHeight = size * 6;
const pixels = new Uint8Array(atlasWidth * atlasHeight * 4);
const [ink, dark, shell, steel, rim, bright] =
  ["#101522", "#172131", "#263346", "#3b4b61", "#576b7c", "#8496a2"].map(hex);
const [cold, teal, cyan, white, copper, gold, flare] =
  ["#15324a", "#39899a", "#79dae4", "#dcfaf7", "#965d43", "#e9bc73", "#fff1c3"].map(hex);

function plate(p, x, y, w, h, cut, fill, highlight) {
  p.poly([[x + cut, y], [x + w - cut, y], [x + w, y + cut], [x + w, y + h - cut],
    [x + w - cut, y + h], [x + cut, y + h], [x, y + h - cut], [x, y + cut]], fill);
  p.line(x + cut, y, x + w - cut, y, highlight);
  p.line(x, y + cut, x, y + h - cut - 1, highlight);
}

function deck(p, accent) {
  p.disc(74, 67, 55, ink, (d, a) => Math.sin(a) > .45 ? .5 * (1 - d) : 0);
  plate(p, 21, 39, 102, 56, 10, ink, rim);
  plate(p, 24, 42, 96, 47, 8, shell, steel);
  p.rect(40, 90, 64, 2, steel);
  p.rect(29, 69, 6, 3, accent);
  p.rect(109, 69, 6, 3, accent);
}

function diamond(p, x, y, r, color, alpha = 1) {
  p.poly([[x, y-r], [x+r, y], [x, y+r], [x-r, y]], color, alpha);
}

function chest(p, frame) {
  deck(p, gold);
  const lift = [0, 0, 1, 3, 12, 23, 27, 24, 22, 23, 23, 23][frame];
  const shake = [0, -1, 2, -1, 1, 0, 0, 0, 0, 0, 0, 0][frame];
  const x = 72 + shake;
  const pulse = [0, .14, .33, .54, .8, 1, .82, .64, .4, .2, .06, 0][frame];

  // Fixed base and recessed interior.
  plate(p, x - 33, 54, 66, 30, 7, ink, steel);
  plate(p, x - 30, 53, 60, 24, 5, dark, rim);
  plate(p, x - 26, 56, 52, 19, 4, ink, copper);
  for (const offset of [-18, 0, 18]) {
    plate(p, x + offset - 5, 61 + Math.abs(offset) / 9, 10, 8, 2, copper, gold);
    diamond(p, x + offset, 64 + Math.abs(offset) / 9, 2, gold);
  }
  p.rect(x - 25, 74, 50, 6, shell);
  p.line(x - 23, 75, x + 23, 75, steel);
  for (const offset of [-22, 19]) p.rect(x + offset, 79, 3, 2, rim);

  // The lid hinges on its far edge while the near edge rises.
  if (lift < 5) {
    plate(p, x - 29, 45 - lift, 58, 27, 5, steel, bright);
    p.rect(x - 27, 56 - lift, 54, 7, shell);
    p.rect(x - 26, 65 - lift, 52, 2, gold);
    diamond(p, x, 60 - lift, 10, ink);
    diamond(p, x, 60 - lift, 6, gold);
    p.rect(x - 1, 58 - lift, 2, 4, flare);
  } else {
    p.poly([[x-27, 56], [x+27, 56], [x+31, 63-lift], [x+26, 58-lift],
      [x-26, 58-lift], [x-31, 63-lift]], shell);
    p.poly([[x-26, 55], [x+26, 55], [x+24, 62-lift], [x-24, 62-lift]], dark);
    p.polyline([[x - 31, 63 - lift], [x - 26, 58 - lift], [x + 26, 58 - lift],
      [x + 31, 63 - lift]], bright);
    p.line(x - 23, 64 - lift, x + 23, 64 - lift, steel);
    p.line(x - 24, 55, x + 24, 55, gold);
    p.rect(x - 3, 56, 6, 2, copper);
  }

  if (pulse === 0) return;
  if (frame < 4) {
    p.line(x - 18, 66 - lift, x + 18, 66 - lift, flare, pulse);
    return;
  }
  const age = frame - 4;
  p.poly([[x - 15, 62], [x - 27, 29], [x + 26, 29], [x + 16, 62]], gold, pulse * .08);
  p.line(x - 6, 57, x - 12, 27 + age, gold, pulse * .34);
  p.line(x + 7, 57, x + 15, 34 + age, gold, pulse * .29);
  for (let i = 0; i < 7; i++) {
    const side = i - 3;
    const px = x + Math.round(side * (3.5 + age * 1.6));
    const py = 58 - age * (4 + i % 3) + Math.round(age * age * .17);
    p.rect(px, py + 3, i % 2 + 1, 3, gold, pulse * .42);
    diamond(p, px, py, i === 3 ? 3 : 2, i % 3 === 1 ? flare : gold, pulse);
  }
  p.line(x - 22, 66, x + 22, 66, flare, pulse * .75);
}

function merchant(p, frame, idle = false) {
  deck(p, cyan);
  const phase = frame / frameCount * Math.PI * 2;
  const active = idle ? (1 - Math.cos(phase)) * .34 : [0, .3, .73, .94, 1, 1, .92, .81, .62, .42, .22, .1][frame];
  const raise = idle ? 0 : [0, 0, 1, 3, 4, 6, 10, 12, 11, 7, 3, 0][frame];
  const lean = idle ? Math.round(Math.sin(phase)) : [0, 0, 0, 1, 1, 0, -1, -2, -1, 0, 0, 0][frame];
  const headX = 72 + lean;
  const headY = 29 - Math.round(active * (idle ? 8 : 4));

  plate(p, 33, 49, 77, 31, 4, steel, rim);
  p.rect(37, 55, 70, 19, ink);
  p.rect(40, 77, 65, 8, dark);
  p.rect(51, 80, 42, 2, active > .4 ? cyan : teal);
  const litPanel = idle ? Math.floor(frame / 4) : frame >= 2 && frame < 8 ? Math.floor((frame - 2) / 2) : -1;
  for (let i = 0; i < 3; i++) {
    const x = 42 + 21 * i;
    p.rect(x, 59, 16, 10, shell);
    p.rect(x + 2, 61, 11, 6, cold);
    p.rect(x + 4, 63, 6, 2, litPanel === i ? white : teal);
  }
  // The shopkeeper is a hologram projected from the console.
  p.poly([[50, 46], [92, 46], [78, 59], [66, 59]], cyan, .12 + active * .11);
  p.line(51, 47, 65, 58, teal, .5 + active * .35);
  p.line(91, 47, 79, 58, teal, .5 + active * .35);
  const body = [[headX-8, headY+7], [headX+8, headY+7], [headX+17, headY+14],
    [headX+12, headY+21], [headX-12, headY+21], [headX-17, headY+14]];
  p.poly(body, cold, .94);
  p.polyline([...body, body[0]], teal);
  p.line(headX - 8, headY + 12, headX - 3, headY + 19, cyan, .77);
  p.line(headX + 8, headY + 12, headX + 3, headY + 19, cyan, .77);
  plate(p, headX - 10, headY - 8, 20, 17, 5, cold, cyan);
  p.rect(headX - 7, headY - 2, 14, 5, teal);
  const eyeHeight = idle && frame === 8 ? 1 : 2;
  p.rect(headX - 6, headY - 1, 4, eyeHeight, cyan);
  p.rect(headX + 2, headY - 1, 4, eyeHeight, cyan);
  p.line(headX - 4, headY + 5, headX + 4, headY + 5, teal);
  for (let y = headY - 5; y <= headY + 19; y += 4)
    p.line(headX - 6, y, headX + 6, y, cyan, .13 + active * .17);
  if (idle) {
    const scan = headY + 18 - frame * 2;
    const strength = Math.sin(Math.PI * frame / frameCount);
    p.line(headX - 8, scan, headX + 8, scan, cyan, strength * .5);
    diamond(p, 98, 44 - Math.round(3 * active), 1, cyan, strength * .62);
  }
  if (raise === 0) return;
  const handX = 93 + Math.round(raise / 3);
  const handY = 45 - raise;
  p.polyline([[headX+13, headY+14], [91, 43], [handX, handY]], teal, .92);
  p.polyline([[headX+14, headY+13], [92, 42], [handX+1, handY]], cyan, .63);
  plate(p, handX - 3, handY - 5, 6, 7, 1, teal, cyan);
  p.rect(handX - 1, handY - 5, 2, 4, white);
  const sweep = 49 - frame * 4;
  p.line(51, sweep, 90, sweep, cyan, .32 * active);
  diamond(p, 104, 24 - frame, 2, white, active * .8);
  diamond(p, 46, 33 - Math.round(frame / 2), 1, cyan, active * .7);
}

for (const [row, draw] of [chest, merchant, (p, frame) => merchant(p, frame, true)].entries()) {
  for (let i = 0; i < frameCount; i++) {
    const frame = createFrame(size, size);
    draw(new Painter(frame), i);
    const rgba = new Uint8Array(size * size * 4);
    composite(frame, rgba);
    const x = i % 6 * size;
    const y = (row * 2 + Math.floor(i / 6)) * size;
    for (let line = 0; line < size; line++) {
      pixels.set(rgba.subarray(line * size * 4, (line + 1) * size * 4), ((y + line) * atlasWidth + x) * 4);
    }
  }
}
writeFileSync(output, encodePNG(atlasWidth, atlasHeight, pixels));
