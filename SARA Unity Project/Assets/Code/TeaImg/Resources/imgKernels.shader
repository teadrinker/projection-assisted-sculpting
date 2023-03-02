Shader "teadrinker/imgKernels" {

	Properties{

		_MainTex("Texture0", 2D) = "white" {}
		_Tex1("Texture1", 2D) = "white" {}
		//_Noise("Noise", 2D) = "white" {}
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
			#define ARG_COUNT 1

			float4 fn(float4 a) { return                  -a; }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#define ARG_COUNT 1

			float4 fn(float4 a) { return                  abs(a); }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#define ARG_COUNT 1

			float4 fn(float4 a) { return                  sin(a); }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#define ARG_COUNT 1

			float4 fn(float4 a) { return                  cos(a); }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#define ARG_COUNT 1

			float4 fn(float4 a) { return                  floor(a); }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#define ARG_COUNT 1

			float4 fn(float4 a) { return                  frac(a); }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#define ARG_COUNT 1

			float4 fn(float4 a) { return                  a * a; }
			#include "imgKernels.cginc"
			ENDCG
		}
				 
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#define ARG_COUNT 1

			float4 fn(float4 a) { return                  a * a * a; }
			#include "imgKernels.cginc"
			ENDCG
		}




		Pass // absdiff
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B

			float4 op(float4 a, float4 b) { return        abs(a - b); }
			#include "imgKernels.cginc"
			ENDCG
		}	

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B

			float4 op(float4 a, float4 b) { return         a + b; }
			#include "imgKernels.cginc"
			ENDCG
		}
		
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B

			float4 op(float4 a, float4 b) { return         a - b; }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B

			float4 op(float4 a, float4 b) { return         a * b; }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B

			float4 op(float4 a, float4 b) { return         a / b; }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B

			float4 op(float4 a, float4 b) { return         a < b; }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B

			float4 op(float4 a, float4 b) { return         a <= b; }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B

			float4 op(float4 a, float4 b) { return         a > b; }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B

			float4 op(float4 a, float4 b) { return         a >= b; }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B

			float4 op(float4 a, float4 b) { return         a != 0.0 && b != 0.0; }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B

			float4 op(float4 a, float4 b) { return         a != 0.0 || b != 0.0; }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass // xor
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B

			float4 op(float4 a, float4 b) { return         !(a == b); }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass // nand
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B

			float4 op(float4 a, float4 b) { return         !(a != 0.0 && b != 0.0); }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass // nor
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B

			float4 op(float4 a, float4 b) { return         !(a != 0.0 || b != 0.0); }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass // xnor / logical equality
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B

			float4 op(float4 a, float4 b) { return         a == b; }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B

			float4 op(float4 a, float4 b) { return         a % b; }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B

			float4 op(float4 a, float4 b) { return         min(a, b); }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B

			float4 op(float4 a, float4 b) { return         max(a, b); }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B

			float4 op(float4 a, float4 b) { return         pow(a, b); }
			#include "imgKernels.cginc"
			ENDCG
		}
		




		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B
			#pragma multi_compile _ _USE_SCALAR_C
			#define ARG_COUNT 3

			float4 op(float4 a, float4 b, float4 c) { return     lerp(a, b, c); }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B
			#pragma multi_compile _ _USE_SCALAR_C
			#define ARG_COUNT 3

			float4 op(float4 a, float4 b, float4 c) { return     clamp(a, b, c); }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile _ _USE_SCALAR_B
			#pragma multi_compile _ _USE_SCALAR_C
			#define ARG_COUNT 3

			float4 op(float4 a, float4 b, float4 c) { return     a >= b && a < c; }
			#include "imgKernels.cginc"
			ENDCG
		}




		Pass // reduce_sum_iter
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#define REDUCE 1
			float4 reduce_fn(float4 a, float4 b) { return         a + b; }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass // reduce_mean_iter
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#define REDUCE 1
			float4 reduce_fn(float4 a, float4 b) { return         (a + b) * 0.5; } // useful if you want average using 8bit, but only correct for power of 2! 
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass // reduce_prod_iter
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#define REDUCE 1
			float4 reduce_fn(float4 a, float4 b) { return         a * b; }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass // reduce_min_iter
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#define REDUCE 1
			float4 reduce_fn(float4 a, float4 b) { return         min(a, b); }
			#include "imgKernels.cginc"
			ENDCG
		}

		Pass // reduce_max_iter
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#define REDUCE 1
			float4 reduce_fn(float4 a, float4 b) { return         max(a, b); }
			#include "imgKernels.cginc"
			ENDCG
		}





		Pass // channel_matrix
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#define CHANNEL_MATRIX 1
			#define ARG_COUNT 1

			#include "imgKernelsPre.cginc"
			float4 fn(float4 a) { return                  mul(_ChannelMatrix, a) + _ChannelMatrixOffset; }
			#include "imgKernels.cginc"
			ENDCG
		}




	}
}