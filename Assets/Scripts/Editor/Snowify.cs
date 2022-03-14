using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

public class Snowify : EditorWindow
{
    [MenuItem("Tools/Snowify")]
    static void Init()
    {
        // Get existing open window or if none, make a new one:
        Snowify window = EditorWindow.GetWindow<Snowify>("Snowify");
        //window.title = "Snowify"; //to avoid unity warning
        window.Show();
    }

    Material snowmaterial;
    float uvScale = 1;
    public enum SnowDirBy { Transform, Vector3};
    SnowDirBy snowDirBy = SnowDirBy.Vector3;
    Transform snowDir;
    Vector3 snowDirection = -Vector3.up;
    Quaternion snowDirRotation;
    float extrudeAmount = 0.2f;
    int heightSegments = 4;
    float bulgeFactor = 0.55f;
    float maxAngle = 60;
    float minZ;
    int subdivisions = 0;
    int smoothIterations = 0;
    bool alongNormal = false;
    bool closeBottom = false;
    bool cutBelowHeight = false;
    float minHeight = 0;
    SnowCreator.RemoveCoveredMode removeCoveredMode = SnowCreator.RemoveCoveredMode.WhenIslandCovered;
    string prefix = "";
    string suffix = "_snow";
    bool deleteOld = true;
    StaticEditorFlags staticflags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic;
    enum TweakMode { Basic, Advanced }
    TweakMode tweakMode = TweakMode.Basic;

    void OnGUI()
    {
        if (!snowmaterial)
        {
            snowmaterial = AssetDatabase.LoadAssetAtPath("Assets/Snowify/Materials/Snow.mat", typeof(Material)) as Material;
        }
        tweakMode = (TweakMode)EditorGUILayout.EnumPopup("Mode", tweakMode);
        GUILayout.Box("", new GUILayoutOption[]{GUILayout.ExpandWidth(true), GUILayout.Height(1)});
        EditorGUILayout.Separator();
        snowDirBy = (SnowDirBy)EditorGUILayout.EnumPopup("Snow direction by", snowDirBy);
        {
            if (snowDirBy == SnowDirBy.Transform)
                snowDir = EditorGUILayout.ObjectField("Snow direction (by transform.forward)", snowDir, typeof(Transform), true) as Transform;
            else
                snowDirection = EditorGUILayout.Vector3Field("Snow direction (by Vector3)", snowDirection);
        }
        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
        EditorGUILayout.Separator();
        extrudeAmount = Mathf.Max(0, EditorGUILayout.FloatField("Snow Thickness", extrudeAmount));
        if (tweakMode == TweakMode.Advanced)
            alongNormal = EditorGUILayout.Toggle("Extrude along normal", alongNormal);
        heightSegments = Mathf.Max(1, EditorGUILayout.IntField("Height Segments", heightSegments));
        if (tweakMode == TweakMode.Advanced)
        {
            bulgeFactor = Mathf.Max(0, EditorGUILayout.FloatField("Bulge Factor", bulgeFactor));
            maxAngle = Mathf.Max(0.1f, EditorGUILayout.FloatField("Max Angle (degrees)", maxAngle));
            maxAngle = Mathf.Min(maxAngle, 180);
            subdivisions = Mathf.Clamp(EditorGUILayout.IntField("Subdivisions", subdivisions), 0, 5);
            smoothIterations = Mathf.Clamp(EditorGUILayout.IntField("Smooth Iterations", smoothIterations),0,10);
        }
        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
        EditorGUILayout.Separator();
        closeBottom = EditorGUILayout.Toggle("Close Bottom", closeBottom); 
        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
        EditorGUILayout.Separator();
        cutBelowHeight = EditorGUILayout.Toggle("Only High Snow", cutBelowHeight);
        if (cutBelowHeight)
            minHeight = EditorGUILayout.FloatField("Minimum Height", minHeight);
        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
        EditorGUILayout.Separator();
        removeCoveredMode = (SnowCreator.RemoveCoveredMode)EditorGUILayout.EnumPopup("Remove covered snow", removeCoveredMode);
        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
        EditorGUILayout.Separator();
        snowmaterial = EditorGUILayout.ObjectField("Snow material", snowmaterial, typeof(Material), true) as Material;
        if (tweakMode == TweakMode.Advanced)
        {
            uvScale = EditorGUILayout.FloatField("UV Scale (texture size)", uvScale);
            GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
            EditorGUILayout.Separator();
            prefix = EditorGUILayout.TextField("Prefix", prefix);
            suffix = EditorGUILayout.TextField("Suffix", suffix);
            GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
            EditorGUILayout.Separator();
            staticflags = (StaticEditorFlags)EditorGUILayout.EnumMaskField("Static Flags", staticflags);
            GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
            EditorGUILayout.Separator();
            deleteOld = EditorGUILayout.Toggle("Delete old snow object", deleteOld);
            GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
            EditorGUILayout.Separator();
        }
        else
        {
            GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
            EditorGUILayout.Separator();
        }
        GUILayout.Box("Select the GameObjects you wich to Snowify", GUILayout.ExpandWidth(true));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("\nSNOWIFY\n"))
            CreateSnow();
        GUILayout.FlexibleSpace();
        EditorGUILayout.Separator();
        EditorGUILayout.LabelField("(temporarily) backup selected snowmeshes:");
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("\nSave\n"))
            Save();
        if (GUILayout.Button("\nLoad\n"))
            Load();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Separator();
    }

    List<MeshFilter> originalObjects = new List<MeshFilter>();
    List<GameObject> snowObjects = new List<GameObject>();
    List<Mesh> snowMeshesSaved = new List<Mesh>();
    List<Mesh> snowMeshesTemp = new List<Mesh>();

    void Save()
    {
        GameObject[] sel = Selection.gameObjects;
        List<MeshFilter> meshfilters = new List<MeshFilter>();
        foreach (GameObject go in sel)
        {
            meshfilters.AddRange(go.GetComponentsInChildren<MeshFilter>());
        }

        while (snowMeshesSaved.Count < snowObjects.Count)
            snowMeshesSaved.Add(null);
        while (snowMeshesTemp.Count < snowObjects.Count)
            snowMeshesTemp.Add(null);

        int count = 0;// for Debug.Log() only
        foreach (MeshFilter mf in meshfilters)
        {
            int index = originalObjects.IndexOf(mf);
            if (index == -1)
                index = snowObjects.IndexOf(mf.gameObject);
            if (index != -1 && snowObjects[index])
            {
                Mesh snowmesh = snowObjects[index].GetComponent<MeshFilter>().sharedMesh;
                if (snowMeshesSaved[index] != snowmesh)
                {
                    snowMeshesTemp[index] = snowMeshesSaved[index];
                    snowMeshesSaved[index] = snowmesh;
                }
                count++;
            }
        }
        Debug.Log("" + count + " snowmeshes (temporarily) saved\nSnowify");
        //Debug.Log("snowMeshesSaved.Count : " + snowMeshesSaved.Count);
        //Debug.Log("snowMeshesTemp.Count : " + snowMeshesTemp.Count);
    }
    void Load()
    {
        GameObject[] sel = Selection.gameObjects;
        List<MeshFilter> meshfilters = new List<MeshFilter>();
        foreach (GameObject go in sel)
        {
            meshfilters.AddRange(go.GetComponentsInChildren<MeshFilter>());
        }

        while (snowMeshesSaved.Count < snowObjects.Count)
            snowMeshesSaved.Add(null);
        while (snowMeshesTemp.Count < snowObjects.Count)
            snowMeshesTemp.Add(null);

        int count = 0;// for debug.log only
        foreach (MeshFilter mf in meshfilters)
        {
            int index = originalObjects.IndexOf(mf);
            if (index == -1)
                index = snowObjects.IndexOf(mf.gameObject);
            if (index != -1 && snowMeshesSaved[index])
            {
                if (snowObjects[index])
                {
                    if (snowMeshesSaved[index] == snowObjects[index].GetComponent<MeshFilter>().sharedMesh) //the current mesh is already the saved mesh, so load temp mesh instead
                    {
                        if (snowMeshesTemp[index])
                            snowObjects[index].GetComponent<MeshFilter>().mesh = snowMeshesTemp[index];
                    }
                    else //load saved mesh
                    {
                        snowMeshesTemp[index] = snowObjects[index].GetComponent<MeshFilter>().sharedMesh;
                        snowObjects[index].GetComponent<MeshFilter>().mesh = snowMeshesSaved[index];
                    }
                }
                else //snow object has been deleted, so we create a new one
                {
                    GameObject snow = new GameObject(prefix + originalObjects[index].gameObject.name + suffix);
                    snow.transform.position = originalObjects[index].transform.position;
                    snow.AddComponent<MeshFilter>().mesh = snowMeshesSaved[index]; //load saved mesh
                    snow.AddComponent<MeshRenderer>().sharedMaterial = snowmaterial;
                    snowObjects[index] = snow;
                }
                count++;
            }
        }
        Debug.Log("" + count + " snowmeshes loaded\nSnowify");
        //Debug.Log("snowMeshesSaved.Count : " + snowMeshesSaved.Count);
        //Debug.Log("snowMeshesTemp.Count : " + snowMeshesTemp.Count);
    }

    void CreateSnow()
    {
        if (tweakMode == TweakMode.Basic)
        {
            bulgeFactor = 0.5f;
            maxAngle = 60;
            subdivisions = 0;
            smoothIterations = 0;
            alongNormal = false;
            prefix = "";
            suffix = "_snow";
            staticflags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic;
            deleteOld = true;
        }
        if (snowDirBy == SnowDirBy.Transform)
        {
            if (snowDir)
                snowDirection = snowDir.forward;
            else
                snowDirBy = SnowDirBy.Vector3;
        }
        if (Mathf.Approximately(snowDirection.magnitude,0))
            snowDirection = -Vector3.up;
        snowDirRotation = Quaternion.FromToRotation(snowDirection, -Vector3.up);
        if (!alongNormal)
        {
            maxAngle = Mathf.Min(maxAngle, 89.99f);
        }
        minZ = Mathf.Cos(maxAngle * Mathf.Deg2Rad);


        if (originalObjects.Count < snowObjects.Count)
        {
            for (int i = originalObjects.Count; i < snowObjects.Count; i++)
                Debug.LogWarning("Source object not found for snow object " + snowObjects[i] + "\nSnowify Warning");
            snowObjects = snowObjects.GetRange(0, originalObjects.Count);
        }
        else if (originalObjects.Count > snowObjects.Count)
        {
            for (int i = snowObjects.Count; i < originalObjects.Count; i++)
                Debug.LogWarning("Snow object not found for source object " + originalObjects[i] + ". If you wish to recreate a snowobject for this object, and the previous snowobject still exists, you will have to delete it manually.\nSnowify Warning");
            originalObjects = originalObjects.GetRange(0, snowObjects.Count);
        }
        for (int i = 0; i < originalObjects.Count; i++)
        {
            if (!originalObjects[i] || !snowObjects[i])
            {
                if (!originalObjects[i] && snowObjects[i])
                    Debug.LogWarning("Source object not found for snow object " + snowObjects[i] + "\nSnowify Warning");
                else if (originalObjects[i] && !snowObjects[i])
                    Debug.LogWarning("Snow object not found for source object " + originalObjects[i] + ". If you wish to recreate a snowobject for this object, and the previous snowobject still exists, you will have to delete it manually.\nSnowify Warning");
                originalObjects.RemoveAt(i);
                snowObjects.RemoveAt(i);
            }
        }

        GameObject[] sel = Selection.gameObjects;
        List<MeshFilter> meshfilters = new List<MeshFilter>();
        foreach (GameObject go in sel)
        {
            meshfilters.AddRange(go.GetComponentsInChildren<MeshFilter>());
        }
        for (int i=0; i<meshfilters.Count; )
        {
            if (snowObjects.Contains(meshfilters[i].gameObject))
            {
                int index = snowObjects.IndexOf(meshfilters[i].gameObject);
                if (!meshfilters.Contains(originalObjects[index]))
                {
                    meshfilters.Add(originalObjects[index]);
                }
                meshfilters.RemoveAt(i);
            }
            else i++;

        }
        if (meshfilters == null || meshfilters.Count == 0)
        {
            Debug.LogWarning("Can not create snow; no meshes selected.\nSnowify Warning");
            return;
        }
        foreach(MeshFilter meshfilter in meshfilters)
        {
            for (int i=0; i<originalObjects.Count; i++)
            {
                if (originalObjects[i] == meshfilter)
                {
                    if (deleteOld)
                        DestroyImmediate(snowObjects[i].gameObject, false);
                    snowObjects.RemoveAt(i);
                    originalObjects.RemoveAt(i);
                    break;
                }
            }

            GameObject snow = SnowCreator.CreateSnowObject(meshfilter, snowmaterial, snowDirRotation, cutBelowHeight, minHeight, maxAngle, minZ, smoothIterations, removeCoveredMode, alongNormal, extrudeAmount, heightSegments, bulgeFactor, subdivisions, closeBottom, uvScale, prefix, suffix);
            if (snow)
            {
                GameObjectUtility.SetStaticEditorFlags(snow, staticflags);
                originalObjects.Add(meshfilter);
                snowObjects.Add(snow);
            }
        }
    }
}
