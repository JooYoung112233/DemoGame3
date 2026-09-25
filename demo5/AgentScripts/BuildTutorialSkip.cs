using System;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Apply after BuildTutorialNarrative / BuildSystemMenus. Existing paper and fonts only.
// Layout remains authored in reusable prefabs; runtime only controls availability.
public static class BuildTutorialSkip
{
    const string Folder = "Assets/Prefabs/Settlement/";

    static void Place(Transform value, float x, float y, float width, float height)
    {
        var rect = (RectTransform)value;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    static Button EnsureButton(Transform parent, string name, Button source, string label,
        float x, float y, float width, float height, int fontSize)
    {
        var existing = parent.Find(name);
        var button = existing ? existing.GetComponent<Button>() : null;
        if (!button)
        {
            if (existing) throw new InvalidOperationException(name + " exists without a Button.");
            var clone = Object.Instantiate(source.gameObject, parent, false);
            clone.name = name;
            button = clone.GetComponent<Button>();
            // The runtime owner connects the action, never a copied persistent click handler.
            button.onClick = new Button.ButtonClickedEvent();
        }
        button.gameObject.SetActive(true);
        button.interactable = true;
        Place(button.transform, x, y, width, height);
        var text = button.transform.Find("Label").GetComponent<Text>();
        Place(text.transform, 16, 0, width - 32, height);
        text.text = label;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.resizeTextForBestFit = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return button;
    }

    static Button Narrative(Transform view)
    {
        var next = view.Find("Next").GetComponent<Button>();
        var skip = EnsureButton(view, "SkipTutorial", next, "튜토리얼 건너뛰기", 1500, 28, 340, 64, 26);
        var skipRect=(RectTransform)skip.transform;
        skipRect.anchorMin=skipRect.anchorMax=skipRect.pivot=new Vector2(1,1);
        skipRect.anchoredPosition=new Vector2(-80,-28);
        var hint = view.Find("ReadingHint").GetComponent<Text>();
        Place(hint.transform, 80, 986, 1250, 52);
        hint.alignment = TextAnchor.MiddleLeft;
        hint.fontSize = 24;
        hint.resizeTextForBestFit = false;
        hint.raycastTarget = false;
        var oldHint=view.Find("SkipHint");
        var skipHint=oldHint?oldHint.GetComponent<Text>():Object.Instantiate(hint,view,false);
        skipHint.name="SkipHint";
        Place(skipHint.transform,1290,100,550,40);
        skipHint.rectTransform.anchorMin=skipHint.rectTransform.anchorMax=skipHint.rectTransform.pivot=new Vector2(1,1);
        skipHint.rectTransform.anchoredPosition=new Vector2(-80,-100);
        skipHint.text="남은 도입 준비·시간 자동 처리";
        skipHint.fontSize=23;skipHint.alignment=TextAnchor.MiddleRight;skipHint.raycastTarget=false;
        return skip;
    }

    static void Menu(SettlementGameMenu menu)
    {
        var paper = menu.Page.transform.Find("Paper");
        var old=paper.Find("SkipTutorial");if(old)old.gameObject.SetActive(false);
        var buttons = new[] { menu.Save, menu.Load, menu.Settings, menu.Title };
        for (int i = 0; i < buttons.Length; i++)
        {
            Place(buttons[i].transform, 85, 130 + i * 140, 730, 90);
            Place(buttons[i].transform.Find("Label"), 20, 0, 690, 90);
        }
    }

    static void MarkNested(GameObject root)
    {
        foreach (var component in root.GetComponentsInChildren<Component>(true))
            if (component && PrefabUtility.IsPartOfPrefabInstance(component))
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
    }

    static void Edit(string name, Action<GameObject> apply)
    {
        string path = Folder + name + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            apply(root);
            MarkNested(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static string Run()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Stop Play Mode first.");

        Edit("TutorialNarrativeView", root => Narrative(root.transform));
        Edit("SystemMenu", root => Menu(root.GetComponent<SettlementGameMenu>()));
        Edit("SettlementScreen", root =>
        {
            var controller = root.GetComponent<SettlementController>();
            var narrative = controller.Narrative;
            if (!narrative || !narrative.View || !controller.GameMenu)
                throw new InvalidOperationException("Build the tutorial narrative and system menu first.");
            narrative.Skip = Narrative(narrative.View.transform);
            Menu(controller.GameMenu);
        });
        AssetDatabase.SaveAssets();
        return "PASS: first-introduction skip placed top-right; later dialogues hide it; menu entry hidden and four remaining actions reflowed.";
    }
}
