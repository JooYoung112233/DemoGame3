using System;
using System.IO;
using Live49.Chapter00;
using UnityEditor;
using UnityEngine;

public static class ImportContinuationAssets
{
    public static string Run()
    {
        string workspace=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
        string destination="Assets/_Project/Resources/Live49/Stages";
        Directory.CreateDirectory(destination);
        var graph=JsonUtility.FromJson<ContinuationGraph>(File.ReadAllText("Assets/_Project/Resources/Live49/continuation.json"));
        foreach(var scene in graph.scenes)
        {
            string target=destination+"/"+scene.id+".png";
            if(!File.Exists(target))File.Copy(Path.Combine(workspace,scene.path),target);
            AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=4096;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        }
        AssetDatabase.Refresh();return "Imported "+graph.scenes.Length+" existing art files. No new scene or raster editing.";
    }
}
