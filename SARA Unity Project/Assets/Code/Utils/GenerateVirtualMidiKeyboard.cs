
// by: Martin Eklund, music@teadrinker.net
// Licence: GNU GPL v3.0 (https://www.gnu.org/licenses/gpl-3.0.en.html)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class GenerateVirtualMidiKeyboard : MonoBehaviour
{
    public float octaves = 5f + 1/12f; // five octaves + one note
    public float whiteKeyWidth = 21f;
    public float whiteKeyDepth = 47f;
    public float whiteKeySpacing = 2.5555555f;
    public float blackKeyWidth = 7.5f;
    public float blackKeyDepth = 79f;
    public float blackKeySpacing1 = 36f - 7.5f;
    public float blackKeySpacing2 = 35f - 7.5f;
    public float blackKeyHeight = 10f;
    public float blackKeyDepthSpacing = 7f;
    public Material mat = null; 

    void SetBoxTransformAndMat(Transform dest, Vector3 min, Vector3 max)
	{
        dest.localPosition = (min + max) * 0.5f;
        dest.localRotation = Quaternion.identity;
        dest.localScale = (max - min);
        if(mat != null)
		{
            dest.GetComponent<MeshRenderer>().sharedMaterial = mat;
		}
    }
    void OnDisable()
	{
        for(int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject.DestroyImmediate(transform.GetChild(i).gameObject);
        }
	}
    void OnEnable()
    {
        var baseScale = 0.001f;
        var keySize = whiteKeyWidth + whiteKeySpacing;
        var octaveSize = 7 * keySize;
        for (float o = 0f; o < octaves; o++)
		{
            var x = o * octaveSize;
            var y = 0f;
            var z = 0f;
            for (int w = 0; w < 7; w++)
            {
                var key = GameObject.CreatePrimitive(PrimitiveType.Cube);
                key.transform.parent = transform;
                key.name = "white " + o + " " + w;
                SetBoxTransformAndMat(key.transform, baseScale * new Vector3(x, y, z), baseScale * new Vector3(x + whiteKeyWidth , y, z + whiteKeyDepth));
                x += whiteKeyWidth + whiteKeySpacing;
                if (o + 1f > octaves)
                    return;
            }
            y = blackKeyHeight;
            z = whiteKeyDepth + blackKeyDepthSpacing;
            for (int b = 0; b < 5; b++)
            {
                if(b < 2)
                    x = o * octaveSize + whiteKeyWidth * 1.5f + whiteKeySpacing + blackKeySpacing1 * (b - 0.5f) - blackKeyWidth / 2f;
                else
                    x = o * octaveSize + whiteKeyWidth * 5f + 4.5f * whiteKeySpacing + blackKeySpacing2 * (b - 3f) - blackKeyWidth / 2f;
                var key = GameObject.CreatePrimitive(PrimitiveType.Cube);
                key.transform.parent = transform;
                key.name = "black " + o + " " + b;
                SetBoxTransformAndMat(key.transform, baseScale * new Vector3(x, y, z), baseScale * new Vector3(x + blackKeyWidth, y, z + blackKeyDepth));
                x += whiteKeyWidth + whiteKeySpacing;
            }

        }
    }
}
