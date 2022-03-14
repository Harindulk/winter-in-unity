using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using System.IO;

public class SnowifyTerrain : EditorWindow
{
    [MenuItem("Tools/SnowifyTerrain")]
    static void Init()
    {
        // Get existing open window or if none, make a new one:
        SnowifyTerrain window = EditorWindow.GetWindow<SnowifyTerrain>("SnowifyTerrain");
        window.Show();
    }

    int snowTextureIndex = 0;
    int fillTextureIndex = 1;
    float StartFadeAngle = 30;
    float EndFadeAngle = 60;
    float StartFadeHeight = 0;
    float EndFadeHeight = -1;

    float snowHeight = 0.2f;
    int blurSnowSamples = 10;
    AnimationCurve snowcurve = AnimationCurve.EaseInOut(0.5f,0,1,1);

    public enum SnowDirBy { Transform, Vector3 };
    SnowDirBy snowDirBy = SnowDirBy.Vector3;
    Transform snowDir;
    Vector3 snowDirection = -Vector3.up;

    bool autoSaveLoad = true;

    static List<Terrain> terrains = new List<Terrain>();
    static List<float[, ,]> splatsBackups = new List<float[, ,]>();
    static List<float[,]> heightsBackups = new List<float[,]>();

    void OnGUI()
    {
        Terrain terrain = null;
        Texture snow = null;
        Texture fill = null;
        Transform selection = Selection.activeTransform;
        if (selection)
            terrain = selection.GetComponent<Terrain>();
        if (terrain)
        {
            if (snowTextureIndex < terrain.terrainData.splatPrototypes.Length)
                snow = terrain.terrainData.splatPrototypes[snowTextureIndex].texture;
            if (fillTextureIndex < terrain.terrainData.splatPrototypes.Length)
                fill = terrain.terrainData.splatPrototypes[fillTextureIndex].texture;
        }

        if (terrain)
            //EditorGUILayout.LabelField("terrain '" + terrain.name + "' selected");
            GUILayout.Box("terrain '" + terrain.name + "' selected", GUILayout.ExpandWidth(true));
        else
            //EditorGUILayout.LabelField("no terrain selected");
            GUILayout.Box("no terrain selected", GUILayout.ExpandWidth(true));
        //GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
        EditorGUILayout.Separator();

        snowTextureIndex = EditorGUILayout.IntField("Snow Texture Index", snowTextureIndex);
        EditorGUILayout.Separator();
        EditorGUILayout.Separator();
        EditorGUILayout.Separator();
        EditorGUILayout.Separator();
        fillTextureIndex = EditorGUILayout.IntField("Default Texture Index", fillTextureIndex);
        EditorGUILayout.Separator();
        EditorGUILayout.Separator();
        EditorGUILayout.Separator();
        EditorGUILayout.Separator();
        if (snow)
            EditorGUI.DrawPreviewTexture(new Rect(6, 51, 24, 24), snow);
        if (fill)
            EditorGUI.DrawPreviewTexture(new Rect(6, 97, 24, 24), fill);
        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
        EditorGUILayout.Separator();

        StartFadeAngle = EditorGUILayout.FloatField("Start Fade Angle", StartFadeAngle);
        EndFadeAngle = EditorGUILayout.FloatField("End Fade Angle", EndFadeAngle);
        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
        EditorGUILayout.Separator();

        StartFadeHeight = EditorGUILayout.FloatField("Start Fade Height", StartFadeHeight);
        EndFadeHeight = EditorGUILayout.FloatField("End Fade Height", EndFadeHeight);
        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
        EditorGUILayout.Separator();

        snowDirBy = (SnowDirBy)EditorGUILayout.EnumPopup("Snow direction by", snowDirBy);
        {
            if (snowDirBy == SnowDirBy.Transform)
                snowDir = EditorGUILayout.ObjectField("Snow direction (by transform.forward)", snowDir, typeof(Transform), true) as Transform;
            else
                snowDirection = EditorGUILayout.Vector3Field("Snow direction (by Vector3)", snowDirection);
        }
        EditorGUILayout.Separator();

        if (GUILayout.Button("\nSNOWIFY (Paint)\n"))
            PaintSnow();
        if (GUILayout.Button("Remove Snow (Paint)"))
            PaintSnow(false);
        EditorGUILayout.Separator();
        EditorGUILayout.Separator();

        snowHeight = EditorGUILayout.FloatField("Snow Height", snowHeight);
        blurSnowSamples = EditorGUILayout.IntField("Extra Snow Samples", blurSnowSamples);
        snowcurve = EditorGUILayout.CurveField("snowcurve", snowcurve);
        EditorGUILayout.Separator();

        if (GUILayout.Button("\nOffset Snow\n"))
            OffsetSnow();
        EditorGUILayout.Separator();

        autoSaveLoad = EditorGUILayout.Toggle("Auto-Save/Load", autoSaveLoad);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("\nSave\n"))
            Save();
        if (GUILayout.Button("\nLoad\n"))
            Load();
        GUILayout.EndHorizontal();
    }

    void OffsetSnow()
    {
        int terrainIndex = GetTerrain();
        if (terrainIndex == -1 ||
            snowTextureIndex >= terrains[terrainIndex].terrainData.splatPrototypes.Length ||
            fillTextureIndex >= terrains[terrainIndex].terrainData.splatPrototypes.Length)
            return;

        Terrain terrain = terrains[terrainIndex];

        float[, ,] splats = terrain.terrainData.GetAlphamaps(0, 0, terrain.terrainData.alphamapWidth, terrain.terrainData.alphamapHeight);
        float[,] heights = terrain.terrainData.GetHeights(0,0,terrain.terrainData.heightmapWidth, terrain.terrainData.heightmapHeight);

        for (int i = 0; i < blurSnowSamples; i++)
        {
            float[, ,] newsplats = terrain.terrainData.GetAlphamaps(0, 0, terrain.terrainData.alphamapWidth, terrain.terrainData.alphamapHeight);
            int maxX = terrain.terrainData.alphamapWidth - 1;
            int maxY = terrain.terrainData.alphamapHeight - 1;
            for (int x = 0; x < terrain.terrainData.alphamapWidth; x++)
            {
                for (int y = 0; y < terrain.terrainData.alphamapHeight; y++)
                {
                    int amount = 1;
                    float total = splats[x, y, snowTextureIndex];
                    if (x>0)  
                    {
                        amount++;
                        total += splats[x-1, y, snowTextureIndex];
                        if (y>0)  
                        {
                            amount++;
                            total += splats[x-1, y-1, snowTextureIndex];
                        }
                        if (y<maxY)  
                        {
                            amount++;
                            total += splats[x-1, y+1, snowTextureIndex];
                        }
                    }
                    if (y>0)  
                    {
                        amount++;
                        total += splats[x, y-1, snowTextureIndex];
                        if (x<maxX)  
                        {
                            amount++;
                            total += splats[x+1, y-1, snowTextureIndex];
                        }
                    }
                    if (x<maxX)  
                    {
                        amount++;
                        total += splats[x+1, y, snowTextureIndex];
                        if (y<maxY)   
                        {
                            amount++;
                            total += splats[x+1, y+1, snowTextureIndex];
                        }
                    }
                    if (y<maxY)  
                    {
                        amount++;
                        total += splats[x, y+1, snowTextureIndex];
                    }

                    newsplats[x, y, snowTextureIndex] = Mathf.Min(splats[x, y, snowTextureIndex], total / (float)amount);
                }
            }
            splats = newsplats;
        }

        Debug.Log("terrain.terrainData.alphamapWidth : " + terrain.terrainData.alphamapWidth);
        Debug.Log("terrain.terrainData.heightmapWidth : " + terrain.terrainData.heightmapWidth);
        Debug.Log("terrain.terrainData.size : " + terrain.terrainData.size);

        for (int x = 0; x < terrain.terrainData.heightmapWidth; x++)
        {
            for (int y = 0; y < terrain.terrainData.heightmapHeight; y++)
            {
                float snow = splats[Mathf.RoundToInt(x * ((terrain.terrainData.alphamapWidth + 1.0f) / (terrain.terrainData.heightmapWidth))), Mathf.RoundToInt(y * ((terrain.terrainData.alphamapHeight + 1.0f) / (terrain.terrainData.heightmapHeight))), snowTextureIndex];
                heights[x, y] += snowcurve.Evaluate(snow) * snowHeight / terrain.terrainData.size.y;
            }
        }

        terrain.terrainData.SetHeights(0, 0, heights);
    }

    void PaintSnow(bool addSnow=true)
    {
        int terrainIndex = GetTerrain();
        if (terrainIndex==-1 ||
            snowTextureIndex >= terrains[terrainIndex].terrainData.splatPrototypes.Length ||
            fillTextureIndex >= terrains[terrainIndex].terrainData.splatPrototypes.Length)
            return;

        Terrain terrain = terrains[terrainIndex];

        if (addSnow)
        {
            if (autoSaveLoad)
            {
                if (splatsBackups[terrainIndex] != null)
                    Load(terrainIndex);
                else
                    Save(terrainIndex);
            }

            if (snowDirBy == SnowDirBy.Transform)
            {
                if (snowDir)
                    snowDirection = snowDir.forward;
                else
                    snowDirBy = SnowDirBy.Vector3;
            }
            if (Mathf.Approximately(snowDirection.magnitude, 0))
                snowDirection = -Vector3.up;
        }

        bool angled = true;
        if (snowDirection.normalized == -Vector3.up)
            angled = false;

        float StartFadeH = EndFadeHeight - terrain.transform.position.y;
        float EndFadeH = StartFadeHeight - terrain.transform.position.y;

        float[,,] splats = terrain.terrainData.GetAlphamaps(0, 0, terrain.terrainData.alphamapWidth, terrain.terrainData.alphamapHeight);

        for (int x=0; x < terrain.terrainData.alphamapWidth; x++)
        {
            for (int y=0; y < terrain.terrainData.alphamapHeight; y++)
            {
                float y1 = (float)x/(float)terrain.terrainData.alphamapWidth;
                float x1 = (float)y/(float)terrain.terrainData.alphamapHeight;

                //Reset (remove snow):
                splats[x, y, snowTextureIndex] = 0;
                float sum = 0;
                for (int i = 0; i < terrain.terrainData.alphamapLayers; i++)
                {
                    sum += splats[x, y, i];
                }
                if (sum == 0)
                {
                    for (int i = 0; i < terrain.terrainData.alphamapLayers; i++)
                    {
                        if (i == fillTextureIndex)
                            splats[x, y, i] = 1;
                        else
                            splats[x, y, i] = 0;
                    }
                }
                else
                {
                    for (int i = 0; i < terrain.terrainData.alphamapLayers; i++)
                    { splats[x, y, i] /= sum; }
                }

                if (addSnow)
                {
                    float angle = 0;
                    if (angled)
                    {
                        angle = Vector3.Angle(terrain.terrainData.GetInterpolatedNormal(x1, y1), -snowDirection);
                    }
                    else
                    {
                        angle = terrain.terrainData.GetSteepness(x1, y1); //because this is faster than the Vector3.Angle() stuff (not that it matters much)
                    }
                    float snow = Mathf.Clamp01((angle - EndFadeAngle) / (StartFadeAngle - EndFadeAngle)); //angle
                    snow *= Mathf.Clamp01((terrain.terrainData.GetHeight((int)(x1 * terrain.terrainData.heightmapWidth), (int)(y1 * terrain.terrainData.heightmapHeight)) - StartFadeH) / (EndFadeH - StartFadeH)); //height
                    for (int i = 0; i < terrain.terrainData.alphamapLayers; i++)
                    {
                        if (i == snowTextureIndex)
                            splats[x, y, i] = snow;
                        else
                            splats[x, y, i] *= 1 - snow;
                    }
                }
            }
        }
        terrain.terrainData.SetAlphamaps(0, 0, splats);
    }

    void Save()
    {
        int terrainIndex = GetTerrain();
        if (terrainIndex == -1)
            return;

        Save(terrainIndex);
    }
    void Load()
    {
        int terrainIndex = GetTerrain();
        if (terrainIndex==-1)
            return;

        Load(terrainIndex);
    }
    void Save(int terrainIndex)
    {
        splatsBackups[terrainIndex] = terrains[terrainIndex].terrainData.GetAlphamaps(0, 0, terrains[terrainIndex].terrainData.alphamapWidth, terrains[terrainIndex].terrainData.alphamapHeight);
        heightsBackups[terrainIndex] = terrains[terrainIndex].terrainData.GetHeights(0, 0, terrains[terrainIndex].terrainData.heightmapWidth, terrains[terrainIndex].terrainData.heightmapHeight);
    }
    void Load(int terrainIndex)
    {
        terrains[terrainIndex].terrainData.SetAlphamaps(0, 0, splatsBackups[terrainIndex]);
        terrains[terrainIndex].terrainData.SetHeights(0, 0, heightsBackups[terrainIndex]);
    }

    int GetTerrain()
    {
        Transform selection = Selection.activeTransform;
        Terrain terrain = null;
        if (selection)
            terrain = selection.GetComponent<Terrain>();
        else return -1;

        //Debug.Log("terrains.Count before cleanup: " + terrains.Count);
        for (int i = 0; i < terrains.Count;) //cleanup
        {
            if (terrains[i] == null)
            {
                terrains.RemoveRange(i, 1);
                splatsBackups.RemoveRange(i, 1);
                heightsBackups.RemoveRange(i, 1);
            }
            else i++;
        }
        //Debug.Log("terrains.Count after cleanup: " + terrains.Count);
        
        int index = terrains.IndexOf(terrain);
        if (index == -1)
        {
            terrains.Add(terrain);
            index = terrains.Count - 1;
            splatsBackups.Add(terrain.terrainData.GetAlphamaps(0, 0, terrain.terrainData.alphamapWidth, terrain.terrainData.alphamapHeight));
            heightsBackups.Add(terrain.terrainData.GetHeights(0, 0, terrain.terrainData.heightmapWidth, terrain.terrainData.heightmapHeight));
        }
        //Debug.Log("GetTerrain: index = " + index);
        //Debug.Log("GetTerrain: terrains = " + terrains.Count);
        //Debug.Log("GetTerrain: splatsBackups = " + splatsBackups.Count);
        //Debug.Log("GetTerrain: heightsBackups = " + heightsBackups.Count);

        return index;
    }
}
