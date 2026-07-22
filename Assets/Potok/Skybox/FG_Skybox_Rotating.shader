Shader "Flooded_Grounds/Skybox_Rotating" {
Properties {
    _Tint ("Tint Color", Color) = (.5, .5, .5, .5)
    [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
    _Rotation ("Rotation", Range(0, 360)) = 0
    _RotSpeed ("Rotation Speed", Range(0, 360)) = 0
    _GroundColor ("Ground Color", Color) = (0.18, 0.42, 0.08, 1)
    _GroundBlend ("Ground Blend Height", Range(-0.5, 0.5)) = 0.05
    _GroundFade ("Ground Fade Softness", Range(0.01, 1)) = 0.3
    _CloudOpacity ("Cloud Opacity", Range(0, 2)) = 1.0
    _CloudTint ("Cloud Tint", Color) = (1, 1, 1, 1)
    _FlipAmount ("Sky Flip Amount (altitude)", Range(0, 1)) = 0
    [NoScaleOffset] _Tex ("Sky Cubemap (HDR)", Cube) = "grey" {}
    _CloudTex ("Cloud Layer (RGBA)", 2D) = "black" {}
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

        TEXTURE2D(_CloudTex);
        SAMPLER(sampler_CloudTex);

        half4 _Tint;
        half _Exposure;
        float _Rotation, _RotSpeed;
        half4 _GroundColor;
        half _GroundBlend;
        half _GroundFade;
        half _CloudOpacity;
        half4 _CloudTint;
        half _FlipAmount;

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

        half3 DecodeHDRSkybox(half4 data, half4 hdr)
        {
            half alpha = hdr.y > 0 ? data.a : 1.0;
            return data.rgb * hdr.x * pow(abs(alpha), hdr.y);
        }

        // Overlay blend mode (Photoshop-style)
        half3 BlendOverlay(half3 base, half3 blend)
        {
            return lerp(
                2.0 * base * blend,
                1.0 - 2.0 * (1.0 - base) * (1.0 - blend),
                step(0.5, base)
            );
        }

        half4 frag (Varyings i) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

            // --- SKY LAYER (cubemap) ---
            half4 tex = SAMPLE_TEXTURECUBE(_Tex, sampler_Tex, i.texcoord);
            half3 sky = DecodeHDRSkybox(tex, _Tex_HDR);
            sky = sky * _Tint.rgb * 2.0;
            sky *= _Exposure;

            // --- CLOUD LAYER (2D texture, overlay blend) ---
            // Latlong UV z direction vector
            half3 dir = normalize(i.texcoord);

            // Wysokosciowe odwrocenie nieba: os Y patrzenia interpolowana z -Y.
            // _FlipAmount=0 -> normalnie; =1 -> gora<->dol zamienione.
            // Sterowane z C# (SkyFlipByAltitude) wg wysokosci gracza.
            half flipY = lerp(dir.y, -dir.y, _FlipAmount);

            half2 cloudUV;
            cloudUV.x = atan2(dir.z, dir.x) / (2.0 * PI) + 0.5;
            cloudUV.y = asin(flipY) / PI + 0.5;

            half4 cloudSample = SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, cloudUV);
            half3 cloudColor = cloudSample.rgb * _CloudTint.rgb;
            half cloudAlpha = cloudSample.a * _CloudOpacity;

            // Overlay blend -- chmury rozjaśniają jasne, przyciemniają ciemne
            half3 c = lerp(sky, BlendOverlay(sky, cloudColor), cloudAlpha);

            // --- GROUND gradient (tez odwracany wysokoscia) ---
            half viewY = flipY;
            half groundMask = saturate((_GroundBlend - viewY) / _GroundFade);
            c = lerp(c, _GroundColor.rgb * _Exposure, groundMask);

            return half4(c, 1);
        }
        ENDHLSL
    }
}

Fallback Off
}
