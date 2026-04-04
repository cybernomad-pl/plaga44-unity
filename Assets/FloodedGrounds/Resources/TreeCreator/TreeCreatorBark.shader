Shader "Nature/Tree Creator Bark" {
Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _Shininess ("Shininess", Range (0.01, 1)) = 0.078125
    _MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
    _BumpMap ("Normalmap", 2D) = "bump" {}
    _GlossMap ("Gloss (A)", 2D) = "black" {}

    // These are here only to provide default values
    _SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 1)
    [HideInInspector] _TreeInstanceColor ("TreeInstanceColor", Vector) = (1,1,1,1)
    [HideInInspector] _TreeInstanceScale ("TreeInstanceScale", Vector) = (1,1,1,1)
    [HideInInspector] _SquashAmount ("Squash", Float) = 1
}

SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TreeBark" }
    LOD 200

    Pass {
        Name "ForwardLit"
        Tags { "LightMode" = "UniversalForward" }
        HLSLPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
        TEXTURE2D(_BumpMap);  SAMPLER(sampler_BumpMap);
        TEXTURE2D(_GlossMap); SAMPLER(sampler_GlossMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half _Shininess;
        CBUFFER_END

        struct Attributes {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float4 tangentOS  : TANGENT;
            float2 uv         : TEXCOORD0;
            float4 color      : COLOR;
        };

        struct Varyings {
            float4 positionCS  : SV_POSITION;
            float2 uv          : TEXCOORD0;
            float4 color       : COLOR;
            float  fogFactor   : TEXCOORD1;
            float3 normalWS    : TEXCOORD2;
            float3 positionWS  : TEXCOORD3;
        };

        Varyings vert(Attributes IN) {
            Varyings OUT = (Varyings)0;
            VertexPositionInputs pi = GetVertexPositionInputs(IN.positionOS.xyz);
            VertexNormalInputs ni = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
            OUT.positionCS = pi.positionCS;
            OUT.positionWS = pi.positionWS;
            OUT.normalWS = ni.normalWS;
            OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
            OUT.color = IN.color;
            OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
            return OUT;
        }

        half4 frag(Varyings IN) : SV_Target {
            half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
            c.rgb *= IN.color.rgb * IN.color.a * _Color.rgb;

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
            sd.smoothness = _Shininess;
            sd.normalTS = half3(0, 0, 1);
            sd.occlusion = 1.0;
            sd.alpha = c.a;

            half4 color = UniversalFragmentPBR(inputData, sd);
            color.rgb = MixFog(color.rgb, IN.fogFactor);
            return color;
        }
        ENDHLSL
    }
}

Dependency "OptimizedShader" = "Hidden/Nature/Tree Creator Bark Optimized"
FallBack Off
}
