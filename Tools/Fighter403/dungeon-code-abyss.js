// Copy to PixelFX src/fx/fighter403. Use export-dungeon-code-abyss.mjs to pack the layouts.
import { clamp, fit, mix, rand, TAU } from "../../core/index.js";

const period = 4.8;
const palettes = {
  cache: ["#0c1830", "#1b3a62", "#32669b", "#4f9fcb", "#78cbec", "#bbecff"],
  firewall: ["#231309", "#50301b", "#845029", "#bc7434", "#e69d4e", "#ffd28a"],
  core: ["#260d1a", "#50182b", "#8e2e43", "#bf4250", "#e76864", "#ffaaa0"],
};

export const meta = {
  name: "Dungeon code abyss",
  desc: "Terminals, wireframes and animated telemetry for the three depths of the dungeon.",
  tags: ["scifi", "glitch", "loop", "background"],
  size: [256, 160], origin: [0, 0], duration: period, loop: true, fps: 10, bg: "black",
  options: { theme: "cache", layout: 0, palette: [], seed: 403 },
};

const wave = (phase) => .5 + .5 * Math.cos(TAU * phase);
function pulse(phase, start, length) {
  const age = ((phase - start + 1) % 1) / length;
  return age < 1 ? Math.sin(age * Math.PI) ** 2 : 0;
}

function bracket(p, x, y, w, h, c, a = 1) {
  const tip = 7;
  for (const sx of [0, 1]) {
    for (const sy of [0, 1]) {
      const px = x + sx * w, py = y + sy * h;
      p.line(px, py, px + tip * (1 - sx * 2), py, c, a);
      p.line(px, py, px, py + tip * (1 - sy * 2), c, a);
    }
  }
}

function bytes(p, x, y, values, c, opacity) {
  for (let i = 0; i < values.length; i++)
    p.text(values[i], x + i * 15, y, c, { a: opacity });
}

function residue(p, phase, color, rng, count) {
  for (let i = 0; i < count; i++) {
    const age = (phase + rng(i, 20)) % 1;
    const opacity = Math.sin(Math.PI * age) ** 2;
    const x = 12 + Math.floor(rng(i, 21) * 216) + Math.round((age - .5) * 6);
    const y = 9 + Math.floor(rng(i, 22) * 142);
    const length = 3 + Math.floor(rng(i, 23) * 13);
    p.rect(x, y, length, 1, color, .48 * opacity);
  }
}

function cache(p, phase, c, rng) {
  const x = 20 + Math.round(rng(0, 1) * 11);
  const y = 17 + Math.round(rng(0, 2) * 8);
  const commands = ["READ CACHE", "TRACE INDEX", "CHECK SUM"];
  const cycle = ((phase + .37 * rng(1, 1)) % 1) * 2;
  const state = cycle % 1;
  const command = commands[(Math.floor(rng(1, 2) * commands.length) + Math.floor(cycle)) % commands.length];
  const type = Math.min(clamp((state - .04) / .40), clamp((.99 - state) / .14));
  const typed = command.slice(0, Math.floor(type * command.length));
  const strike = mix(c[4], c[5], .22 + .57 * wave(phase));
  p.rect(x, y + 1, 3, 5, c[4], .7);
  p.text("CACHE / 01", x + 9, y, c[4]);
  p.rect(x, y + 11, 105, 1, mix(c[1], c[2], .62));
  p.rect(x + 92, y + 11, 13, 1, c[3]);
  p.text(">", x, y + 21, c[4], { scale: 2 });
  p.text(typed, x + 13, y + 21, strike, { scale: 2 });
  const cursorX = x + 15 + p.measure(typed, { scale: 2 })[0];
  p.rect(cursorX, y + 29, 5, 2, c[4], .23 + wave(phase * 3) * .57);

  const returned = pulse(state, .37, .56);
  p.text("INDEX OK", x + 13, y + 40, mix(c[2], c[4], .13 + .56 * returned));
  bytes(p, x + 13, y + 50, ["00", "3F", "2A", "01", "2E"], c[2], 1);
  p.rect(x, y + 40, 1, 15, c[2], .96);

  const sx = 152 + Math.round(rng(2, 1) * 18), sy = 90 + Math.round(rng(2, 2) * 12);
  bracket(p, sx, sy, 64, 28, c[2], .84);
  p.text("MAP", sx + 8, sy + 8, c[3], { scale: 2 });
  p.text("SECTOR 0C", sx + 8, sy + 21, c[2]);
  for (let lane = 0; lane < 4; lane++) {
    const level = wave(phase * 2 - lane * .19);
    p.rect(sx + 37 + lane * 5, sy + 17 - Math.round(level * 9), 2, 2 + Math.round(level * 9),
      mix(c[2], c[4], level * .82), .82);
  }

  p.text("01:0F", 177, 36, c[3], { a: .62 });
  p.rect(177, 45, 33, 1, c[2], .76);
  p.text("SYNC", 37, 121, c[3], { a: .63 });
  p.line(57, 123, 98, 123, c[2], .73);
  const sync = (phase + rng(2, 3)) % 1;
  const tick = 59 + Math.floor(sync * 35);
  p.rect(tick, 122, 4, 3, c[4], .62 * Math.sin(sync * Math.PI));
  residue(p, phase, c[2], rng, 9);
}

function firewall(p, phase, c, rng) {
  const x = 117 + Math.round(rng(3, 1) * 12), y = 27 + Math.round(rng(3, 2) * 13);
  p.text("ROUTE", x + 18, y - 14, c[4], { scale: 2 });
  p.text("FW / 02", x + 68, y - 11, c[3], { a: .76 });
  p.polyline([[x, y + 16], [x + 24, y + 16], [x + 24, y], [x + 88, y], [x + 88, y + 19], [x + 109, y + 19]], c[2]);
  p.polyline([[x + 44, y], [x + 44, y + 31], [x + 69, y + 31], [x + 69, y + 50], [x + 104, y + 50]], c[2]);
  p.polyline([[x + 12, y + 33], [x + 30, y + 33], [x + 30, y + 42], [x + 53, y + 42]], c[2], .88);
  for (const [dx, dy] of [[0, 16], [24, 16], [88, 19], [109, 19], [44, 31], [104, 50], [12, 33], [53, 42]])
    p.rect(x + dx - 1, y + dy - 1, 3, 3, c[3], .86);
  const transit = (phase + .23) % 1;
  const distance = Math.floor(transit * 144);
  let packetX = distance, packetY = 16;
  if (distance >= 123) [packetX, packetY] = [distance - 35, 19];
  else if (distance >= 104) [packetX, packetY] = [88, distance - 104];
  else if (distance >= 40) [packetX, packetY] = [distance - 16, 0];
  else if (distance >= 24) [packetX, packetY] = [24, 40 - distance];
  p.rect(x + packetX - 1, y + packetY - 1, 3, 3, mix(c[2], c[5], Math.sin(transit * Math.PI) * .94));
  const gate = pulse(phase, .43, .26);
  p.rect(x + 102, y + 17, 7, 5, c[4], .3 + .62 * gate);

  const log = 21 + Math.round(rng(4, 1) * 13), ly = 76 + Math.round(rng(4, 2) * 8);
  p.rect(log, ly, 1, 48, c[2]);
  p.text("FILTER", log + 7, ly, c[4], { a: .83 });
  p.text("DROP", log + 7, ly + 11, mix(c[4], c[5], .22 + .58 * gate), { scale: 2 });
  p.text("PASS 7E", log + 46, ly + 14, c[3]);
  p.rect(log + 7, ly + 26, 79, 1, c[2], .79);
  for (let i = 0; i < 12; i++) {
    const h = 2 + Math.round(wave(phase + rng(i, 30)) * (2 + rng(i, 32) * 11));
    p.rect(log + 7 + i * 6, ly + 44 - h, 3, h, i % 4 === 0 ? c[4] : c[3], .79);
  }

  for (let i = 0; i < 6; i++)
    p.line(169 + i * 7, 119, 164 + i * 7, 124, c[3], .56 + .29 * wave(phase * 2 - i * .15));
  p.text("RULE 07", 174, 132, c[3], { a: .85 });
  p.text("0A  6C", 36, 39, c[3], { a: .72 });
  residue(p, phase, c[2], rng, 8);
}

function core(p, phase, c, rng) {
  const x = 29 + Math.round(rng(5, 1) * 18), y = 26 + Math.round(rng(5, 2) * 9);
  const alert = pulse(phase, .10, .7);
  const tear = pulse(phase, .62, .115);
  bracket(p, x - 12, y - 10, 137, 61, c[3], .75);
  for (let i = 0; i < 2; i++) {
    p.polyline([[x + i, y + 25], [x + 13, y + 2 + i * 2], [x + 26 - i, y + 25], [x + i, y + 25]],
      mix(c[3], c[5], .12 + alert * .60));
  }
  p.rect(x + 12, y + 10, 3, 8, mix(c[4], c[5], .3 + .6 * alert));
  p.rect(x + 12, y + 20, 3, 2, c[5]);
  const title = mix(c[4], c[5], .28 + .61 * alert);
  p.text("CORE", x + 36, y + 5, title, { scale: 2 });
  p.text("LOCK", x + 76 + Math.round(tear * 4), y + 5, title, { scale: 2 });
  p.text("ERR 7F / SEALED", x + 36, y + 20, c[4]);
  for (let i = 0; i < 7; i++)
    p.rect(x + i * 16, y + 36, i === 4 ? 4 : 12, 2, i <= 3 ? c[4] : c[2], .68 + .22 * wave(phase - i * .11));
  if (tear > .1) {
    p.rect(x + 34 - Math.round(tear * 8), y + 13, 49, 1, c[4], tear * .72);
    p.rect(x + 9, y + 31, 27, 1, c[3], tear * .84);
  }

  const hx = 129 + Math.round(rng(6, 1) * 12), hy = 100 + Math.round(rng(6, 2) * 8);
  p.text("SIGNAL LOST", hx, hy - 10, c[3]);
  p.line(hx, hy + 11, hx + 93, hy + 11, c[2], .76);
  const drift = (phase + .19) % 1;
  const signal = hx + 15 + Math.round(drift * 54);
  p.polyline([[hx, hy + 7], [signal - 12, hy + 7], [signal - 6, hy + 1], [signal, hy + 18],
    [signal + 6, hy], [signal + 11, hy + 7], [hx + 87, hy + 7]], c[3], .72);
  p.rect(signal - 1, hy + 17, 3, 3, c[4], .63 * Math.sin(drift * Math.PI));
  bytes(p, hx + 3, hy + 26, ["FF", "13", "00", "A7", "2F", "EE"], c[3], .74);

  p.text("03 / HALT", 27, 116, c[3], { a: .80 });
  bracket(p, 21, 107, 58, 24, c[2], .94);
  residue(p, phase, c[2], rng, 8);
}

function panelHeader(p, x, y, title, index, width, c) {
  p.text(index, x, y, c[3]);
  p.text(title, x + 15, y, c[4]);
  p.rect(x, y + 9, width, 1, c[2]);
  p.rect(x, y + 9, 15, 1, c[4]);
}

function graph(p, x, y, width, samples, phase, c) {
  for (let i = 0; i < 4; i++)
    p.line(x, y + i * 8, x + width, y + i * 8, c[1]);
  const points = samples.map((sample, i) => [
    x + Math.round(i * width / (samples.length - 1)),
    y + 22 - sample,
  ]);
  p.polyline(points, c[3]);
  const travel = ((phase + .18) % 1) * (points.length - 1);
  const index = Math.floor(travel);
  const [x0, y0] = points[index], [x1, y1] = points[index + 1];
  const headX = Math.round(x0 + (x1 - x0) * (travel - index));
  const headY = Math.round(y0 + (y1 - y0) * (travel - index));
  p.rect(headX - 1, headY - 1, 3, 3, mix(c[3], c[5], Math.sin(phase * Math.PI) * .92));
}

function cacheMemory(p, phase, c, rng) {
  const x = 26, y = 27;
  panelHeader(p, x, y, "MEMORY BANK", "0B", 100, c);
  bracket(p, x - 5, y + 14, 110, 52, c[2], .84);
  for (let row = 0; row < 5; row++) {
    for (let col = 0; col < 11; col++) {
      const value = rng(row, col, 71);
      const read = (Math.floor(phase * 11) + row * 2) % 11 === col;
      const color = read ? c[4] : value > .82 ? c[3] : value > .38 ? c[2] : c[1];
      p.rect(x + 3 + col * 9, y + 21 + row * 8, 6, 5, color);
      if (read) p.rect(x + 3 + col * 9, y + 25 + row * 8, 6, 1, c[2]);
    }
  }
  p.text("0X07E4", x + 1, y + 74, c[3]);

  const sx = 184, sy = 103;
  const angle = TAU * phase;
  for (let ring = 0; ring < 3; ring++)
    p.arc(sx, sy, 12 + ring * 7, 1, angle + ring * 1.8, angle + ring * 1.8 + 4.5, c[2 + ring % 2], .88);
  p.line(sx, sy, sx + 24 * Math.cos(angle), sy + 24 * Math.sin(angle), c[4], .74);
  p.rect(sx - 1, sy - 1, 3, 3, c[5], .83);
  p.text("PAGE SCAN", sx - 16, sy + 32, c[3]);
  p.line(156, 40, 210, 40, c[1]);
  p.text("RW / 2A", 168, 32, c[2]);
}

function cacheTrace(p, phase, c) {
  panelHeader(p, 137, 32, "TRACE LOG", "07", 100, c);
  const rows = ["MOUNT /CACHE", "READ 002E", "INDEX READY", "EOF"];
  for (let i = 0; i < rows.length; i++) {
    const highlight = wave(phase - i / rows.length) ** 6;
    p.rect(137, 50 + i * 11, 3, 3, mix(c[2], c[5], highlight));
    p.text(rows[i], 146, 49 + i * 11, mix(c[3], c[4], highlight));
  }
  const x = 36, y = 101;
  bracket(p, x - 9, y - 14, 80, 50, c[2], .78);
  graph(p, x, y, 62, [3, 8, 7, 13, 10, 19, 9, 11, 7], phase, c);
  p.text("I/O", x, y - 7, c[4]);
  p.text("3F", 30, 33, c[2], { scale: 2 });
  p.line(28, 45, 93, 45, c[2], .8);
  p.rect(42 + Math.floor(phase * 45), 45, 5, 1, c[4], .84 * Math.sin(phase * Math.PI));
}

function firewallMatrix(p, phase, c, rng) {
  panelHeader(p, 30, 24, "ACCESS CONTROL", "04", 104, c);
  for (let row = 0; row < 6; row++) {
    const y = 44 + row * 11;
    const selected = Math.floor(phase * 6) === row;
    p.rect(29, y, 92, 7, selected ? c[2] : c[1]);
    p.text(["00A1", "7FE8", "0114", "006C", "3AD2", "00E0"][row], 34, y + 1, selected ? c[5] : c[3]);
    p.text(row % 3 === 1 ? "DENY" : "ALLOW", 87, y + 1, selected ? c[5] : c[3]);
  }
  const x = 181, y = 96;
  p.polyline([[x - 28, y - 6], [x - 28, y - 18], [x - 15, y - 30], [x + 22, y - 30],
    [x + 39, y - 14], [x + 39, y + 8], [x + 22, y + 24], [x - 12, y + 24], [x - 28, y + 8],
    [x - 28, y - 6]], c[2], .82);
  for (let i = 0; i < 5; i++) {
    const h = 5 + Math.round(18 * wave(phase - i * .17 + rng(i, 91)));
    p.rect(x - 16 + i * 9, y + 10 - h, 4, h, i === 2 ? c[4] : c[3], .85);
  }
  p.text("GATE 4", x - 6, y + 30, c[3]);
  p.text("LOG / TCP", 40, 130, c[2]);
}

function firewallRelay(p, phase, c) {
  const nodes = [[35, 72], [71, 43], [88, 100], [125, 69], [169, 88], [214, 56], [222, 108]];
  const paths = [[0, 1], [0, 2], [1, 3], [2, 3], [3, 4], [3, 5], [4, 5], [4, 6]];
  for (let i = 0; i < paths.length; i++) {
    const [a, b] = paths[i];
    const [x0, y0] = nodes[a], [x1, y1] = nodes[b];
    p.line(x0, y0, x1, y1, c[2]);
    const k = (phase + i * .23) % 1;
    if (i % 3 !== 0)
      p.rect(x0 + (x1 - x0) * k - 1, y0 + (y1 - y0) * k - 1, 3, 3, c[4], .91 * Math.sin(k * Math.PI));
  }
  nodes.forEach(([x, y], i) => {
    p.poly([[x, y - 4], [x + 4, y], [x, y + 4], [x - 4, y]], c[2]);
    p.rect(x - 1, y - 1, 3, 3, mix(c[3], c[5], .72 * wave(phase - i * .21)));
  });
  p.text("RELAY MESH", 136, 29, c[4], { scale: 2 });
  p.rect(136, 39, 45, 1, c[2]);
  p.text("HOP 07", 39, 114, c[3]);
  p.text("TUNNEL / 1C", 158, 129, c[2]);
  p.line(151, 139, 225, 139, c[2], .78);
}

function coreQuarantine(p, phase, c) {
  const x = 180, y = 54;
  const lock = pulse(phase, .14, .67);
  for (let i = 0; i < 3; i++) {
    const radius = 33 - i * 6;
    const angle = TAU * phase * (i % 2 === 0 ? 1 : -1) + i;
    p.arc(x, y, radius, 1, angle, angle + 3.9, c[2 + i % 2], .9);
  }
  p.polyline([[x, y - 14], [x + 17, y], [x, y + 14], [x - 17, y], [x, y - 14]], c[4], .68 + lock * .30);
  p.rect(x - 5, y - 1, 11, 3, c[4]);
  p.rect(x - 1, y - 5, 3, 11, c[4]);
  p.text("ISOLATION", x - 17, y + 39, c[3]);

  panelHeader(p, 22, 82, "PROCESS TABLE", "03", 97, c);
  for (let i = 0; i < 4; i++) {
    const yy = 101 + i * 9;
    p.text(["PROC 0E", "PROC FF", "WATCHDOG", "HALT"][i], 22, yy, i === 3 ? c[4] : c[3]);
    p.rect(68, yy + 2, [21, 36, 14, 7][i], 2, i === 3 ? c[4] : c[2], .92);
  }
  p.text("DEADLOCK", 139, 121, mix(c[2], c[4], .4 + lock * .42), { scale: 2 });
}

function coreDump(p, phase, c, rng) {
  panelHeader(p, 27, 26, "KERNEL DUMP", "7F", 104, c);
  for (let row = 0; row < 6; row++) {
    const y = 46 + row * 10;
    const position = Math.floor((phase + row * .23) % 1 * 5);
    p.text(["A0", "D8", "EE", "F0", "FC", "FF"][row], 27, y, c[3]);
    for (let col = 0; col < 5; col++) {
      const value = Math.floor(rng(102, row, col) * 256).toString(16).toUpperCase().padStart(2, "0");
      if (col === position && row % 2 === 0)
        p.rect(44 + col * 15, y - 1, 11, 7, c[2]);
      p.text(value, 46 + col * 15, y, col === position ? c[4] : c[2]);
    }
  }
  const x = 151, y = 105;
  const age = pulse(phase, .31, .42);
  bracket(p, x - 8, y - 10, 83, 43, c[2], .9);
  graph(p, x, y, 67, [7, 8, 8, 20, 4, 19, 7, 8, 8, 3, 11], phase, c);
  p.text("RESET", 165, 41, mix(c[3], c[5], age * .7), { scale: 2 });
  p.text("FAILED", 176, 54, c[3]);
  for (let strip = 0; strip < 6; strip++)
    p.line(28 + strip * 9, 130, 34 + strip * 9, 122, c[3], .65 + .3 * wave(phase - strip * .13));
}

const layouts = {
  cache: [cache, cacheMemory, cacheTrace],
  firewall: [firewall, firewallMatrix, firewallRelay],
  core: [core, coreQuarantine, coreDump],
};

export function draw(p, t, o) {
  const theme = Object.hasOwn(palettes, o.theme) ? o.theme : "cache";
  const layout = clamp(Math.floor(o.layout ?? 0), 0, layouts[theme].length - 1);
  const palette = fit(o.palette?.length ? o.palette : palettes[theme], 6);
  const phase = ((t % period) + period) % period / period;
  const rng = (...key) => rand(o.seed, ...key);
  layouts[theme][layout](p, phase, palette, rng);
  if (layout !== 0) residue(p, phase, palette[2], (...key) => rng(layout, ...key), 5);
}
