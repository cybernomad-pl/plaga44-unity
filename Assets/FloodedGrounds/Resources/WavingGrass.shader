Shader "Hidden/TerrainEngine/Details/WavingDoublePass" {
Properties {
    _WavingTint ("Fade Color", Color) = (.7,.6,.5, 0)
    _MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
    _WaveAndDistance ("Wave and distance", Vector) = (12, 3.6, 1, 1)
    _Cutoff ("Cutoff", float) = 0.5
}
SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "IgnoreProjector"="True" "RenderType"="Grass" }
    Cull Off
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
        half4 _WavingTint;
        float4 _WaveAndDistance;
        half _Cutoff;
        struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
        struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float fogFactor : TEXCOORD1; };
        Varyings vert(Attributes IN) {
            Varyings OUT;
            float wave = sin(_Time.y * _WaveAndDistance.x + IN.positionOS.x * _WaveAndDistance.y) * IN.color.a * 0.1;
            float3 pos = IN.positionOS.xyz; pos.x += wave;
            OUT.positionCS = TransformObjectToHClip(pos);
            OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
            OUT.color = IN.color * _WavingTint;
            OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
            return OUT;
        }
        half4 frag(Varyings IN) : SV_Target {
            half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * (IN.color + 0.7);
            clip(c.a - _Cutoff);
            c.rgb = MixFog(c.rgb, IN.fogFactor);
            return c;
        }
        ENDHLSL
    }
}
Fallback Off
}
