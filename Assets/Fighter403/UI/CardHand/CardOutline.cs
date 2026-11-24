using UnityEngine;
using UnityEngine.UI;

namespace Fighter403.UI
{
    internal sealed class CardOutline : MaskableGraphic
    {
        private const float Corner = 9f, RimOffset = 1.5f, Thickness = 1.8f, SweepDuration = 1.55f;
        private const float ShadeCorner = 8f, EdgeThickness = 2f;
        private static readonly Color Rim = new Color32(192, 202, 219, 225);
        private static readonly Color32 Shade = new(8, 13, 24, 174);
        private static readonly Color32 ReadinessEdge = new(160, 175, 201, 205);
        private static readonly Color Orange = new(1f, 0.44f, 0.14f);
        private static readonly Color Highlight = new(1f, 0.91f, 0.61f);
        private static readonly Color RewardHighlight = new(0.99f, 1f, 1f);
        private static readonly Color Denied = new(1f, 0.24f, 0.29f);
        private float readiness = 1f;
        private Color readyFlash;
        private bool flowing, rewardStyle;
        private float rewardStarted;

        internal void SetReadiness(float progress)
        {
            if (readiness == progress) return;
            readiness = progress;
            SetVerticesDirty();
        }

        internal void SetReadyFlash(Color tint)
        {
            if (readyFlash == tint) return;
            readyFlash = tint;
            SetVerticesDirty();
        }

        internal void Show(bool animate, bool denied, bool hovered)
        {
            if (flowing != animate || animate) SetVerticesDirty();
            flowing = animate;
            color = denied ? Denied : animate ? Orange : hovered ? Color.white : Rim;
        }

        internal void ShowRewardSelection(bool selected, Color accent, bool restart = false)
        {
            if (selected && (!flowing || restart)) rewardStarted = Time.unscaledTime;
            rewardStyle = true;
            flowing = selected;
            color = accent;
            SetVerticesDirty();
        }

        private void Update()
        {
            if (rewardStyle && flowing) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect face = rectTransform.rect;
            if (readiness < 1f) DrawShade(mesh, face);
            else if (readyFlash.a > 0f) DrawFaceFill(mesh, face, face.yMax, readyFlash);

            Rect outer = Rect.MinMaxRect(face.xMin - RimOffset, face.yMin - RimOffset,
                face.xMax + RimOffset, face.yMax + RimOffset);
            if (outer.width <= Corner * 2f || outer.height <= Corner * 2f) return;
            bool rewardActive = rewardStyle && flowing;
            float thickness = rewardActive ? 3.3f : Thickness;
            Rect inner = Rect.MinMaxRect(outer.xMin + thickness, outer.yMin + thickness,
                outer.xMax - thickness, outer.yMax - thickness);
            float diagonalInset = 2f - Mathf.Sqrt(2f);
            float innerCorner = Corner - thickness * diagonalInset;
            float perimeter = 2f * (outer.width + outer.height) - 4f * Corner * diagonalInset;
            float phase = rewardStyle
                ? Mathf.Repeat((Time.unscaledTime - rewardStarted) / 1.02f, 1f)
                : Mathf.Repeat(Time.unscaledTime / SweepDuration, 1f);
            int vertexStride = rewardActive ? 3 : 2;
            float traversed = 0f;
            Color rimTint = Color.Lerp(color, new Color(readyFlash.r, readyFlash.g, readyFlash.b), readyFlash.a);

            for (int edge = 0; edge < 8; ++edge)
            {
                int next = (edge + 1) % 8;
                Vector2 outerStart = Point(outer, Corner, edge), outerEnd = Point(outer, Corner, next);
                Vector2 innerStart = Point(inner, innerCorner, edge), innerEnd = Point(inner, innerCorner, next);
                float length = Vector2.Distance(outerStart, outerEnd);
                int segments = flowing ? Mathf.CeilToInt(length / 9f) : 1;
                int start = mesh.currentVertCount;
                for (int segment = 0; segment <= segments; ++segment)
                {
                    float t = (float)segment / segments;
                    Color32 tint = rimTint;
                    float intensity = 0f;
                    if (flowing)
                    {
                        float trail = Mathf.Repeat(phase - (traversed + length * t) / perimeter, 1f);
                        float falloff = Mathf.Min(trail / 0.22f, (1f - trail) / 0.035f);
                        intensity = 1f - Mathf.SmoothStep(0f, 1f, falloff);
                        tint = Color.Lerp(color, rewardStyle ? RewardHighlight : Highlight, intensity);
                    }

                    Vector2 rim = Vector2.Lerp(outerStart, outerEnd, t);
                    Vector2 inset = Vector2.Lerp(innerStart, innerEnd, t);
                    mesh.AddVert(rim, tint, Vector2.zero);
                    mesh.AddVert(inset, tint, Vector2.zero);
                    if (rewardActive)
                    {
                        Vector2 glow = rim + (rim - inset) * Mathf.Lerp(.75f, 2.3f, intensity);
                        mesh.AddVert(glow, new Color32(tint.r, tint.g, tint.b, 0), Vector2.zero);
                    }
                    if (segment == 0) continue;
                    int previous = start + vertexStride * (segment - 1);
                    int current = previous + vertexStride;
                    mesh.AddTriangle(previous, current, previous + 1);
                    mesh.AddTriangle(current, current + 1, previous + 1);
                    if (rewardActive)
                    {
                        mesh.AddTriangle(previous + 2, current + 2, previous);
                        mesh.AddTriangle(current + 2, current, previous);
                    }
                }
                traversed += length;
            }
        }

        private void DrawShade(VertexHelper mesh, Rect bounds)
        {
            float edge = Mathf.Lerp(bounds.yMax, bounds.yMin, readiness);
            DrawFaceFill(mesh, bounds, edge, Shade);
            if (readiness > 0f)
                AddFaceStrip(mesh, bounds, Mathf.Max(bounds.yMin, edge - EdgeThickness), edge, ReadinessEdge);
        }

        private static void DrawFaceFill(VertexHelper mesh, Rect bounds, float edge, Color32 tint)
        {
            float bottomBevel = bounds.yMin + ShadeCorner;
            float topBevel = bounds.yMax - ShadeCorner;
            AddFaceStrip(mesh, bounds, bounds.yMin, Mathf.Min(edge, bottomBevel), tint);
            AddFaceStrip(mesh, bounds, bottomBevel, Mathf.Min(edge, topBevel), tint);
            AddFaceStrip(mesh, bounds, topBevel, edge, tint);
        }

        private static void AddFaceStrip(VertexHelper mesh, Rect bounds, float bottom, float top, Color32 tint)
        {
            if (top <= bottom) return;
            float bottomInset = Mathf.Max(0f, ShadeCorner - Mathf.Min(bottom - bounds.yMin, bounds.yMax - bottom));
            float topInset = Mathf.Max(0f, ShadeCorner - Mathf.Min(top - bounds.yMin, bounds.yMax - top));
            int start = mesh.currentVertCount;
            mesh.AddVert(new Vector2(bounds.xMin + bottomInset, bottom), tint, Vector2.zero);
            mesh.AddVert(new Vector2(bounds.xMin + topInset, top), tint, Vector2.zero);
            mesh.AddVert(new Vector2(bounds.xMax - topInset, top), tint, Vector2.zero);
            mesh.AddVert(new Vector2(bounds.xMax - bottomInset, bottom), tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        private static Vector2 Point(Rect bounds, float corner, int index) => index switch
        {
            0 => new Vector2(bounds.xMin + corner, bounds.yMax),
            1 => new Vector2(bounds.xMax - corner, bounds.yMax),
            2 => new Vector2(bounds.xMax, bounds.yMax - corner),
            3 => new Vector2(bounds.xMax, bounds.yMin + corner),
            4 => new Vector2(bounds.xMax - corner, bounds.yMin),
            5 => new Vector2(bounds.xMin + corner, bounds.yMin),
            6 => new Vector2(bounds.xMin, bounds.yMin + corner),
            _ => new Vector2(bounds.xMin, bounds.yMax - corner)
        };
    }
}
