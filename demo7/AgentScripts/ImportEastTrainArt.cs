using EastTrain;
using UnityEditor;
using UnityEngine;

public static class ImportEastTrainArt
{
    static EastTrainArtSet.Part Load(string filename)
    {
        string path = "Assets/Art/FirstPass/" + filename + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new System.Exception("Missing art: " + path);
        importer.textureType = TextureImporterType.Default; importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false; importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear; importer.isReadable = true; importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var pixels = texture.GetPixels32(); int minX = texture.width, minY = texture.height, maxX = 0, maxY = 0;
        int clear = 0;
        for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
        {
            if (pixels[y * texture.width + x].a <= 8) { clear++; continue; }
            minX = Mathf.Min(x, minX); maxX = Mathf.Max(x, maxX); minY = Mathf.Min(y, minY); maxY = Mathf.Max(y, maxY);
        }
        if (clear < texture.width * texture.height / 100 || minX >= maxX) throw new System.Exception("Invalid transparent sprite: " + filename);
        var rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        Debug.Log(filename + " native=" + texture.width + "x" + texture.height + " bounds=" + rect + " alpha-clear=" + clear);
        importer.isReadable = false; importer.SaveAndReimport();
        return new EastTrainArtSet.Part { Texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path), Pixels = rect };
    }
    public static string Run()
    {
        AssetDatabase.Refresh();
        const string path = "Assets/Resources/EastTrainArtSet.asset";
        var set = AssetDatabase.LoadAssetAtPath<EastTrainArtSet>(path);
        if (set == null) { set = ScriptableObject.CreateInstance<EastTrainArtSet>(); AssetDatabase.CreateAsset(set, path); }
        set.Furnace = Load("furnace-v1"); set.Engine = Load("engine-v1"); set.Workbench = Load("workbench-v1");
        EditorUtility.SetDirty(set); AssetDatabase.SaveAssets();
        return "Imported three native-resolution transparent props; crops use sprite rectangles and preserve original PNG pixels.";
    }
}
