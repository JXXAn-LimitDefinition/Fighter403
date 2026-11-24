using UnityEngine;
using UnityEngine.UI;

namespace Fighter403.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class EnergyGauge : MaskableGraphic
    {
        internal const float TrackInsetLeft = 152f, TrackInsetRight = 32f, TrackCenter = -1f, TrackHeight = 16f;
        private static readonly Color32 OuterEdge = new(47, 74, 94, 255);
        private static readonly Color32 OuterMetal = new(19, 32, 48, 255);
        private static readonly Color32 DarkFace = new(9, 18, 31, 255);
        private static readonly Color32 ReadoutFace = new(17, 34, 49, 255);
        private static readonly Color32 RecessRim = new(28, 50, 65, 255);
        private static readonly Color32 Recess = new(6, 14, 25, 255);
        private static readonly Color32 SocketBase = new(19, 36, 52, 255);
        private static readonly Color32 SocketLight = new(29, 55, 71, 255);
        private static readonly Color32 Charge = new(35, 162, 170, 255);
        private static readonly Color32 ChargeEdge = new(138, 246, 230, 255);
        private static readonly Color32 ChargeShade = new(22, 102, 117, 255);
        private static readonly Color32 Spent = new(43, 89, 110, 255);
        private static readonly Color32 SpentEdge = new(102, 155, 173, 255);
        private static readonly Color32 Reserved = new(129, 79, 46, 255);
        private static readonly Color32 ReserveEdge = new(255, 191, 108, 255);
        private static readonly Color32 ReserveShade = new(89, 53, 39, 255);
        private static readonly Color32 Warning = new(250, 109, 102, 255);

        private int segments;
        private float current, available, trail, reservationGlow, fullPulse, deniedPulse;

        internal void SetCharge(int maximum, float current, float available, float trail, float reservationGlow,
            float fullPulse, float deniedPulse)
        {
            int segments = Mathf.Clamp(maximum, 1, 30);
            if (this.segments == segments && this.current == current && this.available == available &&
                this.trail == trail && this.reservationGlow == reservationGlow &&
                this.fullPulse == fullPulse && this.deniedPulse == deniedPulse)
                return;

            this.segments = segments;
            this.current = current;
            this.available = available;
            this.trail = trail;
            this.reservationGlow = reservationGlow;
            this.fullPulse = fullPulse;
            this.deniedPulse = deniedPulse;
            SetVerticesDirty();
        }

        internal float ChargeEdgeOnTrack()
        {
            float width = rectTransform.rect.width - TrackInsetLeft - TrackInsetRight;
            if (segments == 0 || width <= 0f || current <= 0f) return 0f;

            float pitch = width / segments;
            float chargedCells = current * segments;
            int cell = LastChargedCell(chargedCells);
            return (pitch * cell + CellWidth(pitch) * Mathf.Clamp01(chargedCells - cell)) / width;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = rectTransform.rect;
            if (segments == 0 || bounds.width <= TrackInsetLeft + TrackInsetRight + 12f || bounds.height < 44f) return;

            float left = bounds.xMin, right = bounds.xMax;
            float top = bounds.yMax, bottom = bounds.yMin;
            float start = left + TrackInsetLeft;
            float end = right - TrackInsetRight;
            float ceiling = TrackCenter + TrackHeight * .5f;
            float floor = TrackCenter - TrackHeight * .5f;
            float pitch = (end - start) / segments;
            float cellWidth = CellWidth(pitch);
            float chargedCells = current * segments;
            int lastChargedCell = LastChargedCell(chargedCells);
            Color32 chargeRim = Color.Lerp(ChargeEdge, Color.white, .65f * fullPulse);
            Color32 reserveRim = Color.Lerp(ReserveEdge, Color.white, .32f * reservationGlow);
            Color32 contacts = Color.Lerp(chargeRim, Warning, deniedPulse);

            DrawShell(mesh, left, right, bottom, top, start, end, floor, ceiling, contacts);
            for (int i = 0; i < segments; ++i)
            {
                float cellLeft = start + pitch * i;
                float cellRight = cellLeft + cellWidth;
                float chargeEnd = cellLeft + cellWidth * Mathf.Clamp01(chargedCells - i);
                float freeEnd = cellLeft + cellWidth * Mathf.Clamp01(available * segments - i);
                float trailEnd = cellLeft + cellWidth * Mathf.Clamp01(trail * segments - i);

                DrawCell(mesh, cellLeft, cellRight, floor, ceiling, SocketBase, SocketLight, Recess);
                DrawCell(mesh, chargeEnd, trailEnd, floor, ceiling, Spent, SpentEdge, SocketBase);
                DrawCell(mesh, cellLeft, freeEnd, floor, ceiling, Charge, chargeRim, ChargeShade);
                DrawCell(mesh, freeEnd, chargeEnd, floor, ceiling, Reserved, reserveRim, ReserveShade);
                DrawHatch(mesh, freeEnd, chargeEnd, floor + 2f, ceiling - 2f, reserveRim);

                bool major = i == 0 || (i + 1) % 5 == 0 || i == segments - 1;
                float contactX = (cellLeft + cellRight) * .5f;
                Color32 contactTint = freeEnd >= cellRight - .001f ? ChargeShade
                    : chargeEnd >= cellRight - .001f ? Reserved : RecessRim;
                float contactWidth = major ? 9f : 3f;
                Quad(mesh, contactX - contactWidth * .5f, floor - 6f, contactX + contactWidth * .5f, floor - 4f, contactTint);

                if (i == lastChargedCell && chargeEnd > cellLeft + .01f)
                    Quad(mesh, Mathf.Max(cellLeft, chargeEnd - 1.5f), floor, chargeEnd, ceiling, contacts);
            }
        }

        private static float CellWidth(float pitch) => pitch - Mathf.Min(4f, pitch * .18f);

        private static int LastChargedCell(float chargedCells) => Mathf.CeilToInt(chargedCells) - 1;

        private static void DrawShell(VertexHelper mesh, float left, float right, float bottom, float top,
            float start, float end, float floor, float ceiling, Color32 contacts)
        {
            CutPlate(mesh, left + 1f, bottom, right, top - 1f, 11f, OuterEdge);
            CutPlate(mesh, left + 2f, bottom + 1f, right - 1f, top - 2f, 10f, OuterMetal);
            CutPlate(mesh, left + 7f, bottom + 5f, right - 7f, top - 6f, 6f, DarkFace);
            CutPlate(mesh, left + 9f, bottom + 6f, start - 13f, top - 7f, 6f, ReadoutFace);
            CutPlate(mesh, left + 16f, -11f, left + 43f, 12f, 5f, RecessRim);
            CutPlate(mesh, left + 17f, -10f, left + 42f, 11f, 5f, Recess);

            // A recessed connector routes the icon into the charge cells.
            Quad(mesh, left + 19f, top - 3f, start - 28f, top - 2f, RecessRim);
            Quad(mesh, start - 22f, top - 4f, start - 12f, top - 2f, contacts);
            Quad(mesh, start - 11f, bottom + 11f, start - 9f, top - 10f, OuterEdge);
            CutPlate(mesh, start - 6f, floor - 2f, end + 3f, ceiling + 2f, 3f, RecessRim);
            CutPlate(mesh, start - 4f, floor - 1f, end + 2f, ceiling + 1f, 2f, Recess);
            Quad(mesh, start, ceiling + 4f, start + 64f, ceiling + 5f, OuterEdge);
            Quad(mesh, end - 20f, bottom + 4f, end + 13f, bottom + 5f, OuterEdge);
            for (int i = 0; i < 3; ++i)
            {
                float contactY = -7f + i * 6f;
                Quad(mesh, end + 10f, contactY, end + 16f, contactY + 2f, i == 2 ? contacts : OuterEdge);
            }
        }

        private static void DrawCell(VertexHelper mesh, float from, float to, float floor, float ceiling,
            Color32 face, Color32 edge, Color32 shade)
        {
            Quad(mesh, from, floor, to, ceiling, face);
            Quad(mesh, from, ceiling - 2f, to, ceiling, edge);
            Quad(mesh, from, floor, to, floor + 3f, shade);
        }

        private static void DrawHatch(VertexHelper mesh, float from, float to, float floor, float ceiling, Color32 tint)
        {
            if (to <= from) return;
            tint.a = 115;
            float slant = (ceiling - floor) * .6f;
            for (float x = from - slant; x < to; x += 10f)
            {
                float bottomLeft = Mathf.Clamp(x, from, to);
                float bottomRight = Mathf.Clamp(x + 2f, from, to);
                float topLeft = Mathf.Clamp(x + slant, from, to);
                float topRight = Mathf.Clamp(x + slant + 2f, from, to);
                AddQuad(mesh, new Vector2(bottomLeft, floor), new Vector2(topLeft, ceiling),
                    new Vector2(topRight, ceiling), new Vector2(bottomRight, floor), tint);
            }
        }

        private static void CutPlate(VertexHelper mesh, float left, float bottom, float right, float top,
            float cut, Color32 tint)
        {
            AddQuad(mesh, new Vector2(left + cut, bottom), new Vector2(left, bottom + cut),
                new Vector2(right, bottom + cut), new Vector2(right - cut, bottom), tint);
            Quad(mesh, left, bottom + cut, right, top - cut, tint);
            AddQuad(mesh, new Vector2(left, top - cut), new Vector2(left + cut, top),
                new Vector2(right - cut, top), new Vector2(right, top - cut), tint);
        }

        private static void Quad(VertexHelper mesh, float left, float bottom, float right, float top, Color32 tint)
        {
            if (right <= left || top <= bottom) return;
            AddQuad(mesh, new Vector2(left, bottom), new Vector2(left, top),
                new Vector2(right, top), new Vector2(right, bottom), tint);
        }

        private static void AddQuad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 tint)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero);
            mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero);
            mesh.AddVert(d, tint, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
