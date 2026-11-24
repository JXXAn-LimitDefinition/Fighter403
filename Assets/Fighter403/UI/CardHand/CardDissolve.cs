using UnityEngine;
using UnityEngine.UI;

namespace Fighter403.UI
{
    internal sealed class CardDissolve : MonoBehaviour
    {
        private const int Columns = 22, Rows = 32;
        private const float Duration = 0.38f;
        private Texture2D maskTexture;
        private Sprite maskSprite;
        private float elapsed;
        private int lastStep = -1;
        private readonly float[] thresholds = new float[Columns * Rows];
        private readonly Color32[] pixels = new Color32[Columns * Rows];
        internal HandCard Card { get; private set; }

        internal void Play(CardHoverVisual view, HandCard card)
        {
            Card = card;
            view.LockVisual();
            RectTransform face = view.Face;
            var stencil = new GameObject("Pixel dissolve mask", typeof(RectTransform), typeof(Image), typeof(Mask));
            stencil.layer = gameObject.layer;
            var rect = (RectTransform)stencil.transform;
            rect.SetParent(face.parent, false);
            rect.localPosition = face.localPosition;
            rect.localRotation = face.localRotation;
            rect.localScale = face.localScale;
            rect.pivot = face.pivot;
            rect.sizeDelta = face.rect.size + new Vector2(12f, 16f);
            face.SetParent(rect, true);

            maskTexture = new Texture2D(Columns, Rows, TextureFormat.RGBA32, false, true)
            {
                name = "Card dissolve pixels",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            maskSprite = Sprite.Create(maskTexture, new Rect(0, 0, Columns, Rows), Vector2.one * 0.5f,
                100f, 0, SpriteMeshType.FullRect);
            var image = stencil.GetComponent<Image>();
            image.sprite = maskSprite;
            image.raycastTarget = false;
            stencil.GetComponent<Mask>().showMaskGraphic = false;

            // PixelFX's pixel dissolve groups neighboring cells so the face breaks away in patches.
            float seed = Random.Range(0f, 127f);
            for (int y = 0; y < Rows; ++y)
            for (int x = 0; x < Columns; ++x)
            {
                float patch = Mathf.PerlinNoise(seed + x * 0.18f, y * 0.18f);
                float fleck = Mathf.PerlinNoise(x * 1.37f, seed + y * 1.73f);
                thresholds[y * Columns + x] = 0.48f * patch + 0.27f * fleck + 0.24f * (1f - y / (Rows - 1f));
            }
            UpdateMask(0f);
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= Duration)
            {
                Destroy(gameObject);
                return;
            }
            UpdateMask(elapsed / Duration);
        }

        private void UpdateMask(float progress)
        {
            int step = Mathf.FloorToInt(progress * 16f);
            if (step == lastStep) return;
            lastStep = step;
            float cutoff = Mathf.Lerp(0.16f, 0.82f, step / 15f);
            for (int i = 0; i < pixels.Length; ++i)
                pixels[i] = new Color32(255, 255, 255, thresholds[i] > cutoff ? (byte)255 : (byte)0);
            maskTexture.SetPixels32(pixels);
            maskTexture.Apply(false);
        }

        private void OnDestroy()
        {
            if (maskSprite != null) Destroy(maskSprite);
            if (maskTexture != null) Destroy(maskTexture);
        }
    }
}
