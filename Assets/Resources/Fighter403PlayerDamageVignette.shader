Shader "Hidden/Fighter403/PlayerDamageVignette"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI texture", 2D) = "white" {}
        _HitOpacity ("Hit opacity", Range(0, 1)) = 0
        _CriticalOpacity ("Critical opacity", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float _HitOpacity;
            float _CriticalOpacity;

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float2 squared = p * p;
                // A rounded rectangle keeps all four edges visible and leaves the center clear.
                float radius = sqrt(sqrt(dot(squared, squared)));
                half black = smoothstep(0.48, 1.08, radius) * _HitOpacity;
                half glow = smoothstep(0.62, 1.08, radius);
                half rim = smoothstep(0.90, 1.02, radius);
                half red = (glow * 0.44 + rim * 0.38) * _CriticalOpacity * (1.0 - black);
                half3 tint = lerp(half3(0.38, 0.004, 0.018), half3(0.88, 0.018, 0.052), rim);
                return half4(tint * red, black + red);
            }
            ENDCG
        }
    }
}
