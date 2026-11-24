using UnityEngine;

namespace Fighter403.Player
{
    internal enum EnemyCue { None = -1, Combat, Windup, Release, Stun, ShieldBreak, Slow, Fracture }

    internal sealed class EnemyOverhead
    {
        private const int FrameWidth = 36, FrameHeight = 28, Columns = 8;
        private const float PixelsPerUnit = 32f, Height = 1.06f, BarWidth = 34f / PixelsPerUnit;
        private static readonly Color Outline = new Color32(20, 18, 33, 248);
        private static readonly Color HealthTrack = new Color32(62, 38, 48, 240);
        private static readonly Color ShieldTrack = new Color32(50, 42, 78, 245);
        private static readonly Color HealthFill = new Color32(237, 112, 55, 255);
        private static readonly Color DamageTrail = new Color32(255, 164, 65, 255);
        private static readonly Color ShieldFill = new Color32(186, 151, 255, 255);

        private static Sprite[,] cueFrames;
        private static Sprite pixel;
        private static int users;

        private readonly Transform root, meters;
        private readonly SpriteRenderer body, icon, pulse;
        private readonly Meter health, shield;
        private readonly float height, barWidth;

        internal EnemyOverhead(SpriteRenderer body, bool hasShield, float height = Height, float barWidth = BarWidth)
        {
            this.body = body;
            this.height = height;
            this.barWidth = barWidth;
            if (users++ == 0) LoadSprites();
            root = new GameObject("Enemy overhead").transform;
            root.SetParent(body.transform, false);
            meters = new GameObject("Meters").transform;
            meters.SetParent(root, false);
            health = new Meter(this, hasShield ? 3f / PixelsPerUnit : 0f, HealthTrack, HealthFill, 4f, animate: true);
            if (hasShield) shield = new Meter(this, -3f / PixelsPerUnit, ShieldTrack, ShieldFill, 3f);
            icon = Create("Status", 5, null, Color.white);
            icon.transform.localPosition = new Vector3(0f, 0.57f, 0f);
            icon.enabled = false;
            pulse = Create("Support effect", 3, pixel, Color.white);
            pulse.transform.localPosition = new Vector3(0f, -0.23f, 0f);
            pulse.transform.localScale = new Vector3(22f / PixelsPerUnit, 2f / PixelsPerUnit, 1f);
            pulse.enabled = false;
        }

        internal void SetPulse(Color color, float strength)
        {
            pulse.enabled = strength > 0f;
            color.a *= strength;
            pulse.color = color;
        }

        internal void Show(float healthFraction, float shieldFraction, EnemyCue cue, float phase)
        {
            if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
            root.SetPositionAndRotation(body.transform.position + Vector3.up * height, Quaternion.identity);
            health.Set(healthFraction, Time.unscaledTime);
            shield?.SetImmediate(shieldFraction);
            icon.enabled = cue != EnemyCue.None && cueFrames != null;
            if (icon.enabled)
            {
                int frame = Mathf.Clamp(Mathf.FloorToInt(phase * Columns), 0, Columns - 1);
                icon.sprite = cueFrames[(int)cue, frame];
            }
        }

        internal void Hide() => root.gameObject.SetActive(false);

        internal Vector3 DamageNumberOrigin => body.transform.position + Vector3.up * (height + 0.16f);

        internal void SetMetersVisible(bool visible) => meters.gameObject.SetActive(visible);

        internal void Reset()
        {
            SetPulse(Color.clear, 0f);
            health.SetImmediate(1f);
            shield?.SetImmediate(1f);
            Show(1f, 1f, EnemyCue.None, 0f);
        }

        private SpriteRenderer Create(string name, int order, Sprite sprite, Color color, Transform parent = null)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(parent ?? root, false);
            renderer.sharedMaterial = body.sharedMaterial;
            renderer.sortingLayerID = body.sortingLayerID;
            renderer.sortingOrder = body.sortingOrder + 110 + order;
            renderer.sprite = sprite;
            renderer.color = color;
            return renderer;
        }

        private static void LoadSprites()
        {
            pixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f,
                1f, 0, SpriteMeshType.FullRect, Vector4.zero, false);
            var sheet = Resources.Load<Texture2D>("Fighter403EnemyStatus");
            int rows = (int)EnemyCue.Fracture + 1;
            if (sheet == null || sheet.width != FrameWidth * Columns || sheet.height != FrameHeight * rows)
            {
                Debug.LogError("Fighter403EnemyStatus has unexpected frame dimensions.");
                return;
            }

            cueFrames = new Sprite[rows, Columns];
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < Columns; column++)
            {
                var rect = new Rect(column * FrameWidth, (rows - row - 1) * FrameHeight, FrameWidth, FrameHeight);
                cueFrames[row, column] = Sprite.Create(sheet, rect, Vector2.one * 0.5f,
                    PixelsPerUnit, 0, SpriteMeshType.FullRect, Vector4.zero, false);
            }
        }

        internal void Dispose()
        {
            if (--users != 0) return;
            Object.Destroy(pixel);
            pixel = null;
            if (cueFrames == null) return;
            foreach (var sprite in cueFrames) Object.Destroy(sprite);
            cueFrames = null;
        }

        private sealed class Meter
        {
            private const float FillTime = 0.14f, TrailDelay = 0.17f, TrailTime = 0.32f;
            private readonly SpriteRenderer fill, highlight;
            private readonly SpriteRenderer trail;
            private readonly float y, height;
            private readonly float barWidth;
            private float fraction = 1f, displayed = 1f, trailing = 1f;
            private float previousDisplay, previousTrail, changedAt;

            internal Meter(EnemyOverhead overhead, float y, Color track, Color color, float pixels, bool animate = false)
            {
                this.y = y;
                height = pixels / PixelsPerUnit;
                barWidth = overhead.barWidth;
                Place(overhead.Create("Meter outline", 0, pixel, Outline, overhead.meters), 0f, y,
                    barWidth + 4f / PixelsPerUnit, height + 2f / PixelsPerUnit);
                Place(overhead.Create("Meter track", 1, pixel, track, overhead.meters), 0f, y, barWidth, height);
                if (animate) trail = overhead.Create("Damage trail", 2, pixel, DamageTrail, overhead.meters);
                fill = overhead.Create("Meter fill", 3, pixel, color, overhead.meters);
                highlight = overhead.Create("Meter highlight", 4, pixel, Color.Lerp(color, Color.white, 0.43f), overhead.meters);
                Draw();
            }

            internal void Set(float value, float now)
            {
                value = Mathf.Clamp01(value);
                if (value > fraction)
                {
                    SetImmediate(value);
                    return;
                }

                float elapsed = now - changedAt;
                if (displayed != fraction || trailing != fraction)
                {
                    displayed = Mathf.Lerp(previousDisplay, fraction, EaseOut(elapsed / FillTime));
                    trailing = Mathf.Lerp(previousTrail, fraction, EaseOut((elapsed - TrailDelay) / TrailTime));
                    Draw();
                }

                if (value == fraction) return;
                previousDisplay = displayed;
                previousTrail = trailing;
                fraction = value;
                changedAt = now;
            }

            internal void SetImmediate(float value)
            {
                value = Mathf.Clamp01(value);
                if (fraction == value && displayed == value && trailing == value) return;
                previousDisplay = previousTrail = fraction = displayed = trailing = value;
                Draw();
            }

            private void Draw()
            {
                SetWidth(fill, displayed, y, height);
                SetWidth(highlight, displayed, y + height * 0.5f - 0.5f / PixelsPerUnit, 1f / PixelsPerUnit);
                if (trail != null)
                {
                    SetWidth(trail, trailing, y, height);
                    trail.enabled = trailing - displayed > 0.001f;
                }
            }

            private static float EaseOut(float progress)
            {
                float remaining = 1f - Mathf.Clamp01(progress);
                return 1f - remaining * remaining * remaining;
            }

            private void SetWidth(SpriteRenderer renderer, float fraction, float y, float height)
            {
                renderer.enabled = fraction > 0f;
                float width = barWidth * fraction;
                Place(renderer, (width - barWidth) * 0.5f, y, width, height);
            }

            private static void Place(SpriteRenderer renderer, float x, float y, float width, float height)
            {
                renderer.transform.localPosition = new Vector3(x, y, 0f);
                renderer.transform.localScale = new Vector3(width, height, 1f);
            }
        }
    }
}
