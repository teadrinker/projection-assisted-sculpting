Shader "teadrinker/Inpainting"
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

//#define DESP

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                if(all(col == 0))
                {
                    float tsx = _MainTex_TexelSize.x;
                    float tsy = _MainTex_TexelSize.x;

                    // closest 4 pixels (left, right, up, down)

                    float4 x1 = tex2D(_MainTex, i.uv + float2(-tsx, 0.0));
                    float4 x3 = tex2D(_MainTex, i.uv + float2( tsx, 0.0));
                    float4 y1 = tex2D(_MainTex, i.uv + float2(0.0, -tsy));
                    float4 y3 = tex2D(_MainTex, i.uv + float2(0.0,  tsy));

                    bool bx1 = any(x1);
                    bool bx3 = any(x3);
                    bool by1 = any(y1);
                    bool by3 = any(y3);

                    if (bx1 && bx3 && by1 && by3)
                        return (x1 + x3 + y1 + y3) * 0.25;
                    if (bx1 && bx3)
                        return (x1 + x3) * 0.5;
                    if (by1 && by3)
                        return (y1 + y3) * 0.5;

                    // closest 4 corners

                    float4 ul = tex2D(_MainTex, i.uv + float2(-tsx, -tsy));
                    float4 lr = tex2D(_MainTex, i.uv + float2( tsx,  tsy));
                    float4 ur = tex2D(_MainTex, i.uv + float2( tsx, -tsy));
                    float4 ll = tex2D(_MainTex, i.uv + float2(-tsx,  tsy));

                    bool bul = any(ul);
                    bool blr = any(lr);
                    bool bur = any(ur);
                    bool bll = any(ll);

                    if (bul && blr && bur && bll)
                        return (ul + lr + ur + ll) * 0.25;
                    if (bul && blr)
                        return (ul + lr) * 0.5;
                    if (bur && bll)
                        return (ur + ll) * 0.5;


                    // try 5x5 kernel here?


                    // no easy way to calc plausible gradient, estimate by average 2 closest

                    if (bx1 && by1)
                        return (x1 + y1) * 0.5;
                    if (bx1 && by3)
                        return (x1 + y3) * 0.5;
                    if (bx3 && by1)
                        return (x3 + y1) * 0.5;
                    if (bx3 && by3)
                        return (x3 + y3) * 0.5;

                    // use closest fallback

                    if (bx1)
                        return x1;
                    if (by1)
                        return y1;
                    if (bx3)
                        return x3;
                    if (by3)
                        return y3;

                }
                return col;
            }
            ENDCG
        }
    }
}
