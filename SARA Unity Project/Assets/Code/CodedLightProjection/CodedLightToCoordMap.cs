
// by: Martin Eklund, music@teadrinker.net
// Licence: GNU GPL v3.0 (https://www.gnu.org/licenses/gpl-3.0.en.html)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace teadrinker
{

    public class CodedLightToCoordMap : MonoBehaviour
    {
        ImgContext _ctx;

        void OnEnable()
        {
            if (_ctx == null)
            {
                _ctx = new ImgContext();
                _ctx.RunTests();
                _ctx.Add("inpaint", 1, Resources.Load<Shader>("Inpainting"));
                _ctx.Add("median", 1, Resources.Load<Shader>("Median3x3"));
                _ctx.Add("fromGrayCode", 1, Resources.Load<Shader>("FromGrayCode"));
            }
        }

        public bool useDiffCheck = false;
        public bool useInpainting = true;
        public bool useInversion = false;
        public float amount = 1f;
        public float noiseThreshold = 0.1f;
        public float validThresholdAbs = 0.1f;
        public float codedLightThresholdRel = 0.3f;
        public int waitFrames = 40;
        public int averageFrames = 5;
        public int StartAtBit = 1;
        private bool _inverted = false;
        [Space]
        public Material ProjectorStageMat;
        [Space]
        public RenderTexture DebugOutput;
        public RenderTexture DebugValidAreas;
        public int _bitStage = 9; // for debug readout only
        public enum State
        {
            Start,
            CaptureBg,
            CaptureFullFill,
            CaptureDiffX,
            CaptureDiffY,
            PostProcess,
            Done,
        }
        private State _curState = State.Start;
        public State GetState() { return _curState; }
        public void Restart()
        {
            Cleanup();
            Debug.Log("restart");
            _curState = State.Start;
        }

        RenderTexture _prevInputTex = null;
        Img _bg = null;
        Img _fullFill = null;
        Img _thresholdBasedOnFullFill = null;
        Img _codedLightFrame = null;
        Img _codedLightFrameInv = null;
        Img _validAreas = null;
        Img _output = null;

        void Cleanup()
        {
            UnityGpuHelpers.SafeRelease(ref _prevInputTex);
            Img.SafeRelease(ref _bg);
            Img.SafeRelease(ref _fullFill);
            Img.SafeRelease(ref _thresholdBasedOnFullFill);
            Img.SafeRelease(ref _codedLightFrame);
            Img.SafeRelease(ref _codedLightFrameInv);
            Img.SafeRelease(ref _validAreas);
            Img.SafeRelease(ref _output);
        }

        void OnDisable()
        {
            Cleanup();
        }

        private int _frameCounter = 0;
        bool CaptureAveragedFrame(ref Img captureImg, Img inputImg, float diff, float noiseThreshold)
        {
            _frameCounter++;
            //Debug.LogWarning("diff " + diff + " " + noiseThreshold);
            if (diff < noiseThreshold)
            {
                //Debug.LogWarning("diff " + (diff < noiseThreshold));
                if (_frameCounter == 1) // first frame
                    Img.RequestAndAssign(ref captureImg, inputImg.grayScale());
                else
                    Img.RequestAndAssign(ref captureImg, captureImg.lerp(inputImg.grayScale(), 1f / _frameCounter));

                if (_frameCounter >= averageFrames)
                {
                    //Debug.Log("CaptureAveragedFrame complete " + _curState.ToString() + " Stage:" + _bitStage);
                    _frameCounter = 0;
                    return true;
                }
            }
            else
            {
                Img.SafeRelease(ref captureImg);
                Debug.Log("Capture interupted durign state " + _curState.ToString() + " at frame " + _frameCounter);
                _frameCounter = 0;
            }
            return false;
        }

        private float _waitTimeStamp = 0;
        int BitsNeeded(int v)
        {
            int r = 0;

            while ((v >>= 1) > 0)
            {
                r++;
            }
            return r;
        }

        public void PushNewFrame(RenderTexture input)
        {
            var inputTex = input;

            if (inputTex == null)
                return;

            if (Time.realtimeSinceStartup - _waitTimeStamp < waitFrames * (1f / 30f))
            {
                return;
            }

            if (_prevInputTex != null)
            {
                var inputImg = _ctx.DataSource(inputTex);
                var diff = noiseThreshold * 0.5f;

                if (useDiffCheck)
                {
                    var prevInputImg = _ctx.DataSource(_prevInputTex);

                    diff = inputImg
                        .absdiff(prevInputImg)
                        .reduceMean().RequestPixelMaxOfRGB();
                }

                if (_curState == State.Start)
                {
                    Img.SafeRelease(ref _bg);
                    _bitStage = 16;
                    _inverted = false;
                    _waitTimeStamp = Time.realtimeSinceStartup;
                    _curState++;
                }
                else if (_curState == State.CaptureBg)
                {
                    if (CaptureAveragedFrame(ref _bg, inputImg, diff, noiseThreshold))
                    {
                        _waitTimeStamp = Time.realtimeSinceStartup;
                        _curState++;
                    }
                }
                else if (_curState == State.CaptureFullFill)
                {
                    if (CaptureAveragedFrame(ref _fullFill, inputImg, diff, noiseThreshold))
                    {
                        var fullFill = _fullFill.absdiff(_bg);
                        Img.RequestAndAssign(ref _thresholdBasedOnFullFill, fullFill.mul(codedLightThresholdRel), false);
                        Img.RequestAndAssign(ref _validAreas, fullFill.greaterThan(validThresholdAbs));
                        Img.SafeRelease(ref _fullFill);
                        _bitStage = StartAtBit;
                        _waitTimeStamp = Time.realtimeSinceStartup;
                        _curState++;
                    }
                }
                else if (_curState == State.CaptureDiffX || _curState == State.CaptureDiffY)
                {
                    var complete = _inverted ?
                                      CaptureAveragedFrame(ref _codedLightFrameInv, inputImg, diff, noiseThreshold)
                                    : CaptureAveragedFrame(ref _codedLightFrame, inputImg, diff, noiseThreshold);
                    if (complete)
                    {
                        if (!useInversion || _inverted)
                        {
                            var codedLightOnes = _codedLightFrame.absdiff(_bg).greaterThan(_thresholdBasedOnFullFill);
                            codedLightOnes.RequestTexure(); // cache, as this is used multiple times

                            if (useInversion)
                            {
                                var codedLightZeroes = _codedLightFrameInv.absdiff(_bg).greaterThan(_thresholdBasedOnFullFill);
                                Img.RequestAndAssign(ref _validAreas, _validAreas.and(codedLightOnes.xor(codedLightZeroes))); // use xor to invalidate areas that bleed due to bloom in the lens or material
                                                                                                                              //Img.RequestAndAssign(ref _validAreas, _validAreas.and(codedLightOnes.or(codedLightZeroes))); 
                            }

                            var channelMask = new Vector4(_curState == State.CaptureDiffX ? 1f : 0f, _curState == State.CaptureDiffY ? 1f : 0f, 0f, 1f);
                            Img.RequestAndAccumulate(ref _output, codedLightOnes.mul(channelMask * (1 << _bitStage)).HighPrecision());
                            codedLightOnes.Release();

                            _bitStage++;
                            if (_bitStage > BitsNeeded(_curState == State.CaptureDiffX ? inputImg.w : inputImg.h))
                            {
                                _bitStage = StartAtBit;
                                _curState++;
                            }
                        }
                        if (useInversion)
                            _inverted = !_inverted;
                        _waitTimeStamp = Time.realtimeSinceStartup;
                    }
                }
                if (_curState == State.PostProcess)
                {
                    var tmp = _output;
                    if (ProjectorStageMat.GetFloat("_UseGrayCodes") > 0.5f)
                        tmp = tmp.call("fromGrayCode").HighPrecision();
                    tmp = tmp.mul(_validAreas)/*.mul(1f / 1020f).call("median")*/;
                    if (useInpainting)
                        tmp = tmp.call("inpaint").call("inpaint");
                    Img.RequestAndAssign(ref _output, tmp);
                    _curState++;
                }

                if (_output != null && _output.result != null)
                    DebugOutput = (RenderTexture)_output.result.AsTexture();

                if (_validAreas != null && _validAreas.result != null)
                    DebugValidAreas = (RenderTexture)_validAreas.result.AsTexture();

                var stageMul = _curState == State.CaptureDiffY ? -1 : 1;
                ProjectorStageMat.SetFloat("_Stage", stageMul * (_bitStage + 0.5f));
                ProjectorStageMat.SetFloat("_Amount", (_inverted || _curState == State.CaptureFullFill) ? -amount : amount);

                //Debug.Log(diff);
                //_ctx.ReportStats();


                /*               
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("1:  " +img.result.w +" "+ img.result.h +"  :"  + _ctx.DataSource(img.result.AsColor(), img.result.w, img.result.h).reduceSum().RequestPixelMaxOfRGB(false, false)))
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("2:  " +img.result.w +" "+ img.result.h +"  :"  + _ctx.DataSource(img.result.AsColor(), img.result.w, img.result.h).reduceSum().RequestPixelMaxOfRGB(false, false)))
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("3:  " +img.result.w +" "+ img.result.h +"  :"  + _ctx.DataSource(img.result.AsColor(), img.result.w, img.result.h).reduceSum().RequestPixelMaxOfRGB(false, false)) )
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("4:  " +img.result.w +" "+ img.result.h +"  :"  + _ctx.DataSource(img.result.AsColor(), img.result.w, img.result.h).reduceSum().RequestPixelMaxOfRGB(false, false)) )
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("5:  " +img.result.w +" "+ img.result.h +"  :"  + _ctx.DataSource(img.result.AsColor(), img.result.w, img.result.h).reduceSum().RequestPixelMaxOfRGB(false, false)) )
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("6:  " +img.result.w +" "+ img.result.h +"  :"  + _ctx.DataSource(img.result.AsColor(), img.result.w, img.result.h).reduceSum().RequestPixelMaxOfRGB(false, false)) )
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("7:  " +img.result.w +" "+ img.result.h +"  :"  + _ctx.DataSource(img.result.AsColor(), img.result.w, img.result.h).reduceSum().RequestPixelMaxOfRGB(false, false)) )
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("8:  " +img.result.w +" "+ img.result.h +"  :"  + _ctx.DataSource(img.result.AsColor(), img.result.w, img.result.h).reduceSum().RequestPixelMaxOfRGB(false, false)) )
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("8:  " +img.result.w +" "+ img.result.h +"  :"  + _ctx.DataSource(img.result.AsColor(), img.result.w, img.result.h).reduceSum().RequestPixelMaxOfRGB(false, false)) )
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("9:  " +img.result.w +" "+ img.result.h +"  :"  + _ctx.DataSource(img.result.AsColor(), img.result.w, img.result.h).reduceSum().RequestPixelMaxOfRGB(false, false)) )
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("10: " +img.result.w +" "+ img.result.h +"  :"  + _ctx.DataSource(img.result.AsColor(), img.result.w, img.result.h).reduceSum().RequestPixelMaxOfRGB(false, false)) )
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("11: " +img.result.w +" "+ img.result.h +"  :"  + _ctx.DataSource(img.result.AsColor(), img.result.w, img.result.h).reduceSum().RequestPixelMaxOfRGB(false, false)) )


                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("1  " +img.result.w +" "+ img.result.h +"  :"   + _ctx.DataSource(img.result.AsTexture(_ctx)).reduceSum().RequestPixelMaxOfRGB(false, true)))
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("2  " +img.result.w +" "+ img.result.h +"  :"   + _ctx.DataSource(img.result.AsTexture(_ctx)).reduceSum().RequestPixelMaxOfRGB(false, true)))
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("3  " +img.result.w +" "+ img.result.h +"  :"   + _ctx.DataSource(img.result.AsTexture(_ctx)).reduceSum().RequestPixelMaxOfRGB(false, true)) )
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("4  " +img.result.w +" "+ img.result.h +"  :"   + _ctx.DataSource(img.result.AsTexture(_ctx)).reduceSum().RequestPixelMaxOfRGB(false, true)) )
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("5  " +img.result.w +" "+ img.result.h +"  :"   + _ctx.DataSource(img.result.AsTexture(_ctx)).reduceSum().RequestPixelMaxOfRGB(false, true)) )
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("6  " +img.result.w +" "+ img.result.h +"  :"   + _ctx.DataSource(img.result.AsTexture(_ctx)).reduceSum().RequestPixelMaxOfRGB(false, true)) )
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("7  " +img.result.w +" "+ img.result.h +"  :"   + _ctx.DataSource(img.result.AsTexture(_ctx)).reduceSum().RequestPixelMaxOfRGB(false, true)) )
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("8  " +img.result.w +" "+ img.result.h +"  :"   + _ctx.DataSource(img.result.AsTexture(_ctx)).reduceSum().RequestPixelMaxOfRGB(false, true)) )
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("9  " +img.result.w +" "+ img.result.h +"  :"   + _ctx.DataSource(img.result.AsTexture(_ctx)).reduceSum().RequestPixelMaxOfRGB(false, true)) )
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("10 " +img.result.w +" "+ img.result.h +"  :"   + _ctx.DataSource(img.result.AsTexture(_ctx)).reduceSum().RequestPixelMaxOfRGB(false, true)) )
                                .reduceSumIter().WhenResultIsSet(img => Debug.Log("11 " +img.result.w +" "+ img.result.h +"  :"   + _ctx.DataSource(img.result.AsTexture(_ctx)).reduceSum().RequestPixelMaxOfRGB(false, true)) )

                               .reduceSumIter()
                               .reduceSumIter()
                               .reduceSumIter()
                               .reduceSumIter()
                               .reduceSumIter()
                               .reduceSumIter()
                               .reduceSumIter()
                               .reduceSumIter()
                               .reduceSum()
                               .RequestPixelMaxOfRGB();

                              //.reduceMean().RequestPixelMaxOfRGB();
                           Debug.Log(diff);
                               */

            }

            UnityGpuHelpers.MakeSureValidRT(ref _prevInputTex, inputTex);
            Graphics.CopyTexture(inputTex, _prevInputTex);
        }
    }

}