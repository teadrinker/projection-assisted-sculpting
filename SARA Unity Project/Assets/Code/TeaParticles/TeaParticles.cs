//
//  TeaParticles, by Martin Eklund 2021
//
//       License: GNU GPL v3, https://www.gnu.org/licenses/gpl-3.0.en.html
//       For commercial use, contact music@teadrinker.net
//  
//Features:
//
//  * Analytical antialias
//  * Using quads or triangles 
//  * Multiple blending and render styles
//  * Smooth anim of particle spawn/death (animate size)
//  * near / far settings to make particles smoothly disappear in relation to camera
//
//
//  KernelUpdate.Basic particle update is based on https://github.com/keijiro/KvantStream
//


using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace teadrinker { 
public class TeaParticles : MonoBehaviour
{
        public enum KernelUpdate
		{
            Basic,

            TextureData,

            // TextureData_LinesU
            // TextureData_LinesV
            // TextureData_Grid
            // TextureData_TriGrid

            // TextureDataX2_Lines   // 2 TextureData inputs (startpoints and endpoints)

            Grid,   // should ta
		}

        [Header("Generation")]
        public KernelUpdate kernelUpdate = KernelUpdate.Basic;
        public int maxParticles = 16000;  // Note: there is a hard limit at 32 million
        public Vector2Int uvResolution = Vector2Int.zero;

        [Space()]
        public Texture2D textureData;
        public Texture2D textureColor;

        [Header("Simulation & Rendering")]
        public bool enableSimulation = true;
        public bool runSimulation = true;

        public enum RenderBlend { Additive = 0, Alpha = 1, OpaqueWithDepth = 2 };
        public RenderBlend renderBlend = RenderBlend.Additive;
        public enum RenderStyle { Round = 0, Circle = 1, Streak = 16, Drop = 16 + 1, Trail = 32, TrailHeavy = 32 + 1 };
        public RenderStyle renderStyle = RenderStyle.Streak;
        public bool forceQuads = false;

        [Range(0f, 32f)] public float analyticalAntialias = 1f;

        [Space()]
        public bool glowEnable = false;
        [Range(-0.999f, 0.999f)] public float glow = 0f;
        public bool debugGlowSize = false;


        [Space()]
        public bool emitAll = false;
        [Range(0, 1)] public float flow = 1.0f;

        public float timeMul = 1f;
        public float lifeDuration = 2f;
        [Range(0, 1)] public float lifeDurationV = 0.5f;

        public float size = 1.0f;
        [Range(0, 1)] public float sizeV = 0.5f;

        public float tail = 1.0f;

        public float tailFromSpeed = 1.0f;

        public float near = 0.5f;
        [Range(0, 1)] public float nearFade = 0.5f;
        public float far = 15f;
        [Range(0, 1)] public float farFade = 1f;


        public Vector3 emitterPosition = Vector3.forward * 20;

        public Vector3 emitterSize = Vector3.one * 40;


        public Vector3 direction = -Vector3.forward;

        public float minSpeed = 0.2f;

        public float maxSpeed = 1.0f;

        [Range(0, 1)]
        public float spread = 0.2f;

        public float noiseAmplitude = 0.1f;

        public float noiseFrequency = 0.2f;

        public float noiseSpeed = 1.0f;

        public Color color = Color.white;

        public int RenderQueue = -1;


        public int randomSeed = 0;

        public bool debug;



//        public Shader updateShader;
//        public Shader updateShaderMRT;
        //public Shader particleShader;
        //public Shader debugShader;

        private static Shader _shaderTPDrop;
        private static Shader _shaderTPDrop_tr;
        private static Shader _shaderTPSimple;
        private static Shader _shaderTPSimple_tr;
        private static Shader _shaderTPGrid_tr;
//        private static Shader _shaderTPTextureData_tr;

        private Material _kernelMaterial;
        private Material _kernelMaterialMRT;
        private Material _lineMaterial;
        private Material _debugMaterial;


        private bool _useBoxCut;
        private Matrix4x4 _boxMatrix;
        private Vector4 _boxGradient;

        public void DisableBoxCut()
		{
            _useBoxCut = false;
        }
        public void SetBoxCut(Vector3 position, Quaternion rotation, Vector3 boxRadius, Vector3 gradientLength)
		{
            _useBoxCut = true;
            _boxGradient = new Vector4( boxRadius.x / (gradientLength.x + 0.00001f), 
                                        boxRadius.y / (gradientLength.y + 0.00001f), 
                                        boxRadius.z / (gradientLength.z + 0.00001f), 0f);
            _boxMatrix = Matrix4x4.TRS(position, rotation, boxRadius).inverse;
        }

        private RenderTexture _particleBuffer1;
        private RenderTexture _particleBuffer2;
        Mesh _mesh;
        bool _needsReset = true;

        int BufferWidth 
        { 
            get 
            { 
                if (uvResolution.x > 0)
                    return uvResolution.x;
                return 4096; 
            } 
        }

        int BufferHeight
        {
            get
            {
                if (uvResolution.y > 0)
                    return uvResolution.y;
                return Mathf.Clamp(maxParticles / BufferWidth + 1, 1, 2*4096);
            }
        }

        static float deltaTime
        {
            get
            {
                return Application.isPlaying && Time.frameCount > 1 ? Time.deltaTime : 1.0f / 10;
            }
        }




        Material CreateMaterial(Shader shader)
        {
            var material = new Material(shader);
            material.hideFlags = HideFlags.DontSave;
            return material;
        }

        RenderTexture CreateBuffer()
        {
            var buffer = new RenderTexture(BufferWidth, BufferHeight, 0, RenderTextureFormat.ARGBFloat);
            buffer.hideFlags = HideFlags.DontSave;
            buffer.filterMode = FilterMode.Point;
            buffer.wrapMode = TextureWrapMode.Repeat;
            return buffer;
        }

        public static Mesh CreateParticleMesh(int width, int height, int depth, bool useQuads, int wstep = 1, int hstep = 1, int zstep = 1, int uvFramesW = 1, int uvFramesH = 1, int zFirstFrame = 0, int zLastFrame = 0, uint[] optBitmap = null)
        {
            int frames = uvFramesW * uvFramesH * ((zLastFrame - zFirstFrame) + 1);

            var mesh = new Mesh();
            mesh.hideFlags = HideFlags.DontSave;

            if(optBitmap != null)
			{
                List<Vector3> verts = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                List<int> ind = new List<int>();

                if (useQuads)
                {
                    for (var fy = 0; fy < uvFramesH; fy++)
                    {
                        for (var fx = 0; fx < uvFramesW; fx++)
                        {
                            for (var fz = zFirstFrame; fz <= zLastFrame; fz++)
                            {
                                float frameZ = fz;
                                Vector2 frame = new Vector2(fx, fy);
                                for (var z = 0; z < depth; z += zstep)
                                {
                                    var zv = frameZ + (float)z / depth;
                                    var id = 0;
                                    for (var y = 0; y < height; y += hstep)
                                    {
                                        for (var x = 0; x < width; x += wstep)
                                        {
                                            if ((optBitmap[id >> 5] & (1 << (id & 31))) != 0)
                                            {
                                                int index = verts.Count;
                                                verts.Add(new Vector3( 0.01f, 0, zv));
                                                verts.Add(new Vector3(-0.01f, 0, zv));
                                                verts.Add(new Vector3( 0.01f, 0.01f, zv));
                                                verts.Add(new Vector3(-0.01f, 0.01f, zv));

                                                var u = (float)x / width;
                                                var v = (float)y / height;
                                                var uv = new Vector2(u, v) + frame;
                                                uvs.Add(uv);
                                                uvs.Add(uv);
                                                uvs.Add(uv);
                                                uvs.Add(uv);

                                                ind.Add(index + 0);
                                                ind.Add(index + 1);
                                                ind.Add(index + 2);
                                                ind.Add(index + 2);
                                                ind.Add(index + 1);
                                                ind.Add(index + 3);
                                            }
                                            id++;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    for (var fy = 0; fy < uvFramesH; fy++)
                    {
                        for (var fx = 0; fx < uvFramesW; fx++)
                        {
                            for (var fz = zFirstFrame; fz <= zLastFrame; fz++)
                            {
                                float frameZ = fz;
                                Vector2 frame = new Vector2(fx, fy);
                                for (var z = 0; z < depth; z += zstep)
                                {
                                    var id = 0;
                                    var zv = frameZ + (float)z / depth;
                                    for (var y = 0; y < height; y += hstep)
                                    {
                                        for (var x = 0; x < width; x += wstep)
                                        {
                                            if ((optBitmap[id >> 5] & (1 << (id & 31))) != 0)
                                            {
                                                int index = verts.Count;
                                                verts.Add(new Vector3( 0.01f,   0, zv));
                                                verts.Add(new Vector3(-0.01f,   0, zv));
                                                verts.Add(new Vector3( 0f,    0.01f, zv));

                                                var u = (float)x / width;
                                                var v = (float)y / height;
                                                var uv = new Vector2(u, v) + frame;
                                                uvs.Add(uv);
                                                uvs.Add(uv);
                                                uvs.Add(uv);

                                                ind.Add(index + 0);
                                                ind.Add(index + 1);
                                                ind.Add(index + 2);
                                            }
                                            id++;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.indexFormat = ind.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.SetIndices(ind, MeshTopology.Triangles, 0);

            }
            else
			{
                var Nx = (width + wstep - 1) / wstep;
                var Ny = (height + hstep - 1) / hstep;
                var Nz = (depth + zstep - 1) / zstep;

                Vector3[] verts;
                Vector2[] uvs;

                var Ai = 0;

                int[] ind;

                if (useQuads)
			    {
                    verts = new Vector3[Nx * Ny * Nz * 4 * frames];
                    uvs = new Vector2[Nx * Ny * Nz * 4 * frames];
                    ind = new int[Nx * Ny * Nz * 6 * frames];
                    var AiIndex = 0;

                    for (var fy = 0; fy < uvFramesH; fy++)
                    {
                        for (var fx = 0; fx < uvFramesW; fx++)
                        {
                            for (var fz = zFirstFrame; fz <= zLastFrame; fz++)
                            {
                                float frameZ = fz;
                                Vector2 frame = new Vector2(fx, fy);
                                for (var z = 0; z < depth; z += zstep)
                                {
                                    var zv = frameZ + (float)z / depth;
                                    for (var y = 0; y < height; y += hstep)
                                    {
                                        for (var x = 0; x < width; x += wstep)
                                        {
                                            verts[Ai + 0] = new Vector3( 0.01f, 0, zv);
                                            verts[Ai + 1] = new Vector3(-0.01f, 0, zv);
                                            verts[Ai + 2] = new Vector3( 0.01f, 0.01f, zv);
                                            verts[Ai + 3] = new Vector3(-0.01f, 0.01f, zv);

                                            var u = (float)x / width;
                                            var v = (float)y / height;
                                            uvs[Ai] = uvs[Ai + 1] = uvs[Ai + 2] = uvs[Ai + 3] = new Vector2(u, v) + frame;

                                            ind[AiIndex + 0] = Ai + 0;
                                            ind[AiIndex + 1] = Ai + 1;
                                            ind[AiIndex + 2] = Ai + 2;
                                            ind[AiIndex + 3] = Ai + 2;
                                            ind[AiIndex + 4] = Ai + 1;
                                            ind[AiIndex + 5] = Ai + 3;

                                            Ai += 4;
                                            AiIndex += 6;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
			    {
                    verts = new Vector3[Nx * Ny * Nz * 3 * frames];
                    uvs = new Vector2[Nx * Ny * Nz * 3 * frames];
                    ind = new int[Nx * Ny * Nz * 3 * frames];

                    for (var fy = 0; fy < uvFramesH; fy++)
                    {
                        for (var fx = 0; fx < uvFramesW; fx++)
                        {
                            for (var fz = zFirstFrame; fz <= zLastFrame; fz++)
                            {
                                float frameZ = fz;
                                Vector2 frame = new Vector2(fx, fy);
                                for (var z = 0; z < depth; z += zstep)
                                {
                                    var zv = frameZ + (float)z / depth;
                                    for (var y = 0; y < height; y += hstep)
                                    {
                                        for (var x = 0; x < width; x += wstep)
                                        {
                                            verts[Ai + 0] = new Vector3( 0.01f,    0, zv);
                                            verts[Ai + 1] = new Vector3(-0.01f,    0, zv);
                                            verts[Ai + 2] = new Vector3( 0f,    0.01f, zv);

                                            var u = (float)x / width;
                                            var v = (float)y / height;
                                            uvs[Ai] = uvs[Ai + 1] = uvs[Ai + 2] = new Vector2(u, v) + frame;

                                            ind[Ai + 0] = Ai + 0;
                                            ind[Ai + 1] = Ai + 1;
                                            ind[Ai + 2] = Ai + 2;

                                            Ai += 3;
                                        }
                                    }
                                }
                            }
                        }
                    }
			    }

                mesh.vertices = verts;
                mesh.uv = uvs;
                mesh.indexFormat = ind.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.SetIndices(ind, MeshTopology.Triangles, 0);
			}

            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);

            return mesh;
        }

        void UpdateKernelShader()
        {
            if(kernelUpdate == KernelUpdate.Basic)
			{
                var m = _kernelMaterial;

                m.SetVector("_EmitterPos", emitterPosition);
                m.SetVector("_EmitterSize", emitterSize);

                var dir = new Vector4(direction.x, direction.y, direction.z, spread);
                m.SetVector("_Direction", dir);

                m.SetVector("_SpeedParams", new Vector2(minSpeed, maxSpeed));

                if (noiseAmplitude > 0)
                {
                    var np = new Vector3(noiseFrequency, noiseAmplitude, noiseSpeed);
                    m.SetVector("_NoiseParams", np);
                    m.EnableKeyword("NOISE_ON");
                }
                else
                {
                    m.DisableKeyword("NOISE_ON");
                }
                var dt = deltaTime * timeMul;
                _noiseTime += dt;
                //m.SetVector("_Config", new Vector4(throttle, lifeDuration, randomSeed, (1f/60f) * timeMul)); // using dt causes flicker when using "tail from speed"
                var flowForThisUpdate = emitAll ? 1f : flow;
                emitAll = false;
                m.SetVector("_Config", new Vector4(flowForThisUpdate, lifeDuration, randomSeed, dt)); 
                m.SetFloat("_LifeVariance", lifeDurationV);
                m.SetFloat("_NoiseTime", _noiseTime);

			}
            else
			{

			}
        }

        private float _noiseTime = 0f;
        private Shader GetShader(string name)
		{
            return Resources.Load<Shader>("Shaders/" + name);
		}

        void ResetResources()
        {
            Debug.Log("ResetResources");
            // Mesh object.
            //if (_mesh == null) _mesh = CreateMesh();

            // Particle buffers.
            if (_particleBuffer1) DestroyImmediate(_particleBuffer1);
            if (_particleBuffer2) DestroyImmediate(_particleBuffer2);

            _particleBuffer1 = CreateBuffer();
            _particleBuffer2 = CreateBuffer();

            if (_shaderTPDrop == null)
            {
                _shaderTPDrop = GetShader("TPDrop");
                _shaderTPDrop_tr = GetShader("TPDrop tr");
                _shaderTPSimple = GetShader("TPSimple");
                _shaderTPSimple_tr = GetShader("TPSimple tr");
                _shaderTPGrid_tr = GetShader("TPGrid tr");
                //_shaderTPTextureData_tr = GetShader("TPTextureData tr");
                _prevShader = _shaderTPDrop_tr;
            }

            if (!_kernelMaterialMRT) _kernelMaterialMRT = CreateMaterial(GetShader("TeaParticleUpdateMRT"));
            if (!_kernelMaterial) _kernelMaterial = CreateMaterial(GetShader("TeaParticleUpdate"));
            if (!_lineMaterial  ) _lineMaterial   = CreateMaterial(_shaderTPDrop_tr);
            //if (!_debugMaterial ) _debugMaterial  = CreateMaterial(debugShader);

            UpdateKernelShader();
            InitializeAndPrewarmBuffers();

            _needsReset = false;
        }

        void InitializeAndPrewarmBuffers()
        {
            // Initialization.
            Graphics.Blit(null, _particleBuffer2, _kernelMaterial, 0);

            // Execute the kernel shader repeatedly.
            for (var i = 0; i < 8; i++)
            {
                Graphics.Blit(_particleBuffer2, _particleBuffer1, _kernelMaterial, 1);
                Graphics.Blit(_particleBuffer1, _particleBuffer2, _kernelMaterial, 1);
            }
        }


		private void OnDisable()
		{
            _needsReset = true;
            if (_mesh) DestroyImmediate(_mesh);
            if (_particleBuffer1) DestroyImmediate(_particleBuffer1);
            if (_particleBuffer2) DestroyImmediate(_particleBuffer2);
            if (_kernelMaterial) DestroyImmediate(_kernelMaterial);
            if (_lineMaterial) DestroyImmediate(_lineMaterial);
            if (_debugMaterial) DestroyImmediate(_debugMaterial);
            _mesh = null;
            _particleBuffer1 = null;
            _particleBuffer2 = null;
            _kernelMaterial = null;
            _lineMaterial = null;
            _debugMaterial = null;
        }
        void BlitMRT(Texture source, RenderTexture out0, RenderTexture out1, Material mat, int pass)
		{
            mat.mainTexture = source;

            Graphics.SetRenderTarget(new RenderBuffer[] { out0.colorBuffer, out1.colorBuffer }, out0.depthBuffer);

            GL.PushMatrix();

            mat.SetPass(pass);

            GL.LoadOrtho();

            GL.Begin(GL.QUADS);

            GL.TexCoord(new Vector3(0, 0, 0));
            GL.Vertex3(0, 0, 0);

            GL.TexCoord(new Vector3(1, 0, 0));
            GL.Vertex3(1, 0, 0);

            GL.TexCoord(new Vector3(1, 1, 0));
            GL.Vertex3(1, 1, 0);

            GL.TexCoord(new Vector3(0, 1, 0));
            GL.Vertex3(0, 1, 0);

            GL.End();

            GL.PopMatrix();
        }

        private Shader _prevShader;
        private bool _prevUseQuads = false;

        void LateUpdate()
        {
            if (_needsReset) ResetResources();

            UpdateKernelShader();

            bool useQuads = forceQuads;

            if (renderStyle == RenderStyle.Trail || renderStyle == RenderStyle.TrailHeavy || kernelUpdate == KernelUpdate.Grid)
                useQuads = true;

            if (_mesh == null || _prevUseQuads != useQuads)
            {
                if (GetComponent<MeshRenderer>() == null) gameObject.AddComponent<MeshRenderer>();
                if (GetComponent<MeshFilter>() == null) gameObject.AddComponent<MeshFilter>();
                _mesh = CreateParticleMesh(BufferWidth, BufferHeight, 1, useQuads);
                gameObject.GetComponent<MeshFilter>().sharedMesh = _mesh;
                gameObject.GetComponent<MeshRenderer>().sharedMaterial = _lineMaterial;
                _prevUseQuads = useQuads;
            }

            if (runSimulation)
            {
                if (Application.isPlaying)
                {

                    if (kernelUpdate == KernelUpdate.Basic)
                    {
                        // Swap the particle buffers.
                        var temp = _particleBuffer1;
                        _particleBuffer1 = _particleBuffer2;
                        _particleBuffer2 = temp;

                        // Execute the kernel shader.
                        Graphics.Blit(_particleBuffer1, _particleBuffer2, _kernelMaterial, 1);
                    }
                    else if (kernelUpdate == KernelUpdate.Grid)
                    {
                        BlitMRT(null, _particleBuffer1, _particleBuffer2, _kernelMaterialMRT, 0);
                    }


                }
                else
                {
                    InitializeAndPrewarmBuffers();
                }

            }

            bool smooth = (((int)renderStyle) & 1) == 0;
            var shader = _shaderTPDrop_tr;
            if (kernelUpdate == KernelUpdate.TextureData)
                shader = _shaderTPSimple_tr;
                //shader = _shaderTPTextureData_tr;
            if (kernelUpdate == KernelUpdate.Grid)
                shader = _shaderTPGrid_tr;
            else if (renderStyle == RenderStyle.Round || renderStyle == RenderStyle.Circle)
                shader = renderBlend == RenderBlend.OpaqueWithDepth ? _shaderTPSimple : _shaderTPSimple_tr;
            else if (renderStyle == RenderStyle.Streak || renderStyle == RenderStyle.Drop)
                shader = renderBlend == RenderBlend.OpaqueWithDepth ? _shaderTPDrop : _shaderTPDrop_tr;

            if (_prevShader != shader)
            {
                _prevShader = shader;
                _lineMaterial.shader = shader;
            }

            if(RenderQueue == -1)
                _lineMaterial.renderQueue = renderBlend == RenderBlend.OpaqueWithDepth ? 2000 : 3000;
            else
                _lineMaterial.renderQueue = RenderQueue;

            if (kernelUpdate == KernelUpdate.TextureData)
            { 
                _lineMaterial.SetTexture("_ParticleTex1", textureData);
                _lineMaterial.SetTexture("_ParticleTex2", textureData);
            }
            else
            { 
                _lineMaterial.SetTexture("_ParticleTex1", _particleBuffer1);
                _lineMaterial.SetTexture("_ParticleTex2", _particleBuffer2);
            }

            if (textureColor != null)
            {
                _lineMaterial.EnableKeyword("USE_MAINTEX_AS_COLOR_SOURCE");
                _lineMaterial.mainTexture = textureColor;
            }
            else
                _lineMaterial.DisableKeyword("USE_MAINTEX_AS_COLOR_SOURCE");

            _lineMaterial.SetColor("_Color", color);

            SetGlow(_lineMaterial, glowEnable, glow, out float sizeMulForGlow, debugGlowSize);

            _lineMaterial.SetVector("_Size", new Vector4(sizeMulForGlow * size * (smooth ? 1.0f : 1f), sizeV, 0f, 0f));
            SetSizeByCameraDistance(_lineMaterial, near, nearFade, far, farFade);
            _lineMaterial.SetFloat("_Tail", tail);
            _lineMaterial.SetFloat("_TailFromSpeed", tailFromSpeed);
            _lineMaterial.SetFloat("_AntiAlias", 400f / (analyticalAntialias * sizeMulForGlow));

            SetAdditive(_lineMaterial, renderBlend == RenderBlend.Additive);

            SetBoxCut(_lineMaterial, _useBoxCut, transform, _boxMatrix, _boxGradient);

            KeywordEnable(_lineMaterial, "TPDROP_SHARP", !smooth);
            KeywordEnable(_lineMaterial, "TPDROP_USE_QUAD", useQuads);
        }

        private static float rsi(float x) { return x / (1 - Mathf.Abs(x)); }
        public static void SetSizeByCameraDistance(Material lineMaterial, float near, float nearFade, float far, float farFade)
		{
            lineMaterial.SetVector("_NearFar", new Vector4(near, 1f / (near * nearFade + 0.000001f), far, 1f / (far * farFade + 0.000001f)));
		}

        public static void SetGlow(Material lineMaterial, bool glowEnable, float glow, out float sizeMul, bool debugGlowSize)
		{
            if(glowEnable)
			{
                // https://madtealab.com/?V=1&C=3&F=3&G=1&W=1043&GW=989&GH=458&GX=0.43279476216396634&GY=0.7213246036066105&GS=0.3026939747542403&a=0.4&b=1.4999999999999998&bMa=2&bN=sizeMul&c=0.2&cMi=-0.999&cMa=0.999&cN=glow&f1=rsmul%28sq%28f3%28x%29%29%2Ca%29&f2=sq%28rsmul%28f3%28x%29%2Ca%29%29&f3=1-%281-x%29%2FsizeMul&Expr=a+%3D+0.5+-+glow+%2A+0.5%3B%0AsizeMul+%3D+rsi%281-a%29%0A
                var a = 0.5f - glow * 0.5f;
                sizeMul = glow < 0f ? 1f : Mathf.Lerp(1f, rsi(1 - a), 0.2f);
                //sizeMul = glow < 0f ? 1f : 1f - Mathf.Log(a * 2f);
            
                if(!debugGlowSize)
			    {
                    KeywordEnable(lineMaterial, "TP_USE_GLOW", true);
                    lineMaterial.SetFloat("_Glow", a);
			    }
                else
			    {
                    KeywordEnable(lineMaterial, "TP_USE_GLOW", false);
			    }
			}
            else
			{
                sizeMul = 1f;
                KeywordEnable(lineMaterial, "TP_USE_GLOW", false);
			}

        }
        public static void SetBoxCut(Material lineMaterial,
                     bool useBoxCut,
                     Transform trans,
                     Matrix4x4 boxMatrix,
                     Vector4 boxGradient)
		{
            KeywordEnable(lineMaterial, "TP_USE_BOXCUT", useBoxCut);
            if(useBoxCut)
			{
                lineMaterial.SetMatrix("_BoxCutMatrix", boxMatrix * trans.localToWorldMatrix);
                lineMaterial.SetVector("_BoxCutGradient", boxGradient);
			}
		}


        public static void SetAdditive(Material lineMaterial, bool additive)
		{
            KeywordEnable(lineMaterial, "TPDROP_ADDITIVE", additive);
			if (additive)
			{
                lineMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                lineMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
			}
            else
			{
                lineMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                lineMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
			}
		}

        static void KeywordEnable(Material m, string keyword, bool enable)
		{
            if(enable)
                m.EnableKeyword(keyword);
            else
                m.DisableKeyword(keyword);
		}

        void OnGUI()
        {
            if (debug && Event.current.type.Equals(EventType.Repaint))
            {
                if (_debugMaterial && _particleBuffer2)
                {
                    var rect = new Rect(0, 0, BufferWidth, BufferHeight);
                    Graphics.DrawTexture(rect, _particleBuffer2, _debugMaterial);
                }
            }
        }
        
        void OnDrawGizmosSelected()
        {
            if(kernelUpdate == KernelUpdate.Basic)
			{
                Gizmos.color = Color.yellow;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireCube(emitterPosition, emitterSize);
			}
        }

    }
}

