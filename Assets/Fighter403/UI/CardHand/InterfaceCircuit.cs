using UnityEngine;
using UnityEngine.UI;

namespace Fighter403.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class InterfaceCircuit : MaskableGraphic
    {
        private const float TracePitch = 74f;
        private const float StepRate = 24f;
        private const float CycleSeconds = 8.4f;
        private const float TravelEnd = .27f;
        private static readonly Color32 Etched = new(77, 138, 154, 49);
        private static readonly Color32 Pad = new(81, 155, 173, 62);
        private static readonly Color32 Live = new(81, 180, 201, 132);
        private static readonly Color32 Contact = new(156, 222, 226, 148);
        private bool vertical, reversed;
        private float phaseOffset;
        private int previousFrame = -1;

        internal void Initialize(bool isVertical, float phase, bool reverse = false)
        {
            vertical = isVertical;
            reversed = reverse;
            phaseOffset = phase;
            raycastTarget = false;
        }

        private void LateUpdate()
        {
            int frame = Mathf.FloorToInt(Time.unscaledTime * StepRate);
            if (frame == previousFrame || canvasRenderer.cull) return;
            previousFrame = frame;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = rectTransform.rect;
            float length = vertical ? bounds.height : bounds.width;
            float depth = vertical ? bounds.width : bounds.height;
            if (length < 24f || depth < 7f) return;

            int count = Mathf.Max(1, Mathf.RoundToInt(length / TracePitch));
            float pitch = length / count;
            float time = Mathf.Floor(Time.unscaledTime * StepRate) / StepRate;
            for (int i = 0; i < count; i++)
            {
                float start = Mathf.Round(i * pitch) + 5f;
                float end = Mathf.Round((i + 1) * pitch) - 8f;
                float joint = Mathf.Round(Mathf.Lerp(start, end, .32f + (i % 3) * .09f));
                float near = 2f;
                float far = Mathf.Floor(depth - 3f);
                if ((i & 1) != 0) (near, far) = (far, near);
                float middle = Mathf.Round((near + far) * .5f);
                // Offset neighbors independently so no continuous highlight runs down the panel edge.
                float phase = Mathf.Repeat(time / CycleSeconds + phaseOffset + i * .382f, 1f);
                float traceLength = end - start + Mathf.Abs(near - far);
                float reach = Mathf.Clamp01((phase - .03f) / TravelEnd) * (traceLength + 10f);
                float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((phase - .03f) / .055f));
                float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((phase - .21f) / .18f));
                float opacity = fadeIn * fadeOut;
                var a = new Vector2(start, near);
                var b = new Vector2(joint, near);
                var c = new Vector2(joint, far);
                var d = new Vector2(end, far);
                DrawTrace(mesh, bounds, a, b, reach, opacity);
                DrawTrace(mesh, bounds, b, c, reach - (joint - start), opacity);
                DrawTrace(mesh, bounds, c, d, reach - (joint - start + Mathf.Abs(near - far)), opacity);
                DrawContact(mesh, bounds, a, .4f * opacity * Response(reach));
                DrawContact(mesh, bounds, d, opacity * Response(reach - traceLength));

                if (i % 3 == 1 || depth < 10f) continue;
                var branchStart = new Vector2(joint, middle);
                var branchEnd = new Vector2(Mathf.Round(Mathf.Lerp(joint, end, .72f)), middle);
                float branchReach = reach - (joint - start + Mathf.Abs(near - middle));
                DrawTrace(mesh, bounds, branchStart, branchEnd, branchReach, opacity * .72f);
                DrawContact(mesh, bounds, branchEnd, opacity * .55f * Response(branchReach - (branchEnd.x - joint)));

                float dissolve = Mathf.Clamp01((phase - .28f) / .12f);
                if (dissolve <= 0f || dissolve >= 1f || i % 4 != 0) continue;
                var chip = branchEnd + new Vector2(3f + Mathf.Floor(dissolve * 4f), near > far ? -2f : 2f);
                PaintRect(mesh, bounds, chip - Vector2.one * .5f, chip + Vector2.one * .5f,
                    WithAlpha(Live, .55f * Mathf.Sin(dissolve * Mathf.PI)));
            }
        }

        private void DrawTrace(VertexHelper mesh, Rect bounds, Vector2 start, Vector2 end,
            float reach, float opacity)
        {
            DrawLine(mesh, bounds, start, end, .34f, Etched);
            float length = Vector2.Distance(start, end);
            if (opacity < .02f || reach <= 0f || reach - 13f >= length || length < 1f) return;

            Vector2 direction = (end - start) / length;
            DrawPulse(mesh, bounds, start, direction, length, reach - 13f, reach - 4f, WithAlpha(Live, opacity * .55f));
            DrawPulse(mesh, bounds, start, direction, length, reach - 4f, reach, WithAlpha(Contact, opacity));
        }

        private void DrawPulse(VertexHelper mesh, Rect bounds, Vector2 start, Vector2 direction,
            float length, float from, float to, Color32 color)
        {
            float low = Mathf.Max(0f, from);
            float high = Mathf.Min(length, to);
            if (high <= low) return;
            DrawLine(mesh, bounds, start + direction * low, start + direction * high, .5f, color);
        }

        private void DrawContact(VertexHelper mesh, Rect bounds, Vector2 center, float intensity)
        {
            PaintRect(mesh, bounds, center - Vector2.one * 1.25f, center + Vector2.one * 1.25f, Pad);
            if (intensity < .02f) return;
            PaintRect(mesh, bounds, center - Vector2.one * .75f, center + Vector2.one * .75f, WithAlpha(Contact, intensity));
        }

        private static float Response(float distance) =>
            Mathf.Clamp01(1f - Mathf.Abs(distance - 2f) / 7f);

        private void DrawLine(VertexHelper mesh, Rect bounds, Vector2 a, Vector2 b, float radius, Color32 color) =>
            PaintRect(mesh, bounds, Vector2.Min(a, b) - Vector2.one * radius, Vector2.Max(a, b) + Vector2.one * radius, color);

        private void PaintRect(VertexHelper mesh, Rect bounds, Vector2 low, Vector2 high, Color32 color)
        {
            var a = Position(bounds, low.x, low.y);
            var b = Position(bounds, high.x, high.y);
            var min = Vector2.Max(bounds.min, Vector2.Min(a, b));
            var max = Vector2.Min(bounds.max, Vector2.Max(a, b));
            if (min.x >= max.x || min.y >= max.y) return;
            int first = mesh.currentVertCount;
            mesh.AddVert(min, color, Vector2.zero);
            mesh.AddVert(new Vector2(min.x, max.y), color, Vector2.zero);
            mesh.AddVert(max, color, Vector2.zero);
            mesh.AddVert(new Vector2(max.x, min.y), color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }

        private Vector2 Position(Rect bounds, float along, float across) => vertical
            ? new Vector2(reversed ? bounds.xMax - across : bounds.xMin + across,
                reversed ? bounds.yMin + along : bounds.yMax - along)
            : new Vector2(bounds.xMin + along, bounds.yMin + across);

        private static Color32 WithAlpha(Color32 color, float opacity)
        {
            color.a = (byte)Mathf.RoundToInt(color.a * opacity);
            return color;
        }
    }
}
