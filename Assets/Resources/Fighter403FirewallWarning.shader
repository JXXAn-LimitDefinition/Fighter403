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
                float angle = atan2(p.y, p.x) * 0.159154943;
                float ticks = step(min(abs(p.x), abs(p.y)), 1.5 * pixel) *
                    step(abs(rimDistance + 3.0 * pixel), 3.0 * pixel);
                float accent = 0.0;
                float pattern = 0.0;

                if (style == 0.0)
                {
                    // The impact point stays fixed while four brackets dial in around it.
                    float target = radius * lerp(0.83, 0.36, _Charge);
                    float bracket = step(abs(max(abs(p.x), abs(p.y)) - target), pixel);
                    bracket *= step(target - 0.24, min(abs(p.x), abs(p.y)));
                    float dial = frac(angle + 0.5);
                    float bearing = step(dial, _Charge) * step(abs(rimDistance + pixel * 5.0), pixel);
                    accent = max(bracket * 0.75, bearing * 0.71);
                    pattern = step(diamond, radius * 0.28) * 0.12;
                }
                else if (style == 1.0)
                {
                    innerDistance = abs(radial - _Shape.y);
                    float arm = frac(angle * 3.0 + radial * 0.65 - _Charge * 8.0);
                    float teeth = step(arm, 0.14) * step(_Shape.y, radial);
                    float innerTick = step(0.83, frac(angle * 12.0)) *
                        step(abs(radial - _Shape.y + pixel * 3.0), pixel * 3.0);
                    accent = innerTick * 0.63;
                    pattern = teeth * (0.08 + _Charge * 0.13);
                }
                else if (style == 2.0)
                {
                    float side = (abs(p.y) - p.x) * inverseRootTwo;
                    rimDistance = max(rimDistance, side);
                    innerDistance = abs(radial - radius + 7.0 * pixel) + step(0.0, side);
                    ticks = 0.0;
                    float shutter = step(abs(frac(radial * 1.4 - _Charge * 2.0) - 0.5), pixel * 2.1);
                    float direction = step(abs(abs(p.y) - p.x * 0.72), pixel * 1.35);
                    accent = shutter * direction * step(p.x, radius * 0.76) * 0.73;
                    pattern = shutter * (0.045 + _Charge * 0.1);
                }
                else if (style == 3.0)
                {
                    rimDistance = (diamond - radius) * inverseRootTwo;
                    innerDistance = abs(rimDistance + 5.0 * pixel);
                    float scan = step(abs(p.y - lerp(radius, -radius, _Charge)), pixel);
                    accent = scan * step(diamond, radius - pixel * 2.0) * 0.65;
                    pattern = step(frac(cell.y * 0.25), 0.10) * 0.11;
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
                float fill = inside * (0.055 + _Charge * 0.05 + dots * (0.11 + _Charge * 0.05) + pattern);
                float outline = max(echo * 0.31, max(inner * 0.59, max(ticks * 0.80, rim * (0.66 + _Charge * 0.34))));
                float opacity = max(inside * accent, max(fill, outline));
                float gleam = saturate(rim * _Charge * 0.40 + ticks * 0.32 + inner * 0.34 + accent * 0.29);
                float3 color = lerp(_Tint.rgb, float3(1.0, 0.93, 0.82), gleam);
                return fixed4(color, opacity * _Tint.a);
            }
            ENDCG
        }
    }
}
