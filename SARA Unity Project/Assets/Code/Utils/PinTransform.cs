
// by: Martin Eklund, music@teadrinker.net
// Licence: GNU GPL v3.0 (https://www.gnu.org/licenses/gpl-3.0.en.html)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class PinTransform : MonoBehaviour
{
    public enum PriorityOrder {
        Prio_1_2_3,
        Prio_2_1_3,
        Prio_3_1_2,
    };
    public PriorityOrder priority = PriorityOrder.Prio_1_2_3;
    public Transform referencePin1;
    public Transform referencePin2;
    public Transform referencePin3;
    public Transform targetPin1;
    public Transform targetPin2;
    public Transform targetPin3;


    static void RotateAround(Transform transform, Vector3 pivotPoint, Quaternion rot)
    {
        transform.position = rot * (transform.position - pivotPoint) + pivotPoint;
        transform.rotation = rot * transform.rotation;
    }

    void Update()
    {
        var targets = new Transform[4];
        var refs = new Transform[4];

        if(priority == PriorityOrder.Prio_1_2_3)
        {
            targets[1] = targetPin1;
            targets[2] = targetPin2;   
            targets[3] = targetPin3;          
            refs[1] = referencePin1;
            refs[2] = referencePin2;   
            refs[3] = referencePin3;          
        }
        else if (priority == PriorityOrder.Prio_3_1_2)
        {
            targets[1] = targetPin3;
            targets[2] = targetPin1;   
            targets[3] = targetPin2;          
            refs[1] = referencePin3;
            refs[2] = referencePin1;   
            refs[3] = referencePin2;                  
        }
        else if (priority == PriorityOrder.Prio_2_1_3)
        {
            targets[1] = targetPin2;
            targets[2] = targetPin1;   
            targets[3] = targetPin3;          
            refs[1] = referencePin2;
            refs[2] = referencePin1;   
            refs[3] = referencePin3;                  
        }

        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        var refPin1 = refs[1].position;
        var refPin2 = refs[2].position;
        //var refPin3 = referencePin3.position;
        var pin1 = targets[1].position;
        var pin2 = targets[2].position;
        //var pin3 = targetPin3.position;

        transform.position = pin1 - refPin1;
        var rot = Quaternion.FromToRotation((refPin1 - refPin2).normalized, (pin1 - pin2).normalized);
        RotateAround(transform, pin1, rot);

        // create plane to rotate orthogonally to (pin1 - pin2).normalized
        var plane = new Plane((pin2 - pin1).normalized, pin1);
        var pin3 = plane.ClosestPointOnPlane(targets[3].position);
        var refPin3 = plane.ClosestPointOnPlane(refs[3].position);
        var rot2 = Quaternion.FromToRotation((refPin3 - pin1).normalized, (pin3 - pin1).normalized);
        RotateAround(transform, pin1, rot2);

    }
}
