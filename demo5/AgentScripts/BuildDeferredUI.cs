using System;
using System.Collections.Generic;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Deferred UI items of balance pass 1 (기획/밸런스-1차.md §6). Edit mode only, on the base prefabs (never inside nested instances).
// 1. Loot (발견물) window: '모두 담기' (Workspace/TakeAll, a copy of Transfer with a white tint) and the return report's 3-button footer:
//    돌아가기 (80) | 선택 가져오기 (1178) | 모두 담기 (1532), each 320 wide, labels (8,0,304,76) centred. Mock 08 has no take-all: a deviation, not a reproduction.
// 2. Battle result: the used-item list shows exactly three 62px rows (3×62 + 2×12 = 210), so a resting list never shows a partial fourth row.
// Order: StandardizePopupFrames → FixPopupContentAlignment → PolishRemainingUI → BuildDeferredUI. Never call BuildLoot or BuildBattleResultSummary
// (both rebuild these prefabs from scratch). Idempotent: prints every value it changes (before → after); "Already applied." when nothing changes.
public static class BuildDeferredUI
{
    const string P = "Assets/Prefabs/Settlement/";
    static readonly List<string> log = new List<string>();
    static readonly Vector2 TopLeft = new Vector2(0, 1);
    static string V(RectTransform r) => r.anchoredPosition.x + "," + (-r.anchoredPosition.y) + "," + r.sizeDelta.x + "," + r.sizeDelta.y;
    // Top-left anchor and pivot in the 1920×1080 canvas, y measured downward: the same convention as StandardizePopupFrames.R.
    static void R(string what, Component c, float x, float y, float w, float h)
    {
        var r = (RectTransform)c.transform; var pos = new Vector2(x, -y); var size = new Vector2(w, h);
        if (r.anchorMin == TopLeft && r.anchorMax == TopLeft && r.pivot == TopLeft && r.anchoredPosition == pos && r.sizeDelta == size) return;
        log.Add(what + " " + V(r) + "→" + x + "," + y + "," + w + "," + h);
        r.anchorMin = r.anchorMax = r.pivot = TopLeft; r.anchoredPosition = pos; r.sizeDelta = size;
    }
    static void Height(string what, Transform t, float h)
    {
        if (!t) throw new Exception("Missing " + what);
        var r = (RectTransform)t; if (Mathf.Approximately(r.sizeDelta.y, h)) return;
        log.Add(what + " 높이 " + r.sizeDelta.y + "→" + h); r.sizeDelta = new Vector2(r.sizeDelta.x, h);
    }
    static void Set<T>(string what, T current, T value, Action<T> apply) { if (!EqualityComparer<T>.Default.Equals(current, value)) { log.Add(what + " " + current + "→" + value); apply(value); } }

    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play first");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new Exception("Preserve unsaved scene changes");
        log.Clear();
        Loot();
        BattleResult();
        AssetDatabase.SaveAssets();
        return log.Count == 0 ? "Already applied." : "Applied: " + string.Join("; ", log);
    }

    static void Loot()
    {
        string path = P + "ExpeditionLootPanel.prefab";
        var g = PrefabUtility.LoadPrefabContents(path);
        try
        {
            int n = log.Count; var c = g.GetComponent<ExpeditionLootPanel>(); var w = c.Workspace.transform;
            if (c.Transfer.transform.parent != w || c.Back.transform.parent != w) throw new Exception("Loot footer is not inside Workspace");
            var t = w.Find("TakeAll");
            if (!t)
            {
                var copy = Object.Instantiate(c.Transfer.gameObject, w); copy.name = "TakeAll"; t = copy.transform;
                log.Add("발견물 Workspace/TakeAll 생성 (선택 가져오기 복제)");
            }
            var take = t.GetComponent<Button>(); var paper = t.GetComponent<Image>();
            if (!take || !paper) throw new Exception("Workspace/TakeAll is not a button");
            // Transfer keeps its green primary tint; take-all is a white footer paper like the return report's buttons.
            Set("모두 담기 색", paper.color, Color.white, v => paper.color = v);
            Set("모두 담기 종이", paper.sprite, c.Transfer.GetComponent<Image>().sprite, v => paper.sprite = v);
            Set("모두 담기 연결", c.TakeAll, take, v => c.TakeAll = v);
            // Inside Workspace, right after Transfer: the LeaveReview's CanvasGroup blocking applies to it too.
            int ti = c.Transfer.transform.GetSiblingIndex(), ki = t.GetSiblingIndex();
            if (ki != ti + 1) { log.Add("모두 담기 순서 " + ki + "→" + (ti + 1)); t.SetSiblingIndex(ki < ti ? ti : ti + 1); }
            R("발견물 Back", c.Back, 80, 974, 320, 76); R("발견물 Transfer", c.Transfer, 1178, 974, 320, 76); R("발견물 TakeAll", take, 1532, 974, 320, 76);
            var buttons = new[] { c.Back, c.Transfer, take }; var sizes = new[] { 34, 32, 32 };
            for (int i = 0; i < buttons.Length; i++)
            {
                var label = buttons[i].GetComponentInChildren<Text>(true); string name = buttons[i].name; int size = sizes[i];
                if (!label) throw new Exception("Missing label on " + name);
                R("발견물 " + name + "/Label", label, 8, 0, 304, 76);
                Set("발견물 " + name + " 정렬", label.alignment, TextAnchor.MiddleCenter, v => label.alignment = v);
                Set("발견물 " + name + " 글자 크기", label.fontSize, size, v => label.fontSize = v);
            }
            var takeLabel = take.GetComponentInChildren<Text>(true);
            Set("모두 담기 문구", takeLabel.text, "모두 담기", v => takeLabel.text = v);
            // LeaveText, DenLeaveNote and TakeAll* were added after the prefab was last saved: write them so they show in the prefab file.
            if (!System.IO.File.ReadAllText(path).Contains("TakeAllNone:")) log.Add("발견물 문구 필드 저장 (LeaveText · DenLeaveNote · TakeAllDone/Left/None)");
            if (log.Count > n) { EditorUtility.SetDirty(c); PrefabUtility.SaveAsPrefabAsset(g, path); }
        }
        finally { PrefabUtility.UnloadPrefabContents(g); }
    }

    // Addressed like BuildBattleResultSummary.TuneLive (SummaryContent/Used, Used/Viewport, Used/ScrollTrack); only the heights change.
    static void BattleResult()
    {
        string path = P + "BattleResultPanel.prefab";
        var g = PrefabUtility.LoadPrefabContents(path);
        try
        {
            int n = log.Count; var root = g.transform.Find("SummaryContent");
            if (!root) throw new Exception("Missing BattleResultPanel/SummaryContent (run BuildBattleResultSummary first)");
            Height("전투 결과 SummaryContent/Used", root.Find("Used"), 210);
            Height("전투 결과 Used/Viewport", root.Find("Used/Viewport"), 210);
            Height("전투 결과 Used/ScrollTrack", root.Find("Used/ScrollTrack"), 210);
            if (log.Count > n) PrefabUtility.SaveAsPrefabAsset(g, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(g); }
    }
}
