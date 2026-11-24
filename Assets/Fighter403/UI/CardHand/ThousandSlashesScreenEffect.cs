using System;
using System.Collections.Generic;
using Fighter403.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Fighter403.UI
{
    internal sealed class ThousandSlashesScreenEffect : IDisposable
    {
        private const float ColorRestoreSeconds = 0.28f;

        private readonly Camera camera;
        private readonly TopDownCameraFollow cameraFollow;
        private readonly Material material;
        private readonly Mesh quad;
        private readonly ScreenPass pass;
        private readonly List<CanvasState> canvases = new();
        private bool active;
        private double colorRestoreStartedAt = -1d;

        internal ThousandSlashesScreenEffect(Camera camera, TopDownCameraFollow cameraFollow)
        {
            this.camera = camera;
            this.cameraFollow = cameraFollow;
            var shader = Resources.Load<Shader>("Fighter403ThousandSlashes");
            if (shader == null) return;
            material = CoreUtils.CreateEngineMaterial(shader);
            quad = new Mesh
            {
                name = "Thousand Slashes sprite quad",
                vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f),
                    new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one },
                triangles = new[] { 0, 2, 1, 2, 3, 1 }
            };
            pass = new ScreenPass(material, quad)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing,
                requiresIntermediateTexture = true
            };
        }

        internal void Begin()
        {
            if (pass == null || (active && colorRestoreStartedAt < 0d)) return;
            colorRestoreStartedAt = -1d;
            pass.Blackout = 0f;
            pass.ColorRestore = 0f;
            pass.Older = pass.Current = default;
            if (active) return;

            active = true;
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>())
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay ||
                    canvas.targetDisplay != camera.targetDisplay) continue;
                canvases.Add(new CanvasState(canvas, canvas.worldCamera, canvas.planeDistance));
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + 0.5f;
            }
            RenderPipelineManager.beginCameraRendering += Enqueue;
        }

        internal void SetBlackout(float value)
        {
            if (pass != null) pass.Blackout = value;
        }

        internal void Focus(Transform target, Vector2? includePoint = null) =>
            cameraFollow?.SetCombatFocus(target, includePoint);

        internal void SetZoom(float scale) => cameraFollow?.SetCombatZoom(scale);

        internal Vector2 FinisherScale(Vector2 direction)
        {
            // Blade lengths in the paused impact frame, imported at 26 pixels per unit.
            const float ImpactBladeLength = 161f / 26f;
            const float CrossingBladeLength = 144f / 26f;
            var diagonal = new Vector2(direction.x + direction.y, direction.y - direction.x) * 0.707107f;
            float x = Mathf.Abs(diagonal.x);
            float y = Mathf.Abs(diagonal.y);
            float width = Mathf.Max(x * ImpactBladeLength, y * CrossingBladeLength);
            float height = Mathf.Max(y * ImpactBladeLength, x * CrossingBladeLength);
            float viewHeight = (cameraFollow?.CombatViewSize ?? camera.orthographicSize) * 2f;
            float scale = Mathf.Min(viewHeight * camera.aspect * 0.5f / width, viewHeight * 0.86f / height);
            return Vector2.one * scale;
        }

        internal void SetSlashes(SlashFrame older, SlashFrame current)
        {
            if (pass == null) return;
            pass.Older = older;
            pass.Current = current;
        }

        internal void End(bool blendCamera = false)
        {
            SetZoom(1f);
            if (blendCamera && active)
            {
                cameraFollow?.BlendFromCombatFocus();
                pass.Older = pass.Current = default;
                colorRestoreStartedAt = Time.unscaledTimeAsDouble;
                return;
            }

            cameraFollow?.SetCombatFocus(null);
            StopRendering();
        }

        internal void StepColorRestore()
        {
            if (colorRestoreStartedAt < 0d) return;
            float progress = (float)((Time.unscaledTimeAsDouble - colorRestoreStartedAt) / ColorRestoreSeconds);
            if (progress >= 1f)
            {
                StopRendering();
                return;
            }

            pass.ColorRestore = Mathf.SmoothStep(0f, 1f, progress);
        }

        private void StopRendering()
        {
            if (!active) return;
            active = false;
            colorRestoreStartedAt = -1d;
            RenderPipelineManager.beginCameraRendering -= Enqueue;
            foreach (var state in canvases)
            {
                if (state.Canvas == null) continue;
                state.Canvas.worldCamera = state.Camera;
                state.Canvas.planeDistance = state.PlaneDistance;
                state.Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            canvases.Clear();
            pass.Older = pass.Current = default;
        }

        private void Enqueue(ScriptableRenderContext _, Camera renderingCamera)
        {
            if (renderingCamera == camera)
                camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(pass);
        }

        public void Dispose()
        {
            End();
            CoreUtils.Destroy(material);
            CoreUtils.Destroy(quad);
        }

        private readonly struct CanvasState
        {
            internal readonly Canvas Canvas;
            internal readonly Camera Camera;
            internal readonly float PlaneDistance;

            internal CanvasState(Canvas canvas, Camera camera, float planeDistance)
            {
                Canvas = canvas;
                Camera = camera;
                PlaneDistance = planeDistance;
            }
        }

        internal readonly struct SlashFrame
        {
            internal readonly Sprite Sprite;
            internal readonly Vector3 Position;
            internal readonly Vector2 Direction;
            internal readonly Vector2 Scale;

            internal SlashFrame(Sprite sprite, Vector3 position, Vector2 direction, Vector2 scale)
            {
                Sprite = sprite;
                Position = position;
                Direction = direction;
                Scale = scale;
            }
        }

        private sealed class ScreenPass : ScriptableRenderPass
        {
            private static readonly int BlackoutId = Shader.PropertyToID("_Blackout");
            private static readonly int ColorRestoreId = Shader.PropertyToID("_ColorRestore");
            private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
            private static readonly int FrameUvId = Shader.PropertyToID("_FrameUv");
            private readonly Material material;
            private readonly Mesh quad;
            private readonly MaterialPropertyBlock properties = new();
            internal float Blackout { get; set; }
            internal float ColorRestore { get; set; }
            internal SlashFrame Older { get; set; }
            internal SlashFrame Current { get; set; }

            internal ScreenPass(Material material, Mesh quad)
            {
                this.material = material;
                this.quad = quad;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                var resources = frame.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var source = resources.activeColorTexture;
                var description = graph.GetTextureDesc(source);
                description.name = "Thousand Slashes monochrome";
                description.clearBuffer = false;
                var destination = graph.CreateTexture(description);
                material.SetFloat(BlackoutId, Blackout);
                material.SetFloat(ColorRestoreId, ColorRestore);
                var blit = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0);
                graph.AddBlitPass(blit, passName: description.name);
                resources.cameraColor = destination;

                if (Blackout >= 1f || (Older.Sprite == null && Current.Sprite == null)) return;
                using var builder = graph.AddRasterRenderPass<SlashPassData>("Thousand Slashes color", out var data);
                data.Material = material;
                data.Quad = quad;
                data.Properties = properties;
                data.Older = Older;
                data.Current = Current;
                builder.SetRenderAttachment(destination, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (SlashPassData pass, RasterGraphContext context) =>
                {
                    DrawSlash(context.cmd, pass, pass.Older);
                    DrawSlash(context.cmd, pass, pass.Current);
                });
            }

            private static void DrawSlash(RasterCommandBuffer commands, SlashPassData pass, SlashFrame frame)
            {
                if (frame.Sprite == null) return;
                var texture = frame.Sprite.texture;
                Rect rect = frame.Sprite.textureRect;
                pass.Properties.SetTexture(MainTexId, texture);
                pass.Properties.SetVector(FrameUvId, new Vector4(rect.width / texture.width, rect.height / texture.height,
                    rect.x / texture.width, rect.y / texture.height));
                Vector3 size = frame.Sprite.bounds.size;
                var rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(frame.Direction.y, frame.Direction.x) * Mathf.Rad2Deg);
                var matrix = Matrix4x4.TRS(frame.Position, rotation, new Vector3(size.x * frame.Scale.x, size.y * frame.Scale.y, 1f));
                commands.DrawMesh(pass.Quad, matrix, pass.Material, 0, 1, pass.Properties);
            }

            private sealed class SlashPassData
            {
                internal Material Material;
                internal Mesh Quad;
                internal MaterialPropertyBlock Properties;
                internal SlashFrame Older, Current;
            }
        }
    }
}
