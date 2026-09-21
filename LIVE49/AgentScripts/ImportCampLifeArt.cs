using System.IO;
using UnityEditor;
using UnityEngine;
public static class ImportCampLifeArt
{
    public static string Run()
    {
        string[] ids={"E03-HUB-rations-shared","E04-HUB-stove-off","E17-HUB-cooking","E17-HUB-warm-meal","E18-HUB-cabinet-secured","E21-HUB-camera-protected","E23-L2-fuel-collected","01-L5-wary","02-L5-water-placed","03-L5-relaxed","E12-L5-place-water","E12-L5-food-alternative","E14-HUB-rest","E24-HUB-memories","E24-HUB-memories-with-dog"};
        foreach(string id in ids)
        {
            string source=Path.GetFullPath(Path.Combine(Application.dataPath,"../../art/chapter00-01/art-polish-v1/review/"+id+".png"));
            string target="Assets/_Project/Resources/Live49/Stages/life-"+id+".png";File.Copy(source,target,true);AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);
            var t=(TextureImporter)AssetImporter.GetAtPath(target);t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.maxTextureSize=4096;t.npotScale=TextureImporterNPOTScale.None;t.mipmapEnabled=false;t.textureCompression=TextureImporterCompression.Uncompressed;t.SaveAndReimport();
        }
        return "Imported 15 existing scene originals without modifying paintings, dialogue, music or scenes.";
    }
}
