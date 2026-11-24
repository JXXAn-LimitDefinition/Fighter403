Shader "Fighter403/FirewallPulse"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Tint ("Pulse tint", Color) = (1,0.3,0.2,1)
        _Flash ("Flash", Range(0,1)) = 0
        _Sweep ("Sweep progress", Range(0,1)) = 0
        _Direction ("Sweep direction", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _Tint;
            float _Flash;
            float _Sweep;
            float4 _Direction;

            Varyings vert(appdata_base v)
            {
                Varyings o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            fixed4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                float2 p = uv * 2.0 - 1.0;
                float2 delta = abs(p);
                float edge = smoothstep(0.54, 1.20, length(delta));
                float radial = length(p * float2(1.23, 1.0)) * 0.55;
                float planar = dot(p, _Direction.xy) * 0.5 + 0.5;
                float path = lerp(radial, planar, saturate(length(_Direction.xy)));
                float wave = abs(path - _Sweep * 1.28);
                float scan = 1.0 - smoothstep(0.014, 0.105, wave);
                float bands = step(0.75, frac(uv.y * 117.0 + _Sweep * 11.0));
                float2 cell = floor(uv * float2(43.0, 25.0));
                float dropouts = step(0.77, frac(sin(dot(cell, float2(12.9, 78.2))) * 43758.5));
                float brokenSweep = scan * dropouts;
                float sides = smoothstep(0.80, 0.98, max(delta.x, delta.y));
                float3 color = lerp(_Tint.rgb * 0.32, _Tint.rgb, saturate(scan * 0.45 + sides * 0.42 + brokenSweep));
                color = lerp(color, float3(1.0, 0.96, 0.89), brokenSweep * 0.55);
                float alpha = _Flash * (0.035 + edge * (0.73 + bands * 0.26) + scan * 0.19 + brokenSweep * 0.24);
                return fixed4(color, saturate(alpha));
            }
            ENDCG
        }
    }
}
