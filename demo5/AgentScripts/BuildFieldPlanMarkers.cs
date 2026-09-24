using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Exploration target markers (2026-09-25, user: '맡긴 사물 위에 담당 대원'): under the arrival panel's Main a 'PlanTargetMarkers'
// node with FieldPlanTargetMarkers and a pool of AssignmentBubble instances (the shared kit from BuildAssignmentBubble).
// Base ExpeditionArrivalPanel.prefab only (SettlementScreen's nested instance inherits it). The node is created after every
// hotspot and just before 'TurnFlow' (not the last child), so the stop stamp and the '계속 진행' toggle draw over the bubbles;
// the turn banner's place is kept free instead (KeepClearAlways), so no ring fills under it.
// Nothing is added under hotspot buttons (BuildNpcStory clones Objects[0]). Idempotent: nodes are found or created by name;
// no existing node is renamed, reordered or re-rected; Inspector values (placements, area, texts) are kept.
// Run after BuildAssignmentBubble.Run and BuildFieldTurnFlow.Run (the toggle and the banner are wired here), and after
// BuildExplorationRoomPresentation, BuildExplorationHotspots, ReviewExplorationPolish.ApplyHint, BuildNpcStory.
// Temporary layout, no approved mock.
public static class BuildFieldPlanMarkers
{
    const string ArrivalPath = "Assets/Prefabs/Settlement/ExpeditionArrivalPanel.prefab", BubblePath = "Assets/Prefabs/Settlement/AssignmentBubble.prefab";
    public const string NodeName = "PlanTargetMarkers";
    public const int PoolSize = 6;
    // Screen papers, the member row and the bottom buttons the bubbles must never cover (Main-relative paths).
    public static readonly string[] KeepClearPaths = { "DayPaper", "PlacePaper", "TurnPaper", "RoutePaper", "ResourcePaper", "RiskPaper", "ArrivalPaper", "Members", "Hint", "Return", "Hush", "TurnAdvance", "TurnFlow/AutoAdvance" };
    // Kept free even while hidden: the turn banner shows during every turn sequence, when the rings fill.
    public static readonly string[] KeepClearAlwaysPaths = { "TurnFlow/TurnBanner" };
    static readonly List<string> log = new List<string>();

    public static string Run()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Stop Play and preserve the scene first");
        log.Clear();
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(BubblePath);
        if (!asset || !asset.GetComponent<AssignmentBubble>()) throw new Exception("Run BuildAssignmentBubble.Run first (" + BubblePath + ")");
        var root = PrefabUtility.LoadPrefabContents(ArrivalPath);
        try
        {
            var arrival = root.GetComponent<ExpeditionArrivalPanel>(); if (!arrival || !arrival.Main) throw new Exception("ExpeditionArrivalPanel/Main missing");
            var main = arrival.Main.transform;
            foreach (var path in new[] { "TurnFlow/AutoAdvance" }.Concat(KeepClearAlwaysPaths)) if (!main.Find(path)) throw new Exception("Run BuildFieldTurnFlow.Run first (Main/" + path + ")");
            var node = main.Find(NodeName);
            if (!node)
            {
                var g = new GameObject(NodeName, typeof(RectTransform)); g.layer = main.gameObject.layer; g.transform.SetParent(main, false); node = g.transform;
                var r = (RectTransform)node; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, .5f); r.offsetMin = r.offsetMax = Vector2.zero;
                // Over every hotspot (they are all earlier siblings); under the turn banner and the stop stamp.
                var flow = main.Find("TurnFlow"); if (flow) node.SetSiblingIndex(flow.GetSiblingIndex());
                log.Add("Main/" + NodeName + " created" + (flow ? " (before TurnFlow)" : " (last)"));
            }
            var group = node.GetComponent<CanvasGroup>(); if (!group) { group = node.gameObject.AddComponent<CanvasGroup>(); log.Add("CanvasGroup"); }
            if (group.interactable || group.blocksRaycasts) { group.interactable = group.blocksRaycasts = false; log.Add("markers pass clicks"); }
            var mk = node.GetComponent<FieldPlanTargetMarkers>(); if (!mk) { mk = node.gameObject.AddComponent<FieldPlanTargetMarkers>(); log.Add("+FieldPlanTargetMarkers"); }
            if (mk.Arrival != arrival) { mk.Arrival = arrival; log.Add("Arrival"); }

            var bubbles = new List<AssignmentBubble>();
            for (int i = 0; i < PoolSize; i++)
            {
                var t = node.Find("Bubble_" + i);
                if (!t)
                {
                    var g = (GameObject)PrefabUtility.InstantiatePrefab(asset, node); g.name = "Bubble_" + i; g.SetActive(false); t = g.transform;
                    ((RectTransform)t).localPosition = Vector3.zero; log.Add("Bubble_" + i + " created");
                }
                var b = t.GetComponent<AssignmentBubble>(); if (!b) throw new Exception(t.name + " is not an AssignmentBubble");
                bubbles.Add(b);
            }
            // Keep any extra bubble someone wired in the Inspector.
            if (mk.Bubbles != null) foreach (var b in mk.Bubbles) if (b && !bubbles.Contains(b)) bubbles.Add(b);
            if (mk.Bubbles == null || !mk.Bubbles.SequenceEqual(bubbles)) { mk.Bubbles = bubbles.ToArray(); log.Add(bubbles.Count + " bubbles wired"); }

            var clear = (mk.KeepClear ?? new RectTransform[0]).Where(r => r).ToList(); int before = clear.Count;
            foreach (var path in KeepClearPaths) { var r = main.Find(path) as RectTransform; if (r && !clear.Contains(r)) clear.Add(r); }
            if (clear.Count != before || mk.KeepClear == null || mk.KeepClear.Length != clear.Count) { mk.KeepClear = clear.ToArray(); log.Add("keep clear: " + string.Join(", ", clear.Select(r => r.name))); }
            var always = (mk.KeepClearAlways ?? new RectTransform[0]).Where(r => r).ToList(); before = always.Count;
            foreach (var path in KeepClearAlwaysPaths) { var r = main.Find(path) as RectTransform; if (r && !always.Contains(r)) always.Add(r); }
            if (always.Count != before || mk.KeepClearAlways == null || mk.KeepClearAlways.Length != always.Count) { mk.KeepClearAlways = always.ToArray(); log.Add("kept free while hidden: " + string.Join(", ", always.Select(r => r.name))); }

            if (log.Count > 0) { EditorUtility.SetDirty(mk); PrefabUtility.SaveAsPrefabAsset(root, ArrivalPath); }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return log.Count == 0 ? "Already applied." : "Applied: " + string.Join("; ", log);
    }
}
