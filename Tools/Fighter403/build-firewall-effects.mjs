import { mkdirSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { homedir } from "node:os";

const pixelfx = process.env.FIGHTER403_PIXELFX ?? resolve(homedir(), ".codex/tools/pixelfx");
const { createFrame, painterFor, composite, hex, sparkle, TAU } =
    await import(pathToFileURL(resolve(pixelfx, "src/core/index.js")));
const { encodePNG } = await import(pathToFileURL(resolve(pixelfx, "tools/lib/png.mjs")));
const repo = fileURLToPath(new URL("../../", import.meta.url));
const output = process.argv[2] ?? resolve(repo, "Assets/Resources/Fighter403/Bosses/FirewallEffects.png");
const size = 112;
const columns = 12;
const color = (...values) => values.map(hex);
const amber = color("#321925", "#77372b", "#d66032", "#ffab55", "#ffdb9b", "#fff6da");
const pink = color("#342038", "#732b63", "#be418f", "#fa76b4", "#ffbfdc", "#fff0fa");
const green = color("#102b29", "#23604e", "#3d916e", "#7de7a0", "#b9f9ca", "#efffee");
const red = color("#2b1728", "#7a2738", "#bc4146", "#fd6660", "#ffae81", "#fff1ce");
const cyan = color("#102132", "#224b69", "#337fa0", "#66cfe6", "#b7f4ff", "#f4ffff");
const clamp = (x, a = 0, b = 1) => Math.min(b, Math.max(a, x));
const fade = (t, start = 0.48) => clamp((1 - t) / (1 - start));
const easeOut = (x) => 1 - (1 - x) ** 3;
const polar = (angle, radius) => [Math.cos(angle) * radius, Math.sin(angle) * radius];
const tip = (p, angle, near, far, tint, alpha = 1) =>
    p.line(...polar(angle, near), ...polar(angle, far), tint, alpha);
const diamond = (p, x, y, radius, c, alpha = 1, filled = false) => {
    const points = [[x, y - radius], [x + radius, y], [x, y + radius], [x - radius, y]];
    if (filled) p.poly(points, c, alpha);
    else p.polyline([...points, points[0]], c, alpha);
};
const dashArc = (p, radius, start, arc, width, tint, alpha = 1) =>
    p.arc(0, 0, radius, width, start, start + arc, tint, alpha);
const radialPath = (angle, radius, points) => points.map(([along, across]) => [
    (radius + along) * Math.cos(angle) - across * Math.sin(angle),
    (radius + along) * Math.sin(angle) + across * Math.cos(angle),
]);

function arrival(p, t) {
    const c = amber;
    const alpha = fade(t, .58);
    for (let i = 0; i < 4; i++) {
        const a = i * TAU / 4;
        const dock = easeOut(clamp((t - i * .051) * 3.4));
        const r = 44 - 15 * dock;
        const live = clamp((dock - .78) * 6.2);
        const jaw = [[-3, -12], [2, -12], [5, -7], [5, 7], [2, 12], [-3, 12], [-5, 8], [-5, -8]];
        p.poly(radialPath(a, r, jaw), c[0], alpha);
        p.polyline(radialPath(a, r, [[-3, -12], [2, -12], [5, -7], [5, 7], [2, 12]]), c[2], alpha);
        p.polyline(radialPath(a, r, [[-2, -9], [-4, -6], [-4, 6], [-2, 9]]), c[3], alpha * (.55 + live * .45));
        p.polyline(radialPath(a, r, [[1, -6], [1, 6]]), c[5], alpha * live);
        dashArc(p, r - 7, a - .38, .76, 2, c[2], alpha * .75);
        for (const side of [-1, 1]) {
            const [x, y] = radialPath(a, r, [[0, side * 10]])[0];
            p.dot(x, y, c[4], alpha);
        }
        tip(p, a, r - 14, r - 9, c[4], alpha * live);
    }
    if (t > .36) {
        const release = (t - .36) / .64;
        const r = 15 + 36 * easeOut(release);
        for (let i = 0; i < 4; i++) {
            const start = i * TAU / 4 + .17;
            dashArc(p, r, start, 1.21, 2, c[3], fade(release) * .72);
            dashArc(p, r - 3, start + .23, .75, 1, c[2], fade(release) * .78);
        }
        p.glow(0, 0, 25, c[3], .18 * fade(release));
    }
}

function packetCharge(p, t) {
    const c = amber;
    const squeeze = easeOut(t);
    const length = 14 + t * 8;
    for (const side of [-1, 1]) {
        const y = side * (12 - 6 * squeeze);
        const rail = [[-20, y], [-10, y], [-5, y - side * 2], [length - 12, y - side * 2]];
        p.polyline(rail, c[3]);
        p.polyline(rail.map(([x, y]) => [x, y + side * 2]), c[1]);
        p.line(-16, y, -10, y, c[4]);
        p.rect(-19 + t * 24, side * 3 - 1, 5, 2, c[3]);
        p.px(-15 + t * 24, side * 3 - 1, c[5]);
        p.line(-22, y, -22, y + side * 3, c[2]);
    }
    diamond(p, 2, 0, 4.5 + 3.5 * t, c[2], .9);
    diamond(p, 2, 0, 2 + 2 * t, t > .65 ? c[5] : c[3], 1, true);
    p.glow(3, 0, 13, c[3], .16 + .22 * t);
}

function packet(p, t) {
    const c = amber;
    p.poly([[-14, -2], [-8, -6], [5, -6], [13, -1], [13, 1], [5, 6], [-8, 6], [-14, 2]], c[1]);
    p.poly([[-11, 0], [-7, -4], [5, -4], [11, 0], [4, 4], [-7, 4]], c[3]);
    p.polyline([[-8, -5], [4, -5], [10, -2]], c[4]);
    p.poly([[-5, -2], [4, -2], [9, 0], [4, 2], [-5, 2], [-8, 0]], c[5]);
    p.line(-7, 5, 3, 5, c[2]);
    p.px(11, 0, c[5]);
    p.line(-7 + 13 * t, -3, -5 + 13 * t, -3, c[5], .82);
    for (let i = 0; i < 4; i++) {
        const slide = (t + i / 4) % 1;
        const x = -11 - slide * 16;
        const y = (i % 2 === 0 ? -1 : 1) * (2 + 3 * slide);
        p.line(x, y, x - (7 - 3 * slide), y, slide < .4 ? c[3] : c[2], 1 - slide * .82);
    }
    p.glow(2, 0, 12, c[3], .24);
}

function packetHit(p, t) {
    const c = amber;
    const alpha = fade(t);
    const travel = easeOut(t);
    for (let i = 0; i < 7; i++) {
        const a = -1.22 + i * .39;
        const radius = 3 + (13 + i % 3 * 7) * travel;
        const points = radialPath(a, radius, [[-9 * alpha, 0], [-2, -1.5], [3 * alpha, 0], [-2, 1.5]]);
        p.poly(points, c[2 + i % 2], alpha);
        const [x, y] = polar(a, radius);
        p.dot(x, y, t < .43 ? c[5] : c[3], alpha);
    }
    const shock = fade(t, .10);
    dashArc(p, 5 + 12 * travel, 1.99, 2.22, 2, c[3], shock * .82);
    if (t < .36) sparkle(p, 1, 0, 9 * (1 - t * 2), c[5], c[3], 1 - t * 2);
    p.glow(0, 0, 14, c[3], .34 * alpha);
}

function meteorFall(p, t) {
    const c = pink;
    const tail = 25 + 13 * t + 4 * Math.sin(t * TAU * 1.5);
    p.poly([[-4, -9], [-tail, -14], [-tail + 10, -2], [-tail + 2, 5], [-tail + 19, 6], [-6, 12], [4, 7]], c[1], .83);
    p.poly([[-tail + 6, -8], [-16, -5], [1, -2], [-6, 7], [-22, 4]], c[2], .88);
    p.polyline([[-tail + 8, -8], [-13, -4], [-5, -1]], c[4], .87);
    p.poly([[-10, -1], [-6, -10], [4, -12], [11, -5], [13, 2], [7, 10], [-3, 11], [-9, 5]], c[0]);
    p.poly([[-8, -1], [-4, -8], [4, -10], [9, -4], [10, 2], [6, 8], [-3, 8], [-7, 4]], c[3]);
    p.poly([[-4, -8], [4, -10], [9, -4], [1, 0], [-5, -.5]], c[4]);
    p.poly([[-1, -8], [4, -9], [7, -5], [0, -3]], c[5]);
    p.poly([[1, 0], [9, -4], [10, 2], [6, 8], [0, 6]], c[2]);
    p.line(-6, 4, 0, 6, c[1], .9);
    p.line(9, 1, 6, 6, c[4]);
    for (let i = 0; i < 6; i++) {
        const d = (t * 1.9 + i / 6) % 1;
        const x = -14 - 25 * d;
        const y = (i % 2 ? -1 : 1) * (5 + 8 * d);
        p.line(x - (5 - 3 * d), y, x, y, i % 2 ? c[4] : c[2], 1 - d * .84);
    }
    p.glow(-1, 0, 17, c[3], .28);
}

function meteorBurst(p, t) {
    const c = pink;
    const out = easeOut(t);
    const alpha = fade(t, .35);
    for (let i = 0; i < 7; i++) {
        const a = i * TAU / 7 + .18;
        const radius = 6 + 24 * out + i % 2 * 5;
        const [x, y] = polar(a, radius);
        const [midX, midY] = polar(a + (i % 2 ? .19 : -.14), 4 + 13 * out);
        const [rootX, rootY] = polar(a - .12, 6 * out);
        p.polyline([[rootX, rootY], [midX, midY], [x, y]], c[2], alpha * .72);
        const shard = radialPath(a + t * (i % 2 ? 1.4 : -1.5), 0,
            [[-6 + 3 * t, -2], [4 - 3 * t, 0], [-1, 3], [-5, 1]]);
        const placedShard = shard.map(([sx, sy]) => [sx + x, sy + y]);
        p.poly(placedShard, c[2], alpha);
        p.line(...placedShard[0], ...placedShard[1], c[4], alpha);
        if (t < .52) p.dot(...placedShard[1], c[5], alpha);
    }
    for (let i = 0; i < 4; i++) {
        dashArc(p, 6 + out * 30, i * TAU / 4 + .12, 1.10, 2, c[3], alpha);
        dashArc(p, 9 + out * 33, i * TAU / 4 + .39, .39, 1, c[4], alpha * .72);
    }
    if (t < .28) {
        p.ring(0, 0, 36, 2, c[4], .85 - t * 3);
        p.circle(0, 0, 13 - t * 23, (d) => d < .4 ? c[5] : d < .69 ? c[4] : c[2], 1 - t * 2);
        sparkle(p, -1, -1, 18 * (1 - t * 2), c[5], c[4], .87);
    }
    p.glow(0, 0, 23, c[3], .37 * alpha);
}

function deployShutters(p, travel, alpha) {
    for (let i = 0; i < 4; i++) {
        const angle = i * TAU / 4 + Math.PI / 4;
        const inset = clamp((travel - i * .065) * 1.3);
        const offset = 7 + 14 * easeOut(inset);
        const plate = [[-5, -11], [0, -15], [5, -11], [5, 11], [1, 15], [-5, 10]];
        p.poly(radialPath(angle, offset, plate), green[0], alpha);
        p.poly(radialPath(angle, offset, [[-3, -10], [1, -13], [3, -10], [3, 10], [0, 12], [-3, 9]]),
            green[1], alpha);
        p.polyline(radialPath(angle, offset, [[-5, -10], [-5, 10], [-2, 12]]), green[3], alpha);
        p.polyline(radialPath(angle, offset, [[3, -9], [3, 8], [1, 10]]), green[2], alpha);
        for (const bolt of [-8, 8]) {
            const [x, y] = radialPath(angle, offset, [[0, bolt]])[0];
            p.dot(x, y, green[4], alpha);
        }
    }
}

function deployOpen(p, t) {
    const c = green;
    const extent = 2 + 25 * easeOut(clamp((t - .17) * 1.22));
    const live = clamp(t * 1.4);
    diamond(p, 0, 0, extent, c[1], .66, true);
    for (let y = -Math.floor(extent); y <= extent; y += 5) {
        const half = extent - Math.abs(y);
        p.line(-half, y, half, y, c[2], (.21 + .14 * Math.sin(y * .32 - t * 15)) * live);
    }
    diamond(p, 0, 0, extent, c[3], .81 * live);
    if (extent > 9) diamond(p, 0, 0, extent - 5, c[2], .48 * live);
    const beam = clamp((t - .35) * 1.75);
    const y = (1 - 2 * beam) * extent;
    p.line(-extent + Math.abs(y), y, extent - Math.abs(y), y, c[4], .93 * Math.sin(beam * Math.PI));
    p.glow(0, 0, 18, c[3], .17 * live);
    deployShutters(p, t, 1);
}

function deployClose(p, t) {
    const c = green;
    const alpha = fade(t);
    const extent = 27 * (1 - easeOut(t * .8));
    diamond(p, 0, 0, extent, c[3], alpha);
    diamond(p, 0, 0, Math.max(1, extent - 4), c[2], alpha * .64);
    deployShutters(p, 1 - t, alpha);
    for (let i = 0; i < 8; i++) {
        const a = i * TAU / 8;
        const [x, y] = polar(a, (25 + 17 * easeOut(t)) * (i % 2 ? .79 : 1));
        p.rect(x, y, i % 2 ? 4 : 2, i % 2 ? 2 : 4, c[3], alpha * clamp(t * 4));
        p.dot(x, y, t < .28 ? c[5] : c[4], alpha * clamp(t * 4));
    }
    const latch = 1 - Math.min(1, Math.abs(t - .36) * 4.3);
    if (latch > 0) {
        p.line(-18 * latch, 0, 18 * latch, 0, c[4], latch);
        sparkle(p, 0, 0, 10 * latch, c[5], c[3], latch);
    }
    p.glow(0, 0, 18, c[3], .26 * latch);
}

function seal(p, t) {
    const c = red;
    const alpha = fade(t, .23);
    const rim = 48 - 9 * t;
    const end = Math.PI / 4;
    const sweep = rim - 5 - 20 * t;
    if (t < .30) {
        const wash = .28 * (1 - t / .30);
        p.each(0, -rim, rim, rim, (x, y, sx, sy) => {
            const r = Math.hypot(x, y);
            if (Math.abs(y) > x || r >= rim - 1.5) return;
            p.blendPx(sx, sy, c[3], wash * (.33 + .67 * r / rim));
        });
    }
    dashArc(p, sweep, -end + .05, 2 * end - .10, 3.5, c[2], alpha * .73);
    dashArc(p, sweep + 1.5, -end + .07, 2 * end - .14, 1.5, c[4], alpha);
    const panelSpan = 2 * end / 5;
    for (let panel = 0; panel < 5; panel++) {
        const start = -end + panelSpan * panel;
        const withdraw = easeOut(clamp((t - .21 - panel * .05) * 1.6));
        const panelRadius = rim - withdraw * 9;
        const panelAlpha = alpha * (1 - withdraw * .6);
        dashArc(p, panelRadius, start + .013, panelSpan - .029, 8 - withdraw * 3, c[1], panelAlpha);
        dashArc(p, panelRadius - 2, start + .038, panelSpan - .076, 4.5, c[2], panelAlpha);
        dashArc(p, panelRadius, start + .036, panelSpan - .072, 1.7, t < .20 ? c[5] : c[4], panelAlpha);
        tip(p, start + panelSpan * .5, panelRadius - 9, panelRadius - 5, c[4], panelAlpha);
        tip(p, start + panelSpan * .17, panelRadius - 5, panelRadius - 3, c[0], panelAlpha);
    }
}

function vortex(p, t) {
    const c = cyan;
    p.circle(0, 0, 15, (d) => d < .69 ? c[0] : d < .8 ? c[1] : c[2], .89);
    p.ring(0, 0, 12, 1.4, c[3], .77);
    for (let i = 0; i < 3; i++) {
        const base = TAU * (i - t) / 3;
        let last = polar(base, 12);
        for (let s = 1; s <= 42; s++) {
            const u = s / 42;
            const a = base - u * 3.55;
            const r = 12 + 34 * u ** 1.32;
            const next = polar(a, r);
            if (s % 10 < 8) {
                const focus = (s + Math.floor(t * 14)) % 14 < 4;
                p.beam(...last, ...next, 3.25 - u * 2.3, c[2], 1 - u * .34);
                p.line(...last, ...next, focus ? c[4] : c[3], 1 - u * .53);
                if (s % 10 < 3) tip(p, a, r, r + 3.4, c[2], .62 - u * .17);
            }
            last = next;
        }
        for (let node = 0; node < 2; node++) {
            const travel = 1 - ((t + node * .5) % 1);
            const [x, y] = polar(base - travel * 3.55, 12 + 34 * travel ** 1.32);
            const glow = Math.sin(travel * Math.PI);
            diamond(p, x, y, 2.6, c[3], glow, true);
            p.dot(x, y, c[5], glow);
        }
    }
    p.glow(0, 0, 22, c[2], .19);
}

function collapse(p, t) {
    const c = cyan;
    const compression = easeOut(clamp(t / .29));
    const release = clamp((t - .27) / .73);
    const alpha = fade(release, .25);
    const radius = 19 - 14 * compression + 42 * easeOut(release);
    for (let i = 0; i < 6; i++) {
        const a = i * TAU / 6 + .1 - .18 * compression;
        dashArc(p, radius, a, .57, 4 - 2.6 * release, c[2], alpha);
        dashArc(p, radius, a + .12, .32, 1.6, c[4], alpha);
        const [x, y] = polar(a + .20, Math.max(2, radius - 5 - 3 * release));
        diamond(p, x, y, 2.6 - 1.1 * release, c[4], alpha);
    }
    if (release < .32) {
        p.circle(0, 0, 11 - 8 * compression, c[0], alpha);
        diamond(p, 0, 0, 13 - 9 * compression, c[4], alpha);
        const flash = clamp(1 - Math.abs(t - .29) * 4.21);
        sparkle(p, 0, 0, 14 * flash, c[5], c[3], flash);
        p.glow(0, 0, 22, c[3], .37 * flash);
    }
}

function overload(p, t) {
    const c = red;
    const alpha = fade(t, .47);
    const build = easeOut(clamp(t / .30));
    for (let i = 0; i < 8; i++) {
        const a = TAU * i / 8 + .14;
        const radius = 10 + 39 * easeOut(t);
        dashArc(p, radius, a, .51, 7 - 4 * t, c[1], alpha);
        dashArc(p, radius - 2, a + .05, .39, 4 - 2 * t, c[2], alpha);
        dashArc(p, radius, a + .10, .29, 2, c[4], alpha);
        tip(p, a + .22, 6 + t * 30, 11 + t * 37, c[3], alpha);
        if (i % 2 === 0) {
            const rail = 14 + build * 17;
            dashArc(p, rail, a - .05, .62, 2, c[2], alpha);
            dashArc(p, rail, a + .24, .30, 1, c[5], alpha * .74);
        }
    }
    for (let i = 0; i < 5; i++) {
        const y = (i - 2) * (5 + t * 6);
        const x = Math.sqrt(Math.max(0, 25 ** 2 - y ** 2));
        const stretch = clamp((t - i * .025) * 4.9);
        p.line(-x * stretch, y, x * stretch, y, i === 2 ? c[5] : c[3], alpha * fade(t, .22) * .75);
    }
    p.glow(0, 0, 25, c[3], .29 * alpha);
}

function defeat(p, t) {
    const c = amber;
    const alpha = fade(t, .27);
    const out = easeOut(t);
    for (let i = 0; i < 12; i++) {
        const angle = i * TAU / 12 + .10 + (i % 3 - 1) * .07;
        const [x, y] = polar(angle, 9 + (22 + (i % 3) * 7) * out);
        const length = (i % 3 ? 5 : 8) * alpha;
        const width = (i % 3 ? 2.1 : 4.5) * alpha;
        const localAngle = angle + t * (i % 2 === 0 ? 1.1 : -1.2);
        const shape = radialPath(localAngle, 0, [[-length, 0], [-2, -width], [length * .52, -width],
            [length, 0], [2, width]]).map(([sx, sy]) => [sx + x, sy + y]);
        p.poly(shape, c[i % 3 === 0 ? 1 : 0], alpha);
        p.polyline(shape.slice(1, 4), c[i % 3 + 2], alpha);
        if (t < .45) p.dot(...shape[3], c[5], alpha);
    }
    const fracture = 17 * (1 - t);
    p.polyline([[-fracture, 0], [-3, -4], [4, 3], [fracture, -2]], c[4], alpha);
    p.polyline([[1, -fracture], [-3, -4], [4, 3], [1, fracture]], c[4], alpha);
    if (t < .28) sparkle(p, 0, 0, 23 * (1 - t * 2), c[5], c[3], 1 - t * 2);
    p.glow(0, 0, 30, c[3], .36 * alpha);
}

function vent(p, t) {
    const c = cyan;
    for (let side = 0; side < 4; side++) {
        const a = side * TAU / 4;
        const recoil = .5 + .5 * Math.sin((t + side / 4) * TAU);
        const mouth = 24 + 2 * recoil;
        const grille = [[-1, -7], [3, -8], [4, 8], [-1, 7]];
        p.poly(radialPath(a, mouth, grille), c[0], .96);
        p.polyline(radialPath(a, mouth, [[-1, -7], [-1, 7]]), c[3], .78);
        p.polyline(radialPath(a, mouth, [[3, -7], [3, 7]]), c[2], .92);
        for (let i = 0; i < 4; i++) {
            const travel = (t + i / 4 + side / 12) % 1;
            const glow = Math.sin(Math.PI * travel);
            const angle = a + (i - 1.5) * (.085 + .06 * travel);
            const r = mouth + 1 + 20 * travel;
            tip(p, angle, r - 4 - 3 * glow, r, c[2], glow * .9);
            tip(p, angle, r - 1, r + 1.5, c[4], glow * .88);
        }
    }
}

function packetLaunch(p, t) {
    const c = amber;
    const alpha = fade(t, .27);
    const out = easeOut(t);
    for (const side of [-1, 1]) {
        const y = side * (6 + 4 * out);
        p.polyline([[-9, y], [0, side * 5], [8, side * 5], [18 * out, y + side * 3]], c[2], alpha);
        p.line(4, side * 5, 9 + 16 * out, y + side * 3, c[4], alpha * .84);
        p.line(-9, y, -5, y + side * 3, c[3], alpha);
    }
    if (t < .4) {
        p.poly([[-4, -7], [20 * out + 6, 0], [-4, 7], [2, 0]], c[3], 1 - t * 1.9);
        p.beam(0, 0, 5 + 15 * out, 0, 2.5, c[5], 1 - t * 1.9);
        dashArc(p, 7 + 7 * out, -1.12, 2.24, 1.4, c[4], .83 * (1 - t * 1.9));
    }
    p.glow(1, 0, 14, c[3], .24 * alpha);
}

// The row order matches FirewallBoss.PixelEffect.
const clips = [
    [arrival, false], [packetCharge, false], [packet, true], [packetHit, false],
    [meteorFall, false], [meteorBurst, false], [deployOpen, false], [deployClose, false],
    [seal, false], [vortex, true], [collapse, false], [overload, false], [defeat, false],
    [vent, true], [packetLaunch, false],
];
const width = size * columns;
const height = size * clips.length;
const atlas = new Uint8ClampedArray(width * height * 4);
for (const [row, [draw, loop]] of clips.entries()) {
    for (let frame = 0; frame < columns; frame++) {
        const pixels = createFrame(size, size);
        const painter = painterFor(pixels);
        painter.place(size / 2, size / 2);
        draw(painter, frame / (loop ? columns : columns - 1));
        const rgba = new Uint8ClampedArray(size * size * 4);
        composite(pixels, rgba, null, 0.6);
        for (let y = 0; y < size; y++) {
            const start = (row * size + y) * width + frame * size;
            atlas.set(rgba.subarray(y * size * 4, (y + 1) * size * 4), start * 4);
        }
    }
}
mkdirSync(resolve(output, ".."), { recursive: true });
writeFileSync(output, encodePNG(width, height, atlas));
