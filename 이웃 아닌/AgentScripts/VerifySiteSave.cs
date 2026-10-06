using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Save v13 (SiteBoards): the site board (위험도, 소음 게이지, 기억한 방, 방문, 장소 판 안내) survives a save/load.
// Save v14 (2026-09-25): the supplies item was retired; older saves drop it before the first Validate (RetiredSupplies).
// The resident lesson flag (SavedSiteBoard.ResidentLessonShown) needs no version: checked apart from Say() (ResidentLesson).
// Migration, Refusals, RetiredSupplies, ResidentLesson: edit mode (or play), pure data against the title's save catalog (SettlementScreen.prefab).
// RoundTrip: play mode after VerifyFieldTurnPlan.Enter (a new game in the settlement, the site not visited yet).
public static class VerifySiteSave
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    const string Refused = "장소 상태 기록이 올바르지 않습니다.";
    const string Fresh = "mall/0/0//False", Seen = "mall/0/0//True";
    static string Say(SavedSiteBoard b) => b == null ? "none" : b.Id + "/" + b.Danger + "/" + b.Gauge + "/" + b.Remembered + "/" + b.IntroShown;

    // ---- pure data ----
    // The same catalog the title reads slots with (TitleMenu SaveCatalog).
    static SettlementController Catalog()
    {
        var g = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab"); var c = g ? g.GetComponent<SettlementController>() : null;
        Check(c && c.Roster && c.Roster.Candidates.Length >= 2 && c.CraftPanel && c.ArrivalPanel && c.ArrivalPanel.Loot, "Save catalog missing"); return c;
    }
    // A valid save at any version up to 12, built by hand: two roster members, every material at 0, no orders and no visit evidence.
    static CampaignSaveData Save(SettlementController cat, int version)
    {
        var people = cat.Roster.Candidates.Take(2).ToArray();
        return new CampaignSaveData
        {
            Version = version, SavedUtc = DateTime.UtcNow.ToString("O"), HomeId = CampaignPersistence.HomeIds[0], Day = 2, Minute = 600, Ammo = 0,
            IntroductionStep = 10, Development = new SavedDevelopment { Bed = true, Workbench = true, Cooker = true, Warehouse = 1 }, Opening = new SavedOpeningChapter(),
            Members = people.Select(p => new SavedMember { Id = p.Id, Name = string.IsNullOrWhiteSpace(p.DisplayName) ? p.Id : p.DisplayName, Role = p.RoleTitle, Description = p.Description, Health = p.Health, Maximum = p.Health, Aim = p.Aim, Capacity = p.BagCapacity, Bag = new SavedCount[0] }).ToArray(),
            Materials = cat.CraftPanel.Materials.Select(m => new SavedCount { Id = m.Id, Count = 0 }).ToArray(),
            Rest = new SavedRest[0], Craft = new SavedCraft[0], Cooking = new SavedCooking[0], Visitor = new SavedVisitor(),
            Inspected = new string[0], Searches = new SavedSearch[0], Activity = new string[0]
        };
    }
    static SavedReport Report(CampaignSaveData s) => new SavedReport { Destination = "폐상가", Body = "", Minutes = 60, Members = new[] { new SavedReportMember { Id = s.Members[0].Id, Before = s.Members[0].Health, After = s.Members[0].Health, Maximum = s.Members[0].Maximum } }, Items = new SavedReportItem[0] };
    static SavedSearch Search(string id) => new SavedSearch { Id = id, Loot = new SavedCount[0] };
    static SavedSiteBoard[] Migrate(SettlementController cat, int version, Action<CampaignSaveData> evidence, string what)
    {
        var s = Save(cat, version); evidence?.Invoke(s); CampaignPersistence.Upgrade(s, cat);
        Check(s.Version == CampaignPersistence.CurrentVersion && s.SiteBoards != null, what + ": v" + version + " ended at v" + s.Version);
        return s.SiteBoards;
    }
    static void One(SavedSiteBoard[] b, string expected, string what) => Check(b.Length == 1 && Say(b[0]) == expected, what + ": " + b.Length + " " + Say(b.FirstOrDefault()));
    // A current (v14) save with one fresh 'mall' board (a report migrated from v12). Not a v13 fixture: Upgrade runs to the end.
    static CampaignSaveData Visited(SettlementController cat) { var s = Save(cat, 12); s.ReturnReport = Report(s); CampaignPersistence.Upgrade(s, cat); One(s.SiteBoards, Fresh, "Visited base"); return s; }
    // A v12 file on disk has no SiteBoards key at all.
    static CampaignSaveData RawV12(CampaignSaveData s)
    {
        var json = Regex.Replace(JsonUtility.ToJson(s), @"""SiteBoards"":\[[^\]]*\],?", ""); Check(!json.Contains("SiteBoards"), "Raw v12 still has SiteBoards");
        return JsonUtility.FromJson<CampaignSaveData>(json);
    }

    public static string Migration()
    {
        Check(CampaignPersistence.CurrentVersion == 15, "Save version " + CampaignPersistence.CurrentVersion);
        var cat = Catalog(); var done = new List<string>();

        // 1. v12: any visit evidence gives one fresh board; only an office shelf record proves the intro was seen.
        var b = Migrate(cat, 12, null, "none"); Check(b.Length == 0, "No evidence: " + b.Length);
        One(Migrate(cat, 12, s => s.ReturnReport = Report(s), "report"), Fresh, "Report only");
        One(Migrate(cat, 12, s => s.Searches = new[] { Search("mall.arcade.crate") }, "crate"), Fresh, "Crate search at progress 0");
        One(Migrate(cat, 12, s => s.CorridorVisited = true, "corridor"), Fresh, "Corridor visited");
        One(Migrate(cat, 12, s => s.Opening.FirstReturn = true, "first return"), Fresh, "First return");
        One(Migrate(cat, 12, s => { s.Inspected = new[] { "mall.arcade.crate" }; s.ReturnReport = Report(s); }, "crate + report"), Fresh, "Inspected crate and report");
        One(Migrate(cat, 12, s => s.Inspected = new[] { "mall.office.shelf" }, "shelf inspected"), Seen, "Office shelf inspected");
        One(Migrate(cat, 12, s => s.Searches = new[] { Search("mall.office.shelf") }, "shelf searched"), Seen, "Office shelf searched");
        done.Add("v12 evidence → one fresh board, office shelf → intro seen, none → no board");

        // 2. Older chains end at the current version (14) the same way.
        foreach (int v in new[] { 1, 5, 10, 11 })
        {
            b = Migrate(cat, v, null, "chain"); Check(b.Length == 0, "Chain from v" + v + " without evidence: " + b.Length);
            One(Migrate(cat, v, s => s.ReturnReport = Report(s), "chain"), Fresh, "Chain from v" + v + " with a report");
        }
        done.Add("v1/v5/v10/v11 → v14");

        // 3. Raw v12 JSON (no SiteBoards key; 'no report' is an empty report object, normalised before the step).
        var raw = RawV12(Save(cat, 12)); CampaignPersistence.Upgrade(raw, cat); Check(raw.ReturnReport == null && raw.SiteBoards.Length == 0, "Raw v12 without a report: " + raw.SiteBoards.Length);
        var withReport = Save(cat, 12); withReport.ReturnReport = Report(withReport); raw = RawV12(withReport); CampaignPersistence.Upgrade(raw, cat); One(raw.SiteBoards, Fresh, "Raw v12 with a report");
        done.Add("raw v12 JSON");

        // 4. A current (v14) save goes through Upgrade and JSON unchanged.
        var cur = Visited(cat); var board = cur.SiteBoards[0]; board.Danger = 2; board.Gauge = 3; board.Remembered = "mall.corridor"; board.IntroShown = true;
        var again = JsonUtility.FromJson<CampaignSaveData>(JsonUtility.ToJson(cur)); CampaignPersistence.Upgrade(again, cat);
        One(again.SiteBoards, "mall/2/3/mall.corridor/True", "v14 JSON round trip");
        done.Add("v14 unchanged");

        // 5. The component: RestoreSaved → ExportSaved for every room id; never visited exports nothing.
        var go = EditorUtility.CreateGameObjectWithHideFlags("SiteSaveProbe", HideFlags.HideAndDontSave, typeof(ExpeditionSiteThreat));
        try
        {
            var t = go.GetComponent<ExpeditionSiteThreat>();
            t.RestoreSaved(null); Check(!t.Visited && t.ExportSaved() == null && t.State == null, "Never visited exports a board");
            foreach (var room in new[] { "" }.Concat(CampaignPersistence.SiteRoomIds))
            {
                var saved = new SavedSiteBoard { Id = "mall", Danger = 2, Gauge = 1, Remembered = room, IntroShown = room.Length > 0 };
                t.RestoreSaved(saved); Check(t.Visited && t.State == null && Say(t.ExportSaved()) == Say(saved), "Component " + Say(saved) + " → " + Say(t.ExportSaved()));
            }
            t.RestoreSaved(null); Check(!t.Visited && t.ExportSaved() == null, "Restore to never visited");
        }
        finally { Object.DestroyImmediate(go); }
        done.Add("component restore/export for " + (CampaignPersistence.SiteRoomIds.Length + 1) + " rooms");
        return "PASS migration · " + string.Join(" · ", done);
    }

    public static string Refusals()
    {
        var cat = Catalog(); var done = new List<string>(); string dir = Path.GetFullPath("Temp/SiteSaveVerification/Refusals");
        var cases = new (string name, Action<CampaignSaveData> spoil)[]
        {
            ("danger 4", s => s.SiteBoards[0].Danger = 4),
            ("gauge -1", s => s.SiteBoards[0].Gauge = -1),
            ("id store", s => s.SiteBoards[0].Id = "store"),
            ("duplicate mall", s => s.SiteBoards = new[] { s.SiteBoards[0], new SavedSiteBoard { Id = "mall", Remembered = "" } }),
            ("room roof", s => s.SiteBoards[0].Remembered = "roof"),
            ("null record", s => s.SiteBoards = new SavedSiteBoard[] { null }),
            ("SiteBoards null at v13", s => s.SiteBoards = null),
        };
        CampaignSaveStore.TestDirectory = dir;
        try
        {
            foreach (var k in cases)
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                var s = Visited(cat); k.spoil(s);
                Check(!CampaignSaveStore.Write(0, s, cat, out var error), "Accepted: " + k.name);
                Check(error == Refused && !File.Exists(CampaignSaveStore.SlotPath(0)), k.name + ": " + error);
                done.Add(k.name);
            }
            // Control: a good board writes and reads back unchanged (the refusals above are about the board, not the base save).
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            var good = Visited(cat); var b = good.SiteBoards[0]; b.Danger = 3; b.Gauge = 2; b.Remembered = "mall.office"; b.IntroShown = true;
            Check(CampaignSaveStore.Write(0, good, cat, out var e), "Good board refused: " + e);
            var read = CampaignSaveStore.Read(0, cat); Check(read.CanLoad && read.Data.Version == CampaignPersistence.CurrentVersion, "Good board read: " + read.Error);
            One(read.Data.SiteBoards, "mall/3/2/mall.office/True", "Good board on disk");
        }
        finally { CampaignSaveStore.TestDirectory = null; }
        return "PASS refusals (" + string.Join(", ", done) + ") and a good board written/read";
    }

    // ---- save v14: the retired supplies item (run after BuildRetireSupplies removed it from InventoryPanel.Items) ----
    const string UnknownItem = "알 수 없거나 잘못된 물자 데이터입니다.", BadReportItem = "귀환 물자 기록이 올바르지 않습니다.";
    // Mirror of CampaignSaveStore's private envelope: Write validates with the current catalog, so an old file is written by hand.
    [Serializable] sealed class Envelope { public string Magic, Payload, Hash; }
    static string Digest(string value) { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value))); }
    static void WriteRaw(string payload) => File.WriteAllText(CampaignSaveStore.SlotPath(0), JsonUtility.ToJson(new Envelope { Magic = "NEIGHBOR-NOT-SAVE", Payload = payload, Hash = Digest(payload) }), new UTF8Encoding(false));
    static SavedCount Stack(string id, int n) => new SavedCount { Id = id, Count = n };
    static SavedSearch Finished(params SavedCount[] loot) => new SavedSearch { Id = "mall.arcade.crate", Pace = 0, Required = 1, Progress = 1, Opened = true, Complete = true, Loot = loot };
    // A save as the pre-v14 game wrote it: supplies in a bag, in finished search loot and in the return report, next to scrap.
    static CampaignSaveData WithSupplies(SettlementController cat, int version)
    {
        var s = Save(cat, version); s.SiteBoards = version >= 13 ? new SavedSiteBoard[0] : null;
        s.Members[0].Bag = new[] { Stack("scrap", 1), Stack("supplies", 2) };
        s.Searches = new[] { Finished(Stack("scrap", 1), Stack("supplies", 1)) };
        s.ReturnReport = Report(s); s.ReturnReport.Items = new[] { new SavedReportItem { Id = "supplies", Before = 2, After = 0 }, new SavedReportItem { Id = "scrap", Before = 0, After = 1 } };
        return s;
    }
    static bool HasSupplies(CampaignSaveData s) => s.Members.Any(p => p.Bag.Any(i => i.Id == "supplies")) || s.Searches.Any(r => r.Loot.Any(i => i.Id == "supplies")) || (s.ReturnReport != null && s.ReturnReport.Items.Any(i => i.Id == "supplies"));
    static void Retired(CampaignSaveData s, SettlementController cat, string what)
    {
        Check(s.Version == CampaignPersistence.CurrentVersion, what + ": ended at v" + s.Version);
        Check(!HasSupplies(s), what + ": supplies left in the save");
        Check(s.Members[0].Bag.Length == 1 && s.Members[0].Bag[0].Id == "scrap" && s.Members[0].Bag[0].Count == 1, what + ": bag scrap lost");
        Check(s.Searches.Length == 1 && s.Searches[0].Complete && s.Searches[0].Loot.Length == 1 && s.Searches[0].Loot[0].Id == "scrap" && s.Searches[0].Loot[0].Count == 1, what + ": search loot scrap lost");
        Check(s.ReturnReport != null && s.ReturnReport.Items.Length == 1 && s.ReturnReport.Items[0].Id == "scrap" && s.ReturnReport.Items[0].After == 1, what + ": report scrap lost");
        CampaignPersistence.Validate(s, cat);
        var json = JsonUtility.ToJson(s); Check(!json.Contains("\"Supplies\"") && !json.Contains("\"supplies\""), what + ": JSON still mentions supplies");
    }

    public static string RetiredSupplies()
    {
        Check(CampaignPersistence.CurrentVersion == 15, "Save version " + CampaignPersistence.CurrentVersion);
        var cat = Catalog(); var done = new List<string>();
        Check(!cat.InventoryPanel.Items.Any(i => i.Id == "supplies"), "InventoryPanel still lists supplies: run BuildRetireSupplies first");
        Check(cat.InventoryPanel.Items.Any(i => i.Id == "scrap"), "Fixture item scrap missing from InventoryPanel");

        // 1. In memory: v13 and v12 saves holding supplies upgrade to v14 without it; scrap stays; Validate passes.
        var v13 = WithSupplies(cat, 13); CampaignPersistence.Upgrade(v13, cat); Retired(v13, cat, "v13 in memory");
        var v12 = WithSupplies(cat, 12); CampaignPersistence.Upgrade(v12, cat); Retired(v12, cat, "v12 in memory");
        One(v12.SiteBoards, Fresh, "v12 with supplies: search evidence gives one fresh board");
        done.Add("v13/v12 in memory → v14, supplies dropped, scrap kept");

        string dir = Path.GetFullPath("Temp/SiteSaveVerification/RetiredSupplies");
        CampaignSaveStore.TestDirectory = dir;
        try
        {
            // 2. On disk: a v13 file with the old "Supplies" key and supplies rows loads (slot list / Continue) at v14.
            if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
            string payload = JsonUtility.ToJson(WithSupplies(cat, 13)), old = payload.Replace("\"Minute\":600,", "\"Minute\":600,\"Supplies\":3,");
            Check(old != payload && old.Contains("\"supplies\""), "Old v13 payload not built");
            WriteRaw(old);
            var read = CampaignSaveStore.Read(0, cat); Check(read.CanLoad, "v13 file with supplies: " + read.Error);
            Retired(read.Data, cat, "v13 file");
            Check(CampaignSaveStore.Latest(cat) != null, "Continue finds no loadable slot");
            done.Add("v13 file (old Supplies key) loads at v14");

            // 3. A v14 save that still holds supplies is refused: Write, a hand-written file, and Upgrade (no stripping at v14).
            var cases = new (string name, Action<CampaignSaveData> spoil, string error)[]
            {
                ("v14 bag", s => s.Members[0].Bag = new[] { Stack("supplies", 1) }, UnknownItem),
                ("v14 search loot", s => s.Searches = new[] { Finished(Stack("supplies", 1)) }, UnknownItem),
                ("v14 return report", s => { s.ReturnReport = Report(s); s.ReturnReport.Items = new[] { new SavedReportItem { Id = "supplies", Before = 1, After = 0 } }; }, BadReportItem),
            };
            foreach (var k in cases)
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                var s = Save(cat, CampaignPersistence.CurrentVersion); s.SiteBoards = new SavedSiteBoard[0]; k.spoil(s);
                Check(!CampaignSaveStore.Write(0, s, cat, out var error), "Accepted: " + k.name);
                Check(error == k.error && !File.Exists(CampaignSaveStore.SlotPath(0)), k.name + ": " + error);
                Directory.CreateDirectory(dir); WriteRaw(JsonUtility.ToJson(s));
                var bad = CampaignSaveStore.Read(0, cat); Check(!bad.CanLoad && bad.Error == k.error, k.name + " file: " + (bad.Error ?? "loaded"));
                string refused = null; try { CampaignPersistence.Upgrade(s, cat); } catch (InvalidOperationException e) { refused = e.Message; }
                Check(refused == k.error, k.name + " upgrade: " + (refused ?? "accepted"));
                done.Add(k.name + " refused");
            }
        }
        finally { CampaignSaveStore.TestDirectory = null; }
        return "PASS retired supplies · " + string.Join(" · ", done);
    }

    // The resident lesson flag rides on the v13+ site board with no version bump (a missing key reads false).
    public static string ResidentLesson()
    {
        var cat = Catalog(); var done = new List<string>();
        var s = Visited(cat); Check(!s.SiteBoards[0].ResidentLessonShown, "A migrated board starts with the lesson shown");
        s.SiteBoards[0].ResidentLessonShown = true;
        var again = JsonUtility.FromJson<CampaignSaveData>(JsonUtility.ToJson(s)); CampaignPersistence.Upgrade(again, cat);
        Check(again.SiteBoards.Length == 1 && again.SiteBoards[0].ResidentLessonShown && Say(again.SiteBoards[0]) == Say(s.SiteBoards[0]), "Lesson flag JSON round trip");
        var json = Regex.Replace(JsonUtility.ToJson(s), @",?""ResidentLessonShown"":(true|false)", ""); Check(!json.Contains("ResidentLessonShown"), "Lesson key not stripped");
        var keyless = JsonUtility.FromJson<CampaignSaveData>(json); keyless.Version = 13; CampaignPersistence.Upgrade(keyless, cat);
        Check(keyless.Version == CampaignPersistence.CurrentVersion && keyless.SiteBoards.Length == 1 && !keyless.SiteBoards[0].ResidentLessonShown, "Key-less v13 board");
        done.Add("JSON round trip, key-less v13 → false");

        var go = EditorUtility.CreateGameObjectWithHideFlags("SiteLessonProbe", HideFlags.HideAndDontSave, typeof(ExpeditionSiteThreat));
        try
        {
            var t = go.GetComponent<ExpeditionSiteThreat>();
            foreach (bool shown in new[] { false, true })
            {
                var b = new SavedSiteBoard { Id = "mall", Remembered = "", IntroShown = true, ResidentLessonShown = shown };
                t.RestoreSaved(b); var e = t.ExportSaved();
                Check(t.ResidentLessonShown == shown && e != null && e.ResidentLessonShown == shown && Say(e) == Say(b), "Component lesson " + shown);
            }
            t.ReviewResetResidentLesson(); Check(!t.ResidentLessonShown && !t.ExportSaved().ResidentLessonShown, "Review reset");
            t.MarkResidentLesson(); Check(t.ResidentLessonShown && t.ExportSaved().ResidentLessonShown, "Mark");
            t.RestoreSaved(null); Check(!t.ResidentLessonShown && t.ExportSaved() == null, "Never visited clears the lesson");
        }
        finally { Object.DestroyImmediate(go); }
        done.Add("component restore/export/mark/reset");
        return "PASS resident lesson flag · " + string.Join(" · ", done);
    }

    // ---- play mode ----
    public static async Task<string> RoundTrip()
    {
        Check(Application.isPlaying, "Play first");
        var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null && c.ArrivalPanel && c.ArrivalPanel.Threat, "Run VerifyFieldTurnPlan.Enter first");
        var log = new List<string>();

        // 1. First visit (asleep): a meeting in the arcade, out through the exit without inspecting anything.
        Check(!c.ArrivalPanel.Threat.Visited, "A new game has not been to the site (run VerifyFieldTurnPlan.Enter first)");
        var a = await Depart(c); var t = a.Threat; var e = a.Encounter;
        Check(t.State != null && t.State.Asleep, "First visit asleep");
        e.OpenThreat("가까운 곳에서 무언가 움직입니다.", "무언가 1", null); await Task.Delay(200);
        await Tap(e.Retreat); await Tap(e.Confirm); await Task.Delay(600);
        Check(!a.IsOpen && t.Visited && t.State == null, "Home through the arcade exit");
        Check(a.Rooms.Inspected.Count == 0 && !a.Rooms.CorridorVisited, "Nothing inspected, corridor not visited");
        if (c.ReturnPanel && c.ReturnPanel.View.activeSelf) await Tap(c.ReturnPanel.Back); await Task.Delay(200);
        var first = t.ExportSaved(); Check(first != null && first.Id == "mall" && !first.IntroShown && first.Danger <= 1 && first.Remembered == "", "After the asleep visit: " + Say(first));
        log.Add("asleep visit, no inspection → " + Say(first));

        // 2. Save and load (the opening guide off, as for any later visit; it comes back from the save): the visit is remembered.
        if (c.Opening) c.Opening.State.Enabled = false;
        var (loaded, saved) = await SaveAndLoad(c, "first save");
        One(saved.SiteBoards, Say(first), "Captured board");
        c = loaded; t = c.ArrivalPanel.Threat;
        Check(t.Visited && t.State == null && Say(t.ExportSaved()) == Say(first), "Loaded board: " + Say(t.ExportSaved()));
        log.Add("load keeps the visit");

        // 3. Second visit: awake, and the site board intro once.
        a = await Depart(c); await Until(() => a.Popup.activeSelf, 2000, "intro");
        Check(!t.State.Asleep && a.PopupTitle.text == t.IntroTitle, "Intro: " + a.PopupTitle.text);
        await Tap(a.PopupBack); await Task.Delay(400); Check(!a.Popup.activeSelf, "Intro once");
        log.Add("second visit awake, intro once");

        // 4. Loud turns straight on the board (no dice, it stays home at 위험도 1): 위험도 1+ and the arcade remembered, then home.
        for (int i = 0; i < 12 && !(t.State.Danger >= 1 && t.State.Remembered >= 0); i++) { t.State.MoveParty(FieldSiteState.Arcade); t.State.EndTurn(t.Rules.LoudNoise, false); }
        t.Refresh();
        int danger = t.State.Danger, gauge = t.State.Gauge, room = t.State.Remembered;
        Check(danger >= 1 && room >= 0 && !t.State.Encounter, "Loud turns: 위험도 " + danger + ", room " + room);
        Check(a.FinishReturn(), "Return"); await Task.Delay(500); if (c.ReturnPanel.View.activeSelf) await Tap(c.ReturnPanel.Back); await Task.Delay(200);
        var snap = t.ExportSaved();
        Check(snap != null && snap.IntroShown && snap.Danger == danger && snap.Gauge == gauge && snap.Remembered == CampaignPersistence.SiteRoomIds[room], "Folded board: " + Say(snap));
        log.Add("loud visit → " + Say(snap));

        // 5. Save/load again: the board is unchanged, and the next visit starts from it without the intro.
        (loaded, saved) = await SaveAndLoad(c, "second save");
        One(saved.SiteBoards, Say(snap), "Captured board");
        c = loaded; t = c.ArrivalPanel.Threat;
        Check(Say(t.ExportSaved()) == Say(snap), "Reloaded board: " + Say(t.ExportSaved()) + " / " + Say(snap));
        a = await Depart(c); var st = t.State;
        Check(st != null && !st.Asleep && st.Danger == snap.Danger && st.Gauge == snap.Gauge && st.Remembered == Array.IndexOf(CampaignPersistence.SiteRoomIds, snap.Remembered), "Third visit board: " + (st == null ? "none" : st.Danger + "/" + st.Gauge + "/" + st.Remembered));
        await Task.Delay(500); Check(!(a.Popup.activeSelf && a.PopupTitle.text == t.IntroTitle), "Intro shown again after a load");
        log.Add("third visit carries 위험도 " + st.Danger + " · 소음 " + st.Gauge + " · " + FieldSiteState.RoomNames[st.Remembered] + ", no intro");

        // Home again, so the editor is left in the settlement.
        Check(a.FinishReturn(), "Last return"); await Task.Delay(500); if (c.ReturnPanel.View.activeSelf) await Tap(c.ReturnPanel.Back);
        return "PASS site save round trip · " + string.Join(" · ", log);
    }

    // Capture → isolated slot → Read → Prepare → reload the settlement scene.
    static async Task<(SettlementController, CampaignSaveData)> SaveAndLoad(SettlementController c, string what)
    {
        var saved = CampaignPersistence.Capture(c);
        CampaignSaveStore.TestDirectory = Path.GetFullPath("Temp/SiteSaveVerification/RoundTrip");
        try
        {
            Check(CampaignSaveStore.Write(0, saved, c, out var error), what + " write: " + error);
            var read = CampaignSaveStore.Read(0, c); Check(read.CanLoad && read.Data.Version == CampaignPersistence.CurrentVersion, what + " read: " + read.Error);
            Check(read.Data.SiteBoards.Length == saved.SiteBoards.Length && read.Data.SiteBoards.Select(Say).SequenceEqual(saved.SiteBoards.Select(Say)), what + " boards on disk");
            CampaignPersistence.Prepare(read.Data, c);
        }
        finally { CampaignSaveStore.TestDirectory = null; }
        SceneManager.LoadScene("Settlement"); await Task.Delay(800);
        var loaded = Object.FindAnyObjectByType<SettlementController>(); Check(loaded && loaded != c && loaded.Campaign != null && loaded.ArrivalPanel.Threat, what + ": settlement not reloaded");
        if (loaded.ReturnPanel && loaded.ReturnPanel.View.activeSelf) await Tap(loaded.ReturnPanel.Back);
        return (loaded, saved);
    }

    // ---- helpers (as VerifyBalance1 / VerifyFieldTurnPlan) ----
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
    static async Task Until(Func<bool> done, int ms, string what) { var w = Stopwatch.StartNew(); while (!done()) { if (w.ElapsedMilliseconds > ms) throw new Exception("Timeout: " + what); await Task.Delay(30); } }
    static async Task<ExpeditionArrivalPanel> Depart(SettlementController c)
    {
        await Tap(c.Exit); var plan = c.ExpeditionPanel; int mall = Array.FindIndex(plan.Destinations, d => d.Id == "mall");
        if (mall >= 0) { plan.Markers[mall].onClick.Invoke(); await Task.Delay(100); }
        foreach (var card in plan.Cards) if (!card.Check.gameObject.activeSelf) await Tap(card.Button);
        await Tap(plan.Pack); await Tap(c.PackingPanel.Ready); await Tap(c.PackingPanel.Depart); await Task.Delay(1100);
        Check(c.ArrivalPanel.IsOpen && !c.ArrivalPanel.InTransit, "Arrived"); return c.ArrivalPanel;
    }
}
