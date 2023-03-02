#define INTEGER_ACCESS 1
#include "imgKernelsPre.cginc"

#if CONVOLVE_1D

    #ifdef CONVOLVE_VERTICAL
        #define CONVOLVE_DIR int2(0,1)
    #else
        #define CONVOLVE_DIR int2(1,0)
    #endif

    float4 fn(uint2 p)
    {
        int N = IMG_B_SIZE().x;
        int HALF_N = N >> 1;
        float4 ret = 0.;
        for (int i = 0; i < N; i++)
        {
            float4 a = IMG_A(p + CONVOLVE_DIR * (HALF_N - i));
            float4 b = IMG_B(int2(i, 0));
            ret += a * b;
        }
        //ret.x = IMG_A_SIZE();
        //ret.y = IMG_B_SIZE();
        return ret;
    }

#else


    float4 fn(uint2 p)
    {
        float4 ret = 0.;

        int2 bsize = IMG_B_SIZE();
        int2 halfBsize = bsize >> 1;
        for (int y = 0; y < bsize.y; y++) {
            for (int x = 0; x < bsize.x; x++)
            {
                int2 bpos = int2(x, y);
                float4 a = IMG_A(p + (halfBsize - bpos));
                float4 b = IMG_B(bpos);
                ret += a * b;
            }
        }
        return ret;
    }


#endif

#include "imgKernels.cginc"
