using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Fighter403.Player
{
    internal sealed partial class RootBoss
    {
        private static readonly int OverlayOpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int OverlayAgeId = Shader.PropertyToID("_Age");
        private readonly PixelEffect[] guardShields = new PixelEffect[4];
        private RawImage killOverlay;
        private Material killOverlayMaterial;
        private Texture2D guardTexture;
        private Sprite guardSprite;
        private float bodyAngle;

        private void UpdateBodyMotion(float dt)
        {
            float turn = transition > 0f ? 100f : 24f;
            if (vulnerable <= 0f && !Stunned)
            {
                bodyAngle = (bodyAngle + dt * turn) % 360f;
                transform.rotation = Quaternion.Euler(0f, 0f, bodyAngle);
            }
            for (int i = 0; i < guards.Count; i++)
            {
                PixelEffect panel = guardShields[i];
                if (panel == null || panel.Object == null) continue;
                if (guards[i] == null || !guards[i].Alive)
                {
                    Vector2 corner = ArenaCenter + GuardOffset(i);
                    Pixel(PixelCue.GuardCrack, corner, 1.05f, .48f, Body.sortingOrder + 2);
                    Pixel(PixelCue.GuardCrack, panel.Object.transform.position, .84f, .48f,
                        Body.sortingOrder + 2);
                    DestroyEffect(panel.Object);
                    guardShields[i] = null;
                    continue;
                }

                float angle = bodyAngle + i * 90f;
                panel.Object.transform.SetPositionAndRotation(
                    Position + Rotate(Vector2.right, angle) * 1.8f, Quaternion.Euler(0f, 0f, angle));
            }
        }

        private Vector2 GuardOffset(int index)
        {
            Vector2 offset = ArenaHalfSize - Vector2.one * 2.1f;
            return Vector2.Scale(offset, new Vector2(index < 2 ? -1f : 1f, index % 2 == 0 ? -1f : 1f));
        }

        private void ShowGuardShield()
        {
            for (int i = 0; i < guardShields.Length; i++)
            {
                guardShields[i] = Pixel(PixelCue.ShieldTile, Position, 1.05f, .83f,
                    Body.sortingOrder + 1, loop: true);
                if (guardShields[i] != null) guardShields[i].Age = i * .21f;
            }
            UpdateBodyMotion(0f);
        }

        private void RemoveGuardShield()
        {
            for (int i = 0; i < guardShields.Length; i++)
            {
                if (guardShields[i] != null) DestroyEffect(guardShields[i].Object);
                guardShields[i] = null;
            }
        }

        private GameObject Marker(string label, Vector2 location) =>
            Telegraph(label, Rectangle(location, Vector2.one * .1f), Color.clear, 0f);

        private GameObject SectorMark(Vector2 origin, Vector2 direction, bool released)
        {
            const float span = SectorRadius * 2f + .12f;
            string label = released ? "Root 越权释放" : "Root 越权判定";
            var mark = Telegraph(label, Sector(origin, direction), PhaseViolet, released ? .38f : .18f);
            Floor(mark, released ? FloorCue.SectorStrike : FloorCue.SectorWarning, origin,
                new Vector2(span, span), direction, PhaseViolet, .32f, released);
            if (released) afterglows.Add((mark, .32f));
            return mark;
        }

        private GameObject BlinkMarker(string label, Vector2 location)
        {
            var mark = Telegraph(label, Circle(location, HurtRadius, 8), new Color32(106, 105, 196, 162), .12f);
            AttachedPixel(PixelCue.BlinkTarget, mark, location, 1.5f, BlinkWarningSeconds, -7);
            return mark;
        }

        private void AddDeletionStrip(List<GameObject> marks, string label, Vector2 position, Vector2 half,
            Color tint, float fill, Vector2 outward, bool activated)
        {
            GameObject mark = Telegraph(label, Rectangle(position, half), tint, fill);
            marks.Add(mark);
            bool horizontal = Mathf.Abs(outward.y) > .5f;
            Vector2 sizes = 2f * (horizontal ? half : new Vector2(half.y, half.x));
            var edgeDirection = new Vector2(outward.y, -outward.x);
            Floor(mark, activated ? FloorCue.DeletedBand : FloorCue.DeletionWarning, position,
                sizes, edgeDirection, tint);

            float offset = horizontal ? half.y : half.x;
            float extension = horizontal ? half.x : half.y;
            Vector2 boundary = position - outward * offset;
            PixelCue cue = activated ? PixelCue.DeletedBand : PixelCue.DeletionWarning;
            float angle = Angle(edgeDirection);
            for (int i = 0; i < 3; i++)
            {
                float fraction = (i - 1) * .58f;
                var glyph = AttachedPixel(cue, mark, boundary + edgeDirection * (extension * fraction),
                    1.03f, .93f, -8, angle, loop: true);
                if (glyph != null) glyph.Age = i * .2f;
            }
        }

        private void UpdateChildKillMarks()
        {
            foreach (var child in children)
            {
                if (child.Target == null || !child.Target.Alive) continue;
                Vector2 position = child.Target.transform.position;
                if (child.KillMark == null)
                {
                    child.KillMark = Pixel(PixelCue.KillReticle, position, 1.22f, .48f, -7, loop: true);
                }
                else
                {
                    child.KillMark.Object.transform.position = position;
                }
            }
        }

        private static void ClearChildKillMark(ChildProcess child)
        {
            if (child.KillMark == null) return;
            DestroyEffect(child.KillMark.Object);
            child.KillMark = null;
        }

        private void CreateGuardArt()
        {
            if (guardSprite != null) return;
            string[] pattern =
            {
                "................",
                ".......11.......",
                "......1221......",
                ".....125421.....",
                "....12565421....",
                "...1256765421...",
                "..125678765421..",
                ".12567888765421.",
                ".12456888654221.",
                "..124567654221..",
                "...1235654221...",
                "....12254221....",
                ".....122221.....",
                "......1111......",
                ".......11.......",
                "................",
            };
            Color32[] palette =
            {
                new(0, 0, 0, 0), new(18, 14, 31, 255), new(43, 29, 66, 255),
                new(75, 50, 106, 255), new(103, 75, 152, 255), new(154, 125, 216, 255),
                new(163, 198, 246, 255), new(192, 231, 255, 255), new(235, 252, 255, 255)
            };
            int size = pattern.Length;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                char value = pattern[size - y - 1][x];
                if (value != '.') pixels[y * size + x] = palette[value - '0'];
            }

            guardTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "守护进程",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            guardTexture.SetPixels32(pixels);
            guardTexture.Apply(false, true);
            guardSprite = Sprite.Create(guardTexture, new Rect(0f, 0f, size, size), Vector2.one * .5f, 16f);
        }

        private void SetKillOverlay(float opacity)
        {
            if (killOverlay == null)
            {
                if (opacity <= 0f || Hud == null) return;
                var shader = Resources.Load<Shader>("Fighter403RootKillOverlay");
                if (shader == null) return;
                killOverlayMaterial = new Material(shader);
                var layer = new GameObject("Root kill -9 / screen edge", typeof(RectTransform));
                layer.layer = Hud.gameObject.layer;
                var rect = (RectTransform)layer.transform;
                rect.SetParent(Hud, false);
                rect.SetAsFirstSibling();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                killOverlay = layer.AddComponent<RawImage>();
                killOverlay.material = killOverlayMaterial;
                killOverlay.raycastTarget = false;
                killOverlay.maskable = false;
            }

            killOverlayMaterial.SetFloat(OverlayOpacityId, opacity);
            killOverlayMaterial.SetFloat(OverlayAgeId, cast?.Age ?? 0f);
            killOverlay.enabled = opacity > 0f;
        }

        private void DestroyKillOverlay()
        {
            if (killOverlay != null)
            {
                killOverlay.enabled = false;
                Destroy(killOverlay.gameObject);
            }
            if (killOverlayMaterial != null) Destroy(killOverlayMaterial);
        }

        private void TickAfterglows(float dt)
        {
            for (int i = afterglows.Count - 1; i >= 0; i--)
            {
                var (mark, remaining) = afterglows[i];
                remaining -= dt;
                if (remaining <= 0f)
                {
                    RemoveTelegraph(mark);
                    afterglows.RemoveAt(i);
                }
                else
                    afterglows[i] = (mark, remaining);
            }
        }

        private static Vector2[] Circle(Vector2 center, float radius, int sides)
        {
            var points = new Vector2[sides];
            for (int i = 0; i < sides; i++)
                points[i] = center + Rotate(Vector2.up, 360f * i / sides) * radius;
            return points;
        }

        private static Vector2[] Rectangle(Vector2 center, Vector2 half) =>
            new[]
            {
                center + new Vector2(-half.x, -half.y), center + new Vector2(half.x, -half.y),
                center + new Vector2(half.x, half.y), center + new Vector2(-half.x, half.y)
            };

        private static Vector2[] Sector(Vector2 center, Vector2 direction)
        {
            const int segments = 24;
            var points = new Vector2[segments + 2];
            points[0] = center;
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(-SectorHalfAngle, SectorHalfAngle, i / (float)segments);
                points[i + 1] = center + Rotate(direction, angle) * SectorRadius;
            }
            return points;
        }
    }
}
