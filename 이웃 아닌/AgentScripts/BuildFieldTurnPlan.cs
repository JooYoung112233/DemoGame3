using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Member action slots 1차 (기획/탐험-대원별행동배정-1차-구현.md): card variant with an action tag, worker-card captions,
// the '턴 진행' button, the restyled '모두 숨죽이기' button, the two-line plan chip and the FieldTurnPlanner component.
// Idempotent; run after BuildFieldStrategy. Layout lives in the prefabs (edit there); runtime code only writes text, colours and visibility.
public static class BuildFieldTurnPlan
{
    const string P = "Assets/Prefabs/Settlement/";
    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new Exception("Missing " + path);
    // Screen rect (x, y from the top-left of Main, 1920×1080) → top-left anchored rect, whatever the pivot.
    static void Place(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = new Vector2(0, 1); r.sizeDelta = new Vector2(w, h);
        r.anchoredPosition = new Vector2(x + r.pivot.x * w, -(y + (1 - r.pivot.y) * h));
    }
    // A child stretched across its parent's width, placed by its top edge.
    static void Band(RectTransform r, float top, float h)
    {
        r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(.5f, 1);
        r.anchoredPosition = new Vector2(0, -top); r.sizeDelta = new Vector2(-12, h);
    }
    static Text AddText(string name, Transform parent, Text style, int size, TextAnchor align, string text)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>(); t.font = style.font; t.fontStyle = style.fontStyle; t.fontSize = size; t.color = style.color; t.alignment = align;
        t.lineSpacing = style.lineSpacing; t.supportRichText = true; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate; t.text = text;
        return t;
    }

    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop first");
        var log = new List<string>();
        // 0. SettlementScreen nests the arrival panel: refuse if it overrides what this builder changes (coordinate first).
        var screen = Load<GameObject>(P + "SettlementScreen.prefab");
        var nested = screen.GetComponentsInChildren<ExpeditionArrivalPanel>(true).FirstOrDefault();
        if (nested)
        {
            var root = PrefabUtility.GetNearestPrefabInstanceRoot(nested.gameObject);
            var mods = root ? PrefabUtility.GetPropertyModifications(root) ?? new PropertyModification[0] : new PropertyModification[0];
            var blocked = mods.Where(m => m.target && (m.target is ExpeditionArrivalPanel && m.propertyPath == "MemberPrefab" || m.target is Component c && c.transform && (c.transform.name == "Hush" || c.transform.name == "Hint" || c.transform.name == "TurnAdvance") || m.target is GameObject g && (g.name == "Hush" || g.name == "Hint" || g.name == "TurnAdvance"))).Select(m => m.target.name + "." + m.propertyPath).ToArray();
            if (blocked.Length > 0) throw new Exception("SettlementScreen overrides: " + string.Join(", ", blocked));
        }

        // 1. Arrival-screen member card variant: action tag at the top-right (07), name shrinks to fit, bag line readable.
        var cardBase = Load<GameObject>(P + "ExpeditionMemberCard.prefab");
        var variant = AssetDatabase.LoadAssetAtPath<GameObject>(P + "FieldMemberCard.prefab");
        if (!variant)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(cardBase); inst.name = "FieldMemberCard";
            try
            {
                var card = inst.GetComponent<ExpeditionMemberCard>();
                var tag = AddText("ActionTag", inst.transform, card.Name, 17, TextAnchor.MiddleRight, "숨죽이기");
                var tr = tag.rectTransform; tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(1, 1); tr.anchoredPosition = new Vector2(-12, -14); tr.sizeDelta = new Vector2(74, 26);
                tag.horizontalOverflow = HorizontalWrapMode.Overflow; tag.verticalOverflow = VerticalWrapMode.Overflow; tag.gameObject.SetActive(false);
                card.Action = tag;
                var nr = card.Name.rectTransform; float left = nr.anchoredPosition.x - nr.pivot.x * nr.sizeDelta.x;
                nr.sizeDelta = new Vector2(76, nr.sizeDelta.y); nr.anchoredPosition = new Vector2(left + nr.pivot.x * 76, nr.anchoredPosition.y);
                card.Name.resizeTextForBestFit = true; card.Name.resizeTextMinSize = 20; card.Name.resizeTextMaxSize = card.Name.fontSize;
                if (card.Role) { card.Role.resizeTextForBestFit = true; card.Role.resizeTextMinSize = 16; card.Role.resizeTextMaxSize = card.Role.fontSize; }
                if (card.State) card.State.fontSize = 21; // 22 × 1.48 line height did not fit the 32 px strip, so '가방 n / cap' never showed
                variant = PrefabUtility.SaveAsPrefabAsset(inst, P + "FieldMemberCard.prefab"); log.Add("card variant");
            }
            finally { Object.DestroyImmediate(inst); }
        }

        // 2. Search worker cards: a small caption on the portrait corner (수색 담당 / 동행 / 망보기 / 조명 지원 / 다른 사물).
        var workerRoot = PrefabUtility.LoadPrefabContents(P + "SearchWorkerCard.prefab");
        try
        {
            var card = workerRoot.GetComponent<ExpeditionMemberCard>();
            if (card && !card.ActionRoot)
            {
                var go = new GameObject("ActionTag", typeof(RectTransform)); go.transform.SetParent(workerRoot.transform, false);
                var r = (RectTransform)go.transform; r.pivot = new Vector2(0, 1); Place(r, 62, 4, 66, 24);
                var paper = go.AddComponent<Image>(); paper.sprite = card.Paper ? card.Paper.sprite : null; paper.type = card.Paper ? card.Paper.type : Image.Type.Simple; paper.color = new Color(.97f, .94f, .85f); paper.raycastTarget = false;
                var style = card.Name;
                var t = AddText("Text", go.transform, style, 15, TextAnchor.MiddleCenter, "수색 담당");
                t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
                t.resizeTextForBestFit = true; t.resizeTextMinSize = 12; t.resizeTextMaxSize = 15; t.color = new Color(.16f, .15f, .13f);
                go.SetActive(false); card.Action = t; card.ActionRoot = go; EditorUtility.SetDirty(card);
                PrefabUtility.SaveAsPrefabAsset(workerRoot, P + "SearchWorkerCard.prefab"); log.Add("worker caption");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(workerRoot); }

        // 3. Arrival panel: card variant, '모두 숨죽이기' + '턴 진행' on the bottom row, chip in the hint slot, planner component.
        var arrivalRoot = PrefabUtility.LoadPrefabContents(P + "ExpeditionArrivalPanel.prefab");
        try
        {
            var arrival = arrivalRoot.GetComponent<ExpeditionArrivalPanel>(); var threat = arrivalRoot.GetComponent<ExpeditionSiteThreat>();
            if (!threat || !threat.Hush) throw new Exception("Run BuildFieldStrategy first");
            var main = arrival.Main.transform;
            var variantCard = variant.GetComponent<ExpeditionMemberCard>();
            if (arrival.MemberPrefab != variantCard) { arrival.MemberPrefab = variantCard; EditorUtility.SetDirty(arrival); log.Add("arrival cards → FieldMemberCard"); }

            var hush = threat.Hush; var hushRect = (RectTransform)hush.transform;
            if (!hush.transform.Find("Subtitle"))
            {
                Place(hushRect, 1090, 952, 320, 78);
                var label = hush.transform.Find("Label")?.GetComponent<Text>() ?? hush.GetComponentsInChildren<Text>(true).First();
                label.name = "Title"; Band(label.rectTransform, 3, 42); label.fontSize = 28; label.alignment = TextAnchor.MiddleCenter; label.text = "모두 숨죽이기";
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                var sub = AddText("Subtitle", hush.transform, label, 17, TextAnchor.MiddleCenter, "이번 턴만 · 배정 유지"); Band(sub.rectTransform, 46, 26);
                EditorUtility.SetDirty(hush); log.Add("hush restyled");
            }
            var planner = arrivalRoot.GetComponent<FieldTurnPlanner>();
            if (!planner) { planner = arrivalRoot.AddComponent<FieldTurnPlanner>(); log.Add("planner component"); }
            if (threat.Planner != planner) { threat.Planner = planner; EditorUtility.SetDirty(threat); }

            var turn = main.Find("TurnAdvance");
            if (!turn)
            {
                var go = Object.Instantiate(hush.gameObject, main); go.name = "TurnAdvance"; turn = go.transform;
                Place((RectTransform)turn, 1430, 952, 410, 78);
                var button = go.GetComponent<Button>(); button.onClick = new Button.ButtonClickedEvent();
                var paper = go.GetComponent<Image>(); if (paper) paper.color = new Color(.76f, .86f, .68f);
                var title = turn.Find("Title").GetComponent<Text>(); Band(title.rectTransform, 3, 45); title.fontSize = 30; title.text = "턴 진행";
                var sub = turn.Find("Subtitle").GetComponent<Text>(); Band(sub.rectTransform, 48, 27); sub.fontSize = 18; sub.text = "배정 확인 후 시간 진행";
                turn.SetAsLastSibling(); go.SetActive(false); log.Add("turn button");
            }
            var hint = main.Find("Hint").GetComponent<Text>();
            var chip = hint.transform.Find("PlanChip")?.GetComponent<Text>();
            if (!chip)
            {
                chip = AddText("PlanChip", hint.transform, hint, 22, TextAnchor.MiddleLeft, "소음 +0 → □□□□\n모두 숨죽이기");
                var r = chip.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(0, 7); r.sizeDelta = new Vector2(470, 78);
                chip.resizeTextForBestFit = true; chip.resizeTextMinSize = 17; chip.resizeTextMaxSize = 22; chip.gameObject.SetActive(false); log.Add("plan chip");
            }
            if (hint.raycastTarget) { hint.raycastTarget = false; EditorUtility.SetDirty(hint); }
            planner.TurnButton = turn.GetComponent<Button>(); planner.TurnTitle = turn.Find("Title").GetComponent<Text>(); planner.TurnSubtitle = turn.Find("Subtitle").GetComponent<Text>();
            planner.Hint = hint; planner.Chip = chip; EditorUtility.SetDirty(planner);
            PrefabUtility.SaveAsPrefabAsset(arrivalRoot, P + "ExpeditionArrivalPanel.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(arrivalRoot); AssetDatabase.SaveAssets(); }
        return log.Count == 0 ? "Already built." : "Built: " + string.Join("; ", log);
    }
}
