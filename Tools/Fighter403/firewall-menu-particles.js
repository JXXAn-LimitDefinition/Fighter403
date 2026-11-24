import { Rng, fit, smooth } from "../../core/index.js";

const duration = 4.8;
export const meta = {
  name: "Firewall standby particles",
  desc: "Fragments drift above the steel collar. Short cyan and copper contacts spit sparks from the outer seam.",
  tags: ["ui", "loop", "firewall", "sparks"],
  size: [192, 192],
  origin: [96, 96],
  duration,
  loop: true,
  fps: 15,
  bg: "dark",
  options: { seed: 403, palette: ["#1b3240", "#3e7482", "#71b8b7", "#aff5e7", "#edfff6"] },
};

const vents = [
  { angle: -2.54, radius: 75, delay: .07, copper: false },
  { angle: -.58, radius: 78, delay: .29, copper: false },
  { angle: .87, radius: 76, delay: .51, copper: true },
  { angle: 2.49, radius: 74, delay: .74, copper: false },
  { angle: -1.44, radius: 72, delay: .89, copper: false },
];
const copperRamp = fit(["#492d25", "#905537", "#d39057", "#ffc381", "#fff3d1"], 5);

function cycle(phase) {
  return phase - Math.floor(phase);
}

export function draw(p, t, o) {
  const phase = cycle(t / duration);
  const teal = fit(o.palette, 5);
  const random = new Rng(o.seed);
  for (let i = 0; i < 44; i++) {
    const angle = random.range(-Math.PI, Math.PI);
    const radius = random.range(31, 82);
    const life = cycle(phase + random.range(0, 1));
    const rise = random.range(10, 27);
    const drift = random.range(-11, 12);
    const shimmer = Math.sin(Math.PI * life) ** 2;
    const x = Math.cos(angle) * radius + drift * (life - .5);
    const y = Math.sin(angle) * radius - rise * (life - .5);
    const bright = i % 4 === 0;
    const tint = i % 11 === 0 ? copperRamp : teal;
    p.blend(x, y, bright ? tint[3] : tint[1], shimmer * (bright ? .79 : .54));
    if (bright) p.blend(x + 1, y, tint[2], shimmer * .45);
    if (i % 10 === 0) p.glow(x, y, 3, tint[2], shimmer * .19);
  }

  for (const vent of vents) {
    const seconds = cycle(phase - vent.delay) * duration;
    const ramp = vent.copper ? copperRamp : teal;
    const x = Math.cos(vent.angle) * vent.radius;
    const y = Math.sin(vent.angle) * vent.radius;
    const arcFlare = smooth(0, .075, seconds) * (1 - smooth(.15, .41, seconds));
    if (arcFlare > 0) {
      p.arc(0, 0, vent.radius, 1.8, vent.angle - .082, vent.angle + .079, ramp[3], arcFlare * .78);
      p.glow(x, y, 7, ramp[2], arcFlare * .42);
    }
    for (let i = 0; i < 10; i++) {
      const heading = vent.angle + random.range(-.72, .72);
      const speed = random.range(13, 39);
      const lifetime = random.range(.19, .49);
      const driftY = random.range(4, 13);
      const dt = seconds - (.09 + i * .025);
      if (dt <= 0 || dt >= lifetime) continue;
      const age = dt / lifetime;
      const fade = smooth(0, .045, dt) * (1 - smooth(.28, 1, age));
      const travel = dt * speed * (1 - age * .30);
      const vx = Math.cos(heading), vy = Math.sin(heading);
      const px = x + vx * travel, py = y + vy * travel - driftY * dt * dt;
      if (i % 3 === 0) p.line(px - vx * 4, py - vy * 4, px, py, ramp[2], fade * .86);
      p.blend(px, py, age < .32 ? ramp[4] : ramp[2], fade * .96);
      if (i % 4 === 0) p.glow(px, py, 3, ramp[2], fade * .23);
    }
  }
}

