using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.NightRun
{
    public sealed partial class NightRunView
    {
        public CampaignState Campaign{get;private set;}
        GameObject journeyRoot,partyPage,homesPage,homePage,lostPage;
        Text journeyTitle,journeyHint,homeResources,homeMembers;
        Button confirmParty,restButton,ammoButton;
        readonly Button[] candidateButtons=new Button[4];
        readonly Text[] candidateLabels=new Text[4];
        public void ToggleCandidate(int i){Campaign.Toggle(i);Refresh();}
        public void ConfirmParty(){Campaign.ConfirmParty();Refresh();}
        public void ChooseHome(int i){Campaign.Settle(i);Refresh();}
        public void Depart(){if(!Campaign.Depart())return;State=Campaign.ActiveRun;Selected=0;SelectedOrder=Order.Move;StartVisit();Refresh();}
        void Portrait(Transform parent,int candidate,float x,float y,float size)
        {
            var baseImg=Box(parent,"TintableBase",x+size*.16f,y+size*.74f,size*.68f,size*.22f,World.ColorFor(candidate));baseImg.sprite=World.WhiteBase;baseImg.raycastTarget=false;
            var body=Box(parent,"SharedSilhouette",x,y,size,size,Color.white);body.sprite=World.BodyFor(candidate);body.preserveAspect=true;body.raycastTarget=false;
        }
        void BuildJourney()
        {
            journeyRoot=Group(root,"Journey");Box(journeyRoot.transform,"HeaderPaper",30,28,950,108,Cream);
            journeyTitle=Label(journeyRoot.transform,"",54,42,904,76,36,Ink);
            Box(journeyRoot.transform,"Footer",24,979,1872,78,PanelColor);journeyHint=Label(journeyRoot.transform,"",44,991,1340,60,22,Cream);
            MakeButton(journeyRoot.transform,"NewJourney",1510,992,360,49,"새 게임 (진행 초기화)",Restart);
            partyPage=Group(journeyRoot.transform,"PartySelection");
            for(int i=0;i<4;i++)
            {
                int id=i;candidateButtons[i]=MakeButton(partyPage.transform,"Candidate_"+i,46+i*464,230,436,579,"",()=>ToggleCandidate(id));
                Portrait(candidateButtons[i].transform,i,143,91,150);candidateLabels[i]=Label(candidateButtons[i].transform,"",24,309,388,246,25,Ink);
            }
            confirmParty=MakeButton(partyPage.transform,"ConfirmParty",1260,867,600,76,"선택한 2명과 시작 →",ConfirmParty);
            Label(partyPage.transform,"모험가를 누르면 선택 / 다시 누르면 해제",46,884,1190,54,27,Cream);
            homesPage=Group(journeyRoot.transform,"HomeSelection");
            for(int i=0;i<3;i++)
            {
                int id=i;var site=new CampaignState().Sites[i];var b=MakeButton(homesPage.transform,"Home_"+i,46+i*621,403,590,405,"",()=>ChooseHome(id));
                Label(b.transform,site.Name,25,30,540,56,38,Ink);
                Label(b.transform,site.Description+"\n\n시작 보급품 "+site.Supplies+" · 탄약 "+site.Ammo+"\n휴식 체력 +"+site.Recovery+"\n\n이곳에 정착하기 →",25,114,540,269,26,Ink);
            }
            MakeButton(homesPage.transform,"BackToParty",46,866,470,78,"← 모험가 다시 선택",()=>{if(Campaign.UsesFrontEndSelection)UnityEngine.SceneManagement.SceneManager.LoadScene("PartySelection");else{Campaign.BackToParty();Refresh();}});
            Label(homesPage.transform,"현재 생존지 배경은 공통 시안 1종입니다.",675,876,1185,55,26,Cream,TextAnchor.MiddleRight);
            homePage=Group(journeyRoot.transform,"Settlement");Box(homePage.transform,"SettlementFooter",24,778,1872,184,PanelColor);
            homeResources=Label(homePage.transform,"",50,798,430,138,28,Cream);homeMembers=Label(homePage.transform,"",510,798,620,138,25,Cream);
            restButton=MakeButton(homePage.transform,"Rest",1160,795,340,62,"휴식 · 보급품 1",()=>{Campaign.Rest();Refresh();});
            ammoButton=MakeButton(homePage.transform,"PrepareAmmo",1160,877,340,62,"탄약 +2 · 보급품 1",()=>{Campaign.PrepareAmmo();Refresh();});
            MakeButton(homePage.transform,"Depart",1525,795,341,144,"폐상가로\n수색 출발 →",Depart);
            MakeButton(homePage.transform,"HomeLighting",1510,40,360,65,"조명 켜기 / 끄기",()=>{World.LightingEnabled=!World.LightingEnabled;Refresh();});
            lostPage=Group(journeyRoot.transform,"JourneyLost");Box(lostPage.transform,"LostPaper",300,330,1320,460,Cream);
            Label(lostPage.transform,"돌아오는 발소리가 없다.\n\n새로운 모험가 조합으로 다시 시작할 수 있다.",350,380,1220,210,36,Ink);
            MakeButton(lostPage.transform,"StartOver",600,670,720,75,"새로운 여정",Restart);
        }
        void RefreshJourney()
        {
            partyPage.SetActive(Campaign.Stage==JourneyStage.Party);homesPage.SetActive(Campaign.Stage==JourneyStage.HomeChoice);homePage.SetActive(Campaign.Stage==JourneyStage.Settlement);lostPage.SetActive(Campaign.Stage==JourneyStage.Lost);
            journeyTitle.text=Campaign.Stage==JourneyStage.Party?"함께 살아남을 두 사람":Campaign.Stage==JourneyStage.HomeChoice?"돌아올 정착지를 정하자":Campaign.Stage==JourneyStage.Lost?"여정의 끝":"DAY "+Campaign.Day+" · "+Campaign.Home.Name;
            journeyHint.text=Campaign.Message;
            for(int i=0;i<4;i++){var p=Campaign.Candidates[i];bool chosen=Campaign.Chosen.Contains(i);candidateButtons[i].GetComponent<Image>().color=chosen?Gold:Cream;candidateLabels[i].text=p.Name+" / "+p.Role+"\n\n"+p.Description+"\n체력 "+p.MaxHealth+" · 사격 +"+p.Aim+"%p\n\n"+(chosen?"● 선택됨":"선택하기");}
            confirmParty.interactable=Campaign.Chosen.Count==2;if(Campaign.Home==null)return;
            homeResources.text="보급품 "+Campaign.Supplies+"\n탄약 "+Campaign.Ammo+"\n"+Campaign.Home.Name;
            homeMembers.text=string.Join("\n\n",Campaign.Party.Select(p=>p.Name+" / "+p.Role+"  "+(p.Health>0?"체력 "+p.Health+"/"+p.MaxHealth:"미귀환")));
            restButton.interactable=Campaign.Supplies>0&&Campaign.Party.Any(p=>p.Health>0&&p.Health<p.MaxHealth);ammoButton.interactable=Campaign.Supplies>0;
        }
    }
}
