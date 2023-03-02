Shader "teadrinker/RealtimeUVProjection"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_Color ("Color", COLOR) = (1,1,1,1)
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			#include "ClassicNoise4D.cginc"
			//#include "noise4D.cginc"
			#include "ClassicNoise3D.cginc"
			  

			struct appdata
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float3 normal : NORMAL;
				float3 worldPos : TEXCOORD1;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 _Color;
	        uniform float4x4 _UVProjMatrix;	
			float3 _ProjectorPos;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.worldPos = mul(unity_ObjectToWorld, float4(v.vertex.x, v.vertex.y, v.vertex.z, 1.0)).xyz;			
				o.normal = UnityObjectToWorldNormal(v.normal);
				return o;
			}
			



			float erfi(float x)
			{
				float w, p;
				w = -log((1.0 - x) * (1.0 + x));
				if (w < 5.000000) {
					w = w - 2.500000;
					p = 2.81022636e-08;
					p = 3.43273939e-07 + p * w;
					p = -3.5233877e-06 + p * w;
					p = -4.39150654e-06 + p * w;
					p = 0.00021858087 + p * w;
					p = -0.00125372503 + p * w;
					p = -0.00417768164 + p * w;
					p = 0.246640727 + p * w;
					p = 1.50140941 + p * w;
				}
				else {
					w = sqrt(w) - 3.000000;
					p = -0.000200214257;
					p = 0.000100950558 + p * w;
					p = 0.00134934322 + p * w;
					p = -0.00367342844 + p * w;
					p = 0.00573950773+ p * w;
					p = -0.0076224613 + p * w;
					p = 0.00943887047 + p * w;
					p = 1.00167406 + p * w;
					p = 2.83297682 + p * w;
				}
				return p * x;
			}

			float toNormalDistribution(float x) {
				return erfi(x * 2.0 - 1.0);
			}


			fixed4 frag (v2f i) : SV_Target
			{
				float4 frustumPos = mul(_UVProjMatrix, float4(i.worldPos, 1.0) );
				float2 uv = frustumPos.xy;
				uv /= frustumPos.w;
				//uv.y *= -1.0;
				uv = uv.xy*0.5 + 0.5;				

				fixed4 col = 0.; 
				if(uv.x > 0. && uv.y > 0. && uv.x < 1. && uv.y < 1.)
					col = tex2D(_MainTex, uv);

				col.rgb *= max(0., dot(normalize(_ProjectorPos.xyz - i.worldPos), i.normal));
				float noise = (cnoise(i.worldPos * 20.) + 0.5) * 0.1 + (cnoise(i.worldPos * 7.) + 0.5) * 0.16 + (cnoise(i.worldPos * 2.) + 0.5) * 0.25;

				float triangleTime = abs(frac(_Time[1]) - 0.5);
				//float animNoise = (cnoise(float4(i.vertex.xyx*0.3, triangleTime * 200 )) + 1.0) * 0.5;
				float animNoise = (cnoise(float4(i.worldPos * 140 + triangleTime, triangleTime * 100 )) + 1.0) * 0.5;
				animNoise = erfi(min(animNoise, 0.99));
				animNoise *= 0.5;

				return col * _Color     + 0.2 * noise    + animNoise * 0.4;  
			}

			ENDCG 
		}
	}
}
