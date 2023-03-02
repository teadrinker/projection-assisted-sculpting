//
//  TeaParticles, by Martin Eklund 2021
//
//       License: GNU GPL v3, https://www.gnu.org/licenses/gpl-3.0.en.html
//       For commercial use, contact music@teadrinker.net
//  


float sq(float x) { return x * x; }
// PRNG function.
float nrand(float2 uv, float salt)
{
    uv += float2(salt, -salt);
    return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
}

//#if USE_MAINTEX_AS_COLOR_SOURCE    // unity editor get confused if this is sometimes removed
    sampler2D _MainTex;
//#endif


#if USE_POINTCLOUD

    #define USE_COLOR_AMOUNT 1

   // #if USE_POINTCLOUD_STRUCTURED_BUF

    #if USE_POINTCLOUD_TEXTURESOURCE

    #else
        #if !USE_ENCODE_SIZE_IN_DEPTH
            StructuredBuffer<uint> _InfraredMap;
        #endif
            StructuredBuffer<uint> _DepthMap;
            StructuredBuffer<float> _DepthCameraCalibration;

            float4 _DepthMapTexelSize;
    #endif

  //  #else
  //      sampler2D _DepthAndColTex;
  //      sampler2D _DepthCameraCalibration;

  //      float3 getPointCloudPosFromUV(float2 uv) {

  //          float4 depthAndCol = tex2Dlod(_DepthAndColTex, float4(uv, 0, 0)) * 255.0;
  //          float4 calibration = tex2Dlod(_DepthCameraCalibration, float4(uv, 0, 0));
  //          float depth = (depthAndCol.x + depthAndCol.y * 256.0) * 0.001;
  //          return float3(calibration.x * depth, calibration.y * depth, depth);
  //      }

 //   #endif

    //float2 _SpaceScale;
    float4 _SecondLinePointOffset;


    #if USE_MAINTEX_AS_COLOR_SOURCE
        #if USE_COLOR_AMOUNT
            float _ColorAmount;
        #endif
    #endif

    float _InfraToColor;
    float _InfraToSize;
    float _ReduceStray;
    float4 _InputTransforms;
    float4 _DepthTransform;
    float4 _DepthToSize;

    float4 _GhostFrames;


    float3 getSpacePos(uint idx, float fDepth)
    {
        float scale = 1.;
        uint di = idx * 2;
        //#ifdef SHADER_API_D3D11
        //float3 spacePos = float3(_SpaceTable[di] * fDepth * _SpaceScale.x, _SpaceTable[di + 1] * fDepth * _SpaceScale.y, fDepth);
        float3 spacePos = float3(_DepthCameraCalibration[di] * fDepth * scale, _DepthCameraCalibration[di + 1] * fDepth * scale, fDepth);
        //#else
        //	float4 spacePos = float4(0, 0, 0, 1.0);
        //#endif

        return spacePos;
    }
     
    uint getUShort(uint i, StructuredBuffer<uint> buf)
    {
        uint uintval = buf[i >> 1];
        return i & 1 != 0 ? uintval >> 16 : uintval & 0xffff;
    }

    float3 getPointCloudPos(uint i, uint stride) {
        uint depth = getUShort(i, _DepthMap);
        float fDepth = (float)depth * 0.001;
        return getSpacePos(i, fDepth); 
    }

    float linstep(float a, float b, float x) {
        return clamp((x - a) / (b - a), 0.0, 1.0);
    }

    float reduceAndSum(float2 v) { return v.x + v.y; }
    float reduceAndSum(float3 v) { return v.x + v.y + v.z; }
    float reduceAndSum(float4 v) { return v.x + v.y + v.z + v.w; }

    #define DepthBits 11

    float3 getPointCloudPosFromUV(float2 uv, out float sizeFromPointCloud) {

        float2 uvInt = uv;
        uv = frac(uv);
        uvInt -= uv;
        float meshIndex = uvInt.x;
        float ghostFrameIndex = meshIndex;// floor(frac(meshIndex * _GhostFrames.x + _GhostFrames.y) * _GhostFrames.z);
        uint ghostFrameStride = ghostFrameIndex * ((uint)_GhostFrames.w);


       // uv = clamp(uv, _DepthMapTexelSize.xy*5., 1. - _DepthMapTexelSize.xy*5.);
        uint dx = (uint) (uv.x * _DepthMapTexelSize.z + 0.5);
        uint dy = (uint) (uv.y * _DepthMapTexelSize.w + 0.5);
        uint stride = (uint) (_DepthMapTexelSize.z + 0.5);
        uint i = dx + dy * stride;
        uint ifra = i + ghostFrameStride;
        uint udepth = getUShort(ifra, _DepthMap);

#if USE_ENCODE_SIZE_IN_DEPTH

        const uint IntRangeInMM = (1 << DepthBits) - 1;
        const float OOIntRangeInMM = 1.0 / ((float) IntRangeInMM);
        float normDepth = (udepth & IntRangeInMM) * OOIntRangeInMM;
        float depth = normDepth * _DepthTransform.x + _DepthTransform.y + reduceAndSum(_DepthTransform.zw * (uv - 0.5));
        sizeFromPointCloud = (udepth >> DepthBits) * ((1. / 31.) * 3.);

#else

        float depth = udepth * 0.001;
        float normDepth = (depth - _DepthTransform.y - reduceAndSum(_DepthTransform.zw * (uv - 0.5))) / _DepthTransform.x;

        float reduceStray = 1.;
        float infraRed = getUShort(i, _InfraredMap) * _InputTransforms.x + _InputTransforms.y;
        infraRed = clamp(infraRed, 0., 1.);
    #define USE_POINTCLOUD_REDUCE_STRAY 1
    #if USE_POINTCLOUD_REDUCE_STRAY
        float4 dd = 0.;
        dd.x = getUShort(ifra +1, _DepthMap) * 0.001;
        dd.y = getUShort(ifra -1, _DepthMap) * 0.001;
        dd.z = getUShort(ifra +stride, _DepthMap) * 0.001;
        dd.w = getUShort(ifra -stride, _DepthMap) * 0.001;
        //dd = max(0., abs(dd - depth)-0.001);
        dd = abs(dd - depth);
        float maxDiff = max(max(dd.x, dd.y), max(dd.z, dd.w));
        maxDiff *= 250. * 1.0 / depth;;
        //maxDiff *= 250.;
        reduceStray = linstep(0., 4.0 , 4.0 - maxDiff);
        //reduceStray = clamp(reduceStray, 0., 1.);
    #endif

        sizeFromPointCloud = 1.0;
        sizeFromPointCloud *= lerp(1., infraRed * 3., _InfraToSize);
        sizeFromPointCloud *= lerp(1., reduceStray, _ReduceStray);

#endif
        // DepthToSize
        normDepth = lerp(normDepth, smoothstep(0., 1., normDepth), _DepthToSize.w); // apply curve
        sizeFromPointCloud *= lerp(1, saturate((1. - normDepth) * _DepthToSize.z), _DepthToSize.x);

        //float trailN;
        //float trailWriteID;
        //if(trailWriteID != ghostFrameIndex)
           //sizeFromPointCloud *= frac(  (trailN - (trailWriteID) - _FrameFraction) / trailN + ghostFrameIndex/ trailN);
           //sizeFromPointCloud *= frac((trailN - (trailWriteID - ghostFrameIndex) - _FrameFraction) / trailN);

        float trailWriteID = _GhostFrames.z;
        if(trailWriteID != ghostFrameIndex)
           sizeFromPointCloud *= frac(_GhostFrames.x + ghostFrameIndex * _GhostFrames.y);
     //   else
     //       sizeFromPointCloud *= frac(-_GhostFrames.x);

        //if (-(uv.y-0.5)*2.0 + ((depth-2.6)/2.0) > 0.5) //combo_
        //    sizeFromPointCloud = 0.;

        return getSpacePos(i, depth);



        /*
        uint idx = (dx + dy * stride);

        uint depth2 = _DepthMap[idx >> 1];
        uint depth = idx & 1 != 0 ? depth2 >> 16 : depth2 & 0xffff;

        float fDepth = (float)depth * 0.001;
        float scale = 10.;
        return float3(uv * scale, fDepth);*/
    }



#endif



struct appdata
{
    float4 position : POSITION;
    float2 texcoord : TEXCOORD0;
};
 
struct v2f
{
    float4 position : SV_POSITION;

#if USE_MAINTEX_AS_COLOR_SOURCE
    nointerpolation float4 color : COLOR;
#endif

#if TPDROP_USE_QUAD
    float4 uv : TEXCOORD0;
#else
    float3 uv : TEXCOORD0;
#endif

#if USE_POINTCLOUD_LINES
    float uv2 : TEXCOORD1;
#endif

#if USE_PROJPOS
    float4 projPos : TEXCOORD2;
#endif

};

sampler2D _ParticleTex1;
float4 _ParticleTex1_TexelSize;

sampler2D _ParticleTex2;

half4 _Color;
#if !USE_DIRECTIONLESS
float _Tail;
float _TailFromSpeed;
#endif
float4 _Size;
float4 _NearFar;
float _AntiAlias;
float _Glow;





void AdjustSize(inout float size, float distToCam) 
{
    size *= 0.01; // in meter

    // particles that are too close to the camera are uncomfortable in VR
    size *= clamp((distToCam - _NearFar.x) * _NearFar.y, 0., 1.);

    // "fog", reduce size by distance (will also cull particles later)
    float fog = clamp((_NearFar.z - distToCam) * _NearFar.w, 0., 1.);
    fog *= fog; // use squared to get a bit of a more natural feel
    size *= fog;
}

#if TP_USE_BOXCUT

    float4x4 _BoxCutMatrix;
    float4 _BoxCutGradient;

    void BoxCut(inout float size, float3 posObjSpace) 
    {
        // _BoxCutMatrix is in worldspace 
        //float3 wpos = mul(unity_ObjectToWorld, float4(posObjSpace, 1.0)).xyz;
        //float3 secCut = mul(_BoxCutMatrix, float4((wpos).xyz, 1.0)).xyz;

        // _BoxCutMatrix is in objectspace 
        float3 secCut = mul(_BoxCutMatrix, float4(posObjSpace, 1.0)).xyz;

        float3 secCutOffset = -(abs(secCut) - float3(1.0, 1.0, 1.0));

        secCutOffset = clamp(secCutOffset * _BoxCutGradient, 0.0, 1.0);

        size *= secCutOffset.x * secCutOffset.y * secCutOffset.z;

    }

#else

    #define BoxCut(size, posObjSpace)

#endif



bool GetAntialiasApha(float inSize, float distToCam, out float aaSize, out float aaAlpha) {

    aaSize = inSize;
    aaAlpha = 1.0;
    //float size_div_dist = 200.0 * inSize / distToCam; // good for VR
    float size_div_dist = UNITY_MATRIX_P[0].x * _AntiAlias * inSize / distToCam;

#if USE_TRANSPARENT_PARTICLES

    if (size_div_dist < 1) {
        // below a certain size, enlarge and adjust alpha
        // (essentially anti-alias)
        aaAlpha *= size_div_dist;

        aaAlpha *= aaAlpha; // more perceptually linear fade out (assuming additive or bright against dark alpha)       NOTE: this is bad for dark particles!

        // skip drawing of very small particles
        if (aaAlpha < 0.0009) {
            return false;
        }

        aaSize /= size_div_dist;
    }

#else
    if (size_div_dist < 0.08) {
        return false;
    }
#endif

    return true;
}




v2f vert(appdata v)
{
    v2f o;

    o.position = float4(0., 0., 0., 0.);

    #if TPDROP_USE_QUAD
        o.uv = float4(0., 0., 0., 0.);
    #else
        o.uv = float3(0., 0., 0.);
    #endif

    #if USE_PROJPOS
        o.projPos = float4(0., 0., 0., 0.);
    #endif


    float2 uv = v.texcoord.xy;

#if USE_POINTCLOUD
    //float infraRed = 1.;
    //float reduceStray = 1.;
    float sizeFromPointCloud;
    float3 pos = getPointCloudPosFromUV(uv, sizeFromPointCloud);
#else
    uv += _ParticleTex1_TexelSize.xy / 2; // needed?
    float4 particlePosAndLife = tex2Dlod(_ParticleTex2, float4(uv, 0, 0));
    float3 pos = particlePosAndLife.xyz;
#endif 

#if USE_MAINTEX_AS_COLOR_SOURCE
    float4 color = tex2Dlod(_MainTex, float4(uv, 0, 0));
    //color.rgb = lerp(color.rgb, infraRed, _InfraToColor);
    #if USE_COLOR_AMOUNT
        color = lerp(1.0, color, _ColorAmount);
    #endif
    o.color = color;
#endif


#if USE_POINTCLOUD_LINES
        float zPos = v.position.z;
        //float infraRed2;
        //float reduceStray2;
        float sizeFromPointCloud2;
        float3 pos2 = getPointCloudPosFromUV(uv + _SecondLinePointOffset.xy * float2(1.0 - zPos, zPos), sizeFromPointCloud2);

#elif !USE_DIRECTIONLESS

        float4 particlePosAndLifePrev = tex2Dlod(_ParticleTex1, float4(uv, 0, 0));

        if (particlePosAndLifePrev.w < 0)
            return o;
#endif



    {
        float3 camPosInObjSpace = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos.xyz, 1.));
        
        float2 grad = v.position.xy * 100.; // vert pos normalization use 0.01, to avoid unity editor freeze due to overdraw when dealing with a lot of particles

        float size = _Size.x;

#if USE_POINTCLOUD

        size *= sizeFromPointCloud;
        //size *= lerp(1., infraRed * 3., _InfraToSize);
        //size *= lerp(1., reduceStray, _ReduceStray);

    #if USE_POINTCLOUD_LINES

        float size2 = _Size.x;
        size2 *= sizeFromPointCloud2;

        //size2 *= lerp(1., infraRed2 * 3., _InfraToSize);
        //size2 *= lerp(1., reduceStray2, _ReduceStray);

    #endif
#else
        size *= lerp(1., nrand(uv, 2.8), _Size.y); // size variation per particle
        size *= 1. - (2. * abs(0.5 - particlePosAndLife.w)); // fade in and out during lifetime
#endif


        /*
        float size = _Size.x;
        
#if !USE_POINTCLOUD
        size *= lerp(1., nrand(uv, 2.8), _Size.y); // size variation per particle
        size *= 1. - (2. * abs(0.5 - p2.w)); // fade in and out during lifetime
#endif

        size *= 0.01; // in meter

#if USE_POINTCLOUD
        size *= lerp(1., infraRed * 3., _InfraToSize);
        size *= lerp(1., reduceStray, _ReduceStray);
#endif

        float3 pos = p2.xyz;

        float3 camPosInObjSpace = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos.xyz, 1.));
        float3 diffToCam = pos - camPosInObjSpace;
        float  distToCam = length(diffToCam);

        // particles that are too close to the camera are uncomfortable in VR
        size *= clamp((distToCam - _NearFar.x) * _NearFar.y, 0., 1.); 

        // "fog", reduce size by distance (will also cull particles later)
        float fog = clamp((_NearFar.z - distToCam) * _NearFar.w, 0., 1.);
        fog *= fog; // use squared to get a bit of a more natural feel
        size *= fog;
        */


        float  distToCam = length(pos - camPosInObjSpace);
        AdjustSize(size, distToCam);
        BoxCut(size, pos);


#if USE_POINTCLOUD_LINES

        float  distToCam2 = length(pos2 - camPosInObjSpace);
        AdjustSize(size2, distToCam2);
        BoxCut(size2, pos2);

        float alphaMul = 1.0;
        float aaSize = 0.0;
        bool skip1 = !GetAntialiasApha(size, distToCam, aaSize, alphaMul);

        float alphaMul2 = 0.0;
        float aaSize2 = 0.0;
        bool skip2 = !GetAntialiasApha(size2, distToCam2, aaSize2, alphaMul2);

        bool skip = skip1 && skip2;


        // remove long lines 
        float3 posDiff = pos - pos2;
        float lineLen = length(posDiff);

        const float thres = 0.03;
        //size *= 1. - min(1.0, lineLen * (1. / thres));   // (fade-out by length)
        if (lineLen > 0.05)
            skip = true;

        if(skip)
            return o;


        /*
        // combine/normalize size of the two points
        float size1 = size;
        size = max(size1, size2);
        size1 /= size;
        size2 /= size;
        size1 = max(size1, 0.1);
        size2 = max(size2, 0.1);
        float distToCam1 = distToCam;
        distToCam = ((distToCam + distToCam2) * 0.5);


        // remove long lines 
        float3 posDiff = pos - pos2;
        float lineLen = length(posDiff);

        const float thres = 0.03;
        //size *= 1. - min(1.0, lineLen * (1. / thres));   // (fade-out by length)
        if (lineLen > 0.05)
            size = 0;

        float alphaMul = 1.0;
        float aaSize = 0.0;
        if (!GetAntialiasApha(size, distToCam, aaSize, alphaMul)) {
            return o;
        }
        
        */
#else

        float alphaMul = 1.0;
        float aaSize = 0.0;
        if (!GetAntialiasApha(size, distToCam, aaSize, alphaMul))
            return o; 

#endif


#if !USE_POINTCLOUD_LINES
        float3 view_forward = (pos - camPosInObjSpace) / distToCam;
#endif



#if USE_POINTCLOUD_LINES
        float tangentPosN = grad.x;
        float isTail = grad.y;
        
        if(aaSize > aaSize2)
            aaSize2 = max(aaSize2, aaSize * 0.1);
        else
            aaSize = max(aaSize, aaSize2 * 0.1);
        
        float radius1 = aaSize * 0.5;
        float radius2 = aaSize2 * 0.5;
        float radius  = lerp(radius2, radius1, isTail);

        float distToCam1 = distToCam;

        alphaMul  = lerp(alphaMul2,  alphaMul,    isTail);
        pos       = lerp(pos2,       pos,         isTail);
        distToCam = lerp(distToCam2, distToCam,   isTail);

        float3 view_forward = (pos - camPosInObjSpace) / distToCam;

        float3 diffDir = posDiff / lineLen;

        float tangentPos = tangentPosN * radius;

        float3 forward_component = view_forward * dot(view_forward, diffDir);
        float3 view_component = diffDir - forward_component;
        float view_component_len = length(view_component);
        float3 diffDir2D = view_component / view_component_len;
        float3 tangentDir = normalize(cross(diffDir2D, view_forward));

        pos += tangentDir * tangentPos + diffDir2D * lerp(-radius, radius, isTail);

        o.position = UnityObjectToClipPos(float4(pos, 1));
        float tailGrad = 1.0 - isTail;

        float tailLenInUV = (lineLen*view_component_len + radius1 + radius2) / radius; // only works when radius1 == radius2
        //float tailLenInUV = (lineLen*view_component_len + radius1 * (distToCam / distToCam1) + radius2 * (distToCam / distToCam2)) / radius; // only works when radius1 == radius2
        //float tailLenInUV = lineLen * view_component_len / radius;
        //if(tailLenInUV < 6.5)
        //    alphaMul = 0;
        float stretchBasedOnViewAngle = tailLenInUV;
        float tailGrad2 = stretchBasedOnViewAngle * isTail;
        float tailGrad3 = stretchBasedOnViewAngle * (1.0 - isTail);

        float shrink = radius1 > radius2 ? lerp(radius2 / radius1, 1., isTail) : lerp(1., radius1 / radius2, isTail);

        o.uv = float4(tangentPosN * shrink, shrink, alphaMul, tailGrad2 * shrink);
        o.uv2 = tailGrad3 * shrink;


#elif USE_DIRECTIONLESS

        grad.y = grad.y * 2. - 1.;
        float3 tangentDir  = normalize(cross(float3(0.0, 1.0, 0.0), view_forward));
        float3 tangentDir2 = normalize(cross(tangentDir, view_forward));

    #if TPDROP_USE_QUAD
        o.uv = float4(grad.x, grad.y, alphaMul, grad.y);
    #else
        float triSizeMul = 1.62;
        grad.y += 0.385;
        grad *= triSizeMul;
        o.uv = float3(grad.x, grad.y, alphaMul);
    #endif

        pos += (tangentDir * grad.x - tangentDir2 * grad.y) * (aaSize * 0.5);
        o.position = UnityObjectToClipPos(float4(pos, 1));

#else
        float tangentPosN = grad.x;
        float isTail = grad.y;

        float tangentPos = tangentPosN * (aaSize * 0.5);

        float3 diff = particlePosAndLifePrev.xyz - pos;
        float len = length(diff);
        float3 diffDir = diff / len;

        float3 forward_component = view_forward * dot(view_forward, diffDir);
        float3 view_component = diffDir - forward_component;
        float view_component_len = length(view_component);
        float3 diffDir2D = view_component / view_component_len;
        float3 tangentDir = normalize(cross(diffDir2D, view_forward));

        float fullLen = size + size * _Tail + _TailFromSpeed * len;

        pos += diffDir * fullLen * lerp(-0.35, 0.65, isTail) + tangentDir * tangentPos;

        o.position = UnityObjectToClipPos(float4(pos, 1));
        float tailGrad = 1.0 - isTail;

    #if TPDROP_USE_QUAD
        float tailLenInUV = fullLen / (aaSize * 0.5);
        float stretchBasedOnViewAngle = view_component_len;
        float tailGrad2 = isTail * tailLenInUV * stretchBasedOnViewAngle;
        o.uv = float4(tangentPosN, tailGrad, alphaMul, tailGrad2);
    #else
        o.uv = float3(tangentPosN, tailGrad, alphaMul);
    #endif

#endif



//#if USE_DROP_SHAPE
//        o.uv = float2(tangentPosN * 0.8, isTail ? 0.0 : 1.0);
//#endif
//#if USE_TRANSPARENT_PARTICLES
//        o.uv = float3(tangentPosN, saturate(-tangentPosN), isTail ? 0.0 : 1.0);
//#endif

    }
    //o.color = _Color;
    //o.color.a *= sw;

    //UNITY_TRANSFER_FOG(o, o.position);

#if USE_PROJPOS
    o.projPos = ComputeScreenPos(o.position);
    o.projPos.z = -UnityObjectToViewPos(v.position).z;  // COMPUTE_EYEDEPTH(o.projPos.z);
#endif


    return o;
}
float pow4(float x) { x *= x;  return x * x; }
float pow3(float x) { return x * x * x; }
float dot2(float x) { return x * x; }
float dot2(float2 x) { return dot(x, x); }
float dot2(float3 x) { return dot(x, x); }
float rsmul(float x, float a) { return a * x / (2.0 * a * x - a - x + 1.0); }

float reduceMul(float2 v) { return v.x * v.y; }
float reduceMul(float3 v) { return v.x * v.y * v.z; }

float4 frag(v2f i) : SV_Target
{
    //  return float4(1.0, 1.0, 1.0, 1.0);

//    clip(1. - dot(i.uv, i.uv));
//        if (length(i.uv) > 1.) discard;

        fixed4 c = _Color;

#if USE_MAINTEX_AS_COLOR_SOURCE
        c *= i.color;
#endif

#if USE_CUSTOM_COLOR_TRANSFORM
    #if USE_PROJPOS
        //c = CustomColorTransform(c, i.position, i.projPos);
        c = CustomColorTransform(c, i.position, i.projPos.xy / i.projPos.w);
    #else
        c = CustomColorTransform(c, i.position, i.uv);
    #endif
#endif

#if USE_TRANSPARENT_PARTICLES

        // debug size
        //if(frac(0.4*_Time[1]) < 0.2) return float4((0.1).xxx, 1.);

    #if USE_POINTCLOUD_LINES
            //return float4((frac(i.uv.x / i.uv.y * 2.)).xxx, 1.0);

            float tailGradAspect1 = i.uv.w / i.uv.y;
            float tailGradAspect2 = i.uv2.x / i.uv.y;
            float grad = i.uv.x / i.uv.y;
            /*
            float circle1  = max(0., 1.0 - dot2(float2(grad, tailGradAspect1 - 1.0)));
            float circle2  = max(0., 1.0 - dot2(float2(grad, tailGradAspect2 - 1.0)));
            float rounded1 = max(0., 1.0 - dot2(float2(grad, min(0., tailGradAspect1 - 1.0))));
            float rounded2 = max(0., 1.0 - dot2(float2(grad, min(0., tailGradAspect2 - 1.0))));
            float alpha = pow3(min(rounded1, rounded2)) - 0.5 * (pow3(circle1) + pow3(circle2));
*/
            float circle1  = max(0., 1.0 - length(float2(grad, tailGradAspect1 - 1.0)));
            float circle2  = max(0., 1.0 - length(float2(grad, tailGradAspect2 - 1.0)));
            float rounded1 = max(0., 1.0 - length(float2(grad, min(0., tailGradAspect1 - 1.0))));
            float rounded2 = max(0., 1.0 - length(float2(grad, min(0., tailGradAspect2 - 1.0))));
            //float alpha = min(rounded1, rounded2) - 0.5 * max(circle1, circle2);
            //float alpha = min(rounded1, rounded2) - 0.5 * (circle1 + circle2);
            //float alpha = min(rounded1, rounded2) - circle1;

            float alpha = pow4(min(rounded1, rounded2)) - 0.5 * pow4(max(circle1, circle2));
            //float alpha = dot2(min(rounded1, rounded2)) - 0.5 * dot2(max(circle1, circle2));
            //float alpha = min(rounded1, rounded2) - 0.5 * max(circle1, circle2);

            //alpha *= alpha;
            //alpha *= alpha;

            //if (frac(0.4 * _Time[1]) < 0.2)
            //    alpha = 0.2;

    #elif USE_DIRECTIONLESS

            #if TP_USE_GLOW

                // use length to get proper linear starting point

                float alpha = 1. - length(i.uv.xy);
                alpha = max(0., alpha);
                alpha *= alpha;

            #else

                // use dot2 / pow3 instead of length / signedSquare
                // should be faster, but also give a slightly harder/more visible character

                float alpha = 1. - dot2(i.uv.xy); 
                #if TPDROP_SHARP
                    float softness = 2.0;
                    alpha = alpha / max(fwidth(alpha * softness), 0.0001);
                #else
                    alpha = pow3(alpha);
                #endif
            #endif
                

    #else   

            float tailGrad = i.uv.y;
            float grad = i.uv.x;
            tailGrad = max(0.001, tailGrad); // fix for artifacts caused by values <= 0 (negative values can occur due to interpolation!)

        #if TPDROP_USE_QUAD
            float tailGradAspect = i.uv.w;
            float rounded = max(0., 1.0 - dot2(float2(grad, min(0., tailGradAspect-1.0))));
            float ygrad = tailGrad;
            #if TPDROP_SHARP
                // not really sharp, but less soft...
                float quadGradient = ygrad * rounded;
            #else
                float xgrad = 1. - abs(grad);
                float quadGradient = xgrad * ygrad * rounded;
            #endif
            quadGradient *= quadGradient;
            float alpha = quadGradient;
        #else
            grad /= tailGrad;
            float dropGradient = (1.0 - dot2(float2(grad, tailGrad))) * tailGrad;
            float alpha = dropGradient;

            #if TPDROP_SHARP
                float softness = 1.0/4.;
                 alpha = alpha / max(fwidth(grad * softness), 0.0001);
            #else
                alpha *= 2.595;
                alpha *= alpha * alpha;
            #endif

        #endif

    #endif 

    #if TP_USE_GLOW
        alpha = rsmul(alpha, _Glow);
    #endif

        alpha = saturate(alpha) * i.uv.z;

    #if TPDROP_ADDITIVE
        c.rgb *= alpha * c.a; // additive
    #else
        c.a *= alpha; // alpha
    #endif


#else 
    // opaque

    #if USE_DIRECTIONLESS

            float alpha = 1. - dot2(i.uv.xy);
            c.a = alpha / max(fwidth(alpha), 0.0001) + 0.5; 

        //#if TPDROP_SHARP
        //    float softness = 2.0;
        //    alpha = alpha / max(fwidth(alpha * softness), 0.0001);
        //#else
        //    alpha = pow3(alpha);
        //#endif

    #endif

#endif


#if USE_DROP_SHAPE

    #define smoother 1

    #if smoother
        float alpha = (1. - dot2(float2((i.uv.r) / i.uv.g, i.uv.g))) * i.uv.g;
        c.a = alpha / max(fwidth(alpha), 0.0001) + 0.5;  // AlphaToCoverage sharpening https://medium.com/@bgolus/anti-aliased-alpha-test-the-esoteric-alpha-to-coverage-8b177335ae4f


    #else
        float2 uv = i.uv * float2(0.8, 1.0);

        //uv.x /= uv.y;
       //return float4(abs(i.uv.xxx) / i.uv.y, 1.);
       //return float4(abs(i.uv.xxx), 1.);


        float alpha = dot(uv, uv);
        c.a = (1. - alpha) / max(fwidth(-alpha), 0.0001) + 0.5;  // AlphaToCoverage sharpening https://medium.com/@bgolus/anti-aliased-alpha-test-the-esoteric-alpha-to-coverage-8b177335ae4f
    #endif
#endif
        //c.rgb = i.uv.xyx;
        //fixed4 c = i.color;
        //UNITY_APPLY_FOG(i.fogCoord, c);
        return c;
}
