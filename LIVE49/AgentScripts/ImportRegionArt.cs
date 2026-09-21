using System.IO;
using UnityEditor;
using UnityEngine;
public static class ImportRegionArt
{
    public static string Run()
    {
        string[] sources={"gas-station","general-store","repair-shop","house","riverside","hill-road"};
        for(int i=0;i<sources.Length;i++)
        {
            var source=Path.GetFullPath(Path.Combine(Application.dataPath,"../../art/chapter00-01/background-harmony-v2/sources/"+sources[i]+".png"));
            string dest="Assets/_Project/Resources/Live49/Stages/region-L"+(i+2)+".png";
            Directory.CreateDirectory(Path.GetDirectoryName(dest));File.Copy(source,dest,true);
            AssetDatabase.ImportAsset(dest,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(dest);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.maxTextureSize=4096;importer.npotScale=TextureImporterNPOTScale.None;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        }
        return "Six existing region background originals imported through Unity; no new art or scenes.";
    }
}
