using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    public sealed class HomeSelectionController:MonoBehaviour
    {
        public HomeCandidateCard[] Cards;
        public PartyRoster Roster;
        public Image[] PartyPortraits;
        public Text[] PartyNames;
        public Text LocationName,LocationDescription,Resources,Hint;
        public GameObject ResourceIcons;
        public Button Back,Continue;
        public string PartyScene="PartySelection",DestinationScene="NightExpedition";
        public CampaignState Campaign{get;private set;}
        public int SelectedIndex{get;private set;}=-1;
        bool leaving;
        void Awake()
        {
            Campaign=PartySelectionSession.Pending;
            if(Campaign!=null&&Campaign.Stage!=JourneyStage.HomeChoice)Campaign=null;
            Back.onClick.AddListener(GoBack);Continue.onClick.AddListener(Confirm);
            var members=Campaign?.Party.ToArray();
            for(int i=0;i<PartyNames.Length;i++){
                bool valid=members!=null&&i<members.Length;PartyNames[i].text=valid?members[i].Name:"동료 미선택";
                var chosen=valid&&i<PartySelectionSession.Selected.Count?Roster.Candidates.FirstOrDefault(c=>c.Id==PartySelectionSession.Selected[i]):null;
                PartyPortraits[i].sprite=chosen?.Portrait;PartyPortraits[i].enabled=chosen!=null;
            }
            Refresh();
        }
        public void Select(int index){if(leaving||index<0||index>=Cards.Length)return;SelectedIndex=index;Refresh();}
        void Refresh()
        {
            var sites=(Campaign??new CampaignState()).Sites;
            for(int i=0;i<Cards.Length;i++){int index=i;Cards[i].Bind(sites[i],i==SelectedIndex,()=>Select(index));}
            Continue.interactable=Campaign!=null&&SelectedIndex>=0&&!leaving;
            Hint.text=Campaign==null?"먼저 함께할 두 사람을 선택하세요.":SelectedIndex<0?"처음 머물 정착지를 선택하세요.":sites[SelectedIndex].Name+"에서 두 사람의 여정을 시작합니다.";
            LocationName.text=SelectedIndex<0?"돌아올 곳을 정해주세요":sites[SelectedIndex].Name;
            LocationDescription.text=SelectedIndex<0?"장소 카드를 누르면 시작 자원과\n정착지의 특징을 확인할 수 있습니다.":sites[SelectedIndex].Description;
            Resources.text=SelectedIndex<0?"장소마다 시작 보급품과 탄약,\n휴식 시 회복량이 다릅니다.":"시작 보급품  "+sites[SelectedIndex].Supplies+"\n시작 탄약  "+sites[SelectedIndex].Ammo+"\n휴식 시 체력  +"+sites[SelectedIndex].Recovery;
            if(ResourceIcons)ResourceIcons.SetActive(SelectedIndex>=0);
        }
        public void GoBack(){if(leaving)return;leaving=true;PartySelectionSession.Pending=null;SceneManager.LoadScene(PartyScene);}
        public void Confirm()
        {
            if(leaving||Campaign==null||SelectedIndex<0)return;
            if(!Application.CanStreamedLevelBeLoaded(DestinationScene)){Hint.text="다음 화면을 열 수 없습니다.";return;}
            if(!Campaign.Settle(SelectedIndex))return;
            leaving=true;PartySelectionSession.Pending=Campaign;Continue.interactable=false;SceneManager.LoadScene(DestinationScene);
        }
    }
}
