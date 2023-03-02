
// by: Martin Eklund, music@teadrinker.net
// Licence: GNU GPL v3.0 (https://www.gnu.org/licenses/gpl-3.0.en.html)

using UnityEngine;

using System;
using System.Collections.Generic;
using System.IO;

public static class PLYReader
{

    public static void SavePLY(string filepath, CloudData data) //, int indexStart = -1, int indexEnd = -1) {\
    {
        if(data.colors.Count > 0 && data.vertices.Count != data.colors.Count)
		{
            throw (new System.Exception("SavePLY mismatching sizes"));
		}
        bool saveColor = data.colors.Count > 0;
        string header =
                "ply\n" +
                "format binary_little_endian 1.0\n" +
                "element vertex " + data.vertices.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n" +
                "property float x\n" +
                "property float y\n" +
                "property float z\n" +
                (saveColor ? (
                "property uchar red\n" +
                "property uchar green\n" +
                "property uchar blue\n" 
                ) : "") + 
                "end_header\n";
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(System.Text.Encoding.ASCII.GetBytes(header));
        for(int i = 0; i < data.vertices.Count; i++)
        {
            bw.Write(data.vertices[i].x);
            bw.Write(data.vertices[i].y);
            bw.Write(data.vertices[i].z);
            if(saveColor)
			{
                bw.Write(data.colors[i].r);
                bw.Write(data.colors[i].g);
                bw.Write(data.colors[i].b);
            }
        }
//        bw.Flush(); not needed it seems?
        FileStream file = new FileStream(filepath, FileMode.OpenOrCreate, FileAccess.Write);
        ms.WriteTo(file);
        file.Close();
        ms.Close();

    }

    public static CloudData LoadPLY(string filepath) { //, int indexStart = -1, int indexEnd = -1) {\

            CloudData data = null;
            var stream = File.Open(filepath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if(stream == null) 
                return null;
            try {
                var header = ReadDataHeader(new StreamReader(stream));
                var reader = new BinaryReader(stream);

                data = new CloudData(header.vertexCount);

                float x = 0, y = 0, z = 0;
                Byte r = 255, g = 255, b = 255, a = 255;

                for (var i = 0; i < header.vertexCount; i++)
                {
                    foreach (var prop in header.properties)
                    {
                        switch (prop)
                        {
                            case DataProperty.R8: r = reader.ReadByte(); break;
                            case DataProperty.G8: g = reader.ReadByte(); break;
                            case DataProperty.B8: b = reader.ReadByte(); break;
                            case DataProperty.A8: a = reader.ReadByte(); break;

                            case DataProperty.R16: r = (byte)(reader.ReadUInt16() >> 8); break;
                            case DataProperty.G16: g = (byte)(reader.ReadUInt16() >> 8); break;
                            case DataProperty.B16: b = (byte)(reader.ReadUInt16() >> 8); break;
                            case DataProperty.A16: a = (byte)(reader.ReadUInt16() >> 8); break;

                            case DataProperty.SingleX: x = reader.ReadSingle(); break;
                            case DataProperty.SingleY: y = reader.ReadSingle(); break;
                            case DataProperty.SingleZ: z = reader.ReadSingle(); break;

                            case DataProperty.DoubleX: x = (float)reader.ReadDouble(); break;
                            case DataProperty.DoubleY: y = (float)reader.ReadDouble(); break;
                            case DataProperty.DoubleZ: z = (float)reader.ReadDouble(); break;

                            case DataProperty.Data8: reader.ReadByte(); break;
                            case DataProperty.Data16: reader.BaseStream.Position += 2; break;
                            case DataProperty.Data32: reader.BaseStream.Position += 4; break;
                            case DataProperty.Data64: reader.BaseStream.Position += 8; break;
                        }
                    }

                    data.AddPoint(x, y, z, r, g, b, a);
                }
            } catch(Exception err) {
                Debug.Log("LoadPLY Err "+err.Message);
            }

            return data;

    }

        enum DataProperty {
            Invalid,
            R8, G8, B8, A8,
            R16, G16, B16, A16,
            SingleX, SingleY, SingleZ,
            DoubleX, DoubleY, DoubleZ,
            Data8, Data16, Data32, Data64
        }

        static int GetPropertySize(DataProperty p)
        {
            switch (p)
            {
                case DataProperty.R8: return 1;
                case DataProperty.G8: return 1;
                case DataProperty.B8: return 1;
                case DataProperty.A8: return 1;
                case DataProperty.R16: return 2;
                case DataProperty.G16: return 2;
                case DataProperty.B16: return 2;
                case DataProperty.A16: return 2;
                case DataProperty.SingleX: return 4;
                case DataProperty.SingleY: return 4;
                case DataProperty.SingleZ: return 4;
                case DataProperty.DoubleX: return 8;
                case DataProperty.DoubleY: return 8;
                case DataProperty.DoubleZ: return 8;
                case DataProperty.Data8: return 1;
                case DataProperty.Data16: return 2;
                case DataProperty.Data32: return 4;
                case DataProperty.Data64: return 8;
            }
            return 0;
        }

        class DataHeader
        {
            public List<DataProperty> properties = new List<DataProperty>();
            public int vertexCount = -1;
        }

        public class CloudData
        {
            public List<Vector3> vertices;
            public List<Color32> colors;

            public CloudData(int vertexCount)
            {
                vertices = new List<Vector3>(vertexCount);
                colors = new List<Color32>(vertexCount);
            }
            public void AddPoint(Vector3 v)

            {
                vertices.Add(v);
            }

            public void AddPoint(Vector3 v, Color32 col)

            {
                vertices.Add(v);
                colors.Add(col);
            }

            public void AddPoint(
                float x, float y, float z,
                byte r, byte g, byte b, byte a
            )
            {
                vertices.Add(new Vector3(x, y, z));
                colors.Add(new Color32(r, g, b, a));
            }
        }


        static DataHeader ReadDataHeader(StreamReader reader)
        {
            var data = new DataHeader();
            var readCount = 0;

            // Magic number line ("ply")
            var line = reader.ReadLine();
            readCount += line.Length + 1;
            if (line != "ply")
                throw new ArgumentException("Magic number ('ply') mismatch.");

            // Data format: check if it's binary/little endian.
            line = reader.ReadLine();
            readCount += line.Length + 1;
            if (line != "format binary_little_endian 1.0")
                throw new ArgumentException(
                    "Invalid data format ('" + line + "'). " +
                    "Should be binary/little endian.");

            // Read header contents.
            for (var skip = false;;)
            {
                // Read a line and split it with white space.
                line = reader.ReadLine();
                readCount += line.Length + 1;
                if (line == "end_header") break;
                var col = line.Split();

                // Element declaration (unskippable)
                if (col[0] == "element")
                {
                    if (col[1] == "vertex")
                    {
                        data.vertexCount = Convert.ToInt32(col[2]);
                        skip = false;
                    }
                    else
                    {
                        // Don't read elements other than vertices.
                        skip = true;
                    }
                }

                if (skip) continue;

                // Property declaration line
                if (col[0] == "property")
                {
                    var prop = DataProperty.Invalid;

                    // Parse the property name entry.
                    switch (col[2])
                    {
                        case "red"  : prop = DataProperty.R8; break;
                        case "green": prop = DataProperty.G8; break;
                        case "blue" : prop = DataProperty.B8; break;
                        case "alpha": prop = DataProperty.A8; break;
                        case "x"    : prop = DataProperty.SingleX; break;
                        case "y"    : prop = DataProperty.SingleY; break;
                        case "z"    : prop = DataProperty.SingleZ; break;
                    }

                    // Check the property type.
                    if (col[1] == "char" || col[1] == "uchar" ||
                        col[1] == "int8" || col[1] == "uint8")
                    {
                        if (prop == DataProperty.Invalid)
                            prop = DataProperty.Data8;
                        else if (GetPropertySize(prop) != 1)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else if (col[1] == "short" || col[1] == "ushort" ||
                             col[1] == "int16" || col[1] == "uint16")
                    {
                        switch (prop)
                        {
                            case DataProperty.Invalid: prop = DataProperty.Data16; break;
                            case DataProperty.R8: prop = DataProperty.R16; break;
                            case DataProperty.G8: prop = DataProperty.G16; break;
                            case DataProperty.B8: prop = DataProperty.B16; break;
                            case DataProperty.A8: prop = DataProperty.A16; break;
                        }
                        if (GetPropertySize(prop) != 2)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else if (col[1] == "int"   || col[1] == "uint"   || col[1] == "float" ||
                             col[1] == "int32" || col[1] == "uint32" || col[1] == "float32")
                    {
                        if (prop == DataProperty.Invalid)
                            prop = DataProperty.Data32;
                        else if (GetPropertySize(prop) != 4)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else if (col[1] == "int64"  || col[1] == "uint64" ||
                             col[1] == "double" || col[1] == "float64")
                    {
                        switch (prop)
                        {
                            case DataProperty.Invalid: prop = DataProperty.Data64; break;
                            case DataProperty.SingleX: prop = DataProperty.DoubleX; break;
                            case DataProperty.SingleY: prop = DataProperty.DoubleY; break;
                            case DataProperty.SingleZ: prop = DataProperty.DoubleZ; break;
                        }
                        if (GetPropertySize(prop) != 8)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else
                    {
                        throw new ArgumentException("Unsupported property type ('" + line + "').");
                    }

                    data.properties.Add(prop);
                }
            }

            // Rewind the stream back to the exact position of the reader.
            reader.BaseStream.Position = readCount;

            return data;
        }

}
