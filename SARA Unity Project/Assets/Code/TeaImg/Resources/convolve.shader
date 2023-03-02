Shader "teadrinker/convolve"
{
	Properties{
		_MainTex("Texture0", 2D) = "white" {}
		_Tex1("Texture1", 2D) = "white" {}
	}
	SubShader 
	{
		Cull Off
		ZTest Always
		ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #define CONVOLVE_1D 1
            #include "convolve.cginc"
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #define CONVOLVE_1D 1
			#define CONVOLVE_VERTICAL 1
            #include "convolve.cginc"
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "convolve.cginc"
            ENDCG
        }
    }
}
