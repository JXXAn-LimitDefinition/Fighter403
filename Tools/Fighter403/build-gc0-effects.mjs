import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { createHash } from "node:crypto";

const project = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
const pixelFx = resolve(process.env.PIXELFX_HOME ?? join(process.env.USERPROFILE, ".codex/tools/pixelfx"));
const { clamp, composite, createFrame, ease, hex, painterFor, rand, sparkle, TAU } =
  await import(pathToFileURL(join(pixelFx, "src/core/index.js")));
const { encodePNG } = await import(pathToFileURL(join(pixelFx, "tools/lib/png.mjs")));

const copper = ["#20132a", "#58223b", "#ac463b", "#ed7940", "#ffbc61", "#fff3b4", "#fffdeb"].map(hex);
const cold = ["#121b36", "#234263", "#317c91", "#45a8b3", "#7ce1d3", "#c5fff0", "#f0fffc"].map(hex);
const steel = ["#151825", "#283144", "#425470", "#586e8a", "#91a9bc", "#cddcec"].map(hex);

const polar = (angle, radius) => [Math.cos(angle) * radius, Math.sin(angle) * radius];
const out = x => ease.outCubic(clamp(x));
const step = x => Math.round(x * 20) / 20;

function brackets(p, x, y, rx, ry, length, c, a = 1) {
  for (const sx of [-1, 1]) for (const sy of [-1, 1]) {
    const bx = x + sx * rx, by = y + sy * ry;
    p.polyline([[bx - sx * length, by], [bx - sx * 3, by], [bx, by - sy * 3],
      [bx, by - sy * length]], c, a);
  }
}

function segmentedRing(p, radius, rotation, thick, palette, a, count = 8, gap = .14) {
  for (let i = 0; i < count; i++) {
    const start = i * TAU / count + rotation + gap, end = (i + 1) * TAU / count + rotation - gap;
    p.arc(0, 0, radius, thick + 2, start, end, palette[1], a * .80);
    p.arc(0, 0, radius, thick, start, end, palette[3 + (i % 2)], a);
    p.arc(0, 0, radius + 1.5, 1.1, start, start + (end - start) * .64, palette[5], a);
  }
}

function racingBits(p, t, palette, count, maxRadius, seed, spread = TAU, facing = 0, startRadius = 0) {
  for (let i = 0; i < count; i++) {
    const delay = rand(seed, i, 1) * .16;
    const life = .45 + rand(seed, i, 2) * .38;
    const age = (t - delay) / life;
    if (age <= 0 || age >= 1) continue;
    const angle = facing + (rand(seed, i, 3) - .5) * spread;
    const radius = startRadius + (maxRadius - startRadius) * out(age) * (.49 + rand(seed, i, 4) * .51);
    const length = (1 - age) * (2 + rand(seed, i, 5) * 9);
    const point = polar(angle, radius), tail = polar(angle, radius - length);
    const color = palette[age < .25 ? 5 : age < .65 ? 4 : 2];
    p.line(...tail, ...point, palette[2], 1 - age);
    p.rect(point[0], point[1], i % 5 === 0 ? 3 : 2, age < .36 ? 2 : 1, color, 1 - age * .55);
    if (age < .33 && i % 4 === 0) p.glow(...point, 4, palette[3], .16 * (1 - age));
  }
}

const effects = [
  {
    name: "Entrance", size: [160, 160], frames: 20,
    draw(p, t) {
      const a = Math.min(1, t * 9) * (1 - out((t - .61) / .39));
      const r = 62 - 25 * out(t / .40) + 12 * out((t - .45) / .50);
      segmentedRing(p, r, -.35 + step(t) * .25, 3, copper, a, 6);
      for (let i = 0; i < 4; i++) {
        const angle = (i + .5) * TAU / 4;
        const [x, y] = polar(angle, 58 - 26 * out(t / .43));
        const s = 2 + 8 * (1 - out((t - .32) / .52));
        p.rect(x - s / 2, y - 2, s, 4, steel[2], a);
        p.line(x - s / 2, y - 2, x + s / 2, y - 2, copper[5], a);
      }
      const gate = 1 - Math.abs(t - .40) / .11;
      if (gate > 0) {
        const y = -39 + 78 * clamp((t - .29) / .22);
        p.beam(-42, y, 42, y, 4, copper[3], gate);
        p.line(-42, y, 42, y, copper[6], gate);
        p.glowLine(-40, y, 40, y, 10, copper[4], .34 * gate);
      }
      racingBits(p, t - .39, copper, 18, 70, 13, TAU, 0, 27);
    }
  },
  {
    name: "ScanCell", size: [120, 120], frames: 12, hold: true,
    draw(p, t) {
      const lead = out(t / .74), flash = t > .83 ? .94 : .62 + .24 * lead;
      const arm = 9 + lead * 15;
      brackets(p, 0, 0, 55, 55, arm, copper[2], 1);
      brackets(p, 0, 0, 53, 53, arm - 3, copper[4], flash);
      p.disc(0, 0, 47, copper[1], .07 + .06 * lead);
      for (let i = 0; i < 5; i++) {
        const y = 13 + 10 * i;
        const offset = Math.floor(t * 14 + i * 2) % 6;
        for (let j = 0; j < 5; j++) {
          const x = -44 + j * 19 + offset;
          p.line(x, y, x + 5, y, copper[3], .32 + .13 * lead);
        }
      }
      const line = -48 + 92 * lead;
      p.beam(-46, line, 46, line, 3, copper[2], .68);
      p.line(-45, line, 45, line, copper[4], .64 + .26 * lead);
      p.rect(-12, -12, 25, 25, copper[0], .96);
      brackets(p, 0, 0, 11, 11, 7, copper[4], flash);
      p.polyline([[-6, -6], [0, 0], [6, -6]], copper[5], flash);
      p.polyline([[-6, 1], [0, 7], [6, 1]], copper[3], flash);
    }
  },
  {
    name: "ScanBeam", size: [28, 64], frames: 12, loop: true,
    draw(p, t) {
      p.rect(-9, -32, 6, 64, copper[2], .28);
      p.rect(-3, -32, 6, 64, copper[3], 1);
      p.rect(-1, -32, 3, 64, copper[5]);
      p.rect(0, -32, 1, 64, copper[6]);
      p.rect(3, -32, 1, 64, copper[4], .44);
      for (let i = 0; i < 6; i++) {
        const y = ((i * 11 + t * 64) % 64) - 32;
        const reach = 3 + (i * 7) % 8;
        p.line(-reach, y, -3, y, i % 2 ? copper[3] : copper[4], .48);
        p.rect(-reach - 2, y, 2, 1, copper[2], .72);
      }
    }
  },
  {
    name: "ScanIntersection", size: [72, 72], frames: 12, loop: true,
    draw(p, t) {
      const pulse = .70 + Math.sin(t * TAU) * .28;
      const reach = 13 + pulse * 6;
      p.poly([[0, -reach], [reach, 0], [0, reach], [-reach, 0]], copper[1], .74);
      brackets(p, 0, 0, 22, 22, 6 + pulse * 5, copper[4], .80);
      sparkle(p, 0, 0, 17 * pulse + 4, copper[6], copper[4], .72);
      p.glow(0, 0, 21, copper[4], .20 * pulse);
    }
  },
  {
    name: "ScanHit", size: [88, 88], frames: 12,
    draw(p, t) {
      const a = 1 - out(t);
      for (let i = 0; i < 4; i++) {
        const y = (i - 1.5) * 13, x = -9 + i * 5 + out(t) * 22;
        p.rect(x - 3, y - 8, 5, 16, copper[3], a);
        p.line(x, y - 7, x, y + 7, copper[i % 2 ? 5 : 6], a);
      }
      if (t < .17) p.glowLine(-4, -21, -4, 21, 8, copper[5], .40 * a);
      racingBits(p, t, copper, 16, 41, 106, 2.1);
    }
  },
  {
    name: "ChargeChevron", size: [56, 72], frames: 12, hold: true,
    draw(p, t) {
      const a = .55 + .45 * out(t), tip = -4 + Math.floor(t * 8);
      const shape = [[-21, -24], [tip, -24], [21, 0], [tip, 24], [-21, 24], [5 + tip, 0]];
      p.poly(shape, copper[1], .80);
      p.polyline([[-21, -24], [tip, -24], [21, 0], [tip, 24], [-21, 24]], copper[3], a);
      p.polyline([[-8, -17], [tip, -17], [15, 0], [tip, 17], [-8, 17]], copper[5], a);
      for (const sign of [-1, 1]) p.rect(-19, sign * 27, 12, 2, copper[2], a);
    }
  },
  {
    name: "ChargeThrusters", size: [144, 112], frames: 12, loop: true,
    draw(p, t) {
      for (const y of [-23, 23]) {
        const speed = 45 + Math.round(Math.sin((t + y / 9) * TAU * 3) * 13);
        const plume = [[-12, y - 7], [-speed, y - 4], [-66, y], [-speed - 2, y + 5], [-12, y + 7]];
        p.poly(plume, copper[1], .77);
        p.poly([[-12, y - 5], [-speed - 9, y], [-12, y + 5]], copper[3], .90);
        p.poly([[-11, y - 3], [-speed + 4, y], [-11, y + 3]], copper[5]);
        p.line(-speed + 16, y, -8, y, copper[6]);
        p.glowLine(-44, y, -14, y, 8, copper[3], .26);
        for (let i = 0; i < 5; i++) {
          const x = -18 - ((i * 8 + t * 48) % 48);
          const offset = 10 + (i * 3) % 7;
          p.line(x, y + offset, x - 5 - i, y + offset, copper[i % 2 + 3], .63);
        }
      }
    }
  },
  {
    name: "ChargeHit", size: [122, 96], frames: 13,
    draw(p, t) {
      const a = 1 - out(t);
      const x = 5 + out(t) * 25;
      const s = 1 + out(t) * .70;
      for (const offset of [0, -9]) {
        p.polyline([[x - 14 + offset, -23 * s], [x + offset, -6], [x + 3 + offset, 0],
          [x + offset, 6], [x - 14 + offset, 23 * s]], offset === 0 ? copper[5] : copper[2], a);
      }
      if (t < .24) sparkle(p, 0, 0, 18 + 36 * t, copper[6], copper[4], 1 - t / .24);
      racingBits(p, t, copper, 23, 46, 118, 2.45);
    }
  },
  {
    name: "WallImpact", size: [180, 180], frames: 18,
    draw(p, t) {
      const a = 1 - out(t);
      const radius = 9 + out(t) * 70;
      for (const sign of [-1, 1]) {
        const angle = sign * TAU / 4;
        p.arc(0, 0, radius, 3, angle - .7, angle + .7, copper[3], a);
        p.arc(0, 0, radius - 4, 1.6, angle - .55, angle + .55, copper[5], a * .9);
      }
      const flare = 1 - t / .24;
      if (flare > 0) {
        p.poly([[0, -46 * flare], [4, -8], [21, 0], [4, 8], [0, 46 * flare], [-5, 5], [-26, 0], [-5, -5]], copper[4], flare);
        sparkle(p, 0, 0, 26 * flare + 2, copper[6], copper[4], flare);
        p.glow(0, 0, 25, copper[3], .56 * flare);
      }
      racingBits(p, t, copper, 35, 82, 68, 2.58, Math.PI, 6);
      for (let i = 0; i < 5; i++) {
        const sign = i % 2 ? 1 : -1, y = sign * (24 + i * 5) * out(t);
        p.disc(-15 - i * 7, y, 6 + t * 10, steel[4], a * .14);
      }
    }
  },
  {
    name: "WallStagger", size: [120, 120], frames: 14, hold: true,
    draw(p, t) {
      const a = (1 - t * .68) * (.74 + Math.sin(t * TAU * 3) * .24);
      brackets(p, 0, 0, 47, 44, 14, copper[4], a);
      brackets(p, 0, 0, 50, 47, 10, copper[2], a);
      for (const x of [-33, 33]) {
        const direction = Math.sign(x);
        const y = -19 + (t * 72 + direction * 18) % 42;
        const points = [[x, y], [x + direction * 5, y - 3], [x + direction * 2, y - 9], [x + direction * 8, y - 15]];
        p.polyline(points, copper[5], a);
        p.line(x - direction * 2, y, x + direction * 3, y + 3, copper[2], a);
        p.glow(x, y, 12, copper[3], a * .22);
      }
      for (let i = 0; i < 5; i++) {
        const y = 42, x = (i - 2) * 7;
        p.rect(x - 2, y, 4, 4, i < (1 - t) * 5 ? copper[4] : copper[1], a);
      }
    }
  },
  {
    name: "FragmentSpawn", size: [88, 72], frames: 16,
    draw(p, t) {
      const a = Math.min(1, t * 12) * (1 - out((t - .57) / .43));
      const shift = 21 * out(t / .52);
      const top = -19 * out(t / .53), h = 11 + 11 * out(t / .4);
      for (const direction of [-1, 1]) {
        const x = direction * shift;
        p.poly([[x, -h], [x + direction * 15, -h], [x + direction * 21, -h + 6],
          [x + direction * 21, h - 6], [x + direction * 15, h], [x, h]], steel[1], a);
        p.polyline([[x, h], [x, -h], [x + direction * 15, -h]], cold[4], a);
        p.line(x + direction * 3, -h + 4, x + direction * 3, h - 4, cold[2], a);
      }
      p.line(-shift, top, shift, top, cold[5], a);
      const pulse = clamp((1 - Math.abs(t - .40) / .22));
      if (pulse > 0) sparkle(p, 0, 0, 16 * pulse + 2, cold[6], cold[4], pulse);
      racingBits(p, t - .17, cold, 14, 41, 79);
    }
  },
  {
    name: "FragmentConsume", size: [138, 138], frames: 18,
    draw(p, t) {
      const glow = 1 - Math.abs(t - .31) / .24;
      if (glow > 0) p.glow(0, 0, 23, cold[4], glow * .31);
      for (const sign of [-1, 1]) {
        const shift = sign * (53 - out(t / .42) * 41);
        const a = 1 - out((t - .43) / .41);
        p.polyline([[shift, -22], [shift - sign * 5, -15], [shift - sign * 5, 15], [shift, 22]], cold[3], a);
        p.polyline([[shift + sign * 5, -13], [shift + sign * 11, 0], [shift + sign * 5, 13]], cold[5], a);
        for (let i = 0; i < 4; i++) {
          const y = i * 9 - 13;
          p.rect(shift + sign * (15 + i % 2 * 5), y, 3, 2, cold[i % 3 + 3], a * .84);
        }
      }
      if (t > .27) {
        const a = 1 - out((t - .27) / .67);
        const r = 24 + out((t - .27) / .67) * 31;
        p.line(0, -r, 0, -r + 8, cold[5], a);
        p.line(-4, -r + 4, 4, -r + 4, cold[5], a);
        const shape = [[0, -r], [r, 0], [0, r], [-r, 0], [0, -r]];
        p.polyline(shape, cold[4], a * .78);
        racingBits(p, t - .27, cold, 12, 63, 92);
      }
    }
  },
  {
    name: "FragmentBreak", size: [86, 86], frames: 14,
    draw(p, t) {
      const plume = clamp(1 - t * 1.6);
      const radius = 7 + out(t) * 31;
      for (let i = 0; i < 5; i++) {
        const angle = (i + .33) * TAU / 5;
        const [x, y] = polar(angle, radius * (.49 + rand(81, i) * .42));
        p.rect(x - 3, y - 3, 6 - t * 4, 5 - t * 3, steel[3], plume);
        p.line(x - 3, y - 3, x + 1, y - 3, cold[5], plume);
      }
      if (t < .27) sparkle(p, 0, 0, 14 - t * 24, cold[6], cold[4], 1 - t / .27);
      racingBits(p, t, cold, 14, 38, 86);
    }
  },
  {
    name: "StopWarning", size: [158, 158], frames: 16, hold: true,
    draw(p, t) {
      const r = 66 - out(t) * 16;
      segmentedRing(p, r, .18 - step(t) * .38, 2.5, cold, .80, 6);
      for (let i = 0; i < 18; i++) {
        const angle = i * TAU / 18;
        const a = .20 + .78 * clamp(t * 1.55 - i / 18);
        p.line(...polar(angle, r + 4), ...polar(angle, r + 9), cold[i % 3 === 0 ? 5 : 3], a);
      }
      const marker = (Math.floor(t * 12) / 12) * TAU - Math.PI / 2;
      p.arc(0, 0, r - 7, 2, -Math.PI / 2, marker + .04, cold[5], .92);
      p.glow(...polar(marker, r - 7), 7, cold[5], .28);
    }
  },
  {
    name: "StopWave", size: [192, 192], frames: 18,
    draw(p, t) {
      const a = 1 - out(t);
      const r = 29 + out(t) * 57;
      segmentedRing(p, r, .15, 4 - t * 3, cold, a, 4, .06);
      brackets(p, 0, 0, r * .80, r * .80, 14, cold[4], a);
      if (t < .13) {
        const flare = 1 - t / .13;
        p.ring(0, 0, 38 + t * 70, 3, cold[6], flare);
        p.glowRing(0, 0, 38 + t * 70, 9, cold[5], .25 * flare);
      }
      racingBits(p, t, cold, 28, 91, 103, TAU, 0, 33);
    }
  },
  {
    name: "StopLock", size: [140, 140], frames: 16, hold: true,
    draw(p, t) {
      const a = t < .85 ? .81 : .81 * (1 - .55 * (t - .85) / .15);
      segmentedRing(p, 47, Math.PI / 4, 3, cold, a, 4, .16);
      brackets(p, 0, 0, 47, 47, 14, cold[4], a);
      const y = -39;
      p.rect(-11, y - 12, 22, 16, cold[0], .91);
      p.rect(-7, y - 9, 4, 10, cold[5], a);
      p.rect(3, y - 9, 4, 10, cold[5], a);
      const y2 = 38;
      for (let i = 0; i < 10; i++) p.rect(-27 + i * 6, y2, 4, 2,
        i / 10 < 1 - t ? cold[4] : cold[1], a);
    }
  },
  {
    name: "FragmentLock", size: [34, 34], frames: 8, hold: true,
    draw(p, t) {
      brackets(p, 0, 0, 14, 14, 7, cold[4], .83);
      p.rect(-6, -15, 12, 8, cold[0], .87);
      p.rect(-4, -13, 2, 4, cold[5], .9 - t * .25);
      p.rect(2, -13, 2, 4, cold[5], .9 - t * .25);
    }
  },
  {
    name: "PhaseShift", size: [194, 194], frames: 20,
    draw(p, t) {
      const a = Math.min(1, t * 11) * (1 - out((t - .36) / .64));
      for (const direction of [-1, 1]) {
        const r = 25 + out(t / .72) * 52 + direction * 5;
        segmentedRing(p, r, direction * step(t) * .40, 3.5, direction > 0 ? copper : steel, a, 6, .12);
      }
      for (let i = 0; i < 6; i++) {
        const angle = i * TAU / 6;
        const [x, y] = polar(angle, 46 + 35 * out(t));
        p.poly([[x, y - 6], [x + 7, y], [x, y + 6], [x - 7, y]], copper[3], a);
        p.polyline([[x - 7, y], [x, y - 6], [x + 7, y]], copper[5], a);
      }
      const line = 1 - Math.abs(t - .23) / .15;
      if (line > 0) {
        p.line(-61, 0, 61, 0, copper[6], line);
        p.line(0, -61, 0, 61, copper[6], line);
        p.glow(0, 0, 29, copper[5], .37 * line);
      }
      racingBits(p, t - .13, copper, 25, 90, 31, TAU, 0, 30);
    }
  },
  {
    name: "Defeat", size: [216, 216], frames: 24,
    draw(p, t) {
      const fracture = 1 - out((t - .06) / .64);
      const zoom = out(t / .78);
      for (let i = 0; i < 7; i++) {
        const angle = (i + rand(81, i) * .24) * TAU / 7, radius = 13 + zoom * (34 + rand(45, i) * 46);
        const [x, y] = polar(angle, radius), size = (8 + rand(52, i) * 11) * (1 - .65 * zoom);
        const skew = Math.cos(angle + t * 8) * size * .42;
        p.poly([[x - size, y - size * .3], [x + skew, y - size * .65], [x + size, y],
          [x + size * .4, y + size * .65], [x - size * .7, y + size * .35]], steel[i % 3 + 1], fracture);
        p.polyline([[x - size, y - size * .3], [x + skew, y - size * .65], [x + size, y]], copper[5], fracture);
        p.line(x - size * .4, y + size * .1, x + size * .5, y + size * .1, copper[3], fracture);
      }
      const flash = 1 - out(t / .23);
      if (flash > 0) {
        sparkle(p, 0, 0, 32 + t * 46, copper[6], copper[4], flash);
        p.glow(0, 0, 45, copper[3], .67 * flash);
      }
      const ring = 1 - out(t / .65);
      segmentedRing(p, 24 + 68 * out(t / .75), .09 + t * .64, 3, copper, ring, 7, .10);
      racingBits(p, t, copper, 39, 103, 50);
      for (let i = 0; i < 6; i++) {
        const [x, y] = polar(i * TAU / 6, 8 + zoom * (39 + i * 3));
        const haze = Math.sin(t * Math.PI) * .085, radius = 8 + zoom * 14;
        p.disc(x, y, radius, i < 2 ? copper[2] : steel[4], haze);
        p.disc(x + 4, y - 5, radius * .62, steel[4], haze * .44);
      }
    }
  }
];

const downloads = process.argv[2];
const assetRoot = join(project, "Assets/Resources/Fighter403/Bosses/GC0Effects");
const template = readFileSync(join(assetRoot, "../GC0.png.meta"), "utf8");
const guidFor = name => createHash("md5").update("Fighter403.GC0Effects/" + name).digest("hex");
mkdirSync(assetRoot, { recursive: true });
writeFileSync(assetRoot + ".meta", `fileFormatVersion: 2\nguid: ${guidFor("folder")}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n`);

const render = (effect, progress) => {
  const [w, h] = effect.size;
  const frame = createFrame(w, h);
  const p = painterFor(frame);
  p.place(w / 2, h / 2, false, 0);
  if (effect.loop || effect.hold || progress < 1) effect.draw(p, progress);
  return composite(frame, Buffer.alloc(w * h * 4));
};

for (const effect of effects) {
  const [w, h] = effect.size, columns = Math.min(6, effect.frames), rows = Math.ceil(effect.frames / columns);
  const data = Buffer.alloc(w * columns * h * rows * 4);
  for (let n = 0; n < effect.frames; n++) {
    const pixels = render(effect, n / (effect.loop ? effect.frames : effect.frames - 1));
    const x = n % columns * w, y = Math.floor(n / columns) * h;
    for (let line = 0; line < h; line++) {
      const target = ((y + line) * w * columns + x) * 4;
      pixels.copy(data, target, line * w * 4, (line + 1) * w * 4);
    }
  }
  writeFileSync(join(assetRoot, effect.name + ".png"), encodePNG(w * columns, h * rows, data));
  const meta = template.replace(/^guid: .*$/m, "guid: " + guidFor(effect.name))
    .replace("spriteMode: 1", "spriteMode: 0").replace("textureType: 8", "textureType: 0")
    .replaceAll(/^( +\w+:) +$/gm, "$1");
  writeFileSync(join(assetRoot, effect.name + ".png.meta"), meta);
}

if (downloads) {
  mkdirSync(downloads, { recursive: true });
  const width = 1152, rowHeight = 93, height = rowHeight * effects.length + 32;
  const contact = createFrame(width, height);
  const labels = painterFor(contact);
  labels.text("GC-0 / EFFECT DEVELOPMENT", 16, 11, steel[5], { font: "big", scale: 2 });
  const fill = composite(contact, Buffer.alloc(width * height * 4), hex("#101722"));
  for (const [i, effect] of effects.entries()) {
    const [w, h] = effect.size;
    const size = h <= 72 ? 1 : Math.min(.74, (rowHeight - 9) / h), top = i * rowHeight + 33;
    labels.text(effect.name, 16, top + 32, steel[5], { font: "mini", scale: 2 });
    for (let n = 0; n < 7; n++) {
      const sample = render(effect, effect.loop ? n / 7 : (n + 1) / 8.25);
      const sw = Math.floor(w * size), sh = Math.min(rowHeight - 7, Math.floor(h * size));
      const left = Math.floor(219 + n * 139 - sw / 2), sy = Math.max(0, (Math.floor(h * size) - sh) / 2);
      for (let y = 0; y < sh; y++) for (let x = 0; x < sw; x++) {
        const source = (Math.floor((y + sy) / size) * w + Math.floor(x / size)) * 4;
        const dest = ((top + y) * width + left + x) * 4;
        const a = sample[source + 3] / 255;
        for (let channel = 0; channel < 3; channel++)
          fill[dest + channel] = Math.round(fill[dest + channel] * (1 - a) + sample[source + channel] * a);
      }
    }
  }
  const text = composite(contact, Buffer.alloc(width * height * 4));
  for (let i = 0; i < text.length; i += 4) {
    if (text[i + 3]) text.copy(fill, i, i, i + 4);
  }
  writeFileSync(join(downloads, "gc0-effects-contact.png"), encodePNG(width, height, fill));
}
