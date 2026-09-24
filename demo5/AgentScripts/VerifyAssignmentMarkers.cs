using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Place = Demo5.FrontEnd.SettlementAssignmentMarkers.Place;

// Assignment markers 1차: the shared bubble prefab and the settlement facility markers (temporary layout, no approved UI mock).
// Wiring(): edit or play mode, after BuildAssignmentBubble.Run. GlyphSheet() / Settlement(): play mode after VerifyFieldTurnPlan.Enter.
// Stills go to Temp/AssignmentMarkersCapture.
public static class VerifyAssignmentMarkers
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    public static string Wiring()
    {
        var bubble = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/AssignmentBubble.prefab")?.GetComponent<AssignmentBubble>();
        Check(bubble, "AssignmentBubble.prefab missing: run BuildAssignmentBubble.Run");
        Check(bubble.Visual && bubble.Ring && bubble.Badge && bubble.BadgeFill && bubble.Glyph && bubble.LabelRoot && bubble.Label && bubble.LabelAccent, "Bubble references");
        Check(bubble.Portraits != null && bubble.Portraits.Length == 3 && bubble.Portraits.All(p => p && p.GetComponent<PaperPortraitStyle>() && p.preserveAspect), "Three styled portraits");
        Check(bubble.Portraits[0].transform.GetSiblingIndex() > bubble.Portraits[1].transform.GetSiblingIndex(), "Lead portrait drawn on top");
        Check(bubble.Portraits[0].transform.parent.GetComponent<Mask>(), "Portraits clipped by the paper disc");
        var group = bubble.GetComponent<CanvasGroup>(); Check(group && !group.blocksRaycasts && !group.interactable, "Bubble never takes clicks");
        Check(bubble.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), "No raycast targets inside the bubble");
        Check(bubble.GetComponentsInChildren<Image>(true).All(i => !i.sprite || !AssetDatabase.GetAssetPath(i.sprite).Contains("icon-")), "No raster action icons (code-drawn glyphs only)");
        var r = bubble.Rect; Check(r.pivot == new Vector2(.5f, 0), "Pivot = tail tip");
        // Nothing hides the ring: it draws over the pin tail, and the badge sits outside the ring band (same parent, centre pivots).
        var ring = bubble.Ring.rectTransform; var pin = bubble.Visual.Find("Pin"); var badge = (RectTransform)bubble.Badge.transform;
        Check(pin && ring.parent == pin.parent && ring.GetSiblingIndex() > pin.GetSiblingIndex(), "Ring drawn over the pin");
        Check(badge.parent == ring.parent && Vector2.Distance(badge.localPosition, ring.localPosition) >= (ring.sizeDelta.x + badge.sizeDelta.x) * .5f, "Badge covers the ring band");

        var screen = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab"); var c = screen.GetComponentInChildren<SettlementController>(true);
        var main = c.Main.transform; var mk = main.Find("AssignmentMarkers")?.GetComponent<SettlementAssignmentMarkers>(); Check(mk && mk.Owner == c, "Main/AssignmentMarkers");
        var expected = new Dictionary<Place, Button> { { Place.Bed, c.Bed }, { Place.Stock, c.Stock }, { Place.Workbench, c.Workbench }, { Place.Cabinet, c.Cabinet }, { Place.Research, c.Development.ResearchButton } };
        foreach (var kv in expected)
        {
            var slot = mk.SlotFor(kv.Key); Check(slot != null && slot.Button == kv.Value && slot.Bubble && slot.Bubble.transform.parent == mk.transform, "Slot " + kv.Key);
            Check(PrefabUtility.GetCorrespondingObjectFromSource(slot.Bubble.gameObject) != null, kv.Key + " bubble is a prefab instance");
        }
        for (int i = 0; i < 2; i++) { var wb = main.Find("WorkBubble_" + i); Check(!wb || !wb.gameObject.activeSelf, "WorkBubble_" + i + " still active"); }
        int facility = main.Cast<Transform>().Where(t => t.name.StartsWith("Facility_")).Max(t => t.GetSiblingIndex());
        Check(mk.transform.GetSiblingIndex() > facility, "Markers drawn above the facilities");
        Check(mk.HideHeadBadges, "Head badges hidden");
        // Facility mapping mirrors SettlementPawnMotion.JobPosition.
        Check(SettlementAssignmentMarkers.PlaceOf("research-tools") == (int)Place.Research && SettlementAssignmentMarkers.PlaceOf("build-research") == (int)Place.Research
            && SettlementAssignmentMarkers.PlaceOf("expand-stock") == (int)Place.Cabinet && SettlementAssignmentMarkers.PlaceOf("repair-bed") == (int)Place.Bed
            && SettlementAssignmentMarkers.PlaceOf("upgrade-cooker") == (int)Place.Stock && SettlementAssignmentMarkers.PlaceOf("bandage") == (int)Place.Workbench
            && SettlementAssignmentMarkers.PlaceOf("open-side-room") < 0 && SettlementAssignmentMarkers.PlaceOf("prepare-side-room") < 0, "Facility mapping");
        Check(mk.Duration(40) == "40분" && mk.Duration(60) == "1시간" && mk.Duration(80) == "1시간 20분", "Duration text");
        return "PASS wiring: bubble prefab (pin/ring over the tail/3 masked ink portraits/badge off the ring/label, no raycasts, no raster icons), 5 facility slots bound to Bed/Stock/Workbench/Cabinet/ResearchButton, WorkBubble_0/1 off, mapping = JobPosition, duration text";
    }

    // ---- Play mode ----
    static async Task Tap(Button button)
    {
        Check(button && button.IsActive() && button.IsInteractable(), "Unavailable " + (button ? button.name : "null")); Hit(button);
        var data = new PointerEventData(EventSystem.current) { position = Point(button), button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler); await Task.Delay(100);
    }
    static Vector2 Screen(RectTransform r) { Canvas.ForceUpdateCanvases(); return RectTransformUtility.WorldToScreenPoint(r.GetComponentInParent<Canvas>().worldCamera, r.TransformPoint(r.rect.center)); }
    // The topmost thing under the centre belongs to this button (bubbles must never block a click).
    // Facilities take clicks only on their silhouette (SettlementFacilityFocus), so the rect centre can be empty floor: use the first grid point on it.
    static Vector2 Point(Selectable target)
    {
        var r = (RectTransform)target.transform; var centre = Screen(r); var focus = target.GetComponent<SettlementFacilityFocus>(); if (!focus) return centre;
        var cam = r.GetComponentInParent<Canvas>().worldCamera; var rect = r.rect;
        for (int y = 1; y < 8; y++) for (int x = 1; x < 12; x++)
        {
            var p = RectTransformUtility.WorldToScreenPoint(cam, r.TransformPoint(new Vector2(rect.xMin + rect.width * x / 12f, rect.yMin + rect.height * y / 8f)));
            if (focus.IsRaycastLocationValid(p, cam)) return p;
        }
        return centre;
    }
    static void Hit(Selectable target)
    {
        var data = new PointerEventData(EventSystem.current) { position = Point(target) }; var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Selectable>() == target, "Blocked " + target.name + " by " + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
    }
    static async Task Until(Func<bool> done, int ms, string what) { var w = Stopwatch.StartNew(); while (!done()) { if (w.ElapsedMilliseconds > ms) throw new Exception("Timeout: " + what); await Task.Delay(30); } }
    static string Shots => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "AssignmentMarkersCapture"));
    static Task Still(MonoBehaviour host, string name) { var done = new TaskCompletionSource<bool>(); host.StartCoroutine(StillRoutine(name, done)); return done.Task; }
    static IEnumerator StillRoutine(string name, TaskCompletionSource<bool> done)
    {
        yield return new WaitForEndOfFrame();
        var raw = new RenderTexture(UnityEngine.Screen.width, UnityEngine.Screen.height, 0); var rt = new RenderTexture(raw.width, raw.height, 0); ScreenCapture.CaptureScreenshotIntoRenderTexture(raw);
        if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(raw, rt, new Vector2(1, -1), new Vector2(0, 1)); else Graphics.Blit(raw, rt);
        var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new UnityEngine.Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); RenderTexture.active = prev; Object.Destroy(raw); Object.Destroy(rt);
        Directory.CreateDirectory(Shots); File.WriteAllBytes(Path.Combine(Shots, name + ".png"), tex.EncodeToPNG()); Object.Destroy(tex); done.SetResult(true);
    }
    // World-space bounds of the bubble drawing (disc/ring and, when shown, the badge and the label strip).
    static Rect Bounds(AssignmentBubble b, Transform space)
    {
        var parts = new List<RectTransform> { b.Ring ? b.Ring.rectTransform : b.Visual }; if (b.LabelRoot && b.LabelRoot.activeSelf) parts.Add((RectTransform)b.LabelRoot.transform);
        if (b.Badge && b.Badge.activeSelf) parts.Add((RectTransform)b.Badge.transform);
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue; var c = new Vector3[4];
        foreach (var r in parts) { r.GetWorldCorners(c); foreach (var w in c) { var p = space.InverseTransformPoint(w); x0 = Mathf.Min(x0, p.x); y0 = Mathf.Min(y0, p.y); x1 = Mathf.Max(x1, p.x); y1 = Mathf.Max(y1, p.y); } }
        return Rect.MinMaxRect(x0, y0, x1, y1);
    }
    static Rect Bounds(RectTransform r, Transform space) { var c = new Vector3[4]; r.GetWorldCorners(c); Vector3 a = space.InverseTransformPoint(c[0]), b = space.InverseTransformPoint(c[2]); return Rect.MinMaxRect(a.x, a.y, b.x, b.y); }
    // Tail tip on the facility's top edge (+ Inspector offset), horizontally inside the facility.
    static void Over(SettlementAssignmentMarkers mk, Place place, Button facility)
    {
        var slot = mk.SlotFor(place); var b = slot.Bubble; var space = b.Rect.parent; var f = Bounds((RectTransform)facility.transform, space); Vector2 tip = b.Rect.localPosition;
        Check(Mathf.Abs(tip.y - (f.yMax + slot.Offset.y)) < 1 && Mathf.Abs(tip.x - (f.center.x + slot.Offset.x)) < 1, place + " bubble not on the facility top: tip " + tip + " rect " + f);
        Check(tip.x >= f.xMin && tip.x <= f.xMax, place + " tail tip outside the facility");
        var main = Bounds((RectTransform)mk.transform, space); var bb = Bounds(b, space);
        Check(bb.xMin >= main.xMin && bb.xMax <= main.xMax && bb.yMin >= main.yMin && bb.yMax <= main.yMax, place + " bubble leaves the screen " + bb);
    }
    static int Segments(int minutes, SettlementAssignmentMarkers mk) => Mathf.CeilToInt(minutes / (float)Mathf.Max(1, mk.MinutesPerSegment));
    // Ring = elapsed / total over all cells (a full ring at the end even when the total is not a whole number of cells).
    static float Ring(int total, int left, SettlementAssignmentMarkers mk) => (total - left) / (float)total * Segments(total, mk);

    // All ten glyphs, 1–3 portraits and the three tones in one still, for reviewing the code-drawn art.
    public static async Task<string> GlyphSheet()
    {
        var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run VerifyFieldTurnPlan.Enter first");
        var mk = c.Main.GetComponentInChildren<SettlementAssignmentMarkers>(true); Check(mk && mk.BubbleFor(Place.Bed), "Run BuildAssignmentBubble.Run first");
        var sprites = c.Roster.Candidates.Select(x => x.Portrait).Where(x => x).ToArray(); Check(sprites.Length >= 3, "Roster portraits");
        var names = new[] { "수색 · 1/3 → 2/3", "다음 턴 · 복도로 이동", "", "흔적 관찰", "망보기", "조명 지원", "짧은 휴식 · 30분 남음", "붕대 ×2 · 1시간 20분", "따뜻한 식사 2인분 · 20분", "공구 연구 · 2시간" };
        var kinds = ((ActionGlyph.Kind[])Enum.GetValues(typeof(ActionGlyph.Kind))).Where(k => k != ActionGlyph.Kind.None).ToArray();
        var made = new List<GameObject>(); var space = (RectTransform)mk.transform;
        try
        {
            for (int i = 0; i < kinds.Length; i++)
            {
                var b = Object.Instantiate(mk.BubbleFor(Place.Bed).gameObject, space).GetComponent<AssignmentBubble>(); made.Add(b.gameObject);
                var tone = i % 3 == 0 ? AssignmentBubble.Tone.Planned : i % 3 == 1 ? AssignmentBubble.Tone.Working : AssignmentBubble.Tone.Warning;
                b.Show(sprites.Take(1 + i % 3).ToList(), kinds[i], names[i], i % 4, 1, 4 + i % 3, tone);
                float x = 190 + (i % 5) * 360, y = 330 + (i / 5) * 330; b.Rect.localPosition = new Vector3(space.rect.xMin + x, space.rect.yMax - y, 0);
            }
            await Task.Delay(700); Canvas.ForceUpdateCanvases();
            Check(made.All(g => g.activeSelf), "Sheet bubbles shown");
            await Still(c, "00-glyph-sheet");
        }
        finally { foreach (var g in made) Object.Destroy(g); }
        return "Captured 00-glyph-sheet: " + string.Join(", ", kinds) + " with 1–3 portraits and Planned/Working/Warning tones";
    }

    public static async Task<string> Settlement()
    {
        var c = Object.FindAnyObjectByType<SettlementController>(); Check(c && c.Campaign != null, "Run VerifyFieldTurnPlan.Enter first");
        var mk = c.Main.GetComponentInChildren<SettlementAssignmentMarkers>(true); Check(mk && mk.Slots != null, "Run BuildAssignmentBubble.Run first");
        var log = new List<string>();
        // Fixture (like VerifyBedSilhouette): after the opening chapter, bed + workbench restored, materials for one item, a hurt member.
        foreach (var close in new Action[] { c.WorkPanel.Close, c.CraftPanel.Close, c.TimePanel.Close }) close();
        if (c.Opening) c.Opening.State.Enabled = false; if (c.Introduction) c.Introduction.Restore(10);
        c.Development.State.Workbench = true; c.Development.State.Bed = true;
        Check(c.WorkPanel.Orders.Count == 0 && c.CraftPanel.Orders.Count == 0 && c.CookingPanel.Orders.Count == 0, "Start without orders");
        var people = c.Campaign.Party.ToArray(); Check(people.Length >= 2 && people[0].MaxHealth >= 2, "Two members, the first can recover"); var a = people[0]; var b = people[1]; int health = a.Health;
        a.Health = Mathf.Max(1, a.MaxHealth - 1); foreach (var m in c.CraftPanel.Materials) m.Initial = Mathf.Max(m.Initial, 20);
        c.RefreshMembers(); await Task.Delay(250);
        var bed = mk.BubbleFor(Place.Bed); var bench = mk.BubbleFor(Place.Workbench);
        Check(c.Bed.gameObject.activeInHierarchy && c.Workbench.gameObject.activeInHierarchy, "Bed and workbench shown");
        Check(mk.Slots.All(s => !s.Bubble.Visible), "No bubble without orders");
        for (int i = 0; i < 2; i++) { var wb = c.Main.transform.Find("WorkBubble_" + i); Check(!wb || !wb.gameObject.activeInHierarchy, "Old head bubble " + i); }

        try
        {
            // 1. A rest through the real bed panel; bubbles stay hidden while a panel is open.
            await Tap(c.Bed); await Until(() => c.WorkPanel.IsOpen, 2000, "rest panel"); await Task.Delay(80);
            await Tap(c.WorkPanel.Rows[0].Button); await Tap(c.WorkPanel.ShortRest); Check(c.WorkPanel.Confirm.interactable, "Rest confirm: " + c.WorkPanel.ConfirmLabel.text);
            await Tap(c.WorkPanel.Confirm); Check(!c.WorkPanel.IsOpen && c.WorkPanel.Orders.Count == 1, "Rest registered"); var rest = c.WorkPanel.Orders[0]; int restTotal = rest.Minutes;
            // 2. A craft through the real workbench panel, by the second member.
            await Tap(c.Workbench); await Until(() => c.CraftPanel.IsOpen, 2000, "craft panel"); await Task.Delay(120);
            Check(!bed.Visible, "Bubble hidden behind the craft panel");
            await Tap(c.CraftPanel.WorkerRows[1].Button);
            for (int i = 0; i < c.CraftPanel.RecipeRows.Count && !c.CraftPanel.Confirm.interactable; i++) await Tap(c.CraftPanel.RecipeRows[i].Button);
            Check(c.CraftPanel.Confirm.interactable, "No craftable item: " + c.CraftPanel.ConfirmLabel.text);
            await Tap(c.CraftPanel.Confirm); Check(!c.CraftPanel.IsOpen && c.CraftPanel.Orders.Count == 1, "Craft registered"); var craft = c.CraftPanel.Orders[0]; int craftTotal = craft.Minutes;
            Check(SettlementAssignmentMarkers.PlaceOf(craft.Recipe.Id) == (int)Place.Workbench, "Item goes to the workbench: " + craft.Recipe.Id);
            await Task.Delay(400);

            // 3. One bubble over each facility with that member's portrait, glyph, label and ring.
            Check(bed.Visible && bench.Visible, "Bubbles over the bed and the workbench");
            Over(mk, Place.Bed, c.Bed); Over(mk, Place.Workbench, c.Workbench);
            Check(bed.PortraitCount == 1 && bed.PortraitAt(0) == c.DataFor(a).Portrait, "Bed portrait = resting member");
            Check(bed.GlyphKind == ActionGlyph.Kind.Rest && bed.LabelText == string.Format(mk.RestFormat, rest.Name, mk.Duration(rest.Minutes)), "Bed label: " + bed.LabelText);
            Check(bed.Total == Segments(restTotal, mk) && bed.Done == 0 && Mathf.Approximately(bed.Progress, 0), "Bed ring " + bed.Done + "/" + bed.Total);
            Check(bench.PortraitCount == 1 && bench.PortraitAt(0) == c.DataFor(b).Portrait && bench.GlyphKind == ActionGlyph.Kind.Work, "Bench portrait/glyph");
            Check(bench.LabelText.StartsWith(craft.Recipe.Name) && bench.LabelText.EndsWith(mk.Duration(craft.Minutes)), "Bench label: " + bench.LabelText);
            Check(bench.Total == Segments(craftTotal, mk) && bench.Done == 0, "Bench ring " + bench.Done + "/" + bench.Total);
            Check(mk.Slots.Where(s => s.Facility != Place.Bed && s.Facility != Place.Workbench).All(s => !s.Bubble.Visible), "Other facilities stay empty");
            foreach (var target in new Selectable[] { c.Bed, c.Workbench, c.Advance }) Hit(target);
            foreach (var bubble in new[] { bed, bench })
            {
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = Screen(bubble.Ring.rectTransform) }, hits);
                Check(hits.All(h => !h.gameObject.GetComponentInParent<AssignmentBubble>()), "A bubble takes clicks");
            }
            var space = bed.Rect.parent;
            // The markers draw above the HUD nodes that come before the facilities, so they must stay clear of them too.
            var hud = new[] { "DayPanel", "AdvanceTime", "Location", "ResourceHud", "Journal" }.Select(n => c.Main.transform.Find(n)).Where(t => t && t.gameObject.activeInHierarchy).Cast<RectTransform>();
            foreach (var bubble in new[] { bed, bench }) foreach (var r in new[] { (RectTransform)c.Advance.transform }.Concat(hud).Concat(c.Members.Where(m => m.gameObject.activeInHierarchy).Select(m => (RectTransform)m.transform)))
                    Check(!Bounds(bubble, space).Overlaps(Bounds(r, space)), "Bubble covers " + r.name);
            var motion = Object.FindAnyObjectByType<SettlementPawnMotion>();
            if (motion) Check(motion.WorkBadges.Where(x => x).All(x => !x.GetComponent<SpriteRenderer>().enabled), "Head WorkBadge still drawn");
            await Still(c, "01-rest-and-craft"); log.Add("bed+bench bubbles, portraits, labels, empty rings, clicks free");

            // 4. 15 minutes pass through the real time panel: hidden while it is open, then the ring fills over ~0.6 s.
            int restLeft = rest.Minutes, craftLeft = craft.Minutes;
            await Tap(c.Advance); await Until(() => c.TimePanel.IsOpen, 2000, "time panel"); await Task.Delay(80); Check(!bed.Visible && !bench.Visible, "Hidden behind the time panel");
            await Tap(c.TimePanel.Choices[0]); Check(c.TimePanel.SelectedMinutes == 15, "15 minutes"); await Tap(c.TimePanel.Confirm); await Tap(c.TimePanel.CloseButton);
            Check(rest.Minutes == restLeft - 15 && c.WorkPanel.Orders.Contains(rest), "Rest advanced");
            await Task.Delay(60); float early = bed.Progress, goal = Ring(restTotal, rest.Minutes, mk);
            Check(bed.Visible && bed.LabelText == string.Format(mk.RestFormat, rest.Name, mk.Duration(rest.Minutes)), "Bed label after time: " + bed.LabelText);
            await Still(c, "02-ring-filling");
            await Task.Delay(Mathf.CeilToInt(mk.AnimateSeconds * 1000) + 250);
            Check(early < goal - .01f && Mathf.Abs(bed.Progress - goal) < .01f && bed.Done == Mathf.FloorToInt(goal), "Ring animation " + early + " → " + bed.Progress + " (goal " + goal + ")");
            if (c.CraftPanel.Orders.Contains(craft)) Check(Mathf.Abs(bench.Progress - Ring(craftTotal, craft.Minutes, mk)) < .01f && bench.LabelText.EndsWith(mk.Duration(craft.Minutes)), "Bench after time: " + bench.LabelText);
            else Check(!bench.Visible, "Finished craft leaves no bubble");
            await Still(c, "03-after-15-minutes"); log.Add("hidden under the time panel, ring " + early.ToString("0.00") + "→" + bed.Progress.ToString("0.00") + " over " + mk.AnimateSeconds + " s, label counts down");

            // 5. Stop the rest (bubble goes), then the first member also works at the bench: two portraits, '외 1'.
            await Tap(c.Bed); await Until(() => c.WorkPanel.IsOpen, 2000, "rest panel"); await Tap(c.WorkPanel.Rows[0].Button);
            Check(c.WorkPanel.ConfirmLabel.text == "휴식 중단", "Stop label: " + c.WorkPanel.ConfirmLabel.text); await Tap(c.WorkPanel.Confirm);
            await Task.Delay(150); Check(c.WorkPanel.Orders.Count == 0 && !bed.Visible, "Stopped rest leaves no bubble");
            if (!c.CraftPanel.Orders.Contains(craft))
            {
                await Tap(c.Workbench); await Until(() => c.CraftPanel.IsOpen, 2000, "craft panel"); await Task.Delay(120); await Tap(c.CraftPanel.WorkerRows[1].Button);
                for (int i = 0; i < c.CraftPanel.RecipeRows.Count && !c.CraftPanel.Confirm.interactable; i++) await Tap(c.CraftPanel.RecipeRows[i].Button);
                await Tap(c.CraftPanel.Confirm);
            }
            await Tap(c.Workbench); await Until(() => c.CraftPanel.IsOpen, 2000, "craft panel"); await Task.Delay(120); await Tap(c.CraftPanel.WorkerRows[0].Button);
            for (int i = 0; i < c.CraftPanel.RecipeRows.Count && !c.CraftPanel.Confirm.interactable; i++) await Tap(c.CraftPanel.RecipeRows[i].Button);
            await Tap(c.CraftPanel.Confirm); Check(c.CraftPanel.Orders.Count == 2, "Two orders at the bench"); await Task.Delay(300);
            var first = c.CraftPanel.Orders.OrderBy(o => o.Minutes).First();
            Check(bench.Visible && bench.PortraitCount == 2 && bench.PortraitAt(0) == c.DataFor(first.Member).Portrait, "Two portraits, soonest job first");
            Check(bench.LabelText.EndsWith(string.Format(mk.OthersFormat, "", 1)), "Others suffix: " + bench.LabelText);
            Over(mk, Place.Workbench, c.Workbench); Hit(c.Workbench);
            await Still(c, "04-two-at-the-bench"); log.Add("two members at one bench → one bubble, 2 portraits, '외 1'");
        }
        finally
        {
            // Clean up through the real panels: cancelling returns the reserved materials.
            if (c.WorkPanel.IsOpen) c.WorkPanel.Close(); if (c.TimePanel.IsOpen) c.TimePanel.Close();
            foreach (var m in c.Campaign.Party.ToArray()) c.WorkPanel.Cancel(m);
            if (c.CraftPanel.Orders.Count > 0)
            {
                if (!c.CraftPanel.IsOpen) c.CraftPanel.Open(); await Task.Delay(150);
                for (int guard = 0; guard < 6 && c.CraftPanel.OrderRows.Count > 0; guard++) { c.CraftPanel.OrderRows[0].Cancel.onClick.Invoke(); c.CraftPanel.CancelYes.onClick.Invoke(); await Task.Delay(80); }
            }
            if (c.CraftPanel.IsOpen) c.CraftPanel.Close(); a.Health = health; c.RefreshMembers();
        }
        await Task.Delay(150); Check(mk.Slots.All(s => !s.Bubble.Visible), "Bubbles gone after cleanup");
        return "PASS settlement markers: " + string.Join("; ", log) + ". Stills: " + Shots;
    }
}
