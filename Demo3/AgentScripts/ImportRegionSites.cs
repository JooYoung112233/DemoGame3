using System.IO;
using UnityEditor;
using UnityEngine;
public static class ImportRegionSites
{
    public static string Run()
    {
        foreach(string id in new[]{"L8","L9","L10"})
        {
            string source=Path.GetFullPath(Path.Combine(Application.dataPath,"../../art/chapter01/region-sites-v1/"+id+"-source.png"));
            string path="Assets/_Project/Resources/Live49/Stages/region-"+id+".png";
            File.Copy(source,path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var t=(TextureImporter)AssetImporter.GetAtPath(path);t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.maxTextureSize=2048;t.mipmapEnabled=false;t.textureCompression=TextureImporterCompression.Uncompressed;t.SaveAndReimport();
            var s=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(s==null||s.rect.width!=1672||s.rect.height!=941)throw new System.Exception("Native dimensions mismatch "+id);
        }
        return "Imported L8/L9/L10 native 1672x941 backgrounds, no resize";
    }
}
