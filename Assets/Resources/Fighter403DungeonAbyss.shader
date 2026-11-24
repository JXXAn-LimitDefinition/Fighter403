Shader "Fighter403/DungeonAbyss"
{
    Properties
    {
        _CodeAtlas ("PixelFX code atlas", 2D) = "black" {}
        _AbyssInk ("Deep background", Vector) = (.016,.024,.045,0)
        _WallInk ("Background near walls", Vector) = (.027,.049,.084,0)
        [PerRendererData] _RoomBounds ("Room center and half size", Vector) = (0,0,10.5,6)
        [PerRendererData] _AbyssTime ("Unscaled time", Float) = 0
        [PerRendererData] _SeedPhase ("Room phase", Float) = 0
        [PerRendererData] _RoomSeed ("Room pattern seed", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _CodeAtlas;
            float4 _RoomBounds;
            float4 _AbyssInk, _WallInk;
            float4 _RoomSeed;
            float _AbyssTime, _SeedPhase;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 world : TEXCOORD0;
            };

            Varyings vert(appdata_base input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.world = mul(unity_ObjectToWorld, input.vertex).xy;
                return output;
            }

            float hash(float2 cell)
            {
                float3 p = frac(cell.xyx * .1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            half3 SampleCode(float2 pixels, float time, float2 seed)
            {
                const float2 tileSize = float2(256,160);
                const float2 atlasGrid = float2(8,12);
                float2 tile = floor(pixels / tileSize);
                float2 tileSeed = tile + seed;
                // Stagger patterns across both axes, then move each within its blank margins.
                float layout = frac((tile.x + 2.0 * tile.y + floor(hash(seed) * 3.0) + .5) / 3.0) * 3.0;
                layout = floor(layout);
                float2 jitter = floor((float2(hash(tileSeed), hash(tileSeed.yx + 43.2)) - .5) * float2(14.0, 10.0));
                float2 position = floor(frac(pixels / tileSize) * tileSize) - jitter;
                float insideTile = all(position >= 0.0) && all(position < tileSize);
                float2 coordinate = (clamp(position, 0.0, tileSize - 1.0) + .5) / tileSize;
                float step = layout < .5 ? 1.0 : .5;
                float offset = layout < .5 ? 0.0 : 24.0 * (layout + 1.0);
                float frame = floor(fmod((time * 10.0 + 48.0 * hash(tileSeed + 11.7)) * step, 48.0 * step)) + offset;
                float2 index = float2(fmod(frame, atlasGrid.x), atlasGrid.y - 1.0 - floor(frame / atlasGrid.x));
                half4 texel = tex2D(_CodeAtlas, (index + coordinate) / atlasGrid);
                return texel.rgb * texel.a * insideTile;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 local = input.world - _RoomBounds.xy;
                float2 edge = abs(local) - _RoomBounds.zw;
                clip(max(edge.x, edge.y));
                float depth = length(max(edge, 0.0));
                float nearWall = exp2(-depth * .63);
                float2 cameraOffset = _WorldSpaceCameraPos.xy - _RoomBounds.xy;
                float time = _AbyssTime + _SeedPhase;

                float2 seed = _RoomSeed.xy + _RoomSeed.zw * .6875;
                float2 farOrigin = float2(hash(seed + 17.1), hash(seed.yx + 51.3)) * float2(256,160);
                float2 nearOrigin = float2(hash(seed + 71.3), hash(seed.yx + 124.6)) * float2(256,160);
                float2 farPixels = (local - cameraOffset * .43) * float2(39.0, 33.0) + farOrigin + float2(0.0, -time * 1.5);
                float2 nearPixels = (local - cameraOffset * .13) * 18.0 + nearOrigin + float2(0.0, -time * 2.3);
                half3 distantCode = SampleCode(farPixels, time + 1.65, seed + 31.5) * (.05 + .08 * nearWall);
                half3 closeCode = SampleCode(nearPixels, time, seed) * (.38 + .29 * nearWall);

                half3 ink = lerp(_AbyssInk.rgb, _WallInk.rgb, nearWall);
                #ifndef UNITY_COLORSPACE_GAMMA
                ink = GammaToLinearSpace(ink);
                #endif
                return half4(ink + distantCode + closeCode, 1.0);
            }
            ENDCG
        }
    }
}
