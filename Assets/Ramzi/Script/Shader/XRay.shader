Shader "URP/DitheredFakeTransparency"
{
    Properties
    {
        _BaseMap   ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color (RGBA)", Color) = (1,1,1,1)
        _DitherAlpha ("Dither Alpha (0..1)", Range(0,1)) = 0.5
        _DitherScale ("Dither Screen Scale", Range(0.25, 4)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            // Dither transparency tampak seperti transparan namun tetap zwrite
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest"
        }

        // =========================
        // Forward (Unlit) Pass
        // =========================
        Pass
        {
            Name "ForwardUnlit"
            Tags{ "LightMode"="UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual
            // Tidak pakai blending supaya tetap tembus dither, bukan alpha blend
            Blend Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // URP Core (tanpa fog)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float  _DitherAlpha;
            float  _DitherScale;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // 4x4 Bayer threshold (0..15). Dibagi 16 agar jadi [0..1]
            static const float _Bayer4x4[16] =
            {
                0,  8,  2, 10,
                12, 4, 14, 6,
                3, 11,  1,  9,
                15, 7, 13, 5
            };

            float DitherThreshold4x4(uint2 pix)
            {
                uint x = pix.x & 3u;
                uint y = pix.y & 3u;
                uint idx = y * 4u + x;
                // +0.5 agar threshold nggak tepat di boundary
                return (_Bayer4x4[idx] + 0.5) / 16.0;
            }

            float4 SampleBase(float2 uv)
            {
                float4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                return c * _BaseColor;
            }

            float2 screenPx(float4 clipPos)
            {
                // dari clip space ke NDC [−1..1], lalu ke pixel approx dengan _ScreenParams
                float2 ndc = clipPos.xy / max(1e-6, clipPos.w);
                float2 uv = 0.5 * (ndc + 1.0); // [0..1]
                // skala opsional agar kisi dither “lebih besar/kecil”
                uv /= max(1e-6, _DitherScale);
                return uv * _ScreenParams.xy;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 baseCol = SampleBase(IN.uv);

                // ambil posisi pixel (integer) buat bayer
                uint2 pix = (uint2)floor(screenPx(IN.positionCS));
                float thresh = DitherThreshold4x4(pix);

                // alpha efektif
                float a = saturate(baseCol.a * _DitherAlpha);

                // dithered cutout
                if (a < thresh) discard;

                // Unlit: cukup kembalikan warna RGB (alpha 1 agar tidak ter-blend)
                return float4(baseCol.rgb, 1.0);
            }
            ENDHLSL
        }

        // =========================
        // ShadowCaster Pass (sederhana, tanpa include URP)
        // =========================
        Pass
        {
            Name "ShadowCaster"
            Tags{ "LightMode"="ShadowCaster" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0 // tidak menulis warna—hanya depth

            HLSLPROGRAM
            #pragma vertex   sc_vert
            #pragma fragment sc_frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float  _DitherAlpha;
            float  _DitherScale;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct SCAttributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct SCVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            SCVaryings sc_vert(SCAttributes IN)
            {
                SCVaryings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            static const float _Bayer4x4[16] =
            {
                0,  8,  2, 10,
                12, 4, 14, 6,
                3, 11,  1,  9,
                15, 7, 13, 5
            };

            float DitherThreshold4x4(uint2 pix)
            {
                uint x = pix.x & 3u;
                uint y = pix.y & 3u;
                uint idx = y * 4u + x;
                return (_Bayer4x4[idx] + 0.5) / 16.0;
            }

            float2 screenPx(float4 clipPos)
            {
                float2 ndc = clipPos.xy / max(1e-6, clipPos.w);
                float2 uv = 0.5 * (ndc + 1.0);
                uv /= max(1e-6, _DitherScale);
                return uv * _ScreenParams.xy;
            }

            float4 sc_frag(SCVaryings IN) : SV_Target
            {
                // pakai aturan dither yang sama agar bayangan konsisten
                float4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                uint2 pix = (uint2)floor(screenPx(IN.positionCS));
                float thresh = DitherThreshold4x4(pix);
                float a = saturate(baseCol.a * _DitherAlpha);

                if (a < thresh) discard;

                // tulis depth saja (ColorMask 0), nilai warna tidak dipakai
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
