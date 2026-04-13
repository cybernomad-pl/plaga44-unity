Shader "Flooded_Grounds/Triplanar_BumpSpec"
{
    Properties
    {
        _TexScale ("Tex Scale", Range (0.1, 10.0))= 1.0
        _BlendPlateau ("BlendPlateau", Range (0.0, 1.0)) = 0.2
        _MainTex ("Base 1 (RGB) Gloss(A)", 2D) = "white" {}
        _BumpMap1 ("NormalMap 1 (_Y_X)", 2D)  = "bump" {}
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "IgnoreProjector"="True" "RenderType"="Opaque" }
        ZWrite On
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap1); SAMPLER(sampler_BumpMap1);

            CBUFFER_START(UnityPerMaterial)
                half _TexScale;
                half _BlendPlateau;
                half _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 localPos   : TEXCOORD0;
                float3 localNorm  : TEXCOORD1;
                float4 color      : COLOR;
                float  fogFactor  : TEXCOORD2;
                float3 normalWS   : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                VertexPositionInputs pi = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs ni = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = pi.positionCS;
                OUT.positionWS = pi.positionWS;
                OUT.normalWS   = ni.normalWS;
                OUT.localPos   = IN.positionOS.xyz;
                OUT.localNorm  = IN.normalOS;
                OUT.color      = IN.color;
                OUT.fogFactor  = ComputeFogFactor(pi.positionCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Triplanar blend weights
                half3 blend_weights = abs(IN.localNorm.xyz);
                blend_weights = max(blend_weights - _BlendPlateau, 0);
                blend_weights /= (blend_weights.x + blend_weights.y + blend_weights.z);

                // UV coords for each projection
                half2 coord1 = IN.localPos.yz * _TexScale;
                half2 coord2 = IN.localPos.zx * _TexScale;
                half2 coord3 = IN.localPos.xy * _TexScale;

                // Sample color
                half4 col1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, coord1);
                half4 col2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, coord2);
                half4 col3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, coord3);

                half4 blended_color = col1 * blend_weights.x + col2 * blend_weights.y + col3 * blend_weights.z;
                half4 c = blended_color;
                c.rgb *= IN.color.rgb;

                clip(c.a - _Cutoff);

                // Simple lit output
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalize(IN.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.fogCoord = IN.fogFactor;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.shadowCoord = float4(0, 0, 0, 0);

                SurfaceData sd = (SurfaceData)0;
                sd.albedo = c.rgb;
                sd.metallic = 0;
                sd.smoothness = 0.2;
                sd.normalTS = half3(0, 0, 1);
                sd.occlusion = 1.0;
                sd.emission = half3(0, 0, 0);
                sd.alpha = c.a;

                half4 color = UniversalFragmentPBR(inputData, sd);
                color.rgb = MixFog(color.rgb, IN.fogFactor);
                return color;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
