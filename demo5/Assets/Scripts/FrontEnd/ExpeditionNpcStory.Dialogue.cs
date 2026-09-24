using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    public sealed partial class ExpeditionNpcStory
    {
        public GameObject InvestigationLayout, DialogueLayout;
        public Text DialogueSpeaker, DialogueBody, DialogueUnknownPortrait, NextLabel;
        public Image DialoguePortrait;
        public Button Next, ContinueSurface;
        public Button[] DialogueChoices = Array.Empty<Button>();
        public Text[] DialogueChoiceLabels = Array.Empty<Text>();
        // These short reading beats are presentation only. Saves retain the existing
        // Stage/Page checkpoint; closing the window preserves the current beat.
        public int DialogueLine { get; private set; }
        string dialogueSection;
        bool ReunionPending => State.Stage == 3 && State.Returned && State.VisitCount > State.MetVisit && !State.Reunited;
        string DialogueSection => State.Stage + ":" + State.Page + ":" + ReunionPending + ":" + State.Reunited;
        int LastDialogueLine => State.Stage == 2 || ReunionPending ? 2 : State.Reunited ? 0 : 1;
        public bool AwaitingDialogueChoice => IsOpen && State.Stage > 0 &&
            dialogueSection == DialogueSection && DialogueLine == LastDialogueLine &&
            (State.Stage == 1 && State.Page == 0 || State.Stage == 2);

        void InitializeDialogue()
        {
            if (Next) Next.onClick.AddListener(AdvanceDialogue);
            if (ContinueSurface) ContinueSurface.onClick.AddListener(AdvanceDialogue);
            for (int i = 0; i < DialogueChoices.Length; i++)
            { int index = i; DialogueChoices[i].onClick.AddListener(() => Choose(index)); }
        }
        void ResetDialogue() { dialogueSection = null; DialogueLine = 0; }
        public void AdvanceDialogue()
        {
            if (!IsOpen || State.Stage == 0 || !Next || !Next.IsActive() || !Next.IsInteractable() || AwaitingDialogueChoice) return;
            if (DialogueLine < LastDialogueLine) { DialogueLine++; RefreshPanel(); return; }
            if (State.Stage == 1 && State.Page == 1)
            {
                State.Stage = 2; State.Page = 2;
                Log("이름 확인 · 명찰에서 장도윤이라는 이름을 읽었다. 이 상가의 시설관리 직원이었다.");
                RefreshPanel(); return;
            }
            if (ReunionPending)
            {
                State.Reunited = true;
                Log("재회 · 장도윤은 지난 대화를 기억했다. 복도에서 공구 가방을 찾으려면 문을 받칠 방법이 필요하다고 했다.");
            }
            Close();
        }
        void RefreshDialogue()
        {
            if (dialogueSection != DialogueSection) { dialogueSection = DialogueSection; DialogueLine = 0; }
            bool known = State.Stage >= 2;
            if (DialoguePortrait) DialoguePortrait.gameObject.SetActive(known);
            if (DialogueUnknownPortrait) DialogueUnknownPortrait.gameObject.SetActive(!known);
            foreach (var button in DialogueChoices) button.gameObject.SetActive(false);
            string text; bool narration = false;
            if (State.Stage == 1 && State.Page == 0)
            {
                narration = DialogueLine == 0;
                text = narration ? "기계 아래에 작업 장갑이 말려 있다.\n안쪽에서 열쇠가 한 번 부딪히더니 소리가 멎는다." : "기계는 밀지 마요.";
            }
            else if (State.Stage == 1)
            {
                narration = DialogueLine == 1;
                text = narration ? "그가 기계 뒤에서 천천히 몸을 편다.\n가방 끈에 걸린 낡은 명찰이 돌아간다." : "입구로 온 거죠? 복도 쪽 말고.";
            }
            else if (State.Stage == 2)
            {
                narration = DialogueLine == 0;
                text = narration ? "낡은 명찰에는 ‘장도윤 · 시설관리’라고 적혀 있다." : DialogueLine == 1 ?
                    "저도 나가려던 참이에요. 두고 온 게 있어서." : "공구 가방은 복도 쪽에 있어요.\n문이 자꾸 저절로 닫혀요.";
            }
            else if (ReunionPending)
            {
                narration = DialogueLine == 2;
                text = DialogueLine == 0 ? (State.Choice == 1 ? "그때 기계 안 밀어줘서 고마웠어요." : "알려준 입구까지는 갔다 왔어요.") :
                    DialogueLine == 1 ? "공구 가방은 아직 안쪽에 있어요.\n문부터 받쳐야겠어요." :
                    "문을 고정할 방법과 가방을 옮길 도움이 필요하다.\n들은 이야기를 기록해 두자.";
            }
            else if (State.Reunited)
                text = "문을 받칠 만한 게 있으면 알려줘요.\n공구 가방을 두고 갈 수는 없어서요.";
            else
            {
                narration = DialogueLine == 1;
                text = narration ? "그는 공구 가방을 두고 떠나기 어려워한다.\n귀환한 뒤 기록을 남기고, 다음 방문에 다시 찾아올 수 있다." :
                    State.Choice == 1 ? "조금만 더 있다 나갈게요.\n기다려 줘서 고마워요." : "입구는 기억할게요.\n지금 나가겠다는 건 아니에요.";
            }
            DialogueSpeaker.text = narration ? "주변" : DisplayName;
            DialogueBody.text = text;
            if (AwaitingDialogueChoice)
            {
                DialogueChoice(0, State.Stage == 1 ? "거기서 나올 필요 없어요." : "천천히 얘기해도 괜찮아요.");
                DialogueChoice(1, State.Stage == 1 ? "[조금 떨어져 기다린다]" : "[우리가 들어온 입구를 알려준다]");
            }
            Next.gameObject.SetActive(!AwaitingDialogueChoice);
            NextLabel.text = DialogueLine < LastDialogueLine ? "다음  ›" : State.Stage == 1 ? "명찰 확인  ›" :
                ReunionPending ? "기록하고 대화 마치기" : "대화 마치기";
            // Let the UI input module submit once; do not also handle Enter/Space here.
            var focus = Next.gameObject.activeSelf ? Next.gameObject : DialogueChoices.Length > 0 ? DialogueChoices[0].gameObject : null;
            EventSystem.current?.SetSelectedGameObject(focus);
        }
        void DialogueChoice(int index, string text)
        {
            if (index >= DialogueChoices.Length) return;
            DialogueChoices[index].gameObject.SetActive(true); DialogueChoices[index].interactable = true;
            if (index < DialogueChoiceLabels.Length) DialogueChoiceLabels[index].text = (index + 1) + ".  " + text;
        }
    }
}
