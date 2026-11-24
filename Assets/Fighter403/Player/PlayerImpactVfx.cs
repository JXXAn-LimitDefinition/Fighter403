using UnityEngine;

namespace Fighter403.Player
{
    internal sealed class PlayerImpactVfx
    {
        private const int TileSize = 80, FrameCount = 8;
        private const float Duration = 0.36f, FlashDuration = 0.16f;
        private static readonly int FlashTintId = Shader.PropertyToID("_FlashTint");
        private readonly RoundPlayerMovement movement;
        private readonly SpriteRenderer body, spark, silhouette;
        private readonly Material flashMaterial;
        private readonly Sprite[,] frames = new Sprite[2, FrameCount];
        private float startedAt = float.NegativeInfinity;
        private bool wounded;

        internal PlayerImpactVfx(RoundPlayerMovement movement)
        {
            this.movement = movement;
            body = movement.GetComponent<SpriteRenderer>();
            var sheet = Resources.Load<Texture2D>("Fighter403PlayerImpact");
            var shader = Resources.Load<Shader>("Fighter403PlayerHitSilhouette");
            if (body == null || sheet == null || shader == null ||
                sheet.width != TileSize * FrameCount || sheet.height != TileSize * 2) return;

            for (int row = 0; row < frames.GetLength(0); row++)
            for (int frame = 0; frame < FrameCount; frame++)
                frames[row, frame] = Sprite.Create(sheet,
                    new Rect(frame * TileSize, (1 - row) * TileSize, TileSize, TileSize),
                    Vector2.one * 0.5f, 32f, 0, SpriteMeshType.FullRect);

            flashMaterial = new Material(shader);
            silhouette = Create("Player hit flash", movement.transform, 1, flashMaterial);
            spark = Create("Player impact sparks", null, 2, body.sharedMaterial);
        }

        internal void Play(Vector2 direction, bool healthLost)
        {
            if (spark == null || !spark.gameObject.activeInHierarchy) return;
            float elapsed = Time.unscaledTime - startedAt;
            if (!healthLost && wounded && elapsed < 0.12f) return;
            Vector2 travel = direction.sqrMagnitude > 0.0001f ? direction.normalized : -movement.FacingDirection;
            Vector2 contact = movement.Position - travel * (movement.CombatRadius * 0.68f);
            spark.transform.SetPositionAndRotation(new Vector3(contact.x, contact.y, movement.transform.position.z),
                Quaternion.Euler(0f, 0f, Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg));
            startedAt = Time.unscaledTime;
            wounded = healthLost;
            Tick();
        }

        internal void Tick()
        {
            if (spark == null) return;
            float elapsed = Time.unscaledTime - startedAt;
            spark.enabled = elapsed < Duration;
            if (spark.enabled)
            {
                int frame = Mathf.Min(FrameCount - 1, (int)(elapsed / Duration * FrameCount));
                spark.sprite = frames[wounded ? 0 : 1, frame];
            }
            if (silhouette == null) return;
            silhouette.enabled = wounded && elapsed < FlashDuration && body != null && body.enabled;
            if (!silhouette.enabled) return;
            silhouette.sprite = body.sprite;
            silhouette.flipX = body.flipX;
            silhouette.flipY = body.flipY;
            float falloff = 1f - elapsed / FlashDuration;
            var tint = Color.Lerp(new Color(1f, 0.28f, 0.19f), Color.white, falloff);
            tint.a = falloff * falloff;
            flashMaterial.SetColor(FlashTintId, tint);
        }

        internal void Clear()
        {
            startedAt = float.NegativeInfinity;
            Tick();
        }

        internal void Dispose()
        {
            if (spark != null) Object.Destroy(spark.gameObject);
            if (silhouette != null) Object.Destroy(silhouette.gameObject);
            if (flashMaterial != null) Object.Destroy(flashMaterial);
            foreach (var sprite in frames)
                if (sprite != null) Object.Destroy(sprite);
        }

        private SpriteRenderer Create(string label, Transform parent, int order, Material material)
        {
            var renderer = new GameObject(label).AddComponent<SpriteRenderer>();
            renderer.gameObject.layer = body.gameObject.layer;
            renderer.transform.SetParent(parent, false);
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = body.sortingLayerID;
            renderer.sortingOrder = body.sortingOrder + order;
            renderer.enabled = false;
            return renderer;
        }
    }
}
