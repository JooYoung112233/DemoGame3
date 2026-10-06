using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using Demo5.NightRun;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// The supplies item (보급품) retired on 2026-09-25 (BuildRetireSupplies + save v14). Run after BuildRetireSupplies.Run and BuildTutorialFocus.Run.
// Static: edit mode (or play), assets and sources only.
// Runtime: play mode. Starts a new game in the real scenes (PartySelection → HomeSelection → Settlement), so run it on its own.
// Save migration of old saves: VerifySiteSave.RetiredSupplies.
public static class VerifyRetireSupplies
{
    const string P = "Assets/Prefabs/Settlement/", ScreenPath = P + "SettlementScreen.prefab", HudPath = P + "ResourceHud.prefab", InventoryPath = P + "InventoryPanel.prefab",
        PackingPath = P + "ExpeditionPackingPanel.prefab", LootPath = P + "ExpeditionLootPanel.prefab", ArrivalPath = P + "ExpeditionArrivalPanel.prefab",
        DetailsPath = "Assets/Prefabs/HomeSelection/HomeDetails.prefab", HomeScreenPath = "Assets/Prefabs/HomeSelection/HomeSelectionScreen.prefab",
        SuppliesIcon = "Assets/Art/HomeSelection/icon-supplies.png", AmmoIcon = "Assets/Art/HomeSelection/icon-ammo.png", PartyIcon = "Assets/Art/PartySelection/icon-party.png",
        Shots = "Assets/Screenshots/RetireSupplies/";
    const string Word = "보급품", FoodTab = "식량", HomeDefault = "남은 물자는 수색으로 확보하고\n시설은 하나씩 정리합니다.";
    static readonly string[] KeptItems = { "ammo", "wood", "rope", "nails", "scrap", "cloth", "prybar", "flashlight", "food", "water", "can", "meal", "ration", "raw-water", "bandage" };
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static T Root<T>(string path) where T : Component { var g = AssetDatabase.LoadAssetAtPath<GameObject>(path); var c = g ? g.GetComponent<T>() : null; Check(c, typeof(T).Name + " missing on " + path); return c; }
    static string Rect(RectTransform r) => r.anchoredPosition.x + "," + (-r.anchoredPosition.y) + "," + r.sizeDelta.x + "," + r.sizeDelta.y;
    static bool TopLeft(RectTransform r) => r.anchorMin == new Vector2(0, 1) && r.anchorMax == new Vector2(0, 1) && r.pivot == new Vector2(0, 1);

    // ---- static (assets and sources) ----
    public static string Static()
    {
        var done = new List<string>();
        // Code: the counter field is gone and saves are v14.
        Check(typeof(SettlementController).GetField("SupplyCount") == null, "SettlementController.SupplyCount still exists");
        Check(CampaignPersistence.CurrentVersion == 15, "Save version " + CampaignPersistence.CurrentVersion);
        var literal = new List<string>();
        foreach (var file in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(file); string name = Path.GetFileName(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("SupplyCount")) literal.Add(name + ":" + (i + 1) + " SupplyCount");
                // Allowed: the retired-id list of the save migration, and the tutorial guide's no-op filter (owned by the tutorial work).
                if (lines[i].Contains("\"supplies\"") && !(name == "CampaignSaveData.cs" && lines[i].Contains("RetiredItemIds")) && name != "SettlementTutorialGuide.cs") literal.Add(name + ":" + (i + 1) + " \"supplies\"");
            }
        }
        Check(literal.Count == 0, "Leftover supplies code: " + string.Join(", ", literal));
        done.Add("code (no SupplyCount, only RetiredItemIds names supplies, save v14)");

        // Items: supplies gone, the other 15 kept; the shared icon stays on food/can/meal.
        var inv = Root<SettlementInventoryPanel>(InventoryPath);
        Check(!inv.Items.Any(i => i.Id == "supplies" || (i.Name ?? "").Contains(Word)), "InventoryPanel still lists supplies");
        var missing = KeptItems.Where(id => !inv.Items.Any(i => i.Id == id)).ToArray(); Check(missing.Length == 0, "Items lost: " + string.Join(",", missing));
        Check(File.Exists(SuppliesIcon) && AssetDatabase.LoadAssetAtPath<Sprite>(SuppliesIcon), "icon-supplies.png must stay (food/can/meal icon)");
        foreach (var id in new[] { "food", "can", "meal" }) Check(AssetDatabase.GetAssetPath(inv.Items.First(i => i.Id == id).Icon) == SuppliesIcon, id + " icon changed");
        done.Add("InventoryPanel.Items " + inv.Items.Length + " without supplies, icon-supplies kept on food/can/meal");

        // ResourceHud: 2 pairs, ammo and party, 18/56 and 150/188, root 278×72, icon and value centres on y 36.
        var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath); Check(hud, "ResourceHud missing");
        var names = hud.transform.Cast<Transform>().Select(t => t.name).ToArray();
        Check(names.OrderBy(n => n).SequenceEqual(new[] { "Icon_Ammo", "Icon_Party", "Value_Ammo", "Value_Party" }), "ResourceHud children: " + string.Join(",", names));
        Check(((RectTransform)hud.transform).sizeDelta == new Vector2(278, 72), "ResourceHud root " + ((RectTransform)hud.transform).sizeDelta);
        foreach (var (icon, value, x, sprite) in new[] { ("Icon_Ammo", "Value_Ammo", 18f, AmmoIcon), ("Icon_Party", "Value_Party", 150f, PartyIcon) })
        {
            var ir = (RectTransform)hud.transform.Find(icon); var vr = (RectTransform)hud.transform.Find(value);
            Check(TopLeft(ir) && TopLeft(vr) && Rect(ir) == x + ",22,28,28" && Rect(vr) == (x + 38) + ",10,70,52", icon + " " + Rect(ir) + " / " + value + " " + Rect(vr));
            Check(Mathf.Approximately(-ir.anchoredPosition.y + ir.sizeDelta.y / 2, -vr.anchoredPosition.y + vr.sizeDelta.y / 2), icon + " and " + value + " centres differ");
            var im = ir.GetComponent<Image>(); Check(im && AssetDatabase.GetAssetPath(im.sprite) == sprite, icon + " sprite " + (im ? AssetDatabase.GetAssetPath(im.sprite) : "none"));
            var t = vr.GetComponent<Text>(); Check(t && t.resizeTextForBestFit && t.resizeTextMinSize == 20 && t.resizeTextMaxSize == 28 && t.alignment == TextAnchor.MiddleLeft, value + " text fit");
        }
        Check(!hud.GetComponentsInChildren<Image>(true).Any(i => AssetDatabase.GetAssetPath(i.sprite) == SuppliesIcon), "ResourceHud still shows the supplies icon");
        done.Add("ResourceHud 2 slots (ammo 18/56, party 150/188) on 278×72");

        // SettlementScreen: the instance at 1430,24,278,72, 16px before the menu on the same line; references on the renamed children.
        var c = Root<SettlementController>(ScreenPath); var hr = c.Main.transform.Find("ResourceHud") as RectTransform; Check(hr, "Main/ResourceHud missing");
        Check(TopLeft(hr) && Rect(hr) == "1430,24,278,72", "HUD instance " + Rect(hr));
        Check(c.GameMenu && c.GameMenu.OpenButton, "Menu button missing"); var menu = (RectTransform)c.GameMenu.OpenButton.transform;
        Check(TopLeft(menu) && Mathf.Approximately(menu.anchoredPosition.x - (1430 + 278), 16) && Mathf.Approximately(menu.anchoredPosition.y, hr.anchoredPosition.y) && Mathf.Approximately(menu.sizeDelta.y, hr.sizeDelta.y), "Menu " + Rect(menu) + " vs HUD right edge 1708");
        Check(c.AmmoCount && c.AmmoCount.name == "Value_Ammo" && c.AmmoCount.transform.parent == hr, "AmmoCount → " + (c.AmmoCount ? c.AmmoCount.name : "null"));
        Check(c.PartyCount && c.PartyCount.name == "Value_Party" && c.PartyCount.transform.parent == hr, "PartyCount → " + (c.PartyCount ? c.PartyCount.name : "null"));
        Check(!File.ReadAllText(ScreenPath).Contains("SupplyCount"), "SettlementScreen still serializes SupplyCount (re-save it through BuildRetireSupplies)");
        done.Add("HUD 1430,24,278,72 (right edge 1708, 16px to the menu at " + menu.anchoredPosition.x + "), Ammo/PartyCount wired");

        // Texts: packing tab, HomeDetails default, and no '보급품' in any Text of the touched screens.
        var packing = Root<ExpeditionPackingPanel>(PackingPath);
        Check(Label(packing.Tabs[1]) == FoodTab && c.PackingPanel && Label(c.PackingPanel.Tabs[1]) == FoodTab, "Packing Tab1 '" + Label(packing.Tabs[1]) + "' / in screen '" + (c.PackingPanel ? Label(c.PackingPanel.Tabs[1]) : "none") + "'");
        var details = AssetDatabase.LoadAssetAtPath<GameObject>(DetailsPath); var dr = details ? details.transform.Find("Resources") : null;
        Check(dr && dr.GetComponent<Text>().text == HomeDefault, "HomeDetails Resources default");
        var home = Root<HomeSelectionController>(HomeScreenPath); Check(home.Resources && home.Resources.text == HomeDefault, "HomeSelectionScreen Resources '" + (home.Resources ? home.Resources.text : "null") + "'");
        var worded = new List<string>();
        foreach (var path in new[] { ScreenPath, InventoryPath, PackingPath, HomeScreenPath, DetailsPath })
            foreach (var t in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<Text>(true)) if ((t.text ?? "").Contains(Word)) worded.Add(Path.GetFileNameWithoutExtension(path) + ":" + t.name);
        Check(worded.Count == 0, "Texts still say 보급품: " + string.Join(", ", worded));
        done.Add("packing Tab1 '식량', HomeDetails default, no '보급품' Text");

        // Loot: no supplies drop anywhere; the base row was replaced in place.
        var ids = new HashSet<string>(inv.Items.Select(i => i.Id)); var bad = new List<string>();
        var arrival = Root<ExpeditionArrivalPanel>(ArrivalPath); var baseLoot = Root<ExpeditionLootPanel>(LootPath);
        foreach (var (name, loot) in new[] { ("base", baseLoot), ("arrival", arrival.Loot), ("screen", c.ArrivalPanel ? c.ArrivalPanel.Loot : null) })
            if (!loot || loot.Sites == null) bad.Add(name + " loot table missing");
            else for (int i = 0; i < loot.Sites.Length; i++) foreach (var d in loot.Sites[i].Drops ?? new ExpeditionLootPanel.Drop[0]) if (!ids.Contains(d.Id)) bad.Add(name + "[" + i + "] " + d.Id);
        Check(bad.Count == 0, "Drop ids not in Items: " + string.Join(", ", bad));
        Check(baseLoot.Sites[0].Drops[0].Id == "wood", "Base Sites[0].Drops[0] " + baseLoot.Sites[0].Drops[0].Id);
        done.Add("loot ids all items, base Sites[0].Drops[0] wood");
        return "PASS static · " + string.Join(" · ", done);
    }
    static string Label(Button b) { var t = b ? b.transform.Find("Label") : null; var text = t ? t.GetComponent<Text>() : null; return text ? text.text : "(none)"; }

    // ---- runtime (play mode, real scenes) ----
    public static async Task<string> Runtime()
    {
        Check(Application.isPlaying, "Play first");
        var log = new List<string>();

        // 1. New game: no supplies anywhere, ammo 0, party 2.
        SceneManager.LoadScene("PartySelection"); await Task.Delay(800);
        var p = Object.FindAnyObjectByType<PartySelectionController>(); if (PartySelectionSession.Selected.Count != 2) { PartySelectionSession.Clear(); p.Refresh(); } await Task.Delay(100);
        foreach (var card in p.Cards.Where(x => x.gameObject.activeInHierarchy && x.Button.IsInteractable())) { if (PartySelectionSession.Selected.Count >= 2) break; await Tap(card.Button); }
        await Tap(p.Continue); await Task.Delay(800);
        var h = Object.FindAnyObjectByType<HomeSelectionController>(); await Tap(h.Cards[0].Button);
        Check(!h.Resources.text.Contains(Word) && (h.ResourceValues ?? new Text[0]).All(v => !v || !v.text.Contains(Word)), "Home details still mention 보급품: " + h.Resources.text);
        await Tap(h.Continue); await Task.Delay(900);
        var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null && c.Introduction && c.Introduction.Step == 0, "New game settlement with the introduction");
        Check(c.Campaign.Ammo == 0 && c.AmmoCount.text == "0" && c.PartyCount.text == "2", "New game HUD ammo " + c.AmmoCount.text + " party " + c.PartyCount.text);
        Check(c.InventoryPanel.StockCount("supplies") == 0 && !c.InventoryPanel.Items.Any(i => i.Id == "supplies"), "Supplies in stock");
        log.Add("new game ammo 0 · party 2");

        // 2. Introduction step 2 ('남은 상자 수색') finds ammo: the HUD shows it.
        await Tap(c.Introduction.Action); await Task.Delay(300); await Tap(c.Introduction.Action); await Task.Delay(300);
        int found = c.Introduction.FoundAmmo;
        Check(c.Introduction.Step == 2 && c.Campaign.Ammo == found && c.AmmoCount.text == found.ToString() && c.InventoryPanel.StockCount("ammo") == found, "After the crate search: step " + c.Introduction.Step + " ammo " + c.Campaign.Ammo + " HUD " + c.AmmoCount.text);
        log.Add("crate search → ammo " + found + " on the HUD");

        // 3. Two-digit values fit the 70px value box.
        foreach (var t in new[] { c.AmmoCount, c.PartyCount })
        {
            string was = t.text; t.text = "88"; Canvas.ForceUpdateCanvases();
            Check(t.preferredWidth <= t.rectTransform.rect.width, t.name + " '88' needs " + t.preferredWidth + " of " + t.rectTransform.rect.width); t.text = was;
        }

        // 4. A field trip with ammo in a bag: dropping and taking it moves Campaign.Ammo through AdjustFieldResource.
        if (c.Introduction) c.Introduction.Restore(10); await Task.Delay(300);
        var member = c.Campaign.Party.First();
        Check(c.InventoryPanel.MoveFor(member, "ammo", found, true) && c.InventoryPanel.CountFor(member, "ammo") == found && c.Campaign.Ammo == found && c.InventoryPanel.StockCount("ammo") == 0, "Bagging ammo");
        var a = await Depart(c, true);
        Check(a.Resources.text.Contains("탄약 " + found), "Field HUD: " + a.Resources.text);
        Check(c.InventoryPanel.TransferField(member, "ammo", 1, false) && c.Campaign.Ammo == found - 1, "Drop 1 ammo: campaign " + c.Campaign.Ammo);
        Check(c.InventoryPanel.TransferField(member, "ammo", 1, true) && c.Campaign.Ammo == found, "Take 1 ammo back: campaign " + c.Campaign.Ammo);
        Check(!c.InventoryPanel.TransferField(member, "supplies", 1, true) && c.Campaign.Supplies == 0, "Supplies can still be taken");
        Check(c.InventoryPanel.TransferField(member, "ammo", 1, false) && c.Campaign.Ammo == found - 1 && c.InventoryPanel.CountFor(member, "ammo") == found - 1, "Drop 1 ammo for good");
        log.Add("field drop/take moves Campaign.Ammo " + found + " → " + (found - 1) + " → " + found + " → " + (found - 1));

        // 5. FinishReturn refreshes the HUD (it used to write SupplyCount) without an exception.
        c.AmmoCount.text = "?"; c.PartyCount.text = "?";
        Check(a.FinishReturn(), "Return"); await Task.Delay(500);
        if (c.ReturnPanel && c.ReturnPanel.IsOpen) await Tap(c.ReturnPanel.Back); await Task.Delay(200);
        Check(c.AmmoCount.text == (found - 1).ToString() && c.PartyCount.text == "2", "HUD after return: ammo " + c.AmmoCount.text + " party " + c.PartyCount.text);
        log.Add("return HUD ammo " + c.AmmoCount.text);

        // 6. Save and load keep ammo and the bag; the file has no supplies.
        if (c.Opening) c.Opening.State.Enabled = false;
        var saved = CampaignPersistence.Capture(c); string json = JsonUtility.ToJson(saved);
        Check(saved.Version == 15 && saved.Ammo == found - 1 && !json.Contains("\"Supplies\"") && !json.Contains("\"supplies\""), "Captured save v" + saved.Version + " ammo " + saved.Ammo);
        CampaignSaveStore.TestDirectory = Path.GetFullPath("Temp/RetireSuppliesVerification");
        try
        {
            Check(CampaignSaveStore.Write(0, saved, c, out var error), "Write: " + error);
            var read = CampaignSaveStore.Read(0, c); Check(read.CanLoad && read.Data.Version == 15 && read.Data.Ammo == found - 1, "Read: " + read.Error);
            CampaignPersistence.Prepare(read.Data, c);
        }
        finally { CampaignSaveStore.TestDirectory = null; }
        SceneManager.LoadScene("Settlement"); await Task.Delay(800);
        var loaded = Object.FindAnyObjectByType<SettlementController>(); Check(loaded && loaded != c && loaded.Campaign != null, "Settlement not reloaded");
        if (loaded.ReturnPanel && loaded.ReturnPanel.IsOpen) await Tap(loaded.ReturnPanel.Back);
        var again = loaded.Campaign.Party.First();
        Check(loaded.Campaign.Ammo == found - 1 && loaded.AmmoCount.text == (found - 1).ToString() && loaded.PartyCount.text == "2" && loaded.InventoryPanel.CountFor(again, "ammo") == found - 1 && loaded.Campaign.Supplies == 0, "Loaded ammo " + loaded.Campaign.Ammo + " HUD " + loaded.AmmoCount.text);
        log.Add("save v14/load keeps ammo " + loaded.Campaign.Ammo);
        // The HUD still for the side-by-side with mock 04, taken last (introduction done, opening chapter disabled before the save); check it for a tutorial veil.
        await Task.Delay(300); await Capture("01-settlement-hud");
        return "PASS runtime · " + string.Join(" · ", log) + " · stills " + Shots + " at " + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height;
    }

    static async Task Capture(string name) { Directory.CreateDirectory(Shots); ScreenCapture.CaptureScreenshot(Shots + name + ".png"); await Task.Delay(400); }

    // ---- helpers (as VerifySiteSave) ----
    static async Task<ExpeditionArrivalPanel> Depart(SettlementController c, bool shoot)
    {
        await Tap(c.Exit); var plan = c.ExpeditionPanel; int mall = Array.FindIndex(plan.Destinations, d => d.Id == "mall");
        if (mall >= 0) { plan.Markers[mall].onClick.Invoke(); await Task.Delay(100); }
        foreach (var card in plan.Cards) if (!card.Check.gameObject.activeSelf) await Tap(card.Button);
        await Tap(plan.Pack); Check(c.PackingPanel.IsOpen && Label(c.PackingPanel.Tabs[1]) == FoodTab, "Packing tab 1 '" + Label(c.PackingPanel.Tabs[1]) + "'");
        if (shoot) await Capture("02-packing-tabs");
        await Tap(c.PackingPanel.Ready); await Tap(c.PackingPanel.Depart); await Task.Delay(1100);
        Check(c.ArrivalPanel.IsOpen && !c.ArrivalPanel.InTransit, "Arrived"); return c.ArrivalPanel;
    }
    static async Task Tap(Button button)
    {
        Check(button && button.IsActive() && button.IsInteractable(), "Unavailable " + (button ? button.name : "null")); Hit(button);
        var r = (RectTransform)button.transform; var data = new PointerEventData(EventSystem.current) { position = Point(r), button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler); await Task.Delay(100);
    }
    static Vector2 Point(RectTransform r) { Canvas.ForceUpdateCanvases(); return RectTransformUtility.WorldToScreenPoint(r.GetComponentInParent<Canvas>().worldCamera, r.TransformPoint(r.rect.center)); }
    static void Hit(Selectable target)
    {
        var data = new PointerEventData(EventSystem.current) { position = Point((RectTransform)target.transform) }; var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Selectable>() == target, "Blocked " + target.name + " by " + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
    }
}
