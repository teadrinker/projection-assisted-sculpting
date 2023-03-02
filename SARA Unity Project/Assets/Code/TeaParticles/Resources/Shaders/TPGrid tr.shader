
Shader "teadrinker/TPGrid tr"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _ParticleTex1 ("-", 2D)    = ""{}
        _ParticleTex2 ("-", 2D)    = ""{}
        [HDR] _Color  ("-", Color) = (1, 1, 1, 1)
        _Size         ("Size", Vector) = (1, 0.5, 0, 0)
        _Tail         ("Tail", Float) = 1
        _TailFromSpeed("TailFromSpeed", Float) = 1
        _NearFar      ("NearFar", Vector) = (0.5, 3.0, 100.0, 0.01) 

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
            #pragma multi_compile __ TP_USE_BOXCUT

            #include "UnityCG.cginc"

            #if USE_POINTCLOUD_LINES
                #define USE_LINES 1
            #else
                #define USE_DIRECTIONLESS 1
            #endif
            #define USE_TRANSPARENT_PARTICLES 1
            #define USE_POINTCLOUD 1
            #include "TeaParticleShader.cginc"

            ENDCG
        }
    } 
}
