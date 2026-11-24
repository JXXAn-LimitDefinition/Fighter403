using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fighter403.Player
{
    internal sealed class EnemyDamageNumber : MonoBehaviour
    {
        internal const float Duration = 0.52f;
        private const float BaseScale = 0.62f, SettleTime = 0.09f, FadeStart = 0.30f;
        private const int CellWidth = 16, CellHeight = 20, DigitCount = 10;
        private const float PixelsPerUnit = 32f;

        private static readonly Vector2[] LaneOffsets =
        {
            new(0.49f, 0f), new(-0.49f, 0f),
            new(0.88f, 0.34f), new(-0.88f, 0.34f),
            new(0f, 0.81f)
        };
        private static readonly Sprite[] Digits = new Sprite[DigitCount];
        internal static int LaneCount => LaneOffsets.Length;

        private SpriteRenderer[] renderers;
        private Vector3 origin;
        private float startTime, sideways;

        internal static void Show(SpriteRenderer body, Vector3 origin, int amount, int lane)
        {
            if (Digits[0] == null && !LoadDigits()) return;

            var number = new GameObject("Enemy damage number");
            var scene = body.gameObject.scene;
            if (scene != number.scene && scene.isLoaded) SceneManager.MoveGameObjectToScene(number, scene);

            var popup = number.AddComponent<EnemyDamageNumber>();
            popup.Initialize(body, origin, amount.ToString(CultureInfo.InvariantCulture), lane);
        }

        private void Initialize(SpriteRenderer body, Vector3 anchor, string digits, int lane)
        {
            renderers = new SpriteRenderer[digits.Length];
            Vector2 offset = LaneOffsets[lane];
            origin = anchor + (Vector3)offset;
            sideways = offset.x;
            startTime = Time.unscaledTime;

            float width = 0f;
            foreach (char digit in digits) width += Advance(digit);
            float cursor = -width * 0.5f;

            for (int i = 0; i < digits.Length; i++)
            {
                var digitObject = new GameObject($"Digit {digits[i]}") { layer = body.gameObject.layer };
                digitObject.transform.SetParent(transform, false);
                float advance = Advance(digits[i]);
                digitObject.transform.localPosition = Vector3.right * (cursor + advance * 0.5f);
                cursor += advance;

                var renderer = digitObject.AddComponent<SpriteRenderer>();
                renderer.sharedMaterial = body.sharedMaterial;
                renderer.sortingLayerID = body.sortingLayerID;
                renderer.sortingOrder = body.sortingOrder + 120;
                renderer.sprite = Digits[digits[i] - '0'];
                renderers[i] = renderer;
            }

            Animate(0f);
        }

        private void LateUpdate()
        {
            float age = Time.unscaledTime - startTime;
            if (age >= Duration)
            {
                Destroy(gameObject);
                return;
            }

            Animate(age);
        }

        private void Animate(float age)
        {
            float timeLeft = 1f - age / Duration;
            float rise = 1f - timeLeft * timeLeft;
            transform.position = origin + new Vector3(sideways * rise * 0.22f, rise * 0.34f, 0f);
            float settle = 1f - Mathf.Clamp01(age / SettleTime);
            transform.localScale = Vector3.one * (BaseScale * (1f + 0.10f * settle * settle));

            if (age < FadeStart) return;
            var tint = new Color(1f, 1f, 1f, 1f - Mathf.InverseLerp(FadeStart, Duration, age));
            foreach (var renderer in renderers) renderer.color = tint;
        }

        private static float Advance(char digit) => (digit == '1' ? 9f : 13f) / PixelsPerUnit;

        private static bool LoadDigits()
        {
            // The atlas's top row contains PixelFX's 5x7 glyphs in the damage palette.
            var atlas = Resources.Load<Texture2D>("Fighter403DamageDigits");
            if (atlas == null || atlas.width != CellWidth * DigitCount || atlas.height != CellHeight * 2)
            {
                Debug.LogError("Fighter403DamageDigits has unexpected atlas dimensions.");
                return false;
            }

            for (int digit = 0; digit < DigitCount; digit++)
            {
                var rect = new Rect(digit * CellWidth, CellHeight, CellWidth, CellHeight);
                Digits[digit] = Sprite.Create(atlas, rect, new Vector2(0.5f, 0f),
                    PixelsPerUnit, 0, SpriteMeshType.FullRect, Vector4.zero, false);
            }
            return true;
        }
    }
}
