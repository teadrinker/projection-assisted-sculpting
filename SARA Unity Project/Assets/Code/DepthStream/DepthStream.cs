//
//  DepthStream, by Martin Eklund 2021
//
//       License: GNU GPL v3, https://www.gnu.org/licenses/gpl-3.0.en.html
//       For commercial use, contact music@teadrinker.net
//  

#define USE_KINECTLIB

//#define USE_UNSAFE

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if USE_KINECTLIB
    using com.rfilkov.kinect;
#endif

namespace teadrinker
{

    public class CompressedDepthStream
	{
        public int W;
        public int H;
        public int WOrg;
        public int HOrg;
        public int XOffset;
        public int YOffset;
        public List<uint[]> Frames = new List<uint[]>();
        public uint[] GridOptimizeBitmap;

        public KeyValueStore MetaData = new KeyValueStore();

        public void SaveData(System.IO.BinaryWriter w)
        {
            int N;

            w.Write("PCS!");
            MetaData.SaveData(w);
            w.Write(W);
            w.Write(H);
            w.Write(WOrg);
            w.Write(HOrg);
            w.Write(XOffset);
            w.Write(YOffset);
            w.Write(Frames.Count);
            for (int i = 0; i < Frames.Count; i++)
            {
                var ar = Frames[i];
                N = ar.Length;
                w.Write(N);
                // possible to use unsafe and cast and to byte[] ? Write(byte[] buffer, int index, int count) 
                for (int j = 0; j < N; j++)
                    w.Write(ar[j]);
            }

            // Write compressed frame indicating what areas need to be drawn
            N = W * H;
            ushort[] used = new ushort[N];
            for (int i = 0; i < N; i++)
                used[i] = (ushort)((GridOptimizeBitmap[i >> 5] >> (i & 31)) & 1);
            List<uint> compressedOpt = new List<uint>();
            DiffRLE.Encode(null, used, compressedOpt);
            N = compressedOpt.Count;
            w.Write(N);
            for (int j = 0; j < N; j++)
                w.Write(compressedOpt[j]);

        }
        public void LoadData(System.IO.BinaryReader r)
        {
            int N;

            var magic = r.ReadString();
            if (magic != "PCS!")
            {
                Debug.LogError("File format error");
                return;
            }
            MetaData.LoadData(r);
            W = r.ReadInt32();
            H = r.ReadInt32();
            WOrg = r.ReadInt32();
            HOrg = r.ReadInt32();
            XOffset = r.ReadInt32();
            YOffset = r.ReadInt32();
            var frames = r.ReadInt32();
            Frames.Clear();
            for (int i = 0; i < frames; i++)
            {
                N = r.ReadInt32();
                var ar = new uint[N];
                // possible to use unsafe and cast and to byte[] ? Read(byte[] buffer, int index, int count)
                for (int j = 0; j < N; j++)
                    ar[j] = r.ReadUInt32();
                Frames.Add(ar);
            }

            ushort[] used = new ushort[W * H];
            GridOptimizeBitmap = new uint[(W * H + 31) / 32];
            N = r.ReadInt32();
            var usedComp = new uint[N];
            for (int j = 0; j < N; j++)
                usedComp[j] = r.ReadUInt32();
            DiffRLE.Decode(used, usedComp);
            N = W * H;
            for (int i = 0; i < N; i++)
                if (used[i] != 0)
                    GridOptimizeBitmap[i >> 5] |= (uint)(1 << (i & 31));
        }

    }

    public class DepthStream : MonoBehaviourCR
    {

        [Header("Recorder and Timeline")]
        public bool ShowLive = false;
        public bool Record = false;
        public bool PlayRecording = false;
        public float FPS = 30f;
        [Range(0f, 1f)] public float FramePosition = 0f;
        private float _prevFramePos = 0;
        //public int CurFrame = 0;
        public Vector2Int ActiveCrop = Vector2Int.zero;


        [Header("Load / Save from Disk")]
        public bool SaveRecording = false;
        public bool LoadRecording = false;
        public bool ScheduleLoadOnStart = false;
        public string RecordingPath = "";
        public string RecordingName = "Anim";
        public bool UseRawFormat = false;
        public bool AllowPartialLoad = false;


        [Header("Settings")]
        public int RecorderMaxFrames = 100;
        public int sensorIndex = 0;
        public MeshFilter PointCloudExtraTarget;
        public Material debugView;


        [Header("Preview depth and infrared")]
        public bool PreviewDepth = false;
        [Range(0f, 6f)] public float DepthPreviewMul = 0.25f;
        [Range(0f, 1f)] public float DepthPreviewAdd = 0.0f;
        public bool PreviewInfra = false;
        public int InfraredPreviewDiv = 10;
        

        [Header("Preview point cloud")]
        public bool PointCloud = true;
        public bool SelfieMirror = false;
        public bool Portrait = false;
        public int PointCloudXSkip = 0;
        public int PointCloudYSkip = 0;
        [Space()]
        [Header("Process & Clean up")]
        public bool ProcessAndCompressAll = false;
        public bool ProcessDepth = false;
        public int MinDepth = DepthCamNearLimit;
        public int MaxDepth = DepthCamNearLimit + IntRangeInMM;
        public Vector2 SkewBack = Vector2.zero;
        [Range(0f, 1f)] public float Top = 0f;
        public Vector3 TopPlane = new Vector3(0f, 2f, 0f);  // rotation, centerpoint in depth, top 2d offset (same as Top)
        [Range(0f, 1f)] public float Bottom = 0f;
        public Vector3 BottomPlane = new Vector3(0f, 2f, 0f);  // rotation, centerpoint in depth, top 2d offset (same as Top)
        [Range(0f, 1f)] public float Left = 0f;
        public Vector3 LeftPlane = new Vector3(0f, 2f, 0f);  // rotation, centerpoint in depth, top 2d offset (same as Top)
        [Range(0f, 1f)] public float Right = 0f;
        public Vector3 RightPlane = new Vector3(0f, 2f, 0f);  // rotation, centerpoint in depth, top 2d offset (same as Top)
        [Space()]
        public int averageDepthFrames = 0;

        [Header("Process options")]
        public bool FilterIR = false;
        [Range(0f, 1f)] public float NoiseDither = 0f;
        public bool CompressTest = false;
        public bool DebugNearFar = false;
        //public int CompRelaxNear = 10;
        //public int CompDeadThreshold = 0;
        public float CompTolerance = 0f;
        public string CompressionRate = "";
        [Space]
        [Header("Render options")]
        //public bool RenderAsLines = false;
        //public bool LineVertical = true;
        public bool VerticalLines = false;
        public bool HorisontalLines = false;
        [Range(0f, 2f)] public float LineOffset = 1.0f;
        [Space]
        [Range(0f, 4f)] public float PointSize = 1.0f;
        [Space]
        [Range(0f, 1f)] public float ReduceStray = 1.0f;
        [Range(0f, 1f)] public float InfraToSize = 1.0f;
        [Space]
        [Range(0f, 1f)] public float DepthToSize = 1.0f;
        [Range(0f, 1f)] public float DepthToSizeRange = 0.6f;
        [Range(0f, 1f)] public float DepthToSizeCurve = 0.5f;
        public enum RenderStyle { Additive = 0, Alpha = 1, OpaqueWithDepth = 2};
        [Space]
        public RenderStyle PointCloudRenderStyle = RenderStyle.Additive;
        [Range(0f, 10f)]public float TrailFrames = 0;
        [Space]
        public float InfraredScale = 10f;
        public float InfraredOffset = 0f;
        [Space()]
        [Range(0f, 1f)] public float ColorAmount = 0.0f;
        [Range(0f, 1f)] public float InfraToColor = 0.0f;

        [Space()]
        [Range(0f, 32f)] public float analyticalAntialias = 1f;

        [Space()]
        public float near = 0.0f;
        [Range(0, 1)] public float nearFade = 0.0f;
        public float far = 10000f;
        [Range(0, 1)] public float farFade = 0f;

        [Space()]
        public bool glowEnable = false;
        [Range(-0.999f, 0.999f)] public float glow = 0f;
        public bool debugGlowSize = false;



        //public Shader OverrideShader = null;
        public Material OverrideMaterial = null;
        private static Shader _shader;
        private static Shader _shaderTransparent;
        private static Shader GetShader(bool transparent)
		{
            if (_shader == null)
			{
                _shader = Resources.Load<Shader>("Shaders/TPPointCloud");
                _shaderTransparent = Resources.Load<Shader>("Shaders/TPPointCloud tr");
			}
            return transparent ? _shaderTransparent : _shader;
        }

        private MeshFilter _pointCloudTarget;
        private Material _pointCloudMat;


        private static Vector2[] s_depthCalibration;

        public static Vector2[] GetDepthCalibration(string calibrationPathAndName, int w, int h, int sensorIndex)
                                    //#if USE_KINECTLIB
                                    //                ,KinectInterop.SensorData sensorData
                                    //#endif         
            //)
		{
            if (s_depthCalibration == null)
            {
                var calibrationFullpath = calibrationPathAndName + "_" + w + "_" + h + ".ply";
                Vector3[] depthCal;
                if (System.IO.File.Exists(calibrationFullpath))
                {
                    var ply = PLYReader.LoadPLY(calibrationFullpath);
                    depthCal = ply.vertices.ToArray();
                }
                else
                {
                    //if (!ShowLive && !Record)
                    //{
                    //    Debug.LogError("Missing " + calibrationFullpath);
                    //}

                    PLYReader.CloudData ply = new PLYReader.CloudData(w * h);
                    depthCal = new Vector3[w * h];
#if USE_KINECTLIB

                    var kinectManager = KinectManager.Instance;
                    var sensorData = (kinectManager != null && kinectManager.IsInitialized()) ? kinectManager.GetSensorData(sensorIndex) : null;
                    DepthSensorBase sensor = (DepthSensorBase)sensorData.sensorInterface;



                    for (int y = 0, i = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++, i++)
                        {
                            Vector2 depthPos = new Vector2(x, y);
                            var coord3d = sensor.MapDepthPointToSpaceCoords(sensorData, depthPos, 1000);
                            ply.AddPoint(coord3d);
                            depthCal[i] = coord3d;

                        }
                    }
                    PLYReader.SavePLY(calibrationFullpath, ply);
#else
                            Debug.LogError("NEED KINECTLIB!!! or " + calibrationFullpath);
#endif
                }

                var depthCalN = w * h;
                s_depthCalibration = new Vector2[depthCalN];
                for (int i = 0; i < depthCalN; i++)
                {
                    s_depthCalibration[i] = depthCal[i];
                }

            }
            return s_depthCalibration;
        }

        public override CustomReflection GenerateCustomReflection()
		{
            var r = new CustomReflection();
            //r.Add("RenderAsLines", false, (val) => RenderAsLines = val, () => RenderAsLines);
            //r.Add("LineVertical", false, (val) => LineVertical = val, () => LineVertical);
            r.Add("HorisontalLines", false, (val) => HorisontalLines = val, () => HorisontalLines);
            r.Add("VerticalLines", false, (val) => VerticalLines = val, () => VerticalLines);
            r.Add("LineOffset", 1f, (val) => LineOffset = val, () => LineOffset);    
            r.Add("PointSize", 0.8f, (val) => PointSize = val, () => PointSize);
            r.Add("DepthToSize", 1.0f, (val) => DepthToSize = val, () => DepthToSize);
            r.Add("DepthToSizeRange", 0.6f, (val) => DepthToSizeRange = val, () => DepthToSizeRange);
            r.Add("DepthToSizeCurve", 0.5f, (val) => DepthToSizeCurve = val, () => DepthToSizeCurve);
            r.Add("PointCloudRenderStyle", 0, (val) => PointCloudRenderStyle = (RenderStyle) val, () => (int) PointCloudRenderStyle);
            r.Add("TrailFrames", 0f, (val) => TrailFrames = val, () => TrailFrames);
            r.Add("MinDepth", DepthCamNearLimit, (val) => MinDepth = val, () => MinDepth);
            r.Add("MaxDepth", DepthCamNearLimit + IntRangeInMM, (val) => MaxDepth = val, () => MaxDepth);
            r.Add("SkewBack", Vector2.zero, (val) => SkewBack = val, () => SkewBack);
            r.Add("FPS", 30f, (val) => FPS = val, () => FPS);
            r.Add("Portrait", false, (val) => Portrait = val, () => Portrait);
            r.Add("SelfieMirror", false, (val) => SelfieMirror = val, () => SelfieMirror);
            r.Add("PointCloudXSkip", 0, (val) => PointCloudXSkip = val, () => PointCloudXSkip);
            r.Add("PointCloudYSkip", 0, (val) => PointCloudYSkip = val, () => PointCloudYSkip);

            r.Add("Top", 0, (val) => Top = val, () => Top);
            r.Add("Bottom", 0, (val) => Bottom = val, () => Bottom);
            r.Add("Left", 0, (val) => Left = val, () => Left);
            r.Add("Right", 0, (val) => Right = val, () => Bottom);
            r.Add("TopPlane", new Vector3(0f, 2f, 0f), (val) => TopPlane = val, () => TopPlane);
            r.Add("BottomPlane", new Vector3(0f, 2f, 0f), (val) => BottomPlane = val, () => BottomPlane);
            r.Add("LeftPlane", new Vector3(0f, 2f, 0f), (val) => LeftPlane = val, () => LeftPlane);
            r.Add("RightPlane", new Vector3(0f, 2f, 0f), (val) => RightPlane = val, () => BottomPlane);


            // not strictly needed for rendering
            r.Add("ReduceStray", 1f, (val) => ReduceStray = val, () => ReduceStray);  
            r.Add("InfraToSize", 0f, (val) => InfraToSize = val, () => InfraToSize);  
            r.Add("InfraredScale", 10f, (val) => InfraredScale = val, () => InfraredScale);  
            r.Add("InfraredOffset", 0f, (val) => InfraredOffset = val, () => InfraredOffset);  
            r.Add("ColorAmount", 0f, (val) => ColorAmount = val, () => ColorAmount);  
            r.Add("InfraToColor", 0f, (val) => InfraToColor = val, () => InfraToColor); 

            return r;
		}

        /*
        void SaveData(System.IO.BinaryWriter w)
		{
            int N;
            var refl = GetCustomReflection();

            w.Write("PCS!");
            refl.SaveData(w);
            w.Write(_compressedW);
            w.Write(_compressedH);
            w.Write(_compressedWOrg);
            w.Write(_compressedHOrg);
            w.Write(_compressedXOffset);
            w.Write(_compressedYOffset);
            w.Write(_compressedFrames.Count);
            for(int i = 0; i < _compressedFrames.Count; i++)
			{
                var ar = _compressedFrames[i];
                N = ar.Length;
                w.Write(N);
                // possible to use unsafe and cast and to byte[] ? Write(byte[] buffer, int index, int count) 
                for(int j = 0; j < N; j++)
                    w.Write(ar[j]);
            }

            // Write compressed frame indicating what areas need to be drawn
            N = _compressedW * _compressedH;
            ushort[] used = new ushort[N];
            for (int i = 0; i < N; i++)
                used[i] = (ushort) ((_gridOptimizeBitmap[i >> 5] >> (i & 31)) & 1);
            List<uint> compressedOpt = new List<uint>();
            DiffRLE.Encode(null, used, compressedOpt);
            N = compressedOpt.Count;
            w.Write(N);
            for (int j = 0; j < N; j++)
                w.Write(compressedOpt[j]);

        }
        void LoadData(System.IO.BinaryReader r)
		{
            int N;
            var refl = GetCustomReflection();

            var magic = r.ReadString();
            if(magic != "PCS!")
			{
                Debug.LogError("File format error");
                return;
			}
            refl.LoadData(r);
            _compressedW = r.ReadInt32();
            _compressedH = r.ReadInt32();
            _compressedWOrg = r.ReadInt32();
            _compressedHOrg = r.ReadInt32();
            _compressedXOffset = r.ReadInt32();
            _compressedYOffset = r.ReadInt32();
            var frames = r.ReadInt32();
            _compressedFrames.Clear();
            for(int i = 0; i < frames; i++)
			{
                N = r.ReadInt32();
                var ar = new uint[N];
                // possible to use unsafe and cast and to byte[] ? Read(byte[] buffer, int index, int count)
                for(int j = 0; j < N; j++)
                    ar[j] = r.ReadUInt32();
                _compressedFrames.Add(ar);
            }

            ushort[] used = new ushort[_compressedW * _compressedH];
            _gridOptimizeBitmap = new uint[(_compressedW * _compressedH + 31) / 32];
            N = r.ReadInt32();
            var usedComp = new uint[N];
            for (int j = 0; j < N; j++)
                usedComp[j] = r.ReadUInt32();
            DiffRLE.Decode(used, usedComp);
            N = _compressedW * _compressedH;
            for (int i = 0; i < N; i++)
                if(used[i] != 0)
                    _gridOptimizeBitmap[i >> 5] |= (uint)(1 << (i & 31));



            ActiveCrop = new Vector2Int(0, frames - 1);
        }

        */


        private const int DepthBits = 11; // constant duplicated in TeaParticleShader.cginc
        private const int IntRangeInMM = (1<<DepthBits) - 1;
        private const int DepthCamNearLimit = 360; // only used to init MinDepth/MaxDepth

        void Start()
		{
            if (ScheduleLoadOnStart)
                LoadRecording = true;
		}

        private string GetPath()
		{
            return RecordingPath.Length == 0 ? Application.dataPath + "/../" : RecordingPath;
        }

#if USE_KINECTLIB
        private KinectManager _kinectManager = null;
        private KinectInterop.SensorData _sensorData = null;
        private DepthSensorBase _sensor = null;
#endif

        private ushort[] _depthData = null;
        private ComputeBuffer _depthDataBuf = null;

        private ushort[] _infraData = null;
        private ComputeBuffer _infraDataBuf = null;


        private Color32[] _infraredPlaybackBuffer;
        private Texture2D _infraredPlaybackTex;
        private List<ushort[]> _infraredRecorderFrames = new List<ushort[]>();
        private List<ushort[]> _depthRecorderFrames = new List<ushort[]>();
        private List<RenderTexture> _colorRecorderFrames = new List<RenderTexture>();


        public void SetCompressed(CompressedDepthStream data)
		{
            _compressed = data;
            ActiveCrop.x = 0;
            ActiveCrop.y = _compressed.Frames.Count;
            _dataAvailableForPlay = true;
        }
        private CompressedDepthStream _compressed = new CompressedDepthStream(); 
        /*
        private int _compressedW;
        private int _compressedH;
        private int _compressedWOrg;
        private int _compressedHOrg;
        private int _compressedXOffset;
        private int _compressedYOffset;
        private List<uint[]> _compressedFrames = new List<uint[]>();
        */

        private int _CurFrame = 0;


        private RenderTexture _colorTex = null;
        private RenderTexture _colorTex2 = null;

        private ulong _lastDepthFrameTime = 0;


        private int[] _averageDepthSum = null;
        private List<ushort[]> _averageDepthHistory = new List<ushort[]>();
        private int _averageDepthHistoryOffset = 0;
        private void CalcAverageDepth(ref ushort[] depth)
		{
            int N = depth.Length;
            if(_averageDepthSum == null || _averageDepthSum.Length != N || _averageDepthHistory.Count > averageDepthFrames)
			{
                _averageDepthSum = new int[N];
                _averageDepthHistory.Clear();
                _averageDepthHistoryOffset = 0;
            }

            ushort[] buf = null;
            if (_averageDepthHistory.Count < averageDepthFrames)
			{
                buf = new ushort[N];
                _averageDepthHistory.Add(buf);
			}
            else
			{
                buf = _averageDepthHistory[_averageDepthHistoryOffset];
                for (int i = 0; i < N; i++)
			    {
                    _averageDepthSum[i] -= buf[i];
                }
                _averageDepthHistoryOffset = (_averageDepthHistoryOffset + 1) % averageDepthFrames;
			}

            var divisor = _averageDepthHistory.Count;
            for (int i = 0; i < N; i++)
			{
                var d = depth[i];
                if (d < MinDepth || d > MaxDepth)
                    d = (ushort) MaxDepth;

                buf[i] = d;
                _averageDepthSum[i] += d;

                depth[i] = (ushort)(_averageDepthSum[i] / divisor);
            }
        }

        void OnEnable()
        {

        }


        private static System.Reflection.MethodInfo _setNativeDataMethod;
        private static object[] _setNativeDataArgs = new object[5];

        public static void SetComputeBufferData(ComputeBuffer computeBuffer, System.Array data, int elemCount, int elemSize, int destOffset = 0)
        {
            var pData = System.Runtime.InteropServices.GCHandle.Alloc(data, System.Runtime.InteropServices.GCHandleType.Pinned);
            SetComputeBufferDataI(computeBuffer, pData.AddrOfPinnedObject(), elemCount, elemSize, destOffset);
            pData.Free();
        }

        public static void SetComputeBufferDataI(ComputeBuffer computeBuffer, System.IntPtr dataPointer, int elemCount, int elemSize, int destOffset)
        {
            if (_setNativeDataMethod == null)
            {
                _setNativeDataMethod = typeof(ComputeBuffer).GetMethod("InternalSetNativeData",
                    System.Reflection.BindingFlags.InvokeMethod | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            }

            _setNativeDataArgs[0] = dataPointer;
            _setNativeDataArgs[1] = 0;      // source offset
            _setNativeDataArgs[2] = destOffset;      // buffer offset
            _setNativeDataArgs[3] = elemCount;
            _setNativeDataArgs[4] = elemSize;

            _setNativeDataMethod.Invoke(computeBuffer, _setNativeDataArgs);
        }


        void OnDisable()
        {
            _dataAvailableForPlay = false;

            if (_depthDataBuf != null)
            {
                _depthData = null;

                _depthDataBuf.Release();
                _depthDataBuf.Dispose();
                _depthDataBuf = null;
            }

            if (_infraDataBuf != null)
            {
                _infraData = null;

                _infraDataBuf.Release();
                _infraDataBuf.Dispose();
                _infraDataBuf = null;
            }            
            if (_colorTex)
            {
#if USE_KINECTLIB
                if (_sensor != null && _sensor.pointCloudColorTexture == _colorTex)
                    _sensor.pointCloudColorTexture = null;
#endif
                _colorTex.Release();
                _colorTex = null;
            }

            if (_colorTex2)
            {
                _colorTex2.Release();
                _colorTex2 = null;
            }

            if (_depthCalibrationBuf != null)
            {
                _depthCalibrationBuf.Release();
                _depthCalibrationBuf.Dispose();
                _depthCalibrationBuf = null;
            }

#if USE_KINECTLIB
            _kinectManager = null;
#endif
            _lastDepthFrameTime = 0;
        }

        bool _dataAvailableForPlay = false;
        int _depthW;
        int _depthH;
        int _depthWDS;
        int _depthHDS;
        int _depthXOffsetDS;
        int _depthYOffsetDS;

        private string GetSingleFilename()
		{
            return GetPath() + RecordingName + ".pcs";
		}
        private string GetSequenceFilename(int i, bool color = false)
		{
            if (color)
                return GetPath() + RecordingName +         i.ToString("D4") + ".png";
            return     GetPath() + RecordingName + "_D_" + i.ToString("D4") + ".png";
		}
        
        void LoadRaw()
		{
            var depthSize = -1;
            var tmpDepth = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var tmpColor = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            ActiveCrop = Vector2Int.zero;

            if(!AllowPartialLoad && System.IO.File.Exists(GetSequenceFilename(RecorderMaxFrames)))
			{
                Debug.LogError("Load Failed, RecorderMaxFrames too low");
                return;
			}

            for (int i = 0; i < RecorderMaxFrames; i++)
            {
                var depthName = GetSequenceFilename(i);
                if(!System.IO.File.Exists(depthName))
				{
                    break;
				}                    
                byte[] dbytes = System.IO.File.ReadAllBytes(depthName);
                tmpDepth.LoadImage(dbytes);
                if(i == 0)
				{
                    _depthW = tmpDepth.width;
                    _depthH = tmpDepth.height;
                    depthSize = _depthW * _depthH;
                    ForceSize(_depthRecorderFrames   , RecorderMaxFrames, depthSize);
                    ForceSize(_infraredRecorderFrames, RecorderMaxFrames, depthSize);
                }
                var tmpDepthCol = tmpDepth.GetPixels32();
                var dst = _depthRecorderFrames[i];
                var dstIR = _infraredRecorderFrames[i];
                for (int j = 0; j < depthSize; j++)
                {
                    dst[j] = (ushort)(((int)tmpDepthCol[j].r) | (((int)tmpDepthCol[j].g) << 8));
                    dstIR[j] = (ushort)(((int)tmpDepthCol[j].b) | (((int)tmpDepthCol[j].a) << 8));
                }

                byte[] cbytes = System.IO.File.ReadAllBytes(GetSequenceFilename(i,true));
                tmpColor.LoadImage(cbytes);
                if(i == 0)
                    ForceSize(_colorRecorderFrames, RecorderMaxFrames, tmpColor.width, tmpColor.height);
                Graphics.Blit(tmpColor, _colorRecorderFrames[i]);

                if (i != 0) // ActiveCrop defines beginning frame and end frame, so if there is 1 frame, beginning AND end should be 0
                    ActiveCrop.y++;
            }
            _dataAvailableForPlay = true;
		}
        
        void SaveRaw()
		{
            var depthSize = _depthW * _depthH; // _sensorData.depthImageWidth * _sensorData.depthImageHeight;
            var tmpDepthCol = new Color32[depthSize];
            var tmpDepth = new Texture2D(_depthW, _depthH, TextureFormat.RGBA32, false);

            var tmpColor = new Texture2D(_colorRecorderFrames[0].width, _colorRecorderFrames[0].height, TextureFormat.RGBA32, false);
            for (int i = 0; i < RecorderMaxFrames; i++)
            {
                var src = _depthRecorderFrames[i];
                var srcIR = _infraredRecorderFrames[i];
                for (int j = 0; j < depthSize; j++) tmpDepthCol[j] = new Color32((byte)(src[j] & 255), (byte)(src[j] >> 8), (byte)(srcIR[j] & 255), (byte)(srcIR[j] >> 8));
                tmpDepth.SetPixels32(tmpDepthCol);
                byte[] dbytes = ImageConversion.EncodeToPNG(tmpDepth);
                System.IO.File.WriteAllBytes(GetSequenceFilename(i), dbytes);

                Graphics.SetRenderTarget(_colorRecorderFrames[i]);
                tmpColor.ReadPixels(new Rect(0, 0, _colorRecorderFrames[i].width, _colorRecorderFrames[i].height), 0, 0);
                tmpColor.Apply();
                byte[] cbytes = ImageConversion.EncodeToPNG(tmpColor);
                System.IO.File.WriteAllBytes(GetSequenceFilename(i, true), cbytes);
            }

        }
        void LoadAsSingleFile()
        {
            byte[] dbytes = System.IO.File.ReadAllBytes(GetSingleFilename());
            using (System.IO.MemoryStream stream = new System.IO.MemoryStream(dbytes))
            {
                var r = new System.IO.BinaryReader(stream);
                _compressed.LoadData(r);
                GetCustomReflection().RecallFromKeyValueStore(_compressed.MetaData);
                ActiveCrop.x = 0;
                ActiveCrop.y = _compressed.Frames.Count;
            }
            _dataAvailableForPlay = true;
        }

        void SaveAsSingleFile()
        {
            using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
            {
                var w = new System.IO.BinaryWriter(stream);
                _compressed.MetaData = GetCustomReflection().StoreToKeyValueStore();
                _compressed.SaveData(w);
                w.Flush();
                var ar = stream.ToArray();
                var filepath = GetSingleFilename();
                Debug.Log("Saved " + filepath + " " + (ar.Length / (1024)) + " kb");
                System.IO.File.WriteAllBytes(filepath, ar);
            }
        }

        void ProcessAndCompressAllNow()
		{
            _compressed.W = _depthWDS;
            _compressed.H = _depthHDS;
            _compressed.WOrg = _depthW;
            _compressed.HOrg = _depthH;
            _compressed.XOffset = _depthXOffsetDS;
            _compressed.YOffset = _depthYOffsetDS;

            int n = _depthDataDS.Length;
            var gridOptimizeBitmap  = new uint[(n + 31) / 32];
            ushort[] minD = new ushort[n];
            ushort[] maxD = new ushort[n];
            for (int j = 0; j < n; j++)
            {
                minD[j] = 65535;
            }

            _compressed.Frames.Clear();
            for(int i = ActiveCrop.x; i <= ActiveCrop.y; i++)
			{
                ProcessAndCompress(_depthRecorderFrames[i], _infraredRecorderFrames[i], true, false);
                _compressed.Frames.Add(_compressedStream.ToArray());
                for(int j = 0; j < n; j++)
				{
                    var v = _depthDataDS[j];
                    if (v < minD[j])
                        minD[j] = v;
                    if (v > maxD[j])
                        maxD[j] = v;
                }
                for (int j = 0; j < n; j++)
                {
                    if(maxD[j] != 0)
                        gridOptimizeBitmap[j >> 5] |= (uint) (1 << (j & 31));
                }

            }
            _compressed.GridOptimizeBitmap = gridOptimizeBitmap;
        }

        void Update()
        {

            if (ProcessAndCompressAll)
			{
                ProcessAndCompressAll = false;
                ProcessAndCompressAllNow();
            }

            if (LoadRecording)
            {
                LoadRecording = false;
                if(UseRawFormat)
                    LoadRaw();
                else
                    LoadAsSingleFile();
            }

            if (!(ShowLive || _dataAvailableForPlay))
                return;

            ValidateBuffersAndFormats();


            if (SaveRecording)
            {
                SaveRecording = false;
                if(UseRawFormat)
                    SaveRaw();
                else
                    SaveAsSingleFile();
            }


            HandleRecordAndPlay();
        }

#if USE_KINECTLIB

        static Vector2Int GetPointCloudTexResolution(DepthSensorBase sensorInt, KinectInterop.SensorData sensorData)
        {
            Vector2Int texRes = Vector2Int.zero;

            switch (sensorInt.pointCloudResolution)
            {
                case DepthSensorBase.PointCloudResolution.DepthCameraResolution:
                    texRes = new Vector2Int(sensorData.depthImageWidth, sensorData.depthImageHeight);
                    break;

                case DepthSensorBase.PointCloudResolution.ColorCameraResolution:
                    texRes = new Vector2Int(sensorData.colorImageWidth, sensorData.colorImageHeight);
                    break;
            }

            if (texRes == Vector2Int.zero)
            {
                throw new System.Exception("Unsupported point cloud resolution: " + sensorInt.pointCloudResolution + " or the respective image is not available.");
            }

            return texRes;
        }
#endif


        // creates new render texture with the given dimensions and format
        public static RenderTexture CreateRenderTexture(RenderTexture currentTex, int width, int height, RenderTextureFormat texFormat = RenderTextureFormat.Default)
        {
            if (currentTex != null)
            {
                if (RenderTexture.active == currentTex)
                    RenderTexture.active = null;
                currentTex.Release();
                //UnityEngine.Object.Destroy(currentTex);
            }

            RenderTexture renderTex = new RenderTexture(width, height, 0, texFormat);
            renderTex.wrapMode = TextureWrapMode.Clamp;
            renderTex.filterMode = FilterMode.Point;
            renderTex.enableRandomWrite = true;

            return renderTex;
        }


        // creates new compute buffer with the given length and stride
        public static ComputeBuffer CreateComputeBuffer(ComputeBuffer currentBuf, int bufLen, int bufStride)
        {
            if (currentBuf != null)
            {
                currentBuf.Release();
                currentBuf.Dispose();
            }

            ComputeBuffer computeBuf = new ComputeBuffer(bufLen, bufStride);
            return computeBuf;
        }

        private static void CopyBuffer(ushort[] src, ushort[] dst)
		{
#if USE_KINECTLIB
            KinectInterop.CopyBytes(src, sizeof(ushort), dst, sizeof(ushort));
#else
            var N = Mathf.Min(src.Length, dst.Length);
            for (int i = 0; i < N; i++)
                dst[i] = src[i];
#endif
        }


        private void ForceSize(List<RenderTexture> textures, int length, int w, int h)
		{
            if(length != 0 && textures.Count == length && w == textures[0].width && h == textures[0].height)
                return;
            
            foreach(var tex in textures)
			{
                tex.Release();
                //Destroy(tex);
       		}

            textures.Clear();
            for(int i = 0; i < length; i++)
			{
                textures.Add(CreateRenderTexture(null, w, h, RenderTextureFormat.ARGB32));
            }

        }
        private void ForceSize(List<ushort[]> textures, int length, int pixelCount)
		{
            if(length != 0 && textures.Count == length && pixelCount == textures[0].Length)
                return;

            textures.Clear();
            for(int i = 0; i < length; i++)
			{
                textures.Add(new ushort[pixelCount]);
            }
        }

        private int GetTargetYMin() { return (int)((_depthH - 1) * Top); }
        private int GetTargetYMax() { return (int)((_depthH - 1) * (1f - Bottom)); }
        private int GetTargetXMin() { return (int)((_depthW - 1) * Left); }
        private int GetTargetXMax() { return (int)((_depthW - 1) * (1f - Right)); }

        private bool UsingPreProcessed()
		{
            return (!ShowLive && !Record && !UseRawFormat);
        }
        private bool UsingProcessed()
		{
            return UsingPreProcessed() || ProcessDepth;
        }
        private void ValidateBuffersAndFormats()
        {
            if(OverrideMaterial != null)
			{
                _pointCloudMat = OverrideMaterial;
            }
            else
			{
                //var shader = OverrideShader != null ? OverrideShader : GetShader(PointCloudRenderStyle != RenderStyle.OpaqueWithDepth);
                var shader = GetShader(PointCloudRenderStyle != RenderStyle.OpaqueWithDepth);
                if(_pointCloudMat == null || _pointCloudMat.shader != shader)
			    {
                    _pointCloudMat = new Material(shader);
			    }
			}


            if(PointCloud && _pointCloudTarget == null)
			{
                var go = new GameObject("DepthStreamRenderer");
                go.transform.SetParent(transform, false);
                _pointCloudTarget = go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>();
			}


#if USE_KINECTLIB
            if (_kinectManager == null && (ShowLive || Record))
            {
                Debug.Log("KinectRecorder LazyInit()");
                _kinectManager = KinectManager.Instance;
                _sensorData = (_kinectManager != null && _kinectManager.IsInitialized()) ? _kinectManager.GetSensorData(sensorIndex) : null;
            }
#endif

            Vector2Int colorRes = Vector2Int.zero;

            if (Record || ShowLive)
			{
#if USE_KINECTLIB
                _sensor = (DepthSensorBase)_sensorData.sensorInterface;
                colorRes = GetPointCloudTexResolution(_sensor, _sensorData);
                _depthW = _sensorData.depthImageWidth;
                _depthH = _sensorData.depthImageHeight;
#else   
                Debug.LogError("define USE_KINECTLIB");
#endif
            }
            else if(_colorRecorderFrames.Count > 0)
			{
                colorRes = new Vector2Int(_colorRecorderFrames[0].width, _colorRecorderFrames[0].height);
            }

            if(UsingPreProcessed())
			{
                _depthW = _compressed.WOrg;
                _depthH = _compressed.HOrg;
                _depthWDS = _compressed.W;
                _depthHDS = _compressed.H;
                _depthXOffsetDS = _compressed.XOffset;
                _depthYOffsetDS = _compressed.YOffset;
            }
            else if(ProcessDepth)
			{
                var xstep = PointCloudXSkip + 1;
                var ystep = PointCloudYSkip + 1;
                _depthWDS = ((GetTargetXMax() + 1 - GetTargetXMin()) + xstep - 1) / xstep;
                _depthHDS = ((GetTargetYMax() + 1 - GetTargetYMin()) + ystep - 1) / ystep;
                _depthXOffsetDS = GetTargetXMin();
                _depthYOffsetDS = GetTargetYMin();
                if(_depthWDS < 1 || _depthHDS < 1)
				{
                    Debug.LogError("Too small!");
                    _depthWDS = _depthW;
                    _depthHDS = _depthH;
                    _depthXOffsetDS = 0;
                    _depthYOffsetDS = 0;
				}
            }
			else
			{
                _depthWDS = _depthW;
                _depthHDS = _depthH;
                _depthXOffsetDS = 0;
                _depthYOffsetDS = 0;
			}

            if((_colorTex == null || _colorTex.width != colorRes.x || _colorTex.height != colorRes.y)   && colorRes.x > 0 && colorRes.y > 0)
			{
                _colorTex = CreateRenderTexture(_colorTex, colorRes.x, colorRes.y, RenderTextureFormat.ARGB32);
                _colorTex2 = CreateRenderTexture(_colorTex2, colorRes.x, colorRes.y, RenderTextureFormat.ARGB32);
            }

            int depthImageLength = _depthW * _depthH;
            if (_depthData == null || _depthData.Length != depthImageLength)
            {
                _depthData = new ushort[depthImageLength];
            }


            if (_infraredPlaybackTex == null || _infraredPlaybackTex.width != _depthW || _infraredPlaybackTex.height != _depthH)
            {
                _infraredPlaybackBuffer = new Color32[depthImageLength];
                _infraredPlaybackTex = new Texture2D(_depthW, _depthH, TextureFormat.RGBA32, false);
            }

            int depthImageLengthDS = _depthWDS * _depthHDS;
            if (_depthDataDS == null || _depthDataDS.Length != depthImageLengthDS)
			{
                _depthDataDS = new ushort[depthImageLengthDS];
                _infraDataDS = new ushort[depthImageLengthDS];
			}

            int computeSize = depthImageLengthDS / 2;
            var trailN = Mathf.FloorToInt(TrailFrames + 1f);
            int computeSizeWithGhost = trailN * depthImageLengthDS / 2;
            if(_depthDataBuf == null || _depthDataBuf.count != computeSizeWithGhost)
                _depthDataBuf = CreateComputeBuffer(_depthDataBuf, computeSizeWithGhost, sizeof(uint));

            if(_infraDataBuf == null || _infraDataBuf.count != computeSize)
                _infraDataBuf = CreateComputeBuffer(_infraDataBuf, computeSize, sizeof(uint));

            if (Record || ShowLive)
			{
#if USE_KINECTLIB
                _sensor.pointCloudColorTexture = _colorTex;
#endif
                if (Record)
				{
                    ForceSize(_colorRecorderFrames, RecorderMaxFrames, _colorTex.width, _colorTex.height);
                    ForceSize(_depthRecorderFrames, RecorderMaxFrames, _depthData.Length);
                    ForceSize(_infraredRecorderFrames, RecorderMaxFrames, _depthData.Length);
				}
			}
        }

         
        private void GeneratePreview(ushort[] infra, ushort[] depth, RenderTexture dest)
		{
            var muli = (uint) (65535f / InfraredPreviewDiv);
            var muld = (uint) (65535f * DepthPreviewMul);
            var addd = (uint) (255 * 65535f * DepthPreviewAdd);
            if (PreviewInfra && PreviewDepth)
            {
                for (int i = 0; i < _infraredPlaybackBuffer.Length; i++)
                {
                    uint infr = infra[i];
                    infr = (infr * muli) >> 16;
                    if (infr > 255) infr = 255;

                    uint dep = depth[i];
                    dep = ((dep) * muld - addd) >> 16;
                    //if (dep > 255) dep = 0;
                    dep = 255 - dep;

                    _infraredPlaybackBuffer[i] = new Color32((byte)infr, (byte)dep, (byte)infr, 255);
                }
            }
            else if (PreviewInfra)
            {
                for (int i = 0; i < _infraredPlaybackBuffer.Length; i++)
                {
                    uint infr = infra[i];
                    infr = (infr * muli) >> 16;
                    if (infr > 255) infr = 255;

                    _infraredPlaybackBuffer[i] = new Color32((byte)infr, (byte)infr, (byte)infr, 255);
                }
            }
            else if(PreviewDepth)
            {
                var errCol = new Color32(50, 0, 0, 255);
                for (int i = 0; i < _infraredPlaybackBuffer.Length; i++)
                {
                    uint dep = depth[i];
                    if(dep == 0)
                        _infraredPlaybackBuffer[i] = errCol;
                    else
					{
                        dep = ((dep) * muld - addd) >> 16;
                        //if (dep > 255) dep = 0;
                        dep = 255 - dep;

                        _infraredPlaybackBuffer[i] = new Color32((byte)dep, (byte)dep, (byte)dep, 255);
					}
                }
            }

            //var handle = System.Runtime.InteropServices.GCHandle.Alloc(_infraredPlaybackBuffer, System.Runtime.InteropServices.GCHandleType.Pinned);
            //_infraredPlaybackTex.LoadRawTextureData(handle.AddrOfPinnedObject(), _infraredPlaybackBuffer.Length * 4);
            //handle.Free();

            _infraredPlaybackTex.SetPixels32(_infraredPlaybackBuffer);
            _infraredPlaybackTex.Apply(false);
            Graphics.CopyTexture(_infraredPlaybackTex, dest);
        }

        private ushort[] GetRawInfraredMap(int sensorIndex)
		{
#if USE_KINECTLIB
            if (_kinectManager == null)
                return null;
            return _kinectManager.GetRawInfraredMap(sensorIndex);
#else
            return null;
#endif
        }

        private int ClipLength()
		{
            return ActiveCrop.y - ActiveCrop.x + 1;
        }

        private void AdvanceFrame()
		{
            var localFrame = Mathf.Max(0, _CurFrame - ActiveCrop.x);
            localFrame = (localFrame + 1) % ClipLength();
            FramePosition = ((float)localFrame) / ClipLength();
            _prevFramePos = FramePosition;
            _CurFrame = localFrame + ActiveCrop.x;
        }

        private bool _prevRecord = false;
        private Vector2[] _depthCalibrationDS;
        private ComputeBuffer _depthCalibrationBuf;
        private Mesh _cloudMesh;
        private int _cloudMeshHash = -1;

        private List<uint> _compressedStream = new List<uint>();
        private ushort[] _prevDepthData;
        private ushort[] _depthDataDS;
        private ushort[] _infraDataDS;
        private static float abs(float x) { return x < 0f ? -x : x; }
        private static float max(float a, float b) { return a > b ? a : b; }
        private static float clamp01(float x) { return x < 0 ? 0f : (x > 1f ? 1f : x); }
        private static float linstep(float a, float b, float x) { return clamp01((x - a) / (b - a)); }

        private void ProcessDebugNearFar(ushort[] dest)
		{
            bool alternate = (_frameCounter & 1) == 0;
            var n = dest.Length;
            for(int i = 0; i < n; i++)
			{
                if(dest[i] == 0)
				{
                    var dv = (ushort)(alternate ? 1 : (1 << DepthBits) - 1);
                    dv |= (ushort)(31 << DepthBits);
                    dest[i] = dv;
                }
			}
        }

        private void ProcessAndCompress(ushort[] depthÌnput, ushort[] infraInput, bool generateCompressedFrame, bool testCompressedFrame)
        {
            var dData = _depthDataDS;
            //ushort[] iData;

            var W = _depthWDS;
            var H = _depthHDS;
            var N = W*H;

            ushort compMaxDepth = (ushort)MaxDepth;
            ushort compMinDepth = (ushort)MinDepth;
            float scale = ((float)IntRangeInMM) / (MaxDepth - MinDepth);
            //if(ProcessDepth)
            {
                //dData = _depthDataDS;
                //iData = _infraDataDS;
                int xstep = PointCloudXSkip + 1;
                int ystep = PointCloudYSkip + 1;
                int xsteph = xstep/2;
                int ysteph = ystep/2;
                float infraredTransform_x = InfraredScale / 65536f;
                float infraredTransform_y = InfraredOffset;
                uint random = 1337;
                int iTop = (int)(_depthH * TopPlane.z);
                int iBot = (int)(_depthH * (1f- BottomPlane.z));
                int iLef = (int)(_depthW * LeftPlane.z);
                int iRig = (int)(_depthW * (1f- RightPlane.z));
                int iTopD = (int)(1000f * TopPlane.y);
                int iBotD = (int)(1000f * BottomPlane.y);
                int iLefD = (int)(1000f * LeftPlane.y);
                int iRigD = (int)(1000f * RightPlane.y);
                float iTopS = TopPlane.x;
                float iBotS = BottomPlane.x;
                float iLefS = LeftPlane.x;
                float iRigS = RightPlane.x;
                bool doSkew = SkewBack.x != 0 || SkewBack.y != 0;
                //float int2U = 1f / _depthW;
                //float int2V = 1f / _depthH;
                float int2outU = 1f / _depthWDS;
                float int2outV = 1f / _depthHDS;
                float skewX = SkewBack.x * 1000f;
                float skewY = SkewBack.y * 1000f;
                //Debug.Log(" " + W + " " + H + "   " + _depthW + " " + _depthH);
                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        int intx = x * xstep + _depthXOffsetDS;
                        int inty = y * ystep + _depthYOffsetDS;
                        int isrc = intx + inty * _depthW;
                        int idst = x + y * W;
                        var dv = depthÌnput[isrc];

                        //random = random * 1103515245 + 12345;
                        //dv = (ushort) (random % 5000);

                        var iv = infraInput[isrc];
                        if(FilterIR && x > xsteph && x < W - xsteph - 1 && y > ysteph && y < H - ysteph - 1)
                        {
                            uint ivSum = 0;
                            for (int yy = -ysteph; yy <= ysteph; yy++)
                            {
                                for (int xx = -xsteph; xx <= xsteph; xx++)
                                {
                                    ivSum += infraInput[isrc + xx + yy * _depthW];
                                }
                            }
                            iv = (ushort) (ivSum / ((xsteph * 2 + 1) * (ysteph * 2 + 1)));

                        }


                        bool skip = true;

                        if(dv != 0)
                        {
                            skip = false;

                            if (iTop - inty + ((int)((dv - iTopD) * iTopS)) > 0)
                                skip = true;

                            if (-(iBot - inty) + ((int)((dv - iBotD) * iBotS)) > 0)
                                skip = true;

                            if (iLef - intx + ((int)((dv - iLefD) * iLefS)) > 0)
                                skip = true;

                            if (-(iRig - intx) + ((int)((dv - iRigD) * iRigS)) > 0)
                                skip = true;

                            if (doSkew)
                                // it is possible to calculate/compensate skew center point difference for a cropped rect, 
                                // but nicer to keep model simpler (skew center is always at destination center) even if that 
                                // means you might have to re-adjust skew if you change crop rect

                                //dv = (ushort) Mathf.Max(0f, (dv - (intx * int2U - 0.5f) * skewX - (inty * int2V - 0.5f) * skewY));  

                                dv = (ushort) Mathf.Max(0f, (dv - (x * int2outU - 0.5f) * skewX - (y * int2outV - 0.5f) * skewY));

                            if (dv > compMaxDepth || dv < compMinDepth)
                                skip = true;


                            //if (intx < iLef || intx > iRig || inty < iTop || inty > iBot)
                            //    skip = true;
                        }

                        if (skip)
                        { 
                            dv = 0;
                        }
                        else
                        {
                            float sizeMul = 1f / 3f;

                            if (InfraToSize != 0f)
                            {
                                float infraRed = iv * infraredTransform_x + infraredTransform_y;
                                infraRed = infraRed < 0 ? 0f : (infraRed > 1f ? 1f : infraRed);
                                sizeMul = (1f / 3f) + InfraToSize * (infraRed - (1f / 3f));
                            }

                            if (ReduceStray != 0f && x > 1 && x < W - 1 && y > 1 && y < H - 1)
                            {
                                //float curd = dv * 0.001f; // cannot reuse cos of skew support
                                float curd = depthÌnput[isrc] * 0.001f;
                                float ddx = abs(depthÌnput[isrc + 1] * 0.001f - curd);
                                float ddy = abs(depthÌnput[isrc - 1] * 0.001f - curd);
                                float ddz = abs(depthÌnput[isrc + _depthW] * 0.001f - curd);
                                float ddw = abs(depthÌnput[isrc - _depthW] * 0.001f - curd);
                                float maxDiff = max(max(ddx, ddy), max(ddz, ddw));
                                maxDiff *= 250f * 0.75f / curd;
                                float reduceStray = linstep(0f, 4f, 4f - maxDiff);
                                sizeMul *= 1f + ReduceStray * (reduceStray - 1f);
                            }

                            // round
                            //uint isize = (uint) (size * 31f + 0.5f);

                            // dither 
                            random = random * 1103515245 + 12345;
                            float frnd = (random & 4095) * (1f / 4096f);
                            frnd = 0.5f + NoiseDither * (frnd - 0.5f);
                            uint isize = (uint) (sizeMul * 31f + frnd);


                            dv -= compMinDepth;
                            if (scale != 1f)
                                dv = (ushort) (dv * scale);

                            dv  = (ushort) (dv & ((1 << DepthBits) - 1));
                            dv |= (ushort) (isize << DepthBits);

                        }

                        dData[idst] = dv;

                        //iData[idst] = iv;
                    }
                }
            }


            //ushort compRelaxNear = (ushort)CompRelaxNear;
            //ushort compDeadThreshold = (ushort)CompDeadThreshold;
            var frameC = Time.frameCount;

#if USE_UNSAFE
            unsafe
            {
                fixed (ushort* /*prevDepthData = _prevDepthData,*/
            depthData = dData)
                            {
#else
                {
                    {
                        //ushort[] prevDepthData = _prevDepthData;
                        ushort[] depthData = dData;
#endif
                        /*
                        for (int i = 0; i < N; i++)
                        {
                            var v = depthData[i];
                            if (v > compMaxDepth || v < compMinDepth)
                                v = 0;

                            var prevV = prevDepthData[i];
                            var diff = v - prevV;
                            if(diff < 0) diff = -diff; // diff = abs(diff)

                            if (diff < compDeadThreshold)
                                v = prevV;

                            depthData[i] = (ushort) ((v & ((1 << 11) - 1)) | ;
                        }
*/
                        /*
                        for (int y = 0; y < H; y++)
                        {
                            for (int x = (y + frameC) & 1; x < W; x += 2)
                            {
                                var i = x + y * W;
                                var prevV = prevDepthData[i];
                                var v = depthData[i];

                                var diff = v - prevV;

                                if(diff < 0) diff = -diff; // diff = abs(diff)

                                if (diff < compRelaxNear)
                                {
                                    if (diff < compDeadThreshold)
                                        v = prevV;
                                    else
                                        v = (ushort)((prevV + v) >> 1);
                                }
                                depthData[i] = v;
                            }
                        }*/

                }
            }

            if (generateCompressedFrame)
            {
                _compressedStream.Clear();
                DiffRLE.Encode(null, dData, _compressedStream, CompTolerance);
                if(testCompressedFrame)
				{
                    DiffRLE.Decode(dData, _compressedStream);
                    if (Time.frameCount % 30 == 0)
                        CompressionRate = "" + ((int)(-100f + 100f * (N) / (_compressedStream.Count * 2f))) + "% (" + (_compressedStream.Count * 4 / 1024) + " kb/frame)";
				}
            }
        }


        private float _frameFraction = 0f;
        private int _lastFrameValid = -1;
        private int _frameCounter = -1;

        private float _curSize = -1;
        private float _curIntensity = 1f;
        public void SetIntensity(float intensity)
        {
            _curIntensity = intensity;
        }
        public void SetFrame(float frame)
        {
            Debug.Log("~!!!!!!!!!!!!!!!!!!!!!!! " + frame);
            _CurFrame = (((int)(frame + 0.5f)) % ClipLength()) + ActiveCrop.x;
        }
        private void EnableRenderers(bool enable)
		{
            _pointCloudTarget.gameObject.SetActive(enable);
            if (PointCloudExtraTarget != null)
                PointCloudExtraTarget.gameObject.SetActive(enable);
		}
        
        private void HandleRecordAndPlay()
        {
            bool newFrame = false;

            int trailN = 1, trailWriteID = 0;

            if(_curIntensity <= 0f)
			{
                EnableRenderers(false);
                return;
			}
            EnableRenderers(true);

            if (Record || ShowLive)
			{
#if USE_KINECTLIB
                newFrame = _lastDepthFrameTime != _sensorData.lastDepthFrameTime;
#endif
            }
            else
			{
                if(PlayRecording)
				{
                    _frameFraction += Mathf.Min(1/15f, Time.deltaTime) * FPS;
                    float frames = Mathf.Floor(_frameFraction);
                    _frameFraction -= frames;
                    while(frames > 0.5f)
				    {
                        AdvanceFrame();
                        frames--;
				    }
				}
				else
				{
                    _frameFraction = 0;
				}

                if (_prevFramePos != FramePosition) // detect if frame position was changed from outside (also if ui-slider was dragged in inspector)
                {
                    _CurFrame = Mathf.FloorToInt(FramePosition * ClipLength() * 0.999999f) + ActiveCrop.x;
                }

                newFrame = _CurFrame != _lastFrameValid;
            }

            if(newFrame)
			{
                _frameCounter++;
			}

            if(TrailFrames != 0 && PointCloud && _pointCloudMat != null && _pointCloudTarget != null)
			{
                trailN = Mathf.FloorToInt(TrailFrames + 1f);
                trailWriteID = _frameCounter % trailN;
                _pointCloudMat.SetVector("_GhostFrames", new Vector4((trailN - trailWriteID - _frameFraction) / trailN, 1f / trailN, trailWriteID, _depthWDS * _depthHDS));
			}
            else
			{
                _pointCloudMat.SetVector("_GhostFrames", new Vector4(1f, 1f, 0f, _depthWDS * _depthHDS));
			}


            if (!newFrame)
                return;


            if (newFrame)
            {
#if USE_KINECTLIB
                if (Record || ShowLive)
                    _lastDepthFrameTime = _sensorData.lastDepthFrameTime;
#endif
                if (Record && !_prevRecord)
				{
                    // start recording
                    _CurFrame = 0;
                    ActiveCrop.x = 0;
				}

                if (_CurFrame < ActiveCrop.x || _CurFrame > ActiveCrop.y)
                    _CurFrame = ActiveCrop.x;

                if (PlayRecording && Record)
                {
                    _CurFrame = 0;
                    PlayRecording = false;
                }


                if (PlayRecording || !ShowLive)
                {
                    ShowLive = false;
                    if(UseRawFormat)
					{
                        if (PointCloud || PreviewDepth)
                            CopyBuffer(_depthRecorderFrames[_CurFrame], _depthData);

                        _infraData = _infraredRecorderFrames[_CurFrame];

                        if (PreviewInfra || PreviewDepth)
                        {
                            GeneratePreview(_infraData, _depthData, _colorTex2);
                        }
                        else
                        {
                            Graphics.CopyTexture(_colorRecorderFrames[_CurFrame], _colorTex2);
                        }
					}

                }
                else
                {
#if USE_KINECTLIB
                    if(PointCloud || PreviewDepth)
                        KinectInterop.CopyBytes(_sensorData.depthImage, sizeof(ushort), _depthData, sizeof(ushort));
#endif
                    //_infraData = Record || PreviewInfra ? GetRawInfraredMap(sensorIndex) : null;
                    _infraData = GetRawInfraredMap(sensorIndex);

                    if(PreviewInfra || PreviewDepth)
					{
                        GeneratePreview(_infraData, _depthData, _colorTex2);
                    }
                    else
					{
                        Graphics.CopyTexture(_colorTex, _colorTex2);
					}

#if USE_KINECTLIB
                    if (Record)
                    {
                        _dataAvailableForPlay = true;
                        KinectInterop.CopyBytes(_infraData            , sizeof(ushort), _infraredRecorderFrames[_CurFrame], sizeof(ushort));
                        KinectInterop.CopyBytes(_sensorData.depthImage, sizeof(ushort), _depthRecorderFrames[_CurFrame], sizeof(ushort));
                        Graphics.CopyTexture(_colorTex, _colorRecorderFrames[_CurFrame]);
                        ActiveCrop.y = Mathf.Min(_CurFrame + 1, RecorderMaxFrames - 1);
                        AdvanceFrame();
                        if (_CurFrame == 0)
                        {
                            Record = false;
                        }
                    }
#endif
                }
                //CurFrame = _recorderCurFrame;
                _prevRecord = Record;
                _lastFrameValid = Record || ShowLive ? -1 : _CurFrame;

                if(averageDepthFrames > 0)
				{
                    CalcAverageDepth(ref _depthData);
				}


                if (debugView != null)
				{
                    debugView.mainTexture = _colorTex2;
                }

                if(PointCloud && _pointCloudMat != null && _pointCloudTarget != null)
				{
                    var dData = _depthData;
                    //var iData = _infraData;

                    if (UseRawFormat)
                    {
                        if (ProcessDepth)
                        {
                            ProcessAndCompress(_depthData, _infraData, CompressTest, CompressTest);
                            dData = _depthDataDS;
                        }
                    }
                    else
                    {
                        int index = _CurFrame - ActiveCrop.x;
                        if (index < _compressed.Frames.Count)
                            DiffRLE.Decode(dData, _compressed.Frames[index]);
                        else
                            Debug.LogWarning("No compressed data loaded or generated (use RAW format instead?)");
                    }

                    if(DebugNearFar)
					{
                        ProcessDebugNearFar(dData);
					}

                    if(UsingProcessed())
                        _pointCloudMat.EnableKeyword("USE_ENCODE_SIZE_IN_DEPTH");
                    else 
                        _pointCloudMat.DisableKeyword("USE_ENCODE_SIZE_IN_DEPTH");


                    int uintBufLen = _depthWDS * _depthHDS / 2; // hlsl don't support 16 bit ints

                    Vector4 lineOffset = new Vector4(LineOffset / _depthWDS, LineOffset / _depthHDS, 0f, 0f);
                        //(LineVertical ? 0f : 1f) * LineOffset / _depthWDS,
                        //(LineVertical ? 1f : 0f) * LineOffset / _depthHDS, 0f, 0f);

                    if (!UsingProcessed())
					{
                        lineOffset.x *= (PointCloudXSkip + 1);
                        lineOffset.y *= (PointCloudYSkip + 1);
					}

                    SetComputeBufferData(_depthDataBuf, dData, uintBufLen, sizeof(uint), trailWriteID * _depthWDS * _depthHDS / 2);
                    //SetComputeBufferData(_depthDataBuf, dData, uintBufLen, sizeof(uint));
                    _pointCloudMat.SetBuffer("_DepthMap", _depthDataBuf);
                    _pointCloudMat.SetVector("_DepthMapTexelSize", new Vector4(1f / _depthWDS, 1f / _depthHDS, _depthWDS, _depthHDS));
                      

                    _pointCloudMat.SetVector("_SecondLinePointOffset", lineOffset);
                    
                    //    PointCloudMat.SetVector("_SecondLinePointOffset", new Vector4(
                    //            (1f - vertMul) * LineOffset * (PointCloudXSkip + 1) / _depthW,
                    //                  vertMul * LineOffset * (PointCloudYSkip + 1) / _depthH, 0f, 0f));

                    // KinectInterop.CopyBytes(_depthData, sizeof(ushort), _prevDepthData, sizeof(ushort));


                    if(!UsingProcessed())
					{
                        SetComputeBufferData(_infraDataBuf, _infraData, uintBufLen, sizeof(uint));
                        _pointCloudMat.SetBuffer("_InfraredMap", _infraDataBuf);
					}


                    /*
                    if (s_depthCalibration == null)
					{
                        Vector3[] depthCal;
                        string calibrationFullpath = "DepthCalibration.PLY";
                        if(System.IO.File.Exists(calibrationFullpath))
						{
                            var ply = PLYReader.LoadPLY(calibrationFullpath);
                            depthCal = ply.vertices.ToArray();
                        }
                        else
						{
                            if(!ShowLive && !Record)
							{
                                Debug.LogError("Missing DepthCalibration.PLY!");
							}

                            PLYReader.CloudData ply = new PLYReader.CloudData(_depthW * _depthH);
                            depthCal = new Vector3[_depthW * _depthH];
#if USE_KINECTLIB

                            for (int y = 0, i = 0; y < _depthH; y++)
                            {
                                for (int x = 0; x < _depthW; x++, i++)
                                {
                                    Vector2 depthPos = new Vector2(x, y);   
                                    var coord3d = _sensor.MapDepthPointToSpaceCoords(_sensorData, depthPos, 1000);
                                    ply.AddPoint(coord3d);
                                    depthCal[i] = coord3d;

                                }
                            }
                            PLYReader.SavePLY(calibrationFullpath, ply);
#else
                            Debug.LogError("NEED KINECTLIB!!! or DepthCalibration.PLY");
#endif
                        }

                        var depthCalN = _depthW * _depthH;
                        s_depthCalibration = new Vector2[depthCalN];
                        for (int i = 0; i < depthCalN; i++)
						{
                            s_depthCalibration[i] = depthCal[i];
                        }

					}
                    */

                    int spaceBufferLength = _depthWDS * _depthHDS * 2;
                    if(_depthCalibrationBuf == null || _depthCalibrationBuf.count != spaceBufferLength)
					{
                        var calib = GetDepthCalibration("KinectCalibration", _depthW, _depthH, sensorIndex);
                              //      #if USE_KINECTLIB
                              //          ,_sensorData
                              //      #endif
                              //  );
                        _depthCalibrationBuf = CreateComputeBuffer(_depthCalibrationBuf, spaceBufferLength, sizeof(float));
                        if (!UsingProcessed())
                        {
                            _depthCalibrationBuf.SetData(calib);
                        }
                        else
						{
                            var W = _depthWDS;
                            var H = _depthHDS;
                            int xstep = PointCloudXSkip + 1;
                            int ystep = PointCloudYSkip + 1;

                            Debug.Log(" " + W + " " + H + "   " + _depthW + " " + xstep + " " + ystep);

                            _depthCalibrationDS = new Vector2[_depthWDS * _depthHDS];

                            for (int y = 0; y < H; y++)
                            {
                                for (int x = 0; x < W; x++)
                                {
                                    int isrc = (x * xstep + _depthXOffsetDS) + (y * ystep + _depthYOffsetDS) * _depthW;
                                    int idst = x + y * W;
                                    var tmp = calib[isrc];
                                    _depthCalibrationDS[idst] = tmp;
                                }
                            }
                            _depthCalibrationBuf.SetData(_depthCalibrationDS);
						}

					}
                    _pointCloudMat.SetBuffer("_DepthCameraCalibration", _depthCalibrationBuf);

                    var size = PointSize;
                    //size *= Mathf.LerpUnclamped(1f, 3f, InfraToSize);
                    if(VerticalLines || HorisontalLines)
                        size *= Mathf.LerpUnclamped(1f, Mathf.Sqrt(((VerticalLines ? PointCloudXSkip : 1f) + 1f) * ((HorisontalLines ? PointCloudYSkip : 1f) + 1f)), 0.5f);
                    else
                        size *= Mathf.LerpUnclamped(1f, Mathf.Sqrt((PointCloudXSkip + 1f) * (PointCloudYSkip + 1f)), 0.5f);
                    size *= Mathf.LerpUnclamped(1f, 1.5f, DepthToSize);
                    _curSize = size;
                    _pointCloudMat.SetFloat("_InfraToSize", InfraToSize);
                    _pointCloudMat.SetFloat("_ReduceStray", ReduceStray);
                    _pointCloudMat.SetVector("_DepthToSize", new Vector4(DepthToSize, DepthToSizeRange, 1f/(DepthToSizeRange + 0.00001f), DepthToSizeCurve));
                    if (ColorAmount > 0f)
					{
                        _pointCloudMat.EnableKeyword("USE_MAINTEX_AS_COLOR_SOURCE");
                        //_pointCloudMat.EnableKeyword("USE_POINTCLOUD_COLOR");
                        _colorTex2.filterMode = FilterMode.Bilinear;
                        _pointCloudMat.mainTexture = _colorTex2;
                        _pointCloudMat.SetFloat("_ColorAmount", ColorAmount);
					}
                    else
					{
                        _pointCloudMat.DisableKeyword("USE_MAINTEX_AS_COLOR_SOURCE");
                        //_pointCloudMat.DisableKeyword("USE_POINTCLOUD_COLOR");
					}
                    TeaParticles.SetAdditive(_pointCloudMat, PointCloudRenderStyle == RenderStyle.Additive);
                    _pointCloudMat.SetFloat("_InfraToColor", InfraToColor);
                    _pointCloudMat.SetVector("_InputTransforms", new Vector4(InfraredScale / 65536f, InfraredOffset, 0f, 0f));
                    _pointCloudMat.SetVector("_DepthTransform", new Vector4((MaxDepth - MinDepth) * 0.001f, MinDepth * 0.001f, SkewBack.x, SkewBack.y));
                    
                    
                    if(HorisontalLines || VerticalLines)
                        _pointCloudMat.EnableKeyword("USE_POINTCLOUD_LINES");
                    else
                        _pointCloudMat.DisableKeyword("USE_POINTCLOUD_LINES");

                    

                    Vector2Int zRange = new Vector2Int(0, 0);
                    if(HorisontalLines && VerticalLines)
                        zRange = new Vector2Int(0, 1);
                    else if(VerticalLines)
                        zRange = new Vector2Int(1, 1);

                    var cloudMeshHash = UsingProcessed() ? -_depthWDS * _depthHDS : (_depthW / (PointCloudXSkip + 1)) * (_depthH / (PointCloudYSkip + 1));
                    var useOptimized = !UseRawFormat && _compressed.GridOptimizeBitmap != null;
                    if (useOptimized) cloudMeshHash += 12345678;
                    cloudMeshHash *= trailN;
                    cloudMeshHash += zRange.x * 7654 + zRange.y * 6543;
                    if (_cloudMesh == null || _cloudMeshHash != cloudMeshHash)
					{
                        if (UsingProcessed())
                            _cloudMesh = TeaParticles.CreateParticleMesh(_depthWDS, _depthHDS, 1, true, 1,                   1,                   1, trailN, 1, zRange.x, zRange.y, useOptimized ? _compressed.GridOptimizeBitmap : null);
                        else
                            _cloudMesh = TeaParticles.CreateParticleMesh(_depthW  , _depthH,   1, true, PointCloudXSkip + 1, PointCloudYSkip + 1, 1, trailN, 1, zRange.x, zRange.y, useOptimized ? _compressed.GridOptimizeBitmap : null);
                        _cloudMeshHash = cloudMeshHash;
                    }
                    _pointCloudTarget.sharedMesh = _cloudMesh;
                    _pointCloudTarget.transform.localRotation = Quaternion.Euler(0f, 0f, Portrait ? 90f: 0f);
                    _pointCloudTarget.transform.localScale = new Vector3(SelfieMirror ? -1f : 1f, -1f, 1f);
                    _pointCloudTarget.gameObject.GetComponent<MeshRenderer>().sharedMaterial = _pointCloudMat;
                    if(PointCloudExtraTarget != null)
					{
                        PointCloudExtraTarget.sharedMesh = _cloudMesh;
                        PointCloudExtraTarget.gameObject.GetComponent<MeshRenderer>().sharedMaterial = _pointCloudMat;
					}


                }
            }
            if(_pointCloudMat != null) {
                _pointCloudMat.SetVector("_Size", new Vector4(_curSize * _curIntensity, 0f, 0f, 0f));
                TeaParticles.SetSizeByCameraDistance(_pointCloudMat, near, nearFade, far, farFade);
                TeaParticles.SetGlow(_pointCloudMat, glowEnable, glow, out float sizeMulForGlow, debugGlowSize);
                _pointCloudMat.SetFloat("_AntiAlias", 400f / (analyticalAntialias * sizeMulForGlow));
            }
        }
    }
}
