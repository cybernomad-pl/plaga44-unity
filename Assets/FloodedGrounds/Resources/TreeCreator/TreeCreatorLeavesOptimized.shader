Shader "Hidden/Nature/Tree Creator Leaves Optimized" {
Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _TranslucencyColor ("Translucency Color", Color) = (0.73,0.85,0.41,1)
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.3
    _MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
    _ShadowTex ("Shadow (RGB)", 2D) = "white" {}
    _BumpSpecMap ("Normalmap (GA) Spec (R)", 2D) = "bump" {}
    _TranslucencyMap ("Trans (B) Gloss(A)", 2D) = "white" {}
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
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        float4 _MainTex_ST;
        half4 _Color;
        half4 _TranslucencyColor;
        half _Cutoff;
        struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float3 normal : NORMAL; };
        struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float fogFactor : TEXCOORD1; };
        Varyings vert(Attributes IN) {
            Varyings OUT;
            OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
            OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
            OUT.color = IN.color;
            OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
            return OUT;
        }
        half4 frag(Varyings IN) : SV_Target {
            half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
            c.rgb *= IN.color.rgb * IN.color.a;
            c.a = saturate(c.a);
            clip(c.a - _Cutoff);
            c.rgb += _TranslucencyColor.rgb * 0.02;
            c.rgb = MixFog(c.rgb, IN.fogFactor);
            return c;
        }
        ENDHLSL
    }
    Pass {
        Name "ShadowCaster"
        Tags { "LightMode" = "ShadowCaster" }
        ZWrite On ZTest LEqual ColorMask 0
        HLSLPROGRAM
        #pragma vertex vertShadow
        #pragma fragment fragShadow
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_ShadowTex); SAMPLER(sampler_ShadowTex);
        float4 _ShadowTex_ST;
        half _Cutoff;
        struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
        struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
        Varyings vertShadow(Attributes IN) {
            Varyings OUT;
            OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
            OUT.uv = TRANSFORM_TEX(IN.uv, _ShadowTex);
            return OUT;
        }
        half4 fragShadow(Varyings IN) : SV_Target {
            half alpha = SAMPLE_TEXTURE2D(_ShadowTex, sampler_ShadowTex, IN.uv).r;
            clip(alpha - _Cutoff);
            return 0;
        }
        ENDHLSL
    }
}
Dependency "BillboardShader" = "Hidden/Nature/Tree Creator Leaves Rendertex"
}
