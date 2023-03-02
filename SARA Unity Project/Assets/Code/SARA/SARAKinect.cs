
// by: Martin Eklund, music@teadrinker.net
// Licence: GNU GPL v3.0 (https://www.gnu.org/licenses/gpl-3.0.en.html)

//#define USE_UNSAFE

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using com.rfilkov.kinect;


namespace teadrinker
{

    public class SARAKinect : MonoBehaviour
    {
        //public int Exposure = 1142;
        //public int Brightness = 128;
        public bool ShowLive = false;
        public Vector2 testDebug;
        public int reduceRange = 230;
        public float threshold = 0.1f;
        public int sensorIndex = 0;
        public Material debugView;

        [Header("Preview depth")]
        public bool PreviewDepth = false;
        [Range(0f, 6f)] public float DepthPreviewMul = 0.25f;
        [Range(0f, 1f)] public float DepthPreviewAdd = 0.0f;
        private bool PreviewInfra = false;
        private int InfraredPreviewDiv = 10;

        private Color32[] _infraredPlaybackBuffer;
        private Texture2D _infraredPlaybackTex;


        private KinectManager _kinectManager = null;
        private KinectInterop.SensorData _sensorData = null;
        private DepthSensorBase _sensor = null;

        private ushort[] _depthData = null;
        private ComputeBuffer _depthDataBuf = null;
        private RenderTexture _colorTex = null;
        private RenderTexture _colorTex2 = null;

        void OnEnable()
        {

        }

        void OnDisable()
        {
            if (_depthDataBuf != null)
            {
                _depthData = null;

                _depthDataBuf.Release();
                _depthDataBuf.Dispose();
                _depthDataBuf = null;
            }

            if (_colorTex)
            {
                if (_sensor != null && _sensor.pointCloudColorTexture == _colorTex)
                    _sensor.pointCloudColorTexture = null;
                _colorTex.Release();
                _colorTex = null;
            }

            if (_colorTex2)
            {
                _colorTex2.Release();
                _colorTex2 = null;
            }

            _kinectManager = null;
        }

        void Update()
        {
            /*
            var sensor = ((Kinect4AzureInterface)_sensor);
            if(sensor)
			{
                sensor.kinectSensor.SetColorControl(Microsoft.Azure.Kinect.Sensor.ColorControlCommand.ExposureTimeAbsolute, Microsoft.Azure.Kinect.Sensor.ColorControlMode.Manual, Exposure);
                sensor.kinectSensor.SetColorControl(Microsoft.Azure.Kinect.Sensor.ColorControlCommand.Brightness, Microsoft.Azure.Kinect.Sensor.ColorControlMode.Manual, Brightness);

			}
            */
            //Microsoft.Azure.Kinect.Sensor.Device

            ValidateBuffersAndFormats();

            HandleRecordAndPlay();
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

        int _depthW;
        int _depthH;

        private void ValidateBuffersAndFormats()
        {

            if (_kinectManager == null && ShowLive)
            {
                Debug.Log("KinectRecorder LazyInit()");
                _kinectManager = KinectManager.Instance;
                _sensorData = (_kinectManager != null && _kinectManager.IsInitialized()) ? _kinectManager.GetSensorData(sensorIndex) : null;
            }

            Vector2Int colorRes = Vector2Int.zero;

            if (ShowLive)
            {
                _sensor = (DepthSensorBase)_sensorData.sensorInterface;
                colorRes = GetPointCloudTexResolution(_sensor, _sensorData);
                _depthW = _sensorData.depthImageWidth;
                _depthH = _sensorData.depthImageHeight;

            }


            if ((_colorTex == null || _colorTex.width != colorRes.x || _colorTex.height != colorRes.y) && colorRes.x > 0 && colorRes.y > 0)
            {
                _colorTex = KinectInterop.CreateRenderTexture(_colorTex, colorRes.x, colorRes.y, RenderTextureFormat.ARGB32);
                _colorTex2 = KinectInterop.CreateRenderTexture(_colorTex2, colorRes.x, colorRes.y, RenderTextureFormat.ARGB32);

                //_sensor.MapDepthPointToSpaceCoords()
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

            int depthImageLengthDS = _depthW * _depthH;
            if (_depthDataDS == null || _depthDataDS.Length != depthImageLengthDS)
            {
                _depthDataDS = new ushort[depthImageLengthDS];
                _infraDataDS = new ushort[depthImageLengthDS];
            }

            int computeSize = depthImageLengthDS / 2;
            if (_depthDataBuf == null || _depthDataBuf.count != computeSize)
                _depthDataBuf = KinectInterop.CreateComputeBuffer(_depthDataBuf, computeSize, sizeof(uint));

            if (ShowLive)
            {
                _sensor.pointCloudColorTexture = _colorTex;
            }
        }

        private Texture2D _tmptex;
        private ushort[] _colorDepthBuffer;
        private Texture2D GeneratePreview(ushort[] infra, ushort[] depth, RenderTexture dest)
        {
            var muli = (uint)(65535f / InfraredPreviewDiv);
            var muld = (uint)(65535f * DepthPreviewMul);
            var addd = (uint)(255 * 65535f * DepthPreviewAdd);
            /*   if (PreviewInfra && PreviewDepth)
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
               else */
            if (PreviewDepth)
            {
                var errCol = new Color32(50, 0, 0, 255);
                for (int i = 0; i < _infraredPlaybackBuffer.Length; i++)
                {
                    uint dep = depth[i];
                    if (dep == 0)
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

            //_sensorData.colorImageTexture.get
            //_colorTex
            var colW = _colorTex.width;
            var colH = _colorTex.height;
            if (_tmptex == null || _tmptex.width != colW || _tmptex.height != colH)
            {
                if (_tmptex != null)
                    Destroy(_tmptex);
                _tmptex = new Texture2D(colW, colH, TextureFormat.ARGB32, false, true);
            }
            if(_colorDepthBuffer == null || _colorDepthBuffer.Length != colW * colH)
			{
                _colorDepthBuffer = new ushort[colW * colH];
            }
            Rect rect = new Rect(0, 0, colW, colH);
            RenderTexture.active = _colorTex;
            _tmptex.ReadPixels(rect, 0, 0);
            _tmptex.Apply(false, false);
            RenderTexture.active = null;
            var colPixels = _tmptex.GetPixels32();
            var colWH = colW * colH;

            /*
            for (int y = reduceRange; y < colH - reduceRange; y++)
            {
                for (int x = reduceRange; x < colW - reduceRange; x++)
                {
                    var cSpace = new Vector2(((float)x), ((float)y));
                    var cSpace = _sensor.MapColorPointToSpaceCoords(_sensorData, dSpace, d);
                }
            }
            */
            for (int i = 0; i < _colorDepthBuffer.Length; i++)
            {
                _colorDepthBuffer[i] = 65000;
            }

            CalcDepthToColorCoords();

            Color32 black = new Color32(0, 0, 0, 255);
            for (int i = 0; i < 2; i++) {
                for (int y = reduceRange; y < _depthH - reduceRange; y++) {
                    for (int x = reduceRange; x < _depthW - reduceRange; x++)
                    {
                        var index = x + y * _depthW;
                        var d = depth[index];
                        if (d == 0)
                            continue;

                        //var dSpace = new Vector2(((float)x), ((float)y));
                        //var cSpace = _sensor.MapDepthPointToColorCoords(_sensorData, dSpace, d);
                        var cSpace = DepthPointToColorCoord(index, d);


                        var cIndex = clamp(((int)cSpace.x) + colW * ((int)cSpace.y), 1 + colW, colWH-1 -1 - colW);
                        var dval = _colorDepthBuffer[cIndex];
                        if(i == 0)
						{
                            if (d < dval)
                            {
                                _colorDepthBuffer[cIndex] = d;
                            }
                            
                            if (d < _colorDepthBuffer[cIndex - 1])
                            {
                                _colorDepthBuffer[cIndex - 1] = d;
                            }
                            if (d < _colorDepthBuffer[cIndex + 1])
                            {
                                _colorDepthBuffer[cIndex + 1] = d;
                            }
                            if (d < _colorDepthBuffer[cIndex -colW])
                            {
                                _colorDepthBuffer[cIndex - colW] = d;
                            }
                            if (d < _colorDepthBuffer[cIndex + colW])
                            {
                                _colorDepthBuffer[cIndex + colW] = d;
                            }

                            if (d < _colorDepthBuffer[cIndex - 1 - colW])
                            {
                                _colorDepthBuffer[cIndex - 1 - colW] = d;
                            }
                            if (d < _colorDepthBuffer[cIndex + 1 + colW])
                            {
                                _colorDepthBuffer[cIndex + 1 + colW] = d;
                            }
                            if (d < _colorDepthBuffer[cIndex + 1 - colW])
                            {
                                _colorDepthBuffer[cIndex + 1 - colW] = d;
                            }
                            if (d < _colorDepthBuffer[cIndex - 1 + colW])
                            {
                                _colorDepthBuffer[cIndex - 1 + colW] = d;
                            }


						}
                        else
						{
                            _infraredPlaybackBuffer[index] = d <= dval + threshold ? colPixels[cIndex] : black;
						}


                        //var diff = dSpace - cSpace + testDebug;
                        //_infraredPlaybackBuffer[index] = new Color32((byte)clamp((int)(abs(diff.x) * threshold), 0, 255), (byte)clamp((int)(abs(diff.y) * threshold), 0, 255), 0, 255);

                        /*
                        var space3D = _sensor.MapDepthPointToSpaceCoords(_sensorData, dSpace, d);
                        //var space3D2 = _sensor.MapColorPointToSpaceCoords(_sensorData, cSpace, d);
                        if (cSpace.sqrMagnitude > 0.01f && space3D.sqrMagnitude > 0.01f)// && space3D2.sqrMagnitude > 0.01f)
					    {
                            var cSpace2 = _sensor.MapSpacePointToColorCoords(_sensorData, space3D);
                            var dist = (cSpace - cSpace2).magnitude;
                            //var dist = (space3D - space3D2).magnitude;
                            if(dist > threshold)
                                _infraredPlaybackBuffer[index] = new Color32(255, (byte)(int) (dist/255f), 0, 255);
					    }
                        else 
                            _infraredPlaybackBuffer[index] = new Color32(255, 0, 255, 255);
                        */

                    }
                }
            }

            _infraredPlaybackTex.SetPixels32(_infraredPlaybackBuffer);
            _infraredPlaybackTex.Apply(false);

            return _infraredPlaybackTex;
            //Graphics.CopyTexture(_infraredPlaybackTex, dest);
        }

        private ushort[] GetRawInfraredMap(int sensorIndex)
        {
            if (_kinectManager == null)
                return null;
            return _kinectManager.GetRawInfraredMap(sensorIndex);
        }


        private ushort[] _depthDataDS;
        private ushort[] _infraDataDS;
        private static float abs(float x) { return x < 0f ? -x : x; }
        private static float max(float a, float b) { return a > b ? a : b; }
        private static float clamp01(float x) { return x < 0 ? 0f : (x > 1f ? 1f : x); }
        private static float linstep(float a, float b, float x) { return clamp01((x - a) / (b - a)); }
        private static int clamp(int x, int mi, int ma) { return x < mi ? mi : (x > ma ? ma : x); }



        private ulong _lastDepthFrameTime;
        public ulong GetKinectFrame() { return _lastDepthFrameTime; }



        private Vector2[] _map_1_0;
        private Vector2[] _map_2_0;
        public int DepthW() { return _depthW; }
        public int DepthH() { return _depthH; }
        public Vector2 DepthPointToColorCoord(int index, int depth)
		{
            return Vector2.LerpUnclamped(_map_1_0[index], _map_2_0[index], (depth - 1000f) / (2000f - 1000f)) / ((float)depth);
        }
        public void CalcDepthToColorCoords()
		{
            if (_map_1_0 == null)
            {
                _map_1_0 = new Vector2[_depthH * _depthW];
                _map_2_0 = new Vector2[_depthH * _depthW];
                for (int y = 0; y < _depthH; y++)
                {
                    for (int x = 0; x < _depthW; x++)
                    {
                        var i = x + y * _depthW;
                        var dSpace = new Vector2(((float)x), ((float)y));
                        _map_1_0[i] = _sensor.MapDepthPointToColorCoords(_sensorData, dSpace, 1000);
                        _map_2_0[i] = _sensor.MapDepthPointToColorCoords(_sensorData, dSpace, 2000);

                        //ushort mid = 1800;
                        //var map_mid = _sensor.MapDepthPointToColorCoords(_sensorData, dSpace, mid);
                        //Debug.Log("rec " +(Vector2.LerpUnclamped(_map_1_0[i], _map_2_0[i]*2f, (mid - 1000f) / (2000f - 1000f) ) / (mid / 1000f) - map_mid)); // correct
                        //Debug.Log("lin " +(Vector2.LerpUnclamped(_map_1_0[i], _map_2_0[i], (mid - 1000f) / (2000f - 1000f) ) - map_mid[i])); // wrong

                        // prepare for perspective interpolation
                        _map_1_0[i] *= 1000f; 
                        _map_2_0[i] *= 2000f; 
                    }
                }
            }
        }

        [System.NonSerialized] public bool AccumulateDepth = false;
        [System.NonSerialized] public int[] AccumulatedDepthBuf = null;
        [System.NonSerialized] public int AccumulatedCount = 0;

        public void ClearAccumulatedDepth()
		{
            AccumulatedDepthBuf = null;
            AccumulatedCount = 0;
        }


        private void HandleRecordAndPlay()
        {
            bool newFrame = false;


            if (ShowLive)
            {
                newFrame = _lastDepthFrameTime != _sensorData.lastDepthFrameTime;
            }

            if (newFrame)
            {

                if (ShowLive)
                    _lastDepthFrameTime = _sensorData.lastDepthFrameTime;

                if(_sensorData.depthImage != null && _depthW != 0 && _depthH != 0)
				{
                    if(AccumulateDepth)
				    {
                        if (AccumulatedDepthBuf == null)
                        {
                            AccumulatedDepthBuf = new int[_depthW * _depthH];
                        }

                        var dep = _sensorData.depthImage;

                        if (AccumulatedCount == 0)
					    {
                            for (int i = 0; i < AccumulatedDepthBuf.Length; i++)
                                AccumulatedDepthBuf[i] = dep[i];
					    }
					    else
					    {
                            for (int i = 0; i < AccumulatedDepthBuf.Length; i++)
                                AccumulatedDepthBuf[i] += dep[i];
					    }
                        AccumulatedCount++;
                    }
                    else
				    {
                        AccumulatedCount = 0;
                    }

				}


                if (PreviewDepth)
                    KinectInterop.CopyBytes(_sensorData.depthImage, sizeof(ushort), _depthData, sizeof(ushort));


                //_infraData = GetRawInfraredMap(sensorIndex);

                if (PreviewInfra || PreviewDepth)
                {
                    var tmptex = GeneratePreview(/*_infraData*/null, _depthData, _colorTex2);
                    if (debugView != null)
                    {
                        debugView.mainTexture = tmptex;
                    }
                }
                else
                {
                    Graphics.CopyTexture(_colorTex, _colorTex2);
                    if (debugView != null)
                    {
                        debugView.mainTexture = _colorTex2;
                    }
                }



            }
        }

    }
}
