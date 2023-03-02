
// by: Martin Eklund, music@teadrinker.net
// Licence: GNU GPL v3.0 (https://www.gnu.org/licenses/gpl-3.0.en.html)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RenderCodedLight : MonoBehaviour
{
    public Material material;
    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        material.SetFloat("_Width", src.width);
        material.SetFloat("_Height", src.height);
        Graphics.Blit(src, dest, material);
    }
}
