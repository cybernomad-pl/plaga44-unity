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
        _MainTex ("Base (RGB)", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float phase = _Time.y * _WaveFreq;
                float wo = (IN.positionOS.x + IN.positionOS.z * 2.0) * 8.0;
                IN.positionOS.y += sin(phase + wo) * _WaveHeight;

                VertexPositionInputs pi = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs ni = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS  = pi.positionCS;
                OUT.positionWS  = pi.positionWS;
                OUT.normalWS    = ni.normalWS;
                OUT.tangentWS   = ni.tangentWS;
                OUT.bitangentWS = ni.bitangentWS;

                float t = _Time.y * _ScrollSpeed;
                OUT.uv1 = IN.uv * _BumpMap_ST.xy + _BumpMap_ST.zw + float2(t, t * 0.5);
                OUT.uv2 = IN.uv * _BumpMap2_ST.xy + _BumpMap2_ST.zw + float2(-t * 0.7, t * 0.3);

                OUT.fogFactor = ComputeFogFactor(pi.positionCS.z);
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

                SurfaceData sd = (SurfaceData)0;
                sd.albedo = _Color.rgb;
                sd.metallic = _Metallic;
                sd.smoothness = _Smth;
                sd.normalTS = normalTS;
                sd.occlusion = 1.0;
                sd.emission = _Color.rgb * _Emis;
                sd.alpha = 1.0;

                half4 color = UniversalFragmentPBR(inputData, sd);
                color.rgb = MixFog(color.rgb, IN.fogFactor);
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
