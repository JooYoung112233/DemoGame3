using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Every string of the member-slot screens (Inspector, ExpeditionArrivalPanel.prefab → FieldTurnPlanner).
    [Serializable] public sealed class FieldTurnTexts
    {
        [Header("턴 진행 버튼")]
        public string TurnTitle = "턴 진행";
        public string TurnSubtitle = "배정 확인 후 시간 진행", TurnHushSubtitle = "모두 숨죽이기 · 1턴";
        [Header("방 화면 대원 카드")]
        public string TagHush = "숨죽이기";
        public string TagLead = "수색 담당", TagTogether = "동행", TagWatch = "망보기", TagLight = "조명 지원", TagDown = "쓰러짐", TagPaused = "멈춤";
        public string RoleLead = "{0} {1}/{2}", RoleSupport = "{0} 지원", RoleHush = "소리 없이 대기", RoleDown = "행동 불가", RolePaused = "멈춤 · {0}", RoleIdle = "현장 가방";
        [Header("멈춘 이유 (짧게)")]
        public string PauseDowned = "쓰러짐";
        public string PauseDen = "선반 닫힘", PauseTool = "{0} 없음", PauseLight = "손전등 동료 없음", PauseComplete = "완료", PauseRoom = "다른 방";
        [Header("미리보기 두 줄")]
        public string ChipNoise = "소음 +{0} → {1}";
        public string ChipDanger = " · 위험도 {0}", ChipMeetPass = "조우 · 모두 숨죽이면 지나감", ChipMeet = "이번 턴 조우", ChipPaused = "멈춤 {0} · {1}", ChipForfeit = "함께 수색 보정 −{0}%p 사라짐",
            ChipPass = "무언가가 지나감", ChipNoted = "큰 소리 · 이 방을 기억", ChipDen = "이번 턴 뒤 선반 닫힘", ChipIncoming = "다음 턴 무언가 들어옴", ChipDoneOne = "{0} 완료", ChipDoneMany = "완료 {0}곳",
            ChipRunning = "수색 {0}곳 · 숨죽이기 {1}명", ChipHushAll = "모두 숨죽이기";
        [Header("상황판")]
        [TextArea(2, 3)] public string StatusSaved = "{0} 배정 저장\n'턴 진행'을 누르면 함께 진행됩니다.";
        [TextArea(2, 3)] public string StatusRan = "수색 {0}곳 진행 · {1}곳 완료\n배정 유지 · '턴 진행'으로 계속", StatusDone = "수색 {0}곳 진행 · {1}곳 완료\n발견물을 확인하세요.",
            StatusReleased = "{0} 배정 해제\n{1}", StatusHushed = "모두 숨죽이고 한 턴을 보냈습니다.\n배정은 그대로입니다.", StatusSearching = "{0} · 수색 중\n진행도 유지 · 다음 턴 대기";
        public string ReleaseDowned = "담당이 쓰러졌습니다.", ReleaseDen = "관리실에 무언가 돌아왔습니다.", ReleaseTool = "담당 가방에 도구가 없습니다.", ReleaseLight = "손전등을 든 동료가 없습니다.";
        public string MoveSuffix = " · 배정 해제";
        [Header("수색 지정 창 (두 번째 방문부터)")]
        public string ChooseAssign = "행동 배정";
        public string ChooseNext = "다음 1턴 진행", ChooseRelease = "배정 해제", ChooseWorker = "담당자 선택", ChooseTool = "{0} 필요", ChooseLight = "손전등 동료 필요", ChooseDen = "선반 닫힘";
        public string CostLine = "수색도 {0} / {1}턴  ·  이번 턴 소음 +{2}";
        // CostTogether {0}: 동료 · CostWatch {0}: 동료, {1}: 사물 소음, {2}: 망보기 뒤 소음 · CostLight {0}: 동료, {1}: 조명 보정 (%p).
        public string CostTogether = "함께 수색 · {0} · 1턴 빨리", CostWatch = "망보기 · {0} · 소음 {1}→{2}", CostLight = "조명 · {0} · 조명 +{1}%p", CostSolo = "혼자 수색 · 지원 없음",
            CostSoloBusy = "혼자 수색 · 빈 대원 없음", CostForfeit = "함께 수색 · 동료 없음 · 보정 +{0}%p 사라짐", CostWatchBusy = "망보기 · 빈 대원 없음 · 소음 감소 없음", CostLightNone = "조명 · 손전등 동료 없음";
        [Tooltip("속도 삭제 전 저장의 진행 중 함께 수색 ({0}: 동료 · {1}: 발견 보정 %p)")] public string CostTogetherBonus = "함께 수색 · {0} · 발견 +{1}%p";
        [Tooltip("소음 없는 사물에 망보기 (새 수색에서는 고를 수 없음)")] public string CostWatchSilent = "망보기 · 소음 없는 사물 · 필요 없음";
        public string NoticeMoved = "{0} · {1} 배정에서 빠집니다.", NoticeReleased = "배정을 해제했습니다 · 진행도는 유지됩니다.", NoticeLeadAgain = "담당 카드를 다시 누르면 배정을 해제합니다.",
            NoticeDutyAgain = "선택한 역할을 다시 누르면 혼자 수색합니다.", NoticeSolo = "역할을 누르면 동료가 함께합니다.", NoticeNoFree = "빈 대원이 없어 혼자 수색합니다.",
            NoticeRelease = "'배정 해제'를 누르면 시간 없이 해제됩니다.", NoticeTool = "{0}를 가진 대원을 담당으로 고르세요.", NoticeLight = "손전등을 가진 동료가 있어야 조명 수색을 이어 갈 수 있습니다.", NoticeDen = "관리실에 무언가가 있습니다 · 굴이 빌 때만 뒤질 수 있습니다.", NoticeLocked = "진행 중인 수색은 역할과 남은 턴이 고정됩니다.";
        public string CardLead = "수색 담당", CardTogether = "동행", CardWatch = "망보기", CardLight = "조명 지원", CardBusy = "다른 사물", CardDown = "쓰러짐";
        [Header("수색 지정 확인창")]
        [Tooltip("{0}: 사물 · {1}: 이번 턴 소음 (ExpeditionSearchPanel.ReviewNoise / ReviewQuiet)")] public string ReviewTitle = "{0}  ·  {1}";
        public string ReviewLead = "담당  {0}", ReviewTogether = "동료  함께 수색 · {0}", ReviewWatch = "동료  망보기 · {0}", ReviewLight = "동료  조명 · {0}", ReviewSolo = "동료  혼자 수색", ReviewSoloBusy = "동료  혼자 수색 · 빈 대원 없음",
            ReviewProgress = "수색도  {0} → {1} / {2}턴", ReviewComplete = "  ·  완료", ReviewOpens = "  ·  덮개 개방 포함", ReviewMoved = "{0} · {1} 배정에서 빠짐",
            ReviewRunMany = "같은 턴 수색 {0}곳 · ", ReviewRunFull = "모든 대원 배정 · 확인하면 1턴 진행", ReviewRunSame = "이번 확인  1턴 진행 · 전체 배정 실행", ReviewSave = "배정만 저장 · 시간 흐르지 않음",
            ReviewChipPrefix = "턴 진행 시  ", ConfirmRun = "1턴 진행", ConfirmSave = "배정 저장";
        [Header("문에 귀 대기")]
        public string TagListen = "귀 대기";
        public string CardListen = "귀 대기", RoleListen = "{0} 문", PauseDenEmpty = "관리실 빔", ReleaseListenDen = "관리실이 비었습니다.";
        public string ListenTitle = "문에 귀 대기 · {0}", ListenReleaseTitle = "귀 대기 해제 · {0}", ListenNobodyTitle = "문에 귀 대기";
        public string ListenQuiet = "소음 0 · 시간 흐르지 않음", ListenBreaksHush = "소음 0 · 숨죽이기 아님 · 게이지 −1 없음", ListenBreaksPass = "숨죽이기 아님 · 이번 턴 조우",
            ListenFromSupport = "{0} {1}에서 빠짐", ListenLightPause = " · 조명 수색 멈춤", ListenFromDoor = "{0} 문에서 옮김", ListenReleaseSubtitle = "시간 흐르지 않음 · 다음 턴부터 숨죽이기",
            ListenNobody = "빈 대원 없음 · 수색 담당은 뺄 수 없음";
        [TextArea(2, 3)] public string StatusListenSet = "{0} · {1} 문 귀 대기\n'턴 진행'마다 문 너머를 듣습니다.";
        [TextArea(2, 3)] public string StatusListenFromSupport = "{0} · {1} 문 귀 대기\n{2} {3}에서 빠졌습니다.", StatusListenFromDoor = "{0} · {1} 문 귀 대기\n{2} 문에서 옮겼습니다.",
            StatusListenOff = "{0} 문 귀 대기 해제\n시간은 흐르지 않았습니다.", StatusListened = "귀 대기 {0}곳 · 한 턴을 보냈습니다.\n문 위 표시를 확인하세요.", StatusListenReleased = "{0} 문 귀 대기 해제\n{1}";
        public string ChipHunt = "이번 턴 조우 · 쫓아옴 (숨죽여도 소용없음)";
        public string ChipSoon = "그다음 턴 무언가 들어옴", ChipListening = "귀 대기 {0}곳 · 숨죽이기 아님", ChipRunningListen = "수색 {0}곳 · 귀 대기 {1}곳";
        public string NoticeListenMoved = "{0} · {1} 문 귀 대기에서 빠집니다.", ReviewListenMoved = "{0} · {1} 문 귀 대기에서 빠짐", NoticeListenBusy = "{0} 귀 대기 중 · 혼자 수색합니다.";
        public string MoveHeardMeet = "문 너머에 무언가 · 지금 들어가면 마주칩니다", MoveHeardLeave = "문 너머에 무언가 · 다음 턴 {0}로 감", MoveHeardQuiet = "문 너머 조용함 · 지금은 안에 없음";
        [Header("흔적 관찰")]
        public string TagObserve = "관찰", RoleObserve = "{0} 조사", ChipObserve = "관찰 {0}곳 · 숨죽이기 아님";
        public string StatusObserved = "흔적 {0}곳 관찰 완료\n확인한 인물과 단서를 살펴보세요.";
        [Header("문에 모이기 (말 놓기)")]
        [Tooltip("대원 카드 이름표")] public string TagGather = "이동 대기";
        [Tooltip("대원 카드 역할 줄 ({0}: 갈 방)")] public string RoleGather = "{0}로 이동 대기";
        [Tooltip("미리보기 둘째 줄 · 문에 모인 대원만 있을 때 ({0}: 인원)")] public string ChipGathering = "문에 모이는 중 {0}명 · 숨죽이기";
        [Tooltip("멈춘 까닭 · 잠긴 문을 열 도구를 가진 대원이 없음")] public string PauseLock = "문 못 엶";
        [Header("가방 물건 쓰기 (다음 '턴 진행' 때)")]
        [Tooltip("대원 카드 이름표")] public string TagUse = "가방 사용";
        [Tooltip("대원 카드 역할 줄 ({0}: 물건)")] public string RoleUse = "{0} 사용";
        [Tooltip("미리보기 둘째 줄 · 가방 사용만 있을 때 ({0}: 인원)")] public string ChipUsing = "가방 사용 {0}명 · 숨죽이기 아님";
        [Tooltip("멈춘 까닭 · 가방에 물건이 없음")] public string PauseNoItem = "물건 없음";
        [Tooltip("사용 예약 ({0}: 대원, {1}: 물건)")] [TextArea(2, 3)] public string StatusUseQueued = "{0} · {1} 사용 예약\n'턴 진행' 때 사용합니다.";
        [Tooltip("다른 일에서 옮겨 사용 예약 ({0}: 대원, {1}: 물건, {2}: 하던 일)")] [TextArea(2, 3)] public string StatusUseQueuedMoved = "{0} · {1} 사용 예약\n{2}에서 빠졌습니다.";
        [Tooltip("사용 예약 취소 ({0}: 대원, {1}: 물건)")] [TextArea(2, 3)] public string StatusUseCancelled = "{0} · {1} 사용 예약 취소\n시간은 흐르지 않았습니다.";
        [Tooltip("턴 뒤 · 가방 사용만 한 턴 ({0}: 인원)")] [TextArea(2, 3)] public string StatusUsed = "가방 사용 {0}명 · 한 턴을 보냈습니다.\n체력을 확인하세요.";
        [Tooltip("턴 뒤 · 사용하지 못함 ({0}: 대원, {1}: 물건, {2}: 까닭)")] [TextArea(2, 3)] public string StatusUseDropped = "{0} · {1} 사용 못 함\n{2}";
        public string ReleaseNoItem = "가방에 물건이 부족합니다.", ReleaseHealed = "체력이 가득 찼습니다.";
        [Header("첫 방문 조우 (그것이 잠든 방문)")]
        [Tooltip("미리보기 둘째 줄 끝 · 경고(발소리) 뒤 이번 턴 수색의 조우 확률 ({0}: %)")] public string ChipEncounter = " · 조우 {0}%";
    }

    // Member action slots (placing pawns, every visit: Placing; the awake site board: Active): holds the plan, shows it on the room
    // screen and resolves one turn for everyone. The preview is the turn itself run on a copy of the site state, so what the chip
    // shows is what happens. FieldPlacement (FieldPlacement.cs) is the placing rules over this plan; DoorLog keeps what doors heard.
    // '턴 진행' is the only way a turn passes in the room (기획/탐험-말놓기-조작-재설계.md): the first visit runs the same turn, with the
    // old random encounter rolled once per turn that searched (ExpeditionEncounterPanel.AfterSearch); a turn with nobody placed hushes
    // (the '모두 숨죽이기' button is gone); a bag item is the member's action for the turn (QueueUse); everyone at one door moves.
    public sealed partial class FieldTurnPlanner : MonoBehaviour
    {
        [Header("대원별 행동 칸 · 모든 방문 (말 놓기)")]
        [Tooltip("끄면 1차처럼 수색 한 번 = 1턴")] public bool Enabled = true;
        [Tooltip("끄면(말 놓기 기본) 놓은 말만 협동합니다. 켜면 예전처럼 빈 대원이 자동으로 돕습니다")] public bool AutoFillHelpers;
        public Button TurnButton;
        public Text TurnTitle, TurnSubtitle, Hint, Chip;
        [Tooltip("같은 입력이 두 번 들어와 턴이 두 번 흐르지 않게 막는 시간")] [Min(0)] public float TurnCooldown = .2f;
        public Color LeadTint = new Color(1, .76f, .35f), SupportTint = new Color(1, .9f, .7f), BusyTint = new Color(.8f, .8f, .78f);
        [Tooltip("종이 위 경고(확인창)")] public Color WarnColor = new Color32(0xB5, 0x45, 0x2F, 255);
        [Tooltip("어두운 바닥 위 경고(방 화면 미리보기)")] public Color ChipWarnColor = new Color(1f, .62f, .5f);
        public FieldTurnTexts Texts = new FieldTurnTexts();
        [Header("말 놓기 (FieldPlacement · 실루엣 핀 · 역할 칩 · 상황판 · 말 이름표)")]
        public FieldPlacementTexts PlaceTexts = new FieldPlacementTexts();
        [Header("문 기록 (이번 방문 동안 문에서 들은 것)")]
        public FieldDoorLog DoorLog = new FieldDoorLog();

        public FieldTurnPlan Plan { get; } = new FieldTurnPlan();
        // The resident is awake (2nd visit on): danger, door markers, the awake-board encounter rules.
        public bool Active => Enabled && threat && threat.Active;
        // Members are placed and '턴 진행' passes the turn — on every visit, the first (it sleeps) included (기획/탐험-말놓기-조작-재설계.md).
        public bool Placing => Enabled && threat && threat.State != null;
        // '턴 진행' is about to resolve (after every check passed, before anything changes; also before a reserved move leaves):
        // walking pawns snap to their spots. Resolved: the turn was applied (an encounter it ran into may be open; a move is on its way).
        public event Action TurnStarting, TurnResolved;
        // Counts Reset / Clear / OnEncounter (the plan ended).
        public int Clears { get; private set; }
        public bool Resolving => resolving;
        // The check of the last resolved turn and any preview ≠ result difference (empty when they matched).
        public FieldPlanCheck LastCheck { get; private set; }
        public string LastMismatch { get; private set; } = "";
        public int PendingLoot => pendingLoot.Count;

        ExpeditionArrivalPanel arrival; ExpeditionSiteThreat threat; ArrivalFacts facts;
        readonly Queue<int> pendingLoot = new Queue<int>();
        bool resolving, wroteRoles; int lockedFrame = -1; float lockUntil;
        FieldPlanCheck current; (int frame, int version, int turns, int room) currentKey = (-1, -1, -1, -1);

        public void Initialize(ExpeditionArrivalPanel panel, ExpeditionSiteThreat owner)
        {
            arrival = panel; threat = owner; facts = new ArrivalFacts(this); Plan.AutoFill = AutoFillHelpers;
            if (TurnButton) TurnButton.onClick.AddListener(() => Run());
            Refresh();
        }
        // Visit start and end: the door log goes too (the thing is back in its den each visit).
        public void Reset() { Plan.AutoFill = AutoFillHelpers; Plan.Clear(); pendingLoot.Clear(); LastCheck = null; LastMismatch = ""; ClearFresh(); if (DoorLog != null) DoorLog.Clear(); Clears++; }
        // Arrival in another room: assignments belonged to the old room (the door log stays for the visit).
        public void Clear() { Plan.Clear(); pendingLoot.Clear(); ClearFresh(); Clears++; }
        // Before the encounter view copies the member cards: the plan ends and the tags go (both visits).
        public void OnEncounter() { Plan.Clear(); pendingLoot.Clear(); ClearFresh(); HideTags(); Clears++; }
        // One press = one turn: blocks a second call in the same frame, during a turn, or within the cooldown.
        public bool TryLock()
        {
            if (resolving || Time.frameCount == lockedFrame || Time.unscaledTime < lockUntil) return false;
            lockedFrame = Time.frameCount; lockUntil = Time.unscaledTime + TurnCooldown; return true;
        }
        public IFieldPlanFacts Facts() => facts;
        // The plan's check now (cached for the frame, the plan version and the room): many views read it every frame.
        public FieldPlanCheck Current
        {
            get
            {
                if (facts == null || !arrival) return null;
                var key = (Time.frameCount, Plan.Version, arrival.Rooms ? arrival.Rooms.Turns : 0, arrival.Rooms ? arrival.Rooms.CurrentRoom : 0);
                if (current == null || !key.Equals(currentKey)) { current = Plan.Check(facts); currentKey = key; }
                return current;
            }
        }
        // The door every living member stands at (the reserved move, ExpeditionRoomNavigation.Queue.cs), −1 when none.
        public int GatheredDoor { get { if (!Placing) return -1; var k = Current; return k != null ? k.GatheredDoor : -1; } }

        public (FieldPlanCheck check, FieldSiteState outlook, bool hushWouldPass) Forecast(FieldTurnPlan plan)
        {
            var k = plan.Check(facts); var o = threat.State.Copy(); o.MoveParty(arrival.Rooms.CurrentRoom); o.EndTurn(k.Noise, k.HushedAll);
            bool pass = false;
            if (o.Encounter && !k.HushedAll) { var h = threat.State.Copy(); h.MoveParty(arrival.Rooms.CurrentRoom); h.EndTurn(0, true); pass = h.PassedBy; }
            return (k, o, pass);
        }
        // The first visit's old random encounter for this plan's turn: the chance (%) its roll would have (0 when the turn does not search,
        // before the warning, while cooling down). ExpeditionEncounterPanel.NextChance with this turn's noise included.
        public int EncounterChance(FieldPlanCheck k)
        {
            var e = arrival ? arrival.Encounter : null;
            if (k == null || k.Runs.Count == 0 || !e || !arrival.Rooms || threat.State == null || !threat.State.Asleep) return 0;
            return e.NextChance(arrival.Rooms.Noise + k.Noise);
        }

        // Resolve one turn for the whole plan ('턴 진행'). fromSite: legacy (the 07 window's confirm); a turn never passes from a window now.
        public bool Run(int fromSite = -1)
        {
            if (fromSite >= 0) return false;
            if (!Placing || !arrival.IsOpen || arrival.InTransit || arrival.Popup.activeSelf || arrival.Loot && arrival.Loot.IsOpen || arrival.FieldBags && arrival.FieldBags.IsOpen || arrival.Encounter && arrival.Encounter.IsOpen || Story && Story.IsOpen || arrival.MissingPerson && arrival.MissingPerson.IsOpen) return false;
            if (arrival.Search && arrival.Search.IsOpen) return false;
            // Everyone at one door: the move is this turn (ExpeditionRoomNavigation.Queue.cs).
            if (arrival.Rooms)
            {
                bool moving = arrival.Rooms.HasQueuedMove; if (moving) Raise(TurnStarting);
                if (arrival.Rooms.RunQueuedMove()) { if (moving) Raise(TurnResolved); return true; }
            }
            if (FieldIdleConfirm.AskFirst(this)) return false; // members with nothing to do: ask first (FieldIdleConfirm.cs)
            var (k, outlook, _) = Forecast(Plan);
            foreach (var r in k.Runs)
            {
                int before = arrival.Loot.Peek(r.Site, out var s) ? s.Progress : 0; bool done = s != null && s.Complete;
                if (before != r.Before || done) { Debug.LogError("FieldTurnPlan: " + arrival.ObjectNames[r.Site] + " changed since it was planned"); return false; }
            }
            if (!TryLock()) return false;
            Raise(TurnStarting);
            resolving = true; LastCheck = k;
            try
            {
                Plan.Adopt(k); var completed = new List<int>();
                foreach (var r in k.Runs)
                {
                    if (arrival.Loot.ApplyRun(r) && r.Completes) completed.Add(r.Site);
                    arrival.Search.Assignments[r.Site] = new ExpeditionSearchPanel.Assignment { ObjectIndex = r.Site, Worker = arrival.Participants[r.Lead], Pace = r.Pace, Duty = r.Duty };
                }
                // Bag items: used as this turn's action (no noise, no time of their own).
                foreach (var u in k.Uses) arrival.Inventory.ApplyFieldItem(arrival.Participants[u.Member], u.Item);
                // Record paid observations before the site step can interrupt with an encounter. Dialogue waits for a safe screen.
                CompleteObservations(k);
                // Exactly one turn: time, the room's summed noise, and the site board's step (which may open an encounter).
                arrival.Rooms.SpendTurn(k.Noise, k.HushedAll);
                Plan.PruneObservations(k);
                if (k.Uses.Count > 0) arrival.RefreshFieldBags();
                LastMismatch = Compare(threat.State, outlook);
                foreach (var l in k.Listens) if (!threat.State.ListenAt(l.Door).Equals(outlook.ListenAt(l.Door))) LastMismatch += (LastMismatch.Length > 0 ? ", " : "") + "listen " + l.Door;
                if (LastMismatch.Length > 0) Debug.LogError("FieldTurnPlan preview ≠ result: " + LastMismatch);
                // 첫 방문 (it sleeps): a turn that searched is one search turn of the old random encounter; meeting it ends every assignment,
                // as on the site board, and after it the finds of that object open (if it completed) or the board stays.
                if (threat.State.Asleep && k.Runs.Count > 0 && arrival.Encounter && !arrival.Encounter.IsOpen)
                {
                    HideTags(); // before the encounter view copies the member cards
                    if (arrival.Encounter.AfterSearch(FirstCompletedOr(k))) { OnEncounter(); return true; }
                }
                if (arrival.Encounter && arrival.Encounter.IsOpen) return true;
                Heard(k);
                var released = Plan.Prune(k); var deaf = Plan.PruneListens(k); Plan.PruneGathers(k); var used = Plan.PruneUses(k);
                string heardStatus = ObservationStatus(k) ?? ListenStatus(k, deaf);
                if (completed.Count > 0) { arrival.Loot.Open(completed[0]); completed.RemoveAt(0); }
                foreach (int site in completed) pendingLoot.Enqueue(site);
                string status;
                var note = released.FirstOrDefault(x => x.Reason != FieldPause.Complete && x.Reason != FieldPause.OtherRoom);
                var dropped = used.FirstOrDefault(x => x.Reason != FieldPause.Complete && x.Reason != FieldPause.Downed);
                if (note.Reason != FieldPause.None) status = string.Format(Texts.StatusReleased, arrival.ObjectNames[note.Site], ReleaseText(note.Reason));
                else if (dropped.Item != null) status = string.Format(Texts.StatusUseDropped, arrival.Participants[dropped.Member].Name, ItemName(dropped.Item), dropped.Reason == FieldPause.Complete ? Texts.ReleaseHealed : Texts.ReleaseNoItem);
                else if (heardStatus != null) status = heardStatus;
                else if (k.Runs.Count == 0 && k.Uses.Count > 0) status = string.Format(Texts.StatusUsed, k.Uses.Count);
                else if (k.Runs.Count == 0) status = Texts.StatusHushed;
                else status = string.Format(k.Runs.Any(r => r.Completes) ? Texts.StatusDone : Texts.StatusRan, k.Runs.Count, k.Runs.Count(r => r.Completes));
                arrival.Status.text = status;
                return true;
            }
            finally { resolving = false; threat.Refresh(); Raise(TurnResolved); }
        }
        // The object the first-visit encounter hangs off: the first search that completed this turn, else the first that ran.
        static int FirstCompletedOr(FieldPlanCheck k) { var done = k.Runs.FirstOrDefault(r => r.Completes); return done != null ? done.Site : k.Runs[0].Site; }
        // A listener that throws never breaks the turn.
        static void Raise(Action e) { if (e == null) return; try { e(); } catch (Exception x) { Debug.LogException(x); } }
        static string Compare(FieldSiteState a, FieldSiteState b)
        {
            var d = new List<string>();
            void Same<T>(string name, T x, T y) { if (!EqualityComparer<T>.Default.Equals(x, y)) d.Add(name + " " + x + "≠" + y); }
            Same("gauge", a.Gauge, b.Gauge); Same("danger", a.Danger, b.Danger); Same("remembered", a.Remembered, b.Remembered); Same("room", a.ResidentRoom, b.ResidentRoom);
            Same("next", a.Next, b.Next); Same("resident", a.Resident, b.Resident); Same("encounter", a.Encounter, b.Encounter); Same("passed", a.PassedBy, b.PassedBy);
            Same("surprise", a.Surprise, b.Surprise); Same("noticed", a.Noticed, b.Noticed); Same("noted", a.Noted, b.Noted); Same("clock", a.TurnsUsed, b.TurnsUsed);
            return string.Join(", ", d);
        }

        // ---- 가방 물건 쓰기 (추가 결정: the member's action for the next '턴 진행') ----
        // The bag window's use: the member leaves what they did (the pawn rules, FieldTurnPlan.Unassign) and uses the item when the
        // turn resolves; the same item again takes it back. No time passes here. Returns the status line ("" = refused).
        public string QueueUse(int member, string item)
        {
            if (!Placing || facts == null || resolving || !arrival || member < 0 || member >= arrival.Participants.Count || string.IsNullOrEmpty(item)) return "";
            var tx = Texts; string name = arrival.Participants[member].Name, itemName = ItemName(item), line;
            if (UseQueued(member, item)) { Plan.ReleaseUse(member); line = string.Format(tx.StatusUseCancelled, name, itemName); }
            else
            {
                var before = Plan.Check(facts); var was = before.Actions.Length > member ? before.Actions[member] : FieldAction.Hush;
                string task = was != FieldAction.Hush && was != FieldAction.Down && was != FieldAction.Use ? RoleFor(member, before) : "";
                var draft = Plan.Clone(); draft.Unassign(member, facts); draft.AssignUse(member, item);
                if (draft.Check(facts).Actions[member] != FieldAction.Use) return "";
                Plan.Unassign(member, facts); Plan.AssignUse(member, item); Plan.Keep(Plan.Check(facts));
                line = task.Length > 0 ? string.Format(tx.StatusUseQueuedMoved, name, itemName, task) : string.Format(tx.StatusUseQueued, name, itemName);
            }
            if (arrival.Status) arrival.Status.text = line;
            if (threat) threat.Refresh();
            return line;
        }
        public bool UseQueued(int member, string item) { var u = Plan.UseBy(member); return u != null && u.Item == item; }
        string ItemName(string id) => arrival && arrival.Inventory != null ? arrival.Inventory.Items.FirstOrDefault(i => i.Id == id)?.Name ?? id : id;

        // ---- room screen ----
        public void Refresh()
        {
            if (!arrival || resolving || arrival.InTransit) return;
            Plan.AutoFill = AutoFillHelpers;
            bool on = Placing, show = on && !(arrival.Encounter && arrival.Encounter.IsOpen);
            if (TurnButton && TurnButton.gameObject.activeSelf != show) TurnButton.gameObject.SetActive(show);
            if (Chip && Chip.gameObject.activeSelf != on) Chip.gameObject.SetActive(on);
            if (Hint && Hint.enabled == on) Hint.enabled = !on;
            if (!on)
            {
                HideTags();
                if (wroteRoles) { foreach (var c in arrival.Cards) if (c && c.Role) c.Role.text = Texts.RoleIdle; wroteRoles = false; }
                return;
            }
            var (k, o, pass) = Forecast(Plan);
            if (TurnTitle) TurnTitle.text = Texts.TurnTitle;
            if (TurnSubtitle) TurnSubtitle.text = Plan.HasAssignments ? Texts.TurnSubtitle : Texts.TurnHushSubtitle;
            if (Chip) Chip.text = ChipLines(k, o, pass, "\n", ChipWarnColor);
            bool encounter = arrival.Encounter && arrival.Encounter.IsOpen;
            for (int i = 0; i < arrival.Cards.Count && i < k.Actions.Length; i++)
            {
                var c = arrival.Cards[i]; if (!c) continue;
                c.SetAction(encounter ? null : TagFor(k.Actions[i]));
                if (c.Role) c.Role.text = RoleFor(i, k);
            }
            wroteRoles = true;
        }
        void HideTags() { if (arrival) foreach (var c in arrival.Cards) if (c) c.SetAction(null); }
        string TagFor(FieldAction a) => a switch
        {
            FieldAction.Lead => Texts.TagLead, FieldAction.Together => Texts.TagTogether, FieldAction.Watch => Texts.TagWatch, FieldAction.Light => Texts.TagLight,
            FieldAction.Down => Texts.TagDown, FieldAction.Paused => Texts.TagPaused, FieldAction.Listen => Texts.TagListen, FieldAction.Observe => Texts.TagObserve,
            FieldAction.Gather => Texts.TagGather, FieldAction.Use => Texts.TagUse, _ => Texts.TagHush
        };
        string RoleFor(int member, FieldPlanCheck k)
        {
            var a = k.Actions[member]; int site = k.SiteOf[member]; string name = site >= 0 && site < arrival.ObjectNames.Length ? arrival.ObjectNames[site] : "";
            switch (a)
            {
                case FieldAction.Lead: { var r = k.RunFor(site); return string.Format(Texts.RoleLead, name, r.Before, r.Required); }
                case FieldAction.Together: case FieldAction.Watch: case FieldAction.Light: return string.Format(Texts.RoleSupport, name);
                case FieldAction.Down: return Texts.RoleDown;
                case FieldAction.Listen: return string.Format(Texts.RoleListen, FieldSiteState.RoomNames[k.DoorOf[member]]);
                case FieldAction.Observe: return string.Format(Texts.RoleObserve, ObservationLabel(k.ObserveOf[member]));
                case FieldAction.Gather: return string.Format(Texts.RoleGather, FieldSiteState.RoomNames[k.GatherOf[member]]);
                case FieldAction.Use: return string.Format(Texts.RoleUse, ItemName(k.UseOf[member]));
                case FieldAction.Paused:
                    return string.Format(Texts.RolePaused, !string.IsNullOrEmpty(k.ObserveOf[member]) ? PauseShort(-1, k.ObservePauseFor(member)) : k.DoorOf[member] >= 0 ? PauseShort(-1, k.ListenPauseFor(member))
                        : k.GatherOf[member] >= 0 ? PauseShort(-1, k.GatherPauseFor(member)) : PauseShort(site, k.PauseFor(site)));
                default: return Texts.RoleHush;
            }
        }
        public string PauseShort(int site, FieldPause reason) => reason switch
        {
            FieldPause.Downed => Texts.PauseDowned, FieldPause.DenClosed => Texts.PauseDen, FieldPause.NoLight => Texts.PauseLight, FieldPause.Complete => Texts.PauseComplete, FieldPause.OtherRoom => Texts.PauseRoom,
            FieldPause.NoTool => site >= 0 ? string.Format(Texts.PauseTool, ToolName(site)) : Texts.PauseLock, FieldPause.DenEmpty => Texts.PauseDenEmpty, FieldPause.NoItem => Texts.PauseNoItem, _ => ""
        };
        string ReleaseText(FieldPause reason) => reason switch
        {
            FieldPause.Downed => Texts.ReleaseDowned, FieldPause.DenClosed => Texts.ReleaseDen, FieldPause.NoTool => Texts.ReleaseTool, FieldPause.NoLight => Texts.ReleaseLight, FieldPause.DenEmpty => Texts.ReleaseListenDen, _ => ""
        };
        public string ToolName(int site)
        {
            string tool = site >= 0 && site < arrival.Loot.Sites.Length ? arrival.Loot.Sites[site].RequiredTool : null;
            return arrival.Inventory.Items.FirstOrDefault(i => i.Id == tool)?.Name ?? tool;
        }
        static string Colored(string s, Color color) => "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + s + "</color>";

        // Two lines: this turn's noise and the gauge (and danger if it rises); then the one thing that matters most (on the first
        // visit, once the footsteps were heard, this search turn's encounter chance is added).
        public string ChipLines(FieldPlanCheck k, FieldSiteState o, bool pass, string join) => ChipLines(k, o, pass, join, WarnColor);
        public string ChipLines(FieldPlanCheck k, FieldSiteState o, bool pass, string join, Color warnColor)
        {
            string Warn(string s) => Colored(s, warnColor);
            // The danger dots stay hidden on the first visit (it sleeps: the old encounter's risk paper, not the site board's danger).
            string line1 = string.Format(Texts.ChipNoise, k.Noise, threat.Bars(o.Gauge)) + (threat.Active && o.Danger > threat.State.Danger ? Warn(string.Format(Texts.ChipDanger, threat.Dots(o.Danger))) : "");
            var paused = k.Paused.Where(p => p.Reason != FieldPause.Complete && p.Reason != FieldPause.OtherRoom).ToList();
            var observePaused = k.ObservePaused.Where(p => p.Reason != FieldPause.Complete).ToList();
            var done = k.Runs.Where(r => r.Completes).ToList(); var forfeit = k.Runs.FirstOrDefault(r => r.Forfeits);
            string t; bool warn = true;
            if (o.Encounter && pass) t = Texts.ChipMeetPass;
            else if (o.Encounter) t = o.Danger >= 3 ? Texts.ChipHunt : Texts.ChipMeet;
            else if (paused.Count > 0) t = string.Format(Texts.ChipPaused, paused.Count, PauseShort(paused[0].Site, paused[0].Reason));
            else if (observePaused.Count > 0) t = string.Format(Texts.ChipPaused, observePaused.Count, PauseShort(-1, observePaused[0].Reason));
            else if (forfeit != null) t = string.Format(Texts.ChipForfeit, forfeit.Lost);
            else if (o.PassedBy) { t = Texts.ChipPass; warn = false; }
            else if (o.Noted) t = Texts.ChipNoted;
            else if (k.Runs.Any(r => r.Site == threat.DenSite) && !o.DenEmpty) t = Texts.ChipDen;
            else
            {
                warn = false;
                if (o.Incoming && HeardPresent(out _, out _)) t = Texts.ChipSoon;
                else if (k.Observations.Count > 0) t = ObservationPreview(k);
                else if (done.Count == 1) t = string.Format(Texts.ChipDoneOne, arrival.ObjectNames[done[0].Site]);
                else if (done.Count > 1) t = string.Format(Texts.ChipDoneMany, done.Count);
                else if (k.Runs.Count > 0) t = k.Listens.Count > 0 ? string.Format(Texts.ChipRunningListen, k.Runs.Count, k.Listens.Count) : string.Format(Texts.ChipRunning, k.Runs.Count, k.Hushing);
                else if (k.Listens.Count > 0) t = string.Format(Texts.ChipListening, k.Listens.Count);
                else if (k.Uses.Count > 0) t = string.Format(Texts.ChipUsing, k.Uses.Count);
                else if (k.Gathers.Count > 0) t = string.Format(Texts.ChipGathering, k.Gathers.Count);
                else t = Texts.ChipHushAll;
            }
            int chance = EncounterChance(k);
            return line1 + join + (warn ? Warn(t) : t) + (chance > 0 ? Warn(string.Format(Texts.ChipEncounter, chance)) : "");
        }

        void LateUpdate()
        {
            if (!arrival) return;
            UpdateListenOffer();
            bool on = Placing, show = on && !(arrival.Encounter && arrival.Encounter.IsOpen);
            if (TurnButton && TurnButton.gameObject.activeSelf != show) TurnButton.gameObject.SetActive(show);
            // '모두 숨죽이기' is gone (user decision: a turn with nobody placed hushes; BuildPawnRules removes the button). A prefab that
            // still wires it keeps it hidden.
            if (threat.Hush && threat.Hush.gameObject.activeSelf) threat.Hush.gameObject.SetActive(false);
            if (!on) return;
            bool can = threat.CanAct && !resolving;
            if (TurnButton && TurnButton.interactable != can) TurnButton.interactable = can;
            // Several objects finished in one turn: their finds open one after another.
            if (can && pendingLoot.Count > 0)
            {
                int site = pendingLoot.Dequeue();
                if (arrival.Loot.IsSiteInCurrentRoom(site) && arrival.Loot.Peek(site, out var s) && s.Complete) arrival.Loot.Open(site);
            }
        }

        sealed class ArrivalFacts : IFieldPlanFacts, IFieldListenFacts, IFieldObservationFacts, IFieldGatherFacts, IFieldUseFacts
        {
            public FieldPause ObserveBlock(string id) => planner.Story ? planner.Story.ObserveBlock(id) : FieldPause.OtherRoom;
            // A door can be listened at when it opens from the party's room; the storage only once its lock is open (nothing can be
            // inside a locked storage). The den is heard whether it is home or out (it leaves, it returns). The first visit (it sleeps)
            // has nothing to hear: doors only gather (추가 결정).
            public FieldPause ListenBlock(int door)
            {
                int room = A.Rooms.CurrentRoom;
                if (!FieldSiteState.Adjacent(room, door) || door == FieldSiteState.Storage && !A.Rooms.StorageUnlocked) return FieldPause.OtherRoom;
                return A.Threat && A.Threat.State != null && A.Threat.State.Asleep ? FieldPause.OtherRoom : FieldPause.None;
            }
            // A door the whole party can go through from here (never the den); the locked storage needs its tool in someone's bag (option A).
            public FieldPause GatherBlock(int door)
            {
                var r = A.Rooms; if (!r || !FieldPlacement.IsMoveDoor(A, r.CurrentRoom, door) || door == FieldSiteState.Storage && !r.Storage) return FieldPause.OtherRoom;
                return door == FieldSiteState.Storage && !r.CanUnlock ? FieldPause.NoTool : FieldPause.None;
            }
            public FieldPause UseBlock(int m, string item)
            {
                if (!Alive(m)) return FieldPause.Downed;
                var p = A.Participants[m]; if (A.Inventory.FieldItemBlock(p, item) == null) return FieldPause.None;
                return p.Health >= p.MaxHealth ? FieldPause.Complete : FieldPause.NoItem;
            }
            readonly FieldTurnPlanner planner;
            public ArrivalFacts(FieldTurnPlanner owner) { planner = owner; }
            ExpeditionArrivalPanel A => planner.arrival;
            public int Members => A.Participants.Count;
            public int LightBonus => A.Loot.LightBonus;
            public bool Alive(int m) => m >= 0 && m < Members && A.Participants[m].Health > 0;
            public bool HasLight(int m) => Alive(m) && A.Inventory.CountFor(A.Participants[m], "flashlight") > 0;
            public bool HasTool(int site, int m)
            {
                string tool = site >= 0 && site < A.Loot.Sites.Length ? A.Loot.Sites[site].RequiredTool : null;
                return string.IsNullOrEmpty(tool) || m >= 0 && m < Members && A.Inventory.CountFor(A.Participants[m], tool) > 0;
            }
            public FieldSiteFacts Site(int site)
            {
                var f = new FieldSiteFacts { InRoom = A.Loot.IsSiteInCurrentRoom(site), Searchable = !A.Threat || A.Threat.CanSearchSite(site) };
                if (A.Loot.Peek(site, out var s)) { f.Complete = s.Complete; f.Opened = s.Opened; f.Progress = s.Progress; f.Required = s.Required; f.Pace = s.Pace; f.Duty = s.Duty; f.Bonus = s.Bonus; }
                f.Noise = A.Loot.SiteNoise(site); f.Turns = A.Loot.SiteTurns(site); // the object's own noise and base turns (prefab data)
                return f;
            }
        }
    }
}
