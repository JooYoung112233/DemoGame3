using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Native Unity proof, using the actual FieldPawn prefab, materials, body assets and arcade artwork.
/// The preview scene never becomes the active scene and no existing scene/game object is modified.
/// Run is safe with a stopped or playing editor; it does not change play state or save files.
/// </summary>
public static class RenderStandeeReview
{
    const string Output = "아트/리소스검토/";
    const string PawnPath = "Assets/Prefabs/Settlement/FieldPawn.prefab";
    const string FontPath = "Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf";
    const int Width = 1920, Height = 1080, Layer = 31;
    static readonly Color AllyBase = new Color(.64f, .79f, .74f), EnemyBase = new Color(.83f, .43f, .34f);

    sealed class Entry
    {
        public string Id, Name;
        public Sprite Body;
        public Rect Pixels;
        public float BodyHeight = 1.72f, Hover;
        public bool Enemy;
    }

    public static string Run()
    {
        // Validate all requested art before producing partial captures.
        var party = PartyEntries();
        var creatures = CreatureEntries();
        var visitors = VisitorEntries();
        Require(creatures.Count == 12, "Expected the twelve current creature definitions.");
        Require(party.Count == 6, "Expected six individual party bodies.");
        Require(Directory.Exists(Output), "The existing art review directory is missing.");
        var activeBefore = SceneManager.GetActiveScene();
        bool playingBefore = EditorApplication.isPlaying;
        var files = new List<string>();
        using (var stage = new Stage())
        {
            stage.Header("공통 말 리소스 · 대원과 NPC", "실제 Unity 렌더 · 동일한 검정 몸체 / 종이 테두리 / 공통 받침대");
            for (int i = 0; i < party.Count; i++) stage.AddPawn(party[i], new Vector2(-7.5f + i * 3f, -1.05f));
            for (int i = 0; i < visitors.Count; i++) stage.AddPawn(visitors[i], new Vector2(-6f + i * 4f, -4.02f));
            files.Add(stage.Capture(Output + "standee-common-party-review.png"));
        }
        for (int page = 0; page < 2; page++)
        {
            using (var stage = new Stage())
            {
                stage.Header("공통 말 리소스 · 크리쳐 " + (page + 1) + "/2", "실제 전투 몸체 높이와 부유 높이 유지 · 받침대 구조와 테두리는 대원과 동일");
                for (int i = 0; i < 6; i++)
                    stage.AddPawn(creatures[page * 6 + i], new Vector2(-6f + (i % 3) * 6f, i < 3 ? -.92f : -4.02f));
                files.Add(stage.Capture(Output + (page == 0 ? "standee-common-creatures-review.png" : "standee-common-creatures-review-2.png")));
            }
        }
        Require(SceneManager.GetActiveScene() == activeBefore && EditorApplication.isPlaying == playingBefore, "The editor scene or play state unexpectedly changed.");
        return string.Join("\n", files);
    }

    public static string PeopleV3()
    {
        var party=PartyEntries();var visitors=VisitorEntries().Where(e=>!e.Enemy).ToList();
        using(var stage=new Stage())
        {
            stage.Header("대원과 NPC · 전신 비율 수정", "기존 두 대원 기준의 표시 높이 · 단순한 바지와 신발 · 전신 보존");
            for(int i=0;i<party.Count;i++)stage.AddPawn(party[i],new Vector2(-7.5f+i*3f,-.70f));
            for(int i=0;i<visitors.Count;i++)stage.AddPawn(visitors[i],new Vector2(-6f+i*6f,-4.02f));
            return stage.Capture(Output+"standee-proportions-v3-review.png");
        }
    }

    public static string FacingComparison()
    {
        var entries=PartyEntries();
        using(var stage=new Stage())
        {
            for(int i=0;i<entries.Count;i++)
            {
                stage.AddPawn(entries[i],new Vector2(-7.5f+i*3f,-.7f),false);
                var flipped=stage.AddPawn(entries[i],new Vector2(-7.5f+i*3f,-3.7f),false);
                var body=flipped.transform.Find("Body").GetComponent<SpriteRenderer>();body.flipX=true;
                body.GetComponent<PaperStandeeStyle>().ApplyNow();
            }
            return stage.Capture(Output+"standee-facing-comparison.png");
        }
    }

    /// <summary>
    /// Exports native-rendered component layers at each original sprite's resolution plus padding.
    /// This is a game-render export, not a newly generated or restored high-resolution painting.
    /// The source PNG is preserved; body, edge and base are separate real-alpha PNGs for PSD assembly.
    /// </summary>
    public static string ExportLayers()
    {
        var entries = PartyEntries().Concat(CreatureEntries()).Concat(VisitorEntries()).ToList();
        return Export(entries,"standee-common-layer-exports.json");
    }

    public static string ExportRevisedLayers()
    {
        var entries=PartyEntries().Where(e=>e.Id!="scout"&&e.Id!="medic").Concat(VisitorEntries().Where(e=>!e.Enemy)).ToList();
        foreach(var entry in entries)
        {
            Require(AssetDatabase.GetAssetPath(entry.Body).Contains("-body-v"),"Selected full body is not connected: "+entry.Id);
            entry.Id+="-final";
        }
        return Export(entries,"standee-proportions-v3-layer-exports.json");
    }

    static string Export(List<Entry> entries,string manifestName)
    {
        Require(Directory.Exists(Output), "The existing art review directory is missing.");
        var records = new List<LayerExport>();
        foreach (var entry in entries)
        {
            // Source pixel size is retained; padding only gives the native shader room for its common paper edge.
            float scale = entry.BodyHeight * entry.Body.pixelsPerUnit / entry.Pixels.height;
            float unitsPerSourcePixel = scale / entry.Body.pixelsPerUnit;
            // The shared base/shadow extends below the body; do not crop it off an otherwise tightly trimmed sprite.
            int pad = Mathf.Max(16, Mathf.CeilToInt((.6f + entry.Hover) / unitsPerSourcePixel));
            int width = Mathf.CeilToInt(entry.Body.rect.width) + pad * 2;
            int height = Mathf.CeilToInt(entry.Body.rect.height) + pad * 2;
            using (var stage = new Stage(false, width, height))
            {
                stage.Camera.orthographicSize = height * unitsPerSourcePixel * .5f;
                // Center the entire original canvas in the export, not its alpha bounds.
                float canvasCenterY = (entry.Body.rect.height * .5f - entry.Pixels.y) * unitsPerSourcePixel + .035f + entry.Hover;
                float canvasCenterX = (entry.Body.rect.width * .5f - entry.Pixels.center.x) * unitsPerSourcePixel;
                stage.Camera.transform.position = new Vector3(canvasCenterX, canvasCenterY, -10);
                var pawn = stage.AddPawn(entry, Vector2.zero, false);
                var body = pawn.transform.Find("Body").GetComponent<SpriteRenderer>();
                var style = body.GetComponent<PaperStandeeStyle>();
                var material = new Material(style.StyleMaterial) { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    style.Configure(material, stage.LegacyPaper);
                    // Match the edge seen in the 1920×1080 proof, while exporting original source pixels.
                    material.SetFloat("_RimPixels", material.GetFloat("_RimPixels") / (100 * unitsPerSourcePixel));
                    Color ink = material.GetColor("_BodyColor"), paper = material.GetColor("_RimColor");
                    var baseRenderers = pawn.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r != body).ToArray();
                    string compositeFile = stage.Capture(Output + "standee-" + entry.Id + "-composite.png", true);
                    foreach (var part in baseRenderers) part.enabled = false;
                    material.SetColor("_RimColor", new Color(paper.r, paper.g, paper.b, 0));
                    string bodyFile = stage.Capture(Output + "standee-" + entry.Id + "-ink-layer.png", true);
                    material.SetColor("_RimColor", paper);
                    material.SetColor("_BodyColor", new Color(ink.r, ink.g, ink.b, 0));
                    string paperFile = stage.Capture(Output + "standee-" + entry.Id + "-paper-layer.png", true);
                    body.enabled = false;
                    foreach (var part in baseRenderers) part.enabled = true;
                    string baseFile = stage.Capture(Output + "standee-" + entry.Id + "-base-layer.png", true);
                    records.Add(new LayerExport {
                        id = entry.Id, source = AssetDatabase.GetAssetPath(entry.Body), sourceWidth = (int)entry.Body.rect.width,
                        sourceHeight = (int)entry.Body.rect.height, exportWidth = width, exportHeight = height,
                        bodyHeight = entry.BodyHeight, hover = entry.Hover, pixelsPerWorldUnit = 1 / unitsPerSourcePixel,
                        ink = bodyFile, paper = paperFile, sharedBase = baseFile, composite = compositeFile,
                        sourceRectPixels = new PixelRect(entry.Body.rect), sourceTextureWidth = entry.Body.texture.width, sourceTextureHeight = entry.Body.texture.height,
                        note = "Native Unity component export. Original source pixels retained; no detail restoration or new high-resolution artwork claimed."
                    });
                }
                finally { Object.DestroyImmediate(material); }
            }
        }
        string manifest = Output + manifestName;
        File.WriteAllText(manifest, Newtonsoft.Json.JsonConvert.SerializeObject(new LayerManifest { layers = records.ToArray() }, Newtonsoft.Json.Formatting.Indented));
        return "Exported " + records.Count + " native ink/paper/base layer sets.\n" + Path.GetFullPath(manifest);
    }

    [Serializable] sealed class LayerManifest { public LayerExport[] layers; }
    [Serializable] sealed class PixelRect
    {
        public float x,y,width,height;
        public PixelRect(Rect rect){x=rect.x;y=rect.y;width=rect.width;height=rect.height;}
    }
    [Serializable] sealed class LayerExport
    {
        public string id, source, ink, paper, sharedBase, composite, note;
        public int sourceWidth, sourceHeight, sourceTextureWidth, sourceTextureHeight, exportWidth, exportHeight;
        public PixelRect sourceRectPixels;
        public float bodyHeight, hover, pixelsPerWorldUnit;
    }

    static List<Entry> PartyEntries()
    {
        var roster = AssetDatabase.LoadAssetAtPath<PartyRoster>("Assets/Data/PartyRoster.asset");
        Require(roster, "PartyRoster asset is missing.");
        return roster.Candidates.Select(c => {
            Require(c != null && c.Body, "A party Body mapping is missing.");
            return new Entry { Id = c.Id, Name = c.DisplayName, Body = c.Body, BodyHeight=1.72f*roster.BodyScaleFor(c.Body),
                Pixels = c.BodyVisiblePixels.height > 0 ? c.BodyVisiblePixels : AlphaBounds(c.Body) };
        }).ToList();
    }

    static List<Entry> CreatureEntries()
    {
        var roster = AssetDatabase.LoadAssetAtPath<BattleCreatureRoster>("Assets/Data/BattleCreatures.asset");
        Require(roster, "BattleCreatures asset is missing.");
        return roster.Creatures.Select(c => {
            Require(c != null && c.Body, "A creature Body mapping is missing.");
            return new Entry { Id = c.Id, Name = c.Name, Body = c.Body, Enemy = true, BodyHeight = c.Height, Hover = c.Hover,
                Pixels = c.Visible.height > 0 ? c.Visible : AlphaBounds(c.Body) };
        }).ToList();
    }

    static List<Entry> VisitorEntries()
    {
        var entries = new List<Entry>();
        string[] ids = { "npc-doyun", "npc-jun", "npc-giho", "infected" };
        string[] names = { "장도윤", "배준", "남기호", "감염자" };
        for (int i = 0; i < ids.Length; i++)
        {
            string path = "Assets/Art/Tokens/" + (i == 3 ? "infected-body-v4" : ids[i] + "-body") + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            float bodyHeight=1.72f;
            if(i<3)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/Npc_"+ids[i].Substring(4)+".prefab");
                var body=prefab?prefab.transform.Find("Body").GetComponent<SpriteRenderer>():null;
                if(body&&body.sprite)
                {
                    sprite=body.sprite;
                    bodyHeight=AlphaBounds(sprite).height*Mathf.Abs(body.transform.localScale.y)/sprite.pixelsPerUnit;
                }
            }
            Require(sprite, "Required body sprite is not imported: " + path);
            entries.Add(new Entry { Id = ids[i], Name = names[i], Body = sprite, Pixels = AlphaBounds(sprite), Enemy = i == 3,BodyHeight=bodyHeight });
        }
        return entries;
    }

    static Rect AlphaBounds(Sprite sprite)
    {
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            string path = AssetDatabase.GetAssetPath(sprite);
            Require(ImageConversion.LoadImage(source, File.ReadAllBytes(path), false), "Cannot read source PNG: " + path);
            float sx = (float)source.width / sprite.texture.width, sy = (float)source.height / sprite.texture.height;
            int left = Mathf.RoundToInt(sprite.rect.xMin * sx), bottom = Mathf.RoundToInt(sprite.rect.yMin * sy);
            int right = Mathf.RoundToInt(sprite.rect.xMax * sx), top = Mathf.RoundToInt(sprite.rect.yMax * sy);
            var pixels = source.GetPixels32(); int minX = right, minY = top, maxX = left - 1, maxY = bottom - 1, clear = 0;
            for (int y = bottom; y < top; y++) for (int x = left; x < right; x++)
            {
                if (pixels[y * source.width + x].a < 32) { clear++; continue; }
                minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
            }
            Require(maxX >= minX && clear > 0, "Source has no valid isolated alpha silhouette: " + path);
            return new Rect((minX - left) / sx, (minY - bottom) / sy, (maxX - minX + 1) / sx, (maxY - minY + 1) / sy);
        }
        finally { Object.DestroyImmediate(source); }
    }

    sealed class Stage : IDisposable
    {
        readonly Scene scene;
        readonly GameObject root, pawnPrefab;
        readonly Material bodyMaterial;
        readonly int width, height;
        readonly Font font;
        readonly Canvas canvas;
        public readonly Camera Camera;
        public readonly Sprite[] LegacyPaper;

        public Stage(bool background = true, int width = Width, int height = Height)
        {
            this.width = width; this.height = height;
            pawnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PawnPath);
            bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/PaperStandee.mat");
            Require(pawnPrefab && bodyMaterial, "Apply the common FieldPawn paper style first.");
            LegacyPaper = new[] { AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Settlement/standee-scout.png"), AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Settlement/standee-medic.png") };
            font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            Require(font, "The approved game font is missing.");
            scene = EditorSceneManager.NewPreviewScene();
            root = Make("Isolated standee review");
            var cameraObject = Make("Review camera");
            Camera = cameraObject.AddComponent<Camera>();
            Camera.enabled = false;
            Camera.cameraType = CameraType.Preview;
            Camera.scene = scene;
            Camera.cullingMask = 1 << Layer;
            Camera.orthographic = true; Camera.orthographicSize = 5.4f;
            Camera.aspect = (float)width / height;
            Camera.nearClipPlane = .1f; Camera.farClipPlane = 30;
            Camera.transform.position = new Vector3(0, 0, -10);
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = background ? new Color(.035f, .065f, .07f, 1) : Color.clear;
            Camera.allowHDR = false; Camera.allowMSAA = false; Camera.useOcclusionCulling = false;
            var additional = Camera.GetUniversalAdditionalCameraData();
            additional.SetRenderer(0); additional.renderPostProcessing = false;
            additional.requiresColorOption = CameraOverrideOption.Off; additional.requiresDepthOption = CameraOverrideOption.Off;
            var lightObject = Make("Review ambient");
            var light = lightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global; light.intensity = .96f; light.color = Color.white;
            if (background)
            {
                var backdrop = Make("Actual storage background").AddComponent<SpriteRenderer>();
                backdrop.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ExpeditionArrival/storage.png");
                Require(backdrop.sprite, "The actual storage background is missing.");
                backdrop.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/RuinedRoom.mat");
                if (!backdrop.sharedMaterial) backdrop.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/StandeeSpriteLit.mat");
                backdrop.sortingOrder = -100;
                backdrop.transform.localScale = new Vector3(19.2f / backdrop.sprite.bounds.size.x, 10.8f / backdrop.sprite.bounds.size.y, 1);
                var canvasObject = Make("Review captions", typeof(RectTransform), typeof(Canvas));
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera;
                var canvasRect=(RectTransform)canvasObject.transform;
                canvasRect.sizeDelta=new Vector2(Width,Height);canvasRect.localScale=Vector3.one*.01f;canvasRect.localPosition=new Vector3(0,0,-2);
                canvas.sortingOrder = 2000;
                var scaler = canvasObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                Canvas.ForceUpdateCanvases();
            }
        }

        GameObject Make(string name, params Type[] components)
        {
            var go = new GameObject(name, components) { hideFlags = HideFlags.HideAndDontSave, layer = Layer };
            SceneManager.MoveGameObjectToScene(go, scene);
            if (root) go.transform.SetParent(root.transform, false);
            return go;
        }

        public void Header(string title, string subtitle)
        {
            Label(title, 70, 32, 1780, 45, 31, TextAnchor.MiddleLeft);
            Label(subtitle, 70, 78, 1780, 32, 19, TextAnchor.MiddleLeft);
        }

        public GameObject AddPawn(Entry entry, Vector2 foot, bool caption = true)
        {
            var holder = Make("Review holder " + entry.Id);
            holder.SetActive(false);
            var pawn = Object.Instantiate(pawnPrefab, holder.transform);
            pawn.name = entry.Name;
            // Never initialize movement, saved-game or encounter components for an art review clone.
            foreach (var behaviour in pawn.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(behaviour is PaperStandeeStyle)) Object.DestroyImmediate(behaviour);
            foreach (var transform in pawn.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = Layer;
            pawn.transform.localPosition = new Vector3(foot.x, foot.y, 0);
            var body = pawn.transform.Find("Body").GetComponent<SpriteRenderer>();
            body.sprite = entry.Body; body.flipX = false; body.flipY = false; body.color = Color.white;
            float scale = entry.BodyHeight * entry.Body.pixelsPerUnit / entry.Pixels.height;
            body.transform.localScale = Vector3.one * scale;
            body.transform.localPosition = new Vector3((entry.Body.pivot.x - entry.Pixels.center.x) * scale / entry.Body.pixelsPerUnit,
                (entry.Body.pivot.y - entry.Pixels.y) * scale / entry.Body.pixelsPerUnit + .035f + entry.Hover, 0);
            var style = body.GetComponent<PaperStandeeStyle>() ?? body.gameObject.AddComponent<PaperStandeeStyle>();
            style.Configure(bodyMaterial, LegacyPaper);
            var baseRenderer = pawn.transform.Find("Base").GetComponent<SpriteRenderer>();
            baseRenderer.color = entry.Enemy ? EnemyBase : AllyBase;
            var group = pawn.GetComponent<SortingGroup>();
            if (group) group.sortingOrder = Mathf.RoundToInt(100 - foot.y * 10);
            holder.SetActive(true); style.ApplyNow();
            if (caption && canvas)
            {
                float px = (foot.x + 9.6f) * 100, py = (5.4f - foot.y) * 100;
                Label(entry.Name, px - 137, py + 24, 274, 34, 24, TextAnchor.MiddleCenter);
            }
            return pawn;
        }

        void Label(string value, float x, float y, float w, float h, int size, TextAnchor align)
        {
            if (!canvas) return;
            var go = Make("Review note " + value, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
            var text = go.GetComponent<Text>(); text.text = value; text.font = font; text.fontSize = size;
            text.color = new Color(.94f, .88f, .74f, 1); text.alignment = align;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow; text.raycastTarget = false;
            var shadow = go.AddComponent<Shadow>(); shadow.effectColor = new Color(.02f, .035f, .035f, .95f); shadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        public string Capture(string path, bool alpha = false)
        {
            Canvas.ForceUpdateCanvases();
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            RenderTexture straight = null;
            Material straightMaterial = null;
            var previous = RenderTexture.active;
            var originalTarget = Camera.targetTexture;
            var image = new Texture2D(width, height, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false, false);
            try
            {
                rt.Create(); Camera.targetTexture = rt;
                var request = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(Camera, request)) RenderPipeline.SubmitRenderRequest(Camera, request);
                else Camera.Render();
                if (alpha)
                {
                    var shader = Shader.Find("Hidden/Demo5/StandeeStraightAlphaExport");
                    Require(shader && shader.isSupported, "Native straight-alpha export shader is missing or unsupported.");
                    straightMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                    straight = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    straight.Create();
                    // Convert the render target's premultiplied blend result to PNG/PSD straight-alpha storage.
                    // This is native frame export, never a modification of the source artwork.
                    Graphics.Blit(rt, straight, straightMaterial);
                    RenderTexture.active = straight;
                }
                else RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(false);
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(image));
            }
            finally
            {
                Camera.targetTexture = originalTarget; RenderTexture.active = previous;
                Object.DestroyImmediate(image); rt.Release(); Object.DestroyImmediate(rt);
                if (straight) { straight.Release(); Object.DestroyImmediate(straight); }
                if (straightMaterial) Object.DestroyImmediate(straightMaterial);
            }
            return Path.GetFullPath(path);
        }

        public void Dispose()
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
