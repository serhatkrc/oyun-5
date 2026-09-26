// Bölüm 1.11.3: map chunks from a Texture2DArray. Alpha = material code (0 land, ~0.5 water, 1 lava).
Shader "PixelGenesis/MapChunk"
{
    Properties
    {
        _MainTex ("Chunks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma require 2darray
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_ARRAY(_MainTex);
            SAMPLER(sampler_MainTex);
            half4 _EraTint; // global, set by the era system (Bölüm 2); white by default

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 uv : TEXCOORD0; // xy = uv, z = array layer (chunk index)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 uv : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = v.uv;
                o.worldPos = ws.xy;
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, i.uv.xy, i.uv.z);
                float t = _Time.y;

                float2 px = floor(i.worldPos); // effects stay on the tile grid (pixel art)
                if (c.a > 0.3 && c.a < 0.7)
                {
                    // Water: +-6% brightness wave and one random glint pixel per 8x8 cell.
                    c.rgb *= 1.0 + 0.06 * sin(t * 1.5 + px.x * 0.7 + px.y * 0.4);
                    float2 cell = floor(px / 8.0);
                    float slot = floor(t * 2.0);
                    float2 pick = floor(float2(Hash21(cell + slot), Hash21(cell + slot + 17.1)) * 8.0);
                    if (all(px - cell * 8.0 == pick) && Hash21(cell - slot) > 0.6)
                        c.rgb = lerp(c.rgb, 1.0, 0.45);
                }
                else if (c.a > 0.8)
                {
                    // Lava: slow flicker and emission.
                    float f = 0.85 + 0.15 * sin(t * 2.3 + px.x * 0.35 + px.y * 0.5);
                    c.rgb = c.rgb * f * 1.25;
                }

                c.rgb *= _EraTint.rgb;
                c.a = 1.0;
                return c;
            }
            ENDHLSL
        }
    }
}
