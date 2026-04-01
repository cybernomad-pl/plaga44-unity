Shader "Flooded_Grounds/PBR_Water"
{
    Properties
    {
        _Color ("Water Color", Color) = (0.4, 0.45, 0.42, 1)
        _Metallic ("Metallic", Range(0,1)) = 0.85
        _Smth ("Smoothness", Range(0,1)) = 0.95
        _Emis ("Emission", Range(0,0.5)) = 0.02
        _BumpMap ("Normal Map 1", 2D) = "bump" {}
        _BumpMap2 ("Normal Map 2", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0,3)) = 1.0
        _BumpLerp ("Normal 2 Blend", Range(0,1)) = 0.5
        _ScrollSpeed ("Scroll Speed", Range(0,2)) = 0.15
        _WaveFreq ("Wave Frequency", Range(0,100)) = 20
        _WaveHeight ("Wave Height", Range(0,3)) = 0.15
        _WaveComplexity ("Wave Complexity", Range(0,1)) = 0.5
        _WaveSteepness ("Wave Steepness", Range(0,1)) = 0.3
        _ReflStr ("Reflection Strength", Range(0,3)) = 1.0
        _FresnelPow ("Fresnel Power", Range(0.1,10)) = 4.0
        _UVScale ("UV Density", Range(0.1,200)) = 1.0
        _Alpha ("Transparency", Range(0,1)) = 1.0
        _FoamColor ("Foam Color", Color) = (0.85, 0.9, 0.85, 0.8)
        _FoamDepth ("Foam Depth Range", Range(0.01,5)) = 0.5
        _FoamStr ("Foam Strength", Range(0,3)) = 0.0
        _MainTex ("Base (RGB)", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
                float _WaveFreq;
                float _WaveHeight;
                float _WaveComplexity;
                float _WaveSteepness;
                float _ReflStr;
                float _FresnelPow;
                float _UVScale;
                float _Alpha;
                float4 _FoamColor;
                float _FoamDepth;
                float _FoamStr;
            CBUFFER_END

            TEXTURE2D(_BumpMap);  SAMPLER(sampler_BumpMap);
            TEXTURE2D(_BumpMap2); SAMPLER(sampler_BumpMap2);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
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
                float4 screenPos   : TEXCOORD7;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // Multi-octave Gerstner-style waves
                float t = _Time.y;
                float3 p = IN.positionOS.xyz;
                float h = 0;
                float dx = 0, dz = 0;

                // Wave 1 -- primary
                float phase1 = t * _WaveFreq + (p.x * 1.0 + p.z * 2.0) * 8.0;
                h += sin(phase1) * _WaveHeight;
                dx += cos(phase1) * _WaveSteepness * _WaveHeight * 1.0;
                dz += cos(phase1) * _WaveSteepness * _WaveHeight * 2.0;

                // Wave 2 -- cross wave (adds complexity)
                float phase2 = t * _WaveFreq * 0.7 + (p.x * 2.3 - p.z * 1.1) * 5.0;
                h += sin(phase2) * _WaveHeight * 0.5 * _WaveComplexity;
                dx += cos(phase2) * _WaveSteepness * _WaveHeight * 0.5 * 2.3;
                dz += cos(phase2) * _WaveSteepness * _WaveHeight * 0.5 * -1.1;

                // Wave 3 -- small ripples
                float phase3 = t * _WaveFreq * 1.8 + (p.x * 0.7 + p.z * 3.5) * 12.0;
                h += sin(phase3) * _WaveHeight * 0.25 * _WaveComplexity;

                // Wave 4 -- long swell
                float phase4 = t * _WaveFreq * 0.3 + (p.x * 0.3 + p.z * 0.8) * 2.0;
                h += sin(phase4) * _WaveHeight * 0.8 * _WaveComplexity;

                IN.positionOS.y += h;
                IN.positionOS.x += dx * _WaveComplexity;
                IN.positionOS.z += dz * _WaveComplexity;

                VertexPositionInputs pi = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs ni = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS  = pi.positionCS;
                OUT.positionWS  = pi.positionWS;
                OUT.normalWS    = ni.normalWS;
                OUT.tangentWS   = ni.tangentWS;
                OUT.bitangentWS = ni.bitangentWS;

                float scrollT = _Time.y * _ScrollSpeed;
                float2 baseUV = IN.uv * _UVScale;
                OUT.uv1 = baseUV * _BumpMap_ST.xy + _BumpMap_ST.zw + float2(scrollT, scrollT * 0.5);
                OUT.uv2 = baseUV * _BumpMap2_ST.xy + _BumpMap2_ST.zw + float2(-scrollT * 0.7, scrollT * 0.3);

                OUT.fogFactor = ComputeFogFactor(pi.positionCS.z);
                OUT.screenPos = ComputeScreenPos(pi.positionCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                half4 n1 = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv1);
                half4 n2 = SAMPLE_TEXTURE2D(_BumpMap2, sampler_BumpMap2, IN.uv2);

                half3 norm1;
                norm1.xy = (n1.wy * 2.0 - 1.0) * _BumpScale;
                norm1.z = sqrt(1.0 - saturate(dot(norm1.xy, norm1.xy)));

                half3 norm2;
                norm2.xy = (n2.wy * 2.0 - 1.0) * _BumpScale;
                norm2.z = sqrt(1.0 - saturate(dot(norm2.xy, norm2.xy)));

                half3 normalTS = normalize(lerp(norm1, norm2, _BumpLerp));

                float3x3 TBN = float3x3(
                    normalize(IN.tangentWS),
                    normalize(IN.bitangentWS),
                    normalize(IN.normalWS));
                float3 normalWS = normalize(mul(normalTS, TBN));

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.fogCoord = IN.fogFactor;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.bakedGI = SampleSH(normalWS);

                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif

                // Manual reflection probe/skybox cubemap sampling
                float3 viewDir = inputData.viewDirectionWS;
                float3 reflDir = reflect(-viewDir, normalWS);
                half mip = (1.0 - _Smth) * 6.0; // roughness to mip level

                // Sample unity_SpecCube0 (reflection probe or skybox fallback)
                half4 envSample = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflDir, mip);
                half3 envColor = DecodeHDREnvironment(envSample, unity_SpecCube0_HDR);

                // Fresnel - more reflective at grazing angles
                half NdotV = saturate(dot(normalWS, viewDir));
                half fresnel = pow(1.0 - NdotV, _FresnelPow);
                half reflAmount = lerp(_Metallic, 1.0, fresnel) * _ReflStr;

                // Blend water color with environment reflection
                half3 waterBase = _Color.rgb;
                half3 finalColor = lerp(waterBase, envColor, reflAmount);

                // Still use PBR for direct lighting (sun specular, shadows)
                SurfaceData sd = (SurfaceData)0;
                sd.albedo = finalColor;
                sd.metallic = 0; // we handle reflections manually
                sd.smoothness = _Smth;
                sd.normalTS = normalTS;
                sd.occlusion = 1.0;
                sd.emission = _Color.rgb * _Emis;
                sd.alpha = 1.0;

                half4 color = UniversalFragmentPBR(inputData, sd);

                // Add reflection on top of PBR result
                color.rgb += envColor * reflAmount * 0.3;

                // Depth-based transparency: shallow = transparent, deep = opaque
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float surfaceDepth = IN.screenPos.w;
                float depthDiff = sceneDepth - surfaceDepth;
                float depthFactor = saturate(depthDiff / _FoamDepth);
                // Shallow (near shore): more transparent. Deep (center): more opaque
                color.a = lerp(_Alpha * 0.1, _Alpha, depthFactor);
                // Foam color at shoreline (inverse -- subtle)
                float foamMask = (1.0 - depthFactor) * _FoamStr;
                color.rgb = lerp(color.rgb, _FoamColor.rgb, foamMask * _FoamColor.a * 0.3);

                color.rgb = MixFog(color.rgb, IN.fogFactor);
                color.a = _Alpha;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex sv
            #pragma fragment sf
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
                float _WaveFreq;
                float _WaveHeight;
                float _WaveComplexity;
                float _WaveSteepness;
                float _ReflStr;
                float _FresnelPow;
                float _UVScale;
                float _Alpha;
            CBUFFER_END

            struct A { float4 p : POSITION; };
            struct V { float4 p : SV_POSITION; };

            float3 _LightDirection;

            V sv(A i)
            {
                V o;
                float phase = _Time.y * _WaveFreq;
                float wo = (i.p.x + i.p.z * 2.0) * 8.0;
                i.p.y += sin(phase + wo) * _WaveHeight;
                o.p = TransformObjectToHClip(i.p.xyz);
                return o;
            }

            half4 sf(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack Off
}
