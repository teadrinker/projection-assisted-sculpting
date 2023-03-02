


// NOTE: THIS DOES NOT WORK!
// My CPU version of this algorithm was fine, but was not able to port it over to GPU...


Shader "teadrinker/InpaintMissing"
{
	Properties{
		_MainTex("Texture0", 2D) = "white" {}
		_Tex1("Texture1", 2D) = "white" {}

        _KernelRadius("KernelRadius", Float) = 35
        _SampleMargin("SampleMargin", Range(0.0, 5.0)) = 0.5
        _TresholdCombine("TresholdCombine", Range(0.0, 9.0)) = 0.0
        _WeightC("WeightC", Float) = 1.5
        _WeightCMarginCombine("WeightCMarginCombine", Float) = 1.5
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

            #pragma target 5.0
            #pragma exclude_renderers d3d11_9x
            #pragma exclude_renderers d3d9

            #define INTEGER_ACCESS 1
            #include "imgKernelsPre.cginc"
     
            float _KernelRadius;
            float _SampleMargin;
            float _TresholdCombine;
            float _WeightC;
            float _WeightCMarginCombine;
            float _OutlierFilterThres;

    float4 ACCESS(int src,int x,int y) { return IMG_A(uint2(x,y)); } 
    float4 MUL(float4 a, float4 b) { return a * b; }
    float4 ADD(float4 a, float4 b) { return a + b; }
    float DIFF(float4 a, float4 b) { return length(a.xy - b.xy); }
    bool IS_VALID(float4 a) { return !(a.x == 0.0 && a.y == 0.0); }

    #define SQRT  sqrt
    #define ZERO  0.0
    #define KernelSize _KernelRadius
    #define SampleMargin _SampleMargin
    #define TresholdCombine _TresholdCombine
    #define WeightC _WeightC
    #define WeightCMarginCombine _WeightCMarginCombine
    #define KernelDebugStart 0
    #define OverWriteInput false

    float4 kernelf(int x, int y) { int src = 0;
        //return ACCESS(src, x, y);
        //return float4(0.,0.,1.,1.);
        int kN = KernelSize; // full kernel size = kN * 2 + 1;
        int kN2 = KernelDebugStart; // full kernel size = kN * 2 + 1;
        int i, pl, cn, ax, ay, xx;
        uint yy, endy = 0;
        int end = 0;
        int foundN = 0;

        float xxx,yyy;
        float weight = 0;
        float weightSum = 0;
        float weightSumMax = 0;
        float symx, symy;
        float dirW = 0;
        float totalWeight = 0;

        float4 c, c2, c3, c_org;

        bool flip = false, found = false;

        c = c_org = ACCESS(src,x,y);
        bool isValid = IS_VALID(c);
        bool useAsOutlierFilter = _OutlierFilterThres > 0;
        if (!useAsOutlierFilter && !isValid || useAsOutlierFilter && isValid)
        { 
            c = ZERO;
            for (i = 0; i < 4; i++) // 4 pixel directions (use symmetry)
            {
                symx = i == 2 || i == 1 ? -1.0 : 1.0;
                symy = i == 3 || i == 1 ? -1.0 : 1.0;

                for (pl = 0; pl < 2; pl++) // side case (0), corner case (1)
                {
                    c3 = ZERO;
                    weightSum = 0.0;
                    found = false;
                    xxx = 0.0;
                    yyy = 0.0;
                    endy = kN;

                    [loop]
                    for (yy = kN2; yy < endy; yy++) // distance from center
                    {
                        flip = i>=2;

                        for (cn = 0; cn <= pl; cn++) // corner case needs 2 loops
                        {

                            if(pl == 0) {
                                end = floor(yy/2);
                                xx = -end;
                            } else if(cn == 0) {
                                end = yy;
                                xx = floor(yy/2)+1;
                            } else {
                                flip = !flip;
                                end = yy-1;
                                xx = floor(yy/2)+1;     
                            }
                             
                            [loop]
                            for (; xx <= end; xx++)
                            {
                                ax = (flip ? yy : xx) * symx;
                                ay = (flip ? xx : yy) * symy;
   
                                c2 = ACCESS(src, x + ax, y + ay);
                                if (IS_VALID(c2))
                                {
                                    if(!found) {
                                        found = true;
                                        endy = min(endy, yy * (1 + SampleMargin));
                                    }
                                    weight = pow(WeightCMarginCombine,-SQRT(ax * ax + ay* ay));
                                    xxx += ax * weight;
                                    yyy += ay * weight;
                                    c3 = ADD(c3, MUL(c2, weight));
                                    weightSum += weight;
                                }
                            }
                        }
                    }

                    if(found) {
                        xxx /= weightSum;
                        yyy /= weightSum;
                        c3 = MUL(c3, 1 / weightSum);

                        dirW = pow(WeightC,-SQRT(xxx * xxx + yyy * yyy));
                        c = ADD(c, MUL(c3, dirW));
                        totalWeight += dirW;

                        foundN++;
                    }

                }
            }
            if (!useAsOutlierFilter) {
                if(foundN < TresholdCombine)
                    c = ZERO;
                else
                    c = MUL(c, 1 / totalWeight);
            }
            else {
                if (DIFF(c, c_org) < _OutlierFilterThres)
                    c = c_org;
            }
        }

        return c;
    }


            float4 fn(uint2 p)
            {
                return kernelf(p.x, p.y);
            }

            #include "imgKernels.cginc"

            ENDCG
        }
    }
}
