using System.Collections.Generic;
using UnityEngine;

namespace Fighter403.UI
{
    [DisallowMultipleComponent]
    internal sealed class DashPredictionVisual : MonoBehaviour
    {
        private static readonly Color GhostColor = new(1f, 0.83f, 0.42f, 0.46f);
        private static readonly Color PathColor = new(1f, 0.83f, 0.42f, 0.78f);
        private static readonly Color AreaColor = new(1f, 0.83f, 0.42f, 0.5f);

        private SpriteRenderer source;
        private SpriteRenderer ghost;
        private LineRenderer path;
        private Material pathMaterial;
        private readonly List<CardAimPreview> areas = new();

        internal Transform GhostTransform => ghost != null ? ghost.transform : null;

        internal void Initialize(SpriteRenderer sourceRenderer)
        {
            source = sourceRenderer;
            if (ghost != null) return;

            var ghostObject = new GameObject("Dash prediction ghost", typeof(SpriteRenderer));
            ghostObject.layer = sourceRenderer.gameObject.layer;
            ghostObject.transform.SetParent(transform, false);
            ghost = ghostObject.GetComponent<SpriteRenderer>();
            ghost.sortingLayerID = sourceRenderer.sortingLayerID;
            ghost.sortingOrder = sourceRenderer.sortingOrder + 1;
            ghost.sharedMaterial = sourceRenderer.sharedMaterial;
            ghost.color = GhostColor;
            ghost.enabled = false;

            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            pathMaterial = new Material(shader);
            var pathObject = new GameObject("Dash prediction path", typeof(LineRenderer));
            pathObject.layer = sourceRenderer.gameObject.layer;
            pathObject.transform.SetParent(transform, false);
            path = pathObject.GetComponent<LineRenderer>();
            path.sharedMaterial = pathMaterial;
            path.startColor = path.endColor = PathColor;
            path.startWidth = path.endWidth = 0.045f;
            path.sortingLayerID = sourceRenderer.sortingLayerID;
            path.sortingOrder = sourceRenderer.sortingOrder - 1;
            path.useWorldSpace = true;
            path.enabled = false;
        }

        internal void Show(IReadOnlyList<Vector2> points)
        {
            if (ghost == null || source == null || points == null || points.Count < 2)
            {
                Hide();
                return;
            }

            Vector2 destination = points[points.Count - 1];
            for (int i = points.Count - 1; i < areas.Count; i++) areas[i].Hide();
            ghost.transform.position = new Vector3(destination.x, destination.y, source.transform.position.z);
            ghost.transform.rotation = source.transform.rotation;
            ghost.transform.localScale = source.transform.lossyScale;
            ghost.enabled = true;
            if (path == null) return;

            path.positionCount = points.Count;
            for (int i = 0; i < points.Count; ++i)
                path.SetPosition(i, new Vector3(points[i].x, points[i].y, source.transform.position.z));
            path.enabled = true;
        }

        internal void ShowDamageArea(int index, CardAttackArea area)
        {
            if (index >= areas.Count) areas.Add(new CardAimPreview(source.sortingLayerID, source.sortingOrder - 1));
            areas[index].ShowArea(area, source.transform.position.z, AreaColor);
        }

        internal void Hide()
        {
            if (ghost != null) ghost.enabled = false;
            if (path != null) path.enabled = false;
            foreach (var area in areas) area.Hide();
        }

        private void LateUpdate()
        {
            if (ghost == null || source == null || !ghost.enabled) return;
            ghost.sprite = source.sprite;
            ghost.flipX = source.flipX;
            ghost.flipY = source.flipY;
        }

        private void OnDestroy()
        {
            foreach (var area in areas) area.Dispose();
            if (pathMaterial != null) Destroy(pathMaterial);
        }
    }
}
