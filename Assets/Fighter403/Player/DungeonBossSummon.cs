using UnityEngine;

namespace Fighter403.Player
{
    internal sealed class DungeonBossSummon : CombatTarget
    {
        private string displayName;
        private EnemyOverhead overhead;
        private bool anchored;

        public override string DisplayName => displayName;
        internal override bool Anchored => anchored || PositionLocked;
        internal bool PositionLocked { get; set; }

        internal void Initialize(string label, Sprite sprite, int health, float radius, SpriteRenderer bossBody,
            bool anchored, float artRadius)
        {
            displayName = label;
            this.anchored = anchored;
            var renderer = GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerID = bossBody.sortingLayerID;
            renderer.sortingOrder = bossBody.sortingOrder;
            if (artRadius <= 0f)
                artRadius = sprite == null ? radius : Mathf.Max(sprite.bounds.extents.x, sprite.bounds.extents.y);
            if (artRadius > 0f) transform.localScale = Vector3.one * (radius / artRadius);
            BodyCollider.radius = artRadius;
            BodyCollider.isTrigger = true;
            SetHealthCapacity(health);
            overhead = new EnemyOverhead(renderer, false, radius + .12f, Mathf.Max(.54f, radius * 2f));
        }

        internal void Dismiss()
        {
            if (Alive) Retire();
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (Alive) overhead?.Show((float)Health / MaxHealth, 0f, EnemyCue.None, 0f);
        }

        protected override void OnDefeated()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnDestroy() => overhead?.Dispose();
    }
}
