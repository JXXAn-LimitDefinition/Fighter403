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
    glints(p, t, 40, 18, [cyan, violet, lilac], 47, true);
  } else {
    const t = (q - .53) / .47;
    p.ring(0, 0, 16 + 24 * ease.outQuart(t), 3 - 2 * t, lilac, decay(t));
    const span = 34 * (1 - t);
    p.line(-span, 0, span, 0, cyan, decay(t));
    p.glow(0, 0, 23, lilac, .55 * decay(t));
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
  p.glow(x, 0, 19, lilac, strength * .48);
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
  glints(p, q, 37, double ? 20 : 14, double ? [cyan, lilac, blue] : [blue, cyan], 24 + Number(double));
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
  glints(p, q, 32, 17, [blue, violet, lilac], 8);
}

function blinkIn(p, q) {
  const strip = (1 - ease.outQuart(q)) * 22;
  p.polyline(diamond(20 - 14 * q), lilac, decay(q));
  p.poly([[0, -strip], [5 * (1 - q), 0], [0, strip], [-5 * (1 - q), 0]], cyan, decay(q));
  p.line(0, -strip, 0, strip, white, decay(q));
  sparkle(p, 0, 0, 9 * (1 - q), cyan, decay(q));
  p.glow(0, 0, 22, cyan, .57 * decay(q));
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
    p.rect(x - 1, y - 1, 3, 3, white, .4 + .6 * pulse(q * 1.8 + i * .5));
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
    p.glow(x, side * 6, 9, lilac, strength * .37);
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
  p.polyline(diamond(r * .74), violet, strength);
  sparkle(p, 0, 0, 7 * (1 - q), cyan, strength);
  p.glow(0, 0, 18, blue, strength * .47);
}

function childHit(p, q) {
  const radius = 18 + 8 * ease.outQuart(q);
  const strength = decay(q);
  p.arc(0, 0, radius, 4 - q * 2, -.64, .64, lilac, strength);
  p.arc(0, 0, radius - 1, 1, -.49, .49, cyan, strength);
  for (let i = 0; i < 3; i++) {
    sparkle(p, radius - 1 - i * 4, (i - 1) * 6, 2, lilac, strength);
  }
}

function childWarning(p, q) {
  const x = 12 + 5 * q;
  p.polyline([[x - 4, -7], [x, -7], [x + 6, 0], [x, 7], [x - 4, 7]], lilac, .55 + .4 * q);
  p.line(x + 3, 0, x + 8, 0, cyan, .36 + .58 * q);
  p.arc(0, 0, 24, 1.3, -.67, .67, violet, .56);
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
  p.glow(0, 0, 24, rose, .17 + q * .27);
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
  p.polyline(diamond(8 + 4 * pulse(q)), rose, .8);
  p.dot(0, 0, coral, .85);
  p.glow(0, 0, 18, rose, .26);
}

function detonation(p, q) {
  const r = 6 + ease.outQuart(q) * 28;
  const strength = decay(q);
  p.ring(0, 0, r, 3 - 2 * q, claret, strength);
  p.ring(0, 0, r - 2, 1.7, rose, strength);
  p.polyline(diamond(7 + 22 * ease.outQuart(q)), coral, strength);
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
  p.glow(0, 0, 16, rose, fade * .36);
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
    p.glow(x, y, 9, blue, strength * .35);
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
  p.line(5, -10 + 20 * q, 8, -10 + 20 * q, white, .9);
  p.glow(3, 0, 13, blue, .37);
}

function guardCrack(p, q) {
  const s = decay(q);
  const move = ease.outQuart(q) * 16;
  p.polyline([[-13 - move, -12], [-5 - move, -13], [-1 - move, -6], [-7 - move, 1], [-5 - move, 10]], lilac, s);
  p.polyline([[4 + move, -11], [12 + move, -7], [9 + move, 3], [12 + move, 12], [4 + move, 15]], cyan, s);
  p.polyline([[3, -15], [-2, -5], [4, -1], [-4, 5], [0, 11]], white, s);
  p.glow(0, 0, 22, cyan, .42 * s);
  glints(p, q, 36, 20, [blue, cyan, lilac], 29);
}

function shieldBreak(p, q) {
  const r = 16 + 22 * ease.outQuart(q);
  for (let i = 0; i < 4; i++) {
    const a = i * TAU / 4;
    const points = [point(r - q * 5, a - .20), point(r + 3, a - .05), point(r - 2, a + .21)];
    p.polyline(points, i % 2 ? cyan : lilac, decay(q));
    p.glow(...points[1], 9, blue, decay(q) * .31);
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
  p.polyline([[-4, -3], [0, 1], [4, -3]], coral, alpha);
  p.polyline([[-4, 4], [0, 8], [4, 4]], rose, alpha);
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
  p.line(-25, 10, 25, 10, rose, .7);
  p.line(-17, 14, 17, 14, claret, .54);
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
  p.glow(0, 0, 32, violet, s * .48);
  glints(p, q, 41, 24, [claret, rose, lilac, cyan], 61, true);
}

function death(p, q) {
  const radius = 7 + ease.outQuart(q) * 32;
  const strength = decay(q);
  if (q < .6) {
    for (let i = 0; i < 3; i++) {
      const angle = i * TAU / 3 - TAU / 4 + .6 * q;
      const [x, y] = point(radius * .65, angle);
      const points = triangle(9 - q * 9, angle + q).map(([u, v]) => [u + x, v + y]);
      p.poly(points, violet, strength);
      p.polyline(close(points), lilac, strength);
      p.glow(x, y, 14, violet, strength * .49);
    }
  }
  glints(p, q, 42, 40, [violet, blue, lilac, cyan], 43);
  const a = 40 * Math.max(0, 1 - q * 5);
  p.line(-a, 0, a, 0, white, strength);
  p.line(0, -a * .56, 0, a * .56, cyan, strength);
  p.glow(0, 0, 32, cyan, strength * .63);
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
  sheet.text("PIXELFX  /  HAND-AUTHORED TOP-DOWN ANIMATION", 22, 39, 2, [141, 157, 196]);
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
