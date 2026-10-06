using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Retire the supplies item (보급품; 2026-09-25 user decision '없애 일단', 기획/밸런스-1차.md §9-6 #5).
// Code side (already compiled before this runs): save v14 strips supplies from older saves before the first Validate,
// SettlementController.SupplyCount is gone, the HUD refreshes through RefreshResources.
// Prefab side (this builder). Every step is idempotent, logs before → after, and the whole run prints 'Already applied.' when nothing changes:
//  (f) ExpeditionLootPanel base Sites[0].Drops[0] supplies → wood IN PLACE (never resized or reordered: SettlementScreen overrides that row
//      and inherits other fields by index). The effective SettlementScreen table is snapshotted before/after and must not change.
//  (a) InventoryPanel.Items without supplies (16 → 15). Nothing overrides Items by index.
//  (b) ResourceHud: Icon_0/Value_0 (supplies) deleted; Icon_1/Value_1 → Icon_Ammo/Value_Ammo, Icon_2/Value_2 → Icon_Party/Value_Party
//      (references go by fileID, so AmmoCount/PartyCount survive); 18/56 and 150/188; root 278×72.
//  (c) SettlementScreen Main/ResourceHud instance at 1430,24,278,72 (right edge 1708, 16px before the menu at 1724); the re-save drops the stale
//      'SupplyCount:' key and the dangling stripped Value_0 reference.
//  (d) ExpeditionPackingPanel Workspace/Tab1/Label '보급품' → '식량' (the food/water category; InventoryPanel Tab_1 uses the same name).
//      A default the user can decline: approved mock 03-v2 has 전체/물/치료/도구/기타.
//  (e) HomeDetails Resources default text without '시작 보급품' (the runtime overwrites it; this is the prefab default).
// icon-supplies.png stays: it is the food/can/meal item icon (CookingPanel, CraftWorkPanel, ExpeditionPlanPanel, ReturnItemRow, HomeDetails).
// Refuses in Play mode, with a dirty active scene, or before the code changes compiled (SupplyCount still present or save < v14).
// Run order (one SettlementScreen builder at a time): after StandardizePopupFrames → FixPopupContentAlignment → PolishRemainingUI → BuildDeferredUI
// → BuildBalance1 and after the crate-scrap change; then re-run BuildTutorialFocus (expect 'Already applied.'). Then VerifyRetireSupplies.
// Never run BuildSettlementInventory, BuildLoot, BuildSettlement, PolishSettlement or PolishSettlementHud after it (they are guarded).
public static class BuildRetireSupplies
{
    const string P = "Assets/Prefabs/Settlement/";
    const string InventoryPath = P + "InventoryPanel.prefab", HudPath = P + "ResourceHud.prefab", ScreenPath = P + "SettlementScreen.prefab",
        PackingPath = P + "ExpeditionPackingPanel.prefab", LootPath = P + "ExpeditionLootPanel.prefab", ArrivalPath = P + "ExpeditionArrivalPanel.prefab",
        DetailsPath = "Assets/Prefabs/HomeSelection/HomeDetails.prefab", HomeScreenPath = "Assets/Prefabs/HomeSelection/HomeSelectionScreen.prefab";
    public const string Retired = "supplies", Replacement = "wood";
    public const string FoodTab = "식량";
    public const string HomeDefault = "남은 물자는 수색으로 확보하고\n시설은 하나씩 정리합니다.";
    // HUD slots: (name, old name, icon x). Icon 28×28 at y 22, value 70×52 at y 10 and x + 38 (centres both at y 36).
    public static readonly (string icon, string value, string oldIcon, string oldValue, float x)[] Slots =
        { ("Icon_Ammo", "Value_Ammo", "Icon_1", "Value_1", 18), ("Icon_Party", "Value_Party", "Icon_2", "Value_2", 150) };
    public static readonly Rect HudRect = new Rect(1430, 24, 278, 72);
    static readonly List<string> log = new List<string>();

    public static string Run()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Stop Play and preserve the scene first");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && new[] { InventoryPath, HudPath, ScreenPath, PackingPath, LootPath, ArrivalPath, DetailsPath, HomeScreenPath }.Contains(stage.assetPath)) throw new Exception("Close Prefab Mode for " + stage.assetPath + " first");
        if (typeof(SettlementController).GetField("SupplyCount") != null || CampaignPersistence.CurrentVersion < 14)
            throw new Exception("Compile the supplies code changes first (SettlementController.SupplyCount removed, save v14 with DropRetiredItems); otherwise old saves with supplies become unloadable");
        log.Clear();
        string before = Snapshot(ScreenAsset().ArrivalPanel.Loot);

        bool loot = Loot(), items = Inventory(), hud = Hud(), packing = Packing();
        if (loot) Reimport(ArrivalPath);
        if (loot || items || hud || packing) Reimport(ScreenPath);
        ScreenHud();
        if (Details()) Reimport(HomeScreenPath);
        if (log.Count > 0) AssetDatabase.SaveAssets();

        string after = Snapshot(ScreenAsset().ArrivalPanel.Loot);
        if (after != before) throw new Exception("The effective SettlementScreen loot table changed (it must not):\nbefore\n" + before + "\nafter\n" + after);
        CheckDropIds();
        return log.Count == 0 ? "Already applied." : "Applied: " + string.Join("; ", log);
    }

    // (f) Base loot: the one supplies row becomes wood in place. SettlementScreen's site 0 overrides this row's Id, so the game never read it.
    static bool Loot()
    {
        var root = PrefabUtility.LoadPrefabContents(LootPath);
        try
        {
            var l = root.GetComponent<ExpeditionLootPanel>(); if (!l) throw new Exception("ExpeditionLootPanel missing on " + LootPath);
            if (l.Sites == null || l.Sites.Length == 0 || l.Sites[0].Drops == null || l.Sites[0].Drops.Length == 0) throw new Exception("Base Sites[0].Drops missing");
            var rows = new List<string>();
            for (int i = 0; i < l.Sites.Length; i++) for (int k = 0; k < (l.Sites[i].Drops?.Length ?? 0); k++) if (l.Sites[i].Drops[k].Id == Retired && !(i == 0 && k == 0)) rows.Add("[" + i + "][" + k + "]");
            if (rows.Count > 0) throw new Exception("Unexpected base supplies rows " + string.Join(",", rows) + ": only Sites[0].Drops[0] is known; check the table by hand");
            var d = l.Sites[0].Drops[0]; if (d.Id != Retired) return false;
            // Only safe while SettlementScreen overrides this row's Id (then its effective table is unchanged); refuse before saving anything otherwise.
            if (!File.ReadAllText(ScreenPath).Contains("propertyPath: Sites.Array.data[0].Drops.Array.data[0].Id"))
                throw new Exception("SettlementScreen no longer overrides Sites[0].Drops[0].Id: the base row would become live; check the loot table by hand");
            log.Add("ExpeditionLootPanel 기본 Sites[0].Drops[0] " + d.Id + " " + d.Count + "@" + d.Chance + " → " + Replacement + " " + d.Count + "@" + d.Chance + " (제자리, 행 " + l.Sites[0].Drops.Length + "개 유지)");
            d.Id = Replacement; EditorUtility.SetDirty(l);
            PrefabUtility.SaveAsPrefabAsset(root, LootPath); return true;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // (a) The item catalog (also the save catalog's item ids).
    static bool Inventory()
    {
        var root = PrefabUtility.LoadPrefabContents(InventoryPath);
        try
        {
            var p = root.GetComponent<SettlementInventoryPanel>(); if (!p || p.Items == null) throw new Exception("SettlementInventoryPanel.Items missing on " + InventoryPath);
            if (!p.Items.Any(i => i.Id == Retired)) return false;
            int n = p.Items.Length; p.Items = p.Items.Where(i => i.Id != Retired).ToArray();
            log.Add("InventoryPanel.Items " + n + " → " + p.Items.Length + " (supplies 제거)");
            EditorUtility.SetDirty(p); PrefabUtility.SaveAsPrefabAsset(root, InventoryPath); return true;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // (b) Two slots, ammo and party, right-aligned by the instance position (c).
    static bool Hud()
    {
        var root = PrefabUtility.LoadPrefabContents(HudPath); int start = log.Count;
        try
        {
            var t = root.transform;
            foreach (var n in new[] { "Icon_0", "Value_0" }) { var old = t.Find(n); if (old) { log.Add("ResourceHud/" + n + " 삭제 (보급품 칸)"); Object.DestroyImmediate(old.gameObject); } }
            foreach (var s in Slots)
            {
                var icon = Child(t, s.icon, s.oldIcon); var value = Child(t, s.value, s.oldValue);
                Place(icon, s.x, 22, 28, 28, "ResourceHud/" + s.icon); Place(value, s.x + 38, 10, 70, 52, "ResourceHud/" + s.value);
                var text = value.GetComponent<Text>(); if (!text) throw new Exception("ResourceHud/" + s.value + " has no Text");
                if (!text.resizeTextForBestFit || text.resizeTextMinSize != 20 || text.resizeTextMaxSize != 28 || text.alignment != TextAnchor.MiddleLeft)
                {
                    log.Add("ResourceHud/" + s.value + " 글자 맞춤 " + (text.resizeTextForBestFit ? text.resizeTextMinSize + "-" + text.resizeTextMaxSize : "없음") + " " + text.alignment + " → 20-28 MiddleLeft");
                    text.resizeTextForBestFit = true; text.resizeTextMinSize = 20; text.resizeTextMaxSize = 28; text.alignment = TextAnchor.MiddleLeft; EditorUtility.SetDirty(text);
                }
            }
            var r = (RectTransform)t;
            if (r.sizeDelta != HudRect.size) { log.Add("ResourceHud 루트 " + r.sizeDelta.x + "×" + r.sizeDelta.y + " → " + HudRect.width + "×" + HudRect.height); r.sizeDelta = HudRect.size; }
            var extra = t.Cast<Transform>().Select(c => c.name).Except(Slots.SelectMany(s => new[] { s.icon, s.value })).ToArray();
            if (extra.Length > 0) throw new Exception("ResourceHud has unexpected children: " + string.Join(", ", extra));
            bool changed = log.Count > start; if (changed) PrefabUtility.SaveAsPrefabAsset(root, HudPath); return changed;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static RectTransform Child(Transform t, string name, string old)
    {
        var c = t.Find(name); if (c) return (RectTransform)c;
        c = t.Find(old); if (!c) throw new Exception("ResourceHud/" + old + " (or " + name + ") missing");
        log.Add("ResourceHud/" + old + " → " + name); c.name = name; return (RectTransform)c;
    }

    // (c) The instance overrides its own size and position, so the base root size alone would change nothing.
    static void ScreenHud()
    {
        bool stale = HasSupplyKey(); var root = PrefabUtility.LoadPrefabContents(ScreenPath); int start = log.Count;
        try
        {
            var c = root.GetComponent<SettlementController>(); if (!c || !c.Main) throw new Exception("SettlementController/Main missing on " + ScreenPath);
            var hud = c.Main.transform.Find("ResourceHud") as RectTransform; if (!hud) throw new Exception("Main/ResourceHud missing");
            var source = PrefabUtility.GetCorrespondingObjectFromSource(hud.gameObject);
            if (!source || AssetDatabase.GetAssetPath(source) != HudPath) throw new Exception("Main/ResourceHud is not a ResourceHud.prefab instance");
            if (c.GameMenu && c.GameMenu.OpenButton)
            {
                var menu = (RectTransform)c.GameMenu.OpenButton.transform; var corner = new Vector2(0, 1);
                if (menu.anchorMin == corner && menu.anchorMax == corner && menu.pivot == corner && menu.anchoredPosition.x < HudRect.xMax)
                    throw new Exception("The menu button starts at x " + menu.anchoredPosition.x + ", left of the HUD's right edge " + HudRect.xMax + ": re-plan the HUD position");
            }
            Place(hud, HudRect.x, HudRect.y, HudRect.width, HudRect.height, "SettlementScreen Main/ResourceHud");
            if (!c.AmmoCount || c.AmmoCount.name != "Value_Ammo" || c.AmmoCount.transform.parent != hud) throw new Exception("AmmoCount does not point at ResourceHud/Value_Ammo: " + (c.AmmoCount ? c.AmmoCount.name : "null"));
            if (!c.PartyCount || c.PartyCount.name != "Value_Party" || c.PartyCount.transform.parent != hud) throw new Exception("PartyCount does not point at ResourceHud/Value_Party: " + (c.PartyCount ? c.PartyCount.name : "null"));
            if (stale) log.Add("SettlementScreen 남은 'SupplyCount' 키와 끊긴 Value_0 참조 정리 (재저장)");
            if (log.Count > start) PrefabUtility.SaveAsPrefabAsset(root, ScreenPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        if (HasSupplyKey()) throw new Exception("SettlementScreen still serializes SupplyCount after the re-save");
    }
    static bool HasSupplyKey() => File.ReadAllText(ScreenPath).Contains("SupplyCount:");

    // (d) The packing tab of category 1 (food and water).
    static bool Packing()
    {
        var root = PrefabUtility.LoadPrefabContents(PackingPath);
        try
        {
            var p = root.GetComponent<ExpeditionPackingPanel>(); if (!p) throw new Exception("ExpeditionPackingPanel missing on " + PackingPath);
            if (p.Tabs == null || p.Tabs.Length < 2 || !p.Tabs[1] || p.Tabs[1].name != "Tab1") throw new Exception("ExpeditionPackingPanel.Tabs[1] is not Tab1");
            var t = p.Tabs[1].transform.Find("Label"); var label = t ? t.GetComponent<Text>() : null; if (!label) throw new Exception("Tab1/Label missing");
            if (label.text == FoodTab) return false;
            log.Add("ExpeditionPackingPanel Tab1 라벨 '" + label.text + "' → '" + FoodTab + "'");
            label.text = FoodTab; EditorUtility.SetDirty(label); PrefabUtility.SaveAsPrefabAsset(root, PackingPath); return true;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // (e) HomeDetails default (HomeSelectionController writes the same fallback when no site is selected).
    static bool Details()
    {
        var root = PrefabUtility.LoadPrefabContents(DetailsPath);
        try
        {
            var t = root.transform.Find("Resources"); var text = t ? t.GetComponent<Text>() : null; if (!text) throw new Exception("HomeDetails/Resources missing");
            if (text.text == HomeDefault) return false;
            log.Add("HomeDetails Resources 기본 문구 '" + text.text.Replace("\n", " / ") + "' → '" + HomeDefault.Replace("\n", " / ") + "'");
            text.text = HomeDefault; EditorUtility.SetDirty(text); PrefabUtility.SaveAsPrefabAsset(root, DetailsPath); return true;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ---- checks ----
    static SettlementController ScreenAsset()
    {
        var g = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath); var c = g ? g.GetComponent<SettlementController>() : null;
        if (!c || !c.ArrivalPanel || !c.ArrivalPanel.Loot || !c.InventoryPanel) throw new Exception("SettlementScreen references missing"); return c;
    }
    public static string Snapshot(ExpeditionLootPanel l) => l.Sites == null ? "null" : string.Join("\n", l.Sites.Select((s, i) => "[" + i + "] room " + s.Room + " tool " + s.RequiredTool + " · " + Drops(s.Drops)));
    static string Drops(ExpeditionLootPanel.Drop[] d) => d == null ? "" : string.Join(" ", d.Select(x => x.Id + " " + x.Count + "@" + x.Chance));
    // Every drop id in the base, ArrivalPanel and SettlementScreen tables must be an item (Validate rejects unknown ids in saved loot).
    static void CheckDropIds()
    {
        var inv = AssetDatabase.LoadAssetAtPath<GameObject>(InventoryPath); var panel = inv ? inv.GetComponent<SettlementInventoryPanel>() : null;
        if (!panel || panel.Items == null) throw new Exception("InventoryPanel.Items missing");
        var ids = new HashSet<string>(panel.Items.Select(i => i.Id)); var bad = new List<string>();
        if (ids.Contains(Retired)) bad.Add("InventoryPanel.Items still lists supplies");
        var lootAsset = AssetDatabase.LoadAssetAtPath<GameObject>(LootPath); var arrivalAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ArrivalPath);
        var arrival = arrivalAsset ? arrivalAsset.GetComponent<ExpeditionArrivalPanel>() : null;
        var tables = new (string name, ExpeditionLootPanel loot)[] {
            ("ExpeditionLootPanel", lootAsset ? lootAsset.GetComponent<ExpeditionLootPanel>() : null),
            ("ExpeditionArrivalPanel", arrival ? arrival.Loot : null),
            ("SettlementScreen", ScreenAsset().ArrivalPanel.Loot) };
        foreach (var (name, loot) in tables)
        {
            if (!loot || loot.Sites == null) { bad.Add(name + " loot table missing"); continue; }
            for (int i = 0; i < loot.Sites.Length; i++) foreach (var d in loot.Sites[i].Drops ?? new ExpeditionLootPanel.Drop[0]) if (!ids.Contains(d.Id)) bad.Add(name + " Sites[" + i + "] '" + d.Id + "'");
        }
        if (bad.Count > 0) throw new Exception("Drop ids missing from InventoryPanel.Items: " + string.Join(", ", bad));
    }

    // ---- helpers ----
    static void Reimport(string path) => AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    // Top-left anchored rect in the builders' convention: x, y down from the top, width, height.
    static void Place(RectTransform r, float x, float y, float w, float h, string what)
    {
        var corner = new Vector2(0, 1); var pos = new Vector2(x, -y); var size = new Vector2(w, h);
        if (r.anchorMin == corner && r.anchorMax == corner && r.pivot == corner && r.anchoredPosition == pos && r.sizeDelta == size) return;
        log.Add(what + " " + r.anchoredPosition.x + "," + (-r.anchoredPosition.y) + "," + r.sizeDelta.x + "," + r.sizeDelta.y + " → " + x + "," + y + "," + w + "," + h);
        r.anchorMin = r.anchorMax = r.pivot = corner; r.anchoredPosition = pos; r.sizeDelta = size;
    }
}
