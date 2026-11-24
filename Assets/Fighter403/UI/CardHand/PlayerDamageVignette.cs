using UnityEngine;

namespace Fighter403.UI
{
    internal sealed class PlayerDamageVignette
    {
        private const float HitDuration = 0.32f;
        private const float HitStrength = 0.81f;
        private static readonly int HitOpacityId = Shader.PropertyToID("_HitOpacity");
        private static readonly int CriticalOpacityId = Shader.PropertyToID("_CriticalOpacity");

        private readonly UnityEngine.UI.RawImage overlay;
        private readonly Material material;
        private double hitAt = double.NegativeInfinity;
        private float criticalOpacity;

        internal PlayerDamageVignette(RectTransform parent)
        {
            var shader = Resources.Load<Shader>("Fighter403PlayerDamageVignette");
            if (shader == null)
            {
                Debug.LogError("Fighter403PlayerDamageVignette shader was not found.", parent);
                return;
            }

            material = new Material(shader);
            var layer = new GameObject("Player damage vignette", typeof(RectTransform));
            layer.layer = parent.gameObject.layer;
            var rect = (RectTransform)layer.transform;
            rect.SetParent(parent, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            overlay = layer.AddComponent<UnityEngine.UI.RawImage>();
            overlay.material = material;
            overlay.raycastTarget = false;
            overlay.maskable = false;
            overlay.enabled = false;
        }

        internal void Update(bool tookDamage, bool critical)
        {
            if (overlay == null) return;

            double now = Time.unscaledTimeAsDouble;
            if (tookDamage) hitAt = now;
            float remaining = Mathf.Clamp01(1f - (float)((now - hitAt) / HitDuration));
            // Show the impact immediately and ease the black edge out in real time.
            float hit = HitStrength * remaining * remaining;
            criticalOpacity = Mathf.MoveTowards(criticalOpacity, critical ? 1f : 0f,
                Time.unscaledDeltaTime * (critical ? 8f : 5f));

            material.SetFloat(HitOpacityId, hit);
            material.SetFloat(CriticalOpacityId, criticalOpacity);
            overlay.enabled = hit > 0f || criticalOpacity > 0f;
        }

        internal void Clear()
        {
            hitAt = double.NegativeInfinity;
            criticalOpacity = 0f;
            if (overlay != null) overlay.enabled = false;
        }

        internal void Dispose()
        {
            if (overlay != null) Object.Destroy(overlay.gameObject);
            if (material != null) Object.Destroy(material);
        }
    }
}
