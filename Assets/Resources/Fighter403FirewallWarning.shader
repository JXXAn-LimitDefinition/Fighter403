Shader "Fighter403/FirewallWarning"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Tint ("Warning tint", Color) = (1,0.6,0.4,0)
        _Shape ("Radius, inner radius, extent, style", Vector) = (1,0,1.17,0)
        _Charge ("Warning charge", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off
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
            float4 _Shape;
            float _Charge;

            Varyings vert(appdata_base v)
            {
                Varyings o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            fixed4 frag(Varyings i) : SV_Target
            {
                const float pixelsPerUnit = 24.0;
                const float pixel = 1.0 / pixelsPerUnit;
                const float inverseRootTwo = 0.70710678;
                float radius = _Shape.x;
                float style = _Shape.w;
                float2 p = (i.uv * 2.0 - 1.0) * _Shape.z;
                if (style == 2.0) p.x = i.uv.x * _Shape.z;
                float2 cell = floor(p * pixelsPerUnit);
                p = (cell + 0.5) * pixel;

                float radial = length(p);
                float diamond = abs(p.x) + abs(p.y);
                float rimDistance = radial - radius;
                float innerDistance = abs(diamond - _Shape.y) * inverseRootTwo;
                float ticks = step(min(abs(p.x), abs(p.y)), 1.5 * pixel) *
                    step(abs(rimDistance + 3.0 * pixel), 3.0 * pixel);
                float solid = 0.0;

                if (style == 1.0)
                {
                    innerDistance = abs(radial - _Shape.y);
                    solid = step(radial, _Shape.y);
                }
                else if (style == 2.0)
                {
                    float side = (abs(p.y) - p.x) * inverseRootTwo;
                    rimDistance = max(rimDistance, side);
                    innerDistance = abs(radial - radius + 7.0 * pixel) + step(-3.0 * pixel, side);
                    ticks = 0.0;
                }
                else if (style == 3.0)
                {
                    rimDistance = (diamond - radius) * inverseRootTwo;
                    innerDistance = abs(rimDistance + 5.0 * pixel);
                }
                else if (style == 4.0)
                {
                    // Open arrow pointing in the firing direction.
                    float arrow = p.x + abs(p.y) * 0.80 - radius * 0.64;
                    float rim = step(abs(arrow), pixel * 1.25) * step(abs(p.y), radius * 0.87);
                    float echo = step(abs(arrow + pixel * 4.0), pixel) * step(abs(p.y), radius * 0.62);
                    float3 color = lerp(_Tint.rgb, float3(1.0, 0.94, 0.76), _Charge * rim * 0.72);
                    return fixed4(color, (rim * (0.64 + 0.36 * _Charge) + (1.0 - rim) * echo * 0.33) * _Tint.a);
                }

                float rim = step(abs(rimDistance), pixel * (1.05 + _Charge * 0.85));
                float inner = step(innerDistance, pixel * (0.82 + _Charge * 0.56));
                float echo = step(abs(rimDistance + pixel * 4.0), pixel * 0.67);
                float inside = step(rimDistance, 0.0);
                float dots = step(frac(cell.x * 0.25), 0.1) * step(frac(cell.y * 0.25), 0.1);
                float hatch = step(frac((cell.x + cell.y) * 0.125), 0.14);
                float fill = inside * (0.055 + _Charge * 0.06 + dots * (0.13 + _Charge * 0.07) + solid * hatch * 0.16);
                float opacity = max(fill, max(echo * 0.31, max(inner * 0.64, max(ticks * 0.80, rim * (0.66 + _Charge * 0.34)))));
                float gleam = saturate(rim * _Charge * 0.40 + ticks * 0.32 + inner * 0.34);
                float3 color = lerp(_Tint.rgb, float3(1.0, 0.93, 0.82), gleam);
                return fixed4(color, opacity * _Tint.a);
            }
            ENDCG
        }
    }
}
