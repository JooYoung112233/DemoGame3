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
        public string CostTogether = "함께 수색 · {0} · 발견 +{1}%p", CostWatch = "망보기 · {0} · 턴당 소음 −1", CostLight = "조명 · {0} · 조명 +{1}%p", CostSolo = "혼자 수색 · 지원 없음",
            CostSoloBusy = "혼자 수색 · 빈 대원 없음", CostForfeit = "함께 수색 · 동료 없음 · 보정 +{0}%p 사라짐", CostWatchBusy = "망보기 · 빈 대원 없음 · 소음 감소 없음", CostLightNone = "조명 · 손전등 동료 없음";
        public string NoticeMoved = "{0} · {1} 배정에서 빠집니다.", NoticeReleased = "배정을 해제했습니다 · 진행도는 유지됩니다.", NoticeLeadAgain = "담당 카드를 다시 누르면 배정을 해제합니다.",
            NoticeDutyAgain = "선택한 역할을 다시 누르면 혼자 수색합니다.", NoticeSolo = "역할을 누르면 동료가 함께합니다.", NoticeNoFree = "빈 대원이 없어 혼자 수색합니다.",
            NoticeRelease = "'배정 해제'를 누르면 시간 없이 해제됩니다.", NoticeTool = "{0}를 가진 대원을 담당으로 고르세요.", NoticeLight = "손전등을 가진 동료가 있어야 조명 수색을 이어 갈 수 있습니다.", NoticeDen = "관리실에 무언가가 있습니다 · 굴이 빌 때만 뒤질 수 있습니다.", NoticeLocked = "진행 중인 수색은 속도와 역할이 고정됩니다.";
        public string CardLead = "수색 담당", CardTogether = "동행", CardWatch = "망보기", CardLight = "조명 지원", CardBusy = "다른 사물", CardDown = "쓰러짐";
        [Header("수색 지정 확인창")]
        public string ReviewTitle = "{0}  ·  {1}";
        public string[] PaceNames = { "빠른 수색", "보통 수색", "정밀 수색" };
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
    }

    // Member action slots on the site board (2nd visit on): holds the plan, shows it on the room screen and resolves one turn for everyone.
    // The preview is the turn itself run on a copy of the site state, so what the chip shows is what happens.
    public sealed partial class FieldTurnPlanner : MonoBehaviour
    {
        [Header("대원별 행동 칸 · 두 번째 방문부터")]
        [Tooltip("끄면 1차처럼 수색 한 번 = 1턴")] public bool Enabled = true;
        public Button TurnButton;
        public Text TurnTitle, TurnSubtitle, Hint, Chip;
        [Tooltip("같은 입력이 두 번 들어와 턴이 두 번 흐르지 않게 막는 시간")] [Min(0)] public float TurnCooldown = .2f;
        public Color LeadTint = new Color(1, .76f, .35f), SupportTint = new Color(1, .9f, .7f), BusyTint = new Color(.8f, .8f, .78f);
        [Tooltip("종이 위 경고(확인창)")] public Color WarnColor = new Color32(0xB5, 0x45, 0x2F, 255);
        [Tooltip("어두운 바닥 위 경고(방 화면 미리보기)")] public Color ChipWarnColor = new Color(1f, .62f, .5f);
        public FieldTurnTexts Texts = new FieldTurnTexts();

        public FieldTurnPlan Plan { get; } = new FieldTurnPlan();
        public bool Active => Enabled && threat && threat.Active;
        public bool Resolving => resolving;
        // The check of the last resolved turn and any preview ≠ result difference (empty when they matched).
        public FieldPlanCheck LastCheck { get; private set; }
        public string LastMismatch { get; private set; } = "";
        public int PendingLoot => pendingLoot.Count;

        ExpeditionArrivalPanel arrival; ExpeditionSiteThreat threat; ArrivalFacts facts;
        readonly Queue<int> pendingLoot = new Queue<int>();
        bool resolving, wroteRoles; int lockedFrame = -1; float lockUntil;

        public void Initialize(ExpeditionArrivalPanel panel, ExpeditionSiteThreat owner)
        {
            arrival = panel; threat = owner; facts = new ArrivalFacts(this);
            if (TurnButton) TurnButton.onClick.AddListener(() => Run());
            Refresh();
        }
        public void Reset() { Plan.Clear(); pendingLoot.Clear(); LastCheck = null; LastMismatch = ""; ClearHeard(); }
        public void Clear() { Plan.Clear(); pendingLoot.Clear(); ClearHeard(); }
        // Before the encounter view copies the member cards: the plan ends and the tags go.
        public void OnEncounter() { Plan.Clear(); pendingLoot.Clear(); ClearHeard(); HideTags(); }
        // One press = one turn: blocks a second call in the same frame, during a turn, or within the cooldown.
        public bool TryLock()
        {
            if (resolving || Time.frameCount == lockedFrame || Time.unscaledTime < lockUntil) return false;
            lockedFrame = Time.frameCount; lockUntil = Time.unscaledTime + TurnCooldown; return true;
        }
        public IFieldPlanFacts Facts() => facts;

        public (FieldPlanCheck check, FieldSiteState outlook, bool hushWouldPass) Forecast(FieldTurnPlan plan)
        {
            var k = plan.Check(facts); var o = threat.State.Copy(); o.MoveParty(arrival.Rooms.CurrentRoom); o.EndTurn(k.Noise, k.HushedAll);
            bool pass = false;
            if (o.Encounter && !k.HushedAll) { var h = threat.State.Copy(); h.MoveParty(arrival.Rooms.CurrentRoom); h.EndTurn(0, true); pass = h.PassedBy; }
            return (k, o, pass);
        }

        // Resolve one turn for the whole plan. fromSite: the press came from that object's search panel.
        public bool Run(int fromSite = -1)
        {
            if (!Active || !arrival.IsOpen || arrival.InTransit || arrival.Popup.activeSelf || arrival.Loot && arrival.Loot.IsOpen || arrival.FieldBags && arrival.FieldBags.IsOpen || arrival.Encounter && arrival.Encounter.IsOpen || Story && Story.IsOpen) return false;
            if (fromSite >= 0 ? !(arrival.Search.IsOpen && !arrival.Search.Review.activeSelf) : arrival.Search && arrival.Search.IsOpen) return false;
            if (fromSite < 0 && arrival.Rooms && arrival.Rooms.RunQueuedMove()) return true; // a reserved door: the move is this turn (ExpeditionRoomNavigation.Queue.cs)
            var (k, outlook, _) = Forecast(Plan);
            if (fromSite >= 0 && k.RunFor(fromSite) == null) return false;
            foreach (var r in k.Runs)
            {
                int before = arrival.Loot.Peek(r.Site, out var s) ? s.Progress : 0; bool done = s != null && s.Complete;
                if (before != r.Before || done) { Debug.LogError("FieldTurnPlan: " + arrival.ObjectNames[r.Site] + " changed since it was planned"); return false; }
            }
            if (!TryLock()) return false;
            resolving = true; LastCheck = k;
            try
            {
                Plan.Adopt(k); var completed = new List<int>();
                foreach (var r in k.Runs)
                {
                    if (arrival.Loot.ApplyRun(r) && r.Completes) completed.Add(r.Site);
                    arrival.Search.Assignments[r.Site] = new ExpeditionSearchPanel.Assignment { ObjectIndex = r.Site, Worker = arrival.Participants[r.Lead], Pace = r.Pace, Duty = r.Duty };
                }
                // Record paid observations before the site step can interrupt with an encounter. Dialogue waits for a safe screen.
                CompleteObservations(k);
                // Exactly one turn: time, the room's summed noise, and the site board's step (which may open an encounter).
                arrival.Rooms.SpendTurn(k.Noise, k.HushedAll);
                Plan.PruneObservations(k);
                LastMismatch = Compare(threat.State, outlook);
                foreach (var l in k.Listens) if (!threat.State.ListenAt(l.Door).Equals(outlook.ListenAt(l.Door))) LastMismatch += (LastMismatch.Length > 0 ? ", " : "") + "listen " + l.Door;
                if (LastMismatch.Length > 0) Debug.LogError("FieldTurnPlan preview ≠ result: " + LastMismatch);
                if (arrival.Encounter && arrival.Encounter.IsOpen) return true;
                Heard(k);
                var released = Plan.Prune(k); var deaf = Plan.PruneListens(k); string heardStatus = ObservationStatus(k) ?? ListenStatus(k, deaf);
                bool fromDone = fromSite >= 0 && completed.Contains(fromSite);
                if (fromDone) { arrival.Search.Close(); arrival.Loot.Open(fromSite); }
                else if (fromSite < 0 && completed.Count > 0) { arrival.Loot.Open(completed[0]); completed.RemoveAt(0); }
                foreach (int site in completed) if (site != fromSite) pendingLoot.Enqueue(site);
                string status;
                var note = released.FirstOrDefault(x => x.Reason != FieldPause.Complete && x.Reason != FieldPause.OtherRoom);
                if (fromSite >= 0 && !fromDone) status = string.Format(Texts.StatusSearching, arrival.ObjectNames[fromSite]);
                else if (note.Reason != FieldPause.None) status = string.Format(Texts.StatusReleased, arrival.ObjectNames[note.Site], ReleaseText(note.Reason));
                else if (heardStatus != null) status = heardStatus;
                else if (k.Runs.Count == 0) status = Texts.StatusHushed;
                else status = string.Format(k.Runs.Any(r => r.Completes) ? Texts.StatusDone : Texts.StatusRan, k.Runs.Count, k.Runs.Count(r => r.Completes));
                arrival.Status.text = status;
                if (fromSite >= 0 && !fromDone) arrival.Search.RefreshBoard();
                return true;
            }
            finally { resolving = false; threat.Refresh(); }
        }
        static string Compare(FieldSiteState a, FieldSiteState b)
        {
            var d = new List<string>();
            void Same<T>(string name, T x, T y) { if (!EqualityComparer<T>.Default.Equals(x, y)) d.Add(name + " " + x + "≠" + y); }
            Same("gauge", a.Gauge, b.Gauge); Same("danger", a.Danger, b.Danger); Same("remembered", a.Remembered, b.Remembered); Same("room", a.ResidentRoom, b.ResidentRoom);
            Same("next", a.Next, b.Next); Same("resident", a.Resident, b.Resident); Same("encounter", a.Encounter, b.Encounter); Same("passed", a.PassedBy, b.PassedBy);
            Same("surprise", a.Surprise, b.Surprise); Same("noticed", a.Noticed, b.Noticed); Same("noted", a.Noted, b.Noted); Same("clock", a.TurnsUsed, b.TurnsUsed);
            return string.Join(", ", d);
        }

        // ---- room screen ----
        public void Refresh()
        {
            if (!arrival || resolving || arrival.InTransit) return;
            bool on = Active, show = on && !(arrival.Encounter && arrival.Encounter.IsOpen);
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
            FieldAction.Down => Texts.TagDown, FieldAction.Paused => Texts.TagPaused, FieldAction.Listen => Texts.TagListen, FieldAction.Observe => Texts.TagObserve, _ => Texts.TagHush
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
                case FieldAction.Paused: return string.Format(Texts.RolePaused, !string.IsNullOrEmpty(k.ObserveOf[member]) ? PauseShort(-1, k.ObservePauseFor(member)) : k.DoorOf[member] >= 0 ? PauseShort(-1, k.ListenPauseFor(member)) : PauseShort(site, k.PauseFor(site)));
                default: return Texts.RoleHush;
            }
        }
        public string PauseShort(int site, FieldPause reason) => reason switch
        {
            FieldPause.Downed => Texts.PauseDowned, FieldPause.DenClosed => Texts.PauseDen, FieldPause.NoLight => Texts.PauseLight, FieldPause.Complete => Texts.PauseComplete, FieldPause.OtherRoom => Texts.PauseRoom,
            FieldPause.NoTool => string.Format(Texts.PauseTool, ToolName(site)), FieldPause.DenEmpty => Texts.PauseDenEmpty, _ => ""
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

        // Two lines: this turn's noise and the gauge (and danger if it rises); then the one thing that matters most.
        public string ChipLines(FieldPlanCheck k, FieldSiteState o, bool pass, string join) => ChipLines(k, o, pass, join, WarnColor);
        public string ChipLines(FieldPlanCheck k, FieldSiteState o, bool pass, string join, Color warnColor)
        {
            string Warn(string s) => Colored(s, warnColor);
            string line1 = string.Format(Texts.ChipNoise, k.Noise, threat.Bars(o.Gauge)) + (o.Danger > threat.State.Danger ? Warn(string.Format(Texts.ChipDanger, threat.Dots(o.Danger))) : "");
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
                else t = k.Runs.Count > 0 ? (k.Listens.Count > 0 ? string.Format(Texts.ChipRunningListen, k.Runs.Count, k.Listens.Count) : string.Format(Texts.ChipRunning, k.Runs.Count, k.Hushing))
                    : k.Listens.Count > 0 ? string.Format(Texts.ChipListening, k.Listens.Count) : Texts.ChipHushAll;
            }
            return line1 + join + (warn ? Warn(t) : t);
        }

        void LateUpdate()
        {
            if (!arrival) return;
            UpdateListenOffer();
            bool on = Active, show = on && !(arrival.Encounter && arrival.Encounter.IsOpen);
            if (TurnButton && TurnButton.gameObject.activeSelf != show) TurnButton.gameObject.SetActive(show);
            if (on && threat.Hush && threat.Hush.gameObject.activeSelf != show) threat.Hush.gameObject.SetActive(show);
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

        sealed class ArrivalFacts : IFieldPlanFacts, IFieldListenFacts, IFieldObservationFacts
        {
            public FieldPause ObserveBlock(string id) => planner.Story ? planner.Story.ObserveBlock(id) : FieldPause.OtherRoom;
            // A door can be listened at when it opens from the party's room; the storage only once its lock is open
            // (nothing can be inside a locked storage); the den only while it is home.
            public FieldPause ListenBlock(int door)
            {
                int room = A.Rooms.CurrentRoom;
                if (!FieldSiteState.Adjacent(room, door) || door == FieldSiteState.Storage && !A.Rooms.StorageUnlocked) return FieldPause.OtherRoom;
                return door == FieldSiteState.Den && A.Threat && A.Threat.State != null && A.Threat.State.DenEmpty ? FieldPause.DenEmpty : FieldPause.None;
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
                return f;
            }
        }
    }
}
