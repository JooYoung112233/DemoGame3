using System;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Demo5.FrontEnd
{
    [Serializable] public sealed class SavedOpeningChapter
    {
        public bool Enabled, FirstReturn, ClueRead, SurveyReturned, Complete;
        public bool NightHeard; public int NightChoice;
    }

    // Goals observe real inventory, work orders and expedition state; they grant no resources.
    public sealed partial class SettlementOpeningChapter : MonoBehaviour
    {
        public SavedOpeningChapter State = new SavedOpeningChapter();
        public GameObject View;
        public Text EventTitle, EventBody;
        public Button Back;
        public bool IsOpen=>(View&&View.activeSelf)||(NightView&&NightView.activeSelf)||(RecordsView&&RecordsView.activeSelf);
        SettlementController owner;
        GameObject returnFocus;
        public bool Active => State.Enabled && (!State.Complete || NightAvailable);
        public string GoalTitle { get; private set; }
        public string GoalBody { get; private set; }
        public string ActionText { get; private set; }
        string recipe;
        string shownGoalTitle,shownGoalBody;
        int action;
        public string CurrentRecipeId=>recipe;
        public int CurrentAction=>action;

        public void Initialize(SettlementController c, bool restoring)
        {
            owner=c;View.SetActive(false);Back.onClick.AddListener(Close);InitializeNight();InitializeRecords();
            State=new SavedOpeningChapter { Enabled=c.Campaign!=null&&!restoring };
        }
        public void OnReturn()
        {
            if(!Active)return;
            State.FirstReturn=true;
            if(!State.SurveyReturned && State.ClueRead && owner.ArrivalPanel.Rooms.StorageVisited && owner.ArrivalPanel.Loot.State(6).Complete)
            {
                State.SurveyReturned=true;
                owner.ActivityLog.Add("첫 생활 · 폐상가 보관실을 확인하고 원정대가 돌아왔다.");
            }
        }
        int Stock(string id)=>owner.CraftPanel.Available(id);
        int Bags(string id)=>owner.Campaign.Party.Sum(p=>owner.InventoryPanel.CountFor(p,id));
        void Goal(string title,string body,string label,int target,string project=null)
        {GoalTitle=title;GoalBody=body;ActionText=label;action=target;recipe=project;}
        void Project(string id,string title,string body)
        {
            if(owner.CraftPanel.UnderConstruction(id)) { Goal(title+" · 작업 중","재료를 다듬고 조립하는 중입니다.\n작업이 끝날 때까지 시간을 보냅시다.","작업 시간 확인",3);return; }
            var r=owner.CraftPanel.Recipes.First(x=>x.Id==id);
            var missing=r.Costs.Where(c=>Stock(c.MaterialId)+Bags(c.MaterialId)<c.Count).ToArray();
            if(missing.Length>0){
                string needs=string.Join(" · ",missing.Select(c=>(owner.CraftPanel.Materials.FirstOrDefault(m=>m.Id==c.MaterialId)?.Name??c.MaterialId)+" "+(c.Count-Stock(c.MaterialId)-Bags(c.MaterialId))));
                Goal("아직 자재가 모자랍니다","부족: "+needs+"\n폐상가에서 찾아 가방에 챙겨옵시다.","부족한 재료 수색",0);return;
            }
            bool inBags=r.Costs.Any(c=>Stock(c.MaterialId)<c.Count && Bags(c.MaterialId)>0);
            if(!inBags&&!owner.Campaign.Party.Any(p=>p.Health>0&&!owner.IsAssigned(p))&&owner.TimePanel.NextCompletion()>0){
                Goal("일손을 기다리는 동안","모두 다른 일을 맡고 있습니다.\n먼저 맡긴 일을 마칩시다.","작업 시간 확인",3);return;
            }
            Goal(title,body,inBags?"가방의 재료 보관하기":"제작·복구 확인",inBags?2:1,id);
        }
        public void Evaluate()
        {
            if(!Active || owner.Campaign==null)return;
            if(State.Complete){Goal("밤의 사건 · 문밖의 두드림","관리표에 적힌 경고가 떠오릅니다.\n문을 열기 전에 상황을 살펴보세요.","문밖의 소리 확인",6);return;}
            if(owner.Campaign.Stage==JourneyStage.Settlement&&owner.ReturnPanel.HasReport)OnReturn();
            if(!State.FirstReturn)
            {
                if(!owner.Campaign.Party.Any(p=>p.Health>0&&!owner.IsAssigned(p))&&owner.TimePanel.NextCompletion()>0)
                {Goal("맡긴 일이 끝나면 출발","나갈 수 있는 대원이 모두 작업 중입니다.\n예약한 일을 마친 뒤 동행을 정합시다.","작업 시간 확인",3);return;}
                if(owner.MissingPerson)Goal(owner.MissingPerson.GoalTitle,owner.MissingPerson.GoalBody,"폐상가 갈 준비",0);
                else Goal("머물 곳을 고칠 자재","보관할 자리와 작업대가 필요합니다.\n폐상가에서 목재·고철을 찾아봅시다.","폐상가 갈 준비",0);return;
            }
            if(owner.Development.State.Warehouse==0)
            {Project("build-stock","회수품을 둘 자리","짐을 내려놓아야 다시 나설 수 있습니다.\n목재 2 · 고철 1로 창고를 고칩시다.");return;}
            if(!owner.Development.State.Workbench)
            {Project("build-bench","도구를 만들 작업대","재료를 펼칠 튼튼한 작업면이 필요합니다.\n목재 3 · 고철 2로 작업대를 고칩시다.");return;}
            if(!State.ClueRead)
            {Goal("서랍 안의 접힌 관리표","작업대 서랍에 물자 기록이 남아 있습니다.\n전에 머물던 사람이 쓴 걸까요?","관리표 읽기",4);return;}
            if(State.SurveyReturned)
            {Goal("직접 확인한 보관실","관리표에 적힌 곳을 확인했습니다.\n알아낸 사실을 기록해둡시다.","첫 생활 기록 정리",5);return;}
            if(owner.ArrivalPanel.Rooms.StorageUnlocked)
            {Goal("관리표 속 비축 선반","보관실로 통하는 문은 열렸습니다.\n선반에 무엇이 남았는지 확인합시다.","보관실 원정 준비",0);return;}
            if(Stock("prybar")+Bags("prybar")>0)
            {Goal("걸린 문틈을 벌릴 도구",Bags("prybar")==0?"문틈을 벌릴 지렛대가 완성됐습니다.\n함께 나갈 대원의 가방에 챙깁시다.":"지렛대를 가진 대원과 함께 갑시다.\n폐상가 복도 뒤에 보관실이 있습니다.",Bags("prybar")==0?"지렛대 챙기기":"보관실 원정 준비",Bags("prybar")==0?2:0);return;}
            if(owner.CraftPanel.UnderConstruction("prybar")) {Project("prybar","문틈을 벌릴 도구","지렛대를 조립하고 있습니다.");return;}
            if(Stock("rope")+Bags("rope")<1) {Project("rope","손잡이를 감쌀 끈","맨손에 가시가 박히지 않게 감쌀 끈입니다.\n천 조각 2개로 밧줄 1개를 만듭시다.");return;}
            if(Stock("nails")+Bags("nails")<2) {Project("nails","목재를 고정할 못","지렛대가 벌어지지 않게 못 2개가 필요합니다.\n고철 1개로 못 1개를 만듭시다.");return;}
            Project("prybar","문틈을 벌릴 도구","목재 2 · 밧줄 1 · 못 2로 조립합니다.\n완성하면 문틈을 벌릴 수 있습니다.");
        }
        public SavedOpeningChapter Export() { Evaluate();return JsonUtility.FromJson<SavedOpeningChapter>(JsonUtility.ToJson(State)); }
        public void RefreshGuidance()
        {
            if(!Active)return;Evaluate();
            // Skipping teaching leaves genuine discoveries and night events available.
            if(owner.TutorialSkipped&&action<4){
                owner.Introduction.Action.gameObject.SetActive(false);
                if(owner.NoticeTitle.text==shownGoalTitle&&owner.NoticeBody.text==shownGoalBody){
                    owner.NoticeTitle.text="자유롭게 생활하기";
                    owner.NoticeBody.text="출입구에서 원정을 준비할 수 있습니다.\n시설은 가져온 재료로 복구하세요.";
                }
                shownGoalTitle=shownGoalBody=null;return;
            }
            owner.NoticeTitle.text=GoalTitle;owner.NoticeBody.text=GoalBody;
            shownGoalTitle=GoalTitle;shownGoalBody=GoalBody;
            owner.Introduction.Action.gameObject.SetActive(true);owner.Introduction.ActionLabel.text=ActionText;
        }
        void ShowEvent(string title,string body)
        {
            if(IsOpen)return;returnFocus=EventSystem.current?.currentSelectedGameObject;
            EventTitle.text=title;EventBody.text=body;owner.Main.interactable=owner.Main.blocksRaycasts=false;
            View.SetActive(true);EventSystem.current?.SetSelectedGameObject(Back.gameObject);
        }
        public void Close()
        {
            if(!IsOpen)return;View.SetActive(false);if(NightView)NightView.SetActive(false);if(RecordsView)RecordsView.SetActive(false);owner.Main.interactable=owner.Main.blocksRaycasts=true;
            EventSystem.current?.SetSelectedGameObject(returnFocus);
        }
        public void Act()
        {
            if(!Active||owner.Campaign.Stage!=JourneyStage.Settlement||!owner.Main.interactable)return;
            Evaluate();
            switch(action)
            {
                case 6: OpenNight();break;
                case 0: owner.ExpeditionPanel.Open();break;
                case 1: owner.CraftPanel.Open();owner.CraftPanel.FocusRecipe(recipe);break;
                case 2: owner.InventoryPanel.Open();if(recipe!=null)owner.InventoryPanel.Notice.text="복구·제작 재료를 보관하세요 · 창고가 가득 차면 다른 물자를 가방으로 옮기세요.";break;
                case 3: owner.TimePanel.Open();break;
                case 4:
                    State.ClueRead=true;
                    owner.ActivityLog.Add("단서 · 작업대 서랍의 관리표: 폐상가 보관실에 비축 선반. 문이 걸려 틈을 벌릴 도구가 필요하다고 적혀 있다.");
                    ShowEvent("접힌 관리표", "<size=26>작업대 서랍에 남겨진 물자 기록.</size>\n\n<size=23><color=#4D5145>폐상가 · 복도 끝 보관실</color></size>\n물과 통조림. 문이 걸린다. 도구를 가져올 것.\n\n<color=#713C29>“문 너머에서 세 번 두드리면, 먼저 대답하지 말 것.”</color>\n\n지렛대로 문틈을 벌려보자.\n비축품이 아직 남아 있는지는 알 수 없다.");break;
                case 5:
                    State.Complete=true;
                    owner.ActivityLog.Add("첫 생활 완료 · 수색한 재료로 창고와 작업대를 복구하고, 지렛대로 보관실을 확인해 귀환했다.");
                    string next=!owner.Development.State.Bed&&!owner.Development.State.Cooker?"바닥 잠자리 대신 침대를 고치고,\n음식을 준비할 조리대를 마련하자.":!owner.Development.State.Bed?"바닥 잠자리 대신 침대를 고쳐\n제대로 몸을 쉬게 할 곳을 마련하자.":!owner.Development.State.Cooker?"음식을 준비할 조리대를 마련하고\n다음에 필요한 물자를 찾아보자.":"쉬고 먹을 자리가 생겼다.\n다음에는 필요한 물자를 찾아보자.";
                    ShowEvent("첫 생활을 이어갈 준비", "회수한 재료로 창고와 작업대를 복구했다.\n직접 만든 도구로 보관실도 확인했다.\n\n관리표의 경고가 누구의 말인지는 아직 모른다.\n\n"+next);
                    owner.NoticeTitle.text="생활을 이어갈 준비";owner.NoticeBody.text="쉬고 먹을 자리와 남은 물자를 살펴봅시다.\n다음 수색은 필요한 물자에 맞춰 정하세요.";break;
            }
        }
    }
}


