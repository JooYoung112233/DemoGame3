using System.IO;
using UnityEditor;
using UnityEngine;
public static class ImportFirstWeekArt
{
    public static string Run()
    {
        string json="Assets/_Project/Resources/Live49/FirstWeek/story.json";Directory.CreateDirectory(Path.GetDirectoryName(json));
        File.Copy(Path.GetFullPath(Path.Combine(Application.dataPath,"../../design/chapter01/first-week-runtime-v1/story.json")),json,true);AssetDatabase.ImportAsset(json,ImportAssetOptions.ForceSynchronousImport);
        string[] scenes={"E05-delivery","E06-ready","E06-drawing","E06-complete","E07-L2-sejin-repair","E08-L4-retrieve","E09-L2-camera-handover","L4-toolbag-available","L4-toolbag-collected","L6-first-shoot"};
        foreach(var scene in scenes)Copy("art/chapter00-01/art-polish-v1/review/"+scene+".png","week-"+scene);
        Copy("art/chapter01/first-photo-v1/sources/riverside-photo-style-source-v2.png","week-first-photo");
        Copy("art/chapter01/first-photo-v1/layers/album-blank-native.png","week-album");
        Copy("art/chapter01/completion-v1/layers/sejin-dialogue-native.png","week-sejin-portrait");
        Copy("art/chapter01/colored-pencils-v1/colored-pencils-cutout-native-v1.png","week-pencils");
        return "14 existing originals imported through AssetDatabase; no new paintings or scene files.";
    }
    static void Copy(string source,string id)
    {
        source=Path.GetFullPath(Path.Combine(Application.dataPath,"../../"+source));string target="Assets/_Project/Resources/Live49/Stages/"+id+".png";
        File.Copy(source,target,true);AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(target);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.maxTextureSize=4096;importer.npotScale=TextureImporterNPOTScale.None;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
    }
}
