using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // What the tutorial's next click is on the pawn board (SettlementTutorialGuide words each step itself).
    public enum FieldGuideStep { None, PickPawn, PlaceGhost, PickHelper, PlaceHelper, PressTurn }

    // Every string the board writes (Inspector: ExpeditionArrivalPanel.prefab → Main/PawnBoard → FieldPawnBoard → Texts). The
    // placing lines themselves are FieldTurnPlanner → PlaceTexts (FieldPlacement), the heard lines FieldTurnPlanner → DoorLog.
    [Serializable] public sealed class FieldPawnBoardTexts
    {
        [Tooltip("아무 말도 들지 않고 사물 · 문을 눌렀을 때 상황판")] [TextArea(2, 3)] public string PickFirst = "대원 말을 먼저 누르세요\n말을 누르고 사물을 누르세요.";
        [Tooltip("든 말을 놓을 수 없는 곳을 눌렀을 때 상황판 ({0}: 대원, {1}: 까닭)")] [TextArea(2, 3)] public string Blocked = "{0} · {1}\n다른 빛나는 자리를 누르세요.";
        [Header("문 기록 (문 우클릭)")]
        [Tooltip("창 제목 ({0}: 문 너머 방)")] public string DoorLogTitle = "문 기록 · {0} 쪽";
        [Tooltip("한 줄 앞에 붙는 들은 대원 ({0}: 대원)")] public string DoorLogWho = "{0} · ";
        [Tooltip("아직 들은 것이 없을 때")] [TextArea(2, 3)] public string DoorLogEmpty = "아직 들은 것이 없습니다.\n대원 말 하나를 이 문에 놓으면 턴마다 듣습니다.";
        [Tooltip("첫 방문 (잠든 것 · 들을 것이 없음) · 지나갈 수 있는 문")] [TextArea(2, 3)] public string DoorLogAsleep = "문 너머가 조용합니다.\n모두 이 문에 놓으면 다음 턴에 이동합니다.";
        [Tooltip("지금은 들을 수도 지나갈 수도 없는 문 (들은 것이 없을 때)")] public string DoorLogQuiet = "문 너머가 조용합니다.";
        [Tooltip("관리실 문 (귀 대기만 · 이동 없음)")] [TextArea(2, 3)] public string DoorLogDen = "관리실 문 · 들어갈 수 없음\n굴이 빈 동안만 선반을 뒤질 수 있습니다.";
        [Tooltip("잠긴 문 ({0}: 도구, {1}: 걸리는 턴, {2}: 소음)")] [TextArea(2, 3)] public string DoorLocked = "잠긴 철문 · {0} 필요\n모두 모이면 문을 따고 들어갑니다 · {1}턴 · 소음 {2}";
        [Header("흔적 우클릭")]
        [Tooltip("창 제목 ({0}: 흔적 이름)")] public string TraceTitle = "{0}";
        [Tooltip("설명")] [TextArea(2, 4)] public string TraceBody = "누군가 머문 흔적이 남아 있습니다.\n대원 말을 흔적 옆에 놓으면 한 턴 동안 살펴봅니다.";
    }

    // 말 놓기 판 (기획/탐험-말놓기-조작-재설계.md · 시안 01~03; the rules are FieldPlacement, the plan is FieldTurnPlanner.Plan): a
    // member's pawn is picked up (its pawn or its card), every place it can take shows a silhouette (a see-through copy of the pawn at
    // that spot, with a pin and a short label at the object; a place it cannot take now is only a grey padlock pin with the reason),
    // and a press on a silhouette, its pin or the object / door itself places it (no time passes; the turn is '턴 진행' only). The
    // pawn then walks there (FieldPawnWalker); where every pawn stands is only ever a view of the plan. Empty floor or a right press
    // takes a pawn off its task; a second pawn on an object gets the role chips (함께 · 망보기 · 조명) under its base; a pawn placed
    // at a door that still needs the others picks up the next member (추가 결정); right press opens an object's 07 window, a door's
    // heard log, the trace's note. Both visits (FieldTurnPlanner.Placing). Everything drawn lives under this node (Main's last child)
    // and the world PawnGhosts node; nothing goes under the hotspot buttons. Before the tutorial guide (1000).
    [DefaultExecutionOrder(400)]
    public sealed class FieldPawnBoard : MonoBehaviour, IPointerClickHandler
    {
        public ExpeditionArrivalPanel Arrival;
        [Tooltip("몸의 보이는 픽셀 (누름 영역 · 실루엣) · 비우면 거점의 명부")] public PartyRoster Roster;
        [Header("층 (이 노드 아래 · 뒤에 있을수록 위)")]
        [Tooltip("말을 든 동안 방 누름을 받는 투명 면 (방 영역만 · 아래 대원 카드 줄은 덮지 않음)")] public Image Catcher;
        [Tooltip("대원 말 누름 영역 (모자라면 첫 칸을 복제)")] public FieldPawnHandle[] Handles;
        [Tooltip("실루엣 누름 영역 (모자라면 첫 칸을 복제)")] public FieldPawnHandle[] GhostHandles;
        [Tooltip("사물 표식 위 핀 (행동 그림 · 설명 · 못 놓는 자리 자물쇠 · 모자라면 첫 칸을 복제)")] public FieldTargetGlow[] Pins;
        [Tooltip("역할 칩 줄 (자식 Button = 칩 · 모자라면 첫 줄을 복제)")] public RectTransform[] ChipRows;
        [Tooltip("종이 이름표 (말 이름표 · 문 기록 · 칩 까닭 · 자식 Text · 모자라면 첫 장을 복제)")] public RectTransform[] Tags;
        [Tooltip("사물 위 수색 진행 칸 (시안 02 · 종이 + 자식 FieldSearchPips · 모자라면 첫 칸을 복제)")] public RectTransform[] ProgressStrips;
        [Header("실루엣 (월드)")]
        [Tooltip("실루엣 프리팹 (FieldPawn 변형 · 바닥 그림자 없음) · 비우면 대원 말 프리팹에서 그림자를 떼어 씀")] public GameObject GhostPrefab;
        [Tooltip("실루엣을 두는 월드 노드 (ExpeditionWorld 아래 · 대원 말 노드와 따로)")] public string GhostRootName = "PawnGhosts";
        [Tooltip("실루엣 불투명도")] [Range(0, 1)] public float GhostAlpha = .4f;
        [Tooltip("포인터가 올라온 실루엣")] [Range(0, 1)] public float GhostHoverAlpha = .7f;
        [Tooltip("끌고 가는 말")] [Range(0, 1)] public float CarryAlpha = .55f;
        [Tooltip("끌고 가는 말은 다른 말 위에 그림 (PawnFacing.SortingBias)")] public int CarrySortingBias = 200;
        [Header("핀")]
        [Tooltip("핀 꼬리 끝과 사물 표식 윗변 사이 (px)")] public float PinGap = 2;
        [Tooltip("한 사물에 핀이 둘일 때 (관리실 문: 귀 대기 · 선반) 좌우 간격 (px)")] public float PinSpread = 96;
        [Tooltip("수색 진행 칸 종이의 좌우 여백 (px)")] [Min(0)] public float ProgressPad = 8;
        [Header("역할 칩 · 이름표")]
        [Tooltip("칩 줄과 받침대 사이 (px)")] public float ChipGap = 8;
        [Tooltip("이름표와 머리 사이 (px)")] public float TagGap = 10;
        [Tooltip("지금 역할 칩")] public Color ChipOn = new Color(.96f, .75f, .28f, 1);
        [Tooltip("고를 수 있는 칩")] public Color ChipOff = new Color(.93f, .9f, .82f, 1);
        [Tooltip("못 고르는 칩")] public Color ChipDisabled = new Color(.55f, .55f, .53f, 1);
        [Tooltip("칩 글자")] public Color ChipText = new Color(.1f, .09f, .08f, 1);
        [Tooltip("못 고르는 칩 글자")] public Color ChipDisabledText = new Color(.3f, .3f, .29f, 1);
        [Header("누름")]
        [Tooltip("말 없이 사물을 누르면 할 일 없는 대원의 발밑 고리가 반짝이는 시간 (초)")] [Min(0)] public float FlashSeconds = .6f;
        [Tooltip("말을 누른 대원 카드에서 떨어진 곳을 누르면 내려놓음 (방 밖 · 대원 카드와 판 위 단추 제외)")] public bool LetGoOutside = true;
        public FieldPawnBoardTexts Texts = new FieldPawnBoardTexts();

        static readonly List<FieldPawnBoard> live = new List<FieldPawnBoard>();
        public static FieldPawnBoard For(ExpeditionArrivalPanel a)
        {
            if (!a) return null;
            live.RemoveAll(b => !b);
            foreach (var b in live) if (b.Arrival == a) return b;
            var found = a.Main ? a.Main.GetComponentInChildren<FieldPawnBoard>(true) : null;
            if (found && !live.Contains(found)) live.Add(found);
            return found;
        }
        // The camera that draws the room and its pawns.
        public static Camera WorldCamera => Camera.main;

        // The member whose pawn is picked up (−1: none) · being carried by a drag.
        public int Held { get; private set; } = -1;
        public bool Carrying { get; private set; }
        public bool IsWalking { get { foreach (var w in walkers) if (w && w.Walking) return true; return false; } }
        // Free members' rings pulse a moment after a press on an object with no pawn picked up (FieldMemberActionSlots reads it).
        public bool Flashing => Time.unscaledTime < flashUntil;
        // The places shown for the held pawn (silhouettes and pins, in FieldPlacement.OptionsFor order).
        public IReadOnlyList<FieldPlaceOption> Shown => shown;
        public string LastHint { get; private set; } = "";
        public FieldPlacement Placement => Arrival ? FieldPlacement.Of(Arrival) : null;
        FieldTurnPlanner Planner => Arrival && Arrival.Threat ? Arrival.Threat.Planner : null;
        ExplorationRoomPresentation Presentation { get { if (!pres && Arrival && Arrival.World) pres = Arrival.World.GetComponent<ExplorationRoomPresentation>(); return pres; } }
        Transform PawnFrame => Presentation && Presentation.PawnRoot ? Presentation.PawnRoot : Arrival ? Arrival.PawnRoot : null;
        Camera UiCamera { get { var c = GetComponentInParent<Canvas>(); return c && c.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? c.rootCanvas.worldCamera : null; } }

        ExplorationRoomPresentation pres; Transform ghostRoot;
        readonly List<FieldPawnHandle> handles = new List<FieldPawnHandle>(), ghostHandles = new List<FieldPawnHandle>();
        readonly List<FieldTargetGlow> pins = new List<FieldTargetGlow>();
        readonly List<RectTransform> rows = new List<RectTransform>(), tags = new List<RectTransform>(), progress = new List<RectTransform>();
        readonly List<int> rowMember = new List<int>();
        readonly List<GameObject> ghosts = new List<GameObject>();
        readonly List<(SpriteRenderer R, Color C)[]> ghostColors = new List<(SpriteRenderer, Color)[]>();
        readonly List<FieldPlaceOption> shown = new List<FieldPlaceOption>();
        readonly List<int> ghostOption = new List<int>(), pinOption = new List<int>();
        readonly List<FieldPawnWalker> walkers = new List<FieldPawnWalker>();
        readonly Dictionary<ExpeditionMemberCard, FieldMemberActionSlot> slots = new Dictionary<ExpeditionMemberCard, FieldMemberActionSlot>();
        readonly Vector3[] corners = new Vector3[4];
        static readonly List<RaycastResult> hits = new List<RaycastResult>();
        GameObject carry; (SpriteRenderer R, Color C)[] carryColors; Vector2 carryAt;
        int shownVersion = -1, shownHeld = -1, shownRoom = -1, arrangedVersion = -1, arrangedRoom = -1;
        Vector3[] floor = new Vector3[0], target = new Vector3[0]; float[] look = new float[0]; bool[] hasFloor = new bool[0], manual = new bool[0], placed = new bool[0];
        GameObject firstPawn; int pawnCount = -1; bool dirty = true, wasTransit, popupBefore, hooked; float flashUntil;
        Func<int, bool> onPressed; Func<Button, bool> onDoor;

        // ---- setup ----
        void Awake()
        {
            onPressed = OnObjectPressed; onDoor = OnDoorPressed;
            Pool(handles, Handles); Pool(ghostHandles, GhostHandles); Pool(pins, Pins); Pool(rows, ChipRows); Pool(tags, Tags); Pool(progress, ProgressStrips);
            foreach (var h in handles) if (h) { h.Board = this; h.Role = FieldPawnHandle.Kind.Pawn; }
            foreach (var h in ghostHandles) if (h) { h.Board = this; h.Role = FieldPawnHandle.Kind.Ghost; }
            for (int r = 0; r < rows.Count; r++) WireRow(r);
            if (Catcher) Catcher.gameObject.SetActive(false);
        }
        static void Pool<T>(List<T> list, T[] from) where T : UnityEngine.Object { list.Clear(); if (from != null) foreach (var x in from) if (x) list.Add(x); }
        void OnEnable()
        {
            live.RemoveAll(b => !b); if (!live.Contains(this)) live.Add(this);
            Hook(); var pl = Planner; if (pl) { pl.TurnStarting -= OnTurnStarting; pl.TurnStarting += OnTurnStarting; }
        }
        void OnDisable()
        {
            live.Remove(this); Cancel(); HideAll();
            var a = Arrival;
            if (a && a.Pressed == onPressed) a.Pressed = null;
            if (a && a.Rooms && a.Rooms.DoorPressed == onDoor) a.Rooms.DoorPressed = null;
            hooked = false; var pl = Planner; if (pl) pl.TurnStarting -= OnTurnStarting;
        }
        // Object presses (ExpeditionArrivalPanel.Pressed) and door presses (ExpeditionRoomNavigation.DoorPressed) come here first.
        void Hook()
        {
            var a = Arrival; if (!a) return;
            if (a.Pressed != onPressed) a.Pressed = onPressed;
            if (a.Rooms && a.Rooms.DoorPressed != onDoor) a.Rooms.DoorPressed = onDoor;
            hooked = true;
        }
        void Start() { Order(); }
        // SettlementScreen adds room hotspots (corridor, storage) to Main after the prefab's own children: keep the turn layer and this
        // board above them (a held pawn's press on those objects would open their window instead). Was FieldQuickAssign.Start.
        void Order()
        {
            var main = transform.parent; if (!main) return;
            foreach (var n in new[] { "PlanTargetMarkers", "TurnFlow", "MemberActions" }) { var t = main.Find(n); if (t && t != transform) t.SetAsLastSibling(); }
            transform.SetAsLastSibling();
        }

        // ---- picking up and letting go ----
        public bool Hold(int member)
        {
            var place = Placement; if (place == null || !place.Ready || !Alive(member) || member >= Arrival.PartyPawns.Count || !Arrival.PartyPawns[member]) return false;
            if (Held != member) { Held = member; shownVersion = -1; }
            return true;
        }
        // Let go with no change.
        public void Cancel()
        {
            Held = -1; Carrying = false; shownVersion = -1; shown.Clear();
            if (carry && carry.activeSelf) carry.SetActive(false);
        }
        // Place the held (or the option's) member there: FieldPlacement.Place (no time), then walk. A pawn put at a door that still
        // needs the others picks up the next member who is not there (추가 결정 · 문에 모이기).
        public bool Drop(FieldPlaceOption o)
        {
            var place = Placement; if (o == null || place == null || !place.Ready) return false;
            if (!o.Enabled) { Say(string.Format(Texts.Blocked, Name(o.Member), o.Blocked)); return false; }
            if (!place.Place(o)) return false;
            int m = o.Member; if (m >= 0 && m < manual.Length) manual[m] = false;
            Cancel(); dirty = true;
            int next = place.NextAfter(o); if (next >= 0) Hold(next);
            return true;
        }
        // A card press (FieldMemberCardInput): pick up that member's pawn, or let go of it.
        public void OnCard(ExpeditionMemberCard card)
        {
            int m = Arrival ? Arrival.Cards.IndexOf(card) : -1; if (m < 0) return;
            if (m == Held) { Cancel(); return; }
            if (!Hold(m)) Cancel();
        }
        // Take the member off whatever they do (a right press on a placed pawn).
        public bool Unassign(int member)
        {
            var place = Placement; if (place == null || !place.Ready || !place.TryGetSpot(member, out _)) return false;
            bool done = place.Unassign(member); if (done) dirty = true; return done;
        }
        void Say(string line) { LastHint = line ?? ""; if (Arrival && Arrival.Status) Arrival.Status.text = LastHint; }
        void PickFirst() { Say(Texts.PickFirst); flashUntil = Time.unscaledTime + FlashSeconds; }

        // ---- presses ----
        // A handle's left press (its Button): a silhouette places, a pawn is picked up (the same one again lets go; another one
        // while holding switches — silhouettes sit above the pawns, so this press is not on one).
        public void OnHandlePressed(FieldPawnHandle h)
        {
            if (!h || h.Dragging || Carrying) return;
            if (h.Role == FieldPawnHandle.Kind.Ghost) { var o = GhostOption(h); if (o != null) Drop(o); return; }
            int m = h.Index; if (m < 0) return;
            if (Held == m) { Cancel(); return; }
            if (!Hold(m)) Cancel();
        }
        public void OnHandleRight(FieldPawnHandle h)
        {
            if (Held >= 0 || Carrying) { Cancel(); return; }
            if (h && h.Role == FieldPawnHandle.Kind.Pawn) Unassign(h.Index);
        }
        // The catcher (the room while a pawn is held): left = resolve the press, right = let go.
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right) { Cancel(); return; }
            if (e.button != PointerEventData.InputButton.Left || Held < 0) return;
            Resolve(e.position);
        }
        // A press while holding, in this order: a silhouette or pin; the object / door under it that has a place for the pawn;
        // another pawn (switch); the pawn itself (let go); empty floor (put it down there: off its task); anything else (let go).
        public void Resolve(Vector2 screen)
        {
            int m = Held; if (m < 0) return;
            var o = OptionAt(screen);
            if (o != null) { if (o.Enabled) Drop(o); else Say(string.Format(Texts.Blocked, Name(m), o.Blocked)); return; }
            var below = Below(screen);
            if (below)
            {
                var at = OptionFor(m, below);
                if (at != null) { if (at.Enabled) Drop(at); else Say(string.Format(Texts.Blocked, Name(m), at.Blocked)); return; }
            }
            int other = PawnAt(screen); if (other >= 0) { if (other == m) Cancel(); else if (!Hold(other)) Cancel(); return; }
            if (below && (below.GetComponentInParent<ExplorationHotspot>() || below.GetComponentInParent<Selectable>())) { Cancel(); return; }
            if (FloorPoint(screen, out var feet)) { PutDown(m, feet); return; }
            Cancel();
        }
        // Empty floor: off its task (FieldPlacement.Unassign) and it stands where it was put.
        void PutDown(int m, Vector3 feet)
        {
            var place = Placement; if (place != null && place.TryGetSpot(m, out _)) place.Unassign(m);
            Grow(); int room = Arrival.Rooms.CurrentRoom; var p = Presentation;
            floor[m] = p ? p.ClampFloor(room, feet) : feet; hasFloor[m] = true; manual[m] = true; dirty = true; Cancel();
        }
        FieldPlaceOption OptionAt(Vector2 screen)
        {
            var cam = UiCamera;
            foreach (var h in ghostHandles)
            {
                if (!h || !h.gameObject.activeInHierarchy) continue;
                var o = GhostOption(h); if (o != null && h.Contains(screen)) return o;
            }
            for (int p = 0; p < pins.Count && p < pinOption.Count; p++)
            {
                var pin = pins[p]; if (!pin || !pin.Visible || pinOption[p] < 0) continue;
                var cap = pin.CaptionArea;
                if (RectTransformUtility.RectangleContainsScreenPoint(pin.HitArea, screen, cam) || cap && RectTransformUtility.RectangleContainsScreenPoint(cap, screen, cam)) return shown[pinOption[p]];
            }
            return null;
        }
        // The option of member m whose object / door / trace is that UI object (a door prefers its own place over the shelf behind it).
        FieldPlaceOption OptionFor(int m, GameObject target)
        {
            var place = Placement; if (place == null || !target) return null;
            var list = m == Held && shown.Count > 0 ? (IReadOnlyList<FieldPlaceOption>)shown : place.OptionsFor(m);
            FieldPlaceOption best = null;
            foreach (var o in list)
            {
                if (!o.Anchor || !target.transform.IsChildOf(o.Anchor.transform)) continue;
                bool door = o.Kind == FieldSpotKind.Listen || o.Kind == FieldSpotKind.Gather;
                var hot = o.Anchor.GetComponent<ExplorationHotspot>(); bool atDoor = hot && hot.IsDoor;
                int score = (o.Enabled ? 2 : 0) + (door == atDoor ? 1 : 0), bestScore = best == null ? -1 : (best.Enabled ? 2 : 0) + ((best.Kind == FieldSpotKind.Listen || best.Kind == FieldSpotKind.Gather) == atDoor ? 1 : 0);
                if (score > bestScore) best = o;
            }
            return best;
        }
        GameObject Below(Vector2 screen)
        {
            var es = EventSystem.current; if (!es) return null;
            hits.Clear(); es.RaycastAll(new PointerEventData(es) { position = screen }, hits);
            foreach (var h in hits) if (h.gameObject && !h.gameObject.transform.IsChildOf(transform)) return h.gameObject;
            return null;
        }
        int PawnAt(Vector2 screen)
        {
            int best = -1; float y = float.MaxValue;
            foreach (var h in handles)
            {
                if (!h || !h.gameObject.activeInHierarchy || h.Index < 0 || !h.Contains(screen)) continue;
                // The front pawn (lower feet) wins where two overlap.
                float fy = h.Base ? h.Base.bounds.center.y : h.Body ? h.Body.bounds.min.y : 0; if (fy < y) { y = fy; best = h.Index; }
            }
            return best;
        }
        // The feet a screen point stands for, when it is on this room's floor band.
        bool FloorPoint(Vector2 screen, out Vector3 feet)
        {
            feet = default; var cam = WorldCamera; var frame = PawnFrame; var p = Presentation; if (!cam || !frame || !Arrival.Rooms) return false;
            var w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z - frame.position.z)));
            feet = frame.InverseTransformPoint(w); feet.z = 0;
            int room = Arrival.Rooms.CurrentRoom;
            return p ? p.OnFloor(room, feet) : ExplorationRoomPresentation.DefaultBand.Contains(feet);
        }

        // Object / door presses (the arrival panel's own hooks). Holding: that object's place (its option), or let go. Nothing held:
        // the plan does not change — '대원 말을 먼저 누르세요' and the free members' rings pulse; a finished object that still holds
        // finds opens them (collecting, not an assignment). The arcade door is Objects[3]: it is a door here, never the move popup.
        bool OnObjectPressed(int index)
        {
            var a = Arrival; var pl = Planner; if (!a || !pl || !pl.Placing) return false;
            var b = a.Objects != null && index >= 0 && index < a.Objects.Length ? a.Objects[index] : null;
            if (b && FieldPlacement.TryDoor(a, b, out _)) return OnDoorPressed(b);
            if (Held >= 0) { PressAnchor(b); return true; }
            if (a.Loot && a.Loot.Peek(index, out var s) && s.Complete && s.Loot.Values.Any(n => n > 0)) return false;
            PickFirst(); return true;
        }
        bool OnDoorPressed(Button b)
        {
            var a = Arrival; var pl = Planner; if (!a || !pl || !pl.Placing) return false;
            if (Held >= 0) { PressAnchor(b); return true; }
            // The den shelf's spot is the office door: finds left on a searched shelf open again while the den stays empty
            // (ExpeditionLootPanel.DenLeaveNote) — collecting, not an assignment (ExpeditionArrivalPanel.Inspect → Loot.Open).
            var t = a.Threat; int den = t ? t.DenSite : -1;
            if (t && b && b == FieldPlacement.SiteButton(a, den) && t.CanSearchSite(den) && a.Loot && a.Loot.Peek(den, out var shelf) && shelf.Complete && shelf.Loot.Values.Any(n => n > 0))
            { a.Inspect(den); return true; }
            PickFirst(); return true;
        }
        void PressAnchor(Button b)
        {
            var o = b ? OptionFor(Held, b.gameObject) : null;
            if (o == null) { Cancel(); return; }
            if (!o.Enabled) { Say(string.Format(Texts.Blocked, Name(o.Member), o.Blocked)); return; }
            Drop(o);
        }
        // A right press on a room hotspot (ExplorationHotspot): let go while holding; the trace's note; a door's heard log (the arcade
        // door too — never the move popup); an object's 07 window (ExpeditionArrivalPanel.Inspect).
        public bool RightPress(ExplorationHotspot hotspot)
        {
            var a = Arrival; if (!a || !hotspot || !hotspot.Button) return false;
            if (Held >= 0 || Carrying) { Cancel(); return true; }
            var b = hotspot.Button; var story = a.Story;
            if (story && b == story.Clue) { a.OpenPopup(string.Format(Texts.TraceTitle, story.ObserveLabel(ExpeditionNpcStory.ObservationId)), Texts.TraceBody); return true; }
            if (FieldPlacement.TryDoor(a, b, out int behind)) { ShowDoorLog(behind); return true; }
            int i = a.Objects != null ? Array.IndexOf(a.Objects, b) : -1;
            if (i >= 0) { a.Inspect(i); return true; }
            return false;
        }
        // '문 기록 · 복도 쪽': what members heard at this door on this visit (newest first, age-stamped), in the common popup frame.
        public void ShowDoorLog(int behind)
        {
            var a = Arrival; var pl = Planner; if (!a || !a.Rooms) return;
            int room = a.Rooms.CurrentRoom; var s = a.Threat ? a.Threat.State : null; var lines = new List<string>(); var tx = Texts;
            bool locked = behind == FieldSiteState.Storage && !a.Rooms.StorageUnlocked, den = behind == FieldSiteState.Den;
            if (locked) lines.Add(string.Format(tx.DoorLocked, a.Rooms.UnlockToolName, a.Rooms.MoveTurns(behind), a.Rooms.UnlockNoise));
            if (den) lines.Add(tx.DoorLogDen);
            var log = pl && pl.DoorLog != null ? pl.DoorLog.For(room, behind) : null;
            if (log != null && log.Count > 0) foreach (var e in log) lines.Add(string.Format(tx.DoorLogWho, Name(e.Member)) + pl.DoorLog.Line(e, s != null ? s.TurnsUsed : 0));
            else
            {
                // Nothing heard yet: only what this door allows now (the planner's rule: a locked storage or the sleeping den cannot be
                // listened at, the den is never walked through; the locked / den line above already says what the door does).
                var lf = pl ? pl.Facts() as IFieldListenFacts : null;
                bool hear = s != null && !s.Asleep && (lf == null || lf.ListenBlock(behind) == FieldPause.None);
                if (hear) lines.Add(tx.DoorLogEmpty);
                else if (!locked && !den && FieldPlacement.IsMoveDoor(a, room, behind)) lines.Add(tx.DoorLogAsleep);
                else if (lines.Count == 0) lines.Add(tx.DoorLogQuiet);
            }
            a.OpenPopup(string.Format(tx.DoorLogTitle, RoomName(behind)), string.Join("\n", lines));
        }
        void OnTurnStarting() { Cancel(); foreach (var w in walkers) if (w) w.Snap(); }

        // ---- carrying a pawn (a drag from its handle) ----
        public bool BeginCarry(FieldPawnHandle h, Vector2 screen)
        {
            if (!h || h.Index < 0 || !Hold(h.Index)) return false;
            Carrying = true; carryAt = screen; return true;
        }
        public void Carry(Vector2 screen) { if (Carrying) carryAt = screen; }
        public void EndCarry(Vector2 screen)
        {
            if (!Carrying) return;
            Carrying = false; if (carry && carry.activeSelf) carry.SetActive(false);
            var place = Placement; if (Held < 0 || place == null || !place.Ready) { Cancel(); return; }
            Resolve(screen);
        }

        // ---- every frame ----
        void Update()
        {
            var a = Arrival; if (!a || Held < 0 && !Carrying) return;
            // Esc lets go. The arrival panel reads the same key first and asks about going home: that question was not meant, so
            // it is put away in the same frame (was FieldQuickAssign.Update).
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                bool asked = a.Popup && a.Popup.activeSelf && !popupBefore;
                Cancel(); if (asked) a.ClosePopup(); return;
            }
            // A press away from the room (not a card, not this board's buttons) lets go; the button pressed still acts.
            var mouse = Mouse.current;
            if (LetGoOutside && !Carrying && mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
            {
                var top = Top(mouse.position.ReadValue());
                if (!top || !top.transform.IsChildOf(transform) && !top.GetComponentInParent<FieldMemberCardInput>()) Cancel();
            }
        }
        static GameObject Top(Vector2 screen)
        {
            var es = EventSystem.current; if (!es) return null;
            hits.Clear(); es.RaycastAll(new PointerEventData(es) { position = screen }, hits);
            return hits.Count > 0 ? hits[0].gameObject : null;
        }
        void LateUpdate()
        {
            var a = Arrival; if (!a) return;
            var pl = Planner; var rooms = a.Rooms;
            bool placing = a.IsOpen && pl && pl.Placing && rooms && a.Threat.State != null;
            if (!placing) { if (Held >= 0 || Carrying) Cancel(); HideAll(); CardInputs(false); firstPawn = null; pawnCount = -1; popupBefore = a.Popup && a.Popup.activeSelf; return; }
            if (!hooked || a.Pressed != onPressed || rooms.DoorPressed != onDoor) Hook();
            var main = transform.parent; if (main && transform.GetSiblingIndex() != main.childCount - 1) Order();
            SyncPawns();
            if (a.InTransit)
            {
                if (!wasTransit) { foreach (var w in walkers) if (w) w.Stop(); }
                wasTransit = true; if (Held >= 0 || Carrying) Cancel(); HideAll(); CardInputs(false); popupBefore = a.Popup.activeSelf; return;
            }
            if (wasTransit) { wasTransit = false; ResetFloors(); }
            bool meeting = Meeting(a);
            foreach (var w in walkers) if (w) w.Frozen = meeting;
            var place = Placement; bool ready = place.Ready;
            bool visible = ready && a.Main && a.Main.interactable && a.Main.alpha >= .5f;
            if (!ready && (Held >= 0 || Carrying)) Cancel();
            if (Held >= 0 && !Alive(Held)) Cancel();
            int ver = place.Version, room = rooms.CurrentRoom;
            if (!meeting && (dirty || ver != arrangedVersion || room != arrangedRoom)) Arrange(room, ver);
            SetCatcher(visible && Held >= 0);
            PlaceHandles(visible);
            ShowOptions(visible && Held >= 0, ver, room);
            PlaceCarry(visible && Carrying);
            ShowChips(visible && Held < 0 && !Carrying, ver);
            ShowTags(visible);
            ShowProgress(visible && Held < 0 && !Carrying);
            CardInputs(!meeting);
            popupBefore = a.Popup && a.Popup.activeSelf;
        }

        // ---- where the pawns stand (the plan's spots; free members keep their floor point) ----
        void SyncPawns()
        {
            var pawns = Arrival.PartyPawns; var first = pawns.Count > 0 ? pawns[0] : null;
            if (first == firstPawn && pawns.Count == pawnCount) return;
            firstPawn = first; pawnCount = pawns.Count; walkers.Clear(); slots.Clear();
            for (int i = 0; i < pawns.Count; i++)
            {
                var p = pawns[i]; if (!p) { walkers.Add(null); continue; }
                // TryGetComponent, not '??': in the Editor a missing component comes back as Unity's fake null, which '??' keeps.
                if (!p.TryGetComponent(out FieldPawnWalker w)) w = p.AddComponent<FieldPawnWalker>();
                w.Arrival = Arrival; walkers.Add(w);
                var facing = p.GetComponent<PawnFacing>(); if (facing) facing.SortingBias = -i; // pawns on one line: member order breaks the tie
            }
            Grow(); ResetFloors(); Cancel();
        }
        void Grow()
        {
            int n = Arrival ? Arrival.PartyPawns.Count : 0; if (floor.Length >= n) return;
            Array.Resize(ref floor, n); Array.Resize(ref target, n); Array.Resize(ref look, n); Array.Resize(ref hasFloor, n); Array.Resize(ref manual, n); Array.Resize(ref placed, n);
        }
        // A new room (or a new visit): every pawn stands where the move left it.
        void ResetFloors()
        {
            Grow(); var pawns = Arrival.PartyPawns;
            for (int i = 0; i < floor.Length; i++) { hasFloor[i] = i < pawns.Count && pawns[i]; if (hasFloor[i]) floor[i] = pawns[i].transform.localPosition; manual[i] = placed[i] = false; }
            dirty = true;
        }
        // Placed members go to their spot; a member let go from a spot walks to the nearest free idle spot (a free pawn never stands
        // at a work spot); free members keep their floor point unless someone now stands there.
        void Arrange(int room, int ver)
        {
            dirty = false; arrangedVersion = ver; arrangedRoom = room; Grow();
            var a = Arrival; var place = Placement; var p = Presentation; var pawns = a.PartyPawns; var taken = new List<Vector3>();
            var now = new bool[floor.Length];
            for (int m = 0; m < pawns.Count; m++)
            {
                look[m] = float.NaN; if (!pawns[m] || !Alive(m)) continue;
                if (place.TryGetSpot(m, out var spot) && SpotFeet(spot, out var feet, out float at)) { target[m] = feet; look[m] = at; now[m] = true; taken.Add(feet); }
            }
            for (int m = 0; m < pawns.Count; m++)
            {
                if (!pawns[m] || !Alive(m) || now[m]) continue;
                var here = pawns[m].transform.localPosition;
                Vector3 fp = placed[m] && !manual[m] && p ? p.NearestIdle(room, here, taken) : hasFloor[m] ? floor[m] : here;
                if (p) fp = p.NearestClear(room, fp, taken);
                floor[m] = fp; hasFloor[m] = true; manual[m] = false; target[m] = fp; taken.Add(fp);
            }
            for (int m = 0; m < pawns.Count; m++)
            {
                if (!pawns[m] || !Alive(m)) continue;
                placed[m] = now[m];
                var w = m < walkers.Count ? walkers[m] : null; if (w) w.WalkTo(target[m], look[m]);
            }
        }
        // The world feet of a place: the room's authored spot, else the object's painted bottom edge clamped onto the floor.
        bool SpotFeet(FieldSpotRef spot, out Vector3 feet, out float lookAtX)
        {
            var p = Presentation; if (p && p.TrySpot(spot.Room, spot.Key, spot.Slot, out feet, out lookAtX)) return true;
            feet = default; lookAtX = float.NaN;
            var anchor = AnchorOf(spot); var hot = anchor ? anchor.GetComponent<ExplorationHotspot>() : null; if (!hot) return false;
            var r = hot.SourceObjectPixels; if (r.width <= 0) return false;
            float x = (r.x + r.width * .5f) / 1672f * 19.2f - 9.6f, y = 5.4f - (r.y + r.height) / 941f * 10.8f;
            var at = new Vector2(x - .6f + 1.25f * spot.Slot, y - .8f - .08f * spot.Slot);
            feet = p ? p.ClampFloor(spot.Room, at) : (Vector3)at; lookAtX = x; return true;
        }
        Button AnchorOf(FieldSpotRef spot)
        {
            var a = Arrival; string key = spot.Key ?? "";
            if (key.StartsWith("search:") && int.TryParse(key.Substring(7), out int site)) return FieldPlacement.SiteButton(a, site);
            if (key.StartsWith("door:") && int.TryParse(key.Substring(5), out int door)) return FieldPlacement.DoorButton(a, spot.Room, door);
            if (key.StartsWith("observe:")) return a.Story ? a.Story.Clue : null;
            return null;
        }
        public Vector3 TargetOf(int member) => member >= 0 && member < target.Length ? target[member] : Vector3.zero;
        public FieldPawnWalker WalkerOf(int member) => member >= 0 && member < walkers.Count ? walkers[member] : null;

        // ---- the handles over the pawns ----
        void PlaceHandles(bool show)
        {
            var a = Arrival; var pawns = a.PartyPawns; int n = show ? pawns.Count : 0;
            while (handles.Count < n && handles.Count > 0 && handles[0]) { var c = Clone(handles[0]); c.Board = this; c.Role = FieldPawnHandle.Kind.Pawn; handles.Add(c); }
            var order = new List<(float y, FieldPawnHandle h)>();
            for (int i = 0; i < handles.Count; i++)
            {
                var h = handles[i]; if (!h) continue;
                var pawn = i < n ? pawns[i] : null; bool on = pawn && pawn.activeInHierarchy && Alive(i);
                if (h.gameObject.activeSelf != on) h.gameObject.SetActive(on);
                if (!on) { h.Index = -1; continue; }
                h.Index = i; Parts(pawn, out var body, out var bas); h.Body = body; h.Base = bas;
                Fit(h.Rect, Union(VisibleBounds(body), bas ? bas.bounds : VisibleBounds(body)));
                order.Add((bas ? bas.bounds.center.y : pawn.transform.position.y, h));
            }
            // The front pawn (lower feet) last: it takes a press where two overlap.
            order.Sort((x, y) => y.y.CompareTo(x.y));
            for (int k = 0; k < order.Count; k++) { var h = order[k].h; int want = k; if (h.transform.GetSiblingIndex() != want) h.transform.SetSiblingIndex(want); }
        }
        public Button HandleOf(int member) { foreach (var h in handles) if (h && h.gameObject.activeInHierarchy && h.Index == member) return h.Button; return null; }
        public FieldPawnHandle HandleComponentOf(int member) { foreach (var h in handles) if (h && h.gameObject.activeInHierarchy && h.Index == member) return h; return null; }
        static void Parts(GameObject pawn, out SpriteRenderer body, out SpriteRenderer bas)
        {
            body = null; bas = null; if (!pawn) return;
            var b = pawn.transform.Find("Body"); body = b ? b.GetComponent<SpriteRenderer>() : null;
            var s = pawn.transform.Find("Base"); bas = s ? s.GetComponent<SpriteRenderer>() : null;
        }
        // The visible pixels of a body sprite in world space (PartyRoster.BodyVisiblePixels; the sprite rect when unknown).
        public Bounds VisibleBounds(SpriteRenderer body)
        {
            if (!body) return default; var s = body.sprite; if (!s) return body.bounds;
            var roster = Roster; if (!roster) { var c = FindAnyObjectByType<SettlementController>(); if (c) roster = Roster = c.Roster; }
            var px = roster ? roster.VisiblePixelsFor(s) : new Rect(0, 0, s.rect.width, s.rect.height); if (px.width <= 0 || px.height <= 0) return body.bounds;
            float ppu = s.pixelsPerUnit; var pivot = s.pivot;
            float x0 = (px.xMin - pivot.x) / ppu, x1 = (px.xMax - pivot.x) / ppu, y0 = (px.yMin - pivot.y) / ppu, y1 = (px.yMax - pivot.y) / ppu;
            if (body.flipX) { float t = -x0; x0 = -x1; x1 = t; }
            if (body.flipY) { float t = -y0; y0 = -y1; y1 = t; }
            var tr = body.transform; var b = new Bounds(tr.TransformPoint(new Vector3(x0, y0, 0)), Vector3.zero); b.Encapsulate(tr.TransformPoint(new Vector3(x1, y1, 0))); return b;
        }
        static Bounds Union(Bounds a, Bounds b) { a.Encapsulate(b); return a; }
        // Put a centre-anchored rect over world bounds (its parent's space; the canvas camera draws the UI).
        void Fit(RectTransform r, Bounds world)
        {
            var parent = r.parent as RectTransform; var cam = WorldCamera; if (!parent || !cam) return;
            Vector2 s0 = RectTransformUtility.WorldToScreenPoint(cam, world.min), s1 = RectTransformUtility.WorldToScreenPoint(cam, world.max);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, s0, UiCamera, out var l0) || !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, s1, UiCamera, out var l1)) return;
            var min = Vector2.Min(l0, l1); var max = Vector2.Max(l0, l1); var size = max - min; var center = (min + max) * .5f;
            var at = new Vector3(center.x + (r.pivot.x - .5f) * size.x, center.y + (r.pivot.y - .5f) * size.y, 0);
            if ((r.localPosition - at).sqrMagnitude > .01f) r.localPosition = at;
            if ((r.sizeDelta - size).sqrMagnitude > .01f) r.sizeDelta = size;
        }
        // A screen point in `space` (the local point), or false.
        bool Local(RectTransform space, Vector3 world, out Vector2 local)
        {
            local = default; var cam = WorldCamera; if (!space || !cam) return false;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(space, RectTransformUtility.WorldToScreenPoint(cam, world), UiCamera, out local);
        }
        T Clone<T>(T proto) where T : Component
        {
            var go = Instantiate(proto.gameObject, proto.transform.parent); go.name = proto.name.Split('_')[0] + "_" + go.transform.GetSiblingIndex(); go.SetActive(false);
            return go.GetComponent<T>();
        }

        // ---- the held pawn's places: silhouettes (world) + their handles + pins ----
        void ShowOptions(bool show, int ver, int room)
        {
            if (!show) { if (shown.Count > 0 || ghostOption.Count > 0) { shown.Clear(); } HideGhosts(); HidePins(); shownVersion = -1; return; }
            if (ver != shownVersion || Held != shownHeld || room != shownRoom)
            {
                shownVersion = ver; shownHeld = Held; shownRoom = room;
                shown.Clear(); shown.AddRange(Placement.OptionsFor(Held));
                ghostOption.Clear(); pinOption.Clear();
                for (int i = 0; i < shown.Count; i++) { if (shown[i].Enabled) ghostOption.Add(i); pinOption.Add(i); }
            }
            var pawn = Held < Arrival.PartyPawns.Count ? Arrival.PartyPawns[Held] : null;
            // Silhouettes: a see-through copy of the held pawn at each spot it can take.
            int g = 0;
            for (int k = 0; k < ghostOption.Count; k++)
            {
                var o = shown[ghostOption[k]]; if (!SpotFeet(o.Spot, out var feet, out float at)) { continue; }
                var ghost = Ghost(g); if (!ghost) break;
                var h = GhostHandle(g); bool hover = h && h.Hovered || PinHovered(ghostOption[k]);
                Dress(ghost, ghostColors[g], pawn, feet, at, hover ? GhostHoverAlpha : GhostAlpha, 0);
                if (h)
                {
                    if (!h.gameObject.activeSelf) h.gameObject.SetActive(true);
                    h.Index = g; Parts(ghost, out var body, out var bas); h.Body = body; h.Base = bas;
                    Fit(h.Rect, Union(VisibleBounds(body), bas ? bas.bounds : VisibleBounds(body)));
                    SetGhostOption(g, ghostOption[k]);
                }
                g++;
            }
            for (int i = g; i < ghosts.Count; i++) if (ghosts[i] && ghosts[i].activeSelf) ghosts[i].SetActive(false);
            for (int i = g; i < ghostHandles.Count; i++) if (ghostHandles[i] && ghostHandles[i].gameObject.activeSelf) { ghostHandles[i].gameObject.SetActive(false); ghostHandles[i].Index = -1; }
            TrimGhostOptions(g);
            // Pins: one per place at its object's marker (two places on one door side by side), the label or the reason as caption.
            while (pins.Count < pinOption.Count && pins.Count > 0 && pins[0]) pins.Add(Clone(pins[0]));
            var mouse = Mouse.current; Vector2 pointer = Carrying ? carryAt : mouse != null ? mouse.position.ReadValue() : new Vector2(-1e5f, -1e5f); var cam = UiCamera;
            var shared = new Dictionary<Button, int>(); foreach (int i in pinOption) { var anchor = shown[i].Anchor; if (anchor) shared[anchor] = shared.TryGetValue(anchor, out int c) ? c + 1 : 1; }
            var placedOn = new Dictionary<Button, int>();
            for (int p = 0; p < pins.Count; p++)
            {
                var pin = pins[p]; if (!pin) continue;
                if (p >= pinOption.Count) { pin.Hide(); continue; }
                var o = shown[pinOption[p]]; var marker = MarkerOf(o.Anchor); if (!marker) { pin.Hide(); continue; }
                var space = (RectTransform)pin.transform.parent; var r = RectIn(marker, space); float top = Above(r, space);
                int count = shared.TryGetValue(o.Anchor, out int c2) ? c2 : 1; int nth = placedOn.TryGetValue(o.Anchor, out int c3) ? c3 : 0; placedOn[o.Anchor] = nth + 1;
                float dx = count > 1 ? (nth - (count - 1) * .5f) * PinSpread : 0;
                pin.ShowPin(o.Key + "#" + o.Spot.Slot + "#" + o.Kind, o.Enabled ? o.Glyph : ActionGlyph.Kind.None, new Vector2(r.center.x + dx, top + PinGap), o.Enabled ? o.Label : o.Blocked, o.Enabled);
                var cap = pin.CaptionArea;
                pin.Hover = RectTransformUtility.RectangleContainsScreenPoint(pin.HitArea, pointer, cam) || cap && RectTransformUtility.RectangleContainsScreenPoint(cap, pointer, cam) || GhostHovered(pinOption[p]);
            }
        }
        readonly List<int> ghostShows = new List<int>();
        void SetGhostOption(int g, int option) { while (ghostShows.Count <= g) ghostShows.Add(-1); ghostShows[g] = option; }
        void TrimGhostOptions(int count) { for (int i = count; i < ghostShows.Count; i++) ghostShows[i] = -1; }
        FieldPlaceOption GhostOption(FieldPawnHandle h) { int g = h ? h.Index : -1; return g >= 0 && g < ghostShows.Count && ghostShows[g] >= 0 && ghostShows[g] < shown.Count ? shown[ghostShows[g]] : null; }
        bool PinHovered(int option) { for (int p = 0; p < pins.Count && p < pinOption.Count; p++) if (pinOption[p] == option && pins[p] && pins[p].Visible && pins[p].Hover) return true; return false; }
        bool GhostHovered(int option) { for (int g = 0; g < ghostHandles.Count && g < ghostShows.Count; g++) if (ghostShows[g] == option && ghostHandles[g] && ghostHandles[g].gameObject.activeInHierarchy && ghostHandles[g].Hovered) return true; return false; }
        FieldPawnHandle GhostHandle(int g)
        {
            while (ghostHandles.Count <= g && ghostHandles.Count > 0 && ghostHandles[0]) { var c = Clone(ghostHandles[0]); c.Board = this; c.Role = FieldPawnHandle.Kind.Ghost; ghostHandles.Add(c); }
            return g < ghostHandles.Count ? ghostHandles[g] : null;
        }
        // The silhouette Button showing that place (key "search:0", slot; slot < 0: any), or null.
        public Button GhostOf(string key, int slot)
        {
            for (int g = 0; g < ghostHandles.Count && g < ghostShows.Count; g++)
            {
                var h = ghostHandles[g]; if (!h || !h.gameObject.activeInHierarchy || ghostShows[g] < 0 || ghostShows[g] >= shown.Count) continue;
                var o = shown[ghostShows[g]]; if (o.Key == key && (slot < 0 || o.Spot.Slot == slot)) return h.Button;
            }
            return null;
        }
        public Button GhostOf(FieldPlaceOption o) => o == null ? null : GhostOf(o.Key, o.Spot.Slot);
        public FieldTargetGlow PinOf(string key)
        {
            for (int p = 0; p < pins.Count && p < pinOption.Count; p++) if (pins[p] && pins[p].Visible && shown[pinOption[p]].Key == key) return pins[p];
            return null;
        }
        void HidePins() { foreach (var p in pins) if (p) p.Hide(); pinOption.Clear(); }
        void HideGhosts()
        {
            foreach (var g in ghosts) if (g && g.activeSelf) g.SetActive(false);
            foreach (var h in ghostHandles) if (h && h.gameObject.activeSelf) { h.gameObject.SetActive(false); h.Index = -1; }
            ghostOption.Clear(); TrimGhostOptions(0);
        }
        // World silhouettes live under ExpeditionWorld/PawnGhosts (never under the pawn root: the resident reads its pawns).
        Transform GhostRoot
        {
            get
            {
                if (ghostRoot) return ghostRoot; var world = Arrival ? Arrival.World : null; if (!world) return null;
                ghostRoot = world.transform.Find(GhostRootName);
                if (!ghostRoot) { var go = new GameObject(GhostRootName); go.layer = world.layer; go.transform.SetParent(world.transform, false); ghostRoot = go.transform; }
                return ghostRoot;
            }
        }
        GameObject Ghost(int g)
        {
            while (ghosts.Count <= g)
            {
                var made = NewGhost("Ghost_" + ghosts.Count, out var colors); if (!made) return null;
                ghosts.Add(made); ghostColors.Add(colors);
            }
            return ghosts[g];
        }
        GameObject NewGhost(string name, out (SpriteRenderer, Color)[] colors)
        {
            colors = null; var root = GhostRoot; var prefab = GhostPrefab ? GhostPrefab : Arrival ? Arrival.PawnPrefab : null; if (!root || !prefab) return null;
            var g = Instantiate(prefab, root); g.name = name;
            if (!GhostPrefab)
            {
                foreach (var s in g.GetComponents<PawnGroundShadow>()) Destroy(s);
                var cast = g.transform.Find("CastShadow"); if (cast) Destroy(cast.gameObject);
            }
            colors = g.GetComponentsInChildren<SpriteRenderer>(true).Select(r => (r, r.color)).ToArray();
            g.SetActive(false); return g;
        }
        // The held pawn's look (its body sprite, size, feet) at `feet`, turned to `lookAtX`, see-through.
        void Dress(GameObject ghost, (SpriteRenderer R, Color C)[] colors, GameObject pawn, Vector3 feet, float lookAtX, float alpha, int bias)
        {
            if (!ghost || !pawn) return;
            Parts(pawn, out var src, out _); Parts(ghost, out var dst, out _);
            if (src && dst)
            {
                if (dst.sprite != src.sprite) dst.sprite = src.sprite;
                dst.transform.localPosition = src.transform.localPosition; dst.transform.localScale = src.transform.localScale;
            }
            var frame = PawnFrame; var world = frame ? frame.TransformPoint(feet) : feet;
            bool moved = (ghost.transform.position - world).sqrMagnitude > 1e-6f || !ghost.activeSelf;
            ghost.transform.position = world;
            if (!ghost.activeSelf) ghost.SetActive(true);
            var facing = ghost.GetComponent<PawnFacing>();
            if (facing) { facing.SortingBias = bias; if (moved) { bool right = !float.IsNaN(lookAtX) && Mathf.Abs(lookAtX - feet.x) > .15f ? lookAtX > feet.x : src && facing.Body ? src.flipX != facing.ArtworkFacesRight : true; facing.Face(right); } }
            if (colors != null) foreach (var (r, c) in colors) if (r) { var k = c; k.a = c.a * alpha; if (r.color != k) r.color = k; }
        }
        void PlaceCarry(bool show)
        {
            if (!show) { if (carry && carry.activeSelf) carry.SetActive(false); return; }
            if (!carry) { carry = NewGhost("Carry", out carryColors); if (!carry) return; }
            var pawn = Held >= 0 && Held < Arrival.PartyPawns.Count ? Arrival.PartyPawns[Held] : null;
            if (!FloorPoint(carryAt, out var feet)) FloorPointClamped(carryAt, out feet);
            Dress(carry, carryColors, pawn, feet, float.NaN, CarryAlpha, CarrySortingBias);
        }
        // Any screen point as feet clamped onto this room's floor band (the carried copy never leaves the floor).
        void FloorPointClamped(Vector2 screen, out Vector3 feet)
        {
            feet = default; var cam = WorldCamera; var frame = PawnFrame; if (!cam || !frame) return;
            var w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z - frame.position.z)));
            Vector2 local = frame.InverseTransformPoint(w); var p = Presentation; int room = Arrival.Rooms ? Arrival.Rooms.CurrentRoom : 0;
            feet = p ? p.ClampFloor(room, local) : (Vector3)local;
        }

        // ---- role chips under a helper pawn (and 이동 · 귀 대기 under a lone member at a door) ----
        void WireRow(int r)
        {
            var row = rows[r]; var buttons = row ? row.GetComponentsInChildren<Button>(true) : new Button[0];
            for (int i = 0; i < buttons.Length; i++) { int chip = i, index = r; buttons[i].onClick.AddListener(() => OnChip(index, chip)); }
            while (rowMember.Count <= r) rowMember.Add(-1);
            while (rowButtons.Count <= r) rowButtons.Add(null); rowButtons[r] = buttons;
        }
        readonly List<Button[]> rowButtons = new List<Button[]>();
        readonly List<(int Member, IReadOnlyList<FieldRoleChip> Chips)> chipLists = new List<(int, IReadOnlyList<FieldRoleChip>)>(); int chipVersion = -1;
        void OnChip(int row, int chip)
        {
            int m = row < rowMember.Count ? rowMember[row] : -1; var place = Placement; if (m < 0 || place == null) return;
            if (place.Choose(m, chip)) dirty = true;
        }
        void ShowChips(bool show, int ver)
        {
            var a = Arrival; var place = Placement; int r = 0; var mouse = Mouse.current; var cam = UiCamera;
            Vector2 pointer = mouse != null ? mouse.position.ReadValue() : new Vector2(-1e5f, -1e5f);
            chipWhy = null;
            // Who has chips and which (re-read only when the placing version changes).
            if (show && ver != chipVersion)
            {
                chipVersion = ver; chipLists.Clear();
                for (int m = 0; m < a.PartyPawns.Count; m++) { if (!a.PartyPawns[m] || !Alive(m)) continue; var list = place.ChipsFor(m); if (list != null && list.Count > 0) chipLists.Add((m, list)); }
            }
            if (!show) chipVersion = -1;
            if (show && rows.Count > 0 && rows[0])
                foreach (var (m, list) in chipLists)
                {
                    var pawn = m < a.PartyPawns.Count ? a.PartyPawns[m] : null; if (!pawn || !Alive(m)) continue;
                    while (rows.Count <= r) { var c = Clone(rows[0]); rows.Add(c); WireRow(rows.Count - 1); }
                    var row = rows[r]; while (rowMember.Count <= r) rowMember.Add(-1); rowMember[r] = m;
                    if (!row.gameObject.activeSelf) row.gameObject.SetActive(true);
                    var buttons = r < rowButtons.Count && rowButtons[r] != null ? rowButtons[r] : row.GetComponentsInChildren<Button>(true);
                    for (int i = 0; i < buttons.Length; i++)
                    {
                        var b = buttons[i]; bool on = i < list.Count; if (b.gameObject.activeSelf != on) b.gameObject.SetActive(on); if (!on) continue;
                        var chip = list[i]; var text = b.GetComponentInChildren<Text>(true); if (text && text.text != chip.Label) text.text = chip.Label;
                        var paper = b.targetGraphic ? b.targetGraphic : b.GetComponent<Graphic>();
                        var col = chip.On ? ChipOn : chip.Enabled ? ChipOff : ChipDisabled; if (paper && paper.color != col) paper.color = col;
                        if (text) { var tc = chip.Enabled || chip.On ? ChipText : ChipDisabledText; if (text.color != tc) text.color = tc; }
                        if (b.interactable != (chip.Enabled || chip.On)) b.interactable = chip.Enabled || chip.On;
                        if (!chip.Enabled && !string.IsNullOrEmpty(chip.Why) && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)b.transform, pointer, cam)) chipWhy = ((RectTransform)b.transform, chip.Why);
                    }
                    PlaceRow(row, pawn); r++;
                }
            for (int i = r; i < rows.Count; i++) { if (rows[i] && rows[i].gameObject.activeSelf) rows[i].gameObject.SetActive(false); if (i < rowMember.Count) rowMember[i] = -1; }
        }
        (RectTransform Chip, string Why)? chipWhy;
        // Under the pawn's base; above its head when that would reach into the member tray (the room area's bottom).
        void PlaceRow(RectTransform row, GameObject pawn)
        {
            var space = row.parent as RectTransform; Parts(pawn, out var body, out var bas); if (!space || !body) return;
            var foot = bas ? bas.bounds : VisibleBounds(body); var head = VisibleBounds(body);
            if (!Local(space, new Vector3(foot.center.x, foot.min.y, 0), out var under) || !Local(space, new Vector3(head.center.x, head.max.y, 0), out var over)) return;
            var size = row.rect.size; float floorY = RoomBottom(space);
            bool below = under.y - ChipGap - size.y >= floorY;
            var anchor = below ? new Vector2(under.x, under.y - ChipGap - size.y) : new Vector2(over.x, over.y + ChipGap);
            PlaceBottom(row, anchor);
        }
        // The bottom edge of the room area in `space` (the catcher covers the room, above the member tray).
        float RoomBottom(RectTransform space)
        {
            if (Catcher) return RectIn(Catcher.rectTransform, space).yMin;
            return space.rect.yMin;
        }
        // Put r so its bottom centre is at `p` (its parent's space).
        static void PlaceBottom(RectTransform r, Vector2 p)
        {
            var size = r.rect.size; var at = new Vector3(p.x + (r.pivot.x - .5f) * size.x, p.y + r.pivot.y * size.y, 0);
            if ((r.localPosition - at).sqrMagnitude > .01f) r.localPosition = at;
        }
        static void PlaceTop(RectTransform r, Vector2 p)
        {
            var size = r.rect.size; var at = new Vector3(p.x + (r.pivot.x - .5f) * size.x, p.y + (r.pivot.y - 1) * size.y, 0);
            if ((r.localPosition - at).sqrMagnitude > .01f) r.localPosition = at;
        }
        public RectTransform ChipRowOf(int member) { for (int r = 0; r < rows.Count && r < rowMember.Count; r++) if (rowMember[r] == member && rows[r] && rows[r].gameObject.activeInHierarchy) return rows[r]; return null; }
        public Button ChipOf(int member, int chip) { var row = ChipRowOf(member); var b = row ? row.GetComponentsInChildren<Button>(false) : null; return b != null && chip >= 0 && chip < b.Length ? b[chip] : null; }

        // ---- 사물 위 수색 진행 칸 (시안 02 '상자 위 칸 = 수색 진행 (노랑 = 이번 턴)'): over an object someone is placed on (its
        // turns, this turn's cell light) or one half searched, in this room; hidden while a pawn is held (the pins stand there then) ----
        void ShowProgress(bool show)
        {
            var a = Arrival; int n = 0;
            if (show && progress.Count > 0 && progress[0] && a.Loot && a.Objects != null)
            {
                var k = Planner ? Planner.Current : null;
                for (int site = 0; site < a.Loot.Sites.Length && site < a.Objects.Length; site++)
                {
                    var button = a.Objects[site]; if (!button || !button.gameObject.activeInHierarchy || !a.Loot.IsSiteInCurrentRoom(site)) continue;
                    var run = k != null ? k.RunFor(site) : null; bool has = a.Loot.Peek(site, out var s);
                    if (has && s.Complete) continue;
                    int count, done, next;
                    if (run != null) { count = run.Required; done = run.Before; next = Mathf.Max(0, run.After - run.Before); }
                    else if (has && s.Progress > 0) { count = s.Required; done = s.Progress; next = 0; }
                    else continue;
                    var marker = MarkerOf(button); if (!marker) continue;
                    var strip = Strip(n++); if (!strip) break;
                    var cells = strip.GetComponentInChildren<FieldSearchPips>(true); if (cells) cells.Set(count, done, next);
                    float w = (cells ? cells.PreferredWidth : 60) + 2 * ProgressPad; if (Mathf.Abs(strip.sizeDelta.x - w) > .5f) strip.sizeDelta = new Vector2(w, strip.sizeDelta.y);
                    var space = (RectTransform)strip.parent; var r = RectIn(marker, space);
                    PlaceBottom(strip, new Vector2(r.center.x, Above(r, space) + PinGap));
                }
            }
            for (int i = n; i < progress.Count; i++) if (progress[i] && progress[i].gameObject.activeSelf) progress[i].gameObject.SetActive(false);
        }
        RectTransform Strip(int i)
        {
            while (progress.Count <= i && progress.Count > 0 && progress[0]) progress.Add(Clone(progress[0]));
            var s = i < progress.Count ? progress[i] : null; if (s && !s.gameObject.activeSelf) s.gameObject.SetActive(true); return s;
        }

        // ---- paper tags: a pawn's task on hover (every pawn while Alt is held), a door's newest heard line, a chip's reason ----
        void ShowTags(bool show)
        {
            var a = Arrival; int t = 0;
            if (show && tags.Count > 0 && tags[0])
            {
                var kb = Keyboard.current; bool alt = kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);
                var place = Placement;
                foreach (var h in handles)
                {
                    if (!h || !h.gameObject.activeInHierarchy || h.Index < 0 || Carrying && h.Index == Held || !(alt || h.Hovered)) continue;
                    var body = h.Body; if (!body) continue; var head = VisibleBounds(body);
                    var tag = Tag(t++); if (!tag) break; SetTag(tag, place.Describe(h.Index));
                    if (Local((RectTransform)tag.parent, new Vector3(head.center.x, head.max.y, 0), out var p)) PlaceBottom(tag, p + new Vector2(0, TagGap));
                }
                var pl = Planner; var rooms = a.Rooms; var s = a.Threat ? a.Threat.State : null;
                if (pl && pl.DoorLog != null && rooms && s != null)
                    foreach (int door in FieldPlacement.Behind(rooms.CurrentRoom))
                    {
                        var b = FieldPlacement.DoorButton(a, rooms.CurrentRoom, door); var hot = b ? b.GetComponent<ExplorationHotspot>() : null;
                        if (!hot || !hot.IsExpanded || !pl.DoorLog.TryLatest(rooms.CurrentRoom, door, out var latest)) continue;
                        var under = hot.DoorStatus && hot.DoorStatus.alpha > .5f && hot.DoorStatus.gameObject.activeInHierarchy ? (RectTransform)hot.DoorStatus.transform : hot.Caption ? (RectTransform)hot.Caption.transform : (RectTransform)b.transform;
                        var tag = Tag(t++); if (!tag) break; SetTag(tag, pl.DoorLog.Line(latest, s.TurnsUsed));
                        var r = RectIn(under, (RectTransform)tag.parent); PlaceTop(tag, new Vector2(r.center.x, r.yMin - 4));
                    }
                if (chipWhy.HasValue)
                {
                    var tag = Tag(t++);
                    if (tag) { SetTag(tag, chipWhy.Value.Why); var r = RectIn(chipWhy.Value.Chip, (RectTransform)tag.parent); PlaceBottom(tag, new Vector2(r.center.x, r.yMax + 4)); }
                }
            }
            for (int i = t; i < tags.Count; i++) if (tags[i] && tags[i].gameObject.activeSelf) tags[i].gameObject.SetActive(false);
        }
        RectTransform Tag(int i)
        {
            while (tags.Count <= i && tags.Count > 0 && tags[0]) tags.Add(Clone(tags[0]));
            var tag = i < tags.Count ? tags[i] : null; if (tag && !tag.gameObject.activeSelf) tag.gameObject.SetActive(true); return tag;
        }
        static void SetTag(RectTransform tag, string text) { var label = tag.GetComponentInChildren<Text>(true); if (label && label.text != text) label.text = text; }
        // What a pawn's hover tag says now (tests).
        public string TagText(int member) { var place = Placement; return place != null ? place.Describe(member) : ""; }
        public bool TagShown(string text) { foreach (var t in tags) if (t && t.gameObject.activeInHierarchy) { var l = t.GetComponentInChildren<Text>(true); if (l && l.text == text) return true; } return false; }

        // ---- the tutorial's next click (SettlementTutorialGuide) ----
        // The next real press toward `goalKey` ("search:0", "door:1", "observe:ID"): the pawn to pick up, its silhouette, the second
        // pawn and its co-op silhouette (coop), or '턴 진행' once the plan does it. Null when nothing leads there from here.
        public Button GuideTarget(string goalKey, bool coop, out FieldGuideStep step)
        {
            step = FieldGuideStep.None; var a = Arrival; var place = Placement; var pl = Planner;
            if (!a || place == null || !place.Ready || !pl || string.IsNullOrEmpty(goalKey)) return null;
            var alive = Enumerable.Range(0, a.Participants.Count).Where(Alive).ToList(); if (alive.Count == 0) return null;
            if (goalKey.StartsWith("door:") && int.TryParse(goalKey.Substring(5), out int door))
            {
                if (a.Rooms.HasQueuedMove && a.Rooms.QueuedRoom == door) { step = FieldGuideStep.PressTurn; return pl.TurnButton; }
                var at = place.AtDoor(door);
                if (Held >= 0 && !at.Contains(Held)) { var o = Find(Held, goalKey, null); var ghost = GhostOf(o); if (ghost) { step = FieldGuideStep.PlaceGhost; return ghost; } }
                foreach (int m in alive) if (!at.Contains(m) && Find(m, goalKey, null) != null) { step = FieldGuideStep.PickPawn; return HandleOf(m); }
                return null;
            }
            if (goalKey.StartsWith("search:") && int.TryParse(goalKey.Substring(7), out int site))
            {
                var k = place.CheckNow(); var run = k != null ? k.RunFor(site) : null;
                bool lead = run != null, helper = run != null && run.Support >= 0;
                if (lead && (!coop || helper)) { step = FieldGuideStep.PressTurn; return pl.TurnButton; }
                var kind = lead ? FieldSpotKind.Join : FieldSpotKind.Lead;
                if (Held >= 0) { var o = Find(Held, goalKey, kind); var ghost = GhostOf(o); if (ghost) { step = lead ? FieldGuideStep.PlaceHelper : FieldGuideStep.PlaceGhost; return ghost; } }
                // The member to pick up: one with nothing to do first, then anyone the plan would take there.
                var order = alive.OrderBy(m => k != null && m < k.Actions.Length && k.Actions[m] == FieldAction.Hush ? 0 : 1).ThenBy(m => m);
                foreach (int m in order) if ((run == null || m != run.Lead) && Find(m, goalKey, kind) != null) { step = lead ? FieldGuideStep.PickHelper : FieldGuideStep.PickPawn; return HandleOf(m); }
                // A running search nobody can join (e.g. a 함께 search already started alone): the co-op step is past, the turn goes on.
                if (lead) { step = FieldGuideStep.PressTurn; return pl.TurnButton; }
                return null;
            }
            if (goalKey.StartsWith("observe:"))
            {
                string id = goalKey.Substring(8); var k = place.CheckNow();
                if (k != null) for (int m = 0; m < k.ObserveOf.Length; m++) if (k.ObserveOf[m] == id && k.Actions[m] == FieldAction.Observe) { step = FieldGuideStep.PressTurn; return pl.TurnButton; }
                if (Held >= 0) { var ghost = GhostOf(Find(Held, goalKey, FieldSpotKind.Observe)); if (ghost) { step = FieldGuideStep.PlaceGhost; return ghost; } }
                foreach (int m in alive) if (Find(m, goalKey, FieldSpotKind.Observe) != null) { step = FieldGuideStep.PickPawn; return HandleOf(m); }
            }
            return null;
        }
        FieldPlaceOption Find(int m, string key, FieldSpotKind? kind)
        {
            var place = Placement; if (place == null || m < 0) return null;
            foreach (var o in place.OptionsFor(m)) if (o.Enabled && o.Key == key && (!kind.HasValue || o.Kind == kind.Value)) return o;
            return null;
        }

        // ---- catcher, cards, hiding ----
        void SetCatcher(bool on) { if (Catcher && Catcher.gameObject.activeSelf != on) Catcher.gameObject.SetActive(on); }
        // The arrival row's member cards take presses on the board (FieldMemberCardInput) and show the held member's outline.
        void CardInputs(bool on)
        {
            var a = Arrival; if (!a) return;
            for (int i = 0; i < a.Cards.Count; i++)
            {
                var card = a.Cards[i]; if (!card) continue;
                if (!slots.TryGetValue(card, out var slot) || !slot) { slot = card.GetComponentInChildren<FieldMemberActionSlot>(true); slots[card] = slot; }
                if (!slot) continue;
                if (slot.Input) slot.Input.SetHit(on);
                slot.SetSelected(on && i == Held);
            }
        }
        void HideAll()
        {
            SetCatcher(false); HideGhosts(); HidePins(); shown.Clear(); shownVersion = -1;
            if (carry && carry.activeSelf) carry.SetActive(false);
            foreach (var h in handles) if (h && h.gameObject.activeSelf) { h.gameObject.SetActive(false); h.Index = -1; }
            foreach (var r in rows) if (r && r.gameObject.activeSelf) r.gameObject.SetActive(false);
            foreach (var t in tags) if (t && t.gameObject.activeSelf) t.gameObject.SetActive(false);
            foreach (var p in progress) if (p && p.gameObject.activeSelf) p.gameObject.SetActive(false);
        }

        // ---- small helpers ----
        bool Alive(int m) { var a = Arrival; return a && m >= 0 && m < a.Participants.Count && a.Participants[m] != null && a.Participants[m].Health > 0; }
        string Name(int m) { var a = Arrival; return a && m >= 0 && m < a.Participants.Count && a.Participants[m] != null ? a.Participants[m].Name : ""; }
        static string RoomName(int room) => room >= 0 && room < FieldSiteState.RoomNames.Length ? FieldSiteState.RoomNames[room] : "";
        static bool Meeting(ExpeditionArrivalPanel a) => a.Encounter && (a.Encounter.IsOpen || a.Encounter.Battle && a.Encounter.Battle.IsOpen);
        // The top of what stands on an object's mark: a door's heard/threat paper just above its arrow ('숨소리', '귀 대는 중') lifts
        // the pin over it so the pin never hides those words (2026-09-26).
        float Above(Rect mark, RectTransform space)
        {
            float top = mark.yMax; var t = Arrival ? Arrival.Threat : null; if (!t || t.DoorMarkers == null) return top;
            foreach (var m in t.DoorMarkers)
            {
                if (!m || !m.gameObject.activeInHierarchy) continue;
                var r = RectIn(m.Paper ? m.Paper.rectTransform : m.Root ? m.Root : (RectTransform)m.transform, space);
                if (r.xMax < mark.xMin - 8 || r.xMin > mark.xMax + 8 || r.yMin < mark.yMin || r.yMin > mark.yMax + 80) continue;
                top = Mathf.Max(top, r.yMax);
            }
            return top;
        }
        static RectTransform MarkerOf(Component button)
        {
            if (!button || !button.gameObject.activeInHierarchy) return null;
            var marker = button.transform.Find("ExplorationMarkerPaper") as RectTransform;
            return marker && marker.gameObject.activeInHierarchy ? marker : button.transform as RectTransform;
        }
        Rect RectIn(RectTransform r, RectTransform space)
        {
            r.GetWorldCorners(corners); var a = space.InverseTransformPoint(corners[0]); var b = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
    }
}
