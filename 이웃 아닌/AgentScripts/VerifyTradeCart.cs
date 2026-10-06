using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Disposable campaign, real UI raycasts, isolated save slots. No production saves or scene assets.
// Run in Play Mode after the item catalog and trade-cart prefab builder. Finish after stopping.
public static class VerifyTradeCart
{
    static string TestDirectory => Path.GetFullPath("Temp/TradeCartVerification");
    static SettlementController C => Object.FindAnyObjectByType<SettlementController>();
    static SettlementVisitorPanel V => C.VisitorPanel;
    static readonly List<string> checks = new List<string>();
    static readonly List<string> shots = new List<string>();
    static readonly string[] NewIds = { "fuel", "lubricant", "battery", "tobacco", "coffee", "alcohol", "electrical-parts", "weapon-parts" };
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static int Stock(string id) => C.CraftPanel.Materials.Single(m => m.Id == id).Initial;
    static void SetStock(string id, int value) => C.CraftPanel.Materials.Single(m => m.Id == id).Initial = value;
    static string ItemName(string id) => C.CraftPanel.Materials.Single(m => m.Id == id).Name;
    static TradeCartRow[] Rows(bool ours) => (ours ? V.GiveRows : V.TakeRows).Where(r => r && r.gameObject.activeSelf).ToArray();
    static string Cart() => string.Join("|", V.Goods.Select(g => g.Id + ":" + V.CartCount(true, g.Id) + "/" + V.CartCount(false, g.Id)));
    static string Inventory() => string.Join("|", C.CraftPanel.Materials.OrderBy(m => m.Id).Select(m => m.Id + ":" + m.Initial)) + ";visitor:" +
        string.Join("|", V.Goods.OrderBy(g => g.Id).Select(g => g.Id + ":" + V.StockFor(g.Id)));

    public static async Task<string> Run()
    {
        Check(Application.isPlaying, "Enter Play Mode first."); checks.Clear(); shots.Clear(); string failure = null;
        try
        {
            await Start();
            await Tap(V.StartTrade);
            Check(V.Goods.Length == 22 && NewIds.All(id => V.Goods.Any(g => g.Id == id)), "Everyday trade catalog missing.");
            Check(V.CartValue(true) == 0 && V.CartValue(false) == 0 && Rows(true).Length == 0 && Rows(false).Length == 0 && !V.Offer.interactable,
                "Opening trade creates an unsolicited offer.");
            Check(V.GiveEmpty.activeInHierarchy && V.TakeEmpty.activeInHierarchy, "Empty cart instructions missing.");
            string untouched = Inventory();
            await Add(true, "tobacco"); await Add(true, "coffee");
            await Add(false, "fuel"); await Add(false, "battery");
            Check(V.CartCount(true, "tobacco") == 1 && V.CartCount(true, "coffee") == 1 && V.CartCount(false, "fuel") == 1 && V.CartCount(false, "battery") == 1,
                "Item clicks did not add one unit per selected item.");
            Check(V.CartValue(true) == V.BuyValueFor("tobacco") + V.BuyValueFor("coffee") && V.CartValue(false) == V.SellValueFor("fuel") + V.SellValueFor("battery"),
                "Mixed cart values do not use the current buy/sell prices.");
            Check(Inventory() == untouched, "Adding cart items consumed physical stock.");
            string cart = Cart(); await InspectAllPages();
            Check(Cart() == cart && Inventory() == untouched, "Paging changed cart or physical stock.");
            await Tap(Row(true, "tobacco").Plus); Check(V.CartCount(true, "tobacco") == 2, "Cart + failed.");
            await Tap(Row(true, "tobacco").Minus); Check(V.CartCount(true, "tobacco") == 1, "Cart - failed.");
            await Tap(V.Offer); Check(V.Review.activeSelf, "Mixed quote review missing.");
            foreach (string id in new[] { "tobacco", "coffee", "fuel", "battery" }) Check(V.ConfirmBody.text.Contains(ItemName(id)), "Confirmation omits " + id);
            await Tap(V.Cancel); Check(Inventory() == untouched && Cart() == cart, "Cancel consumed stock or discarded the cart.");
            await Tap(V.Offer);
            int tobacco = Stock("tobacco"), coffee = Stock("coffee"), fuel = Stock("fuel"), battery = Stock("battery");
            int mt = V.StockFor("tobacco"), mc = V.StockFor("coffee"), mf = V.StockFor("fuel"), mb = V.StockFor("battery");
            await Tap(V.Confirm); string once = Inventory(); V.Commit();
            Check(Stock("tobacco") == tobacco - 1 && Stock("coffee") == coffee - 1 && Stock("fuel") == fuel + 1 && Stock("battery") == battery + 1 &&
                V.StockFor("tobacco") == mt + 1 && V.StockFor("coffee") == mc + 1 && V.StockFor("fuel") == mf - 1 && V.StockFor("battery") == mb - 1,
                "Mixed quote did not exchange every line atomically.");
            Check(Inventory() == once, "Duplicate confirmation consumed stock twice.");
            Check(V.CartValue(true) == 0 && V.CartValue(false) == 0, "Completed quote remains ready to submit again.");
            checks.Add("Actual item/page/cart +/-/review/cancel/confirm raycasts: two goods on each side, exact prices, no changes before confirmation, atomic commit once, page-preserved cart.");

            await CheckInputLimits();
            await CheckStaleStates();
            await CheckSave();
            await CheckGenericReview();
            await PreviewSizes();
            await CheckEmptyStocks();
            Seed(); await PopulatePreview();
        }
        catch (Exception error) { failure = error.ToString(); }
        string output = Path.GetFullPath("아트/리소스검토/trade-cart-runtime.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output, Newtonsoft.Json.JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), result = failure == null ? "PASS" : "FAIL", checks, screenshots = shots, failure }, Newtonsoft.Json.Formatting.Indented));
        Check(failure == null, output + "\n" + failure); return "PASS\n" + string.Join("\n", checks) + "\n" + output;
    }

    static async Task Start()
    {
        CampaignSaveStore.TestDirectory = TestDirectory; PartySelectionSession.Clear(); SceneManager.LoadScene("PartySelection"); await Task.Delay(700);
        var selection = Object.FindAnyObjectByType<PartySelectionController>(); Check(selection, "Party selection missing.");
        foreach (var card in selection.Cards.Where(c => c.gameObject.activeInHierarchy && c.Button.IsInteractable()))
        { if (PartySelectionSession.Selected.Count >= 2) break; await Tap(card.Button); }
        await Tap(selection.Continue); await Task.Delay(650);
        var home = Object.FindAnyObjectByType<HomeSelectionController>(); Check(home, "Home selection missing.");
        await Tap(home.Cards[0].Button); await Tap(home.Continue); await Task.Delay(900);
        C.Introduction.Restore(10); C.Development.State.Warehouse = 2;
        C.Campaign.AdvanceSettlementTime(Math.Max(0, V.ArrivalMinute - C.Campaign.MinuteOfDay)); V.Sync();
        Check(V.IsPresent, "First visitor has not arrived."); Seed(); V.Open(); await Task.Delay(100);
    }

    static void Seed()
    {
        if (V.Review.activeSelf) V.CancelReview();
        foreach (var material in C.CraftPanel.Materials) material.Initial = 0;
        foreach (var good in V.Goods) SetStock(good.Id, 3);
        ((IList<SettlementCraftPanel.Order>)C.CraftPanel.Orders).Clear();
        C.Development.State.Warehouse = 2;
        var saved = V.Export(); foreach (var count in saved.Stock) count.Count = 3;
        V.Restore(saved); V.ClearOffer(); V.Sync();
    }

    static async Task CheckInputLimits()
    {
        Seed(); string before = Inventory();
        await Add(false, "fuel");
        Check(!V.Offer.interactable && V.CartValue(true) == 0, "Receive-only cart is free.");
        await Add(true, "wood");
        Check(!V.Offer.interactable && V.CartValue(true) < V.CartValue(false), "Insufficient offer is enabled.");
        Check(!string.IsNullOrWhiteSpace(V.TradeHint.text), "Value shortage has no explanation.");
        int fuelTake = V.CartCount(false, "fuel"); await Add(true, "fuel");
        Check(V.CartCount(true, "fuel") == 0 && V.CartCount(false, "fuel") == fuelTake, "Same good permitted on both sides.");
        V.ChangeCartQuantity(true, "wood", 999);
        Check(V.CartCount(true, "wood") == C.CraftPanel.Available("wood"), "Give quantity exceeds available stock.");
        Check(!Row(true, "wood").Plus.interactable, "Give + remains enabled at available stock.");
        V.ChangeCartQuantity(false, "fuel", 999);
        Check(V.CartCount(false, "fuel") == V.StockFor("fuel") && !Row(false, "fuel").Plus.interactable, "Take quantity exceeds remaining stock.");
        V.ChangeCartQuantity(true, "wood", -999); Check(V.CartCount(true, "wood") == 0 && !Rows(true).Any(r => r.ItemId == "wood"), "Zero quantity row not removed.");
        Check(Inventory() == before, "Cart quantity edits changed physical stock.");
        await Tap(V.ClearTrade); Check(V.CartValue(true) == 0 && V.CartValue(false) == 0 && !V.Offer.interactable, "Clear button leaves selected quantities.");
        // Fixture quantity stress only: verify API clamp even if a future warehouse holds 100+.
        SetStock("wood", 150); V.Sync(); V.ChangeCartQuantity(true, "wood", 999);
        Check(V.CartCount(true, "wood") == 99, "Per-kind 99 limit bypassed.");
        V.ClearOffer(); Seed();
        checks.Add("Empty/one-sided/underfunded offers blocked; same good cannot appear on both sides; actual stock and 99 limits clamp; clear/removal move no stock.");
    }

    static async Task PrepareSmallQuote(string give = "alcohol", string take = "wood", int count = 1)
    {
        V.ClearOffer(); await Add(true, give); for (int i = 1; i < count; i++) await Tap(Row(true, give).Plus);
        await Add(false, take); Check(V.Offer.interactable, "Fixture quote unexpectedly blocked: " + V.TradeHint.text);
        await Tap(V.Offer); Check(V.Review.activeSelf, "Expected quote review.");
    }

    static async Task CheckStaleStates()
    {
        Seed(); await PrepareSmallQuote("alcohol", "fuel");
        var emptyFuel = V.Export(); emptyFuel.Stock.Single(s => s.Id == "fuel").Count = 0; V.Restore(emptyFuel);
        string before = Inventory(); await Tap(V.Confirm); Check(Inventory() == before, "Sold-out stale quote consumed goods.");
        await InspectAllPages(); Check(!(await VisibleIds(false)).Contains("fuel"), "Sold-out goods remain in the visitor catalog.");

        Seed(); await PrepareSmallQuote(); var good = V.Goods.Single(g => g.Id == "alcohol"); int oldPrice = good.Value;
        try { good.Value++; before = Inventory(); await Tap(V.Confirm); Check(Inventory() == before, "Quote committed after unit price changed."); }
        finally { good.Value = oldPrice; }

        Seed(); await PrepareSmallQuote("alcohol", "wood", 2);
        var recipe = C.CraftPanel.Recipes.Single(r => r.Id == "bandage-alcohol");
        var reservation = new SettlementCraftPanel.Order { Recipe = recipe, Member = C.Campaign.Party.Last(), Quantity = 2, Minutes = 20,
            Reserved = recipe.Costs.ToDictionary(c => c.MaterialId, c => c.Count * 2) };
        var orders = (IList<SettlementCraftPanel.Order>)C.CraftPanel.Orders; orders.Add(reservation);
        try { before = Inventory(); await Tap(V.Confirm); Check(Inventory() == before, "Quote spent newly reserved crafting materials."); }
        finally { orders.Remove(reservation); }

        Seed(); SetStock("wood", Stock("wood") + C.Development.Capacity - C.Development.StockUsed); V.Sync();
        Check(C.Development.StockUsed == C.Development.Capacity, "Capacity fixture not full.");
        await Add(true, "fuel"); await Add(false, "wood"); await Tap(Row(false, "wood").Plus);
        Check(!V.Offer.interactable, "Mixed quantity gain bypasses warehouse capacity.");
        SetStock("wood", Stock("wood") - 1); V.Sync(); Check(V.Offer.interactable, "One free warehouse space not recognized.");
        await Tap(V.Offer); SetStock("wood", Stock("wood") + 1); before = Inventory(); await Tap(V.Confirm);
        Check(Inventory() == before, "Stale quote exceeded warehouse capacity.");

        Seed(); await PrepareSmallQuote(); var expired = V.Export(); expired.Dismissed = true; V.Restore(expired);
        before = Inventory(); await Tap(V.Confirm); Check(Inventory() == before, "Ended visit committed a stale quote.");
        expired.Dismissed = false; V.Restore(expired); V.Sync();
        Seed();
        checks.Add("Quote rejects changed price, depleted visitor stock, newly reserved recipe inputs, full warehouse including late capacity change, and ended visit without partial stock changes. Reservation/capacity changes are explicit isolated fixtures.");
    }

    static async Task CheckSave()
    {
        Seed(); await PrepareSmallQuote("alcohol", "wood"); await Tap(V.Confirm);
        var save = CampaignPersistence.Capture(C); string visitor = JsonUtility.ToJson(save.Visitor);
        Check(CampaignSaveStore.Write(0, save, C, out var error), error);
        var loaded = CampaignSaveStore.Read(0, C); Check(loaded.CanLoad, "Trade save failed: " + loaded.Error);
        Check(JsonUtility.ToJson(loaded.Data.Visitor) == visitor, "Disk roundtrip changed merchant stock.");
        Check(string.Join("|", loaded.Data.Materials.OrderBy(m => m.Id).Select(m => m.Id + ":" + m.Count)) ==
            string.Join("|", save.Materials.OrderBy(m => m.Id).Select(m => m.Id + ":" + m.Count)), "Disk roundtrip changed warehouse quantities.");
        string preferred = V.PreferredGoodId; await Tap(V.TradeBack); await Tap(V.Back);
        V.Restore(loaded.Data.Visitor); V.Sync(); V.Open(); await Tap(V.StartTrade);
        Check(V.PreferredGoodId == preferred && JsonUtility.ToJson(V.Export()) == visitor, "Reopening/restoring rerolled demand or restocked merchant.");
        Check(V.CartValue(true) == 0 && V.CartValue(false) == 0, "Reopening restored an unconfirmed offer.");
        checks.Add("Committed mixed-cart-compatible inventory/22 stock records survive isolated current-version save and reopen; demand remains stable; unconfirmed cart is not persisted.");
    }

    static async Task CheckEmptyStocks()
    {
        Seed(); V.ClearOffer(); foreach (var material in C.CraftPanel.Materials) material.Initial = 0;
        var empty = V.Export(); foreach (var count in empty.Stock) count.Count = 0; V.Restore(empty); V.Sync();
        Check(V.OurSlots.All(s => !s.gameObject.activeSelf) && V.TheirSlots.All(s => !s.gameObject.activeSelf) && !V.Offer.interactable, "Empty stocks remain tradable.");
        Check(V.OurPage == 0 && V.TheirPage == 0 && !V.OurNextPage.interactable && !V.OurPreviousPage.interactable && !V.TheirNextPage.interactable && !V.TheirPreviousPage.interactable,
            "Empty page controls not repaired.");
        Check(Rows(true).Length == 0 && Rows(false).Length == 0 && V.GiveEmpty.activeInHierarchy && V.TakeEmpty.activeInHierarchy,
            "Empty cart explanatory state missing.");
        checks.Add("Zero stock hides every catalog item, resets pagination and retains clear empty-cart instructions with confirmation disabled."); await Task.Delay(50);
    }

    static async Task CheckGenericReview()
    {
        await Tap(V.TradeBack); await Tap(V.Dismiss);
        Check(V.Review.activeInHierarchy && V.ConfirmBody.gameObject.activeInHierarchy && !V.CartReview.activeSelf,
            "Generic dismissal confirmation failed to restore its text after a cart review.");
        Fits(V.ConfirmBody); Hit(V.Confirm); Hit(V.Cancel); string before = Inventory();
        await Tap(V.Cancel);
        Check(!V.Review.activeSelf && V.IsPresent && !V.Export().Dismissed && Inventory() == before, "Cancel dismissed the visitor or changed stock.");
        await Tap(V.StartTrade);
        checks.Add("Generic dismissal review restores its text and hides cart rows after trade review; cancel keeps visitor and inventory unchanged.");
    }

    static async Task Add(bool ours, string id)
    {
        var previous = ours ? V.OurPreviousPage : V.TheirPreviousPage; while (previous.interactable) await Tap(previous);
        string name = ItemName(id);
        for (int page = 0; page < 30; page++)
        {
            var slot = (ours ? V.OurSlots : V.TheirSlots).FirstOrDefault(s => s.gameObject.activeInHierarchy && s.Label.text == name);
            if (slot) { await Tap(slot.Button); return; }
            var next = ours ? V.OurNextPage : V.TheirNextPage; if (!next.interactable) break; await Tap(next);
        }
        throw new InvalidOperationException("Cannot reach catalog item " + id + " / ours=" + ours);
    }
    static TradeCartRow Row(bool ours, string id) => Rows(ours).Single(r => r.ItemId == id);

    static async Task<HashSet<string>> VisibleIds(bool ours)
    {
        var ids = new HashSet<string>(); var previous = ours ? V.OurPreviousPage : V.TheirPreviousPage;
        while (previous.interactable) await Tap(previous);
        for (int page = 0; page < 30; page++)
        {
            foreach (var slot in (ours ? V.OurSlots : V.TheirSlots).Where(s => s.gameObject.activeInHierarchy))
            {
                string id = C.CraftPanel.Materials.Single(m => m.Name == slot.Label.text).Id;
                Check((ours ? C.CraftPanel.Available(id) : V.StockFor(id)) > 0, "Zero stock catalog item: " + id);
                Check(ids.Add(id), "Duplicate catalog item across pages: " + id); Fits(slot.Label); Hit(slot.Button);
            }
            var next = ours ? V.OurNextPage : V.TheirNextPage; if (!next.interactable) break; await Tap(next);
        }
        return ids;
    }
    static async Task InspectAllPages()
    {
        foreach (bool ours in new[] { true, false })
        {
            string cart = Cart(); var expected = V.Goods.Where(g => (ours ? C.CraftPanel.Available(g.Id) : V.StockFor(g.Id)) > 0).Select(g => g.Id);
            Check((await VisibleIds(ours)).SetEquals(expected), "Catalog omits available goods.");
            Check(Cart() == cart, "Browsing stock pages changes cart selections.");
        }
    }

    static async Task PopulatePreview()
    {
        V.ClearOffer(); await Add(true, "tobacco"); await Tap(Row(true, "tobacco").Plus); await Add(true, "coffee");
        await Add(false, "fuel"); await Add(false, "battery"); await Add(false, "alcohol");
        Check(V.Offer.interactable, "Preview offer unexpectedly invalid: " + V.TradeHint.text);
        V.GiveCartScroll.verticalNormalizedPosition = V.TakeCartScroll.verticalNormalizedPosition = 1; Canvas.ForceUpdateCanvases();
    }

    static async Task InspectLargeCart(bool ours)
    {
        V.ClearOffer(); foreach (var good in V.Goods) V.ChangeCartQuantity(ours, good.Id, 1); Canvas.ForceUpdateCanvases(); await Task.Delay(80);
        var rows = Rows(ours); var scroll = ours ? V.GiveCartScroll : V.TakeCartScroll;
        Check(rows.Length == V.Goods.Length && rows.Select(r => r.ItemId).Distinct().Count() == V.Goods.Length, "Cart cannot show all 22 distinct goods.");
        Check(scroll && scroll.content && scroll.viewport && scroll.content.rect.height > scroll.viewport.rect.height, "Long cart has no scrollable overflow.");
        Check(scroll.verticalScrollbar && scroll.verticalScrollbar.gameObject.activeInHierarchy, "Long cart scrollbar is hidden.");
        foreach (var row in rows)
        {
            Check(row.Ours == ours && row.Icon.sprite && row.NameLabel.text.Contains(ItemName(row.ItemId)), "Cart row identity or icon mismatch.");
            foreach (var text in new[] { row.NameLabel, row.CountLabel, row.ValueLabel }) { Fits(text); Inside(text.rectTransform, (RectTransform)row.transform); }
            Inside(row.Icon.rectTransform, (RectTransform)row.transform);
            Inside((RectTransform)row.Minus.transform, (RectTransform)row.transform); Inside((RectTransform)row.Plus.transform, (RectTransform)row.transform);
        }
        Check(V.CartValue(ours) == V.Goods.Sum(g => ours ? V.BuyValueFor(g.Id) : V.SellValueFor(g.Id)), "22-line cart total wrong.");
        scroll.StopMovement(); scroll.verticalNormalizedPosition = 1; Canvas.ForceUpdateCanvases(); await Task.Delay(80);
        Hit(rows[0].Plus);
        // The actual ScrollRect receives wheel events at its viewport, then the last row is clicked.
        var canvas = scroll.GetComponentInParent<Canvas>(); var viewport = scroll.viewport;
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, viewport.TransformPoint(viewport.rect.center)), scrollDelta = new Vector2(0, -8) };
        for (int i = 0; i < 30 && scroll.verticalNormalizedPosition > .001f; i++) { ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.scrollHandler); await Task.Delay(10); }
        scroll.StopMovement(); Canvas.ForceUpdateCanvases(); Check(scroll.verticalNormalizedPosition < .01f, "Mouse wheel cannot reach the last cart line.");
        string last = rows.Last().ItemId; await Tap(Row(ours, last).Plus); Check(V.CartCount(ours, last) == 2, "Last scrolled row + is not reachable.");
        await Tap(Row(ours, last).Minus); Check(V.CartCount(ours, last) == 1, "Last scrolled row - is not reachable.");
        V.ClearOffer();
    }

    static void Fits(Text text)
    {
        Check(text, "Required trade text reference missing.");
        Check(text.preferredHeight <= text.rectTransform.rect.height + 2, "Trade text overflow: " + text.name + " / " + text.text + " / " + text.preferredHeight + " > " + text.rectTransform.rect.height);
    }
    static void Inside(RectTransform child, RectTransform parent)
    {
        var corners = new Vector3[4]; child.GetWorldCorners(corners); Rect limits = parent.rect;
        foreach (var corner in corners) { Vector3 point = parent.InverseTransformPoint(corner); Check(point.x >= limits.xMin - 1 && point.x <= limits.xMax + 1 && point.y >= limits.yMin - 1 && point.y <= limits.yMax + 1,
            "Cart row content exceeds its paper: " + parent.name + " / " + child.name); }
    }
    static PointerEventData Hit(Button button)
    {
        Canvas.ForceUpdateCanvases(); Check(button && button.IsActive() && button.IsInteractable(), "Unavailable button: " + button?.name);
        var canvas = button.GetComponentInParent<Canvas>(); var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, "Trade hit blocked: " + button.name + " / " + (hits.Count > 0 ? hits[0].gameObject.name : "none")); return pointer;
    }
    static async Task Tap(Button button) { await Task.Delay(35); ExecuteEvents.Execute(button.gameObject, Hit(button), ExecuteEvents.pointerClickHandler); await Task.Delay(35); }

    static void InspectReview()
    {
        Check(V.Review.activeInHierarchy && V.CartReview.activeInHierarchy && !V.ConfirmBody.gameObject.activeSelf,
            "Trade confirmation did not show the icon-row review and hide the fallback text.");
        foreach (bool ours in new[] { true, false })
        {
            var rows = (ours ? V.ReviewGiveRows : V.ReviewTakeRows).Where(r => r && r.gameObject.activeSelf).ToArray();
            var expected = V.Goods.Where(g => V.CartCount(ours, g.Id) > 0).Select(g => g.Id).ToArray();
            Check(rows.Select(r => r.ItemId).SequenceEqual(expected), "Review lines do not match the frozen cart order.");
            foreach (var row in rows)
            {
                int count = V.CartCount(ours, row.ItemId), unit = ours ? V.BuyValueFor(row.ItemId) : V.SellValueFor(row.ItemId);
                Check(row.Ours == ours && row.Icon.sprite && row.NameLabel.text == ItemName(row.ItemId) && row.CountLabel.text == count + "개" && row.ValueLabel.text == "가치 " + (count * unit),
                    "Review identity, quantity or subtotal mismatch: " + row.ItemId);
                foreach (var text in new[] { row.NameLabel, row.CountLabel, row.ValueLabel }) { Fits(text); Inside(text.rectTransform, (RectTransform)row.transform); }
                Inside(row.Icon.rectTransform, (RectTransform)row.transform);
                Check(!row.Minus || !row.Minus.gameObject.activeInHierarchy || !row.Minus.interactable, "Read-only review minus remains interactive.");
                Check(!row.Plus || !row.Plus.gameObject.activeInHierarchy || !row.Plus.interactable, "Read-only review plus remains interactive.");
            }
            var total = ours ? V.ReviewGiveTotal : V.ReviewTakeTotal;
            Check(total.text.EndsWith(V.CartValue(ours).ToString(), StringComparison.Ordinal), "Review total mismatch."); Fits(total);
        }
        Fits(V.ReviewSummary); Hit(V.Confirm); Hit(V.Cancel);
    }

    static async Task Capture(string name)
    {
        string native = Path.GetFullPath("Temp/" + name + ".png"); if (File.Exists(native)) File.Delete(native);
        ScreenCapture.CaptureScreenshot(native); for (int i = 0; i < 40 && !File.Exists(native); i++) await Task.Delay(100);
        Check(File.Exists(native), "Capture missing: " + name); await Task.Delay(150);
        string output = Path.GetFullPath("아트/리소스검토/" + name + ".png"); Directory.CreateDirectory(Path.GetDirectoryName(output)); File.Copy(native, output, true); shots.Add(output);
    }

    static async Task PreviewSizes()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        var assembly = typeof(UnityEditor.Editor).Assembly; var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var group = sizesType.GetMethod("GetGroup", flags).Invoke(sizes, new[] { sizesType.GetProperty("currentGroupType", flags).GetValue(sizes) });
        var view = UnityEditor.EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView")); var selected = view.GetType().GetProperty("selectedSizeIndex", flags); int original = (int)selected.GetValue(view);
        var type = assembly.GetType("UnityEditor.GameViewSize");
        try
        {
            foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 800) })
            {
                int count = (int)group.GetType().GetMethod("GetTotalCount", flags).Invoke(group, null), index = -1;
                for (int i = 0; i < count; i++) { var candidate = group.GetType().GetMethod("GetGameViewSize", flags).Invoke(group, new object[] { i }); if ((int)type.GetProperty("width", flags).GetValue(candidate) == size.x && (int)type.GetProperty("height", flags).GetValue(candidate) == size.y) { index = i; break; } }
                Check(index >= 0, "Add fixed GameView size before review: " + size); selected.SetValue(view, index); view.Repaint(); await Task.Delay(500);
                Check(Screen.width == size.x && Screen.height == size.y, "GameView size did not apply.");
                Seed(); await InspectAllPages(); await InspectLargeCart(true); await InspectLargeCart(false);
                await Add(false, "fuel"); Check(!V.Offer.interactable, "Receive-only shortage became tradable."); Fits(V.Balance); Fits(V.OfferLabel);
                await PopulatePreview();
                foreach (var text in new[] { V.Status, V.GiveTotal, V.TakeTotal, V.TradeHint, V.Balance, V.OfferLabel, V.OurPageLabel, V.TheirPageLabel }) Fits(text);
                foreach (var row in Rows(true).Concat(Rows(false))) { foreach (var text in new[] { row.NameLabel, row.CountLabel, row.ValueLabel }) Fits(text); Hit(row.Minus); Hit(row.Plus); }
                Hit(V.Offer); Hit(V.ClearTrade); Hit(V.TradeBack);
                await Capture("trade-cart-" + size.x + "x" + size.y);
                string before = Inventory(), cart = Cart(); await Tap(V.Offer); InspectReview();
                V.ChangeCartQuantity(true, "tobacco", 1); Check(Cart() == cart, "Cart changes while its frozen confirmation is open.");
                await Capture("trade-cart-review-" + size.x + "x" + size.y); await Tap(V.Cancel);
                Check(Inventory() == before && Cart() == cart, "Review screenshot/cancel changed goods or cart.");
            }
        }
        finally { selected.SetValue(view, original); view.Repaint(); await Task.Delay(100); }
        checks.Add("Native 1920×1080 and 1280×800: all catalog pages, 22-kind cart per side, visible scrollbar, wheel-to-last-row +/- hits, row bounds/text, shortage and balanced notices; icon-row confirmation counts/totals/bounds and confirm/cancel hits. Four original screenshots saved.");
    }

    public static string Finish()
    {
        Check(!Application.isPlaying, "Stop Play Mode first."); if (CampaignSaveStore.TestDirectory == TestDirectory) CampaignSaveStore.TestDirectory = null; PartySelectionSession.Clear();
        return "Trade cart verification override and temporary party selection cleared.";
    }
}
