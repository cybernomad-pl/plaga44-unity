Shader "Flooded_Grounds/Skybox_Rotating" {
Properties {
    _Tint ("Tint Color", Color) = (.5, .5, .5, .5)
    [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
    _Rotation ("Rotation", Range(0, 360)) = 0
    _RotSpeed ("Rotation Speed", Range(0, 360)) = 0
    _CloudBoost ("Cloud Brightness", Range(0, 5)) = 1.5
    _CloudThreshold ("Cloud Threshold", Range(0, 1)) = 0.3
    _GroundColor ("Ground Color", Color) = (0.18, 0.42, 0.08, 1)
    _GroundBlend ("Ground Blend Height", Range(-0.5, 0.5)) = 0.05
    _GroundFade ("Ground Fade Softness", Range(0.01, 1)) = 0.3
    [NoScaleOffset] _Tex ("Cubemap   (HDR)", Cube) = "grey" {}
}

SubShader {
    Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
    Cull Off ZWrite Off

    Pass {
        Name "Skybox"
        HLSLPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_instancing

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURECUBE(_Tex);
        SAMPLER(sampler_Tex);
        half4 _Tex_HDR;
        half4 _Tint;
        half _Exposure;
        float _Rotation, _RotSpeed;
        half _CloudBoost;
        half _CloudThreshold;
        half4 _GroundColor;
        half _GroundBlend;
        half _GroundFade;

        float4 RotateAroundYInDegrees (float4 vertex, float degrees)
        {
            float alpha = degrees * PI / 180.0;
            float sina, cosa;
            sincos(alpha, sina, cosa);
            float2x2 m = float2x2(cosa, -sina, sina, cosa);
            return float4(mul(m, vertex.xz), vertex.yw).xzyw;
        }

        struct Attributes {
            float4 positionOS : POSITION;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings {
            float4 positionCS : SV_POSITION;
            float3 texcoord : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings vert (Attributes v)
        {
            Varyings o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.positionCS = TransformObjectToHClip(RotateAroundYInDegrees(v.positionOS, _Rotation + (_Time.y * _RotSpeed)).xyz);
            o.texcoord = v.positionOS.xyz;
            return o;
        }

        // URP-compatible HDR decode (replaces built-in DecodeHDREnvironment)
        half3 DecodeHDRSkybox(half4 data, half4 hdr)
        {
            // hdr.x = multiplier, hdr.y = power (usually 1 for cubemaps)
            half alpha = hdr.y > 0 ? data.a : 1.0;
            return data.rgb * hdr.x * pow(abs(alpha), hdr.y);
        }

        half4 frag (Varyings i) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            half4 tex = SAMPLE_TEXTURECUBE(_Tex, sampler_Tex, i.texcoord);
            half3 c = DecodeHDRSkybox(tex, _Tex_HDR);
            c = c * _Tint.rgb * 2.0;
            c *= _Exposure;

            half lum = dot(c, half3(0.299, 0.587, 0.114));
            half cloudMask = saturate((lum - _CloudThreshold) / (1.0 - _CloudThreshold));
            c += c * cloudMask * (_CloudBoost - 1.0);

            // Zielony gradient od dolu -- blend z ground color ponizej horyzontu
            half viewY = normalize(i.texcoord).y;
            half groundMask = saturate((_GroundBlend - viewY) / _GroundFade);
            c = lerp(c, _GroundColor.rgb * _Exposure, groundMask);

            return half4(c, 1);
        }
        ENDHLSL
    }
}

Fallback Off
}
