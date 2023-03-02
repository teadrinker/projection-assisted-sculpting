using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetDepthTextureMode : MonoBehaviour
{
    public DepthTextureMode mode = DepthTextureMode.Depth;

    // Update is called once per frame
    void Update()
    {
        GetComponent<Camera>().depthTextureMode = mode;
    }
}
