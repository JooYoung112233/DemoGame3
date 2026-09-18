using System.IO;
using UnityEditor;
using UnityEngine;
public static class ImportKitchenPot
{
    public static string Run()
    {
        string source=Path.GetFullPath(Path.Combine(Application.dataPath,"../../art/chapter01/kitchen/soup-pot-v1.png"));
        const string target="Assets/_Project/Resources/Live49/Kitchen/soup-pot.png";
        Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(source,target,true);AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(target);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(target);if(sprite==null||sprite.rect.width!=714||sprite.rect.height!=523)throw new System.Exception("Pot original dimensions mismatch");
        return "Imported existing transparent 714x523 pot; no resize or art regeneration";
    }
}
