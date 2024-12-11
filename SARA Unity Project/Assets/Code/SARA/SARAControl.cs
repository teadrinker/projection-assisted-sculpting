
// by: Martin Eklund, music@teadrinker.net
// Licence: GNU GPL v3.0 (https://www.gnu.org/licenses/gpl-3.0.en.html)

using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace teadrinker
{
    public class SARAControl : MonoBehaviour
    {

        [Space(10)]
        [Header("Calibration")]

        public bool debugKinectStream = false;
        [Range(0f, 1f)] public float kinectRGBExposure = 0.5f;
        [Space(10)]

        [Range(0f, 1f)] public float calibrationColorMul = 1f;

        public bool startCalibration = false;
        public bool completeCalibration = false;
        public bool showCalibration = false;
        //public bool saveCalibration = false;

        [Space(10)]
        [Header("Settings")]
        [Space(20)]

        public string feedbackColorsPngName = "feedback_colors";
        [Space(10)]
        public bool averageKinectDepth = false;
        public int averageKinectDepthFrames = 15;
        [Space(10)]
        public bool KinectLimitDepth = false;
        public float KinectMaxDepth = 2f;
        public Vector2 KinectSkewBack = Vector2.zero;

        [Space(20)]
        //[Header("Coded Light Calibration")]
        [Range(0f, 1f)] public float displacement = 1f;

        //[Space(30)]
        [HideInInspector] public bool DisplacementOutlierFilter = false;
        [HideInInspector] [Range(1, 50)] public float DisplacementOutlierFilterRadius = 20;
        [HideInInspector] [Range(0, 50)] public float DisplacementOutlierFilterThres = 0f;
        [Space(20)]
        public bool DisplacementInpaint = true;
        [Range(1, 50)] public int DisplacementInpaintRadius = 20;
        [Range(1, 3)] public float DisplacementInpaintConstant = 1.5f;
        [Range(0, 5)] public float DisplacementInpaintSampleMargin = 0.5f;
        [Space(20)]
        public bool DisplacementBlur = false;
        [Range(1, 50)] public int DisplacementBlurRadius = 10;
        public bool regenerateDisplacementMap = false;

        [Space(30)]
        public CodedLightCalibration codedLightController = null;
        public Material pointCloudMaterial = null;
        public DepthStream depthStream = null;
        public com.rfilkov.kinect.Kinect4AzureInterface kinect = null;
        public DisplaceImagePostFX displaceImagePostFX = null;

        private float _prevKinectRGBExposure = -1f;

        private Color _orgBackColor = Color.black;
        private LayerMask _orgCullingMask = 0;
        [System.NonSerialized] private Vector2Int kinectRGBExposureRange = new Vector2Int(300, 18000);

        private Texture2D _feedbackColorTex = null;


        private string GetCalibrationFullPath()
        {
            return Application.dataPath + "/../SARACalibration.dat";
        }


        void OnEnable()
        {
            _orgBackColor = displaceImagePostFX.gameObject.GetComponent<Camera>().backgroundColor;
            _orgCullingMask = displaceImagePostFX.gameObject.GetComponent<Camera>().cullingMask;

            var path = Application.streamingAssetsPath + "/" + feedbackColorsPngName + ".png";
            if (System.IO.File.Exists(path))
            {
                var pngBytes = System.IO.File.ReadAllBytes(path);
                _feedbackColorTex = new Texture2D(16, 16);
                _feedbackColorTex.LoadImage(pngBytes);
                _feedbackColorTex.filterMode = FilterMode.Point;
                _feedbackColorTex.hideFlags = HideFlags.HideAndDontSave;
                pointCloudMaterial.SetTexture("_FeedbackColors", _feedbackColorTex);
            }
            else
            {
                Debug.LogWarning("Missing " + path);
            }

            codedLightController.OnCalibrationComplete = SaveCalibration;
            LoadCalibration();
        }
        void OnDisable()
        {
            pointCloudMaterial.SetTexture("_FeedbackColors", null);
            Destroy(_feedbackColorTex);
            _feedbackColorTex = null;
        }

        void OnDrawGizmos()
        {
            if (showCalibration)
                codedLightController.DrawRayGizmos();
        }

        void LoadCalibration()
        {
            FileUtil.LoadBinaryFileIfExist(GetCalibrationFullPath(), reader =>
            {
                FileUtil.LoadBinary(codedLightController.destination.transform, reader, false);

                int w = reader.ReadInt32();
                int h = reader.ReadInt32();
                int N = w * h;
                var disp = new Color[N];
                for (int i = 0; i < N; i++)
                {
                    disp[i].r = reader.ReadSingle();
                    disp[i].g = reader.ReadSingle();
                    disp[i].b = reader.ReadSingle();
                }

                codedLightController.SetDisplacement(disp, w, h);
            });
        }

        public void SaveCalibration()
        {

            FileUtil.SaveBinaryFile(GetCalibrationFullPath(), writer =>
            {
                FileUtil.SaveBinary(codedLightController.destination.transform, writer, false);

                int w = codedLightController.lastDisplacementW;
                int h = codedLightController.lastDisplacementH;
                var disp = codedLightController.lastDisplacement;

                writer.Write(w);
                writer.Write(h);
                int N = w * h;
                for (int i = 0; i < N; i++)
                {
                    writer.Write(disp[i].r);
                    writer.Write(disp[i].g);
                    writer.Write(disp[i].b);
                }
            });

            Debug.Log("Calibration Saved " + GetCalibrationFullPath());
        }

        void Update()
        {
            //if(saveCalibration)
            //{
            //    saveCalibration = false;
            //}

            if (startCalibration)
            {
                startCalibration = false;
                debugKinectStream = false;
                codedLightController.gameObject.SetActive(true);
            }

            codedLightController.SetColorMul(calibrationColorMul);


            if (completeCalibration)
            {
                completeCalibration = false;
                if (codedLightController.gameObject.activeSelf)
                {
                    codedLightController.doSecondCodedLightPass = true;
                }
                else
                {
                    Debug.LogError("Error, CompleteCalibration: calibration must first be started!");
                }
            }

            bool isCalibrating = codedLightController.gameObject.activeInHierarchy;

            if (regenerateDisplacementMap)
            {
                regenerateDisplacementMap = false;
                codedLightController.GenerateDisplacement();
            }


            if (_prevKinectRGBExposure != kinectRGBExposure)
            {
                kinect.kinectSensor.SetColorControl(Microsoft.Azure.Kinect.Sensor.ColorControlCommand.ExposureTimeAbsolute, Microsoft.Azure.Kinect.Sensor.ColorControlMode.Manual, (int)(kinectRGBExposure * (kinectRGBExposureRange.y - kinectRGBExposureRange.x) + kinectRGBExposureRange.x));
                kinect.kinectSensor.SetColorControl(Microsoft.Azure.Kinect.Sensor.ColorControlCommand.Brightness, Microsoft.Azure.Kinect.Sensor.ColorControlMode.Manual, 128);
            }



            if (debugKinectStream)
            {
                depthStream.OverrideMaterial = null;
                depthStream.ColorAmount = 1f;
                kinect.pointCloudResolution = com.rfilkov.kinect.DepthSensorBase.PointCloudResolution.DepthCameraResolution;
                displaceImagePostFX.gameObject.GetComponent<Camera>().backgroundColor = new Color(calibrationColorMul, calibrationColorMul, calibrationColorMul, 1f);
                displaceImagePostFX.gameObject.GetComponent<Camera>().cullingMask = 0;
            }
            else
            {
                depthStream.OverrideMaterial = pointCloudMaterial;
                depthStream.ColorAmount = 0f;
                kinect.pointCloudResolution = com.rfilkov.kinect.DepthSensorBase.PointCloudResolution.ColorCameraResolution;
                displaceImagePostFX.gameObject.GetComponent<Camera>().backgroundColor = _orgBackColor;
                displaceImagePostFX.gameObject.GetComponent<Camera>().cullingMask = _orgCullingMask;
            }


            depthStream.averageDepthFrames = averageKinectDepth ? averageKinectDepthFrames : 0;

            depthStream.ProcessDepth = KinectLimitDepth;
            depthStream.MaxDepth = (int)(KinectMaxDepth * 1000f);
            depthStream.SkewBack = KinectSkewBack;

            displaceImagePostFX.amount = isCalibrating ? 0 : displacement;

            if (
                codedLightController.DisplacementOutlierFilter != DisplacementOutlierFilter ||
                codedLightController.DisplacementOutlierFilterRadius != DisplacementOutlierFilterRadius ||
                codedLightController.DisplacementOutlierFilterThres != DisplacementOutlierFilterThres ||
                codedLightController.DisplacementInpaint != DisplacementInpaint ||
                codedLightController.DisplacementInpaintConstant != DisplacementInpaintConstant ||
                codedLightController.DisplacementInpaintRadius != DisplacementInpaintRadius ||
                codedLightController.DisplacementInpaintSampleMargin != DisplacementInpaintSampleMargin ||
                codedLightController.DisplacementBlur != DisplacementBlur ||
                codedLightController.DisplacementBlurRadius != DisplacementBlurRadius)
            {
                codedLightController.DisplacementOutlierFilter = DisplacementOutlierFilter;
                codedLightController.DisplacementOutlierFilterRadius = DisplacementOutlierFilterRadius;
                codedLightController.DisplacementOutlierFilterThres = DisplacementOutlierFilterThres;
                codedLightController.DisplacementInpaint = DisplacementInpaint;
                codedLightController.DisplacementInpaintConstant = DisplacementInpaintConstant;
                codedLightController.DisplacementInpaintRadius = DisplacementInpaintRadius;
                codedLightController.DisplacementInpaintSampleMargin = DisplacementInpaintSampleMargin;
                codedLightController.DisplacementBlur = DisplacementBlur;
                codedLightController.DisplacementBlurRadius = DisplacementBlurRadius;

                codedLightController.UpdateDisplacement();
            }
        }
    }

}