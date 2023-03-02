
// by: Martin Eklund, music@teadrinker.net
// Licence: GNU GPL v3.0 (https://www.gnu.org/licenses/gpl-3.0.en.html)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace teadrinker
{
	public static class UnityGpuHelpers { 
		public static void SafeRelease(ref RenderTexture rt)
		{
			if (rt != null) { 
				rt.Release();
				rt = null;
			}
		}
		public static void MakeSureValidRT(ref RenderTexture rt, RenderTexture sizeAndFormatReference, int depth = -1)
		{
			MakeSureValidRT(ref rt, sizeAndFormatReference.width, sizeAndFormatReference.height, sizeAndFormatReference.format, depth != -1 ? depth : sizeAndFormatReference.depth);
		}
		public static void MakeSureValidRT(ref RenderTexture rt, int w, int h, RenderTextureFormat f = RenderTextureFormat.ARGB32, int depth = 0)
		{
			if(rt == null || rt.width != w || rt.height != h || rt.format != f || rt.depth != depth)
			{
				if (rt != null)
					rt.Release();

				rt = new RenderTexture(w, h, depth, f);
			}
		}

		public static Color[] RenderTextureToColor(RenderTexture rt)
		{
			Texture2D texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false);

			RenderTexture.active = rt;

			texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
			texture.Apply();

			RenderTexture.active = null;
			var pixels = texture.GetPixels();

			Object.Destroy(texture);

			return pixels;
		}
		public static Color32[] RenderTextureToColor32(RenderTexture rt)
		{
			Texture2D texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);

			RenderTexture.active = rt;

			texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
			texture.Apply();

			RenderTexture.active = null;
			var pixels = texture.GetPixels32();

			Object.Destroy(texture);

			return pixels;
		}

	}
}