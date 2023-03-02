// Custom compression algorithm for 16 bit integers, optionally lossy

// by: Martin Eklund, music@teadrinker.net
// Licence: GNU GPL v3.0 (https://www.gnu.org/licenses/gpl-3.0.en.html)

//#define USE_UNSAFE

using System.Collections;
using System.Collections.Generic;
using UnityEngine;



/*

kinect test frame stats:

OP__SET_TYPE_REP      __OP__LENGTH1       13861
OP__SET_TYPE_DIFF_INT4__OP__LENGTH1        9215
OP__SET_TYPE_DIFF_INT4__OP__LENGTH_UINT4   7605
OP__SET_TYPE_REP      __OP__LENGTH2        4201
OP__SET_TYPE_DIFF_INT4__OP__LENGTH2        4010
OP__SET_TYPE_REP      __OP__LENGTH_UINT4   3758
OP__SET_TYPE_UINT16   __OP__LENGTH1        1828
OP__SET_TYPE_DIFF_INT8__OP__LENGTH_UINT4    908

OP__SET_TYPE_REP      __OP__LENGTH_UINT12   702
OP__SET_TYPE_DIFF_INT4__OP__LENGTH_UINT12   449
OP__SET_TYPE_DIFF_INT8__OP__LENGTH_UINT12   381
OP__SET_TYPE_UINT16   __OP__LENGTH2          93
OP__SET_TYPE_UINT16   __OP__LENGTH_UINT4     41
OP__SET_TYPE_DIFF_INT8__OP__LENGTH2          42
OP__SET_TYPE_DIFF_INT8__OP__LENGTH1          60
OP__SET_TYPE_UINT16   __OP__LENGTH_UINT12     1

length(s) distribution   (zipf law maybe?)

0,24964,8346,4212,1299,1643,1209,836,664,407,396,342,303,235,179,169,165,125,128,102,94,69,82,50,45...

*/

namespace teadrinker
{

    public static class DiffRLE
    {
        // compressed format:  <op> <length> <data #1> <data #2> ...
        // (operand is 4 bytes)
        // (bitsize of length depend on OP__LENGTH_UINT4 / OP__LENGTH_UINT12, length is omitted for OP__LENGTH1 and OP__LENGTH2)
        // (bitsize of each data depend on OP__SET_TYPE_DIFF_INT4 / OP__SET_TYPE_DIFF_INT8 / OP__SET_TYPE_DIFF_INT16, data is omitted for OP__SET_TYPE_REP)

        const uint OP__SET_TYPE_REP        = 0;
        const uint OP__SET_TYPE_DIFF_INT4  = 1;   // -8    7
        const uint OP__SET_TYPE_DIFF_INT8  = 2;   // –128  127
       // const uint OP__SET_TYPE_DIFF_INT16 = 3;   // –32768 32767
        const uint OP__SET_TYPE_UINT16     = 3;   

        const uint OP__LENGTH1             = 0 << 2;  // 1
        const uint OP__LENGTH2             = 1 << 2;  // 2
        const uint OP__LENGTH_UINT4        = 2 << 2;  // 3 - 18
        const uint OP__LENGTH_UINT12       = 3 << 2;  // 19 - 4113   (4114 is reserved for eos / pad)

        const int opBits = 4;
        const uint opMask = (1 << opBits) - 1;


        
        static string debugOpType(uint i)
        {
            i = i & 3;
            if (i == OP__SET_TYPE_REP) return "OP__SET_TYPE_REP";
            else if (i == OP__SET_TYPE_DIFF_INT4) return "OP__SET_TYPE_DIFF_INT4";
            else if (i == OP__SET_TYPE_DIFF_INT8) return "OP__SET_TYPE_DIFF_INT8";
            else if (i == OP__SET_TYPE_UINT16) return "OP__SET_TYPE_UINT16";
            //else if (i == OP__SET_TYPE_DIFF_INT16) return "OP__SET_TYPE_DIFF_INT16";
            return "error!!!";
        }
        static string debugLenType(uint i)
        {
            i = i & (4|8);
            if (i == OP__LENGTH1) return "OP__LENGTH1";
            else if (i == OP__LENGTH2) return "OP__LENGTH2";
            else if (i == OP__LENGTH_UINT4) return "OP__LENGTH_UINT4";
            else if (i == OP__LENGTH_UINT12) return "OP__LENGTH_UINT12";
            //else if (i == OP__SET_TYPE_DIFF_INT16) return "OP__SET_TYPE_DIFF_INT16";
            return "error!!!";
        }

        static private string debugUint(uint cur)
        {
            return (cur & 255) + "  " + ((cur >> 8) & 255) + "  " + ((cur >> 16) & 255) + "  " + ((cur >> 24) & 255);
        }
        static private string debugUintdataShift(uint cur)
        {
            return (cur & 15) + "  " + ((cur >> 4) & 15) + "  " + ((cur >> 8) & 15) + "  " + ((cur >> 12) & 15) + "     " +((cur >> 16) & 15) + "  " + ((cur >> 20) & 15) + "  " + ((cur >> 24) & 15) + "  " + ((cur >> 28) & 15);
        }
        class EncoderStream
        {
            uint cur = 0;
            int curShift = 0;
            List<uint> outEncoded;
            public void Flush()
            {
                if (curShift != 0)
                {
                    ForceFlush();
                }
            }
            private void ForceFlush()
            {
                //Debug.Log("outEncoded "+ cur +"    " + debugUint(cur));

                outEncoded.Add(cur);
                curShift = 0;
                cur = 0;
            }

            private void AllocBits(uint bits)
            {
                if (curShift + bits > 32)
                {
                    ForceFlush();
                }

            }
            private bool HasBits(uint bits)
            {
                return curShift + bits <= 32;
            }

            public void Write4(uint value)
            {
                if (value > (1 << 4) - 1)
                    Debug.LogError("value > (1<<4) - 1");

                //Debug.Log("Write4 " + value);


                AllocBits(4);
                cur |= ((uint)value) << curShift;
                curShift += 4;

            }

            public void Write8(uint value)
            {
                if (value > (1 << 8) - 1)
                    Debug.LogError("value > (1<<8) - 1");

                //Debug.Log("Write8 " + value);

                if (!HasBits(8))
                {

                    if (HasBits(4))
                    {
                        // split data across uint boundry

                        Write4(value & 15);
                        Write4(value >> 4);

                        return;
                    }

                    ForceFlush();
                }
                cur |= ((uint)value) << curShift;
                curShift += 8;

            }
            public void Write12(uint value)
            {
                if(value > (1<<12) - 1)
                    Debug.LogError("value > (1<<12) - 1");

                //Debug.Log("Write12 " + value);

                if (!HasBits(12))
                {
                    if (HasBits(4))
                    {
                        if (HasBits(8)) // avoid stupid boundry split
                        {
                            Write8(value & 255);
                            Write4(value >> 8);
                        }
                        else
                        {
                            Write4(value & 15);
                            Write8(value >> 4);
                        }

                        return;
                    }

                    ForceFlush();
                }
                cur |= ((uint)value) << curShift;
                curShift += 12;

            }

            public void Write16(uint value)
            {
                if (value > (1 << 16) - 1)
                    Debug.LogError("value > (1<<16) - 1");

                //Debug.Log("Write16 " + value);

                if (!HasBits(16))
                {
                    if (HasBits(4))
                    {
                        // split data across uint boundry

                        Write8(value & 255); // this might split data too!
                        Write8(value >> 8);

                        return;
                    }

                    ForceFlush();
                }
                cur |= ((uint)value) << curShift;
                curShift += 16;

            }
            public void WriteEOS()
			{
                //WriteOPAndLength(OP__LENGTH_UINT12 | OP__SET_TYPE_DIFF_INT16, 4114, false);
                WriteOPAndLength(OP__LENGTH_UINT12 | OP__SET_TYPE_UINT16, 4114, false);
            }

            public int[] collectTypeStats = null;
            public int[] collectLenStats = null;

            public void WriteOPAndLength(uint setTypeOp, uint len, bool protect = true)
            {
                if (protect && len > 4113)
                    throw new System.Exception("too long");


                var op = OP__LENGTH_UINT12;

                if (len == 1)
                    op = OP__LENGTH1;
                else if (len == 2)
                    op = OP__LENGTH2;
                else if (len <= 18)
                    op = OP__LENGTH_UINT4;

                //Debug.Log("WriteOPAndLength " + debugOpType(setTypeOp) + "   n=" + len + "   cur:"  + cur);

                Write4(op | setTypeOp);

                if(collectTypeStats != null)
				{
                    collectTypeStats[op | setTypeOp]++;
                    if (len <= 4113)
                        collectLenStats[len]++;
                }

                switch (op)
                {
                    case OP__LENGTH_UINT4:
                        Write4(len - 3);
                        break;

                    case OP__LENGTH_UINT12:
                        Write12(len - 19);
                        break;
                }
            }


            public EncoderStream(List<uint> _outEncoded)
            {
                outEncoded = _outEncoded;
            }
        }
        private static int errorMetric(int x) { return abs(x); }
        private static int abs(int x) { return x < 0 ? -x : x; }
        private static int min(int a, int b) { return a < b ? a : b; }
        private static int clamp(int x, int mi, int ma) { return x < mi ? mi : (x > ma ? ma : x); }
        public static void Encode(ushort[] prevBuf, ushort[] buf, List<uint> outEncoded, float errorTolerance = 0f, bool printStats = false)
        {
            var o = new EncoderStream(outEncoded);
            if(printStats)
			{
                o.collectTypeStats = new int[16];
                o.collectLenStats = new int[4114];
            }
            int N = buf.Length;
            int prevWrite = 0;

            for (int i = 0; i < N;)
            {
                int outsideToleranceN = 0;
                int nRep=0, n4=0, n8=0;
                for (int jj = 0; jj < 4113 && i+jj < N; jj++)
                {
                    int errorRep = 0;
                    int error4 = 0;
                    int error8 = 0;
                    int prevValRep = prevWrite;
                    int prevVal4 = prevWrite;
                    int prevVal8 = prevWrite;
                    int maxIt = min(4113, N - i - jj);
                    nRep = 0;
                    n4 = 0;
                    n8 = 0;
                    bool checkRep = true;
                    bool check4 = true;
                    bool check8 = true;
                    int j = 0;
                    for (; j < maxIt; j++)
                    {
                        var newVal = (int)buf[i + j + jj];
                        bool withinTolerance = false;

                        if (checkRep)
                        {
                            var valRep = prevValRep;
                            errorRep += errorMetric(valRep - newVal);
                            prevValRep = valRep;
                            if (errorRep / ((float)(j + 1)) > errorTolerance)
                            {
                                checkRep = false;
                                if (j != 0) // if available, repetition is always the best case
                                    break;
                            }
                            else
							{
                                nRep = j + 1;
                                withinTolerance = true;
							}
                        }

                        if (check4)
                        {
                            var diff4 = clamp(newVal - prevVal4, -8, 7);
                            var val4 = prevVal4 + diff4;
                            var curErr = errorMetric(val4 - newVal);
                            var curErr2 = newVal - prevVal4;
                            //Debug.Log("check4  " + newVal +" "+ prevVal4 +"  diff"+ diff4);
                            error4 += curErr;
                            prevVal4 = val4;
                            if (curErr2 == 0 || error4 / ((float)(j + 1)) > errorTolerance) // break on curErr2 == 0, cos probably better to encode as rep
							{
                                check4 = false;
                            }
                            else
							{
                                n4 = j + 1;
                                withinTolerance = true;
							}
                        }

                        if (check8)
                        {
                            var diff8 = clamp(newVal - prevVal8, -128, 127);
                            var val8 = prevVal8 + diff8;
                            var curErr = errorMetric(val8 - newVal);
                            var curErr2 = newVal - prevVal8;
                            //Debug.Log("check8  " + newVal + " " + prevVal8 + "  diff" + diff8);
                            error8 += curErr;
                            prevVal8 = val8;
                            if (curErr2 == 0 || error8 / ((float)(j + 1)) > errorTolerance) // break on curErr2 == 0, cos probably better to encode as rep
                            {
                                check8 = false;
                            }
                            else
							{
                                n8 = j + 1;
                                withinTolerance = true;
							}
                        }

                        if (!withinTolerance)
                            break;
                    }
                    if (j == 0)
                    {
                        if (outsideToleranceN > 0 && abs(((int)buf[i + j + jj]) - prevWrite) < 128)
                            break;
                        prevWrite = buf[i + j + jj];
                        outsideToleranceN++;
                    }
                    else
                    {
                        break;
                    }
                }
                if(outsideToleranceN > 0)
				{
                    /*
                    o.WriteOPAndLength(OP__SET_TYPE_DIFF_INT16, (uint) outsideToleranceN);
                    for(int k = 0; k < outsideToleranceN; k++)
					{
                        var newVal = (int)buf[i + k];
                        var dif = newVal - prevWrite;

                        //if (dif < -32768) dif += 65536;
                        //if (dif > 32767) dif -= 65536;
                        //var unsig = ((uint)(dif << 16)) >> 16;

                        //var unsig = (uint)(ushort)dif;

                        //if      (dif > 32767) dif = 32767 - dif;
                        //if (dif < -32768) dif += 65536 - 1;
                        var unsig = (uint)(ushort)dif;

                        o.Write16(unsig);
                        prevWrite = newVal;
					}
                    */

                    o.WriteOPAndLength(OP__SET_TYPE_UINT16, (uint) outsideToleranceN);
                    for (int k = 0; k < outsideToleranceN; k++)
                    {
                        var val = buf[i + k];
                        o.Write16(val);
                        prevWrite = (int) val;
                    }

                    i += outsideToleranceN;
                }
				else
				{
                    if(nRep > 0)
					{
                        o.WriteOPAndLength(OP__SET_TYPE_REP, (uint) nRep);
                        i += nRep;
					}
                    else
					{
                        const int weight4 = 2;
                        const int weight8 = 1;
                        if (n4 * weight4 > n8 * weight8)
						{
                            o.WriteOPAndLength(OP__SET_TYPE_DIFF_INT4, (uint) n4);
                            for(int k = 0; k < n4; k++)
					        {
                                var newVal = (int)buf[i + k];
                                var dif = clamp(newVal - prevWrite, -8, 7);
                                var unsig = ((uint) (dif << 28)) >> 28;
                                o.Write4(unsig);
                                prevWrite += dif;
					        }
                            i += n4;
						}
                        else
						{
                            o.WriteOPAndLength(OP__SET_TYPE_DIFF_INT8, (uint) n8);
                            for(int k = 0; k < n8; k++)
					        {
                                var newVal = (int)buf[i + k];
                                var dif = clamp(newVal - prevWrite, -128, 127);
                                var unsig = ((uint) (dif << 24)) >> 24;
                                o.Write8(unsig);
                                prevWrite += dif;
					        }
                            i += n8;
						}

					}
				}

            }
            o.WriteEOS();
            o.Flush();

            if (printStats)
			{
                string stats = "";
                for(uint i = 0; i < 16; i++)
				{
                    stats += debugOpType(i)+"__"+debugLenType(i) + "   " + o.collectTypeStats[i] + "\n";
				}
                Debug.Log(stats);
                Debug.Log(string.Join(" ", o.collectLenStats));
            }
        }

        public static void Decode(ushort[] buf, List<uint> encoded)
        {
            Decode(buf, encoded.ToArray(), 0, encoded.Count);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public
#if USE_UNSAFE
            unsafe
#endif
            static void advanceBits(int n, ref int dataShift, ref uint data, ref uint dataNext, ref int i,
#if USE_UNSAFE
                        uint* encodedData,
#else
                        uint[] encodedData,
#endif
                        int endOffset
                        )
		{
            // hmm, this is actually pretty ugly...
            // for a more faster (optimized) decoder, better use uint aligned chunks

            if(dataShift - n <= 0)
			{
                n -= dataShift;
                dataShift = 32;
                i++;
                data = dataNext;
                dataNext = i >= endOffset ? 0xFFFFFFFF : encodedData[i];
                if (n == 0)
                    return;
			}

            data >>= n;
            dataShift -= n;
            data |= dataNext << dataShift;

            //Debug.Log(debugUintdataShift(data) + " --- " + debugUintdataShift(dataNext));
            //Debug.Log(debugUintdataShift(encodedData[i-1]) + " --- " + debugUintdataShift(encodedData[i]));
        }
        public static void Decode(ushort[] _buf, uint[] _encodedData, int startOffset = 0, int endOffset = -1)
        {
            if (endOffset == -1)
                endOffset = _encodedData.Length;

            ushort prevWrite = 0;
            uint curType = 0;
            uint todoN = 0;
            uint bufPos = 0;
            int diff = 0;

#if USE_UNSAFE
            unsafe
            {
                fixed (ushort* buf = _buf) { 
                fixed (uint* encodedData = _encodedData)
                {
#else
            {
            ushort[] buf = _buf;
            uint[] encodedData = _encodedData;
            {{
#endif
                    int i = startOffset;
                    uint data = encodedData[i];
                    i++;
                    uint dataNext = i >= endOffset ? 0xFFFFFFFF : encodedData[i];
                    int dataShift = 32;

                    while (i < endOffset + 1)
                    {
                        //Debug.Log(" -- -- Decode Loop start       data:  "+debugUint(data) + "        dataNext:  "+debugUint(dataNext));
                        //while (dataShift < 8)
                        //{
                            //Debug.Log(" -- Decode Loop start   pos="+ bufPos + "  n=" + todoN);
                        if(todoN > 0)
						{
                            switch (curType)
                            {
                                case OP__SET_TYPE_REP:
                                    // this case is actually not needed (implemented to increase performance only)
                                    while(todoN > 1)
									{
                                        buf[bufPos] = prevWrite;
                                        bufPos++;
                                        todoN--;
                                    } 
                                    break;

                                case OP__SET_TYPE_DIFF_INT4:
                                    diff = ((int) (data<<28)) >> 28;  // cast shifted value for sign-bit to be correct 

                                    //if (diff < -8 || diff > 7) Debug.LogError("diff < -8 || diff > 7");

                                    advanceBits(4, ref dataShift, ref data, ref dataNext, ref i, encodedData, endOffset);

                                    prevWrite = (ushort) ((int)prevWrite + diff);
                                    break;

                                case OP__SET_TYPE_DIFF_INT8:
                                    diff = ((int) (data<<24)) >> 24; // cast shifted value for sign-bit to be correct 

                                    //if (diff < -128 || diff > 127) Debug.LogError("diff < -128 || diff > 127");

                                    advanceBits(8, ref dataShift, ref data, ref dataNext, ref i, encodedData, endOffset);

                                    prevWrite = (ushort) ((int)prevWrite + diff);
                                    break;

                                case OP__SET_TYPE_UINT16:
                                    prevWrite = (ushort) (data & 65535);

                                    advanceBits(16, ref dataShift, ref data, ref dataNext, ref i, encodedData, endOffset);

                                    break;
/*
                                case OP__SET_TYPE_DIFF_INT16:
                                    diff = ((int) (data<<16)) >> 16;

                                    if (diff < -32768 || diff > 32767) Debug.LogError("diff < -32768 || diff > 32767");

                                    data >>= 16;
                                    dataShift += 4;
                                    data |= dataNext << ((8 - dataShift) * 4);

                                    prevWrite = (ushort) ((int)prevWrite + diff);
                                    break;
*/
                            }
                            todoN--;
                            //Debug.Log("Write buf[" + bufPos + "] = " + prevWrite + "    todoN:" + todoN + "  i=" + i + "  dataShift=" + dataShift);
                            buf[bufPos] = prevWrite;
                            bufPos++;
                        }
                        else
						{
                            //Debug.Log("-- Decode Loop else   pos = "+ bufPos + "  n = " + todoN);
                            uint op = data & opMask;
                            advanceBits(4, ref dataShift, ref data, ref dataNext, ref i, encodedData, endOffset);

                            curType = op & (1 | 2);

                            switch (op & (4|8))
                            {

                                case OP__LENGTH1:
                                    todoN = 1;
                                    break;

                                case OP__LENGTH2:
                                    todoN = 2;
                                    break;

                                case OP__LENGTH_UINT4:
                                    todoN = (data & 15) + 3;
                                    advanceBits(4, ref dataShift, ref data, ref dataNext, ref i, encodedData, endOffset);
                                    break;

                                case OP__LENGTH_UINT12:
                                    todoN = (data & ((1<<12) - 1)) + 19;

                                    if(todoN == 4114) // eos
                                        return;

                                    advanceBits(12, ref dataShift, ref data, ref dataNext, ref i, encodedData, endOffset);
                                    break;
                            }
                            //Debug.Log("Decode " + debugOpType(curType) + "  n=" + todoN + "  i="+i + "  dataShift="+dataShift);
						}
                        //Debug.Log("dataShift " + dataShift);
                    }
                    Debug.LogError("terminated without eos");
                }}
            }
        }


        static void Check(ushort[] a, ushort[] b, ushort[] org_a, string test, List<uint> encoded)
        {
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    Debug.LogError("DiffRLE. test failed at " + i + " : " + test + "       encode from (" + org_a[i] + ") to " + a[i] + ", should be " + b[i] + "      :" + string.Join(", ", encoded.ToArray())
                        //+ "\n\n" + string.Join(", ",a) + "    " + string.Join(", ", b)
                        );
            }
        }
        static void RunTest(string name, ushort[] a, ushort[] b)
        {
            var in_b = b;
            b = new ushort[b.Length];
            System.Array.Copy(in_b, b, b.Length);

            var in_a = a;
            a = new ushort[a.Length];
            System.Array.Copy(in_a, a, a.Length);

            List<uint> encoded = new List<uint>();
            Debug.Log("test " + name + " : " + string.Join(", ", a) + "    " + string.Join(", ", b));
            Encode(a, b, encoded);
            Decode(a, encoded);
            Check(a, b, in_a, name, encoded);
        }
        public static void RunTests()
        {
            RunTest("seq4", new ushort[] { 5, 0, 6, 1, 7, 2, 8, 3, 9, 4, 10, 5, 11, 6, 12, 7 }, new ushort[] { 5, 0, 6, 1, 7, 2, 8, 3, 9, 4, 10, 5, 11, 6, 12, 7 });
            RunTest("seq8", new ushort[] { 50, 0, 51, 1, 52, 2, 53, 3, 54, 4, 55, 5, 56, 6 }, new ushort[] { 50, 0, 51, 1, 52, 2, 53, 3, 54, 4, 55, 5, 56, 6 });
            RunTest("seq16", new ushort[] { 500, 0, 501, 1, 502, 2, 503, 3, 504, 4, 505, 5, 506, 6 }, new ushort[] { 500, 0, 501, 1, 502, 2, 503, 3, 504, 4, 505, 5, 506, 6 });

            RunTest("basic 1", new ushort[] { 0 }, new ushort[] { 2 });
            RunTest("basic 2", new ushort[] { 0 }, new ushort[] { 22 });
            RunTest("basic 3", new ushort[] { 0 }, new ushort[] { 500 });

            RunTest("same 1", new ushort[] { 2 }, new ushort[] { 2 });
            RunTest("same 2", new ushort[] { 22 }, new ushort[] { 22 });
            RunTest("same 3", new ushort[] { 500 }, new ushort[] { 500 });

            RunTest("inc 1", new ushort[] { 2 }, new ushort[] { 3 });
            RunTest("inc 2", new ushort[] { 22 }, new ushort[] { 23 });
            RunTest("inc 3", new ushort[] { 500 }, new ushort[] { 501 });
            RunTest("inc 4", new ushort[] { 2 }, new ushort[] { 55555 });

            RunTest("basic 1 b", new ushort[] { 0, 0 }, new ushort[] { 2, 2 });
            RunTest("basic 2 b", new ushort[] { 0, 0 }, new ushort[] { 22, 22 });
            RunTest("basic 3 b", new ushort[] { 0, 0 }, new ushort[] { 500, 500 });

            RunTest("same 1 b", new ushort[] { 2, 0 }, new ushort[] { 2, 0 });
            RunTest("same 2 b", new ushort[] { 22, 0 }, new ushort[] { 22, 0 });
            RunTest("same 3 b", new ushort[] { 500, 0 }, new ushort[] { 500, 0 });

            RunTest("inc 1 b", new ushort[] { 2, 0 }, new ushort[] { 3, 3 });
            RunTest("inc 2 b", new ushort[] { 22, 0 }, new ushort[] { 23, 23 });
            RunTest("inc 3 b", new ushort[] { 500, 0 }, new ushort[] { 501, 501 });
            RunTest("inc 4 b", new ushort[] { 2, 0 }, new ushort[] { 55555, 55555 });

            RunTest("bigval 1", new ushort[] { 40784 }, new ushort[] { 25929 });
            RunTest("bigval 2", new ushort[] { 13314, 40784 }, new ushort[] { 61715, 25929 });
            //return;

            const int len = 10000;
            var zero = new ushort[len];
            var a = new ushort[len];
            var b = new ushort[len];
            int tmp = 333;
            for (int i = 0; i < len; i++)
            {
                zero[i] = 0;
                tmp = tmp * 1103515245 + 12345;
                a[i] = (ushort)tmp;
                tmp = tmp * 1103515245 + 12345;
                b[i] = (ushort)tmp;
            }

            RunTest("zero2a", zero, a);
            RunTest("zero2b", zero, b);
            RunTest("a2b", a, b);
            RunTest("b2a", b, a);
        }
    }

}









/*

INITIAL APPROACH

//#define USE_UNSAFE 1

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace teadrinker
{

    public static class DiffRLEV1
    {
        const uint OP__READ_UINT16 = 0; // it's important that this operand is 0!
        const uint OP__READ_UINT8 = 1;
        const uint OP__READ_UINT4 = 2;
        const uint OP__ADD_AND_WRITE = 3;
        const uint OP__SUB_AND_WRITE = 4;
        const uint OP__PREV_ADD_AND_WRITE = 5;
        const uint OP__PREV_SUB_AND_WRITE = 6;
        const uint OP__ADVANCE_POS = 7;

        const int opBits = 3;
        const uint opMask = (1 << opBits) - 1;

        static string debugOp(uint i)
        {
            if (i == OP__READ_UINT4) return "OP__READ_UINT4";
            else if (i == OP__READ_UINT8) return "OP__READ_UINT8";
            else if (i == OP__READ_UINT16) return "OP__READ_UINT16";
            else if (i == OP__ADD_AND_WRITE) return "OP__ADD_AND_WRITE";
            else if (i == OP__SUB_AND_WRITE) return "OP__SUB_AND_WRITE";
            else if (i == OP__PREV_ADD_AND_WRITE) return "OP__PREV_ADD_AND_WRITE";
            else if (i == OP__PREV_SUB_AND_WRITE) return "OP__PREV_SUB_AND_WRITE";
            else if (i == OP__ADVANCE_POS) return "OP__ADVANCE_POS";
            return "errooooorrrr";
        }
        class EncoderStream
        {
            uint cur = 0;
            int curShift = 0;
            List<uint> outEncoded;
            public void Flush()
            {
                if (cur != 0)
                {
                    ForceFlush();
                }
            }
            private void ForceFlush()
            {
                outEncoded.Add(cur);
                curShift = 0;
                cur = 0;
            }

            private void AllocBits(uint bits)
            {
                if (curShift + bits > 32)
                {
                    ForceFlush();
                }
            }
            private bool HasBits(uint bits)
            {
                return curShift + bits <= 32;
            }

            private void Write4(uint value)
            {
                const uint bits = opBits + 4;
                AllocBits(bits);
                cur |= OP__READ_UINT4 << curShift;
                cur |= ((uint)value) << (curShift + opBits);
                curShift += (int)bits;
            }

            private void Write8(uint value)
            {
                const uint bits = opBits + 8;
                if (!HasBits(bits))
                {

                    if (HasBits(opBits + 4))
                    {
                        // split data across uint boundry

                        Write4(value & 15);
                        Write4(value >> 4);

                        return;
                    }

                    ForceFlush();
                }
                cur |= OP__READ_UINT8 << curShift;
                cur |= ((uint)value) << (curShift + opBits);
                curShift += (int)bits;
            }

            private void Write16(uint value)
            {
                const uint bits = opBits + 16;
                if (!HasBits(bits))
                {
                    if (HasBits(opBits + 4))
                    {
                        // split data across uint boundry

                        Write8(value & 255); // this might split data too!
                        Write8(value >> 8);

                        return;
                    }

                    ForceFlush();
                }
                cur |= OP__READ_UINT16 << curShift;
                cur |= ((uint)value) << (curShift + opBits);
                curShift += (int)bits;
            }

            public void WriteOP(uint op)
            {
                AllocBits(opBits);
                cur |= op << curShift;
                curShift += opBits;
                //Debug.Log("WriteOP " + debugOp(op) +" "+ + cur);
            }

            public void Write(int value)
            {
                if (value < 0)
                    throw new System.Exception("be more positive!");
                Write((uint)value);
            }

            public void Write(uint value)
            {
                if (value == 0)
                    return;

                if ((value >> 4) == 0)
                    Write4(value);
                else if ((value >> 8) == 0)
                    Write8(value);
                else if ((value >> 12) == 0) // worth it supporting 12 bit? (only saves 1 bit compared to 16bit chunk)
                {
                    if (HasBits(opBits + 8)) // avoid stupid boundry split
                    {
                        Write8(value & 255);
                        Write4(value >> 8);
                    }
                    else
                    {
                        Write4(value & 15);
                        Write8(value >> 4);
                    }
                }
                else if ((value >> 16) == 0)
                {
                    Write16(value);
                }
                else if ((value >> 24) == 0)
                {
                    Write16(value & 65535);
                    Write8(value >> 16);
                }
                else
                {
                    throw new System.Exception("DiffRLE.EncoderStream.Write: over 16bit not supported " + value);
                }
                //Debug.Log("Write " +value+ "  cur:" + cur);
            }
            public EncoderStream(List<uint> _outEncoded)
            {
                outEncoded = _outEncoded;
            }
        }
        public static int abs(int x) { return x < 0 ? -x : x; }
        public static void Encode(ushort[] prevBuf, ushort[] buf, List<uint> outEncoded)
        {
            var o = new EncoderStream(outEncoded);
            int N = buf.Length;
            int skipped = 0;
            ushort prevWrite = 0;
            for (int i = 0; i < N; i++)
            {
                var targetVal = buf[i];
                int diff = ((int)targetVal) - ((int)prevBuf[i]);
                if (diff == 0)
                    skipped++;
                else
                {
                    if (skipped > 0)
                    {
                        if (skipped > 1)
                            o.Write(skipped - 1);
                        o.WriteOP(OP__ADVANCE_POS);
                        skipped = 0;
                    }
                    int diff2 = ((int)targetVal) - ((int)prevWrite);
                    var abs_diff = abs(diff);
                    var abs_diff2 = abs(diff2);
                    if (abs_diff <= abs_diff2)
                    {
                        o.Write(abs_diff);
                        if (diff >= 0)
                            o.WriteOP(OP__ADD_AND_WRITE);
                        else
                            o.WriteOP(OP__SUB_AND_WRITE);
                    }
                    else
                    {
                        o.Write(abs_diff2);
                        if (diff2 >= 0)
                            o.WriteOP(OP__PREV_ADD_AND_WRITE);
                        else
                            o.WriteOP(OP__PREV_SUB_AND_WRITE);
                    }
                    prevWrite = targetVal;
                }
            }
            o.Flush();
        }

        public static void Decode(ushort[] buf, List<uint> encoded)
        {
            Decode(buf, encoded.ToArray(), 0, encoded.Count);
        }
        public static void Decode(ushort[] _buf, uint[] _encodedData, int startOffset, int endOffset)
        {
            ushort prevWrite = 0;
            uint regPos = 0;
            uint regData = 0;
            int regDataShift = 0;

#if USE_UNSAFE
            unsafe
            {
                fixed (ushort* buf = _buf) { 
                fixed (uint* encodedData = _encodedData)
                {
#else
            {
            ushort[] buf = _buf;
            uint[] encodedData = _encodedData;
            {{
#endif
                    for (int i = startOffset; i < endOffset; i++)
                    {
                        uint data = encodedData[i];
                        while (data != 0)
                        {
                            uint op = data & opMask;
                            data >>= opBits;
                            switch (op)
                            {
                                case OP__ADD_AND_WRITE:
                                    prevWrite = (ushort)(buf[regPos] + regData);
                                    buf[regPos] = prevWrite;
                                    regPos++;
                                    regData = 0;
                                    regDataShift = 0;
                                    break;

                                case OP__SUB_AND_WRITE:
                                    prevWrite = (ushort)(buf[regPos] - regData);
                                    buf[regPos] = prevWrite;
                                    regPos++;
                                    regData = 0;
                                    regDataShift = 0;
                                    break;

                                case OP__PREV_ADD_AND_WRITE:
                                    prevWrite = (ushort)(prevWrite + regData);
                                    buf[regPos] = prevWrite;
                                    regPos++;
                                    regData = 0;
                                    regDataShift = 0;
                                    break;

                                case OP__PREV_SUB_AND_WRITE:
                                    prevWrite = (ushort)(prevWrite - regData);
                                    buf[regPos] = prevWrite;
                                    regPos++;
                                    regData = 0;
                                    regDataShift = 0;
                                    break;

                                case OP__ADVANCE_POS:
                                    regPos += regData + 1;
                                    regData = 0;
                                    regDataShift = 0;
                                    break;

                                case OP__READ_UINT4:
                                    var uint4Data = data & 15;
                                    data >>= 4;
                                    regData |= uint4Data << regDataShift;
                                    regDataShift += 4;
                                    break;

                                case OP__READ_UINT8:
                                    var uint8Data = data & 255;
                                    data >>= 8;
                                    regData |= uint8Data << regDataShift;
                                    regDataShift += 8;
                                    break;

                                case OP__READ_UINT16:
                                    var uint16Data = data & 65535;
                                    data >>= 16;
                                    regData |= uint16Data << regDataShift;
                                    regDataShift += 16;
                                    break;
                            }

                            //if(op >= 3)  // is using a conditional here better than duplicating the code inside the switch?
                            //{
                            //    regPos++;
                            //    regData = 0;
                            //    regDataShift = 0;
                            //}

                        }
                    }
                }}
            }
        }


        static void Check(ushort[] a, ushort[] b, ushort[] org_a, string test, List<uint> encoded)
        {
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    Debug.LogError("DiffRLE. test failed at " + i + " : " + test + "       encode from (" + org_a[i] + ") to " + a[i] + ", should be " + b[i] + "      :" + string.Join(", ", encoded.ToArray())
                        //+ "\n\n" + string.Join(", ",a) + "    " + string.Join(", ", b)
                        );
            }
        }
        static void RunTest(string name, ushort[] a, ushort[] b)
        {
            var in_b = b;
            b = new ushort[b.Length];
            System.Array.Copy(in_b, b, b.Length);

            var in_a = a;
            a = new ushort[a.Length];
            System.Array.Copy(in_a, a, a.Length);

            List<uint> encoded = new List<uint>();
            Debug.Log("test " + name + " : " + string.Join(", ", a) + "    " + string.Join(", ", b));
            Encode(a, b, encoded);
            Decode(a, encoded);
            Check(a, b, in_a, name, encoded);
        }
        public static void RunTests()
        {
            RunTest("basic 1", new ushort[] { 0 }, new ushort[] { 2 });
            RunTest("basic 2", new ushort[] { 0 }, new ushort[] { 22 });
            RunTest("basic 3", new ushort[] { 0 }, new ushort[] { 500 });

            RunTest("same 1", new ushort[] { 2 }, new ushort[] { 2 });
            RunTest("same 2", new ushort[] { 22 }, new ushort[] { 22 });
            RunTest("same 3", new ushort[] { 500 }, new ushort[] { 500 });

            RunTest("inc 1", new ushort[] { 2 }, new ushort[] { 3 });
            RunTest("inc 2", new ushort[] { 22 }, new ushort[] { 23 });
            RunTest("inc 3", new ushort[] { 500 }, new ushort[] { 501 });
            RunTest("inc 4", new ushort[] { 2 }, new ushort[] { 55555 });

            RunTest("basic 1 b", new ushort[] { 0, 0 }, new ushort[] { 2, 2 });
            RunTest("basic 2 b", new ushort[] { 0, 0 }, new ushort[] { 22, 22 });
            RunTest("basic 3 b", new ushort[] { 0, 0 }, new ushort[] { 500, 500 });

            RunTest("same 1 b", new ushort[] { 2, 0 }, new ushort[] { 2, 0 });
            RunTest("same 2 b", new ushort[] { 22, 0 }, new ushort[] { 22, 0 });
            RunTest("same 3 b", new ushort[] { 500, 0 }, new ushort[] { 500, 0 });

            RunTest("inc 1 b", new ushort[] { 2, 0 }, new ushort[] { 3, 3 });
            RunTest("inc 2 b", new ushort[] { 22, 0 }, new ushort[] { 23, 23 });
            RunTest("inc 3 b", new ushort[] { 500, 0 }, new ushort[] { 501, 501 });
            RunTest("inc 4 b", new ushort[] { 2, 0 }, new ushort[] { 55555, 55555 });

            RunTest("bigval 1", new ushort[] { 40784 }, new ushort[] { 25929 });
            RunTest("bigval 2", new ushort[] { 13314, 40784 }, new ushort[] { 61715, 25929 });

            const int len = 10000;
            var zero = new ushort[len];
            var a = new ushort[len];
            var b = new ushort[len];
            int tmp = 333;
            for (int i = 0; i < len; i++)
            {
                zero[i] = 0;
                tmp = tmp * 1103515245 + 12345;
                a[i] = (ushort)tmp;
                tmp = tmp * 1103515245 + 12345;
                b[i] = (ushort)tmp;
            }

            RunTest("zero2a", zero, a);
            RunTest("zero2b", zero, b);
            RunTest("a2b", a, b);
            RunTest("b2a", b, a);
        }
    }

}

*/