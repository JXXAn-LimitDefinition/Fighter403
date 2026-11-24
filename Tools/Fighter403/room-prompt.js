// Place this file in PixelFX's src/fx/fighter403 and run
// node tools/render.mjs fighter403/room-prompt --sheet
import { hex } from "../../core/index.js";

export const meta = {
  name: "Fighter403 room prompt",
  desc: "A stepped pixel keycap on a compact room action prompt.",
  tags: ["ui", "keycap", "interact"],
  size: [72, 18],
  origin: [36, 9],
  duration: 1,
  loop: true,
  fps: 1,
};

const [shadow, plate, rim, socket, key, bevel, accent, glyph] =
  ["#050a10", "#14212e", "#2b3e51", "#101a26", "#2b4350", "#4d6877", "#7ddde6", "#e1f4f3"].map(hex);

export function draw(p) {
  // Unity stretches the flat area between the left keycap and the right cut.
  p.poly([[-34,-7],[30,-7],[35,-2],[35,5],[32,8],[-33,8],[-36,5],[-36,-5]], shadow);
  p.poly([[-34,-8],[30,-8],[35,-3],[35,4],[32,7],[-33,7],[-36,4],[-36,-5]], rim);
  p.poly([[-33,-7],[29,-7],[34,-2],[34,3],[31,6],[-32,6],[-35,3],[-35,-4]], plate);

  p.poly([[-32,-6],[-22,-6],[-20,-4],[-20,4],[-21,5],[-33,5],[-34,4],[-34,-4]], socket);
  p.poly([[-31,-5],[-22,-5],[-21,-4],[-21,3],[-22,4],[-32,4],[-33,3],[-33,-3]], key);
  p.line(-31, -5, -24, -5, bevel);
  p.line(-31, 4, -23, 4, accent);

  p.rect(-29, -4, 1, 7, glyph);
  p.rect(-28, -4, 4, 1, glyph);
  p.rect(-28, -1, 3, 1, glyph);
  p.rect(-28, 2, 4, 1, glyph);
}
