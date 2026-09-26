// Atlas sprites on world-space quads (features, clouds). Vertex color tints; alpha below 0.5 is cut unless _Soft = 1.
Shader "PixelGenesis/Sprite"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Soft ("Soft alpha (clouds)", Float) = 0
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
            half4 _EraTint; // global (Bölüm 2.12)

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float _Soft;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                if (_Soft < 0.5)
                {
                    clip(c.a - 0.5);
                    c.a = 1.0;
                }
                c.rgb *= _EraTint.rgb;
                return c;
            }
            ENDHLSL
        }
    }
}
