using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = System.Random;

namespace Fighter403.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class Fighter403MenuParticles : Graphic
    {
        private const int TileSize = 16;
        private const int AtlasColumns = 4;
        private const float FramesPerSecond = 24f;

        // The order matches the four tiles in AmbientParticles.png.
        private enum Shape { Dust, Fragment, Glint, Streak }

        private struct Particle
        {
            public Shape Shape;
            public Vector2 Origin;
            public Vector2 Travel;
            public float Period;
            public float Phase;
            public float VisiblePart;
            public float Opacity;
            public Color Tint;
        }

        private static readonly Color DistantCyan = new Color32(119, 178, 192, 255);
        private static readonly Color BrightCyan = new Color32(148, 225, 219, 255);
        private static readonly Color Copper = new Color32(241, 166, 103, 255);

        private readonly List<Particle> particles = new();
        private Texture2D atlas;
        private int frame = -1;

        public override Texture mainTexture => atlas != null ? atlas : base.mainTexture;

        internal void Initialize(Texture2D texture)
        {
            atlas = texture;
            raycastTarget = false;
            var random = new Random(403);
            AddLayer(random, Shape.Dust, 12, 8, new Vector2(56, 36), .54f, 1f);
            AddLayer(random, Shape.Fragment, 8, 5, new Vector2(114, 67), .78f, .84f);
            AddLayer(random, Shape.Glint, 4, 3, new Vector2(129, 72), .86f, .25f);
            AddLayer(random, Shape.Streak, 5, 4, new Vector2(288, 144), .96f, .23f);
            SetAllDirty();
        }

        internal void Advance(float unscaledTime)
        {
            int nextFrame = Mathf.FloorToInt(unscaledTime * FramesPerSecond);
            if (nextFrame == frame) return;
            frame = nextFrame;
            SetVerticesDirty();
        }

        private void AddLayer(Random random, Shape shape, int columns, int rows, Vector2 travel, float opacity,
            float visiblePart)
        {
            // Jitter one source per cell to spread the movement across the whole screen.
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    Color tint = shape == Shape.Dust ? DistantCyan : BrightCyan;
                    if (shape != Shape.Dust && random.Next(5) == 0) tint = Copper;
                    particles.Add(new Particle
                    {
                        Shape = shape,
                        Origin = new Vector2((x + Range(random, .07f, .93f)) / columns,
                            (y + Range(random, .07f, .93f)) / rows),
                        Travel = travel * Range(random, .55f, 1.35f),
                        Period = Range(random, 4.5f, 9.5f),
                        Phase = Range(random, 0f, 1f),
                        VisiblePart = visiblePart * Range(random, .76f, 1f),
                        Opacity = opacity * Range(random, .62f, 1f),
                        Tint = tint
                    });
                }
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var bounds = rectTransform.rect;
            if (atlas == null || bounds.width < 1 || bounds.height < 1) return;

            float time = Mathf.Max(0, frame) / FramesPerSecond;
            float scale = canvas.scaleFactor;
            float texelSize = Mathf.Max(1f, Mathf.Round(scale * 1.65f)) / scale;
            float halfSize = TileSize * texelSize * .5f;
            float breeze = 9f * Mathf.Sin(time * .84f);
            foreach (var particle in particles)
            {
                float age = Mathf.Repeat(time / particle.Period + particle.Phase, 1f) / particle.VisiblePart;
                if (age >= 1f) continue;
                float fadeIn = Mathf.SmoothStep(0f, 1f, age / .20f);
                float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, (age - .46f) / .54f);
                var tint = particle.Tint * color;
                tint.a *= particle.Opacity * fadeIn * fadeOut;
                if (tint.a < .02f) continue;

                float sway = 7f * Mathf.Sin((age + particle.Phase) * 2f * Mathf.PI);
                var position = Vector2.Scale(particle.Origin, bounds.size) + particle.Travel * (age - .5f);
                position += new Vector2(breeze + sway, -sway * .62f);
                // Wrap while the whole tile is outside the screen.
                float x = bounds.xMin + Mathf.Repeat(position.x + halfSize, bounds.width + halfSize * 2f) - halfSize;
                float y = bounds.yMin + Mathf.Repeat(position.y + halfSize, bounds.height + halfSize * 2f) - halfSize;
                AddTile(mesh, particle.Shape, tint, Mathf.Round(x * scale) / scale,
                    Mathf.Round(y * scale) / scale, halfSize);
            }
        }

        private static void AddTile(VertexHelper mesh, Shape shape, Color32 color, float x, float y, float halfSize)
        {
            float uMin = (int)shape / (float)AtlasColumns;
            float uMax = uMin + 1f / AtlasColumns;
            int first = mesh.currentVertCount;
            mesh.AddVert(new Vector3(x - halfSize, y - halfSize), color, new Vector2(uMin, 0));
            mesh.AddVert(new Vector3(x - halfSize, y + halfSize), color, new Vector2(uMin, 1));
            mesh.AddVert(new Vector3(x + halfSize, y + halfSize), color, new Vector2(uMax, 1));
            mesh.AddVert(new Vector3(x + halfSize, y - halfSize), color, new Vector2(uMax, 0));
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first + 2, first + 3, first);
        }

        private static float Range(Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
    }
}
