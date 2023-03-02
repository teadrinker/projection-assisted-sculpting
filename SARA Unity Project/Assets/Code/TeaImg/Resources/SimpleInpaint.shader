Shader "teadrinker/SimpleInpaint"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
        SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            #define INTEGER_ACCESS 1
            #include "imgKernelsPre.cginc"


            fixed4 inpaintSimple(int2 pixel)
            {
                fixed4 col = IMG_A(pixel); // tex2D(_MainTex, i.uv);
                if(all(col == 0))
                {
                    float tsx = _MainTex_TexelSize.x;
                    float tsy = _MainTex_TexelSize.y;

                    // closest 4 pixels (left, right, up, down)
                    float4 x1 = IMG_A(pixel + int2(-1, 0)); // tex2D(_MainTex, i.uv + float2(-tsx, 0.0));
                    float4 x3 = IMG_A(pixel + int2( 1, 0)); // tex2D(_MainTex, i.uv + float2( tsx, 0.0));
                    float4 y1 = IMG_A(pixel + int2( 0,-1)); // tex2D(_MainTex, i.uv + float2(0.0, -tsy));
                    float4 y3 = IMG_A(pixel + int2( 0, 1)); // tex2D(_MainTex, i.uv + float2(0.0,  tsy));

                    bool bx1 = any(x1);
                    bool bx3 = any(x3);
                    bool by1 = any(y1);
                    bool by3 = any(y3);

                    float weight = 0.0;
                    float4 sum = float4(0.0, 0.0, 0.0, 0.0);

                    if (bx1) { weight++; sum += x1; }
                    if (bx3) { weight++; sum += x3; }
                    if (by1) { weight++; sum += y1; }
                    if (by3) { weight++; sum += y3; }

                    /*
                    // closest 4 corners
                    float4 ul = IMG_A(pixel + int2(-1,-1)); // tex2D(_MainTex, i.uv + float2(-tsx, -tsy));
                    float4 lr = IMG_A(pixel + int2( 1, 1)); // tex2D(_MainTex, i.uv + float2( tsx,  tsy));
                    float4 ur = IMG_A(pixel + int2( 1,-1)); // tex2D(_MainTex, i.uv + float2( tsx, -tsy));
                    float4 ll = IMG_A(pixel + int2(-1, 1)); // tex2D(_MainTex, i.uv + float2(-tsx,  tsy));

                    bool bul = any(ul);
                    bool blr = any(lr);
                    bool bur = any(ur);
                    bool bll = any(ll);

                    float cornerWeight = 0.7;

                    if (bul) { weight += cornerWeight; sum += ul * cornerWeight; }
                    if (blr) { weight += cornerWeight; sum += lr * cornerWeight; }
                    if (bur) { weight += cornerWeight; sum += ur * cornerWeight; }
                    if (bll) { weight += cornerWeight; sum += ll * cornerWeight; }

                    */

                    return weight == 0.0 ? sum : sum / weight;
                }
                return col;
            }



            float4 fn(uint2 p)
            {
                return inpaintSimple((int2)p);
            }

            #include "imgKernels.cginc"

            ENDCG
        }
    }
}
