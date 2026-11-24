using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Fighter403.Player
{
    public sealed partial class FirewallBoss
    {
        private static readonly Color Amber = new Color32(255, 171, 92, 255);
        private static readonly Color MeteorColor = new Color32(255, 124, 186, 255);
        private static readonly Color Overload = new Color32(255, 95, 89, 255);
        private static readonly Color PortalColor = new Color32(152, 243, 169, 255);
        private static readonly Color VortexColor = new Color32(110, 230, 241, 255);
        private static readonly int FlashId = Shader.PropertyToID("_Flash");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int SweepId = Shader.PropertyToID("_Sweep");
        private static readonly int DirectionId = Shader.PropertyToID("_Direction");
        private static readonly int WarningShapeId = Shader.PropertyToID("_Shape");
        private static readonly int WarningChargeId = Shader.PropertyToID("_Charge");
        private const int FrameSize = 112, Columns = 8, Rows = 14;
        private const float BodyScale = 1.8f;

        private sealed class Burst
        {
            internal LineRenderer Ring, Echo;
            internal SpriteRenderer[] Fragments;
            internal Vector2 Origin;
            internal Color Color;
            internal double Start;
            internal float Radius, Duration;
        }

        private readonly List<Burst> bursts = new();
        private readonly LineRenderer[] spirals = new LineRenderer[3];
        private readonly SpriteRenderer[] gates = new SpriteRenderer[4];
        private readonly Sprite[,] frames = new Sprite[Rows, Columns];
        private EnemyOverhead overhead;
        private SpriteRenderer core, rotor, chargeGlass;
        private Material lineMaterial, screenMaterial, warningMaterial;
        private MaterialPropertyBlock warningProperties;
        private Sprite warningQuad;
        private RawImage overlay;
        private TopDownCameraFollow shotCamera;
        private Color screenTint;
        private float screenEnergy;
        private Vector2 screenDirection;
        private double screenStart;
        private float coreHitAt = float.NegativeInfinity;
        private Transform effects;

        internal override void SetWorldHealthVisible(bool visible) => overhead.SetMetersVisible(visible);

        private void InitializeVisuals()
        {
            core = GetComponent<SpriteRenderer>();
            overhead = new EnemyOverhead(core, false, 3.05f, 4.5f);
            effects = new GameObject("Firewall effects").transform;
            effects.SetParent(transform, false);
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            warningMaterial = new Material(Resources.Load<Shader>("Fighter403FirewallWarning"));
            warningProperties = new MaterialPropertyBlock();
            var white = Texture2D.whiteTexture;
            warningQuad = Sprite.Create(white, new Rect(0, 0, white.width, white.height),
                Vector2.one * 0.5f, white.width, 0, SpriteMeshType.FullRect);
            if (animationSheet != null && animationSheet.width == FrameSize * Columns && animationSheet.height == FrameSize * Rows)
            {
                for (int row = 0; row < Rows; row++)
                for (int column = 0; column < Columns; column++)
                    frames[row, column] = Sprite.Create(animationSheet,
                        new Rect(column * FrameSize, (Rows - 1 - row) * FrameSize, FrameSize, FrameSize),
                        Vector2.one * 0.5f, row < 5 ? 32f / BodyScale : 32f, 0, SpriteMeshType.FullRect);
                core.sprite = frames[0, 0];
            }
            for (int gate = 0; gate < gates.Length; gate++)
            {
                gates[gate] = AddSprite("Armored gate", center, frames[5, 0], 3);
                gates[gate].transform.localScale = Vector3.one * BodyScale;
            }
            rotor = AddSprite("Reactor flywheel", center, frames[12, 0], 1);
            chargeGlass = AddSprite("Core energy filaments", center, frames[13, 0], 2);
            LoadPixelEffects();
        }

        private void CreateArena()
        {
            Vector2 half = ArenaHalfSize;
            var arena = new GameObject("Firewall Matrix trial arena").transform;
            arena.SetParent(transform, false);
            if (arenaSurface != null)
            {
                var surface = AddSprite("Firewall Matrix floor", center, arenaSurface, -13);
                surface.transform.SetParent(arena, true);
                Vector2 size = arenaSurface.bounds.size;
                surface.transform.localScale = new Vector3((half.x * 2f + 0.4f) / size.x,
                    (half.y * 2f + 0.4f) / size.y, 1f);
            }

            void Wall(Vector2 offset, Vector2 size)
            {
                var wall = new GameObject("Circuit barrier");
                wall.transform.SetParent(arena, false);
                wall.transform.localPosition = offset;
                wall.AddComponent<BoxCollider2D>().size = size;
            }
            const float width = 0.24f;
            Wall(new Vector2(0f, half.y + width / 2), new Vector2(half.x * 2f + width * 2f, width));
            Wall(new Vector2(0f, -half.y - width / 2), new Vector2(half.x * 2f + width * 2f, width));
            Wall(new Vector2(-half.x - width / 2, 0f), new Vector2(width, half.y * 2f));
            Wall(new Vector2(half.x + width / 2, 0f), new Vector2(width, half.y * 2f));

            var floor = Stroke("Circuit grid", new[]
            {
                center + new Vector2(-half.x, -half.y), center + new Vector2(-half.x, half.y),
                center + new Vector2(half.x, half.y), center + new Vector2(half.x, -half.y)
            }, true, -9, arena);
            SetLine(floor, new Color32(107, 148, 191, 163), 0.08f);
        }

        private void CreateScreenEffect(RectTransform parent)
        {
            var shader = Resources.Load<Shader>("Fighter403FirewallPulse");
            if (shader == null) return;
            screenMaterial = new Material(shader);
            var layer = new GameObject("Firewall overload screen pulse", typeof(RectTransform));
            layer.layer = parent.gameObject.layer;
            var rect = (RectTransform)layer.transform;
            rect.SetParent(parent, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            overlay = layer.AddComponent<RawImage>();
            overlay.material = screenMaterial;
            overlay.maskable = overlay.raycastTarget = false;
            overlay.enabled = false;
        }

        protected override void LateUpdate()
        {
            TickFeedback();
            TickPixelEffects();
            if (!Alive)
            {
                deathAge += Time.deltaTime;
                core.color = Color.white;
                if (frames[4, 0] != null) core.sprite = frames[4, Frame(deathAge / 0.72f)];
                core.enabled = deathAge < 0.72f;
                rotor.enabled = deathAge < 0.68f;
                rotor.transform.localScale = Vector3.one * (BodyScale + deathAge * 2.8f);
                rotor.transform.rotation = Quaternion.Euler(0f, 0f, idleAge * 61f + deathAge * 220f);
                rotor.color = new Color(1f, 0.59f, 0.4f, Mathf.Clamp01(1f - deathAge / 0.68f));
                chargeGlass.enabled = false;
                for (int gate = 0; gate < gates.Length; gate++)
                    PaintGate(gate, 0.4f + deathAge * 3.6f, deathAge * 180f * (gate % 2 == 0 ? 1f : -1f),
                        Amber, 0.1f, Mathf.Clamp01(1f - deathAge / 0.62f));
                return;
            }

            base.LateUpdate();
            int row = phaseTwo ? 1 : 0;
            int frame = (int)(idleAge * (phaseTwo ? 17f : 10f)) % Columns;
            if (transition > 0f) { row = 3; frame = Frame(1f - transition / TransitionTime); }
            else if (recovery > 0f) row = 2;
            if (frames[row, frame] != null) core.sprite = frames[row, frame];
            overhead.Show((float)Health / MaxHealth, 0f,
                Stunned ? EnemyCue.Stun : EnemyCue.None, idleAge * 5f % 1f);

            AnimateRig();
        }

        private void AnimateRig()
        {
            float heat = phaseTwo ? 0.32f : 0.12f;
            float overload = transition > 0f ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(1f - transition / TransitionTime)) : 0f;
            float hit = Mathf.Clamp01(1f - (Time.unscaledTime - coreHitAt) / 0.17f);
            Color hue = phaseTwo ? Overload : Amber;
            foreach (var cast in casts)
            {
                float activity = CastHeat(cast);
                if (activity <= heat) continue;
                heat = activity;
                hue = CastColor(cast.Skill);
            }
            heat = Mathf.Max(heat, overload);
            float open = recovery > 0f ? Mathf.Min(1f, recovery * 5f) * 0.7f : 0f;
            if (open > 0f) hue = VortexColor;
            float breathe = Mathf.Sin(idleAge * (phaseTwo ? 6.4f : 3.8f));
            float rotorAngle = idleAge * (phaseTwo ? 61f : 34f) + overload * 42f;
            rotor.enabled = true;
            rotor.sprite = frames[12, (int)(idleAge * 16f) % Columns];
            rotor.transform.rotation = Quaternion.Euler(0f, 0f, rotorAngle);
            rotor.transform.localScale = Vector3.one * (BodyScale + open * 0.14f + heat * 0.20f + breathe * 0.038f);
            rotor.color = Color.Lerp(Color.white, hue, heat * 0.65f);

            chargeGlass.enabled = true;
            chargeGlass.sprite = frames[13, (int)(idleAge * (phaseTwo ? 23f : 15f)) % Columns];
            chargeGlass.transform.rotation = Quaternion.Euler(0f, 0f, -rotorAngle * 1.3f);
            chargeGlass.transform.localScale = Vector3.one * (1.54f + breathe * 0.11f + heat * 0.38f + hit * 0.25f);
            Color glass = Color.Lerp(hue, Color.white, 0.45f + hit * 0.45f);
            glass.a = Mathf.Clamp01(0.18f + heat * 0.70f + hit * 0.55f);
            chargeGlass.color = glass;

            for (int gate = 0; gate < gates.Length; gate++)
            {
                float wave = Mathf.Sin(idleAge * (phaseTwo ? 7.2f : 4.0f) - gate * Mathf.PI * 0.5f);
                float spread = open + wave * 0.12f + overload * 0.48f + hit * 0.23f;
                float tilt = wave * 5f + overload * 17f * (gate % 2 == 0 ? 1f : -1f);
                float glow = Mathf.Max(phaseTwo ? 0.35f : 0.15f, overload);
                Color gateHue = hue;
                foreach (var cast in casts)
                {
                    float activity = CastHeat(cast);
                    float kick = 1f - Mathf.Clamp01((cast.Age - cast.LastRelease) / 0.38f);
                    switch (cast.Skill)
                    {
                        case Skill.Rain when gate == cast.RainEdge:
                            spread += activity * 0.32f - kick * 0.44f;
                            tilt += activity * 12f - kick * 22f;
                            break;
                        case Skill.Meteor:
                            spread += activity * 0.31f + kick * 0.28f;
                            tilt += Mathf.Sin(cast.Age * 9f - gate) * activity * 11f;
                            break;
                        case Skill.Deploy when gate % 2 == 1:
                            spread += activity * 0.61f;
                            tilt += (gate == 1 ? 1f : -1f) * activity * 19f;
                            break;
                        case Skill.Seal when gate != cast.SafeGate:
                            spread += kick * 0.95f - activity * 0.29f;
                            tilt += kick * 12f;
                            break;
                        case Skill.Vortex:
                            spread += wave * activity * 0.28f - activity * 0.19f + kick * 0.50f;
                            tilt -= activity * 24f;
                            break;
                        default:
                            continue;
                    }
                    if (activity <= glow) continue;
                    glow = activity;
                    gateHue = CastColor(cast.Skill);
                }
                PaintGate(gate, Mathf.Clamp(spread, -0.35f, 1.35f), tilt, gateHue, glow, 1f);
            }
        }

        private static float CastHeat(Cast cast)
        {
            float build = Mathf.Clamp01(cast.Age / 0.42f);
            float remaining = Mathf.Clamp01((cast.Duration - cast.Age) / 0.38f);
            return build * remaining * (0.67f + 0.29f * Mathf.Sin(cast.Age * 12f + 0.4f));
        }

        private static Color CastColor(Skill skill) => skill switch
        {
            Skill.Meteor => MeteorColor,
            Skill.Deploy => PortalColor,
            Skill.Seal => Overload,
            Skill.Vortex => VortexColor,
            _ => Amber
        };

        private void PaintGate(int gate, float separation, float tilt, Color hue, float glow, float opacity)
        {
            var renderer = gates[gate];
            if (renderer == null) return;
            float angle = GateAngle(gate);
            renderer.sprite = frames[5, glow > 0.48f ? 1 : 0];
            renderer.transform.SetPositionAndRotation(center + Polar(angle, 1.12f * BodyScale + separation),
                Quaternion.Euler(0f, 0f, angle - 90f + tilt));
            Color tint = Color.Lerp(core.color, hue, glow * 0.62f);
            tint.a = opacity;
            renderer.color = tint;
        }

        // Values passed to the pixel warning shader.
        private enum WarningShape { Meteor, Vortex, Sector, Portal, Launch }

        private Mark NewMark(string label, Vector2 point, WarningShape shape, float radius, float impact,
            float angle = 0f, float innerRadius = 0f)
        {
            float extent = radius + 0.17f;
            var renderer = AddSprite(label, point, warningQuad, -1);
            renderer.sharedMaterial = warningMaterial;
            renderer.transform.localScale = new Vector3(2f * extent, 2f * extent, 1f);
            renderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            if (shape == WarningShape.Sector)
            {
                renderer.transform.position = point + Polar(angle, extent * 0.5f);
                renderer.transform.localScale = new Vector3(extent, 2f * extent, 1f);
            }
            var mark = new Mark { Position = point, Impact = impact, Warning = renderer,
                WarningShape = new Vector4(radius, innerRadius, extent, (int)shape) };
            return mark;
        }

        private void PaintMark(Mark mark, Color color, float age, float time)
        {
            float remaining = mark.Impact - age;
            float charge = 1f - Mathf.Clamp01(remaining / time);
            float impact = remaining <= 0f ? Mathf.Clamp01(1f + remaining * 3.4f) : 0f;
            color.a = remaining > 0f ? 1f : impact;
            warningProperties.SetColor(TintId, color);
            warningProperties.SetVector(WarningShapeId, mark.WarningShape);
            warningProperties.SetFloat(WarningChargeId, charge);
            mark.Warning.SetPropertyBlock(warningProperties);
        }

        private void UpdateVortexSpirals(Cast cast)
        {
            for (int arm = 0; arm < spirals.Length; arm++)
            {
                if (spirals[arm] == null)
                    spirals[arm] = Stroke("Code spiral", new Vector2[22], false, -2);
                var line = spirals[arm];
                float spin = cast.Age * 144f + cast.Age * cast.Age * 32f + arm * 120f;
                float contraction = Mathf.SmoothStep(1f, 0.12f, Mathf.InverseLerp(2.46f, 2.76f, cast.Age));
                for (int i = 0; i < line.positionCount; i++)
                {
                    float t = i / (line.positionCount - 1f);
                    float radius = Mathf.Lerp(0.28f, VortexRadius, t);
                    line.SetPosition(i, cast.Center + Polar(spin - t * 205f, radius * contraction));
                }
                Color tint = VortexColor;
                tint.a = 0.36f * Mathf.Clamp01((cast.Duration - cast.Age) * 3f);
                SetLine(line, tint, 0.06f);
            }
        }

        private void ClearSpirals()
        {
            for (int i = 0; i < spirals.Length; i++)
            {
                Remove(spirals[i]);
                spirals[i] = null;
            }
        }

        private void Punch(float magnitude, float shakeTime, Color tint, float flash, Vector2 direction = default)
        {
            shotCamera?.Shake(magnitude, shakeTime);
            if (overlay == null) return;
            if (flash >= ScreenStrength(Time.unscaledTimeAsDouble))
            {
                screenStart = Time.unscaledTimeAsDouble;
                screenEnergy = flash;
                screenTint = tint;
                screenDirection = direction;
                PaintScreen(flash);
            }
        }

        private void Shock(Vector2 position, float radius, Color tint, float time)
        {
            var ring = Stroke("Impact wave", Circle(position, 0.12f, 36), true, 1);
            SetLine(ring, tint, 0.12f);
            var echo = Stroke("Impact wave echo", Circle(position, 0.10f, 36), true, -1);
            var fragments = new SpriteRenderer[radius > 4f ? 20 : 7];
            for (int i = 0; i < fragments.Length; i++)
                fragments[i] = AddSprite("Scattered code shard", position, frames[5, 5], 5);
            bursts.Add(new Burst
            {
                Ring = ring, Echo = echo, Fragments = fragments, Origin = position, Color = tint, Radius = radius,
                Duration = time, Start = Time.unscaledTimeAsDouble
            });
        }

        private void TickFeedback()
        {
            double now = Time.unscaledTimeAsDouble;
            if (overlay != null) PaintScreen(ScreenStrength(now));
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                Burst burst = bursts[i];
                float t = Mathf.Clamp01((float)(now - burst.Start) / burst.Duration);
                if (t >= 1f)
                {
                    RemoveBurst(burst);
                    bursts.RemoveAt(i);
                    continue;
                }
                float radius = Mathf.Lerp(0.12f, burst.Radius, 1f - (1f - t) * (1f - t));
                for (int point = 0; point < burst.Ring.positionCount; point++)
                {
                    Vector2 ray = Polar(360f * point / burst.Ring.positionCount, 1f);
                    burst.Ring.SetPosition(point, burst.Origin + ray * radius);
                    burst.Echo.SetPosition(point, burst.Origin + ray * radius * 0.76f);
                }
                Color tint = burst.Color;
                tint.a *= (1f - t) * 0.78f;
                SetLine(burst.Ring, Color.Lerp(tint, new Color(1f, 0.96f, 0.87f, tint.a), 0.47f * (1f - t)),
                    Mathf.Lerp(0.24f, 0.018f, t));
                SetLine(burst.Echo, new Color(tint.r, tint.g, tint.b, tint.a * 0.53f), Mathf.Lerp(0.12f, 0.02f, t));
                for (int shard = 0; shard < burst.Fragments.Length; shard++)
                {
                    var fragment = burst.Fragments[shard];
                    float angle = 32f + 360f * shard / burst.Fragments.Length;
                    float travel = radius * (0.42f + (shard % 4) * 0.12f);
                    fragment.transform.SetPositionAndRotation(burst.Origin + Polar(angle, travel),
                        Quaternion.Euler(0f, 0f, angle - 90f + t * 58f));
                    fragment.transform.localScale = Vector3.one * (0.95f - t * 0.64f);
                    fragment.color = tint;
                }
            }
        }

        private float ScreenStrength(double now)
        {
            float remaining = Mathf.Clamp01(1f - (float)(now - screenStart) / 0.47f);
            return screenEnergy * remaining * remaining;
        }

        private void PaintScreen(float amount)
        {
            overlay.enabled = amount > 0.001f;
            screenMaterial.SetFloat(FlashId, amount);
            screenMaterial.SetColor(TintId, screenTint);
            screenMaterial.SetFloat(SweepId, Mathf.Clamp01((float)(Time.unscaledTimeAsDouble - screenStart) / 0.47f));
            screenMaterial.SetVector(DirectionId, new Vector4(screenDirection.x, screenDirection.y, 0f, 0f));
        }

        private static void RemoveBurst(Burst burst)
        {
            Remove(burst.Ring);
            Remove(burst.Echo);
            foreach (var shard in burst.Fragments) Remove(shard);
        }

        private void ClearFeedback()
        {
            screenEnergy = 0f;
            if (overlay != null) overlay.enabled = false;
            foreach (var burst in bursts) RemoveBurst(burst);
            bursts.Clear();
            ClearPixelEffects();
        }

        private LineRenderer Stroke(string label, Vector2[] points, bool loop, int order, Transform parent = null)
        {
            var line = new GameObject(label).AddComponent<LineRenderer>();
            line.transform.SetParent(parent != null ? parent : effects, false);
            line.sharedMaterial = lineMaterial;
            line.sortingLayerID = core.sortingLayerID;
            line.sortingOrder = core.sortingOrder + order;
            line.loop = loop;
            line.positionCount = points.Length;
            line.numCornerVertices = 2;
            SetLine(line, Color.clear, 0.04f);
            for (int i = 0; i < points.Length; i++) line.SetPosition(i, points[i]);
            return line;
        }

        private static void SetLine(LineRenderer line, Color color, float width)
        {
            line.startColor = line.endColor = color;
            line.widthMultiplier = width;
        }

        private SpriteRenderer AddSprite(string label, Vector2 position, Sprite sprite, int order)
        {
            var renderer = new GameObject(label).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(effects, false);
            renderer.transform.position = position;
            renderer.sprite = sprite;
            renderer.sortingLayerID = core.sortingLayerID;
            renderer.sortingOrder = core.sortingOrder + order;
            renderer.sharedMaterial = core.sharedMaterial;
            return renderer;
        }

        private static Vector2 Polar(float angle, float radius)
        {
            angle *= Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private static Vector2[] Circle(Vector2 position, float radius, int count)
        {
            var points = new Vector2[count];
            for (int i = 0; i < count; i++)
                points[i] = position + Polar(360f * i / count, radius);
            return points;
        }

        private static int Frame(float t) => Mathf.Min(Columns - 1, (int)(Mathf.Clamp01(t) * Columns));

        private static void Remove(Component component)
        {
            if (component == null) return;
            component.gameObject.SetActive(false);
            Destroy(component.gameObject);
        }

        private void OnDestroy()
        {
            overhead?.Dispose();
            DisposePixelEffects();
            Remove(overlay);
            if (screenMaterial != null) Destroy(screenMaterial);
            if (lineMaterial != null) Destroy(lineMaterial);
            if (warningMaterial != null) Destroy(warningMaterial);
            if (warningQuad != null) Destroy(warningQuad);
            foreach (var sprite in frames)
                if (sprite != null) Destroy(sprite);
        }
    }
}
