Shader "Custom/PaperPaint"
{
    // ── Shader Graph equivalent ───────────────────────────────────────────────
    //  PaperTexture  ──► Sample Texture 2D ──► Base Color
    //  PaintTexture  ──► Sample Texture 2D ──┐
    //                                        ├─► Lerp(base, paint.rgb, paint.a) ──► Output
    //  _EraseMode exposed as float toggle (managed by C# script via RT alpha)
    // ─────────────────────────────────────────────────────────────────────────

    Properties
    {
        _PaperTexture ("Paper Texture",          2D)    = "white" {}
        _PaintTexture ("Paint Texture (RT)",     2D)    = "black" {}
        _BrushColor   ("Brush Color",            Color) = (0, 0, 0, 1)
        _BrushSize    ("Brush Size",             Float) = 0.03
        [Toggle] _EraseMode ("Erase Mode",       Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        // ── Forward pass ─────────────────────────────────────────────────────
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_PaperTexture); SAMPLER(sampler_PaperTexture);
            TEXTURE2D(_PaintTexture); SAMPLER(sampler_PaintTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _PaperTexture_ST;
                float4 _PaintTexture_ST;
                half4  _BrushColor;
                float  _BrushSize;
                float  _EraseMode;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _PaperTexture);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // Sample base paper
                half4 paper = SAMPLE_TEXTURE2D(_PaperTexture, sampler_PaperTexture, IN.uv);

                // Sample accumulated paint layer (alpha = stroke coverage)
                half4 paint = SAMPLE_TEXTURE2D(_PaintTexture, sampler_PaintTexture, IN.uv);

                // Overlay blend: paint sits on top of paper, driven by paint.a
                half3 result = lerp(paper.rgb, paint.rgb, paint.a);

                return half4(result, 1.0h);
            }
            ENDHLSL
        }

        // ── Shadow caster ─────────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _PaperTexture_ST;
                float4 _PaintTexture_ST;
                half4  _BrushColor;
                float  _BrushSize;
                float  _EraseMode;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 posOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 posCS : SV_POSITION; };

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS    = TransformObjectToWorld(IN.posOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

#if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - posWS);
#else
                float3 lightDirectionWS = _LightDirection;
#endif

                float4 posCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, lightDirectionWS));
                posCS = ApplyShadowClamping(posCS);
                OUT.posCS = posCS;
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // ── Depth-only pass ───────────────────────────────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _PaperTexture_ST;
                float4 _PaintTexture_ST;
                half4  _BrushColor;
                float  _BrushSize;
                float  _EraseMode;
            CBUFFER_END

            struct Attributes { float4 posOS : POSITION; };
            struct Varyings   { float4 posCS : SV_POSITION; };

            Varyings DepthVert(Attributes IN)
            {
                Varyings OUT;
                OUT.posCS = TransformObjectToHClip(IN.posOS.xyz);
                return OUT;
            }

            half4 DepthFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
