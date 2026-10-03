using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EastTrain;
using UnityEditor;
using UnityEngine;

public static class ImportEastTrainCrewArt
{
    [Serializable] public class Item { public string name, source; public int width, height; public Rect pixels; public int transparentPixels; }
    [Serializable] public class Report { public string generator = "Built-in image_gen"; public List<Item> entries = new List<Item>(); }
    static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    static readonly Dictionary<string, Color32[]> colors = new Dictionary<string, Color32[]>();
    static readonly Report report = new Report();
    static readonly List<EastTrainWorldArtSet.Entry> entries = new List<EastTrainWorldArtSet.Entry>();
    static void Add(string name, string file = null, int column = -1, int row = -1, bool opaque = false, Rect? region = null)
    {
        file = file ?? name;
        string path = "Assets/Art/CrewPass/" + file + ".png";
        if (!textures.TryGetValue(path, out var tex))
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new Exception("Missing " + path);
            importer.textureType = TextureImporterType.Default; importer.alphaIsTransparency = !opaque;
            importer.npotScale = TextureImporterNPOTScale.None; importer.mipmapEnabled = false;
            importer.maxTextureSize = 8192; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = true; importer.filterMode = FilterMode.Bilinear; importer.SaveAndReimport();
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            textures[path] = tex; colors[path] = tex.GetPixels32();
        }
        int x0 = column < 0 ? 0 : column * tex.width / 2, x1 = column < 0 ? tex.width : (column + 1) * tex.width / 2;
        int y0 = row < 0 ? 0 : (1 - row) * tex.height / 2, y1 = row < 0 ? tex.height : (2 - row) * tex.height / 2;
        if (region.HasValue)
        {
            var v = region.Value;
            x0 = Mathf.RoundToInt(v.xMin * tex.width); x1 = Mathf.RoundToInt(v.xMax * tex.width);
            y0 = Mathf.RoundToInt((1 - v.yMax) * tex.height); y1 = Mathf.RoundToInt((1 - v.yMin) * tex.height);
        }
        int minX = x1, maxX = x0, minY = y1, maxY = y0, clear = 0;
        var data = colors[path];
        for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
        {
            if (!opaque && data[y * tex.width + x].a <= 12) { clear++; continue; }
            minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
        }
        if (minX >= maxX || minY >= maxY) throw new Exception("Empty sprite " + name);
        if (!opaque && clear < (x1 - x0) * (y1 - y0) / 100) throw new Exception("Transparency absent " + name);
        var rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        entries.Add(new EastTrainWorldArtSet.Entry { Name = name, Image = new EastTrainArtSet.Part { Texture = tex, Pixels = rect } });
        report.entries.Add(new Item { name = name, source = path, width = tex.width, height = tex.height, pixels = rect, transparentPixels = clear });
    }
    public static string Run()
    {
        textures.Clear(); colors.Clear(); entries.Clear(); report.entries.Clear(); AssetDatabase.Refresh();
        foreach (var name in new[] { "warm-workshop", "warm-engine", "warm-cab", "crew-1", "crew-2", "crew-3" }) Add(name);
        Add("warm-snow", opaque:true);
        const string destination = "Assets/Resources/EastTrainCrewArtSet.asset";
        var asset = AssetDatabase.LoadAssetAtPath<EastTrainWorldArtSet>(destination);
        if (asset == null) { asset = ScriptableObject.CreateInstance<EastTrainWorldArtSet>(); AssetDatabase.CreateAsset(asset, destination); }
        asset.Entries = entries.ToArray(); EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
        foreach (var path in textures.Keys)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.isReadable = false; importer.SaveAndReimport();
        }
        var data = new { generator = report.generator, entries = report.entries.Select(v => new {
            v.name, v.source, v.width, v.height, v.transparentPixels,
            pixels = new { x = v.pixels.x, y = v.pixels.y, width = v.pixels.width, height = v.pixels.height }
        }).ToArray() };
        File.WriteAllText("아트/승무원-객차-v3/import-report.json", Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented));
        return "Imported " + entries.Count + " native crew/carriage assets; alpha verified.";
    }
}
