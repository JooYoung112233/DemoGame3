using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // 수색 쪽지 (2026-09-25, 기획/탐험-수색쪽지와-협동-1차.md · 시안 탐험-수색쪽지-시안/01~02 without the pace row and the '나올 것' row the
    // user removed). A press on a room object opens this small paper beside it instead of the 07 window (the hook is
    // ExpeditionArrivalPanel.Pressed; Inspect still opens 07, which '자세히 >' does). It shows the object, '수색도 p / r', the lead and
    // helper slots, the three support roles while a helper is there, one line of what this turn does, '빼기', '자세히 >' and, on the
    // first visit, '수색 · 1턴'.
    // Site board: a slot filled from the face row, or a portrait dropped on it, assigns at once through FieldQuickAssign (the same
    // checked targets as the glows, no time); a role rewrites the standing order through the planner's plan API (while the rules
    // allow a lookout only on a fast search, 망보기 makes it fast and says so; 함께 / 조명 give back the pace the note took). First
    // visit: the slots only choose who; '수색 · 1턴' runs the 07 window's confirm (ExpeditionSearchPanel.RunFromNote) and a meeting it
    // runs into brings the note back (Resume). Every number is a FieldRun of FieldTurnPlan.Check, never worked out here: the
    // planner's Forecast on the board, a one-order copy of the rules on the first visit (the same rules Advance applies; its gates
    // stay Loot.CanSearch / CanSupport). Not a window: the room is not dimmed, the HUD and bubbles stay; a press outside the paper closes it and goes on
    // to what lies under it (the note's own object only closes it); a right press, Esc or a passing turn closes it. Only positions
    // that follow the object are computed here (the paper beside it, the tail to its marker); sizes, texts and colours are this
    // Inspector and the prefab (BuildSearchNote.Run). After the plan markers (400) and the quick assignment (420).
    [DefaultExecutionOrder(430)]
    public sealed class FieldSearchNote : MonoBehaviour, IPointerClickHandler
    {
        public enum Side { Right, Left, Above, Below }
        [Serializable] public sealed class Slot
        {
            [Tooltip("칸 누름 (얼굴 줄 열기 · 대원을 고른 채 누르면 바로 넣기)")] public Button Button;
            [Tooltip("칸 종이")] public Image Paper;
            [Tooltip("칸 테두리 (빈 칸은 점선)")] public FieldFrameGraphic Frame;
            [Tooltip("대원 초상")] public Image Portrait;
            [Tooltip("빈 칸 표시 (점선 동그라미 + '+')")] public GameObject Empty;
            [Tooltip("칸 이름 ('담당' · '협동')")] public Text Label;
            [Tooltip("빈 칸 아래 작은 글")] public Text Caption;
        }
        [Serializable] public sealed class Chip
        {
            [Tooltip("칩 누름 (이 역할로 바꾸기)")] public Button Button;
            [Tooltip("칩 종이")] public Image Paper;
            [Tooltip("역할 이름")] public Text Title;
            [Tooltip("둘째 줄 (보정 · 소음 · 못 쓰는 까닭)")] public Text Line;
        }
        [Serializable] public sealed class Face
        {
            [Tooltip("얼굴 누름 (이 대원을 칸에 넣기)")] public Button Button;
            [Tooltip("대원 초상")] public Image Portrait;
            [Tooltip("지금 그 칸의 대원 표시 (다시 누르면 칸을 비움)")] public Graphic Current;
        }

        [Tooltip("탐험 도착 화면 (사물 누름을 이 쪽지로 받음)")] public ExpeditionArrivalPanel Arrival;
        [Tooltip("대원 먼저 맡기기 (칸 채우기 · 끌어 놓기 · 다음 대원 고르기)")] public FieldQuickAssign Quick;
        [Tooltip("배정 말풍선 (쪽지가 피해 가고, 열린 사물의 이름표를 숨김)")] public FieldPlanTargetMarkers Markers;
        [Header("쪽지")]
        [Tooltip("쪽지 종이 (피벗 가운데 · 열 때 켜짐)")] public RectTransform Paper;
        [Tooltip("쪽지 밖 누름을 받는 투명 면 (방 영역만). 누르면 쪽지를 닫고 그 아래로 누름을 넘깁니다")] public Graphic Catcher;
        [Tooltip("사물 표식을 가리키는 꼬리")] public FieldNoteTail Tail;
        [Tooltip("사물 이름")] public Text Title;
        [Tooltip("수색도")] public Text Progress;
        [Header("대원 칸")]
        [Tooltip("담당 칸")] public Slot Lead = new Slot();
        [Tooltip("협동 칸")] public Slot Helper = new Slot();
        [Tooltip("협동 대원이 없을 때 오른쪽 안내")] public Text HelperHint;
        [Header("협동 역할 (0 함께 수색 · 1 망보기 · 2 조명 지원)")]
        [Tooltip("역할 칩 3개")] public Chip[] Roles = new Chip[0];
        [Header("대원 고르기")]
        [Tooltip("칸을 누르면 역할 자리에 뜨는 얼굴 줄")] public RectTransform Picker;
        [Tooltip("얼굴 줄 제목")] public Text PickerTitle;
        [Tooltip("얼굴 (모자라면 첫 칸을 복제)")] public Face[] Faces = new Face[0];
        [Header("이번 턴 · 버튼")]
        [Tooltip("이번 턴 결과 한 줄")] public Text Forecast;
        [Tooltip("'빼기' · 이 사물의 배정 해제 (첫 방문: 칸 비우기)")] public Button Remove;
        [Tooltip("'자세히 >' · 이 사물의 07 수색 창")] public Button Detail;
        [Tooltip("'수색 · 1턴' · 첫 방문만 (07 창의 확인과 같음)")] public Button Run;

        [Header("자리")]
        [Tooltip("쪽지가 머무는 방 영역 (Main 기준 · 왼쪽 위 원점 px: x, y, 폭, 높이). 아래 판(대원 · 이번 턴 · 행동) 위까지")] public Rect RoomArea = new Rect(16, 8, 1888, 738);
        [Tooltip("사물과 꼬리 끝 사이 (px)")] [Min(0)] public float Gap = 14;
        [Tooltip("꼬리 길이 (px)")] [Min(0)] public float TailLength = 26;
        [Tooltip("꼬리 밑변 절반 (px)")] [Min(1)] public float TailHalfWidth = 15;
        [Tooltip("먼저 시도할 자리 순서 (가리는 것이 가장 적은 자리를 고르고, 같으면 앞의 자리)")] public Side[] SideOrder = { Side.Right, Side.Left, Side.Above, Side.Below };
        [Tooltip("말풍선이 아직 없을 때 사물 표식 위에 비워 둘 자리 (폭, 높이 px) · 맡기면 그 자리에 말풍선이 섭니다")] public Vector2 BubbleReserve = new Vector2(240, 176);
        [Header("동작")]
        [Tooltip("첫 방문: 쪽지를 열면 담당 칸에 그 사물의 지난 담당(없으면 첫 대원)을 미리 넣습니다")] public bool FirstVisitDefaultLead = true;
        [Tooltip("첫 방문 새 수색의 속도 (1 보통 · 망보기를 고르면 0 빠름)")] [Range(0, 2)] public int FirstVisitPace = 1;
        [Tooltip("쪽지가 열린 사물의 말풍선 이름표를 숨깁니다 (쪽지가 같은 내용을 보여 줌)")] public bool HideBubbleLabel = true;
        [Tooltip("다시 읽는 최소 간격 (초 · 가방 도구 · 체력 변화 대비)")] [Min(.05f)] public float PollSeconds = .3f;

        [Header("색")]
        [Tooltip("고른 역할 칩")] public Color ChipOn = new Color(.96f, .75f, .28f, 1);
        [Tooltip("고를 수 있는 역할 칩")] public Color ChipOff = new Color(.99f, .96f, .88f, 1);
        [Tooltip("못 쓰는 역할 칩")] public Color ChipDisabled = new Color(.84f, .83f, .8f, 1);
        [Tooltip("글자")] public Color Ink = new Color(.1f, .09f, .08f, 1);
        [Tooltip("흐린 글자 (못 쓰는 칩)")] public Color InkDisabled = new Color(.48f, .47f, .44f, 1);
        [Tooltip("이번 턴 경고 (멈춤 · 도구 없음)")] public Color WarnInk = new Color32(0xB5, 0x45, 0x2F, 255);
        [Tooltip("찬 칸 종이")] public Color SlotFilledPaper = Color.white;
        [Tooltip("빈 칸 종이")] public Color SlotEmptyPaper = new Color(.97f, .94f, .86f, 1);
        [Tooltip("찬 칸 테두리")] public Color SlotFilledBorder = new Color(.1f, .09f, .08f, 1);
        [Tooltip("빈 칸 테두리 (점선)")] public Color SlotEmptyBorder = new Color(.6f, .5f, .32f, 1);
        [Tooltip("얼굴 줄을 연 칸 테두리")] public Color SlotPicking = new Color(.96f, .75f, .28f, 1);
        [Tooltip("칸 테두리 두께 (px)")] [Min(.5f)] public float SlotBorder = 3;
        [Tooltip("빈 칸 점선 한 칸 (px)")] [Min(0)] public float SlotDash = 8;
        [Tooltip("고를 수 없는 얼굴의 초상 알파")] [Range(0, 1)] public float FaceDisabledAlpha = .35f;

        [Header("문구")]
        [Tooltip("수색도 ({0}: 지금, {1}: 필요 턴)")] public string ProgressFormat = "수색도 {0} / {1}";
        [Tooltip("칸 이름 (담당 · 협동)")] public string LeadName = "담당", HelperName = "협동";
        [Tooltip("빈 칸 아래")] public string EmptyCaption = "끌어다 놓기";
        [Tooltip("협동 대원이 없을 때")] [TextArea(2, 4)] public string HelperHintText = "두 번째 대원을 이 사물에\n끌어다 놓거나 협동 칸을\n누르면 함께합니다.\n함께 수색 · 망보기 · 조명";
        [Tooltip("살아 있는 대원이 혼자일 때")] [TextArea(2, 3)] public string AloneHint = "함께할 대원이 없습니다.";
        [Tooltip("담당이 없을 때")] [TextArea(2, 3)] public string LeadHint = "담당 칸을 눌러\n수색할 대원을 고르세요.";
        [Tooltip("진행 중이라 협동을 더할 수 없을 때")] [TextArea(2, 3)] public string LockedHint = "진행 중인 수색은\n역할이 고정됩니다.";
        [Tooltip("역할 이름 (0 함께 수색 · 1 망보기 · 2 조명 지원)")] public string[] RoleNames = { "함께 수색", "망보기", "조명 지원" };
        [Tooltip("함께 수색 둘째 줄 · 규칙이 주는 것만 이어 붙임 ({0}: 발견 보정 %p)")] public string TogetherLine = "발견 +{0}%p";
        [Tooltip("함께 수색 둘째 줄 · 혼자보다 빨리 끝날 때 ({0}: 줄어드는 턴)")] public string TogetherFasterLine = "{0}턴 빨리";
        [Tooltip("함께 수색 둘째 줄 · 규칙상 보정이 보이지 않을 때")] public string TogetherPlainLine = "함께 뒤짐";
        [Tooltip("둘째 줄 여러 항목 사이")] public string LineJoin = " · ";
        [Tooltip("망보기 둘째 줄 ({0}: 이번 턴 소음)")] public string WatchLine = "소음 {0}";
        [Tooltip("망보기 둘째 줄 · 규칙이 빠르게 뒤질 때만 망보기를 허락해 쪽지가 빠르게 바꿀 때 ({0}: 이번 턴 소음)")] public string WatchFastLine = "빠르게 · 소음 {0}";
        [Tooltip("조명 지원 둘째 줄 ({0}: 조명 보정 %p)")] public string LightLine = "손전등 · +{0}%p";
        [Tooltip("못 쓰는 역할의 둘째 줄: 손전등 없음 · 빈 대원 없음 · 진행 중이라 고정")] public string LightNeed = "손전등 필요", NobodyFree = "빈 대원 없음", Locked = "진행 중 · 고정";
        [Tooltip("못 쓰는 망보기의 둘째 줄: 사물이 조용해 망볼 것이 없음 (빈 대원은 있음)")] public string NoNoise = "소음 없음";
        [Tooltip("이번 턴 ({0}: 지금, {1}: 이번 턴 뒤, {2}: 필요 턴, {3}: 이 사물의 소음)")] public string ForecastFormat = "이번 턴 {0}/{2} → {1}/{2} · 소음 +{3}";
        [Tooltip("발견 보정이 있을 때 덧붙임 ({0}: %p)")] public string ForecastBonus = " · 발견 +{0}%p";
        [Tooltip("이번 턴에 끝날 때 덧붙임")] public string ForecastDone = " · 완료";
        [Tooltip("담당이 없을 때")] public string ForecastNoLead = "담당을 정하면 이번 턴 결과가 보입니다.";
        [Tooltip("이번 턴 멈춤 ({0}: 까닭)")] public string ForecastPaused = "이번 턴 멈춤 · {0}";
        [Tooltip("첫 방문 · 담당 가방에 도구가 없을 때 ({0}: 도구)")] public string ForecastNoTool = "{0} 필요 · 담당 가방에 없음";
        [Tooltip("첫 방문 · 조명 수색에 손전등 동료가 없을 때")] public string ForecastNoLight = "손전등을 가진 동료가 필요합니다";
        [Tooltip("첫 방문 · 진행 중인 망보기 수색에 함께할 동료가 없을 때")] public string ForecastNoHelper = "망볼 동료가 없어 이어 갈 수 없습니다";
        [Tooltip("첫 방문 · 지금 뒤질 수 없을 때")] public string ForecastBlocked = "지금은 뒤질 수 없습니다";
        [Tooltip("버튼 글 (빼기 · 자세히 · 첫 방문 수색)")] public string RemoveLabel = "빼기", DetailLabel = "자세히 >", RunLabel = "수색 · 1턴";
        [Tooltip("얼굴 줄 제목 (담당 · 협동 · 고를 대원 없음)")] public string PickLeadTitle = "담당 고르기", PickHelperTitle = "협동 고르기", PickNone = "행동이 남은 대원이 없습니다";
        [Tooltip("빼기 뒤 상황판 ({0}: 사물)")] [TextArea(2, 3)] public string StatusRemoved = "{0} 배정 해제\n진행도는 유지됩니다.";
        [Tooltip("혼자 수색으로 바꾼 뒤 상황판 ({0}: 사물)")] [TextArea(2, 3)] public string StatusAlone = "{0} 혼자 수색\n'턴 진행'으로 함께 진행합니다.";
        [Header("튜토리얼 안내 (첫 방문 · SettlementTutorialGuide)")]
        [Tooltip("담당이 없을 때 제목")] public string GuideLeadTitle = "담당 칸 · 조사할 사람";
        [Tooltip("담당이 없을 때")] [TextArea(2, 3)] public string GuideLeadText = "쪽지의 담당 칸을 누르면\n고를 수 있는 대원이 보입니다.";
        [Tooltip("담당 가방에 도구가 없을 때")] [TextArea(2, 3)] public string GuideToolText = "도구를 가진 대원을\n담당으로 고르세요.";
        [Tooltip("얼굴 줄이 열렸을 때 제목")] public string GuidePickTitle = "수색 대원 고르기";
        [Tooltip("얼굴 줄이 열렸을 때")] [TextArea(2, 3)] public string GuidePickText = "대원 한 명에게 이곳 수색을\n맡깁니다. 얼굴을 눌러 고르세요.";
        [Tooltip("'수색 · 1턴'을 가리킬 때 제목")] public string GuideRunTitle = "수색 · 1턴 진행";
        [Tooltip("{0}: 한 턴의 분")] [TextArea(2, 3)] public string GuideRunText = "이번 턴 결과를 보고 누르세요.\n1턴은 이곳에서 {0}분을 씁니다.";
        [Tooltip("쪽지에서 이 수색을 이어 갈 수 없을 때 제목 (함께하던 동료가 없음 · 지금 뒤질 수 없음)")] public string GuideStuckTitle = "수색을 이어 갈 수 없음";
        [Tooltip("쪽지에서 이 수색을 이어 갈 수 없을 때 (누를 곳 없이 안내만)")] [TextArea(2, 3)] public string GuideStuckText = "쪽지 아래 줄의 까닭 때문에\n지금은 이곳을 이어 뒤질 수 없습니다.";

        public bool IsOpen => Paper && Paper.gameObject.activeSelf;
        public int Site { get; private set; } = -1;
        public bool Board { get { var p = Planner; return p && p.Active; } }
        public bool PickerOpen => Picker && Picker.gameObject.activeSelf;
        // 0 lead slot, 1 helper slot, -1 closed.
        public int Picking { get; private set; } = -1;
        // What the note shows (member indices, -1 = empty) and the pace / role the forecast is for.
        public int LeadShown { get; private set; } = -1;
        public int HelperShown { get; private set; } = -1;
        public int PaceShown { get; private set; } = 1;
        public int DutyShown { get; private set; }
        // The rules' run the forecast line shows (FieldTurnPlan.Check: the board's plan, or the first visit's choice alone); null when none.
        public FieldRun Shown { get; private set; }
        public bool RolesShown => Roles != null && Roles.Length > 0 && Roles[0] != null && Roles[0].Button && Roles[0].Button.gameObject.activeSelf;
        public Side PlacedSide { get; private set; }
        public IReadOnlyList<int> FaceMembers => faceMembers;
        // The face showing a member in the open face row (null when not shown).
        public Button FaceFor(int member) { for (int i = 0; i < faceMembers.Count && i < facePool.Count; i++) if (faceMembers[i] == member) return facePool[i].Button; return null; }
        // The room area in this node's space (the paper stays inside it).
        public Rect Area { get { var r = ((RectTransform)transform).rect; return new Rect(r.xMin + RoomArea.x, r.yMax - RoomArea.y - RoomArea.height, RoomArea.width, RoomArea.height); } }

        static readonly List<FieldSearchNote> live = new List<FieldSearchNote>();
        static readonly List<RaycastResult> hits = new List<RaycastResult>();
        readonly List<int> faceMembers = new List<int>();
        readonly List<Face> facePool = new List<Face>();
        readonly Dictionary<int, int> paceBeforeWatch = new Dictionary<int, int>();
        readonly List<Rect> hard = new List<Rect>(), soft = new List<Rect>();
        readonly List<Rect> others = new List<Rect>();
        [Tooltip("다른 사물(버튼 영역)을 덮는 자리의 감점 비율 (표식은 1)")] [Range(0, 1)] public float ObjectWeight = .3f;
        readonly Vector3[] corners = new Vector3[4];
        int firstLead = -1, firstHelper = -1, firstPace = 1, firstDuty, firstPaceBefore = 1, openTurns, resumeSite = -1;
        bool firstForcedFast, openedOnBoard, placed, popupBefore, wired, haveKey; float nextPoll; object key;
        // Why the first visit's '수색 · 1턴' is off (the tutorial's next click follows it).
        enum FirstBlock { None, NoLead, NoTool, Stuck }
        FirstBlock firstBlock;
        ExplorationHotspot[] hotspots;

        // The note of an arrival panel (the tutorial guide and verify scripts).
        public static FieldSearchNote For(ExpeditionArrivalPanel a)
        {
            if (!a) return null;
            foreach (var n in live) if (n && n.Arrival == a) return n;
            return a.GetComponentInChildren<FieldSearchNote>(true);
        }
        // After a first-visit meeting (ExpeditionEncounterPanel: hiding worked, or the fight is over): the note whose '수색 · 1턴'
        // ran into it comes back for that object; a search run from the 07 window goes back to 07 (false).
        public static bool Resume(ExpeditionArrivalPanel a, int site)
        {
            var n = For(a); if (!n || site < 0 || n.resumeSite != site) return false;
            n.resumeSite = -1; return n.Open(site);
        }
        FieldTurnPlanner Planner => Arrival && Arrival.Threat ? Arrival.Threat.Planner : null;
        Camera Cam { get { var c = GetComponentInParent<Canvas>(); return c ? c.rootCanvas.worldCamera : null; } }
        public bool Covers(Vector2 screen) => IsOpen && RectTransformUtility.RectangleContainsScreenPoint(Paper, screen, Cam);

        void Awake() { Wire(); }
        void Wire()
        {
            if (wired) return; wired = true;
            if (Lead.Button) Lead.Button.onClick.AddListener(() => OnSlot(0));
            if (Helper.Button) Helper.Button.onClick.AddListener(() => OnSlot(1));
            for (int i = 0; Roles != null && i < Roles.Length; i++) { int d = i; if (Roles[i] != null && Roles[i].Button) Roles[i].Button.onClick.AddListener(() => ChooseRole(d)); }
            if (Faces != null) foreach (var f in Faces) if (f != null && f.Button) AddFace(f);
            if (Remove) Remove.onClick.AddListener(RemoveAll);
            if (Detail) Detail.onClick.AddListener(ShowDetail);
            if (Run) Run.onClick.AddListener(() => RunOnce());
        }
        void AddFace(Face f) { int i = facePool.Count; facePool.Add(f); f.Button.onClick.RemoveAllListeners(); f.Button.onClick.AddListener(() => OnFace(i)); }
        void OnEnable()
        {
            Wire(); live.RemoveAll(n => !n); if (!live.Contains(this)) live.Add(this);
            if (Arrival) Arrival.Pressed = OnPressed;
        }
        void OnDisable()
        {
            live.Remove(this); Close();
            if (Arrival && Arrival.Pressed == (Func<int, bool>)OnPressed) Arrival.Pressed = null;
        }
        void Start()
        {
            // Over the quick assignment layer (its veil), under the popups (siblings of Main).
            var main = transform.parent; if (main && Quick && Quick.transform.parent == main && transform.GetSiblingIndex() < Quick.transform.GetSiblingIndex()) transform.SetAsLastSibling();
        }

        // ---- opening and closing ----
        // The arrival panel's object press: a search object of this room that is not finished gets its note; doors and finished
        // objects keep Inspect (the door popup, the finds). Inspect's own order.
        bool OnPressed(int index)
        {
            var a = Arrival;
            if (!a || !a.Loot || a.Rooms && index == 3 || !a.Loot.IsSiteInCurrentRoom(index) || a.Loot.State(index).Complete) return false;
            if (IsOpen && Site == index) { Close(); return true; }
            return Open(index);
        }
        bool Allowed(ExpeditionArrivalPanel a) => a && a.IsOpen && !a.InTransit && a.Main && a.Main.interactable && a.Main.blocksRaycasts && !(a.Popup && a.Popup.activeSelf)
            && !(a.Search && a.Search.IsOpen) && !(a.Loot && a.Loot.IsOpen) && !(a.FieldBags && a.FieldBags.IsOpen) && !(a.Story && a.Story.IsOpen)
            && !Meeting(a);
        static bool Meeting(ExpeditionArrivalPanel a) => a && a.Encounter && (a.Encounter.IsOpen || a.Encounter.Battle && a.Encounter.Battle.IsOpen);
        // A reserved room move (the next turn walks away): no note; the press goes on to Inspect (the 07 window) as before.
        static bool Moving(ExpeditionArrivalPanel a) => a && a.Rooms && a.Rooms.HasQueuedMove;
        public bool Open(int site)
        {
            var a = Arrival;
            if (!Paper || !Allowed(a) || Moving(a) || !a.Loot || site < 0 || site >= a.Loot.Sites.Length || site >= a.ObjectNames.Length || !a.Loot.IsSiteInCurrentRoom(site) || a.Loot.State(site).Complete) return false;
            if (!FieldQuickAssign.SiteButtonOf(a, site)) return false;
            bool same = IsOpen && Site == site;
            if (!same)
            {
                Site = site; Picking = -1; placed = false; if (Picker) Picker.gameObject.SetActive(false);
                firstPace = Mathf.Clamp(FirstVisitPace, 0, 2); firstDuty = 0; firstForcedFast = false; firstHelper = -1;
                firstLead = -1;
                if (FirstVisitDefaultLead && !Board)
                {
                    // The object's last lead while standing, else the first member standing.
                    if (a.Search && a.Search.Assignments.TryGetValue(site, out var saved) && saved.Worker != null) { int last = IndexOf(a, saved.Worker); if (Alive(a, last)) firstLead = last; }
                    for (int m = 0; firstLead < 0 && m < a.Participants.Count; m++) if (Alive(a, m)) firstLead = m;
                }
            }
            // As Inspect does (the '?' mark, the visit record). Opening a note is not a window: the status paper keeps its line.
            if (a.Rooms && !a.Rooms.Inspected.Contains(site)) { string status = a.Status ? a.Status.text : null; a.Rooms.MarkInspected(site); if (status != null) a.Status.text = status; }
            openedOnBoard = Board; openTurns = a.Rooms ? a.Rooms.Turns : 0;
            if (!Paper.gameObject.activeSelf) Paper.gameObject.SetActive(true);
            if (Catcher && !Catcher.gameObject.activeSelf) Catcher.gameObject.SetActive(true);
            if (Tail && !Tail.gameObject.activeSelf) Tail.gameObject.SetActive(true);
            // Already open for this object (a slot filled from it): refreshed where it stands.
            haveKey = false; Refresh(true); Place(!same);
            return true;
        }
        public void Close()
        {
            if (Paper && Paper.gameObject.activeSelf) Paper.gameObject.SetActive(false);
            if (Catcher && Catcher.gameObject.activeSelf) Catcher.gameObject.SetActive(false);
            if (Tail && Tail.gameObject.activeSelf) Tail.gameObject.SetActive(false);
            if (Picker && Picker.gameObject.activeSelf) Picker.gameObject.SetActive(false);
            Picking = -1; Site = -1; placed = false; haveKey = false; Shown = null;
            if (Markers && Markers.MutedLabel != null) Markers.MutedLabel = null;
        }
        // Still this note: the same room, not finished, the same kind of visit, and (on the board) no turn passed.
        bool Valid(ExpeditionArrivalPanel a)
        {
            if (!Allowed(a) || Site < 0 || !a.Loot.IsSiteInCurrentRoom(Site) || a.Loot.Peek(Site, out var s) && s.Complete || Board != openedOnBoard) return false;
            return !(a.Rooms && a.Rooms.Turns != openTurns) && !Moving(a);
        }

        // ---- presses ----
        // A press that reaches the note itself: on the paper (not a control) it only folds the face row; outside it (the catcher)
        // it closes the note and goes on to what lies under it (the note's own object or bubble only closes it). Right press closes.
        public void OnPointerClick(PointerEventData e)
        {
            if (!IsOpen) return;
            if (e.button == PointerEventData.InputButton.Right) { Close(); return; }
            if (e.button != PointerEventData.InputButton.Left) return;
            if (Covers(e.position)) { if (PickerOpen) { ClosePicker(); Refresh(true); } return; }
            int site = Site; Close();
            var below = Below(e.position); if (!below || Own(below, site, e.position)) return;
            ExecuteEvents.ExecuteHierarchy(below, e, ExecuteEvents.pointerClickHandler);
        }
        GameObject Below(Vector2 screen)
        {
            var es = EventSystem.current; if (!es) return null;
            hits.Clear(); es.RaycastAll(new PointerEventData(es) { position = screen }, hits);
            foreach (var h in hits) if (h.gameObject && !h.gameObject.transform.IsChildOf(transform)) return h.gameObject;
            return null;
        }
        // The note's own object or its bubble (while nobody is chosen: a chosen member's press there assigns).
        bool Own(GameObject below, int site, Vector2 screen)
        {
            if (Quick && Quick.Selected >= 0) return false;
            var button = FieldQuickAssign.SiteButtonOf(Arrival, site); if (button && below.transform.IsChildOf(button.transform)) return true;
            return Quick && Quick.BubbleKeyAt(screen) == FieldPlanTargetMarkers.SearchKey(site);
        }
        void OnSlot(int which)
        {
            if (!IsOpen) return;
            // A member chosen on the cards goes straight into the slot.
            if (Board && Quick && Quick.Selected >= 0 && Pick(which, Quick.Selected)) return;
            if (Picking == which) ClosePicker(); else { Picking = which; if (Picker) Picker.gameObject.SetActive(true); }
            Refresh(true);
        }
        void ClosePicker() { Picking = -1; if (Picker && Picker.gameObject.activeSelf) Picker.gameObject.SetActive(false); }
        void OnFace(int i)
        {
            if (!IsOpen || i < 0 || i >= faceMembers.Count) return;
            int m = faceMembers[i], which = Picking, current = which == 0 ? LeadShown : HelperShown;
            ClosePicker();
            if (m == current) { if (which == 0) RemoveAll(); else ClearHelper(); }
            else Pick(which, m);
            Refresh(true);
        }

        // ---- slots, roles, buttons ----
        // Put a member in a slot. Board: at once (lead = the glow's lead target, helper = join or take the helper's place; no time).
        // First visit: only who ('수색 · 1턴' commits).
        public bool Pick(int which, int member)
        {
            var a = Arrival; if (!IsOpen || !Alive(a, member)) return false;
            ClosePicker();
            if (Board)
            {
                if (!Quick) return false;
                var order = Planner.Plan.Find(Site);
                var t = which == 0 || order == null ? Quick.LeadFor(Site, member) : Quick.JoinFor(Site, member);
                bool ok = t != null && Quick.Commit(t); haveKey = false; Refresh(true); return ok;
            }
            if (which == 0) { if (firstHelper == member) firstHelper = firstLead; firstLead = member; }
            else { if (member == firstLead) return false; firstHelper = member; }
            haveKey = false; Refresh(true); return true;
        }
        // Board: the search goes on alone (only before it starts: a started search keeps the helper it needs).
        public bool ClearHelper()
        {
            var a = Arrival; if (!IsOpen || !Board) return false;
            var pl = Planner; var facts = pl.Facts(); var order = pl.Plan.Find(Site); if (order == null) return false;
            var o = SoloOrder(order); var r = BoardDraft(pl, facts, o);
            if (r == null || r.Support >= 0) return false;
            pl.Plan.Assign(o); pl.Plan.Keep(pl.Plan.Check(facts)); paceBeforeWatch.Remove(Site);
            a.Status.text = string.Format(StatusAlone, SiteName(a, Site)); a.Threat.Refresh();
            haveKey = false; Refresh(true); return true;
        }
        // The order without a helper: the pace 망보기 took comes back (as 함께 / 조명 give it back) and a new search's role is 함께 again,
        // so a helper added later joins as 함께 수색 (the default role).
        FieldOrder SoloOrder(FieldOrder order)
        {
            var o = order.Copy(); o.Solo = true; o.Support = -1; o.Prefer = -1; o.Duty = 0;
            if (o.Pace == 0 && paceBeforeWatch.TryGetValue(order.Site, out int before)) o.Pace = before;
            return o;
        }
        // A support role. Board: rewrites the standing order (a copy of the plan must bind a helper in that role); 망보기 makes the
        // search fast (the rules as of 밸런스 1차 allow a lookout only then; rules without a pace ignore it), 함께 / 조명 give back the
        // pace the note took (a pace chosen in 07 stays). First visit: only roles a copy of the rules binds (FirstDraft).
        public bool ChooseRole(int duty)
        {
            var a = Arrival; if (!IsOpen || duty < 0 || duty > 2) return false;
            if (!Board)
            {
                var s = a.Loot.State(Site); var worker = LeadPerson(a); if (s.Progress > 0 || worker == null) return false;
                if (duty != 0 && !Fits(FirstDraft(firstLead, duty == 1 ? 0 : NormalPace, duty, FirstHelper(a)), duty)) return false;
                if (duty == 1) { if (firstPace != 0) { firstPaceBefore = firstPace; firstPace = 0; firstForcedFast = true; } }
                else if (firstForcedFast && firstPace == 0) { firstPace = firstPaceBefore; firstForcedFast = false; }
                firstDuty = duty; haveKey = false; Refresh(true); return true;
            }
            var pl = Planner; var facts = pl.Facts(); var order = pl.Plan.Find(Site); if (order == null) return false;
            var run = pl.Plan.Check(facts).RunFor(Site); if (run == null || !run.Starts || run.Support < 0) return false;
            var o = RoleOrder(order, duty, run.Support); if (!Fits(BoardDraft(pl, facts, o), duty)) return false;
            if (duty == 1) { if (order.Pace != 0) paceBeforeWatch[Site] = order.Pace; }
            else paceBeforeWatch.Remove(Site);
            pl.Plan.Assign(o); pl.Plan.Keep(pl.Plan.Check(facts));
            var after = pl.Plan.Check(facts).RunFor(Site);
            if (Quick && after != null && after.Support >= 0) a.Status.text = string.Format(Quick.StatusJoin, a.Participants[after.Support].Name, SiteName(a, Site), RoleName(duty));
            a.Threat.Refresh(); haveKey = false; Refresh(true); return true;
        }
        FieldOrder RoleOrder(FieldOrder order, int duty, int helper)
        {
            var o = order.Copy(); o.Duty = duty; o.Solo = false; o.Prefer = helper; o.Support = helper;
            if (duty == 1) o.Pace = 0;
            else if (order.Pace == 0 && paceBeforeWatch.TryGetValue(order.Site, out int before)) o.Pace = before;
            return o;
        }
        static bool Fits(FieldRun r, int duty) => r != null && r.Support >= 0 && r.Role == (duty == 1 ? FieldAction.Watch : duty == 2 ? FieldAction.Light : FieldAction.Together);
        // '빼기': board = release this object's order (progress stays, no time); first visit = empty the slots.
        public void RemoveAll()
        {
            var a = Arrival; if (!IsOpen) return;
            ClosePicker();
            if (Board)
            {
                var pl = Planner; if (pl.Plan.Find(Site) == null) return;
                pl.Plan.Release(Site); paceBeforeWatch.Remove(Site);
                a.Status.text = string.Format(StatusRemoved, SiteName(a, Site)); a.Threat.Refresh();
            }
            else { firstLead = firstHelper = -1; firstDuty = 0; if (firstForcedFast) firstPace = firstPaceBefore; firstForcedFast = false; }
            haveKey = false; Refresh(true);
        }
        // '자세히 >': today's 07 window for this object (Inspect); on the first visit it starts from the note's choice.
        public void ShowDetail()
        {
            var a = Arrival; if (!IsOpen) return;
            int site = Site, lead = firstLead, pace = firstPace, duty = firstDuty; bool board = Board;
            Close(); a.Inspect(site);
            if (!board && a.Search && a.Search.IsOpen && Alive(a, lead)) a.Search.TakeNoteChoice(a.Participants[lead], pace, duty);
        }
        // '수색 · 1턴' (first visit): exactly the 07 window's confirm; the note stays for the next turn unless a window took over.
        // One press = one turn: the planner's press lock ('턴 진행' and 숨죽이기 use it on the first visit too) stops a double press,
        // which the 07 window's confirm stopped by closing its review. A meeting it runs into brings the note back after (Resume).
        public bool RunOnce()
        {
            var a = Arrival; if (!IsOpen || Board || !a.Search || !Alive(a, firstLead) || Run && !Run.IsInteractable()) return false;
            var pl = Planner; if (pl && !pl.TryLock()) return false;
            int site = Site; ClosePicker();
            bool ran = a.Search.RunFromNote(site, a.Participants[firstLead], firstPace, firstDuty);
            if (ran && Meeting(a)) resumeSite = site;
            openTurns = a.Rooms ? a.Rooms.Turns : 0; haveKey = false;
            if (!IsOpen) return ran;
            if (!Valid(a)) Close(); else { Refresh(true); Place(false); }
            return ran;
        }

        // ---- drops (FieldQuickAssign) ----
        // A portrait dropped on the lead slot leads this object, on the helper slot joins it (leads it when nobody does yet).
        public FieldQuickTarget DropTarget(Vector2 screen, int member)
        {
            if (!IsOpen || !Board || !Quick || member < 0) return null; var cam = Cam;
            bool lead = Lead.Button && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)Lead.Button.transform, screen, cam);
            bool helper = !lead && Helper.Button && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)Helper.Button.transform, screen, cam);
            if (!lead && !helper) return null;
            return lead || Planner.Plan.Find(Site) == null ? Quick.LeadFor(Site, member) : Quick.JoinFor(Site, member);
        }

        // ---- the tutorial's next click (first visit) ----
        // True with a target: the control to press. True without one: a search the note cannot move on (the banner says why).
        public bool GuideStep(out string title, out string text, out Button target)
        {
            title = text = null; target = null; var a = Arrival;
            if (!IsOpen || Board || !a) return false;
            if (PickerOpen)
            {
                for (int i = 0; i < faceMembers.Count && i < facePool.Count && !target; i++) if (faceMembers[i] != LeadShown && facePool[i].Button && facePool[i].Button.IsInteractable()) target = facePool[i].Button;
                title = GuidePickTitle; text = GuidePickText;
            }
            else if (!Alive(a, firstLead) || firstBlock == FirstBlock.NoLead) { target = Lead.Button; title = GuideLeadTitle; text = GuideLeadText; }
            else if (firstBlock == FirstBlock.NoTool) { target = Lead.Button; title = GuideLeadTitle; text = GuideToolText; }
            // Nothing on the note moves this search on (its helper is gone · the object is shut now): the reason only, no target.
            else if (firstBlock == FirstBlock.Stuck || Run && !Run.IsInteractable()) { title = GuideStuckTitle; text = GuideStuckText; return true; }
            else { target = Run; title = GuideRunTitle; text = string.Format(GuideRunText, a.Rooms ? a.Rooms.MinutesPerTurn : 10); }
            return target;
        }

        // ---- every frame ----
        void Update()
        {
            var a = Arrival; if (!IsOpen || !a) return;
            var mouse = Mouse.current; if (mouse != null && mouse.rightButton.wasPressedThisFrame && Covers(mouse.position.ReadValue())) { Close(); return; }
            var kb = Keyboard.current; if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            // The arrival panel reads Esc first and asks about going home: that question was not meant (put away this frame).
            bool asked = a.Popup && a.Popup.activeSelf && !popupBefore;
            Close(); if (asked) a.ClosePopup();
        }
        void LateUpdate()
        {
            var a = Arrival;
            // The meeting ended without coming back to this object (a retreat, the way home): nothing to resume.
            if (resumeSite >= 0 && !Meeting(a)) resumeSite = -1;
            if (IsOpen && !Valid(a)) Close();
            popupBefore = a && a.Popup && a.Popup.activeSelf;
            if (Markers) { string mute = IsOpen && HideBubbleLabel ? FieldPlanTargetMarkers.SearchKey(Site) : null; if (Markers.MutedLabel != mute) Markers.MutedLabel = mute; }
            if (!IsOpen) return;
            Refresh(false); Place(false);
        }

        // ---- what the note shows ----
        void Refresh(bool force)
        {
            var a = Arrival; if (!IsOpen || !a) return;
            var pl = Planner; var rooms = a.Rooms; int alive = 0; for (int i = 0; i < a.Participants.Count; i++) if (Alive(a, i)) alive |= 1 << i;
            int progress = a.Loot.Peek(Site, out var st) ? st.Progress : -1;
            var now = (Site, Board, pl ? pl.Plan.Version : -1, rooms ? rooms.Turns : 0, alive, Picking, (firstLead, firstHelper, firstPace, firstDuty), Quick ? Quick.Selected : -1, progress);
            if (!force && haveKey && now.Equals(key) && Time.unscaledTime < nextPoll) return;
            key = now; haveKey = true; nextPoll = Time.unscaledTime + PollSeconds;
            Set(Title, SiteName(a, Site));
            Label(Remove, RemoveLabel); Label(Detail, DetailLabel); Label(Run, RunLabel);
            if (Board) RefreshBoard(a); else RefreshFirst(a);
            bool picking = PickerOpen;
            if (picking) BuildFaces(a);
            if (Picker && Picker.gameObject.activeSelf != picking) Picker.gameObject.SetActive(picking);
        }
        void RefreshBoard(ExpeditionArrivalPanel a)
        {
            var pl = Planner; var facts = pl.Facts(); var k = pl.Forecast(pl.Plan).check; var run = k.RunFor(Site); var order = pl.Plan.Find(Site); var f = facts.Site(Site);
            int lead = run != null ? run.Lead : order != null && facts.Alive(order.Lead) ? order.Lead : -1, helper = run != null ? run.Support : -1;
            bool started = f.Progress > 0;
            PaceShown = run != null ? run.Pace : order != null ? order.Pace : Quick ? Quick.DefaultPace : 1; DutyShown = run != null ? run.Duty : order != null ? order.Duty : 0;
            LeadShown = lead; HelperShown = helper; Shown = run;
            ShowProgress(f.Progress, started ? f.Required : run != null ? run.Required : AloneRequired(a, PaceShown));
            if (run != null) SetForecast(Line(run.Before, run.After, run.Required, run.Noise, run.Bonus, run.Completes) + (run.Forfeits ? Warn(" · " + string.Format(pl.Texts.ChipForfeit, run.Lost)) : ""));
            else if (order != null) SetForecast(Warn(string.Format(ForecastPaused, pl.PauseShort(Site, k.PauseFor(Site)))));
            else SetForecast(ForecastNoLead);
            ShowSlot(Lead, a, lead, LeadName, Picking == 0); ShowSlot(Helper, a, helper, HelperName, Picking == 1);
            bool roles = lead >= 0 && helper >= 0 && !PickerOpen;
            if (roles)
            {
                // Each chip is a copy of the plan with that role, checked by the rules (the bound helper, the turns, the noise, the bonus).
                var together = started ? null : BoardDraft(pl, facts, RoleOrder(order, 0, helper));
                int alone = started ? -1 : BoardDraft(pl, facts, SoloOrder(order))?.Required ?? -1;
                for (int d = 0; d < Roles.Length; d++)
                {
                    if (started) { bool lit = run.Duty == d; ShowChip(d, lit, false, lit ? RoleLine(d, run, -1, false) : Locked); continue; }
                    var o = RoleOrder(order, d, helper); var r = d == 0 ? together : BoardDraft(pl, facts, o); bool ok = Fits(r, d);
                    ShowChip(d, run.Duty == d, ok, ok ? RoleLine(d, r, alone, d == 1 && WatchNeedsFast(pl, facts, o)) : Unavailable(d, together));
                }
            }
            ShowRoles(roles);
            bool canJoin = lead >= 0 && helper < 0 && Quick && Enumerable.Range(0, a.Participants.Count).Any(m => m != lead && m < k.Actions.Length && k.Actions[m] == FieldAction.Hush && Quick.JoinFor(Site, m) != null);
            Hint(!roles && !PickerOpen, lead < 0 ? LeadHint : Living(a) < 2 ? AloneHint : started && !canJoin ? LockedHint : HelperHintText);
            if (Remove && Remove.interactable != (order != null)) Remove.interactable = order != null;
            if (Detail && !Detail.interactable) Detail.interactable = true;
            if (Run && Run.gameObject.activeSelf) Run.gameObject.SetActive(false);
        }
        void RefreshFirst(ExpeditionArrivalPanel a)
        {
            var loot = a.Loot; var s = loot.State(Site); bool started = s.Progress > 0;
            if (!Alive(a, firstLead)) firstLead = -1;
            var worker = LeadPerson(a);
            // A new search's role the rules would not bind (no second member · no flashlight · nothing to watch) falls back to 함께 수색,
            // as the 07 window's Refresh does.
            if (!started && worker != null && firstDuty != 0 && !Fits(FirstDraft(firstLead, firstPace, firstDuty, FirstHelper(a)), firstDuty))
            {
                firstDuty = 0; if (firstForcedFast && firstPace == 0) { firstPace = firstPaceBefore; firstForcedFast = false; }
            }
            int pace = started ? s.Pace : firstPace, duty = started ? s.Duty : firstDuty;
            // The rules' run for this choice (a running search keeps what it stored); the helper is whoever the rules bind.
            var run = worker != null ? FirstDraft(firstLead, pace, duty, FirstHelper(a)) : null;
            int helper = run != null ? run.Support : -1;
            PaceShown = pace; DutyShown = duty; LeadShown = firstLead; HelperShown = helper; Shown = run;
            ShowProgress(s.Progress, started ? s.Required : run != null ? run.Required : AloneRequired(a, pace));
            // What stops '수색 · 1턴' is the 07 window's own gate (Loot.CanSearch / CanSupport, which Advance checks again).
            string tool = loot.Sites[Site].RequiredTool; bool canSearch = worker != null && loot.CanSearch(Site, worker), canSupport = worker != null && loot.CanSupport(duty, worker);
            bool noTool = worker != null && !canSearch && !string.IsNullOrEmpty(tool) && !s.Opened && a.Inventory.CountFor(worker, tool) == 0;
            if (worker == null) { firstBlock = FirstBlock.NoLead; SetForecast(ForecastNoLead); }
            else if (!canSearch) { firstBlock = noTool ? FirstBlock.NoTool : FirstBlock.Stuck; SetForecast(Warn(noTool ? string.Format(ForecastNoTool, ToolName(a, tool)) : ForecastBlocked)); }
            else if (!canSupport) { firstBlock = FirstBlock.Stuck; SetForecast(Warn(duty == 2 ? ForecastNoLight : ForecastNoHelper)); }
            else if (run == null) { firstBlock = FirstBlock.Stuck; SetForecast(Warn(ForecastBlocked)); }
            else { firstBlock = FirstBlock.None; SetForecast(Line(run.Before, run.After, run.Required, run.Noise, run.Bonus, run.Completes)); }
            ShowSlot(Lead, a, firstLead, LeadName, Picking == 0); ShowSlot(Helper, a, helper, HelperName, Picking == 1);
            bool roles = worker != null && helper >= 0 && !PickerOpen;
            if (roles)
            {
                int normal = NormalPace, other = FirstHelper(a);
                var together = started ? null : duty == 0 ? run : FirstDraft(firstLead, normal, 0, other);
                int alone = started ? -1 : FirstDraft(firstLead, normal, 0, -1, true)?.Required ?? -1;
                for (int d = 0; d < Roles.Length; d++)
                {
                    bool lit = d == duty;
                    if (started) { ShowChip(d, lit, false, lit ? RoleLine(d, run, -1, false) : Locked); continue; }
                    var r = d == duty ? run : d == 0 ? together : FirstDraft(firstLead, d == 1 ? 0 : normal, d, other); bool ok = d == 0 || Fits(r, d);
                    bool fast = d == 1 && ok && !Fits(FirstDraft(firstLead, Mathf.Max(1, normal), 1, other), 1);
                    ShowChip(d, lit, ok, ok ? RoleLine(d, r, alone, fast) : Unavailable(d, together));
                }
            }
            ShowRoles(roles);
            Hint(!roles && !PickerOpen, worker == null ? LeadHint : Living(a) < 2 ? AloneHint : started ? LockedHint : HelperHintText);
            if (Remove && Remove.interactable != (firstLead >= 0)) Remove.interactable = firstLead >= 0;
            if (Detail && !Detail.interactable) Detail.interactable = true;
            if (Run)
            {
                if (!Run.gameObject.activeSelf) Run.gameObject.SetActive(true);
                bool can = firstBlock == FirstBlock.None; if (Run.interactable != can) Run.interactable = can;
            }
        }
        // The pace 함께 / 조명 use on the first visit (the one 망보기 took, else the note's).
        int NormalPace => firstForcedFast ? firstPaceBefore : firstPace;

        // ---- the rules' own numbers ----
        // Every number on the note is a FieldRun of FieldTurnPlan.Check, the check the turn itself applies (the first visit's Advance
        // applies the same rules): turns, noise, bonus and who is bound are never worked out here, so a rule change reaches the note by
        // itself. Board: a copy of the real plan. First visit: the note's choice alone (everyone else is free), with this object's
        // tool and place taken as given (those gates are Loot.CanSearch's).
        static FieldRun Draft(FieldTurnPlan plan, FieldOrder o, IFieldPlanFacts facts) { if (plan == null || o == null || facts == null) return null; plan.Assign(o); return plan.Check(facts).RunFor(o.Site); }
        FieldRun BoardDraft(FieldTurnPlanner pl, IFieldPlanFacts facts, FieldOrder o) => pl && o != null ? Draft(pl.Plan.Clone(), o, facts) : null;
        FieldRun FirstDraft(int lead, int pace, int duty, int helper, bool solo = false)
        {
            var pl = Planner; if (!pl || lead < 0) return null;
            return Draft(new FieldTurnPlan(), new FieldOrder { Site = Site, Lead = lead, Pace = pace, Duty = solo ? 0 : duty, Prefer = solo ? -1 : helper, Solo = solo }, new Loose(pl.Facts(), Site));
        }
        // 망보기 bound only at a fast pace: the rules allow a lookout only on a fast search (the note makes it fast and says so).
        bool WatchNeedsFast(FieldTurnPlanner pl, IFieldPlanFacts facts, FieldOrder watch) { var slow = watch.Copy(); slow.Pace = Mathf.Max(1, slow.Pace); return !Fits(BoardDraft(pl, facts, slow), 1); }
        // What this object takes searched alone by anyone standing (the progress line before a lead is chosen or while it pauses).
        int AloneRequired(ExpeditionArrivalPanel a, int pace)
        {
            var pl = Planner; if (!pl) return 0; int lead = -1;
            for (int m = 0; m < a.Participants.Count && lead < 0; m++) if (Alive(a, m)) lead = m;
            var r = lead < 0 ? null : Draft(new FieldTurnPlan(), new FieldOrder { Site = Site, Lead = lead, Pace = pace, Solo = true }, new Loose(pl.Facts(), Site));
            return r != null ? r.Required : 0;
        }
        // The chip's small line: what the rules give that role (함께: the bonus and / or the turns saved against searching alone).
        string RoleLine(int duty, FieldRun r, int alone, bool fast)
        {
            if (r == null) return "";
            if (duty == 1) return string.Format(fast ? WatchFastLine : WatchLine, r.Noise);
            if (duty == 2) return string.Format(LightLine, r.Bonus);
            string line = r.Bonus > 0 ? string.Format(TogetherLine, r.Bonus) : "";
            if (alone > r.Required && r.Starts) line += (line.Length > 0 ? LineJoin : "") + string.Format(TogetherFasterLine, alone - r.Required);
            return line.Length > 0 ? line : TogetherPlainLine;
        }
        // Why a role cannot be chosen: no flashlight helper · someone is free but the object makes no noise to watch · nobody free.
        // 망보기 only lowers an object's own noise: a silent object says so; a noisy one is only missing a free helper.
        string Unavailable(int duty, FieldRun together) => duty == 2 ? LightNeed : duty == 1 && Arrival && Arrival.Loot && Arrival.Loot.SiteNoise(Site) == 0 ? NoNoise : NobodyFree;
        // The planner's facts with one object's tool and place taken as given (numbers only).
        sealed class Loose : IFieldPlanFacts
        {
            readonly IFieldPlanFacts facts; readonly int site;
            public Loose(IFieldPlanFacts f, int target) { facts = f; site = target; }
            public int Members => facts.Members;
            public int LightBonus => facts.LightBonus;
            public bool Alive(int m) => facts.Alive(m);
            public bool HasLight(int m) => facts.HasLight(m);
            public bool HasTool(int s, int m) => s == site || facts.HasTool(s, m);
            public FieldSiteFacts Site(int s) { var f = facts.Site(s); if (s == site) { f.InRoom = true; f.Searchable = true; } return f; }
        }
        void ShowProgress(int now, int required) => Set(Progress, string.Format(ProgressFormat, now, required > 0 ? required.ToString() : "?"));
        int FirstHelper(ExpeditionArrivalPanel a)
        {
            if (firstHelper != firstLead && Alive(a, firstHelper)) return firstHelper;
            firstHelper = -1; for (int m = 0; m < a.Participants.Count && firstHelper < 0; m++) if (m != firstLead && Alive(a, m)) firstHelper = m;
            return firstHelper;
        }
        string Line(int before, int after, int required, int noise, int bonus, bool completes) =>
            string.Format(ForecastFormat, before, after, required, noise) + (bonus > 0 ? string.Format(ForecastBonus, bonus) : "") + (completes ? ForecastDone : "");
        string Warn(string s) => "<color=#" + ColorUtility.ToHtmlStringRGB(WarnInk) + ">" + s + "</color>";

        // The face row: members who still have an action (board: hushing this turn · first visit: anyone for the lead, anyone but
        // the lead for the helper), plus whoever is in the slot now (pressing them again empties it).
        void BuildFaces(ExpeditionArrivalPanel a)
        {
            faceMembers.Clear(); int which = Picking, current = which == 0 ? LeadShown : HelperShown;
            var pl = Planner; var k = Board ? pl.Plan.Check(pl.Facts()) : null; bool hasOrder = Board && pl.Plan.Find(Site) != null;
            var enabled = new List<bool>();
            for (int m = 0; m < a.Participants.Count; m++)
            {
                if (!Alive(a, m)) continue;
                bool free = Board ? m < k.Actions.Length && k.Actions[m] == FieldAction.Hush : which == 0 || m != LeadShown;
                if (!free && m != current) continue;
                bool ok = m == current ? (Board ? which == 0 || ClearableHelper() : which == 0)
                    : Board ? Quick && (which == 0 || !hasOrder ? Quick.LeadFor(Site, m) != null : Quick.JoinFor(Site, m) != null) : true;
                faceMembers.Add(m); enabled.Add(ok);
            }
            while (facePool.Count < faceMembers.Count && facePool.Count > 0 && facePool[0].Button)
            {
                var src = facePool[0]; var copy = Instantiate(src.Button.gameObject, src.Button.transform.parent); copy.name = "Face_" + facePool.Count;
                var f = new Face { Button = copy.GetComponent<Button>() };
                if (src.Portrait) f.Portrait = Twin<Image>(src.Button.transform, src.Portrait.transform, copy.transform);
                if (src.Current) f.Current = Twin<Graphic>(src.Button.transform, src.Current.transform, copy.transform);
                AddFace(f);
            }
            for (int i = 0; i < facePool.Count; i++)
            {
                var f = facePool[i]; if (f == null || !f.Button) continue; bool on = i < faceMembers.Count;
                if (f.Button.gameObject.activeSelf != on) f.Button.gameObject.SetActive(on); if (!on) continue;
                int m = faceMembers[i];
                if (f.Portrait) { var sprite = Portrait(a, m); if (f.Portrait.sprite != sprite) f.Portrait.sprite = sprite; var c = f.Portrait.color; c.a = enabled[i] ? 1 : FaceDisabledAlpha; if (f.Portrait.color != c) f.Portrait.color = c; }
                if (f.Current && f.Current.gameObject.activeSelf != (m == current)) f.Current.gameObject.SetActive(m == current);
                if (f.Button.interactable != enabled[i]) f.Button.interactable = enabled[i];
            }
            Set(PickerTitle, faceMembers.Count == 0 ? PickNone : which == 0 ? PickLeadTitle : PickHelperTitle);
        }
        bool ClearableHelper()
        {
            var pl = Planner; var order = pl.Plan.Find(Site); if (order == null) return false;
            var r = BoardDraft(pl, pl.Facts(), SoloOrder(order)); return r != null && r.Support < 0;
        }
        static T Twin<T>(Transform root, Transform part, Transform copy) where T : Component
        {
            var path = new List<int>(); for (var t = part; t && t != root; t = t.parent) path.Insert(0, t.GetSiblingIndex());
            var at = copy; foreach (int i in path) { if (i >= at.childCount) return null; at = at.GetChild(i); }
            return at.GetComponent<T>();
        }

        void ShowSlot(Slot slot, ExpeditionArrivalPanel a, int member, string name, bool picking)
        {
            if (slot == null) return; bool filled = member >= 0;
            if (slot.Portrait)
            {
                if (slot.Portrait.gameObject.activeSelf != filled) slot.Portrait.gameObject.SetActive(filled);
                var sprite = filled ? Portrait(a, member) : null; if (filled && slot.Portrait.sprite != sprite) slot.Portrait.sprite = sprite;
            }
            if (slot.Empty && slot.Empty.activeSelf == filled) slot.Empty.SetActive(!filled);
            Set(slot.Label, name);
            if (slot.Caption) { bool on = !filled && !string.IsNullOrEmpty(EmptyCaption); if (slot.Caption.gameObject.activeSelf != on) slot.Caption.gameObject.SetActive(on); if (on) Set(slot.Caption, EmptyCaption); }
            if (slot.Frame) slot.Frame.SetStyle(picking ? SlotPicking : filled ? SlotFilledBorder : SlotEmptyBorder, SlotBorder, filled || picking ? 0 : SlotDash);
            if (slot.Paper) { var c = filled ? SlotFilledPaper : SlotEmptyPaper; if (slot.Paper.color != c) slot.Paper.color = c; }
        }
        void ShowChip(int d, bool lit, bool enabled, string line)
        {
            if (Roles == null || d >= Roles.Length || Roles[d] == null) return; var c = Roles[d];
            Set(c.Title, d < RoleNames.Length ? RoleNames[d] : ""); Set(c.Line, line);
            if (c.Button && c.Button.interactable != enabled) c.Button.interactable = enabled;
            var paper = lit ? ChipOn : enabled ? ChipOff : ChipDisabled; if (c.Paper && c.Paper.color != paper) c.Paper.color = paper;
            var ink = lit || enabled ? Ink : InkDisabled; if (c.Title && c.Title.color != ink) c.Title.color = ink; if (c.Line && c.Line.color != ink) c.Line.color = ink;
        }
        void ShowRoles(bool on) { if (Roles != null) foreach (var c in Roles) if (c != null && c.Button && c.Button.gameObject.activeSelf != on) c.Button.gameObject.SetActive(on); }
        void Hint(bool on, string text) { if (!HelperHint) return; if (HelperHint.gameObject.activeSelf != on) HelperHint.gameObject.SetActive(on); if (on) Set(HelperHint, text); }
        void SetForecast(string s) { if (!Forecast) return; Set(Forecast, s); if (Forecast.color != Ink) Forecast.color = Ink; }
        static void Set(Text t, string s) { if (t && t.text != s) t.text = s; }
        static void Label(Button b, string s) { if (b) Set(b.GetComponentInChildren<Text>(true), s); }

        // ---- where the paper goes ----
        // Beside the object (the side that covers least: the object itself, its bubble or the place its bubble will take, then other
        // bubbles and marks), inside the room area above the tray, the tail from the paper's edge to the marker. Placed on open;
        // moved again only when what it covers changes (a bubble appears), so it does not follow every frame.
        void Place(bool force)
        {
            var a = Arrival; var button = FieldQuickAssign.SiteButtonOf(a, Site); if (!Paper || !button) return;
            var obj = RectOf((RectTransform)button.transform); var mark = MarkerRect(button, obj); var area = Area; var size = Paper.rect.size;
            Obstacles(a, button, mark);
            var now = new Rect((Vector2)Paper.localPosition - size * .5f, size);
            if (!force && placed && Score(now, obj, area) < 1) { PlaceTail(now, PlacedSide, mark); return; }
            float best = float.MaxValue; Rect pick = now; Side side = Side.Right;
            for (int n = 0; SideOrder != null && n < SideOrder.Length; n++)
            {
                var r = Candidate(SideOrder[n], obj, mark, size, area); float score = Score(r, obj, area) + n * .5f;
                if (score < best) { best = score; pick = r; side = SideOrder[n]; }
            }
            var at = new Vector3(pick.center.x, pick.center.y, 0); if (Paper.localPosition != at) Paper.localPosition = at;
            PlacedSide = side; placed = true; PlaceTail(pick, side, mark);
        }
        Rect Candidate(Side side, Rect obj, Rect mark, Vector2 size, Rect area)
        {
            float g = Gap + TailLength; Vector2 c;
            switch (side)
            {
                case Side.Right: c = new Vector2(obj.xMax + g + size.x * .5f, mark.center.y); break;
                case Side.Left: c = new Vector2(obj.xMin - g - size.x * .5f, mark.center.y); break;
                case Side.Above: c = new Vector2(mark.center.x, obj.yMax + g + size.y * .5f); break;
                default: c = new Vector2(mark.center.x, obj.yMin - g - size.y * .5f); break;
            }
            c.x = Mathf.Clamp(c.x, area.xMin + size.x * .5f, Mathf.Max(area.xMin + size.x * .5f, area.xMax - size.x * .5f));
            c.y = Mathf.Clamp(c.y, area.yMin + size.y * .5f, Mathf.Max(area.yMin + size.y * .5f, area.yMax - size.y * .5f));
            return new Rect(c - size * .5f, size);
        }
        float Score(Rect r, Rect obj, Rect area)
        {
            float s = 100 * Overlap(r, obj) + 100 * (r.width * r.height - Overlap(r, area));
            foreach (var h in hard) s += 20 * Overlap(r, h);
            foreach (var o in soft) s += Overlap(r, o);
            foreach (var o in others) s += ObjectWeight * Overlap(r, o);
            return s;
        }
        // Hard: this object's bubble (or the place it will take). Soft: other bubbles, other objects' marks and door names.
        void Obstacles(ExpeditionArrivalPanel a, Button own, Rect mark)
        {
            hard.Clear(); soft.Clear(); others.Clear(); string key = FieldPlanTargetMarkers.SearchKey(Site); bool bubble = false;
            if (Markers)
                foreach (var s in Markers.Placed)
                {
                    if (!s.Bubble || !s.Bubble.Visible) continue; var r = Convert(Markers.transform, s.Bounds);
                    if (s.Key == key) { hard.Add(r); bubble = true; } else soft.Add(r);
                }
            if (!bubble) hard.Add(new Rect(mark.center.x - BubbleReserve.x * .5f, mark.yMax, BubbleReserve.x, BubbleReserve.y));
            bool stale = hotspots == null; if (!stale) foreach (var h in hotspots) if (!h) { stale = true; break; }
            if (stale && a.Main) hotspots = a.Main.GetComponentsInChildren<ExplorationHotspot>(true);
            if (hotspots != null)
                foreach (var h in hotspots)
                {
                    if (!h || !h.isActiveAndEnabled || h.transform == own.transform) continue;
                    if (h.Marker && h.Marker.gameObject.activeInHierarchy) soft.Add(RectOf(h.Marker.rectTransform));
                    // The rest of the object too (lighter): a note over another object would take the press meant for it.
                    if (!h.IsDoor && h.Button && h.Button.gameObject.activeInHierarchy) others.Add(RectOf((RectTransform)h.Button.transform));
                    if (h.IsDoor && h.Caption && h.Caption.gameObject.activeInHierarchy && h.Caption.alpha > .01f) soft.Add(RectOf((RectTransform)h.Caption.transform));
                }
        }
        void PlaceTail(Rect paper, Side side, Rect mark)
        {
            if (!Tail) return;
            const float inset = 6; var target = mark.center; float pad = TailHalfWidth + 10; Vector2 at, along;
            float y = Mathf.Clamp(target.y, paper.yMin + pad, Mathf.Max(paper.yMin + pad, paper.yMax - pad)), x = Mathf.Clamp(target.x, paper.xMin + pad, Mathf.Max(paper.xMin + pad, paper.xMax - pad));
            switch (side)
            {
                case Side.Right: at = new Vector2(paper.xMin + inset, y); along = Vector2.up; break;
                case Side.Left: at = new Vector2(paper.xMax - inset, y); along = Vector2.up; break;
                case Side.Above: at = new Vector2(x, paper.yMin + inset); along = Vector2.right; break;
                default: at = new Vector2(x, paper.yMax - inset); along = Vector2.right; break;
            }
            var dir = target - at; float length = Mathf.Min(TailLength + inset, dir.magnitude);
            Tail.Set(at, along, TailHalfWidth, dir.sqrMagnitude > 1 ? at + dir.normalized * length : at);
        }
        // The object's 40 px paper mark (the tail points at it), else the object itself.
        Rect MarkerRect(Button button, Rect fallback)
        {
            var marker = button.transform.Find("ExplorationMarkerPaper") as RectTransform;
            return marker && marker.gameObject.activeInHierarchy ? RectOf(marker) : fallback;
        }
        // A rect in this node's space.
        public Rect RectOf(RectTransform r)
        {
            r.GetWorldCorners(corners); var a = transform.InverseTransformPoint(corners[0]); var b = transform.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
        Rect Convert(Transform from, Rect r)
        {
            var a = transform.InverseTransformPoint(from.TransformPoint(new Vector3(r.xMin, r.yMin, 0))); var b = transform.InverseTransformPoint(from.TransformPoint(new Vector3(r.xMax, r.yMax, 0)));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
        static float Overlap(Rect a, Rect b) { float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin); return w > 0 && h > 0 ? w * h : 0; }

        // ---- members ----
        static bool Alive(ExpeditionArrivalPanel a, int m) => a && m >= 0 && m < a.Participants.Count && a.Participants[m] != null && a.Participants[m].Health > 0;
        static int Living(ExpeditionArrivalPanel a) { int n = 0; for (int i = 0; i < a.Participants.Count; i++) if (Alive(a, i)) n++; return n; }
        static int IndexOf(ExpeditionArrivalPanel a, Adventurer p) { if (p == null) return -1; for (int i = 0; i < a.Participants.Count; i++) if (a.Participants[i] == p) return i; return -1; }
        Adventurer LeadPerson(ExpeditionArrivalPanel a) => Alive(a, firstLead) ? a.Participants[firstLead] : null;
        static Sprite Portrait(ExpeditionArrivalPanel a, int m) => m >= 0 && m < a.Cards.Count && a.Cards[m] && a.Cards[m].Portrait ? a.Cards[m].Portrait.sprite : null;
        static string SiteName(ExpeditionArrivalPanel a, int site) => a && a.ObjectNames != null && site >= 0 && site < a.ObjectNames.Length ? a.ObjectNames[site] : "";
        string RoleName(int duty) => RoleNames != null && duty >= 0 && duty < RoleNames.Length ? RoleNames[duty] : "";
        static string ToolName(ExpeditionArrivalPanel a, string tool) => a.Inventory && a.Inventory.Items != null ? a.Inventory.Items.FirstOrDefault(i => i.Id == tool)?.Name ?? tool : tool;
    }
}
