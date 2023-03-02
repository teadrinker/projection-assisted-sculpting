
// by: Martin Eklund, music@teadrinker.net
// Licence: GNU GPL v3.0 (https://www.gnu.org/licenses/gpl-3.0.en.html)

using System.Collections.Generic;
using UnityEngine;


namespace teadrinker
{

    interface CustomReflectionSupport {
        CustomReflection GetCustomReflection();
	}

    public class CRData
    {
        public System.Type Type;
        public string Name;
        public object Default; 
        public object SetFunc; // System.Action<T>
        public object GetFunc; // System.Func  <T>
    }

    public class CustomReflection
    {
        private Dictionary<string, CRData> _members = new Dictionary<string, CRData>();
        public void Add<T>(string name, T _default, System.Action<T> set, System.Func<T> get)
        {
            _members.Add(name, new CRData() { Type = typeof(T), Name = name, Default = _default, SetFunc = set, GetFunc = get } );
        }
        public virtual void ForEachMember(System.Action<string, CRData> callback)
		{
            foreach (var kv in _members)
            {
                callback(kv.Key, kv.Value);
            }
		}
        public virtual void LoadData(System.IO.BinaryReader r)
		{
            var kvin = new KeyValueStore();
            kvin.LoadData(r);
            RecallFromKeyValueStore(kvin);
		}
        public virtual void RecallFromKeyValueStore(KeyValueStore kvin)
		{
            foreach(var kv in _members)
			{
                var m = kv.Value;
                var t = m.Type;
                if      (t == typeof(float))       { ((System.Action<float>  )m.SetFunc)(kvin.Get(m.Name, (float)  m.Default)); }
                else if (t == typeof(string))      { ((System.Action<string> )m.SetFunc)(kvin.Get(m.Name, (string) m.Default)); }
                else if (t == typeof(bool))        { ((System.Action<bool> )  m.SetFunc)(kvin.Get(m.Name, (bool)   m.Default)); }
                else if (t == typeof(int))         { ((System.Action<int> )   m.SetFunc)(kvin.Get(m.Name, (int)    m.Default)); }
                else if (t == typeof(Vector2))     { ((System.Action<Vector2>)m.SetFunc)(kvin.Get(m.Name, (Vector2)m.Default)); }
                else if (t == typeof(Vector3))     { ((System.Action<Vector3>)m.SetFunc)(kvin.Get(m.Name, (Vector3)m.Default)); }
                else if (t == typeof(Vector4))     { ((System.Action<Vector4>)m.SetFunc)(kvin.Get(m.Name, (Vector4)m.Default)); }
                else if (t == typeof(Color))       { ((System.Action<Color>  )m.SetFunc)(kvin.Get(m.Name, (Color)  m.Default)); }
                else
				{
                    Debug.LogError("Type missing " + t.FullName);
				}
            }
		}

        public virtual KeyValueStore StoreToKeyValueStore()
        {
            var kvout = new KeyValueStore();
            foreach (var kv in _members)
            {
                var m = kv.Value;
                var t = m.Type;
                if      (t == typeof(float  )) { var v = ((System.Func<float>  )m.GetFunc)(); if (!m.Default.Equals(v)) kvout.AddProperty(m.Name, v); }
                else if (t == typeof(string )) { var v = ((System.Func<string> )m.GetFunc)(); if (!m.Default.Equals(v)) kvout.AddProperty(m.Name, v); }
                else if (t == typeof(bool   )) { var v = ((System.Func<bool>   )m.GetFunc)(); if (!m.Default.Equals(v)) kvout.AddProperty(m.Name, v); }
                else if (t == typeof(int    )) { var v = ((System.Func<int>    )m.GetFunc)(); if (!m.Default.Equals(v)) kvout.AddProperty(m.Name, v); }
                else if (t == typeof(Vector2)) { var v = ((System.Func<Vector2>)m.GetFunc)(); if (!m.Default.Equals(v)) kvout.AddProperty(m.Name, v); }
                else if (t == typeof(Vector3)) { var v = ((System.Func<Vector3>)m.GetFunc)(); if (!m.Default.Equals(v)) kvout.AddProperty(m.Name, v); }
                else if (t == typeof(Vector4)) { var v = ((System.Func<Vector4>)m.GetFunc)(); if (!m.Default.Equals(v)) kvout.AddProperty(m.Name, v); }
                else if (t == typeof(Color  )) { var v = ((System.Func<Color>  )m.GetFunc)(); if (!m.Default.Equals(v)) kvout.AddProperty(m.Name, v); }
                else
                {
                    Debug.LogError("Type missing " + t.FullName);
                }
            }
            return kvout;
        }
        public virtual void SaveData(System.IO.BinaryWriter w)
		{
            var kvout = StoreToKeyValueStore();
            kvout.SaveData(w);
        }
    }




    /*
            // reflection per class rather than class instance feels like overengineering...

            public class CRData
            {

            }
            public interface CustomReflection
            {
                System.Type GetReflType();
            }
            public class CustomReflectionTyped<T> : CustomReflection
            {
                public Dictionary<string, CRData> Members = new Dictionary<string, CRData>();
                public void Add(string name, System.Action<T, float> set, System.Func<T, float> get)
                {
                    Members.Add(name, new CRData())
                }
            }
            public static CustomReflection GetCustomReflection()
            {
                var r = new CustomReflectionTyped<KinectRecorder>();
                r.Add("PointSize", (mb, val) => mb.PointSize = val, (mb) => mb.PointSize);
                return r;
            }
    */

}