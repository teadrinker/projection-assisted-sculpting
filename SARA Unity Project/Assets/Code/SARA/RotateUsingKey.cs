
// by: Martin Eklund, music@teadrinker.net
// Licence: GNU GPL v3.0 (https://www.gnu.org/licenses/gpl-3.0.en.html)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateUsingKey : MonoBehaviour
{
    public float rotationDegrees = 30;
    public KeyCode rotateClockwise = KeyCode.D;
    public KeyCode rotateCounterClockwise = KeyCode.A;
    public KeyCode resetRotation = KeyCode.S;
    public bool rotateClockwiseNow = false;
    public bool rotateCounterClockwiseNow = false;

    static Vector3 eulerRotateY(Vector3 euler, float diff)
	{
        euler.y += diff;
        return euler;
    }
    void Update()
    {
        if (Input.GetKeyDown(rotateClockwise) || rotateClockwiseNow)
        {
            rotateClockwiseNow = false;
            transform.localEulerAngles = eulerRotateY(transform.localEulerAngles, rotationDegrees);
            GetComponent<SaveTransform>()?.SaveNow();
        }

        if (Input.GetKeyDown(rotateCounterClockwise) || rotateCounterClockwiseNow)
        {
            rotateCounterClockwiseNow = false;
            transform.localEulerAngles = eulerRotateY(transform.localEulerAngles, -rotationDegrees);
            GetComponent<SaveTransform>()?.SaveNow();
        }

        if (Input.GetKeyDown(resetRotation))
        {
            transform.localEulerAngles = Vector3.zero;
            GetComponent<SaveTransform>()?.SaveNow();
        }
    }
}
