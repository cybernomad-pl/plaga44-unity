Shader "Hidden/Nature/Tree Creator Bark Optimized" {
Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
    _BumpSpecMap ("Normalmap (GA) Spec (R)", 2D) = "bump" {}
    _TranslucencyMap ("Trans (RGB) Gloss(A)", 2D) = "white" {}
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
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        float4 _MainTex_ST;
        half4 _Color;
        struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
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
            c.rgb *= IN.color.rgb * IN.color.a * _Color.rgb;
            c.rgb = MixFog(c.rgb, IN.fogFactor);
            return c;
        }
        ENDHLSL
    }
}
Dependency "BillboardShader" = "Hidden/Nature/Tree Creator Bark Rendertex"
}
