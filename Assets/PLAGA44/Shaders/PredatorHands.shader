// =============================================================================
// PredatorHands.shader
// Translucent hands z fresnel rim + skybox reflection + normal distortion.
// URP, transparent queue, Quest-friendly (brak screen-space refraction).
// =============================================================================
Shader "PLAGA44/PredatorHands"
{
    Properties
    {
        [Header(Tint)]
        _TintColor        ("Tint Color (RGB * Alpha)", Color) = (1, 0.45, 0.1, 0.15)
        _BaseAlpha        ("Base Alpha", Range(0, 1)) = 0.05

        [Header(Fresnel Rim)]
        _RimColor         ("Rim Color", Color) = (1, 0.7, 0.3, 1)
        _FresnelPower     ("Fresnel Power", Range(0.1, 16)) = 4.0
        _RimStrength      ("Rim Strength", Range(0, 4)) = 2.0

        [Header(Reflection)]
        _ReflStrength     ("Reflection Strength", Range(0, 2)) = 1.0
        _ReflRoughness    ("Reflection Roughness (mip)", Range(0, 6)) = 1.0

        [Header(Distortion)]
        _DistortAmount    ("Distortion Amount", Range(0, 0.3)) = 0.04
        _DistortSpeed     ("Distortion Speed", Range(0, 4)) = 0.6
        _DistortFreq      ("Distortion Frequency", Range(0.5, 16)) = 4.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull  Back

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile _ _REFLECTION_PROBE_BOX_PROJECTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float  _BaseAlpha;
                float4 _RimColor;
                float  _FresnelPower;
                float  _RimStrength;
                float  _ReflStrength;
                float  _ReflRoughness;
                float  _DistortAmount;
                float  _DistortSpeed;
                float  _DistortFreq;
            CBUFFER_END

            Varyings Vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = nrmInputs.normalWS;
                OUT.uv         = IN.uv;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

                // Fresnel -- rim (Schlick-like approximation)
                float NdotV   = saturate(dot(N, V));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);

                // Reflection vector, distorted by moving sine pattern on UVs
                // (fake refraction-ish wobble a'la predator cloak)
                float  t       = _Time.y * _DistortSpeed;
                float2 wobble  = float2(
                    sin(IN.uv.y * _DistortFreq + t),
                    cos(IN.uv.x * _DistortFreq - t)
                ) * _DistortAmount;

                float3 R = reflect(-V, N);
                R.xy += wobble;
                R = normalize(R);

                // Sample reflection probe (skybox fallback)
                half4 reflEncoded = SAMPLE_TEXTURECUBE_LOD(
                    unity_SpecCube0, samplerunity_SpecCube0, R, _ReflRoughness);
                half3 reflColor = DecodeHDREnvironment(reflEncoded, unity_SpecCube0_HDR);

                // Compose
                half3 rim      = _RimColor.rgb * fresnel * _RimStrength;
                half3 refl     = reflColor * _ReflStrength;
                half3 tint     = _TintColor.rgb;
                half3 rgb      = tint + refl + rim;

                half alpha = saturate(_BaseAlpha + fresnel * _TintColor.a + fresnel * _RimColor.a);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
