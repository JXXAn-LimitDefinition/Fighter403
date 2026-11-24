// Place this file in PixelFX's src/fx/fighter403 and run
// node tools/render.mjs fighter403/hud-meter-contact --sheet --no-actor
import { clamp, ease, fit } from "../../core/index.js";

export const meta = {
  name: "HUD meter contact",
  desc: "A short electrical contact at the changed edge of a meter breaks into a few directional pixel chips.",
  tags: ["ui", "meter", "sparks", "one-shot"],
  size: [48, 24],
  origin: [24, 12],
  duration: .48,
  loop: false,
  fps: 25,
  bg: "dark",
  options: { palette: ["#505c66", "#95a3af", "#d6e0e6", "#ffffff"] },
};

const chips = [
  [19, -6, 0, .34], [14, 5, .02, .28], [11, -8, .05, .31],
  [20, 2, .05, .31], [-7, 4, .01, .20],
];

export function draw(p, t, o) {
  const [shadow, edge, pale, hot] = fit(o.palette, 4);
  if (t < .16) {
    const intensity = 1 - t / .16;
    p.glow(0, 0, 8, pale, .32 * intensity);
    for (let y = -6; y <= 6; y++) p.blend(0, y, pale, intensity * (1 - Math.abs(y) / 9));
    for (let x = -3; x <= 9 + 3 * (1 - intensity); x++) p.blend(x, 0, edge, intensity);
    if (t < .08) p.rect(-1, -2, 3, 5, hot);
  }

  for (const [dx, dy, delay, lifetime] of chips) {
    const age = (t - delay) / lifetime;
    if (age < 0 || age >= 1) continue;
    const travel = ease.outCubic(clamp(age + .11));
    const x = Math.round(dx * travel), y = Math.round(dy * travel);
    const alpha = (1 - age) ** 1.5;
    const tail = Math.sign(dx) * Math.round(5 * alpha);
    for (let i = 1; i <= Math.abs(tail); i++)
      p.blend(x - i * Math.sign(dx), y, shadow, alpha * (1 - i / 7));
    p.blend(x, y, age < .30 ? hot : pale, alpha);
    if (dx === 19 && age < .35) p.blend(x + 1, y, pale, alpha * .78);
    p.glow(x, y, 3, edge, .18 * alpha);
  }
}
