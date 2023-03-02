Shader "teadrinker/DisplaceImagePostFX"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Displacement("Texture", 2D) = "white" {}

        _Width ("Width" ,  Float) = 100.0
        _Height("Height" , Float) = 200.0
        _Amount("Amount" , Range(0 ,1))  = 0.0
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
            sampler2D _Displacement;

            float _Width;
            float _Height;
            float _Amount;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            { 
//                int pixelpos = (((int)(2 < 0 ? i.uv.y * _Height : i.uv.x * _Width)) % (dist * 2));
                float4 displacement = tex2D(_Displacement, i.uv) * _Amount;
                return lerp(float4(0.,0.,0.,1.), tex2D(_MainTex, i.uv + displacement.xy), displacement.b > 0);
            }
            ENDCG
        }
    }
}
