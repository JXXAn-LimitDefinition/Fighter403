import { writeFileSync } from "node:fs";
import { join } from "node:path";
import { pathToFileURL } from "node:url";

const [output, library] = process.argv.slice(2);
if (!output || !library) throw new Error("Usage: node build-dungeon-props.mjs <asset directory> <pixelfx directory>");
const { Painter, createFrame, composite, hex } = await import(pathToFileURL(join(library, "src/core/index.js")));
const { encodePNG } = await import(pathToFileURL(join(library, "tools/lib/png.mjs")));

const width = 64, height = 56;
const [ink, shadow, side, steel, edge, flash] = ["#101722", "#1c2939", "#293d51", "#465f70", "#6e8694", "#a9c0c8"].map(hex);
const [cyan, amber, violet, mint, coral] = ["#70c9d1", "#d1a269", "#af9ed8", "#83c0a9", "#ce887e"].map(hex);
const props = [];

function plate(p, x, y, w, h, cut, fill, highlight) {
  p.poly([[x + cut, y], [x + w - cut, y], [x + w, y + cut], [x + w, y + h - cut],
    [x + w - cut, y + h], [x + cut, y + h], [x, y + h - cut], [x, y + cut]], fill);
  p.line(x + cut, y, x + w - cut, y, highlight);
  p.line(x, y + cut, x, y + h - cut - 1, highlight);
}

function hull(p, x, y, w, h, trim = edge) {
  plate(p, x + 1, y + h - 5, w, 9, 3, ink, ink);
  plate(p, x, y, w, h, 3, shadow, trim);
  plate(p, x + 2, y + 2, w - 4, h - 9, 2, steel, edge);
  p.rect(x + 3, y + h - 6, w - 6, 4, side);
}

function panel(p, x, y, w, h, color) {
  plate(p, x, y, w, h, 2, ink, shadow);
  p.rect(x + 3, y + 3, w - 5, h - 5, side);
  p.rect(x + 4, y + 4, Math.max(2, w - 10), 1, color);
}

function leds(p, x, y, count, color, step = 4) {
  for (let i = 0; i < count; i++) p.rect(x + i * step, y, 2, 2, i % 3 === 2 ? edge : color);
}

function grille(p, x, y, count, length) {
  for (let i = 0; i < count; i++) {
    p.rect(x, y + i * 3, length, 2, ink);
    p.rect(x + 1, y + i * 3, length - 2, 1, side);
  }
}

function tube(p, x, y, h, color) {
  plate(p, x, y, 11, h, 3, shadow, edge);
  p.rect(x + 2, y + 4, 7, h - 9, side);
  p.rect(x + 3, y + 5, 2, h - 11, color);
  p.rect(x + 5, y + 5, 1, h - 11, color, .45);
  p.line(x + 2, y + h - 4, x + 8, y + h - 4, edge);
}

function trace(p, points, color = edge) {
  p.polyline(points.map(([x, y]) => [x, y + 1]), ink);
  p.polyline(points, side);
  const [x, y] = points.at(-1);
  p.rect(x - 1, y - 1, 3, 3, color);
}

function ring(p, x, y, r, color) {
  p.disc(x + 1, y + 1, r + 1, ink, 1);
  p.disc(x, y, r, color, 1);
  p.disc(x, y, r - 2, shadow, 1);
  p.line(x - 4, y - r + 2, x + 2, y - r + 2, edge);
}

function stripe(p, x, y, count, color) {
  for (let i = 0; i < count; i++) p.line(x + i * 5, y + 4, x + 4 + i * 5, y, color);
}

function add(name, family, kind, footprint, draw) {
  const frame = createFrame(width, height);
  draw(new Painter(frame));
  const rgba = new Uint8Array(width * height * 4);
  composite(frame, rgba);
  let left = width, top = height, right = 0, bottom = 0;
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
    if (rgba[(y * width + x) * 4 + 3] === 0) continue;
    left = Math.min(left, x); top = Math.min(top, y);
    right = Math.max(right, x + 1); bottom = Math.max(bottom, y + 1);
  }
  if (left === 0 || top === 0 || right === width || bottom === height || right === 0)
    throw new Error(`${name} is missing or cropped at an edge`);
  const [fx = 0, fy = 0, fw = 0, fh = 0] = footprint;
  if (fw > 0 && (fx < left || fy < top || fx + fw > right || fy + fh > bottom))
    throw new Error(`${name} footprint ${footprint} exceeds bounds ${[left, top, right, bottom]}`);
  props.push({ name, family, kind, fx: fw ? fx - left : 0, fy: fh ? fy - top : 0, fw, fh,
    rgba, left, top, w: right - left, h: bottom - top });
}

// Fabrication and service.
add("ToolBench", 0, 2, [11, 30, 41, 11], p => {
  hull(p, 8, 12, 47, 29); panel(p, 12, 17, 23, 13, cyan);
  p.line(17, 25, 27, 20, flash); p.rect(40, 18, 10, 9, side); leds(p, 39, 32, 3, cyan);
});
add("HydraulicPress", 0, 2, [19, 35, 28, 12], p => {
  hull(p, 15, 31, 36, 15); hull(p, 17, 9, 31, 10);
  p.rect(19, 17, 4, 20, edge); p.rect(43, 17, 4, 20, steel);
  p.rect(30, 17, 6, 12, flash); p.rect(26, 27, 14, 4, side); stripe(p, 23, 34, 4, amber);
});
add("CableSpool", 0, 2, [18, 33, 27, 10], p => {
  p.disc(33, 34, 14, ink, 1); p.disc(32, 27, 15, side, 1); ring(p, 31, 23, 15, steel);
  ring(p, 31, 23, 9, amber); p.polyline([[20, 35], [18, 43], [12, 45], [7, 42]], ink);
  p.polyline([[20, 34], [18, 42], [12, 44], [7, 41]], amber); p.rect(30, 22, 3, 3, edge);
});
add("RepairHoist", 0, 2, [17, 35, 29, 10], p => {
  hull(p, 14, 33, 35, 12); p.rect(17, 12, 5, 24, ink); p.rect(24, 7, 23, 4, ink);
  p.polyline([[19, 35], [19, 13], [26, 8], [45, 8]], edge);
  p.line(43, 9, 43, 24, flash); p.polyline([[41, 24], [46, 24], [46, 28], [43, 29]], amber);
  p.rect(25, 35, 14, 4, ink); leds(p, 25, 38, 3, cyan);
});
add("HandTools", 0, 1, [], p => {
  p.polyline([[16, 36], [27, 26], [34, 25], [34, 20], [29, 19], [27, 23]], ink);
  p.line(17, 35, 27, 25, edge); p.polyline([[28, 25], [33, 24], [33, 21]], flash);
  p.polyline([[34, 38], [42, 29], [47, 30]], amber); p.line(33, 38, 38, 40, side);
});
add("BoltTray", 0, 1, [], p => {
  plate(p, 17, 20, 30, 18, 4, shadow, edge); plate(p, 20, 22, 24, 11, 2, ink, steel);
  for (const [x,y] of [[24,25],[32,26],[38,24],[27,30]]) { p.rect(x,y,4,3,steel); p.dot(x,y,flash); }
});
add("ServiceCart", 0, 1, [], p => {
  p.disc(20, 40, 4, ink, 1); p.disc(42, 40, 4, ink, 1); hull(p, 15, 24, 33, 15);
  p.polyline([[17, 26], [13, 19], [20, 19]], edge); p.rect(23, 27, 17, 4, ink); p.line(28, 29, 34, 29, amber);
});
add("CrewGrate", 0, 0, [], p => {
  plate(p, 10, 16, 44, 22, 4, shadow, steel); grille(p, 16, 21, 5, 32);
  for (const x of [14,49]) p.rect(x, 25, 2, 5, edge);
});
add("ServiceConduits", 0, 0, [], p => {
  trace(p, [[8, 21], [25, 21], [34, 30], [55, 30]], amber);
  trace(p, [[8, 25], [25, 25], [35, 35], [55, 35]], cyan);
  for (const [x,y] of [[9,20],[21,20],[43,28]]) p.rect(x, y, 4, 9, steel);
});
add("WorkZone", 0, 0, [], p => {
  p.polyline([[9, 22], [9, 35], [54, 35], [54, 22]], side);
  stripe(p, 11, 29, 8, amber); p.rect(19, 22, 25, 2, steel); p.rect(27, 18, 9, 1, edge);
});

// Research and monitoring.
add("SpecimenPod", 1, 2, [22, 34, 20, 12], p => {
  hull(p, 19, 31, 26, 15); plate(p, 21, 9, 22, 26, 6, shadow, edge);
  plate(p, 24, 12, 16, 19, 5, side, cyan); p.poly([[32,15],[37,22],[33,28],[28,24]], mint);
  p.line(26, 16, 26, 23, flash); leds(p, 26, 38, 3, cyan);
});
add("InstrumentDesk", 1, 2, [11, 31, 42, 12], p => {
  hull(p, 8, 22, 48, 20); hull(p, 15, 11, 26, 19);
  panel(p, 18, 13, 20, 12, violet); p.polyline([[22, 20],[27, 20],[29, 17],[34, 19]], cyan);
  p.rect(42, 26, 8, 5, side); leds(p, 20, 34, 5, violet);
});
add("SignalDish", 1, 2, [19, 35, 24, 10], p => {
  hull(p, 16, 33, 30, 12); p.rect(29, 22, 6, 13, steel); p.rect(29, 23, 2, 10, edge);
  p.poly([[13,10],[43,11],[51,19],[41,28],[25,27]], ink);
  p.poly([[16,11],[42,12],[48,19],[39,25],[25,25]], steel);
  p.polyline([[16,11],[26,21],[40,23],[48,19]], edge); p.line(27,18,43,8,side); p.rect(42,7,4,3,cyan);
});
add("ColdStorage", 1, 2, [16, 32, 31, 13], p => {
  hull(p, 13, 9, 37, 36); panel(p, 17, 12, 27, 21, cyan);
  p.rect(20, 17, 19, 10, side); p.line(30, 17, 30, 26, cyan); p.line(25, 22, 35, 22, cyan);
  p.rect(42, 17, 2, 11, flash); grille(p, 20, 34, 2, 21);
});
add("SlideCase", 1, 1, [], p => {
  hull(p, 18, 22, 29, 16); p.rect(23, 25, 18, 7, ink); p.rect(25, 26, 14, 4, cyan, .52);
  p.line(26, 27, 34, 27, flash); p.rect(39, 32, 4, 2, edge);
});
add("HandScanner", 1, 1, [], p => {
  plate(p, 20, 16, 22, 17, 4, shadow, edge); panel(p, 24, 19, 14, 9, violet);
  plate(p, 28, 31, 10, 10, 2, side, steel); p.rect(33, 34, 2, 4, cyan);
});
add("PhialRack", 1, 1, [], p => {
  plate(p, 14, 30, 36, 8, 3, shadow, steel); tube(p, 17, 19, 18, cyan);
  tube(p, 27, 15, 22, violet); tube(p, 37, 21, 16, mint);
});
add("SensorGrid", 1, 0, [], p => {
  plate(p, 12, 16, 40, 25, 5, shadow, side);
  for (let y = 21; y <= 35; y += 7) for (let x = 18; x <= 46; x += 7) p.rect(x, y, 2, 2, x === 32 ? cyan : steel);
  p.polyline([[14, 25],[14, 36],[23, 36]], violet);
});
add("SurveySeals", 1, 0, [], p => {
  for (const [x,y] of [[16,19],[36,26],[27,36]]) {
    p.poly([[x,y-4],[x+5,y],[x,y+4],[x-5,y]], side); p.line(x-3,y,x+3,y,edge); p.dot(x,y,cyan);
  }
});
add("SampleTransfer", 1, 0, [], p => {
  trace(p, [[9,33],[19,33],[25,23],[42,23],[51,15]], violet);
  trace(p, [[11,37],[22,37],[28,27],[45,27],[54,19]], cyan);
  plate(p, 28, 19, 13, 12, 2, side, steel); p.rect(32, 23, 5, 3, violet);
});

// Security and containment.
add("ShieldRelay", 2, 2, [16, 33, 32, 12], p => {
  hull(p, 13, 32, 38, 13); p.poly([[15,14],[32,8],[49,14],[45,29],[32,36],[19,29]], ink);
  p.poly([[17,15],[32,10],[47,15],[43,28],[32,33],[21,28]], steel);
  p.polyline([[23,18],[32,15],[40,18],[38,25],[32,29],[26,25],[23,18]], violet);
  p.rect(31, 20, 3, 4, flash);
});
add("PatrolDock", 2, 2, [10, 33, 44, 12], p => {
  hull(p, 7, 14, 50, 30); panel(p, 12, 19, 40, 14, coral);
  p.poly([[20,23],[28,19],[36,20],[43,25],[38,29],[25,29]], steel);
  p.rect(28, 23, 9, 3, coral); p.rect(13, 34, 4, 5, edge); p.rect(47, 34, 4, 5, edge);
});
add("WatchTower", 2, 2, [20, 34, 24, 12], p => {
  hull(p, 17, 33, 30, 13); p.rect(29, 15, 7, 24, side); p.rect(29, 16, 2, 18, edge);
  hull(p, 19, 8, 25, 17); panel(p, 24, 11, 15, 9, coral); p.rect(36, 14, 3, 3, flash);
});
add("ContainmentBar", 2, 2, [9, 33, 46, 10], p => {
  hull(p, 6, 34, 52, 9); p.rect(12, 14, 6, 24, steel); p.rect(46, 14, 6, 24, steel);
  p.rect(13, 13, 4, 6, flash); p.rect(47, 13, 4, 6, flash);
  plate(p, 13, 22, 38, 10, 2, side, edge); stripe(p, 18, 24, 6, amber);
});
add("SafetyBeacon", 2, 1, [], p => {
  hull(p, 25, 32, 14, 8); plate(p, 28, 18, 8, 16, 2, side, steel);
  p.rect(29, 20, 6, 7, coral); p.rect(30, 21, 2, 4, flash);
});
add("PatrolDrone", 2, 1, [], p => {
  p.poly([[11,31],[22,26],[27,19],[36,19],[42,26],[53,31],[47,36],[17,36]], ink);
  p.poly([[15,31],[23,28],[28,21],[35,21],[41,28],[49,31],[45,33],[18,33]], side);
  p.line(26,27,37,27,edge); p.rect(30,28,5,3,coral);
});
add("LockModule", 2, 1, [], p => {
  hull(p, 19, 20, 26, 19); panel(p, 23, 23, 17, 10, coral);
  p.polyline([[28,27],[28,24],[34,24],[34,27]], flash); p.rect(28,27,7,3,steel);
});
add("HazardCorners", 2, 0, [], p => {
  for (const x of [9,40]) for (const y of [16,32]) {
    p.polyline([[x,y+8],[x,y],[x+13,y]], side); p.line(x+1,y+1,x+7,y+1,amber);
  }
});
add("DetectionArc", 2, 0, [], p => {
  p.polyline([[12,33],[16,23],[24,17],[40,17],[48,23],[52,33]], side);
  p.polyline([[18,32],[22,25],[28,22],[37,22],[43,26],[46,32]], steel);
  for (const x of [13,30,49]) p.rect(x, 34, 3, 2, coral);
});
add("GateApproachMark", 2, 0, [], p => {
  for (const y of [21,30,39]) p.polyline([[22,y],[31,y-6],[42,y]], y === 30 ? coral : steel);
  p.rect(14, 20, 2, 18, side); p.rect(48, 20, 2, 18, side);
});

// Power and data.
add("CapacitorTower", 3, 2, [17, 34, 30, 12], p => {
  hull(p, 14, 33, 36, 13); tube(p, 18, 10, 27, cyan); tube(p, 35, 10, 27, cyan);
  p.rect(25, 14, 14, 3, steel); p.rect(27, 25, 10, 5, edge); leds(p, 25, 38, 4, cyan);
});
add("ServerStack", 3, 2, [16, 34, 32, 12], p => {
  hull(p, 12, 9, 40, 37); for (const y of [13,23,33]) { panel(p, 16, y, 31, 8, violet); leds(p, 35, y + 3, 3, cyan, 3); }
  p.line(16, 13, 16, 41, edge); p.rect(48, 16, 2, 18, shadow);
});
add("TurbineStation", 3, 2, [11, 33, 42, 12], p => {
  hull(p, 8, 21, 48, 24); ring(p, 32, 25, 16, steel); ring(p, 32, 25, 12, side);
  for (let i = 0; i < 5; i++) {
    const a = i * Math.PI * 2 / 5;
    p.line(32, 25, Math.round(32 + Math.cos(a)*9), Math.round(25 + Math.sin(a)*9), edge);
  }
  p.disc(32, 25, 3, cyan, 1); p.rect(12, 32, 5, 3, amber); p.rect(47, 32, 5, 3, amber);
});
add("CrystalTransformer", 3, 2, [19, 34, 26, 12], p => {
  hull(p, 16, 32, 32, 14); p.poly([[32,6],[43,19],[38,34],[26,34],[22,19]], ink);
  p.poly([[32,8],[41,20],[35,32],[28,32],[24,20]], violet);
  p.poly([[32,8],[34,19],[28,32],[24,20]], side); p.line(32,10,38,19,flash);
  p.rect(24, 38, 16, 2, violet);
});
add("BatteryCells", 3, 1, [], p => {
  for (const [x,y] of [[17,20],[28,16],[39,21]]) tube(p, x, y, 17, mint);
  p.rect(16, 35, 35, 3, shadow);
});
add("FiberReel", 3, 1, [], p => {
  ring(p, 30, 27, 12, edge); ring(p, 30, 27, 8, cyan);
  p.rect(28, 25, 5, 4, steel); trace(p, [[37,34],[42,37],[49,36],[54,31]], cyan);
});
add("MemoryCanister", 3, 1, [], p => {
  plate(p, 23, 16, 18, 23, 5, side, edge); panel(p, 26, 19, 12, 14, violet);
  p.rect(27, 34, 10, 2, steel); p.rect(28, 22, 2, 6, cyan);
});
add("PowerBus", 3, 0, [], p => {
  for (const y of [21,27,33]) trace(p, [[5,y],[18,y],[23,y-3],[40,y-3],[47,y],[58,y]], cyan);
  for (const x of [15,44]) p.rect(x, 17, 4, 20, steel);
});
add("InductionPad", 3, 0, [], p => {
  plate(p, 10, 15, 44, 27, 7, shadow, steel); ring(p, 32, 28, 10, side);
  p.polyline([[28, 20],[24, 26],[30, 26],[27, 34],[37, 26],[31, 26],[34, 20]], cyan);
  for (const x of [16,45]) p.rect(x, 28, 3, 2, edge);
});
add("DataLattice", 3, 0, [], p => {
  trace(p, [[10,21],[24,21],[32,28],[42,28],[53,20]], violet);
  trace(p, [[10,37],[24,37],[32,29],[42,29],[53,37]], cyan);
  p.poly([[32,23],[39,29],[32,35],[25,29]], shadow); p.polyline([[29,28],[34,28],[34,31]], edge);
});

// Freight and salvage.
add("CargoLocker", 4, 2, [15, 33, 34, 12], p => {
  hull(p, 11, 10, 42, 35); p.rect(16, 16, 31, 21, side);
  p.line(31, 16, 31, 37, ink); grille(p, 18, 18, 3, 10); grille(p, 35, 18, 3, 10);
  p.rect(28, 30, 2, 5, flash); p.rect(33, 30, 2, 5, amber);
});
add("SalvageBin", 4, 2, [12, 34, 41, 11], p => {
  hull(p, 8, 17, 48, 28); plate(p, 12, 21, 40, 17, 3, ink, steel);
  p.poly([[18,29],[22,20],[34,22],[38,31],[29,35]], side); p.poly([[36,29],[44,22],[48,26],[46,33]], steel);
  p.line(22,22,31,24,coral); p.rect(14, 38, 13, 2, amber);
});
add("CrateStack", 4, 2, [11, 32, 43, 12], p => {
  hull(p, 8, 22, 30, 22); hull(p, 29, 16, 27, 27); hull(p, 16, 10, 23, 19);
  for (const [x,y] of [[13,30],[36,28],[21,17]]) { p.rect(x,y,12,3,side); p.rect(x+6,y,3,3,amber); }
});
add("BrokenWalker", 4, 2, [14, 32, 36, 13], p => {
  p.polyline([[11,38],[22,27],[33,29],[39,33],[53,40]], ink);
  p.polyline([[11,36],[22,26],[33,28],[39,32],[53,38]], steel);
  plate(p, 20, 14, 25, 23, 7, side, edge); p.polyline([[36,15],[29,22],[34,27],[29,32]], ink);
  p.rect(24, 21, 6, 3, coral); p.rect(15, 40, 5, 2, steel); p.rect(47, 43, 4, 2, shadow);
});
add("ScrapBundle", 4, 1, [], p => {
  p.poly([[12,35],[19,23],[29,25],[38,21],[53,31],[46,40],[23,40]], shadow);
  p.poly([[17,33],[22,25],[33,28],[27,36]], steel); p.poly([[32,33],[37,23],[46,25],[50,33]], side);
  p.polyline([[25,26],[31,33],[41,35]], amber); p.line(20, 33, 24, 28, edge);
});
add("CourierPack", 4, 1, [], p => {
  hull(p, 18, 19, 29, 21); p.polyline([[24,21],[24,35],[39,35],[39,22]], ink);
  p.rect(26, 24, 12, 7, side); p.rect(29, 26, 6, 2, mint);
});
add("LooseCartridges", 4, 1, [], p => {
  for (const [x,y] of [[13,28],[27,21],[39,33]]) {
    plate(p,x,y,13,6,2,side,edge); p.rect(x+8,y+1,3,4,amber);
  }
});
add("LoadingRails", 4, 0, [], p => {
  for (const y of [22,34]) { p.rect(6, y, 52, 3, shadow); p.line(8,y,55,y,steel); }
  for (const x of [11,22,33,44]) p.rect(x, 22, 3, 15, side);
  p.rect(8, 28, 5, 2, amber); p.rect(49, 28, 5, 2, amber);
});
add("PalletOutline", 4, 0, [], p => {
  p.polyline([[11,19],[50,19],[54,25],[54,39],[11,39],[11,19]], side);
  for (const x of [17,26,35,44]) p.line(x,23,x,35,steel); p.rect(17, 20, 8, 1, amber);
});
add("ImpactScar", 4, 0, [], p => {
  p.poly([[11,26],[21,20],[40,22],[53,29],[44,35],[24,37]], shadow);
  p.polyline([[13,32],[24,25],[32,30],[36,24],[44,25],[52,19]], ink);
  p.polyline([[14,33],[24,26],[32,31],[36,25],[43,26]], steel);
  p.line(38,25,45,23,coral);
});

if (props.length !== 50 || new Set(props.map(prop => prop.name)).size !== 50)
  throw new Error("RoomProps requires 50 distinct sprites");
const atlasSize = 512, columns = 8;
const atlas = new Uint8Array(atlasSize * atlasSize * 4);
const entries = props.map(({ rgba, left, top, w, h, ...info }, index) => {
  const x = index % columns * width + 1;
  const y = Math.floor(index / columns) * height + 1;
  for (let row = 0; row < h; row++) {
    const start = ((top + row) * width + left) * 4;
    atlas.set(rgba.subarray(start, start + w * 4), ((y + row) * atlasSize + x) * 4);
  }
  return { ...info, x, y, w, h };
});
writeFileSync(join(output, "RoomProps.png"), encodePNG(atlasSize, atlasSize, atlas));
writeFileSync(join(output, "RoomPropLayout.json"), JSON.stringify({ pieces: entries }, null, 2) + "\n");
