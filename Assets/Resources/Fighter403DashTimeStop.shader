Shader "Hidden/Fighter403/DashTimeStop"
{
    Properties
    {
        _PulseProgress ("Pulse progress", Range(0, 1)) = 0
        _SandevistanDistortion ("Sandevistan distortion", Vector) = (0, 0, 0, 0)
        _SandevistanCenterAndFade ("Sandevistan center and fade", Vector) = (0.5, 0.5, 0, 0)
        _SandevistanGrading ("Sandevistan radial blur, vignette, and tint", Vector) = (1, 0.72, 0.58, 0)
        _BigBangScreenWave ("Big Bang center, progress, and recovery start", Vector) = (0.5, 0.5, 0, 0)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Dash Time Stop"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _PulseProgress;

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float2 centered = (uv - 0.5) * float2(_ScreenParams.x / _ScreenParams.y, 1.0);
                float outerCorner = length(float2(_ScreenParams.x / _ScreenParams.y, 1.0)) * 0.5;
                float radius = (1.0 - pow(1.0 - _PulseProgress, 1.45)) * (outerCorner + 0.10);
                float edge = radius - length(centered);
                float coverage = smoothstep(-0.012, 0.012, edge);
                float ring = smoothstep(0.0, 0.009, edge) * (1.0 - smoothstep(0.010, 0.041, edge));
                half gray = dot(scene.rgb, half3(0.2126, 0.7152, 0.0722));
                gray = saturate(lerp(0.5, gray, 1.1) + ring * 0.24);
                return half4(lerp(scene.rgb, gray.xxx, coverage), scene.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Sandevistan Distortion"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _SandevistanDistortion;
            float4 _SandevistanCenterAndFade;
            float4 _SandevistanGrading;

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 center = _SandevistanCenterAndFade.xy;
                #if UNITY_UV_STARTS_AT_TOP
                    center.y = 1.0 - center.y;
                #endif
                float2 aspect = float2(_ScreenParams.x / _ScreenParams.y, 1.0);
                float2 fromPlayer = (uv - center) * aspect;
                float radius = length(fromPlayer);
                float2 direction = fromPlayer / (max(radius, 0.001) * aspect);
                float cornerRadius = length(max(center, 1.0 - center) * aspect);
                float progress = _SandevistanDistortion.x;
                float waveDistance = radius - lerp(0.02, cornerRadius + 0.16, 1.0 - pow(1.0 - progress, 1.45));
                float waveWidth = waveDistance * 23.0;
                float wave = exp2(-waveWidth * waveWidth);
                float pulse = wave * sin(waveDistance * 45.0) * (1.0 - progress) * _SandevistanDistortion.y;

                float2 fromCenter = uv - 0.5;
                float edge = smoothstep(0.17, 0.61, length(fromCenter * float2(1.0, 0.91)));
                float periphery = smoothstep(0.19, 0.79, radius);
                float time = _SandevistanCenterAndFade.w;
                float fade = _SandevistanCenterAndFade.z;
                float activation = (1.0 - progress) * _SandevistanDistortion.y;
                float warp = periphery * (0.034 + 0.003 * sin(radius * 16.0 - time * 3.0)) * _SandevistanDistortion.z;
                float2 distorted = uv + fade * ((uv - center) * warp + direction * pulse * 0.028);
                float blurStrength = periphery * (0.13 * _SandevistanGrading.x + 0.12 * activation) * fade;
                float2 blurStep = (uv - center) * blurStrength * 0.25;
                float2 split = fade * _SandevistanDistortion.w *
                    ((uv - center) * periphery * 0.010 + direction * abs(pulse) * 0.012);

                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, distorted);
                scene.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, distorted + split).r;
                scene.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, distorted - split).b;
                half3 streaks = scene.rgb * 0.30;
                streaks += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, distorted - blurStep).rgb * 0.26;
                streaks += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, distorted - blurStep * 2.0).rgb * 0.20;
                streaks += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, distorted - blurStep * 3.0).rgb * 0.15;
                streaks += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, distorted - blurStep * 4.0).rgb * 0.09;
                scene.rgb = lerp(scene.rgb, streaks, periphery);

                half gray = dot(scene.rgb, half3(0.2126, 0.7152, 0.0722));
                half3 cold = lerp(scene.rgb, gray.xxx, 0.62) * half3(0.67, 0.99, 1.06);
                float tint = _SandevistanGrading.z * fade * lerp(0.55, 1.0, periphery);
                scene.rgb = lerp(scene.rgb, cold, tint);
                scene.rgb *= 1.0 - edge * _SandevistanGrading.y * fade;
                scene.rgb += half3(0.027, 0.085, 0.082) * wave * activation * fade;
                return scene;
            }
            ENDHLSL
        }
        Pass
        {
            Name "Big Bang Shockwave"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _BigBangScreenWave;

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 center = _BigBangScreenWave.xy;
                #if UNITY_UV_STARTS_AT_TOP
                    center.y = 1.0 - center.y;
                #endif
                float progress = saturate(_BigBangScreenWave.z);
                float recoveryStart = saturate(_BigBangScreenWave.w);
                if (recoveryStart <= 0.0 || progress >= 1.0)
                    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float phase = saturate(progress / recoveryStart);
                float envelope = 1.0 - smoothstep(recoveryStart, 1.0, progress);
                float2 aspect = float2(_ScreenParams.x / _ScreenParams.y, 1.0);
                float2 fromCaster = (uv - center) * aspect;
                float radius = length(fromCaster);
                float2 radialDirection = fromCaster / (max(radius, 0.001) * aspect);
                float cornerRadius = length(max(center, 1.0 - center) * aspect);

                float travel = saturate(phase / 0.63);
                float expansion = travel * (1.58 - travel * 0.58);
                float frontRadius = lerp(0.027, cornerRadius + 0.10, expansion);
                float angle = atan2(fromCaster.y, fromCaster.x + 0.00001);
                float ripple = (sin(angle * 6.0 - phase * 7.0) * 0.008 +
                    sin(angle * 11.0 + phase * 5.0) * 0.004) * smoothstep(0.035, 0.21, frontRadius);
                float frontDistance = radius - frontRadius + ripple;

                float lensDistance = frontDistance / 0.063;
                float lens = exp2(-lensDistance * lensDistance * 2.1) * envelope;
                float bend = -0.075 * lensDistance * lens;
                float split = 0.010 * lens;
                float2 refracted = uv + radialDirection * bend;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, refracted);
                scene.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, refracted + radialDirection * split).r;
                scene.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, refracted - radialDirection * split).b;

                float wake = (1.0 - smoothstep(-0.031, 0.018, frontDistance)) * envelope;
                half lightest = max(scene.r, max(scene.g, scene.b));
                half darkest = min(scene.r, min(scene.g, scene.b));
                // Reflect the RGB channels within their original range to rotate HSV hue by 180 degrees.
                half3 complement = (lightest + darkest) - scene.rgb;
                scene.rgb = lerp(scene.rgb, complement, wake);

                float crestDistance = (frontDistance - 0.009) / 0.012;
                float echoDistance = (frontDistance + 0.047) / 0.023;
                float crest = exp2(-crestDistance * crestDistance * 2.4) * envelope;
                float echo = exp2(-echoDistance * echoDistance * 2.1) * envelope;
                float glint = 0.71 + 0.29 * sin(angle + 1.15);
                scene.rgb += half3(0.68, 0.39, 0.09) * (0.52 * crest * glint);
                scene.rgb += half3(0.29, 0.13, 0.64) * (0.35 * echo);

                float spark = (1.0 - smoothstep(0.0, 0.20, phase)) *
                    (1.0 - smoothstep(0.019, 0.13, radius)) * envelope;
                scene.rgb += half3(0.42, 0.28, 0.12) * spark;
                return scene;
            }
            ENDHLSL
        }
    }
}
