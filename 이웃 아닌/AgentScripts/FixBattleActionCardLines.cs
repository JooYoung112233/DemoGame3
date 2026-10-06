using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Two user decisions in the battle UI (2026-09-25). Idempotent: logs each change as before -> after, 'Already applied.' when nothing changes.
// (1) '예고피해는 표기해줘야 할듯': the shared action card (BattleActionCard.prefab) gets a two-line description slot
//     (Label (82,0) 142×41 27px, Description (82,-37) 142×53 17px) and the guard card reads GuardDescription "물릴 확률 -{0}%p\n예고 피해 -{1}".
//     GuardDescription is migrated only from the known old strings; any other value is an Inspector edit and is kept.
// (2) '튜토에서 알려주는걸로하자': the resident lesson's marks (ExpeditionBattlePanel.Lesson.cs) are made and wired here:
//     Workspace/LessonMarks (DangerFrame, MoveFrame, Pointer) right above PawnHudLayer, Workspace/LessonGuardFrame right after the last action card.
// This builder owns these parts of both prefabs; BuildFieldBattle.cs carries the same card layout for a full rebuild (never re-run it over later edits).
// Run after BuildBattleFeel / BuildBattlePhase / BuildCreatureBattle: BuildBattleFeel.Build rebuilds PawnHudLayer, so nothing here lives under it.
public static class FixBattleActionCardLines
{
    const string CardPath = "Assets/Prefabs/Settlement/BattleActionCard.prefab", PanelPath = "Assets/Prefabs/Settlement/ExpeditionBattlePanel.prefab";
    const string GuardText = "물릴 확률 -{0}%p\n예고 피해 -{1}", GuardSample = "물릴 확률 -40%p\n예고 피해 -1";
    static readonly string[] OldGuardTexts = { "물릴 확률 -{0}%p", "물림 -{0}%p · 예고 -{1}" };
    static readonly Vector2 LabelPosition = new Vector2(82, 0), LabelSize = new Vector2(142, 41), DescriptionPosition = new Vector2(82, -37), DescriptionSize = new Vector2(142, 53);
    const int LabelFont = 27, DescriptionFont = 17;
    static readonly List<string> log = new List<string>(), notes = new List<string>();

    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play first");
        if (EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Save or discard the active scene first (" + EditorSceneManager.GetActiveScene().name + ")");
        log.Clear(); notes.Clear();

        // Before touching the shared card: the four panel cards must not override the text rects or fonts, or the new slot would not reach them.
        var check = PrefabUtility.LoadPrefabContents(PanelPath);
        try { foreach (var card in Cards(check.GetComponent<ExpeditionBattlePanel>())) NoLayoutOverrides(card); }
        finally { PrefabUtility.UnloadPrefabContents(check); }

        // (a) BattleActionCard.prefab: the two-line slot.
        var cardRoot = PrefabUtility.LoadPrefabContents(CardPath);
        try
        {
            var label = TextAt(cardRoot.transform, "Label"); var description = TextAt(cardRoot.transform, "Description");
            TopLeftAnchored(label); TopLeftAnchored(description);
            int before = log.Count;
            Place(label, LabelPosition, LabelSize);
            if (label.fontSize != LabelFont) notes.Add("Label font " + label.fontSize + " kept (plan: " + LabelFont + ")");
            Place(description, DescriptionPosition, DescriptionSize);
            if (description.fontSize != DescriptionFont) { log.Add("Description font " + description.fontSize + " -> " + DescriptionFont); description.fontSize = DescriptionFont; }
            if (description.alignment != TextAnchor.MiddleLeft) { log.Add("Description alignment " + description.alignment + " -> MiddleLeft"); description.alignment = TextAnchor.MiddleLeft; }
            if (description.horizontalOverflow != HorizontalWrapMode.Wrap) { log.Add("Description horizontal " + description.horizontalOverflow + " -> Wrap"); description.horizontalOverflow = HorizontalWrapMode.Wrap; }
            if (description.verticalOverflow != VerticalWrapMode.Truncate) { log.Add("Description vertical " + description.verticalOverflow + " -> Truncate"); description.verticalOverflow = VerticalWrapMode.Truncate; }
            if (description.lineSpacing != 1) { log.Add("Description line spacing " + description.lineSpacing + " -> 1"); description.lineSpacing = 1; }
            if (description.resizeTextForBestFit) { log.Add("Description best fit on -> off"); description.resizeTextForBestFit = false; }
            // Self-check without a canvas (scale 1): the title keeps 1 px and two description lines keep 2 px of real headroom.
            Fits(label, 1, label.text);
            var keep = description.text; description.text = GuardSample; Fits(description, 2, GuardSample); description.text = keep;
            if (log.Count > before) PrefabUtility.SaveAsPrefabAsset(cardRoot, CardPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(cardRoot); }

        // (b-e) ExpeditionBattlePanel.prefab: guard wording, the guard card's text, card self-checks, the lesson marks.
        var root = PrefabUtility.LoadPrefabContents(PanelPath);
        try
        {
            var panel = root.GetComponent<ExpeditionBattlePanel>(); if (!panel) throw new Exception("ExpeditionBattlePanel missing");
            var cards = Cards(panel); int before = log.Count;
            if (panel.GuardDescription != GuardText)
            {
                if (OldGuardTexts.Contains(panel.GuardDescription)) { log.Add("GuardDescription " + Quote(panel.GuardDescription) + " -> " + Quote(GuardText)); panel.GuardDescription = GuardText; }
                else notes.Add("GuardDescription kept (an Inspector edit, not a known old string): " + Quote(panel.GuardDescription));
            }
            // The guard card's placeholder text (Initialize writes the same through Describe at runtime).
            var guardLine = string.Format(panel.GuardDescription, panel.Rules.GuardHitPenalty, panel.Rules.GuardStrikeReduction);
            var guardText = TextAt(panel.Guard.transform, "Description");
            if (guardText.text != guardLine) { log.Add("Guard card text " + Quote(guardText.text) + " -> " + Quote(guardLine)); guardText.text = guardLine; PrefabUtility.RecordPrefabInstancePropertyModifications(guardText); }
            foreach (var card in cards)
            {
                var d = TextAt(card.transform, "Description"); var l = TextAt(card.transform, "Label");
                if (d.rectTransform.rect.height != DescriptionSize.y || l.rectTransform.rect.height != LabelSize.y) throw new Exception(card.name + " did not take the shared card slot: label h " + l.rectTransform.rect.height + ", description h " + d.rectTransform.rect.height);
                if (-d.rectTransform.anchoredPosition.y + d.rectTransform.rect.height > ((RectTransform)card.transform).rect.height) throw new Exception(card.name + " description leaves the card");
                Fits(l, 1, card.name + " title"); Fits(d, 2, card.name + " description");
            }
            Lesson(panel);
            if (log.Count > before) { EditorUtility.SetDirty(panel); PrefabUtility.SaveAsPrefabAsset(root, PanelPath); }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        string extra = notes.Count > 0 ? " Notes: " + string.Join("; ", notes) : "";
        return log.Count == 0 ? "Already applied." + extra : "Applied: " + string.Join("; ", log) + extra;
    }

    static Button[] Cards(ExpeditionBattlePanel panel)
    {
        if (!panel) throw new Exception("ExpeditionBattlePanel missing");
        var workspace = panel.Workspace ? panel.Workspace.transform : null; if (!workspace) throw new Exception("Workspace missing");
        var cards = new[] { panel.Melee, panel.Shoot, panel.Guard, panel.Items };
        var names = new[] { "Melee", "Shoot", "Guard", "Items" };
        for (int i = 0; i < 4; i++)
            if (!cards[i] || cards[i].name != names[i] || cards[i].transform.parent != workspace) throw new Exception("Action card " + names[i] + " not found under Workspace");
        return cards;
    }
    static void NoLayoutOverrides(Button card)
    {
        var go = card.gameObject;
        if (!PrefabUtility.IsAnyPrefabInstanceRoot(go) || AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(go)) != CardPath) throw new Exception(card.name + " is not a BattleActionCard instance");
        foreach (var m in PrefabUtility.GetPropertyModifications(go) ?? new PropertyModification[0])
        {
            var target = m.target as Component;
            if (!target || (target.name != "Label" && target.name != "Description") || !(target is RectTransform || target is Text)) continue;
            if (m.propertyPath.StartsWith("m_AnchoredPosition") || m.propertyPath.StartsWith("m_SizeDelta") || m.propertyPath.StartsWith("m_FontData"))
                throw new Exception(card.name + " overrides " + target.name + "." + m.propertyPath + " = " + m.value + " · revert that override so the shared two-line slot reaches it");
        }
    }
    static Text TextAt(Transform parent, string name)
    {
        var t = parent.Find(name); var text = t ? t.GetComponent<Text>() : null;
        if (!text) throw new Exception(parent.name + "/" + name + " has no Text"); return text;
    }
    static void TopLeftAnchored(Text t)
    {
        var r = t.rectTransform; var corner = new Vector2(0, 1);
        if (r.anchorMin != corner || r.anchorMax != corner || r.pivot != corner) throw new Exception(t.name + " is not anchored top-left; the card slot values assume it");
    }
    static void Place(Text t, Vector2 position, Vector2 size)
    {
        var r = t.rectTransform; if (r.anchoredPosition == position && r.sizeDelta == size) return;
        log.Add(t.name + " (" + r.anchoredPosition.x + "," + r.anchoredPosition.y + ") " + r.sizeDelta.x + "×" + r.sizeDelta.y + " " + t.fontSize + "px -> (" + position.x + "," + position.y + ") " + size.x + "×" + size.y);
        r.anchoredPosition = position; r.sizeDelta = size;
    }
    // Legacy Text with Truncate drops a whole line that does not fit, so each slot keeps real headroom.
    static void Fits(Text t, float headroom, string what)
    {
        float need = t.preferredHeight, room = t.rectTransform.rect.height - headroom;
        if (need > room) throw new Exception("Self-check failed: " + what + " needs " + need.ToString("0.00") + " px, " + t.name + " allows " + room.ToString("0.00") + " (" + t.rectTransform.rect.height + " - " + headroom + ")");
    }
    static string Quote(string s) => "'" + (s ?? "").Replace("\n", "\\n") + "'";

    // ---- (2) the resident lesson's marks ----
    static void Lesson(ExpeditionBattlePanel panel)
    {
        var workspace = panel.Workspace.transform;
        var hud = panel.HudLayer ? panel.HudLayer : workspace.Find("PawnHudLayer") as RectTransform;
        if (!hud || hud.parent != workspace) throw new Exception("PawnHudLayer missing under Workspace (run BuildBattleFeel first)");
        var marks = workspace.Find("LessonMarks") as RectTransform;
        if (!marks) { marks = new GameObject("LessonMarks", typeof(RectTransform)).GetComponent<RectTransform>(); marks.SetParent(workspace, false); log.Add("LessonMarks 생성"); }
        Anchor(marks, new Vector2(0, 1), new Vector2(0, 1));
        if (marks.anchoredPosition != Vector2.zero || marks.sizeDelta != new Vector2(1920, 1080)) { log.Add("LessonMarks " + marks.anchoredPosition + " " + marks.sizeDelta + " -> (0,0) 1920×1080"); marks.anchoredPosition = Vector2.zero; marks.sizeDelta = new Vector2(1920, 1080); }
        // Above the head tags, below the aim layer and the feedback strip.
        if (marks.GetSiblingIndex() != hud.GetSiblingIndex() + 1) { log.Add("LessonMarks 순서 " + marks.GetSiblingIndex() + " -> PawnHudLayer 바로 위"); marks.SetAsLastSibling(); marks.SetSiblingIndex(hud.GetSiblingIndex() + 1); }
        var danger = Frame(marks, "DangerFrame", panel.LessonDangerColor);
        var move = Frame(marks, "MoveFrame", panel.LessonTargetColor);
        var pointerT = marks.Find("Pointer");
        if (!pointerT) { pointerT = new GameObject("Pointer", typeof(RectTransform), typeof(CanvasRenderer), typeof(TutorialPointer)).transform; pointerT.SetParent(marks, false); ((RectTransform)pointerT).sizeDelta = panel.LessonPointerSize; log.Add("LessonMarks/Pointer 생성"); }
        var pointer = pointerT.GetComponent<TutorialPointer>(); if (!pointer) throw new Exception("LessonMarks/Pointer has no TutorialPointer");
        Anchor((RectTransform)pointerT, new Vector2(0, 1), new Vector2(.5f, .5f));
        if (pointerT.GetSiblingIndex() != marks.childCount - 1) { log.Add("Pointer 맨 앞"); pointerT.SetAsLastSibling(); }
        if (pointer.color != panel.LessonTargetColor) { log.Add("Pointer 색 " + pointer.color + " -> " + panel.LessonTargetColor); pointer.color = panel.LessonTargetColor; }
        Off(pointerT.gameObject);
        // The guard card frame renders above BottomBand, so it sits right after the last action card.
        var guard = Frame(workspace, "LessonGuardFrame", panel.LessonTargetColor);
        var last = Cards(panel).Select(c => c.transform).OrderBy(t => t.GetSiblingIndex()).Last();
        if (guard.transform.GetSiblingIndex() != last.GetSiblingIndex() + 1) { log.Add("LessonGuardFrame 순서 " + guard.transform.GetSiblingIndex() + " -> " + last.name + " 바로 뒤"); guard.transform.SetAsLastSibling(); guard.transform.SetSiblingIndex(last.GetSiblingIndex() + 1); }
        if (panel.LessonMarks != marks) { panel.LessonMarks = marks; log.Add("LessonMarks 연결"); }
        if (panel.LessonDangerFrame != danger) { panel.LessonDangerFrame = danger; log.Add("LessonDangerFrame 연결"); }
        if (panel.LessonMoveFrame != move) { panel.LessonMoveFrame = move; log.Add("LessonMoveFrame 연결"); }
        if (panel.LessonGuardFrame != guard) { panel.LessonGuardFrame = guard; log.Add("LessonGuardFrame 연결"); }
        if (panel.LessonPointer != pointer) { panel.LessonPointer = pointer; log.Add("LessonPointer 연결"); }
    }
    static TutorialTargetGraphic Frame(Transform parent, string name, Color color)
    {
        var t = parent.Find(name); string path = parent.name + "/" + name;
        if (!t) { t = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TutorialTargetGraphic)).transform; t.SetParent(parent, false); ((RectTransform)t).sizeDelta = new Vector2(160, 110); log.Add(path + " 생성"); }
        var g = t.GetComponent<TutorialTargetGraphic>(); if (!g) throw new Exception(path + " has no TutorialTargetGraphic");
        Anchor((RectTransform)t, new Vector2(0, 1), new Vector2(.5f, .5f));
        if (g.color != color) { log.Add(path + " 색 " + g.color + " -> " + color); g.color = color; }
        Off(t.gameObject);
        return g;
    }
    // Runtime places the marks by position and size; anchors stay at one point so sizeDelta is the size.
    static void Anchor(RectTransform r, Vector2 anchor, Vector2 pivot)
    {
        if (r.anchorMin == anchor && r.anchorMax == anchor && r.pivot == pivot) return;
        log.Add(r.name + " anchor " + r.anchorMin + "/" + r.anchorMax + " pivot " + r.pivot + " -> " + anchor + " pivot " + pivot);
        r.anchorMin = r.anchorMax = anchor; r.pivot = pivot;
    }
    static void Off(GameObject g) { if (g.activeSelf) { g.SetActive(false); log.Add(g.name + " 기본 꺼짐"); } }
}
