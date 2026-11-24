using UnityEngine;

namespace Fighter403.Player
{
    [System.Serializable]
    internal sealed class SandevistanVfxSettings
    {
        [Min(0.05f), Tooltip("每道残影的寿命，单位为现实秒。")] public float trailLifetime = 1.1f;
        [Min(0.03f), Tooltip("移动多少个场景单位后留下下一道残影。")] public float trailSpacing = 0.46f;
        [Range(1, MaxAfterimages), Tooltip("同时可见的最大残影数。")] public int trailCount = 12;
        [Range(0f, 1f)] public float trailOpacity = 0.72f;
        [Range(0.3f, 4f), Tooltip("越小则残影保持可见的时间越久。")] public float trailFadePower = 1.35f;
        [Range(0f, 1f)] public float fringeOpacity = 0.52f;
        [Range(0f, 6f)] public float fringePixels = 2f;
        public Color nearColor = new Color32(116, 255, 244, 255);
        public Color middleColor = new Color32(92, 247, 154, 255);
        public Color farColor = new Color32(192, 240, 103, 255);

        [Header("全屏空间扭曲")]
        public bool screenEffect = true;
        [Min(0.05f)] public float pulseSeconds = 0.55f;
        [Range(0f, 2f)] public float pulseStrength = 1f;
        [Range(0f, 2f), Tooltip("外围景物沿径向拉伸的强度。")] public float radialBlur = 1.0f;
        [Range(0f, 1f), Tooltip("画面外围压暗的幅度。")] public float vignette = 0.72f;
        [Range(0f, 1f), Tooltip("背景去饱和及冷青调色的强度。")] public float coolTint = 0.58f;
        [Range(0f, 2f)] public float edgeWarp = 0.7f;
        [Range(0f, 2f)] public float edgeColorSplit = 0.45f;
        [Min(0.01f)] public float screenFadeOutSeconds = 0.65f;

        public const int MaxAfterimages = 16;
    }

    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class SandevistanAfterimages : MonoBehaviour
    {
        private const int Capacity = SandevistanVfxSettings.MaxAfterimages;

        private sealed class Afterimage
        {
            internal SpriteRenderer Body;
            internal SpriteRenderer Fringe;
            internal double BornAt;
        }

        private readonly Afterimage[] trail = new Afterimage[Capacity];
        private SpriteRenderer source;
        private CombatVitals vitals;
        private SandevistanVfxSettings settings;
        private Vector3 lastPosition;
        private int next;

        internal void Play(SpriteRenderer sprite, CombatVitals playerVitals, SandevistanVfxSettings visuals)
        {
            source = sprite;
            vitals = playerVitals;
            settings = visuals;
            lastPosition = sprite.transform.position;
        }

        private void LateUpdate()
        {
            if (vitals == null) return;
            if (source == null || !source.enabled || !source.gameObject.activeInHierarchy)
            {
                Clear();
                return;
            }

            double now = Time.unscaledTimeAsDouble;
            Vector3 position = source.transform.position;
            Vector2 delta = position - lastPosition;
            if (vitals.SandevistanActive && delta.sqrMagnitude >= settings.trailSpacing * settings.trailSpacing && source.sprite != null)
            {
                Capture(now, delta.normalized);
                lastPosition = position;
            }

            for (int i = 0; i < Capacity; i++)
            {
                int rank = (next + Capacity - 1 - i) % Capacity;
                var image = trail[rank];
                if (image == null || image.Body == null || !image.Body.enabled) continue;
                float age = (float)(now - image.BornAt) / settings.trailLifetime;
                if (age >= 1f || i >= settings.trailCount)
                {
                    image.Body.enabled = image.Fringe.enabled = false;
                    continue;
                }

                Color tint = age < 0.45f ? Color.Lerp(settings.nearColor, settings.middleColor, age / 0.45f)
                    : Color.Lerp(settings.middleColor, settings.farColor, (age - 0.45f) / 0.55f);
                float fade = Mathf.Pow(1f - age, settings.trailFadePower);
                tint.a *= settings.trailOpacity * fade;
                image.Body.color = tint;
                image.Body.sortingOrder = source.sortingOrder - 2 - i * 2;
                Color fringe = Color.Lerp(tint, age < 0.45f ? settings.middleColor : settings.farColor, 0.6f);
                fringe.a = settings.fringeOpacity * fade;
                image.Fringe.color = fringe;
                image.Fringe.sortingOrder = image.Body.sortingOrder - 1;
            }
        }

        private void Capture(double now, Vector2 direction)
        {
            var image = trail[next] ??= new Afterimage
            {
                Body = CreateRenderer("Sandevistan afterimage"),
                Fringe = CreateRenderer("Sandevistan color fringe")
            };
            image.BornAt = now;
            var pose = source.transform;
            float pixel = 1f / source.sprite.pixelsPerUnit;
            CopyPose(image.Body, pose.position);
            CopyPose(image.Fringe, pose.position - (Vector3)(direction * pixel * settings.fringePixels));
            next = (next + 1) % Capacity;
        }

        private SpriteRenderer CreateRenderer(string label)
        {
            var renderer = new GameObject(label).AddComponent<SpriteRenderer>();
            renderer.gameObject.layer = gameObject.layer;
            renderer.sharedMaterial = source.sharedMaterial;
            return renderer;
        }

        private void CopyPose(SpriteRenderer renderer, Vector3 position)
        {
            var pose = source.transform;
            renderer.transform.SetPositionAndRotation(position, pose.rotation);
            renderer.transform.localScale = pose.lossyScale;
            renderer.sprite = source.sprite;
            renderer.flipX = source.flipX;
            renderer.flipY = source.flipY;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.enabled = true;
        }

        internal void Clear()
        {
            vitals = null;
            foreach (var image in trail)
            {
                if (image == null) continue;
                if (image.Body != null) image.Body.enabled = false;
                if (image.Fringe != null) image.Fringe.enabled = false;
            }
        }

        private void OnDisable() => Clear();

        private void OnDestroy()
        {
            foreach (var image in trail)
            {
                if (image == null) continue;
                if (image.Body != null) Destroy(image.Body.gameObject);
                if (image.Fringe != null) Destroy(image.Fringe.gameObject);
            }
        }
    }
}
