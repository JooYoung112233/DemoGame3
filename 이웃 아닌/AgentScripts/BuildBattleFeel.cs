using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object = UnityEngine.Object;

// Adds the combat-feel layer to the existing battle prefab without rebuilding its approved layout.
// Safe to re-run: every element it owns is replaced by name.
public static class BuildBattleFeel
{
    const string P = "Assets/Prefabs/Settlement/", A = "Assets/Art/PartySelection/", Audio = "Assets/Audio/Battle/";
    static Font font;
    static readonly Color Ink = new Color(.045f, .065f, .06f), Cream = new Color(.95f, .92f, .82f), Ochre = new Color(1, .78f, .38f),
        Danger = new Color(.86f, .42f, .33f), Sage = new Color(.62f, .8f, .66f);

    static RectTransform Node(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = anchor; r.pivot = pivot; r.anchoredPosition = position; r.sizeDelta = size; return r;
    }
    // Top-left placement in 1920x1080 canvas pixels, matching BuildFieldBattle.
    static RectTransform TopLeft(string name, Transform parent, float x, float y, float w, float h) => Node(name, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h));
    // HUD children sit on the HUD's bottom-center pivot.
    static RectTransform Bottom(string name, Transform parent, float x, float y, float w, float h) => Node(name, parent, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(x, y), new Vector2(w, h));
    static Text Label(RectTransform r, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var t = r.gameObject.AddComponent<Text>(); t.font = font; t.fontSize = size; t.text = text; t.color = color; t.alignment = align; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; return t;
    }
    static Image Picture(RectTransform r, string sprite, Color color)
    {
        var i = r.gameObject.AddComponent<Image>(); if (sprite != null) i.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(sprite); i.color = color; i.raycastTarget = false; return i;
    }
    static T Graphic<T>(RectTransform r, Color color) where T : MaskableGraphic { var g = r.gameObject.AddComponent<T>(); g.color = color; g.raycastTarget = false; return g; }
    static void Outline(Graphic g, float distance) { var o = g.gameObject.AddComponent<Outline>(); o.effectColor = new Color(Ink.r, Ink.g, Ink.b, .92f); o.effectDistance = new Vector2(distance, -distance); }
    static GameObject Save(GameObject root, string name) { var asset = PrefabUtility.SaveAsPrefabAsset(root, P + name + ".prefab"); Object.DestroyImmediate(root); return asset; }
    static void Remove(Transform parent, string name) { var old = parent.Find(name); if (old) Object.DestroyImmediate(old.gameObject); }
    static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Audio + "battle-" + name + ".wav");

    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop first");
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");
        var flash = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/StandeeFlash.mat");
        if (!flash) { flash = new Material(Shader.Find("Demo5/StandeeFlash")) { name = "StandeeFlash" }; AssetDatabase.CreateAsset(flash, "Assets/Settings/StandeeFlash.mat"); }

        // Overhead HUD: intent / danger / aim tags, name, segmented health.
        var hud = Node("BattlePawnHud", null, new Vector2(0, 1), new Vector2(.5f, 0), Vector2.zero, new Vector2(184, 122));
        var h = hud.gameObject.AddComponent<BattlePawnHud>();
        Picture(Bottom("BarBack", hud, 0, 2, 114, 16), null, new Color(.02f, .03f, .03f, .88f));
        h.Segments = Bottom("Segments", hud, 0, 5, 108, 10);
        var layout = h.Segments.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 2;
        layout.childControlWidth = layout.childControlHeight = layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        h.SegmentTemplate = Picture(Bottom("SegmentTemplate", h.Segments, 0, 0, 10, 10), A + "paper-texture.png", Color.white);
        h.Guard = Graphic<BattleShieldIcon>(Bottom("Guard", hud, -69, -1, 18, 21), Sage).gameObject;
        h.Name = Label(Bottom("Name", hud, 0, 21, 184, 26), "탐험가 1", 19, Cream); Outline(h.Name, 1.6f);
        var intent = Bottom("Intent", hud, 0, 52, 152, 32); h.Intent = intent.gameObject;
        Picture(Bottom("Paper", intent, 0, 0, 152, 32), A + "footer-paper.png", Cream);
        h.IntentClaw = Graphic<BattleSlashGraphic>(Bottom("Claw", intent, -48, 5, 22, 22), Danger); h.IntentClaw.Angle = -62; h.IntentClaw.Width = .22f;
        h.IntentArrow = Graphic<BattleChevronGraphic>(Bottom("Arrow", intent, -48, 6, 20, 20), Ink);
        h.IntentLabel = Label(Bottom("Label", intent, 16, 1, 112, 30), "물기 70%", 18, Ink);
        var danger = Bottom("Danger", hud, 0, 52, 124, 32); h.Danger = danger.gameObject;
        Picture(Bottom("Paper", danger, 0, 0, 124, 32), A + "footer-paper.png", Danger);
        h.DangerLabel = Label(Bottom("Label", danger, 0, 1, 120, 30), "! 70%", 19, Cream);
        var aim = Bottom("Aim", hud, 0, 88, 118, 30); h.Aim = aim.gameObject;
        Picture(Bottom("Paper", aim, 0, 0, 118, 30), A + "footer-paper.png", Ochre);
        h.AimLabel = Label(Bottom("Label", aim, 0, 1, 114, 28), "명중 75%", 18, Ink);
        var hudAsset = Save(hud.gameObject, "BattlePawnHud");

        var floating = Node("BattleFloatingText", null, new Vector2(0, 1), new Vector2(.5f, .5f), Vector2.zero, new Vector2(340, 96));
        var ft = floating.gameObject.AddComponent<BattleFloatingText>(); ft.Label = Label(Node("Label", floating, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(340, 96)), "-3", 46, Cream);
        Outline(ft.Label, 2.6f); var shadow = ft.Label.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .55f); shadow.effectDistance = new Vector2(3, -4);
        var floatingAsset = Save(floating.gameObject, "BattleFloatingText");

        var burst = Node("BattleImpactBurst", null, new Vector2(0, 1), new Vector2(.5f, .5f), Vector2.zero, new Vector2(128, 128)); Graphic<BattleBurstGraphic>(burst, Cream);
        var burstAsset = Save(burst.gameObject, "BattleImpactBurst");
        var slash = Node("BattleSlash", null, new Vector2(0, 1), new Vector2(.5f, .5f), Vector2.zero, new Vector2(150, 150)); Graphic<BattleSlashGraphic>(slash, Cream);
        var slashAsset = Save(slash.gameObject, "BattleSlash");
        var tracer = Node("BattleTracer", null, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(1920, 1080)); Graphic<BattleTracerGraphic>(tracer, Ochre);
        var tracerAsset = Save(tracer.gameObject, "BattleTracer");
        var shield = Node("BattleGuardShield", null, new Vector2(0, 1), new Vector2(.5f, .5f), Vector2.zero, new Vector2(72, 82)); Graphic<BattleShieldIcon>(shield, Sage);
        var shieldAsset = Save(shield.gameObject, "BattleGuardShield");
        var threat = Node("BattleThreatLine", null, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(1920, 1080)); Graphic<BattleThreatLine>(threat, new Color(Danger.r, Danger.g, Danger.b, .82f));
        var threatAsset = Save(threat.gameObject, "BattleThreatLine");

        var review = PrefabUtility.LoadPrefabContents(P + "BattleRetreatReview.prefab");
        try
        {
            review.transform.Find("Body").GetComponent<Text>().text = "복도로 이동 · 원정대 전체 1턴\n\n등을 보이면 붙어 있는 감염자가 한 번씩 덤빕니다.\n명중 50% · 피해 1 · 쓰러지지는 않습니다.\n가방과 수색 진행도는 잃지 않습니다.";
            PrefabUtility.SaveAsPrefabAsset(review, P + "BattleRetreatReview.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(review); }

        var root = PrefabUtility.LoadPrefabContents(P + "ExpeditionBattlePanel.prefab");
        try
        {
            var c = root.GetComponent<ExpeditionBattlePanel>(); var workspace = root.transform.Find("Workspace");
            foreach (var n in new[] { "PawnHudLayer", "BattleFxLayer", "NoiseGauge" }) Remove(workspace, n);
            // HUD above the board cells, under the feedback strip and bottom band. FX on top of the whole workspace.
            c.HudLayer = TopLeft("PawnHudLayer", workspace, 0, 0, 1920, 1080); c.HudLayer.SetSiblingIndex(workspace.Find("FeedbackPaper").GetSiblingIndex());
            c.FxLayer = TopLeft("BattleFxLayer", workspace, 0, 0, 1920, 1080); c.FxLayer.SetAsLastSibling();
            var edge = Graphic<BattleEdgeFlash>(TopLeft("EdgeFlash", c.FxLayer, 0, 0, 1920, 1080), Color.clear);
            var gauge = TopLeft("NoiseGauge", workspace, 440, 97, 308, 34); gauge.SetSiblingIndex(workspace.Find("Round").GetSiblingIndex() + 1); c.NoiseGauge = gauge;
            Label(TopLeft("Label", gauge, 0, 0, 64, 34), "소음", 21, Cream, TextAnchor.MiddleLeft);
            Picture(TopLeft("Track", gauge, 66, 11, 152, 13), null, new Color(.02f, .03f, .03f, .9f));
            c.NoiseFill = Picture(TopLeft("Fill", gauge, 68, 13, 148, 9), A + "paper-texture.png", Ochre);
            c.NoiseFill.type = Image.Type.Filled; c.NoiseFill.fillMethod = Image.FillMethod.Horizontal; c.NoiseFill.fillAmount = 0;
            // Static placeholders read the rules so the editor view never shows stale numbers (runtime overwrites them anyway).
            c.NoiseValue = Label(TopLeft("Value", gauge, 226, 0, 82, 34), "0 / " + c.Rules.ReinforcementNoise, 20, Cream, TextAnchor.MiddleLeft);

            var audio = root.GetComponent<AudioSource>(); if (!audio) audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false; audio.spatialBlend = 0; audio.loop = false;
            var fx = root.GetComponent<BattlePresentation>(); if (!fx) fx = root.AddComponent<BattlePresentation>();
            fx.Audio = audio; fx.FlashMaterial = flash; fx.EdgeFlash = edge;
            fx.FloatingTextPrefab = floatingAsset.GetComponent<BattleFloatingText>(); fx.BurstPrefab = burstAsset.GetComponent<BattleBurstGraphic>();
            fx.SlashPrefab = slashAsset.GetComponent<BattleSlashGraphic>(); fx.TracerPrefab = tracerAsset.GetComponent<BattleTracerGraphic>();
            fx.ShieldPrefab = shieldAsset.GetComponent<BattleShieldIcon>();
            fx.Swing = Clip("swing"); fx.Impact = Clip("impact"); fx.Critical = Clip("critical"); fx.Gunshot = Clip("gunshot"); fx.Miss = Clip("miss");
            fx.Bite = Clip("bite"); fx.Block = Clip("block"); fx.Fall = Clip("fall"); fx.Step = Clip("step"); fx.Turn = Clip("turn"); fx.Growl = Clip("growl");
            c.Presentation = fx; c.HudPrefab = hudAsset.GetComponent<BattlePawnHud>(); c.ThreatPrefab = threatAsset.GetComponent<BattleThreatLine>();
            c.RetreatBody = c.RetreatReview.transform.Find("Body").GetComponent<Text>();
            c.ActionPause = .3f; c.ResultDelay = .5f;
            foreach (var cell in c.Cells) if (!cell.GetComponent<BattleCellHover>()) cell.gameObject.AddComponent<BattleCellHover>();
            string[] names = { "Melee", "Shoot", "Guard" }, descriptions = { string.Format(c.MeleeDescription, c.Rules.MeleeDamage),
                string.Format(c.ShootDescription, c.Rules.ShotDamage, c.Rules.ShotNoise), string.Format(c.GuardDescription, c.Rules.GuardHitPenalty, c.Rules.GuardStrikeReduction) };
            for (int i = 0; i < names.Length; i++) workspace.Find(names[i] + "/Description").GetComponent<Text>().text = descriptions[i];
            PrefabUtility.SaveAsPrefabAsset(root, P + "ExpeditionBattlePanel.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        return "Battle feel layer added: HUD/floating text/burst/slash/tracer/shield/threat prefabs, flash material, noise gauge, presentation + 11 clips.";
    }
}
