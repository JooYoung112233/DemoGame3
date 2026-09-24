using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Sprites;
using Object = UnityEngine.Object;

/// <summary>
/// Native GPU regression for the flip-dependent standee clipping bug. Run through run_script.
/// Uses the connected 6 party / 12 creature / 3 NPC / 1 fallback bodies, never imports or edits art.
/// A preview scene owns every temporary object. The user's scenes and play state remain unchanged.
/// </summary>
public static class VerifyStandeeFacing
{
    const string Output = "아트/리소스검토/";
    const string MaterialPath = "Assets/Settings/PaperStandee.mat";
    const int Width = 512, Height = 256, Layer = 31;
    const float BodyHeight = 1.72f;
    const string ReportPath = Output + "standee-facing-regression.json";
    const string SamplePath = Output + "standee-facing-regression-sample.png";

    sealed class Entry { public string Id; public Sprite Body; public Rect Pixels; public bool Legacy; }
    [Serializable] sealed class Report
    {
        public string utc, result, sample, methodology;
        public int bodies, cases;
        public bool sourceFilesUnchanged, activeSceneUnchanged, playStateUnchanged;
        public List<Case> checks = new List<Case>();
        public List<string> failures = new List<string>();
    }
    [Serializable] sealed class Case
    {
        public string id, source, projection, facing;
        public float requestedBodyPixels, inkMirrorIoU, paperMirrorIoU, inkMeanAlphaError;
        public int inkBoundsError, paperBoundsError, referenceCorePixels, missingReferenceCorePixels;
        public Box inkBounds, paperBounds;
        public bool verticesInFrame, passed;
    }
    [Serializable] struct Box
    {
        public int x, y, width, height;
        public Box(int x, int y, int width, int height) { this.x=x; this.y=y; this.width=width; this.height=height; }
    }
    struct Comparison { public float IoU, MeanError; public int BoundsError; }

    public static string Run()
    {
        Require(Directory.Exists(Output), "Existing art review directory is missing.");
        var entries = Entries();
        Require(entries.Count == 22, "Expected 22 connected bodies, found " + entries.Count + ".");
        var sourcePaths = entries.Select(e => AssetDatabase.GetAssetPath(e.Body)).Distinct()
            .SelectMany(p => new[] { p, p + ".meta" }).ToArray();
        var hashes = sourcePaths.ToDictionary(p => p, Hash);
        var activeScene = SceneManager.GetActiveScene();
        bool playing = EditorApplication.isPlaying;
        var report = new Report {
            utc = DateTime.UtcNow.ToString("O"), bodies = entries.Count, sample = Path.GetFullPath(SamplePath),
            methodology = "Native URP renders: orthographic 180px and perspective 120px body height; four SpriteRenderer flipX/Y states. " +
                "Separate ink and complete-paper alpha are compared with the unflipped render mirrored around the canvas center. " +
                "Solid source cores from the built-in URP Sprite-Unlit shader must remain covered (1px raster tolerance). " +
                "Legacy scout/medic reference cores exclude their baked light paper. No bases, shadows or gameplay scripts enter pixel measurements. " +
                "This tests renderer fidelity, not approval of the anatomical proportions in an original drawing."
        };
        try
        {
            using (var stage = new Stage(Width, Height))
            {
                for (int projection = 0; projection < 2; projection++)
                {
                    stage.Projection(projection == 1);
                    foreach (var entry in entries)
                    {
                        stage.SetBody(entry);
                        stage.Facing(false, false, Vector2.zero);
                        Color32[] reference = stage.Render(true, false);
                        Color32[] normalInk = null, normalPaper = null;
                        for (int facing = 0; facing < 4; facing++)
                        {
                            bool flipX = (facing & 1) != 0, flipY = (facing & 2) != 0;
                            stage.Facing(flipX, flipY, Vector2.zero);
                            var ink = stage.Render(false, true);
                            var paper = stage.Render(false, false);
                            if (facing == 0) { normalInk = ink; normalPaper = paper; }
                            var inkComparison = Compare(normalInk, ink, flipX, flipY);
                            var paperComparison = Compare(normalPaper, paper, flipX, flipY);
                            int core, missing;
                            CoreCoverage(reference, ink, entry.Legacy, flipX, flipY, out core, out missing);
                            var item = new Case {
                                id=entry.Id, source=AssetDatabase.GetAssetPath(entry.Body),
                                projection=projection == 0 ? "orthographic" : "perspective-distance-15",
                                facing=(flipX ? "X" : "-") + (flipY ? "Y" : "-"),
                                requestedBodyPixels=projection == 0 ? 180 : 120,
                                inkMirrorIoU=inkComparison.IoU, paperMirrorIoU=paperComparison.IoU,
                                inkMeanAlphaError=inkComparison.MeanError,
                                inkBoundsError=inkComparison.BoundsError, paperBoundsError=paperComparison.BoundsError,
                                inkBounds=Bounds(ink), paperBounds=Bounds(paper),
                                referenceCorePixels=core, missingReferenceCorePixels=missing,
                                verticesInFrame=stage.VerticesInFrame()
                            };
                            // A 1px raster edge can differ with GPU interpolation. Multi-pixel cut edges cannot pass.
                            item.passed = item.verticesInFrame && core > 20 &&
                                missing <= Mathf.Max(2, Mathf.FloorToInt(core * .0005f)) &&
                                item.inkBoundsError <= 1 && item.paperBoundsError <= 1 &&
                                item.inkMirrorIoU >= .985f && item.paperMirrorIoU >= .985f && item.inkMeanAlphaError <= .015f;
                            report.checks.Add(item);
                            if (!item.passed) report.failures.Add(entry.Id + " / " + item.projection + " / " + item.facing +
                                ": bounds " + item.inkBoundsError + "/" + item.paperBoundsError +
                                ", IoU " + item.inkMirrorIoU.ToString("F4") + "/" + item.paperMirrorIoU.ToString("F4") +
                                ", missing source core " + missing + "/" + core + ", vertices in frame " + item.verticesInFrame);
                        }
                    }
                }
            }
            Sample(entries);
        }
        catch (Exception error) { report.failures.Add(error.ToString()); }
        finally
        {
            report.cases = report.checks.Count;
            report.sourceFilesUnchanged = hashes.All(p => Hash(p.Key) == p.Value);
            report.activeSceneUnchanged = SceneManager.GetActiveScene() == activeScene;
            report.playStateUnchanged = EditorApplication.isPlaying == playing;
            if (!report.sourceFilesUnchanged) report.failures.Add("A source PNG or importer metadata changed during the run.");
            if (!report.activeSceneUnchanged || !report.playStateUnchanged) report.failures.Add("The active scene or play state changed during the run.");
            if (report.cases != 176) report.failures.Add("Incomplete run: expected 22 x 2 x 4 = 176 cases.");
            report.result = report.failures.Count == 0 ? "PASS" : "FAIL";
            File.WriteAllText(ReportPath, Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        }
        Require(report.result == "PASS", "Standee facing regression failed. " + Path.GetFullPath(ReportPath) + "\n" + string.Join("\n", report.failures.Take(12)));
        return "PASS: 22 connected bodies, 176 native flip/projection cases; source cores, mirrored silhouette bounds and paper edges preserved.\n" +
            Path.GetFullPath(ReportPath) + "\n" + Path.GetFullPath(SamplePath);
    }

    static List<Entry> Entries()
    {
        var result = new List<Entry>();
        var party = AssetDatabase.LoadAssetAtPath<PartyRoster>("Assets/Data/PartyRoster.asset");
        var creatures = AssetDatabase.LoadAssetAtPath<BattleCreatureRoster>("Assets/Data/BattleCreatures.asset");
        Require(party && creatures, "Connected party/creature rosters are missing.");
        foreach (var candidate in party.Candidates)
        {
            Require(candidate != null && candidate.Body, "Missing party Body.");
            result.Add(new Entry { Id="party-"+candidate.Id, Body=candidate.Body, Pixels=candidate.BodyVisiblePixels,
                Legacy=candidate.Id == "scout" || candidate.Id == "medic" });
        }
        foreach (var creature in creatures.Creatures)
        {
            Require(creature != null && creature.Body, "Missing creature Body.");
            result.Add(new Entry { Id="creature-"+creature.Id, Body=creature.Body, Pixels=creature.Visible });
        }
        foreach (string id in new[] { "doyun", "jun", "giho" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/Npc_"+id+".prefab");
            var body = prefab ? prefab.transform.Find("Body").GetComponent<SpriteRenderer>() : null;
            Require(body && body.sprite, "NPC Body is missing: " + id);
            result.Add(new Entry { Id="npc-"+id, Body=body.sprite });
        }
        var battlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/ExpeditionBattlePanel.prefab");
        var battle = battlePrefab ? battlePrefab.GetComponent<ExpeditionBattlePanel>() : null;
        if (!battle && battlePrefab) battle = battlePrefab.GetComponentInChildren<ExpeditionBattlePanel>(true);
        Require(battle && battle.EnemyBody, "Connected fallback EnemyBody is missing.");
        result.Add(new Entry { Id="infected-fallback", Body=battle.EnemyBody });
        foreach (var entry in result)
        {
            Require(entry.Body.vertices.Length == 4, "Not a FullRect sprite: " + entry.Id);
            if (entry.Pixels.height <= 0) entry.Pixels = SourceBounds(entry.Body);
        }
        return result;
    }

    static Rect SourceBounds(Sprite sprite)
    {
        var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Require(ImageConversion.LoadImage(image, File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)), false), "Cannot inspect original source alpha.");
            float sx=(float)image.width/sprite.texture.width, sy=(float)image.height/sprite.texture.height;
            int x0=Mathf.RoundToInt(sprite.rect.xMin*sx), y0=Mathf.RoundToInt(sprite.rect.yMin*sy);
            int x1=Mathf.RoundToInt(sprite.rect.xMax*sx), y1=Mathf.RoundToInt(sprite.rect.yMax*sy);
            var pixels=image.GetPixels32(); int left=x1, right=x0-1, bottom=y1, top=y0-1;
            for (int y=y0;y<y1;y++) for (int x=x0;x<x1;x++)
            {
                if (pixels[y*image.width+x].a < 128) continue;
                left=Mathf.Min(left,x);right=Mathf.Max(right,x);bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);
            }
            Require(right>=left && top>=bottom, "Empty source alpha: " + sprite.name);
            return new Rect((left-x0)/sx,(bottom-y0)/sy,(right-left+1)/sx,(top-bottom+1)/sy);
        }
        finally { Object.DestroyImmediate(image); }
    }

    static Comparison Compare(Color32[] normal, Color32[] actual, bool flipX, bool flipY)
    {
        int intersection=0, union=0; double error=0;
        for (int y=0;y<Height;y++) for (int x=0;x<Width;x++)
        {
            int a=normal[(flipY?Height-1-y:y)*Width+(flipX?Width-1-x:x)].a;
            int b=actual[y*Width+x].a;
            if (a>=128 || b>=128) { union++; error+=Math.Abs(a-b)/255.0; }
            if (a>=128 && b>=128) intersection++;
        }
        var expected=Bounds(normal);
        if (flipX) expected.x=Width-expected.x-expected.width;
        if (flipY) expected.y=Height-expected.y-expected.height;
        var observed=Bounds(actual);
        int d=Mathf.Max(Mathf.Abs(expected.x-observed.x),Mathf.Abs(expected.y-observed.y),
            Mathf.Abs(expected.width-observed.width),Mathf.Abs(expected.height-observed.height));
        return new Comparison { IoU=union>0?(float)intersection/union:0, MeanError=union>0?(float)(error/union):1, BoundsError=d };
    }

    static Box Bounds(Color32[] pixels)
    {
        int left=Width,right=-1,bottom=Height,top=-1;
        for (int y=0;y<Height;y++) for (int x=0;x<Width;x++) if (pixels[y*Width+x].a>=128)
        { left=Mathf.Min(left,x);right=Mathf.Max(right,x);bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y); }
        return right<left ? new Box(0,0,0,0) : new Box(left,bottom,right-left+1,top-bottom+1);
    }

    static void CoreCoverage(Color32[] reference,Color32[] ink,bool legacy,bool flipX,bool flipY,out int core,out int missing)
    {
        core=missing=0;
        for (int y=1;y<Height-1;y++) for (int x=1;x<Width-1;x++)
        {
            Color32 source=reference[y*Width+x];
            if (source.a<245 || (legacy && Mathf.Max(source.r,source.g,source.b)>80)) continue;
            core++;
            int tx=flipX?Width-1-x:x, ty=flipY?Height-1-y:y, best=0;
            for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++) best=Mathf.Max(best,ink[(ty+dy)*Width+tx+dx].a);
            if(best<128)missing++;
        }
    }

    static void Sample(List<Entry> entries)
    {
        string[] ids={"party-cook","party-mechanic","party-guard","party-researcher"};
        using(var stage=new Stage(1280,896))
        {
            stage.Camera.orthographic=true;stage.Camera.orthographicSize=4.48f;
            stage.Camera.backgroundColor=new Color(.72f,.69f,.60f,1);
            for(int row=0;row<4;row++) for(int facing=0;facing<4;facing++)
            {
                var entry=entries.Single(e=>e.Id==ids[row]);
                stage.AddSample(entry,(facing&1)!=0,(facing&2)!=0,new Vector2(-4.8f+facing*3.2f,3.36f-row*2.24f));
            }
            stage.SaveNative(SamplePath);
        }
    }

    sealed class Stage : IDisposable
    {
        readonly Scene scene;
        readonly int width,height;
        readonly Material paper,plain;
        readonly RenderTexture target;
        readonly Texture2D readback;
        SpriteRenderer body;
        PaperStandeeStyle style;
        Entry entry;
        readonly Color rim;
        public readonly Camera Camera;

        public Stage(int width,int height)
        {
            this.width=width;this.height=height;
            var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var plainShader=Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            Require(material && material.shader.isSupported && plainShader && plainShader.isSupported,"Standee/reference shaders are unavailable.");
            paper=new Material(material){hideFlags=HideFlags.HideAndDontSave};
            plain=new Material(plainShader){hideFlags=HideFlags.HideAndDontSave};
            rim=paper.GetColor("_RimColor");
            scene=EditorSceneManager.NewPreviewScene();
            Camera=Make("Facing regression camera").AddComponent<Camera>();
            Camera.enabled=false;Camera.cameraType=CameraType.Preview;Camera.scene=scene;Camera.cullingMask=1<<Layer;
            Camera.aspect=(float)width/height;Camera.nearClipPlane=.1f;Camera.farClipPlane=50;
            Camera.clearFlags=CameraClearFlags.SolidColor;Camera.backgroundColor=Color.clear;
            Camera.allowHDR=false;Camera.allowMSAA=false;Camera.useOcclusionCulling=false;
            var extra=Camera.GetUniversalAdditionalCameraData();extra.SetRenderer(0);extra.renderPostProcessing=false;
            extra.requiresColorOption=CameraOverrideOption.Off;extra.requiresDepthOption=CameraOverrideOption.Off;
            target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=1};
            target.Create();Camera.targetTexture=target;
            readback=new Texture2D(width,height,TextureFormat.RGBA32,false,false);
            Projection(false);
        }
        GameObject Make(string name)
        {
            var go=new GameObject(name){hideFlags=HideFlags.HideAndDontSave,layer=Layer};
            SceneManager.MoveGameObjectToScene(go,scene);return go;
        }
        public void Projection(bool perspective)
        {
            float halfHeight=BodyHeight*Height/(180f*2);
            Camera.orthographic=!perspective;Camera.orthographicSize=halfHeight;
            Camera.fieldOfView=2*Mathf.Atan(halfHeight/10)*Mathf.Rad2Deg;
            Camera.transform.position=new Vector3(0,0,perspective?-15:-10);
        }
        public void SetBody(Entry next)
        {
            if(body)Object.DestroyImmediate(body.gameObject);
            entry=next;body=Make(next.Id).AddComponent<SpriteRenderer>();
            body.sprite=next.Body;body.color=Color.white;body.drawMode=SpriteDrawMode.Simple;
            body.maskInteraction=SpriteMaskInteraction.None;
            body.transform.localScale=Vector3.one*(BodyHeight*next.Body.pixelsPerUnit/next.Pixels.height);
            style=body.gameObject.AddComponent<PaperStandeeStyle>();
            style.Configure(paper,next.Legacy?new[]{next.Body}:Array.Empty<Sprite>());
        }
        public void Facing(bool flipX,bool flipY,Vector2 center)
        {
            body.flipX=flipX;body.flipY=flipY;
            var localCenter=body.sprite.bounds.center;
            body.transform.position=new Vector3(center.x-localCenter.x*body.transform.localScale.x*(flipX?-1:1),
                center.y-localCenter.y*body.transform.localScale.y*(flipY?-1:1),0);
            style.ApplyNow();
            var properties=new MaterialPropertyBlock();body.GetPropertyBlock(properties);
            Require(properties.GetVector("_UvRect")==DataUtility.GetOuterUV(entry.Body),"Stale UV rect after replacing or flipping " + entry.Id);
        }
        public bool VerticesInFrame()
        {
            // Transparent source padding may extend off-camera. Require the visible alpha bounds
            // plus the same 6px paper-edge allowance to fit, rather than the empty FullRect canvas.
            var pixels=entry.Pixels;
            var corners=new[] { new Vector2(pixels.xMin,pixels.yMin),new Vector2(pixels.xMax,pixels.yMin),
                new Vector2(pixels.xMax,pixels.yMax),new Vector2(pixels.xMin,pixels.yMax) };
            foreach(var corner in corners)
            {
                var vertex=(corner-body.sprite.pivot)/body.sprite.pixelsPerUnit;
                var p=new Vector3(vertex.x*(body.flipX?-1:1),vertex.y*(body.flipY?-1:1),0);
                var screen=Camera.WorldToViewportPoint(body.transform.TransformPoint(p));
                if(screen.x*width<6 || screen.x*width>width-6 || screen.y*height<6 || screen.y*height>height-6)return false;
            }
            return true;
        }
        public Color32[] Render(bool reference,bool inkOnly)
        {
            paper.SetColor("_RimColor",inkOnly?new Color(rim.r,rim.g,rim.b,0):rim);
            if(reference)body.sharedMaterial=plain;else style.ApplyNow();
            ReadNative();return readback.GetPixels32();
        }
        void ReadNative()
        {
            var previous=RenderTexture.active;
            try
            {
                var request=new RenderPipeline.StandardRequest{destination=target};
                if(RenderPipeline.SupportsRenderRequest(Camera,request))RenderPipeline.SubmitRenderRequest(Camera,request);else Camera.Render();
                RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,width,height),0,0);readback.Apply(false);
            }
            finally{RenderTexture.active=previous;}
        }
        public void AddSample(Entry sample,bool flipX,bool flipY,Vector2 center)
        {
            // Leave previous renderer alive: this is one native camera contact sheet, never pixel compositing.
            body=null;SetBody(sample);Facing(flipX,flipY,center);
            var label=Make(sample.Id+" label");var mesh=label.AddComponent<TextMesh>();
            mesh.text=sample.Id.Replace("party-","")+"  "+(flipX?"X":"-")+(flipY?"Y":"-");
            mesh.fontSize=32;mesh.characterSize=.065f;mesh.anchor=TextAnchor.MiddleCenter;mesh.color=new Color(.06f,.07f,.06f);
            label.transform.position=new Vector3(center.x,center.y-1.03f,-1);
            var renderer=label.GetComponent<MeshRenderer>();renderer.sortingOrder=10;
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.font=font;renderer.sharedMaterial=font.material;
        }
        public void SaveNative(string path)
        {
            paper.SetColor("_RimColor",rim);ReadNative();File.WriteAllBytes(path,ImageConversion.EncodeToPNG(readback));
        }
        public void Dispose()
        {
            if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);
            Object.DestroyImmediate(readback);target.Release();Object.DestroyImmediate(target);
            Object.DestroyImmediate(paper);Object.DestroyImmediate(plain);
        }
    }

    static string Hash(string path)
    {
        using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","");
    }
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
