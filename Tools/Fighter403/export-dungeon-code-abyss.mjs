import { writeFileSync } from "node:fs";
import { join } from "node:path";
import { pathToFileURL } from "node:url";

const [library, output] = process.argv.slice(2);
if (!library || !output)
  throw new Error("Usage: node export-dungeon-code-abyss.mjs <pixelfx directory> <Unity texture directory>");

// Copy the companion effect to PixelFX's src/fx/fighter403 before exporting.
const { loadEffect, renderFrames } = await import(pathToFileURL(join(library, "tools/lib/fxlib.mjs")));
const { encodePNG } = await import(pathToFileURL(join(library, "tools/lib/png.mjs")));
const effect = await loadEffect("fighter403/dungeon-code-abyss");
const [width, height] = effect.meta.size;
const layouts = [10, 5, 5];
const columns = 8;
const rows = 12;
const atlasWidth = columns * width;
const atlasHeight = rows * height;

for (const [theme, file] of [
  ["cache", "CodeAbyss.png"],
  ["firewall", "CodeAbyssFirewall.png"],
  ["core", "CodeAbyssCore.png"],
]) {
  const atlas = new Uint8Array(atlasWidth * atlasHeight * 4);
  let frameIndex = 0;
  for (const [layout, fps] of layouts.entries()) {
    const { frames } = renderFrames(effect, { fps, options: { theme, layout } });
    for (const frame of frames) {
      const x = frameIndex % columns * width;
      const y = Math.floor(frameIndex / columns) * height;
      for (let row = 0; row < height; row++) {
        const source = row * width * 4;
        const destination = ((y + row) * atlasWidth + x) * 4;
        atlas.set(frame.clear.subarray(source, source + width * 4), destination);
      }
      frameIndex++;
    }
  }
  if (frameIndex !== columns * rows)
    throw new Error(`Expected ${columns * rows} frames in ${file}, got ${frameIndex}`);
  writeFileSync(join(output, file), encodePNG(atlasWidth, atlasHeight, atlas));
}
