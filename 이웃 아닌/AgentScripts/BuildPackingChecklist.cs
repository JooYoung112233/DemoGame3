using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Packing checklist as in approved mock 03 (2026-09-25 user: "시안처럼 막대로 해줘"): three rows (icon · name · gauge) and a red notice
// with a warning sign, a title and one line. Base ExpeditionPackingPanel.prefab only (SettlementScreen's instance inherits it).
// Idempotent: prints what it changes, 'Already applied.' when nothing does. Run after PolishRemainingUI (it only sets fonts here).
public static class BuildPackingChecklist
{
    const string PackPath = "Assets/Prefabs/Settlement/ExpeditionPackingPanel.prefab", InvPath = "Assets/Prefabs/Settlement/InventoryPanel.prefab";
    static readonly List<string> log = new List<string>();
    static readonly (string label, string[] items, string icon)[] Rows = {
        ("식량·물", new[] { "food", "water", "can", "meal", "ration" }, "water"),
        ("치료", new[] { "bandage" }, "bandage"),
        ("탄약·도구", new[] { "ammo", "prybar", "flashlight" }, "ammo") };
    static readonly Color Ink = new Color(.045f, .065f, .06f, 1), Track = new Color(.16f, .17f, .16f, 1), Green = new Color(.36f, .66f, .36f, 1), Sign = new Color(.62f, .13f, .11f, 1);

    public static string Run()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Stop Play and preserve the scene first");
        log.Clear();
        var inv = AssetDatabase.LoadAssetAtPath<GameObject>(InvPath)?.GetComponent<SettlementInventoryPanel>() ?? throw new Exception("Inventory items missing");
        var root = PrefabUtility.LoadPrefabContents(PackPath);
        try
        {
            var p = root.GetComponent<ExpeditionPackingPanel>(); var ws = root.transform.Find("Workspace");
            var paperSprite = ws.Find("ChecklistPaper").GetComponent<Image>().sprite; var font = ws.Find("ChecklistTitle").GetComponent<Text>().font;
            Active(ws.Find("ChecklistPaper"), false); Active(ws.Find("Checklist"), false);
            int after = ws.Find("ChecklistTitle").GetSiblingIndex();
            var rows = new ExpeditionPackingPanel.CheckRow[Rows.Length];
            for (int i = 0; i < Rows.Length; i++)
            {
                var (label, items, icon) = Rows[i]; float y = 716 + i * 44;
                var row = Child(ws, "CheckRow" + i, typeof(Image)); Place(row, 1372, y, 438, 40); Img(row, paperSprite, Color.white);
                if (row.GetSiblingIndex() != after + 1 + i) row.SetSiblingIndex(after + 1 + i);
                var ic = Child(row, "Icon", typeof(Image)); Place(ic, 12, 5, 30, 30); var icImg = Img(ic, inv.Items.First(x => x.Id == icon).Icon, Color.white); if (!icImg.preserveAspect) { icImg.preserveAspect = true; log.Add(row.name + " 아이콘 비율"); }
                var name = Child(row, "Name", typeof(Text)); Place(name, 52, 0, 140, 40); var t = Txt(name, font, 22, label, TextAnchor.MiddleLeft);
                var track = Child(row, "Track", typeof(Image)); Place(track, 196, 12, 226, 16); Img(track, null, Track);
                var fill = Child(track, "Fill", typeof(Image)); var fr = (RectTransform)fill; if (fr.anchorMin != Vector2.zero || fr.offsetMin != Vector2.zero || fr.offsetMax != Vector2.zero) { fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(.5f, 1); fr.pivot = new Vector2(0, .5f); fr.offsetMin = fr.offsetMax = Vector2.zero; log.Add(row.name + " 막대"); }
                Img(fill, null, Green);
                rows[i] = new ExpeditionPackingPanel.CheckRow { Label = label, Items = items, Name = t, Fill = fr };
            }
            if (!Same(p.CheckRows, rows)) { p.CheckRows = rows; log.Add("체크리스트 3줄 연결"); }

            // Notice: sign · title · one line (mock 03).
            var paper = ws.Find("NoticePaper"); Place(paper, 1372, 852, 438, 72);
            var sign = Child(ws, "NoticeSign", typeof(WarningTriangle)); Place(sign, 1388, 866, 40, 36); var tri = sign.GetComponent<WarningTriangle>(); if (tri.color != Sign) { tri.color = Sign; log.Add("알림 표시 색"); }
            if (sign.GetSiblingIndex() != paper.GetSiblingIndex() + 1) sign.SetSiblingIndex(paper.GetSiblingIndex() + 1);
            var bang = Child(sign, "Mark", typeof(Text)); Place(bang, 0, 6, 40, 30); var bt = Txt(bang, font, 24, "!", TextAnchor.MiddleCenter); if (bt.color != Color.white) { bt.color = Color.white; log.Add("알림 ! 색"); }
            var title = Child(ws, "NoticeTitle", typeof(Text)); Place(title, 1438, 855, 360, 32); var tt = Txt(title, font, 22, p.NoticeTitleText, TextAnchor.MiddleLeft);
            if (title.GetSiblingIndex() != sign.GetSiblingIndex() + 1) title.SetSiblingIndex(sign.GetSiblingIndex() + 1);
            Place(p.Notice.transform, 1438, 886, 366, 32); if (p.Notice.fontSize != 18) { p.Notice.fontSize = 18; log.Add("알림 본문 18"); }
            if (p.NoticeTitle != tt) { p.NoticeTitle = tt; log.Add("알림 제목 연결"); }
            if (p.NoticeDefault != "필요한 물품을 모두 챙겼는지 다시 확인하세요.") { p.NoticeDefault = "필요한 물품을 모두 챙겼는지 다시 확인하세요."; log.Add("알림 기본 문구"); }
            if (log.Count > 0) { EditorUtility.SetDirty(p); PrefabUtility.SaveAsPrefabAsset(root, PackPath); }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return log.Count == 0 ? "Already applied." : "Applied: " + string.Join("; ", log);
    }

    static bool Same(ExpeditionPackingPanel.CheckRow[] a, ExpeditionPackingPanel.CheckRow[] b) => a != null && a.Length == b.Length && a.Zip(b, (x, y) => x != null && x.Label == y.Label && x.Items.SequenceEqual(y.Items) && x.Name == y.Name && x.Fill == y.Fill).All(v => v);
    static void Active(Transform t, bool on) { if (t && t.gameObject.activeSelf != on) { t.gameObject.SetActive(on); log.Add(t.name + (on ? " 켜기" : " 끄기")); } }
    static Transform Child(Transform parent, string name, Type component)
    {
        var t = parent.Find(name); if (t) return t;
        var g = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), component); g.transform.SetParent(parent, false); g.layer = parent.gameObject.layer; log.Add(parent.name + "/" + name + " 생성"); return g.transform;
    }
    // Top-left placement in the parent's space (1920×1080 for Workspace children), like the other packing builders.
    static void Place(Transform t, float x, float y, float w, float h)
    {
        var r = (RectTransform)t; var pos = new Vector2(x, -y); var size = new Vector2(w, h);
        if (r.anchorMin == new Vector2(0, 1) && r.anchorMax == new Vector2(0, 1) && r.pivot == new Vector2(0, 1) && r.anchoredPosition == pos && r.sizeDelta == size) return;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = pos; r.sizeDelta = size; log.Add(t.name + $" {x},{y},{w},{h}");
    }
    static Image Img(Transform t, Sprite sprite, Color color)
    {
        var i = t.GetComponent<Image>(); if (i.sprite != sprite || i.color != color || i.raycastTarget) { i.sprite = sprite; i.color = color; i.raycastTarget = false; log.Add(t.name + " 그림"); }
        return i;
    }
    static Text Txt(Transform t, Font font, int size, string text, TextAnchor align)
    {
        var x = t.GetComponent<Text>();
        if (x.font != font || x.fontSize != size || x.text != text || x.alignment != align || x.color != Ink && t.name != "Mark" || x.raycastTarget || x.verticalOverflow != VerticalWrapMode.Overflow)
        { x.font = font; x.fontSize = size; x.text = text; x.alignment = align; if (t.name != "Mark") x.color = Ink; x.raycastTarget = false; x.horizontalOverflow = HorizontalWrapMode.Wrap; x.verticalOverflow = VerticalWrapMode.Overflow; log.Add(t.name + " 글자"); }
        return x;
    }
}
