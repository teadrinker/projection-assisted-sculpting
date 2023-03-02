
// This helps manipulating the camera when there is a target object in focus

// by: Martin Eklund, music@teadrinker.net
// Licence: GNU GPL v3.0 (https://www.gnu.org/licenses/gpl-3.0.en.html)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace teadrinker
{

    [ExecuteInEditMode]
    public class NormalizedCameraController : MonoBehaviour
    {
        public Camera output;
        [Space]
        public float baseFOV = 60f;
        public float targetDistance = 1f;
        [Space]
        [Range(-1f, 1f)] public float panLeftRight = 0f;
        [Range(-1f, 1f)] public float panUpDown = 0f;
        [Range(-1f, 1f)] public float orbitLeftRight = 0f;
        [Range(-1f, 1f)] public float orbitUpDown = 0f;
        [Range(-1f, 1f)] public float distance = 0f;
        [Range(-1f, 1f)] public float adjusthorizon = 0f;
        [Space]
        [Range(-1f, 1f)] public float lensShiftX = 0f;
        [Range(-1f, 1f)] public float lensShiftY = 0f;
        [Space(40)]
        public float panRange = 10f;
        public float orbitRange = 10f;
        public float distRange = 0.1f;
        public float lensShiftRange = 1.5f;
        [Space]
        [Range(-.1f, .1f)] public float fineDist = 0f;
        public bool flattenNow = false;
        public bool ResetNow = false;

        static void SetObliqueness(Camera cam, float horizObl, float vertObl)
        {
            cam.ResetProjectionMatrix();
            Matrix4x4 mat = cam.projectionMatrix;
            mat[0, 2] = horizObl;
            mat[1, 2] = vertObl;
            cam.projectionMatrix = mat;
        }

        void DrawFrustum(Camera cam, float midDist) // started from: https://stackoverflow.com/questions/44424842/unity3d-focus-cam-in-a-rect-using-project-matrix
        {
            Vector3[] nearCorners = new Vector3[4]; //Approx'd nearplane corners
            Vector3[] farCorners = new Vector3[4]; //Approx'd farplane corners
            Plane[] camPlanes = GeometryUtility.CalculateFrustumPlanes(cam); //get planes from matrix
            Plane temp = camPlanes[1]; camPlanes[1] = camPlanes[2]; camPlanes[2] = temp; //swap [1] and [2] so the order is better for the loop

            Vector3[] midCorners = new Vector3[4];
            Plane midPlane = camPlanes[4];
            midPlane.distance -= midDist - cam.nearClipPlane;

            for (int i = 0; i < 4; i++)
            {
                nearCorners[i] = Plane3Intersect(camPlanes[4], camPlanes[i], camPlanes[(i + 1) % 4]); //near corners on the created projection matrix
                farCorners[i] = Plane3Intersect(camPlanes[5], camPlanes[i], camPlanes[(i + 1) % 4]); //far corners on the created projection matrix

                midCorners[i] = Plane3Intersect(midPlane, camPlanes[i], camPlanes[(i + 1) % 4]); 
            }

            for (int i = 0; i < 4; i++)
            {
                GizmoDrawLine(nearCorners[i], nearCorners[(i + 1) % 4], Color.red); //near corners on the created projection matrix
                GizmoDrawLine(farCorners[i], farCorners[(i + 1) % 4], Color.blue); //far corners on the created projection matrix
                GizmoDrawLine(nearCorners[i], farCorners[i], Color.green); //sides of the created projection matrix
                GizmoDrawLine(midCorners[i], midCorners[(i + 1) % 4], Color.green); //sides of the created projection matrix
            }
        }

        Vector3 Plane3Intersect(Plane p1, Plane p2, Plane p3)
        { //get the intersection point of 3 planes
            return ((-p1.distance * Vector3.Cross(p2.normal, p3.normal)) +
                    (-p2.distance * Vector3.Cross(p3.normal, p1.normal)) +
                    (-p3.distance * Vector3.Cross(p1.normal, p2.normal))) /
                (Vector3.Dot(p1.normal, Vector3.Cross(p2.normal, p3.normal)));
        }
        void GizmoDrawLine(Vector3 a, Vector3 b, Color c)
	    {
            Gizmos.DrawLine(a, b);
	    }

        void OnDrawGizmosSelected()
        {
            var forward = transform.forward;
            var pos = transform.position + forward * fineDist;
            var orbitPivot = pos + forward * targetDistance;

            Gizmos.color = new Color(0f, 0.6f, 1f, 1.0f);
            Gizmos.DrawLine(pos, orbitPivot);

            Gizmos.color = new Color(1f, 0.6f, 0f, 1.0f);

            DrawFrustum(output, targetDistance + fineDist);
            Gizmos.color = new Color(6f, 6f, 6f, 1.0f);
            Gizmos.DrawSphere(orbitPivot, 0.02f);
           // Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, new Vector3(output.aspect, 1.0f, 1.0f));
          //  Gizmos.DrawFrustum(Vector3.zero, output.fieldOfView, output.farClipPlane, output.nearClipPlane, 1.0f);
        }

        void Update()
        {
            UpdateNow();
        }

        public void UpdateNow()
        {
            if (flattenNow)
		    {
                flattenNow = false;
                baseFOV = output.fieldOfView;
                transform.position = output.transform.position;
                transform.rotation = output.transform.rotation;
                ResetNow = true;
            }
            if(ResetNow)
		    {
                ResetNow = false;
                panLeftRight = 0f;
                panUpDown = 0f;
                orbitLeftRight = 0f;
                orbitUpDown = 0f;
                distance = 0f;
                adjusthorizon = 0f;
            }

            var forward = transform.forward;
            var pos = transform.position + forward * fineDist;
            var eul = transform.eulerAngles;

            var orbitPivot = pos + forward * targetDistance;

            var distanceDiff = distance * distRange;
            pos -= forward * distanceDiff;

            var height = FrustumHeightAtDistance(targetDistance, baseFOV);
            var curFov = FOVForHeightAndDistance(height, targetDistance + distanceDiff);

            eul.x -= panUpDown * panRange - orbitUpDown * orbitRange - lensShiftRange * lensShiftY * curFov * 0.5f;
            eul.y -= panLeftRight * panRange + orbitLeftRight * orbitRange + lensShiftRange * lensShiftX * curFov * 0.5f * output.aspect;
            eul.z += adjusthorizon * panRange;

            var orbitPos = pos - orbitPivot;

            var orbitRot = Quaternion.Euler(new Vector3(-orbitUpDown * orbitRange, -orbitLeftRight * orbitRange, 0f));
            //orbitRot = orbitRot * Quaternion.Euler(eul);
            orbitPos = orbitRot * orbitPos;

            pos = orbitPos + orbitPivot;

            output.fieldOfView = curFov;
            output.transform.position = pos;
            output.transform.eulerAngles = eul;

            SetObliqueness(output, lensShiftRange * lensShiftX, lensShiftRange * lensShiftY);
        }




        // https://docs.unity3d.com/560/Documentation/Manual/DollyZoom.html

        // Calculate the frustum height at a given distance from the camera.
        static float FrustumHeightAtDistance(float distance, float fov)
        {
            return 2.0f * distance * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        }

        // Calculate the FOV needed to get a given frustum height at a given distance.
        static float FOVForHeightAndDistance(float height, float distance)
        {
            return 2.0f * Mathf.Atan(height * 0.5f / distance) * Mathf.Rad2Deg;
        }
    }

}