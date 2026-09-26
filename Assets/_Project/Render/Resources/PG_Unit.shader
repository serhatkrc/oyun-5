// Bölüm 3.10: gray-index unit sprites (EkC 4.1: gray = index * 28) recolored through a palette texture (8 x rows).
// uv0 = atlas, uv1.x = palette row (v coordinate); vertex color tints (status effects).
Shader "PixelGenesis/Unit"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Palette ("Palette", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_Palette);
            SAMPLER(sampler_Palette);
            half4 _EraTint;

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Palette_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 row : TEXCOORD1;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 row : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.row = v.row;
                o.color = v.color;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 s = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(s.a - 0.5);
                float index = clamp(round(s.r * 255.0 / 28.0), 1.0, 8.0);
                half4 c = SAMPLE_TEXTURE2D(_Palette, sampler_Palette, float2((index - 0.5) / 8.0, i.row.x));
                c.rgb *= i.color.rgb * _EraTint.rgb;
                c.a = i.color.a;
                return c;
            }
            ENDHLSL
        }
    }
}
