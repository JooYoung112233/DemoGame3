using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Changes only expedition instances, first the panel and then its nested
/// SettlementScreen overrides. The shared FacilityHotspot asset is untouched.
/// Source painting positions use 1672 × 941; the existing contained view uses
/// 1920 × 1080. No scene, background, reward table, or input action is rebuilt.
/// </summary>
public static class BuildExplorationHotspots
{
    const string Prefabs = "Assets/Prefabs/Settlement/";
    const string Art = "Assets/Art/PartySelection/";
    const float SX = 1920f / 1672f, SY = 1080f / 941f, MarkerSize = 40;
    static readonly Color Ink = new Color(.055f, .075f, .07f, 1);
    static readonly Color Paper = new Color(.94f, .89f, .77f, 1);
    static readonly Color LightInk = new Color(.97f, .94f, .86f, 1);
    static Font font;
    static Sprite markerPaper, captionPaper, arrow;

    readonly struct Target
    {
        public readonly int Index;
        public readonly Rect Bounds;
        public readonly Vector2 Mark;
        public Target(int index, Rect bounds, Vector2 mark) { Index = index; Bounds = bounds; Mark = mark; }
    }

    // Tight bounds around objects in the original paintings, not the old badges.
    static readonly Target[] Searches = {
        new Target(0, new Rect(906, 414, 153, 125), new Vector2(1033, 437)), // Arcade crate.
        new Target(1, new Rect(1224, 468, 220, 177), new Vector2(1408, 498)), // Arcade right table.
        new Target(2, new Rect(556, 172, 175, 281), new Vector2(698, 237)), // SPACE cabinet.
        new Target(4, new Rect(190, 207, 108, 127), new Vector2(256, 242)), // Corridor fuse box.
        new Target(5, new Rect(1210, 341, 112, 113), new Vector2(1278, 373)), // Corridor right crate.
        new Target(6, new Rect(973, 168, 288, 303), new Vector2(1225, 220)), // Storage shelving.
        new Target(7, new Rect(1355, 217, 210, 262), new Vector2(1523, 303)) // Boards + toolbox.
    };

    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before modifying expedition prefabs.");
        font = Required<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");
        markerPaper = Required<Sprite>(Art + "arrow-paper.png");
        captionPaper = Required<Sprite>(Art + "footer-paper.png");
        arrow = Required<Sprite>(Art + "icon-left.png");
        var log = new List<string>();
        // The later nested pass preserves tuned Objects/SearchStatus arrays and
        // all other overrides instead of replacing the entire arrival panel.
        Edit(Prefabs + "ExpeditionArrivalPanel.prefab", log);
        Edit(Prefabs + "SettlementScreen.prefab", log);
        AssetDatabase.SaveAssets();
        return string.Join("\n", log);
    }

    static T Required<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!asset) throw new InvalidOperationException("Missing existing asset: " + path);
        return asset;
    }

    static void Edit(string path, List<string> log)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var arrivals = root.GetComponentsInChildren<ExpeditionArrivalPanel>(true);
            foreach (var arrival in arrivals) Configure(arrival, log, path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void Configure(ExpeditionArrivalPanel arrival, List<string> log, string path)
    {
        var search = arrival.GetComponent<ExpeditionSearchStatus>();
        int count = arrival.Objects.Length;
        Text[] labels = search ? Copy(search.Labels, count) : null;
        Text[] unknown = search ? Copy(search.UnknownMarks, count) : null;
        Image[] icons = search ? Copy(search.SiteIcons, count) : null;
        int changed = 0;
        foreach (var target in Searches)
        {
            if (target.Index >= count || !arrival.Objects[target.Index]) continue;
            var button = arrival.Objects[target.Index];
            var hotspot = ConfigureButton(arrival, button, target.Bounds, target.Mark, false, false);
            var mark = TextAt(button.transform, "ExplorationUnknownMark");
            Set(mark.rectTransform, LocalMark(target.Bounds, target.Mark), new Vector2(MarkerSize, MarkerSize));
            Style(mark, 25, Ink); mark.text = "?"; mark.gameObject.SetActive(true);
            var status = ImageAt(button.transform, "ExplorationSearchStatus", captionPaper);
            var caption = (RectTransform)button.transform.Find("Caption");
            Set(status.rectTransform, new Vector2(caption.anchoredPosition.x, -caption.anchoredPosition.y + 34), new Vector2(216, 32));
            status.color = new Color(.075f, .11f, .105f, .97f);
            var statusText = TextAt(status.transform, "Text");
            Set(statusText.rectTransform, new Vector2(8, 0), new Vector2(200, 32));
            Style(statusText, 20, LightInk); statusText.text = "미수색";
            hotspot.SearchStatus = Group(status.gameObject, false);
            // The old blocks may be nested added overrides. Keep their serialized
            // objects intact but remove their oversized visual presentation.
            HideLegacy(button.transform, "SearchStatus");
            HideLegacy(button.transform, "UnknownMark");
            var icon = button.transform.Find("Icon").GetComponent<Image>();
            icon.enabled = false;
            if (search) { labels[target.Index] = statusText; unknown[target.Index] = mark; icons[target.Index] = icon; }
            hotspot.RefreshPresentation();
            EditorUtility.SetDirty(hotspot);
            changed++;
        }
        if (search)
        {
            search.Arrival = arrival; search.Labels = labels; search.UnknownMarks = unknown; search.SiteIcons = icons;
            EditorUtility.SetDirty(search);
        }
        if (count > 3 && arrival.Objects[3])
        {
            ConfigureButton(arrival, arrival.Objects[3], new Rect(1545, 220, 109, 370), new Vector2(1591, 346), true, false);
            changed++;
        }
        var rooms = arrival.Rooms;
        if (rooms)
        {
            if (rooms.CorridorBack) { ConfigureButton(arrival, rooms.CorridorBack, new Rect(27, 208, 102, 356), new Vector2(78, 367), true, true); changed++; }
            if (rooms.LockedDoor) { ConfigureButton(arrival, rooms.LockedDoor, new Rect(936, 184, 177, 256), new Vector2(1042, 300), true, false); changed++; }
            if (rooms.OfficeDoor) { ConfigureButton(arrival, rooms.OfficeDoor, new Rect(1404, 206, 132, 309), new Vector2(1454, 334), true, false); changed++; }
            if (rooms.StorageBack) { ConfigureButton(arrival, rooms.StorageBack, new Rect(28, 173, 124, 412), new Vector2(78, 353), true, true); changed++; }
        }
        log.Add(path + ": " + changed + " expedition targets; 40px marks, object bounds, hover/focus/Alt details, independent threat tags.");
    }

    static ExplorationHotspot ConfigureButton(ExpeditionArrivalPanel arrival, Button button, Rect source, Vector2 sourceMark, bool door, bool pointsLeft)
    {
        var rect = (RectTransform)button.transform;
        Set(rect, new Vector2(source.x * SX, source.y * SY), new Vector2(source.width * SX, source.height * SY));
        var oldHover = button.GetComponent<SettlementHotspot>();
        if (oldHover) { oldHover.enabled = false; oldHover.SuppressLabel = true; EditorUtility.SetDirty(oldHover); }
        foreach (var effect in button.GetComponents<BaseMeshEffect>()) effect.enabled = false;
        var hit = button.GetComponent<Image>();
        if (!hit) hit = button.gameObject.AddComponent<Image>();
        hit.sprite = null; hit.type = Image.Type.Simple; hit.color = Color.clear; hit.enabled = true;
        hit.raycastTarget = true; hit.alphaHitTestMinimumThreshold = 0;
        var markPosition = LocalMark(source, sourceMark);
        var marker = ImageAt(button.transform, "ExplorationMarkerPaper", markerPaper);
        marker.transform.SetAsFirstSibling();
        Set(marker.rectTransform, markPosition, new Vector2(MarkerSize, MarkerSize));
        marker.color = Paper;
        button.transition = Selectable.Transition.None;
        button.targetGraphic = marker;
        var navigation = button.navigation; navigation.mode = Navigation.Mode.Automatic; button.navigation = navigation;
        var iconTransform = button.transform.Find("Icon");
        var icon = iconTransform ? iconTransform.GetComponent<Image>() : null;
        if (!icon) icon = ImageAt(button.transform, "Icon", null);
        Set(icon.rectTransform, markPosition + new Vector2(8, 8), new Vector2(24, 24));
        icon.color = Ink; icon.preserveAspect = true; icon.raycastTarget = false;
        if (door)
        {
            icon.sprite = arrow; icon.enabled = true;
            // Flip through local rotation so the icon stays centered in its slot.
            icon.rectTransform.pivot = new Vector2(.5f, .5f);
            icon.rectTransform.anchoredPosition += new Vector2(12, -12);
            icon.rectTransform.localEulerAngles = pointsLeft ? Vector3.zero : new Vector3(0, 180, 0);
            HideLegacy(button.transform, "UnknownMark"); HideLegacy(button.transform, "ExplorationUnknownMark");
            HideLegacy(button.transform, "SearchStatus"); HideLegacy(button.transform, "ExplorationSearchStatus");
        }
        var caption = ImageAt(button.transform, "Caption", captionPaper);
        float width = door ? 148 : 216;
        float canvasMarkX = sourceMark.x * SX;
        float captionCanvasX = Mathf.Clamp(canvasMarkX - width * .5f, 24, 1920 - width - 24);
        var captionPosition = new Vector2(captionCanvasX - source.x * SX, markPosition.y + MarkerSize + 7);
        Set(caption.rectTransform, captionPosition, new Vector2(width, 34));
        caption.color = Paper; caption.gameObject.SetActive(true);
        var text = TextAt(caption.transform, "Text");
        Set(text.rectTransform, new Vector2(7, 0), new Vector2(width - 14, 34));
        Style(text, 22, Ink);
        var focus = Corners(button.transform, rect.sizeDelta);
        var behaviour = button.GetComponent<ExplorationHotspot>();
        if (!behaviour) behaviour = button.gameObject.AddComponent<ExplorationHotspot>();
        behaviour.Arrival = arrival; behaviour.Button = button; behaviour.HitArea = hit; behaviour.Marker = marker;
        behaviour.IsDoor = door; behaviour.SourceObjectPixels = source; behaviour.Caption = Group(caption.gameObject, door);
        behaviour.FocusCorners = Group(focus.gameObject, false);
        if (door)
        {
            var status = button.transform.Find("DoorStatus") as RectTransform;
            if (status)
            {
                Set(status, captionPosition + new Vector2(-34, 38), new Vector2(216, 32));
                var statusImage = status.GetComponent<Image>();
                if (statusImage) { statusImage.sprite = captionPaper; statusImage.color = new Color(.075f, .11f, .105f, .97f); statusImage.raycastTarget = false; }
                var statusText = status.GetComponentInChildren<Text>(true);
                if (statusText) { Set(statusText.rectTransform, new Vector2(8, 0), new Vector2(200, 32)); Style(statusText, 20, LightInk); }
                status.gameObject.SetActive(true); behaviour.DoorStatus = Group(status.gameObject, false);
            }
            // The threat's Show/Hide logic owns this node. Only its position is
            // moved into a dedicated row above the marker; never group it with details.
            var footsteps = button.transform.Find("Footsteps") as RectTransform;
            if (footsteps)
            {
                float alertCanvasX = Mathf.Clamp(canvasMarkX - 88, 24, 1920 - 176 - 24);
                Set(footsteps, new Vector2(alertCanvasX - source.x * SX, markPosition.y - 50), new Vector2(176, 42));
                footsteps.transform.SetAsLastSibling();
                foreach (var graphic in footsteps.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            }
        }
        foreach (var graphic in button.GetComponentsInChildren<Graphic>(true)) if (graphic != hit) graphic.raycastTarget = false;
        behaviour.RefreshPresentation();
        EditorUtility.SetDirty(button); EditorUtility.SetDirty(behaviour);
        return behaviour;
    }

    static Vector2 LocalMark(Rect source, Vector2 mark) => new Vector2((mark.x - source.x) * SX - MarkerSize * .5f, (mark.y - source.y) * SY - MarkerSize * .5f);
    static T[] Copy<T>(T[] source, int size) { var result = new T[size]; if (source != null) Array.Copy(source, result, Mathf.Min(source.Length, size)); return result; }
    static void HideLegacy(Transform parent, string name) { var found = parent.Find(name); if (found) found.gameObject.SetActive(false); }
    static void Set(RectTransform rect, Vector2 topLeft, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        rect.anchoredPosition = new Vector2(topLeft.x, -topLeft.y); rect.sizeDelta = size;
    }
    static RectTransform Child(Transform parent, string name)
    {
        var found = parent.Find(name) as RectTransform;
        if (found) return found;
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect;
    }
    static Image ImageAt(Transform parent, string name, Sprite sprite)
    {
        var rect = Child(parent, name);
        var image = rect.GetComponent<Image>();
        if (!image) image = rect.gameObject.AddComponent<Image>();
        if (sprite) image.sprite = sprite; image.raycastTarget = false; image.type = Image.Type.Simple;
        image.gameObject.SetActive(true); return image;
    }
    static Text TextAt(Transform parent, string name)
    {
        var rect = Child(parent, name);
        var text = rect.GetComponent<Text>();
        if (!text) text = rect.gameObject.AddComponent<Text>();
        text.raycastTarget = false; text.gameObject.SetActive(true); return text;
    }
    static void Style(Text text, int size, Color color)
    {
        text.font = font; text.fontSize = size; text.resizeTextForBestFit = false; text.color = color;
        text.alignment = TextAnchor.MiddleCenter; text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate; text.raycastTarget = false;
    }
    static CanvasGroup Group(GameObject obj, bool visible)
    {
        var group = obj.GetComponent<CanvasGroup>();
        if (!group) group = obj.AddComponent<CanvasGroup>();
        group.alpha = visible ? 1 : 0; group.blocksRaycasts = group.interactable = false; group.ignoreParentGroups = false; return group;
    }
    static RectTransform Corners(Transform parent, Vector2 size)
    {
        var rect = Child(parent, "ExplorationFocusCorners"); Set(rect, Vector2.zero, size);
        for (int corner = 0; corner < 4; corner++)
        {
            bool right = (corner & 1) != 0, bottom = (corner & 2) != 0;
            var h = ImageAt(rect, "Horizontal" + corner, null); h.sprite = null; h.color = new Color(.98f, .79f, .43f, .88f);
            var v = ImageAt(rect, "Vertical" + corner, null); v.sprite = null; v.color = h.color;
            Set(h.rectTransform, new Vector2(right ? size.x - 15 : 0, bottom ? size.y - 2 : 0), new Vector2(15, 2));
            Set(v.rectTransform, new Vector2(right ? size.x - 2 : 0, bottom ? size.y - 15 : 0), new Vector2(2, 15));
        }
        rect.SetAsFirstSibling(); return rect;
    }
}
