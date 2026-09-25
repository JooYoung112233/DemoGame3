using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using Demo5.NightRun;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Stop-mode fixture after BuildEverydayGoods -> BuildEverydayRecipes.
// Uses disposable DTOs, an inactive cloned screen, and Temp/EverydayGoodsSlots only.
public static class VerifyEverydayGoodsPersistence
{
    static readonly string[] Added = { "fuel", "lubricant", "battery", "tobacco", "coffee", "alcohol", "electrical-parts", "weapon-parts" };
    static readonly string[] CraftIds = { "water-fuel", "bandage-alcohol", "flashlight-parts", "nails-lubricant" };
    static readonly string[] MealIds = { "warm-fuel", "stew-fuel" };
    [Serializable] sealed class TextValue { public string Value; }
    static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    static T Copy<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
    static string Snapshot(object value) => value == null ? "null" : value is Array rows ? "[" + string.Join(",", rows.Cast<object>().Select(Snapshot)) + "]" : value is string text ? JsonUtility.ToJson(new TextValue { Value = text }) : JsonUtility.ToJson(value);
    static bool Same(object a, object b) => Snapshot(a) == Snapshot(b);
    static SettlementController Catalog() => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab").GetComponent<SettlementController>();
    static SavedCount[] Cost(SettlementCraftPanel.Cost[] costs, int quantity) => costs.GroupBy(c => c.MaterialId).Select(g => new SavedCount { Id = g.Key, Count = g.Sum(c => c.Count) * quantity }).ToArray();
    static void Owner(object component, SettlementController owner) => component.GetType().GetField("owner", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, owner);
    static int Stock(SettlementCraftPanel panel, string id) => panel.Materials.Single(m => m.Id == id).Initial;

    static CampaignSaveData Save(SettlementController c, int version = 17)
    {
        var data = new CampaignSaveData {
            Version = version, SavedUtc = "2026-09-25T00:00:00Z", HomeId = "garage", Day = c.VisitorPanel.FirstDay, Minute = c.VisitorPanel.ArrivalMinute,
            IntroductionStep = 10, Development = new SavedDevelopment { Workbench = true, Cooker = true, Research = true, Tools = true, Warehouse = 2 },
            Opening = new SavedOpeningChapter(), SideRoomConnected = true, SideRoomReady = true,
            Members = c.Roster.Candidates.Select(p => new SavedMember { Id = p.Id, Name = p.DisplayName, Role = p.RoleTitle, Description = p.Description,
                Maximum = p.Health, Health = p.Id == "medic" ? 0 : p.Health - 1, Aim = p.Aim, Capacity = p.BagCapacity,
                Bag = p.Id == "scout" ? new[] { new SavedCount { Id = "cloth", Count = 2 } } : Array.Empty<SavedCount>() }).ToArray(),
            Materials = c.CraftPanel.Materials.Where(m => version >= 17 || !Added.Contains(m.Id)).Select(m => new SavedCount { Id = m.Id, Count = 20 }).ToArray(),
            Rest = Array.Empty<SavedRest>(), Craft = Array.Empty<SavedCraft>(), Cooking = Array.Empty<SavedCooking>(),
            Visitor = new SavedVisitor { VisitDay = c.VisitorPanel.FirstDay, RecruitId = "guard", Recruited = true, Dismissed = true,
                Stock = c.VisitorPanel.Goods.Where(g => version >= 17 || !Added.Contains(g.Id)).Select(g => new SavedCount { Id = g.Id, Count = Math.Max(0, g.Initial - 1) }).ToArray() },
            Inspected = new[] { "mall.arcade.table" }, Searches = new[] { new SavedSearch { Id = "mall.arcade.table", Progress = 1, Required = 1, Complete = true, Opened = true,
                Loot = new[] { new SavedCount { Id = "cloth", Count = 1 }, new SavedCount { Id = "wood", Count = 2 } } } },
            SiteBoards = new[] { new SavedSiteBoard { Id = "mall", Danger = 2, Gauge = 3, Remembered = "mall.corridor", IntroShown = true, ResidentLessonShown = true } },
            Doyun = new SavedNpcStory { Stage = 3, Page = 3, Choice = 2, VisitCount = 3, MetVisit = 2, Returned = true, Reunited = true },
            UnlockedCharacters = c.Roster.Candidates.Select(p => p.Id).OrderBy(id => id).ToArray(), Activity = new[] { "기존 원정과 영입 기록" }
        };
        var craft = c.CraftPanel.Recipes.Single(r => r.Id == "nails");
        data.Craft = new[] { new SavedCraft { MemberId = "mechanic", RecipeId = craft.Id, Quantity = 2, Minutes = 7, Reserved = Cost(craft.Costs, 2) } };
        var meal = c.CookingPanel.Meals.Single(r => r.Id == "warm");
        data.Cooking = new[] { new SavedCooking { MemberId = "cook", RecipeId = meal.Id, Quantity = 1, Minutes = 11,
            OutputId = meal.OutputId, OutputCount = meal.Servings, Reserved = Cost(meal.Costs, 1) } };
        return data;
    }

    public static string Migration()
    {
        var c = Catalog(); Check(CampaignPersistence.CurrentVersion == 17, "Expected save v17.");
        Check(Added.All(id => c.CraftPanel.Materials.Any(m => m.Id == id) && c.VisitorPanel.Goods.Any(g => g.Id == id)), "Everyday goods are not registered in the actual screen.");
        var old = Save(c, 16); var before = Copy(old); CampaignPersistence.Validate(old, c); CampaignPersistence.Upgrade(old, c);
        Check(old.Version == 17, "Legacy version did not advance.");
        Check(Added.All(id => old.Materials.Single(m => m.Id == id).Count == 0 && old.Visitor.Stock.Single(m => m.Id == id).Count == 0), "Migration granted new goods or restocked the current visitor.");
        Check(Same(old.Materials.Where(m => !Added.Contains(m.Id)).ToArray(), before.Materials) && Same(old.Visitor.Stock.Where(m => !Added.Contains(m.Id)).ToArray(), before.Visitor.Stock), "Existing stock changed.");
        Check(old.Visitor.VisitDay == before.Visitor.VisitDay && old.Visitor.RecruitId == before.Visitor.RecruitId && old.Visitor.Dismissed == before.Visitor.Dismissed && old.Visitor.Recruited == before.Visitor.Recruited, "Visitor history changed.");
        Check(Same(old.Members, before.Members) && Same(old.Craft, before.Craft) && Same(old.Cooking, before.Cooking) && Same(old.Rest, before.Rest), "Jobs, reserved costs, injuries, death or bags changed.");
        Check(Same(old.Searches, before.Searches) && Same(old.Inspected, before.Inspected) && Same(old.SiteBoards, before.SiteBoards), "Completed search loot or field knowledge was rerolled.");
        Check(Same(old.UnlockedCharacters, before.UnlockedCharacters) && Same(old.Doyun, before.Doyun) && Same(old.Activity, before.Activity) && Same(old.Development, before.Development), "Unlock/story/activity/development changed.");
        var once = Copy(old); CampaignPersistence.Upgrade(old, c); Check(Same(old, once), "Repeated loading duplicated stock or changed history.");
        var notVisited = Save(c, 16); notVisited.Visitor = new SavedVisitor(); CampaignPersistence.Upgrade(notVisited, c);
        Check(notVisited.Visitor.VisitDay == 0 && notVisited.Visitor.Stock.Length == 0, "An unvisited merchant gained stock before arrival.");
        var partial = Save(c, 16); partial.Materials = partial.Materials.Concat(new[] { new SavedCount { Id = "fuel", Count = 3 } }).ToArray();
        partial.Visitor.Stock = partial.Visitor.Stock.Concat(new[] { new SavedCount { Id = "coffee", Count = 2 } }).ToArray(); CampaignPersistence.Upgrade(partial, c);
        Check(partial.Materials.Single(m => m.Id == "fuel").Count == 3 && partial.Visitor.Stock.Single(m => m.Id == "coffee").Count == 2, "A valid already-present new good was overwritten.");
        return "PASS v16 -> v17 adds only missing eight goods at zero, preserves partial new stock, old stock/visitor/jobs/injuries/unlocks/story/searches, empty unvisited trader, idempotence.";
    }

    static void Rejected(CampaignSaveData data, SettlementController c, string name)
    {
        bool rejected = false; try { CampaignPersistence.Upgrade(data, c); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Invalid data accepted: " + name);
    }
    public static string Refusals()
    {
        var c = Catalog(); CampaignPersistence.Validate(Save(c), c); var s = Save(c, 16); s.Materials = s.Materials.Where(m => m.Id != "wood").ToArray(); Rejected(s, c, "missing OLD material");
        s = Save(c, 16); s.Visitor.Stock = s.Visitor.Stock.Skip(1).ToArray(); Rejected(s, c, "missing OLD merchant good");
        s = Save(c, 16); s.Materials = null; Rejected(s, c, "missing material container");
        s = Save(c, 16); s.Visitor.Stock = null; Rejected(s, c, "missing visited merchant stock container");
        s = Save(c); s.Materials = s.Materials.Where(m => m.Id != "fuel").ToArray(); Rejected(s, c, "missing v17 material");
        s = Save(c); s.Visitor.Stock = s.Visitor.Stock.Where(m => m.Id != "coffee").ToArray(); Rejected(s, c, "missing v17 merchant good");
        s = Save(c); s.Materials.Single(m => m.Id == "fuel").Count = -1; Rejected(s, c, "negative new material");
        s = Save(c); s.Materials = s.Materials.Concat(new[] { new SavedCount { Id = "water-fuel", Count = 1 } }).ToArray(); Rejected(s, c, "recipe alias masquerading as an output item");
        return "PASS eight invalid current/legacy containers and item identities remain rejected.";
    }

    public static string Disk()
    {
        string previous = CampaignSaveStore.TestDirectory;
        try {
            CampaignSaveStore.TestDirectory = Path.GetFullPath("Temp/EverydayGoodsSlots"); var c = Catalog(); var old = Save(c, 16);
            Check(CampaignSaveStore.Write(0, old, c, out var error), "Legacy file write: " + error);
            string raw = File.ReadAllText(CampaignSaveStore.SlotPath(0)); var read = CampaignSaveStore.Read(0, c);
            Check(read.CanLoad && read.Data.Version == 17, "Legacy file read: " + read.Error);
            Check(File.ReadAllText(CampaignSaveStore.SlotPath(0)) == raw, "Reading rewrote the original slot.");
            Check(CampaignSaveStore.Write(1, read.Data, c, out error), "Current file write: " + error);
            var again = CampaignSaveStore.Read(1, c); Check(again.CanLoad && Same(again.Data, read.Data), "Current file roundtrip changed state: " + again.Error);
            var bad = Copy(read.Data); bad.Materials = bad.Materials.Where(m => m.Id != "fuel").ToArray();
            string intact = File.ReadAllText(CampaignSaveStore.SlotPath(1)); Check(!CampaignSaveStore.Write(1, bad, c, out error), "Invalid replacement was accepted.");
            Check(File.ReadAllText(CampaignSaveStore.SlotPath(1)) == intact, "Invalid replacement damaged the valid slot.");
            return "PASS actual v16/current Temp file roundtrip, read leaves old file intact, invalid replacement keeps the good save.";
        }
        finally { CampaignSaveStore.TestDirectory = previous; }
    }

    public static string Production()
    {
        var selected = PartySelectionSession.Selected.ToArray(); var root = new GameObject("Everyday goods verification fixture"); root.SetActive(false);
        try {
            var catalog = Catalog(); var clone = Object.Instantiate(catalog.gameObject, root.transform); var c = clone.GetComponent<SettlementController>();
            var baseline = Save(c); PartySelectionSession.Selected.Clear(); PartySelectionSession.Selected.AddRange(baseline.Members.Select(p => p.Id));
            typeof(SettlementController).GetProperty("Campaign").SetValue(c, CampaignState.RestoreSettled(baseline));
            Owner(c.CraftPanel, c); Owner(c.CookingPanel, c); Owner(c.Introduction, c); c.Introduction.Restore(10);
            c.Development.State = new SavedDevelopment();
            Check(CraftIds.All(id => !c.CraftPanel.IsRecipeUnlocked(c.CraftPanel.Recipes.Single(r => r.Id == id))) && MealIds.All(id => !c.CookingPanel.IsRecipeUnlocked(c.CookingPanel.Meals.Single(r => r.Id == id))), "New alternatives are visible before facilities.");
            c.Development.Complete("build-bench");
            Check(c.CraftPanel.IsRecipeUnlocked(c.CraftPanel.Recipes.Single(r => r.Id == "water-fuel")) && c.CraftPanel.IsRecipeUnlocked(c.CraftPanel.Recipes.Single(r => r.Id == "bandage-alcohol")), "Workshop alternatives stayed locked.");
            Check(!c.CraftPanel.IsRecipeUnlocked(c.CraftPanel.Recipes.Single(r => r.Id == "flashlight-parts")), "Parts recipe bypasses tools research.");
            c.Development.Complete("build-cooker"); c.Development.Complete("build-research"); c.Development.Complete("research-tools");
            Check(CraftIds.All(id => c.CraftPanel.IsRecipeUnlocked(c.CraftPanel.Recipes.Single(r => r.Id == id))) && MealIds.All(id => c.CookingPanel.IsRecipeUnlocked(c.CookingPanel.Meals.Single(r => r.Id == id))), "Completed facilities did not reveal the alternatives.");

            c.CraftPanel.RestoreSaved(baseline); c.CookingPanel.RestoreSaved(baseline);
            c.CraftPanel.Materials.Single(m => m.Id == "scrap").Initial = 4; // Two are reserved by the legacy nails fixture.
            c.CraftPanel.Materials.Single(m => m.Id == "lubricant").Initial = 1;
            var consumer = c.CraftPanel.Recipes.Single(r => r.Id == "prybar");
            Check(CraftMaterialGuide.FindSourceRecipe(c.CraftPanel,"nails",consumer,3)?.Id == "nails-lubricant", "Guide cannot find the usable three-nail alternative by output ID.");
            c.Development.State.Tools = false;
            Check(CraftMaterialGuide.FindSourceRecipe(c.CraftPanel,"nails",consumer,3)?.Id == "nails", "Guide exposes a research-locked alternative.");
            c.Development.State.Tools = true; c.CraftPanel.Materials.Single(m => m.Id == "lubricant").Initial = 0;
            Check(CraftMaterialGuide.FindSourceRecipe(c.CraftPanel,"nails",consumer,2)?.Id == "nails", "Guide prefers an unaffordable alternative over a fully supplied recipe.");

            foreach (string id in new[] { "nails", "water", "bandage", "flashlight" }.Concat(CraftIds)) {
                var recipe = c.CraftPanel.Recipes.Single(r => r.Id == id); var data = Save(c); data.Cooking = Array.Empty<SavedCooking>();
                data.Craft = new[] { new SavedCraft { MemberId = "mechanic", RecipeId = id, Quantity = 2, Minutes = 3, Reserved = Cost(recipe.Costs, 2) } };
                CampaignPersistence.Validate(data, c); c.CraftPanel.RestoreSaved(data); c.CookingPanel.RestoreSaved(data);
                var stock = c.CraftPanel.Materials.ToDictionary(m => m.Id, m => m.Initial);
                foreach (var cost in data.Craft[0].Reserved) Check(c.CraftPanel.Available(cost.Id) == stock[cost.Id] - cost.Count, "Craft reservation not reflected: " + id);
                Check(c.CraftPanel.AdvanceTime(2).Count == 0 && c.CraftPanel.Orders.Count == 1 && c.CraftPanel.Materials.All(m => m.Initial == stock[m.Id]), "Craft completed or consumed early: " + id);
                var expected = stock.ToDictionary(kv => kv.Key, kv => kv.Value); foreach (var cost in data.Craft[0].Reserved) expected[cost.Id] -= cost.Count;
                expected[recipe.ProducedId] += recipe.ProducedCount * 2;
                var completed = c.CraftPanel.AdvanceTime(1);
                Check(completed.Count == 1 && c.CraftPanel.Orders.Count == 0 && c.CraftPanel.Materials.All(m => m.Initial == expected[m.Id]), "Wrong actual craft costs/output: " + id);
                string product = c.CraftPanel.Materials.Single(m => m.Id == recipe.ProducedId).Name;
                Check(completed[0].EndsWith(" · " + product + " ×" + (recipe.ProducedCount * 2) + " 완료", StringComparison.Ordinal), "Completion text reports batches instead of actual product quantity: " + id);
                Check(c.CraftPanel.AdvanceTime(100).Count == 0 && c.CraftPanel.Materials.All(m => m.Initial == expected[m.Id]), "Craft produced twice: " + id);
                Check(!CraftIds.Any(alias => c.CraftPanel.Materials.Any(m => m.Id == alias)), "Alternative ID became a phantom inventory item.");
            }
            foreach (string id in new[] { "warm", "stew" }.Concat(MealIds)) {
                var recipe = c.CookingPanel.Meals.Single(m => m.Id == id); var data = Save(c); data.Craft = Array.Empty<SavedCraft>();
                data.Cooking = new[] { new SavedCooking { MemberId = "cook", RecipeId = id, Quantity = 2, Minutes = 3, OutputId = recipe.OutputId, OutputCount = recipe.Servings * 2, Reserved = Cost(recipe.Costs, 2) } };
                CampaignPersistence.Validate(data, c); c.CraftPanel.RestoreSaved(data); c.CookingPanel.RestoreSaved(data);
                var stock = c.CraftPanel.Materials.ToDictionary(m => m.Id, m => m.Initial);
                foreach (var cost in data.Cooking[0].Reserved) Check(c.CraftPanel.Available(cost.Id) == stock[cost.Id] - cost.Count, "Cooking reservation not reflected: " + id);
                Check(c.CookingPanel.AdvanceTime(2).Count == 0 && c.CookingPanel.Orders.Count == 1, "Meal completed early: " + id);
                var expected = stock.ToDictionary(kv => kv.Key, kv => kv.Value); foreach (var cost in data.Cooking[0].Reserved) expected[cost.Id] -= cost.Count;
                expected[recipe.OutputId] += recipe.Servings * 2;
                Check(c.CookingPanel.AdvanceTime(1).Count == 1 && c.CookingPanel.Orders.Count == 0 && c.CraftPanel.Materials.All(m => m.Initial == expected[m.Id]), "Wrong actual meal costs/output: " + id);
                Check(c.CookingPanel.AdvanceTime(100).Count == 0 && c.CraftPanel.Materials.All(m => m.Initial == expected[m.Id]), "Meal produced twice: " + id);
            }
            foreach (string unused in new[] { "tobacco", "coffee", "weapon-parts" })
                Check(!c.CraftPanel.Recipes.Any(r => r.Costs.Any(cost => cost.MaterialId == unused)) && !c.CookingPanel.Meals.Any(r => r.Costs.Any(cost => cost.MaterialId == unused)), "Trade-only item has an unapproved consumption recipe: " + unused);
            return "PASS facility gates; restored reservations and exact once-only consumption/production for four old + four alternative craft recipes and two old + two fuel meals; output IDs preserved; three trade-only items have no use recipe.";
        }
        finally { Object.DestroyImmediate(root); PartySelectionSession.Selected.Clear(); PartySelectionSession.Selected.AddRange(selected); }
    }

    public static string Run()
    {
        Check(!Application.isPlaying, "Run the disposable fixture while stopped.");
        return Migration() + "\n" + Refusals() + "\n" + Disk() + "\n" + Production();
    }

    public static string Finish()
    {
        Check(!Application.isPlaying, "Stop Play before clearing the test session override.");
        string expected = Path.GetFullPath("Temp/EverydayProductionSlots").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string active = CampaignSaveStore.TestDirectory;
        bool ours = !string.IsNullOrEmpty(active) && string.Equals(Path.GetFullPath(active).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), expected, StringComparison.OrdinalIgnoreCase);
        if (ours) CampaignSaveStore.TestDirectory = null;
        PartySelectionSession.Clear();
        return ours ? "Cleared only EverydayProductionSlots override and the test party session; no slot files deleted." : "Other save-directory override left untouched; test party session cleared; no slot files deleted.";
    }

    static SettlementController Live => Object.FindAnyObjectByType<SettlementController>();
    static async Task Until(Func<bool> condition, string message)
    {
        for (int i = 0; i < 80; i++) { if (condition()) return; await Task.Delay(50); }
        throw new InvalidOperationException(message);
    }
    static void RegisterCraft(string id)
    {
        var c = Live; var panel = c.CraftPanel; var recipe = panel.Recipes.Single(r => r.Id == id);
        panel.Open(); Check(panel.IsOpen, "Craft view did not open."); panel.FocusRecipe(id);
        Check(panel.SelectedRecipeId == id, "New craft alternative cannot be selected: " + id);
        string name = c.Roster.Candidates.Single(p => p.Id == "mechanic").DisplayName;
        panel.WorkerRows.Single(row => row.Label.text == name).Button.onClick.Invoke();
        Check(panel.Confirm.interactable, "Craft registration is blocked: " + panel.ConfirmLabel.text);
        panel.Confirm.onClick.Invoke(); Check(panel.Orders.Any(o => o.Recipe.Id == id), "Craft Confirm did not register: " + id);
    }
    static void RegisterMeal(string id)
    {
        var c = Live; var panel = c.CookingPanel; var recipe = panel.Meals.Single(m => m.Id == id);
        panel.Open(); Check(panel.IsOpen, "Cooking view did not open.");
        panel.Tabs[recipe.Category].onClick.Invoke(); panel.RecipeRows.Single(row => row.Label.text == recipe.Name).Button.onClick.Invoke();
        string name = c.Roster.Candidates.Single(p => p.Id == "cook").DisplayName;
        panel.WorkerRows.Single(row => row.Label.text == name).Button.onClick.Invoke();
        Check(panel.Confirm.interactable, "Cooking registration is blocked: " + panel.ConfirmLabel.text);
        panel.Confirm.onClick.Invoke(); Check(panel.Orders.Any(o => o.Recipe.Id == id), "Cooking Confirm did not register: " + id);
    }
    static System.Collections.Generic.Dictionary<string, int> ActualStock() => Live.CraftPanel.Materials.ToDictionary(m => m.Id, m => m.Initial);
    static void StockEquals(System.Collections.Generic.Dictionary<string, int> expected, string message)
        => Check(Live.CraftPanel.Materials.Length == expected.Count && Live.CraftPanel.Materials.All(m => expected.TryGetValue(m.Id, out int count) && count == m.Initial), message);
    static void ApplyCosts(System.Collections.Generic.Dictionary<string, int> stock, System.Collections.Generic.Dictionary<string, int> costs, string output, int count)
    {
        foreach (var cost in costs) stock[cost.Key] -= cost.Value; stock[output] += count;
    }

    public static async Task<string> PlayMode()
    {
        Check(Application.isPlaying, "Enter Play before the actual registration/cancel/restart fixture.");
        CampaignSaveStore.TestDirectory = Path.GetFullPath("Temp/EverydayProductionSlots");
        var catalog = Catalog(); var data = Save(catalog); data.Craft = Array.Empty<SavedCraft>(); data.Cooking = Array.Empty<SavedCooking>();
        int previous = Live ? Live.GetEntityId().GetHashCode() : 0;
        CampaignPersistence.Prepare(data, catalog); SceneManager.LoadScene("Settlement");
        await Until(() => Live && Live.GetEntityId().GetHashCode() != previous && Live.Campaign != null, "Production fixture scene did not load.");
        var start = ActualStock(); int time = Live.Campaign.MinuteOfDay;

        Live.Development.State.Tools = false; var craft = Live.CraftPanel; craft.Open();
        foreach (string id in new[] { "flashlight-parts", "nails-lubricant" }) {
            string name = craft.Recipes.Single(r => r.Id == id).Name;
            Check(!craft.RecipeRows.Any(row => row.Label.text == name), "Locked alternative appears in the actual list: " + id);
            craft.FocusRecipe(id); Check(craft.SelectedRecipeId != id, "Direct focus bypasses research lock: " + id);
        }
        craft.Close(); Live.Development.Complete("research-tools");
        RegisterCraft("water-fuel"); craft.Open(); craft.OrderRows.Single().Cancel.onClick.Invoke();
        Check(craft.CancelPopup.activeSelf, "Craft cancellation review did not open."); craft.CancelYes.onClick.Invoke(); craft.Close();
        Check(craft.Orders.Count == 0 && Live.Campaign.MinuteOfDay == time, "Craft cancel left a job or advanced time."); StockEquals(start, "Craft cancel consumed reserved material.");

        RegisterMeal("warm-fuel"); var cooking = Live.CookingPanel; cooking.Open(); cooking.OrderRows.Single().Cancel.onClick.Invoke();
        Check(cooking.CancelPopup.activeSelf, "Cooking cancellation review did not open."); cooking.CancelYes.onClick.Invoke(); cooking.Close();
        Check(cooking.Orders.Count == 0 && Live.Campaign.MinuteOfDay == time, "Cooking cancel left a job or advanced time."); StockEquals(start, "Cooking cancel consumed reserved material.");

        RegisterCraft("water-fuel"); RegisterMeal("warm-fuel");
        Check(Live.Campaign.AdvanceSettlementTime(5), "Could not advance concurrent production.");
        var saved = CampaignPersistence.Capture(Live); Check(saved.Craft.Length == 1 && saved.Cooking.Length == 1 && saved.Craft[0].Minutes > 0 && saved.Cooking[0].Minutes > 0, "Concurrent production did not remain partially complete.");
        StockEquals(start, "Partial jobs consumed reserved materials early.");
        var expected = ActualStock(); var cr = Live.CraftPanel.Orders.Single(); var meal = Live.CookingPanel.Orders.Single();
        ApplyCosts(expected, cr.Reserved, cr.Recipe.ProducedId, cr.Recipe.ProducedCount * cr.Quantity);
        ApplyCosts(expected, meal.Reserved, meal.OutputId, meal.OutputCount);
        Check(CampaignSaveStore.Write(0, saved, Live, out var error), "Partial production save failed: " + error);
        var loaded = CampaignSaveStore.Read(0, Live); Check(loaded.CanLoad, "Partial production load failed: " + loaded.Error);
        previous = Live.GetEntityId().GetHashCode(); CampaignPersistence.Prepare(loaded.Data, Live); SceneManager.LoadScene("Settlement");
        await Until(() => Live && Live.GetEntityId().GetHashCode() != previous && Live.Campaign != null, "Restored production scene did not load.");
        var restored = CampaignPersistence.Capture(Live);
        Check(Same(restored.Craft, saved.Craft) && Same(restored.Cooking, saved.Cooking), "Reload changed remaining time, selected output or reservations.");
        Check(Live.Campaign.AdvanceSettlementTime(Math.Max(saved.Craft[0].Minutes, saved.Cooking[0].Minutes)), "Restored production did not advance.");
        Check(Live.CraftPanel.Orders.Count == 0 && Live.CookingPanel.Orders.Count == 0, "Restored jobs did not complete."); StockEquals(expected, "Restored production lost or duplicated material/output.");
        Live.Campaign.AdvanceSettlementTime(30); StockEquals(expected, "Completed jobs paid out twice after reload.");

        foreach (string id in new[] { "bandage-alcohol", "flashlight-parts", "nails-lubricant" }) {
            expected = ActualStock(); RegisterCraft(id); var order = Live.CraftPanel.Orders.Single();
            ApplyCosts(expected, order.Reserved, order.Recipe.ProducedId, order.Recipe.ProducedCount * order.Quantity);
            Check(Live.Campaign.AdvanceSettlementTime(order.Minutes), "Craft time failed: " + id); StockEquals(expected, "Actual registered recipe output/cost mismatch: " + id);
        }
        expected = ActualStock(); RegisterMeal("stew-fuel"); var finalMeal = Live.CookingPanel.Orders.Single();
        ApplyCosts(expected, finalMeal.Reserved, finalMeal.OutputId, finalMeal.OutputCount);
        Check(Live.Campaign.AdvanceSettlementTime(finalMeal.Minutes), "Fuel stew time failed."); StockEquals(expected, "Fuel stew actual output/cost mismatch.");
        return "PASS actual recipe list locks, craft/cooking Confirm registration and cancel with no spending; concurrent fuel jobs save/reload remaining time and complete once; all six alternatives produce the intended existing items. UI listeners invoked, physical pointer/raycast not claimed; Temp slots only.";
    }
}
