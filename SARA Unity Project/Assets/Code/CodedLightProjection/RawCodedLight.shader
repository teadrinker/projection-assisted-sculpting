Shader "Unlit/RawCodedLight"
{
    Properties
    {
        _Width ("Width" ,  Float) = 100.0
        _Height("Height" , Float) = 200.0
        _Stage ("Stage" ,  Range(-16,16)) = 0.0
        _Amount("Amount" , Range(-1 ,1))  = 0.0
        _UseGrayCodes("UseGrayCodes" , Range(0 ,1))  = 0.0
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

            float _Width;
            float _Height;
            float _Stage;
            float _Amount;
            float _UseGrayCodes;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                int dist = 1 << ((int) abs(_Stage));
                float gray = 0.0;
                uint pixelIndex = (uint)(_Stage < 0 ? i.uv.y * _Height : i.uv.x * _Width);
                if (_UseGrayCodes > 0.5) {
                    int pixelpos = pixelIndex % (dist * 4);
                    if(pixelpos >= dist && pixelpos < dist * 3.)
                        gray = 1.0;
                }
                else {
                    int pixelpos = pixelIndex % (dist * 2);
                    if(pixelpos >= dist)
                        gray = 1.0;
                }
                gray = lerp(gray, 1.0 - gray, _Amount < 0);
                gray *= abs(_Amount);
                fixed4 col = float4(gray.xxx, 1.0);
                return col;
            }
            ENDCG
        }
    }
}
