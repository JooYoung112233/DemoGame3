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
        int action;
        public string CurrentRecipeId=>recipe;

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
            if(owner.CraftPanel.UnderConstruction(id)) { Goal(title+" · 진행 중","담당자가 작업하고 있습니다.\n시간을 진행해 완료하세요.","작업 시간 확인",3);return; }
            var r=owner.CraftPanel.Recipes.First(x=>x.Id==id);
            var missing=r.Costs.Where(c=>Stock(c.MaterialId)+Bags(c.MaterialId)<c.Count).ToArray();
            if(missing.Length>0){
                string needs=string.Join(" · ",missing.Select(c=>(owner.CraftPanel.Materials.FirstOrDefault(m=>m.Id==c.MaterialId)?.Name??c.MaterialId)+" "+(c.Count-Stock(c.MaterialId)-Bags(c.MaterialId))));
                Goal("먼저 재료부터","부족: "+needs+"\n폐상가에서 수색 후 돌아오세요.","부족한 재료 수색",0);return;
            }
            bool inBags=r.Costs.Any(c=>Stock(c.MaterialId)<c.Count && Bags(c.MaterialId)>0);
            if(!inBags&&!owner.Campaign.Party.Any(p=>p.Health>0&&!owner.IsAssigned(p))&&owner.TimePanel.NextCompletion()>0){
                Goal("기존 작업 먼저 마치기","대원들이 작업 중입니다.\n시간을 진행한 뒤 새 작업을 배정하세요.","작업 시간 확인",3);return;
            }
            Goal(title,body,inBags?"가방의 재료 보관하기":"제작·복구 확인",inBags?2:1,id);
        }
        public void Evaluate()
        {
            if(!Active || owner.Campaign==null)return;
            if(State.Complete){Goal("밤의 사건 · 문밖의 두드림","관리표에 적힌 경고가 떠오릅니다.\n문을 열기 전에 상황을 살펴보세요.","문밖의 소리 확인",6);return;}
            if(owner.Campaign.Stage==JourneyStage.Settlement&&owner.ReturnPanel.HasReport)OnReturn();
            if(!State.FirstReturn)
            {Goal("첫 외출 · 복구 재료","폐상가 입구의 상자를 수색하세요.\n목재·고철을 챙겨 돌아오세요.","첫 원정 준비",0);return;}
            if(owner.Development.State.Warehouse==0)
            {Project("build-stock","회수품을 둘 자리","창고 복구: 목재 2 · 고철 1\n초과 물자는 가방에 남습니다.");return;}
            if(!owner.Development.State.Workbench)
            {Project("build-bench","다음 외출을 위한 작업대","작업대 복구: 목재 3 · 고철 2\n가져온 재료로 도구를 준비하세요.");return;}
            if(!State.ClueRead)
            {Goal("첫 사건 · 접힌 관리표","작업대를 정리하다 오래된\n상가 비축품 관리표를 찾았습니다.","관리표 읽기",4);return;}
            if(State.SurveyReturned)
            {Goal("첫 생활 · 돌아온 사람들","보관실 확인을 마치고 돌아왔습니다.\n회수품을 정리하고 기록을 남기세요.","첫 생활 기록 정리",5);return;}
            if(owner.ArrivalPanel.Rooms.StorageUnlocked)
            {Goal("다음 목적 · 보관실 확인","폐상가 → 복도 → 보관실\n비축 선반을 수색하고 귀환하세요.","보관실 원정 준비",0);return;}
            if(Stock("prybar")+Bags("prybar")>0)
            {Goal("두 번째 외출 · 잠긴 철문",Bags("prybar")==0?"지렛대를 대원 가방에 넣으세요.\n폐상가 복도의 철문을 열 수 있습니다.":"지렛대를 가진 대원을 데려가세요.\n목적지: 폐상가 복도 뒤 보관실.",Bags("prybar")==0?"지렛대 챙기기":"보관실 원정 준비",Bags("prybar")==0?2:0);return;}
            if(owner.CraftPanel.UnderConstruction("prybar")) {Project("prybar","철문을 열 도구","지렛대를 제작 중입니다.");return;}
            if(Stock("rope")+Bags("rope")<1) {Project("rope","지렛대 준비 · 손잡이 끈","천 조각 2개로 밧줄 1개를 만드세요.\n회수한 천을 사용할 수 있습니다.");return;}
            if(Stock("nails")+Bags("nails")<2) {Project("nails","지렛대 준비 · 고정용 못","고철 1개로 못 1개를 만듭니다.\n못을 총 2개 준비하세요.");return;}
            Project("prybar","철문을 열 도구","지렛대: 목재 2 · 밧줄 1 · 못 2\n제작한 뒤 대원 가방에 챙기세요.");
        }
        public SavedOpeningChapter Export() { Evaluate();return JsonUtility.FromJson<SavedOpeningChapter>(JsonUtility.ToJson(State)); }
        public void RefreshGuidance()
        {
            if(!Active)return;Evaluate();owner.NoticeTitle.text=GoalTitle;owner.NoticeBody.text=GoalBody;
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
                    owner.ActivityLog.Add("단서 · 작업대 서랍의 관리표: 폐상가 복도 끝 철문 뒤에 비축 선반이 있다.");
                    ShowEvent("접힌 관리표", "<size=26>작업대 서랍에서 발견한 오래된 기록.</size>\n\n<size=23><color=#4D5145>비축 장소</color></size>\n폐상가 · 복도 끝 보관실 — 물과 통조림\n\n<size=23><color=#4D5145>다른 필체로 덧붙인 경고</color></size>\n<color=#713C29>“안에서 두드려도 대답하지 말 것.”</color>\n\n<size=23><color=#4D5145>다음 목표</color></size>\n지렛대를 제작해 보관실로 가져가자.");break;
                case 5:
                    State.Complete=true;
                    owner.ActivityLog.Add("첫 생활 완료 · 수색한 재료로 창고와 작업대를 복구하고, 지렛대로 보관실을 확인해 귀환했다.");
                    ShowEvent("첫 생활을 이어갈 준비", "회수한 재료로 창고와 작업대를 복구했다.\n직접 만든 도구로 잠긴 보관실도 확인했다.\n\n관리표의 경고가 누구의 말인지는 아직 모른다.\n\n이제 잠자리·조리대 복구와 다음 수색을 계획하자.\n남긴 물품과 열린 문은 다음 방문에도 유지된다.");
                    owner.NoticeTitle.text="다음 생활 준비";owner.NoticeBody.text="잠자리·조리대를 복구하세요.\n남은 수색지와 방문자를 확인하세요.";break;
            }
        }
    }
}


