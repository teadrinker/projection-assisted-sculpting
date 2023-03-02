
Shader "teadrinker/TeaParticleUpdateMRT"
{
    Properties
    {
    }


	SubShader{
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			struct f2o
			{
				float4 col0 : COLOR0;
				float4 col1 : COLOR1;
			};

			v2f vert(appdata IN)
			{
				v2f OUT;
				OUT.vertex = UnityObjectToClipPos(IN.vertex);
				OUT.uv = IN.uv;
				return OUT;
			}

			sampler2D _MainTex;

			f2o frag(v2f IN) {
				f2o OUT;
				float3 pos = float3(IN.uv.x, IN.uv.y, 0.3 * dot(1., sin(IN.uv * 10. + _Time[1])));// float3(sin(_Time[1] + IN.uv.x), cos(_Time[1] + IN.uv.y), cos(_Time[1] * 2.0 + IN.uv.x * 2.0));
				OUT.col0 = float4(pos, 0.5);
				OUT.col1 = float4(pos + float3(0, 0.01, 0), 0.5);
				return OUT;
			}
			ENDCG
		}
	}


}
