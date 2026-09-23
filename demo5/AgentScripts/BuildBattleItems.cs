using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object = UnityEngine.Object;

// Battle items (UI11) and card-game aiming layer, added to the existing battle prefab without touching the UI10 layout.
// Safe to re-run: every element it owns is replaced by name. Run BuildBattleFeel first.
public static class BuildBattleItems
{
    const string P = "Assets/Prefabs/Settlement/", A = "Assets/Art/PartySelection/";
    static Font font;
    static readonly Color Ink = new Color(.045f, .065f, .06f), Cream = new Color(.95f, .92f, .82f), Ochre = new Color(1, .78f, .38f),
        Danger = new Color(.86f, .42f, .33f), Sage = new Color(.55f, .74f, .58f), Muted = new Color(.78f, .8f, .76f);

    static RectTransform Node(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
        r.anchorMin = min; r.anchorMax = max; r.pivot = pivot; r.anchoredPosition = position; r.sizeDelta = size; return r;
    }
    // Top-left placement in parent pixels (y grows downward), matching BuildFieldBattle.
    static RectTransform TL(string name, Transform parent, float x, float y, float w, float h) => Node(name, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h));
    // Stretch inside the parent by fractions, with a pixel inset.
    static RectTransform Fit(string name, Transform parent, float x0, float y0, float x1, float y1, float inset = 0)
    {
        var r = Node(name, parent, new Vector2(x0, y0), new Vector2(x1, y1), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset); return r;
    }
    static Text Label(RectTransform r, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, bool fit = false)
    {
        var t = r.gameObject.AddComponent<Text>(); t.font = font; t.fontSize = size; t.text = text; t.color = color; t.alignment = align; t.raycastTarget = false;
        if (fit) { t.resizeTextForBestFit = true; t.resizeTextMinSize = 12; t.resizeTextMaxSize = size; }
        return t;
    }
    static Image Picture(RectTransform r, string sprite, Color color, bool raycast = false)
    {
        var i = r.gameObject.AddComponent<Image>(); if (sprite != null) i.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(sprite); i.color = color; i.raycastTarget = raycast; return i;
    }
    static Image Filled(RectTransform r, string sprite, Color color) { var i = Picture(r, sprite, color); i.type = Image.Type.Filled; i.fillMethod = Image.FillMethod.Horizontal; i.fillAmount = 1; return i; }
    static T Graphic<T>(RectTransform r, Color color) where T : MaskableGraphic { var g = r.gameObject.AddComponent<T>(); g.color = color; g.raycastTarget = false; return g; }
    // The charcoal-teal paper panel used by the UI10 bottom areas.
    static void DarkPanel(Transform p)
    {
        Picture(Fit("Border", p, 0, 0, 1, 1), A + "card-paper.png", new Color(.34f, .38f, .33f));
        Picture(Fit("DarkPaper", p, 0, 0, 1, 1, 3), A + "teal-texture.png", new Color(.85f, .9f, .9f));
    }
    static Button PaperButton(string name, Transform parent, float x, float y, float w, float h, string text, int size, Color tint)
    {
        var im = Picture(TL(name, parent, x, y, w, h), A + "card-paper.png", tint, true); var b = im.gameObject.AddComponent<Button>(); b.targetGraphic = im;
        Label(Fit("Label", im.transform, 0, 0, 1, 1, 6), text, size, Ink, TextAnchor.MiddleCenter); return b;
    }
    static void Heading(Transform parent, string name, float x, float y, string text)
    {
        var paper = Picture(TL(name, parent, x, y, 280, 52), A + "footer-paper.png", Color.white);
        Label(TL("Label", paper.transform, 20, 2, 240, 48), text, 31, Ink);
    }
    static GameObject Save(GameObject root, string name) { var asset = PrefabUtility.SaveAsPrefabAsset(root, P + name + ".prefab"); Object.DestroyImmediate(root); return asset; }
    static void Remove(Transform parent, string name) { var old = parent.Find(name); if (old) Object.DestroyImmediate(old.gameObject); }
    static GameObject Place(GameObject asset, Transform parent, float x, float y)
    {
        var g = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent); var r = (RectTransform)g.transform; r.anchoredPosition = new Vector2(x, -y); return g;
    }

    static BattleTargetChip Chip(string name)
    {
        var root = Node(name, null, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(246, 132));
        var c = root.gameObject.AddComponent<BattleTargetChip>();
        c.Paper = Picture(Fit("Paper", root, 0, 0, 1, 1), A + "card-paper.png", Cream, true);
        c.Button = root.gameObject.AddComponent<Button>(); c.Button.targetGraphic = c.Paper;
        c.Portrait = Picture(Fit("Portrait", root, .03f, .08f, .38f, .92f), A + "portrait-scout.png", Color.white); c.Portrait.preserveAspect = true;
        c.Name = Label(Fit("Name", root, .41f, .56f, .97f, .94f), "윤서진", 22, Ink, TextAnchor.MiddleLeft, true);
        Picture(Fit("HealthBack", root, .41f, .40f, .95f, .52f), null, new Color(.03f, .05f, .05f, .9f));
        c.HealthFill = Filled(Fit("HealthFill", root, .41f, .40f, .95f, .52f, 2), A + "paper-texture.png", new Color(.44f, .67f, .45f));
        c.Health = Label(Fit("Health", root, .41f, .06f, .95f, .38f), "2 / 3", 22, Ink, TextAnchor.MiddleCenter, true);
        return c;
    }

    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop first");
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");

        // ---- small prefabs ----
        var slotRoot = Node("BattleItemSlot", null, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(128, 104));
        var slot = slotRoot.gameObject.AddComponent<BattleItemSlot>();
        slot.Border = Picture(Fit("Border", slotRoot, 0, 0, 1, 1), null, slot.BorderColor);
        slot.Fill = Picture(Fit("Fill", slotRoot, 0, 0, 1, 1, 3), null, slot.FillColor, true);
        slot.Button = slotRoot.gameObject.AddComponent<Button>(); slot.Button.targetGraphic = slot.Fill;
        slot.Icon = Picture(Fit("Icon", slotRoot, .16f, .14f, .84f, .86f), A + "icon-bag.png", Color.white); slot.Icon.preserveAspect = true;
        slot.Count = Label(Fit("Count", slotRoot, .46f, 0, .95f, .38f), "2", 24, Cream, TextAnchor.LowerRight);
        var lockMark = Graphic<BattleCrossGraphic>(Fit("Lock", slotRoot, .3f, .25f, .7f, .75f), new Color(.6f, .62f, .58f, .35f)); lockMark.Thickness = .08f; lockMark.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        slot.Lock = lockMark.gameObject;
        var slotAsset = Save(slotRoot.gameObject, "BattleItemSlot");

        var chipAsset = Save(Chip("BattleTargetChip").gameObject, "BattleTargetChip");

        var orderRoot = Node("BattleOrderCard", null, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(102, 128));
        var order = orderRoot.gameObject.AddComponent<BattleTurnCard>();
        order.Paper = Picture(Fit("Paper", orderRoot, 0, 0, 1, 1), A + "card-paper.png", Color.white);
        order.Portrait = Picture(Fit("Portrait", orderRoot, .12f, .32f, .88f, .94f), A + "portrait-scout.png", Color.white); order.Portrait.preserveAspect = true;
        order.Label = Label(Fit("Name", orderRoot, .04f, .04f, .96f, .3f), "윤서진", 20, Ink, TextAnchor.MiddleCenter, true);
        var layoutElement = orderRoot.gameObject.AddComponent<LayoutElement>(); layoutElement.minWidth = 48; layoutElement.preferredWidth = 102; layoutElement.preferredHeight = 128;
        var orderAsset = Save(orderRoot.gameObject, "BattleOrderCard");

        var crossRoot = Node("BattleTreatCross", null, new Vector2(0, 1), new Vector2(0, 1), new Vector2(.5f, .5f), Vector2.zero, new Vector2(70, 70));
        Graphic<BattleCrossGraphic>(crossRoot, Sage);
        var crossAsset = Save(crossRoot.gameObject, "BattleTreatCross");

        // ---- aim info card ----
        var tipRoot = Node("BattleAimTooltip", null, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(330, 238));
        var tip = tipRoot.gameObject.AddComponent<BattleAimTooltip>(); tip.Root = tipRoot; DarkPanel(tipRoot);
        tip.Title = Label(TL("Title", tipRoot, 16, 8, 206, 38), "감염자 1", 26, Cream, TextAnchor.MiddleLeft, true);
        var armor = Picture(TL("ArmorTag", tipRoot, 224, 12, 94, 30), A + "footer-paper.png", Muted);
        tip.Armor = Label(Fit("Label", armor.transform, 0, 0, 1, 1, 2), "방어력 0", 17, Ink, TextAnchor.MiddleCenter);
        tip.Health = Label(TL("Health", tipRoot, 16, 46, 298, 32), "체력  4 / 4  →  1", 21, Cream);
        Picture(TL("HealthBack", tipRoot, 16, 80, 298, 12), null, new Color(.02f, .03f, .03f, .92f));
        tip.HealthLost = Filled(TL("HealthLost", tipRoot, 18, 82, 294, 8), A + "paper-texture.png", Cream);
        tip.HealthAfter = Filled(TL("HealthAfter", tipRoot, 18, 82, 294, 8), A + "paper-texture.png", Danger);
        var attack = TL("Attack", tipRoot, 0, 98, 330, 140); tip.AttackBlock = attack.gameObject;
        tip.Chance = Label(TL("Chance", attack, 16, 0, 298, 52), "명중률  65%", 34, Ochre);
        tip.Factors = Label(TL("Factors", attack, 16, 50, 298, 26), "기본 75 · 대상 줄 -5 · 가로 -5", 17, Muted, TextAnchor.MiddleLeft, true);
        tip.Damage = Label(TL("Damage", attack, 16, 76, 298, 32), "피해 3  ·  급소 10% (4)", 21, Cream, TextAnchor.MiddleLeft, true);
        tip.Note = Label(TL("Note", attack, 16, 108, 298, 32), "처치 가능  ·  탄약 1 · 소음 +2", 20, Cream, TextAnchor.MiddleLeft, true);
        var intent = TL("Intent", tipRoot, 0, 98, 330, 40); tip.IntentBlock = intent.gameObject;
        tip.Intent = Label(TL("Label", intent, 16, 2, 298, 32), "다음 행동 · 전진", 20, Cream, TextAnchor.MiddleLeft, true);
        var tipAsset = Save(tipRoot.gameObject, "BattleAimTooltip");

        // ---- item drawer (UI11 right side) ----
        var drawerRoot = Node("BattleItemDrawer", null, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(578, 912));
        var drawer = drawerRoot.gameObject.AddComponent<BattleItemDrawer>(); DarkPanel(drawerRoot);
        Picture(Fit("InputGuard", drawerRoot, 0, 0, 1, 1), null, Color.clear, true);
        var titlePaper = Picture(TL("TitlePaper", drawerRoot, 12, 14, 430, 58), A + "footer-paper.png", Color.white);
        drawer.Title = Label(TL("Title", titlePaper.transform, 20, 2, 396, 54), "윤서진 · 전투 아이템", 27, Ink, TextAnchor.MiddleLeft, true);
        drawer.Slots = TL("Slots", drawerRoot, 21, 86, 536, 328);
        var grid = drawer.Slots.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(128, 104); grid.spacing = new Vector2(8, 8);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 4;
        drawer.SlotPrefab = slotAsset.GetComponent<BattleItemSlot>();
        var detail = Picture(TL("DetailPaper", drawerRoot, 21, 428, 536, 332), A + "card-paper.png", Cream).rectTransform;
        drawer.DetailIcon = Picture(TL("DetailIcon", detail, 18, 16, 92, 72), A + "icon-bag.png", Ink); drawer.DetailIcon.preserveAspect = true;
        drawer.DetailName = Label(TL("DetailName", detail, 124, 8, 396, 48), "붕대", 30, Ink);
        drawer.DetailDescription = Label(TL("DetailDescription", detail, 124, 54, 400, 58), "치료 · 체력 +2", 20, Ink, TextAnchor.UpperLeft, true);
        Picture(TL("Divider", detail, 18, 120, 500, 3), null, new Color(Ink.r, Ink.g, Ink.b, .55f));
        drawer.TargetTitle = Label(TL("TargetTitle", detail, 20, 130, 220, 40), "대상 선택", 25, Ink);
        drawer.TargetNote = Label(TL("TargetNote", detail, 280, 130, 238, 40), "사용 시 행동 소모", 19, Ink, TextAnchor.MiddleRight);
        drawer.Chips = TL("Chips", detail, 16, 180, 504, 136);
        drawer.ChipGrid = drawer.Chips.gameObject.AddComponent<GridLayoutGroup>(); drawer.ChipGrid.cellSize = new Vector2(246, 136); drawer.ChipGrid.spacing = new Vector2(12, 8);
        drawer.ChipGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; drawer.ChipGrid.constraintCount = 2;
        drawer.ChipPrefab = chipAsset.GetComponent<BattleTargetChip>();
        drawer.Message = Label(TL("Message", drawerRoot, 21, 764, 536, 30), "한해인 체력 2 → 3 · 붕대 -1", 19, Cream, TextAnchor.MiddleCenter, true);
        drawer.Use = PaperButton("Use", drawerRoot, 21, 802, 360, 92, "사용하기", 36, Sage);
        drawer.Cancel = PaperButton("Cancel", drawerRoot, 393, 802, 164, 92, "취소", 30, Cream);
        var drawerAsset = Save(drawerRoot.gameObject, "BattleItemDrawer");

        // ---- wire into the battle panel ----
        var root = PrefabUtility.LoadPrefabContents(P + "ExpeditionBattlePanel.prefab");
        try
        {
            var c = root.GetComponent<ExpeditionBattlePanel>(); var ws = root.transform.Find("Workspace");
            Check(c.HudLayer && c.Presentation, "Run BuildBattleFeel first");
            foreach (var n in new[] { "AimLayer", "ItemBackdrop", "BattleItemDrawer", "OrderHeading", "OrderPanel", "PreviewHeading", "PreviewPanel" }) Remove(ws, n);

            var aim = TL("AimLayer", ws, 0, 0, 1920, 1080); aim.SetSiblingIndex(c.HudLayer.GetSiblingIndex() + 1); c.AimLayer = aim;
            c.AimArrow = Graphic<BattleAimArrow>(TL("AimArrow", aim, 0, 0, 1920, 1080), c.AimReady); c.AimArrow.gameObject.SetActive(false);
            c.Reticle = Graphic<BattleReticle>(Node("Reticle", aim, new Vector2(0, 1), new Vector2(0, 1), new Vector2(.5f, .5f), Vector2.zero, new Vector2(140, 200)), c.AimReady); c.Reticle.gameObject.SetActive(false);
            c.AimTooltip = Place(tipAsset, aim, 0, 0).GetComponent<BattleAimTooltip>(); c.AimTooltip.gameObject.SetActive(false);

            var backdrop = Picture(TL("ItemBackdrop", ws, 1290, 143, 630, 595), A + "teal-texture.png", new Color(.55f, .62f, .62f)); backdrop.gameObject.SetActive(false);
            backdrop.transform.SetSiblingIndex(ws.Find("BottomBand").GetSiblingIndex() + 1);
            Heading(ws, "OrderHeading", 28, 752, "행동 순서"); Heading(ws, "PreviewHeading", 634, 752, "대상 미리보기");
            var orderPanel = TL("OrderPanel", ws, 28, 808, 582, 250); DarkPanel(orderPanel);
            c.OrderContent = TL("Cards", orderPanel, 16, 60, 550, 128);
            c.OrderContent.gameObject.AddComponent<RectMask2D>();
            var row = c.OrderContent.gameObject.AddComponent<HorizontalLayoutGroup>(); row.spacing = 8; row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true; row.childForceExpandWidth = row.childForceExpandHeight = false;
            c.OrderCardPrefab = orderAsset.GetComponent<BattleTurnCard>();
            var previewPanel = TL("PreviewPanel", ws, 634, 808, 582, 250); DarkPanel(previewPanel);
            BattleTargetChip Preview(string name, float x)
            {
                var chip = Place(chipAsset, previewPanel, x, 52).GetComponent<BattleTargetChip>(); chip.name = name;
                ((RectTransform)chip.transform).sizeDelta = new Vector2(218, 150); Object.DestroyImmediate(chip.Button); chip.Button = null; chip.Paper.raycastTarget = false; return chip;
            }
            c.PreviewBefore = Preview("Before", 16);
            var arrow = Graphic<BattleBlockArrowGraphic>(TL("Arrow", previewPanel, 242, 84, 98, 86), Sage);
            Graphic<BattleCrossGraphic>(TL("Cross", arrow.transform, 22, 25, 36, 36), Cream);
            c.PreviewAfter = Preview("After", 348);

            var drawerGo = Place(drawerAsset, ws, 1316, 150); c.Drawer = drawerGo.GetComponent<BattleItemDrawer>(); drawerGo.SetActive(false);
            drawerGo.transform.SetSiblingIndex(c.FxLayer.GetSiblingIndex());
            c.ShowDuringItems = new[] { backdrop.gameObject, ws.Find("OrderHeading").gameObject, orderPanel.gameObject, ws.Find("PreviewHeading").gameObject, previewPanel.gameObject };
            foreach (var g in c.ShowDuringItems) g.SetActive(false);
            c.HideDuringItems = new[] { "BattleUnitDetails", "BattleTargetDetails", "ActionHeading", "ActionTitle", "Melee", "Shoot", "Guard", "Items", "Retreat", "Execute", "TurnViewport", "Round", "NoiseGauge", "FeedbackPaper", "Feedback" }
                .Select(n => ws.Find(n)).Where(t => t).Select(t => t.gameObject).ToArray();
            Check(c.HideDuringItems.Length == 15, "Missing UI10 element: " + c.HideDuringItems.Length);

            c.Presentation.CrossPrefab = crossAsset.GetComponent<BattleCrossGraphic>();
            c.Presentation.Heal = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Battle/battle-heal.wav");
            var items = ws.Find("Items");
            items.Find("Description").GetComponent<Text>().text = "소지품 사용";
            PrefabUtility.SaveAsPrefabAsset(root, P + "ExpeditionBattlePanel.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        return "UI11 drawer, slot/target/order/tooltip/cross prefabs, aim layer and item-mode panels added.";
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
}
