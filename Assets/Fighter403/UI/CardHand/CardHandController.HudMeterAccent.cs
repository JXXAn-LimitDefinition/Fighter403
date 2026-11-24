using UnityEngine;
using UnityEngine.UI;

namespace Fighter403.UI
{
    public sealed partial class CardHandController
    {
        private sealed class HudMeterAccent
        {
            // PixelFX sheet generated from Tools/Fighter403/hud-meter-contact.js.
            private const int Columns = 3;
            private const int Rows = 5;
            private const int VisibleFrames = 12;
            private const float FramesPerSecond = 25f;

            private readonly RawImage image;
            private float startedAt;
            private int lastFrame;

            private HudMeterAccent(RawImage image)
            {
                this.image = image;
            }

            public static HudMeterAccent Create(RectTransform track, Color tint, Vector2? size = null)
            {
                var rect = Box("Meter contact sparks", track, new Vector2(0f, .5f), Vector2.zero,
                    size ?? new Vector2(48f, 24f));
                var image = rect.gameObject.AddComponent<RawImage>();
                image.texture = Resources.Load<Texture2D>("Fighter403HudMeterContact");
                image.raycastTarget = false;
                image.color = tint;
                rect.gameObject.SetActive(false);
                return new HudMeterAccent(image);
            }

            public void Play(float fraction, int direction)
            {
                if (image.texture == null) return;
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(Mathf.Clamp01(fraction), .5f);
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = new Vector3(direction < 0 ? -1f : 1f, 1f, 1f);
                startedAt = Time.unscaledTime;
                DrawFrame(0);
                image.gameObject.SetActive(true);
            }

            public void Tick()
            {
                if (!image.gameObject.activeSelf) return;
                int frame = Mathf.FloorToInt((Time.unscaledTime - startedAt) * FramesPerSecond);
                if (frame >= VisibleFrames)
                {
                    Hide();
                    return;
                }
                if (frame != lastFrame) DrawFrame(frame);
            }

            private void DrawFrame(int frame)
            {
                lastFrame = frame;
                image.uvRect = new Rect((frame % Columns) / (float)Columns, (Rows - 1 - frame / Columns) / (float)Rows,
                    1f / Columns, 1f / Rows);
            }

            public void Hide() => image.gameObject.SetActive(false);
        }
    }
}
