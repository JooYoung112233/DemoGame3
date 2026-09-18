using Live49.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Live49.UI
{
    public partial class ActivityPanel
    {
        JourneyState _cookState;
        StoveDial _dial;KitchenVisual _stoveVisual,_soupVisual;TMP_Text _flameLabel;
        float _kitchenClock;bool _kitchenSaveFailed;
        public StoveDial Dial=>_dial;
        public bool KitchenSaveFailed=>_kitchenSaveFailed;
        void BuildKitchen()
        {
            _title.text="냄비 한 끼";_subtitle.text="끓기 시작하면 불을 낮추고, 잔잔하게 익혀요.";
            if(_cook==null||!ReferenceEquals(_cookState,_state)||_cook.Stage==CookingSession.Phase.Done){_cook=new CookingSession(_state);_cookState=_state;}
            _kitchenSaveFailed=false;_kitchenClock=0;
            var stage=PanelUI.Box(_body,"KitchenStage",0,0,1120,710,new Color32(49,43,32,248));
            for(int i=0;i<8;i++)PanelUI.Box(stage,"WoodLine_"+i,0,95+i*74,1120,1,new Color32(91,73,46,100));
            _stoveVisual=PanelUI.Rect(stage,"StoveSurface",0,0,1120,710).gameObject.AddComponent<KitchenVisual>();_stoveVisual.raycastTarget=false;
            _phase=PanelUI.Text(stage,_font,"CookingPhase","",32,19,1056,50,29,PanelUI.Gold);
            _pot=PanelUI.Rect(stage,"Pot",230,74,660,483);
            var pot=_pot.gameObject.AddComponent<Image>();pot.sprite=Resources.Load<Sprite>("Live49/Kitchen/soup-pot");pot.preserveAspect=true;pot.raycastTarget=false;
            _soupVisual=PanelUI.Rect(_pot,"SoupSteam",0,0,660,483).gameObject.AddComponent<KitchenVisual>();_soupVisual.OverPot=true;_soupVisual.raycastTarget=false;
            _dial=PanelUI.Rect(stage,"StoveDial",460,514,200,200).gameObject.AddComponent<StoveDial>();_dial.Changed=SelectKitchenHeat;
            _flameLabel=PanelUI.Text(stage,_font,"FlameLabel","",85,586,305,47,32);
            PanelUI.Text(stage,_font,"DialHelp","다이얼을 끌어 불을 조절해요.\n키보드 1 · 2 · 3으로도 바꿀 수 있어요.",735,584,314,94,22,PanelUI.Muted);
            PanelUI.Text(stage,_font,"DialLow","약",418,552,50,35,20,PanelUI.Muted);PanelUI.Text(stage,_font,"DialHigh","강",665,552,50,35,20,PanelUI.Muted);
            var card=PanelUI.Box(_body,"RecipeCard",1160,0,580,710,PanelUI.Ink);
            PanelUI.Text(card,_font,"RecipeHeading","오늘의 한 끼",38,22,504,38,22,PanelUI.Gold);
            _heatText=PanelUI.Text(card,_font,"HeatText","",36,80,508,65,32);
            _status=PanelUI.Text(card,_font,"CookingStatus","",38,169,504,114,25,PanelUI.Muted);
            var track=PanelUI.Box(card,"HeatTrack",38,315,504,8,new Color32(63,65,53,255));
            PanelUI.Box(track,"IdealRange",504*62/120f,-5,504*16/120f,18,new Color32(115,146,89,220));
            _heatFill=PanelUI.Box(track,"Fill",0,0,0,8,PanelUI.Gold);
            PanelUI.Text(card,_font,"HeatLegendLow","미지근함",38,336,126,32,18,PanelUI.Muted);
            PanelUI.Text(card,_font,"HeatLegendIdeal","잔잔함",268,336,128,32,18,PanelUI.Muted).alignment=TextAlignmentOptions.Center;
            PanelUI.Text(card,_font,"HeatLegendHigh","거센 끓음",410,336,132,32,18,PanelUI.Muted).alignment=TextAlignmentOptions.MidlineRight;
            _resource=PanelUI.Text(card,_font,"RecipeCosts","",38,397,504,74,24);
            _start=PanelUI.Button(card,_font,"StartCooking","조리 시작 · 25분",38,495,504,62,()=>{if(_cook.Start()){SaveKitchen();RefreshKitchen();}});
            _pauseCook=PanelUI.Button(card,_font,"PauseCooking","잠시 멈춤",38,577,504,62,()=>{if(_kitchenSaveFailed){SaveKitchen();}else if(_cook.Active){_cook.Paused=!_cook.Paused;SaveKitchen();}RefreshKitchen();});
            _result=PanelUI.Text(card,_font,"CookingResult","",38,655,504,42,21,PanelUI.Muted);
            RefreshKitchen();
        }
        void SelectKitchenHeat(int level){if(_kitchenSaveFailed||_closing)return;int before=_cook.Level;_cook.Select(level);if(before!=_cook.Level)SaveKitchen();RefreshKitchen();}
        void SaveKitchen(bool notify=false)
        {
            _kitchenSaveFailed=!SaveSystem.AutoSave(_state,out _,notify);
            if(_kitchenSaveFailed&&_cook.Active)_cook.Paused=true;
        }
        void RefreshKitchen()
        {
            if(_kind!=Kind.Kitchen||!IsOpen)return;
            bool done=_cook.Stage==CookingSession.Phase.Done,ready=_cook.Stage==CookingSession.Phase.Ready;
            _resource.text=(_cook.Active||done?"남은 재료":"사용할 재료  ·  가스 1 + 식재료 1")+"\n가스 "+_state.Count("gas")+"  ·  식재료 "+_state.Count("ingredients");
            _start.interactable=ready&&_cook.BlockReason()==null&&!_kitchenSaveFailed;
            _start.GetComponentInChildren<TMP_Text>().text=done?"조리 완료":_cook.Active?"재료를 넣었어요":"조리 시작 · 25분";
            _phase.text=done?"그릇에 담을 준비가 됐어요":_cook.Paused?"잠시 멈춘 주방":_cook.Stage==CookingSession.Phase.Warming?"01  냄비 데우기":_cook.Stage==CookingSession.Phase.Simmering?"02  잔잔하게 익히기":"가스레인지 · 조리 준비";
            _heatText.text=done?"따뜻한 한 끼 완성":ready?"불을 켜기 전에":_cook.Paused?"이어서 만들 수 있어요":_cook.Heat>86?"거세게 끓고 있어요":_cook.Heat>=78?"끓기 시작했어요":_cook.Heat>=62?"잔잔한 기포가 올라와요":"서서히 데워지고 있어요";
            _heatFill.sizeDelta=new Vector2(504*Mathf.Clamp01(_cook.Heat/120),8);
            _status.text=_kitchenSaveFailed?"저장하지 못해 진행을 멈췄어요. 아래에서 저장을 다시 시도해주세요.":done?(_cook.Great?"잔잔하게 익혀 좋은 식사가 됐어요.":"남은 익힘을 마무리해 한 끼를 완성했어요."):ready?(_cook.BlockReason()??"시작하면 가스와 식재료를 한 번 사용해요. 완성한 식사는 가방에 담아요."):_cook.Paused?"다이얼과 냄비가 멈춰 있어요. 준비되면 ‘이어서 조리’를 선택해요.":_cook.Stage==CookingSession.Phase.Warming?"중불이나 강불로 데워요. 끓기 시작하면 다이얼을 약불 쪽으로 돌려요.":(_cook.Heat>78?"불을 낮춰 끓음을 가라앉혀요.":_cook.Heat<62?"불을 조금 올려 열기를 유지해요.":"지금의 잔잔한 끓음을 유지해요.")+"\n익히기 · "+Mathf.CeilToInt(10-_cook.Simmer)+"초 남음";
            _dial.Display(ready||done?0:_cook.Level,_cook.Active&&!_cook.Paused&&!_kitchenSaveFailed);
            _flameLabel.text=ready||done?"불 꺼짐":_cook.Paused?"조리 멈춤":new[]{"","약불","중불","강불"}[_cook.Level];
            _pauseCook.interactable=_cook.Active||_kitchenSaveFailed;_pauseCook.GetComponentInChildren<TMP_Text>().text=_kitchenSaveFailed?"저장 다시 시도":_cook.Paused?"이어서 조리":"잠시 멈춤";
            _result.text=done?"따뜻한 식사 1개 · 가방에 담았어요.":"닫으면 멈추고, 다시 열면 이어서 조리해요.";
            _stoveVisual.Show(_cook.Heat,ready||done?0:_cook.Level,_cook.Active,_kitchenClock);
            _soupVisual.Show(_cook.Heat,_cook.Level,_cook.Active||done,_kitchenClock);
        }
    }
}
