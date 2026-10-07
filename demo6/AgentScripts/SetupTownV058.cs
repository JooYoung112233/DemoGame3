using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using Demo6.Game;
using Demo6.Core.Town;
using Newtonsoft.Json;

public static class SetupTownV058
{
    const string Out="검증/town-v058";
    public static object State() => new {playing=EditorApplication.isPlaying,paused=EditorApplication.isPaused,
        compiling=EditorApplication.isCompiling,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
        dirty=UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty,sortAtRoot=typeof(SortingGroup).GetProperty("sortAtRoot")!=null};
    public static object Import()
    {
        if(EditorApplication.isPlaying)throw new Exception("Preserve active Play");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var paths=Directory.GetFiles("Assets/Resources/TownArtV058/modules","*.png");
        foreach(var raw in paths)
        {
            string path=raw.Replace('\\','/');var ti=(TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType=TextureImporterType.Sprite;ti.spriteImportMode=SpriteImportMode.Single;ti.spritePixelsPerUnit=40;
            var settings=new TextureImporterSettings();ti.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;
            settings.spriteAlignment=(int)SpriteAlignment.Center;ti.SetTextureSettings(settings);
            ti.alphaIsTransparency=true;ti.mipmapEnabled=false;ti.filterMode=FilterMode.Bilinear;ti.wrapMode=path.Contains("ground-dirt")?TextureWrapMode.Repeat:TextureWrapMode.Clamp;
            ti.textureCompression=TextureImporterCompression.Uncompressed;ti.maxTextureSize=2048;ti.SaveAndReimport();
        }
        return new{imported=paths.Length,State=State()};
    }
    public static object StartReview()
    {
        if(!EditorApplication.isPlaying||GameSession.FromTitle)throw new Exception("Use unsaved test Play only");
        ProfileCarry.Ensure();SceneTravel.Load(SceneTravel.TownPath);return "Unsaved town review started; no device or save override";
    }
    static void Image(Camera camera,string name,int width,int height)
    {
        Directory.CreateDirectory(Out);var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
        var rt=new RenderTexture(width,height,24);var tex=new Texture2D(width,height,TextureFormat.RGBA32,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(Out+"/"+name+".png",tex.EncodeToPNG());}
        finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}
    }
    public static object Capture()
    {
        if(!TownRoot.Instance)throw new Exception("Town not ready");var cam=Camera.main;
        Image(cam,"game-camera",Screen.width,Screen.height);
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Out+"/game-view.png"));
        var oldPos=cam.transform.position;float oldSize=cam.orthographicSize;float oldAspect=cam.aspect;
        try{cam.transform.position=new Vector3(42,39,oldPos.z);cam.orthographicSize=12.8f;cam.aspect=1.5f;Image(cam,"overview",1536,1024);}
        finally{cam.transform.position=oldPos;cam.orthographicSize=oldSize;cam.aspect=oldAspect;}
        var report=new{scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,screen=new[]{Screen.width,Screen.height},cameraSize=oldSize,cameraPosition=new[]{oldPos.x,oldPos.y,oldPos.z},globalLight=TownRoot.Instance.Lighting.GlobalIntensity,
            props=TownRoot.Instance.Props.GetComponentsInChildren<SpriteRenderer>().Length,groups=TownRoot.Instance.Props.GetComponentsInChildren<SortingGroup>().Length,
            colliders=TownRoot.Instance.Props.GetComponentsInChildren<Collider2D>().Length,actorGroup=TownRoot.Instance.Player.GetComponent<SortingGroup>()!=null,
            input=UnityEngine.InputSystem.InputSystem.devices.Select(d=>new{d.name,d.enabled}).ToArray(),fromTitle=GameSession.FromTitle,
            note="Camera render and Game View capture; overview uses a temporary diagnostic camera size, restored immediately. No native user input verification."};
        File.WriteAllText(Out+"/capture-state.json",JsonConvert.SerializeObject(report,Formatting.Indented));return report;
    }
    static async Task WaitReviewFrames(TownRoot root,int minimumFrames=2)
    {
        int from=Time.frameCount;
        var deadline=DateTime.UtcNow.AddSeconds(5);
        // A delay alone does not prove that Unity advanced. Require two actual frames too.
        await Task.Delay(180);
        // Continuations can resume before LateUpdate, so advance one extra frame boundary.
        while(Time.frameCount<=from+minimumFrames)
        {
            if(!EditorApplication.isPlaying||EditorApplication.isPaused||!root||TownRoot.Instance!=root)
                throw new InvalidOperationException("Review requires the same unpaused unsaved town Play.");
            if(DateTime.UtcNow>=deadline)throw new TimeoutException("Unity did not advance two review frames.");
            await Task.Delay(40);
        }
        if(!EditorApplication.isPlaying||EditorApplication.isPaused||!root||TownRoot.Instance!=root)
            throw new InvalidOperationException("Town Play changed during review.");
        Physics2D.SyncTransforms();
    }
    static Collider2D[] ReviewContacts(TownRoot root) => root.Props.GetComponentsInChildren<Collider2D>()
        .Where(c=>c&&c.enabled&&c.gameObject.activeInHierarchy&&!c.isTrigger).ToArray();
    static bool HasPlayerClearance(Vector2 p,Collider2D[] cols) =>
        !cols.Any(c=>c.OverlapPoint(p)||Vector2.Distance(c.ClosestPoint(p),p)<.405f);
    public static async Task<object> OverlapViews()
    {
        if(!TownRoot.Instance||GameSession.FromTitle||!EditorApplication.isPlaying||EditorApplication.isPaused)
            throw new Exception("Same unpaused unsaved town Play required");
        var root=TownRoot.Instance;var player=root.Player;var cam=Camera.main;
        if(!cam||!root.CameraRig||!player)throw new Exception("Town player and camera must be ready.");
        var old=player.Position;var cpos=cam.transform.position;
        var points=new[]{
            new Vector2(807,590),new Vector2(760,452),new Vector2(900,525),
            new Vector2(769,560),new Vector2(771,483),new Vector2(850,526),new Vector2(850,460),
            new Vector2(985,363),new Vector2(985,292),new Vector2(1380,243),new Vector2(1375,265),new Vector2(884,205),new Vector2(884,186)};
        var rows=new List<object>();
        try{
            for(int i=0;i<points.Length;i++){
                if(GameSession.FromTitle||TownRoot.Instance!=root)throw new Exception("Review session changed");
                Physics2D.SyncTransforms();var cols=ReviewContacts(root);
                var requested=TownModularArtV058.FromPixel(points[i].x,points[i].y);
                bool requestedFree=HasPlayerClearance(requested,cols);int frameBefore=Time.frameCount;
                player.Teleport(requested);
                await WaitReviewFrames(root);
                var actual=player.Position;Physics2D.SyncTransforms();
                bool actualFree=HasPlayerClearance(actual,cols);
                root.CameraRig.Snap();Image(cam,"overlap-"+i,Screen.width,Screen.height);
                var group=player.GetComponent<SortingGroup>();
                rows.Add(new{i,pixel=new[]{points[i].x,points[i].y},requestedWorld=new[]{requested.x,requested.y},actualWorld=new[]{actual.x,actual.y},
                    requestedFree,actualFree,displacement=Vector2.Distance(requested,actual),
                    frameBefore,frameCaptured=Time.frameCount,framesAdvanced=Time.frameCount-frameBefore,
                    pose=player.Pose.ToString(),poseTime=player.PoseTime,modal=DungeonUi.Modal,
                    playerOrder=group?(int?)group.sortingOrder:null,ortho=cam.orthographicSize,
                    note=requestedFree?"Synthetic position with normal Unity frame updates":"Blocked synthetic probe; not evidence that a player can stand or walk here"});
            }
        }finally{
            if(root&&TownRoot.Instance==root&&player){
                player.Teleport(old);
                if(EditorApplication.isPlaying&&!EditorApplication.isPaused)await WaitReviewFrames(root);
                if(root.CameraRig)root.CameraRig.Snap();
                if(cam)cam.transform.position=cpos;
            }
        }
        Directory.CreateDirectory(Out);
        File.WriteAllText(Out+"/overlap-views.json",JsonConvert.SerializeObject(new{
            kind="synthetic positioning with at least two normal Unity frames per sample; not native input verification",
            restoredPosition=new[]{(player?player.Position:old).x,(player?player.Position:old).y},rows},Formatting.Indented));return rows;
    }
    public static object Validate()
    {
        var root=TownRoot.Instance;if(!root)throw new Exception("Town not ready");
        var cols=ReviewContacts(root);Physics2D.SyncTransforms();
        bool Free(Vector2 p)=>HasPlayerClearance(p,cols);
        const float step=.25f;var walk=TownLayout.Walk;int width=Mathf.RoundToInt(walk.Width/step)+1,height=Mathf.RoundToInt(walk.Height/step)+1;
        var pass=new bool[width,height];for(int x=0;x<width;x++)for(int y=0;y<height;y++)pass[x,y]=Free(new Vector2(walk.XMin+x*step,walk.YMin+y*step));
        var seen=new bool[width,height];var q=new Queue<Vector2Int>();
        Vector2Int Cell(TownVec p)=>new Vector2Int(Mathf.Clamp(Mathf.RoundToInt((p.X-walk.XMin)/step),0,width-1),Mathf.Clamp(Mathf.RoundToInt((p.Y-walk.YMin)/step),0,height-1));
        var start=Cell(TownLayout.NewPlayStart);
        var exactStart=new Vector2(TownLayout.NewPlayStart.X,TownLayout.NewPlayStart.Y);
        var seedWorld=new Vector2(walk.XMin+start.x*step,walk.YMin+start.y*step);
        bool startFree=walk.Contains(TownLayout.NewPlayStart)&&Free(exactStart);
        bool seedPass=pass[start.x,start.y];
        if(startFree&&seedPass){seen[start.x,start.y]=true;q.Enqueue(start);}
        while(q.Count>0){var p=q.Dequeue();foreach(var d in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right}){var n=p+d;if(n.x<0||n.y<0||n.x>=width||n.y>=height||seen[n.x,n.y]||!pass[n.x,n.y])continue;seen[n.x,n.y]=true;q.Enqueue(n);}}
        var rows=new List<object>();bool all=startFree&&seedPass;
        foreach(var f in TownLayout.Facilities){var p=Cell(f.Pos);bool accessible=false;for(int x=Mathf.Max(0,p.x-6);x<=Mathf.Min(width-1,p.x+6);x++)for(int y=Mathf.Max(0,p.y-6);y<=Mathf.Min(height-1,p.y+6);y++)if(seen[x,y]&&Vector2.Distance(new Vector2(walk.XMin+x*step,walk.YMin+y*step),new Vector2(f.Pos.X,f.Pos.Y))<f.Radius-.1f)accessible=true;all&=accessible;rows.Add(new{id=f.Id,accessible});}
        foreach(var npc in TownNpc.Residents){var v=npc.Position;var c=Cell(new TownVec(v.x,v.y));bool ok=seen[c.x,c.y];all&=ok;rows.Add(new{id=npc.NpcId,accessible=ok});}
        var shadows=root.Player.GetComponentsInChildren<SortingGroup>().Where(g=>g.name.ToLowerInvariant().Contains("shadow")).Select(g=>new{g.name,g.sortAtRoot,g.sortingOrder}).ToArray();
        var result=new{allReachable=all,startFree,seedPass,exactStart=new[]{exactStart.x,exactStart.y},seedWorld=new[]{seedWorld.x,seedWorld.y},seedSnapDistance=Vector2.Distance(exactStart,seedWorld),method="BFS seeded only when exact spawn and its grid cell both have player clearance; actual enabled non-trigger contacts inflated by .405. Synthetic geometry check, not user input.",gridStep=step,colliders=cols.Length,rows,shadows,lamps=root.Props.LitLamps};
        Directory.CreateDirectory(Out);File.WriteAllText(Out+"/runtime-validation.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
    }
}


