using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Resource wiring only. Hidden NPC encounter/save/quest rules remain a separate system.
public static class WireNpcAndInfectedArt
{
    const string Folder = "Assets/Prefabs/Settlement/";
    static string NpcBodyPath(string id)=>"Assets/Art/Tokens/npc-"+id+"-body-"+(id=="giho"?"v2":"v3")+".png";
    public static string RevisedPeople()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before changing art prefabs.");
        var common=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PaperStandee.mat");
        var entries=new[]{(id:"doyun",height:1.72f),(id:"jun",height:1.72f),(id:"giho",height:1.72f)};
        foreach(var entry in entries)
            if(!AssetDatabase.LoadAssetAtPath<Sprite>(NpcBodyPath(entry.id)))throw new InvalidOperationException("Import selected NPC "+entry.id);
        foreach(var entry in entries)
        {
            string path=Folder+"Npc_"+entry.id+".prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var body=root.transform.Find("Body").GetComponent<SpriteRenderer>();
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(NpcBodyPath(entry.id));
                var bounds=Bounds(sprite);float scale=entry.height*sprite.pixelsPerUnit/bounds.height;
                body.sprite=sprite;body.transform.localScale=Vector3.one*scale;
                body.transform.localPosition=new Vector3((sprite.pivot.x-bounds.center.x)*scale/sprite.pixelsPerUnit,
                    (sprite.pivot.y-bounds.y)*scale/sprite.pixelsPerUnit+.035f,body.transform.localPosition.z);
                var style=body.GetComponent<PaperStandeeStyle>()??body.gameObject.AddComponent<PaperStandeeStyle>();style.Configure(common);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();
        return "Revised NPC bodies linked at the original 1.72 full-body height; original bases and foot contact retained; no clipping.";
    }
    public static string RefreshShaders()
    {
        foreach(var path in new[]{"Assets/Shaders/PaperStandee.shader","Assets/Shaders/PaperPortrait.shader","Assets/Shaders/StandeeStraightAlphaExport.shader"})
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
        return "Reimported current shader sources.";
    }
    public static string Run()
    {
        var common = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PaperStandee.mat");
        var portrait = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PaperPortrait.mat");
        var pawn = AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"FieldPawn.prefab");
        var previous = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Tokens/infected-body.png");
        var infected = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Tokens/infected-body-v4.png");
        if(!common || !portrait || !pawn || !infected) throw new InvalidOperationException("Import all bodies and apply the common style first.");
        string[] ids={"doyun","jun","giho"};
        foreach(var id in ids)
            if(!AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Tokens/npc-"+id+"-body.png")) throw new InvalidOperationException("Missing NPC body "+id);
        var files=new List<string>();
        foreach(var id in ids)
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(NpcBodyPath(id));
            if(!sprite)sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Tokens/npc-"+id+"-body-v2.png");
            if(!sprite)sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Tokens/npc-"+id+"-body.png");
            // A prefab contents scene avoids spawning anything into the user's running game.
            var root=PrefabUtility.LoadPrefabContents(Folder+"FieldPawn.prefab"); root.name="Npc_"+id;
            try
            {
                var body=root.transform.Find("Body").GetComponent<SpriteRenderer>();
                body.sprite=sprite; body.flipX=false; body.color=Color.white;
                float height=1.72f;
                var bounds=Bounds(sprite);float s=height*sprite.pixelsPerUnit/bounds.height;
                body.transform.localScale=Vector3.one*s;
                body.transform.localPosition=new Vector3((sprite.pivot.x-bounds.center.x)*s/sprite.pixelsPerUnit,(sprite.pivot.y-bounds.y)*s/sprite.pixelsPerUnit+.035f,0);
                var style=body.GetComponent<PaperStandeeStyle>()??body.gameObject.AddComponent<PaperStandeeStyle>();style.Configure(common);
                var baseSprite=root.transform.Find("Base").GetComponent<SpriteRenderer>();baseSprite.color=new Color(.64f,.79f,.74f,1);
                string path=Folder+root.name+".prefab";PrefabUtility.SaveAsPrefabAsset(root,path);files.Add(path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        int replacements=0;
        string oldPath=AssetDatabase.GetAssetPath(previous);
        var infectedBounds=Bounds(infected);
        var crop=new Rect(infectedBounds.x/infected.rect.width,(infectedBounds.yMax-Mathf.Min(infectedBounds.height,infectedBounds.width*1.1f))/infected.rect.height,infectedBounds.width/infected.rect.width,Mathf.Min(infectedBounds.height,infectedBounds.width*1.1f)/infected.rect.height);
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{Folder.TrimEnd('/')}))
        {
            var path=AssetDatabase.GUIDToAssetPath(guid);
            if(!AssetDatabase.GetDependencies(path).Contains(oldPath))continue;
            var root=PrefabUtility.LoadPrefabContents(path);bool changed=false;
            try
            {
                foreach(var component in root.GetComponentsInChildren<Component>(true))
                {
                    if(!component)continue;
                    var serialized=new SerializedObject(component);var property=serialized.GetIterator();bool dirty=false;
                    while(property.Next(true))
                        if(property.propertyType==SerializedPropertyType.ObjectReference && property.objectReferenceValue==previous)
                        {property.objectReferenceValue=infected;dirty=true;replacements++;}
                    if(dirty){serialized.ApplyModifiedPropertiesWithoutUndo();changed=true;}
                }
                foreach(var fit in root.GetComponentsInChildren<BattlePortraitFit>(true))
                {
                    if(fit.Crops==null)continue;
                    for(int i=0;i<fit.Crops.Length;i++)if(fit.Crops[i].Sprite==infected){fit.Crops[i].Area=crop;changed=true;}
                }
                foreach(var battle in root.GetComponentsInChildren<ExpeditionBattlePanel>(true))
                    if(battle.VisibleBounds!=null)foreach(var entry in battle.VisibleBounds)
                        if(entry!=null&&entry.Sprite==infected){entry.Pixels=infectedBounds;changed=true;}
                foreach(var img in root.GetComponentsInChildren<Image>(true))if(img.sprite==infected)
                {var style=img.GetComponent<PaperPortraitStyle>()??img.gameObject.AddComponent<PaperPortraitStyle>();style.Configure(portrait);changed=true;}
                if(changed)PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();
        return "Created NPC prefabs: "+string.Join(", ",files)+"\nReplaced legacy infected art references: "+replacements+"; updated portrait and battle bounds. No encounter/balance/save rules changed.";
    }
    static Rect Bounds(Sprite sprite)
    {
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            ImageConversion.LoadImage(texture,File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)),false);
            float sx=(float)texture.width/sprite.texture.width,sy=(float)texture.height/sprite.texture.height;
            int l=Mathf.RoundToInt(sprite.rect.x*sx),b=Mathf.RoundToInt(sprite.rect.y*sy),r=Mathf.RoundToInt(sprite.rect.xMax*sx),t=Mathf.RoundToInt(sprite.rect.yMax*sy);
            var p=texture.GetPixels32();int x0=r,y0=t,x1=l-1,y1=b-1;
            for(int y=b;y<t;y++)for(int x=l;x<r;x++)if(p[y*texture.width+x].a>=32){x0=Math.Min(x,x0);y0=Math.Min(y,y0);x1=Math.Max(x,x1);y1=Math.Max(y,y1);}
            if(x1<x0)throw new InvalidOperationException("Empty body");
            return new Rect((x0-l)/sx,(y0-b)/sy,(x1-x0+1)/sx,(y1-y0+1)/sy);
        }finally{Object.DestroyImmediate(texture);}
    }
}
