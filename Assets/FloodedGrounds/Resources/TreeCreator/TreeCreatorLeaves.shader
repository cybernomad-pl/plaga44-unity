Shader "Nature/Tree Creator Leaves" {
Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _Shininess ("Shininess", Range (0.01, 1)) = 0.078125
    _MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
    _BumpMap ("Normalmap", 2D) = "bump" {}
    _GlossMap ("Gloss (A)", 2D) = "black" {}
    _TranslucencyMap ("Translucency (A)", 2D) = "white" {}
    _ShadowOffset ("Shadow Offset (A)", 2D) = "black" {}

    // These are here only to provide default values
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.3
    [HideInInspector] _TreeInstanceColor ("TreeInstanceColor", Vector) = (1,1,1,1)
    [HideInInspector] _TreeInstanceScale ("TreeInstanceScale", Vector) = (1,1,1,1)
    [HideInInspector] _SquashAmount ("Squash", Float) = 1
}

SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "RenderType"="TreeLeaf" }
    LOD 200
    Cull Off

    Pass {
        Name "ForwardLit"
        Tags { "LightMode" = "UniversalForward" }
        HLSLPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);
        TEXTURE2D(_TranslucencyMap); SAMPLER(sampler_TranslucencyMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half4 _TranslucencyColor;
            half _Shininess;
            half _Cutoff;
        CBUFFER_END

        struct Attributes {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
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
            VertexNormalInputs ni = GetVertexNormalInputs(IN.normalOS);
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
            clip(c.a - _Cutoff);

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

    Pass {
        Name "ShadowCaster"
        Tags { "LightMode" = "ShadowCaster" }
        ZWrite On ZTest LEqual ColorMask 0
        Cull Off
        HLSLPROGRAM
        #pragma vertex vertShadow
        #pragma fragment fragShadow
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        float4 _MainTex_ST;
        half _Cutoff;
        struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
        struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
        Varyings vertShadow(Attributes IN) {
            Varyings OUT;
            OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
            OUT.uv = IN.uv * _MainTex_ST.xy + _MainTex_ST.zw;
            return OUT;
        }
        half4 fragShadow(Varyings IN) : SV_Target {
            half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
            clip(alpha - _Cutoff);
            return 0;
        }
        ENDHLSL
    }
}

Dependency "OptimizedShader" = "Hidden/Nature/Tree Creator Leaves Optimized"
FallBack Off
}
