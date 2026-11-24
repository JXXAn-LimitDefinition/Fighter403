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
    if (age < .30 && i % 5 === 0) p.blend(point[0] + 3, point[1], palette[3], .35 * (1 - age));
  }
}

const effects = [
  {
    name: "Entrance", size: [160, 160], frames: 20,
    draw(p, t) {
      const load = out(t / .42), fade = 1 - out((t - .57) / .43);
      const light = clamp(3 * t + .16) * fade;
      const radius = 59 - load * 22;
      for (let i = 0; i < 4; i++) {
        const angle = Math.PI / 2 * i;
        const [x, y] = polar(angle, radius);
        const blade = [[-8,-4],[8,-4],[12,0],[8,4],[-8,4],[-12,0]]
          .map(([u,v]) => [x + Math.cos(angle) * v - Math.sin(angle) * u, y + Math.sin(angle) * v + Math.cos(angle) * u]);
        p.poly(blade, steel[1], light);
        p.polyline(blade.slice(0, 4), steel[4], light);
        p.line(...polar(angle, radius + 8), ...polar(angle, radius + 16), copper[4], light);
        p.line(...polar(angle, radius - 13), ...polar(angle, radius - 6), cold[4], light);
      }
      for (let i = 0; i < 8; i++) {
        const angle = (i + .5) * TAU / 8;
        const a = clamp((load * 8 - i) * .7) * fade;
        p.arc(0, 0, radius + 6, 1.25, angle - .16, angle + .16, steel[4], a * .72);
        p.arc(0, 0, radius + 2, 1.5, angle - .14, angle + .14, copper[4], a);
      }
      const scan = clamp((t - .19) / .35);
      const slit = Math.sin(scan * Math.PI) * .8;
      if (slit > 0) {
        const y = -33 + scan * 66;
        p.rect(-32, y - 2, 64, 5, copper[2], slit * .34);
        p.line(-31, y, 31, y, copper[5], slit);
        p.rect(-20, y - 1, 40, 1, copper[4], slit * .66);
      }
      racingBits(p, t - .37, copper, 14, 68, 13, TAU, 0, 34);
    }
  },
  {
    name: "ScanCell", size: [120, 120], frames: 12, hold: true,
    draw(p, t) {
      const lock = out(t / .40);
      const active = clamp((t - .72) / .18);
      p.poly([[-54,-48],[-48,-54],[48,-54],[54,-48],[54,48],[48,54],[-48,54],[-54,48]], copper[0], .19 + active * .19);
      const radius = 56 - lock * 6, arm = 8 + lock * 12;
      brackets(p, 0, 0, radius + 2, radius + 2, arm, copper[1], .98);
      brackets(p, 0, 0, radius, radius, arm - 2, copper[active > .6 ? 5 : 3], .71 + active * .27);
      brackets(p, 0, 0, radius - 1, radius - 1, arm - 5, copper[3], .52 + active * .35);
      for (const side of [-1, 1]) {
        for (let i = 0; i < 3; i++) {
          const x = -8 + i * 7;
          p.rect(x, side * 45 - 1, 4, 2, copper[3 + (i / 3 < lock ? 1 : 0)], .42 + active * .33);
          p.rect(side * 46 - 1, x, 2, 4, copper[3], .48);
        }
      }
      for (let row = 0; row < 7; row++) for (let col = 0; col < 7; col++) {
        const x = -40 + col * 12, y = -37 + row * 12;
        const alpha = active * (.19 + ((row + col) % 3 === 0 ? .28 : .0));
        p.line(x, y + 4, x + 4, y, copper[3], alpha);
      }
      const scan = clamp((t - .24) / .52);
      if (scan > 0 && scan < 1) {
        const y = -42 + 84 * scan;
        p.rect(-46, y - 2, 92, 4, copper[2], .55);
        p.line(-42, y, 42, y, copper[5], .84);
        p.rect(-46, y - 2, 4, 4, copper[4], .94);
        p.rect(42, y - 2, 4, 4, copper[4], .94);
      }
      if (lock > .5) {
        p.poly([[0,-14],[14,0],[0,14],[-14,0]], copper[0], .98);
        p.polyline([[0,-14],[14,0],[0,14],[-14,0],[0,-14]], copper[3], .65 + active * .3);
        p.rect(-1, -7, 3, 8, copper[active > .55 ? 5 : 4]);
        p.rect(-1, 4, 3, 3, copper[active > .55 ? 5 : 4]);
      }
    }
  },
  {
    name: "ScanBeam", size: [28, 64], frames: 12, loop: true,
    draw(p, t) {
      const drift = Math.floor(t * 64);
      p.rect(-10, -32, 20, 64, copper[0], .32);
      p.rect(-6, -32, 12, 64, copper[2], .68);
      p.rect(-3, -32, 6, 64, copper[3], .97);
      p.rect(-1, -32, 2, 64, copper[5]);
      p.rect(4, -32, 1, 64, copper[4], .75);
      for (let i = 0; i < 8; i++) {
        const y = ((i * 8 + drift) % 64) - 32;
        const reach = 3 + (i * 7) % 7;
        p.rect(-reach, y, reach - 3, 1, copper[4], .72);
        p.rect(5 + i % 3, y, 2, 1, copper[3], .58);
        p.rect(0, y, 2, 1, copper[6], .72);
      }
    }
  },
  {
    name: "ScanIntersection", size: [72, 72], frames: 12, loop: true,
    draw(p, t) {
      const pulse = Math.sin(t * TAU) * .5 + .5;
      p.poly([[0,-20],[20,0],[0,20],[-20,0]], copper[1], .82);
      p.polyline([[0,-20],[20,0],[0,20],[-20,0],[0,-20]], copper[3], .75);
      brackets(p, 0, 0, 24, 24, 8 + 3 * pulse, copper[5], .78);
      const r = 11 + 4 * pulse;
      p.poly([[0,-r],[4,-4],[r,0],[4,4],[0,r],[-4,4],[-r,0],[-4,-4]], copper[4], .9);
      p.poly([[0,-7],[7,0],[0,7],[-7,0]], copper[6], .93);
      for (let i = 0; i < 4; i++) {
        const [x, y] = polar(TAU * (i + .5) / 4, 15 + pulse * 4);
        p.rect(x - 1, y - 1, 2, 2, copper[4], .75);
      }
    }
  },
  {
    name: "ScanHit", size: [88, 88], frames: 12,
    draw(p, t) {
      const speed = out(t), reach = 4 + speed * 29;
      const a = (1 - t) ** 2.7;
      for (let i = 0; i < 3; i++) {
        const y = (i - 1) * 13, x = i * 3 - 10 + reach;
        p.poly([[x - 8,y - 7],[x + 12,y - 6],[x + 6,y + 6],[x - 12,y + 7]], copper[2], a * .87);
        p.line(x - 6, y - 5, x + 7, y - 4, copper[4], a);
        p.line(x + 8, y - 4, x + 3, y + 4, copper[5], a);
      }
      const cut = 1 - clamp(t / .23);
      p.poly([[-9,-22],[-2,-10],[4,8],[8,22],[-5,12],[-10,-8]], copper[4], cut);
      p.line(-8, -20, 6, 20, copper[6], cut);
      racingBits(p, t, copper, 13, 40, 106, 1.9);
    }
  },
  {
    name: "ChargeChevron", size: [56, 72], frames: 12, hold: true,
    draw(p, t) {
      for (let i = 0; i < 3; i++) {
        const lead = clamp(t * 3.55 - i + .3);
        const x = -23 + i * 13 + Math.floor(lead * 4);
        const h = 11 + i * 5;
        const r = 10 + i * 2;
        const shape = [[x,-h],[x+5,-h],[x+r,0],[x+5,h],[x,h],[x+r-5,0]];
        p.poly(shape, copper[1 + (lead > .62 ? 1 : 0)], .48 + lead * .48);
        p.polyline(shape.slice(0, 5), copper[lead > .82 ? 5 : 3], .37 + lead * .61);
        if (lead > .60) p.line(x + r - 3, -4, x + r - 1, 0, copper[6], .69 * lead);
      }
      for (const sign of [-1, 1]) {
        p.rect(-20, sign * 25 - 1, 19, 2, copper[1], .92);
        p.rect(-20, sign * 25 - 1, 19 * out(t), 2, copper[3], .87);
      }
    }
  },
  {
    name: "ChargeThrusters", size: [144, 112], frames: 12, loop: true,
    draw(p, t) {
      for (const y of [-23, 23]) {
        const pulse = .5 + Math.sin((t * 2 + (y < 0 ? .28 : .0)) * TAU) * .5;
        const edge = -51 - 13 * pulse;
        p.poly([[-9,y-7],[-33,y-8],[edge+6,y-4],[-70,y],[edge+3,y+4],[-38,y+7],[-9,y+7]], copper[1], .72);
        p.poly([[-9,y-5],[-29,y-6],[edge,y],[-40,y+3],[-27,y+5],[-9,y+5]], copper[3], .95);
        p.poly([[-9,y-3],[-21,y-4],[edge+12,y],[-26,y+3],[-9,y+3]], copper[5], .95);
        p.rect(-20, y - 1, 13, 2, copper[6], .91);
        for (let i = 0; i < 4; i++) {
          const x = -26 - ((i / 4 + t) % 1) * 31;
          const a = .9 - (-x - 26) / 47;
          p.polyline([[x-4,y-4],[x,y],[x-4,y+4]], copper[i % 2 ? 5 : 4], a);
        }
        for (let i = 0; i < 6; i++) {
          const age = (i / 6 + t) % 1;
          const x = -18 - age * 46;
          const offset = (i % 2 ? -1 : 1) * (8 + i % 3 * 3);
          p.line(x, y + offset, x - 3 - i % 3, y + offset, copper[age < .5 ? 4 : 2], .77 * (1 - age));
        }
      }
    }
  },
  {
    name: "ChargeHit", size: [122, 96], frames: 13,
    draw(p, t) {
      const a = (1 - t) ** 2.4, x = out(t) * 30;
      for (let i = 0; i < 3; i++) {
        const at = x - i * 9, h = (17 + 17 * t) * (1 - i * .25);
        p.polyline([[at-12,-h],[at-4,-h*.7],[at+4,-5],[at+6,0],[at+4,5],[at-4,h*.7],[at-12,h]],
          copper[i === 0 ? 4 : 2], a * (1 - i * .24));
      }
      const contact = 1 - out(t / .24);
      p.poly([[-13,-6],[13,-4],[29,0],[13,4],[-13,6],[-6,0]], copper[4], contact);
      p.line(-8, 0, 24, 0, copper[6], contact);
      racingBits(p, t, copper, 21, 49, 118, 1.75);
    }
  },
  {
    name: "WallImpact", size: [180, 180], frames: 18,
    draw(p, t) {
      const contact = 1 - out(t / .34);
      const squeeze = 35 + out(t / .31) * 24;
      p.poly([[0,-squeeze],[3,-squeeze*.31],[2,0],[3,squeeze*.31],[0,squeeze],[-2,squeeze*.33],[-4,0],[-2,-squeeze*.33]], copper[3], contact);
      p.line(0, -squeeze * .68, 0, squeeze * .68, copper[5], contact);
      p.rect(-1, -8, 3, 17, copper[6], contact * .91);
      racingBits(p, t, copper, 29, 82, 68, 2.62, Math.PI, 3);
      for (const sign of [-1, 1]) for (let i = 0; i < 4; i++) {
        const age = clamp((t - i * .08) / .63);
        const a = Math.sin(age * Math.PI) * .7;
        const x = -4 - age * (21 + 7 * i), y = sign * (13 + 49 * age);
        p.polyline([[x + 4, y - sign * 7], [x, y], [x + 5, y + sign * 3]], copper[i % 2 ? 2 : 4], a);
      }
    }
  },
  {
    name: "WallStagger", size: [120, 120], frames: 14, hold: true,
    draw(p, t) {
      const jitter = Math.round(Math.sin(t * TAU * 4) * 3 * (1 - t));
      brackets(p, 0, 0, 49, 46, 13, steel[3], .88);
      for (const sign of [-1, 1]) {
        const x = sign * 44;
        p.poly([[x,-21],[x+sign*7,-16],[x+sign*7,16],[x,21]], copper[1], .97);
        p.line(x, -21, x, 21, copper[3], .78);
        const y = jitter + sign * 6;
        p.polyline([[x+sign*2,y-10],[x-sign*6,y-3],[x,y+1],[x-sign*5,y+11]], copper[4], .92 - t * .43);
        p.rect(x + sign * 3, y - 3, 2, 5, copper[5], .68 - t * .31);
      }
      p.rect(-23, 41, 46, 7, steel[0], .99);
      for (let i = 0; i < 7; i++) {
        p.rect(-20 + i * 6, 43, 4, 3, i < Math.ceil((1 - t) * 7) ? copper[4] : copper[2], .86);
      }
    }
  },
  {
    name: "FragmentSpawn", size: [88, 72], frames: 16,
    draw(p, t) {
      const open = out((t - .08) / .48), fade = 1 - out((t - .65) / .35);
      const lit = .32 + .68 * out(t / .2);
      const h = 15 + Math.round(open * 9);
      p.poly([[-21,-h+8],[21,-h+8],[29,0],[21,h-8],[-21,h-8],[-29,0]], cold[0], lit * fade * .87);
      for (const direction of [-1, 1]) {
        const x = direction * (3 + 23 * open);
        const shape = [[x,-h], [x+direction*8,-h], [x+direction*16,-h+7],
          [x+direction*16,h-7], [x+direction*8,h], [x,h]];
        p.poly(shape, steel[1], lit * fade);
        p.polyline(shape.slice(0, 5), steel[4], lit * fade);
        p.rect(x + (direction < 0 ? -2 : 0), -h + 1, 2, h * 2 - 2, cold[4], lit * fade);
        for (let i = 0; i < 4; i++)
          p.rect(x + direction * 5 - 1, -h + 7 + i * 8, 3, 2, cold[i / 4 < open ? 5 : 2], fade);
      }
      const reveal = clamp((t - .18) / .48);
      const scan = Math.sin(reveal * Math.PI);
      const y = -14 + reveal * 26, width = 4 + open * 20;
      p.line(-width, y, width, y, cold[5], scan * fade);
      for (let i = 0; i < 4; i++) {
        const age = clamp((t - .37 - i * .08) / .45);
        const y = 18 - 39 * age, x = (i - 1.5) * 6;
        p.rect(x, y, 3, 3, cold[i % 2 ? 4 : 5], Math.sin(age * Math.PI));
      }
    }
  },
  {
    name: "FragmentConsume", size: [138, 138], frames: 18,
    draw(p, t) {
      const shut = out(t / .49), fade = 1 - out((t - .53) / .47);
      const radius = 50 - 42 * shut;
      for (let i = 0; i < 4; i++) {
        const angle = (i + .5) * TAU / 4, [x, y] = polar(angle, radius);
        const [u, v] = polar(angle, radius + 11 - shut * 7);
        p.poly([[x,y],[u+4,v-3],[u+7,v+2],[u-1,v+5]], steel[2], fade * .92);
        p.line(x, y, u + 3, v - 2, cold[4], fade);
      }
      for (let i = 0; i < 14; i++) {
        const delay = i % 5 * .032, age = clamp((t - delay) / .62);
        const angle = i * TAU / 14 + age * 1.2;
        const distance = 58 * (1 - out(age)) + 6;
        const [x, y] = polar(angle, distance), [u, v] = polar(angle - .14, distance + 8 * (1 - age));
        const visibility = Math.sin(age * Math.PI) * fade;
        p.line(u, v, x, y, cold[3], visibility * .82);
        p.rect(x - 1, y - 1, 3, 2, cold[i % 4 === 0 ? 5 : 4], visibility);
      }
      const seal = clamp((t - .38) / .31);
      if (seal > 0) {
        const r = 14 - 9 * seal, light = Math.sin(seal * Math.PI) * fade;
        p.poly([[0,-r],[r,0],[0,r],[-r,0]], cold[3], light);
        p.rect(-1, -5, 2, 11, cold[6], light);
        p.line(-5, 0, 5, 0, cold[5], light * .68);
      }
    }
  },
  {
    name: "FragmentBreak", size: [86, 86], frames: 14,
    draw(p, t) {
      const launch = out(t / .76), fade = 1 - out((t - .12) / .76);
      for (let i = 0; i < 6; i++) {
        const angle = TAU * (i + .38) / 6;
        const radius = 5 + launch * (18 + rand(81, i) * 12);
        const [x, y] = polar(angle, radius);
        const size = (6 + i % 3 * 2) * (1 - launch * .48);
        const skew = Math.sin(angle + launch * 4) * size;
        p.poly([[x-size,y-2],[x+skew,y-size],[x+size,y+1],[x+1,y+size*.65]], steel[3], fade);
        p.line(x-size, y-2, x+skew, y-size, cold[4], fade);
        p.rect(x, y, 2, 1, cold[6], fade * .69);
      }
      const pulse = 1 - out(t / .24);
      p.poly([[-5,-16],[1,-5],[-2,-1],[6,13],[0,7],[1,1],[-4,-3]], cold[5], pulse);
      racingBits(p, t, cold, 12, 39, 86);
    }
  },
  {
    name: "StopWarning", size: [158, 158], frames: 16, hold: true,
    draw(p, t) {
      const r = 57, arcStart = -Math.PI / 2;
      for (let i = 0; i < 12; i++) {
        const angle = arcStart + i * TAU / 12;
        const hot = i / 12 <= t;
        p.arc(0, 0, r, 3, angle - .11, angle + .11, hot ? cold[3] : cold[1], .83);
        p.arc(0, 0, r - 2, 1.1, angle - .09, angle + .09, hot ? cold[5] : cold[2], .91);
        p.line(...polar(angle, r + 4), ...polar(angle, r + (i % 3 ? 8 : 12)), cold[i % 3 ? 3 : 4], .61);
      }
      const tick = arcStart + Math.min(11, Math.floor(t * 12)) * TAU / 12;
      const [x, y] = polar(tick, r - 10);
      p.poly([[x-4,y],[x,y-4],[x+4,y],[x,y+4]], cold[4], .93);
      p.rect(x - 1, y - 1, 2, 2, cold[6], .88);
      const hourglass = [[-8,-48],[8,-48],[5,-44],[1,-40],[4,-36],[8,-31],[-8,-31],[-4,-36],[-1,-40],[-5,-44],[-8,-48]];
      p.poly(hourglass, cold[0], .93);
      p.polyline(hourglass, cold[4], .9);
      p.rect(-3, -36 + Math.floor(t * 2), 6, 2, cold[5], .94);
    }
  },
  {
    name: "StopWave", size: [192, 192], frames: 18,
    draw(p, t) {
      const travel = out(t), a = (1 - t) ** 1.30;
      const radius = 31 + travel * 49;
      const octagon = angle => [...polar(angle, radius)];
      for (let i = 0; i < 8; i++) {
        const angle = i * TAU / 8 + Math.PI / 8;
        const start = octagon(angle), end = octagon(angle + TAU / 8);
        p.beam(...start, ...end, 5 - t * 3, cold[2], a * .75);
        p.line(...start, ...end, cold[i % 2 ? 4 : 5], a * .84);
        p.rect(start[0] - 2, start[1] - 2, 4, 4, cold[5], a * .71);
      }
      for (const axis of [0, TAU / 4, TAU / 2, TAU * 3 / 4]) {
        p.line(...polar(axis, radius - 7), ...polar(axis, radius + 8), cold[4], a);
      }
      for (let i = 0; i < 12; i++) {
        const angle = TAU * i / 12 + .16, r = radius - 9 - 5 * (i % 3);
        const [x, y] = polar(angle, r);
        p.rect(x, y, i % 2 ? 2 : 4, 1, cold[i % 3 ? 3 : 5], a * .65);
      }
    }
  },
  {
    name: "StopLock", size: [140, 140], frames: 16, hold: true,
    draw(p, t) {
      const catchUp = out(t / .28), a = .92 - t * .30;
      const radius = 47 + (1 - catchUp) * 7;
      brackets(p, 0, 0, radius + 2, radius + 2, 22, cold[1], .95);
      brackets(p, 0, 0, radius, radius, 18, cold[4], a);
      for (const sign of [-1, 1]) {
        p.poly([[sign*radius,-11],[sign*(radius+6),-7],[sign*(radius+6),7],[sign*radius,11]], cold[2], .83);
        p.line(sign * radius, -11, sign * radius, 11, cold[4], a);
      }
      p.poly([[-13,-44],[-9,-48],[9,-48],[13,-44],[13,-27],[-13,-27]], cold[0], .95);
      p.line(-9, -48, 9, -48, cold[2], .82);
      p.rect(-7, -43, 4, 11, cold[5], a);
      p.rect(3, -43, 4, 11, cold[5], a);
      for (let i = 0; i < 8; i++) {
        p.rect(-22 + i * 6, 43, 3, 2, i / 8 < 1 - t ? cold[4] : cold[2], a);
      }
    }
  },
  {
    name: "FragmentLock", size: [34, 34], frames: 8, hold: true,
    draw(p, t) {
      const radius = 15 - out(t / .65) * 3;
      brackets(p, 0, 1, radius, radius, 6, cold[4], .83);
      p.poly([[-7,-14],[-5,-16],[5,-16],[7,-14],[7,-7],[-7,-7]], cold[0], .96);
      p.line(-5, -16, 5, -16, cold[3], .78);
      p.rect(-4, -14, 2, 5, cold[5], .92);
      p.rect(2, -14, 2, 5, cold[5], .92);
      for (const sign of [-1, 1]) p.rect(sign * 14 - 1, 0, 2, 2, cold[sign < 0 ? 3 : 4], .84);
    }
  },
  {
    name: "PhaseShift", size: [194, 194], frames: 20,
    draw(p, t) {
      const unlock = out(t / .53), settle = out((t - .60) / .40);
      const a = clamp(t * 9 + .16) * (1 - settle);
      for (let i = 0; i < 6; i++) {
        const angle = (i + .5) * TAU / 6;
        const r = 36 + 20 * unlock - 12 * settle;
        const toWorld = ([u,v]) => [Math.cos(angle) * (r + v) - Math.sin(angle) * u,
          Math.sin(angle) * (r + v) + Math.cos(angle) * u];
        const plate = [[-14,-6],[-10,-11],[10,-11],[14,-6],[10,8],[-10,8]];
        p.poly(plate.map(toWorld), steel[1 + i % 2], a * .95);
        p.polyline(plate.slice(0, 4).map(toWorld), steel[4], a * .88);
        p.line(...toWorld([-8,-6]), ...toWorld([8,-6]), copper[3], a);
        p.line(...toWorld([-6,-4]), ...toWorld([4,-4]), copper[5], a * .82);
        for (let j = 0; j < 3; j++)
          p.line(...toWorld([j * 4 - 4, 0]), ...toWorld([j * 4 - 4, 4]), cold[j / 3 < unlock ? 4 : 2], a);
      }
      const ignite = clamp((t - .24) / .57);
      const arcAlpha = Math.sin(ignite * Math.PI) * .9;
      segmentedRing(p, 43 + ignite * 39, step(t) * .24, 2, copper, arcAlpha, 6, .20);
      racingBits(p, t - .25, copper, 25, 89, 31, TAU, 0, 40);
    }
  },
  {
    name: "Defeat", size: [216, 216], frames: 24,
    draw(p, t) {
      const split = out(t / .62), fade = 1 - out((t - .33) / .62);
      for (let i = 0; i < 8; i++) {
        const angle = (i + rand(81, i) * .24) * TAU / 8;
        const radius = 19 + split * (32 + rand(45, i) * 45);
        const [x, y] = polar(angle, radius);
        const size = (10 + rand(52, i) * 8) * (1 - .45 * split);
        const skew = Math.cos(angle + t * 7) * size * .62;
        const plate = [[x-size,y-size*.2],[x+skew,y-size*.7],[x+size,y-size*.1],
          [x+size*.7,y+size*.55],[x-size*.4,y+size*.61]];
        p.poly(plate, steel[i % 3 + 1], fade);
        p.polyline(plate.slice(0, 3), steel[4], fade * .91);
        p.line(x-size*.38, y+1, x+size*.47, y+1, t < .50 ? copper[4] : cold[3], fade);
        p.rect(x-1, y+3, 2, 2, steel[0], fade);
      }
      const burst = 1 - out(t / .27);
      p.poly([[0,-16],[4,-6],[18,0],[4,4],[0,16],[-4,4],[-18,0],[-4,-4]], copper[4], burst);
      p.line(-13, 0, 13, 0, copper[6], burst);
      p.line(0, -8, 0, 8, copper[5], burst);
      racingBits(p, t + .045, copper, 36, 102, 50, TAU, 0, 8);
      const collapse = clamp((t - .49) / .51), residue = Math.sin(collapse * Math.PI);
      for (let i = 0; i < 10; i++) {
        const rad = (22 + i % 3 * 7) * (1 - collapse * .77);
        const [x, y] = polar(TAU * i / 10, rad);
        p.rect(x, y, 2 + i % 3, 1, cold[i % 2 ? 3 : 4], residue * .62);
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
  // Keep the pixel grid, with smooth opacity changes on thin strokes.
  const writePixel = p.put.bind(p);
  p.put = (x, y, color, alpha = 1) => {
    if (alpha >= 1) writePixel(x, y, color);
    else p.blendPx(x, y, color, alpha);
  };
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
