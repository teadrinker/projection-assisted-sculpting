/*
 *
 *      *** Key / Value Store *** 
 *
 *      Martin Eklund 2020, GNU GPL v3.0
 *   
 *      Flexible key/value store with built-in serialization.
 *      Internally it only uses float and string, other types
 *      will serialize / unserialze to text on Add / Get.
 *      
 *      WARNING: does not check for name collisions between float 
 *               and string storage so a single key can potentially
 *               store both a float and a text value
*/

using System.Collections.Generic;
using UnityEngine;




public class KeyValueStore
{
    private Dictionary<string, float> propertiesF;
    private Dictionary<string, KeyValuePair<string,string> > propertiesS;   // < propertyName , KeyValuePair < metadata , value > >
    public bool ContainsKeyToFloat(string name)
    {
        return propertiesF.ContainsKey(name);
    }
    public bool ContainsKeyToNonFloat(string name)
    {
        return propertiesS.ContainsKey(name);
    }

    public object GetAsType(string name, System.Type t)
    {
        if (t == typeof(float))
        {
            if (propertiesF != null)
            {
                if (propertiesF.TryGetValue(name, out float dictval))
                    return dictval;
            }
        }
        else if(propertiesS != null)
		{
            if (propertiesS.ContainsKey(name))
			{
                if (t == typeof(string))  return Get(name, "");
                if (t == typeof(bool))    return Get(name, false);
                if (t == typeof(int))     return Get(name, 0);
                if (t == typeof(Vector2)) return Get(name, Vector2.zero);
                if (t == typeof(Vector3)) return Get(name, Vector3.zero);
                if (t == typeof(Vector4)) return Get(name, Vector4.zero);
                if (t == typeof(Color))   return Get(name, Color.black);
            }
        }
        return null;
    }

    public float Get(string name, float defaultval)
    {
        var val = defaultval;
        if (propertiesF != null)
        {
            if (propertiesF.TryGetValue(name, out float dictval))
                val = dictval;
        }
        return val;
    }
    public string Get(string name, string defaultval)
    {
        var val = defaultval;
        if (propertiesS != null)
        {
            if (propertiesS.TryGetValue(name, out KeyValuePair<string, string> kv))
                val = kv.Value;
        }
        return val;
    }
    public string Get(string name, out string meta)
    {
        string val = null;
        meta = "";
        if (propertiesS != null)
        {
            if (propertiesS.TryGetValue(name, out KeyValuePair<string, string> kv))
            {
                meta = kv.Key;
                val = kv.Value;
            }
        }
        return val;
    }
    public bool Get(string name, bool defaultval)
    {
        var val = defaultval;
        if (propertiesF != null)
        {
            if (propertiesF.TryGetValue(name, out float dictval))
                val = dictval > 0.5f;
        }
        return val;
    }
    public int Get(string name, int defaultval)
    {
        var val = defaultval;
        if (propertiesS != null)
        {
            if (propertiesS.TryGetValue(name, out KeyValuePair<string, string> dictval))
                val = toInt(dictval.Value);
        }
        return val;
    }
    public void AddProperty(string name, float val)
    {
        if (propertiesF == null)
            propertiesF = new Dictionary<string, float>();
        propertiesF.Add(name, val);
    }
    public void AddProperty(string name, bool val)
    {
        if (propertiesF == null)
            propertiesF = new Dictionary<string, float>();
        propertiesF.Add(name, val ? 1f : 0f);
    }    
   
    public void AddProperty(string name, string value, string meta = "")
    {
        if (propertiesS == null)
            propertiesS = new Dictionary<string, KeyValuePair<string, string>>();
        propertiesS.Add(name, new KeyValuePair<string, string>(meta, value));
    }

    // Foreach
    public void ForEachTextProperty(System.Action<string, string, string> f)
	{
        if (propertiesS != null)
            foreach (var prop in propertiesS)
                f(prop.Key, prop.Value.Value, prop.Value.Key); // propertyName, value, metadata
    }

    public void ForEachFloatProperty(System.Action<string, float> f)
    {
        if (propertiesF != null)
            foreach (var prop in propertiesF)
                f(prop.Key, prop.Value);
    }


    // Serialize
    public virtual void SaveData(System.IO.BinaryWriter w)
    {
        if (propertiesF != null)
		{
            w.Write(propertiesF.Count);
            foreach (var prop in propertiesF)
            {
                w.Write(prop.Key);
                w.Write(prop.Value);
            }
		}
        else
		{
            w.Write(0);
		}

        if (propertiesS != null)
        {
            w.Write(propertiesS.Count);
            foreach (var prop in propertiesS)
            {
                w.Write(prop.Key);
                w.Write(prop.Value.Value);
                w.Write(prop.Value.Key);
            }
        }
        else
		{
            w.Write(0);
		}
    }
    public virtual void LoadData(System.IO.BinaryReader r)
    {
        int N = r.ReadInt32();
        if (N == 0)
		{
            propertiesF = null;
		}
        else
		{
            if (propertiesF == null)
                propertiesF = new Dictionary<string, float>();
            else
                propertiesF.Clear();

            for(int i = 0; i < N; i++)
			{
                string propName = r.ReadString();
                float  val = r.ReadSingle();
                propertiesF.Add(propName, val);
            }
        }

        N = r.ReadInt32();
        if (N == 0)
        {
            propertiesS = null;
        }
        else
        {
            if (propertiesS == null)
                propertiesS = new Dictionary<string, KeyValuePair<string, string>>();
            else
                propertiesS.Clear();

            for (int i = 0; i < N; i++)
            {
                string propName = r.ReadString();
                string val = r.ReadString();
                string meta = r.ReadString();
                propertiesS.Add(propName, new KeyValuePair<string, string>(meta, val));
            }
        }

    }



    private static int toInt(string s) { return System.Convert.ToInt32(s, System.Globalization.CultureInfo.InvariantCulture); }
    private static float toFloat(string s) { return System.Convert.ToSingle(s, System.Globalization.CultureInfo.InvariantCulture); }
    private static string str(int i) { return i.ToString(System.Globalization.CultureInfo.InvariantCulture); }
    private static string str(float f) { return f.ToString(System.Globalization.CultureInfo.InvariantCulture); }
    private static List<float> _tmp = new List<float>(); // avoid allocation, not thread safe?
    public List<float> ParseFloatArray(string s)
    {
        _tmp.Clear();
        var strarray = s.Split(' ');
        for (int i = 0; i < strarray.Length; i++)
            _tmp.Add(toFloat(strarray[i]));
        return _tmp;
    }
    public Vector2 Get(string name, Vector2 def)
    {
        var s = Get(name, (string)null);
        if (s == null)
            return def;
        var floats = ParseFloatArray(s);
        if (floats.Count != 2)
        {
            Debug.LogError("KeyValueStore.Get() Vector2 not 2, key: " + name + ", val:" + s);
            return default;
        }
        return new Vector2(floats[0], floats[1]);
    }
    public Vector3 Get(string name, Vector3 def)
    {
        var s = Get(name, (string)null);
        if (s == null)
            return def;
        var floats = ParseFloatArray(s);
        if (floats.Count != 3)
        {
            Debug.LogError("KeyValueStore.Get() Vector3 not 3, key: " + name + ", val:" + s);
            return default;
        }
        return new Vector3(floats[0], floats[1], floats[2]);
    }
    public Vector4 Get(string name, Vector4 def)
    {
        var s = Get(name, (string)null);
        if (s == null)
            return def;
        var floats = ParseFloatArray(s);
        if (floats.Count != 4)
        {
            Debug.LogError("KeyValueStore.Get() Vector4 not 4, key: " + name + ", val:" + s);
            return default;
        }
        return new Vector4(floats[0], floats[1], floats[2], floats[3]);
    }
    public Color Get(string name, Color def)
    {
        var s = Get(name, (string)null);
        if (s == null)
            return def;
        var floats = ParseFloatArray(s);
        if (floats.Count != 4)
        {
            Debug.LogError("KeyValueStore.Get() Color not 4, key: " + name + ", val:" + s);
            return default;
        }
        return new Color(floats[0], floats[1], floats[2], floats[3]);
    }
    public Quaternion Get(string name, Quaternion def)
    {
        var s = Get(name, (string)null);
        if (s == null)
            return def;
        var floats = ParseFloatArray(s);
        if (floats.Count != 4)
        {
            Debug.LogError("KeyValueStore.Get() Color not 4, key: " + name + ", val:" + s);
            return default;
        }
        return new Quaternion(floats[0], floats[1], floats[2], floats[3]);
    }
    public void AddProperty(string name, int v, string meta = "")
    {
        AddProperty(name, str(v));
    }

    public void AddProperty(string name, Vector2 v, string meta = "")
    {
        AddProperty(name, str(v.x) + " " + str(v.y), meta);
    }
    public void AddProperty(string name, Vector3 v, string meta = "")
    {
        AddProperty(name, str(v.x) + " " + str(v.y) + " " + str(v.z), meta);
    }
    public void AddProperty(string name, Vector4 v, string meta = "")
    {
        AddProperty(name, str(v.x) + " " + str(v.y) + " " + str(v.z) + " " + str(v.w), meta);
    }
    public void AddProperty(string name, Color col, string meta = "")
    {
        AddProperty(name, str(col.r) + " " + str(col.g) + " " + str(col.b) + " " + str(col.a), meta);
    }
    public void AddProperty(string name, Quaternion q, string meta = "")
    {
        AddProperty(name, str(q.x) + " " + str(q.y) + " " + str(q.z) + " " + str(q.w), meta);
    }

    public void AddPropertyAsJson(string name, string json)
    {
        AddProperty(name, json, "json");
    }

}
