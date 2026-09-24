using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Authoring only. Reuses the existing paper, font, heading and button assets.
/// Run after ExpeditionNpcStory is compiled. No scene or shared UI prefab is rebuilt.
/// </summary>
public static class BuildNpcStory
{
    const string P = "Assets/Prefabs/Settlement/";
    const string ViewPath = P + "ExpeditionNpcStoryView.prefab";
    const string ViewName = "ExpeditionNpcStoryView";
    const string ClueName = "HiddenNpcClue";
    const float SX = 1920f / 1672f, SY = 1080f / 941f;
    static readonly Rect ClueBounds = new Rect(39, 180, 165, 300);
    static readonly Vector2 CluePoint = new Vector2(162, 365);
    static readonly Color Ink = new Color(.055f, .075f, .07f, 1);
    static readonly Color Paper = new Color(.94f, .89f, .77f, 1);
    static Font font;
    static Sprite cardPaper, stripPaper, markerPaper, npcBody;
    static GameObject headingPrefab, npcPrefab;
    static Button buttonTemplate;
    static Material portraitMaterial;

    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building NPC story UI.");
        font = Required<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");
        cardPaper = Required<Sprite>("Assets/Art/PartySelection/card-paper.png");
        stripPaper = Required<Sprite>("Assets/Art/PartySelection/footer-paper.png");
        markerPaper = Required<Sprite>("Assets/Art/PartySelection/arrow-paper.png");
        npcBody = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Tokens/npc-doyun-body-v3.png");
        if (!npcBody) npcBody = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Tokens/npc-doyun-body-v2.png");
        if (!npcBody) npcBody = Required<Sprite>("Assets/Art/Tokens/npc-doyun-body.png");
        portraitMaterial = Required<Material>("Assets/Settings/PaperPortrait.mat");
        headingPrefab = Required<GameObject>(P + "PopupHeading.prefab");
        npcPrefab = Required<GameObject>(P + "Npc_doyun.prefab");
        var popup = Required<GameObject>(P + "FieldInformationPopup.prefab");
        var back = popup.transform.Find("Back");
        if (!back || !(buttonTemplate = back.GetComponent<Button>())) throw new InvalidOperationException("Existing field popup Back button is missing.");
        var view = BuildView();
        var log = new List<string>();
        Wire(P + "ExpeditionArrivalPanel.prefab", view, log);
        Wire(P + "SettlementScreen.prefab", view, log);
        AssetDatabase.SaveAssets();
        return string.Join("\n", log);
    }

    static T Required<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!asset) throw new InvalidOperationException("Missing existing asset: " + path);
        return asset;
    }

    static GameObject BuildView()
    {
        bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(ViewPath);
        var root = existing ? PrefabUtility.LoadPrefabContents(ViewPath) : new GameObject(ViewName, typeof(RectTransform));
        try
        {
            Rect(root.transform, 0, 0, 1920, 1080);
            var hud = root.GetComponent<PopupBackgroundHud>();
            if (!hud) hud = root.AddComponent<PopupBackgroundHud>();
            var investigation = Child(root.transform, "InvestigationLayout");
            Rect(investigation, 0, 0, 1920, 1080);
            // Migrate the existing authored view without replacing its controls.
            foreach (string name in new[] { "Dim", "PopupHeading", "Paper", "PortraitPaper", "Portrait", "UnknownPortrait",
                "Speaker", "Body", "Divider", "MemberHeading", "Members", "Choices", "Status" })
            {
                var legacy = root.transform.Find(name);
                if (legacy) legacy.SetParent(investigation, false);
            }
            var dim = Image(investigation, "Dim", 0, 0, 1920, 1080, null);
            dim.color = new Color(.012f, .032f, .033f, .97f); dim.raycastTarget = true;
            dim.transform.SetAsFirstSibling();
            var heading = investigation.Find("PopupHeading");
            if (!heading) heading = ((GameObject)PrefabUtility.InstantiatePrefab(headingPrefab, investigation)).transform;
            heading.name = "PopupHeading"; Rect(heading, 80, 32, 540, 72);
            var headingText = heading.GetComponentInChildren<Text>(true);
            Style(headingText, 34, TextAnchor.MiddleLeft); headingText.text = "탐험 · 낯선 흔적";
            PrefabUtility.RecordPrefabInstancePropertyModifications(headingText);
            Image(investigation, "Paper", 260, 142, 1400, 762, cardPaper).color = Color.white;
            // One paper reading surface: fixed portrait left, flexible prose right.
            Image(investigation, "PortraitPaper", 292, 172, 232, 302, cardPaper).color = new Color(.88f, .85f, .75f, 1);
            FitPortrait(Image(investigation, "Portrait", 304, 184, 208, 278, npcBody), new Vector2(408, -323));
            Label(investigation, "UnknownPortrait", 292, 172, 232, 302, "?", 76, TextAnchor.MiddleCenter);
            Label(investigation, "Speaker", 556, 166, 1072, 60, "?", 32);
            Label(investigation, "Body", 556, 228, 1072, 300,
                "오락기 뒤에서 먼지가 밀린 흔적이 이어집니다.\n안쪽에서 작은 금속 소리가 멎었습니다.", 30, TextAnchor.UpperLeft);
            Image(investigation, "Divider", 292, 532, 1336, 2, null).color = new Color(.22f, .25f, .20f, .24f);
            Label(investigation, "MemberHeading", 292, 486, 232, 38, "누가 살펴볼까요?", 22);
            var members = Child(investigation, "Members"); Rect(members, 292, 542, 1336, 54);
            for (int i = 0; i < 6; i++)
            {
                var button = Button(members, "Member" + i, i * 224, 0, 200, 54, "대원", 26);
                button.GetComponent<Image>().color = Color.white;
            }
            var choices = Child(investigation, "Choices"); Rect(choices, 292, 610, 1336, 174);
            for (int i = 0; i < 3; i++)
            {
                var button = Button(choices, "Choice" + i, i * 452, 0, 432, 174, "선택", 26);
                var choicePaper = button.GetComponent<Image>();
                choicePaper.sprite = cardPaper; choicePaper.color = new Color(.80f, .84f, .65f, 1);
                var title = ButtonLabel(button); title.name = "Title";
                Rect(title.transform, 18, 10, 396, 76); Style(title, 26, TextAnchor.MiddleLeft);
                Label(button.transform, "Detail", 18, 94, 396, 66, "행동과 필요한 조건이 표시됩니다.", 22, TextAnchor.UpperLeft);
            }
            Label(investigation, "Status", 292, 800, 1336, 76,
                "선택하기 전에는 시간이 흐르지 않습니다.", 24, TextAnchor.MiddleLeft);

            var dialogue = Child(root.transform, "DialogueLayout"); Rect(dialogue, 0, 0, 1920, 1080);
            var dialogueDim = Image(dialogue, "Dim", 0, 0, 1920, 1080, null);
            dialogueDim.color = new Color(.012f, .032f, .033f, .32f); dialogueDim.raycastTarget = true;
            dialogueDim.transform.SetAsFirstSibling();
            var dialoguePaper = Image(dialogue, "Paper", 80, 724, 1760, 232, stripPaper);
            dialoguePaper.color = Color.white; dialoguePaper.raycastTarget = true;
            var continueSurface = dialoguePaper.GetComponent<Button>();
            if (!continueSurface) continueSurface = dialoguePaper.gameObject.AddComponent<Button>();
            continueSurface.targetGraphic = dialoguePaper; continueSurface.transition = Selectable.Transition.None;
            continueSurface.onClick = new Button.ButtonClickedEvent();
            continueSurface.navigation = new Navigation { mode = Navigation.Mode.None };
            FitPortrait(Image(dialogue, "Portrait", 110, 746, 140, 180, npcBody), new Vector2(180, -836));
            Label(dialogue, "UnknownPortrait", 110, 746, 140, 180, "?", 76, TextAnchor.MiddleCenter);
            // Funflow's 32px line needs 50px; it still clears the body at y792.
            Label(dialogue, "Speaker", 278, 738, 1200, 50, "?", 32);
            Label(dialogue, "Body", 278, 792, 1480, 118, "안쪽에서 작은 금속 소리가 멎었습니다.", 30, TextAnchor.UpperLeft);
            // Keep both choices above even the widest six-member field formation.
            var dialogueChoices = Child(dialogue, "Choices"); Rect(dialogueChoices, 1120, 300, 720, 200);
            for (int i = 0; i < 2; i++)
            {
                var button = Button(dialogueChoices, "Choice" + i, 0, i * 108, 720, 92, "말을 건넨다", 28);
                button.GetComponent<Image>().color = new Color(.80f, .84f, .65f, 1);
                var label = ButtonLabel(button); Rect(label.transform, 28, 6, 664, 80); Style(label, 28, TextAnchor.MiddleLeft);
                button.gameObject.SetActive(false);
            }
            Button(dialogue, "Next", 1470, 974, 370, 76, "다음", 32);
            investigation.gameObject.SetActive(true); dialogue.gameObject.SetActive(false);
            Button(root.transform, "Back", 80, 974, 410, 76, "탐험으로 돌아가기", 32).transform.SetAsLastSibling();
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
                if (!graphic.GetComponent<Button>() && graphic != dim && graphic != dialogueDim) graphic.raycastTarget = false;
            var asset = PrefabUtility.SaveAsPrefabAsset(root, ViewPath);
            if (!asset) throw new InvalidOperationException("Could not save NPC story view.");
            return asset;
        }
        finally { if (existing) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
    }

    static void Wire(string path, GameObject viewAsset, List<string> log)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var owner = root.GetComponent<SettlementController>();
            foreach (var arrival in root.GetComponentsInChildren<ExpeditionArrivalPanel>(true))
            {
                var story = arrival.GetComponent<ExpeditionNpcStory>();
                if (!story) story = arrival.gameObject.AddComponent<ExpeditionNpcStory>();
                var viewTransform = arrival.transform.Find(ViewName);
                if (!viewTransform) viewTransform = ((GameObject)PrefabUtility.InstantiatePrefab(viewAsset, arrival.transform)).transform;
                if (viewTransform.IsChildOf(arrival.Main.transform)) throw new InvalidOperationException("Story view must stay outside Arrival.Main.");
                var view = viewTransform.gameObject;
                story.Owner = owner; story.Arrival = arrival; story.View = view;
                var investigation = At<RectTransform>(viewTransform, "InvestigationLayout");
                var dialogue = At<RectTransform>(viewTransform, "DialogueLayout");
                story.InvestigationLayout = investigation.gameObject; story.DialogueLayout = dialogue.gameObject;
                story.Title = At<Text>(investigation, "PopupHeading/Label");
                story.Speaker = At<Text>(investigation, "Speaker"); story.Body = At<Text>(investigation, "Body");
                story.Status = At<Text>(investigation, "Status"); story.Portrait = At<Image>(investigation, "Portrait");
                story.UnknownPortrait = At<Text>(investigation, "UnknownPortrait");
                story.MemberHeading = At<Text>(investigation, "MemberHeading");
                story.Back = At<Button>(viewTransform, "Back"); story.NpcPrefab = npcPrefab;
                story.Members = new Button[6]; story.MemberLabels = new Text[6];
                story.Choices = new Button[3]; story.ChoiceTitles = new Text[3]; story.ChoiceDetails = new Text[3];
                for (int i = 0; i < 6; i++)
                {
                    story.Members[i] = At<Button>(investigation, "Members/Member" + i);
                    story.MemberLabels[i] = ButtonLabel(story.Members[i]);
                }
                for (int i = 0; i < 3; i++)
                {
                    story.Choices[i] = At<Button>(investigation, "Choices/Choice" + i);
                    story.ChoiceTitles[i] = At<Text>(story.Choices[i].transform, "Title");
                    story.ChoiceDetails[i] = At<Text>(story.Choices[i].transform, "Detail");
                }
                story.DialogueSpeaker = At<Text>(dialogue, "Speaker"); story.DialogueBody = At<Text>(dialogue, "Body");
                story.DialoguePortrait = At<Image>(dialogue, "Portrait"); story.DialogueUnknownPortrait = At<Text>(dialogue, "UnknownPortrait");
                story.Next = At<Button>(dialogue, "Next"); story.NextLabel = ButtonLabel(story.Next);
                story.ContinueSurface = At<Button>(dialogue, "Paper");
                story.DialogueChoices = new Button[2]; story.DialogueChoiceLabels = new Text[2];
                for (int i = 0; i < 2; i++)
                {
                    story.DialogueChoices[i] = At<Button>(dialogue, "Choices/Choice" + i);
                    story.DialogueChoiceLabels[i] = ButtonLabel(story.DialogueChoices[i]);
                }
                WireClue(story, arrival);
                view.SetActive(false); story.Clue.gameObject.SetActive(false);
                EditorUtility.SetDirty(story);
                log.Add(path + ": separate investigation and dialogue layouts; 6 member tabs, 3 investigation cards, 2 dialogue choices and hidden clue wired.");
            }
            if (owner) AddRecordTab(owner, log);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void WireClue(ExpeditionNpcStory story, ExpeditionArrivalPanel arrival)
    {
        var found = arrival.Main.transform.Find(ClueName);
        if (!found)
        {
            if (arrival.Objects.Length == 0 || !arrival.Objects[0]) throw new InvalidOperationException("A styled exploration button is required.");
            found = Object.Instantiate(arrival.Objects[0].gameObject, arrival.Main.transform).transform;
            found.name = ClueName;
        }
        var clue = found.GetComponent<Button>();
        if (!clue) throw new InvalidOperationException("Clue instance has no Button.");
        clue.onClick = new Button.ButtonClickedEvent(); clue.transition = Selectable.Transition.None;
        Rect(found, ClueBounds.x * SX, ClueBounds.y * SY, ClueBounds.width * SX, ClueBounds.height * SY);
        var hit = clue.GetComponent<Image>();
        if (!hit) hit = clue.gameObject.AddComponent<Image>();
        hit.sprite = null; hit.color = Color.clear; hit.raycastTarget = true; hit.alphaHitTestMinimumThreshold = 0;
        var hover = clue.GetComponent<SettlementHotspot>();
        if (hover) { hover.enabled = false; hover.SuppressLabel = true; }
        foreach (var effect in clue.GetComponents<BaseMeshEffect>()) effect.enabled = false;
        float mx = (CluePoint.x - ClueBounds.x) * SX - 20, my = (CluePoint.y - ClueBounds.y) * SY - 20;
        var markPaper = Image(found, "ExplorationMarkerPaper", mx, my, 40, 40, markerPaper); markPaper.color = Paper;
        var mark = Label(found, "ExplorationUnknownMark", mx, my, 40, 40, "?", 25, TextAnchor.MiddleCenter);
        var icon = found.Find("Icon"); if (icon) icon.gameObject.SetActive(false);
        foreach (string old in new[] { "UnknownMark", "SearchStatus", "ExplorationSearchStatus" })
        { var child = found.Find(old); if (child) child.gameObject.SetActive(false); }
        float captionX = Mathf.Clamp(CluePoint.x * SX - 108, 24, 1680) - ClueBounds.x * SX;
        var caption = Image(found, "Caption", captionX, my + 47, 216, 34, stripPaper); caption.color = Paper;
        var captionText = Label(caption.transform, "Text", 7, 0, 202, 34, "밀린 먼지", 22, TextAnchor.MiddleCenter);
        var hotspot = clue.GetComponent<ExplorationHotspot>();
        if (!hotspot) hotspot = clue.gameObject.AddComponent<ExplorationHotspot>();
        hotspot.Arrival = arrival; hotspot.Button = clue; hotspot.HitArea = hit; hotspot.Marker = markPaper;
        hotspot.SourceObjectPixels = ClueBounds; hotspot.IsDoor = false;
        hotspot.Caption = Group(caption.gameObject); hotspot.SearchStatus = null; hotspot.DoorStatus = null;
        var focus = Child(found, "ExplorationFocusCorners"); Rect(focus, 0, 0, ClueBounds.width * SX, ClueBounds.height * SY);
        float w = ClueBounds.width * SX, h = ClueBounds.height * SY;
        for (int i = 0; i < 4; i++)
        {
            bool right = (i & 1) != 0, bottom = (i & 2) != 0;
            Image(focus, "Horizontal" + i, right ? w - 15 : 0, bottom ? h - 2 : 0, 15, 2, null).color = new Color(.98f, .79f, .43f, .88f);
            Image(focus, "Vertical" + i, right ? w - 2 : 0, bottom ? h - 15 : 0, 2, 15, null).color = new Color(.98f, .79f, .43f, .88f);
        }
        hotspot.FocusCorners = Group(focus.gameObject);
        clue.targetGraphic = markPaper;
        foreach (var graphic in found.GetComponentsInChildren<Graphic>(true)) if (graphic != hit) graphic.raycastTarget = false;
        story.Clue = clue; story.Hotspot = hotspot; story.ClueLabel = captionText; story.ClueMark = mark;
        hotspot.RefreshPresentation(); EditorUtility.SetDirty(hotspot);
    }

    static void AddRecordTab(SettlementController owner, List<string> log)
    {
        var opening = owner.Opening;
        if (!opening) opening = owner.GetComponent<SettlementOpeningChapter>();
        if (!opening || !opening.RecordsView || opening.RecordTabs == null || opening.RecordTabs.Length < 4)
            throw new InvalidOperationException("Existing opening records and their four tabs must be present.");
        var tabs = opening.RecordsView.transform.Find("Tabs");
        if (!tabs) throw new InvalidOperationException("Existing record Tabs container is missing.");
        var tabTransform = tabs.Find("Tab4");
        Button tab;
        if (tabTransform) tab = tabTransform.GetComponent<Button>();
        else
        {
            tab = Object.Instantiate(opening.RecordTabs[0], tabs); tab.name = "Tab4";
        }
        if (!tab) throw new InvalidOperationException("Record tab template has no Button.");
        tab.onClick = new Button.ButtonClickedEvent();
        Rect(tab.transform, 0, 0, 280, 64);
        var label = ButtonLabel(tab); Style(label, 26, TextAnchor.MiddleLeft); label.text = "오락기 뒤의 사람";
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(16, 4); label.rectTransform.offsetMax = new Vector2(-16, -4);
        var layout = tab.GetComponent<LayoutElement>(); if (!layout) layout = tab.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = layout.minHeight = 64;
        var container = (RectTransform)tabs;
        container.sizeDelta = new Vector2(container.sizeDelta.x, Mathf.Max(380, container.sizeDelta.y));
        var references = opening.RecordTabs.ToList(); while (references.Count < 5) references.Add(null); references[4] = tab;
        opening.RecordTabs = references.ToArray(); tab.gameObject.SetActive(false);
        EditorUtility.SetDirty(opening);
        log.Add("Settlement records: 5th undiscovered NPC tab added; 280px column retained, 380px height fits above y734 report button.");
    }

    static void FitPortrait(Image portrait, Vector2 center)
    {
        // The imported sprite already uses the native body alpha bounds. Fitting
        // those bounds again in BattlePortraitFit crops the left side twice.
        var fit = portrait.GetComponent<BattlePortraitFit>();
        if (fit) Object.DestroyImmediate(fit);
        portrait.color = Color.white; portrait.preserveAspect = true; portrait.useSpriteMesh = false;
        portrait.rectTransform.pivot = new Vector2(.5f, .5f);
        portrait.rectTransform.anchoredPosition = center;
        var ink = portrait.GetComponent<PaperPortraitStyle>();
        if (!ink) ink = portrait.gameObject.AddComponent<PaperPortraitStyle>();
        ink.Configure(portraitMaterial);
    }

    static T At<T>(Transform parent, string path) where T : Component
    {
        var child = parent.Find(path); var component = child ? child.GetComponent<T>() : null;
        if (!component) throw new InvalidOperationException("Missing UI reference: " + path);
        return component;
    }
    static RectTransform Child(Transform parent, string name)
    {
        var child = parent.Find(name) as RectTransform;
        if (child) return child;
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect;
    }
    static void Rect(Transform transform, float x, float y, float w, float h)
    {
        var rect = (RectTransform)transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
    }
    static Image Image(Transform parent, string name, float x, float y, float w, float h, Sprite sprite)
    {
        var rect = Child(parent, name); Rect(rect, x, y, w, h);
        var image = rect.GetComponent<Image>(); if (!image) image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite; image.raycastTarget = false; image.type = UnityEngine.UI.Image.Type.Simple;
        image.gameObject.SetActive(true); return image;
    }
    static Text Label(Transform parent, string name, float x, float y, float w, float h, string text, int size, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        var rect = Child(parent, name); Rect(rect, x, y, w, h);
        var label = rect.GetComponent<Text>(); if (!label) label = rect.gameObject.AddComponent<Text>();
        Style(label, size, anchor); label.text = text; label.gameObject.SetActive(true); return label;
    }
    static void Style(Text text, int size, TextAnchor anchor)
    {
        text.font = font; text.fontSize = size; text.resizeTextForBestFit = false; text.color = Ink;
        text.alignment = anchor; text.lineSpacing = .96f; text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
    }
    static Button Button(Transform parent, string name, float x, float y, float w, float h, string label, int size)
    {
        var existing = parent.Find(name);
        var button = existing ? existing.GetComponent<Button>() : null;
        if (!button) { button = Object.Instantiate(buttonTemplate, parent); button.name = name; }
        Rect(button.transform, x, y, w, h); button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        var image = button.GetComponent<Image>(); image.sprite = stripPaper; image.color = Color.white; image.raycastTarget = true;
        button.targetGraphic = image;
        var text = ButtonLabel(button); Rect(text.transform, 12, 0, w - 24, h); Style(text, size, TextAnchor.MiddleCenter); text.text = label;
        button.gameObject.SetActive(true); return button;
    }
    static Text ButtonLabel(Button button)
    {
        var exact = button.transform.Find("Label");
        var text = exact ? exact.GetComponent<Text>() : button.GetComponentsInChildren<Text>(true).FirstOrDefault();
        if (!text) throw new InvalidOperationException("Existing paper button has no label.");
        return text;
    }
    static CanvasGroup Group(GameObject obj)
    {
        var group = obj.GetComponent<CanvasGroup>(); if (!group) group = obj.AddComponent<CanvasGroup>();
        group.alpha = 0; group.blocksRaycasts = group.interactable = false; return group;
    }
}
