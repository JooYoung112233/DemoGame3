using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // The site board on top of the room screens: time left, noise → 위험도, and the one thing that lives here (its den is 관리실).
    // It moves between rooms by the step it announced a turn earlier; meeting it is a matter of where you stand, not a dice roll.
    // First visit: it sleeps (the old noise-roll encounter stays), so the introduction is never blocked by it.
    public sealed partial class ExpeditionSiteThreat : MonoBehaviour
    {
        [Header("현장 판 · 임시 수치")]
        public FieldSiteRules Rules = new FieldSiteRules();
        public bool Enabled = true;
        [Header("그것 · 전투 자산의 종을 쓴다")]
        public BattleCreatureRoster Creatures;
        public string ResidentId = "02-listener";
        [Tooltip("같은 방에 있을 때 말이 서는 자리 (오락실, 복도, 보관실, 관리실)")]
        public Vector3[] ResidentSpots = { new Vector3(5.4f, -1.2f, 0), new Vector3(5.2f, -1.3f, 0), new Vector3(-4.6f, -1.2f, 0), Vector3.zero };
        [Tooltip("원정대 말이 첫 자리 가까이 있으면 대신 서는 자리 (방 순서 같음)")]
        public Vector3[] ResidentAltSpots = { new Vector3(-.2f, -1.45f, 0), new Vector3(2.4f, -1.6f, 0), new Vector3(1.2f, -1.65f, 0), Vector3.zero };
        [Tooltip("관리실 선반 수색 대상 번호 (ExpeditionLootPanel.Sites)")]
        public int DenSite = 8;
        [Header("표시 · 프리팹에서 편집")]
        public FieldThreatMarker[] DoorMarkers;
        public FieldThreatMarker ResidentTag;
        public Button Hush;
        [Tooltip("대원별 행동 칸과 '턴 진행' (두 번째 방문부터)")] public FieldTurnPlanner Planner;
        public AudioSource Audio;
        public AudioClip Footsteps, Breathing, Passing;
        [Header("문구")]
        public string TimeFormat = "남은 시간 {0}턴", OvertimeFormat = "시간 초과 +{0}";
        public string Incoming = "다음 턴 들어옴", Footfall = "발소리", DenBreathing = "숨소리", DenQuiet = "조용함 · 선반", DenSleeping = "잠든 숨소리", DenComing = "곧 돌아옴";
        [TextArea(2, 3)] public string SurpriseBody = "방에 들어서자 무언가가 멈춰 서 있습니다.\n미리 듣지 못했습니다.";
        [TextArea(2, 3)] public string LingerLine = "다음 턴 뒤 오래 머묾:\n위험도가 1 오릅니다.";
        [TextArea(2, 3)] public string NoticedBody = "곁에 있던 무언가가 소리를 알아챘습니다.\n수색을 멈추고 몸을 낮춥니다.";
        [Header("문에 귀 대기 · 들은 결과")]
        public string HeardSoon = "곧 들어옴";
        public string HeardStop = "안에서 멈춤", HeardStay = "안에서 머묾", HeardLeave = "{0}로 감", HeardQuiet = "안에 없음", HeardLeft = "방금 나감", ListenPending = "귀 대는 중";
        public string StatusHeard = "{0}에 무언가 · 다음 턴 {1}", NextStop = "멈춤", NextStay = "머묾", NextTo = "{0}로",
            StatusHeardThen = "조용하면 그다음 {0}", ThenStay = "그대로", ThenTo = "{0}로", StatusHeardHere = "그다음 턴 이 방으로 옵니다.";
        [TextArea(2, 3)] public string HeardSurpriseBody = "방에 들어서자 들은 대로 무언가가 서 있습니다.\n몸을 낮춥니다.";
        [TextArea(2, 3)] public string MetBody = "예고대로 무언가가 들어왔습니다.\n수색을 멈추고 몸을 낮춥니다.";

        public FieldSiteState State { get; private set; }
        public bool Active => Enabled && State != null && !State.Asleep;
        ExpeditionArrivalPanel arrival; GameObject residentPawn; Text risk; bool hushed, walkedIntoHeard, dangerRose, introPending, introShown, residentLessonShown; string wroteStatus, roomStatus;
        // The one-time battle lesson for this site's resident (saved as SavedSiteBoard.ResidentLessonShown, like introShown).
        public bool ResidentLessonShown => residentLessonShown;
        public bool IntroAcknowledged => arrival&&arrival.TutorialSkipped || introShown && !introPending;
        public void MarkResidentLesson() => residentLessonShown = true;
        [Header("장소 판 안내 · 두 번째 방문 처음 한 번")]
        public string IntroTitle = "두 번째 방문 · 장소 판";
        [TextArea(4, 6)] public string IntroBody = "남은 턴 안에 수색하고 빠져나가세요.\n소음이 쌓이면 위험도가 오르고, 큰 소리를 낸 방을 무언가가 찾아옵니다.\n문 위 표시는 한 턴 전 예고입니다 · 모두 숨죽이면 지나갑니다.\n대원마다 행동을 배정하고 '턴 진행'을 누르세요.";
        public string DenEmptyShelf = "조용함 · 빈 선반";
        [Tooltip("관리실이 비었지만 이번 턴에 그것이 돌아올 때: 관리실 문 확인창과 상황판")]
        [TextArea(2, 3)] public string DenReturning = "무언가가 관리실로 돌아오는 중입니다.\n이번 턴에는 선반을 뒤질 수 없습니다.";
        [TextArea(2, 3)] public string DangerRoseLine = "위험도가 올랐습니다 · {0}\n{1}", HuntLine = "무언가가 원정대를 쫓기 시작합니다.\n숨죽여도 지나가지 않습니다.";
        public string[] DangerMeaning = { "굴에서 잠들어 있습니다.", "깨어 있습니다 · 큰 소리를 낸 방을 기억합니다.", "기억한 방이 있으면 찾아 나섭니다.", "원정대를 쫓습니다." };
        [Header("가방 사용 턴")]
        public string ItemTurnQuiet = "소음 없음 · 숨죽이기 아님 · 무언가는 예고대로 움직입니다";
        public string ItemTurnPaused = "소음 없음 · 숨죽이기 아님 · 배정한 행동은 이번 턴 멈춤";
        public string ItemTurnMeets = "<color=#A6331F>이번 턴 무언가가 들어옵니다 · 사용하면 마주칩니다</color>";
        // What the site remembers between visits (saved from v13: CampaignSaveData SiteBoards): danger, noise and the room it heard.
        (int danger, int gauge, int remembered, int clock) carried = (0, 0, FieldSiteState.Nowhere, 0);

        public void Initialize(ExpeditionArrivalPanel panel)
        {
            arrival = panel; risk = arrival.Main.transform.Find("Risk")?.GetComponent<Text>();
            if (Hush) Hush.onClick.AddListener(HushTurn);
            if (Planner) Planner.Initialize(arrival, this);
            HideAll();
        }
        public void Begin(bool firstVisit)
        {
            if (!Enabled) { State = null; HideAll(); return; }
            // Each visit starts its own clock; danger, noise and the remembered room carry over (the first visit only carries what it can).
            State = new FieldSiteState(Rules, () => Random.Range(0, 100), firstVisit, carried.danger, carried.gauge, firstVisit ? FieldSiteState.Nowhere : carried.remembered, 0);
            if (!firstVisit && !introShown && !arrival.TutorialSkipped) introPending = true;
            State.MoveParty(arrival.Rooms ? arrival.Rooms.CurrentRoom : 0); hushed = false; ClearPawn(); if (Planner) Planner.Reset(); Refresh();
        }
        // Review fixture: start this visit as a later one (awake, carrying the given danger, noise and site clock).
        public void ReviewWake(int danger, int gauge, int clock)
        {
            State = new FieldSiteState(Rules, () => Random.Range(0, 100), false, danger, gauge, FieldSiteState.Nowhere, clock);
            State.MoveParty(arrival.Rooms.CurrentRoom); ClearPawn(); if (Planner) Planner.Reset(); Refresh();
        }
        // Review fixture: show the resident lesson again (tests run more than one lesson case per session).
        public void ReviewResetResidentLesson() => residentLessonShown = false;
        // Whether the party has been here before (saved from v13; ArrivalPanel also treats corridor or inspection records as a visit).
        public bool Visited { get; private set; }
        public void End()
        {
            Visited = true;
            // What the site remembers for the next visit: danger (at most 1 from the sleeping first visit), noise, and the loud room it noted.
            if (State != null) carried = (State.Asleep ? Mathf.Min(State.Danger, 1) : State.Danger, State.Gauge, State.Asleep ? FieldSiteState.Nowhere : State.Remembered, 0);
            State = null; ClearPawn(); if (Planner) Planner.Reset(); HideAll();
        }

        // ---- turns (called by room navigation whenever a turn is spent) ----
        public void EndTurn(int noise) => EndTurn(noise, false);
        // hushedAll: nobody in the party searched this turn (the member plan), same effect as the hush button.
        public void EndTurn(int noise, bool hushedAll)
        {
            if (State == null) return;
            State.MoveParty(arrival.Rooms.CurrentRoom); int dangerBefore = State.Danger;
            State.EndTurn(noise, hushed || hushedAll); hushed = false; dangerRose = State.Danger > dangerBefore;
            if (Active)
            {
                if (State.Encounter && arrival.Encounter && !arrival.Encounter.IsOpen) { if (Planner) Planner.OnEncounter(); OpenEncounter(); }
                else if (State.PassedBy) Play(Passing ? Passing : Footsteps, .7f);
                else if (State.Incoming || State.Heard) Play(Footsteps, State.Incoming ? .9f : .6f);
                else if (State.Visible) Play(Breathing, .7f);
            }
            Refresh();
        }
        // A party move ends on arrival; extra turns (a lock forced open on the way) follow in the new room.
        public void PartyArrived(int room, int turns, int noise)
        {
            if (State == null) return;
            // Walking into a room just heard to hold it: the encounter text says so (the rule is the same).
            walkedIntoHeard = Active && Planner && Planner.TryReport(room, out var heard, out _) && heard.Present;
            if (Planner) Planner.Clear();
            State.MoveParty(room);
            for (int i = 0; i < Mathf.Max(1, turns); i++) { EndTurn(i == 0 ? noise : 0); if (arrival.Encounter && arrival.Encounter.IsOpen) break; }
            walkedIntoHeard = false;
        }
        public bool CanAct => arrival && arrival.IsOpen && !(arrival.MissingPerson && arrival.MissingPerson.IsOpen) && !(arrival.Story && arrival.Story.IsOpen) && !arrival.InTransit && !arrival.Popup.activeSelf && !(arrival.Search && arrival.Search.IsOpen) && !(arrival.Loot && arrival.Loot.IsOpen)
            && !(arrival.FieldBags && arrival.FieldBags.IsOpen) && !(arrival.Encounter && arrival.Encounter.IsOpen);
        // 숨죽이기: a turn spent still and silent. Everyone hushed lets it pass by (not while it is hunting).
        public void HushTurn()
        {
            if (!CanAct || State == null) return;
            if (Planner && !Planner.TryLock()) return;
            hushed = true; arrival.Rooms.SpendSearchTurn(0);
        }

        // ---- encounter outcomes ----
        void OpenEncounter()
        {
            var creature = Creatures ? Creatures.Find(ResidentId) : null;
            // On site it is always 무언가; the battle names it.
            arrival.Encounter.OpenThreat(State.Surprise ? (walkedIntoHeard ? HeardSurpriseBody : SurpriseBody) : State.Noticed ? NoticedBody : MetBody, "무언가 1", creature != null ? new[] { creature } : null);
        }
        public void OnHidden() { if (State == null) return; State.Hidden(); Refresh(); }
        public void OnDriven() { if (State == null) return; State.Driven(); Refresh(); }
        // Retreating from a fight on the board: the battle noise (gunfire included) reaches the gauge like a won fight's does.
        public void HearBattle(int noise) { if (!Active) return; int before = State.Danger; State.Hear(noise); dangerRose = State.Danger > before; Refresh(); }

        // ---- the den ----
        public bool CanSearchSite(int index) => index != DenSite || Active && State.DenStaysEmpty;
        // The office door: search the shelf while the den is empty, otherwise listen at it.
        public bool OpenDen()
        {
            if (!Enabled || State == null || !CanAct) return false;
            if (CanSearchSite(DenSite)) { arrival.Press(DenSite); return true; } // the shelf's search note (FieldSearchNote); 07 via its '자세히 >'
            if (Active && State.DenEmpty) { arrival.OpenPopup("관리실 문", DenReturning); return true; }
            arrival.OpenPopup("관리실 문", State.Asleep ? "안에서 느리고 규칙적인 숨소리가 들립니다.\n깨우지 않는 편이 낫겠습니다."
                : "문틈 너머에서 무언가 숨을 고르고 있습니다.\n안이 비었을 때만 선반을 뒤질 수 있습니다.\n\n큰 소리를 내면 그 소리를 찾아 나옵니다.");
            if (Active && Planner) Planner.OfferDen();
            return true;
        }
        // One line for the search confirmation: what this turn's noise will do.
        public string PreviewLine(int noise)
        {
            if (State == null) return "";
            var p = State.Preview(noise, false);
            // Remembered only if this noise actually makes it note the room (awake, 위험도 1+ after the noise lands).
            var c = State.Copy(); c.MoveParty(arrival.Rooms.CurrentRoom); c.EndTurn(noise, false);
            return "소음 +" + noise + " → " + Bars(p.gauge) + (p.danger > State.Danger ? " · 위험도 " + Dots(p.danger) : "")
                + (c.Noted ? " · 큰 소리: 이 방을 기억합니다" : "") + (State.Incoming ? " · 다음 턴 무언가 들어옴" : "");
        }
        // One line for a bag item used on the board: the turn it spends is silent but not a hush. Forecast on a copy only (the real state is never touched).
        public string ItemTurnLine()
        {
            if (State == null || !Active) return null;
            var c = State.Copy(); c.MoveParty(arrival.Rooms ? arrival.Rooms.CurrentRoom : State.PartyRoom); c.EndTurn(0, false);
            if (c.Encounter) return ItemTurnMeets;
            return (Planner && Planner.Plan.HasAssignments ? ItemTurnPaused : ItemTurnQuiet) + (c.Danger > State.Danger ? " · 위험도 " + Dots(c.Danger) : "");
        }

        // ---- screen ----
        public string Dots(int d) => new string('●', d) + new string('○', 3 - d);
        public string Bars(int g) => new string('■', g) + new string('□', Mathf.Max(0, Rules.GaugeSize - g));
        void HideAll()
        {
            if (DoorMarkers != null) foreach (var m in DoorMarkers) if (m) m.Hide();
            if (ResidentTag) ResidentTag.Hide();
            if (Hush) Hush.gameObject.SetActive(false);
            if (Planner) Planner.Refresh();
        }
        public void Refresh()
        {
            if (State == null || !arrival) { HideAll(); return; }
            var s = State; var rooms = arrival.Rooms;
            // First visit (it sleeps): the old encounter keeps the turn count and the risk paper; only the den door hints at what lives here.
            if (Active)
            {
                if (rooms && rooms.TurnLabel) rooms.TurnLabel.text = s.TurnsUsed > Rules.TurnBudget ? string.Format(OvertimeFormat, s.TurnsUsed - Rules.TurnBudget) : string.Format(TimeFormat, s.TurnsLeft);
                if (risk) { risk.supportRichText = true; risk.text = "위험도 " + Dots(s.Danger) + "\n<size=20>소음 " + Bars(s.Gauge) + "</size>"; }
            }
            if (Hush) { bool show = Active && !(arrival.Encounter && arrival.Encounter.IsOpen); if (Hush.gameObject.activeSelf != show) Hush.gameObject.SetActive(show); Hush.interactable = CanAct; }
            // Door tags: a step coming in is always shown a turn early; footsteps next door when it moved; the den from the corridor.
            if (DoorMarkers != null)
                foreach (var m in DoorMarkers)
                {
                    if (!m) continue;
                    if (m.Room != s.PartyRoom) { m.Hide(); continue; }
                    FieldDoorReport r = default; bool heard = Active && Planner && Planner.TryReport(m.From, out r, out _);
                    if (Active && s.Incoming && s.ResidentRoom == m.From) m.Show(Incoming, true);
                    else if (heard && r.ThenIn) m.Show(HeardSoon, FieldThreatMarker.Tone.Soon);
                    else if (heard && r.Present) m.Show(r.Next == r.Door ? (r.Resident == ResidentState.Out ? HeardStop : HeardStay) : string.Format(HeardLeave, FieldSiteState.RoomNames[r.Next]), false);
                    else if (m.From == FieldSiteState.Den) m.Show(s.Asleep ? DenSleeping : !Active ? DenBreathing : s.DenEmpty ? (!s.DenStaysEmpty ? DenComing : ShelfEmpty ? DenEmptyShelf : DenQuiet) : DenBreathing, false);
                    else if (heard) m.Show(r.Left ? HeardLeft : HeardQuiet, false);
                    else if (Active && s.Heard && (s.ResidentRoom == m.From || s.MovedFrom == m.From)) m.Show(Footfall, false);
                    else if (Active && Planner && Planner.Plan.ListenAt(m.From) != null) m.Show(ListenPending, false);
                    else m.Hide();
                }
            // Same room: its standee stands by the door with the step it will take next.
            if (!HideResidentDuringTransit() && Active && s.Visible)
            {
                ShowPawn(s.ResidentRoom);
                if (ResidentTag) ResidentTag.Show(s.Next != s.ResidentRoom && s.Next >= 0 ? "다음: " + FieldSiteState.RoomNames[s.Next] + "로" : s.Resident == ResidentState.Staying ? "머무는 중" : "멈춰 듣는 중", true);
            }
            else if (!arrival.InTransit) { ClearPawn(); if (ResidentTag) ResidentTag.Hide(); }
            // The room screen owns the status paper; a threat line replaces it only while it applies, then the room's line comes back.
            if (arrival.Status.text != wroteStatus) roomStatus = arrival.Status.text;
            var status = StatusLine();
            if (!string.IsNullOrEmpty(status)) arrival.Status.text = wroteStatus = status;
            else if (wroteStatus != null) { arrival.Status.text = roomStatus ?? ""; wroteStatus = null; }
            if (Planner) Planner.Refresh();
        }
        string StatusLine()
        {
            // Two short lines at most (the status paper holds two).
            var s = State; bool lingerRaises = s.NextLinger == 1 && s.Preview(0, false).danger > s.Danger;
            if (!Active) return null;
            if (s.PassedBy) return "무언가가 숨죽인 원정대를\n지나쳤습니다.";
            if (s.Visible) return "무언가가 이 방에 있습니다.\n" + (s.Next != s.ResidentRoom && s.Next >= 0 ? "다음 턴 " + FieldSiteState.RoomNames[s.Next] + "로 갑니다." : s.Resident == ResidentState.Staying ? "여기 머물며 냄새를 맡습니다." : "멈춰 소리를 듣고 있습니다.");
            if (s.Incoming) return FieldSiteState.RoomNames[s.ResidentRoom] + " 쪽 문으로 발소리가 옵니다.\n다음 턴 이 방에 들어옵니다.";
            if (dangerRose && s.Danger >= 3) return HuntLine;
            if (Planner && Planner.HeardPresent(out var r, out _))
            {
                string next = r.Next == r.Door ? (r.Resident == ResidentState.Out ? NextStop : NextStay) : string.Format(NextTo, FieldSiteState.RoomNames[r.Next]);
                string then = r.ThenIn ? StatusHeardHere : string.Format(StatusHeardThen, r.Then == r.Door ? ThenStay : string.Format(ThenTo, FieldSiteState.RoomNames[r.Then]));
                return string.Format(StatusHeard, FieldSiteState.RoomNames[r.Door], next) + "\n" + then;
            }
            // Driven() removes its room while this turn's movement sound can still be true.
            if (s.Heard && s.ResidentRoom >= 0 && s.ResidentRoom < FieldSiteState.RoomNames.Length)
                return FieldSiteState.RoomNames[s.ResidentRoom] + " 쪽에서 발소리가 들립니다.";
            if (s.PartyRoom == FieldSiteState.Corridor && s.DenEmpty && !s.DenStaysEmpty) return DenReturning;
            if (s.PartyRoom == FieldSiteState.Corridor && s.DenEmpty && s.Resident != ResidentState.Gone) return "관리실이 비었습니다.\n지금 선반을 뒤질 수 있습니다.";
            if (s.PartyRoom == FieldSiteState.Corridor && s.Resident == ResidentState.Gone) return "무언가가 달아났습니다.\n관리실 선반을 뒤질 수 있습니다.";
            if (s.LastNoise >= Rules.LoudNoise && s.Remembered == s.PartyRoom) return "큰 소리를 냈습니다.\n무언가가 이 방을 기억합니다.";
            if (dangerRose) return string.Format(DangerRoseLine, Dots(s.Danger), DangerMeaning[Mathf.Clamp(s.Danger, 0, DangerMeaning.Length - 1)]);
            if (lingerRaises) return LingerLine;
            if (s.TurnsUsed > Rules.TurnBudget) return "장소 시계를 다 썼습니다.\n위험도가 3으로 고정됩니다.";
            return null;
        }
        void ShowPawn(int room)
        {
            var creature = Creatures ? Creatures.Find(ResidentId) : null;
            if (creature == null || !creature.Body || !arrival.PawnPrefab || !arrival.PawnRoot) return;
            var spot = room >= 0 && room < ResidentSpots.Length ? ResidentSpots[room] : Vector3.zero;
            if (!residentPawn)
            {
                residentPawn = Instantiate(arrival.PawnPrefab, arrival.PawnRoot); residentPawn.name = "Resident";
                var body = residentPawn.transform.Find("Body").GetComponent<SpriteRenderer>(); var ally = arrival.PawnRoot.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(r => r.name == "Body" && r.transform.root != residentPawn.transform && r.transform.parent != residentPawn.transform);
                // Same height rule as the battle: the creature's Height relative to a 1.72 ally, from the drawn pixels.
                float allyHeight = ally ? ally.bounds.size.y : 1.72f;
                body.sprite = creature.Body; body.flipX = false;
                var px = creature.Visible.height > 0 ? creature.Visible : new Rect(0, 0, creature.Body.rect.width, creature.Body.rect.height);
                float scale = allyHeight * (creature.Height / 1.72f) * creature.Body.pixelsPerUnit / px.height / Mathf.Max(.0001f, residentPawn.transform.lossyScale.y);
                body.transform.localScale = Vector3.one * scale;
                body.transform.localPosition = new Vector3((creature.Body.pivot.x - px.center.x) * scale / creature.Body.pixelsPerUnit, (creature.Body.pivot.y - px.y) * scale / creature.Body.pixelsPerUnit, 0);
                foreach (var r in residentPawn.GetComponentsInChildren<SpriteRenderer>(true)) if (r.name == "Base") r.color = new Color(.86f, .42f, .33f);
            }
            // Stand clear of the party's standees (they stand by the door after coming back into a room).
            if (room >= 0 && ResidentAltSpots != null && room < ResidentAltSpots.Length)
            {
                float Clear(Vector3 p) { float d = float.MaxValue; foreach (Transform t in arrival.PawnRoot) if (t != residentPawn.transform && t.gameObject.activeSelf) d = Mathf.Min(d, Mathf.Abs(t.localPosition.x - p.x)); return d; }
                if (Clear(spot) < 1.6f && Clear(ResidentAltSpots[room]) > Clear(spot)) spot = ResidentAltSpots[room];
            }
            residentPawn.transform.localPosition = spot;
            // Transit hides only presentation. Restore the same standee after the arrived state has chosen its room.
            if (!residentPawn.activeSelf) residentPawn.SetActive(true);
        }
        bool HideResidentDuringTransit()
        {
            if (!arrival || !arrival.InTransit) return false;
            if (residentPawn && residentPawn.activeSelf) residentPawn.SetActive(false);
            if (ResidentTag) ResidentTag.Hide();
            return true;
        }
        void ClearPawn() { if (residentPawn) { residentPawn.SetActive(false); Destroy(residentPawn); residentPawn = null; } }
        // The den shelf searched and emptied: nothing left to take while it is out.
        bool ShelfEmpty => arrival && arrival.Loot && arrival.Loot.Peek(DenSite, out var shelf) && shelf.Complete && shelf.Loot.Values.Sum() == 0;
        // The move popup of a door with a step coming through it: going in means meeting it in the doorway.
        public string IncomingLine(int room) => Active && State.Incoming && State.ResidentRoom == room ? "\n문 너머에서 무언가 다가옵니다 · 지금 들어가면 마주칩니다" : "";
        void LateUpdate()
        {
            if (arrival.TutorialSkipped) introPending = false;
            if (introPending && Active && CanAct) { introPending = false; introShown = true; arrival.OpenPopup(IntroTitle, IntroBody); }
            // Panels open and close without a turn passing: keep the hush button in step with them.
            if (Hush && Hush.gameObject.activeSelf && Hush.interactable != CanAct) Hush.interactable = CanAct;
            // Backgrounds switch before the arrival turn resolves; never carry the old room's resident through that fade.
            if (HideResidentDuringTransit()) return;
            if (!ResidentTag || !residentPawn || !ResidentTag.gameObject.activeSelf) return;
            var canvas = ResidentTag.GetComponentInParent<Canvas>(); var cam = canvas ? canvas.worldCamera : null; var parent = (RectTransform)ResidentTag.transform.parent;
            var body = residentPawn.transform.Find("Body")?.GetComponent<SpriteRenderer>(); if (!body || !cam) return;
            var top = new Vector3(body.bounds.center.x, body.bounds.max.y + .25f, 0);
            // The pawn lives in the world, the tag on the canvas: go through the screen point.
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, RectTransformUtility.WorldToScreenPoint(Camera.main ? Camera.main : cam, top), cam, out var world))
                ResidentTag.transform.position = world;
        }
        void Play(AudioClip clip, float volume) { if (clip && Audio) Audio.PlayOneShot(clip, volume); }
    }
}
