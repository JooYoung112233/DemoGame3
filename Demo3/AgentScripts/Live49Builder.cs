// Builds Live49's 00_Title / 01_Game scenes from the repo handoff data. Runs inside the Editor:
//   unity command run_script --file AgentScripts/Live49Builder.cs --entry Live49Builder.ImportTmpEssentials
//   unity command run_script --file AgentScripts/Live49Builder.cs --entry Live49Builder.All
// Re-running rebuilds both scenes from scratch, so hand edits to those scenes would be lost.
using System;
using System.IO;
using Live49.Chapter00;
using Live49.Dialogue;
using Live49.EditorTools;
using Live49.Title;
using Live49.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class Live49Builder
{
    const string Root = "Assets/_Project";
    const string TitleScenePath = Root + "/Scenes/00_Title.unity";
    const string GameScenePath = Root + "/Scenes/01_Game.unity";
    const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    const string FontSourcePath = Root + "/Shared/Fonts/Live49MenuSerif-Regular.ttf";
    const string FontAssetPath = Root + "/Shared/Fonts/Live49MenuSerif SDF.asset";
    const string StageMaterialPath = Root + "/Shaders/StageImage.mat";

    const string TitleArt = Root + "/Title/Art/";
    const string TitleUI = Root + "/Title/UI/";
    const string SharedUI = Root + "/Shared/UI/";
    const string Portraits = Root + "/Shared/Characters/Portraits/";
    const string Camper = Root + "/Shared/Locations/Camper/";
    const string MemoryArt = Root + "/Chapters/Chapter00/Art/Memory/";
    const string PresentArt = Root + "/Chapters/Chapter00/Art/Present/";
    const string Ch0Data = Root + "/Chapters/Chapter00/Data/";

    const float ArtW = 1672f;
    const float ArtH = 941f;
    static readonly Color Cream = new Color32(0xF2, 0xE4, 0xC4, 0xFF);
    static readonly Color Muted = new Color32(0xC7, 0xB6, 0x95, 0xFF);

    // art/chapter00-01/art-polish-v1/scene-manifest.json · C0-current, in draw order (native 1672x941 coords).
    static readonly (string file, float x, float y, float w, float h)[] PresentLayers =
    {
        ("C0-current-fixed_background", 0, 0, 1672, 941),
        ("C0-current-suhyeok-seated", 836, 289, 116, 155),
        ("C0-current-soi-seated", 947, 330, 94, 114),
        ("C0-current-table_occlusion", 835, 432, 257, 201),
        ("C0-current-book", 904, 454, 102, 73),
        ("C0-current-mug", 1010, 480, 53, 52),
        ("C0-current-suhyeok-seated_forearms", 836, 289, 116, 155),
        ("C0-current-soi-seated_forearms", 947, 330, 94, 114),
        ("C0-current-water-before", 630, 325, 47, 60),
        ("C0-current-blanket-before", 1194, 550, 124, 84),
        ("C0-current-panel-loose", 604, 426, 86, 61),
        ("C0-current-flashlight-stored", 504, 586, 55, 37),
    };

    static TMP_FontAsset _font;
    static Material _stageMaterial;

    public static string ImportTmpEssentials()
    {
        if (File.Exists(TmpSettingsPath)) return "TMP essentials already present";
        TMP_PackageResourceImporter.ImportResources(true, false, false);
        return "TMP essentials import requested";
    }

    public static string All()
    {
        if (!File.Exists(TmpSettingsPath))
            throw new InvalidOperationException("TMP Settings missing; run ImportTmpEssentials first.");

        int textures = ApplyImportRules();
        _font = CreateFont();
        _stageMaterial = StageMaterial();
        BuildTitle();
        BuildGame();

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(TitleScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true),
        };
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.runInBackground = true; // timed beats already pause on focus loss via SeqTime
        AssetDatabase.SaveAssets();

        EditorSceneManager.OpenScene(TitleScenePath);
        return $"textures reimported: {textures}; scenes: {TitleScenePath}, {GameScenePath}";
    }

    // ---------------------------------------------------------------- assets

    static int ApplyImportRules()
    {
        int count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                Live49TextureImportRules.Apply(importer, path);
                importer.SaveAndReimport();
                count++;
            }
        }
        return count;
    }

    static TMP_FontAsset CreateFont()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (existing != null) return existing;

        var source = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
        var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        font.name = "Live49MenuSerif SDF";
        AssetDatabase.CreateAsset(font, FontAssetPath);
        font.atlasTextures[0].name = "Live49MenuSerif SDF Atlas";
        AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
        font.material.name = "Live49MenuSerif SDF Material";
        AssetDatabase.AddObjectToAsset(font.material, font);
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        return font;
    }

    static Material StageMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(StageMaterialPath);
        if (existing != null) return existing;

        var shader = Shader.Find("Live49/UI/StageImage")
            ?? throw new InvalidOperationException("Shader Live49/UI/StageImage not compiled.");
        var material = new Material(shader) { name = "StageImage" };
        AssetDatabase.CreateAsset(material, StageMaterialPath);
        return material;
    }

    // ---------------------------------------------------------------- 00_Title

    static void BuildTitle()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var system = NewRoot("[System]");
        var cameraRoot = NewRoot("[Camera]");
        var uiRoot = NewRoot("[UI]");

        var eventSystem = BuildEventSystem(system.transform);
        var camera = BuildCamera(cameraRoot.transform, 0);
        var viewport = BuildCanvas("Canvas_Title", 0, uiRoot.transform, out _);

        // Push-in pivots on the approved focus (0.66, 0.52 from top-left).
        var zoom = Stretch(NewUI("BackgroundZoom", viewport));
        zoom.pivot = new Vector2(0.66f, 0.48f);
        var titleFrame = ArtFrame("TitleArt_1672x941", zoom);
        AddImage(Place(NewUI("title-background-v1", titleFrame), 0, 0, ArtW, ArtH), S(TitleArt + "title-background-v1.png"));

        var menuRoot = Stretch(NewUI("MenuGroup", viewport));
        var menuGroup = Group(menuRoot);
        menuGroup.blocksRaycasts = true;
        menuGroup.interactable = true;
        AddImage(Stretch(NewUI("ReadabilityShade", menuRoot)), S(TitleUI + "01_readability_shade.png"));
        AddImage(Place(NewUI("Logo", menuRoot), 167, 222, 500, 195), S(TitleArt + "logo-v1.png")).preserveAspect = true;

        var menu = Stretch(NewUI("Menu", menuRoot));
        var continueItem = MenuItem(menu, "continue", "이어하기", 492);
        var startItem = MenuItem(menu, "start", "시작", 492);
        var settingsItem = MenuItem(menu, "settings", "설정", 574);
        var quitItem = MenuItem(menu, "quit", "종료", 656);
        continueItem.gameObject.SetActive(false);

        Text(Place(NewUI("InputHint", menuRoot), 215, 960, 400, 30), "↑ ↓ 선택   ·   Enter 확인", 16, Muted, TextAlignmentOptions.Center);

        var controller = new GameObject("TitleController").AddComponent<TitleController>();
        controller.transform.SetParent(system.transform);
        Wire(controller,
            ("continueItem", continueItem), ("startItem", startItem), ("settingsItem", settingsItem), ("quitItem", quitItem),
            ("menuGroup", menuGroup), ("backgroundZoom", zoom), ("titleCamera", camera), ("eventSystem", eventSystem));

        EditorSceneManager.SaveScene(scene, TitleScenePath);
    }

    static TitleMenuItem MenuItem(RectTransform parent, string id, string label, float y)
    {
        var rt = Place(NewUI("MenuItem_" + char.ToUpper(id[0]) + id.Substring(1), parent), 235, y, 360, 64);
        var surface = AddImage(rt, S(TitleUI + "title-button-normal.png"), true);
        surface.type = Image.Type.Sliced;
        surface.pixelsPerUnitMultiplier = 2f; // sprite drawn at 2x
        var text = Text(Stretch(NewUI("Label", rt)), label, 28, Muted, TextAlignmentOptions.Center);

        var item = rt.gameObject.AddComponent<TitleMenuItem>();
        var so = new SerializedObject(item);
        so.FindProperty("itemId").stringValue = id;
        so.ApplyModifiedPropertiesWithoutUndo();
        Wire(item,
            ("surface", surface), ("label", text),
            ("normalSprite", S(TitleUI + "title-button-normal.png")), ("focusSprite", S(TitleUI + "title-button-focus.png")),
            ("pressedSprite", S(TitleUI + "title-button-pressed.png")), ("disabledSprite", S(TitleUI + "title-button-disabled.png")));
        return item;
    }

    // ---------------------------------------------------------------- 01_Game

    static void BuildGame()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var system = NewRoot("[System]");
        var cameraRoot = NewRoot("[Camera]");
        var uiRoot = NewRoot("[UI]");

        BuildEventSystem(system.transform);
        BuildCamera(cameraRoot.transform, 1);
        var viewport = BuildCanvas("Canvas_Game", 10, uiRoot.transform, out var canvas);
        var gameRoot = canvas.gameObject.AddComponent<CanvasGroup>();
        gameRoot.blocksRaycasts = false;

        var stage = Stretch(NewUI("Stage", viewport));
        var chapter0 = Stretch(NewUI("Chapter00", stage));

        // P02 · P08 — camper wall photo
        var wallRt = Stretch(NewUI("C0_PhotoWall", chapter0));
        var wall = Group(wallRt);
        var wallFrame = ArtFrame("CamperArt_1672x941", wallRt);
        var camperWall = StageImage(Place(NewUI("camper-clean-base-v3", wallFrame), 0, 0, ArtW, ArtH), Camper + "camper-clean-base-v3.png", true, null);
        var dimRt = Stretch(NewUI("WallDim", wallRt));
        Black(dimRt);
        var wallDim = Group(dimRt, 0f);

        var polaroid = Place(NewUI("Polaroid", wallRt), 1442f, 269f, 44f, 28.34f);
        var photoRt = NewUI("family-sunset-source-v1", polaroid);
        // Photo sits in the frame's hole: 56px top/sides, 152px bottom of 1784x1149.
        photoRt.anchorMin = new Vector2(56f / 1784f, 152f / 1149f);
        photoRt.anchorMax = new Vector2(1728f / 1784f, 1093f / 1149f);
        photoRt.offsetMin = photoRt.offsetMax = Vector2.zero;
        var faceMask = AssetDatabase.LoadAssetAtPath<Texture2D>(MemoryArt + "seoyeon-face-mask-v1.png");
        var photo = StageImage(photoRt, MemoryArt + "family-sunset-source-v1.png", true, faceMask);
        StageImage(Stretch(NewUI("polaroid-frame-v1", polaroid)), MemoryArt + "polaroid-frame-v1.png", true, null);

        // P03 ~ P07 — memory path, feet, handhold
        var memoryRt = Stretch(NewUI("C0_MemoryPath", chapter0));
        var memory = Group(memoryRt, 0f);
        var memoryFrame = ArtFrame("MemoryArt_1672x941", memoryRt);
        StageImage(Place(NewUI("path-closeup-review-v1", memoryFrame), 0, 0, ArtW, ArtH), MemoryArt + "path-closeup-review-v1.png", false, null);
        var feetRt = Place(NewUI("feet-paused-v1", memoryFrame), 0, 0, ArtW, ArtH);
        StageImage(feetRt, MemoryArt + "feet-paused-v1.png", false, null);
        var feet = Group(feetRt);
        var handsRt = Place(NewUI("hands-held-v2", memoryFrame), 0, 0, ArtW, ArtH);
        StageImage(handsRt, MemoryArt + "hands-held-v2.png", false, null);
        var hands = Group(handsRt, 0f);

        // P10 — present camper (C0-current layers)
        var presentRt = Stretch(NewUI("C0_Present", chapter0));
        var present = Group(presentRt, 0f);
        var presentFrame = ArtFrame("PresentArt_1672x941", presentRt);
        foreach (var layer in PresentLayers)
            StageImage(Place(NewUI(layer.file, presentFrame), layer.x, layer.y, layer.w, layer.h), PresentArt + layer.file + ".png", false, null);

        // Portraits (left Suhyeok / right Soi)
        var portraitsRt = Stretch(NewUI("Portraits", viewport));
        var suhyeokRt = Place(NewUI("Portrait_Suhyeok", portraitsRt), 100, 50, 720, 1080);
        AddImage(suhyeokRt, S(Portraits + "suhyeok-dialogue-native.png")).preserveAspect = true;
        var suhyeok = Group(suhyeokRt, 0f);
        var soiRt = Place(NewUI("Portrait_Soi", portraitsRt), 1100, 180, 700, 1050);
        AddImage(soiRt, S(Portraits + "soi-answer-soft-v3.png")).preserveAspect = true;
        var soi = Group(soiRt, 0f);

        // Call overlay ("수혁아." / "아빠?")
        var callRt = Stretch(NewUI("CallOverlay", viewport));
        var callOverlay = Group(callRt);
        var bottomDimRt = Stretch(NewUI("BottomDim", callRt));
        AddImage(bottomDimRt, S(SharedUI + "01_bottom_readability_gradient.png"));
        var bottomDim = Group(bottomDimRt, 0f);
        var callTextRt = Place(NewUI("CallText", callRt), 460, 795, 1000, 64);
        Text(callTextRt, string.Empty, 38, Cream, TextAlignmentOptions.Center);
        var callText = callTextRt.gameObject.AddComponent<Typewriter>();
        var callCueRt = Place(NewUI("AdvanceCue", callRt), 1766, 979, 28, 28);
        AddImage(callCueRt, S(SharedUI + "03_advance_cue.png"));
        var callCue = Group(callCueRt, 0f);
        var hintRt = Place(NewUI("FirstInputHint", callRt), 660, 976, 600, 26);
        var hintLabel = Text(hintRt, string.Empty, 16, Muted, TextAlignmentOptions.Center);
        var hint = Group(hintRt, 0f);

        // Shared dialogue window
        var dialogueRt = Stretch(NewUI("DialoguePanel", viewport));
        var view = dialogueRt.gameObject.AddComponent<DialogueView>();
        var panelRt = Stretch(NewUI("Panel", dialogueRt));
        var panel = Group(panelRt, 0f);
        AddImage(Place(NewUI("Background", panelRt), 60, 790, 1800, 240), S(SharedUI + "01_dialogue_panel.png"));
        var nameplateRt = Place(NewUI("Nameplate", panelRt), 110, 752, 240, 64);
        AddImage(nameplateRt, S(SharedUI + "02_nameplate.png"));
        var nameLabel = Text(Stretch(NewUI("NameLabel", nameplateRt)), string.Empty, 28, Cream, TextAlignmentOptions.Center);
        var bodyRt = Place(NewUI("Body", panelRt), 140, 850, 1620, 140);
        Text(bodyRt, string.Empty, 38, Cream, TextAlignmentOptions.TopLeft).textWrappingMode = TextWrappingModes.Normal;
        var body = bodyRt.gameObject.AddComponent<Typewriter>();
        var dialogueCueRt = Place(NewUI("AdvanceCue", panelRt), 1780, 975, 28, 28);
        AddImage(dialogueCueRt, S(SharedUI + "03_advance_cue.png"));
        var dialogueCue = Group(dialogueCueRt, 0f);

        var fadeRt = Stretch(NewUI("ScreenFade", viewport));
        Black(fadeRt);
        var screenFade = Group(fadeRt, 0f);

        Wire(view, ("panel", panel), ("nameplate", nameplateRt.gameObject), ("nameLabel", nameLabel), ("body", body), ("cue", dialogueCue));
        var viewSo = new SerializedObject(view);
        var portraitArray = viewSo.FindProperty("portraits");
        portraitArray.arraySize = 2;
        SetPortrait(portraitArray.GetArrayElementAtIndex(0), "suhyeok", suhyeok);
        SetPortrait(portraitArray.GetArrayElementAtIndex(1), "soi", soi);
        viewSo.ApplyModifiedPropertiesWithoutUndo();

        var director = new GameObject("C0_OpeningDirector").AddComponent<C0OpeningDirector>();
        director.transform.SetParent(system.transform);
        Wire(director,
            ("dialogueJson", AssetDatabase.LoadAssetAtPath<TextAsset>(Ch0Data + "C0_OpeningDialogue.json")),
            ("openingBook", S(TitleArt + "title-background-v1.png")),
            ("gameRoot", gameRoot), ("screenFade", screenFade),
            ("photoWall", wall), ("camperWall", camperWall), ("wallDim", wallDim), ("polaroid", polaroid), ("photo", photo),
            ("memoryPath", memory), ("feet", feet), ("hands", hands), ("present", present),
            ("callOverlay", callOverlay), ("bottomDim", bottomDim), ("callText", callText), ("callCue", callCue),
            ("firstInputHint", hint), ("firstInputHintLabel", hintLabel), ("dialogue", view));

        EditorSceneManager.SaveScene(scene, GameScenePath);
    }

    static void SetPortrait(SerializedProperty element, string speakerId, CanvasGroup group)
    {
        element.FindPropertyRelative("speakerId").stringValue = speakerId;
        element.FindPropertyRelative("group").objectReferenceValue = group;
    }

    // ---------------------------------------------------------------- helpers

    static GameObject NewRoot(string name) => new GameObject(name);

    static EventSystem BuildEventSystem(Transform parent)
    {
        var go = new GameObject("EventSystem");
        go.transform.SetParent(parent);
        var eventSystem = go.AddComponent<EventSystem>();
        WireUiActions(go.AddComponent<InputSystemUIInputModule>());
        return eventSystem;
    }

    const string UiActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";

    // Points the UI module at the project's actions asset. AssignDefaultActions() builds its asset in memory,
    // so after a scene save the module keeps references to a missing asset and gets no pointer input.
    static void WireUiActions(InputSystemUIInputModule module)
    {
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(UiActionsPath)
            ?? throw new InvalidOperationException("Missing " + UiActionsPath);
        var refs = new System.Collections.Generic.Dictionary<string, UnityEngine.InputSystem.InputActionReference>();
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(UiActionsPath))
            if (o is UnityEngine.InputSystem.InputActionReference r) refs[r.name] = r;

        UnityEngine.InputSystem.InputActionReference Required(string action) =>
            refs.TryGetValue("UI/" + action, out var r) ? r : throw new InvalidOperationException($"{UiActionsPath} has no UI/{action}");
        UnityEngine.InputSystem.InputActionReference Optional(string action) =>
            refs.TryGetValue("UI/" + action, out var r) ? r : null;

        module.actionsAsset = asset;
        module.point = Required("Point");
        module.leftClick = Required("Click");
        module.scrollWheel = Required("ScrollWheel");
        module.move = Required("Navigate");
        module.submit = Required("Submit");
        module.cancel = Required("Cancel");
        module.rightClick = Optional("RightClick");
        module.middleClick = Optional("MiddleClick");
        module.trackedDevicePosition = Optional("TrackedDevicePosition");
        module.trackedDeviceOrientation = Optional("TrackedDeviceOrientation");
        EditorUtility.SetDirty(module);
    }

    // Re-wires the UI input module in both saved scenes without rebuilding them.
    public static string FixEventSystems()
    {
        foreach (var path in new[] { GameScenePath, TitleScenePath })
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var module in Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsSortMode.None))
                WireUiActions(module);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        return "UI input actions wired in both scenes";
    }

    static Camera BuildCamera(Transform parent, float depth)
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(0f, 0f, -10f);
        var camera = go.AddComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black; // letterbox outside 16:9
        camera.depth = depth;
        go.AddComponent<AudioListener>();
        return camera;
    }

    // Overlay canvas with a fixed 1920x1080 viewport; Expand keeps the whole 16:9 frame on any aspect.
    static RectTransform BuildCanvas(string name, int sortingOrder, Transform parent, out Canvas canvas)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)) { layer = 5 };
        go.transform.SetParent(parent);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        var viewport = NewUI("Viewport_1920x1080", go.transform);
        viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.sizeDelta = new Vector2(1920f, 1080f);
        viewport.gameObject.AddComponent<RectMask2D>();
        return viewport;
    }

    // 1672x941 art space fitted into 1920x1080 with uniform scale (never stretched per axis).
    static RectTransform ArtFrame(string name, Transform parent)
    {
        var rt = NewUI(name, parent);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(ArtW, ArtH);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one * Mathf.Min(1920f / ArtW, 1080f / ArtH);
        return rt;
    }

    static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // [x, y, w, h] from the top-left, as in the handoff JSON.
    static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    static RectTransform Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    static Image AddImage(RectTransform rt, Sprite sprite, bool raycast = false)
    {
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = raycast;
        return image;
    }

    static void Black(RectTransform rt)
    {
        var image = rt.gameObject.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
    }

    static StageImageFX StageImage(RectTransform rt, string spritePath, bool receivesGlitch, Texture faceMask)
    {
        AddImage(rt, S(spritePath));
        var fx = rt.gameObject.AddComponent<StageImageFX>();
        Wire(fx, ("baseMaterial", _stageMaterial), ("faceMask", faceMask));
        var so = new SerializedObject(fx);
        so.FindProperty("receivesGlitch").boolValue = receivesGlitch;
        so.ApplyModifiedPropertiesWithoutUndo();
        return fx;
    }

    static CanvasGroup Group(RectTransform rt, float alpha = 1f)
    {
        var group = rt.gameObject.AddComponent<CanvasGroup>();
        group.alpha = alpha;
        group.interactable = false;
        group.blocksRaycasts = false;
        return group;
    }

    static TextMeshProUGUI Text(RectTransform rt, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = _font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    static Sprite S(string path) =>
        AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException("Missing sprite " + path);

    static void Wire(Object target, params (string field, Object value)[] refs)
    {
        var so = new SerializedObject(target);
        foreach (var (field, value) in refs)
        {
            var property = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name}.{field} not found");
            property.objectReferenceValue = value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
