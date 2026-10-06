using System;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;

// Append alternatives after BuildEverydayGoods registers the new materials/icons.
// Existing recipe IDs, costs, times and tutorial completions are left intact.
public static class BuildEverydayRecipes
{
    const string P = "Assets/Prefabs/Settlement/";
    public static readonly string[] CraftIds = { "water-fuel", "bandage-alcohol", "flashlight-parts", "nails-lubricant" };
    public static readonly string[] CookingIds = { "warm-fuel", "stew-fuel" };
    static SettlementCraftPanel.Cost Cost(string id, int count) => new SettlementCraftPanel.Cost { MaterialId = id, Count = count };
    static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    static SettlementCraftPanel.Recipe Alternative(SettlementCraftPanel p, string id, string name, string output, int count, int minutes, string unlock, string description, params SettlementCraftPanel.Cost[] costs)
    {
        var product = p.Materials.FirstOrDefault(m => m.Id == output);
        var source = p.Recipes.FirstOrDefault(r => r.Id == output);
        Check(product != null || source != null, "Missing existing output: " + output);
        return new SettlementCraftPanel.Recipe { Id = id, Name = name, OutputId = output, OutputCount = count,
            Category = 0, Minutes = minutes, UnlockProject = unlock, Description = description,
            Icon = product?.Icon ?? source.Icon, Costs = costs };
    }
    public static void ConfigureCraft(SettlementCraftPanel p)
    {
        Check(CampaignPersistence.EverydayIds.All(id => p.Materials.Any(m => m.Id == id)), "Run BuildEverydayGoods before BuildEverydayRecipes.");
        var rows = p.Recipes.Where(r => !CraftIds.Contains(r.Id)).ToList();
        rows.Add(Alternative(p, "water-fuel", "연료 정수", "water", 2, 20, "build-bench",
            "연료로 물 끓이기\n물 2개 완성", Cost("raw-water", 2), Cost("fuel", 1)));
        rows.Add(Alternative(p, "bandage-alcohol", "소독 붕대", "bandage", 1, 15, "build-bench",
            "천을 소독해 제작\n붕대 1개 완성", Cost("cloth", 1), Cost("alcohol", 1)));
        rows.Add(Alternative(p, "flashlight-parts", "부품 손전등", "flashlight", 1, 20, "research-tools",
            "회수 부품 조립\n손전등 1개 완성", Cost("electrical-parts", 1), Cost("battery", 1)));
        rows.Add(Alternative(p, "nails-lubricant", "윤활 못", "nails", 3, 20, "research-tools",
            "윤활유로 고철 절약\n못 3개 완성", Cost("scrap", 2), Cost("lubricant", 1)));
        p.Recipes = rows.ToArray();
    }
    public static void ConfigureCooking(SettlementCookingPanel p, SettlementCraftPanel.Material[] materials)
    {
        var fuel = materials.Single(m => m.Id == "fuel");
        p.Ingredients = p.Ingredients.Where(i => i.Id != "fuel").Concat(new[] {
            new SettlementCookingPanel.Ingredient { Id = fuel.Id, Name = fuel.Name, Icon = fuel.Icon, PreviewCount = 0 }
        }).ToArray();
        var rows = p.Meals.Where(m => !CookingIds.Contains(m.Id)).ToList();
        foreach (string sourceId in new[] { "warm", "stew" }) {
            var source = p.Meals.Single(m => m.Id == sourceId);
            Check(source.Costs.Any(c => c.MaterialId == "wood"), "Existing meal no longer uses wood: " + sourceId);
            rows.Add(new SettlementCookingPanel.Meal { Id = sourceId + "-fuel", Name = sourceId == "warm" ? "연료 한 끼" : "연료 스튜",
                OutputId = source.OutputId, Servings = source.Servings, Minutes = source.Minutes, Category = source.Category,
                WorkerSpeedAllowed = source.WorkerSpeedAllowed, OutputUnit = source.OutputUnit, Icon = source.Icon,
                UnlockProject = "build-cooker", Description = "목재 대신 연료 1개\n식사 " + source.Servings + "개 완성", Use = source.Use,
                Costs = source.Costs.Where(c => c.MaterialId != "wood").Select(c => Cost(c.MaterialId, c.Count)).Concat(new[] { Cost("fuel", 1) }).ToArray() });
        }
        p.Meals = rows.ToArray();
    }
    static void Edit(string file, Action<GameObject> apply)
    {
        string path = P + file + ".prefab"; var root = PrefabUtility.LoadPrefabContents(path);
        try { apply(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    public static string Run()
    {
        Check(!EditorApplication.isPlaying, "Stop Play before changing recipe data.");
        Edit("CraftWorkPanel", g => ConfigureCraft(g.GetComponent<SettlementCraftPanel>()));
        var materials = AssetDatabase.LoadAssetAtPath<GameObject>(P + "CraftWorkPanel.prefab").GetComponent<SettlementCraftPanel>().Materials;
        Edit("CookingPanel", g => ConfigureCooking(g.GetComponent<SettlementCookingPanel>(), materials));
        Edit("SettlementScreen", g => {
            var owner = g.GetComponent<SettlementController>(); ConfigureCraft(owner.CraftPanel); ConfigureCooking(owner.CookingPanel, owner.CraftPanel.Materials);
        });
        AssetDatabase.SaveAssets();
        return "Applied four craft and two cooking alternatives; original recipes preserved; workshop/cooker/tools-research gates. No tobacco, coffee or weapon-parts use recipe.";
    }
}
