

#if CHANNEL_MATRIX
	float4x4 _ChannelMatrix;
	float4 _ChannelMatrixOffset;
#endif

#if INTEGER_ACCESS 
	sampler2D _MainTex;
	float4 _MainTex_TexelSize;
	sampler2D _Tex1;
	float4 _Tex1_TexelSize;
	 
	int2 IMG_A_SIZE() { return uint2(_MainTex_TexelSize.zw + 0.5); }
	int2 IMG_B_SIZE() { /*return uint2(3,1);*/ return uint2(_Tex1_TexelSize.zw + 0.5); }
	float4 IMG_A(uint2 p) { return tex2Dlod(_MainTex,  float4((p + 0.5) * _MainTex_TexelSize.xy ,0,0)); }
	float4 IMG_B(uint2 p) { return tex2Dlod(_Tex1,	   float4((p + 0.5) * _Tex1_TexelSize.xy    ,0,0)); }
#endif