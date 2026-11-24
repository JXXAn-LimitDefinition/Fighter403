Shader "Fighter403/RootFloorEffect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Cue ("Cue", Float) = 0
        _Progress ("Progress, age, radius, half width", Vector) = (0,0,4,0.5)
        _Dimensions ("Width, height", Vector) = (8,8,0,0)
        _Tone ("Tint", Color) = (0.65,0.56,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="False" }
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
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float _Cue;
            float4 _Progress;
            float4 _Dimensions;
            fixed4 _Tone;

            Varyings vert(appdata_base input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord.xy;
                return output;
            }

            float pixelLine(float distance, float width)
            {
                return step(abs(distance), width);
            }

            float hash(float2 cell)
            {
                return frac(sin(dot(cell, float2(32.37, 73.24))) * 15341.727);
            }

            fixed4 tone(float brightness, float alpha)
            {
                return fixed4(lerp(_Tone.rgb, float3(.92,.98,1), saturate(brightness)), alpha * _Tone.a);
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                const float pixel = 1.0 / 24.0;
                float progress = saturate(_Progress.x);
                float age = _Progress.y;
                float radius = _Progress.z;
                float halfWidth = _Progress.w;
                float2 world = (input.uv - .5) * _Dimensions.xy;
                float2 cell = floor(world / pixel);
                float2 samplePosition = (cell + .5) * pixel;
                float timeStep = floor(age * 24);

                if (_Cue < 1.5)
                {
                    float edge = max(length(samplePosition) - radius, (abs(samplePosition.y) - samplePosition.x) * .7071068);
                    if (edge > 0) return 0;

                    float rim = pixelLine(edge, pixel * 1.25);
                    float arc = pixelLine(length(samplePosition) - radius + .18, pixel * .8);
                    float dotMask = step(frac(cell.x * .2), .1) * step(frac(cell.y * .2), .1);
                    float radial = length(samplePosition);
                    float ticks = step(frac(atan2(abs(samplePosition.y), samplePosition.x) * 7), .19) *
                        step(radius - .37, radial) * step(radial, radius - .12);
                    if (_Cue < .5)
                    {
                        float cursor = pixelLine(radial - lerp(.32, radius - .14, progress), pixel);
                        float warning = .55 + .45 * progress;
                        return tone(rim * .32 + cursor * .35,
                            max(rim * (.58 + .38 * progress), max((arc + ticks * .72) * .47,
                                max(cursor * warning * .51, .067 + dotMask * .16 * warning))));
                    }

                    float fade = (1 - progress) * (1 - progress);
                    float crest = pixelLine(radial - lerp(.48, radius, saturate(progress * 2.7)), pixel * 2);
                    float chain = step(frac((cell.x + cell.y * 2 - timeStep) / 14), .16);
                    float shards = step(.83, hash(floor(cell / 4))) * chain;
                    return tone(.58 + crest * .36,
                        fade * max(rim * .85, max(crest, .16 + arc * .52 + shards * .36)));
                }

                if (_Cue < 3.5)
                {
                    if (abs(samplePosition.y) > halfWidth) return 0;
                    float across = abs(samplePosition.y);
                    float rim = pixelLine(across - halfWidth, pixel * 1.28);
                    float lane = pixelLine(across - halfWidth * .57, pixel);
                    float slugs = step(frac(samplePosition.x * .66 - timeStep * .087), .18);
                    float steps = step(frac(samplePosition.x * .94 - timeStep * .047), .25);
                    if (_Cue < 2.5)
                    {
                        float center = pixelLine(samplePosition.y, pixel * 1.15);
                        float locked = step(.7, progress);
                        return tone(center * (.25 + locked * .55) + slugs * .10,
                            max(center * (.45 + .45 * locked), max(.08 + rim * (.46 + locked * .18),
                                lane * (.28 * slugs + .22 + .18 * locked))));
                    }

                    float fade = (1 - progress) * (1 - progress);
                    float center = step(across, lerp(.22, .035, progress));
                    float bar = step(across, .34 + .09 * slugs);
                    float side = pixelLine(across - (.34 + .06 * steps), pixel * 1.20);
                    return tone(max(center, side * .45), fade * max(center, max(bar * (.52 + .23 * slugs),
                        max(side * .85, rim * (.36 + .36 * steps)))));
                }

                float2 border = _Dimensions.xy * .5 - abs(samplePosition);
                float rim = pixelLine(min(border.x, border.y), pixel * 1.55);
                float hatching = step(frac((cell.x + cell.y * .8 - timeStep) / 13), .13);
                float tile = step(frac((cell.x + timeStep) / 23), .3) * step(frac(cell.y / 8), .25);
                float panel = hash(floor(cell / float2(21,9)));
                if (_Cue < 4.5)
                {
                    float pulse = .55 + .45 * sin(age * 9);
                    float crawl = step(frac((cell.x + cell.y) / 22 - timeStep * .046), .24);
                    return tone(rim * crawl * .32, max(rim * (.54 + crawl * .42),
                        .075 + hatching * (.07 + .16 * pulse) + tile * .16));
                }

                float block = step(.69, panel) * tile;
                float edge = pixelLine(min(border.x, border.y) - .14, pixel);
                float impact = .68 * saturate(1 - age * 3);
                return tone(max(impact, rim * .36 + block * .28),
                    max(rim * .89, .18 + impact * .22 + block * .30 + hatching * .10 + edge * .34));
            }
            ENDCG
        }
    }
}
