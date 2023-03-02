
// by: Martin Eklund, music@teadrinker.net
// Licence: GNU GPL v3.0 (https://www.gnu.org/licenses/gpl-3.0.en.html)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisplaceImagePostFX : MonoBehaviour
{
    [Range(0f, 1f)] public float amount = 1f;
    public Texture displacementTexture = null;
    private Material _mat;

	private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if(displacementTexture == null || amount == 0f)
		{
            Graphics.Blit(src, dest);
            return;
		}

        if(_mat == null)
		{
            _mat = new Material(Resources.Load<Shader>("DisplaceImagePostFX"));
		}

        if(displacementTexture.width != src.width || displacementTexture.height != src.height)
		{
            Debug.LogWarning("Run calibration again! mismatching coords for displacement: " + displacementTexture.width + " vs " + src.width + ", " + displacementTexture.height + " vs " + src.height);
            Graphics.Blit(src, dest);
            return;
		}
        else
        {
            _mat.SetTexture("_Displacement", displacementTexture);
            _mat.SetFloat("_Amount", amount);
            _mat.SetFloat("_Width", src.width);
            _mat.SetFloat("_Height", src.height);
            Graphics.Blit(src, dest, _mat);
        }
        
    }
}
