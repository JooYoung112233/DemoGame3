using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // 배정 표시 (2026-09-25): on the site board (2nd visit on) one AssignmentBubble over each target a member works at this turn,
    // not over heads: a search over the object's marker (the den shelf over the office door), an observation over the trace,
    // a listener beside the door, a reserved move over that door (then nothing else: the move releases the assignments).
    // Polls the plan (no events exist); when a turn passes each ring fills from the step shown before it, over the turn
    // sequence (FieldTurnPulse). Presentation only: never takes clicks, never changes the plan, never adds children to hotspot
    // buttons. Only positions that follow targets are computed here (and the label strip sliding back inside the room);
    // everything else is the AssignmentBubble prefab and this Inspector. Temporary layout from planning mockups, no approved UI mock.
    [DefaultExecutionOrder(400)]
    public sealed class FieldPlanTargetMarkers : MonoBehaviour
    {
        public enum Side { Above, Right, Left }
        [System.Serializable] public sealed class Placement
        {
            [Tooltip("사물 번호 (ExpeditionLootPanel.Sites · 8 = 관리실 선반)")] public int Site = -1;
            [Tooltip("먼저 시도할 자리 (무언가를 가리면 다른 자리를 고릅니다)")] public Side First = Side.Above;
            [Tooltip("꼬리 끝 보정 (px, 위 = +)")] public Vector2 Offset;
        }
        // One placed bubble, in this node's space (the verify scripts read these).
        public struct Shown { public string Key; public AssignmentBubble Bubble; public RectTransform Anchor; public Vector2 Tip; public Side Side; public Rect Ring, Label, Tail, Bounds; }

        public ExpeditionArrivalPanel Arrival;
        [Tooltip("말풍선 (AssignmentBubble 프리팹 인스턴스, 이 노드의 자식). 모자라면 첫 칸을 복제합니다.")] public AssignmentBubble[] Bubbles;
        [Header("자리")]
        [Tooltip("말풍선이 머무는 방 영역 (Main 기준 · 왼쪽 위 원점 px: x, y, 폭, 높이). 대원 카드 줄 위까지.")] public Rect RoomArea = new Rect(16, 8, 1888, 738);
        [Tooltip("말풍선이 가리면 안 되는 화면 요소 (날짜·장소·턴 종이, 대원 카드, 버튼). 켜져 있을 때만 봅니다.")] public RectTransform[] KeepClear;
        [Tooltip("턴 연출 중에만 켜지는 요소 (턴 띠). 꺼져 있어도 비워 둡니다 (고리가 띠 밑에서 차오르지 않게).")] public RectTransform[] KeepClearAlways;
        [Tooltip("사물별 첫 자리와 보정 (없는 사물은 표식 위)")] public Placement[] Placements =
        {
            new Placement { Site = 2, First = Side.Right, Offset = new Vector2(0, -60) }, // SPACE 오락기: 턴 띠 아래, 오른쪽
            new Placement { Site = 4, First = Side.Right, Offset = new Vector2(0, -60) }  // 두꺼비집: 턴·경로 종이 아래
        };
        [Tooltip("표식 위에 설 때 표식과 꼬리 끝 사이 (px)")] [Min(0)] public float AboveGap = 2;
        [Tooltip("옆에 설 때, 문 표시 위로 올라설 때 띄우는 간격 (px)")] [Min(0)] public float SideGap = 6;
        [Tooltip("꼬리 폭의 절반 (px · 겹침 판정용)")] [Min(0)] public float TailHalfWidth = 6;
        [Tooltip("이름표가 방 영역을 벗어나면 옆으로 밀 수 있는 최대치 (이름표 폭의 절반 대비)")] [Range(0, 1)] public float LabelSlide = .8f;
        [Tooltip("귀 대기 말풍선 크기 (배율 · 문 옆에 작게)")] [Range(.4f, 1)] public float ListenScale = .72f;
        [Header("문구")]
        [Tooltip("수색: {0} 지금 진행도, {1} 필요 턴, {2} 이번 턴 뒤 진행도")] public string SearchFormat = "수색 · {0}/{1} → {2}/{1}";
        [Tooltip("다음 턴에 풀릴 멈춘 수색: {0} 멈춘 이유")] public string PausedFormat = "멈춤 · {0}";
        [Tooltip("흔적 관찰")] public string ObserveLabel = "흔적 관찰";
        [Tooltip("귀 대기 이름표 (비우면 없음 · 문 표시가 이미 '귀 대는 중'을 보여 줍니다)")] public string ListenLabel = "";
        [Tooltip("이동 인원이 초상 칸보다 많을 때 이름표: {0} 예약 문구, {1} 인원")] public string MoveCrowdFormat = "{0} · {1}명";
        [Header("표시")]
        [Tooltip("도구·조명이 없어 멈춘 수색도 경고 색으로 표시")] public bool ShowPaused = true;
        [Tooltip("흔적 관찰에 1턴 고리")] public bool ObserveRing = true;
        [Tooltip("이동 예약에 걸리는 턴 수만큼 고리")] public bool MoveRing = true;
        [Tooltip("들어가면 마주칠 이동 예약은 경고 색")] public bool MoveWarning = true;
        [Header("고리 움직임")]
        [Tooltip("턴 연출(FieldTurnReplay) 중 고리가 차오르는 구간 (시작, 끝 · 0~1)")] public Vector2 RingWindow = new Vector2(.15f, .85f);
        [Tooltip("턴 연출이 없을 때 고리가 차오르는 시간 (초 · 실제 시간)")] [Min(0)] public float FallbackSeconds = .6f;
        [Tooltip("배정을 다시 읽는 최소 간격 (초 · 가방 도구·체력 변화 대비)")] [Min(.05f)] public float PollSeconds = .5f;

        public const string MoveKey = "move";
        public static string SearchKey(int site) => "search:" + site;
        public static string ObserveKey(string id) => "observe:" + id;
        public static string ListenKey(int door) => "listen:" + door;
        // Board on, room free (no window, no walk, no turn resolving): bubbles are drawn only then.
        public bool Free { get; private set; }
        public IReadOnlyList<Shown> Placed => shown;
        public bool TryGet(string key, out Shown result) { foreach (var s in shown) if (s.Key == key) { result = s; return true; } result = default; return false; }
        public AssignmentBubble BubbleFor(string key) => TryGet(key, out var s) ? s.Bubble : null;
        public Vector2 OffsetFor(int site) { if (Placements != null) foreach (var p in Placements) if (p != null && p.Site == site) return p.Offset; return Vector2.zero; }
        public Rect Area { get { var r = ((RectTransform)transform).rect; return new Rect(r.xMin + RoomArea.x, r.yMax - RoomArea.y - RoomArea.height, RoomArea.width, RoomArea.height); } }
        // The scale the bubble had in the prefab/Inspector (the listener's ListenScale multiplies it).
        public Vector3 BaseScale(AssignmentBubble b) { if (!b) return Vector3.one; if (!homeScale.TryGetValue(b, out var s)) { s = b.Rect.localScale; homeScale[b] = s; } return s; }
        // A rect in this node's space (pulsing tags: pass unscaled to ignore their own scale).
        public Rect RectOf(RectTransform r, bool unscaled = false)
        {
            Vector3 a, b;
            if (unscaled && r.parent) { var rr = r.rect; Vector2 at = r.localPosition; a = r.parent.TransformPoint(at + rr.min); b = r.parent.TransformPoint(at + rr.max); }
            else { r.GetWorldCorners(corners); a = corners[0]; b = corners[2]; }
            a = transform.InverseTransformPoint(a); b = transform.InverseTransformPoint(b);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        sealed class Item
        {
            public string Key, Label; public RectTransform Anchor; public Transform Host, Door; public readonly List<Sprite> Portraits = new List<Sprite>();
            public ActionGlyph.Kind Glyph; public int Done, Next, Total; public AssignmentBubble.Tone Tone = AssignmentBubble.Tone.Planned;
            public Side First = Side.Above; public bool SideOnly; public Vector2 Offset; public float Scale = 1;
        }
        struct Mark { public Transform Owner; public Rect Rect; }

        readonly List<Item> items = new List<Item>();
        readonly List<AssignmentBubble> pool = new List<AssignmentBubble>();
        readonly List<Shown> shown = new List<Shown>();
        readonly Dictionary<string, int> shownDone = new Dictionary<string, int>();
        readonly Dictionary<string, float> animFrom = new Dictionary<string, float>();
        readonly Dictionary<AssignmentBubble, Vector2> labelHome = new Dictionary<AssignmentBubble, Vector2>();
        readonly Dictionary<AssignmentBubble, Vector3> homeScale = new Dictionary<AssignmentBubble, Vector3>();
        readonly Dictionary<AssignmentBubble, string> laidOut = new Dictionary<AssignmentBubble, string>();
        readonly Dictionary<Transform, FieldThreatMarker> tagOf = new Dictionary<Transform, FieldThreatMarker>();
        readonly List<Rect> blocked = new List<Rect>(), own = new List<Rect>(), rel = new List<Rect>(3);
        readonly List<float> levels = new List<float>(3);
        readonly List<Mark> marks = new List<Mark>();
        readonly Vector3[] corners = new Vector3[4];
        static readonly Side[] Sides = { Side.Above, Side.Right, Side.Left };
        ExplorationHotspot[] hotspots; ExpeditionNpcStory story; bool storyLooked;
        (int, int, int, int, int, FieldSiteState, int, int, bool, bool) key; bool haveKey;
        int lastTurns = -1, animPulse; float animStart, nextPoll; bool pulseSeen;

        // No SetActive here (the hierarchy may be switching off): the next LateUpdate hides or shows the pool again.
        void OnDisable() { shown.Clear(); Free = false; Forget(); }

        void LateUpdate()
        {
            var a = Arrival; var t = a ? a.Threat : null; var pl = t ? t.Planner : null; var rooms = a ? a.Rooms : null;
            if (!a || !a.IsOpen || !rooms || !pl || !pl.Active || t.State == null) { Free = false; HideAll(); Forget(); return; }
            Free = !a.InTransit && t.CanAct && !pl.Resolving && a.Main && a.Main.interactable && a.Main.blocksRaycasts && !(a.Encounter && a.Encounter.Battle && a.Encounter.Battle.IsOpen);
            // Any turn (the planner's, a hush, a bag item, a move) passed: the rings fill from the step shown before it.
            bool turned = lastTurns >= 0 && rooms.Turns > lastTurns; lastTurns = rooms.Turns;
            var now = Key(a, t, pl, rooms);
            if (turned || !haveKey || !now.Equals(key) || Time.unscaledTime >= nextPoll) { key = now; haveKey = true; nextPoll = Time.unscaledTime + PollSeconds; Rebuild(a, t, pl, rooms, turned); }
            if (!Free) { HideAll(); return; }
            Present(a, t);
        }
        (int, int, int, int, int, FieldSiteState, int, int, bool, bool) Key(ExpeditionArrivalPanel a, ExpeditionSiteThreat t, FieldTurnPlanner pl, ExpeditionRoomNavigation rooms)
        {
            int alive = 0; for (int i = 0; i < a.Participants.Count; i++) if (a.Participants[i] != null && a.Participants[i].Health > 0) alive++;
            var clue = Clue(a); bool queued = rooms.HasQueuedMove;
            return (pl.Plan.Version, rooms.Turns, rooms.CurrentRoom, queued ? rooms.QueuedRoom : -1, queued ? rooms.QueuedTurns : 0, t.State, t.State.TurnsUsed, alive, clue, Free);
        }

        // ---- what is assigned (the same check the preview and the turn use) ----
        void Rebuild(ExpeditionArrivalPanel a, ExpeditionSiteThreat t, FieldTurnPlanner pl, ExpeditionRoomNavigation rooms, bool turned)
        {
            items.Clear();
            // A reserved move is the whole next turn: it releases every assignment, so only its door shows.
            if (rooms.HasQueuedMove) AddMove(a, rooms);
            else
            {
                var k = pl.Plan.Check(pl.Facts());
                // Listeners first: their small bubble has only the two spots beside the door, the others move around it.
                foreach (var l in k.Listens)
                {
                    var door = DoorFor(a, t, rooms.CurrentRoom, l.Door); var anchor = MarkerOf(door); if (!anchor) continue;
                    var it = Add(ListenKey(l.Door), anchor); it.Door = door; it.Glyph = ActionGlyph.Kind.Listen; it.Label = ListenLabel;
                    it.Scale = ListenScale; it.SideOnly = true; it.First = Side.Right;
                    Portrait(it, a, l.Member);
                }
                foreach (var r in k.Runs)
                {
                    var it = Target(a, t, SearchKey(r.Site), r.Site); if (it == null) continue;
                    it.Glyph = ActionGlyph.Kind.Search; it.Label = string.Format(SearchFormat, r.Before, r.Required, r.After);
                    it.Done = r.Before; it.Next = r.After - r.Before; it.Total = r.Required;
                    Portrait(it, a, r.Lead); Portrait(it, a, r.Support);
                }
                if (ShowPaused)
                    foreach (var p in k.Paused)
                    {
                        if (p.Reason == FieldPause.Downed || p.Reason == FieldPause.Complete || p.Reason == FieldPause.OtherRoom || Has(SearchKey(p.Site))) continue;
                        var it = Target(a, t, SearchKey(p.Site), p.Site); if (it == null) continue;
                        it.Glyph = ActionGlyph.Kind.Search; it.Label = string.Format(PausedFormat, pl.PauseShort(p.Site, p.Reason)); it.Tone = AssignmentBubble.Tone.Warning;
                        if (a.Loot && a.Loot.Peek(p.Site, out var s) && s.Progress > 0) { it.Done = s.Progress; it.Total = s.Required; }
                        Portrait(it, a, p.Lead);
                    }
                var clue = ClueMarker(a);
                if (clue)
                    foreach (var o in k.Observations)
                    {
                        var it = Add(ObserveKey(o.Id), clue); it.Glyph = ActionGlyph.Kind.Observe; it.Label = ObserveLabel;
                        if (ObserveRing) { it.Next = 1; it.Total = 1; }
                        Portrait(it, a, o.Member);
                    }
            }
            if (turned)
            {
                animFrom.Clear(); animStart = Time.unscaledTime; animPulse = FieldTurnPulse.Version; pulseSeen = false;
                foreach (var it in items) if (shownDone.TryGetValue(it.Key, out int before) && it.Done > before) animFrom[it.Key] = before;
            }
            shownDone.Clear(); foreach (var it in items) shownDone[it.Key] = it.Done;
        }
        void AddMove(ExpeditionArrivalPanel a, ExpeditionRoomNavigation rooms)
        {
            var door = rooms.QueuedDoor; var anchor = MarkerOf(door); if (!anchor) return;
            var it = Add(MoveKey, anchor); it.Door = door; it.Glyph = ActionGlyph.Kind.Move; it.Label = rooms.QueuedLabel;
            if (MoveRing) it.Next = it.Total = rooms.QueuedTurns;
            if (MoveWarning) { var o = rooms.QueuedMoveOutlook(); if (o != null && o.Encounter) it.Tone = AssignmentBubble.Tone.Warning; }
            for (int m = 0; m < a.Participants.Count; m++) if (a.Participants[m] != null && a.Participants[m].Health > 0) Portrait(it, a, m);
            // The whole party moves: more people than portrait slots → say how many.
            int slots = Bubbles != null && Bubbles.Length > 0 && Bubbles[0] && Bubbles[0].Portraits != null ? Bubbles[0].Portraits.Length : 3;
            if (it.Portraits.Count > slots) it.Label = string.Format(MoveCrowdFormat, it.Label, it.Portraits.Count);
        }
        // A search site: its 40px marker (the den shelf has no button: the office door's).
        Item Target(ExpeditionArrivalPanel a, ExpeditionSiteThreat t, string key, int site)
        {
            bool den = t && site == t.DenSite; Transform button = den ? (a.Rooms.OfficeDoor ? a.Rooms.OfficeDoor.transform : null)
                : a.Objects != null && site >= 0 && site < a.Objects.Length && a.Objects[site] ? a.Objects[site].transform : null;
            var anchor = MarkerOf(button); if (!anchor) return null;
            var it = Add(key, anchor); if (den) it.Door = button;
            if (Placements != null) foreach (var p in Placements) if (p != null && p.Site == site) { it.First = p.First; it.Offset = p.Offset; break; }
            return it;
        }
        Item Add(string key, RectTransform anchor) { var it = new Item { Key = key, Anchor = anchor, Host = anchor.name == "ExplorationMarkerPaper" && anchor.parent ? anchor.parent : anchor }; items.Add(it); return it; }
        bool Has(string key) { foreach (var it in items) if (it.Key == key) return true; return false; }
        static void Portrait(Item it, ExpeditionArrivalPanel a, int member)
        {
            if (member < 0 || member >= a.Cards.Count || !a.Cards[member] || !a.Cards[member].Portrait) return;
            var sprite = a.Cards[member].Portrait.sprite; if (sprite) it.Portraits.Add(sprite);
        }
        static RectTransform MarkerOf(Component button)
        {
            if (!button || !button.gameObject.activeInHierarchy) return null;
            var marker = button.transform.Find("ExplorationMarkerPaper") as RectTransform;
            return marker && marker.gameObject.activeInHierarchy ? marker : button.transform as RectTransform;
        }
        ExpeditionNpcStory Story(ExpeditionArrivalPanel a) { if (!storyLooked) { story = a.Story; storyLooked = true; } return story; }
        bool Clue(ExpeditionArrivalPanel a) { var s = Story(a); return s && s.Clue && s.Clue.gameObject.activeInHierarchy; }
        RectTransform ClueMarker(ExpeditionArrivalPanel a) => Clue(a) ? MarkerOf(Story(a).Clue) : null;
        // The door of `room` that opens onto `behind`: its threat tag's button, else the known door buttons.
        static Transform DoorFor(ExpeditionArrivalPanel a, ExpeditionSiteThreat t, int room, int behind)
        {
            if (t && t.DoorMarkers != null) foreach (var m in t.DoorMarkers) if (m && m.Room == room && m.From == behind && m.transform.parent) return m.transform.parent;
            var r = a.Rooms; Button b = room == FieldSiteState.Arcade && behind == FieldSiteState.Corridor ? (a.Objects != null && a.Objects.Length > 3 ? a.Objects[3] : null)
                : room == FieldSiteState.Corridor ? (behind == FieldSiteState.Arcade ? r.CorridorBack : behind == FieldSiteState.Storage ? r.LockedDoor : behind == FieldSiteState.Den ? r.OfficeDoor : null)
                : room == FieldSiteState.Storage && behind == FieldSiteState.Corridor ? r.StorageBack : null;
            return b ? b.transform : null;
        }

        // ---- drawing ----
        void Present(ExpeditionArrivalPanel a, ExpeditionSiteThreat t)
        {
            shown.Clear();
            if (!Pool(items.Count)) { HideAll(); return; }
            float p = RingProgress(); var area = Area;
            blocked.Clear(); if (KeepClear != null) foreach (var r in KeepClear) if (r && r.gameObject.activeInHierarchy) blocked.Add(RectOf(r));
            // The turn banner comes and goes with each turn: its place stays free so no ring fills under it and nothing jumps.
            if (KeepClearAlways != null) foreach (var r in KeepClearAlways) if (r) blocked.Add(RectOf(r, true));
            CollectMarks(a, t);
            for (int i = 0; i < pool.Count; i++)
            {
                var b = pool[i]; if (!b) continue;
                if (i >= items.Count) { b.Hide(); continue; }
                var it = items[i]; float from = 0; bool anim = p < 1 && animFrom.TryGetValue(it.Key, out from);
                // While the ring fills, the 'this turn' cells wait; they come back once it is full.
                b.Show(it.Portraits, it.Glyph, it.Label, it.Done, anim ? 0 : it.Next, it.Total, it.Tone);
                b.SetProgress(anim ? Mathf.Lerp(from, it.Done, p) : it.Done);
                var scale = Vector3.Scale(BaseScale(b), new Vector3(it.Scale, it.Scale, 1)); if (b.Rect.localScale != scale) b.Rect.localScale = scale;
                Layout(b, it.Label);
                shown.Add(Place(b, it, area));
            }
            if (p >= 1 && animFrom.Count > 0) animFrom.Clear();
        }
        float RingProgress()
        {
            if (animFrom.Count == 0) return 1;
            // Keep-going presses the button after the sequence began its frame: adopt the sequence that starts right after.
            if (!pulseSeen && FieldTurnPulse.Playing && FieldTurnPulse.Version == animPulse + 1 && Time.unscaledTime - animStart < .25f) animPulse++;
            float u;
            if (FieldTurnPulse.Playing && FieldTurnPulse.Version == animPulse) { pulseSeen = true; u = Mathf.InverseLerp(RingWindow.x, RingWindow.y, FieldTurnPulse.Progress01); }
            else if (pulseSeen) u = 1;
            else u = FallbackSeconds <= 0 ? 1 : (Time.unscaledTime - animStart) / FallbackSeconds;
            return u >= 1 ? 1 : Mathf.SmoothStep(0, 1, Mathf.Clamp01(u));
        }
        // The label strip sizes itself to its text on the next canvas pass; lay it out now so placement sees its width.
        void Layout(AssignmentBubble b, string label)
        {
            if (!b.LabelRoot || !b.LabelRoot.activeInHierarchy) return;
            if (laidOut.TryGetValue(b, out var done) && done == label) return;
            laidOut[b] = label; LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)b.LabelRoot.transform);
        }
        // Other targets' marks and door tags: bubbles avoid covering them (soft), the target's own door tag hard.
        void CollectMarks(ExpeditionArrivalPanel a, ExpeditionSiteThreat t)
        {
            marks.Clear();
            bool stale = hotspots == null; if (!stale) foreach (var h in hotspots) if (!h) { stale = true; break; }
            if (stale) hotspots = a.Main.GetComponentsInChildren<ExplorationHotspot>(true);
            foreach (var h in hotspots)
            {
                if (!h || !h.isActiveAndEnabled) continue;
                if (h.Marker && h.Marker.gameObject.activeInHierarchy) marks.Add(new Mark { Owner = h.transform, Rect = RectOf(h.Marker.rectTransform) });
                if (h.IsDoor && h.Caption && h.Caption.gameObject.activeInHierarchy && h.Caption.alpha > .01f) marks.Add(new Mark { Owner = h.transform, Rect = RectOf((RectTransform)h.Caption.transform) });
            }
            if (t.DoorMarkers != null) foreach (var m in t.DoorMarkers) if (m && m.gameObject.activeInHierarchy) marks.Add(new Mark { Owner = m.transform.parent, Rect = RectOf((RectTransform)m.transform, true) });
            if (t.ResidentTag && t.ResidentTag.gameObject.activeInHierarchy) marks.Add(new Mark { Rect = RectOf((RectTransform)t.ResidentTag.transform, true) });
        }
        FieldThreatMarker TagOf(Transform door, ExpeditionSiteThreat t)
        {
            if (!door) return null;
            if (!tagOf.TryGetValue(door, out var tag) || !tag)
            {
                tag = null; if (t.DoorMarkers != null) foreach (var m in t.DoorMarkers) if (m && m.transform.parent == door) { tag = m; break; }
                tagOf[door] = tag;
            }
            return tag;
        }

        // Tail tip on the target: over the marker first (lifted over the door's own tag), else beside it; the spot that covers
        // nothing (room edge, screen papers, cards, buttons, other marks and bubbles) wins, else the one that covers least.
        Shown Place(AssignmentBubble b, Item it, Rect area)
        {
            var s = (Vector2)b.Rect.localScale; var A = RectOf(it.Anchor);
            rel.Clear();
            var ring = Scaled(LocalRect(b, b.Ring ? b.Ring.rectTransform : b.Visual ? b.Visual : b.Rect), s); rel.Add(ring);
            var label = b.LabelRoot && b.LabelRoot.activeSelf ? (RectTransform)b.LabelRoot.transform : null; Vector2 home = default;
            if (label)
            {
                if (!labelHome.TryGetValue(b, out home)) { home = label.anchoredPosition; labelHome[b] = home; }
                var l = LocalRect(b, label); l.x -= label.anchoredPosition.x - home.x; rel.Add(Scaled(l, s));
            }
            rel.Add(new Rect(-TailHalfWidth * s.x, 0, 2 * TailHalfWidth * s.x, Mathf.Max(0, ring.yMin)));
            own.Clear(); own.Add(A); int tagAt = -1;
            var tag = TagOf(it.Door, Arrival.Threat); if (tag && tag.gameObject.activeInHierarchy) { tagAt = own.Count; own.Add(RectOf((RectTransform)tag.transform, true)); }
            var hotspot = it.Door ? it.Door.GetComponent<ExplorationHotspot>() : null;
            if (hotspot && hotspot.IsDoor && hotspot.Caption && hotspot.Caption.gameObject.activeInHierarchy && hotspot.Caption.alpha > .01f) own.Add(RectOf((RectTransform)hotspot.Caption.transform));
            // Beside: level with the marker first, then with the door's own tag or name. A listener never stands in the tag's row
            // (the tag says '귀 대는 중'): its levels are the marker's and the name's, kept out of that row.
            levels.Clear(); for (int h = 0; h < own.Count; h++) if (!(it.SideOnly && h == tagAt)) levels.Add(own[h].center.y);
            if (it.SideOnly && tagAt >= 0)
            {
                var T = own[tagAt]; float top = float.MinValue, bottom = float.MaxValue; foreach (var r in rel) { top = Mathf.Max(top, r.yMax); bottom = Mathf.Min(bottom, r.yMin); }
                float c = ring.center.y, up = top - c, down = c - bottom;
                for (int h = 0; h < levels.Count; h++) levels[h] = T.center.y >= A.center.y ? Mathf.Min(levels[h], T.yMin - SideGap - up - it.Offset.y) : Mathf.Max(levels[h], T.yMax + SideGap + down - it.Offset.y);
            }
            float best = float.MaxValue, bestShift = 0; var bestTip = Vector2.zero; var bestSide = it.First;
            for (int n = 0; n < Sides.Length && best > 0; n++)
            {
                var side = n == 0 ? it.First : Sides[n] == it.First ? Sides[0] : Sides[n];
                if (it.SideOnly && side == Side.Above) continue;
                for (int h = 0; h < (side == Side.Above ? 1 : levels.Count) && best > 0; h++)
                {
                    var tip = Start(side, A, side == Side.Above ? A.center.y : levels[h], it.Offset);
                    for (int pass = 0; pass < 4 && Push(side, ref tip); pass++) { }
                    float shift = Slide(tip, label != null, area), score = Score(tip, shift, label != null, area, it.Host);
                    if (score < best - .5f) { best = score; bestTip = tip; bestShift = shift; bestSide = side; }
                }
            }
            var at = new Vector3(bestTip.x, bestTip.y, 0); if (b.Rect.localPosition != at) b.Rect.localPosition = at;
            if (label) { var want = home + new Vector2(bestShift / Mathf.Max(.01f, s.x), 0); if (label.anchoredPosition != want) label.anchoredPosition = want; }
            // The placed bubble is in the way of the next ones.
            var result = new Shown { Key = it.Key, Bubble = b, Anchor = it.Anchor, Tip = bestTip, Side = bestSide };
            result.Ring = Move(rel[0], bestTip); result.Tail = Move(rel[rel.Count - 1], bestTip);
            result.Bounds = Union(result.Ring, result.Tail);
            if (label) { var l = Move(rel[1], bestTip); l.x += bestShift; result.Label = l; result.Bounds = Union(result.Bounds, l); blocked.Add(l); }
            blocked.Add(result.Ring); blocked.Add(result.Tail);
            return result;
        }
        Vector2 Start(Side side, Rect A, float level, Vector2 offset)
        {
            float minX = float.MaxValue, maxX = float.MinValue; foreach (var r in rel) { minX = Mathf.Min(minX, r.xMin); maxX = Mathf.Max(maxX, r.xMax); }
            float y = level - rel[0].center.y;
            switch (side)
            {
                case Side.Right: return new Vector2(A.xMax + SideGap - minX, y) + offset;
                case Side.Left: return new Vector2(A.xMin - SideGap - maxX, y) + offset;
                default: return new Vector2(A.center.x, A.yMax + AboveGap) + offset;
            }
        }
        // Step clear of the target's own marker, door tag and door name: up when above, outward when beside.
        bool Push(Side side, ref Vector2 tip)
        {
            bool moved = false;
            foreach (var o in own)
                foreach (var r in rel)
                {
                    var P = Move(r, tip); if (!P.Overlaps(o)) continue;
                    if (side == Side.Above) tip.y += o.yMax + SideGap - P.yMin;
                    else if (side == Side.Right) tip.x += o.xMax + SideGap - P.xMin;
                    else tip.x -= P.xMax - (o.xMin - SideGap);
                    moved = true;
                }
            return moved;
        }
        // A label strip wider than the room edge slides back inside while the disc stays on the target.
        float Slide(Vector2 tip, bool hasLabel, Rect area)
        {
            if (!hasLabel) return 0;
            var L = Move(rel[1], tip); float shift = L.xMin < area.xMin ? area.xMin - L.xMin : L.xMax > area.xMax ? area.xMax - L.xMax : 0, max = LabelSlide * L.width * .5f;
            return Mathf.Clamp(shift, -max, max);
        }
        float Score(Vector2 tip, float shift, bool hasLabel, Rect area, Transform host)
        {
            float score = 0; int tail = rel.Count - 1;
            for (int i = 0; i < rel.Count; i++)
            {
                var P = Move(rel[i], tip); if (hasLabel && i == 1) P.x += shift;
                score += 4 * Outside(P, area);
                for (int j = 0; j < own.Count; j++) if (!(j == 0 && i == tail)) score += 3 * Overlap(P, own[j]);
                foreach (var r in blocked) score += 2 * Overlap(P, r);
                foreach (var m in marks) if (m.Owner != host) score += Overlap(P, m.Rect);
            }
            return score;
        }
        // A part's rect under the bubble root, without the pop/pulse scale on Visual.
        static Rect LocalRect(AssignmentBubble b, RectTransform part)
        {
            var r = part.rect; Vector2 min = r.min, max = r.max; var visual = b.Visual ? b.Visual.transform : null;
            for (var t = (Transform)part; t && t != b.transform; t = t.parent)
            {
                var sc = t == visual ? Vector3.one : t.localScale; Vector2 at = t.localPosition;
                min = at + Vector2.Scale(min, sc); max = at + Vector2.Scale(max, sc);
            }
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }
        static Rect Scaled(Rect r, Vector2 s) => Rect.MinMaxRect(Mathf.Min(r.xMin * s.x, r.xMax * s.x), Mathf.Min(r.yMin * s.y, r.yMax * s.y), Mathf.Max(r.xMin * s.x, r.xMax * s.x), Mathf.Max(r.yMin * s.y, r.yMax * s.y));
        static Rect Move(Rect r, Vector2 by) => new Rect(r.x + by.x, r.y + by.y, r.width, r.height);
        static Rect Union(Rect a, Rect b) => a.width <= 0 && a.height <= 0 ? b : Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        static float Overlap(Rect a, Rect b) { float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin); return w > 0 && h > 0 ? w * h : 0; }
        static float Outside(Rect a, Rect area) => a.width * a.height - Overlap(a, area);

        bool Pool(int count)
        {
            // The prefab scale is read before any listener shrinks a bubble.
            if (pool.Count == 0 && Bubbles != null) foreach (var b in Bubbles) if (b) { BaseScale(b); pool.Add(b); }
            if (pool.Count == 0 || !pool[0]) return count == 0;
            while (pool.Count < count)
            {
                // A copy of the first bubble starts from its prefab scale and label spot, not from where it is shown now.
                var src = pool[0]; var copy = Instantiate(src.gameObject, transform); copy.name = "Bubble_" + pool.Count; copy.SetActive(false);
                var b = copy.GetComponent<AssignmentBubble>(); var scale = BaseScale(src); b.Rect.localScale = scale; homeScale[b] = scale;
                if (b.LabelRoot && labelHome.TryGetValue(src, out var home)) { ((RectTransform)b.LabelRoot.transform).anchoredPosition = home; labelHome[b] = home; }
                pool.Add(b);
            }
            return true;
        }
        void HideAll()
        {
            shown.Clear(); Pool(0);
            foreach (var b in pool) if (b) b.Hide();
        }
        void Forget() { items.Clear(); shownDone.Clear(); animFrom.Clear(); haveKey = false; lastTurns = -1; storyLooked = false; }
    }
}
