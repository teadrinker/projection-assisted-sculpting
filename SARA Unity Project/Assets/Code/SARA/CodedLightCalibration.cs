
// by: Martin Eklund, music@teadrinker.net
// Licence: GNU GPL v3.0 (https://www.gnu.org/licenses/gpl-3.0.en.html)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace teadrinker { 

    public class CodedLightCalibration : MonoBehaviour
    {
        public bool doSecondCodedLightPass;
        [Space]
        public float mergeTolerance = 0.05f; // distance at 1m
        public bool drawGizmos;
        public int drawGizmosSkip = 0;
        [Range(0f, 8f)] public float nearLimit = 0.4f;
        [Range(0f, 8f)] public float farLimit = 3f;

        [Space]
        public bool calcProjectorValues;
        public int projectorRayTests = 100000;
        public float minLineDist = 0.1f;  // if the 3d distance between 1st and 2nd pass points is smaller, ray is excluded (to increase precision)
        public float minNormalDiff = 3f;  // (for each ray test) if angle between ray normals between 1st and 2nd pass point is smaller, test is cancelled (to increase precision)
        //public float minPixelDist = 8f;  // (for each ray test) if the projector pixel distance between 1st and 2nd pass point is smaller
        public float projectorPosMaxRad = 0.2f;  // if the 3d distance between 1st and 2nd pass points is smaller, ray is excluded (to increase precision)
        public NormalizedCameraController destination = null;

        [Space]
        public SARAKinect kinectStream;
        public GameObject Disable1;
        [Space]
        public bool ShowInput;
        public bool ShowCodedLight;
        public bool ShowOutputCodedLight;
        public bool ShowOutputSimulated;
        public bool ShowOutputDiffX;
        public bool ShowOutputDiffY;
        [Space]
        public Transform virtualKinectTrans;
        public Camera destVirtualCamera;
        [Space]
        public Material DebugOutput;

        public System.Action OnCalibrationComplete; 

        private ImgContext _ctx;
        private CodedLightToCoordMap _codedLightToCoordMap;
		private void ValidateContext()
		{
            if (_ctx == null)
            {
                _ctx = new ImgContext();
                _ctx.Add("simpleInpaint", 1, Resources.Load<Shader>("SimpleInpaint"));
                _ctx.Add("inpaintMissing", 1, Resources.Load<Shader>("InpaintMissing"));
            }
        }

		private void OnEnable()
		{
            ValidateContext();
            Disable1.SetActive(false);
        }
		private void OnDisable()
		{
            if (Disable1 != null)
                Disable1.SetActive(true);
        }

        bool _calcValid = false;
        ulong _validFrame = 9999999;
        Color32[] _debugTexPixels;
        Texture2D _debugTex;
        Vector2[] _depthCoordToCodedLightCoords;
        Vector3[] _depthCoordToKinectLocal3D;
        Vector2[] _depthCoordToCodedLightCoordsPrev;
        Vector3[] _depthCoordToKinectLocal3DPrev;

        Vector3[] _depthCoordToKinectLocal3DSaved; // for debug
        Dictionary<int, List<Vector3>> _codedLightCoordsToKinectLocal3D = new Dictionary<int, List<Vector3>>();
        //Vector3[] _depthCoordToCodedLightCoordsS;
        void OnDrawGizmos()
        {
            DrawRayGizmos();
        }
        public void DrawRayGizmos()
		{
            if (!drawGizmos)
                return;

            var cm = 1f + drawGizmosSkip;
            var green = new Color(0f, cm, 0f, 0.3f);
            var white = new Color(cm, cm, cm, 0.1f);
            var orange = new Color(cm, cm * 0.5f, 0f, 0.01f);
            if(_codedLightCoordsToKinectLocal3D.Count > 0)
			{
                var counter = 0;
                var mat = virtualKinectTrans.localToWorldMatrix;
                var ofs = 0.002f * virtualKinectTrans.right;
                Gizmos.color = green;
                for (int i = 0; i < _depthCoordToKinectLocal3DSaved.Length; i++)
				{
                    if (drawGizmosSkip > 0 && (counter++ % (drawGizmosSkip + 1)) != 0) continue;
                    var coord = _depthCoordToKinectLocal3DSaved[i];
                    coord = mat.MultiplyPoint(coord);
                    if (coord.z > 0f)
					{
                        Gizmos.DrawLine(coord, coord + ofs);
					}
				}
                float mergeToleranceSqr = mergeTolerance * mergeTolerance;
                foreach (var item in _codedLightCoordsToKinectLocal3D)
			    {
                    if (drawGizmosSkip > 0 && (counter++ % (drawGizmosSkip + 1)) != 0) continue;
                    var a = item.Value[0];
                    var b = item.Value[1];
                    a = mat.MultiplyPoint(a);
                    b = mat.MultiplyPoint(b);
                    if((a-b).sqrMagnitude > mergeToleranceSqr)
				    {
                        Gizmos.color = orange;
                        var norm = (b - a).normalized;
                        var dist = 1f;
                        Gizmos.DrawLine(a - norm * dist, b + norm * dist);
				    }
                    Gizmos.color = white;
                    Gizmos.DrawLine(a, b);
			    }
                Gizmos.color = white;
                var projPos = mat.MultiplyPoint(_projectorPosInLocalKinect);
                Gizmos.DrawWireSphere(projPos, 0.025f);
            }

        }
        void MergeWithTolerance(bool secondPass)
		{
            float toleranceDistance = mergeTolerance;
            var startId = secondPass ? 1 : 0; 
            List<int> removeKeys = new List<int>();
            foreach (var item in _codedLightCoordsToKinectLocal3D)
            {
                var list = item.Value;
                var count = list.Count - startId;
                if (count > 1 && MergePositionsWithTolerance(list, toleranceDistance * list[startId].z, out Vector3 result, startId))
                {
                    if(secondPass)
	                    list.RemoveRange(1, count);
					else
                        list.Clear();
                    list.Add(result);
                }
                else
                    removeKeys.Add(item.Key);
            }            
            foreach (var itemKey in removeKeys)
            {
                _codedLightCoordsToKinectLocal3D.Remove(itemKey);
            }
        }
        /*
        void removeAllListsWhereCountIsNot(int n)
		{
            List<int> removeKeys = new List<int>();
            foreach (var item in _codedLightCoordsToKinectLocal3D)
            {
                if (item.Value.Count != 2)
                {
                    removeKeys.Add(item.Key);
                }
            }
            foreach (var itemKey in removeKeys)
            {
                _codedLightCoordsToKinectLocal3D.Remove(itemKey);
            }
        }
        */
        void AddTo3DCoordDict(int w, int h, int strideCodedLight, Vector2[] depthCoordToCodedLightCoords, Vector3[] depthCoordToKinectLocal3D, bool secondPass)
		{
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var index = x + y * w;
                    if (depthCoordToKinectLocal3D[index].z != 0f)
                    {
                        var codedLightCoords = depthCoordToCodedLightCoords[index];
                        var codedLightId = ((int)codedLightCoords.x) + strideCodedLight * ((int)codedLightCoords.y);
                        List<Vector3> list;
                        if (!_codedLightCoordsToKinectLocal3D.TryGetValue(codedLightId, out list))
						{
                            if (secondPass)
                                continue;

                            list = new List<Vector3>();
                            _codedLightCoordsToKinectLocal3D.Add(codedLightId, list);
						}
                        list.Add(depthCoordToKinectLocal3D[index]);
                    }
                }
            }
        }

        bool Merge2PositionsWithTolerance(Vector3 a, Vector3 b, float toleranceDistanceSqr, out Vector3 result)
		{
            if((a - b).sqrMagnitude < toleranceDistanceSqr)
			{
                result = (a + b) * (1f / 2f);
                return true;
			}
            result = Vector3.zero;
            return false;
		}
        bool Merge3PositionWithToleranceInner(Vector3 a, Vector3 b, Vector3 c, bool sideAB, bool sideBC, bool sideAC, out Vector3 result)
		{
            if (!sideAB && !sideBC && !sideAC)
			{
                result = Vector3.zero;
                return false;
            }

            if (sideAB && !sideBC && !sideAC)
                result = (a + b) * 0.5f;
            else if (!sideAB && sideBC && !sideAC)
                result = (b + c) * 0.5f;
            else if (!sideAB && !sideBC && sideAC)
                result = (a + c) * 0.5f;
            else
                result = (a + b + c) * (1f / 3f);

            return true;
        }
        bool Merge3PositionsWithTolerance(Vector3 a, Vector3 b, Vector3 c, float toleranceDistanceSqr, out Vector3 result)
		{
            var sideAB = (a - b).sqrMagnitude < toleranceDistanceSqr;
            var sideBC = (b - c).sqrMagnitude < toleranceDistanceSqr;
            var sideAC = (a - c).sqrMagnitude < toleranceDistanceSqr;

            return Merge3PositionWithToleranceInner(a, b, c, sideAB, sideBC, sideAC, out result);
        }
        bool Merge4PositionsWithTolerance(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float toleranceDistanceSqr, out Vector3 result)
		{
            var sideAB = (a - b).sqrMagnitude < toleranceDistanceSqr;
            var sideAC = (a - c).sqrMagnitude < toleranceDistanceSqr;
            var sideAD = (a - d).sqrMagnitude < toleranceDistanceSqr;
            var sideBC = (b - c).sqrMagnitude < toleranceDistanceSqr;
            var sideBD = (b - d).sqrMagnitude < toleranceDistanceSqr;
            var sideCD = (c - d).sqrMagnitude < toleranceDistanceSqr;

            if ((sideAB ? 1 : 0) + (sideBC ? 1 : 0) + (sideAC ? 1 : 0) + (sideAD ? 1 : 0) + (sideBD ? 1 : 0) + (sideCD ? 1 : 0) < 3) // if half of points are outside tolerance, result might not be accurate
			{
                result = Vector3.zero;
                return false;
			}

            if (!sideAB && !sideAC && !sideAD)
                return Merge3PositionWithToleranceInner(b, c, d, sideBC, sideCD, sideBD, out result);
            if (!sideAB && !sideBC && !sideBD)
                return Merge3PositionWithToleranceInner(a, c, d, sideAC, sideCD, sideAD, out result);
            if (!sideAC && !sideBC && !sideCD)
                return Merge3PositionWithToleranceInner(a, b, d, sideAB, sideBD, sideAD, out result);
            if (!sideAD && !sideBD && !sideCD)
                return Merge3PositionWithToleranceInner(a, b, c, sideAB, sideBC, sideAC, out result);

            result = (a + b + c + d) * (1f / 4f);
            return true;

            /*
            result = Vector3.zero;

            if (!sideAB && !sideBC && !sideAC && !sideAD && !sideBD && !sideCD)
                return false;

            if      ( sideAB && !sideBC && !sideAC && !sideAD && !sideBD && !sideCD)  // half of points are outside tolerance? still accept result?
                result = (a + b) * 0.5f;
            else if (!sideAB &&  sideBC && !sideAC && !sideAD && !sideBD && !sideCD)
                result = (b + c) * 0.5f;
            else if (!sideAB && !sideBC &&  sideAC && !sideAD && !sideBD && !sideCD)
                result = (a + c) * 0.5f;
            else if (!sideAB && !sideBC && !sideAC &&  sideAD && !sideBD && !sideCD)
                result = (a + d) * 0.5f;
            else if (!sideAB && !sideBC && !sideAC && !sideAD &&  sideBD && !sideCD)
                result = (b + d) * 0.5f;
            else if (!sideAB && !sideBC && !sideAC && !sideAD && !sideBD &&  sideCD)
                result = (c + d) * 0.5f;
            else 
                result = (a + b + c + d) * (1f / 4f);
            return true;
            */
        }

        bool MergePositionsWithTolerance(List<Vector3> list, float toleranceDistance, out Vector3 result, int startId = 0)//, bool acceptSingleDatapoints = false)
        {
            result = Vector3.zero;
            int N = list.Count - startId;

            if (N < 1)
                return false;

            // When using gray codes and also inverted pass for each stripe, we can rely on our samples and skip tolerance code?
            // (it seems -NO!)

            /*
            result = Vector3.zero;
            for (int i = 0; i < N; i++)
                result += list[startId + i];
            result *= 1f / N; // just average samples
            return true;
            */

            /*if (N == 1)
            {        
                result = list[startId];
                return true;
            }
            else if (N == 2)
            {
                result = (list[startId] + list[startId + 1]) * 0.5f;
                return true;
            }*/
            

            if (N == 2)
			{
                return Merge2PositionsWithTolerance(list[startId], list[startId + 1], toleranceDistance * toleranceDistance, out result);
            }
            else if(N == 3)
			{
                return Merge3PositionsWithTolerance(list[startId], list[startId + 1], list[startId + 2], toleranceDistance * toleranceDistance, out result);
            }
            else if (N == 4)
            {
                return Merge4PositionsWithTolerance(list[startId], list[startId + 1], list[startId + 2], list[startId + 3], toleranceDistance * toleranceDistance, out result);
            }
            else if(N > 4)
			{
                // use 3 passes to handle outliers well. (calc mean -> find outliers -> clac mean with tolerance)
                // if number of outliers > 2, this heuristic might still fail, 
                // however, this should be detected in most cases and return value will then be false.

                float toleranceDistanceSqr = toleranceDistance;
                toleranceDistanceSqr *= 2f; // increase tolerance to account for shift of center
                toleranceDistanceSqr *= toleranceDistanceSqr;

                // calc mean/average position
                var avr = list[startId]; 
                for(int i = 1; i < N; i++)
				{
                    avr += list[startId + i];
				}
                avr /= N;

                // find the furthest 2 outliers
                var outlier1SqrDist = -1f;
                var outlier1Id = -1;
                var outlier2SqrDist = -1f;
                var outlier2Id = -1;
                for (int i = 0; i < N; i++)
				{
                    int id = startId + i;
                    var v = list[id];
                    var sqrDist = (v - avr).sqrMagnitude;
                    if(sqrDist > outlier1SqrDist)
					{
                        outlier2SqrDist = outlier1SqrDist;
                        outlier2Id = outlier1Id;
                        outlier1SqrDist = sqrDist;
                        outlier1Id = id;
                    }
                    else if(sqrDist > outlier2SqrDist)
					{
                        outlier2SqrDist = sqrDist;
                        outlier2Id = id;
                    }
                }

                if (outlier1Id == -1 || outlier2Id == -1)
                    Debug.LogError("Should never happen " + outlier1Id + " " + outlier2Id);

                // refine mean/average position (exclude the 2 outliers)
                avr *= N;
                avr -= list[outlier1Id] + list[outlier2Id];
                avr /= N - 2;

                // sum up points within tolerance
                int withinToleranceN = 0;
                for(int i = 0; i < N; i++)
				{
                    int id = startId + i;
                    var v = list[id];
                    var sqrDist = (v - avr).sqrMagnitude;
                    if (sqrDist < toleranceDistanceSqr)
                    {
                        result += v;
                        withinToleranceN++;
                    }
                }

                if(withinToleranceN >= N / 2)
				{
                    result /= withinToleranceN;
                    return true;
				}
            }
            return false;
        }

        bool RayRayIntersection(Vector3 linePoint1, Vector3 lineVec1, Vector3 linePoint2, Vector3 lineVec2, out Vector3 middlePoint)
        {
            middlePoint = Vector3.zero;
            if(RayRayIntersection(linePoint1, lineVec1, linePoint2, lineVec2, out Vector3 p1, out Vector3 p2)) {
                middlePoint = (p1 + p2) * 0.5f;
                return true;
			}
            return false;
        }

        bool RayRayIntersection(Vector3 linePoint1, Vector3 lineVec1, Vector3 linePoint2, Vector3 lineVec2, out Vector3 closestPointLine1, out Vector3 closestPointLine2)
        {
            var a = Vector3.Dot(lineVec1, lineVec1);
            var b = Vector3.Dot(lineVec1, lineVec2);
            var e = Vector3.Dot(lineVec2, lineVec2);

            var d = a * e - b * b;

            if (d != 0.0)
            {
                var r = linePoint1 - linePoint2;
                var c = Vector3.Dot(lineVec1, r);
                var f = Vector3.Dot(lineVec2, r);

                var s = (b * f - c * e) / d;
                var t = (a * f - c * b) / d;

                closestPointLine1 = linePoint1 + lineVec1 * s;
                closestPointLine2 = linePoint2 + lineVec2 * t;
                return true;
            }
            closestPointLine1 = Vector3.zero;
            closestPointLine2 = Vector3.zero;
            return false;
        }

        public void GenerateDisplacement()
		{
            var pw = (int)(destVirtualCamera.pixelWidth + 0.5f);
            var ph = (int)(destVirtualCamera.pixelHeight + 0.5f); // destVirtualCamera.pixelRect.height ??

            var dDataN = pw * ph;
            var dData0 = new Color[dDataN];
            var mat = virtualKinectTrans.localToWorldMatrix;
            var oopw = 1f / pw;
            var ooph = 1f / ph;
            foreach (var item in _codedLightCoordsToKinectLocal3D)
            {
                var p = (item.Value[0] + item.Value[1]) * 0.5f;
                p = mat.MultiplyPoint(p);
                var virtualXY = destVirtualCamera.WorldToScreenPoint(p);
                var pixelId = item.Key;
                var pry = pixelId / pw;
                var prx = pixelId - pry * pw;
                var id = pry * pw + prx;
                if(id < dDataN)
                    dData0[id] = new Color((virtualXY.x - prx) * oopw, (virtualXY.y - pry) * ooph, 1f, 1f);
            }

            SetDisplacement(dData0, pw, ph);
            /*
            var dData = new Color[dDataN];
            var kN = 15; // full kernel size = kN * 2 + 1
            for (int y = kN; y < ph - kN; y++)
            {
                for (int x = kN; x < pw - kN; x++)
                {
                    var id = y * pw + x;
                    var c = dData0[id];
                    if (c.b == 0f)
                    {
                        for (int yy = -kN; yy <= kN; yy++)
                        {
                            for (int xx = -kN; xx <= kN; xx++)
                            {
                                var c2 = dData0[id + yy * pw + xx];
                                if (c2.b != 0f)
                                {
                                    float weight = 1f / Mathf.Sqrt((xx * xx + yy * yy));
                                    c.r += c2.r * weight;
                                    c.g += c2.g * weight;
                                    c.b += weight;
                                }
                            }
                        }
                        c.r /= c.b;
                        c.g /= c.b;
                    }
                    dData[id] = c;
                }
            }
            SetDisplacement(dData, pw, ph);
            */

        }
        [HideInInspector] public int lastDisplacementW;
        [HideInInspector] public int lastDisplacementH;
        [HideInInspector] public Color[] lastDisplacement;
        private Texture2D lastDisplacementTex;

        public bool DisplacementOutlierFilter = false;
        public float DisplacementOutlierFilterRadius = 20;
        public float DisplacementOutlierFilterThres = 0f;

        public bool DisplacementInpaint = true;
        public int DisplacementInpaintRadius = 40;
        public float DisplacementInpaintConstant = 1.5f;
        public float DisplacementInpaintSampleMargin = 1.5f;

        public bool DisplacementBlur = false;
        public int DisplacementBlurRadius = 10;
        public void SetDisplacement(Color[] dData, int pw, int ph)
        {
            lastDisplacement = dData;
            lastDisplacementW = pw;
            lastDisplacementH = ph;

            lastDisplacementTex = new Texture2D(lastDisplacementW, lastDisplacementH, TextureFormat.RGBAFloat, false);
            lastDisplacementTex.SetPixels(lastDisplacement);
            lastDisplacementTex.Apply(false, true);

            UpdateDisplacement();
        }
        public void UpdateDisplacement() {
            ValidateContext();
            if (lastDisplacementTex == null)
                return;
            var img = _ctx.DataSource(lastDisplacementTex);

            if(!DisplacementInpaint)
			{
                var rad = DisplacementInpaintRadius;

                if (rad > 100)
                    rad = 100;

                for (int i = 0; i < rad; i++)
                    img = img.call("simpleInpaint");
            }
            else
			{
                if (DisplacementOutlierFilter && DisplacementOutlierFilterThres > 0f)
                    img = img.call("inpaintMissing")
                                .set("_OutlierFilterThres", DisplacementOutlierFilterThres)
                                .set("_KernelRadius", DisplacementOutlierFilterRadius)
                                .set("_SampleMargin", DisplacementInpaintSampleMargin)
                                .set("_WeightC", DisplacementInpaintConstant)
                                .set("_WeightCMarginCombine", DisplacementInpaintConstant)
                                ;
                if (DisplacementInpaint)
                    img = img.call("inpaintMissing")
                                .set("_OutlierFilterThres", -1f)
                                .set("_KernelRadius", DisplacementInpaintRadius)
                                .set("_SampleMargin", DisplacementInpaintSampleMargin)
                                .set("_WeightC", DisplacementInpaintConstant)
                                .set("_WeightCMarginCombine", DisplacementInpaintConstant)
                                ;
			}
/*


            if (DisplacementOutlierFilter && DisplacementOutlierFilterThres > 0f)
                img = img.call("inpaintMissing")
                            .set("_OutlierFilterThres", DisplacementOutlierFilterThres)
                            .set("_KernelRadius", DisplacementOutlierFilterRadius)
                            .set("_SampleMargin", DisplacementInpaintSampleMargin)
                            .set("_WeightC", DisplacementInpaintConstant)
                            .set("_WeightCMarginCombine", DisplacementInpaintConstant)
                            ;
            if (DisplacementInpaint)
                img = img.call("inpaintMissing")
                            .set("_OutlierFilterThres", -1f)
                            .set("_KernelRadius", DisplacementInpaintRadius)
                            .set("_SampleMargin", DisplacementInpaintSampleMargin)
                            .set("_WeightC", DisplacementInpaintConstant)
                            .set("_WeightCMarginCombine", DisplacementInpaintConstant)
                            ;
                            */
            if (DisplacementBlur)
                img = img.blur(DisplacementBlurRadius);

            var disp = img.RequestTexure();
            var old = destVirtualCamera.GetComponent<DisplaceImagePostFX>().displacementTexture;
            if (old != null && old != disp && old != (Texture)lastDisplacementTex)
                Destroy(old);
            destVirtualCamera.GetComponent<DisplaceImagePostFX>().displacementTexture = disp;
		}
        private Vector3 _projectorPosInLocalKinect;
        public void SetColorMul(float v) {
            if (_codedLightToCoordMap == null)
                _codedLightToCoordMap = GetComponent<CodedLightToCoordMap>();

            _codedLightToCoordMap.amount = v;
        }
        void Update()
        {
            if (_codedLightToCoordMap == null)
                _codedLightToCoordMap = GetComponent<CodedLightToCoordMap>();

            if (doSecondCodedLightPass) {
                doSecondCodedLightPass = false;

                _calcValid = false;

                _depthCoordToKinectLocal3DPrev = _depthCoordToKinectLocal3D;
                _depthCoordToCodedLightCoordsPrev = _depthCoordToCodedLightCoords;
                kinectStream.ClearAccumulatedDepth();
                _codedLightToCoordMap.Restart();
                return;
            }

            var newFrameId = kinectStream.GetKinectFrame();
            if(newFrameId != _validFrame && kinectStream.debugView != null && kinectStream.debugView.mainTexture != null && kinectStream.debugView.mainTexture is RenderTexture)
			{
                _validFrame = newFrameId;
                var tex = (RenderTexture)kinectStream.debugView.mainTexture;


                _codedLightToCoordMap.PushNewFrame(tex);


                if (_codedLightToCoordMap.GetState() != CodedLightToCoordMap.State.CaptureBg &&
                    _codedLightToCoordMap.GetState() != CodedLightToCoordMap.State.CaptureFullFill &&
                    _codedLightToCoordMap.GetState() != CodedLightToCoordMap.State.Start)
                    kinectStream.AccumulateDepth = true;

                var pw = (int)(destVirtualCamera.pixelWidth + 0.5f);
                var ph = (int)(destVirtualCamera.pixelHeight + 0.5f); // destVirtualCamera.pixelRect.height ??
                var outTex = _codedLightToCoordMap.DebugOutput;
                int sw = 1;
                int sh = 1;
                if (!_calcValid && _codedLightToCoordMap.GetState() == CodedLightToCoordMap.State.Done)
				{
                    sw = outTex.width;
                    sh = outTex.height;
                    var colorPixelsN = sw * sh;
                    var colorPixels = _ctx.DataSource(outTex).RequestPixels();
                    int w = kinectStream.DepthW();
                    int h = kinectStream.DepthH();
                    _depthCoordToCodedLightCoords = new Vector2[w * h];
                    _depthCoordToKinectLocal3D = new Vector3[w * h];
                    //_depthCoordToCodedLightCoordsS = new Vector3[w * h];
                    kinectStream.CalcDepthToColorCoords();
                    var accumDepth = kinectStream.AccumulatedDepthBuf;
                    var accumDepthN = kinectStream.AccumulatedCount;
                    kinectStream.AccumulateDepth = false;
                    var depthCoordTo3D = DepthStream.GetDepthCalibration("KinectCalibration", w, h, kinectStream.sensorIndex);
                    //var projectorResToKinectColorRes = new Vector2(sw / destVirtualCamera.pixelRect.width, sh / destVirtualCamera.pixelRect.height);
                    for (int y = 0; y < h; y++)
					{
                        for(int x = 0; x < w; x++)
						{
                            var index = x + y * w;
                            var depth = ((float)accumDepth[index]) / accumDepthN;
                            var coord = kinectStream.DepthPointToColorCoord(index, (int)depth);
                            //int colX = (int) (coord.x + 0.5f);
                            //int colY = (int) (coord.y + 0.5f);
                            int colX = (int) coord.x;
                            int colY = (int) coord.y;
                            int colIndex = colX + colY * sw;
                            colIndex = Mathf.Clamp(colIndex, 0, colorPixelsN - 1);
                            var col = colorPixels[colIndex]; 
                            _depthCoordToCodedLightCoords[index] = new Vector2(col.r, col.g);

                            depth *= 0.001f;

                            if(depth < nearLimit || depth > farLimit)
                                _depthCoordToKinectLocal3D[index] = Vector3.zero;
                            else
							{
                                var calib = depthCoordTo3D[index];
                                _depthCoordToKinectLocal3D[index] = new Vector3(calib.x * depth, -calib.y * depth, depth);
							}
                            /*
                            if(depth < 400 || depth > 3000)
                                _depthCoordToCodedLightCoordsS[index] = Vector3.zero;
                            else
							{
                                var calib = depthCoordTo3D[index];
                                var point3D = new Vector3(calib.x * depth, calib.y * depth, depth) * 0.001f;

                                point3D = virtualKinectTrans.localToWorldMatrix.MultiplyPoint(point3D);
                                var screen = destVirtualCamera.WorldToScreenPoint(point3D);
                                if (screen.x < 0 || screen.x > pw || screen.y < 0 || screen.y > ph)
                                    screen = Vector3.zero;
                                else
                                    screen.y = ph - 1 - screen.y;
                                _depthCoordToCodedLightCoordsS[index] = screen;
							}
                            */
                        }
					}

                    _calcValid = true;

                    if (_depthCoordToKinectLocal3DPrev != null)
                    {
                        AddTo3DCoordDict(w, h, pw, _depthCoordToCodedLightCoords, _depthCoordToKinectLocal3D, false);
                        int pass2Count = _codedLightCoordsToKinectLocal3D.Count;
                        Debug.Log("Pass 2 total: " + pass2Count);
                        MergeWithTolerance(false);
                        int pass2CountWithinTolerance = _codedLightCoordsToKinectLocal3D.Count;
                        Debug.Log("Pass 2 tolerance loss: " + (1f - (((float)pass2CountWithinTolerance) / pass2Count)));
                        AddTo3DCoordDict(w, h, pw, _depthCoordToCodedLightCoordsPrev, _depthCoordToKinectLocal3DPrev, true);
                        //Debug.Log("_codedLightCoordsToKinectLocal3D.Count 1 and 2:"+ _codedLightCoordsToKinectLocal3D.Count);
                        MergeWithTolerance(true);
                        int pass1And2WithinTolerance = _codedLightCoordsToKinectLocal3D.Count;
                        Debug.Log("Pass 1+2 tolerance loss: " + (1f - (((float)pass1And2WithinTolerance) / pass2CountWithinTolerance)));
                        //Debug.Log("_codedLightCoordsToKinectLocal3D.Count 2 only:"+ _codedLightCoordsToKinectLocal3D.Count);
                        
                        _depthCoordToKinectLocal3DSaved = _depthCoordToKinectLocal3D; // for debug

                        if (calcProjectorValues)
                        {

                            var listForRandAccess = new List<List<Vector3>>();
                            var listRemove = new List<int>();
                            foreach (var item in _codedLightCoordsToKinectLocal3D)
                            {
                                var a = item.Value[0];
                                var b = item.Value[1];
                                if ((a - b).sqrMagnitude > minLineDist * minLineDist)
                                {
                                    listForRandAccess.Add(item.Value);
                                }
                                else
								{
                                    listRemove.Add(item.Key);
								}
                            }
                            foreach (var key in listRemove)
                                _codedLightCoordsToKinectLocal3D.Remove(key);
                            Debug.Log("MinLineDist loss: " + (1f - (((float)listForRandAccess.Count) / pass1And2WithinTolerance)) + ", ray count: " + listForRandAccess.Count);


                            // Estimate projector position by averaging intersections of random pairs of rays 
                            // (also estimate projector direction by just accumulate ray directions)
                            var prevItem = listForRandAccess[0];
                            var count = 0;
                            var sum = Vector3.zero;
                            var listOfIntersections = new List<Vector3>();
                            var direction = Vector3.zero;
                            for (int i = 0; i < projectorRayTests; i++)
                            {
                                var item = listForRandAccess[Random.Range(0, listForRandAccess.Count)];
                                var p1 = item[0];
                                var norm1 = (item[1] - p1).normalized;
                                direction += norm1;
                                var p2 = prevItem[0];
                                var norm2 = (prevItem[1] - p2).normalized;
                                if(Vector3.Angle(norm1, norm2) > minNormalDiff)
								{
                                    if (RayRayIntersection(p1, norm1, p2, norm2, out Vector3 mid))
                                    {
                                        listOfIntersections.Add(mid);
                                        sum += mid;
                                        count++;
                                    }
                                }
                            }

                            // Recalculate projector position, excluding outliers (hopefully increase quality)
                            var projectorPosInLocalKinect1 = sum / count;
                            var projectorPosInLocalKinect2 = Vector3.zero;
                            var count2 = 0;
                            for (int i = 0; i < listOfIntersections.Count; i++)
							{
                                if((projectorPosInLocalKinect1 - listOfIntersections[i]).sqrMagnitude < projectorPosMaxRad * projectorPosMaxRad)
								{
                                    projectorPosInLocalKinect2 += listOfIntersections[i];
                                    count2++;
								}
                            }
                            Debug.Log("projectorPosMaxRad loss: " + (1f - (((float)count2) / count)));
                            _projectorPosInLocalKinect = projectorPosInLocalKinect2 /= count2;

                            var newVirtualProjectorPos = virtualKinectTrans.localToWorldMatrix.MultiplyPoint(_projectorPosInLocalKinect);
                            var newVirtualProjectorDir = virtualKinectTrans.localToWorldMatrix.MultiplyVector(direction.normalized);
                            if (Vector3.Angle(newVirtualProjectorDir, destination.transform.forward) > 90f)
                                newVirtualProjectorDir = -newVirtualProjectorDir;

                            // Set estimated position / rotation
                            destination.transform.position = newVirtualProjectorPos;
                            destination.transform.rotation = Quaternion.LookRotation(newVirtualProjectorDir, Vector3.up);
                            destination.UpdateNow(); // make sure camera is updated as we will use destVirtualCamera.WorldToScreenPoint 


                            // Generate distortion displacement map (this will be applied as a PostFX shader later)                            
                            GenerateDisplacement();
                            gameObject.SetActive(false);
                            OnCalibrationComplete();
                            /*
                            var disp = new Texture2D(pw, ph, TextureFormat.RGBAFloat, false);
                            var dData0 = new Color[disp.width * disp.height];
                            var dData = new Color[disp.width * disp.height];
                            var mat = virtualKinectTrans.localToWorldMatrix;
                            var oopw = 1f / pw;
                            var ooph = 1f / ph;
                            foreach (var item in _codedLightCoordsToKinectLocal3D)
                            {
                                var p = (item.Value[0] + item.Value[1]) * 0.5f;
                                p = mat.MultiplyPoint(p);
                                var virtualXY = destVirtualCamera.WorldToScreenPoint(p);
                                var pixelId = item.Key;
                                var pry = pixelId / pw;
                                var prx = pixelId - pry * pw;
                                dData0[pry * pw + prx] = new Color((virtualXY.x - prx) * oopw, (virtualXY.y - pry) * ooph, 1f, 1f);
                            }
                            var kN = 15; // full kernel size = kN * 2 + 1
                            for (int y = kN; y < ph - kN; y++)
                            {
                                for (int x = kN; x < pw - kN; x++)
                                {
                                    var id = y * pw + x;
                                    var c = dData0[id];
                                    if (c.b == 0f)
									{
                                        for (int yy = -kN; yy <= kN; yy++)
                                        {
                                            for (int xx = -kN; xx <= kN; xx++)
                                            {
                                                var c2 = dData0[id + yy * pw + xx];
                                                if (c2.b != 0f)
                                                {
                                                    float weight = 1f / Mathf.Sqrt((xx * xx + yy * yy));
                                                    c.r += c2.r * weight;
                                                    c.g += c2.g * weight;
                                                    c.b += weight;
                                                }
                                            }
                                        }
                                        c.r /= c.b;
                                        c.g /= c.b;
                                    }
                                    dData[id] = c;
                                }
                            }

                            disp.SetPixels(dData);
                            disp.Apply(false, true);
                            var old = destVirtualCamera.GetComponent<DisplaceImagePostFX>().displacementTexture;
                            if (old) 
                                Destroy(old);
                            destVirtualCamera.GetComponent<DisplaceImagePostFX>().displacementTexture = disp; 
                            */
                        }

                        _depthCoordToKinectLocal3D = null;
                        _depthCoordToKinectLocal3DPrev = null;
                    }

                    if(_debugTex == null)
					{
                        _debugTexPixels = new Color32[w * h];
                        _debugTex = new Texture2D(w, h, TextureFormat.ARGB32, false);
					}
                }


                DebugOutput.color = Color.white;

                if (ShowInput)
				{
                    DebugOutput.mainTexture = kinectStream.debugView.mainTexture;
				}
                else if (ShowCodedLight)
                {
                    DebugOutput.mainTexture = _codedLightToCoordMap.DebugOutput;
                    if(_codedLightToCoordMap.GetState() == CodedLightToCoordMap.State.Done)
                        DebugOutput.color = new Color(1f / pw, 1f / ph, 1f, 1f);
                        //DebugOutput.color = new Color(1f / 1024, 1f / 1024, 1f, 1f);
                }
                else if (_debugTex != null)
                {
                    for (int i = 0; i < _debugTexPixels.Length; i++)
                    {
                        if (ShowOutputCodedLight)
                            _debugTexPixels[i] = new Color32((byte)(_depthCoordToCodedLightCoords[i].x * 255 / pw), (byte)(_depthCoordToCodedLightCoords[i].y * 255 / ph), 0, 255);
                        else
						{
                            //_debugTexPixels[i] = new Color32((byte)(_depthCoordToCodedLightCoordsS[i].x * 255 / pw), (byte)(_depthCoordToCodedLightCoordsS[i].y * 255 / ph), 0, 255);

                            var point3D = virtualKinectTrans.localToWorldMatrix.MultiplyPoint(_depthCoordToKinectLocal3D[i]);
                            var screen = destVirtualCamera.WorldToScreenPoint(point3D);
                            if (screen.x < 0 || screen.x > pw || screen.y < 0 || screen.y > ph)
                                _debugTexPixels[i] = new Color32(12, 12, 12, 255);
                            else
                            {
                                screen.y = ph - 1 - screen.y;

                                if (ShowOutputDiffX && !ShowOutputDiffY)
                                {
                                    float diffx = screen.x - _depthCoordToCodedLightCoords[i].x;
                                    float outsidex = Mathf.Max(0f, diffx * 100f);
                                    float insidex = Mathf.Max(0f, -diffx * 100f);
                                    outsidex = Mathf.LerpUnclamped(outsidex, outsidex % 1f, 0.92f);
                                    insidex = Mathf.LerpUnclamped(insidex, insidex % 1f, 0.92f);

                                    _debugTexPixels[i] = new Color32((byte)(insidex * 255 / pw), (byte)(outsidex * 255 / ph), 0, 255);
                                }
                                else if (!ShowOutputDiffX && ShowOutputDiffY)
                                {
                                    float diffy = screen.y - _depthCoordToCodedLightCoords[i].y;
                                    float outsidey = Mathf.Max(0f, diffy * 100f);
                                    float insidey = Mathf.Max(0f, -diffy * 100f);
                                    outsidey = Mathf.LerpUnclamped(outsidey, outsidey % 1f, 0.92f);
                                    insidey = Mathf.LerpUnclamped(insidey, insidey % 1f, 0.92f);

                                    _debugTexPixels[i] = new Color32((byte)(insidey * 255 / pw), (byte)(outsidey * 255 / ph), 0, 255);
                                }
                                else if (ShowOutputDiffX && ShowOutputDiffY)
                                {
                                    float diffx = screen.x - _depthCoordToCodedLightCoords[i].x;
                                    float outsidex = Mathf.Max(0f, diffx * 100f);
                                    float insidex = Mathf.Max(0f, -diffx * 100f);
                                    outsidex = Mathf.LerpUnclamped(outsidex, outsidex % 1f, 0.92f);
                                    insidex = Mathf.LerpUnclamped(insidex, insidex % 1f, 0.92f);

                                    float diffy = screen.y - _depthCoordToCodedLightCoords[i].y;
                                    float outsidey = Mathf.Max(0f, diffy * 100f);
                                    float insidey = Mathf.Max(0f, -diffy * 100f);
                                    outsidey = Mathf.LerpUnclamped(outsidey, outsidey % 1f, 0.92f);
                                    insidey = Mathf.LerpUnclamped(insidey, insidey % 1f, 0.92f);

                                    _debugTexPixels[i] = new Color32((byte)(insidex * 255 / pw), (byte)(outsidex * 255 / ph), (byte)((outsidey + insidey) * 255 / ph), 255);
                                }
                                else
                                    _debugTexPixels[i] = new Color32((byte)(screen.x * 255 / pw), (byte)(screen.y * 255 / ph), 0, 255);
                            }
						}
                    }
                    _debugTex.SetPixels32(_debugTexPixels);
                    _debugTex.Apply(false, false);
                    DebugOutput.mainTexture = _debugTex;
                }

            }


        }
    }

}


