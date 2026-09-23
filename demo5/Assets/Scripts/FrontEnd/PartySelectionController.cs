using System.Collections.Generic;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    public static class PartySelectionSession
    {
        public static readonly List<string> Selected=new List<string>();
        public static CampaignState Pending;
        public static void Clear(){Selected.Clear();Pending=null;CampaignPersistence.ClearPending();}
        public static CampaignState Take(){var result=Pending;Pending=null;return result;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPlaySession()=>Clear();
    }
    public sealed class PartySelectionController : MonoBehaviour
    {
        public PartyRoster Roster;
        public PartyCandidateCard[] Cards;
        public Button Previous,NextPage,Back,Continue;
        public Text SelectionCount,Message,PageNumber;
        public PartyCandidateDetails Details;
        public string FocusedCandidateId {get;private set;}
        public string TitleScene="StartMenu",DestinationScene="HomeSelection";
        int page;bool leaving;
        public int SelectedCount=>PartySelectionSession.Selected.Count;
        public int Page=>page;
        public void Preview(string id)
        {
            if(leaving)return;
            var candidate=Roster.Candidates.FirstOrDefault(c=>c.Id==id);
            if(candidate==null)return;
            FocusedCandidateId=id;
            if(Details)Details.Bind(candidate,PartySelectionSession.Selected.Contains(id));
        }
        void Awake()
        {
            PartySelectionSession.Selected.RemoveAll(id=>!Roster.Candidates.Any(c=>c.Id==id));
            Previous.onClick.AddListener(()=>ShowPage(page-1));NextPage.onClick.AddListener(()=>ShowPage(page+1));
            Back.onClick.AddListener(GoBack);Continue.onClick.AddListener(Confirm);Refresh();
        }
        void ShowPage(int requested)
        {if(leaving)return;page=Mathf.Clamp(requested,0,Mathf.Max(0,(Roster.Candidates.Length-1)/Cards.Length));Refresh();if(Roster.Candidates.Length>0)Preview(Roster.Candidates[page*Cards.Length].Id);}
        public void Toggle(string id)
        {
            if(leaving||!Roster.Candidates.Any(c=>c.Id==id))return;
            FocusedCandidateId=id;
            var selected=PartySelectionSession.Selected;
            if(selected.Contains(id)){selected.Remove(id);Message.text="함께할 두 사람을 선택하세요.";}
            else if(selected.Count<2){selected.Add(id);Message.text=selected.Count==2?"두 사람의 준비가 끝났습니다.":"한 사람을 더 선택하세요.";}
            else Message.text="두 명까지 선택할 수 있어요. 바꾸려면 먼저 선택을 해제하세요.";
            Refresh();
        }
        public void Refresh()
        {
            int pages=Mathf.Max(1,(Roster.Candidates.Length+Cards.Length-1)/Cards.Length);
            for(int i=0;i<Cards.Length;i++)
            {
                int index=page*Cards.Length+i;Cards[i].gameObject.SetActive(index<Roster.Candidates.Length);
                if(index>=Roster.Candidates.Length)continue;
                var candidate=Roster.Candidates[index];string id=candidate.Id;
                Cards[i].Bind(candidate,PartySelectionSession.Selected.Contains(id),()=>Toggle(id),()=>Preview(id));
            }
            SelectionCount.text=SelectedCount+" / 2";Continue.interactable=SelectedCount==2&&!leaving;
            Previous.interactable=page>0&&!leaving;NextPage.interactable=page+1<pages&&!leaving;
            PageNumber.text=pages>1?(page+1)+" / "+pages:"";
            if(Roster.Candidates.Length>0)Preview(Roster.Candidates.Any(c=>c.Id==FocusedCandidateId)?FocusedCandidateId:Roster.Candidates[0].Id);
        }
        public void GoBack(){if(leaving)return;leaving=true;SceneManager.LoadScene(TitleScene);}
        public void Confirm()
        {
            if(leaving||SelectedCount!=2)return;
            if(!Application.CanStreamedLevelBeLoaded(DestinationScene)){Message.text="다음 화면을 열 수 없습니다.";return;}
            var candidates=Roster.Candidates.Select(c=>new Adventurer(c.DisplayName,string.IsNullOrEmpty(c.RoleTitle)?c.DisplayName:c.RoleTitle,c.Description,c.Health,c.Aim,c.BagCapacity)).ToArray();
            var campaign=new CampaignState(candidates,true);
            foreach(var id in PartySelectionSession.Selected)campaign.Toggle(System.Array.FindIndex(Roster.Candidates,c=>c.Id==id));
            if(!campaign.ConfirmParty())return;
            leaving=true;PartySelectionSession.Pending=campaign;Refresh();SceneManager.LoadScene(DestinationScene);
        }
    }
}
