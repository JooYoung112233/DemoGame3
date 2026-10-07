using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Demo6.Game;
using Demo6.Core.Combat;

// Inspection/export only. Runtime test setup is confined to an assistant-started, disposable Play session.
public static class MineArtReview
{
    const string Root="아트/굴쥐-갱도-정수리-v1";
    static string Json(object value)=>Newtonsoft.Json.JsonConvert.SerializeObject(value,Newtonsoft.Json.Formatting.Indented);
    static void SaveTexture(Texture texture,string path, int divisor=1)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var rt=RenderTexture.GetTemporary(texture.width/divisor,texture.height/divisor,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var previous=RenderTexture.active;
        Texture2D copy=null;
        try{
            Graphics.Blit(texture,rt); RenderTexture.active=rt;
            copy=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
            copy.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);copy.Apply();
            File.WriteAllBytes(path,copy.EncodeToPNG());
        }finally{RenderTexture.active=previous;if(copy)UnityEngine.Object.DestroyImmediate(copy);RenderTexture.ReleaseTemporary(rt);}
    }
    public static object ExportSources()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Export masters while stopped.");
        var rows=new List<object>();
        const string source="아트/정수리/굴쥐-v1";
        foreach(var part in new[]{"body","tail","head","foot","dead","preview"})
        {
            var sprite=RatMineArt.Build(part,256f);
            string name="td_rat_"+(part=="preview"?"body":part=="body"?"torso":part)+"_v001.png";
            SaveTexture(sprite.texture,source+"/layers/"+name);
            if(part=="preview"||part=="foot"||part=="dead"){
                SaveTexture(sprite.texture,source+"/"+name);
                SaveTexture(sprite.texture,"Assets/Art/TopDown/Enemies/"+name,2);
            }
            rows.Add(new{part,file=name,width=sprite.texture.width,height=sprite.texture.height,masterWorldPPU=512,gamePPU=256});
            UnityEngine.Object.DestroyImmediate(sprite.texture);UnityEngine.Object.DestroyImmediate(sprite);
        }
        var map=new[]{ShapeSprites.DirtFloor(new Rect(-14,-8,28,16),1701,null),ShapeSprites.StoneWall(new Rect(-4,-0.5f,8,1)),ShapeSprites.RoughRock(new Rect(-4,-4,8,8)),ShapeSprites.TimberPost(0)};
        for(int i=0;i<map.Length;i++)SaveTexture(map[i].texture,Root+"/맵-PNG/"+new[]{"floor","stone-wall","rock","timber"}[i]+".png");
        for(int i=0;i<3;i++){UnityEngine.Object.DestroyImmediate(map[i].texture);UnityEngine.Object.DestroyImmediate(map[i]);}
        File.WriteAllText(Root+"/원본-규격.json",Json(rows));
        return Json(rows);
    }
    public static object ValidatePsd()
    {
        const string source="아트/정수리/굴쥐-v1/td_rat_master_v001.psd", temp="Assets/__RatPsdReview.psd";
        if(File.Exists(temp))throw new InvalidOperationException("Review path occupied");
        File.Copy(source,temp);
        AssetDatabase.ImportAsset(temp,ImportAssetOptions.ForceSynchronousImport);
        var ti=AssetImporter.GetAtPath(temp) as TextureImporter;
        if(ti==null)throw new InvalidOperationException("PSD importer not TextureImporter");
        ti.isReadable=true;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.mipmapEnabled=false;ti.alphaIsTransparency=false;ti.SaveAndReimport();
        var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(temp);
        if(!tex)throw new InvalidOperationException("PSD texture load failed");
        var expected=new Texture2D(2,2);expected.LoadImage(File.ReadAllBytes("아트/정수리/굴쥐-v1/td_rat_body_v001.png"));
        var a=tex.GetPixels32();var b=expected.GetPixels32();int max=0,differ=0;
        for(int i=0;i<a.Length;i++){int d=Mathf.Max(Mathf.Abs(a[i].r-b[i].r),Mathf.Abs(a[i].g-b[i].g),Mathf.Abs(a[i].b-b[i].b),Mathf.Abs(a[i].a-b[i].a));if(d>0)differ++;max=Mathf.Max(max,d);}
        string result=Json(new{importer=ti.GetType().Name,width=tex.width,height=tex.height,compare="Aseprite layered composite versus directly rendered source PNG",differingPixels=differ,maxChannelDifference=max});
        File.WriteAllText("아트/정수리/굴쥐-v1/psd-validation.json",result);
        UnityEngine.Object.DestroyImmediate(expected);
        Directory.CreateDirectory(Root+"/검수/PSD-import");
        File.Move(temp,Root+"/검수/PSD-import/import-tested.psd");File.Move(temp+".meta",Root+"/검수/PSD-import/import-tested.psd.meta");AssetDatabase.Refresh();
        return result;
    }
    public static async Task<string> StartRoom(){Application.runInBackground=true;await Task.Delay(500);return (string)Capture("mine-entry-final");}
    public static object ImportArt()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
        AssetDatabase.Refresh();
        foreach(var part in new[]{"body","foot","dead"}){
            string path="Assets/Art/TopDown/Enemies/td_rat_"+part+"_v001.png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=256;importer.spritePivot=new Vector2(.5f,.5f);
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;
            importer.SaveAndReimport();
        }
        var guids=AssetDatabase.FindAssets("t:CombatArtSet");
        var pathArt=guids.Select(AssetDatabase.GUIDToAssetPath).First(p=>!p.Contains("Example"));
        var art=AssetDatabase.LoadAssetAtPath<CombatArtSet>(pathArt);
        Directory.CreateDirectory(Root+"/작업전-백업/"+Path.GetDirectoryName(pathArt));
        if(!File.Exists(Root+"/작업전-백업/"+pathArt))File.Copy(pathArt,Root+"/작업전-백업/"+pathArt);
        art.topDown.rat.body=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/TopDown/Enemies/td_rat_body_v001.png");
        art.topDown.rat.foot=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/TopDown/Enemies/td_rat_foot_v001.png");
        art.topDown.rat.dead=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/TopDown/Enemies/td_rat_dead_v001.png");
        art.topDown.rat.scale=1f;
        EditorUtility.SetDirty(art);AssetDatabase.SaveAssetIfDirty(art);
        return Json(new{pathArt,body=art.topDown.rat.body.name,width=art.topDown.rat.body.texture.width,ppu=art.topDown.rat.body.pixelsPerUnit,scale=art.topDown.rat.scale});
    }
    public static object RuntimeState()
    {
        var root=DungeonRoot.Instance;
        return Json(new{playing=EditorApplication.isPlaying,paused=EditorApplication.isPaused,scale=Time.timeScale,time=Time.time,gamePaused=TimeScaleService.Paused,background=Application.runInBackground,focused=Application.isFocused,topDown=TopDownView.Active,
            player=root&&root.Player?root.Player.Position.ToString():"",cell=root&&root.CurrentCell!=null?root.CurrentCell.Id:"",
            enemies=Enemy.All.Where(e=>e&&!e.IsDummy).Select(e=>new{e.name,kind=e.Kind.ToString(),position=e.Position.ToString(),pose=e.CurrentPose.ToString(),e.Aware,e.VisionHidden}).ToArray()});
    }
    public static object StageRat()
    {
        if(!EditorApplication.isPlaying||!DungeonRoot.Instance)throw new InvalidOperationException("Dungeon Play required");
        Application.runInBackground=true;
        var player=DungeonRoot.Instance.Player;
        var rat=Enemy.All.Where(e=>e&&!e.Dead&&!e.IsDummy&&e.Kind==MonsterKind.Rat).OrderBy(e=>(e.Position-player.Position).sqrMagnitude).First();
        Vector2 at=rat.Position+Vector2.left*3f;
        if(rat.Territory.HasValue){var r=rat.Territory.Value;at=new Vector2(Mathf.Clamp(at.x,r.xMin+1f,r.xMax-1f),Mathf.Clamp(at.y,r.yMin+1f,r.yMax-1f));}
        if(Physics2D.OverlapCircle(at,0.45f,Layers.WallMask))at=rat.Position+Vector2.right*2.2f;
        player.Teleport(at);
        // Same existing test-panel protection; no rule, health, AI, or asset value is changed on disk.
        Tuning.Invincible=true;
        return Json(new{rat=rat.name,position=rat.Position.ToString(),player=at.ToString(),note="Existing rat, normal AI, temporary invincibility in review session"});
    }
    public static async Task<string> Sequence(){StageRat();return await ObserveRat();}
    public static async Task<string> ObserveRat()
    {
        var observations=new List<object>();var seen=new HashSet<string>();
        for(int i=0;i<120;i++)
        {
            await Task.Delay(33);
            if(!EditorApplication.isPlaying)break;
            var p=PlayerController.Instance;
            var rat=Enemy.All.Where(e=>e&&!e.Dead&&!e.IsDummy&&e.Kind==MonsterKind.Rat).OrderBy(e=>(e.Position-p.Position).sqrMagnitude).FirstOrDefault();
            if(!rat)continue;
            var body=rat.GetComponent<SpriteFlash>().target;
            var head=body.transform.Find("Rat Head");var tail=body.transform.Find("Rat Tail");
            string pose=rat.CurrentPose.ToString();
            observations.Add(new{time=Time.time,pose,poseTime=rat.PoseTime,position=rat.Position.ToString(),facing=rat.FacingDirection.ToString(),bodyAngle=body.transform.eulerAngles.z,
                bodySprite=body.sprite.name,bodyScale=body.transform.localScale.ToString(),legPositions=rat.transform.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name=="Leg").Select(r=>r.transform.localPosition.ToString()).ToArray(),
                hidden=rat.VisionHidden,headHidden=head&&head.GetComponent<SpriteRenderer>().forceRenderingOff});
            if(seen.Add(pose))Capture("rat-"+pose.ToLowerInvariant());
        }
        File.WriteAllText(Root+"/검수/rat-runtime-observation.json",Json(observations));
        return Json(new{samples=observations.Count,poses=seen.ToArray()});
    }
    public static async Task<string> MapAndVisibility()
    {
        var p=PlayerController.Instance;
        p.Teleport(new Vector2(0f,0f));
        await Task.Delay(700);Capture("mine-start-room");
        var rows=Enemy.All.Where(e=>e&&!e.IsDummy).Select(e=>new{e.name,position=e.Position.ToString(),hidden=e.VisionHidden,bodyHidden=e.GetComponent<SpriteFlash>().target.forceRenderingOff}).ToArray();
        File.WriteAllText(Root+"/검수/visibility.json",Json(rows));
        return Json(new{actors=rows.Length,hidden=rows.Count(x=>x.hidden),hiddenRenderers=rows.Count(x=>x.hidden&&x.bodyHidden),screenshot=Root+"/검수/mine-start-room.png"});
    }
    public static object RestoreReview(){Tuning.Invincible=false;Application.runInBackground=false;return "Review flags restored; no asset settings changed.";}
    public static object Capture(string label)
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play required");
        var camera=Camera.main;var previousTarget=camera.targetTexture;var previous=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(1920,1080,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        Texture2D read=null;
        string path=Root+"/검수/"+label+".png";
        try{
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            read=new Texture2D(1920,1080,TextureFormat.RGBA32,false);read.ReadPixels(new Rect(0,0,1920,1080),0,0);read.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,read.EncodeToPNG());
        }finally{camera.targetTexture=previousTarget;RenderTexture.active=previous;if(read)UnityEngine.Object.DestroyImmediate(read);RenderTexture.ReleaseTemporary(rt);}
        return path;
    }
    public static async Task<string> HitAndDeath()
    {
        var p=PlayerController.Instance;
        var rat=Enemy.All.Where(e=>e&&!e.Dead&&!e.IsDummy&&e.Kind==MonsterKind.Rat).OrderBy(e=>(e.Position-p.Position).sqrMagnitude).First();
        if(rat.Territory.HasValue){var r=rat.Territory.Value;p.Teleport(new Vector2(Mathf.Clamp(rat.Position.x-1f,r.xMin+.7f,r.xMax-.7f),Mathf.Clamp(rat.Position.y,r.yMin+.7f,r.yMax-.7f)));}
        await Task.Delay(100);
        int damage=rat.TakeHit(1,false,DamageSource.SwordWave,100f,true,1f,out _,out _);
        await Task.Delay(35);Capture("rat-hit");
        string pose=rat?rat.CurrentPose.ToString():"removed";
        await Task.Delay(350);
        int lethal=rat.TakeHit(rat.Health.Current,false,DamageSource.Basic);
        await Task.Delay(45);Capture("rat-death");
        string deadSprite=rat?rat.GetComponent<SpriteFlash>().target.sprite.name:"removed";
        await Task.Delay(650);Capture("rat-corpse");
        return Json(new{damage,hitPose=pose,lethal,deadSprite,note="Damage invoked through actual Enemy.TakeHit for visual verification in disposable Play session"});
    }
}
