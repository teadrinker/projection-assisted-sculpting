
// by: Martin Eklund, music@teadrinker.net
// Licence: GNU GPL v3.0 (https://www.gnu.org/licenses/gpl-3.0.en.html)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetKinectExposure : MonoBehaviour
{
    [Range(0f, 1f)] public float kinectRGBExposure;
    private float _prevKinectRGBExposure;
    [System.NonSerialized]private Vector2Int kinectRGBExposureRange = new Vector2Int(300, 18000);
    public com.rfilkov.kinect.Kinect4AzureInterface kinect = null;

    void OnEnable() {
        kinect = GetComponent<com.rfilkov.kinect.Kinect4AzureInterface>();
    }

    void Update()
    {
        if(_prevKinectRGBExposure != kinectRGBExposure)
		{
            kinect.kinectSensor.SetColorControl(Microsoft.Azure.Kinect.Sensor.ColorControlCommand.ExposureTimeAbsolute, Microsoft.Azure.Kinect.Sensor.ColorControlMode.Manual, (int)(kinectRGBExposure * (kinectRGBExposureRange.y - kinectRGBExposureRange.x) + kinectRGBExposureRange.x));
            kinect.kinectSensor.SetColorControl(Microsoft.Azure.Kinect.Sensor.ColorControlCommand.Brightness, Microsoft.Azure.Kinect.Sensor.ColorControlMode.Manual, 128);
        }
    }
}
