
// by: Martin Eklund, music@teadrinker.net
// Licence: GNU GPL v3.0 (https://www.gnu.org/licenses/gpl-3.0.en.html)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectorSimulation : MonoBehaviour
{
	public RenderTexture textureToProject;
	public Material mat;
	void RealtimeUVProjectionTexture(Camera MatrixSource, Material DestinationMaterial)
	{
		if (MatrixSource != null && DestinationMaterial != null)
		{
			var P = MatrixSource.projectionMatrix;
			//P = GL.GetGPUProjectionMatrix(P, true);
			Matrix4x4 V = MatrixSource.worldToCameraMatrix;

			Matrix4x4 VP = P * V;
			DestinationMaterial.SetMatrix("_UVProjMatrix", VP);

			var pos = MatrixSource.transform.position;
			DestinationMaterial.SetVector("_ProjectorPos", new Vector4(pos.x, pos.y, pos.z, 0f));
		}
	}

	// Update is called once per frame
	void Update()
    {
		mat.mainTexture = textureToProject;
		RealtimeUVProjectionTexture(GetComponent<Camera>(), mat);
    }
}
