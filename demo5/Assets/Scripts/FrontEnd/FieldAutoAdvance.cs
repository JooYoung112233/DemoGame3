using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // '계속 진행 ▶▶' on every visit (FieldTurnPlanner.Placing, the first included · 말 놓기): while on, it presses '턴 진행' itself,
    // one turn per turn sequence (FieldTurnReplay) plus a short gap, and only while the room is free. It switches itself
    // off with a stamp '멈춤 · <이유>' when nothing is assigned, a window opens, the room changes, a search/observation
    // completes, an assignment is released, the status paper shows a new warning, or the next turn's preview shows
    // a meeting or a danger rise. On the first visit (it sleeps) it also stops before a search turn whose random-encounter chance
    // reaches the warning strip's threshold (FieldTurnWarning.FirstVisitChance) and when the footsteps are first heard.
    // With members who have nothing to do it asks once when switched on (FieldIdleConfirm, 2026-09-25) and never per turn: its own
    // presses and the player's presses while it is on pass without asking.
    // Temporary layout: there is no approved mock for it.
    [DefaultExecutionOrder(200)]
    public sealed class FieldAutoAdvance : MonoBehaviour
    {
        public ExpeditionArrivalPanel Arrival;
        public FieldTurnReplay Replay;
        [Tooltip("할 일이 없는 대원 확인 · 켤 때 한 번 묻습니다 (비어 있으면 도착 화면에서 찾습니다)")] public FieldIdleConfirm IdleConfirm;
        [Tooltip("첫 방문 조우 확률 문턱을 읽는 경고 띠 (비어 있으면 도착 화면에서 찾습니다 · 없으면 25%)")] public FieldTurnWarning Warning;
        [Header("계속 진행 버튼")]
        public Button Toggle;
        public Image Paper;
        public Text Label;
        public FieldForwardGlyph Glyph;
        [Tooltip("꺼져 있을 때 버튼 문구")] public string OffLabel = "계속 진행";
        [Tooltip("켜져 있을 때 버튼 문구 (누르면 멈춤)")] public string OnLabel = "멈추기";
        [Tooltip("꺼짐 / 켜짐 종이 색")] public Color OffPaper = new Color(.93f, .9f, .8f), OnPaper = new Color(.95f, .74f, .38f);
        [Tooltip("꺼짐 / 켜짐 글자 색")] public Color OffInk = new Color(.2f, .18f, .15f), OnInk = new Color(.2f, .14f, .08f);
        [Header("멈춤 도장")]
        public CanvasGroup Stamp;
        public Text StampLabel;
        [Tooltip("도장 문구 ({0}: 멈춘 이유)")] public string StampFormat = "멈춤 · {0}";
        [Tooltip("도장이 보이는 시간 (초)")] [Min(.2f)] public float StampSeconds = 2f;
        [Tooltip("도장이 나타나고 사라지는 시간 (초)")] [Min(0)] public float StampFade = .2f;
        [Tooltip("창이 열려 있으면 닫힐 때까지 도장을 미룹니다 · 최대 대기 (초)")] [Min(0)] public float StampWait = 30f;
        [Header("시간 · 초")]
        [Tooltip("턴 연출이 끝난 뒤 다음 턴까지 쉬는 시간 (턴 버튼의 연속 입력 방지 시간보다 짧아지지 않습니다)")] [Min(0)] public float Gap = .4f;
        [Tooltip("턴 연출이 없거나 꺼져 있을 때 턴 사이 최소 간격")] [Min(.2f)] public float FallbackInterval = 1.4f;
        [Header("멈춤 이유")]
        [Tooltip("맡긴 일도 이동 예약도 없을 때")] public string StopNothing = "맡긴 일이 없습니다";
        [Tooltip("조우 창이 열렸을 때")] public string StopEncounter = "무언가와 마주침";
        [Tooltip("원정대가 다른 방으로 옮겼을 때")] public string StopRoom = "방을 옮김";
        [Tooltip("다른 창이 열렸거나 지금 행동할 수 없을 때")] public string StopWindow = "창이 열림";
        [Tooltip("대화가 열렸을 때")] public string StopStory = "대화";
        [Tooltip("발견물 창이 열렸거나 확인할 발견물이 남았을 때")] public string StopLoot = "발견물 확인";
        [Tooltip("지난 턴에 배정이 풀렸을 때")] public string StopReleased = "배정 해제";
        [Tooltip("지난 턴에 흔적 관찰을 마쳤을 때")] public string StopObserved = "관찰 완료";
        [Tooltip("지난 턴에 가방 물건을 썼을 때 (그 대원의 행동이 끝남)")] public string StopUsed = "가방 사용 완료";
        [Tooltip("다음 턴 미리보기에 조우가 보일 때")] public string StopMeetAhead = "이번 턴 조우 예고";
        [Tooltip("예약한 이동의 미리보기에 조우가 보일 때")] public string StopMoveMeets = "들어가면 마주침";
        [Tooltip("다음 턴 미리보기에서 위험도가 오를 때")] public string StopDangerAhead = "위험도 상승 예고";
        [Tooltip("턴 버튼을 눌러도 턴이 흐르지 않았을 때")] public string StopStuck = "진행할 수 없음";
        [Tooltip("수색 완료 ({0}: 사물 이름)")] public string StopSearchDone = "{0} 수색 완료";
        [Header("멈춤 이유 · 상황판 경고")]
        [Tooltip("무언가가 숨죽인 원정대를 지나쳤을 때")] public string StopPassedBy = "무언가가 지나감";
        [Tooltip("무언가가 이 방에 보일 때")] public string StopVisible = "무언가가 이 방에 있음";
        [Tooltip("다음 턴 무언가가 이 방에 들어올 때")] public string StopIncoming = "다음 턴 무언가 들어옴";
        [Tooltip("위험도가 올라 추적이 시작됐을 때")] public string StopHunt = "추적 시작";
        [Tooltip("귀 대기로 문 너머의 무언가를 들었을 때")] public string StopHeard = "문 너머에 무언가";
        [Tooltip("옆방에서 발소리가 들릴 때")] public string StopFootsteps = "발소리";
        [Tooltip("관리실이 비었지만 곧 돌아올 때")] public string StopDenReturning = "관리실로 돌아오는 중";
        [Tooltip("관리실이 빈 것을 처음 보였을 때 (계속 비어 있는 동안 다시 멈추지 않음)")] public string StopDenEmpty = "관리실이 빔";
        [Tooltip("무언가가 달아난 것을 처음 보였을 때 (다시 멈추지 않음)")] public string StopDenGone = "무언가가 달아남";
        [Tooltip("큰 소리로 무언가가 이 방을 기억할 때")] public string StopNoted = "큰 소리 · 방을 기억함";
        [Tooltip("지난 턴에 위험도가 올랐을 때")] public string StopDangerRose = "위험도 오름";
        [Tooltip("다음 턴 오래 머묾으로 위험도가 오를 때")] public string StopLinger = "오래 머묾 예고";
        [Tooltip("장소 시계를 다 쓴 것을 처음 보였을 때 (다시 멈추지 않음)")] public string StopOvertime = "장소 시계 초과";
        [Header("멈춤 이유 · 첫 방문 (그것이 잠든 방문)")]
        [Tooltip("이번 턴 수색의 무작위 조우 확률이 경고 띠 문턱 이상일 때 ({0}: %)")] public string StopChanceAhead = "조우 {0}% 예고";
        [Tooltip("가까운 발소리(첫 방문 경고)가 처음 들렸을 때")] public string StopWarned = "가까운 발소리";
        [Tooltip("상황판에 이 목록에 없는 경고가 떴을 때")] public string StopOther = "상황 변화";

        public bool On { get; private set; }
        // Why it last switched itself off ("" when the player switched it off).
        public string LastStop { get; private set; } = "";
        // Turns it pressed since it was last switched on.
        public int AutoTurns { get; private set; }
        public bool StampShowing => Stamp && Stamp.gameObject.activeSelf;

        int baseTurns = -1, room = -1, assigned, turnsSeen = -1; float turnAt = -100, stampAt = -100, stampQueued = -100; FieldPlanCheck check; bool wired, stampPending, warned; string seenStatus;
        // Standing lines (the den out, overtime) the paper has already shown, with their exact text: they do not stop it again
        // until they end (the paper shows no warning or one below them in StatusLine order).
        readonly Dictionary<FieldAutoStop, string> shownStanding = new Dictionary<FieldAutoStop, string>(); readonly List<FieldAutoStop> ended = new List<FieldAutoStop>();

        void LateUpdate()
        {
            if (!wired && Toggle) { Toggle.onClick.AddListener(Flip); wired = true; Paint(); }
            var a = Arrival; var t = a ? a.Threat : null; var pl = t ? t.Planner : null;
            bool board = a && a.IsOpen && a.Rooms && pl && pl.Placing;
            bool show = board && pl.TurnButton && pl.TurnButton.gameObject.activeSelf;
            if (Toggle)
            {
                if (Toggle.gameObject.activeSelf != show) Toggle.gameObject.SetActive(show);
                bool can = On || show && t.CanAct && !pl.Resolving;
                if (show && Toggle.interactable != can) Toggle.interactable = can;
            }
            UpdateStamp(a, t);
            // When the last turn passed, whoever pressed it (the next press keeps clear of the planner's cooldown).
            if (a && a.Rooms && a.Rooms.Turns != turnsSeen) { turnsSeen = a.Rooms.Turns; turnAt = Time.unscaledTime; }
            if (!On)
            {
                // Off, the player watches the paper: what it shows counts as shown.
                if (board && a.Status && a.Status.text != seenStatus) { seenStatus = a.Status.text; Seen(t, true, out _); }
                return;
            }
            if (!a || !a.IsOpen) { Switch(false, null); return; }
            if (!board) { Switch(false, a.Encounter && a.Encounter.IsOpen ? StopEncounter : StopWindow); return; }
            // A turn passed (pressed here or by the player): judge it once the room has settled.
            if (a.Rooms.Turns != baseTurns)
            {
                if (a.InTransit || pl.Resolving) return;
                var after = AfterTurn(a, t, pl); baseTurns = a.Rooms.Turns;
                if (after != null) { Switch(false, after); return; }
                Snapshot(a, t, pl);
            }
            // One turn per sequence: wait for its replay to end, then a short gap (a fixed interval when the replay is off),
            // and never inside the planner's press cooldown (TryLock would refuse the press).
            float now = Time.unscaledTime; bool replay = Replay && Replay.isActiveAndEnabled;
            if (replay ? Replay.Playing || now < Replay.LastEnd + Gap : now < turnAt + FallbackInterval) return;
            if (now < turnAt + pl.TurnCooldown) return;
            if (a.InTransit || pl.Resolving) return;
            var before = BeforeTurn(a, t, pl); if (before != null) { Switch(false, before); return; }
            if (!pl.TurnButton.interactable) return;
            Snapshot(a, t, pl); turnAt = now; int turns = a.Rooms.Turns;
            FieldIdleConfirm.Pass(() => pl.TurnButton.onClick.Invoke()); // asked once when switched on, never per turn
            if (a.Rooms.Turns == turns) Switch(false, StopStuck); else AutoTurns++;
        }

        // The player's press: on (it may stop at once, e.g. nothing assigned) or off. When it would run and a member has nothing
        // to do, it asks first (once); it starts on '숨죽이고 계속' (StartAfterAsking), '돌아가기' leaves it off.
        public void Flip()
        {
            if (On) { Switch(false, null); return; }
            var a = Arrival; var t = a ? a.Threat : null; var pl = t ? t.Planner : null;
            if (!a || !a.IsOpen || !a.Rooms || !pl || !pl.Placing) return;
            var ask = IdleConfirm ? IdleConfirm : a.GetComponent<FieldIdleConfirm>();
            if (ask && BeforeTurn(a, t, pl) == null && ask.AskKeepGoing(pl)) return;
            Begin(a, t, pl);
        }
        // FieldIdleConfirm's '숨죽이고 계속'.
        public void StartAfterAsking()
        {
            var a = Arrival; var t = a ? a.Threat : null; var pl = t ? t.Planner : null;
            if (On || !a || !a.IsOpen || !a.Rooms || !pl || !pl.Placing) return;
            Begin(a, t, pl);
        }
        void Begin(ExpeditionArrivalPanel a, ExpeditionSiteThreat t, FieldTurnPlanner pl)
        {
            On = true; AutoTurns = 0; LastStop = ""; baseTurns = a.Rooms.Turns; Snapshot(a, t, pl); Paint();
            var why = BeforeTurn(a, t, pl); if (why != null) Switch(false, why);
        }
        void Switch(bool on, string reason)
        {
            On = on; LastStop = reason ?? ""; seenStatus = null;
            if (!on && !string.IsNullOrEmpty(reason) && Stamp) { stampPending = true; stampQueued = Time.unscaledTime; if (StampLabel) StampLabel.text = string.Format(StampFormat, reason); }
            Paint();
        }
        void Paint()
        {
            if (Label) { Label.text = On ? OnLabel : OffLabel; Label.color = On ? OnInk : OffInk; }
            if (Paper) Paper.color = On ? OnPaper : OffPaper;
            if (Glyph) { Glyph.Bars = On; Glyph.color = On ? OnInk : OffInk; }
        }
        void Snapshot(ExpeditionArrivalPanel a, ExpeditionSiteThreat t, FieldTurnPlanner pl)
        {
            room = a.Rooms.CurrentRoom; check = pl.LastCheck; assigned = Count(pl.Plan); warned = a.Encounter && a.Encounter.Warned; Seen(t, true, out _);
        }
        static int Count(FieldTurnPlan p) => p.Orders.Count + p.Listens.Count + p.Observations.Count + p.Gathers.Count + p.Uses.Count;
        // The first visit's random encounter chance (%) at which it stops before pressing (the warning strip's own threshold).
        int FirstVisitThreshold(ExpeditionArrivalPanel a)
        {
            if (!Warning && a.Main) Warning = a.Main.GetComponentInChildren<FieldTurnWarning>(true);
            return Warning ? Warning.FirstVisitChance : 25;
        }
        static bool Standing(FieldAutoStop kind) => kind == FieldAutoStop.DenEmpty || kind == FieldAutoStop.DenGone || kind == FieldAutoStop.Overtime;
        // The paper's warning now. Forgets standing lines that ended; with remember, keeps the standing line it shows as shown.
        FieldAutoStop Seen(ExpeditionSiteThreat t, bool remember, out string line)
        {
            var kind = t.AutoStopKind(out line);
            if (shownStanding.Count > 0 && kind != FieldAutoStop.Other)
            {
                ended.Clear(); foreach (var k in shownStanding.Keys) if (kind == FieldAutoStop.None || kind > k) ended.Add(k);
                foreach (var k in ended) shownStanding.Remove(k);
            }
            if (remember && Standing(kind)) shownStanding[kind] = line;
            return kind;
        }

        // Before pressing: the room must be free, something must be planned, and the next turn must not meet it or raise danger.
        string BeforeTurn(ExpeditionArrivalPanel a, ExpeditionSiteThreat t, FieldTurnPlanner pl)
        {
            if (a.Encounter && a.Encounter.IsOpen) return StopEncounter;
            if (a.Loot && a.Loot.IsOpen || pl.PendingLoot > 0) return StopLoot;
            if (a.Story && a.Story.IsOpen) return StopStory;
            if (!t.CanAct) return StopWindow;
            bool move = a.Rooms.HasQueuedMove;
            if (!pl.Plan.HasAssignments && !move) return StopNothing;
            var o = move ? a.Rooms.QueuedMoveOutlook() : pl.Forecast(pl.Plan).outlook;
            if (o != null && o.Encounter) return move ? StopMoveMeets : StopMeetAhead;
            if (o != null && t.Active && o.Danger > t.State.Danger) return StopDangerAhead; // danger is hidden on the first visit (it sleeps)
            // First visit: the old random encounter is not in the site forecast; its chance for this search turn is.
            if (!move && t.State.Asleep) { int chance = pl.EncounterChance(pl.Current); if (chance > 0 && chance >= FirstVisitThreshold(a)) return string.Format(StopChanceAhead, chance); }
            return null;
        }
        // After a turn: what happened in it (the most specific reason first).
        string AfterTurn(ExpeditionArrivalPanel a, ExpeditionSiteThreat t, FieldTurnPlanner pl)
        {
            if (a.Encounter && a.Encounter.IsOpen) return StopEncounter;
            if (a.Rooms.CurrentRoom != room) return StopRoom;
            if (a.Encounter && a.Encounter.Warned && !warned) return StopWarned;
            var k = pl.LastCheck;
            if (k != null && k != check)
            {
                var done = k.Runs.FirstOrDefault(r => r.Completes);
                if (done != null) return string.Format(StopSearchDone, a.ObjectNames != null && done.Site < a.ObjectNames.Length ? a.ObjectNames[done.Site] : "");
                if (k.Observations.Count > 0) return StopObserved;
                if (k.Uses.Count > 0) return StopUsed;
            }
            if (a.Loot && a.Loot.IsOpen || pl.PendingLoot > 0) return StopLoot;
            if (a.Story && a.Story.IsOpen) return StopStory;
            if (Count(pl.Plan) < assigned) return StopReleased;
            // A warning on the status paper stops it; a standing line already shown (the den out, overtime) does not again while
            // it lasts, even after another warning covered it for a while, and only while its text is the same.
            var kind = Seen(t, false, out var line);
            if (kind != FieldAutoStop.None && !(shownStanding.TryGetValue(kind, out var shown) && shown == line)) return Reason(kind);
            if (!t.CanAct) return StopWindow;
            return null;
        }
        string Reason(FieldAutoStop kind) => kind switch
        {
            FieldAutoStop.PassedBy => StopPassedBy, FieldAutoStop.Visible => StopVisible, FieldAutoStop.Incoming => StopIncoming, FieldAutoStop.Hunt => StopHunt,
            FieldAutoStop.HeardBehindDoor => StopHeard, FieldAutoStop.Footsteps => StopFootsteps, FieldAutoStop.DenReturning => StopDenReturning, FieldAutoStop.DenEmpty => StopDenEmpty,
            FieldAutoStop.DenGone => StopDenGone, FieldAutoStop.Noted => StopNoted, FieldAutoStop.DangerRose => StopDangerRose, FieldAutoStop.Linger => StopLinger,
            FieldAutoStop.Overtime => StopOvertime, _ => StopOther
        };

        // The stamp waits for a free room (a loot window or a meeting may have caused the stop), then shows for a moment.
        void UpdateStamp(ExpeditionArrivalPanel a, ExpeditionSiteThreat t)
        {
            if (!Stamp) return;
            float now = Time.unscaledTime;
            if (stampPending)
            {
                bool free = a && a.IsOpen && (t ? t.CanAct : !a.InTransit && !a.Popup.activeSelf);
                if (!a || !a.IsOpen || now - stampQueued > StampWait) stampPending = false;
                else if (free) { stampPending = false; stampAt = now; if (!Stamp.gameObject.activeSelf) Stamp.gameObject.SetActive(true); }
            }
            if (!Stamp.gameObject.activeSelf) return;
            float u = now - stampAt;
            if (u >= StampSeconds || !a || !a.IsOpen) { Stamp.alpha = 0; Stamp.gameObject.SetActive(false); return; }
            Stamp.alpha = StampFade <= 0 ? 1 : Mathf.Clamp01(Mathf.Min(u, StampSeconds - u) / StampFade);
        }
    }
}
