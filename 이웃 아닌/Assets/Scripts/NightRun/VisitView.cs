using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.NightRun
{
    public sealed partial class NightRunView
    {
        public ExplorationRun Visit {get;private set;}
        GameObject visitPage,searchControls,combatControls,meetingControls,resultControls;
        Text visitHeader,visitClock,visitLog,searchInfo,combatInfo,resultText,visitResources;
        readonly Button[] sites=new Button[3],visitCards=new Button[2],paceButtons=new Button[3],dutyButtons=new Button[3],foeButtons=new Button[2];
        readonly Text[] siteTexts=new Text[3],visitCardTexts=new Text[2],foeTexts=new Text[2];
        readonly Button[] boardButtons=new Button[24];
        Button searchCommit,visitReturn,visitResume,visitWidth,previewEncounter;
        Image visitLightBar,visitNoiseBar;
        int chosenSite,searcher;SearchPace pace=SearchPace.Normal;PartnerDuty duty=PartnerDuty.Watch;bool ranged;
        public void StartVisit(){Visit=new ExplorationRun(State);chosenSite=0;searcher=State.Squad.FindIndex(p=>p.Health>0);pace=SearchPace.Normal;duty=PartnerDuty.Watch;ranged=false;}
        public void SearchVisit(){Visit.Search(chosenSite,pace,searcher,duty);Refresh();}
        public void EncounterPreview(){Visit.PreviewEncounter();Refresh();}
        public void FightVisit(){Visit.BeginCombat();Refresh();}
        public void AttackVisit(int enemy){Visit.Attack(enemy,ranged);Refresh();}
        public void ReturnVisit(){if(Visit.Return()){Campaign.Return();Visit=null;}Refresh();}
        public void ResumeVisit(){Visit.Resume();if(Visit.Phase==VisitPhase.Finished){Campaign.Return();Visit=null;}Refresh();}
        void BuildVisit()
        {
            visitPage=Group(root,"ExplorationAndEncounter");
            Box(visitPage.transform,"PlacePaper",18,22,354,92,Cream);visitHeader=Label(visitPage.transform,"폐상가\nB1 · 버려진 아케이드",37,28,320,78,27,Ink);
            Box(visitPage.transform,"TimePaper",1598,22,284,92,Cream);visitClock=Label(visitPage.transform,"",1619,28,250,78,24,Ink);
            for(int i=0;i<3;i++){int id=i;Vector2 p=new[]{new Vector2(570,300),new Vector2(1315,360),new Vector2(1520,435)}[i];sites[i]=MakeButton(visitPage.transform,"SearchSite_"+i,p.x-100,p.y-72,200,56,"",()=>{chosenSite=id;Refresh();});siteTexts[i]=Label(sites[i].transform,"",7,3,186,50,20,Ink,TextAnchor.MiddleCenter);}
            for(int side=0;side<2;side++)for(int lane=0;lane<4;lane++)for(int depth=0;depth<3;depth++)
            {
                int d=depth,l=lane,index=side*12+lane*3+depth;var p=WorldStage.BattlePoint(side,depth,lane);
                var b=MakeButton(visitPage.transform,"Formation_"+side+"_"+depth+"_"+lane,p.x-55,p.y-28,110,72,"",()=>{Visit.Move(d,l);Refresh();});
                boardButtons[index]=b;var tileObject=FormationTilePrefab!=null?Instantiate(FormationTilePrefab,b.transform,false):new GameObject("TileShape",typeof(RectTransform),typeof(FormationTile));if(tileObject.transform.parent==null)tileObject.transform.SetParent(b.transform,false);Place(tileObject.GetComponent<RectTransform>(),0,0,110,72);var tile=tileObject.GetComponent<FormationTile>();tile.raycastTarget=false;tile.color=side==0?new Color(.42f,.63f,.64f,.24f):new Color(.65f,.40f,.35f,.24f);b.GetComponent<Image>().color=Color.clear;
                var floorCanvas=tileObject.AddComponent<Canvas>();floorCanvas.overrideSorting=true;floorCanvas.sortingOrder=2;
            }
            for(int i=0;i<2;i++){int id=i;foeButtons[i]=MakeButton(visitPage.transform,"EnemyTarget_"+i,0,0,150,150,"",()=>AttackVisit(id));foeButtons[i].GetComponent<Image>().color=Color.clear;foeTexts[i]=Label(foeButtons[i].transform,"",0,128,150,52,18,Cream,TextAnchor.MiddleCenter);}
            Box(visitPage.transform,"BottomBand",0,734,1920,346,Hex("171D1B"));
            Box(visitPage.transform,"SquadPanel",16,770,704,286,PanelColor);Box(visitPage.transform,"SquadTab",22,758,160,39,Cream);Label(visitPage.transform,"원정대",32,760,140,34,24,Ink);
            for(int i=0;i<2;i++){int id=i;visitCards[i]=MakeButton(visitPage.transform,"VisitMember_"+i,30+i*338,809,322,222,"",()=>{if(Visit.Phase==VisitPhase.Explore&&State.Squad[id].Health>0){searcher=id;if(duty==PartnerDuty.Light&&!Visit.CanLight(id))duty=PartnerDuty.Watch;}Refresh();});Portrait(visitCards[i].transform,i,14,45,85);visitCardTexts[i]=Label(visitCards[i].transform,"",111,17,202,192,21,Ink);}
            Box(visitPage.transform,"SurvivalPanel",734,770,549,286,PanelColor);Box(visitPage.transform,"SurvivalTab",742,758,167,39,Cream);Label(visitPage.transform,"생존 정보",752,761,148,34,24,Ink);
            Label(visitPage.transform,"조명",756,814,230,29,22,Cream);Box(visitPage.transform,"LightTrack",756,849,234,19,Ink);visitLightBar=Box(visitPage.transform,"LightFill",758,851,230,15,Gold);
            Label(visitPage.transform,"소음",756,885,230,29,22,Cream);Box(visitPage.transform,"NoiseTrack",756,919,234,19,Ink);visitNoiseBar=Box(visitPage.transform,"NoiseFill",758,921,230,15,Teal);
            visitResources=Label(visitPage.transform,"",1012,811,250,140,21,Cream);visitLog=Label(visitPage.transform,"",752,968,509,78,18,Muted);
            Box(visitPage.transform,"ActionPanel",1296,770,608,286,PanelColor);Box(visitPage.transform,"ActionTab",1305,757,173,39,Cream);Label(visitPage.transform,"탐험 / 조우",1314,761,157,34,24,Ink);
            searchControls=Group(visitPage.transform,"SearchControls");
            searchInfo=Label(searchControls.transform,"",1312,803,570,47,21,Cream);
            for(int i=0;i<3;i++){int id=i;paceButtons[i]=MakeButton(searchControls.transform,"SearchPace_"+i,1312+i*194,857,183,43,new[]{"빠른 수색","보통 수색","정밀 수색"}[i],()=>{pace=(SearchPace)id;Refresh();});dutyButtons[i]=MakeButton(searchControls.transform,"PartnerDuty_"+i,1312+i*194,910,183,43,new[]{"함께 수색","망보기","손전등 지원"}[i],()=>{duty=(PartnerDuty)id;Refresh();});}
            searchCommit=MakeButton(searchControls.transform,"CommitSearch",1312,969,376,62,"수색하기 →",SearchVisit);
            visitReturn=MakeButton(searchControls.transform,"ReturnFromVisit",1700,969,182,62,"귀환",ReturnVisit);
            combatControls=Group(visitPage.transform,"CombatControls");combatInfo=Label(combatControls.transform,"",1312,802,570,34,20,Cream);
            MakeButton(combatControls.transform,"VisitMelee",1312,846,278,85,"근접 공격\n전열 · 피해 2",()=>{ranged=false;Refresh();});MakeButton(combatControls.transform,"VisitShoot",1603,846,278,85,"사격\n탄약 1 · 피해 3",()=>{ranged=true;Refresh();}).GetComponent<Image>().color=Hex("BD8875");
            MakeButton(combatControls.transform,"VisitGuard",1312,944,278,85,"방어\n받는 피해 감소",()=>{Visit.Guard();Refresh();}).GetComponent<Image>().color=Hex("9BAA87");MakeButton(combatControls.transform,"VisitFlee",1603,944,278,85,"도주\n성공 확률 65%",()=>{Visit.Flee();Refresh();}).GetComponent<Image>().color=Hex("B8B6A9");
            meetingControls=Group(visitPage.transform,"EncounterPrompt");Label(meetingControls.transform,"감염자 둘이 다가온다.\n맞서 싸울까, 숨어 기다릴까?",1312,815,563,102,24,Cream);
            MakeButton(meetingControls.transform,"FightEncounter",1312,948,278,81,"교전",FightVisit);MakeButton(meetingControls.transform,"AvoidEncounter",1603,948,278,81,"회피 · 5분 / 70%",()=>{Visit.Avoid();Refresh();});
            resultControls=Group(visitPage.transform,"EncounterResult");resultText=Label(resultControls.transform,"",1312,815,563,100,24,Cream);visitResume=MakeButton(resultControls.transform,"ResumeExploration",1312,948,569,81,"탐험으로 돌아가기",ResumeVisit);
            var debug=Group(visitPage.transform,"PrototypeTools");previewEncounter=MakeButton(debug.transform,"PreviewEncounter",22,686,234,37,"전투 시연",EncounterPreview);visitWidth=MakeButton(debug.transform,"ToggleBoardSize",270,686,270,37,"전투판 3×3 / 4×3",()=>{Visit.ChangeLanes(Visit.Lanes==3?4:3);Refresh();});
            Label(debug.transform,"시연용",552,692,140,32,18,Muted);
        }
        void RefreshVisit()
        {
            var v=Visit;bool combat=v.Phase==VisitPhase.Combat;bool explore=v.Phase==VisitPhase.Explore;
            if(explore&&State.Squad[searcher].Health<=0){searcher=State.Squad.FindIndex(p=>p.Health>0);duty=PartnerDuty.Watch;}
            World.ShowVisit(Campaign,v);visitClock.text="DAY "+Campaign.Day+"\n"+(18+v.Minutes/60).ToString("00")+":"+(v.Minutes%60).ToString("00")+" · "+(combat?"전투 "+v.Round+"턴":"탐험");
            visitLightBar.rectTransform.sizeDelta=new Vector2(230*Mathf.Max(30,100-v.Minutes/2)/100f,15);visitNoiseBar.rectTransform.sizeDelta=new Vector2(230*Mathf.Clamp01(v.Noise/12f),15);
            visitResources.text="보급품 "+v.Loot+"  /  탄약 "+v.Ammo+"\n배터리 "+v.Battery+"%\n조우 위험 "+v.Danger+"%\n망보기로 감소";
            visitLog.text=v.Message;searchControls.SetActive(explore);combatControls.SetActive(combat);meetingControls.SetActive(v.Phase==VisitPhase.Encounter);resultControls.SetActive(v.Phase==VisitPhase.Result);
            for(int i=0;i<3;i++){sites[i].gameObject.SetActive(explore);siteTexts[i].text=v.Places[i]+" · "+v.Progress[i]+"%";sites[i].interactable=v.Progress[i]<100;sites[i].GetComponent<Image>().color=i==chosenSite?Gold:Cream;paceButtons[i].GetComponent<Image>().color=i==(int)pace?Gold:Cream;dutyButtons[i].GetComponent<Image>().color=i==(int)duty?Gold:Cream;}
            dutyButtons[2].interactable=v.CanLight(searcher);searchInfo.text=v.Places[chosenSite]+" · 예상 "+v.Duration(pace,searcher,duty)+"분";searchCommit.interactable=v.Progress[chosenSite]<100;
            for(int i=0;i<2;i++){var p=State.Squad[i];visitCards[i].GetComponent<Image>().color=(combat?v.Actor==i:searcher==i)?Gold:Cream;visitCardTexts[i].text=p.Name+" / "+p.Role+"\n체력 "+p.Health+" / "+p.MaxHealth+"\n\n"+(p.Health==0?"전투 불능":combat?(v.Actor==i?"현재 차례":"대기"):searcher==i?"수색 담당":"지원 담당")+(i==v.FlashlightCarrier?"\n손전등 소지":"");}
            for(int i=0;i<2;i++){visitCards[i].transform.Find("SharedSilhouette").GetComponent<Image>().sprite=World.BodyFor(Campaign.Chosen[i]);visitCards[i].transform.Find("TintableBase").GetComponent<Image>().color=World.ColorFor(Campaign.Chosen[i]);}
            for(int side=0;side<2;side++)for(int lane=0;lane<4;lane++)for(int depth=0;depth<3;depth++){var b=boardButtons[side*12+lane*3+depth];b.gameObject.SetActive(combat&&lane<v.Lanes);b.interactable=combat&&side==0&&!v.Moved;}
            for(int i=0;i<2;i++){bool alive=combat&&v.Foes.Count>i&&v.Foes[i].Health>0;foeButtons[i].gameObject.SetActive(alive);if(alive){var e=v.Foes[i];var p=WorldStage.BattlePoint(1,e.X,e.Y);Place(foeButtons[i].GetComponent<RectTransform>(),p.x-75,p.y-145,150,183);foeTexts[i].text="체력 "+e.Health+" · "+v.HitChance(i,ranged)+"%";}}
            if(combat)combatInfo.text=State.Squad[v.Actor].Name+" · "+(ranged?"사격":"근접")+" 선택 / 적을 클릭";
            resultText.text=State.Squad.All(p=>p.Health<=0)?"원정 실패":"전투 종료 · 수색 진척 유지";
            visitResume.GetComponentInChildren<Text>().text=State.Squad.All(p=>p.Health<=0)?"결과 확인":"탐험으로 돌아가기";
            visitWidth.interactable=explore;previewEncounter.interactable=explore;visitWidth.GetComponentInChildren<Text>().text="전투판 "+v.Lanes+"×3 · 변경";
        }
    }
}
