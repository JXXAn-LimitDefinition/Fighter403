import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { pathToFileURL } from "node:url";

const [library, output, preview] = process.argv.slice(2);
if (!library || !output || !preview) {
  throw new Error("Usage: node build-root-boss-effects.mjs <pixelfx directory> <Unity texture directory> <preview directory>");
}

const { createFrame, clearFrame, painterFor, composite, hex, ease, rand, TAU } =
  await import(pathToFileURL(join(library, "src/core/index.js")));
const { canvas, writeVideo } = await import(pathToFileURL(join(library, "tools/lib/fxlib.mjs")));
const { encodePNG } = await import(pathToFileURL(join(library, "tools/lib/png.mjs")));

const cell = 96;
const frames = 12;
const atlasColumns = 16;
const ink = hex("#251a45");
const violet = hex("#7851c9");
const lilac = hex("#c59cff");
const blue = hex("#4f95de");
const cyan = hex("#8aeaff");
const white = hex("#effdff");
const claret = hex("#6f284e");
const rose = hex("#ff557c");
const coral = hex("#ff96ac");

const sin = Math.sin;
const cos = Math.cos;
const pulse = (phase) => .5 - .5 * cos(phase * TAU);
const decay = (t) => (1 - t) ** 2;
const point = (radius, angle) => [radius * cos(angle), radius * sin(angle)];
const triangle = (radius, angle) => Array.from({ length: 3 }, (_, k) => point(radius, angle + k * TAU / 3));
const diamond = (r) => [[0, -r], [r, 0], [0, r], [-r, 0], [0, -r]];
const close = (vertices) => [...vertices, vertices[0]];
const clamp = (t) => Math.min(1, Math.max(0, t));
const smooth = (a, b, t) => {
  const x = clamp((t - a) / (b - a));
  return x * x * (3 - 2 * x);
};
const moved = (points, x, y) => points.map(([u, v]) => [u + x, v + y]);

function panel(p, x, y, width, height, color, face, alpha) {
  const right = x + width;
  const bottom = y + height;
  const shape = [[x, y + 2], [x + 2, y], [right, y], [right, bottom - 2],
    [right - 2, bottom], [x, bottom]];
  p.poly(shape, face, .84 * alpha);
  p.polyline(close(shape), color, .58 * alpha);
  p.line(x + 3, y, right - 2, y, color, .84 * alpha);
  p.line(right, y + 2, right, bottom - 2, color, .46 * alpha);
}

function tracePacket(p, path, phase, color, alpha) {
  const locations = path.map(([x, y], i) => {
    if (i === 0) return { x, y, length: 0 };
    return { x, y, length: Math.hypot(x - path[i - 1][0], y - path[i - 1][1]) };
  });
  const length = locations.reduce((total, sample) => total + sample.length, 0);
  let distance = clamp(phase) * length;
  for (let i = 1; i < locations.length; i++) {
    const a = locations[i - 1];
    const b = locations[i];
    if (distance <= b.length) {
      const t = distance / Math.max(1, b.length);
      const x = a.x + (b.x - a.x) * t;
      const y = a.y + (b.y - a.y) * t;
      p.rect(x - 1, y - 1, 3, 3, color, alpha);
      p.dot(x, y, white, alpha);
      return;
    }
    distance -= b.length;
  }
}

function sparkle(p, x, y, size, color, alpha) {
  p.line(x - size, y, x + size, y, color, alpha);
  p.line(x, y - size, x, y + size, color, alpha);
  p.dot(x, y, white, alpha);
}

function glints(p, q, range, count, colors, seed, inward = false) {
  for (let i = 0; i < count; i++) {
    const start = rand(seed, i, 3) * .22;
    const age = Math.max(0, (q - start) / (1 - start));
    if (age <= 0 || age > .94) continue;
    const angle = TAU * rand(seed, i, 1);
    const travel = ease.outQuart(age);
    const r = range * (.25 + rand(seed, i, 2) * .75) * (inward ? 1.1 - travel : .14 + .86 * travel);
    const [x, y] = point(r, angle);
    const side = 1 + Math.floor(rand(seed, i, 4) * 3);
    const tail = side * (1 + 2 * decay(age));
    const alpha = decay(age) * .92;
    const direction = inward ? -1 : 1;
    p.line(x - cos(angle) * tail * direction, y - sin(angle) * tail * direction, x, y,
      colors[i % colors.length], alpha);
    if (i % 4 === 1) p.glow(x, y, 4, colors[i % colors.length], alpha * .24);
    p.rect(x, y, side, 1, age < .27 ? white : colors[i % colors.length], alpha);
  }
}

function triangles(p, q, radius, color) {
  const turn = -TAU / 4 + q * TAU / 3;
  p.polyline(close(triangle(radius, turn)), color, .68);
  p.polyline(close(triangle(radius - 5, turn)), violet, .60);
  for (let i = 0; i < 3; i++) {
    const [x, y] = point(radius, turn + i * TAU / 3);
    sparkle(p, x, y, 2, color, .84);
  }
}

function arrival(p, q) {
  if (q < .53) {
    const t = q / .53;
    const radius = 34 - ease.outQuart(t) * 15;
    triangles(p, t, radius, cyan);
    for (let i = 0; i < 6; i++) {
      const [x, y] = point(radius + 6, i * TAU / 6 - q * 2);
      p.rect(x - 2, y, 4, 2, i % 2 ? blue : lilac, .8);
    }
    p.glow(0, 0, 29, violet, .44 * t);
    for (let i = 0; i < 3; i++) {
      const angle = i * TAU / 3 - TAU / 4 + .6 * t;
      const [x, y] = point(radius + 6, angle);
      const [u, v] = point(radius + 15 - 10 * t, angle - .36);
      p.arc(0, 0, radius + 11, 2, angle - .56, angle - .14, blue, .46);
      p.line(x, y, u, v, lilac, .63);
      panel(p, x - 3, y - 2, 6, 4, cyan, ink, smooth(.1, .5, t));
    }
    glints(p, t, 40, 15, [cyan, violet, lilac], 47, true);
  } else {
    const t = (q - .53) / .47;
    p.ring(0, 0, 16 + 24 * ease.outQuart(t), 3 - 2 * t, lilac, decay(t));
    const span = 34 * (1 - t);
    p.line(-span, 0, span, 0, cyan, decay(t));
    const phaseEcho = 13 + 24 * ease.outQuart(clamp(t - .11));
    p.polyline(close(triangle(phaseEcho, -TAU / 4 + .82)), blue, decay(t) * .72);
    p.glow(0, 0, 23, lilac, .45 * decay(t));
    glints(p, t, 41, 24, [blue, cyan, lilac], 5);
  }
}

function lock(p, q) {
  const strength = q < .54 ? 1 : decay((q - .54) / .46);
  const closeAmount = ease.outQuart(Math.min(1, q * 3.4));
  const gap = (1 - closeAmount) * 14;
  const lift = -2;
  p.line(-23, 0, -12 - gap, 0, violet, strength * .74);
  p.line(12 + gap, 0, 23, 0, cyan, strength * .74);
  p.polyline([[-22, -5], [-26, -5], [-26, 5], [-22, 5]], lilac, strength);
  p.polyline([[22, -5], [26, -5], [26, 5], [22, 5]], cyan, strength);
  const latches = 15 + gap * .72;
  for (const side of [-1, 1]) {
    const x = side * latches;
    const y = -6 + 6 * closeAmount;
    panel(p, x - 4, y - 3, 8, 6, side < 0 ? lilac : cyan, ink, strength);
    p.line(x - 2, y, x + 2, y, white, strength * smooth(.25, .34, q));
  }
  p.rect(-11, lift, 22, 16, ink, strength);
  p.rect(-10, lift + 1, 20, 13, violet, strength);
  p.rect(-8, lift + 2, 17, 3, lilac, strength);
  p.polyline([[-6, lift], [-6, -10 - gap], [-2, -14 - gap], [3, -14 - gap], [7, -10 - gap],
    [7, lift]], cyan, strength);
  p.polyline([[-5, lift], [-5, -9 - gap], [-1, -12 - gap], [3, -12 - gap], [6, -9 - gap]], lilac, strength);
  p.rect(-1, 4, 3, 6, ink, strength);
  p.circle(0, 5, 2, white, strength);
  sparkle(p, 7, -7, 3, cyan, strength * pulse(Math.min(1, q * 2.6)));
  p.glow(0, 0, 19, violet, strength * .38);
  glints(p, q, 35, 12, [violet, blue, lilac], 12);
}

function breachGlyph(p, q) {
  const strength = decay(q);
  const x = 2 + 11 * ease.outQuart(q);
  p.polyline([[-8, -12], [-1, -12], [10, 0], [-1, 12], [-8, 12]], violet, strength);
  p.polyline([[x - 5, -9], [x, -9], [x + 9, 0], [x, 9], [x - 5, 9]], lilac, strength);
  p.line(x + 7, 0, x + 13, 0, white, strength);
  const edge = Math.min(1, q * 4);
  for (const side of [-1, 1]) {
    const y = side * (8 + 13 * edge);
    p.polyline([[x - 12, side * 6], [x - 4, y], [x + 9, y]], violet, strength * .74);
    p.rect(x + 4, y, 7, 1, cyan, strength * .78);
  }
  p.arc(0, 0, 20 + 8 * edge, 2, -.70 + .5 * q, .66 + .1 * q, lilac, strength * .64);
  p.glow(x, 0, 19, lilac, strength * .4);
  glints(p, q, 31, 16, [blue, cyan, lilac], 19);
}

function auditMuzzle(p, q, double) {
  const strength = decay(q);
  if (double) {
    p.poly([[-14, -17], [5, -17], [13, 0], [5, 17], [-14, 17], [-4, 0]], ink, strength * .88);
    for (const direction of [-1, 1]) {
      const shift = direction * 9;
      p.poly([[0, shift - 3], [28 - 11 * q, shift], [0, shift + 3], [-8, shift]],
        direction > 0 ? lilac : cyan, strength);
      p.line(-6, shift, 18 - 9 * q, shift, white, strength);
      p.glow(2, shift, 14, direction > 0 ? violet : cyan, .51 * strength);
    }
  } else {
    p.poly([[-10, -11], [0, -15], [26, 0], [0, 15], [-10, 11], [-4, 0]], blue, strength);
    p.poly([[-3, -6], [7, -8], [23, 0], [7, 8], [-3, 6]], cyan, strength);
    p.line(-9, 0, 26 - 15 * q, 0, white, strength);
    p.glow(3, 0, 20, cyan, strength * .52);
  }
  const recoil = 11 + 11 * ease.outQuart(q);
  for (const side of [-1, 1]) {
    const base = double ? side * 11 : 0;
    const y = base + side * (6 + 5 * q);
    const color = double && side > 0 ? lilac : cyan;
    p.polyline([[-recoil, y - side * 3], [-recoil + 6, y], [-recoil + 13, base]], color, .7 * strength);
    p.rect(-recoil, y, 5, 2, color, strength);
    p.rect(-recoil - 7, y + side * 2, 3, 1, blue, .5 * strength);
  }
  glints(p, q, 37, double ? 16 : 11, double ? [cyan, lilac, blue] : [blue, cyan], 24 + Number(double));
}

function blinkTarget(p, q) {
  const r = 22 - 6 * ease.outQuart(q);
  for (let i = 0; i < 4; i++) {
    const angle = i * TAU / 4;
    const [x, y] = point(r, angle);
    const a = angle + Math.PI / 2;
    const [dx, dy] = point(6, a);
    p.polyline([[x + dx, y + dy], [x, y], [x - dx, y - dy]], violet);
    const inner = point(r - 3, angle);
    p.line(x, y, inner[0], inner[1], cyan);
  }
  p.polyline(diamond(10 + Math.floor(3 * pulse(q))), lilac, .78);
  const scan = -13 + 26 * q;
  const half = Math.max(1, 14 - Math.abs(scan) * .82);
  p.rect(-half, scan, half * 2, 1, cyan, .34 + .4 * q);
  for (const side of [-1, 1]) {
    p.rect(side * (half + 2), scan, 2, 2, lilac, .72);
  }
  p.polyline(diamond(6), blue, .8);
  p.dot(0, 0, white, .82);
  p.glow(0, 0, 26, blue, .25 + q * .25);
}

function blinkOut(p, q) {
  const spread = ease.outQuart(q);
  for (let i = 0; i < 9; i++) {
    const y = i * 4 - 16;
    const side = i % 2 ? -1 : 1;
    const offset = side * spread * (10 + i % 3 * 6);
    const width = (16 - Math.abs(y) * .65) * (1 - q);
    p.rect(offset - width, y, width * 2, 2, i % 2 ? violet : blue, decay(q));
    p.rect(offset + side * width - 1, y, 3, 1, i % 2 ? lilac : cyan, decay(q));
  }
  for (let i = 0; i < 3; i++) {
    const y = (i - 1) * 10;
    const offset = spread * (18 + i * 5);
    p.rect(-offset, y, 5 + 7 * (1 - q), 2, violet, decay(q) * .78);
    p.rect(offset, y + 2, 7 + 5 * (1 - q), 1, cyan, decay(q) * .58);
  }
  glints(p, q, 32, 13, [blue, violet, lilac], 8);
}

function blinkIn(p, q) {
  const strip = (1 - ease.outQuart(q)) * 22;
  p.polyline(diamond(20 - 14 * q), lilac, decay(q));
  p.poly([[0, -strip], [5 * (1 - q), 0], [0, strip], [-5 * (1 - q), 0]], cyan, decay(q));
  p.line(0, -strip, 0, strip, white, decay(q));
  sparkle(p, 0, 0, 9 * (1 - q), cyan, decay(q));
  for (let i = 0; i < 3; i++) {
    const row = i - 1;
    const width = 4 + 16 * ease.outQuart(q);
    const y = row * (5 + 9 * q);
    p.line(-width, y, width * .8, y, row === 0 ? lilac : blue, decay(q) * .78);
    p.dot(-width, y, cyan, decay(q));
  }
  p.glow(0, 0, 22, cyan, .49 * decay(q));
  glints(p, q, 33, 13, [cyan, blue, lilac], 31);
}

function fork(p, q) {
  const open = ease.outQuart(q);
  const reach = 15 + open * 19;
  const split = 9 + open * 13;
  const paths = [
    [[4, 0], [-5, 0], [-13, -8], [-reach, -split]],
    [[4, 0], [-5, 0], [-13, 8], [-reach, split]],
  ];
  for (const [i, path] of paths.entries()) {
    const [x, y] = path[3];
    p.polyline(path.map(([u, v]) => [u + 1, v + 2]), violet, .88);
    p.polyline(path, i ? cyan : lilac, .94);
    p.polyline([[x - 5, y], [x, y - 5], [x + 5, y], [x, y + 5], [x - 5, y]], cyan, .77);
    const pathAge = clamp((q - i * .15) / .66);
    tracePacket(p, path, pathAge, i ? cyan : lilac, .9);
    const boot = smooth(.37 + i * .12, .7 + i * .12, q);
    panel(p, x - 5, y - 3, 10, 6, i ? cyan : lilac, ink, boot);
    p.rect(x - 2, y - 1, 5, 2, white, boot * (.4 + .6 * pulse(q * 1.8 + i * .5)));
    p.glow(x, y, 11, i ? blue : violet, .37);
  }
  p.polyline(diamond(10), lilac, .76);
  p.glow(-3, 0, 17, violet, .26 + .22 * q);
}

function clone(p, q) {
  const distance = 5 + 24 * ease.outQuart(q);
  const strength = decay(q);
  for (const side of [-1, 1]) {
    const x = distance * side;
    p.polyline([[0, 0], [x * .35, 0], [x * .62, side * 6], [x, side * 6]], violet, strength);
    p.polyline(diamond(6 - 3 * q).map(([u, v]) => [u + x, v + side * 6]), cyan, strength);
    const cellWidth = 5 + 3 * (1 - q);
    const trace = [[0, 0], [x * .35, 0], [x * .62, side * 6], [x, side * 6]];
    tracePacket(p, trace, smooth(0, .38, q), cyan, strength);
    panel(p, x - cellWidth / 2, side * 6 - 3, cellWidth, 6, side > 0 ? cyan : lilac, ink, strength);
    p.glow(x, side * 6, 9, lilac, strength * .30);
  }
  sparkle(p, 0, 0, 6 - 4 * q, lilac, strength);
  glints(p, q, 26, 10, [violet, blue, cyan], 13);
}

function childSpawn(p, q) {
  const r = 6 + 11 * ease.outQuart(q);
  const strength = decay(q);
  for (let i = 0; i < 6; i++) {
    const [x, y] = point(r, i * TAU / 6 + .5 * q);
    p.rect(x - 1, y - 1, i % 2 ? 3 : 5, 2, i % 2 ? cyan : lilac, strength);
  }
  const scan = -12 + 20 * ease.outQuart(q);
  p.line(-14 + Math.abs(scan) * .44, scan, 14 - Math.abs(scan) * .44, scan, cyan, strength);
  p.polyline(diamond(r * .74), violet, strength);
  p.polyline(diamond(r * 1.08), blue, strength * .53);
  sparkle(p, 0, 0, 7 * (1 - q), cyan, strength);
  p.glow(0, 0, 18, blue, strength * .47);
}

function childHit(p, q) {
  const radius = 18 + 8 * ease.outQuart(q);
  const strength = decay(q);
  p.arc(0, 0, radius, 4 - q * 2, -.64, .64, lilac, strength);
  p.arc(0, 0, radius - 1, 1, -.49, .49, cyan, strength);
  for (let i = 0; i < 3; i++) {
    const angle = (i - 1) * .43;
    const inner = point(radius - 8 - q * 4, angle);
    const tip = point(radius + 2, angle);
    p.line(...inner, ...tip, i === 1 ? cyan : blue, strength * .68);
    sparkle(p, ...tip, i === 1 ? 3 : 2, lilac, strength);
  }
  p.arc(0, 0, radius - 6, 1.25, -.52, .52, violet, strength * .84);
}

function childWarning(p, q) {
  const x = 12 + 5 * q;
  p.polyline([[x - 4, -7], [x, -7], [x + 6, 0], [x, 7], [x - 4, 7]], lilac, .55 + .4 * q);
  p.line(x + 3, 0, x + 8, 0, cyan, .36 + .58 * q);
  p.arc(0, 0, 24, 1.3, -.67, .67, violet, .56);
  p.arc(0, 0, 24, 1, -.67, -.67 + 1.34 * q, cyan, .8);
  for (const side of [-1, 1]) {
    p.polyline([[x - 13, side * 4], [x - 7, side * 4], [x - 3, side]], blue, .64);
  }
}

function killCharge(p, q) {
  const r = 31 - ease.outQuart(q) * 9;
  const turn = .2 + q * 1.3;
  for (let i = 0; i < 3; i++) {
    const a = turn + i * TAU / 3;
    p.arc(0, 0, r, 3, a, a + 1.3, claret);
    p.arc(0, 0, r - 1, 1, a + .17, a + 1.05, i === 1 ? coral : rose);
    const [x, y] = point(r, a + 1.3);
    const [dx, dy] = point(r - 9, a + 1.1);
    p.line(x, y, dx, dy, rose, .9);
  }
  const armed = smooth(.49, .83, q);
  for (let i = 0; i < 3; i++) {
    const a = turn + i * TAU / 3 + .54;
    const from = point(r + 10, a);
    const to = point(r + 2, a);
    p.line(...from, ...to, coral, .45 + armed * .45);
    p.line(...to, ...point(r - 3, a + .16), rose, armed * .91);
  }
  p.glow(0, 0, 24, rose, .16 + armed * .21);
  for (let i = 0; i < 3; i++) {
    const angle = (i / 3 + q * .2) * TAU;
    const [x, y] = point(15, angle);
    p.rect(x - 2, y - 2, 5, 2, rose, .3 + .7 * pulse(q + i / 3));
  }
}

function killReticle(p, q) {
  const r = 29;
  for (let i = 0; i < 4; i++) {
    const angle = (i + .5) * TAU / 4;
    p.arc(0, 0, r, 2.2, angle - .28, angle + .28, rose, .63 + .32 * pulse(q + i * .25));
    const [x, y] = point(r - 4, angle);
    const [u, v] = point(r + 1, angle);
    p.line(x, y, u, v, coral, .86);
  }
  const latchRadius = 15 - 5 * pulse(q);
  for (const side of [-1, 1]) {
    p.polyline([[side * latchRadius, -4], [side * (latchRadius - 3), 0], [side * latchRadius, 4]],
      coral, .68 + .18 * pulse(q));
  }
  p.polyline(diamond(8 + 3 * pulse(q)), rose, .72);
  p.dot(0, 0, coral, .85);
  p.glow(0, 0, 18, rose, .26);
}

function detonation(p, q) {
  const r = 6 + ease.outQuart(q) * 28;
  const strength = decay(q);
  p.ring(0, 0, r, 3 - 2 * q, claret, strength);
  p.ring(0, 0, r - 2, 1.7, rose, strength);
  p.polyline(diamond(7 + 22 * ease.outQuart(q)), coral, strength);
  if (q > .08) {
    const echo = 4 + 23 * ease.outQuart(clamp(q - .10));
    p.polyline(diamond(echo), claret, strength * .8);
  }
  for (let i = 0; i < 6; i++) {
    const angle = (i + .42) * TAU / 6;
    const [x, y] = point(r * .94, angle + q * .4);
    const shard = triangle(3.8 - q * 2.5, angle + .6 * q);
    p.poly(moved(shard, x, y), i % 2 ? rose : claret, strength * .87);
    p.line(x, y, ...point(r + 3, angle + q * .4), coral, strength);
  }
  if (q < .27) {
    const core = 9 * (1 - q * 3);
    p.poly(diamond(core).slice(0, -1), coral, strength);
    sparkle(p, 0, 0, core + 6, white, strength);
  }
  p.glow(0, 0, 24, rose, strength * .63);
  glints(p, q, 39, 26, [claret, rose, coral], 14);
}

function healPacket(p, q) {
  const fade = Math.min(1, (1 - q) * 5);
  p.poly(diamond(5).slice(0, -1), claret, fade);
  p.polyline(diamond(5), coral, fade);
  p.line(-6, 0, -19, 0, rose, .55 * fade);
  p.rect(-9, -4, 3, 2, coral, .85 * fade);
  p.rect(-14, 4, 4, 1, rose, .85 * fade);
  for (let i = 0; i < 3; i++) {
    const x = -7 - i * (4 + 3 * q);
    const y = i % 2 ? 2 : -2;
    p.polyline(moved(diamond(3 - .5 * i), x, y), i === 0 ? coral : claret, fade * (.68 - i * .12));
  }
  p.glow(0, 0, 16, rose, fade * .27);
  p.dot(0, 0, white, fade);
}

function guardianRaise(p, q) {
  const expand = ease.outQuart(Math.min(1, q * 2));
  const strength = .88 * (q < .55 ? 1 : decay((q - .55) / .45));
  p.polyline(diamond(9 + expand * 21), violet, strength);
  p.polyline(diamond(5 + expand * 18), cyan, strength);
  for (let i = 0; i < 4; i++) {
    const [x, y] = point(13 + expand * 18, i * TAU / 4);
    p.rect(x - 2, y - 2, 5, 5, ink, strength);
    p.rect(x - 1, y - 1, 3, 3, i % 2 ? cyan : lilac, strength);
    const locked = smooth(.12 + i * .06, .32 + i * .06, q);
    const direction = i * TAU / 4;
    const [u, v] = point(8 + expand * 14, direction);
    p.line(u, v, x, y, cyan, locked * strength);
    panel(p, x - 4, y - 3, 8, 6, i % 2 ? cyan : lilac, ink, locked * strength);
    p.glow(x, y, 8, blue, strength * .3);
  }
  p.glow(0, 0, 19, cyan, strength * .45);
  glints(p, q, 39, 18, [blue, cyan, lilac], 83);
}

function shieldTile(p, q) {
  const plate = [[-6, -17], [3, -15], [8, -9], [8, 9], [3, 15], [-6, 17], [-9, 12], [-9, -12]];
  p.poly(plate, ink, .58);
  p.polyline(close(plate), violet, .86);
  p.polyline([[3, -15], [8, -9], [8, 9], [3, 15]], cyan, .9);
  p.polyline([[-5, -8], [-1, -8], [3, -4], [3, 5], [-1, 9], [-5, 9]], blue, .72);
  const traversal = -12 + 24 * q;
  const scanning = .82 * sin(Math.PI * q);
  p.line(-4, traversal, 7, traversal, cyan, scanning);
  p.line(5, traversal, 8, traversal, white, scanning);
  p.polyline([[-8, -9], [-4, -13], [1, -12]], lilac, .62);
  p.polyline([[-8, 9], [-4, 13], [1, 12]], blue, .7);
  p.glow(3, 0, 13, blue, .37);
}

function guardCrack(p, q) {
  const s = decay(q);
  const move = ease.outQuart(q) * 16;
  p.polyline([[-13 - move, -12], [-5 - move, -13], [-1 - move, -6], [-7 - move, 1], [-5 - move, 10]], lilac, s);
  p.polyline([[4 + move, -11], [12 + move, -7], [9 + move, 3], [12 + move, 12], [4 + move, 15]], cyan, s);
  p.polyline([[3, -15], [-2, -5], [4, -1], [-4, 5], [0, 11]], white, s);
  for (const side of [-1, 1]) {
    const x = side * (9 + move);
    const y = side * (5 + 6 * q);
    const shard = moved(triangle(5 - 3 * q, side * (q + .6)), x, y);
    p.poly(shard, ink, s);
    p.polyline(close(shard), side > 0 ? cyan : lilac, s);
  }
  p.glow(0, 0, 22, cyan, .42 * s);
  glints(p, q, 36, 20, [blue, cyan, lilac], 29);
}

function shieldBreak(p, q) {
  const r = 16 + 22 * ease.outQuart(q);
  for (let i = 0; i < 4; i++) {
    const a = i * TAU / 4;
    const points = [point(r - q * 5, a - .20), point(r + 3, a - .05), point(r - 2, a + .21)];
    p.polyline(points, i % 2 ? cyan : lilac, decay(q));
    const echo = points.map(([x, y]) => [x * .78, y * .78]);
    p.polyline(echo, blue, decay(q) * .7);
    const [x, y] = points[1];
    panel(p, x - 3, y - 2, 6, 4, i % 2 ? cyan : lilac, ink, decay(q));
    p.glow(...points[1], 9, blue, decay(q) * .28);
  }
  p.glow(0, 0, 32, blue, .49 * decay(q));
  glints(p, q, 41, 32, [blue, cyan, violet, lilac], 79);
}

function unsafeRing(p, q) {
  const alpha = .36 + .45 * pulse(q);
  for (const side of [-1, 1]) {
    p.polyline([[-24, side * 14], [-15, side * 14], [-8, side * 8], [8, side * 8], [15, side * 14],
      [24, side * 14]], rose, alpha);
    p.line(-14, side * 21, 14, side * 21, claret, .85);
  }
  const travel = 4 + q * 11;
  for (let i = 0; i < 2; i++) {
    const y = travel + i * 5 - 12;
    const fade = sin(Math.PI * q) * (i ? .56 : .9);
    p.polyline([[-5, y - 4], [0, y], [5, y - 4]], i ? rose : coral, fade);
  }
  p.rect(-27, -2, 4, 4, rose, alpha * .65);
  p.rect(23, -2, 4, 4, rose, alpha * .65);
}

function deletedBand(p, q) {
  for (let i = 0; i < 6; i++) {
    const t = (q + i / 6) % 1;
    const x = 4 + rand(23, i, 3) * 28;
    const y = 12 * (t - .5);
    const f = sin(Math.PI * t);
    p.rect(-x, y, 7 + 13 * t, i % 2 ? 2 : 3, i % 2 ? claret : rose, f * .75);
    p.rect(x - 12, -y, 4 + 10 * t, 2, coral, f * .56);
  }
  p.line(-25, 10, -5, 10, rose, .63);
  p.line(5, 10, 25, 10, rose, .63);
  p.line(-17, 14, 17, 14, claret, .46);
  p.polyline([[-3, 6], [3, 12]], coral, .5);
  p.polyline([[3, 6], [-3, 12]], coral, .5);
  for (const side of [-1, 1]) {
    p.polyline([[side * 25, 6], [side * 29, 6], [side * 29, 11]], rose, .67);
  }
}

function forkPhase(p, q) {
  const r = 18 + 17 * pulse(q * .78);
  const s = q < .67 ? 1 : decay((q - .67) / .33);
  for (let i = 0; i < 3; i++) {
    const angle = i * TAU / 3 - TAU / 4 + q * .7;
    const inner = point(r * .52, angle);
    const tip = point(r, angle);
    const left = point(r + 4, angle - .2);
    const right = point(r + 4, angle + .2);
    p.polyline([left, tip, inner, tip, right], lilac, s);
    p.glow(...tip, 12, violet, s * .45);
    const path = [inner, tip, left];
    tracePacket(p, path, smooth(i * .12, .42 + i * .12, q), cyan, s);
    const [x, y] = right;
    panel(p, x - 3, y - 2, 6, 4, lilac, ink, s * smooth(.15, .46, q));
    p.circle(...tip, 1.5, cyan, s);
  }
  p.glow(0, 0, 31, violet, s * .38);
  glints(p, q, 41, 26, [violet, blue, lilac], 59, true);
}

function sudoPhase(p, q) {
  const s = q < .64 ? 1 : decay((q - .64) / .36);
  const motion = pulse(q);
  for (const i of [-1, 1]) {
    for (const j of [-1, 1]) {
      const r = 33 - 12 * motion;
      p.polyline([[i * (r - 9), j * r], [i * r, j * r], [i * r, j * (r - 9)]], lilac, s);
      p.line(i * (r - 1), j * (r - 1), i * (r - 12), j * (r - 12), rose, s * .7);
      p.rect(i * (r - 3), j * (r - 3), 3, 2, coral, s);
    }
  }
  p.polyline(diamond(17 + 10 * motion), rose, s * .75);
  const scans = [-.11, .08, .23];
  for (const delay of scans) {
    const t = clamp((q - delay) / .52);
    const y = -22 + 44 * t;
    const width = 25 - Math.abs(y) * .36;
    const scan = sin(Math.PI * t);
    p.line(-width, y, width, y, delay < 0 ? lilac : rose, s * scan * .87);
    p.rect(width - 1, y, 3, 2, coral, s * scan);
  }
  const center = 6 + 8 * smooth(.46, .62, q);
  p.polyline([[-center, -center], [center, center]], rose, s * smooth(.36, .58, q));
  p.polyline([[center, -center], [-center, center]], rose, s * smooth(.36, .58, q));
  p.glow(0, 0, 32, violet, s * .48);
  glints(p, q, 41, 24, [claret, rose, lilac, cyan], 61, true);
}

function death(p, q) {
  const rupture = smooth(.15, .59, q);
  const radius = 7 + rupture * 32;
  const strength = q < .15 ? 1 : decay(clamp((q - .15) / .85));
  if (q < .74) {
    for (let i = 0; i < 3; i++) {
      const angle = i * TAU / 3 - TAU / 4 + .6 * q;
      const shellRadius = q < .15 ? 15 - 8 * q / .15 : radius * .73;
      const [x, y] = point(shellRadius, angle);
      const points = moved(triangle(10 - q * 9, angle + q), x, y);
      const trail = point(Math.max(4, shellRadius - 8), angle - q * .26);
      p.line(...trail, x, y, blue, strength * .66);
      p.poly(points, violet, strength);
      p.polyline(close(points), lilac, strength);
      p.glow(x, y, 14, violet, strength * .49);
    }
  }
  glints(p, clamp((q - .14) / .86), 42, 25, [violet, blue, lilac, cyan], 43);
  const a = 38 * Math.max(0, 1 - Math.abs(q - .18) * 6.6);
  if (a > 0) {
    p.line(-a, 0, a, 0, white, strength);
    p.line(0, -a * .57, 0, a * .57, cyan, strength);
    p.polyline(diamond(a * .53), lilac, strength * .74);
  }
  if (q > .38) {
    const opacity = (1 - smooth(.47, 1, q)) * .66;
    const y = 6 + 15 * q;
    p.rect(-23 + 12 * q, y, 23 * (1 - q), 2, violet, opacity);
    p.rect(5, -y, 14 * (1 - q), 1, blue, opacity);
    p.rect(-2, 0, 5 * (1 - q), 1, cyan, opacity);
  }
  p.glow(0, 0, 29, cyan, strength * .47);
}

const rows = [
  ["Root assembles", arrival], ["Permissions seal", lock], ["Adjudication strike", breachGlyph],
  ["Audit discharge", (p, q) => auditMuzzle(p, q, false)],
  ["Twin audit discharge", (p, q) => auditMuzzle(p, q, true)],
  ["Blink destination", blinkTarget], ["Blink exit", blinkOut], ["Blink arrival", blinkIn],
  ["Fork branching", fork], ["Fork replication", clone], ["Child materializes", childSpawn],
  ["Child strike", childHit], ["Kill signal", killCharge], ["Child detonation", detonation],
  ["Returning health packet", healPacket], ["Guardian boots", guardianRaise],
  ["Guardian shield panel", shieldTile], ["Guardian shatters", guardCrack],
  ["Guard circuit breaks", shieldBreak], ["Deletion boundary", unsafeRing],
  ["Deleted data", deletedBand], ["Fork phase", forkPhase], ["Sudo phase", sudoPhase],
  ["Root collapses", death], ["Child strike warning", childWarning], ["Child marked for deletion", killReticle],
];

mkdirSync(output, { recursive: true });
mkdirSync(preview, { recursive: true });
const width = cell * atlasColumns;
const height = cell * Math.ceil(rows.length * frames / atlasColumns);
const atlas = new Uint8ClampedArray(width * height * 4);
const raster = createFrame(cell, cell, 0, 0);
const rendered = rows.map(([label, draw], row) => Array.from({ length: frames }, (_, frame) => {
  clearFrame(raster);
  const painter = painterFor(raster).place(cell / 2, cell / 2);
  draw(painter, frame / frames);
  const rgba = composite(raster, new Uint8ClampedArray(cell * cell * 4), null);
  const occupied = rgba.some((color, index) => index % 4 === 3 && color > 12);
  if (!occupied && frame === 4) throw new Error(`${label} vanished at its midpoint`);
  const atlasIndex = row * frames + frame;
  const column = atlasIndex % atlasColumns;
  const line = Math.floor(atlasIndex / atlasColumns);
  for (let y = 0; y < cell; y++) {
    const source = y * cell * 4;
    const target = ((line * cell + y) * width + column * cell) * 4;
    atlas.set(rgba.subarray(source, source + cell * 4), target);
  }
  return rgba;
}));
writeFileSync(join(output, "RootEffects.png"), encodePNG(width, height, atlas));

const cols = 4;
const tileWidth = 228;
const tileHeight = 142;
const previewFrame = (frame) => {
  const sheet = canvas(cols * tileWidth, 58 + Math.ceil(rows.length / cols) * tileHeight, [15, 16, 25, 255]);
  sheet.text("FIGHTER403    ROOT ENCOUNTER EFFECTS", 20, 17, 3, [230, 232, 251]);
  sheet.text("ROOT / FRAME-BY-FRAME REFINEMENT", 22, 39, 2, [141, 157, 196]);
  rendered.forEach((sequence, i) => {
    const x = (i % cols) * tileWidth + 12;
    const y = 58 + Math.floor(i / cols) * tileHeight;
    sheet.rect(x, y, 204, 130, [25, 27, 41, 255]);
    sheet.rect(x, y, 204, 1, [67, 62, 105, 255]);
    sheet.text(rows[i][0].toUpperCase(), x + 11, y + 12, 2);
    if (frame >= 0) {
      sheet.paste(sequence[frame], cell, cell, x + 54, y + 27);
    } else {
      [1, 4, 7].forEach((step, col) => {
        const thumb = canvas(60, 74, [20, 21, 32, 255]);
        thumb.paste(sequence[step], cell, cell, -18, -11);
        sheet.paste(thumb.data, 60, 74, x + 10 + col * 63, y + 36);
      });
    }
  });
  return { color: sheet.data, w: sheet.w, h: sheet.h };
};
const contact = previewFrame(-1);
writeFileSync(join(preview, "root-effects.png"), encodePNG(contact.w, contact.h, contact.color));
const motion = Array.from({ length: frames }, (_, i) => previewFrame(i));
writeVideo(join(preview, "root-effects.mp4"), { ...motion[0], fps: 12, frames: motion }, 1, { loops: 3 });
writeVideo(join(preview, "root-effects.gif"), { ...motion[0], fps: 12, frames: motion }, 1);
