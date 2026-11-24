import { WHITE } from "../../core/index.js";

export const meta = {
  name: "Firewall ambient particle tiles",
  desc: "Four neutral pixel silhouettes for tinting in the menu: dust, a metal fragment, a glint and a fast spark.",
  tags: ["ui", "firewall", "particles"],
  size: [64, 16],
  origin: [32, 8],
  duration: 1,
  loop: true,
  fps: 1,
  bg: "dark",
};

// Each silhouette occupies one padded 16 x 16 tile. Unity tints the cores and halos together.
export function draw(p) {
  const dust = -24;
  p.glow(dust + .5, .5, 2.6, WHITE, .13);
  p.blend(dust, 0, WHITE, .94);
  p.blend(dust - 1, 0, WHITE, .24);

  const fragment = -8;
  p.glow(fragment + .5, .5, 3.8, WHITE, .18);
  p.blend(fragment + 1, -1, WHITE, .86);
  p.blend(fragment, -1, WHITE, .95);
  p.blend(fragment, 0, WHITE, 1);
  p.blend(fragment - 1, 0, WHITE, .63);
  p.blend(fragment - 1, 1, WHITE, .33);

  const glint = 8;
  p.glow(glint + .5, .5, 5.6, WHITE, .22);
  p.blend(glint - 2, 0, WHITE, .27);
  p.blend(glint - 1, 0, WHITE, .74);
  p.blend(glint + 1, 0, WHITE, .7);
  p.blend(glint + 2, 0, WHITE, .24);
  p.blend(glint, -2, WHITE, .35);
  p.blend(glint, -1, WHITE, .78);
  p.blend(glint, 1, WHITE, .54);
  p.blend(glint, 0, WHITE, 1);

  const spark = 24;
  p.glow(spark + 1, -.5, 4.4, WHITE, .24);
  const trail = [
    [-7, 3, .17], [-6, 3, .29], [-5, 2, .39], [-4, 2, .52],
    [-3, 1, .68], [-2, 1, .75], [-1, 0, .91], [0, 0, 1],
    [1, -1, .96], [2, -1, .79], [0, -1, .61], [-2, 0, .42],
  ];
  for (const [x, y, opacity] of trail) p.blend(spark + x, y, WHITE, opacity);
}

