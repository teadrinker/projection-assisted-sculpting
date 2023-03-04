//
// Description : Array and textureless GLSL 2D/3D/4D simplex 
//               noise functions.
//      Author : Ian McEwan, Ashima Arts.
//  Maintainer : stegu
//     Lastmod : 20110822 (ijm)
//     License : Copyright (C) 2011 Ashima Arts. All rights reserved.
//               Distributed under the MIT License. See LICENSE file.
//               https://github.com/ashima/webgl-noise
//               https://github.com/stegu/webgl-noise
// 

float4 mod289_n4D(float4 x) {
    return x - floor(x * (1.0 / 289.0)) * 289.0;
}

float mod289_n4D(float x) {
    return x - floor(x * (1.0 / 289.0)) * 289.0;
}

float4 permute_n4D(float4 x) {
    return mod289_n4D(((x * 34.0) + 1.0) * x);
}

float permute_n4D(float x) {
    return mod289_n4D(((x * 34.0) + 1.0) * x);
}

float4 taylorInvSqrt_n4D(float4 r)
{
    return 1.79284291400159 - 0.85373472095314 * r;
}

float taylorInvSqrt_n4D(float r)
{
    return 1.79284291400159 - 0.85373472095314 * r;
}

float4 grad4_n4D(float j, float4 ip)
{
    const float4 ones = float4(1.0, 1.0, 1.0, -1.0);
    float4 p, s;

    p.xyz = floor(frac(j * ip.xyz) * 7.0) * ip.z - 1.0;
    p.w = 1.5 - dot(abs(p.xyz), ones.xyz);
    s = float4(p < 0.0);
    p.xyz = p.xyz + (s.xyz * 2.0 - 1.0) * s.www;

    return p;
}

// (sqrt(5) - 1)/4 = F4, used once below
#define F4 0.309016994374947451

float snoise(float4 v)
{
    const float4  C = float4(0.138196601125011,  // (5 - sqrt(5))/20  G4
        0.276393202250021,  // 2 * G4
        0.414589803375032,  // 3 * G4
        -0.447213595499958); // -1 + 4 * G4

// First corner
    float4 i = floor(v + dot(v, F4));
    float4 x0 = v - i + dot(i, C.xxxx);

    // Other corners

    // Rank sorting originally contributed by Bill Licea-Kane, AMD (formerly ATI)
    float4 i0;
    float3 isX = step(x0.yzw, x0.xxx);
    float3 isYZ = step(x0.zww, x0.yyz);
    //  i0.x = dot( isX, float3( 1.0 ) );
    i0.x = isX.x + isX.y + isX.z;
    i0.yzw = 1.0 - isX;
    //  i0.y += dot( isYZ.xy, float2( 1.0 ) );
    i0.y += isYZ.x + isYZ.y;
    i0.zw += 1.0 - isYZ.xy;
    i0.z += isYZ.z;
    i0.w += 1.0 - isYZ.z;

    // i0 now contains the unique values 0,1,2,3 in each channel
    float4 i3 = clamp(i0, 0.0, 1.0);
    float4 i2 = clamp(i0 - 1.0, 0.0, 1.0);
    float4 i1 = clamp(i0 - 2.0, 0.0, 1.0);

    //  x0 = x0 - 0.0 + 0.0 * C.xxxx
    //  x1 = x0 - i1  + 1.0 * C.xxxx
    //  x2 = x0 - i2  + 2.0 * C.xxxx
    //  x3 = x0 - i3  + 3.0 * C.xxxx
    //  x4 = x0 - 1.0 + 4.0 * C.xxxx
    float4 x1 = x0 - i1 + C.xxxx;
    float4 x2 = x0 - i2 + C.yyyy;
    float4 x3 = x0 - i3 + C.zzzz;
    float4 x4 = x0 + C.wwww;

    // Permutations
    i = mod289_n4D(i);
    float j0 = permute_n4D(permute_n4D(permute_n4D(permute_n4D(i.w) + i.z) + i.y) + i.x);
    float4 j1 = permute_n4D(permute_n4D(permute_n4D(permute_n4D(
        i.w + float4(i1.w, i2.w, i3.w, 1.0))
        + i.z + float4(i1.z, i2.z, i3.z, 1.0))
        + i.y + float4(i1.y, i2.y, i3.y, 1.0))
        + i.x + float4(i1.x, i2.x, i3.x, 1.0));
     
    // Gradients: 7x7x6 points over a cube, mapped onto a 4-cross polytope
    // 7*7*6 = 294, which is close to the ring size 17*17 = 289.
    float4 ip = float4(1.0 / 294.0, 1.0 / 49.0, 1.0 / 7.0, 0.0);

    float4 p0 = grad4_n4D(j0, ip);
    float4 p1 = grad4_n4D(j1.x, ip);
    float4 p2 = grad4_n4D(j1.y, ip);
    float4 p3 = grad4_n4D(j1.z, ip);
    float4 p4 = grad4_n4D(j1.w, ip);

    // Normalise gradients
    float4 norm = taylorInvSqrt_n4D(float4(dot(p0, p0), dot(p1, p1), dot(p2, p2), dot(p3, p3)));
    p0 *= norm.x;
    p1 *= norm.y;
    p2 *= norm.z;
    p3 *= norm.w;
    p4 *= taylorInvSqrt_n4D(dot(p4, p4));

    // Mix contributions from the five corners
    float3 m0 = max(0.6 - float3(dot(x0, x0), dot(x1, x1), dot(x2, x2)), 0.0);
    float2 m1 = max(0.6 - float2(dot(x3, x3), dot(x4, x4)), 0.0);
    m0 = m0 * m0;
    m1 = m1 * m1;
    return 49.0 * (dot(m0 * m0, float3(dot(p0, x0), dot(p1, x1), dot(p2, x2)))
        + dot(m1 * m1, float2(dot(p3, x3), dot(p4, x4))));

}


/*

// more uint hash
// https://www.shadertoy.com/view/ttVGDV

int N = 1; // Number of iterations, N = 1000 for benchmark
//#define HASH wellons3
//#define HASH jenkins
//#define HASH murmur
//#define HASH wellons
#define HASH wellons3

//Wang
uint wang(uint a) {
	a = (a ^ 61U) ^ (a >> 16U);
	a = a * 9U;
	a = a ^ (a >> 4);
	a = a * 0x27d4eb2dU;
	a = a ^ (a >> 15);
	return a;
}

// Jenkins
uint jenkins(uint a) {
    a -= (a<<6);
    a ^= (a>>17);
    a -= (a<<9);
    a ^= (a<<4);
    a -= (a<<3);
    a ^= (a<<10);
    a ^= (a>>15);
    return a;
}

// MurmurHash3 finalizer
uint murmur(uint x) {
    x ^= x >> 16;
    x *= 0x85ebca6bU;
    x ^= x >> 13;
    x *= 0xc2b2ae35U;
    x ^= x >> 16;
    return x;
}

// Chris Wellons: https://nullprogram.com/blog/2018/07/31/
uint wellons(uint x) {
    x ^= x >> 16;
    x *= 0x7feb352dU;
    x ^= x >> 15;
    x *= 0x846ca68bU;
    x ^= x >> 16;
    return x;
}

// Chris Wellons 3-round function
// bias: 0.020888578919738908 = minimal theoretic limit
uint wellons3(uint x)
{
    x ^= x >> 17;
    x *= 0xed5ad4bbU;
    x ^= x >> 11;
    x *= 0xac4c1b51U;
    x ^= x >> 15;
    x *= 0x31848babU;
    x ^= x >> 14;
    return x;
}

float hashtest(uint a) {
    uint hash = a;
    for (int i = 0; i < N; i++) {
    	hash = HASH(hash);
    }
    return float(hash) / float(0xFFFFFFFFU); // Uniform in [0,1]
}

vec4 rand4(uint seed){
    return vec4(hashtest(seed^0x34F85A93U),
                hashtest(seed^0x85FB93D5U),
                hashtest(seed^0x6253DF84U),
                hashtest(seed^0x25FC3625U));
}

void mainImage( out vec4 O, vec2 U )
{
    vec2 uv = U-0.5*iResolution.xy;
    uint seed = uint(U.x) + (uint(U.y) << 11);
    // Extra mixing improves Wang hash
    if (iMouse.z > 0.0) seed *= 257U;
    O = rand4(seed);
}





*/
