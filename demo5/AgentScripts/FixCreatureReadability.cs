using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Targeted prefab edits from the creature-combat review (kept in step with BuildBattleFeel / BuildEncounter sources):
// head tags wide enough for the longest creature tag, the threat link ends in a dot instead of an arrowhead,
// and the encounter's wait button no longer names 감염자.
public static class FixCreatureReadability
{
    const string P = "Assets/Prefabs/Settlement/";
    static void Size(Transform root, string path, float x, float width, List<string> log)
    {
        var t = root.Find(path) as RectTransform ?? throw new Exception("Missing " + path);
        if (Mathf.Approximately(t.sizeDelta.x, width) && Mathf.Approximately(t.anchoredPosition.x, x)) return;
        t.sizeDelta = new Vector2(width, t.sizeDelta.y); t.anchoredPosition = new Vector2(x, t.anchoredPosition.y); log.Add(path + " " + width);
    }
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop first");
        var log = new List<string>();
        var hud = PrefabUtility.LoadPrefabContents(P + "BattlePawnHud.prefab");
        try
        {
            int before = log.Count;
            Size(hud.transform, "Intent", 0, 152, log); Size(hud.transform, "Intent/Paper", 0, 152, log); Size(hud.transform, "Intent/Label", 16, 112, log);
            Size(hud.transform, "Danger", 0, 124, log); Size(hud.transform, "Danger/Paper", 0, 124, log); Size(hud.transform, "Danger/Label", 0, 120, log);
            if (log.Count > before) PrefabUtility.SaveAsPrefabAsset(hud, P + "BattlePawnHud.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(hud); }
        var line = PrefabUtility.LoadPrefabContents(P + "BattleThreatLine.prefab");
        try
        {
            var g = line.GetComponent<BattleThreatLine>();
            if (g.Head != 0) { g.Head = 0; g.Dot = 6; log.Add("threat link: dot, no arrowhead"); PrefabUtility.SaveAsPrefabAsset(line, P + "BattleThreatLine.prefab"); }
        }
        finally { PrefabUtility.UnloadPrefabContents(line); }
        var encounter = PrefabUtility.LoadPrefabContents(P + "ExpeditionEncounterPanel.prefab");
        try
        {
            var text = encounter.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.text.Contains("감염자가 지나가길"));
            if (text) { text.text = text.text.Replace("감염자가 지나가길", "무언가 지나가길"); log.Add("encounter wait wording"); PrefabUtility.SaveAsPrefabAsset(encounter, P + "ExpeditionEncounterPanel.prefab"); }
        }
        finally { PrefabUtility.UnloadPrefabContents(encounter); AssetDatabase.SaveAssets(); }
        return log.Count == 0 ? "Already applied." : "Applied: " + string.Join("; ", log);
    }
}
