using System;
using System.Collections.Generic;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Tutorial focus (2026-09-25 user request: the next click was hard to recognise). Adds to SettlementScreen/TutorialGuidance:
// Spotlight (dark veil with holes, first child so the instruction paper stays above it) and Pointer (arrow, after the frame), and wires the guide.
// Idempotent: prints what it changes, 'Already applied.' when nothing does. Run after the other SettlementScreen builders.
public static class BuildTutorialFocus
{
    const string Path = "Assets/Prefabs/Settlement/SettlementScreen.prefab";
    static readonly List<string> log = new List<string>();

    public static string Run()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Stop Play and preserve the scene first");
        log.Clear();
        var root = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var guide = root.GetComponent<SettlementTutorialGuide>() ?? throw new Exception("Guide missing");
            var parent = guide.Marker.transform.parent;
            var spot = parent.Find("Spotlight");
            if (!spot) { spot = new GameObject("Spotlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(TutorialSpotlight)).transform; spot.SetParent(parent, false); log.Add("Spotlight 생성"); }
            var sr = (RectTransform)spot; Rect(sr, Vector2.zero, Vector2.one, new Vector2(-1200, -1200), new Vector2(1200, 1200));
            if (spot.GetSiblingIndex() != 0) { spot.SetAsFirstSibling(); log.Add("Spotlight 맨 뒤"); }
            var s = spot.GetComponent<TutorialSpotlight>(); if (s.color != new Color(0, 0, 0, .72f)) { s.color = new Color(0, 0, 0, .72f); log.Add("Spotlight 색"); }
            if (spot.gameObject.activeSelf) { spot.gameObject.SetActive(false); log.Add("Spotlight 기본 꺼짐"); }

            var point = parent.Find("Pointer");
            if (!point) { point = new GameObject("Pointer", typeof(RectTransform), typeof(CanvasRenderer), typeof(TutorialPointer)).transform; point.SetParent(parent, false); log.Add("Pointer 생성"); }
            var pr = (RectTransform)point; Rect(pr, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero); if (pr.sizeDelta != new Vector2(58, 70)) { pr.sizeDelta = new Vector2(58, 70); log.Add("Pointer 크기 58×70"); }
            if (point.GetSiblingIndex() != parent.childCount - 1) { point.SetAsLastSibling(); log.Add("Pointer 맨 앞"); }
            var p = point.GetComponent<TutorialPointer>(); var gold = new Color(1, .79f, .36f, 1); if (p.color != gold) { p.color = gold; log.Add("Pointer 색"); }
            if (point.gameObject.activeSelf) { point.gameObject.SetActive(false); log.Add("Pointer 기본 꺼짐"); }

            if (guide.Spotlight != s) { guide.Spotlight = s; log.Add("가이드 Spotlight 연결"); }
            if (guide.Pointer != p) { guide.Pointer = p; log.Add("가이드 Pointer 연결"); }
            if (log.Count > 0) { EditorUtility.SetDirty(guide); PrefabUtility.SaveAsPrefabAsset(root, Path); }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return log.Count == 0 ? "Already applied." : "Applied: " + string.Join("; ", log);
    }

    static void Rect(RectTransform r, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
    {
        if (r.anchorMin == min && r.anchorMax == max && (min != max ? r.offsetMin == offMin && r.offsetMax == offMax : true)) return;
        r.anchorMin = min; r.anchorMax = max; r.pivot = new Vector2(.5f, .5f); if (min != max) { r.offsetMin = offMin; r.offsetMax = offMax; }
        log.Add(r.name + " 앵커");
    }
}
