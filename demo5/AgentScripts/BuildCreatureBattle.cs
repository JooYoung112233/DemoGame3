using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Connects the 12 creature sprites (아트/크리쳐-v1, copied to Assets/Art/Creatures/V1) to the field battle.
// Run(): import settings, creates/fills Assets/Data/BattleCreatures.asset (keeps Inspector tuning of existing entries),
// wires the roster, creature sounds, hover-card counter line and portrait crops into the battle prefabs.
// Rebuild(): same, but resets every roster entry to the numbers below.
public static class BuildCreatureBattle
{
    const string Art = "Assets/Art/Creatures/V1/", Sfx = "Assets/Audio/Battle/Creatures/", RosterPath = "Assets/Data/BattleCreatures.asset", P = "Assets/Prefabs/Settlement/";

    sealed class Spec
    {
        public string Id, Name, AttackName, Windup, Trait, Counter, WindupSound, StrikeSound, ImpactSound;
        public CreatureAttack Attack; public CreatureGait Gait;
        public int Health, Armor, Front, ShotArmor, Evasion, Damage, Hit = 70, Start = -1, Hold; public bool Cover;
        public float Height, Hover, Pitch = 1; public Color Accent;
    }
    static Color C(float r, float g, float b) => new Color(r, g, b);
    // Provisional numbers (Inspector-editable afterwards). Committed strikes hit for sure, so they carry the bigger numbers;
    // anything that bites by chance stays at 1. Long moves (돌진·박치기·무너짐) and a missed 내려치기 leave a one-turn opening.
    static readonly Spec[] Specs =
    {
        new Spec { Id = "01-door-bearer", Name = "문지기", Attack = CreatureAttack.Shove, Gait = CreatureGait.Slow, Health = 6, Front = 2, Damage = 1, Start = 1, Cover = true, Height = 2f,
            AttackName = "밀치기", Windup = "문을 세움", Trait = "정면 방어 +2", Counter = "옆 줄에서 치면 문짝이 못 막음", WindupSound = "creature-creak", ImpactSound = "creature-thud", Accent = C(.8f, .68f, .52f), Pitch = .95f },
        new Spec { Id = "02-listener", Name = "귀기울임", Attack = CreatureAttack.Listen, Gait = CreatureGait.Walk, Health = 4, Damage = 2, Hit = 60, Start = 2, Height = 2.35f,
            AttackName = "내려치기", Windup = "귀 기울임", Trait = "총소리를 쫓음", Counter = "쏜 뒤 자리를 옮기면 헛칩니다", WindupSound = "creature-listen", StrikeSound = "creature-slam", Accent = C(.86f, .82f, .72f) },
        new Spec { Id = "03-under-table", Name = "식탁밑", Attack = CreatureAttack.Pounce, Gait = CreatureGait.Walk, Health = 3, Armor = 1, Damage = 2, Start = 1, Height = 1.4f,
            AttackName = "덮치기", Windup = "웅크림", Trait = "두 줄 안으로 도약", Counter = "한 줄 물러나면 닿지 않습니다", WindupSound = "creature-scrape", ImpactSound = "creature-clack", Accent = C(.76f, .6f, .44f) },
        new Spec { Id = "04-seam-hound", Name = "틈새개", Attack = CreatureAttack.Charge, Gait = CreatureGait.Fast, Health = 3, Damage = 2, Start = 2, Height = 1.5f,
            AttackName = "돌진", Windup = "몸을 낮춤", Trait = "가로줄 돌진 · 빈틈", Counter = "옆 줄로 비키면 헛돌진 · 빈틈", WindupSound = "creature-snarl", StrikeSound = "creature-dash", Accent = C(.92f, .88f, .76f) },
        new Spec { Id = "05-laundry", Name = "널린것", Attack = CreatureAttack.Veil, Gait = CreatureGait.Walk, Health = 4, ShotArmor = 1, Damage = 1, Start = 1, Height = 1.75f, Hover = .22f,
            AttackName = "휘감기", Windup = "천을 펼침", Trait = "사격 피해 -1", Counter = "천이 덮칠 줄에서 벗어나세요", WindupSound = "creature-flap", StrikeSound = "creature-flap", Accent = C(.72f, .78f, .84f), Pitch = .9f },
        new Spec { Id = "06-meter-keeper", Name = "계량원", Attack = CreatureAttack.Wire, Gait = CreatureGait.Slow, Health = 4, Armor = 1, Damage = 2, Start = 2, Height = 1.85f,
            AttackName = "방전", Windup = "전선 연결", Trait = "붙은 두 칸 감전", Counter = "연결된 두 칸에서 벗어나세요", WindupSound = "creature-hum", StrikeSound = "creature-zap", Accent = C(.64f, .9f, 1f) },
        new Spec { Id = "07-moth-nest", Name = "먼지둥지", Attack = CreatureAttack.Swarm, Gait = CreatureGait.Fast, Health = 3, Evasion = 20, Damage = 1, Hit = 65, Start = 2, Height = 1.5f, Hover = .35f,
            AttackName = "뒤덮기", Windup = "날갯짓", Trait = "옆까지 덮침", Counter = "붙어 서지 마세요 · 근접이 잘 듦", StrikeSound = "creature-flutter", Accent = C(.68f, .72f, .68f) },
        new Spec { Id = "08-puddle", Name = "고인사람", Attack = CreatureAttack.Grab, Gait = CreatureGait.Slow, Health = 5, ShotArmor = 1, Damage = 1, Start = 1, Height = 1.15f,
            AttackName = "발목잡기", Windup = "물이 번짐", Trait = "사격 피해 -1 · 묶음", Counter = "물결 번진 칸에서 벗어나세요", WindupSound = "creature-drip", StrikeSound = "creature-splash", Accent = C(.52f, .64f, .76f) },
        new Spec { Id = "09-stairback", Name = "계단등", Attack = CreatureAttack.Collapse, Gait = CreatureGait.Slow, Health = 6, Armor = 2, Damage = 2, Start = 1, Height = 1.75f,
            AttackName = "무너짐", Windup = "기울어짐", Trait = "방어 2 · 뒤에 빈틈", Counter = "기우는 쪽을 비우고 빈틈에 치기", WindupSound = "creature-creak", StrikeSound = "creature-crumble", Accent = C(.7f, .7f, .64f), Pitch = .85f },
        new Spec { Id = "10-twin-coat", Name = "겹친이웃", Attack = CreatureAttack.Twin, Gait = CreatureGait.Walk, Health = 5, Damage = 1, Start = 1, Height = 1.95f,
            AttackName = "겹쳐치기", Windup = "두 머리", Trait = "두 칸 연달아", Counter = "표시된 두 칸을 모두 피하세요", WindupSound = "creature-murmur", Accent = C(.82f, .8f, .76f) },
        new Spec { Id = "11-root-receiver", Name = "수신목", Attack = CreatureAttack.Broadcast, Gait = CreatureGait.Rooted, Health = 3, Damage = 0, Start = 2, Hold = 2, Height = 1.75f,
            AttackName = "방송", Windup = "잡음", Trait = "두 차례마다 소음", Counter = "소음을 키우기 전에 쓰러뜨리기", WindupSound = "creature-static", StrikeSound = "creature-static", Accent = C(.56f, .82f, .72f) },
        new Spec { Id = "12-bellied-cart", Name = "빈수레", Attack = CreatureAttack.Ram, Gait = CreatureGait.Walk, Health = 5, Armor = 1, Damage = 1, Start = 2, Height = 1.55f,
            AttackName = "박치기", Windup = "삐걱임", Trait = "가로줄 전체 밀침", Counter = "한 줄에 몰려 서지 마세요", WindupSound = "creature-rattle", StrikeSound = "creature-rattle", ImpactSound = "creature-clang", Accent = C(.74f, .74f, .7f) },
    };

    public static string Run() => Build(false);
    public static string Rebuild() => Build(true);

    static string Build(bool overwrite)
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop first");
        var log = new List<string>();
        // Large single sprites with mipmaps: they are drawn far below their source size, so mip levels keep the edges clean.
        foreach (var s in Specs)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(Art + s.Id + ".png") ?? throw new Exception("Missing " + s.Id);
            if (imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single || !imp.mipmapEnabled || !imp.alphaIsTransparency)
            {
                imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single; imp.mipmapEnabled = true;
                imp.mipmapFilter = TextureImporterMipFilter.KaiserFilter; imp.alphaIsTransparency = true; imp.spritePixelsPerUnit = 100;
                imp.SaveAndReimport(); log.Add("import " + s.Id);
            }
        }
        var roster = AssetDatabase.LoadAssetAtPath<BattleCreatureRoster>(RosterPath);
        if (!roster) { roster = ScriptableObject.CreateInstance<BattleCreatureRoster>(); AssetDatabase.CreateAsset(roster, RosterPath); log.Add("created " + RosterPath); }
        var crops = new List<BattlePortraitFit.Crop>();
        foreach (var s in Specs)
        {
            var c = roster.Find(s.Id); bool fresh = c == null;
            if (fresh) { c = new BattleCreature(); roster.Creatures.Add(c); }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + s.Id + ".png") ?? throw new Exception("No sprite " + s.Id);
            c.Id = s.Id; c.Body = sprite; c.Visible = VisibleRect(Art + s.Id + ".png");
            if (fresh || overwrite)
            {
                c.Name = s.Name; c.Attack = s.Attack; c.Gait = s.Gait; c.Health = s.Health; c.Armor = s.Armor; c.FrontArmor = s.Front; c.ShotArmor = s.ShotArmor; c.ShotEvasion = s.Evasion;
                c.Damage = s.Damage; c.HitChance = s.Hit; c.StartDepth = s.Start; c.HoldDepth = s.Hold; c.Cover = s.Cover; c.Height = s.Height; c.Hover = s.Hover; c.Weight = 1;
                c.AttackName = s.AttackName; c.WindupName = s.Windup; c.Trait = s.Trait; c.Counter = s.Counter; c.Accent = s.Accent; c.Pitch = s.Pitch;
                c.WindupSound = Clip(s.WindupSound); c.StrikeSound = Clip(s.StrikeSound); c.ImpactSound = Clip(s.ImpactSound);
            }
            else
            {
                if (!c.WindupSound) c.WindupSound = Clip(s.WindupSound); if (!c.StrikeSound) c.StrikeSound = Clip(s.StrikeSound); if (!c.ImpactSound) c.ImpactSound = Clip(s.ImpactSound);
            }
            // Portrait window: the top of the body, about as tall as it is wide, so tall creatures show head and shoulders.
            var v = c.Visible; float w = sprite.rect.width, h = sprite.rect.height, window = Mathf.Min(v.height, v.width * 1.1f);
            crops.Add(new BattlePortraitFit.Crop { Sprite = sprite, Area = new Rect(v.x / w, (v.yMax - window) / h, v.width / w, window / h) });
        }
        EditorUtility.SetDirty(roster); AssetDatabase.SaveAssets();
        log.Add("roster " + roster.Creatures.Count + " types");

        foreach (var name in new[] { "BattleTurnCard", "BattleUnitDetails", "BattleTargetDetails" })
        {
            var root = PrefabUtility.LoadPrefabContents(P + name + ".prefab");
            try { int added = AddCrops(root, crops); if (added > 0) { PrefabUtility.SaveAsPrefabAsset(root, P + name + ".prefab"); log.Add(name + " crops +" + added); } }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        var tipRoot = PrefabUtility.LoadPrefabContents(P + "BattleAimTooltip.prefab");
        try
        {
            var tip = tipRoot.GetComponent<BattleAimTooltip>();
            if (!tip.Trait)
            {
                var source = tip.Intent; var block = tip.IntentBlock.transform;
                var go = new GameObject("Trait", typeof(RectTransform), typeof(Text)); go.transform.SetParent(block, false);
                var rt = (RectTransform)go.transform; var from = source.rectTransform;
                rt.anchorMin = from.anchorMin; rt.anchorMax = from.anchorMax; rt.pivot = from.pivot;
                rt.anchoredPosition = from.anchoredPosition + new Vector2(0, -34); rt.sizeDelta = new Vector2(from.sizeDelta.x, 30);
                var text = go.GetComponent<Text>(); text.font = source.font; text.fontSize = 17; text.alignment = source.alignment; text.color = new Color(.86f, .82f, .72f);
                text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; text.raycastTarget = false; text.text = "대응 요령";
                tip.Trait = text; PrefabUtility.SaveAsPrefabAsset(tipRoot, P + "BattleAimTooltip.prefab"); log.Add("hover card counter line");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(tipRoot); }

        var panelRoot = PrefabUtility.LoadPrefabContents(P + "ExpeditionBattlePanel.prefab");
        try
        {
            var panel = panelRoot.GetComponent<ExpeditionBattlePanel>(); var changes = new List<string>();
            if (panel.Creatures != roster) { panel.Creatures = roster; changes.Add("roster"); }
            var show = panel.Presentation;
            if (show && !show.Collide) { show.Collide = Clip("creature-collide"); changes.Add("collide sound"); }
            // The guard card keeps its one-line wording (the strike effect is explained in the hint when a marked cell is under the actor).
            if (panel.GuardDescription == "물림 -{0}%p · 예고 -{1}") { panel.GuardDescription = "물릴 확률 -{0}%p"; changes.Add("guard card wording restored"); }
            string threat = panel.RetreatThreatBody.Replace("감염자 {0}마리가", "적 {0}마리가"), quiet = panel.RetreatQuietBody.Replace("붙어 있는 감염자가", "붙어 있는 적이");
            if (threat != panel.RetreatThreatBody || quiet != panel.RetreatQuietBody) { panel.RetreatThreatBody = threat; panel.RetreatQuietBody = quiet; changes.Add("retreat wording"); }
            int added = AddCrops(panelRoot, crops); if (added > 0) changes.Add("nested portrait crops +" + added);
            if (changes.Count > 0) { PrefabUtility.SaveAsPrefabAsset(panelRoot, P + "ExpeditionBattlePanel.prefab"); log.Add("panel: " + string.Join(", ", changes)); }
        }
        finally { PrefabUtility.UnloadPrefabContents(panelRoot); AssetDatabase.SaveAssets(); }
        return string.Join(" · ", log);
    }
    static int AddCrops(GameObject root, List<BattlePortraitFit.Crop> crops)
    {
        int added = 0;
        foreach (var fit in root.GetComponentsInChildren<BattlePortraitFit>(true))
        {
            var list = (fit.Crops ?? new BattlePortraitFit.Crop[0]).ToList();
            foreach (var crop in crops)
            {
                int at = list.FindIndex(x => x.Sprite == crop.Sprite);
                if (at < 0) { list.Add(crop); added++; } else if (list[at].Area != crop.Area) { list[at] = crop; added++; }
            }
            fit.Crops = list.ToArray(); EditorUtility.SetDirty(fit);
        }
        return added;
    }
    static AudioClip Clip(string name) => string.IsNullOrEmpty(name) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(Sfx + name + ".wav") ?? throw new Exception("No sound " + name);
    // Opaque bounds of the original PNG (alpha >= 128), bottom-left origin like sprite pixel rects.
    static Rect VisibleRect(string path)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            tex.LoadImage(File.ReadAllBytes(path)); var px = tex.GetPixels32(); int w = tex.width, h = tex.height, x0 = w, y0 = h, x1 = -1, y1 = -1;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (px[y * w + x].a >= 128) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
            if (x1 < 0) throw new Exception("Empty sprite " + path);
            return Rect.MinMaxRect(x0, y0, x1 + 1, y1 + 1);
        }
        finally { Object.DestroyImmediate(tex); }
    }
}
