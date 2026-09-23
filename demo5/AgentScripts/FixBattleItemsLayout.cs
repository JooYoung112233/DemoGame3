using System;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Targeted fix from the item-drawer review (kept in step with BuildBattleItems, which must not be re-run over later prefab edits):
// order cards may shrink to 48 px and the row clips, and the feedback strip hides while the drawer is open.
public static class FixBattleItemsLayout
{
    const string P = "Assets/Prefabs/Settlement/";
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop first");
        var log = "";
        var card = PrefabUtility.LoadPrefabContents(P + "BattleOrderCard.prefab");
        try
        {
            var element = card.GetComponent<LayoutElement>(); if (!element) throw new Exception("BattleOrderCard has no LayoutElement");
            if (element.minWidth != 48) { log += "order card minWidth " + element.minWidth + " -> 48; "; element.minWidth = 48; PrefabUtility.SaveAsPrefabAsset(card, P + "BattleOrderCard.prefab"); }
        }
        finally { PrefabUtility.UnloadPrefabContents(card); }
        var root = PrefabUtility.LoadPrefabContents(P + "ExpeditionBattlePanel.prefab");
        try
        {
            var panel = root.GetComponent<ExpeditionBattlePanel>(); bool dirty = false;
            if (!panel.OrderContent) throw new Exception("OrderContent missing");
            if (!panel.OrderContent.GetComponent<RectMask2D>()) { panel.OrderContent.gameObject.AddComponent<RectMask2D>(); log += "RectMask2D on order row; "; dirty = true; }
            var hidden = (panel.HideDuringItems ?? new GameObject[0]).ToList();
            foreach (var name in new[] { "FeedbackPaper", "Feedback" })
            {
                var target = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name)?.gameObject;
                if (!target) throw new Exception("Missing " + name);
                if (!hidden.Contains(target)) { hidden.Add(target); log += "hide " + name + " in item mode; "; dirty = true; }
            }
            panel.HideDuringItems = hidden.ToArray();
            if (dirty) PrefabUtility.SaveAsPrefabAsset(root, P + "ExpeditionBattlePanel.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); AssetDatabase.SaveAssets(); }
        return log.Length == 0 ? "Already applied." : "Applied: " + log;
    }
}
