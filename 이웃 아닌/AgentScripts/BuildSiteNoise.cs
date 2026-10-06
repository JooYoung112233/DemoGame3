using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 속도 삭제와 사물 소음 (기획/탐험-수색쪽지와-협동-1차.md '규칙 변경' · '추가 결정', 2026-09-25): the data, the 07 window
// without the pace row and the texts.
//  1. Each object's noise per search turn and base turns (ExpeditionLootPanel.Site.Noise / Turns) by site index, on the loot panel nested in
//     ExpeditionArrivalPanel.prefab. SettlementScreen.prefab inherits them (it never overrides Noise/Turns); step 4 checks the values the game
//     reads there and repairs a drifted override on the sites SettlementScreen owns (0/1/4/5/6/7) — never 2/8 (BuildBalance1's ownership rule).
//  2. Prying the locked storage door: ExpeditionRoomNavigation.UnlockNoise 3 (ExpeditionArrivalPanel.prefab).
//  3. The first-visit random encounter by search turns (ExpeditionEncounterPanel.prefab): WarnSearches, BaseChance, ChancePerSearch,
//     ChancePerNoise, MaximumChance, NoiseThreshold 0. BuildBalance1 keeps only GraceSearches / MaxRandomEnemies. Tune with SimulateFieldSite.FirstVisit
//     (and FirstVisitTune), then change the constants below.
//  5. The 07 window without the pace row (ExpeditionSearchPanel.prefab): PaceHeading becomes the object's noise line 'ObjectNoise'
//     (ExpeditionSearchPanel.ObjectNoise; the nested font overrides stay with it), Pace_0..2 are hidden (the Paces array stays for older
//     scripts), the worker and role rows move up into the freed space (absolute y). The panel's and SearchDropPreview.prefab's strings.
//  6. The texts (ExpeditionArrivalPanel.prefab): FieldTurnTexts as the code defaults (함께 '1턴 빨리', 망보기 '소음 N→N−1', no '속도'; the
//     removed PaceNames array leaves the file on save), FieldTurnWarning.LineFirstVisit without '소리가 커서'.
//  7. What SettlementScreen.prefab shows for 5-6; a difference there is an override and stops the builder.
// Order: after every existing builder — BuildLoot, ConnectCorridorSearch, ConnectStorageRoom and BuildFieldStrategy rebuild Sites (Noise/Turns
// back to 0), BuildBalance1 owns the loot tables and BuildSearch rebuilds the 07 window (the pace row comes back until this runs again).
// Idempotent: prints each change (before → after); "Already applied." when nothing changes. Check: VerifySiteNoise.Data.
public static class BuildSiteNoise
{
    const string P = "Assets/Prefabs/Settlement/";
    // By site index (CampaignPersistence.SiteIds): 물자 상자, 낡은 탁자, 오락기 뒤판(지렛대), 오락실 문(수색 대상 아님), 배전함(지렛대),
    // 복도 보관 상자, 보관실 선반, 보관실 자재 더미, 관리실 선반. Noise 3 is loud (the resident remembers the room); every object takes 2 turns.
    public static readonly int[] Noise = { 0, 0, 3, 0, 2, 1, 0, 1, 1 };
    public static readonly int[] Turns = { 2, 2, 2, 0, 2, 2, 2, 2, 2 };
    static readonly int[] Rooms = { 0, 0, 0, -1, 1, 1, 2, 2, 1 };
    static readonly int[] ScreenSites = { 0, 1, 4, 5, 6, 7 };
    public const int UnlockNoise = 3;
    // First visit: the warning after 2 search turns, then 40% + 5%p per later search + 5%p per noise, at most 45%.
    // SimulateFieldSite.FirstVisit: 상자만 0% (every role), 세 곳 함께 45%, 두 번 이상 0%, the tutorial crate never warns. The chance is by search
    // turns, so roles that do not shorten a search meet it more often on the three objects (참고, not a target): 망보기 67% (the corridor
    // crate takes 2 turns instead of 1), 조명 / 혼자 90% (6 turns). None of the value sets that meet the targets keeps those in 40-60% too
    // (FirstVisitTune counts them: 0 of 345 on 2026-09-25).
    public const int WarnSearches = 2, BaseChance = 40, ChancePerSearch = 5, ChancePerNoise = 5, MaximumChance = 45, NoiseThreshold = 0, GraceSearches = 3;

    static readonly List<string> log = new List<string>();
    static void Set<T>(string what, ref T field, T value) { if (!EqualityComparer<T>.Default.Equals(field, value)) { log.Add(what + " " + field + "→" + value); field = value; } }
    static string Name(ExpeditionArrivalPanel a, int i) => a.ObjectNames != null && i < a.ObjectNames.Length ? "[" + i + "] " + a.ObjectNames[i] : "Sites[" + i + "]";
    // The full layout is what SettlementScreen reads; ExpeditionArrivalPanel alone still has Rooms -1 and no tool for sites 4-7 (the screen
    // overrides them), so there only the length is checked.
    static void Layout(ExpeditionLootPanel loot, string where, bool full = true)
    {
        if (loot.Sites == null || loot.Sites.Length != Rooms.Length) throw new Exception(where + ": expected 9 field sites, found " + (loot.Sites == null ? 0 : loot.Sites.Length));
        if (!full) return;
        for (int i = 0; i < Rooms.Length; i++) if (loot.Sites[i].Room != Rooms[i]) throw new Exception(where + ": unexpected site layout, Sites[" + i + "].Room " + loot.Sites[i].Room + " (expected " + Rooms[i] + ")");
        if (loot.Sites[2].RequiredTool != "prybar" || loot.Sites[4].RequiredTool != "prybar") throw new Exception(where + ": prybar sites 2/4 changed");
    }

    public static string Run()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Stop Play and preserve the scene first");
        log.Clear();
        // Data: the objects, the lock, the first-visit encounter, then what SettlementScreen reads.
        SitesAndLock(); Encounter(); Effective();
        // The 07 window and the texts, then what SettlementScreen shows.
        Window(); Preview(); Texts(); EffectiveUi();
        return log.Count == 0 ? "Already applied." : "Applied: " + string.Join("; ", log);
    }

    // 1-2. ExpeditionArrivalPanel.prefab: Sites[i].Noise/Turns on the nested loot panel, the lock's noise on the room navigation.
    static void SitesAndLock()
    {
        var arr = PrefabUtility.LoadPrefabContents(P + "ExpeditionArrivalPanel.prefab");
        try
        {
            var a = arr.GetComponent<ExpeditionArrivalPanel>();
            if (!a || !a.Loot || !a.Rooms) throw new Exception("ExpeditionArrivalPanel loot/rooms references missing");
            var loot = a.Loot; int n = log.Count; Layout(loot, "ExpeditionArrivalPanel", false);
            for (int i = 0; i < Noise.Length; i++) { var s = loot.Sites[i]; Set(Name(a, i) + " 소음", ref s.Noise, Noise[i]); Set(Name(a, i) + " 턴", ref s.Turns, Turns[i]); }
            int sites = log.Count;
            Set("잠긴 문 따기 UnlockNoise", ref a.Rooms.UnlockNoise, UnlockNoise);
            if (log.Count > n)
            {
                if (sites > n) { EditorUtility.SetDirty(loot); if (PrefabUtility.IsPartOfPrefabInstance(loot)) PrefabUtility.RecordPrefabInstancePropertyModifications(loot); }
                EditorUtility.SetDirty(a.Rooms); if (PrefabUtility.IsPartOfPrefabInstance(a.Rooms)) PrefabUtility.RecordPrefabInstancePropertyModifications(a.Rooms);
                PrefabUtility.SaveAsPrefabAsset(arr, P + "ExpeditionArrivalPanel.prefab");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(arr); }
    }

    // 3. ExpeditionEncounterPanel.prefab: the first-visit encounter by search turns.
    static void Encounter()
    {
        var enc = PrefabUtility.LoadPrefabContents(P + "ExpeditionEncounterPanel.prefab");
        try
        {
            var e = enc.GetComponent<ExpeditionEncounterPanel>(); if (!e) throw new Exception("ExpeditionEncounterPanel component missing"); int n = log.Count;
            Set("조우 WarnSearches", ref e.WarnSearches, WarnSearches); Set("조우 BaseChance", ref e.BaseChance, BaseChance); Set("조우 ChancePerSearch", ref e.ChancePerSearch, ChancePerSearch);
            Set("조우 ChancePerNoise", ref e.ChancePerNoise, ChancePerNoise); Set("조우 MaximumChance", ref e.MaximumChance, MaximumChance);
            Set("조우 NoiseThreshold", ref e.NoiseThreshold, NoiseThreshold); Set("조우 GraceSearches", ref e.GraceSearches, GraceSearches);
            // Fields added after the prefab was last saved are not in its file yet (the editor shows the code default until a save).
            if (!System.IO.File.ReadAllText(P + "ExpeditionEncounterPanel.prefab").Contains("WarnSearches:")) log.Add("조우 새 값 저장");
            if (log.Count > n) { EditorUtility.SetDirty(e); PrefabUtility.SaveAsPrefabAsset(enc, P + "ExpeditionEncounterPanel.prefab"); }
        }
        finally { PrefabUtility.UnloadPrefabContents(enc); AssetDatabase.SaveAssets(); }
    }

    // 4. What the game reads: SettlementScreen.prefab (its nested arrival panel). A drifted override on a site SettlementScreen owns is
    //    set there; anything else that differs (sites 2/8, the lock, the encounter) is an unexpected override and stops the builder.
    static void Effective()
    {
        const string screenPath = P + "SettlementScreen.prefab";
        AssetDatabase.ImportAsset(screenPath, ImportAssetOptions.ForceUpdate);
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(screenPath); var sc = go ? go.GetComponent<SettlementController>() : null;
        if (!sc || !sc.ArrivalPanel || !sc.ArrivalPanel.Loot || !sc.ArrivalPanel.Rooms || !sc.ArrivalPanel.Encounter) throw new Exception("SettlementScreen arrival loot/rooms/encounter references missing");
        var a = sc.ArrivalPanel; Layout(a.Loot, "SettlementScreen");
        var drift = Enumerable.Range(0, Noise.Length).Where(i => a.Loot.Sites[i].Noise != Noise[i] || a.Loot.Sites[i].Turns != Turns[i]).ToList();
        var foreign = drift.Where(i => !ScreenSites.Contains(i)).ToList();
        if (foreign.Count > 0) throw new Exception("SettlementScreen overrides Noise/Turns of Sites[" + string.Join(",", foreign) + "] (owned by ExpeditionArrivalPanel): revert those overrides");
        var e = a.Encounter;
        string wrong = (a.Rooms.UnlockNoise != UnlockNoise ? " UnlockNoise " + a.Rooms.UnlockNoise : "")
            + (e.WarnSearches != WarnSearches || e.BaseChance != BaseChance || e.ChancePerSearch != ChancePerSearch || e.ChancePerNoise != ChancePerNoise || e.MaximumChance != MaximumChance || e.NoiseThreshold != NoiseThreshold || e.GraceSearches != GraceSearches
                ? " encounter " + e.WarnSearches + "/" + e.BaseChance + "/" + e.ChancePerSearch + "/" + e.ChancePerNoise + "/" + e.MaximumChance + "/" + e.NoiseThreshold + "/" + e.GraceSearches : "");
        if (wrong.Length > 0) throw new Exception("SettlementScreen overrides:" + wrong + " (expected lock " + UnlockNoise + ", encounter " + WarnSearches + "/" + BaseChance + "/" + ChancePerSearch + "/" + ChancePerNoise + "/" + MaximumChance + "/" + NoiseThreshold + "/" + GraceSearches + ")");
        if (drift.Count == 0) return;
        var screen = PrefabUtility.LoadPrefabContents(screenPath);
        try
        {
            var sa = screen.GetComponent<SettlementController>().ArrivalPanel; var loot = sa.Loot; Layout(loot, "SettlementScreen");
            foreach (int i in drift) { var s = loot.Sites[i]; Set("SettlementScreen " + Name(sa, i) + " 소음", ref s.Noise, Noise[i]); Set("SettlementScreen " + Name(sa, i) + " 턴", ref s.Turns, Turns[i]); }
            EditorUtility.SetDirty(loot); PrefabUtility.RecordPrefabInstancePropertyModifications(loot);
            PrefabUtility.SaveAsPrefabAsset(screen, screenPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(screen); }
    }

    // ---- 5-7: the 07 window and the texts (속도 삭제) ----
    const string WindowPath = P + "ExpeditionSearchPanel.prefab", PreviewPath = P + "SearchDropPreview.prefab";
    // The left column's rows (y from the top of the 1080 screen, as BuildSearch lays them out): the noise line takes the old heading's
    // place, the worker and role rows move up by the pace buttons' height; the tool and cost lines stay.
    public const float NoiseY = 433, WorkerHeadingY = 478, WorkersY = 518, DutyHeadingY = 656, DutiesY = 695;
    public const string NoisePlaceholder = "사물 소음";
    // The strings as the code defaults (ExpeditionSearchPanel, SearchDropPreview, FieldTurnWarning); FieldTurnTexts uses new FieldTurnTexts().
    const string NoiseLine = "사물 소음 {0} · 기본 {1}턴", NoiseLoudSuffix = " · 큰 소리", NoiseQuietLine = "소음 없음 · 기본 {1}턴 · 망보기 필요 없음",
        ReviewNoise = "소음 {0}", ReviewQuiet = "소음 없음", FirstCost = "수색도 {0} / {1}턴  ·  누적 소음 {2}", FirstTogether = "함께 수색 · 1턴 빨리",
        FirstAlone = "혼자 수색 · 함께할 동료 없음", FirstWatch = "망보기 · 소음 {0}→{1}", FirstWatchAlone = "망보기 · 망볼 동료 없음 · 소음 {0}",
        FirstLight = "{0} · 조명 +{1}%p", FirstLightNobody = "지원자 없음";
    const string SummaryLine = "남은 {0}턴 · {1}분\n턴당 소음 +{2}", SummaryQuiet = "남은 {0}턴 · {1}분\n소음 없음", CertainSuffix = " · 확정 발견", LightSuffix = " · 조명 +{0}%p",
        TogetherSuffix = " · 함께 +{0}%p", NoteRunning = "진행 중 · 역할과 남은 턴 유지", NoteNew = "각 물건을 개별 판정 · 완료 시 결과 확인";
    public const string LineFirstVisit = "오래 뒤지는 사이 무언가 다가올 수 있습니다";
    static readonly string[] Rows = { "WorkerHeading", "Workers", "DutyHeading" };
    static readonly float[] RowY = { WorkerHeadingY, WorkersY, DutyHeadingY };

    static bool Y(Transform t, float y)
    {
        var r = (RectTransform)t; if (Mathf.Abs(r.anchoredPosition.y + y) < .01f) return false;
        log.Add(t.name + " y " + (-r.anchoredPosition.y) + "→" + y); r.anchoredPosition = new Vector2(r.anchoredPosition.x, -y); return true;
    }
    static Transform Row(Transform workspace, string name) { var t = workspace.Find(name); if (!t) throw new Exception("ExpeditionSearchPanel Workspace/" + name + " missing (run BuildSearch.Build first)"); return t; }

    // 5. ExpeditionSearchPanel.prefab.
    static void Window()
    {
        var root = PrefabUtility.LoadPrefabContents(WindowPath);
        try
        {
            var s = root.GetComponent<ExpeditionSearchPanel>(); if (!s || !s.Workspace || s.Duties == null || s.Duties.Length != 3) throw new Exception("ExpeditionSearchPanel Workspace/Duties missing");
            var ws = s.Workspace.transform; int n = log.Count;
            var line = ws.Find("ObjectNoise");
            if (!line) { line = ws.Find("PaceHeading"); if (!line) throw new Exception("ExpeditionSearchPanel Workspace/PaceHeading (or ObjectNoise) missing"); line.name = "ObjectNoise"; log.Add("PaceHeading → ObjectNoise (사물 소음 줄)"); }
            var text = line.GetComponent<Text>(); if (!text) throw new Exception("ObjectNoise has no Text");
            if (s.ObjectNoise != text) { s.ObjectNoise = text; log.Add("ExpeditionSearchPanel.ObjectNoise 연결"); }
            if (!line.gameObject.activeSelf) { line.gameObject.SetActive(true); log.Add("ObjectNoise 표시"); }
            if (text.text != NoisePlaceholder) { log.Add("ObjectNoise 문구 '" + text.text + "'→'" + NoisePlaceholder + "'"); text.text = NoisePlaceholder; }
            Y(line, NoiseY);
            if (s.Paces != null) foreach (var b in s.Paces) if (b && b.gameObject.activeSelf) { b.gameObject.SetActive(false); log.Add(b.name + " 숨김"); }
            for (int i = 0; i < Rows.Length; i++) Y(Row(ws, Rows[i]), RowY[i]);
            foreach (var d in s.Duties) Y(d.transform, DutiesY);
            Set("07 NoiseLine", ref s.NoiseLine, NoiseLine); Set("07 NoiseLoudSuffix", ref s.NoiseLoudSuffix, NoiseLoudSuffix); Set("07 NoiseQuietLine", ref s.NoiseQuietLine, NoiseQuietLine);
            Set("07 ReviewNoise", ref s.ReviewNoise, ReviewNoise); Set("07 ReviewQuiet", ref s.ReviewQuiet, ReviewQuiet); Set("07 FirstCost", ref s.FirstCost, FirstCost);
            Set("07 FirstTogether", ref s.FirstTogether, FirstTogether); Set("07 FirstAlone", ref s.FirstAlone, FirstAlone); Set("07 FirstWatch", ref s.FirstWatch, FirstWatch);
            Set("07 FirstWatchAlone", ref s.FirstWatchAlone, FirstWatchAlone);
            Set("07 FirstLight", ref s.FirstLight, FirstLight); Set("07 FirstLightNobody", ref s.FirstLightNobody, FirstLightNobody);
            // Fields added after the prefab was last saved are not in its file yet.
            var yaml = File.ReadAllText(WindowPath);
            if (!yaml.Contains("ObjectNoise:") || !yaml.Contains("FirstWatchAlone:")) log.Add("07 새 필드 저장");
            if (log.Count > n) { EditorUtility.SetDirty(s); PrefabUtility.SaveAsPrefabAsset(root, WindowPath); }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // 5. SearchDropPreview.prefab (nested in the 07 window; its strings are not overridden there).
    static void Preview()
    {
        var root = PrefabUtility.LoadPrefabContents(PreviewPath);
        try
        {
            var p = root.GetComponent<SearchDropPreview>(); if (!p) throw new Exception("SearchDropPreview component missing"); int n = log.Count;
            Set("미리보기 SummaryLine", ref p.SummaryLine, SummaryLine); Set("미리보기 SummaryQuiet", ref p.SummaryQuiet, SummaryQuiet); Set("미리보기 CertainSuffix", ref p.CertainSuffix, CertainSuffix);
            Set("미리보기 LightSuffix", ref p.LightSuffix, LightSuffix); Set("미리보기 TogetherSuffix", ref p.TogetherSuffix, TogetherSuffix);
            Set("미리보기 NoteRunning", ref p.NoteRunning, NoteRunning); Set("미리보기 NoteNew", ref p.NoteNew, NoteNew);
            if (!File.ReadAllText(PreviewPath).Contains("SummaryQuiet:")) log.Add("미리보기 새 필드 저장");
            if (log.Count > n) { EditorUtility.SetDirty(p); PrefabUtility.SaveAsPrefabAsset(root, PreviewPath); }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // The FieldTurnTexts strings this change owns (compared with the code defaults).
    static readonly string[] TextNames = { "CostTogether", "CostWatch", "CostTogetherBonus", "CostWatchSilent", "NoticeLocked", "ReviewTitle" };
    static string TextOf(FieldTurnTexts tx, string name) => (string)typeof(FieldTurnTexts).GetField(name).GetValue(tx);

    // 6. ExpeditionArrivalPanel.prefab: the planner's texts and the first-visit warning line.
    static void Texts()
    {
        const string path = P + "ExpeditionArrivalPanel.prefab";
        var arr = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var a = arr.GetComponent<ExpeditionArrivalPanel>(); var pl = a && a.Threat ? a.Threat.Planner : null;
            if (!pl || pl.Texts == null) throw new Exception("ExpeditionArrivalPanel Threat.Planner.Texts missing");
            var w = arr.GetComponentInChildren<FieldTurnWarning>(true); if (!w) throw new Exception("FieldTurnWarning missing in ExpeditionArrivalPanel.prefab");
            int n = log.Count; var want = new FieldTurnTexts();
            foreach (var name in TextNames)
            {
                var f = typeof(FieldTurnTexts).GetField(name); string was = (string)f.GetValue(pl.Texts), now = (string)f.GetValue(want);
                if (was != now) { log.Add("FieldTurnTexts." + name + " '" + was + "'→'" + now + "'"); f.SetValue(pl.Texts, now); }
            }
            Set("FieldTurnWarning.LineFirstVisit", ref w.LineFirstVisit, LineFirstVisit);
            var yaml = File.ReadAllText(path);
            if (yaml.Contains("PaceNames:")) log.Add("PaceNames 제거 (속도 이름)");
            if (!yaml.Contains("CostWatchSilent:")) log.Add("FieldTurnTexts 새 문구 저장");
            if (log.Count > n)
            {
                foreach (var c in new Component[] { pl, w }) { EditorUtility.SetDirty(c); if (PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c); }
                PrefabUtility.SaveAsPrefabAsset(arr, path);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(arr); }
    }

    // 7. What SettlementScreen.prefab shows (its nested arrival panel and 07 window). Any difference is an override: stop and name it.
    static void EffectiveUi()
    {
        const string screenPath = P + "SettlementScreen.prefab";
        AssetDatabase.SaveAssets(); AssetDatabase.ImportAsset(screenPath, ImportAssetOptions.ForceUpdate);
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(screenPath); var sc = go ? go.GetComponent<SettlementController>() : null;
        var a = sc ? sc.ArrivalPanel : null; var s = a ? a.Search : null; var pl = a && a.Threat ? a.Threat.Planner : null; var w = a ? a.GetComponentInChildren<FieldTurnWarning>(true) : null;
        if (!s || !s.Workspace || s.Duties == null || !pl || !w || !s.DropPreview) throw new Exception("SettlementScreen search window / planner / warning / preview missing");
        var wrong = new List<string>(); var ws = s.Workspace.transform;
        float YOf(Transform t) => -((RectTransform)t).anchoredPosition.y;
        if (!s.ObjectNoise || s.ObjectNoise.name != "ObjectNoise" || !s.ObjectNoise.gameObject.activeSelf || Mathf.Abs(YOf(s.ObjectNoise.transform) - NoiseY) > .01f) wrong.Add("ObjectNoise");
        if (s.Paces != null && s.Paces.Any(b => b && b.gameObject.activeSelf)) wrong.Add("Pace_* shown");
        for (int i = 0; i < Rows.Length; i++) { var t = ws.Find(Rows[i]); if (!t || Mathf.Abs(YOf(t) - RowY[i]) > .01f) wrong.Add(Rows[i] + " y"); }
        if (s.Duties.Any(d => Mathf.Abs(YOf(d.transform) - DutiesY) > .01f)) wrong.Add("Duty_* y");
        if (s.NoiseLine != NoiseLine || s.NoiseQuietLine != NoiseQuietLine || s.FirstTogether != FirstTogether || s.FirstWatch != FirstWatch || s.FirstWatchAlone != FirstWatchAlone || s.ReviewNoise != ReviewNoise) wrong.Add("07 문구");
        if (s.DropPreview.SummaryLine != SummaryLine || s.DropPreview.SummaryQuiet != SummaryQuiet || s.DropPreview.NoteRunning != NoteRunning) wrong.Add("미리보기 문구");
        var want = new FieldTurnTexts(); foreach (var name in TextNames) if (TextOf(pl.Texts, name) != TextOf(want, name)) wrong.Add("FieldTurnTexts." + name);
        if (w.LineFirstVisit != LineFirstVisit) wrong.Add("FieldTurnWarning.LineFirstVisit");
        if (wrong.Count > 0) throw new Exception("SettlementScreen shows other values for: " + string.Join(", ", wrong) + " (an override there or in the scene: revert it, then run again)");
    }
}
