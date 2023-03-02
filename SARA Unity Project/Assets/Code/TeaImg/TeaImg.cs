
// by: Martin Eklund, music@teadrinker.net
// Licence: GNU GPL v3.0 (https://www.gnu.org/licenses/gpl-3.0.en.html)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace teadrinker
{
    public enum ImgFormat
	{
        None             = 0,

        TextureRGBA      = 1,
        TextureRGBAFloat = 2,
        TextureR         = 3, // not implemented
        TextureRFloat    = 4, // not implemented

        Color32          = 5,
        Color            = 6, // not implemented
        Byte             = 7, // not implemented
        Float            = 8, // not implemented

        Func             = 9,

        Deleted          = 666,
    }


    public class ImgRet
    {

        public ImgRet(Texture2D dat)
        {
            w = dat.width;
            h = dat.height;
            format = Img.FromUnity(dat.format);
            data = (Texture)dat;
        }
        public ImgRet(RenderTexture dat)
        {
            w = dat.width;
            h = dat.height;
            format = Img.FromUnity(dat.format);
            data = (Texture)dat;
        }
        public ImgRet(Img dat)
        {
            w = dat.w;
            h = dat.h;
            format = dat.format;
            data = dat.data;
        }
        public ImgRet(int _w, int _h, Color[] data) { Init(_w, _h, ImgFormat.Color, data); }
        public ImgRet(int _w, int _h, Color32[] data) { Init(_w, _h, ImgFormat.Color32, data); }
        public void Init(int _w, int _h, ImgFormat _format, object _data)
        {
            w = _w;
            h = _h;
            format = _format;
            data = _data;
        }
        public void Release(object skip)
		{
            var dataEqualsSkip = data != null && data.Equals(skip);

            if(!dataEqualsSkip && data is RenderTexture)
			{
                ImgContext.ReleaseTemporaryRT((RenderTexture)data);
			}
            else if (!dataEqualsSkip && data is Texture2D)
			{
                Object.Destroy((Texture2D)data);
                DebugStatsTexturesReleased++;
			}            
            else if(_asTexture != null && !_asTexture.Equals(skip))
			{
                if (_asTexture is RenderTexture)
				{
                    ImgContext.ReleaseTemporaryRT((RenderTexture)_asTexture);
				}
                else if (_asTexture is Texture2D)
				{
                    Object.Destroy((Texture2D)_asTexture);
                    DebugStatsTexturesReleased++;
				}
            }
            _asTexture = null;
            data = null;
            format = ImgFormat.Deleted;
        }

        public static int DebugStatsTexturesCreated = 0;
        public static int DebugStatsTexturesReleased = 0;
        public static int DebugStatsRTexturesCreated = 0;
        public static int DebugStatsRTexturesReleased = 0;
        public Texture AsTexture()
		{
            //var texturesAllocated = context.__get_tex2Ds();
            if(_asTexture == null)
			{
                _asTexture = data as Texture;
                if (_asTexture == null)
                {
                    if (format == ImgFormat.Color)
                    {
                        var col = (Color[])data;
                        var tex = new Texture2D(w, h, Img.ToUnity(ImgFormat.TextureRGBAFloat), false);
                        DebugStatsTexturesCreated++;
                        tex.filterMode = FilterMode.Point;
                        tex.SetPixels(col);
                        tex.Apply(false, false);
                        //texturesAllocated.Add(tex);
                        _asTexture = tex;
                    }
                    else if (format == ImgFormat.Color32)
                    {
                        var col = (Color32[])data;
                        var tex = new Texture2D(w, h, Img.ToUnity(ImgFormat.TextureRGBA), false);
                        DebugStatsTexturesCreated++;
                        tex.filterMode = FilterMode.Point;
                        tex.SetPixels32(col);
                        tex.Apply(false, false);
                        //texturesAllocated.Add(tex);
                        _asTexture = tex;
                    }
                    else
                    {
                        Debug.LogError("AsTexture() unexpected type:" + data.GetType().Name);
                    }
                }
			}
            return _asTexture;
        }

        public Color[] AsColor()
		{
            if(_asColor == null)
			{
                if (data is Color[])
                { 
                    _asColor = (Color[]) data;
                }
                else if (data is RenderTexture)
                {
                    var tex = (RenderTexture)data;
                    _asColor = UnityGpuHelpers.RenderTextureToColor(tex);
                }
                else if (data is Texture2D)
                {
                    var tex = (Texture2D)data;
                    _asColor = tex.GetPixels();
                }
                else
                {
                    Debug.LogError("AsColor() unexpected type:" + data.GetType().Name);
                }

			}
            return _asColor;
        }
        public Color32[] AsColor32()
        {
            if(_asColor32 == null)
			{
                if (data is Color32[])
                {
                    _asColor32 = (Color32[])data;
                }
                else if (data is RenderTexture)
                {
                    var tex = (RenderTexture)data;
                    _asColor32 = UnityGpuHelpers.RenderTextureToColor32(tex);
                }
                else if (data is Texture2D)
                {
                    var tex = (Texture2D)data;
                    _asColor32 = tex.GetPixels32();
                }
                else
                {
                    Debug.LogError("AsColor32() unexpected type:" + data.GetType().Name);
                }
			}
            return _asColor32;
        }


        //public Img img;
        public int w, h;
        public ImgFormat format;
        public object data;

        // use this to store/cache casts in the same node? 
        private Color[] _asColor;
        private Color32[] _asColor32;
        private Texture _asTexture;
        //private Texture asTextureRGBA;
        //private Texture asTextureRGBAFloat;
        //private Color32[] asColor32;
        //private float[] asFloat;
        //private byte[] asByte;


    }

    public class Img
    {
        public int w,h;
        public ImgFormat format;
        public object data;
        private bool _neverReleaseData = false;

        public bool highPrecisionOutput = false;
        public bool inCleanupList = false;

        public string func;
        public Vector4 scalar;
        public Vector4 scalar2;
        public Matrix4x4 mat = Matrix4x4.identity; 
        public Dictionary<string, float> parameters = null;
        public Img[] inputs;
        public ImgContext context;
        private ImgRet _result;
        public ImgRet result
        {
            get { return _result; }
            set { _result = value;  OnResult?.Invoke(this); }
        }

        public event System.Action<Img> OnResult;
        //private System.Action _onRelease;
        public void Release()
		{
            //_onRelease?.Invoke();
            //_onRelease = null;

            if(_result != null)
			{
                _result.Release(data);

                if(!_neverReleaseData)
				{
                    if(data is RenderTexture)
                        ImgContext.ReleaseTemporaryRT((RenderTexture)data);
                    else if(data is Texture2D) { 
                        Object.Destroy((Texture2D) data);
                        ImgRet.DebugStatsTexturesReleased++;
                    }
                    data = null;
                }

                _result = null;
			}

            inCleanupList = false;
            if(!_neverReleaseData)
                format = ImgFormat.Deleted;
        }


        public Img(ImgContext ctx, string funcName, int _w, int _h, Img[] _inputs, Vector4 _scalar = default, Vector4 _scalar2 = default) { context = ctx; w = _w; h = _h; inputs = _inputs; format = ImgFormat.Func; func = funcName; scalar = _scalar; scalar2 = _scalar2; ValidateInputs(); }
        public Img(ImgContext ctx, Color[] t, int _w, int _h) { context = ctx; w = _w; h = _h; format = ImgFormat.Color; data = t; if (w * h != t.Length) Debug.LogError("reported dimensions wrong " + w + " * " + h + " != " + t.Length); }

        public Img(ImgContext ctx, Texture2D t, bool autoDelete = false)     { _neverReleaseData = !autoDelete;  context = ctx; w = t.width; h = t.height; format = FromUnity(t.format); data = t; func = "NOT_FUNC!(Texture2D)"; }
        public Img(ImgContext ctx, RenderTexture t, bool autoDelete = false) { _neverReleaseData = !autoDelete;  context = ctx; w = t.width; h = t.height; format = FromUnity(t.format); data = t; func = "NOT_FUNC!(RenderTexture)"; }

        private void ValidateInputs() {
            foreach (var i in inputs)
            {
                if (i == null) 
                {
                    Debug.LogError("invalid input: null" + func);
                }
            }
        }

        static private Img Fn(string funcName, Img a) { return new Img(a.context, funcName, a.w, a.h, new Img[] { a }); }
        static private Img Op(string funcName, Img a, Img b) { return new Img(a.context, funcName, a.w, a.h, new Img[] { a, b}); }
        static private Img OpConst(string funcName, Img a, Vector4 b) { return new Img(a.context, funcName, a.w, a.h, new Img[] { a }, b); }
        static private Img OpConst(string funcName, Img a, float b) { return new Img(a.context, funcName, a.w, a.h, new Img[] { a }, new Vector4(b, b, b, b)); }

        static private Img Te     (string funcName, Img a, Img b, Img c)         { return new Img(a.context, funcName, a.w, a.h, new Img[] { a, b, c }); }
        static private Img TeConst(string funcName, Img a, Img b, Vector4 c)     { return new Img(a.context, funcName, a.w, a.h, new Img[] { a, b }, Vector4.zero, c); }
        static private Img TeConst(string funcName, Img a, Img b, float c)       { return new Img(a.context, funcName, a.w, a.h, new Img[] { a, b }, Vector4.zero, new Vector4(c, c, c, c)); }
        static private Img TeConst(string funcName, Img a, Vector4 b, Vector4 c) { return new Img(a.context, funcName, a.w, a.h, new Img[] { a }, b, c); }
        static private Img TeConst(string funcName, Img a, float b, float c)     { return new Img(a.context, funcName, a.w, a.h, new Img[] { a }, new Vector4(b, b, b, b), new Vector4(c, c, c, c)); }

        // Unity gpu backend support
        public static ImgFormat FromUnity(TextureFormat f) { 
            if (f == TextureFormat.RGBA32) return ImgFormat.TextureRGBA; 
            if (f == TextureFormat.RGBAFloat) return ImgFormat.TextureRGBAFloat; 
            if (f == TextureFormat.RFloat) return ImgFormat.TextureRFloat; 
            if (f == TextureFormat.R8) return ImgFormat.TextureR;
            return ImgFormat.None;
        }
        public static TextureFormat ToUnity(ImgFormat f) { 
            if (f == ImgFormat.TextureRGBA) return TextureFormat.RGBA32; 
            if (f == ImgFormat.TextureRGBAFloat) return TextureFormat.RGBAFloat; 
            if (f == ImgFormat.TextureRFloat) return TextureFormat.RFloat; 
            if (f == ImgFormat.TextureR) return TextureFormat.R8;
            
            return TextureFormat.RGBAFloat;
        }
        public static ImgFormat FromUnity(RenderTextureFormat f) { 
            if (f == RenderTextureFormat.ARGB32) return ImgFormat.TextureRGBA; 
            if (f == RenderTextureFormat.ARGBFloat) return ImgFormat.TextureRGBAFloat; 
            if (f == RenderTextureFormat.RFloat) return ImgFormat.TextureRFloat; 
            if (f == RenderTextureFormat.R8) return ImgFormat.TextureR;
            return ImgFormat.None;
        }
        public static RenderTextureFormat ToUnityRT(ImgFormat f) { 
            if (f == ImgFormat.TextureRGBA) return RenderTextureFormat.ARGB32; // note: reversed colors?
            if (f == ImgFormat.TextureRGBAFloat) return RenderTextureFormat.ARGBFloat; 
            if (f == ImgFormat.TextureRFloat) return RenderTextureFormat.RFloat; 
            if (f == ImgFormat.TextureR) return RenderTextureFormat.R8;
            
            return RenderTextureFormat.ARGBFloat;
        }

        // interface

        public float RequestPixelMeanRGB(bool autoCleanup = true, bool preferGpu = true) { var c = RequestPixel(autoCleanup, preferGpu); return (c.r + c.g + c.b) / 3f; }
        public float RequestPixelMaxOfRGB(bool autoCleanup = true, bool preferGpu = true) { var c = RequestPixel(autoCleanup, preferGpu); return Mathf.Max(Mathf.Max(c.r, c.g), c.b); }
        public Color RequestPixel(bool autoCleanup = true, bool preferGpu = true) { var c = context.RequestPixels(this, autoCleanup, preferGpu); if (c.Length > 1) Debug.LogWarning("Warning: RequestPixel() but request has more than single pixel output!"); return c[0]; }
        public Color[] RequestPixels(bool autoCleanup = true, bool preferGpu = false) { return context.RequestPixels(this, autoCleanup, preferGpu); }
        public Texture RequestTexure(bool autoCleanup = true, bool preferGpu = true ) { return context.RequestTexture(this, autoCleanup, preferGpu); }

        public Img copy() { return add(0f); } // todo

        public Img neg() { return Fn("neg", this); }
        public Img abs() { return Fn("abs", this); }
        public Img sin() { return Fn("sin", this); }
        public Img cos() { return Fn("cos", this); }
        public Img floor() { return Fn("floor", this); }
        public Img frac() { return Fn("frac", this); }
        public Img square() { return Fn("square", this); }
        public Img cube() { return Fn("cube", this); }


        public Img absdiff(Img b) { return Op("absdiff", this, b); }
        public Img add(Img b) { return Op("+", this, b); }
        public Img sub(Img b) { return Op("-", this, b); }
        public Img mul(Img b) { return Op("*", this, b); }
        public Img div(Img b) { return Op("/", this, b); }
        public Img mod(Img b) { return Op("%", this, b); }
        public Img lessThan(Img b) { return Op("<", this, b); }
        public Img lessThanOrEqual(Img b) { return Op("<=", this, b); }
        public Img greaterThan(Img b) { return Op(">", this, b); }
        public Img greaterThanOrEqual(Img b) { return Op(">=", this, b); }
        public Img and(Img b) { return Op("&&", this, b); }
        public Img or(Img b) { return Op("||", this, b); }
        public Img xor(Img b) { return Op("xor", this, b); }
        public Img nand(Img b) { return Op("nand", this, b); }
        public Img nor(Img b) { return Op("nor", this, b); }
        public Img xnor(Img b) { return Op("xnor", this, b); }
        public Img min(Img b) { return Op("min", this, b); }
        public Img max(Img b) { return Op("max", this, b); }
        public Img pow(Img b) { return Op("pow", this, b); }

        public Img absdiff(Vector4 b) { return OpConst("absdiff", this, b); }
        public Img add(Vector4 b) { return OpConst("+", this, b); }
        public Img sub(Vector4 b) { return OpConst("-", this, b); }
        public Img mul(Vector4 b) { return OpConst("*", this, b); }
        public Img div(Vector4 b) { return OpConst("/", this, b); }  // optimize to mul?
        public Img mod(Vector4 b) { return OpConst("%", this, b); }
        public Img lessThan(Vector4 b) { return OpConst("<", this, b); }
        public Img lessThanOrEqual(Vector4 b) { return OpConst("<=", this, b); }
        public Img greaterThan(Vector4 b) { return OpConst(">", this, b); }
        public Img greaterThanOrEqual(Vector4 b) { return OpConst(">=", this, b); }
        public Img and(Vector4 b) { return OpConst("&&", this, b); }
        public Img or(Vector4 b) { return OpConst("||", this, b); }
        public Img min(Vector4 b) { return OpConst("min", this, b); }
        public Img max(Vector4 b) { return OpConst("max", this, b); }
        public Img pow(Vector4 b) { return OpConst("pow", this, b); }

        public Img absdiff(float b) { return OpConst("absdiff", this, b); }
        public Img add(float b) { return OpConst("+", this, b); }
        public Img sub(float b) { return OpConst("-", this, b); }
        public Img mul(float b) { return OpConst("*", this, b); }
        public Img div(float b) { return OpConst("/", this, b); }  // optimize to mul?
        public Img mod(float b) { return OpConst("%", this, b); }
        public Img lessThan(float b) { return OpConst("<", this, b); }
        public Img lessThanOrEqual(float b) { return OpConst("<=", this, b); }
        public Img greaterThan(float b) { return OpConst(">", this, b); }
        public Img greaterThanOrEqual(float b) { return OpConst(">=", this, b); }
        public Img and(float b) { return OpConst("&&", this, b); }
        public Img or(float b) { return OpConst("||", this, b); }
        public Img min(float b) { return OpConst("min", this, b); }
        public Img max(float b) { return OpConst("max", this, b); }
        public Img pow(float b) { return OpConst("pow", this, b); }



        public Img lerp(Img b, Img t) { return Te("lerp", this, b, t); }
        public Img lerp(Img b, float t) { return TeConst("lerp", this, b, t); }
        public Img lerp(Img b, Vector4 t) { return TeConst("lerp", this, b, t); }
        public Img lerp(float b, float t) { return TeConst("lerp", this, b, t); }
        public Img lerp(Vector4 b, Vector4 t) { return TeConst("lerp", this, b, t); }

        public Img clamp(Img mi, Img ma) { return Te("clamp", this, mi, ma); }
        public Img clamp(Img mi, float ma) { return TeConst("clamp", this, mi, ma); }
        public Img clamp(Img mi, Vector4 ma) { return TeConst("clamp", this, mi, ma); }
        public Img clamp(float mi, float ma) { return TeConst("clamp", this, mi, ma); }
        public Img clamp(Vector4 mi, Vector4 ma) { return TeConst("clamp", this, mi, ma); }

        public Img inRange(Img mi, Img ma) { return Te("in_range", this, mi, ma); }
        public Img inRange(Img mi, float ma) { return TeConst("in_range", this, mi, ma); }
        public Img inRange(Img mi, Vector4 ma) { return TeConst("in_range", this, mi, ma); }
        public Img inRange(float mi, float ma) { return TeConst("in_range", this, mi, ma); }
        public Img inRange(Vector4 mi, Vector4 ma) { return TeConst("in_range", this, mi, ma); }


        public Img reduceSumIter() { return Fn("reduce_sum_iter", this); }
        public Img reduceMeanIter() { return Fn("reduce_mean_iter", this); }
        public Img reduceProdIter() { return Fn("reduce_prod_iter", this); }
        public Img reduceMinIter() { return Fn("reduce_min_iter", this); }
        public Img reduceMaxIter() { return Fn("reduce_max_iter", this); }

        public Img reduceSum() { return Fn("reduce_sum", this); }
        public Img reduceMean() { return Fn("reduce_mean", this); }
        public Img reduceProd() { return Fn("reduce_prod", this); }
        public Img reduceMin() { return Fn("reduce_min", this); }
        public Img reduceMax() { return Fn("reduce_max", this); }

        public Img grayScale(float mulR = 0.2126f, float mulG = 0.7152f, float mulB = 0.0722f, float offset = 0f) { var v = new Vector4(mulR, mulG, mulB, 0f); return channelMix(new Matrix4x4(v,v,v,new Vector4(0f, 0f, 0f, 1f)).transpose, new Vector4(offset, offset, offset, 0f)); }
        public Img channelMix(Matrix4x4 _mat) { return channelMix(_mat, Vector4.zero); }
        public Img channelMix(Matrix4x4 _mat, Vector4 offset) { var r = Fn("channel_matrix", this); r.mat = _mat; r.scalar = offset; return r; }

        public Img convolve(Img b) { return Op("convolve", this, b); }
        public Img convolve1DHorizontal(Img b) { return Op("convolve_1d_horisontal", this, b); }
        public Img convolve1DVertical(Img b) { return Op("convolve_1d_vertical", this, b); }

        public Img blur(int radius, float type = 2f) { var kern = context.BlurKernel(radius, type); return convolve1DHorizontal(kern).convolve1DVertical(kern); }

        public Img meanSquareError() { return square().reduceMean(); }

        public Img set(string parameterName, float value) { if (parameters == null) parameters = new Dictionary<string, float>(); parameters[parameterName] = value; return this; }
        public Img call(string name) { return Fn(name, this); }
        public Img Call(string name, Img b) { return Op(name, this, b); }
        public Img Call(string name, Img b, Img c) { return Te(name, this, b, c); }

        public Img HighPrecision() { highPrecisionOutput = true; return this; }
        public Img WhenResultIsSet(System.Action<Img> f) { OnResult += f; return this; }
        //public void SetReleaseFunc(System.Action f) { _onRelease = f; }

        public static void SafeRelease(ref Img targetVariable)
        {
            if (targetVariable != null)
			{
                targetVariable.Release();
                targetVariable = null;
            }
        }

        public static void RequestAndAssign(ref Img targetVariable, Img newImg, bool autoCleanup = true) {
            newImg.RequestTexure(autoCleanup);
            SafeRelease(ref targetVariable);
            targetVariable = newImg;
        }
        public static void RequestAndAccumulate(ref Img targetVariable, Img newImg, bool autoCleanup = true) {
            if (targetVariable == null)
			{
                newImg.RequestTexure(autoCleanup);
                targetVariable = newImg;
			}
            else
			{
                var addToPrev = newImg.add(targetVariable);
                addToPrev.RequestTexure(autoCleanup);
                SafeRelease(ref targetVariable);
                targetVariable = addToPrev;
            }
        }
    }




    public class ImgContext
	{
        public Img DataSource(Color t) { return DataSource(new Color[] { t }, 1, 1); }
        public Img DataSource(Color[] t) { return new Img(this, t, t.Length, 1); }
        public Img DataSource(Color[] t, int w, int h) { return new Img(this, t, w, h); }
        public Img DataSource(Texture t) { return t is RenderTexture ? DataSource((RenderTexture) t) : DataSource((Texture2D) t); }
        public Img DataSource(Texture2D t) { return new Img(this, t); }
        public Img DataSource(RenderTexture t) { return new Img(this, t); }
        public Img DataSourceAutoRelease(Texture t) { return t is RenderTexture ? DataSourceAutoRelease((RenderTexture) t) : DataSourceAutoRelease((Texture2D) t); }
        public Img DataSourceAutoRelease(Texture2D t) { return new Img(this, t, true); }
        public Img DataSourceAutoRelease(RenderTexture t) { return new Img(this, t, true); }

        //private List<Img> _kernelCache = new List<Img>();
        public Img BlurKernel(int radius, float type = 1f) {
            //if(type == 1f && _kernelCache.Count > )
            var kern = kernel(radius, type);
            var kernc = new Color[kern.Length];
            for(int i = 0; i < kern.Length; i++)
			{
                var v = kern[i];
                kernc[i] = new Color(v, v, v, v);
			}
            return DataSource(kernc); 
        }

        private Dictionary<string, System.Func<Img, ImgRet[], ImgRet>> _gpu = new Dictionary<string, System.Func<Img, ImgRet[], ImgRet>>();
        private Dictionary<string, System.Func<Img, ImgRet[], ImgRet>> _cpu = new Dictionary<string, System.Func<Img, ImgRet[], ImgRet>>();
        private Texture GetTexture(ImgRet a)
		{
            /*
            if(a.format == ImgFormat.Color)
			{
                var col = (Color[]) a.data;
                new 

			}
            
            var tex = a.data as Texture;
            if (tex == null)
                Debug.LogError("no texture, type is "+(a.data.GetType().Name));
            */
            return a.AsTexture();
		}
        public struct TraverseSubtreeConfig
        {
            public bool preferGPU;
        } 
        public void CleanupTemporary()
		{
            foreach (var img in _needsCleanup)
            {
                if(img.inCleanupList) // if client already released this, don't call release
                    img.Release();
            }
            _needsCleanup.Clear();

            /*
            foreach (var rt in _rts)
            {
                if(rt != exception)
                    ReleaseTemporary(rt);
            }
            _rts.Clear();

            foreach (var tex in _tex2Ds)
            {
                Object.Destroy(tex);
            }
            _tex2Ds.Clear();
            */
        }
        public Texture RequestTexture(Img img, bool autoCleanup = true, bool preferGPU = true)
        {
            var imgRet = TraverseTreeRec(img, new TraverseSubtreeConfig() { preferGPU = preferGPU });

            if (autoCleanup)
            {
                /*
                img.SetReleaseFunc(() =>
                {
                    var tmpRt = (RenderTexture)imgRet.data;
                    ReleaseTemporary(tmpRt);
                    imgRet.data = null;
                });
                */
                CleanupTemporary();
            }

            return (Texture) imgRet.data;
        }

        public Color[] RequestPixels(Img img, bool autoCleanup = true, bool preferGPU = true)
		{
            var imgRet = TraverseTreeRec(img, new TraverseSubtreeConfig() { preferGPU = preferGPU });

            var ret = imgRet.AsColor();

            /*
            Color[] ret = null; 
            
            if(imgRet.data is RenderTexture)
			{
                var tex = (RenderTexture) imgRet.data;
                ret = UnityGpuHelpers.RenderTextureToColor(tex);
			}
            else if(imgRet.data is Texture2D)
			{
                var tex = (Texture2D) imgRet.data;
                ret = tex.GetPixels();
            }
            else
			{
                Debug.LogError(imgRet.data.ToString());
			}
            */

            if (autoCleanup)
            {
                CleanupTemporary();
                img.Release();
            }
            else
			{
                AddToCleanupList(img);
			}

            return ret;
        }



        private ImgRet TraverseTreeRec(Img img, TraverseSubtreeConfig cfg, bool rootCall = true)
		{
            if (img.result != null)
                return img.result;

            if (img.format == ImgFormat.Func)
            {
                if (img.func == null)
                    Debug.LogError("TraverseTree() expected function name!");

                System.Func<Img, ImgRet[], ImgRet> fn;
                bool preferGpuBackend = cfg.preferGPU;
                var backend = preferGpuBackend ? _gpu : _cpu;
                if (!backend.TryGetValue(img.func, out fn))
				{
                    backend = !preferGpuBackend ? _gpu : _cpu;
                    if (!backend.TryGetValue(img.func, out fn))
					{
                        Debug.LogError("TraverseTree() no such function or operand: "+img.func);
                        return null;
					}
				}
                cfg.preferGPU = backend == _gpu;
                var N = img.inputs.Length;
                var inp = new ImgRet[N];
                for (int i = 0; i < N; i++)
				{
                    var input = img.inputs[i];

                    if(input == null)
					{
                        Debug.LogError("TraverseTree() img.inputs[" + i + "] == null: " + img.func);
					}
                    else if(input.format == ImgFormat.Deleted)
					{
                        Debug.LogError("TraverseTree() img.inputs[" + i + "] was deleted " + img.func);
					}

                    inp[i] = TraverseTreeRec(input, cfg, false);
				}

                //Debug.Log(img.func + (cfg.preferGPU ? " (gpu)" : " (cpu)"));

                img.result = fn(img, inp);
            }
            else if (img.data != null)
			{
                img.result = new ImgRet(img);
			}

            if (!rootCall)
                AddToCleanupList(img);

            return img.result;
		}
        private void AddToCleanupList(Img img)
		{
            if (!img.inCleanupList)
            {
                _needsCleanup.Add(img);
                img.inCleanupList = true;
            }
		}
        private Material _opMat;
        //private List<RenderTexture> _rts = new List<RenderTexture>();
        //private List<Texture2D> _tex2Ds = new List<Texture2D>();
        //public List<Texture2D> __get_tex2Ds() { return _tex2Ds; } // should not be public

        public List<Img> _needsCleanup = new List<Img>();

        private static bool _useUnityRTTemp = false;
        public static void ReleaseTemporaryRT(RenderTexture t)
		{
            if (_useUnityRTTemp)
                RenderTexture.ReleaseTemporary(t);
            else
                t.Release();
            ImgRet.DebugStatsRTexturesReleased++;
        }
        private static RenderTexture GetTemporaryRT(int w, int h, int d, RenderTextureFormat f)
		{
            var t = _useUnityRTTemp ? RenderTexture.GetTemporary(w, h, d, f) : new RenderTexture(w, h, d, f);
            t.filterMode = FilterMode.Point;
            ImgRet.DebugStatsRTexturesCreated++;
            return t;
		}

        private ImgRet scalarOp(int passId, ImgRet inp0, Vector4 v)
		{
            return CallGpuFunc(_opMat, passId, inp0, false, true, v);
        }
        private ImgRet CallGpuFunc(Material m, int passId, ImgRet inp0, bool forceFloat, bool useScalar = false, Vector4 v = default, bool useScalar2 = false, Vector4 v2 = default)
        {
            var tex0 = GetTexture(inp0);
            var texRet = GetTemporaryRT(tex0.width, tex0.height, 0, forceFloat ? RenderTextureFormat.ARGBFloat : Img.ToUnityRT(inp0.format)); 

            if(useScalar)
			{
                m.EnableKeyword("_USE_SCALAR_B");
                m.SetVector("_Scalar", v);
			}
            else
                m.DisableKeyword("_USE_SCALAR_B");

            if(useScalar2)
			{
                m.EnableKeyword("_USE_SCALAR_C");
                m.SetVector("_Scalar2", v2);
			}
            else
                m.DisableKeyword("_USE_SCALAR_C");

            Graphics.Blit(tex0, texRet, m, passId);
            return new ImgRet(texRet);
		}

        /*
        private Color[] GetPixels(ImgRet i)
		{

		}*/
        private ImgRet cpuChannelMatrix(Img op, ImgRet[] inputs)
        {
            var data0 = inputs[0].AsColor();
            var N = data0.Length;
            var outC = new Color[N];
            for(var i = 0; i < N; i++)
			{
                Vector4 ic0 = data0[i];
                outC[i] = op.mat * ic0 + op.scalar;
			}
            return new ImgRet(inputs[0].w, inputs[0].h, outC);
        }

        private System.Func<Img, ImgRet[], ImgRet> cpuFn(System.Func<float, float> f)
        {
            return (op, inputs) =>
            {
                var data0 = inputs[0].AsColor();
                var N = data0.Length;
                var outC = new Color[N];
                for(var i = 0; i < N; i++)
				{
                    var ic0 = data0[i];
                    outC[i] = new Color(f(ic0.r), f(ic0.g), f(ic0.b), f(ic0.a));
				}
                return new ImgRet(inputs[0].w, inputs[0].h, outC);
            };
        }

        private System.Func<Img, ImgRet[], ImgRet> cpuOp(System.Func<float, float, float> f)
        {
            return (op, inputs) =>
            {
                var data0 = inputs[0].AsColor();
                var N = data0.Length;
                var outC = new Color[N];
                if(inputs.Length == 1)
				{
                    var ic1 = (Color) op.scalar;
                    for(var i = 0; i < N; i++)
				    {
                        var ic0 = data0[i];
                        outC[i] = new Color(f(ic0.r, ic1.r), f(ic0.g, ic1.g), f(ic0.b, ic1.b), f(ic0.a, ic1.a));
				    }
				}
                else
				{
                    var data1 = inputs[1].AsColor();
                    for(var i = 0; i < N; i++)
				    {
                        var ic0 = data0[i];
                        var ic1 = data1[i];
                        outC[i] = new Color(f(ic0.r, ic1.r), f(ic0.g, ic1.g), f(ic0.b, ic1.b), f(ic0.a, ic1.a));
				    }
				}

                return new ImgRet(inputs[0].w, inputs[0].h, outC);
            };
        }

        private System.Func<Img, ImgRet[], ImgRet> cpuTe(System.Func<float, float, float, float> f)
        {
            return (op, inputs) =>
            {
                var data0 = inputs[0].AsColor();
                var N = data0.Length;
                var outC = new Color[N];
                if(inputs.Length == 1)
				{
                    var ic1 = (Color) op.scalar;
                    var ic2 = (Color) op.scalar2;
                    for(var i = 0; i < N; i++)
				    {
                        var ic0 = data0[i];
                        outC[i] = new Color(f(ic0.r, ic1.r, ic2.r), f(ic0.g, ic1.g, ic2.g), f(ic0.b, ic1.b, ic2.b), f(ic0.a, ic1.a, ic2.a));
                    }
                }
                else if(inputs.Length == 2)
				{
                    var data1 = inputs[1].AsColor();
                    var ic2 = (Color) op.scalar2;
                    for(var i = 0; i < N; i++)
				    {
                        var ic0 = data0[i];
                        var ic1 = data1[i];
                        outC[i] = new Color(f(ic0.r, ic1.r, ic2.r), f(ic0.g, ic1.g, ic2.g), f(ic0.b, ic1.b, ic2.b), f(ic0.a, ic1.a, ic2.a));
                    }
                }
                else
				{
                    var data1 = inputs[1].AsColor();
                    var data2 = inputs[2].AsColor();
                    for(var i = 0; i < N; i++)
				    {
                        var ic0 = data0[i];
                        var ic1 = data1[i];
                        var ic2 = data2[i];
                        outC[i] = new Color(f(ic0.r, ic1.r, ic2.r), f(ic0.g, ic1.g, ic2.g), f(ic0.b, ic1.b, ic2.b), f(ic0.a, ic1.a, ic2.a));
				    }
				}

                return new ImgRet(inputs[0].w, inputs[0].h, outC);
            };
        }


#if USE_DOUBLE_PRECISION_FOR_REDUCE

        private System.Func<Img, ImgRet[], ImgRet> cpuReduce(System.Func<double, double, double> f, bool divideByElementCount = false) 
        {
            return (op, inputs) =>
            {

                var data = inputs[0].AsColor();
                var N = data.Length;

                var outCr = (double) data[0].r;
                var outCg = (double) data[0].g;
                var outCb = (double) data[0].b;
                var outCa = (double) data[0].a;
               for(var i = 1; i < N; i++)
				{
                    var ic = data[i];

                    outCr = f(outCr, (double)ic.r);
                    outCg = f(outCg, (double)ic.g);
                    outCb = f(outCb, (double)ic.b);
                    outCa = f(outCa, (double)ic.a);
				}

                if (divideByElementCount)
				{
                    outCr /= N;
                    outCg /= N;
                    outCb /= N;
                    outCa /= N;
                }

                return new ImgRet(1, 1, new Color[] { new Color((float)outCr, (float)outCg, (float)outCb, (float)outCa) });
            };
        }
#else

        private System.Func<Img, ImgRet[], ImgRet> cpuReduce(System.Func<float, float, float> f, bool divideByElementCount = false)
        {
            return (op, inputs) =>
            {

                var data = inputs[0].AsColor();
                var N = data.Length;
                var outC = data[0];
                for (var i = 1; i < N; i++)
                {
                    var ic = data[i];

                    outC = new Color(f(outC.r, ic.r), f(outC.g, ic.g), f(outC.b, ic.b), f(outC.a, ic.a));
                }

                if (divideByElementCount)
                    outC /= (float)N;

                return new ImgRet(1, 1, new Color[] { outC });
            };
        }

#endif
        private void assignWithRelease(ref ImgRet destVar, ImgRet newA)
		{
            destVar.Release(null);
            destVar = newA;
        }

        private ImgRet gpuReduceOneStep(ImgRet a, int passId, bool forceHighPrecision = false, bool divideByElementCount = false)
        {
            var aTex = GetTexture(a);
            var texRet = GetTemporaryRT(max(1, (aTex.width + 1) / 2), max(1, (aTex.height + 1) / 2), 0, forceHighPrecision ? RenderTextureFormat.ARGBFloat : Img.ToUnityRT(a.format)); 
            _opMat.SetVector("_OutputTexelSize", new Vector4(1f / texRet.width, 1f / texRet.height, texRet.width, texRet.height));
            _opMat.SetTexture("_MainTex", aTex);
            Graphics.Blit(aTex, texRet, _opMat, passId);
            var ret = new ImgRet(texRet);
            if (divideByElementCount)
                assignWithRelease(ref ret, mulScalar(ret, a.w * a.h));
            return ret;
        }
        private System.Func<Img, ImgRet[], ImgRet> gpuOpBuiltIn(int passId, int numArgs = 2, bool useMatrix = false)
        {
            return gpuOp(_opMat, passId, numArgs, useMatrix);
        }

        private System.Func<Img, ImgRet[], ImgRet> gpuOp(Material m, int passId, int numArgs = 2, bool useMatrix = false)
		{
            return (op, inputs) =>
            {
                for (int i = 1; i < inputs.Length; i++)
                {
                    m.SetTexture($"_Tex{i}", GetTexture(inputs[i]));
                }

                if(useMatrix)
				{
                    m.SetMatrix("_ChannelMatrix", op.mat);
                    m.SetVector("_ChannelMatrixOffset", op.scalar);
				}

                if(op.parameters != null)
				{
                    foreach(var item in op.parameters)
                        m.SetFloat(item.Key, item.Value);
				}

                if(numArgs == 3)
				{
                    if(inputs.Length == 1)
                        return CallGpuFunc(m, passId, inputs[0], op.highPrecisionOutput, true, op.scalar, true, op.scalar2);
                    if(inputs.Length == 2)
                        return CallGpuFunc(m, passId, inputs[0], op.highPrecisionOutput, false, op.scalar, true, op.scalar2);
                    return CallGpuFunc(m, passId, inputs[0], op.highPrecisionOutput);
                }

                return CallGpuFunc(m, passId, inputs[0], op.highPrecisionOutput, inputs.Length != numArgs, op.scalar);
            };
		}

        private System.Func<Img, ImgRet[], ImgRet> gpuReduce(int passId, bool forceHighPrecision = false, bool divideByElementCount = false)
		{
            return (op, inputs) =>
            {
                var inp = inputs[0];
                var elementCount = inp.w * inp.h;
                var ret = inp;
                if(elementCount > 1)
				{
                    while (ret.w > 1 || ret.h > 1)
				    {
                        assignWithRelease(ref ret, gpuReduceOneStep(ret, passId, forceHighPrecision, false));
				    }
                    if (divideByElementCount)
                        assignWithRelease(ref ret, mulScalar(ret, 1f / elementCount));
				}
                return ret;
            };
		}
        private System.Func<Img, ImgRet[], ImgRet> gpuReduceOneStep(int passId, bool forceHighPrecision = false, bool divideByElementCount = false)
		{
            return (op, inputs) =>
            {
                return gpuReduceOneStep(inputs[0], passId, forceHighPrecision, divideByElementCount);
            };
		}

        private int max(int a, int b) { return a > b ? a : b; }
        private float sq(float x) { return x * x; }
        private float mix(float a, float b, float t) { return a + t * (b - a); }

        public static void FloatArrayTransform(float[] dst, float mul, float add) { int count = dst.Length; for (var i = 0; i < count; i++) dst[i] = dst[i] * mul + add; }
        public static void FloatArrayMul(float[] dst, float mul) { int count = dst.Length; for (var i = 0; i < count; i++) dst[i] *= mul; }
        public static void FloatArrayMul(float[] dst, float[] a, float[] b) { int count = dst.Length; for (var i = 0; i < count; i++) dst[i] = a[i] * b[i]; }
        public static void FloatArrayAdd(float[] dst, float add) { int count = dst.Length; for (var i = 0; i < count; i++) dst[i] += add; }
        public static void FloatArrayMix(float[] dst, float[] a, float[] b, float mix) { int count = dst.Length; for (var i = 0; i < count; i++) { float ai = a[i]; dst[i] = ai + mix * (b[i] - ai); } }
        public static void FloatArrayMax(float[] dst, float[] a, float[] b) { int count = dst.Length; for (var i = 0; i < count; i++) { float ai = a[i]; float bi = b[i]; dst[i] = ai > bi ? ai : bi; } }
        public static void FloatArrayMin(float[] dst, float[] a, float[] b) { int count = dst.Length; for (var i = 0; i < count; i++) { float ai = a[i]; float bi = b[i]; dst[i] = ai < bi ? ai : bi; } }
        public static void FloatArrayMin(float[] dst, float[] a, float b) { int count = dst.Length; for (var i = 0; i < count; i++) { float ai = a[i]; dst[i] = ai < b ? ai : b; } }
        public static void FloatArrayMax(float[] dst, float[] a, float b) { int count = dst.Length; for (var i = 0; i < count; i++) { float ai = a[i]; dst[i] = ai > b ? ai : b; } }


        // normalized window functions, note: represents half window from 0 to 1  (these are all symmetrical windows)
        private float gaussWin(float x, float sharpness)
        {
            var x2 = x - 1;
            //return exp(-x2*x2*pi) * (1-pow(1-x, 6));// smooth falloff
            return Mathf.Exp(-x2 * x2 * Mathf.PI * sharpness) - Mathf.Exp(-Mathf.PI * sharpness); // subtract to remove offset at window boundary

        }
        private float quadWin(float x)
        {
            return x > 0.5 ? 1 - 2 * sq(1 - x) : x * x * 2;
        }
        private float hannWin(float x) { return 0.5f * (1 + Mathf.Cos((x - 1) * Mathf.PI)); }
        private float combineWin(float x, float t, float gaussSharpness)
        {
            if (t == 0) return 1;
            if (t < 1) return mix(1, x, t);
            if (t == 1) return x;
            if (t < 2) return mix(x, quadWin(x), t - 1);
            if (t == 2) return quadWin(x);
            if (t < 3) return mix(quadWin(x), hannWin(x), t - 2);
            if (t == 3) return hannWin(x);
            if (t < 4) return mix(hannWin(x), gaussWin(x, gaussSharpness), t - 3);
            return gaussWin(x, gaussSharpness);
        }

        private float[] kernel(int radius, float t, float gaussSharpness = 2f)
        {
            var N = radius * 2 + 1;
            var r = new float[N];
            var sum = 0f;
            for (var i = 0; i < radius + 1; i++)
            {
                var val = combineWin((i + 1f) / (radius + 1f), t, gaussSharpness);
                r[i] = val;
                if (i == radius)
                {
                    sum += val;
                }
                else
                {
                    r[N - i - 1] = val;
                    sum += val * 2;
                }
            }
            FloatArrayMul(r, 1 / sum);
            return r;
        }




        private ImgRet mulScalar(ImgRet a, float v) { return scalarOp(_passIdMul, a, new Vector4(v, v, v, v)); }
        private ImgRet addScalar(ImgRet a, float v) { return scalarOp(_passIdAdd, a, new Vector4(v, v, v, v)); }

        private int _passIdAdd;
        private int _passIdMul;
        public bool closeEnough(float a, float b)
		{
            return Mathf.Abs(a - b) < 0.00001f;
		}
        public void TestCase(int i, string test, Color correct, int expectedSize, Img img)
		{
            TestCase(i, test, new Color[] { correct }, expectedSize, img);
        }
        public void TestCase(int i, string test, Color[] correctMultiple, int expectedSize, Img img)
        {
            if (expectedSize == -1)
                expectedSize = correctMultiple.Length;
            var preferGpu = i == 0;
            try
            {
                var c = RequestPixels(img, true, preferGpu);

                if (c.Length != expectedSize)
                    Debug.LogError("TestCase:" + test + (preferGpu?" (gpu)":" (cpu)") +"  -fail size: " + c.Length + ", expected " + expectedSize);

                //if (c[0].r != correct.r || c[0].g != correct.g || c[0].b != correct.b || c[0].a != correct.a)
                //if (!c[0].Equals(correct))
                for(int j = 0; j < correctMultiple.Length; j++)
				{
                    var correct = correctMultiple[j];
                    if (!closeEnough(c[j].r, correct.r) || !closeEnough(c[j].g, correct.g) || !closeEnough(c[j].b, correct.b) || !closeEnough(c[j].a, correct.a))
                    {
                        Debug.LogError("TestCase fail: " + test + (preferGpu? " (gpu)" : " (cpu)") + "\n" + c[j].r + " " + correct.r + "  |  " + c[j].g + " " + correct.g + "  |  " + c[j].b + " " + correct.b + "  |  " + c[j].a + " " + correct.a + " at id "+j);
                        if(j != 0)
						{
                            for (int k = 0; k < correctMultiple.Length; k++)
                            {
                                Debug.LogWarning("TestCase fail: " + test + (preferGpu? " (gpu)" : " (cpu)") + "\n" + c[k].r + " " + correctMultiple[k].r + (closeEnough(c[k].r, correctMultiple[k].r) ? "" : "   <---"));
                            }
                            break;
						}
                    }
				}

                img.Release();
            }
            catch (System.Exception e)
            {
                Debug.LogError("TestCase fail: " + test + (preferGpu? " (gpu)" : " (cpu)") + " EXCEPTION \n" + e.Message + "\n" + e.StackTrace);
			}
		}
        public void ReportStats()
		{
            Debug.Log("Textures active: " + (ImgRet.DebugStatsTexturesCreated - ImgRet.DebugStatsTexturesReleased) + "  total created " + ImgRet.DebugStatsTexturesCreated);
            Debug.Log("RenderTex active: " + (ImgRet.DebugStatsRTexturesCreated - ImgRet.DebugStatsRTexturesReleased) + "  total created " + ImgRet.DebugStatsRTexturesCreated);
		}


        public void RunTests()
		{
            //var t = DataSource(new Color(1f, 2f, 3f, 4f));
            //var c = Request(t);
            //Debug.Log("" + c[0].r + " " + c[0].g + " " + c[0].b + " " + c[0].a);

            var _0 = new Color(0f, 0f, 0f, 0f);
            var _1 = new Color(1f, 1f, 1f, 1f);
            var _2 = new Color(2f, 2f, 2f, 2f);
            var _3 = new Color(3f, 3f, 3f, 3f);
            var _4 = new Color(4f, 4f, 4f, 4f);
            var _5 = new Color(5f, 5f, 5f, 5f);
            var _6 = new Color(6f, 6f, 6f, 6f);
            var _7 = new Color(7f, 7f, 7f, 7f);
            var _8 = new Color(8f, 8f, 8f, 8f);
            var _9 = new Color(9f, 9f, 9f, 9f);
            var col = new Color(1f, 2f, 3f, 4f);

            TestCase(0, "conv horiz 1", new Color[] { _0, _0, _2, _0, _2, _2, _2, _0 }, -1, DataSource(new Color[] { _0, _0, _1, _0, _1, _1, _1, _0 }).convolve1DHorizontal(DataSource(new Color[] { _2 })));
            TestCase(0, "conv vert  1", new Color[] { _0, _0, _2, _0, _2, _2, _2, _0 }, -1, DataSource(new Color[] { _0, _0, _1, _0, _1, _1, _1, _0 },1,8).convolve1DVertical(DataSource(new Color[] { _2 })));
            TestCase(0, "conv 1"      , new Color[] { _0, _0, _2, _0, _2, _2, _2, _0 }, -1, DataSource(new Color[] { _0, _0, _1, _0, _1, _1, _1, _0 }).convolve(DataSource(new Color[] {_2 })));
            
            TestCase(0, "conv horiz 2", new Color[] { _2, _4, _6, _4*2f, _5*2f, _6*2f, _7*2f}, -1, DataSource(new Color[] { _1, _2, _3, _4, _5, _6, _7 }).convolve1DHorizontal(DataSource(new Color[] { _0, _2, _0 })));
            TestCase(0, "conv vert  2", new Color[] { _2, _4, _6, _4*2f, _5*2f, _6*2f, _7*2f}, -1, DataSource(new Color[] { _1, _2, _3, _4, _5, _6, _7 },1,7).convolve1DVertical(DataSource(new Color[] { _0, _2, _0 })));
            TestCase(0, "conv 2"      , new Color[] { _2, _4, _6, _4*2f, _5*2f, _6*2f, _7*2f}, -1, DataSource(new Color[] { _1, _2, _3, _4, _5, _6, _7 }).convolve(DataSource(new Color[] { _0, _2, _0 })));
            
            TestCase(0, "conv horiz 3", new Color[] { _0, _1, _2, _4, _3, _6, _5, _3 }, -1, DataSource(new Color[] { _0, _0, _1, _0, _1, _1, _1, _0 }).convolve1DHorizontal(DataSource(new Color[] { _1, _2, _3 })));
            TestCase(0, "conv vert  2", new Color[] { _0, _1, _2, _4, _3, _6, _5, _3 }, -1, DataSource(new Color[] { _0, _0, _1, _0, _1, _1, _1, _0 },1,8).convolve1DVertical(DataSource(new Color[] { _1, _2, _3 })));
            TestCase(0, "conv 3"      , new Color[] { _0, _1, _2, _4, _3, _6, _5, _3 }, -1, DataSource(new Color[] { _0, _0, _1, _0, _1, _1, _1, _0 }).convolve(DataSource(new Color[] { _1, _2, _3 })));
            
            TestCase(0, "blur"      , new Color[] { _1, _1, _1, _1, _1, _1, _1, _1, _1 }, -1, DataSource(new Color[] { _0, _0, _0, _0, _9, _0, _0, _0, _0 },3,3).blur(1,0f));
            TestCase(0, "blur 2"    , new Color[] { _1*0.25f*0.25f, _1*0.25f*0.50f, _1*0.25f*0.25f, 
                                                    _1*0.50f*0.25f, _1*0.50f*0.50f, _1*0.50f*0.25f, 
                                                    _1*0.25f*0.25f, _1*0.25f*0.50f, _1*0.25f*0.25f }, -1, DataSource(new Color[] { _0, _0, _0, _0, _1, _0, _0, _0, _0 },3,3).blur(1,1f));

            for (int i = 0; i < 2; i++)
			{                
                TestCase(i, "basic1", col, 1, DataSource(col));
                TestCase(i, "basic2 mul", col * 2f,    1, DataSource(col).mul(DataSource(new Color(2f,2f,2f,2f))));
                TestCase(i, "basic3 add", col * 2f,    1, DataSource(col).add(DataSource(col)));
                TestCase(i, "basic4 sub", _0    ,    1, DataSource(col).sub(DataSource(col)));
                TestCase(i, "basic5 div", col / 2f,    1, DataSource(col).div(DataSource(new Color(2f, 2f, 2f, 2f))));
                TestCase(i, "scalar1 mul", col * 2f,    1, DataSource(col).mul(2f));
                TestCase(i, "scalar2 add", col * 2f,    1, DataSource(col).add(col));
                TestCase(i, "scalar3 sub", _0    ,    1, DataSource(col).sub(col));
                TestCase(i, "scalar4 div", col / 2f,    1, DataSource(col).div(2f));
                TestCase(i, "sum0 1x1", col     ,  1, DataSource(new Color[] { col }, 1, 1).reduceSum());
                TestCase(i, "sum1 1x2", col * 2f,  1, DataSource(new Color[] { col, col }, 1, 2).reduceSum());
                TestCase(i, "sum2 2x1", col * 2f,  1, DataSource(new Color[] { col, col }, 2, 1).reduceSum());
                TestCase(i, "sum3 1x3", col * 3f,  1, DataSource(new Color[] { col, col, col }, 1, 3).reduceSum());
                TestCase(i, "sum4 3x1", col * 3f,  1, DataSource(new Color[] { col, col, col }, 3, 1).reduceSum());
                TestCase(i, "sum5 2x2", col * 4f,  1, DataSource(new Color[] { col, col, col, col }, 2, 2).reduceSum());
                TestCase(i, "sum6 3x3", col * 9f,  1, DataSource(new Color[] { col, col, col,  col, col, col,  col, col, col }, 3, 3).reduceSum());
                TestCase(i, "sum7 4x3", col * 12f, 1, DataSource(new Color[] { col, col, col,  col, col, col,  col, col, col,   col, col, col }, 4, 3).reduceSum());
                TestCase(i, "sum8 7" ,  col * ( 7 * ( 7 + 1) / 2), 1, DataSource(new Color[] { col, col*2f, col*3f, col*4f, col*5f, col*6f,  col*7f }, 7, 1).reduceSum());
                TestCase(i, "sum9 7x2",  col * (14 * (14 + 1) / 2), 1, DataSource(new Color[] { col, col*2f, col*3f, col*4f, col*5f, col*6f,  col*7f, col*8f, col*9f, col*10f, col*11f, col*12f, col*13f, col*14f }, 7, 2).reduceSum());
                TestCase(i, "sum10 2x7", col * (14 * (14 + 1) / 2), 1, DataSource(new Color[] { col, col*2f, col*3f, col*4f, col*5f, col*6f,  col*7f, col*8f, col*9f, col*10f, col*11f, col*12f, col*13f, col*14f }, 2, 7).reduceSum());
                TestCase(i, "sum11 5x3", col * (15 * (15 + 1) / 2), 1, DataSource(new Color[] { col, col*2f, col*3f, col*4f, col*5f, col*6f,  col*7f, col*8f, col*9f, col*10f, col*11f, col*12f, col*13f, col*14f, col*15f}, 5, 3).reduceSum());
                TestCase(i, "sum12 3x5", col * (15 * (15 + 1) / 2), 1, DataSource(new Color[] { col, col*2f, col*3f, col*4f, col*5f, col*6f,  col*7f, col*8f, col*9f, col*10f, col*11f, col*12f, col*13f, col*14f, col*15f}, 3, 5).reduceSum());

                TestCase(i, "mean0 1x1", col     ,  1, DataSource(new Color[] { col }, 1, 1).reduceMean());
                TestCase(i, "mean1 1x2", col     ,  1, DataSource(new Color[] { col, col }, 1, 2).reduceMean());
                TestCase(i, "mean2 2x1", col     ,  1, DataSource(new Color[] { col, col }, 2, 1).reduceMean());
                TestCase(i, "mean3 1x3", col     ,  1, DataSource(new Color[] { col, col, col }, 1, 3).reduceMean());
                TestCase(i, "mean4 3x1", col     ,  1, DataSource(new Color[] { col, col, col }, 3, 1).reduceMean());
                TestCase(i, "mean5 2x2", col     ,  1, DataSource(new Color[] { col, col, col, col }, 2, 2).reduceMean());
                TestCase(i, "mean6 3x3", col     ,  1, DataSource(new Color[] { col, col, col,  col, col, col,  col, col, col }, 3, 3).reduceMean());
                TestCase(i, "mean7 4x3", col     , 1, DataSource(new Color[] { col, col, col,  col, col, col,  col, col, col,   col, col, col }, 4, 3).reduceMean());
                TestCase(i, "mean8 7" ,  col * ( 7 * ( 7 + 1) / 2) / 7f, 1, DataSource(new Color[] { col, col*2f, col*3f, col*4f, col*5f, col*6f,  col*7f }, 7, 1).reduceMean());
                TestCase(i, "mean9 14",  col * (14 * (14 + 1) / 2) / 14f, 1, DataSource(new Color[] { col, col*2f, col*3f, col*4f, col*5f, col*6f,  col*7f, col*8f, col*9f, col*10f, col*11f, col*12f, col*13f, col*14f }, 7, 2).reduceMean());
                TestCase(i, "mean10 14", col * (14 * (14 + 1) / 2) / 14f, 1, DataSource(new Color[] { col, col*2f, col*3f, col*4f, col*5f, col*6f,  col*7f, col*8f, col*9f, col*10f, col*11f, col*12f, col*13f, col*14f }, 2, 7).reduceMean());

                TestCase(i, "absdiff1",  _1, 1, DataSource(new Color[] { col }, 1, 1).absdiff(DataSource(new Color[] { col + new Color(-1f,1f,-1f,1f) }, 1, 1)));
                TestCase(i, "absdiff2",  _1, 1, DataSource(new Color[] { col, col }, 2, 1).absdiff(DataSource(new Color[] { col + new Color(0f, 0f, -1f, 1f), col + new Color(-1f, 1f, 0f, 0f) }, 2, 1)).reduceSum());
                
                TestCase(i, "lerp1",  Color.LerpUnclamped(col, _1, 0.5f), 1, DataSource(col).lerp(1f, 0.5f));
                TestCase(i, "lerp2",  Color.LerpUnclamped(col, _1, 0.5f), 1, DataSource(col).lerp(DataSource(_1), 0.5f));
                TestCase(i, "lerp3",  Color.LerpUnclamped(col, _1, 0.5f), 1, DataSource(col).lerp(DataSource(_1), DataSource(_1 * 0.5f)));

                var mi = new Color(1.5f, 1.6f, 1.7f, 1.8f);
                var ma = new Color(mi.r + 1f, mi.g + 1f, mi.b + 1f, mi.a + 1f);
                var clampCol = new Color(
                    Mathf.Clamp(col.r, mi.r, ma.r), 
                    Mathf.Clamp(col.g, mi.g, ma.g), 
                    Mathf.Clamp(col.b, mi.b, ma.b), 
                    Mathf.Clamp(col.a, mi.a, ma.a));

                TestCase(i, "clamp1", clampCol, 1, DataSource(col).clamp(mi, ma));
                TestCase(i, "clamp2", clampCol, 1, DataSource(col).clamp(DataSource(mi), ma));
                TestCase(i, "clamp3", clampCol, 1, DataSource(col).clamp(DataSource(mi), DataSource(ma)));

                TestCase(i, "inRange1", new Color(0f, 1f, 1f, 0f), 1, DataSource(col).inRange(2f, 4f));
                TestCase(i, "inRange2", new Color(0f, 1f, 1f, 0f), 1, DataSource(col).inRange(DataSource(2f * _1), 4f * _1));
                TestCase(i, "inRange3", new Color(0f, 1f, 1f, 0f), 1, DataSource(col).inRange(DataSource(2f * _1), DataSource(4f * _1)));

                TestCase(i, "less1", new Color(1f,1f,0f,0f), 1, DataSource(col).lessThan(2.5f));
                TestCase(i, "less2", new Color(1f,1f,0f,0f), 1, DataSource(col).lessThan(DataSource(2.5f * _1)));
                TestCase(i, "lessOrE1", new Color(1f,1f,1f,0f), 1, DataSource(col).lessThanOrEqual(3f));
                TestCase(i, "lessOrE2", new Color(1f,1f,1f,0f), 1, DataSource(col).lessThanOrEqual(DataSource(3f * _1)));
                TestCase(i, "greater1", new Color(0f,0f,1f,1f), 1, DataSource(col).greaterThan(2.5f));
                TestCase(i, "greater2", new Color(0f,0f,1f,1f), 1, DataSource(col).greaterThan(DataSource(2.5f * _1)));
                TestCase(i, "greaterOrE1", new Color(0f,0f,1f,1f), 1, DataSource(col).greaterThanOrEqual(3f));
                TestCase(i, "greaterOrE2", new Color(0f,0f,1f,1f), 1, DataSource(col).greaterThanOrEqual(DataSource(3f * _1)));

                TestCase(i, "and1", new Color(1f,0f,0f,0f), 1, DataSource(new Color(2f, 2f, 0f, 0f)).and(new Color(2f, 0f, 2f, 0f)));
                TestCase(i, "and2", new Color(1f,0f,0f,0f), 1, DataSource(new Color(2f, 2f, 0f, 0f)).and(DataSource(new Color(2f, 0f, 2f, 0f))));
                TestCase(i, "or1" , new Color(1f,1f,1f,0f), 1, DataSource(new Color(2f, 2f, 0f, 0f)).or (new Color(2f, 0f, 2f, 0f)));
                TestCase(i, "or2" , new Color(1f,1f,1f,0f), 1, DataSource(new Color(2f, 2f, 0f, 0f)).or (DataSource(new Color(2f, 0f, 2f, 0f))));

                TestCase(i, "grayScale1", new Color(14f,14f,14f,4f), 1, DataSource(col).grayScale(1f, 2f, 3f, 0f));
                TestCase(i, "grayScale2", new Color(13f,13f,13f,4f), 1, DataSource(col).grayScale(1f, 2f, 3f, -1f));
            }
            ReportStats();
        }
        public void Add(string name, int argCount, Shader shader, int passId = 0)
		{
            _gpu.Add(name, gpuOp(new Material(shader), passId, argCount));
		}
        public ImgContext()
		{
            _opMat = new Material(Resources.Load<Shader>("imgKernels"));
            var i = 0;

            // 1 arg basic
            _gpu.Add("neg",    gpuOpBuiltIn(i++, 1));
            _gpu.Add("abs",    gpuOpBuiltIn(i++, 1));
            _gpu.Add("sin",    gpuOpBuiltIn(i++, 1));
            _gpu.Add("cos",    gpuOpBuiltIn(i++, 1));
            _gpu.Add("floor",  gpuOpBuiltIn(i++, 1));
            _gpu.Add("frac",   gpuOpBuiltIn(i++, 1));
            _gpu.Add("sqaure", gpuOpBuiltIn(i++, 1));
            _gpu.Add("cube",   gpuOpBuiltIn(i++, 1));

            // 2 args basic
            _gpu.Add("absdiff", gpuOpBuiltIn(i++)); _passIdAdd = i;
            _gpu.Add("+",   gpuOpBuiltIn(i++));
            _gpu.Add("-",   gpuOpBuiltIn(i++)); _passIdMul = i;
            _gpu.Add("*",   gpuOpBuiltIn(i++));
            _gpu.Add("/",   gpuOpBuiltIn(i++));
            _gpu.Add("<",   gpuOpBuiltIn(i++));
            _gpu.Add("<=",  gpuOpBuiltIn(i++)); 
            _gpu.Add(">",   gpuOpBuiltIn(i++));
            _gpu.Add(">=",  gpuOpBuiltIn(i++));
            _gpu.Add("&&",  gpuOpBuiltIn(i++));
            _gpu.Add("||",  gpuOpBuiltIn(i++));
            _gpu.Add("xor", gpuOpBuiltIn(i++));
            _gpu.Add("nand",gpuOpBuiltIn(i++));
            _gpu.Add("nor", gpuOpBuiltIn(i++));
            _gpu.Add("xnor",gpuOpBuiltIn(i++));
            _gpu.Add("%",   gpuOpBuiltIn(i++));
            _gpu.Add("min", gpuOpBuiltIn(i++));
            _gpu.Add("max", gpuOpBuiltIn(i++));
            _gpu.Add("pow", gpuOpBuiltIn(i++));

            _gpu.Add("lerp",    gpuOpBuiltIn(i++, 3));
            _gpu.Add("clamp",   gpuOpBuiltIn(i++, 3));
            _gpu.Add("in_range", gpuOpBuiltIn(i++, 3));

            // reduce 
            int reduceStart = i;
            _gpu.Add("reduce_sum_iter",  gpuReduceOneStep(i++, true));
            //_gpu.Add("reduce_mean_iter", gpuReduceOneStep(i++));
            _gpu.Add("reduce_mean_iter", gpuReduceOneStep(i++ - 1, true));
            _gpu.Add("reduce_prod_iter", gpuReduceOneStep(i++));
            _gpu.Add("reduce_min_iter",  gpuReduceOneStep(i++));
            _gpu.Add("reduce_max_iter",  gpuReduceOneStep(i++));
            i = reduceStart;
            _gpu.Add("reduce_sum",  gpuReduce(i++, true));
            //_gpu.Add("reduce_mean", gpuReduce(i++));
            _gpu.Add("reduce_mean", gpuReduce(i++ - 1, true, true));
            _gpu.Add("reduce_prod", gpuReduce(i++));
            _gpu.Add("reduce_min",  gpuReduce(i++));
            _gpu.Add("reduce_max",  gpuReduce(i++));

            _gpu.Add("channel_matrix", gpuOpBuiltIn(i++, 1, true));


            // cpu backend

            _cpu.Add("neg",    cpuFn((a) => -a));
            _cpu.Add("abs",    cpuFn((a) => a < 0 ? -a : a));
            _cpu.Add("sin",    cpuFn(Mathf.Sin));
            _cpu.Add("cos",    cpuFn(Mathf.Cos));
            _cpu.Add("floor",  cpuFn(Mathf.Floor));
            _cpu.Add("frac",   cpuFn((a) => a % 1f));
            _cpu.Add("sqaure", cpuFn((a) => a * a));
            _cpu.Add("cube",   cpuFn((a) => a * a * a));

            _cpu.Add("absdiff", cpuOp((a, b) => Mathf.Abs(a - b)));
            _cpu.Add("+",   cpuOp((a, b) => a + b));
            _cpu.Add("-",   cpuOp((a, b) => a - b));
            _cpu.Add("*",   cpuOp((a, b) => a * b));
            _cpu.Add("/",   cpuOp((a, b) => a / b));
            _cpu.Add("<",   cpuOp((a, b) => a < b ? 1f : 0f));
            _cpu.Add("<=",  cpuOp((a, b) => a <= b ? 1f : 0f));
            _cpu.Add(">",   cpuOp((a, b) => a > b ? 1f : 0f));
            _cpu.Add(">=",  cpuOp((a, b) => a >= b ? 1f : 0f));
            _cpu.Add("&&",  cpuOp((a, b) => a != 0f && b != 0f ? 1f : 0f));
            _cpu.Add("||",  cpuOp((a, b) => a != 0f || b != 0f ? 1f : 0f));
            _cpu.Add("xor", cpuOp((a, b) => !(a == b) ? 1f : 0f));
            _cpu.Add("nand",cpuOp((a, b) => !(a != 0f && b != 0f) ? 1f : 0f));
            _cpu.Add("nor", cpuOp((a, b) => !(a != 0f || b != 0f) ? 1f : 0f));
            _cpu.Add("xnor",cpuOp((a, b) => (a == b) ? 1f : 0f));
            _cpu.Add("%",   cpuOp((a, b) => a % b));
            _cpu.Add("min", cpuOp(Mathf.Min));
            _cpu.Add("max", cpuOp(Mathf.Max));
            _cpu.Add("pow", cpuOp(Mathf.Pow));

            _cpu.Add("lerp", cpuTe(Mathf.LerpUnclamped));
            _cpu.Add("clamp", cpuTe(Mathf.Clamp));
            _cpu.Add("in_range", cpuTe((x, mi, ma) => x >= mi && x < ma ? 1f : 0f));

            _cpu.Add("reduce_sum",  cpuReduce((a, b) => a + b));
            _cpu.Add("reduce_mean", cpuReduce((a, b) => a + b, true));
            _cpu.Add("reduce_prod", cpuReduce((a, b) => a * b));
            _cpu.Add("reduce_min",  cpuReduce((a, b) => a < b ? a : b));
            _cpu.Add("reduce_max",  cpuReduce((a, b) => a > b ? a : b));

            _cpu.Add("channel_matrix", cpuChannelMatrix);

            //
            var convShader = Resources.Load<Shader>("convolve");
            Add("convolve_1d_horisontal", 2, convShader, 0);
            Add("convolve_1d_vertical", 2, convShader, 1);
            Add("convolve", 2, convShader, 2);
		}
    }




    /*


GLSL bicubic    https://stackoverflow.com/questions/13501081/efficient-bicubic-filtering-code-in-glsl

vec4 cubic(float v){
    vec4 n = vec4(1.0, 2.0, 3.0, 4.0) - v;
    vec4 s = n * n * n;
    float x = s.x;
    float y = s.y - 4.0 * s.x;
    float z = s.z - 4.0 * s.y + 6.0 * s.x;
    float w = 6.0 - x - y - z;
    return vec4(x, y, z, w) * (1.0/6.0);
}

vec4 textureBicubic(sampler2D sampler, vec2 texCoords){

   vec2 texSize = textureSize(sampler, 0);
   vec2 invTexSize = 1.0 / texSize;

   texCoords = texCoords * texSize - 0.5;


    vec2 fxy = fract(texCoords);
    texCoords -= fxy;

    vec4 xcubic = cubic(fxy.x);
    vec4 ycubic = cubic(fxy.y);

    vec4 c = texCoords.xxyy + vec2 (-0.5, +1.5).xyxy;

    vec4 s = vec4(xcubic.xz + xcubic.yw, ycubic.xz + ycubic.yw);
    vec4 offset = c + vec4 (xcubic.yw, ycubic.yw) / s;

    offset *= invTexSize.xxyy;

    vec4 sample0 = texture(sampler, offset.xz);
    vec4 sample1 = texture(sampler, offset.yz);
    vec4 sample2 = texture(sampler, offset.xw);
    vec4 sample3 = texture(sampler, offset.yw);

    float sx = s.x / (s.x + s.y);
    float sy = s.z / (s.z + s.w);

    return mix(
       mix(sample3, sample2, sx), mix(sample1, sample0, sx)
    , sy);
}



    public class ResizeTool
    {


     public static void Resize(Texture2D texture2D, int targetX, int targetY, bool mipmap =true, FilterMode filter = FilterMode.Bilinear)
      {
        //create a temporary RenderTexture with the target size
        RenderTexture rt = RenderTexture.GetTemporary(targetX, targetY, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);

        //set the active RenderTexture to the temporary texture so we can read from it
        RenderTexture.active = rt;

        //Copy the texture data on the GPU - this is where the magic happens [(;]
        Graphics.Blit(texture2D, rt);
        //resize the texture to the target values (this sets the pixel data as undefined)
        texture2D.Resize(targetX, targetY, texture2D.format, mipmap);
        texture2D.filterMode = filter;

        try
        {
          //reads the pixel values from the temporary RenderTexture onto the resized texture
          texture2D.ReadPixels(new Rect(0.0f, 0.0f, targetX, targetY), 0, 0);
          //actually upload the changed pixels to the graphics card
          texture2D.Apply();
        }
        catch
        {
          Debug.LogError("Read/Write is not enabled on texture "+ texture2D.name);
        }


        RenderTexture.ReleaseTemporary(rt);
      }
    }

     */


}