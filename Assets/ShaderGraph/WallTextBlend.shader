Shader "Custom/Wall Text Blend"
{
    Properties
    {
        _BaseMap("Base Texture", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (0.6, 0.6, 0.6, 1)
        _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalStrength("Normal Strength", Range(0, 2)) = 1
        _HeightMap("Height Map", 2D) = "gray" {}
        _HeightStrength("Height Strength", Range(0, 0.1)) = 0
        _NoiseScale("Noise Scale", Float) = 8
        _NoiseStrength("Noise Strength", Range(0, 1)) = 0.15
        _NoiseRoughness("Noise Roughness", Range(0, 1)) = 0.2
        _NoiseMap("Noise Map", 2D) = "gray" {}
        _UseNoiseMap("Use Noise Map", Range(0, 1)) = 0
        _TextMap("Text Render Texture", 2D) = "black" {}
        _TextColor("Text Color", Color) = (0, 0, 0, 1)
        _UseTextMapColor("Use Render Texture Color", Range(0, 1)) = 1
        _TextStrength("Text Strength", Range(0, 1)) = 1
        _ReflectionStrength("Reflection Strength", Range(0, 1)) = 1
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_HeightMap);
            SAMPLER(sampler_HeightMap);
            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);
            TEXTURE2D(_TextMap);
            SAMPLER(sampler_TextMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NormalMap_ST;
                float4 _HeightMap_ST;
                float4 _NoiseMap_ST;
                float4 _TextMap_ST;
                half4 _BaseColor;
                half4 _TextColor;
                half _NormalStrength;
                half _HeightStrength;
                half _NoiseScale;
                half _NoiseStrength;
                half _NoiseRoughness;
                half _UseNoiseMap;
                half _UseTextMapColor;
                half _TextStrength;
                half _ReflectionStrength;
                half _Metallic;
                half _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 baseUV : TEXCOORD1;
                float2 textUV : TEXCOORD2;
                float2 normalUV : TEXCOORD3;
                float2 heightUV : TEXCOORD4;
                float3 tangentWS : TEXCOORD5;
                float3 bitangentWS : TEXCOORD6;
                float3 positionWS : TEXCOORD7;
                half fogFactor : TEXCOORD8;
            };

            float ValueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);

                float a = frac(sin(dot(i, float2(127.1, 311.7))) * 43758.5453);
                float b = frac(sin(dot(i + float2(1.0, 0.0), float2(127.1, 311.7))) * 43758.5453);
                float c = frac(sin(dot(i + float2(0.0, 1.0), float2(127.1, 311.7))) * 43758.5453);
                float d = frac(sin(dot(i + float2(1.0, 1.0), float2(127.1, 311.7))) * 43758.5453);

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float2 ApplyParallaxOffset(float2 uv, float height, float3 viewDirTS)
            {
                float2 offset = viewDirTS.xy / max(viewDirTS.z, 0.25) * (height - 0.5) * _HeightStrength;
                return uv - offset;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.baseUV = TRANSFORM_TEX(input.uv, _BaseMap);
                output.textUV = TRANSFORM_TEX(input.uv, _TextMap);
                output.normalUV = TRANSFORM_TEX(input.uv, _NormalMap);
                output.heightUV = TRANSFORM_TEX(input.uv, _HeightMap);

                float3 tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
                if (dot(tangentWS, tangentWS) < 0.0001)
                {
                    float3 fallbackAxis = abs(output.normalWS.y) < 0.999 ? float3(0, 1, 0) : float3(1, 0, 0);
                    tangentWS = cross(fallbackAxis, output.normalWS);
                }

                output.tangentWS = normalize(tangentWS);
                float tangentSign = input.tangentOS.w == 0 ? 1.0 : input.tangentOS.w;
                output.bitangentWS = cross(output.normalWS, output.tangentWS) * tangentSign * GetOddNegativeScale();
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3x3 tangentToWorld = half3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                half3 viewDirTS = mul(transpose(tangentToWorld), viewDirWS);

                half height = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, input.heightUV).r;
                float2 parallaxUV = ApplyParallaxOffset(input.baseUV, height, viewDirTS);
                float2 parallaxNormalUV = ApplyParallaxOffset(input.normalUV, height, viewDirTS);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, parallaxUV) * _BaseColor;
                half4 textSample = SAMPLE_TEXTURE2D(_TextMap, sampler_TextMap, input.textUV);

                half textBrightness = max(max(textSample.r, textSample.g), textSample.b);
                half textMask = saturate(max(textSample.a, textBrightness) * _TextStrength);

                half noise = ValueNoise(parallaxUV * _NoiseScale);
                half textureNoise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, parallaxUV * _NoiseScale).r;
                noise = lerp(noise, textureNoise, _UseNoiseMap);
                half noiseBlend = lerp(1.0h, noise, _NoiseStrength);
                baseSample.rgb *= noiseBlend;

                half3 finalTextColor = lerp(_TextColor.rgb, textSample.rgb, _UseTextMapColor);
                half3 albedo = lerp(baseSample.rgb, finalTextColor, textMask);

                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, parallaxNormalUV),
                    _NormalStrength
                );
                half3 normalWS = normalize(
                    normalTS.x * input.tangentWS +
                    normalTS.y * input.bitangentWS +
                    normalTS.z * input.normalWS
                );

                half smoothness = _Smoothness * lerp(1.0h, noise, _NoiseRoughness);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.specular = half3(0, 0, 0);
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = smoothness;
                surfaceData.normalTS = normalTS;
                surfaceData.emission = 0;
                surfaceData.occlusion = saturate(_ReflectionStrength);
                surfaceData.alpha = 1;
                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 0;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionHCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirWS;
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = 0;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionHCS);
                inputData.shadowMask = half4(1, 1, 1, 1);
                inputData.tangentToWorld = tangentToWorld;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                half3 matteColor = albedo * max(SampleSH(normalWS), half3(0.2, 0.2, 0.2));
                color.rgb = lerp(matteColor, color.rgb, saturate(_ReflectionStrength));
                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
