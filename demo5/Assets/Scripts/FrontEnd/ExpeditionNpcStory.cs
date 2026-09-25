using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // One persistent encounter. Reading is free; observation belongs to the existing party turn.
    // 말 놓기 (기획/탐험-말놓기-조작-재설계.md, 2026-09-25): the trace is observed by placing a member's pawn beside it (FieldPlacement's
    // observe place) and '턴 진행'. Before anyone looked at it (stage 0) a left press on it assigns nobody: it asks for a pawn first, the
    // same as any object (the pawn board's press hook); a right press shows what it is (FieldPawnBoard.RightPress). From stage 1 on a
    // press opens the conversation as before (free). The stage-0 assignment window below is unreachable then (phase 2 deletes it).
    [DefaultExecutionOrder(150)]
    public sealed partial class ExpeditionNpcStory : MonoBehaviour
    {
        public const string ObservationId = "mall.hidden_doyun";
        public SettlementController Owner;
        public ExpeditionArrivalPanel Arrival;
        public GameObject View, NpcPrefab;
        public Button Clue, Back;
        public Button[] Members = Array.Empty<Button>(), Choices = Array.Empty<Button>();
        public Text[] MemberLabels = Array.Empty<Text>(), ChoiceTitles = Array.Empty<Text>(), ChoiceDetails = Array.Empty<Text>();
        public Text Title, Speaker, Body, Status, ClueLabel, ClueMark;
        public Text UnknownPortrait, MemberHeading;
        public Image Portrait;
        public ExplorationHotspot Hotspot;
        [Tooltip("Original arcade floor; stays beside the machine, outside the party formation.")]
        public Vector3 NpcPosition = new Vector3(-7.1f, -1.15f, 0);
        public SavedNpcStory State = new SavedNpcStory();
        [Tooltip("말 놓기 판이 없을 때 흔적(조사 전)을 누르면 상황판에 쓰는 안내")] [TextArea(2, 3)] public string ClueHint = "대원 말을 먼저 누르세요\n말을 누르고 흔적 옆 실루엣을 누르세요.";
        public bool IsOpen => View && View.activeSelf;
        public bool HasRecord => State.Stage > 0;
        public string DisplayName => State.Stage >= 2 ? "장도윤" : "?";
        public string RecordTitle => State.Stage >= 2 ? "장도윤 · 오락기 뒤에서" : "오락기 뒤의 사람";
        public GameObject VisiblePawn => pawn;
        FieldTurnPlanner Planner => Arrival && Arrival.Threat ? Arrival.Threat.Planner : null;
        GameObject pawn, previousFocus;
        int selected = -1;
        bool initialized;
        bool BoardReady => Arrival && Arrival.IsOpen && Arrival.Threat && Arrival.Threat.Active &&
            Arrival.Threat.IntroAcknowledged && Planner && Planner.Active && State.VisitCount >= 2;
        bool InArcade => Arrival && Arrival.Rooms && Arrival.Rooms.CurrentRoom == 0;
        bool FreeWorld => BoardReady && InArcade && !Arrival.InTransit && !Arrival.Popup.activeSelf &&
            !(Arrival.Search && Arrival.Search.IsOpen) && !(Arrival.Loot && Arrival.Loot.IsOpen) &&
            !(Arrival.FieldBags && Arrival.FieldBags.IsOpen) && !(Arrival.Encounter &&
            (Arrival.Encounter.IsOpen || Arrival.Encounter.Battle && Arrival.Encounter.Battle.IsOpen));

        public void Initialize(SettlementController owner)
        {
            Owner = owner;
            if (!Arrival) Arrival = GetComponent<ExpeditionArrivalPanel>();
            if (initialized) return;
            initialized = true;
            if (View) View.SetActive(false);
            if (Clue) { Clue.onClick.AddListener(Open); Clue.gameObject.SetActive(false); }
            if (Back) Back.onClick.AddListener(Close);
            InitializeDialogue();
            for (int i = 0; i < Members.Length; i++) { int index = i; Members[i].onClick.AddListener(() => SelectMember(index)); }
            for (int i = 0; i < Choices.Length; i++) { int index = i; Choices[i].onClick.AddListener(() => Choose(index)); }
        }

        public void BeginVisit(bool firstVisit)
        {
            State.VisitCount = Math.Max(firstVisit ? 1 : 2, State.VisitCount + 1);
            selected = -1;
            ResetDialogue();
            if (View) View.SetActive(false);
            if (pawn) pawn.SetActive(false);
        }
        public void OnReturn()
        {
            if (IsOpen) Close();
            if (State.Stage == 3 && !State.Returned)
            {
                State.Returned = true;
                Log("인물 기록 · 장도윤과 나눈 말을 정착지 기록에 남겼다. 다음 방문에 오락기 뒤를 확인할 수 있다.");
            }
            if (pawn) pawn.SetActive(false);
            if (Clue) Clue.gameObject.SetActive(false);
        }
        public SavedNpcStory Export() => JsonUtility.FromJson<SavedNpcStory>(JsonUtility.ToJson(State));
        public void Restore(SavedNpcStory state)
        {
            State = state == null ? new SavedNpcStory() : JsonUtility.FromJson<SavedNpcStory>(JsonUtility.ToJson(state));
            selected = -1;
            ResetDialogue();
            if (View) View.SetActive(false);
            if (pawn) pawn.SetActive(false);
        }
        public FieldPause ObserveBlock(string id)
        {
            if (id != ObservationId || !BoardReady || !InArcade) return FieldPause.OtherRoom;
            return State.Stage > 0 ? FieldPause.Complete : FieldPause.None;
        }
        public string ObserveLabel(string id) => id == ObservationId ? "오락기 뒤 흔적" : "흔적";
        public void CompleteObservation(string id, int member)
        {
            if (ObserveBlock(id) != FieldPause.None || member < 0 || member >= Arrival.Participants.Count || Arrival.Participants[member].Health <= 0) return;
            State.Stage = 1; State.Page = 0; State.MetVisit = State.VisitCount; State.PendingDialogue = true;
            Log("발견 · 오락기 뒤에서 작업 장갑과 열쇠 소리를 확인했다. 누군가 숨어 있다.");
        }
        void Log(string text) { if (Owner) Owner.ActivityLog.Add(text); }

        public void Open()
        {
            if (!FreeWorld || IsOpen || !Arrival.Main.interactable || Planner.Resolving || Planner.PendingLoot > 0 || !View) return;
            if (State.Stage == 0 && Planner.Placing) { AskForPawn(); return; }
            previousFocus = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            State.PendingDialogue = false;
            if (State.Stage == 0)
            {
                var standing = Planner.Plan.Observations.FirstOrDefault(x => x.Id == ObservationId);
                selected = standing != null ? standing.Member : -1;
            }
            Arrival.Main.interactable = Arrival.Main.blocksRaycasts = false;
            View.SetActive(true); RefreshPanel();
            if (State.Stage == 0) EventSystem.current?.SetSelectedGameObject(Back ? Back.gameObject : null);
        }
        // Stage 0 on the pawn board: the press goes where a door press goes first (FieldPawnBoard: nothing held → '대원 말을 먼저 누르세요'
        // and the free members' rings pulse; a held pawn → its place beside the trace). Without a board the status paper says so.
        void AskForPawn()
        {
            var hook = Arrival.Rooms ? Arrival.Rooms.DoorPressed : null;
            if (hook != null && Clue && hook(Clue)) return;
            if (Arrival.Status) Arrival.Status.text = ClueHint;
        }
        public void Close()
        {
            if (!IsOpen) return;
            View.SetActive(false);
            if (Arrival && Arrival.Main && Arrival.IsOpen && !Arrival.InTransit)
                Arrival.Main.interactable = Arrival.Main.blocksRaycasts = true;
            EventSystem.current?.SetSelectedGameObject(previousFocus && previousFocus.activeInHierarchy ? previousFocus : null);
        }
        public void SelectMember(int member)
        {
            if (!IsOpen || State.Stage != 0 || member < 0 || member >= Arrival.Participants.Count || Arrival.Participants[member].Health <= 0) return;
            selected = member; RefreshPanel();
        }
        public void Choose(int choice)
        {
            var buttons = State.Stage == 0 ? Choices : DialogueChoices;
            if (!IsOpen || choice < 0 || choice >= buttons.Length || !buttons[choice].IsActive() || !buttons[choice].IsInteractable()) return;
            if (State.Stage > 0 && !AwaitingDialogueChoice) return;
            if (State.Stage == 0)
            {
                if (choice == 1) { Planner.Plan.ReleaseObserve(ObservationId); Close(); Planner.Refresh(); return; }
                if (choice != 0 || selected < 0 || selected >= Arrival.Participants.Count || Arrival.Participants[selected].Health <= 0 || ObserveBlock(ObservationId) != FieldPause.None) return;
                Planner.Plan.AssignObserve(selected, ObservationId); Planner.Plan.Keep(Planner.Plan.Check(Planner.Facts()));
                string who = Arrival.Participants[selected].Name;
                Close(); Planner.Refresh();
                Arrival.Status.text = who + " · 오락기 뒤 흔적 조사 배정\n‘턴 진행’으로 다른 대원과 함께 행동합니다.";
                return;
            }
            if (State.Stage == 1)
            {
                State.Page = 1; RefreshPanel(); return;
            }
            if (State.Stage == 2)
            {
                if (choice > 1) return;
                State.Stage = 3; State.Page = 3; State.Choice = choice + 1;
                Log(choice == 0 ? "장도윤 · 재촉하지 않고 거리를 두었다. 복도 쪽에 공구 가방을 두고 왔다고 했다." :
                    "장도윤 · 원정대가 들어온 오락실 입구를 알려줬다. 지금 복도가 안전하다고 약속하지는 않았다.");
                RefreshPanel(); return;
            }
        }
        void Choice(int index, string title, string detail, bool enabled = true)
        {
            if (index >= Choices.Length) return;
            Choices[index].gameObject.SetActive(true); Choices[index].interactable = enabled;
            if (index < ChoiceTitles.Length) ChoiceTitles[index].text = title;
            if (index < ChoiceDetails.Length) ChoiceDetails[index].text = detail;
        }
        public void RefreshPanel()
        {
            if (!IsOpen) return;
            bool talking = State.Stage > 0;
            if (InvestigationLayout) InvestigationLayout.SetActive(!talking);
            if (DialogueLayout) DialogueLayout.SetActive(talking);
            if (talking) { RefreshDialogue(); return; }
            foreach (var b in Choices) b.gameObject.SetActive(false);
            for (int i = 0; i < Members.Length; i++)
            {
                bool show = State.Stage == 0 && i < Arrival.Participants.Count;
                Members[i].gameObject.SetActive(show);
                if (!show) continue;
                Members[i].interactable = Arrival.Participants[i].Health > 0;
                if (i < MemberLabels.Length) MemberLabels[i].text = Arrival.Participants[i].Name + (Arrival.Participants[i].Health <= 0 ? " · 행동 불가" : "");
                if (Members[i].targetGraphic) Members[i].targetGraphic.color = i == selected ? new Color(1, .8f, .43f) : Color.white;
            }
            if (Portrait) Portrait.gameObject.SetActive(State.Stage >= 2);
            if (UnknownPortrait) UnknownPortrait.gameObject.SetActive(State.Stage < 2);
            if (MemberHeading) MemberHeading.gameObject.SetActive(State.Stage == 0);
            Speaker.text = State.Stage == 0 ? "생활 흔적" : DisplayName;
            Title.text = State.Stage == 0 ? "밀린 오락기" : RecordTitle;
            Status.text = "읽기·대화·미루기: 0턴 · 물품 소모 없음";
            if (State.Stage == 0)
            {
                Body.text = "기계 아래, 오래된 끌림 자국 옆에 먼지가 덜 쌓인 자국이 있습니다.\n\n누가 최근에 기계를 밀었는지 살펴봅니다.\n조사할 대원을 골라 이번 턴 행동을 배정하세요.";
                bool can = selected >= 0 && selected < Arrival.Participants.Count && Arrival.Participants[selected].Health > 0;
                Choice(0, "흔적 조사 배정", "담당 1명 · 1턴 · 소음 0 · 숨죽이기 아님", can);
                if (Planner.Plan.Observations.Any(x => x.Id == ObservationId)) Choice(1, "조사 배정 해제", "시간은 흐르지 않습니다.");
                if (can)
                {
                    var now = Planner.Plan.Check(Planner.Facts());
                    var draft = Planner.Plan.Clone(); draft.AssignObserve(selected, ObservationId);
                    var forecast = Planner.Forecast(draft);
                    Status.text = (now.Actions[selected] != FieldAction.Hush && now.Actions[selected] != FieldAction.Observe ? "선택한 대원은 기존 행동에서 빠집니다.\n" : "배정만 저장 · 실제 행동은 ‘턴 진행’에서\n") +
                        Planner.ChipLines(forecast.check, forecast.outlook, forecast.hushWouldPass, " · ");
                }
                return;
            }
        }
        public string RecordBody()
        {
            if (!HasRecord) return "아직 확인한 사람이 없습니다.";
            if (State.Stage == 1) return "폐상가 · 오락실의 밀린 기계 뒤\n\n작업 장갑과 열쇠 소리, 사람의 목소리를 확인했다.\n아직 이름과 사정은 모른다. 다시 말을 걸 수 있다.";
            string text = "폐상가 · 오락실에서 만난 시설관리 직원\n\n명찰에 적힌 이름은 장도윤.";
            if (State.Stage == 2) return text + "\n\n대화를 마저 이어갈 수 있다.";
            text += "\n공구 가방을 복도 쪽에 두고 왔다고 했다.";
            text += "\n\n우리가 한 일\n" + (State.Choice == 1 ? "기계를 밀지 않고 거리를 두어 그의 말을 들었다." : "직접 지나온 오락실 입구를 알려줬다. 현재 안전을 보장하지 않았다.");
            return text + (State.Reunited ? "\n\n다음 방문의 재회\n그는 우리를 알아봤다. 닫히는 문을 받칠 방법과\n공구 가방을 옮길 도움이 필요하다." : "\n\n다음 확인\n오락기 뒤에서 다시 만나 공구 가방의 사정을 듣자.");
        }
        void LateUpdate()
        {
            if (!initialized || !Arrival || !Owner || Owner.Campaign == null) return;
            bool showClue = BoardReady && InArcade && !Arrival.InTransit;
            if (Clue && Clue.gameObject.activeSelf != showClue) Clue.gameObject.SetActive(showClue);
            if (ClueLabel) ClueLabel.text = State.Stage == 0 ? "밀린 오락기" : State.Stage >= 2 ? "장도윤" : "오락기 뒤의 사람";
            if (ClueMark) ClueMark.text = State.Stage == 0 ? "?" : "…";
            if (State.Stage >= 2 && showClue && NpcPrefab && Arrival.PawnRoot)
            {
                if (!pawn) { pawn = Instantiate(NpcPrefab, Arrival.PawnRoot); pawn.name = "StoryNpc_Doyun"; pawn.transform.localPosition = NpcPosition; var facing = pawn.GetComponent<PawnFacing>(); if (facing) facing.Face(true); }
            }
            if (pawn) pawn.SetActive(State.Stage >= 2 && showClue && !(Arrival.Encounter && (Arrival.Encounter.IsOpen || Arrival.Encounter.Battle && Arrival.Encounter.Battle.IsOpen)));
            if (State.PendingDialogue && FreeWorld && !IsOpen && !Planner.Resolving && Planner.PendingLoot == 0 && Arrival.Main.interactable) Open();
        }
        void OnDestroy() { if (pawn) Destroy(pawn); }
    }
}
