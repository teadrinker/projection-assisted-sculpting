using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class FileUtil
{
    static public void LoadBinaryFileIfExist(string fullPath, System.Action<System.IO.BinaryReader> callback)
    {
        if (System.IO.File.Exists(fullPath))
            LoadBinaryFile(fullPath, callback);
    }
    static public void LoadBinaryFile(string fullPath, System.Action<System.IO.BinaryReader> callback)
    {
        try
        {
            byte[] dbytes = System.IO.File.ReadAllBytes(fullPath);
            using (System.IO.MemoryStream stream = new System.IO.MemoryStream(dbytes))
            {
                var r = new System.IO.BinaryReader(stream);
                callback(r);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("LoadBinaryFile exception " + fullPath + "\n" + e.Message + e.StackTrace);
        }
    }
    static public void SaveBinaryFile(string fullPath, System.Action<System.IO.BinaryWriter> callback)
    {
        try
        {
            using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
            {
                var w = new System.IO.BinaryWriter(stream);
                callback(w);
                w.Flush();
                var ar = stream.ToArray();
                Debug.Log("Saved " + fullPath + " " + (ar.Length / (1024)) + " kb");
                System.IO.File.WriteAllBytes(fullPath, ar);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("SaveBinaryFile exception " + fullPath + "\n" + e.Message);
        }
    }
    static public void LoadBinary(Transform trans, System.IO.BinaryReader reader, bool scale = true, bool rotation = true, bool position = true)
    {
        if (position)
        {
            var pos = new Vector3();
            pos.x = reader.ReadSingle();
            pos.y = reader.ReadSingle();
            pos.z = reader.ReadSingle();
            trans.position = pos;
        }
        if (rotation)
        {
            var rot = new Quaternion();
            rot.x = reader.ReadSingle();
            rot.y = reader.ReadSingle();
            rot.z = reader.ReadSingle();
            rot.w = reader.ReadSingle();
            trans.rotation = rot;
        }
        if (scale)
        {
            var sca = new Vector3();
            sca.x = reader.ReadSingle();
            sca.y = reader.ReadSingle();
            sca.z = reader.ReadSingle();
            trans.localScale = sca;
        }
    }
    static public void SaveBinary(Transform trans, System.IO.BinaryWriter writer, bool scale = true, bool rotation = true, bool position = true)
    {
        if (position)
        {
            var pos = trans.position;
            writer.Write(pos.x);
            writer.Write(pos.y);
            writer.Write(pos.z);
        }
        if (rotation)
        {
            var rot = trans.rotation;
            writer.Write(rot.x);
            writer.Write(rot.y);
            writer.Write(rot.z);
            writer.Write(rot.w);
        }
        if (scale)
        {
            var sca = trans.localScale;
            writer.Write(sca.x);
            writer.Write(sca.y);
            writer.Write(sca.z);
        }
    }
}

[ExecuteInEditMode]
public class SaveTransform : MonoBehaviour
{
    public string filename;
    public bool loadNow = false;
    public bool saveNow = false;
    public bool loadOnEnable = true;

    void OnEnable()
    {
        if (loadOnEnable)
        {
            LoadNow();
        }
    }



    public void LoadNow()
    {
        if(enabled && System.IO.File.Exists(Application.dataPath + "/../" + filename))
        {
            FileUtil.LoadBinaryFile(Application.dataPath + "/../" + filename, writer =>
            {
                FileUtil.LoadBinary(transform, writer);
            });
        }
    }
    public void SaveNow()
    {
        if (!enabled)
        {
            Debug.LogWarning("TRANSFORM NOT SAVED, Component disabled!");
            return;
        }
        FileUtil.SaveBinaryFile(Application.dataPath + "/../" + filename, writer =>
        {
            FileUtil.SaveBinary(transform, writer);
        });
    }
    void Update()
    {
        if(saveNow)
		{
            saveNow = false;
            SaveNow();
        }
		if (loadNow)
		{
            loadNow = false;
            LoadNow();
        }
    }
}
