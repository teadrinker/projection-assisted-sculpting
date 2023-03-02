Shader "teadrinker/FromGrayCode"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            uint GrayToBinary32(uint num)
            {
                num ^= num >> 16;
                num ^= num >> 8;
                num ^= num >> 4;
                num ^= num >> 2;
                num ^= num >> 1;
                return num;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                col.x = float(GrayToBinary32(uint(col.x + 0.5)));
                col.y = float(GrayToBinary32(uint(col.y + 0.5)));
                return col;
            }
            ENDCG
        }
    }
}
