using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Balance pass 1 (기획/밸런스-1차.md): the data side of the pass.
// Creatures (BattleCreatures.asset), first-visit encounter rates (ExpeditionEncounterPanel.prefab), the site board's GoneTurns,
// the den shelf and arcade machine drops (Sites[8]/[2]) and two object descriptions (ExpeditionArrivalPanel.prefab), the convenience store (plan),
// the loot tables of the free, prybar and storage objects Sites[0/1/4/5/6/7] and descriptions [5]/[6] overridden in SettlementScreen.prefab (§3-3),
// and the battle reinforcement noise 5 (ExpeditionBattlePanel.prefab; the code default FieldBattleState.ReinforcementNoise is 5 too).
// SettlementScreen never overrides Sites[2]/[8] (step 3 owns them), Room, RequiredTool or the length (must stay 9).
// Run last in any re-apply chain that touches loot (BuildSettlementDevelopment, ConnectCooking, ConnectLifeSupplies, ConnectWoodSalvage):
// it re-detects drift from base-prefab builders. Check with VerifyBalance1.Loot (edit mode).
// Idempotent: prints every value it changes (before → after); "Already applied." when nothing changes.
public static class BuildBalance1
{
    const string P = "Assets/Prefabs/Settlement/";
    static readonly List<string> log = new List<string>();
    static void Set<T>(string what, ref T field, T value) { if (!EqualityComparer<T>.Default.Equals(field, value)) { log.Add(what + " " + field + "→" + value); field = value; } }
    static ExpeditionLootPanel.Drop D(string id, int count, int chance) => new ExpeditionLootPanel.Drop { Id = id, Count = count, Chance = chance };
    static string Drops(ExpeditionLootPanel.Drop[] d) => d == null ? "(none)" : string.Join(" ", d.Select(x => x.Id + " " + x.Count + "@" + x.Chance));

    // §3-3: free < tool < risk; duplicates adjacent. Site 0's 100% rows alone must pay the opening chain, so visit 1 stays deterministic.
    static ExpeditionLootPanel.Drop[] SiteTable(int s)
    {
        switch (s)
        {
            case 0: return new[] { D("wood", 9, 100), D("wood", 2, 50), D("scrap", 7, 100), D("scrap", 1, 50), D("cloth", 3, 100) };                      // 물자 상자 (2026-09-25 사용자: 도입 체인보다 여유 있게 · 목재 +2 · 고철 +2 · 천 +1)
            case 1: return new[] { D("food", 2, 100), D("food", 2, 70), D("water", 1, 100), D("water", 2, 70), D("wood", 2, 75), D("scrap", 2, 60) }; // 낡은 탁자
            case 4: return new[] { D("scrap", 3, 85), D("nails", 3, 75) };                                                                          // 배전함 (지렛대)
            case 5: return new[] { D("wood", 4, 75), D("scrap", 3, 75), D("cloth", 3, 70), D("raw-water", 3, 75) };                                  // 복도 보관 상자
            case 6: return new[] { D("can", 3, 85), D("water", 3, 80), D("bandage", 1, 55), D("ammo", 1, 35) };                                      // 보관실 선반
            case 7: return new[] { D("wood", 8, 90), D("rope", 2, 80), D("nails", 3, 75), D("cloth", 2, 65) };                                       // 보관실 자재
            default: throw new ArgumentOutOfRangeException(nameof(s));
        }
    }
    static readonly int[] ScreenSites = { 0, 1, 4, 5, 6, 7 };
    static readonly int[] SiteRooms = { 0, 0, 0, -1, 1, 1, 2, 2, 1 };
    // The opening chain (SettlementOpeningChapter): 창고·작업대 복구, 밧줄, 못 ×2, 지렛대. Only raw materials count; the chain crafts its rope and nails.
    static readonly (string id, int times)[] Chain = { ("build-stock", 1), ("build-bench", 1), ("rope", 1), ("nails", 2), ("prybar", 1) };
    const string TableText = "낡은 탁자 위에 먹을 것과 물이 남아 있다.\n판자와 고철도 뜯어낼 수 있겠다.";

    static void CheckLayout(ExpeditionLootPanel loot)
    {
        if (loot.Sites == null || loot.Sites.Length != SiteRooms.Length) throw new Exception("Expected 9 field sites, found " + (loot.Sites == null ? 0 : loot.Sites.Length) + " (BuildLoot/ConnectCorridorSearch/ConnectStorageRoom re-run?)");
        for (int i = 0; i < SiteRooms.Length; i++) if (loot.Sites[i].Room != SiteRooms[i]) throw new Exception("Unexpected site layout: Sites[" + i + "].Room " + loot.Sites[i].Room + " (expected " + SiteRooms[i] + ")");
        if (loot.Sites[2].RequiredTool != "prybar" || loot.Sites[4].RequiredTool != "prybar") throw new Exception("Unexpected site layout: prybar sites 2/4 changed");
    }
    // Raw need of the chain from the real recipes; the crate's 100% rows must cover it, and the chain must craft enough of its own intermediates.
    static void CheckChain(ExpeditionLootPanel.Drop[] crate, SettlementCraftPanel craft)
    {
        var need = new Dictionary<string, int>(); var made = Chain.ToDictionary(x => x.id, x => x.times); var used = new Dictionary<string, int>();
        foreach (var (id, times) in Chain)
        {
            var recipe = craft.Recipes.FirstOrDefault(r => r.Id == id) ?? throw new Exception("Missing recipe " + id);
            foreach (var cost in recipe.Costs) { var into = made.ContainsKey(cost.MaterialId) ? used : need; into[cost.MaterialId] = (into.TryGetValue(cost.MaterialId, out int v) ? v : 0) + cost.Count * times; }
        }
        foreach (var kv in used) if (made[kv.Key] < kv.Value) throw new Exception("Opening chain crafts too few " + kv.Key + ": " + made[kv.Key] + "/" + kv.Value);
        foreach (var kv in need) { int sure = crate.Where(x => x.Id == kv.Key && x.Chance >= 100).Sum(x => x.Count); if (sure < kv.Value) throw new Exception("Opening chain not guaranteed by 물자 상자: " + kv.Key + " " + sure + "/" + kv.Value); }
    }

    public static string Run()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Stop Play and preserve the scene first");
        log.Clear();
        // Before any write: the SettlementScreen site layout this pass assumes, and the new crate against the real recipes.
        var screenAsset = AssetDatabase.LoadAssetAtPath<GameObject>(P + "SettlementScreen.prefab"); var sa = screenAsset ? screenAsset.GetComponent<SettlementController>() : null;
        if (!sa || !sa.ArrivalPanel || !sa.ArrivalPanel.Loot || !sa.CraftPanel) throw new Exception("SettlementScreen loot/craft references missing");
        CheckLayout(sa.ArrivalPanel.Loot); CheckChain(SiteTable(0), sa.CraftPanel);
        // 1. Creatures: read the telegraph and most fights were free; the resident was the easiest fight of all.
        var roster = AssetDatabase.LoadAssetAtPath<BattleCreatureRoster>("Assets/Data/BattleCreatures.asset") ?? throw new Exception("Missing roster");
        BattleCreature C(string prefix) => roster.Creatures.First(c => c.Id.StartsWith(prefix));
        var c01 = C("01"); Set("01 문지기 Damage", ref c01.Damage, 2); Set("01 Weight", ref c01.Weight, 2);
        var c02 = C("02"); Set("02 귀기울임 Health", ref c02.Health, 6); Set("02 HitChance", ref c02.HitChance, 75); Set("02 StartDepth", ref c02.StartDepth, 1); Set("02 Weight", ref c02.Weight, 0);
        var c03 = C("03"); Set("03 식탁밑 Weight", ref c03.Weight, 3);
        var c04 = C("04"); Set("04 틈새개 Weight", ref c04.Weight, 3);
        var c05 = C("05"); Set("05 널린것 Health", ref c05.Health, 5); Set("05 Damage", ref c05.Damage, 2); Set("05 Weight", ref c05.Weight, 3);
        var c06 = C("06"); Set("06 계량원 Health", ref c06.Health, 5); Set("06 Armor", ref c06.Armor, 0); Set("06 StartDepth", ref c06.StartDepth, 1); Set("06 Weight", ref c06.Weight, 2);
        var c07 = C("07"); Set("07 먼지둥지 HitChance", ref c07.HitChance, 75); Set("07 Weight", ref c07.Weight, 3);
        var c08 = C("08"); Set("08 고인사람 Damage", ref c08.Damage, 2); Set("08 Weight", ref c08.Weight, 2);
        var c09 = C("09"); Set("09 계단등 Armor", ref c09.Armor, 1); Set("09 Trait", ref c09.Trait, "방어 1 · 뒤에 빈틈"); Set("09 Weight", ref c09.Weight, 1);
        var c10 = C("10"); Set("10 겹친이웃 Damage", ref c10.Damage, 2); Set("10 Weight", ref c10.Weight, 2);
        var c11 = C("11"); Set("11 수신목 Weight", ref c11.Weight, 0);
        var c12 = C("12"); Set("12 빈수레 Armor", ref c12.Armor, 0); Set("12 Damage", ref c12.Damage, 2); Set("12 StartDepth", ref c12.StartDepth, 1); Set("12 Weight", ref c12.Weight, 2);
        EditorUtility.SetDirty(roster);

        // 2. First visit (random encounters): a near-certain fight with two creatures half the time → rarer, one creature.
        var enc = PrefabUtility.LoadPrefabContents(P + "ExpeditionEncounterPanel.prefab");
        try
        {
            var e = enc.GetComponent<ExpeditionEncounterPanel>(); int n = log.Count;
            Set("조우 BaseChance", ref e.BaseChance, 10); Set("조우 MaximumChance", ref e.MaximumChance, 45);
            Set("조우 NoiseThreshold", ref e.NoiseThreshold, 4); Set("조우 ChancePerNoise", ref e.ChancePerNoise, 4); Set("조우 GraceSearches", ref e.GraceSearches, 3);
            Set("조우 MaxRandomEnemies", ref e.MaxRandomEnemies, 1);
            if (log.Count > n) { EditorUtility.SetDirty(e); PrefabUtility.SaveAsPrefabAsset(enc, P + "ExpeditionEncounterPanel.prefab"); }
        }
        finally { PrefabUtility.UnloadPrefabContents(enc); }

        // 3. The site: the resident stays gone a turn longer after a fight; the risky shelf and the prybar machine pay in the bottleneck items.
        var arr = PrefabUtility.LoadPrefabContents(P + "ExpeditionArrivalPanel.prefab");
        try
        {
            var a = arr.GetComponent<ExpeditionArrivalPanel>(); var t = arr.GetComponent<ExpeditionSiteThreat>(); int n = log.Count;
            Set("그것 GoneTurns", ref t.Rules.GoneTurns, 4);
            // The den shelf closes on the turn it comes home (FieldSiteState.DenStaysEmpty): the door marker and popup say so.
            Set("관리실 표식 DenComing", ref t.DenComing, "곧 돌아옴");
            Set("관리실 문구 DenReturning", ref t.DenReturning, "무언가가 관리실로 돌아오는 중입니다.\n이번 턴에는 선반을 뒤질 수 없습니다.");
            int den = t.DenSite; var shelf = new[] { D("rope", 2, 75), D("nails", 2, 60), D("bandage", 2, 70), D("ammo", 2, 70) };
            if (Drops(a.Loot.Sites[den].Drops) != Drops(shelf)) { log.Add("관리실 선반 " + Drops(a.Loot.Sites[den].Drops) + " → " + Drops(shelf)); a.Loot.Sites[den].Drops = shelf; EditorUtility.SetDirty(a.Loot); }
            var machine = new[] { D("scrap", 3, 85), D("ammo", 2, 55), D("nails", 2, 60) };
            if (Drops(a.Loot.Sites[2].Drops) != Drops(machine)) { log.Add("오락기 " + Drops(a.Loot.Sites[2].Drops) + " → " + Drops(machine)); a.Loot.Sites[2].Drops = machine; EditorUtility.SetDirty(a.Loot); }
            var d = a.ObjectDescriptions.ToArray();
            Set("설명[0]", ref d[0], "뚜껑이 닫힌 물자 상자다.\n목재와 고철, 천이 들어 있을 것 같다.");
            Set("설명[1]", ref d[1], TableText); // SettlementScreen inherits [0]/[1] from here (VerifyBalance1.Loot checks the effective text)
            // Fields added after the prefab was last saved are not in its file yet; the editor may still hold an older default for them.
            if (!System.IO.File.ReadAllText(P + "ExpeditionArrivalPanel.prefab").Contains("DenComing:")) log.Add("관리실 문구 저장");
            if (log.Count > n) { a.ObjectDescriptions = d; EditorUtility.SetDirty(a); EditorUtility.SetDirty(t); PrefabUtility.SaveAsPrefabAsset(arr, P + "ExpeditionArrivalPanel.prefab"); }
        }
        finally { PrefabUtility.UnloadPrefabContents(arr); }

        // 4. The convenience store had a route marked open, so packing ended in a refusal: no route yet, like the warehouse.
        var plan = PrefabUtility.LoadPrefabContents(P + "ExpeditionPlanPanel.prefab");
        try
        {
            var p = plan.GetComponent<ExpeditionPlanPanel>(); var store = p.Destinations.First(x => x.Id == "store"); int n = log.Count;
            Set("편의점 Accessible", ref store.Accessible, false); Set("편의점 Description", ref store.Description, "아직 가는 길을 모르는 곳");
            Set("편의점 Unknown", ref store.Unknown, "주변에서 경로를 찾아야 합니다."); Set("편의점 Risk", ref store.Risk, "미확인");
            if (log.Count > n) { EditorUtility.SetDirty(p); PrefabUtility.SaveAsPrefabAsset(plan, P + "ExpeditionPlanPanel.prefab"); }
        }
        finally { PrefabUtility.UnloadPrefabContents(plan); AssetDatabase.SaveAssets(); }

        // 5. Loot of the free, prybar and storage objects: the SettlementScreen overrides hide the Arrival/base values for these sites.
        //    Writes only Sites[0/1/4/5/6/7].Drops and descriptions [5]/[6]; never Sites[2]/[8], Room, RequiredTool or the length.
        var screen = PrefabUtility.LoadPrefabContents(P + "SettlementScreen.prefab");
        try
        {
            var sc = screen.GetComponent<SettlementController>(); var a = sc.ArrivalPanel; var loot = a.Loot; int n = log.Count;
            CheckLayout(loot);
            foreach (int i in ScreenSites)
            {
                var want = SiteTable(i);
                if (Drops(loot.Sites[i].Drops) != Drops(want)) { log.Add(a.ObjectNames[i] + " " + Drops(loot.Sites[i].Drops) + " → " + Drops(want)); loot.Sites[i].Drops = want; }
            }
            CheckChain(loot.Sites[0].Drops, sc.CraftPanel);
            var d = a.ObjectDescriptions.ToArray(); int texts = log.Count;
            Set("설명[5]", ref d[5], "도구 없이 상자를 열어\n자재와 물을 살펴봅니다.");
            Set("설명[6]", ref d[6], "남겨진 통조림과 물, 붕대를\n선반에서 찾아봅니다.");
            if (log.Count > texts) a.ObjectDescriptions = d;
            if (log.Count > n)
            {
                EditorUtility.SetDirty(loot); EditorUtility.SetDirty(a);
                PrefabUtility.RecordPrefabInstancePropertyModifications(loot); PrefabUtility.RecordPrefabInstancePropertyModifications(a);
                PrefabUtility.SaveAsPrefabAsset(screen, P + "SettlementScreen.prefab");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(screen); }

        // 6. Battle: the noise that calls the first reinforcement 4 → 5 (the code default changed with it; SyncBattleRules compares the two).
        var battle = PrefabUtility.LoadPrefabContents(P + "ExpeditionBattlePanel.prefab");
        try
        {
            var panel = battle.GetComponent<ExpeditionBattlePanel>(); int n = log.Count;
            Set("전투 ReinforcementNoise", ref panel.Rules.ReinforcementNoise, 5);
            if (log.Count > n) { EditorUtility.SetDirty(panel); PrefabUtility.SaveAsPrefabAsset(battle, P + "ExpeditionBattlePanel.prefab"); }
        }
        finally { PrefabUtility.UnloadPrefabContents(battle); AssetDatabase.SaveAssets(); }
        return log.Count == 0 ? "Already applied." : "Applied: " + string.Join("; ", log);
    }
}
