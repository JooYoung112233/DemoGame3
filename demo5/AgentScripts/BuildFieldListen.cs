using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// 문에 귀 대기 1차 (기획/탐험-문에귀대기-1차-구현.md): a listen button inside the door popup (FieldInformationPopup, between the
// body and the back/confirm pair) and the planner's references to it. Idempotent; run after BuildFieldTurnPlan.
// Layout lives in the prefab; runtime writes only the two texts, interactable and visibility.
public static class BuildFieldListen
{
    const string P = "Assets/Prefabs/Settlement/";
    static void Band(RectTransform r, float top, float h)
    {
        r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(.5f, 1);
        r.anchoredPosition = new Vector2(0, -top); r.sizeDelta = new Vector2(-12, h);
    }
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop first");
        var log = new List<string>();
        var popup = PrefabUtility.LoadPrefabContents(P + "FieldInformationPopup.prefab");
        try
        {
            var paper = popup.transform.Find("Paper"); var back = popup.transform.Find("Back");
            if (!paper || !back) throw new Exception("Popup layout changed: Paper/Back missing");
            if (!paper.Find("Listen"))
            {
                var go = Object.Instantiate(back.gameObject, paper); go.name = "Listen";
                var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
                r.anchoredPosition = new Vector2(240, -440); r.sizeDelta = new Vector2(520, 66); // screen (700, 645) inside the 1000×650 paper at (460, 205)
                go.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();
                foreach (var img in go.GetComponentsInChildren<Image>(true).Where(i => i.gameObject != go)) img.gameObject.SetActive(false);
                var title = go.GetComponentsInChildren<Text>(true).First(); title.name = "Title"; title.gameObject.SetActive(true);
                Band(title.rectTransform, 2, 40); title.fontSize = 25; title.alignment = TextAnchor.MiddleCenter; title.resizeTextForBestFit = false;
                title.horizontalOverflow = HorizontalWrapMode.Overflow; title.verticalOverflow = VerticalWrapMode.Truncate; title.raycastTarget = false; title.text = "문에 귀 대기";
                var sub = new GameObject("Subtitle", typeof(RectTransform)).AddComponent<Text>(); sub.transform.SetParent(go.transform, false);
                sub.font = title.font; sub.color = title.color; sub.fontSize = 16; sub.alignment = TextAnchor.MiddleCenter; sub.supportRichText = true; sub.raycastTarget = false;
                sub.horizontalOverflow = HorizontalWrapMode.Overflow; sub.verticalOverflow = VerticalWrapMode.Truncate; sub.text = "소음 0 · 시간 흐르지 않음"; Band(sub.rectTransform, 40, 26);
                go.transform.SetAsLastSibling(); go.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(popup, P + "FieldInformationPopup.prefab"); log.Add("popup listen button");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(popup); }

        var root = PrefabUtility.LoadPrefabContents(P + "ExpeditionArrivalPanel.prefab");
        try
        {
            var arrival = root.GetComponent<ExpeditionArrivalPanel>(); var planner = root.GetComponent<FieldTurnPlanner>();
            if (!planner) throw new Exception("Run BuildFieldTurnPlan first");
            var listen = arrival.Popup.transform.Find("Paper/Listen"); if (!listen) throw new Exception("Listen button missing in the nested popup");
            var button = listen.GetComponent<Button>();
            if (planner.ListenButton != button)
            {
                planner.ListenButton = button; planner.ListenTitle = listen.Find("Title").GetComponent<Text>(); planner.ListenSubtitle = listen.Find("Subtitle").GetComponent<Text>();
                EditorUtility.SetDirty(planner); PrefabUtility.SaveAsPrefabAsset(root, P + "ExpeditionArrivalPanel.prefab"); log.Add("planner wired");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); AssetDatabase.SaveAssets(); }
        return log.Count == 0 ? "Already built." : "Built: " + string.Join("; ", log);
    }
}
