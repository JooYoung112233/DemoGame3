using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object=UnityEngine.Object;

// Import metadata only. The original full-resolution PNG is never cropped or rewritten.
// Run before assigning newly imported sprites to catalogs/prefabs: Single -> Multiple can change local file IDs.
public static class ImportStandeeSprite
{
    public static string Run()
    {
        var ids=new[]{"party-cook","party-mechanic","party-guard","party-researcher","npc-doyun","npc-jun","npc-giho"};
        return string.Join("\n",ids.Select(id=>
        {
            string path="Assets/Art/Tokens/"+id+"-body.png";
            var sprite=Import(path);
            return path+" | sprite rect "+sprite.rect+" | pivot "+sprite.pivot;
        }));
    }

    public static string ImportInfected()
    {
        var sprite=Import("Assets/Art/Tokens/infected-body-v4.png");
        return "Imported corrected infected sprite: "+sprite.rect;
    }

    public static string RevisedPeople()
    {
        var ids=new[]{"party-cook","party-mechanic","party-guard","party-researcher","npc-doyun","npc-jun","npc-giho"};
        return string.Join("\n",ids.Select(id=>
        {
            string version=id=="party-guard"||id=="party-researcher"||id=="npc-giho"?"v2":"v3";
            string path="Assets/Art/Tokens/"+id+"-body-"+version+".png";
            var sprite=Import(path);
            return path+" | visible body preserved; sprite rect "+sprite.rect;
        }));
    }

    public static string RevisedParty()
    {
        return string.Join("\n",new[]{"cook","mechanic","guard","researcher"}.Select(id=>
        {
            string version=id=="guard"||id=="researcher"?"v2":"v3";
            var sprite=Import("Assets/Art/Tokens/party-"+id+"-body-"+version+".png");
            return id+" full body imported: "+sprite.rect;
        }));
    }

    public static Sprite Import(string path,int padding=2,byte alphaThreshold=32)
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before sprite imports.");
        path=path.Replace('\\','/');
        string absolute=Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,path));
        string assets=Path.GetFullPath(Application.dataPath)+Path.DirectorySeparatorChar;
        if(!path.StartsWith("Assets/",StringComparison.Ordinal)||!absolute.StartsWith(assets,StringComparison.OrdinalIgnoreCase)||!path.EndsWith(".png",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Expected a project Assets PNG.",nameof(path));
        if(path=="Assets/Art/Settlement/standee-scout.png"||path=="Assets/Art/Settlement/standee-medic.png")throw new InvalidOperationException("Preserve approved legacy actor import geometry.");
        if(padding<0||padding>32||alphaThreshold==0)throw new ArgumentOutOfRangeException(nameof(padding));
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);Rect ink,rect;int width,height;
        try
        {
            if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(absolute),false))throw new InvalidOperationException("PNG could not be decoded: "+path);
            width=texture.width;height=texture.height;var pixels=texture.GetPixels32();
            int minX=width,minY=height,maxX=-1,maxY=-1,transparent=0;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                if(pixels[y*width+x].a<alphaThreshold){transparent++;continue;}
                minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);
            }
            if(maxX<0||transparent==0)throw new InvalidOperationException("A standee needs visible pixels and genuine alpha: "+path);
            ink=Rect.MinMaxRect(minX,minY,maxX+1,maxY+1);
            rect=Rect.MinMaxRect(Math.Max(0,minX-padding),Math.Max(0,minY-padding),Math.Min(width,maxX+1+padding),Math.Min(height,maxY+1+padding));
        }
        finally{Object.DestroyImmediate(texture);}

        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(!importer)throw new InvalidOperationException("PNG importer missing: "+path);
        var factories=new SpriteDataProviderFactories();factories.Init();
        var existingProvider=factories.GetSpriteEditorDataProviderFromObject(importer);
        existingProvider.InitSpriteEditorDataProvider();
        var existing=existingProvider.GetSpriteRects();
        // Unity can auto-slice newly generated transparent PNGs into a main body and tiny edge islands.
        // Only these newly authored, single-body source files may replace that initial automatic slicing.
        var authoredNames=new[]{"party-cook-body","party-mechanic-body","party-guard-body","party-researcher-body","npc-doyun-body","npc-jun-body","npc-giho-body","infected-body-v4"};
        string authoredName=Path.GetFileNameWithoutExtension(path);
        bool newBody=path.StartsWith("Assets/Art/Tokens/",StringComparison.Ordinal)&&(authoredNames.Contains(authoredName)||
            (authoredName.EndsWith("-v2",StringComparison.Ordinal)||authoredName.EndsWith("-v3",StringComparison.Ordinal))&&authoredNames.Contains(authoredName.Substring(0,authoredName.Length-3)));
        if(importer.spriteImportMode==SpriteImportMode.Multiple&&existing.Length>1&&!newBody)throw new InvalidOperationException("Do not replace an existing sprite sheet: "+path);
        var previous=existing.OrderByDescending(r=>r.rect.width*r.rect.height).FirstOrDefault();
        var spriteId=previous!=null?previous.spriteID:GUID.Generate();
        string spriteName=previous!=null&&!string.IsNullOrEmpty(previous.name)?previous.name:Path.GetFileNameWithoutExtension(path);

        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=100;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;
        importer.mipmapEnabled=false;importer.isReadable=false;importer.npotScale=TextureImporterNPOTScale.None;
        importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.maxTextureSize=Mathf.Clamp(Mathf.NextPowerOfTwo(Math.Max(width,height)),32,16384);
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
        // Preserve a stable per-sprite ID when rerun; the pivot sits at the visible bottom center.
        var spriteRect=new SpriteRect{name=spriteName,spriteID=spriteId,rect=rect,alignment=SpriteAlignment.Custom,
            pivot=new Vector2((ink.center.x-rect.x)/rect.width,(ink.y-rect.y)/rect.height),border=Vector4.zero};
        provider.SetSpriteRects(new[]{spriteRect});
        var names=provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if(names==null)throw new InvalidOperationException("Sprite importer does not support stable name/ID metadata.");
        names.SetNameFileIdPairs(new[]{new SpriteNameFileIdPair(spriteName,spriteId)});
        provider.Apply();importer.SaveAndReimport();
        var result=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();
        if(Vector2.Distance(result.rect.size,rect.size)>.01f)throw new InvalidOperationException("Sprite was downscaled or crop metadata failed: "+path+" -> "+result.rect);
        return result;
    }
}
