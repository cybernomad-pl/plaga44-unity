Shader "Flooded_Grounds/PBR_TopBlend" {
    Properties {
        _MainTex ("Base Albedo (RGB)", 2D) = "white" {}
        _Spc("Base Metalness(R) Smoothness(A)", 2D) = "black" {}
        _BumpMap ("Base Normal", 2D) = "bump" {}
        _AO("Base AO", 2D)= "white" {}
        _layer1Tex ("Layer1 Albedo (RGB) Smoothness (A)", 2D) = "white" {}
        _layer1Metal ("Layer1 Metalness", Range(0,1)) = 0
        _layer1Norm("Layer 1 Normal", 2D) = "bump" {}
        _layer1Breakup ("Layer1 Breakup (R)", 2D) = "white" {}
        _layer1BreakupAmnt ("Layer1 Breakup Amount", Range(0,1)) = 0.5
        _layer1Tiling("Layer1 Tiling", float) = 10
        _Power ("Layer1 Blend Amount", float ) = 1
        _Shift("Layer1 Blend Height", float) = 1
        _DetailBump ("Detail Normal", 2D) = "bump" {}
        _DetailInt ("DetailNormal Intensity", Range(0,1)) = 0.4
        _DetailTiling("DetailNormal Tiling", float) = 2
    }

    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300

        Pass {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);
            TEXTURE2D(_Spc);        SAMPLER(sampler_Spc);
            TEXTURE2D(_BumpMap);    SAMPLER(sampler_BumpMap);
            TEXTURE2D(_AO);         SAMPLER(sampler_AO);
            TEXTURE2D(_layer1Tex);  SAMPLER(sampler_layer1Tex);
            TEXTURE2D(_layer1Norm); SAMPLER(sampler_layer1Norm);
            TEXTURE2D(_layer1Breakup); SAMPLER(sampler_layer1Breakup);
            TEXTURE2D(_DetailBump); SAMPLER(sampler_DetailBump);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BumpMap_ST;
                float _layer1Metal;
                float _layer1BreakupAmnt;
                float _layer1Tiling;
                float _Power;
                float _Shift;
                float _DetailInt;
                float _DetailTiling;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float2 uvBump      : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                float3 normalWS    : TEXCOORD3;
                float3 tangentWS   : TEXCOORD4;
                float3 bitangentWS : TEXCOORD5;
                float  fogFactor   : TEXCOORD6;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                VertexPositionInputs pi = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs ni = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS  = pi.positionCS;
                OUT.positionWS  = pi.positionWS;
                OUT.normalWS    = ni.normalWS;
                OUT.tangentWS   = ni.tangentWS;
                OUT.bitangentWS = ni.bitangentWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.uvBump      = TRANSFORM_TEX(IN.uv, _BumpMap);
                OUT.fogFactor   = ComputeFogFactor(pi.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Base textures
                half3 mainCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).rgb;
                half3 norm = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uvBump), 1.0);
                half4 spec = SAMPLE_TEXTURE2D(_Spc, sampler_Spc, IN.uv);
                half3 ao = SAMPLE_TEXTURE2D(_AO, sampler_AO, IN.uvBump).rgb;

                // Layer 1
                float2 l1uv = IN.uv * _layer1Tiling;
                half4 layer1 = SAMPLE_TEXTURE2D(_layer1Tex, sampler_layer1Tex, l1uv);
                half3 layer1norm = UnpackNormalScale(SAMPLE_TEXTURE2D(_layer1Norm, sampler_layer1Norm, l1uv), 1.0);
                half layer1Breakup = SAMPLE_TEXTURE2D(_layer1Breakup, sampler_layer1Breakup, l1uv).r;

                // Detail normal
                half3 detnorm = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailBump, sampler_DetailBump, IN.uv * _DetailTiling), 1.0);

                // Blended normal for direction test
                half3 modNormal = norm + half3(layer1norm.x * 0.6, layer1norm.y * 0.6, 0);

                // Build TBN
                float3x3 TBN = float3x3(
                    normalize(IN.tangentWS),
                    normalize(IN.bitangentWS),
                    normalize(IN.normalWS));
                float3 modNormalWS = normalize(mul(modNormal, TBN));

                // Blend mask (top-down projection)
                half blend = dot(modNormalWS, half3(0, 1, 0));
                half blend2 = (blend * _Power + _Shift) * lerp(1, layer1Breakup, _layer1BreakupAmnt);
                blend2 = saturate(pow(blend2, 3));

                // Combine
                half3 blendedNormal = lerp(norm, layer1norm, blend2);
                blendedNormal = blendedNormal + (detnorm * half3(_DetailInt, _DetailInt, 0));
                half3 blendedColor = lerp(mainCol, layer1.rgb, blend2);
                half blendedSmoothness = lerp(spec.a, layer1.a, blend2);
                half blendedMetallic = lerp(spec.r, _layer1Metal, blend2);

                float3 finalNormalWS = normalize(mul(normalize(blendedNormal), TBN));

                // URP lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = finalNormalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.fogCoord = IN.fogFactor;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.bakedGI = SampleSH(finalNormalWS);

                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif

                SurfaceData sd = (SurfaceData)0;
                sd.albedo = blendedColor;
                sd.metallic = blendedMetallic;
                sd.smoothness = blendedSmoothness;
                sd.normalTS = normalize(blendedNormal);
                sd.occlusion = ao.r;
                sd.emission = half3(0, 0, 0);
                sd.alpha = 1.0;

                half4 color = UniversalFragmentPBR(inputData, sd);
                color.rgb = MixFog(color.rgb, IN.fogFactor);
                return color;
            }
            ENDHLSL
        }

        Pass {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 p : POSITION; };
            struct V { float4 p : SV_POSITION; };
            V vertShadow(A i) { V o; o.p = TransformObjectToHClip(i.p.xyz); return o; }
            half4 fragShadow(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
