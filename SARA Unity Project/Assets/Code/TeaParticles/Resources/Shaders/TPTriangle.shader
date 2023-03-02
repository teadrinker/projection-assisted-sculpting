
Shader "teadrinker/TPTriangle"
{
    Properties
    {
        _ParticleTex1 ("-", 2D)    = ""{}
        _ParticleTex2 ("-", 2D)    = ""{}
        [HDR] _Color  ("-", Color) = (1, 1, 1, 1)
        _Size         ("Size", Vector) = (1, 0.5, 0, 0)
        _Tail         ("Tail", Float) = 1
        _TailFromSpeed("TailFromSpeed", Float) = 1
    }

    SubShader
    {
        //Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            //AlphaToMask On
            //Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            //#define USE_DROP_SHAPE 1
            #include "TeaParticleShader.cginc"

            ENDCG
        }
    } 
}
