Shader "Hidden/Fighter403/RootKillOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI texture", 2D) = "white" {}
        _Opacity ("Opacity", Float) = 0
        _Age ("Age", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float _Opacity, _Age;

            Varyings vert(appdata_base input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord.xy;
                return output;
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                float2 pixels = floor(input.uv * float2(360, 216));
                float2 p = abs(pixels / float2(360, 216) * 2 - 1);
                float rim = max(p.x, p.y);
                float phase = floor(_Age * 16);
                float edgeSegments = step(.971, rim) * step(frac((pixels.x + pixels.y) / 26), .76);
                float cut = step(.929, rim) * step(.63, p.x) * step(.64, p.y);
                float stripe = step(frac((pixels.x - pixels.y - phase) / 11), .24);
                float blocks = step(.75, frac(sin(dot(floor(pixels / float2(11, 5)),
                    float2(72.12, 33.15))) * 16793.1)) * step(.89, rim) * stripe;
                float red = _Opacity * max(smoothstep(.70, 1, rim) * .32,
                    max(cut * stripe * .71, max(edgeSegments * .92, blocks * .65)));
                float3 tint = lerp(float3(.36,.018,.081), float3(.98,.22,.39), max(edgeSegments, cut * stripe));
                return fixed4(tint * red, red);
            }
            ENDCG
        }
    }
}
