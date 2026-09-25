using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Disposable save DTOs and an explicit new-game fixture only. Never reads player slots.
// Run while stopped; PlayMode after entering Play. BuildCharacterBalance and
// BuildCharacterUnlockUI must have been applied before either entry point.
public static class VerifyCharacterUnlocks
{
    static readonly string[] Pair = { "scout", "medic" };
    static readonly string[] Recruits = { "mechanic", "cook", "guard", "researcher" };
    static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    static T Copy<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
    [Serializable] sealed class TextValue { public string Value; }
    static string Snapshot(object value)
    {
        if (value == null) return "null";
        if (value is Array array) return "[" + string.Join(",", array.Cast<object>().Select(Snapshot)) + "]";
        if (value is string text) return JsonUtility.ToJson(new TextValue { Value = text });
        return JsonUtility.ToJson(value);
    }
    static bool Same(object a, object b) => Snapshot(a) == Snapshot(b);
    static bool SameIds(string[] a, string[] b) => a.OrderBy(x => x).SequenceEqual(b.OrderBy(x => x));
    static SettlementController C => Object.FindAnyObjectByType<SettlementController>();
    static SettlementController Catalog() => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab").GetComponent<SettlementController>();
    static int OldHealth(string id) => id == "medic" || id == "cook" || id == "guard" ? 4 : 3;
    static int OldBag(string id) => id == "mechanic" || id == "cook" || id == "guard" ? 4 : 3;
    static SavedNpcStory ReturnedStory() => new SavedNpcStory { Stage = 3, Page = 3, Choice = 1, VisitCount = 2, MetVisit = 2, Returned = true };

    static CampaignSaveData Save(SettlementController catalog, int version = 16, params string[] ids)
    {
        if (ids.Length == 0) ids = Pair;
        return new CampaignSaveData {
            Version = version, SavedUtc = "2026-09-25T00:00:00Z", HomeId = "garage", Day = 1, Minute = 600, Ammo = 0,
            IntroductionStep = 10, Development = new SavedDevelopment(), Opening = new SavedOpeningChapter(),
            Members = ids.Select(id => {
                var p = catalog.Roster.Candidates.Single(x => x.Id == id);
                return new SavedMember { Id = id, Name = p.DisplayName, Role = p.RoleTitle, Description = p.Description,
                    Health = version < 16 ? OldHealth(id) : p.Health, Maximum = version < 16 ? OldHealth(id) : p.Health,
                    Aim = version < 16 ? 0 : p.Aim, Capacity = version < 16 ? OldBag(id) : p.BagCapacity, Bag = Array.Empty<SavedCount>() };
            }).ToArray(),
            Materials = catalog.CraftPanel.Materials.Select(m => new SavedCount { Id = m.Id, Count = 0 }).ToArray(),
            Rest = Array.Empty<SavedRest>(), Craft = Array.Empty<SavedCraft>(), Cooking = Array.Empty<SavedCooking>(),
            Visitor = new SavedVisitor(), Inspected = Array.Empty<string>(), Searches = Array.Empty<SavedSearch>(),
            SiteBoards = Array.Empty<SavedSiteBoard>(), Doyun = new SavedNpcStory(), Activity = new[] { "기존 수색 기록" },
            UnlockedCharacters = ids.Concat(Pair).Distinct().OrderBy(x => x).ToArray()
        };
    }

    static void VisitorRecord(CampaignSaveData save, SettlementController catalog, string id)
    {
        var v = catalog.VisitorPanel; save.Day = v.FirstDay; save.Minute = v.ArrivalMinute;
        save.Visitor = new SavedVisitor { VisitDay = save.Day, RecruitId = id,
            Stock = v.Goods.Select(g => new SavedCount { Id = g.Id, Count = g.Initial }).ToArray() };
    }

    public static string Migration()
    {
        var catalog = Catalog(); Check(CampaignPersistence.CurrentVersion == 16, "Expected save v16.");
        var old = Save(catalog, 15, Pair.Concat(Recruits).ToArray());
        foreach (var p in old.Members) p.Health--;
        old.Members.Single(p => p.Id == "medic").Health = 0;
        old.Members.Single(p => p.Id == "scout").Health = 1;
        var guard = old.Members.Single(p => p.Id == "guard");
        guard.Bag = new[] { "wood", "cloth", "water", "bandage" }.Select(id => new SavedCount { Id = id, Count = 1 }).ToArray();
        old.ReturnReport = new SavedReport { Destination = "폐상가", Body = "이전 귀환 기록", Minutes = 40,
            Members = old.Members.Select(p => new SavedReportMember { Id = p.Id, Maximum = p.Maximum, Before = p.Maximum - 1, After = p.Health }).ToArray(),
            Items = Array.Empty<SavedReportItem>() };
        CampaignPersistence.Validate(old, catalog); var before = Copy(old);
        CampaignPersistence.Upgrade(old, catalog); CampaignPersistence.Validate(old, catalog);
        Check(old.Version == 16 && SameIds(old.UnlockedCharacters, Pair.Concat(Recruits).ToArray()), "Joined legacy people were not grandfathered.");
        foreach (var p in old.Members) {
            var source = before.Members.Single(x => x.Id == p.Id); var data = catalog.Roster.Candidates.Single(x => x.Id == p.Id);
            int expected = source.Health == 0 ? 0 : Math.Max(1, data.Health - (source.Maximum - source.Health));
            Check(p.Maximum == data.Health && p.Health == expected && p.Aim == data.Aim, "Legacy injury/death/aim changed incorrectly: " + p.Id);
            Check(p.Capacity == Math.Max(data.BagCapacity, source.Bag.Length) && Same(p.Bag, source.Bag), "Legacy bag lost a stack/capacity: " + p.Id);
        }
        Check(Same(old.ReturnReport, before.ReturnReport), "Historical return-report numbers were rewritten instead of preserving the snapshot.");
        Check(Same(old.Materials, before.Materials) && Same(old.Activity, before.Activity) && Same(old.Doyun, before.Doyun), "Unrelated legacy state changed.");
        var upgraded = Copy(old); CampaignPersistence.Upgrade(old, catalog);
        Check(Same(old, upgraded), "A second upgrade reapplied the stat increase.");

        var custom = Save(catalog, 15); custom.Members[0].Maximum = 13; custom.Members[0].Health = 4; custom.Members[0].Aim = 17; custom.Members[0].Capacity = 9;
        var customBefore = Copy(custom.Members[0]); CampaignPersistence.Upgrade(custom, catalog);
        Check(Same(custom.Members[0], customBefore), "Non-baseline custom stats were overwritten.");

        var met = Save(catalog, 15, "scout", "medic", "guard"); VisitorRecord(met, catalog, "researcher");
        CampaignPersistence.Upgrade(met, catalog); CampaignPersistence.Validate(met, catalog);
        Check(SameIds(met.UnlockedCharacters, new[] { "scout", "medic", "guard", "researcher" }), "Existing visitor was locked again, or unmet people were unlocked.");
        Check(!met.Development.Research && !met.Doyun.Returned, "Grandfather fixture incorrectly satisfies the new condition.");
        return "PASS v15 to v16: all six baseline injuries, death zero, old guard's four stacks, original return-report snapshot, custom stats, grandfathered members/visitor, idempotence.";
    }

    public static string Refusals()
    {
        var catalog = Catalog();
        var cases = new (string name, Action<CampaignSaveData> change, string error)[] {
            ("null", s => s.UnlockedCharacters = null, "동료 해금 기록이 올바르지 않습니다."),
            ("unknown", s => s.UnlockedCharacters = Pair.Concat(new[] { "missing-person" }).ToArray(), "동료 해금 기록이 올바르지 않습니다."),
            ("duplicate", s => s.UnlockedCharacters = new[] { "scout", "medic", "scout" }, "동료 해금 기록이 올바르지 않습니다."),
            ("null id", s => s.UnlockedCharacters = new[] { "scout", "medic", null }, "동료 해금 기록이 올바르지 않습니다."),
            ("empty id", s => s.UnlockedCharacters = new[] { "scout", "medic", "" }, "동료 해금 기록이 올바르지 않습니다."),
            ("missing starter/member", s => s.UnlockedCharacters = new[] { "scout" }, "함께하는 동료의 해금 기록이 없습니다."),
            ("missing joined recruit", s => { s.Members = Save(catalog, 16, "scout", "medic", "mechanic").Members; }, "함께하는 동료의 해금 기록이 없습니다."),
            ("missing met visitor", s => VisitorRecord(s, catalog, "researcher"), "방문한 동료의 해금 기록이 없습니다.")
        };
        CampaignPersistence.Validate(Save(catalog), catalog);
        foreach (var entry in cases) {
            var save = Save(catalog); entry.change(save); string error = null;
            try { CampaignPersistence.Validate(save, catalog); } catch (InvalidOperationException e) { error = e.Message; }
            Check(error == entry.error, entry.name + " accepted or failed for an unrelated reason: " + error);
        }
        return "PASS " + cases.Length + " invalid unlock records rejected for the expected reason.";
    }

    public static string Disk()
    {
        string previous = CampaignSaveStore.TestDirectory;
        try {
            CampaignSaveStore.TestDirectory = Path.GetFullPath("Temp/RoleBalanceSlots/Edit"); var catalog = Catalog();
            var legacy = Save(catalog, 15, "scout", "medic", "guard"); VisitorRecord(legacy, catalog, "researcher");
            Check(CampaignSaveStore.Write(1, legacy, catalog, out var error), "Legacy Temp write: " + error);
            var read = CampaignSaveStore.Read(1, catalog);
            Check(read.CanLoad && read.Data.Version == 16 && SameIds(read.Data.UnlockedCharacters, new[] { "scout", "medic", "guard", "researcher" }), "Legacy file migration: " + read.Error);
            Check(CampaignSaveStore.Write(0, read.Data, catalog, out error), "Current Temp write: " + error);
            var again = CampaignSaveStore.Read(0, catalog);
            Check(again.CanLoad && Same(again.Data, read.Data), "Current file roundtrip changed the migrated state: " + again.Error);
            string intact = File.ReadAllText(CampaignSaveStore.SlotPath(0)); var bad = Copy(read.Data); bad.UnlockedCharacters = new[] { "unknown" };
            Check(!CampaignSaveStore.Write(0, bad, catalog, out error) && error == "동료 해금 기록이 올바르지 않습니다.", "Invalid unlock file write was accepted.");
            Check(File.ReadAllText(CampaignSaveStore.SlotPath(0)) == intact, "Rejected write damaged the valid slot.");
            return "PASS actual legacy/current file migration and invalid-write protection in Temp/RoleBalanceSlots/Edit only.";
        }
        finally { CampaignSaveStore.TestDirectory = previous; }
    }

    public static string Run()
    {
        Check(!Application.isPlaying, "Run the DTO/file fixture while stopped. Use PlayMode for a new live campaign.");
        return Migration() + "\n" + Refusals() + "\n" + Disk();
    }

    static async Task Until(Func<bool> condition, string message)
    {
        for (int i = 0; i < 80; i++) { if (condition()) return; await Task.Delay(50); }
        throw new InvalidOperationException(message);
    }
    static void Unlocked(params string[] ids) => Check(SameIds(C.ExportCharacterUnlocks(), ids), "Wrong unlocked set: " + string.Join(",", C.ExportCharacterUnlocks()));
    static void Housing(bool ready)
    {
        var data = CampaignPersistence.Capture(C); data.SideRoomConnected = ready; data.SideRoomReady = ready;
        C.CraftPanel.RestoreSaved(data); C.RefreshMembers();
    }
    static int Clock() => C.Campaign.Day * 1440 + C.Campaign.MinuteOfDay;
    static string Stock() => string.Join(";", C.CraftPanel.Materials.OrderBy(m => m.Id).Select(m => m.Id + ":" + m.Initial));
    static void Traits()
    {
        foreach (var p in C.Campaign.Party) {
            var data = C.Roster.Candidates.Single(x => x.Id == CampaignPersistence.MemberId(C, p));
            Check(p.MaxHealth == data.Health && p.Aim == data.Aim && p.BagCapacity == data.BagCapacity && Same(p.Traits, data.Traits), "Created/restored stats or effective traits differ: " + data.Id);
            Check(!ReferenceEquals(p.Traits, data.Traits), "Runtime shares mutable catalog traits: " + data.Id);
        }
    }

    public static async Task<string> Start()
    {
        Check(Application.isPlaying, "Enter Play first.");
        CampaignSaveStore.TestDirectory = Path.GetFullPath("Temp/RoleBalanceSlots/Play"); PartySelectionSession.Clear();
        SceneManager.LoadScene("PartySelection"); await Until(() => Object.FindAnyObjectByType<PartySelectionController>(), "Party selection did not load.");
        var selection = Object.FindAnyObjectByType<PartySelectionController>();
        Check(SameIds(PartySelectionSession.Selected.ToArray(), Pair) && selection.Cards.Count(x => x.gameObject.activeInHierarchy) == 2, "The fresh opening exposes extra people.");
        string focus = selection.FocusedCandidateId;
        foreach (string id in Recruits) { selection.Toggle(id); selection.Preview(id); }
        Check(SameIds(PartySelectionSession.Selected.ToArray(), Pair) && selection.FocusedCandidateId == focus, "Direct selection reveals or selects a locked person.");
        selection.Continue.onClick.Invoke(); await Until(() => Object.FindAnyObjectByType<HomeSelectionController>(), "Home selection did not load.");
        var home = Object.FindAnyObjectByType<HomeSelectionController>(); home.Cards[0].Button.onClick.Invoke(); home.Continue.onClick.Invoke();
        await Until(() => C && C.Campaign != null, "Settlement did not initialize.");
        // Bypass tutorial presentation only; milestones below exercise the actual campaign APIs.
        C.Introduction.Restore(10); C.Opening.State.Enabled = false;
        Unlocked(Pair); Check(C.CraftPanel.ResidentCapacity == 3, "Starting housing is not three places.");
        foreach (string id in Recruits) Check(!C.RecruitResident(id), "Direct recruit bypassed its lock: " + id);
        Check(C.Campaign.Party.Count() == 2, "A refused recruit changed the party."); Traits();
        return "PASS fresh two-person opening, hidden candidates cannot be selected, direct locked recruitment refused, initial capacity three.";
    }

    public static async Task<string> Milestones()
    {
        Check(C && C.Campaign.Party.Count() == 2, "Run Start first.");
        C.Development.Complete("build-bench"); C.Development.Complete("build-cooker"); Housing(true); C.Development.Complete("build-research");
        Unlocked(Pair); // Each facility without the associated return/story must still be locked.
        C.Development.State.Cooker = false; C.Development.State.Research = false; Housing(false);
        C.ArrivalPanel.Story.Restore(ReturnedStory()); Check(!C.IsCharacterUnlocked("researcher"), "Story without research unlocks the analyst.");
        C.ArrivalPanel.Story.Restore(new SavedNpcStory());
        Check(C.ArrivalPanel.Begin(C.Campaign.Party.ToArray(), C.ExpeditionPanel.Destinations.Single(d => d.Id == "mall")), "Actual first departure failed.");
        await Until(() => !C.ArrivalPanel.InTransit, "Arrival did not finish.");
        Check(C.ArrivalPanel.FinishReturn(), "Actual first return failed."); if (C.ReturnPanel.IsOpen) C.ReturnPanel.Close();
        Check(C.ReturnPanel.HasReport, "Actual return produced no report."); Unlocked("scout", "medic", "mechanic");
        await NextVisit(); Check(C.VisitorPanel.RecruitId == "mechanic", "The first eligible visitor is not the mechanic.");
        C.Development.Complete("build-cooker"); Unlocked("scout", "medic", "mechanic", "cook");
        Housing(true); Check(C.CraftPanel.ResidentCapacity == 6, "Prepared side room must permit all six existing people.");
        Unlocked("scout", "medic", "mechanic", "cook", "guard");
        C.Development.Complete("build-research"); Check(!C.IsCharacterUnlocked("researcher"), "Research alone unlocks the analyst.");
        C.ArrivalPanel.Story.Restore(ReturnedStory()); Unlocked(Pair.Concat(Recruits).ToArray());
        C.VisitorPanel.Sync(); Check(C.VisitorPanel.RecruitId == "mechanic", "New unlocks replaced an already introduced visitor.");
        int logs = C.ActivityLog.Count;
        // Remove conditions before recruitment so permanence cannot accidentally come from joined IDs.
        C.Development.State = new SavedDevelopment(); Housing(false); C.ArrivalPanel.Story.Restore(new SavedNpcStory());
        C.Opening.State.FirstReturn = false; var noReport = CampaignPersistence.Capture(C); noReport.ReturnReport = null; C.ReturnPanel.RestoreSaved(noReport);
        Unlocked(Pair.Concat(Recruits).ToArray()); C.RefreshCharacterUnlocks(); C.RefreshCharacterUnlocks();
        Check(C.ActivityLog.Count == logs, "Repeated/permanent unlock checks duplicated announcements.");
        Housing(true); return "PASS all four paired milestones, real expedition return, negative conditions, six-place housing, permanent unlocks without conditions or joined IDs.";
    }

    static async Task NextVisit()
    {
        var v = C.VisitorPanel; v.Sync(); if (v.IsPresent && !v.HasRecruited) return;
        int day = Math.Max(v.FirstDay, C.Campaign.Day);
        int step = Math.Max(1, v.IntervalDays);
        if (day > v.FirstDay) day = v.FirstDay + (day - v.FirstDay + step - 1) / step * step;
        while (day < C.Campaign.Day || day == C.Campaign.Day && (C.Campaign.MinuteOfDay >= v.DepartureMinute || v.HasRecruited || v.VisitDay == day && !v.IsPresent && C.Campaign.MinuteOfDay >= v.ArrivalMinute)) day += step;
        int delta = day * 1440 + v.ArrivalMinute - Clock();
        Check(delta >= 0, "Visitor fixture attempted to rewind time.");
        while (delta > 0) { int minutes = Math.Min(delta, 10080); Check(C.Campaign.AdvanceSettlementTime(minutes), "Could not reach the next visit."); delta -= minutes; }
        v.Sync(); await Task.Delay(80); Check(v.IsPresent && !v.HasRecruited, "Scheduled visitor is not present.");
    }

    public static async Task<string> RecruitAll()
    {
        Unlocked(Pair.Concat(Recruits).ToArray()); Check(C.CraftPanel.ResidentCapacity == 6, "Run Milestones first.");
        foreach (string id in Recruits) {
            await NextVisit(); var v = C.VisitorPanel;
            Check(v.RecruitId == id, "Wrong eligible visitor order: " + v.RecruitId + ", expected " + id);
            int count = C.Campaign.Party.Count(), time = Clock(); string stock = Stock();
            v.Open(); Check(v.View.activeSelf, "Visitor conversation did not open."); v.OpenRecruit();
            Check(v.RecruitPage.activeSelf && v.RecruitAction.interactable, "Recruitment page is blocked despite a free place.");
            v.RequestRecruit(); Check(v.Review.activeSelf, "Recruit confirmation did not open."); v.CancelReview();
            Check(C.Campaign.Party.Count() == count && !v.HasRecruited && Clock() == time && Stock() == stock, "Cancel spent time/items or recruited a person.");
            v.RequestRecruit(); v.Commit();
            Check(C.Campaign.Party.Count() == count + 1 && PartySelectionSession.Selected.Last() == id && v.HasRecruited && !v.IsPresent, "Visitor did not join once/end the visit: " + id);
            var person = C.Campaign.Party.Last();
            Check(person.Health == person.MaxHealth && C.InventoryPanel.Items.All(item => C.InventoryPanel.CountFor(person, item.Id) == 0), "Recruit did not arrive healthy with an empty bag: " + id);
            Check(Clock() == time && Stock() == stock, "Recruiting spent time or granted visitor trade stock.");
            Check(!C.RecruitResident(id), "The same person can be recruited twice."); v.Close(); Traits();
        }
        Check(C.Campaign.Party.Count() == 6 && SameIds(PartySelectionSession.Selected.ToArray(), Pair.Concat(Recruits).ToArray()), "The final analyst could not join the full six-person roster.");
        await NextVisit(); Check(string.IsNullOrEmpty(C.VisitorPanel.RecruitId), "Already joined people reappear as candidates.");
        return "PASS four real scheduled visitor recruitments to six residents, stable order, cancel/no-cost/empty-bag rules, exact traits and duplicate refusal.";
    }

    public static async Task<string> Restore()
    {
        Check(C && C.Campaign.Party.Count() == 6, "Run RecruitAll first.");
        var save = CampaignPersistence.Capture(C); CampaignPersistence.Validate(save, C);
        Check(CampaignSaveStore.Write(0, save, C, out var error), "Live Temp save failed: " + error);
        var read = CampaignSaveStore.Read(0, C); Check(read.CanLoad && Same(read.Data, save), "Live file changed state: " + read.Error);
        var previous = C.GetEntityId(); CampaignPersistence.Prepare(read.Data, C); SceneManager.LoadScene("Settlement");
        await Until(() => C && C.GetEntityId() != previous && C.Campaign != null, "Restored settlement did not initialize.");
        Unlocked(Pair.Concat(Recruits).ToArray()); Traits();
        var restored = CampaignPersistence.Capture(C);
        Check(C.Campaign.Party.Count() == 6 && C.CraftPanel.ResidentCapacity == 6 && Same(restored.Members, save.Members), "Restored six-person identities/stats/inventory changed.");
        Check(Same(restored.Visitor, save.Visitor) && Same(restored.Activity, save.Activity), "Visitor state or announcements changed on load.");
        Check(!C.Development.State.Research && !C.ArrivalPanel.Story.State.Returned, "Permanence fixture accidentally restored current unlock conditions.");
        return "PASS six residents and effective traits survive actual Temp save -> Prepare -> scene load; permanent unlocks/visitor/activity preserved.";
    }

    public static async Task<string> PlayMode() => await Start() + "\n" + await Milestones() + "\n" + await RecruitAll() + "\n" + await Restore();
}
