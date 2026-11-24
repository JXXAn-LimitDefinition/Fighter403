import { mkdirSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";

const [output, library = process.cwd()] = process.argv.slice(2);
if (!output) throw new Error("Usage: node build-dungeon-art.mjs <output> [pixelfx directory]");

const project = resolve(output);
const core = pathToFileURL(join(library, "src/core/index.js"));
const png = pathToFileURL(join(library, "tools/lib/png.mjs"));
const { Painter, createFrame, composite, hex } = await import(core);
const { encodePNG } = await import(png);

const themes = [
  {
    id: "Cache", floor: ["#222d37", "#26313b", "#28343d", "#1e2932"],
    seam: "#1a222d", bevel: "#303c4a", metal: ["#0e141e", "#202c3a", "#344453", "#465968", "#6d808b"],
    trace: "#4e797c", active: "#85d3ce", accent: "#b59364",
  },
  {
    id: "Firewall", floor: ["#2e282e", "#332c32", "#383036", "#29252b"],
    seam: "#211e2a", bevel: "#3d3747", metal: ["#15121d", "#2c2739", "#44384c", "#5e4752", "#946a64"],
    trace: "#845053", active: "#ff9273", accent: "#d39567",
  },
  {
    id: "Core", floor: ["#22253a", "#262941", "#2a2e46", "#202237"],
    seam: "#1b1b2f", bevel: "#373a51", metal: ["#111320", "#262742", "#393c59", "#50546e", "#818097"],
    trace: "#68668e", active: "#c1abf7", accent: "#6b99ae",
  },
].map(({ id, floor, seam, bevel, metal, trace, active, accent }) => ({
  id, floor: floor.map(hex), seam: hex(seam), bevel: hex(bevel), metal: metal.map(hex),
  trace: hex(trace), active: hex(active), accent: hex(accent),
}));

const T = 48;
// One pixel overlaps the adjoining sprite and one more guards the atlas sample.
const floorOverlap = 1;
const floorBleed = floorOverlap + 1;
const ink = hex("#101522");
const neutral = ["#131925", "#263346", "#3b4b61", "#576b7c", "#8496a2"].map(hex);
const lock = hex("#e86e64");
const go = hex("#8edbd3");

function raster(w, h, draw) {
  const frame = createFrame(w, h);
  draw(new Painter(frame));
  const rgba = new Uint8Array(w * h * 4);
  composite(frame, rgba);
  return { w, h, rgba };
}

function plate(p, x, y, w, h, cut, fill, rim) {
  p.poly([[x + cut, y], [x + w - cut, y], [x + w, y + cut], [x + w, y + h - cut],
    [x + w - cut, y + h], [x + cut, y + h], [x, y + h - cut], [x, y + cut]], fill);
  p.line(x + cut, y, x + w - cut, y, rim);
  p.line(x, y + cut, x, y + h - cut - 1, rim);
}

function circuit(p, points, trough, trace) {
  p.polyline(points.map(([x, y]) => [x, y + 1]), trough);
  p.polyline(points, trace);
}

function floorTile(p, theme, style) {
  const { id, floor, seam, bevel, metal } = theme;
  p.rect(0, 0, T, T, floor[0]);
  if (id === "Cache") {
    if (style === 2) {
      p.rect(7, 9, 32, 31, seam);
      plate(p, 8, 10, 29, 28, 3, floor[1], bevel);
      p.rect(15, 16, 12, 2, metal[2]);
      p.rect(28, 16, 4, 2, metal[2]);
      p.rect(28, 32, 3, 2, seam);
    }
    if (style === 3) {
      p.rect(5, 16, 38, 17, seam);
      p.rect(7, 18, 34, 13, floor[3]);
      for (const x of [10, 18, 28, 36]) p.rect(x, 21, 2, 7, metal[1]);
    }
    if (style) p.rect(5, 5, 5, 1, floor[2]);
  } else if (id === "Firewall") {
    if (style !== 0)
      p.poly([[6, 6], [42, 6], [36, 14], [10, 14], [6, 18]], floor[1]);
    if (style >= 2) {
      circuit(p, [[3, 36], [14, 36], [24, 26], [42, 26]], seam, bevel);
      circuit(p, [[14, 43], [20, 43], [30, 33], [44, 33]], seam, bevel);
    }
    if (style !== 0) p.rect(31, 8, 7, 2, metal[1]);
  } else {
    if (style === 1) p.polyline([[4, 12], [12, 4], [35, 4], [43, 12]], floor[2]);
    if (style >= 2) {
      plate(p, 4, 4, 39, 39, 6, floor[1], bevel);
      p.line(11, 42, 36, 42, seam);
      p.rect(12, 17, 24, 14, seam);
      p.rect(14, 18, 20, 12, floor[3]);
      p.rect(16, 20, 8, 2, bevel);
      p.rect(27, 27, 5, 1, bevel);
    }
  }
}

function wallBase(p, theme, x, w) {
  const { metal: m, floor, seam } = theme;
  p.rect(x, 0, w, 2, m[0]);
  p.rect(x, 2, w, 3, m[2]);
  p.rect(x, 5, w, 19, m[1]);
  p.rect(x, 24, w, 2, m[4]);
  p.rect(x, 26, w, 4, m[2]);
  p.rect(x, 30, w, 3, m[0]);
  p.rect(x, 33, w, 2, seam);
  p.rect(x, 35, w, 2, floor[3]);
}

function wallTile(p, theme, variant, x = 0) {
  const { id, metal: m, accent, trace, active } = theme;
  wallBase(p, theme, x, T);
  p.rect(x + 3, 5, 42, 19, m[0]);
  plate(p, x + 4, 6, 39, 16, 3, m[2], m[3]);
  if (id === "Cache") {
    for (const dx of [8, 11, 14, 17, 20, 23, 26]) {
      p.rect(x + dx, 10, 2, 9, m[0]);
      p.rect(x + dx, 10, 1, 9, m[3]);
    }
    p.rect(x + 33, 10, 7, 3, variant ? active : m[3]);
    p.rect(x + 33, 16, 5, 2, variant ? accent : m[1]);
  } else if (id === "Firewall") {
    p.poly([[x + 5, 9], [x + 21, 9], [x + 30, 20], [x + 14, 20]], m[3]);
    p.polyline([[x + 14, 8], [x + 24, 8], [x + 33, 19], [x + 40, 19]], variant ? active : m[3]);
    p.rect(x + 33, 10, 7, 2, variant ? accent : m[2]);
  } else {
    p.rect(x + 7, 9, 32, 11, m[0]);
    p.rect(x + 10, 11, 26, 7, m[1]);
    p.rect(x + 12, 12, 8, 1, m[4]);
    p.rect(x + 27, 13, 7, 3, variant ? active : trace);
  }
  p.rect(x + 1, 24, 3, 2, m[3]);
}

function cornerTile(p, theme) {
  const { metal: m, trace, accent, seam, floor } = theme;
  p.poly([[0, 11], [11, 0], [48, 0], [48, 37], [37, 37], [37, 48], [0, 48]], floor[3]);
  p.poly([[0, 11], [11, 0], [48, 0], [48, 35], [35, 35], [35, 48], [0, 48]], seam);
  p.poly([[0, 11], [11, 0], [48, 0], [48, 33], [37, 33], [33, 37], [33, 48], [0, 48]], m[0]);
  p.poly([[2, 12], [12, 2], [48, 2], [48, 30], [36, 30], [30, 36], [30, 48], [2, 48]], m[2]);
  p.poly([[7, 14], [14, 7], [38, 7], [43, 12], [43, 23], [31, 23], [23, 31],
    [23, 43], [12, 43], [7, 38]], m[1]);
  circuit(p, [[13, 31], [13, 18], [18, 13], [31, 13]], m[0], trace);
  plate(p, 14, 14, 9, 9, 2, m[0], m[4]);
  p.rect(17, 17, 3, 3, accent);
  p.polyline([[24, 47], [24, 35], [35, 24], [47, 24]], m[4]);
  p.polyline([[26, 47], [26, 36], [36, 26], [47, 26]], m[3]);
}

function gate(p, theme, state) {
  const { id, metal: m, floor, trace, accent } = theme;
  if (state === "Concealed") {
    wallTile(p, theme, 0);
    wallTile(p, theme, 0, T);
    p.polyline([[43, 24], [46, 24], [48, 26], [53, 26]], m[2]);
    p.rect(48, 32, 4, 1, m[2]);
    return;
  }
  const locked = state === "Locked";
  const broken = state === "Breached";
  p.rect(18, 0, 60, 37, m[0]);
  p.rect(22, 0, 52, 35, floor[3]);
  p.rect(25, 1, 46, 31, floor[0]);
  p.rect(25, 8, 46, 1, m[1]);
  p.rect(25, 24, 46, 1, m[1]);
  for (const x of [0, 78]) {
    wallBase(p, theme, x, 18);
    plate(p, x + 1, 2, 16, 36, 3, m[0], m[3]);
    plate(p, x + 3, 4, 12, 30, 2, m[2], m[4]);
    p.rect(x + 5, 9, 8, 13, m[0]);
    p.rect(x + 6, 11, 6, 3, locked ? lock : broken ? accent : go);
    p.rect(x + 6, 17, 6, 1, trace);
    p.rect(x + 4, 27, 9, 3, m[3]);
    p.rect(x + 3, 34, 12, 2, m[1]);
  }
  plate(p, 18, 32, 60, 12, 3, m[0], m[3]);
  p.rect(22, 35, 52, 6, m[2]);
  p.rect(24, 40, 48, 1, m[1]);
  for (const x of [23, 29, 64, 70]) p.rect(x, 37, 3, 2, accent);
  if (state === "Open") {
    for (const y of [21, 28]) p.polyline([[42, y], [48, y - 5], [54, y]], trace);
    p.polyline([[37, 39], [48, 35], [59, 39]], go);
    return;
  }
  if (broken) {
    const tear = [[33, 3], [30, 10], [35, 15], [26, 21], [30, 26], [25, 31]];
    const otherTear = [[64, 4], [69, 12], [63, 17], [71, 24], [68, 30]];
    p.poly([[18, 2], ...tear, [18, 33]], m[2]);
    p.poly([[78, 2], ...otherTear, [78, 32]], m[2]);
    p.polyline(tear, m[4]);
    p.polyline(otherTear, m[4]);
    p.poly([[20, 40], [23, 38], [26, 42], [22, 45]], m[3]);
    p.poly([[70, 43], [74, 40], [77, 44], [75, 46]], m[3]);
    p.rect(25, 23, 3, 2, accent);
    return;
  }
  for (const x of [18, 49]) {
    plate(p, x, 2, 28, 30, 3, m[2], m[4]);
    p.rect(x + 2, 8, 24, 19, m[1]);
    if (id === "Firewall") {
      for (const y of [9, 18]) {
        p.poly([[x + 4, y], [x + 22, y], [x + 25, y + 6], [x + 7, y + 6]], m[3]);
        p.line(x + 5, y, x + 22, y, trace);
      }
    } else if (id === "Core") {
      p.polyline([[x + 4, 10], [x + 9, 15], [x + 19, 15], [x + 24, 10]], trace);
      p.polyline([[x + 4, 25], [x + 9, 20], [x + 19, 20], [x + 24, 25]], trace);
    } else {
      for (const y of [10, 15, 20, 25]) p.rect(x + 4, y, 20, 2, m[3]);
    }
  }
  plate(p, 41, 8, 14, 17, 3, m[0], m[3]);
  p.rect(44, 15, 8, 6, lock);
  p.polyline([[45, 15], [45, 12], [50, 12], [50, 15]], lock);
  p.rect(47, 17, 2, 2, m[0]);
  p.rect(32, 37, 32, 2, lock, .65);
}

function rail(p, theme) {
  const { id, metal: m, trace, accent } = theme;
  if (id === "Cache") {
    p.rect(0, 16, 96, 15, m[0]);
    p.rect(0, 18, 96, 11, m[1]);
    p.rect(0, 20, 96, 1, m[3]);
    p.rect(0, 26, 96, 1, trace);
    for (const x of [14, 17, 46, 49, 78, 81]) p.rect(x, 19, 2, 9, m[2]);
  } else if (id === "Firewall") {
    circuit(p, [[0, 16], [28, 16], [36, 24], [60, 24], [68, 16], [95, 16]], m[1], trace);
    circuit(p, [[0, 29], [22, 29], [34, 35], [62, 35], [74, 29], [95, 29]], m[1], m[3]);
    for (const x of [38, 45, 52]) p.rect(x, 26, 4, 2, accent);
  } else {
    for (const y of [12, 17, 22]) {
      circuit(p, [[0, y], [25, y], [34, y + 3], [61, y + 3], [70, y], [95, y]], m[0], m[3]);
      p.rect(42, y + 2, 11, 1, trace);
    }
  }
}

function equipment(p, theme) {
  const { id, metal: m, trace, active, accent } = theme;
  if (id === "Cache") {
    plate(p, 5, 15, 86, 41, 6, m[0], m[1]);
    for (const [x, y, w] of [[8, 7, 25], [37, 7, 25], [66, 14, 20]]) {
      plate(p, x, y, w, 43, 3, m[2], m[4]);
      p.rect(x + 2, y + 5, w - 4, 27, m[0]);
      p.rect(x + 4, y + 7, w - 8, 3, m[3]);
      for (const dy of [14, 19, 24]) {
        p.rect(x + 4, y + dy, w - 8, 3, m[2]);
        p.rect(x + w - 8, y + dy, 2, 1, dy === 19 ? active : trace);
      }
      p.rect(x + 3, y + 34, w - 6, 4, m[1]);
      p.rect(x + 5, y + 35, 4, 1, accent);
    }
  } else if (id === "Firewall") {
    plate(p, 4, 15, 88, 42, 9, m[0], m[1]);
    plate(p, 7, 10, 82, 40, 9, m[2], m[4]);
    p.poly([[14, 17], [60, 17], [76, 29], [65, 43], [14, 43]], m[0]);
    for (const x of [17, 31, 45]) {
      p.poly([[x, 19], [x + 11, 19], [x + 21, 30], [x + 11, 41], [x, 41], [x + 10, 30]], m[3]);
      p.polyline([[x + 3, 19], [x + 11, 19], [x + 20, 29]], trace);
    }
    p.rect(73, 19, 9, 4, accent);
    p.rect(74, 28, 7, 2, active);
    p.rect(14, 47, 65, 5, m[1]);
  } else {
    p.beam(24, 34, 72, 34, 20, m[0]);
    p.beam(24, 31, 72, 31, 13, m[2]);
    p.rect(29, 26, 38, 2, trace);
    p.rect(29, 36, 38, 2, m[3]);
    for (const x of [24, 72]) {
      p.circle(x, 34, 23, m[0]);
      p.circle(x, 31, 21, m[2]);
      p.arc(x, 31, 21, 2, Math.PI, Math.PI * 2, m[4]);
      p.circle(x, 31, 17, m[0]);
      p.ring(x, 31, 14, 3, trace);
      p.arc(x, 31, 14, 2, 3.5, 5.5, active, 0.75);
      p.poly([[x, 22], [x + 9, 31], [x, 40], [x - 9, 31]], m[2]);
      p.poly([[x, 26], [x + 5, 31], [x, 35], [x - 5, 31]], active);
      p.rect(x - 4, 47, 8, 2, accent);
    }
  }
}

function serviceBay(p, theme) {
  const { id, metal: m, floor, seam, bevel, trace, accent } = theme;
  if (id === "Cache") {
    plate(p, 4, 8, 178, 74, 8, seam, bevel);
    plate(p, 7, 11, 171, 67, 6, floor[3], floor[2]);
    for (const y of [23, 34, 47]) {
      circuit(p, [[9, y], [39, y], [51, y + 12], [120, y + 12], [140, y]], m[0], m[3]);
      p.rect(134, y - 1, 7, 3, trace);
    }
    p.rect(147, 20, 28, 47, m[0]);
    for (const y of [23, 30, 37, 44, 51, 58]) {
      p.rect(150, y, 22, 3, m[2]);
      p.rect(150, y, 22, 1, m[3]);
    }
    p.rect(13, 64, 18, 2, accent);
    p.rect(34, 64, 5, 2, m[3]);
  } else if (id === "Firewall") {
    p.poly([[4, 7], [172, 7], [189, 24], [189, 65], [168, 88], [24, 88], [4, 64]], seam);
    p.poly([[7, 10], [170, 10], [186, 26], [186, 64], [166, 85], [26, 85], [7, 63]], floor[1]);
    p.polyline([[10, 26], [25, 12], [168, 12], [182, 26]], bevel);
    p.beam(17, 48, 174, 48, 12, m[0]);
    p.rect(18, 44, 155, 1, trace);
    p.rect(18, 51, 155, 1, m[3]);
    for (const x of [27, 67, 107, 147]) {
      p.poly([[x - 8, 24], [x + 13, 24], [x + 25, 47], [x + 12, 72], [x - 8, 72], [x + 4, 47]], m[2]);
      p.polyline([[x - 8, 24], [x + 12, 24], [x + 24, 47], [x + 12, 71]], trace);
    }
    for (const x of [31, 38, 45, 140, 147, 154]) p.rect(x, 79, 4, 2, accent, 0.65);
  } else {
    plate(p, 5, 8, 181, 77, 21, seam, bevel);
    plate(p, 9, 12, 173, 69, 18, floor[3], floor[2]);
    for (const y of [23, 30, 62, 69]) {
      circuit(p, [[14, 46], [38, y], [153, y], [177, 46]], m[0], trace);
      p.rect(75, y, 11, 1, accent);
      p.rect(104, y, 11, 1, accent);
    }
    for (const x of [44, 96, 148]) {
      p.circle(x, 46, 12, m[0]);
      p.ring(x, 46, 10, 2, m[3]);
      p.rect(x - 5, 45, 10, 2, trace);
    }
  }
}

function junction(p, theme) {
  const { id, metal: m, trace, accent } = theme;
  const cables = id === "Cache" ? [13, 21] : id === "Core" ? [12, 17, 22] : [12, 26];
  const route = inset => [[inset, 95], [inset, inset + 19], [inset + 19, inset], [95, inset]];
  for (const inset of cables) {
    const points = route(inset);
    for (let i = 1; i < points.length; i++) {
      const [[x0, y0], [x1, y1]] = points.slice(i - 1, i + 1);
      p.beam(x0, y0, x1, y1, id === "Cache" ? 4 : 6, m[0], .8);
    }
    p.polyline(points, trace);
  }
  plate(p, 10, 10, 30, 30, 6, m[0], m[3]);
  plate(p, 13, 13, 24, 24, 5, m[2], m[3]);
  p.rect(17, 19, 16, 11, m[1]);
  for (const x of [19, 24, 29]) p.rect(x, 23, 2, 3, accent);
}

function arena(p, theme) {
  const { id, metal: m, floor, seam, bevel, trace } = theme;
  const cx = 224, cy = 152;
  if (id === "Core") {
    for (const radius of [146, 127, 98]) {
      p.ring(cx, cy, radius, 4, seam, .8);
      p.ring(cx, cy, radius - 1, 1, bevel, .75);
    }
    for (const angle of [0, Math.PI / 2, Math.PI, 3 * Math.PI / 2]) {
      p.arc(cx, cy, 143, 7, angle + .08, angle + .37, trace, .6);
      p.arc(cx, cy, 143, 7, angle - .37, angle - .08, trace, .6);
    }
    for (const x of [11, 371]) {
      plate(p, x, 145, 65, 14, 4, floor[3], m[2]);
      p.rect(x + 7, 150, 51, 2, trace);
    }
    return;
  }
  const cut = id === "Cache" ? 20 : 69;
  const outline = [[cut, 0], [424 - cut, 0], [424, cut], [424, 268 - cut],
    [424 - cut, 268], [cut, 268], [0, 268 - cut], [0, cut], [cut, 0]];
  for (const [scale, color] of [[1, seam], [.98, trace], [.91, bevel]]) {
    p.polyline(outline.map(([x, y]) => [cx + (x - 212) * scale, cy + (y - 134) * scale]), color, .72);
  }
  for (const y of [18, 274]) {
    for (const x of [160, 174, 188, 249, 263, 277]) {
      p.poly([[x, y], [x + 8, y], [x + 13, y + 7], [x + 5, y + 7]], trace, .72);
    }
  }
  if (id === "Firewall") {
    for (const y of [86, 216]) p.arc(cx, y, 46, 2, 0, Math.PI * 2, m[2], .42);
  }
}

function lockSignal(p, frame) {
  const intensity = 0.3 + 0.4 * Math.sin(frame * Math.PI / 12);
  p.rect(33, 37, 30, 2, hex("#ffb19b"), intensity);
  p.rect(6, 11, 6, 3, hex("#ffb19b"), intensity);
  p.rect(84, 11, 6, 3, hex("#ffb19b"), intensity);
  p.glow(48, 38, 10, lock, intensity * .16);
}

const roles = [
  ["Start", "#8edbd3"], ["Combat", "#667c94"], ["Elite", "#eca46c"],
  ["Vault", "#e4c780"], ["Shop", "#e9aa72"], ["Maintenance", "#91c7eb"],
  ["Event", "#c4a2ef"], ["Hidden", "#8294a8"], ["Boss", "#db857d"],
].map(([id, tint]) => ({ id, tint: hex(tint) }));

function deck(p, accent) {
  const [base, low, mid, high] = neutral;
  p.disc(74, 67, 55, base, (d, a) => Math.sin(a) > 0.45 ? 0.5 * (1 - d) : 0);
  plate(p, 21, 39, 102, 56, 10, base, high);
  plate(p, 24, 42, 96, 47, 8, low, mid);
  p.rect(40, 90, 64, 2, mid);
  p.rect(29, 69, 6, 3, accent);
  p.rect(109, 69, 6, 3, accent);
}

function groundedRing(p, tint, radius) {
  p.ring(72, 72, radius, 6, ink, 0.55);
  p.ring(72, 72, radius - 2, 1, tint, 0.5);
  for (const [x, y, w, h] of [[72 - radius, 67, 3, 10], [72 + radius - 3, 67, 3, 10],
    [67, 72 - radius, 10, 3], [67, 72 + radius - 3, 10, 3]]) p.rect(x, y, w, h, tint, 0.75);
}

function landmark(p, role) {
  const [base, low, mid, high, edge] = neutral;
  const { id, tint } = role;
  if (id === "Combat" || id === "Elite") {
    for (const [x, y, dx, dy] of [[12, 12, 1, 1], [132, 12, -1, 1],
      [12, 132, 1, -1], [132, 132, -1, -1]]) {
      p.polyline([[x, y + 17 * dy], [x, y], [x + 17 * dx, y]], hex("#4b586c"));
    }
    if (id === "Elite") {
      for (const y of [20, 124]) {
        p.polyline([[57, y], [72, y + (y < 72 ? 12 : -12)], [87, y]], tint);
        p.rect(70, y, 4, 2, tint);
      }
    }
    return;
  }
  if (id === "Boss") {
    for (const [x, y, dx, dy] of [[120, 64, 0, 1], [176, 120, -1, 0],
      [120, 176, 0, -1], [64, 120, 1, 0]]) {
      p.line(x, y, x + dx * 7, y + dy * 7, high);
      p.line(x + dx * 10, y + dy * 10, x + dx * 12, y + dy * 12, tint, .45);
    }
    return;
  }
  if (id === "Start") {
    p.circle(72, 73, 44, base);
    p.circle(72, 71, 41, mid);
    p.circle(72, 71, 37, low);
    p.ring(72, 71, 32, 5, base);
    p.ring(72, 71, 30, 1, tint, 0.85);
    p.polyline([[59, 73], [72, 61], [85, 73]], tint);
    p.polyline([[63, 80], [72, 72], [81, 80]], edge);
    p.glow(72, 69, 24, tint, 0.14);
    return;
  }
  if (id === "Hidden") {
    deck(p, mid);
    p.poly([[44, 55], [65, 48], [84, 58], [101, 61], [92, 83], [55, 83]], base);
    for (const [x, y, w, h] of [[49, 64, 19, 12], [76, 58, 17, 17], [69, 76, 15, 6]]) {
      p.rect(x, y, w, h, mid);
      p.rect(x + 2, y + 2, w - 4, 2, tint);
    }
    circuit(p, [[54, 78], [58, 88], [46, 99]], base, high);
    return;
  }
  if (id === "Event") {
    groundedRing(p, tint, 42);
    p.poly([[72, 33], [110, 72], [72, 110], [34, 72]], base);
    p.poly([[72, 40], [102, 72], [72, 102], [42, 72]], mid);
    p.poly([[72, 47], [96, 72], [72, 95], [48, 72]], low);
    p.poly([[72, 53], [90, 72], [72, 88], [54, 72]], hex("#ad92d0"));
    p.rect(60, 69, 24, 2, base);
    p.rect(64, 75, 14, 2, base);
    p.glow(72, 71, 23, tint, 0.21);
    return;
  }
  deck(p, tint);
  if (id === "Vault") {
    plate(p, 39, 46, 65, 37, 7, base, mid);
    plate(p, 42, 44, 59, 34, 5, mid, edge);
    p.rect(44, 57, 56, 7, low);
    p.rect(44, 65, 56, 2, tint);
    p.poly([[72, 50], [83, 60], [72, 71], [61, 60]], base);
    p.poly([[72, 54], [78, 60], [72, 66], [66, 60]], tint);
  } else if (id === "Shop") {
    plate(p, 33, 49, 77, 29, 3, high, edge);
    p.rect(37, 55, 70, 17, base);
    for (const [index, x] of [42, 63, 84].entries()) {
      p.rect(x, 58, 16, 10, mid);
      p.rect(x + 2, 60, 10, 6, low);
      if (index === 0) p.rect(x + 4, 62, 6, 2, tint);
      if (index === 1) p.poly([[x + 7, 60], [x + 10, 63], [x + 7, 66], [x + 4, 63]], tint);
      if (index === 2) p.rect(x + 6, 61, 2, 4, tint);
    }
    p.rect(40, 78, 64, 7, low);
    p.rect(51, 80, 42, 2, tint);
  } else {
    plate(p, 47, 43, 49, 43, 7, mid, edge);
    p.rect(51, 49, 41, 31, low);
    p.rect(67, 53, 9, 23, tint);
    p.rect(60, 60, 23, 9, tint);
    p.rect(33, 55, 9, 19, high);
    p.rect(101, 55, 9, 19, high);
    p.rect(35, 58, 5, 2, tint);
    p.rect(103, 58, 5, 2, tint);
  }
}

const tiles = [
  ...[0, 1, 2, 3].map(i => ({ name: `Floor${i}`, x: 2 + i * 52, y: 2, w: 48, h: 48 })),
  { name: "Wall0", x: 210, y: 2, w: 48, h: 48 },
  { name: "Wall1", x: 262, y: 2, w: 48, h: 48 },
  { name: "Corner", x: 314, y: 2, w: 48, h: 48 },
  ...["Open", "Locked", "Concealed", "Breached"].map((name, i) =>
    ({ name, x: 2 + i * 100, y: 54, w: 96, h: 48 })),
  { name: "Rail", x: 2, y: 106, w: 96, h: 48 },
  { name: "Equipment", x: 102, y: 106, w: 96, h: 64 },
  { name: "ServiceBay", x: 202, y: 106, w: 192, h: 96 },
  ...[0, 1, 2, 3].map(i => ({ name: `FloorBlock${i}`, x: 2 + i * 100, y: 206, w: 96, h: 96 })),
  { name: "Junction", x: 2, y: 306, w: 96, h: 96 },
  { name: "Arena", x: 404, y: 2, w: 448, h: 304 },
];

const isFloor = name => name.startsWith("Floor");
const tileSlices = tiles.map(tile => isFloor(tile.name) ? {
  ...tile, x: tile.x - floorOverlap, y: tile.y - floorOverlap,
  w: tile.w + 2 * floorOverlap, h: tile.h + 2 * floorOverlap,
} : tile);

// Negative coordinates measure from the opposite edge, after rotation.
const dressing = [
  [
    ["ServiceBay", 48, 42, 0], ["Equipment", 83, 48, 0],
    ["ServiceBay", -48, -42, 2], ["Equipment", -83, -48, 2],
    ["Rail", -63, 35, 0], ["Rail", 63, -35, 2],
    ["Equipment", -65, 53, 0, "Utility"],
    ["Junction", -43, 41, 1, "Boss"], ["Junction", 43, -41, 3, "Boss"],
  ],
  [
    ["ServiceBay", 51, 34, 0], ["ServiceBay", -51, 34, 0],
    ["ServiceBay", 51, -34, 2, "Battle"], ["ServiceBay", -51, -34, 2, "Battle"],
    ["Equipment", 107, 51, 0], ["Equipment", -107, 51, 2],
    ["Equipment", 103, -51, 0, "Boss"], ["Equipment", -103, -51, 2, "Boss"],
    ["Rail", 82, -48, 0, "Utility"], ["Rail", -82, -48, 2, "Utility"],
  ],
  [
    ["Junction", 39, 39, 0], ["Junction", -39, 39, 1],
    ["Junction", -39, -39, 2], ["Junction", 39, -39, 3],
    ["Equipment", 63, 49, 1], ["Equipment", -63, -49, 3],
    ["Rail", 135, 39, 0], ["Rail", -135, -39, 2],
    ["Equipment", -63, 49, 3, "Battle"], ["Equipment", 63, -49, 1, "Battle"],
    ["ServiceBay", 44, 173, 1, "LargeBoss"], ["ServiceBay", -44, -173, 3, "LargeBoss"],
  ],
].map(pieces => ({
  pieces: pieces.map(([name, x, y, turn, audience = "All"]) => ({ name, x, y, turn, audience })),
}));

function blit(target, sprite, left, top, turn = 0) {
  for (let y = 0; y < sprite.h; y++) {
    for (let x = 0; x < sprite.w; x++) {
      const si = (y * sprite.w + x) * 4;
      const alpha = sprite.rgba[si + 3];
      if (alpha === 0) continue;
      const dx = left + [x, sprite.h - 1 - y, sprite.w - 1 - x, y][turn];
      const dy = top + [y, x, sprite.h - 1 - y, sprite.w - 1 - x][turn];
      if (dx < 0 || dx >= target.w || dy < 0 || dy >= target.h) continue;
      const di = (dy * target.w + dx) * 4;
      const fraction = alpha / 255;
      const under = target.rgba[di + 3] / 255 * (1 - fraction);
      const total = fraction + under;
      for (let channel = 0; channel < 3; channel++) {
        target.rgba[di + channel] = Math.round((sprite.rgba[si + channel] * fraction
          + target.rgba[di + channel] * under) / total);
      }
      target.rgba[di + 3] = Math.round(total * 255);
    }
  }
}

function atlas(name, size, entries, images, floorColor) {
  const [w, h] = size;
  const pixels = { w, h, rgba: new Uint8Array(w * h * 4) };
  for (const entry of entries) {
    if (floorColor && isFloor(entry.name)) {
      const width = entry.w + 2 * floorBleed, height = entry.h + 2 * floorBleed;
      const border = raster(width, height, p => p.rect(0, 0, width, height, floorColor));
      blit(pixels, border, entry.x - floorBleed, entry.y - floorBleed);
    }
    blit(pixels, images[entry.name], entry.x, entry.y);
  }
  writeFileSync(join(project, `${name}.png`), encodePNG(w, h, pixels.rgba));
}

function themeImages(theme) {
  const images = Object.fromEntries(tiles.filter(tile => !tile.name.startsWith("FloorBlock")).map(({ name, w, h }) => [name, raster(w, h, p => {
    if (name.startsWith("Floor")) floorTile(p, theme, Number(name.at(-1)));
    else if (name.startsWith("Wall")) wallTile(p, theme, Number(name.at(-1)));
    else if (name === "Corner") cornerTile(p, theme);
    else if (name === "Rail") rail(p, theme);
    else if (name === "Equipment") equipment(p, theme);
    else if (name === "ServiceBay") serviceBay(p, theme);
    else if (name === "Junction") junction(p, theme);
    else if (name === "Arena") arena(p, theme);
    else gate(p, theme, name);
  })]));
  for (const [i, pattern] of [[0, 0, 0, 0], [1, 0, 0, 1], [0, 2, 0, 1], [3, 0, 1, 0]].entries()) {
    const block = raster(T * 2, T * 2, () => {});
    for (const [cell, variant] of pattern.entries()) {
      blit(block, images[`Floor${variant}`], cell % 2 * T, Math.floor(cell / 2) * T);
    }
    images[`FloorBlock${i}`] = block;
  }
  return images;
}

function preview(themeIndex, images, room, roleImages, signal) {
  const boss = room === "Boss";
  const columns = boss ? 28 : 14, rows = boss ? 18 : 8, border = 16;
  const w = columns * T, h = rows * T;
  const view = raster(w + border * 2, h + border * 2, p => p.rect(0, 0, w + border * 2, h + border * 2, ink));
  const at = (name, x, y, turn = 0) => blit(view, images[name], border + x, border + y, turn);
  for (let y = 0; y < rows; y += 2) {
    for (let x = 0; x < columns; x += 2) {
      const choice = ((x * 17 ^ y * 23 ^ x * y * 3) >>> 0) % 23;
      const edge = x === 0 || x >= columns - 2 || y === 0 || y >= rows - 2;
      const variant = edge && choice > 10 ? 2 + choice % 2 : choice % 3 === 0 ? 1 : 0;
      at(`FloorBlock${variant}`, x * T, y * T);
    }
  }
  const battle = boss || room === "Combat" || room === "Elite";
  const placements = dressing[themeIndex].pieces.filter(({ audience }) => audience === "All"
    || audience === "Battle" && battle || audience === "Utility" && !battle
    || audience === "Boss" && boss || audience === "LargeBoss" && boss && rows >= 18);
  const place = ({ name, x, y, turn }) => {
    const sprite = images[name];
    const [sw, sh] = turn % 2 ? [sprite.h, sprite.w] : [sprite.w, sprite.h];
    at(name, x < 0 ? w + x - sw : x, y < 0 ? h + y - sh : y, turn);
  };
  if (boss) at("Arena", (w - images.Arena.w) / 2, (h - images.Arena.h) / 2);
  for (const item of placements.filter(item => item.name !== "Equipment")) place(item);
  const stamp = roleImages[room];
  blit(view, stamp, border + (w - stamp.w) / 2, border + (h - stamp.h) / 2);
  for (const item of placements.filter(item => item.name === "Equipment")) place(item);

  at("Corner", 0, 0, 0); at("Corner", w - T, 0, 1);
  at("Corner", w - T, h - T, 2); at("Corner", 0, h - T, 3);
  for (const [side, state] of ["Open", "Locked", "Breached", "Concealed"].entries()) {
    const vertical = side % 2, length = vertical ? rows : columns;
    const doorCell = length / 2 - 1;
    for (let cell = 1; cell < length - 1; cell++) {
      const x = vertical ? (side === 1 ? w - T : 0) : cell * T;
      const y = vertical ? cell * T : (side === 2 ? h - T : 0);
      if (cell === doorCell) {
        at(state, x, y, side);
        if (state === "Locked") blit(view, signal, border + x, border + y, side);
        cell++;
      } else {
        at(cell % 5 === 2 ? "Wall1" : "Wall0", x, y, side);
      }
    }
  }
  writeFileSync(join(project, `Preview-${themes[themeIndex].id}${boss ? "-Boss" : ""}.png`), encodePNG(view.w, view.h, view.rgba));
}

mkdirSync(project, { recursive: true });
const roleRects = roles.map(({ id: name }, i) => name === "Boss"
  ? { name, x: 450, y: 2, w: 240, h: 240 }
  : { name, x: 2 + (i % 3) * 148, y: 2 + Math.floor(i / 3) * 148, w: 144, h: 144 });
const roleImages = Object.fromEntries(roles.map((role, index) => {
  const { w, h } = roleRects[index];
  return [role.id, raster(w, h, p => landmark(p, role))];
}));
const signals = Array.from({ length: 12 }, (_, i) => ({
  name: `Lock${i.toString().padStart(2, "0")}`, x: 2 + i % 4 * 100, y: 2 + Math.floor(i / 4) * 52, w: 96, h: 48,
}));
const signalImages = Object.fromEntries(signals.map(({ name, w, h }, i) => [name, raster(w, h, p => lockSignal(p, i))]));
for (const [i, theme] of themes.entries()) {
  const images = themeImages(theme);
  atlas(theme.id, [864, 408], tiles, images, theme.floor[0]);
  preview(i, images, ["Combat", "Elite", "Vault"][i], roleImages, signalImages.Lock02);
  preview(i, images, "Boss", roleImages, signalImages.Lock02);
}
atlas("Landmarks", [1024, 512], roleRects, roleImages);
atlas("DoorSignal", [512, 256], signals, signalImages);
writeFileSync(join(project, "AtlasLayout.json"), JSON.stringify({ tiles: tileSlices, roles: roleRects, signals, dressing }, null, 2) + "\n");
