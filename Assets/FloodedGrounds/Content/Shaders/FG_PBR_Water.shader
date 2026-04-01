Shader "Flooded_Grounds/PBR_Water"
{
    Properties
    {
        _Color ("Water Color", Color) = (0.08, 0.12, 0.10, 0.5)
        _Metallic ("Metallic", Range(0,1)) = 0.85
        _Smth ("Smoothness", Range(0,1)) = 0.95
        _Emis ("Emission", Range(0,0.2)) = 0.02

        [Header(Normal Maps)]
        _BumpMap ("Normal Map 1", 2D) = "bump" {}
        _BumpMap2 ("Normal Map 2", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0,2)) = 1.0
        _BumpLerp ("Normal 2 Blend", Range(0,1)) = 0.5

        [Header(Animation)]
        _ScrollSpeed ("Scroll Speed", Float) = 0.15
        _ScrollDir1 ("Scroll Dir 1 XY", Vector) = (1, 0.5, 0, 0)
        _ScrollDir2 ("Scroll Dir 2 XY", Vector) = (-0.7, 0.3, 0, 0)

        [Header(Waves)]
        _WaveFreq ("Wave Frequency", Float) = 20
        _WaveHeight ("Wave Height", Float) = 0.15
        _WaveSpeed ("Wave Speed", Float) = 1.0

        // Legacy props kept so material values dont get lost
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _ParallaxMap ("Heightmap", 2D) = "black" {}
        _Parallax ("Height", Range(0.005, 0.08)) = 0.02
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
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Metallic;
                float _Smth;
                float _Emis;
                float4 _BumpMap_ST;
                float4 _BumpMap2_ST;
                float _BumpScale;
                float _BumpLerp;
                float _ScrollSpeed;
                float4 _ScrollDir1;
                float4 _ScrollDir2;
                float _WaveFreq;
                float _WaveHeight;
                float _WaveSpeed;
            CBUFFER_END

            TEXTURE2D(_BumpMap);  SAMPLER(sampler_BumpMap);
            TEXTURE2D(_BumpMap2); SAMPLER(sampler_BumpMap2);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv1         : TEXCOORD0;
                float2 uv2         : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                float3 normalWS    : TEXCOORD3;
                float3 tangentWS   : TEXCOORD4;
                float3 bitangentWS : TEXCOORD5;
                float  fogFactor   : TEXCOORD6;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // Wave vertex displacement
                float phase = _Time.y * _WaveSpeed * _WaveFreq;
                float waveOffset = (IN.positionOS.x + IN.positionOS.z * 2.0) * 8.0;
                IN.positionOS.y += sin(phase + waveOffset) * _WaveHeight;

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS  = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.normalWS    = normInputs.normalWS;
                OUT.tangentWS   = normInputs.tangentWS;
                OUT.bitangentWS = normInputs.bitangentWS;

                // Scrolling UVs for animated normals
                float t = _Time.y * _ScrollSpeed;
                OUT.uv1 = IN.uv * _BumpMap_ST.xy + _BumpMap_ST.zw + _ScrollDir1.xy * t;
                OUT.uv2 = IN.uv * _BumpMap2_ST.xy + _BumpMap2_ST.zw + _ScrollDir2.xy * t;

                OUT.fogFactor = ComputeFogFactor(posInputs.positionCS.z);

                return OUT;
            }

            half3 UnpackScaleNormal(half4 packednormal, half bumpScale)
            {
                half3 normal;
                normal.xy = (packednormal.wy * 2.0 - 1.0) * bumpScale;
                normal.z = sqrt(1.0 - saturate(dot(normal.xy, normal.xy)));
                return normal;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Sample and blend two scrolling normal maps
                half4 n1Raw = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv1);
                half4 n2Raw = SAMPLE_TEXTURE2D(_BumpMap2, sampler_BumpMap2, IN.uv2);
                half3 n1 = UnpackScaleNormal(n1Raw, _BumpScale);
                half3 n2 = UnpackScaleNormal(n2Raw, _BumpScale);
                half3 normalTS = normalize(lerp(n1, n2, _BumpLerp));

                // Tangent to world
                float3x3 TBN = float3x3(
                    normalize(IN.tangentWS),
                    normalize(IN.bitangentWS),
                    normalize(IN.normalWS)
                );
                float3 normalWS = normalize(mul(normalTS, TBN));

                // URP PBR lighting
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.fogCoord = IN.fogFactor;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);

                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = _Color.rgb;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smth;
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = 1.0;
                surfaceData.emission = _Color.rgb * _Emis;
                surfaceData.alpha = 1.0;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, IN.fogFactor);

                return color;
            }
            ENDHLSLPROGRAM
        }

        // Shadow caster
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Metallic;
                float _Smth;
                float _Emis;
                float4 _BumpMap_ST;
                float4 _BumpMap2_ST;
                float _BumpScale;
                float _BumpLerp;
                float _ScrollSpeed;
                float4 _ScrollDir1;
                float4 _ScrollDir2;
                float _WaveFreq;
                float _WaveHeight;
                float _WaveSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 _LightDirection;

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                float phase = _Time.y * _WaveSpeed * _WaveFreq;
                float waveOffset = (IN.positionOS.x + IN.positionOS.z * 2.0) * 8.0;
                IN.positionOS.y += sin(phase + waveOffset) * _WaveHeight;

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(IN.normalOS);
                posWS = posWS + _LightDirection * 0.01;
                OUT.positionCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSLPROGRAM
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
