using System;
using Fighter403.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Fighter403.UI
{
    internal sealed class CombatTimeStopPostProcess : IDisposable
    {
        private const float PulseSeconds = 0.38f;
        private const float BigBangSeconds = 0.95f;
        private const float BigBangRestoreProgress = 0.56f;
        internal const float BigBangInversionSeconds = BigBangSeconds * BigBangRestoreProgress;
        private const float BigBangRestoreSeconds = BigBangSeconds - BigBangInversionSeconds;
        private readonly Camera camera;
        private readonly Material material;
        private readonly PulsePass pass;
        private bool dashActive;
        private bool sandevistanActive;
        private bool subscribed;
        private float startedAt;
        private bool bigBangInverting;
        private double bigBangStartedAt;
        private double bigBangUntil;
        private Vector2 bigBangOrigin;
        private Transform player;
        private Vector4 distortion;
        private Vector4 grading;
        private float distortionFade;

        internal CombatTimeStopPostProcess(Camera camera)
        {
            this.camera = camera;
            var shader = Resources.Load<Shader>("Fighter403DashTimeStop");
            if (shader == null) return;
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new PulsePass(material)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing,
                requiresIntermediateTexture = true
            };
        }

        internal void SetDashActive(bool value)
        {
            if (dashActive == value) return;
            dashActive = value;
            if (value) startedAt = Time.unscaledTime;
            UpdateSubscription();
        }

        internal void SetSandevistan(Transform source, SandevistanVfxSettings settings, float age, double remaining)
        {
            sandevistanActive = source != null && settings?.screenEffect == true && remaining > 0d;
            if (sandevistanActive)
            {
                player = source;
                distortion = new Vector4(Mathf.Clamp01(age / settings.pulseSeconds), settings.pulseStrength,
                    settings.edgeWarp, settings.edgeColorSplit);
                grading = new Vector4(settings.radialBlur, settings.vignette, settings.coolTint, 0f);
                distortionFade = Mathf.SmoothStep(0f, 1f, (float)remaining / settings.screenFadeOutSeconds);
            }
            UpdateSubscription();
        }

        internal void PlayBigBang(Vector2 origin)
        {
            bigBangOrigin = origin;
            bigBangStartedAt = Time.unscaledTimeAsDouble;
            bigBangInverting = true;
            UpdateSubscription();
        }

        internal void RestoreBigBangColors()
        {
            bigBangInverting = false;
            bigBangUntil = Time.unscaledTimeAsDouble + BigBangRestoreSeconds;
        }

        internal void ClearBigBang()
        {
            bigBangInverting = false;
            bigBangUntil = 0d;
            UpdateSubscription();
        }

        private bool BigBangActive => bigBangInverting || Time.unscaledTimeAsDouble < bigBangUntil;

        private void UpdateSubscription()
        {
            bool rendering = dashActive || sandevistanActive || BigBangActive;
            if (rendering == subscribed || pass == null) return;
            subscribed = rendering;
            if (rendering) RenderPipelineManager.beginCameraRendering += Enqueue;
            else RenderPipelineManager.beginCameraRendering -= Enqueue;
        }

        private void Enqueue(ScriptableRenderContext context, Camera renderingCamera)
        {
            if (renderingCamera != camera) return;
            pass.BaseEffect = dashActive || sandevistanActive;
            pass.BigBang = BigBangActive;
            if (!pass.BaseEffect && !pass.BigBang) return;
            pass.Sandevistan = sandevistanActive;
            if (sandevistanActive && player != null)
            {
                Vector3 center = camera.WorldToViewportPoint(player.position);
                pass.Distortion = distortion;
                pass.Grading = grading;
                pass.CenterAndFade = new Vector4(Mathf.Clamp01(center.x), Mathf.Clamp01(center.y),
                    distortionFade, Time.unscaledTime);
            }
            if (pass.BigBang)
            {
                Vector3 center = camera.WorldToViewportPoint(bigBangOrigin);
                double now = Time.unscaledTimeAsDouble;
                float progress = bigBangInverting
                    ? Mathf.Min(BigBangRestoreProgress, (float)((now - bigBangStartedAt) / BigBangSeconds))
                    : 1f - (float)((bigBangUntil - now) / BigBangSeconds);
                pass.BigBangWave = new Vector4(Mathf.Clamp01(center.x), Mathf.Clamp01(center.y),
                    Mathf.Clamp01(progress), BigBangRestoreProgress);
            }
            pass.Progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / PulseSeconds);
            camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(pass);
        }

        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering -= Enqueue;
            CoreUtils.Destroy(material);
        }

        private sealed class PulsePass : ScriptableRenderPass
        {
            private static readonly int ProgressId = Shader.PropertyToID("_PulseProgress");
            private static readonly int DistortionId = Shader.PropertyToID("_SandevistanDistortion");
            private static readonly int CenterAndFadeId = Shader.PropertyToID("_SandevistanCenterAndFade");
            private static readonly int GradingId = Shader.PropertyToID("_SandevistanGrading");
            private static readonly int BigBangWaveId = Shader.PropertyToID("_BigBangScreenWave");
            private readonly Material material;
            internal float Progress { get; set; }
            internal bool BaseEffect { get; set; }
            internal bool Sandevistan { get; set; }
            internal bool BigBang { get; set; }
            internal Vector4 BigBangWave { get; set; }
            internal Vector4 Distortion { get; set; }
            internal Vector4 CenterAndFade { get; set; }
            internal Vector4 Grading { get; set; }

            internal PulsePass(Material material) => this.material = material;

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                var resources = frame.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var source = resources.activeColorTexture;
                if (BaseEffect)
                {
                    material.SetFloat(ProgressId, Progress);
                    material.SetVector(DistortionId, Distortion);
                    material.SetVector(CenterAndFadeId, CenterAndFade);
                    material.SetVector(GradingId, Grading);
                    source = Blit(graph, source, Sandevistan ? 1 : 0, Sandevistan ? "Sandevistan distortion" : "Dash time stop");
                }
                if (BigBang)
                {
                    material.SetVector(BigBangWaveId, BigBangWave);
                    source = Blit(graph, source, 2, "Big Bang Shockwave");
                }
                resources.cameraColor = source;
            }

            private TextureHandle Blit(RenderGraph graph, TextureHandle source, int passIndex, string name)
            {
                var description = graph.GetTextureDesc(source);
                description.name = name;
                description.clearBuffer = false;
                var destination = graph.CreateTexture(description);
                var blit = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, passIndex);
                graph.AddBlitPass(blit, passName: description.name);
                return destination;
            }
        }
    }
}
