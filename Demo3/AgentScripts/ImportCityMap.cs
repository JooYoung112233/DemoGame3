using System.IO;
using UnityEditor;
using UnityEngine;
public static class ImportCityMap
{
    public static string Run()
    {
        var source=Path.GetFullPath(Path.Combine(Application.dataPath,"../../art/ui/city-map-v1/city-map-soft-source-v2.png"));
        const string path="Assets/_Project/Resources/Live49/Maps/city-map-soft-v2.png";
        Directory.CreateDirectory(Path.GetDirectoryName(path));File.Copy(source,path,true);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Default;importer.npotScale=TextureImporterNPOTScale.None;
        importer.maxTextureSize=4096;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        return path+" native "+texture.width+"x"+texture.height;
    }
}
