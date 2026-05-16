// Hidden shader used by Paint_Effect.cs via Graphics.Blit to accumulate brush strokes.
// Never assign to a material in the Inspector — managed entirely by the C# script.
Shader "Hidden/BrushStamp"
{
    Properties
    {
        _MainTex       ("Source RT",        2D)     = "black" {}
        _BrushTexture  ("Brush Texture",    2D)     = "white" {}
        _BrushUV       ("Brush UV Center",  Vector) = (0.5, 0.5, 0, 0)
        _BrushSize     ("Brush Size",       Float)  = 0.03
        _BrushColor    ("Brush Color",      Color)  = (0, 0, 0, 1)
        _BrushHardness ("Brush Hardness",   Float)  = 0.8
        _EraseMode     ("Erase Mode",       Float)  = 0
        _UseBrushTex   ("Use Brush Texture",Float)  = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            ZTest  Always
            ZWrite Off
            Cull   Off

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);      SAMPLER(sampler_MainTex);
            TEXTURE2D(_BrushTexture); SAMPLER(sampler_BrushTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BrushTexture_ST;
                float4 _BrushUV;
                float  _BrushSize;
                half4  _BrushColor;
                float  _BrushHardness;
                float  _EraseMode;
                float  _UseBrushTex;
            CBUFFER_END

            struct Attributes { float4 posOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 posCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.posCS = TransformObjectToHClip(IN.posOS.xyz);
                OUT.uv    = IN.uv;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                half4 existing = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                float mask;

                // Radial gradient — always applied so stamp edges fade out smoothly
                float dist        = length(IN.uv - _BrushUV.xy) / max(_BrushSize, 0.0001);
                float radialFade  = 1.0 - smoothstep(_BrushHardness, 1.0, dist);

                if (_UseBrushTex > 0.5)
                {
                    // ── Texture brush ────────────────────────────────────────
                    // Remap fragment position into [0,1] brush-texture space.
                    float2 brushTexUV = (IN.uv - _BrushUV.xy) / (_BrushSize * 2.0) + 0.5;

                    // Red channel drives the shape (works for greyscale & alpha maps)
                    float texShape = SAMPLE_TEXTURE2D(_BrushTexture, sampler_BrushTexture,
                                                      saturate(brushTexUV)).r;

                    // Multiply by radial fade: texture defines shape variations,
                    // radial fade ensures stamps always blend into a continuous stroke
                    float2 inB = step(float2(0,0), brushTexUV) * step(brushTexUV, float2(1,1));
                    mask = texShape * radialFade * inB.x * inB.y;
                }
                else
                {
                    // ── Default circular brush ────────────────────────────────
                    mask = radialFade;
                }

                if (_EraseMode > 0.5)
                {
                    // Erase: fade alpha toward 0 in the brush footprint
                    return half4(existing.rgb, existing.a * (1.0 - mask));
                }
                else
                {
                    // Paint: blend brush color onto existing content
                    float blend  = mask * _BrushColor.a;
                    half3 newRgb = lerp(existing.rgb, _BrushColor.rgb, blend);
                    half  newA   = max(existing.a, blend);
                    return half4(newRgb, newA);
                }
            }
            ENDHLSL
        }
    }
}
