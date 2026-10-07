using System;
using System.IO;
using UnityEditor;
using UnityEngine;
public static class BeetleV31Import
{
    public static object Run()
    {
        // Only this task's new texture imports; reimporting them is safe during Play.
        AssetDatabase.Refresh();int count=0;
        foreach(string dir in new[]{"Assets/Resources/BeetleV31","Assets/Resources/TelegraphV31"})
        foreach(string path in Directory.GetFiles(dir,"*.png"))
        {
            var t=(TextureImporter)AssetImporter.GetAtPath(path);t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=2048;t.mipmapEnabled=false;t.alphaIsTransparency=true;t.filterMode=FilterMode.Bilinear;t.wrapMode=TextureWrapMode.Clamp;
            if(dir.Contains("Telegraph"))
            {
                t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.spritePixelsPerUnit=512;t.spritePivot=Vector2.one*.5f;
                var settings=new TextureImporterSettings();t.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Center;t.SetTextureSettings(settings);
                if(Path.GetFileName(path).StartsWith("rect")){float border=path.Contains("fill")?128:32;t.spriteBorder=new Vector4(border,border,border,border);}
            }
            else t.textureType=TextureImporterType.Default;
            t.SaveAndReimport();count++;
        }
        return new{imported=count};
    }
}
