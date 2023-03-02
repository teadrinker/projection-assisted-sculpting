
	#include "UnityCG.cginc"

#if !INTEGER_ACCESS 

	sampler2D _MainTex;
	float4 _MainTex_TexelSize;
	sampler2D _Tex1;
	float4 _Tex1_TexelSize;

#endif

	sampler2D _Tex2;

	//sampler2D _Noise;
	//float4 _NoiseScaleAndOffset;

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

	v2f vert(appdata v)
	{
		v2f o;
		o.vertex = UnityObjectToClipPos(v.vertex);
		o.uv = v.uv;
		return o;
	}

	float4 _OutputTexelSize; 
#if REDUCE

	float4 SamplePixelPos(float2 pp) {
		return tex2D(_MainTex, (pp + 0.5) * _MainTex_TexelSize.xy);  // https://answers.unity.com/questions/1498163/can-i-sample-a-texture-in-a-fragment-shader-using.html
	}

#endif

#if _USE_SCALAR_B
	float4 _Scalar;
#endif

#if _USE_SCALAR_C
	float4 _Scalar2;
#endif

	fixed4 frag(v2f i) : SV_Target
	{

#if INTEGER_ACCESS 

		int2 pos = uint2((i.uv - _MainTex_TexelSize.xy * 0.5) * _MainTex_TexelSize.zw + 0.5);
		return fn(pos);

#elif REDUCE

		float2 pixelPosDst = (i.uv - _OutputTexelSize.xy * 0.5) * _OutputTexelSize.zw;
		float2 pixelPos0 = pixelPosDst * 2.0;
		float2 pixelPos1 = pixelPosDst * 2.0 + float2(1., 0.);
		float2 pixelPos2 = pixelPosDst * 2.0 + float2(0., 1.);
		float2 pixelPos3 = pixelPosDst * 2.0 + float2(1., 1.);
		//return float4(pixelPos3, _MainTex_TexelSize.zw);
		bool2 hasPixelData = pixelPos3 < _MainTex_TexelSize.zw;
		if (all(hasPixelData))
		{
			return reduce_fn(
						reduce_fn(
							SamplePixelPos(pixelPos0),
							SamplePixelPos(pixelPos1)
						), 
						reduce_fn(
							SamplePixelPos(pixelPos2),
							SamplePixelPos(pixelPos3)
						)
					);
		}
		else if (hasPixelData.x)
		{
			return reduce_fn(
				SamplePixelPos(pixelPos0),
				SamplePixelPos(pixelPos1)
			);
		}
		else if (hasPixelData.y)
		{
			return reduce_fn(
				SamplePixelPos(pixelPos0),
				SamplePixelPos(pixelPos2)
			);
		}

		return SamplePixelPos(pixelPos0);
		//return reduce_bypass(SamplePixelPos(pixelPos0));

#else

		fixed4 col1 = tex2D(_MainTex, i.uv);

	#if ARG_COUNT == 1

		return fn(col1);

	#elif ARG_COUNT == 3

		#if _USE_SCALAR_B

			fixed4 col2 = _Scalar;

		#else

			fixed4 col2 = tex2D(_Tex1, i.uv);

		#endif

		#if _USE_SCALAR_C

			fixed4 col3 = _Scalar2;

		#else

			fixed4 col3 = tex2D(_Tex2, i.uv);

		#endif

		return op(col1, col2, col3);

	#else

		#if _USE_SCALAR_B

			fixed4 col2 = _Scalar;

		#else

			fixed4 col2 = tex2D(_Tex1, i.uv);

		#endif

		return op(col1, col2);

	#endif

#endif

	}


