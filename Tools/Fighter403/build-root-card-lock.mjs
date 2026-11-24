import { join } from "node:path";
import { pathToFileURL } from "node:url";

const [library, output] = process.argv.slice(2);
if (!library || !output) throw new Error("Usage: node root-card-lock.mjs <pixelfx directory> <preview directory>");

const { clamp, ease, fit, lerp, TAU } =
  await import(pathToFileURL(join(library, "src/core/index.js")));
const { contactSheet, renderFrames, save, spriteSheet, writeVideo } =
  await import(pathToFileURL(join(library, "tools/lib/fxlib.mjs")));

const SealFrames = 10;
const HoldFrames = 20;
export const meta = {
  name: "Root card seal",
  desc: "Cyan verification traces dock four violet clamps onto a closed padlock before its plate delaminates into data strips.",
  tags: ["ui", "boss", "lock"],
  size: [96, 96], origin: [48, 48], bg: "dark",
  fps: 30, duration: 40 / 30, loop: false,
  options: { palette: ["#100e24", "#272045", "#554382", "#9681db", "#9ed8ff", "#edf7ff"] },
};

function plate(p, color, alpha) {
  p.poly([[-25,-13],[-13,-25],[13,-25],[25,-13],[25,13],[13,25],[-13,25],[-25,13]], color[0], alpha);
  p.poly([[-23,-12],[-12,-23],[12,-23],[23,-12],[23,12],[12,23],[-12,23],[-23,12]], color[1], alpha);
  p.polyline([[-22,11],[-22,-11],[-11,-22],[11,-22],[22,-11]], color[3], alpha);
  p.polyline([[22,-11],[22,11],[11,22],[-11,22],[-22,11]], color[2], alpha);
  p.rect(-15, -15, 30, 2, color[2], alpha * .55);
  p.rect(-15, 15, 30, 2, color[0], alpha);
  p.rect(-24, -2, 3, 5, color[3], alpha);
  p.rect(21, -2, 3, 5, color[2], alpha);
}

function latch(p, color, x, side, alpha) {
  p.poly([[x, -4], [x + side * 3, -5], [x + side * 6, -2], [x + side * 6, 3],
    [x + side * 3, 5], [x, 4]], color[0], alpha);
  p.polyline([[x, -4], [x + side * 3, -5], [x + side * 6, -2], [x + side * 6, 3]],
    color[3], alpha);
  p.line(x, 1, x + side * 4, 1, color[4], .85 * alpha);
}

function padlock(p, color, offset, alpha, flash) {
  const top = -18 + offset;
  p.poly([[-10,0],[-10,top+7],[-5,top],[5,top],[10,top+7],[10,0]], color[2], alpha);
  p.poly([[-8,-1],[-8,top+7],[-4,top+2],[4,top+2],[8,top+7],[8,-1]], color[4], alpha);
  p.poly([[-5,0],[-5,top+8],[-2,top+5],[2,top+5],[5,top+8],[5,0]], color[0], alpha);
  p.poly([[-14,0],[-11,-3],[11,-3],[14,0],[14,10],[10,14],[-10,14],[-14,10]], color[2], alpha);
  p.poly([[-13,1],[-10,-1],[10,-1],[13,1],[13,9],[10,12],[-10,12],[-13,9]], flash ? color[5] : color[3], alpha);
  p.rect(-9, 0, 18, 2, color[4], alpha);
  p.line(-10, 2, -10, 7, color[4], alpha * .72);
  p.line(9, 2, 9, 7, color[2], alpha * .9);
  p.rect(-8, 8, 3, 1, color[5], alpha * .6);
  p.rect(5, 8, 3, 1, color[2], alpha * .8);
  p.rect(-10, 10, 20, 2, color[2], alpha);
  p.rect(-2, 3, 4, 4, color[0], alpha);
  p.rect(-1, 6, 2, 3, color[0], alpha);
  p.dot(-5, top + 3, color[5], alpha);
}

function brackets(p, color, span, alpha, phase) {
  for (let corner = 0; corner < 4; corner++) {
    const x = corner % 2 ? span : -span;
    const y = corner < 2 ? -span : span;
    const dx = Math.sign(x), dy = Math.sign(y);
    const hot = Math.cos(phase - corner * Math.PI / 2) > .55;
    p.polyline([[x-10*dx,y],[x-3*dx,y],[x,y-3*dy],[x,y-10*dy]], color[2], alpha);
    p.polyline([[x-7*dx,y+dy],[x-3*dx,y+dy],[x+dx,y-3*dy],[x+dx,y-7*dy]], color[hot ? 5 : 3], alpha);
    p.dot(x+3*dx, y+3*dy, color[4], alpha * .7);
  }
}

export function draw(p, t, o) {
  const tick = t * 30;
  if (tick >= 40) return;
  const color = fit(o.palette, 6);
  const closing = clamp(tick / (SealFrames - 1));
  const idle = clamp((tick - SealFrames) / HoldFrames);
  const opening = clamp((tick - SealFrames - HoldFrames) / (10 - 1));
  const visibility = (1 - ease.inQuad(opening)) * Math.min(1, tick / 3);
  const phase = idle * TAU;
  const span = Math.round(29 + 11 * (1 - ease.outQuart(closing)) + 10 * opening);
  const flash = tick >= 5 && tick < 7;
  if (flash) p.glow(0, -5, 25, color[4], .23);
  plate(p, color, visibility);
  const spread = 23 + 10 * (1 - ease.outQuart(closing)) + 11 * opening;
  for (const side of [-1, 1]) {
    latch(p, color, side * spread, side, visibility * .93);
    p.polyline([[side * spread, 0], [side * (spread - 6), 0], [side * (spread - 8), 2], [side * 13, 2]],
      color[3], visibility * .55);
    const inset = 14 + 5 * (1 - closing);
    p.dot(side * inset, 2, color[4], visibility * (flash ? 1 : .52));
  }
  const travel = clamp((tick - 4) / 6);
  if (travel > 0 && travel < 1) {
    const reach = 13 + 17 * ease.outQuad(travel);
    for (const side of [-1, 1]) {
      p.polyline([[side * 12, -3], [side * (reach - 5), -3], [side * reach, -8]],
        color[4], (1 - travel) * visibility);
      p.dot(side * reach, -8, color[5], (1 - travel) * visibility);
    }
  }
  if (visibility > .10)
    padlock(p, color, Math.round(-5 * (1 - closing) - 4 * opening), visibility, flash);
  brackets(p, color, span, visibility, phase);
  const shimmer = Math.round(lerp(-16, 16, (1 - Math.cos(phase)) * .5));
  p.rect(shimmer, -21, 2, 1, color[4], visibility * .7);
  p.rect(-shimmer, 21, 2, 1, color[3], visibility * .7);
  const perimeter = [[-12, -21], [12, -21], [21, -12], [21, 12],
    [12, 21], [-12, 21], [-21, 12], [-21, -12]];
  for (let i = 0; i < perimeter.length; i++) {
    const [x, y] = perimeter[i];
    const energized = .45 + .3 * Math.cos(phase - i * TAU / perimeter.length);
    p.rect(x - 1, y, 2, 1, color[4], visibility * energized);
  }

  const pulse = tick < SealFrames ? clamp((tick - 4) / 6) : opening;
  if (pulse > 0 && pulse < 1) {
    const distance = 25 + 16 * ease.outQuad(pulse);
    for (let burst = 0; burst < 8; burst++) {
      const angle = (burst + .5) * TAU / 8;
      const x = Math.round(Math.cos(angle) * distance);
      const y = Math.round(Math.sin(angle) * distance);
      p.rect(x, y, burst % 2 ? 2 : 4, 2, color[burst % 2 ? 4 : 3], 1 - pulse);
    }
    if (opening > 0) {
      const slip = Math.round(4 + opening * 7);
      p.rect(-24 - slip, -8, 12, 2, color[4], 1 - opening);
      p.rect(12 + slip, 8, 12, 2, color[3], 1 - opening);
      p.rect(-14 - slip, 13, 9, 1, color[3], .77 * (1 - opening));
      p.rect(5 + slip, -15, 10, 1, color[4], .77 * (1 - opening));
      p.rect(21 + slip, -4, 5, 2, color[4], .73 * (1 - opening));
      p.rect(-26 - slip, 4, 5, 2, color[3], .73 * (1 - opening));
    }
  }
}


const id = "fighter403/root-card-lock";
const effect = { id, mod: { meta, draw }, meta };
const rendered = renderFrames(effect);
const sheet = spriteSheet(effect, rendered, "RootCardLock");
save(join(output, "RootCardLock.png"), sheet.png);
save(join(output, "RootCardLock.json"), JSON.stringify(sheet.json, null, 2));
save(join(output, "root-card-lock-contact.png"), contactSheet(effect, rendered, { max: 12, zoom: 2 }));
writeVideo(join(output, "root-card-lock.gif"), rendered, 2);
