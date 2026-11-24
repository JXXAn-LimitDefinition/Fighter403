Shader "Hidden/Fighter403/ThousandSlashes"
{
    Properties
    {
        _Blackout ("Blackout", Range(0, 1)) = 0
        _ColorRestore ("Color restore", Range(0, 1)) = 0
        _MainTex ("Slash atlas", 2D) = "white" {}
        _FrameUv ("Frame UV", Vector) = (1, 1, 0, 0)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Monochrome"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Blackout;
            float _ColorRestore;

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                half gray = dot(scene.rgb, half3(0.2126, 0.7152, 0.0722));
                half3 monochrome = gray.xxx * (1.0 - _Blackout);
                return half4(lerp(monochrome, scene.rgb, _ColorRestore), scene.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Colored slash"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex SlashVertex
            #pragma fragment SlashFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _FrameUv;

            struct Attributes { float3 position : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings SlashVertex(Attributes input)
            {
                Varyings output;
                output.position = TransformObjectToHClip(input.position);
                output.uv = input.uv * _FrameUv.xy + _FrameUv.zw;
                return output;
            }

            half4 SlashFragment(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            }
            ENDHLSL
        }
    }
}
