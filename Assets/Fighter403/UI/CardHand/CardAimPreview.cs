using UnityEngine;
using Fighter403.Player;

namespace Fighter403.UI
{
    internal sealed class CardAimPreview
    {
        private readonly Vector3[] outlinePoints = new Vector3[CardAttackArea.MaxOutlinePoints];
        private readonly int sortingLayer, sortingOrder;
        private LineRenderer outline, bearing;
        private Material material;

        internal CardAimPreview(int sortingLayer = 0, int sortingOrder = 9)
        {
            this.sortingLayer = sortingLayer;
            this.sortingOrder = sortingOrder;
        }

        internal void Show(Vector3 origin, Vector2 direction, CardDefinition card, Vector2? target,
            Vector2? dashEnd = null)
        {
            var shape = card.HitShape;
            var center = (Vector2)origin;
            if (shape == CardHitShape.Landing && target.HasValue) center = target.Value;
            Vector2? bearingEnd = shape switch
            {
                CardHitShape.Dash => dashEnd ?? center + direction * card.Distance,
                CardHitShape.Triangle or CardHitShape.Arc => center + direction * card.Distance,
                CardHitShape.Landing or CardHitShape.Lock => target,
                _ => null
            };
            if (shape == CardHitShape.Dash && bearingEnd == center) bearingEnd = null;

            var color = CardHoverVisual.AttributeColor(card.Class);
            color.a = 0.88f;
            ShowArea(new CardAttackArea(card, center, direction, dashEnd), origin.z, color);
            if (bearingEnd.HasValue && (bearing != null || TryCreateLine(ref bearing, "Card aim axis", false)))
            {
                color.a = 0.45f;
                bearing.startColor = bearing.endColor = color;
                bearing.SetPosition(0, origin);
                bearing.SetPosition(1, new Vector3(bearingEnd.Value.x, bearingEnd.Value.y, origin.z));
                bearing.enabled = true;
            }
        }

        internal void ShowArea(CardAttackArea area, float z, Color color, float width = 0.028f)
        {
            int count = area.WriteOutline(outlinePoints, z);
            ShowOutline(count, color, width);
        }

        internal void ShowArea(CardCombat.FlurryBladeArea blade, float z, Color color, float width)
        {
            blade.WriteOutline(outlinePoints, z);
            ShowOutline(4, color, width);
        }

        private void ShowOutline(int count, Color color, float width)
        {
            if (bearing != null) bearing.enabled = false;
            if (count == 0)
            {
                if (outline != null) outline.enabled = false;
                return;
            }

            if (outline == null && !TryCreateLine(ref outline, "Card range", true)) return;
            outline.positionCount = count;
            for (int i = 0; i < count; i++) outline.SetPosition(i, outlinePoints[i]);
            outline.startColor = outline.endColor = color;
            outline.widthMultiplier = width;
            outline.enabled = true;
        }

        private bool TryCreateLine(ref LineRenderer line, string label, bool loop)
        {
            if (material == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) return false;
                material = new Material(shader);
            }
            line = new GameObject(label).AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.loop = loop;
            line.positionCount = 2;
            line.widthMultiplier = 0.028f;
            line.sortingLayerID = sortingLayer;
            line.sortingOrder = sortingOrder;
            return true;
        }

        internal void Hide()
        {
            if (outline != null) outline.enabled = false;
            if (bearing != null) bearing.enabled = false;
        }

        internal void Dispose()
        {
            if (outline != null) Object.Destroy(outline.gameObject);
            if (bearing != null) Object.Destroy(bearing.gameObject);
            if (material != null) Object.Destroy(material);
        }
    }
}
