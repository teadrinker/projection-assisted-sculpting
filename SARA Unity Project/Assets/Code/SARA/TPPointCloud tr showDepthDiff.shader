
Shader "teadrinker/TPPointCloud tr showDepthDiff"
{
    Properties
    {
        _NearFar      ("NearFar", Vector) = (0.5, 3.0, 100.0, 0.01) 

        _MainTex("Texture", 2D) = "white" {}
        _ParticleTex1 ("-", 2D)    = ""{}
        _ParticleTex2 ("-", 2D)    = ""{}
        [HDR] _Color  ("-", Color) = (1, 1, 1, 1)
        _Size         ("Size", Vector) = (1, 0.5, 0, 0)
        _Tail         ("Tail", Float) = 1
        _TailFromSpeed("TailFromSpeed", Float) = 1
        _NearFar      ("NearFar", Vector) = (0.5, 3.0, 100.0, 0.01)

        _BaseGradientMul("BaseGradientMul", Range(0.0, 1.0)) = 0.5
        _CentimeterAmount("CentimeterAmount", Range(0.0, 1.0)) = 0.5
        _ColorMiddle  ("ColorMiddle", Color) = (1, 1, 1, 1)
        _ColorMiddleSize("_ColorMiddleSize", Range(0.0, 1.0)) = 0.5
        _ColorInside  ("ColorInside", Color) = (1, 1, 1, 1)
        _ColorOutside ("ColorOutside", Color) = (1, 1, 1, 1)

        _FeedbackColors("Texture", 2D) = "white" {}

        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("SrcBlend", Float) = 1.0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("DstBlend", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            //AlphaToMask On
            //Blend SrcAlpha OneMinusSrcAlpha
            Offset[_Offset] ,[_Offset]
            Blend[_SrcBlend][_DstBlend]
            ZTest Always
            ZWrite Off

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #define TPDROP_USE_QUAD 1            //            #pragma multi_compile __ TPDROP_USE_QUAD
            #pragma multi_compile __ USE_POINTCLOUD_LINES
            #pragma multi_compile __ TPDROP_SHARP
            #pragma multi_compile __ TPDROP_ADDITIVE
            #pragma multi_compile __ USE_ENCODE_SIZE_IN_DEPTH
            #pragma multi_compile __ TP_USE_GLOW
            #pragma multi_compile __ USE_MAINTEX_AS_COLOR_SOURCE

            #include "UnityCG.cginc"


            #if USE_POINTCLOUD_LINES
                #define USE_LINES 1
            #else
                #define USE_DIRECTIONLESS 1
            #endif
            #define USE_TRANSPARENT_PARTICLES 1
            #define USE_POINTCLOUD 1


            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            sampler2D _FeedbackColors;

            float _BaseGradientMul;
            float _CentimeterAmount;
            float4 _ColorMiddle;
            float _ColorMiddleSize;
            float4 _ColorInside;
            float4 _ColorOutside;


            // x = width
            // y = height
            // z = 1 + 1.0/width
            // w = 1 + 1.0/height
            //float4 _ScreenParams;

            //float4x4 unity_CameraInvProjection;

            float3 unproject(float2 screenPos, float depth) {
                //float4 clip = mul(unity_CameraProjection, viewPos);
                //float4 ndc = clip / clip.w;
                //float4 screenPos = (ndc.xy + 1.0) * (_ScreenParams.xy * 0.5)

                float2 ndc = screenPos.xy / (_ScreenParams.xy * 0.5) - 1.0;
                float4 clipPos = float4(ndc.xy * depth, depth, depth);
                float4 camPos = mul(unity_CameraInvProjection, clipPos);
                return camPos.xyz;
            }

            #define USE_PROJPOS 1
            #define USE_CUSTOM_COLOR_TRANSFORM 1
            float4 CustomColorTransform(float4 c, float4 in_vertex, float2 in_uv) {
                float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, in_uv));
                float particleZ = in_vertex.w;
                //return float4(frac(sceneZ*100.).xx, in_vertex.z*20., 1.0);
                
                //float diff = sceneZ - particleZ;
                 
                float diff = sign(sceneZ - particleZ) * length( unproject(in_vertex.xy, sceneZ) - unproject(in_vertex.xy, particleZ));
                
                //diff *= 0.5;
                //if (diff < 0.) return 0.;
                //return float4(frac(diff * 100.).xx, in_vertex.z*20., 1.0);
                
                float centimeter = diff * 150;

                //if (abs(centimeter) < 0.01) return 1.;

                float centimeterMarker = frac(-abs(centimeter));
                float4 col = tex2D(_FeedbackColors, float2(clamp(centimeter / 512 + 0.5, 0, 1.), 0.));
                col.rgb *= lerp(1.0, centimeterMarker, _CentimeterAmount);
                col.rgb = lerp(col.rgb, _ColorMiddle.rgb, _ColorMiddle.a * clamp((1.0 - abs(centimeter)/ _ColorMiddleSize), 0.0, 1.0));
                return col;

                float outside = max(0., diff * 100);
                float inside = max(0., -diff * 100);

                outside = lerp(outside * _BaseGradientMul, frac(outside), _CentimeterAmount);
                inside = lerp(inside * _BaseGradientMul, frac(inside), _CentimeterAmount);

                float3 newColor = diff < 0. ? _ColorInside.rgb * inside : _ColorOutside.rgb * outside;
                return c * float4(newColor, 0.005 / (0.01 + diff * diff));
                
                //return c * float4(inside, outside, 0.0, 0.005 / (0.01 + diff * diff));
            }

            #include "../TeaParticles/Resources/Shaders/TeaParticleShader.cginc"

            ENDCG
        }
    } 
}
