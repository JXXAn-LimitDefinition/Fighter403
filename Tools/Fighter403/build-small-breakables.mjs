import { writeFileSync } from "node:fs";
import { join } from "node:path";
import { pathToFileURL } from "node:url";

const [output, library] = process.argv.slice(2);
if (!output || !library) throw new Error("Usage: node build-small-breakables.mjs <PNG output> <pixelfx directory>");

const { Painter, createFrame, composite, hex } = await import(pathToFileURL(join(library, "src/core/index.js")));
const { encodePNG } = await import(pathToFileURL(join(library, "tools/lib/png.mjs")));

const ink = hex("#101522");
const dark = hex("#172131");
const steel = hex("#3b4b61");
const rim = hex("#576b7c");
const bright = hex("#8496a2");
const enamel = hex("#abb9ae");
const cold = hex("#15324a");
const sea = hex("#20666d");
const teal = hex("#39899a");
const cyan = hex("#79dae4");
const glint = hex("#dcfaf7");
const rust = hex("#683f38");
const copper = hex("#965d43");
const amber = hex("#bf8054");
const gold = hex("#e9bc73");
const violetShade = hex("#392d51");
const violet = hex("#68518e");
const lilac = hex("#ae91cb");
const lavender = hex("#dcc8ee");
const ochre = hex("#866335");
const yellow = hex("#c49f4f");
const ceramicShade = hex("#74777b");
const ceramic = hex("#b9b2a1");
const ivory = hex("#e2d8bb");
const greenShade = hex("#2c4848");
const moss = hex("#4d786e");
const sage = hex("#96b5a0");
const plum = hex("#583249");
const magenta = hex("#935776");
const rose = hex("#c18da2");
const slateBlue = hex("#48728d");
const iceBlue = hex("#a8c5d6");

function contactShadow(p, halfWidth) {
  for (const [y, inset, alpha] of [[26, 0, .40], [27, 0, .52], [28, 2, .32], [29, 4, .15]]) {
    for (let x = 16 - halfWidth + inset; x < 16 + halfWidth - inset; x++)
      p.blend(x, y, ink, alpha);
  }
}

function supplyBox(p) {
  contactShadow(p, 14);
  p.poly([[7, 6], [24, 6], [29, 11], [29, 24], [26, 28], [6, 28], [3, 25], [3, 12]], dark);
  p.poly([[7, 7], [24, 7], [28, 11], [25, 16], [6, 16], [4, 12]], rim);
  p.poly([[8, 8], [23, 8], [26, 11], [24, 14], [7, 14], [5, 12]], steel);
  p.polyline([[5, 12], [8, 8], [23, 8]], bright);
  p.line(8, 7, 19, 7, enamel);
  p.rect(11, 10, 10, 2, dark);
  p.rect(12, 10, 8, 1, ink);
  p.poly([[5, 16], [24, 16], [24, 25], [22, 26], [6, 26], [5, 24]], steel);
  p.poly([[25, 16], [28, 12], [28, 24], [25, 26]], cold);
  p.rect(7, 18, 2, 7, rim);
  p.rect(21, 18, 2, 7, rim);
  p.rect(7, 17, 2, 2, bright);
  p.rect(21, 17, 2, 2, bright);
  p.rect(11, 18, 7, 5, dark);
  p.rect(12, 19, 4, 2, teal);
  p.px(12, 19, cyan);
  p.px(17, 19, bright);
  p.line(10, 24, 19, 24, dark);
}

function glassBottle(p) {
  contactShadow(p, 8);
  p.poly([[13, 5], [19, 5], [19, 11], [22, 14], [22, 24], [20, 28],
    [12, 28], [10, 25], [10, 14], [13, 11]], cold);
  p.poly([[14, 8], [18, 8], [18, 12], [21, 15], [21, 24], [19, 26],
    [13, 26], [11, 24], [11, 15], [14, 12]], sea);
  p.poly([[13, 17], [17, 17], [19, 16], [20, 18], [20, 23], [18, 25], [13, 24]], teal);
  p.polyline([[12, 18], [14, 17], [17, 17], [19, 16]], teal);
  p.polyline([[14, 10], [14, 12], [12, 15], [12, 20]], cyan);
  p.rect(12, 15, 1, 3, glint);
  p.line(20, 17, 20, 23, sea);
  p.line(14, 25, 19, 25, teal);
  p.line(15, 25, 17, 25, cyan);
  p.rect(15, 21, 3, 2, sea);
  p.rect(13, 5, 6, 4, steel);
  p.rect(13, 5, 6, 1, enamel);
  p.rect(13, 6, 4, 1, rim);
  p.rect(18, 7, 1, 2, dark);
}

function partsCanister(p) {
  contactShadow(p, 11);
  p.poly([[11, 6], [22, 6], [26, 10], [26, 24], [23, 28], [9, 28], [6, 25], [6, 11]], dark);
  p.poly([[7, 11], [25, 11], [25, 24], [22, 26], [10, 26], [7, 24]], copper);
  p.rect(8, 13, 4, 10, amber);
  p.rect(21, 12, 3, 13, rust);
  p.rect(24, 13, 1, 11, dark);
  p.ellipse(16, 10, 9, 4, steel);
  p.ellipse(16, 9, 8, 2, amber);
  p.line(11, 8, 19, 8, gold);
  p.rect(13, 9, 7, 2, rust);
  p.rect(13, 9, 3, 1, copper);
  p.polyline([[8, 12], [11, 14], [21, 14], [24, 12]], dark);
  p.polyline([[8, 22], [11, 24], [21, 24], [24, 22]], dark);
  p.line(11, 25, 18, 25, amber);
  p.rect(11, 16, 8, 5, enamel);
  p.line(13, 17, 17, 17, rust);
  p.rect(15, 18, 1, 2, copper);
}

function sampleVial(p) {
  contactShadow(p, 9);
  p.poly([[9, 6], [15, 5], [17, 12], [20, 14], [23, 21], [24, 25], [21, 28], [10, 28],
    [8, 25], [8, 21], [10, 15], [11, 12]], violetShade);
  p.poly([[11, 8], [14, 7], [16, 13], [19, 15], [22, 22], [22, 25], [20, 26], [11, 26],
    [9, 24], [11, 17], [13, 13]], violet);
  p.poly([[10, 21], [13, 20], [16, 20], [20, 18], [22, 23], [21, 25], [11, 25]], lilac);
  p.polyline([[10, 22], [13, 21], [16, 21], [20, 19]], lavender);
  p.polyline([[12, 9], [14, 13], [12, 16], [11, 19]], lilac);
  p.line(12, 16, 11, 18, lavender);
  p.px(15, 23, lavender);
  p.poly([[8, 5], [14, 3], [16, 7], [10, 9]], steel);
  p.line(9, 5, 14, 4, enamel);
  p.line(10, 8, 15, 6, dark);
  p.line(11, 27, 20, 27, violet);
}

function batteryCell(p) {
  contactShadow(p, 14);
  p.poly([[7, 12], [23, 12], [28, 15], [28, 25], [25, 28], [5, 28], [3, 25], [3, 16]], dark);
  p.poly([[7, 13], [23, 13], [27, 16], [25, 19], [6, 19], [4, 17]], ochre);
  p.poly([[8, 13], [22, 13], [25, 15], [23, 17], [7, 17], [5, 16]], yellow);
  p.line(8, 13, 21, 13, gold);
  p.rect(8, 10, 5, 5, steel);
  p.rect(8, 10, 4, 2, bright);
  p.rect(20, 11, 4, 4, rust);
  p.rect(20, 11, 4, 2, amber);
  p.poly([[5, 19], [25, 19], [25, 25], [23, 27], [6, 27], [4, 25]], steel);
  p.poly([[25, 19], [27, 17], [27, 25], [25, 26]], cold);
  p.rect(7, 20, 11, 5, ink);
  p.rect(8, 21, 8, 3, ochre);
  p.rect(8, 21, 6, 2, yellow);
  p.rect(8, 21, 3, 1, gold);
  p.rect(20, 20, 3, 1, bright);
}

function ceramicJar(p) {
  contactShadow(p, 10);
  p.poly([[11, 7], [21, 7], [22, 12], [25, 15], [25, 21], [23, 26], [21, 28], [11, 28],
    [8, 26], [6, 22], [6, 17], [8, 13], [10, 12]], dark);
  p.poly([[11, 10], [20, 10], [21, 13], [24, 16], [24, 21], [22, 26], [20, 27], [12, 27],
    [9, 25], [7, 21], [7, 17], [9, 14]], ceramicShade);
  p.poly([[12, 11], [19, 12], [21, 15], [22, 20], [20, 24], [16, 25], [11, 24], [8, 21],
    [8, 17], [10, 14]], ceramic);
  p.poly([[12, 13], [17, 13], [18, 16], [16, 19], [11, 20], [9, 18], [10, 15]], ivory);
  p.ellipse(16, 8, 6, 3, ceramic);
  p.line(12, 7, 18, 7, ivory);
  p.ellipse(16, 8, 4, 1.5, dark);
  p.polyline([[8, 21], [11, 23], [17, 23], [20, 22], [22, 21]], slateBlue);
  p.px(11, 14, enamel);
  p.line(12, 26, 19, 26, ceramic);
}

function toolCase(p) {
  contactShadow(p, 14);
  p.poly([[12, 8], [21, 8], [22, 9], [22, 13], [19, 13], [19, 11], [14, 11], [14, 13], [11, 13], [11, 9]], dark);
  p.line(13, 9, 19, 9, sage);
  p.line(12, 10, 12, 12, moss);
  p.line(21, 10, 21, 12, moss);
  p.poly([[6, 12], [26, 12], [29, 15], [29, 25], [26, 28], [5, 28], [3, 26], [3, 15]], dark);
  p.poly([[7, 13], [25, 13], [28, 15], [26, 18], [5, 18], [4, 16]], moss);
  p.polyline([[5, 16], [7, 14], [23, 14]], sage);
  p.poly([[4, 19], [26, 19], [26, 25], [24, 27], [6, 27], [4, 25]], greenShade);
  p.poly([[26, 19], [28, 16], [28, 24], [26, 26]], cold);
  p.rect(6, 20, 18, 5, moss);
  p.line(7, 24, 23, 24, greenShade);
  p.rect(7, 17, 3, 5, rim);
  p.rect(21, 17, 3, 5, rim);
  p.rect(7, 17, 3, 2, enamel);
  p.rect(21, 17, 3, 2, enamel);
  p.polyline([[13, 23], [16, 20], [19, 20]], sage);
}

function dataCartridge(p) {
  contactShadow(p, 12);
  p.poly([[8, 10], [23, 10], [27, 14], [27, 24], [24, 28], [8, 28], [5, 25], [5, 13]], ink);
  p.poly([[8, 11], [23, 11], [26, 14], [26, 21], [6, 21], [6, 13]], plum);
  p.poly([[9, 11], [22, 11], [24, 14], [24, 19], [8, 19], [7, 14]], magenta);
  p.polyline([[7, 14], [9, 12], [21, 12]], rose);
  p.rect(9, 15, 12, 5, plum);
  p.rect(11, 16, 8, 3, rose);
  p.rect(11, 16, 6, 1, enamel);
  p.rect(23, 16, 3, 3, ink);
  p.poly([[6, 21], [26, 21], [26, 24], [23, 27], [9, 27], [6, 25]], steel);
  p.rect(9, 22, 14, 4, dark);
  for (let x = 10; x < 22; x += 3) {
    p.rect(x, 22, 2, 3, amber);
    p.rect(x, 22, 2, 1, gold);
  }
}

function signalGauge(p) {
  contactShadow(p, 12);
  p.poly([[9, 9], [13, 5], [20, 5], [25, 8], [28, 13], [28, 20], [24, 23], [25, 26], [23, 28],
    [8, 28], [6, 26], [8, 21], [6, 17], [6, 13]], dark);
  p.poly([[18, 7], [22, 7], [26, 10], [27, 14], [27, 19], [23, 22], [18, 22]], slateBlue);
  p.ellipse(15, 15, 9, 9, steel);
  p.ellipse(14, 14, 8, 8, iceBlue);
  p.ellipse(14, 14, 6, 6, cold);
  p.polyline([[10, 11], [12, 9], [16, 9], [19, 12]], enamel);
  p.line(14, 14, 17, 11, cyan);
  p.px(14, 14, glint);
  p.px(10, 15, iceBlue);
  p.px(18, 17, iceBlue);
  p.poly([[9, 22], [23, 22], [24, 25], [22, 27], [9, 27], [7, 25]], steel);
  p.rect(9, 24, 4, 2, bright);
  p.rect(20, 24, 3, 2, bright);
  p.rect(14, 25, 4, 2, dark);
}

const size = 32;
const columns = 3;
const props = [supplyBox, glassBottle, partsCanister, sampleVial, batteryCell, ceramicJar,
  toolCase, dataCartridge, signalGauge];
const rows = Math.ceil(props.length / columns);
const width = size * columns;
const height = size * rows;
const atlas = new Uint8Array(width * height * 4);
for (const [index, draw] of props.entries()) {
  const frame = createFrame(size, size);
  draw(new Painter(frame, 0, 0));
  const pixels = new Uint8Array(size * size * 4);
  composite(frame, pixels, null);
  const xOffset = index % columns * size;
  // Unity's slice coordinates start at the bottom, so added rows leave the first row in place.
  const yOffset = (rows - 1 - Math.floor(index / columns)) * size;
  for (let y = 0; y < size; y++) {
    atlas.set(pixels.subarray(y * size * 4, (y + 1) * size * 4), ((yOffset + y) * width + xOffset) * 4);
  }
}

writeFileSync(output, encodePNG(width, height, atlas));
