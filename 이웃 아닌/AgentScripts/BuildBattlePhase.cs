using System;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds the reusable 우리 차례 / 적의 차례 banner prefab and places one instance above the battle board.
// Idempotent: an existing banner instance on the panel is kept (edit it in the prefab instead).
public static class BuildBattlePhase
{
    const string P = "Assets/Prefabs/Settlement/";
    static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pos, Vector2 size, Vector2? pivot = null)
    {
        var go = new GameObject(name, typeof(RectTransform)); var r = (RectTransform)go.transform; r.SetParent(parent, false);
        r.anchorMin = min; r.anchorMax = max; r.pivot = pivot ?? new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = size; return r;
    }
    static Image Img(RectTransform r, Sprite sprite, Color color) { var i = r.gameObject.AddComponent<Image>(); i.sprite = sprite; i.color = color; i.raycastTarget = false; i.type = sprite && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple; return i; }
    static Text Txt(RectTransform r, Font font, int size, Color color)
    {
        var t = r.gameObject.AddComponent<Text>(); t.font = font; t.fontSize = size; t.color = color; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Truncate; return t;
    }
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop first");
        var panelRoot = PrefabUtility.LoadPrefabContents(P + "ExpeditionBattlePanel.prefab");
        try
        {
            var panel = panelRoot.GetComponent<ExpeditionBattlePanel>();
            if (panel.PhaseBanner) return "Banner already placed: " + AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(panel.PhaseBanner.gameObject));
            var find = new Func<string, Transform>(n => panelRoot.GetComponentsInChildren<Transform>(true).First(t => t.name == n));
            var font = panel.Round.font; var dark = find("FeedbackPaper").GetComponent<Image>().sprite; var paper = find("LocationPaper").GetComponent<Image>().sprite;

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(P + "BattlePhaseBanner.prefab");
            if (!asset)
            {
                var root = new GameObject("BattlePhaseBanner", typeof(RectTransform)); var rr = (RectTransform)root.transform;
                rr.anchorMin = new Vector2(0, 1); rr.anchorMax = new Vector2(1, 1); rr.pivot = new Vector2(.5f, 1); rr.anchoredPosition = new Vector2(0, -318); rr.sizeDelta = new Vector2(0, 150);
                var group = root.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false; group.interactable = false;
                var banner = root.AddComponent<BattlePhaseBanner>(); banner.Group = group;
                var plate = Rect("Plate", rr, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); banner.Plate = plate;
                banner.Band = Img(Rect("Band", plate, new Vector2(0, .5f), new Vector2(1, .5f), Vector2.zero, new Vector2(0, 118)), dark, new Color(.05f, .07f, .07f, .8f));
                banner.TopStripe = Img(Rect("TopStripe", plate, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(0, 61), new Vector2(0, 4)), null, banner.AllyColor);
                banner.BottomStripe = Img(Rect("BottomStripe", plate, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(0, -61), new Vector2(0, 4)), null, banner.AllyColor);
                var sheet = Rect("Paper", plate, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(560, 132)); Img(sheet, paper, Color.white);
                banner.Marker = Img(Rect("Marker", sheet, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(34, 0), new Vector2(10, 84)), null, banner.AllyColor);
                banner.Title = Txt(Rect("Title", sheet, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 16), new Vector2(460, 72)), font, 46, banner.AllyInk); banner.Title.text = "우리 차례";
                banner.Detail = Txt(Rect("Detail", sheet, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -34), new Vector2(460, 36)), font, 22, new Color(.28f, .26f, .22f)); banner.Detail.text = "윤서진부터 · 이동 1회와 행동 1회";
                root.SetActive(false);
                asset = PrefabUtility.SaveAsPrefabAsset(root, P + "BattlePhaseBanner.prefab"); Object.DestroyImmediate(root);
            }
            var fx = panel.FxLayer; var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, fx.parent);
            instance.transform.SetSiblingIndex(fx.GetSiblingIndex() + 1); instance.SetActive(false);
            panel.PhaseBanner = instance.GetComponent<BattlePhaseBanner>();
            PrefabUtility.SaveAsPrefabAsset(panelRoot, P + "ExpeditionBattlePanel.prefab");
            return "Built " + P + "BattlePhaseBanner.prefab and placed it above the board (after " + fx.name + ").";
        }
        finally { PrefabUtility.UnloadPrefabContents(panelRoot); AssetDatabase.SaveAssets(); }
    }
}
