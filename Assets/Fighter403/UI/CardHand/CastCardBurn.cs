using System;
using UnityEngine;
using UnityEngine.UI;

namespace Fighter403.UI
{
    [RequireComponent(typeof(RectTransform), typeof(Canvas))]
    internal sealed class CastCardBurn : MonoBehaviour
    {
        private const int CardWidth = 54, CardHeight = 68;
        private const int PaddingX = 10, PaddingBottom = 6, PaddingTop = 21;
        private const int Width = CardWidth + 2 * PaddingX, Height = CardHeight + PaddingBottom + PaddingTop;
        private const float Ignition = 0.16f, Burned = 0.84f;
        private const float FirstEdge = -0.14f, LastEdge = 1.14f;

        private static readonly Color32 Paper = new(255, 255, 255, 255);
        private Color32 hotEdge;
        private Color32 glowingEdge;
        private Color32 soot;

        private readonly float[] burnOrder = new float[Width * Height];
        private readonly Color32[] maskPixels = new Color32[Width * Height];
        private readonly Color32[] effectPixels = new Color32[Width * Height];
        private readonly Fragment[] fragments = new Fragment[44];

        private RectTransform rect;
        private Texture2D maskTexture, effectTexture;
        private float startedAt, duration, horizontalOffset, renderedProgress = -1f;

        private struct Fragment
        {
            public Vector2 Origin, Drift;
            public float Birth, Lifetime;
            public byte Size;
            public bool Ember;
        }

        internal void Play(CardDefinition card, int energyCost, float offset, float castDuration)
        {
            startedAt = Time.time;
            duration = castDuration;
            horizontalOffset = offset;
            rect = (RectTransform)transform;
            rect.pivot = new Vector2(0.5f, 0f);
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;

            var source = card.GetComponent<CardHoverVisual>().Face;
            Vector2 size = source.rect.size;
            rect.sizeDelta = size;
            var cell = new Vector2(size.x / CardWidth, size.y / CardHeight);
            Vector2 paddedSize = Vector2.Scale(new Vector2(Width, Height), cell);
            var paddingOffset = Vector2.up * ((PaddingTop - PaddingBottom) * cell.y * 0.5f);

            maskTexture = MakeTexture("Card burn mask");
            effectTexture = MakeTexture("Card embers and ash");
            var stencil = MakeLayer("Burn silhouette", maskTexture, paddedSize, paddingOffset);
            stencil.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var face = Instantiate(source, stencil.rectTransform, false);
            face.anchorMin = face.anchorMax = face.pivot = new Vector2(0.5f, 0.5f);
            face.sizeDelta = size;
            face.anchoredPosition = -paddingOffset;
            CardHoverVisual.DressFace(face, card, energyCost);
            Color accent = CardHoverVisual.AttributeColor(card.Class);
            hotEdge = accent;
            glowingEdge = Color.Lerp(accent, Color.white, 0.66f);
            soot = Color.Lerp(new Color(0.02f, 0.025f, 0.045f, 0.84f), accent, 0.12f);
            foreach (var graphic in face.GetComponentsInChildren<Graphic>(true))
            {
                graphic.gameObject.layer = gameObject.layer;
                graphic.raycastTarget = false;
            }
            MakeLayer("Scorched edge and flying ash", effectTexture, paddedSize, paddingOffset);

            PrepareBurn();
            Update();
        }

        private void Update()
        {
            float progress = Mathf.Clamp01((Time.time - startedAt) / duration);
            rect.localPosition = new Vector3(horizontalOffset, 0.67f + 0.24f * progress, 0f);
            float width = Mathf.SmoothStep(0.98f, 1.14f, progress / Ignition);
            rect.localScale = Vector3.one * (width / rect.rect.width);
            if (progress != renderedProgress) Draw(progress);
        }

        private void PrepareBurn()
        {
            float seed = UnityEngine.Random.Range(0f, 96f);
            for (int y = 0; y < CardHeight; ++y)
            for (int x = 0; x < CardWidth; ++x)
            {
                float u = x / (CardWidth - 1f);
                float v = y / (CardHeight - 1f);
                float edgeNoise = (Mathf.PerlinNoise(seed + u * 5f, v * 5f) - 0.5f) * 0.28f;
                float fiber = (Mathf.PerlinNoise(u * 25f, seed + v * 25f) - 0.5f) * 0.075f;
                burnOrder[(y + PaddingBottom) * Width + x + PaddingX] = u * 0.32f + v * 0.68f + edgeNoise + fiber;
            }

            for (int i = 0; i < fragments.Length; ++i)
            {
                bool ember = i < 17;
                int x = UnityEngine.Random.Range(PaddingX + 2, PaddingX + CardWidth - 2);
                int y = UnityEngine.Random.Range(PaddingBottom + 2, PaddingBottom + CardHeight - 2);
                float burn = Mathf.InverseLerp(FirstEdge, LastEdge, burnOrder[y * Width + x]);
                fragments[i] = new Fragment
                {
                    Origin = new Vector2(x, y),
                    Birth = Mathf.Lerp(Ignition, Burned, burn),
                    Lifetime = UnityEngine.Random.Range(0.18f, 0.37f),
                    Drift = new Vector2(UnityEngine.Random.Range(-12f, 19f), UnityEngine.Random.Range(11f, 31f)),
                    Size = (byte)(ember ? 1 : UnityEngine.Random.Range(1, 4)),
                    Ember = ember
                };
            }
        }

        private void Draw(float progress)
        {
            renderedProgress = progress;
            float edge = Mathf.Lerp(FirstEdge, LastEdge, Mathf.InverseLerp(Ignition, Burned, progress));
            Array.Clear(effectPixels, 0, effectPixels.Length);

            for (int y = 0; y < CardHeight; ++y)
            for (int x = 0; x < CardWidth; ++x)
            {
                int index = (y + PaddingBottom) * Width + x + PaddingX;
                float distance = burnOrder[index] - edge;
                maskPixels[index] = distance > 0f ? Paper : default;
                if (distance <= 0f || distance > 0.081f) continue;

                // Keep the bright rim inside the surviving face.
                float flicker = Mathf.PerlinNoise(x * 0.31f, y * 0.18f + progress * 19f);
                float heatWidth = Mathf.Lerp(0.012f, 0.032f, flicker);
                if (distance < heatWidth)
                {
                    effectPixels[index] = Color32.Lerp(hotEdge, glowingEdge, flicker);
                }
                else
                {
                    var charColor = soot;
                    charColor.a = (byte)(soot.a * (1f - Mathf.InverseLerp(heatWidth, 0.081f, distance)));
                    effectPixels[index] = charColor;
                }
            }

            foreach (var fragment in fragments)
            {
                float age = (progress - fragment.Birth) / fragment.Lifetime;
                if (age <= 0f || age >= 1f) continue;
                Vector2 point = fragment.Origin + fragment.Drift * age;
                point.x += Mathf.Sin(age * 4.5f) * (fragment.Ember ? 1f : 3.5f);
                float opacity = (1f - age) * Mathf.Clamp01((1f - progress) * 12f);
                Color32 tint = fragment.Ember
                    ? Color32.Lerp(glowingEdge, hotEdge, age)
                    : Color32.Lerp(new Color32(110, 95, 84, 255), new Color32(164, 161, 155, 255), age);
                tint.a = (byte)(opacity * (fragment.Ember ? 235f : 156f));

                int left = Mathf.RoundToInt(point.x), bottom = Mathf.RoundToInt(point.y);
                for (int y = Mathf.Max(0, bottom); y < Mathf.Min(Height, bottom + fragment.Size); ++y)
                for (int x = Mathf.Max(0, left); x < Mathf.Min(Width, left + fragment.Size); ++x)
                {
                    int index = y * Width + x;
                    if (effectPixels[index].a < tint.a) effectPixels[index] = tint;
                }
            }

            maskTexture.SetPixels32(maskPixels);
            effectTexture.SetPixels32(effectPixels);
            maskTexture.Apply(false);
            effectTexture.Apply(false);
        }

        private UnityEngine.UI.RawImage MakeLayer(string label, Texture2D texture, Vector2 size, Vector2 offset)
        {
            var layer = new GameObject(label, typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            layer.layer = gameObject.layer;
            var image = layer.GetComponent<UnityEngine.UI.RawImage>();
            image.rectTransform.SetParent(rect, false);
            image.rectTransform.sizeDelta = size;
            image.rectTransform.anchoredPosition = offset;
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        private static Texture2D MakeTexture(string label) => new(Width, Height, TextureFormat.RGBA32, false, true)
        {
            name = label,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        private void OnDestroy()
        {
            if (maskTexture != null) Destroy(maskTexture);
            if (effectTexture != null) Destroy(effectTexture);
        }
    }
}
