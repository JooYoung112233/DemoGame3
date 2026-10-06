using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object=UnityEngine.Object;

// Read-only asset checks; writes only the JSON report. Does not import, save, or edit assets.
// The alpha threshold matches the importer. Fainter discarded edge pixels are reported, not called fully preserved.
public static class VerifyRevisedPeople
{
    const string Prefabs="Assets/Prefabs/Settlement/";
    const string Output="아트/리소스검토/standee-proportions-v3-verification.json";
    const int AlphaThreshold=32;
    const float Epsilon=.0002f;
    static readonly string[] PartyIds={"mechanic","cook","researcher","guard"};
    static readonly float[] PartyScales={1f,1f,1f,1f};
    static readonly string[] NpcIds={"doyun","jun","giho"};
    static readonly float[] NpcHeights={1.72f,1.72f,1.72f};

    [Serializable] public sealed class Report
    {
        public string status,utc,error;
        public string scope="Seven visually selected human body PNGs (four v3, three v2), import metadata, roster/battle/story references and NPC prefab geometry. No scene or gameplay execution.";
        public string limitation="Alpha >=32 must be wholly inside the sprite rect and clear of the source canvas edge. Alpha 1–31 outside the rect is recorded as transparent-edge tolerance. This does not judge faces, hands, pose, anatomy, or whether the generated art depicts a complete person.";
        public int alphaThreshold=AlphaThreshold;
        public List<SourceResult> sources=new List<SourceResult>();
        public List<PartyResult> party=new List<PartyResult>();
        public List<NpcResult> npcs=new List<NpcResult>();
        public List<string> checkedReferences=new List<string>();
        public List<FileResult> pngFiles=new List<FileResult>();
        public bool pngBytesUnchangedDuringVerification;
    }
    [Serializable] public sealed class SourceResult
    {
        public string path;
        public int width,height,spriteCount,metadataSpriteCount;
        public Rect alphaBounds,nonzeroAlphaBounds,spriteRect,visiblePixelsInSprite;
        public int clearPixels,visiblePixels,outsideVisiblePixels,outsideFaintPixels,maxOutsideAlpha;
        public bool visiblePixelsTouchSourceEdge,fullRectMesh,noImportDownscale;
    }
    [Serializable] public sealed class PartyResult
    {
        public string id,spritePath;
        public float expectedScale,configuredScale,resolvedScale;
        public Rect visiblePixels;
    }
    [Serializable] public sealed class NpcResult
    {
        public string id,prefab,spritePath;
        public float expectedHeight,actualHeight,footY,centerX,scaleX,scaleY;
        public bool commonStyle,noLegacyPaperRemoval,noSpriteMask;
    }
    [Serializable] public sealed class FileResult
    {
        public string path,sha256Before,sha256After;
        public bool existsBefore,existsAfter,unchanged;
    }

    static string Absolute(string relative)=>Path.Combine(Directory.GetParent(Application.dataPath).FullName,relative);
    sealed class RectReportConverter:Newtonsoft.Json.JsonConverter
    {
        public override bool CanConvert(Type type)=>type==typeof(Rect);
        public override void WriteJson(Newtonsoft.Json.JsonWriter writer,object value,Newtonsoft.Json.JsonSerializer serializer)
        {var r=(Rect)value;serializer.Serialize(writer,new{x=r.x,y=r.y,width=r.width,height=r.height});}
        public override object ReadJson(Newtonsoft.Json.JsonReader reader,Type type,object existing,Newtonsoft.Json.JsonSerializer serializer)=>throw new NotSupportedException();
        public override bool CanRead=>false;
    }
    static string BodyPath(string group,string id)=>"Assets/Art/Tokens/"+group+"-"+id+"-body-"+(id=="guard"||id=="researcher"||id=="giho"?"v2":"v3")+".png";
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static bool Near(float a,float b)=>Mathf.Abs(a-b)<=Epsilon;
    static bool SameRect(Rect a,Rect b)=>Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);
    static string Hash(string path)
    {
        using(var hash=SHA256.Create())using(var stream=File.OpenRead(Absolute(path)))
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
    }

    public static string Run()
    {
        Require(!EditorApplication.isPlaying,"Stop Play Mode before checking serialized asset geometry.");
        var report=new Report{status="RUNNING",utc=DateTime.UtcNow.ToString("O")};
        Exception failure=null;
        try
        {
            var paths=new[]{"Assets/Art/Settlement/standee-scout.png","Assets/Art/Settlement/standee-medic.png"}
                .Concat(PartyIds.Select(id=>"Assets/Art/Tokens/party-"+id+"-body.png"))
                .Concat(NpcIds.Select(id=>"Assets/Art/Tokens/npc-"+id+"-body.png"))
                .Concat(PartyIds.Select(id=>BodyPath("party",id))).Concat(NpcIds.Select(id=>BodyPath("npc",id)));
            foreach(var path in paths)
            {
                bool exists=File.Exists(Absolute(path));
                report.pngFiles.Add(new FileResult{path=path,existsBefore=exists,sha256Before=exists?Hash(path):null});
            }
            Require(report.pngFiles.All(f=>f.existsBefore),"A preserved original or revised PNG is missing: "+string.Join(", ",report.pngFiles.Where(f=>!f.existsBefore).Select(f=>f.path)));
            var sprites=new Dictionary<string,Sprite>();
            foreach(var path in PartyIds.Select(id=>BodyPath("party",id)).Concat(NpcIds.Select(id=>BodyPath("npc",id))))
            {
                var entry=new SourceResult{path=path};report.sources.Add(entry);
                sprites.Add(path,ReadSource(entry));
            }
            CheckParty(report,sprites);
            CheckNpcs(report,sprites);
            CheckStory(report,sprites[BodyPath("npc","doyun")]);
            report.status="PASS";
        }
        catch(Exception exception){failure=exception;report.status="FAIL";report.error=exception.ToString();}
        finally
        {
            foreach(var file in report.pngFiles)
            {
                file.existsAfter=File.Exists(Absolute(file.path));
                file.sha256After=file.existsAfter?Hash(file.path):null;
                file.unchanged=file.existsBefore&&file.existsAfter&&file.sha256Before==file.sha256After;
            }
            report.pngBytesUnchangedDuringVerification=report.pngFiles.Count==16&&report.pngFiles.All(f=>f.unchanged);
            if(!report.pngBytesUnchangedDuringVerification&&failure==null)
            {
                failure=new InvalidOperationException("A PNG disappeared or changed while verification ran.");
                report.status="FAIL";report.error=failure.ToString();
            }
            string output=Absolute(Output);Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output,Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented,new RectReportConverter()));
        }
        if(failure!=null)throw new InvalidOperationException("Revised people verification failed; report: "+Output,failure);
        return "PASS: 7 selected source silhouettes inside sprite metadata; 4 revised party scales at 1; original 2 scales unchanged; 3 NPC heights at 1.72/feet/centers/uniform XY/common style; battle bounds and Doyun story references; 16 PNG byte hashes unchanged during verification. Report: "+Output+". No anatomy or visual-approval claim.";
    }

    static Sprite ReadSource(SourceResult result)
    {
        var importer=AssetImporter.GetAtPath(result.path) as TextureImporter;
        Require(importer,"Import the revised PNG first: "+result.path);
        Require(importer.textureType==TextureImporterType.Sprite&&importer.spriteImportMode==SpriteImportMode.Multiple,"Expected one named sub-sprite: "+result.path);
        var sprites=AssetDatabase.LoadAllAssetsAtPath(result.path).OfType<Sprite>().ToArray();
        result.spriteCount=sprites.Length;Require(sprites.Length==1,"Expected exactly one sprite: "+result.path);
        var sprite=sprites[0];result.spriteRect=sprite.rect;
        var factories=new SpriteDataProviderFactories();factories.Init();
        var provider=factories.GetSpriteEditorDataProviderFromObject(importer);
        Require(provider!=null,"Sprite metadata provider missing: "+result.path);provider.InitSpriteEditorDataProvider();
        var rects=provider.GetSpriteRects();result.metadataSpriteCount=rects.Length;
        Require(rects.Length==1&&SameRect(rects[0].rect,sprite.rect),"Serialized sprite rect differs from loaded sprite: "+result.path);
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
        result.fullRectMesh=settings.spriteMeshType==SpriteMeshType.FullRect&&sprite.vertices.Length==4;
        Require(result.fullRectMesh,"Body needs the shared shader's FullRect mesh: "+result.path);
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            Require(ImageConversion.LoadImage(texture,File.ReadAllBytes(Absolute(result.path)),false),"Could not read original PNG pixels: "+result.path);
            result.width=texture.width;result.height=texture.height;
            result.noImportDownscale=sprite.texture.width==texture.width&&sprite.texture.height==texture.height;
            Require(result.noImportDownscale,"Imported body was downscaled: "+result.path);
            var pixels=texture.GetPixels32();int x0=texture.width,y0=texture.height,x1=-1,y1=-1;
            int ax0=texture.width,ay0=texture.height,ax1=-1,ay1=-1;
            for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)
            {
                int alpha=pixels[y*texture.width+x].a;
                if(alpha==0){result.clearPixels++;continue;}
                ax0=Math.Min(ax0,x);ay0=Math.Min(ay0,y);ax1=Math.Max(ax1,x);ay1=Math.Max(ay1,y);
                bool outside=!sprite.rect.Contains(new Vector2(x+.5f,y+.5f));
                if(outside)
                {
                    result.maxOutsideAlpha=Math.Max(result.maxOutsideAlpha,alpha);
                    if(alpha<AlphaThreshold)result.outsideFaintPixels++;else result.outsideVisiblePixels++;
                }
                if(alpha<AlphaThreshold)continue;
                result.visiblePixels++;x0=Math.Min(x0,x);y0=Math.Min(y0,y);x1=Math.Max(x1,x);y1=Math.Max(y1,y);
            }
            Require(result.visiblePixels>0&&result.clearPixels>0,"PNG needs visible ink and real alpha-zero pixels: "+result.path);
            result.alphaBounds=Rect.MinMaxRect(x0,y0,x1+1,y1+1);
            result.nonzeroAlphaBounds=Rect.MinMaxRect(ax0,ay0,ax1+1,ay1+1);
            result.visiblePixelsTouchSourceEdge=x0==0||y0==0||x1==texture.width-1||y1==texture.height-1;
            result.visiblePixelsInSprite=new Rect(x0-sprite.rect.x,y0-sprite.rect.y,x1-x0+1,y1-y0+1);
            Require(result.outsideVisiblePixels==0,"Sprite metadata cuts visible source pixels: "+result.path);
            Require(!result.visiblePixelsTouchSourceEdge,"Visible ink touches the original PNG edge; check possible source clipping: "+result.path);
            Require(sprite.rect.xMin>=0&&sprite.rect.yMin>=0&&sprite.rect.xMax<=texture.width&&sprite.rect.yMax<=texture.height,"Sprite metadata extends outside its source: "+result.path);
        }
        finally{Object.DestroyImmediate(texture);}
        return sprite;
    }

    static void CheckParty(Report report,Dictionary<string,Sprite> sprites)
    {
        var roster=AssetDatabase.LoadAssetAtPath<PartyRoster>("Assets/Data/PartyRoster.asset");
        Require(roster&&roster.Candidates!=null,"Party roster missing.");
        foreach(string id in PartyIds.Concat(new[]{"scout","medic"}))
        {
            bool revised=Array.IndexOf(PartyIds,id)>=0;
            string path=revised?BodyPath("party",id):"Assets/Art/Settlement/standee-"+id+".png";
            var sprite=revised?sprites[path]:AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var matches=roster.Candidates.Where(c=>c!=null&&c.Id==id).ToArray();
            Require(matches.Length==1,"Missing or duplicate party ID: "+id);var candidate=matches[0];
            float expected=revised?PartyScales[Array.IndexOf(PartyIds,id)]:1;
            report.party.Add(new PartyResult{id=id,spritePath=AssetDatabase.GetAssetPath(candidate.Body),expectedScale=expected,configuredScale=candidate.BodyScale,resolvedScale=roster.BodyScaleFor(candidate.Body),visiblePixels=candidate.BodyVisiblePixels});
            Require(sprite&&candidate.Body==sprite,"Party body reference is not the expected source: "+id);
            Require(Near(candidate.BodyScale,expected)&&Near(roster.BodyScaleFor(sprite),expected),"Incorrect party display scale: "+id);
            if(revised)Require(SameRect(candidate.BodyVisiblePixels,report.sources.Single(s=>s.path==path).visiblePixelsInSprite),"Party alpha cache differs from full source pixels: "+id);
        }
        foreach(string path in new[]{Prefabs+"ExpeditionBattlePanel.prefab",Prefabs+"SettlementScreen.prefab"})
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(prefab,"Missing prefab: "+path);
            var battles=prefab.GetComponentsInChildren<ExpeditionBattlePanel>(true);Require(battles.Length>0,"No battle component in "+path);
            foreach(var battle in battles)foreach(var id in PartyIds)
            {
                var sprite=sprites[BodyPath("party",id)];var expected=report.sources.Single(s=>s.path==BodyPath("party",id)).visiblePixelsInSprite;
                var matches=(battle.VisibleBounds??Array.Empty<ExpeditionBattlePanel.PawnBounds>()).Where(b=>b!=null&&b.Sprite==sprite).ToArray();
                Require(matches.Length==1&&SameRect(matches[0].Pixels,expected),"Battle v3 body bounds missing or stale: "+path+" / "+id);
            }
            report.checkedReferences.Add(path+": revised party sprite bounds");
        }
    }

    static void CheckNpcs(Report report,Dictionary<string,Sprite> sprites)
    {
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PaperStandee.mat");Require(material,"Shared standee material missing.");
        for(int i=0;i<NpcIds.Length;i++)
        {
            string id=NpcIds[i],path=Prefabs+"Npc_"+id+".prefab",source=BodyPath("npc",id);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(prefab,"Missing NPC prefab: "+path);
            var bodyTransform=prefab.transform.Find("Body");Require(bodyTransform,"NPC Body missing: "+path);
            var body=bodyTransform.GetComponent<SpriteRenderer>();Require(body&&body.sprite==sprites[source],"NPC body does not reference v3: "+path);
            var sprite=body.sprite;var pixels=report.sources.Single(s=>s.path==source).visiblePixelsInSprite;
            var matrix=prefab.transform.worldToLocalMatrix*body.transform.localToWorldMatrix;
            float x=(pixels.center.x-sprite.pivot.x)*(body.flipX?-1:1)/sprite.pixelsPerUnit;
            float bottom=(body.flipY?sprite.pivot.y-pixels.yMax:pixels.y-sprite.pivot.y)/sprite.pixelsPerUnit;
            float top=(body.flipY?sprite.pivot.y-pixels.y:pixels.yMax-sprite.pivot.y)/sprite.pixelsPerUnit;
            var foot=matrix.MultiplyPoint3x4(new Vector3(x,bottom,0));var head=matrix.MultiplyPoint3x4(new Vector3(x,top,0));
            var style=body.GetComponent<PaperStandeeStyle>();
            var result=new NpcResult{id=id,prefab=path,spritePath=AssetDatabase.GetAssetPath(sprite),expectedHeight=NpcHeights[i],actualHeight=head.y-foot.y,footY=foot.y,centerX=(head.x+foot.x)*.5f,scaleX=body.transform.localScale.x,scaleY=body.transform.localScale.y,
                commonStyle=style&&style.enabled&&style.StyleMaterial==material,
                noLegacyPaperRemoval=style&&!(style.LegacyPaperSprites??Array.Empty<Sprite>()).Contains(sprite),noSpriteMask=body.maskInteraction==SpriteMaskInteraction.None};
            report.npcs.Add(result);
            Require(Near(result.actualHeight,result.expectedHeight)&&Near(result.footY,.035f)&&Near(result.centerX,0),"NPC height, feet or center differ: "+id);
            Require(result.scaleX>0&&result.scaleY>0&&Near(result.scaleX,result.scaleY),"NPC body is stretched or inverted: "+id);
            Require(result.commonStyle&&result.noLegacyPaperRemoval&&result.noSpriteMask,"NPC shared style/mask configuration differs: "+id);
        }
    }

    static void CheckStory(Report report,Sprite doyUn)
    {
        var npc=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"Npc_doyun.prefab");
        foreach(string path in new[]{Prefabs+"ExpeditionArrivalPanel.prefab",Prefabs+"SettlementScreen.prefab"})
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(prefab,"Missing prefab: "+path);
            var stories=prefab.GetComponentsInChildren<ExpeditionNpcStory>(true);Require(stories.Length>0,"Doyun story missing in "+path);
            foreach(var story in stories)
            {
                Require(story.NpcPrefab==npc,"Story NPC prefab link differs: "+path);
                Require(story.Portrait&&story.Portrait.sprite==doyUn&&story.DialoguePortrait&&story.DialoguePortrait.sprite==doyUn,"Story investigation/dialogue portrait still uses an earlier body: "+path);
            }
            report.checkedReferences.Add(path+": Doyun world prefab and both portrait references");
        }
    }
}
