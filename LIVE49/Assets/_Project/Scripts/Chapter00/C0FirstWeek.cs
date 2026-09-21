using System;
using System.Collections;
using Live49.Core;
using Live49.Dialogue;
using Live49.UI;
using UnityEngine;
using UnityEngine.UI;
namespace Live49.Chapter00
{
    public partial class C0OpeningDirector
    {
        RectTransform _weekDisplay,_weekProp;
        void RenderWeekAction(int index)
        {
            string id=FirstWeekStory.Action(_state);
            if(id!=null)ActionButton("WeekAction",FirstWeekStory.Label(id),index,()=>GameHud.Instance.ShowInteraction(WeekScript.Find(id).title,"준비가 되면 이야기를 이어가요.\n나중에 돌아와도 여기서 계속할 수 있어요.",new[]{id=="accept"?"부탁 맡기":"이어서 보기"},_=>StartCoroutine(PlayWeekEvent(id)),"여행의 기록"));
            else if(!RegionExploration.Outside(_state)&&_state.Has(RegionTravel.Drawing))
                ActionButton("WeekMemories","그림과 사진 다시 보기",index,()=>GameHud.Instance.ShowInteraction("남겨 둔 장면","그림과 사진을 다시 펼쳐 봐요.",_state.Has(RegionTravel.Photo)?new[]{"첫 그림","첫 사진과 앨범"}:new[]{"첫 그림"},i=>StartCoroutine(PlayWeekEvent(i==0?"draw":"album",false,true)),"여행의 기록"));
        }
        IEnumerator PlayWeekEvent(string id,bool resume=false,bool replay=false)
        {
            if(_interactionBusy||(!resume&&!replay&&!FirstWeekStory.Begin(_state,id)))yield break;
            _interactionBusy=true;var hud=GameHud.Instance;hud.SetNarrativeMode();
            dialogue.AddPortrait("sejin",Resources.Load<Sprite>("Live49/Stages/week-sejin-portrait"));
            var e=WeekScript.Find(id);int from=replay?0:_state.weekLine;
            if(!replay&&!resume)SaveWeekCheckpoint();
            string art=null;
            for(int i=from;i<e.lines.Length;i++)
            {
                var source=e.lines[i];
                if(art!=source.art)
                {
                    yield return dialogue.FadePanel(false,.25f);yield return Tween.Fade(screenFade,1,.4f);
                    yield return dialogue.SetPortraits(Array.Empty<string>(),null,0);
                    SetWeekStage(source.art);art=source.art;yield return Tween.Wait(.3f);yield return Tween.Fade(screenFade,0,.65f);
                }
                var visible=string.IsNullOrEmpty(source.speaker)||source.art=="week-album"||source.art=="week-first-photo"?Array.Empty<string>():id=="meet"||id=="accept"||id=="handover"?new[]{"suhyeok","sejin"}:RegionExploration.Outside(_state)?new[]{"suhyeok"}:new[]{"suhyeok","soi"};
                yield return PlayLine(new DialogueLine{id="week_"+id+"_"+i,kind=string.IsNullOrEmpty(source.speaker)?"narration":"dialogue",speakerId=source.speaker,text=source.text,portraits=visible},new[]{_journeyFX});
                if(!replay){FirstWeekStory.ConfirmLine(_state);SaveWeekCheckpoint();}
            }
            if(!replay)FirstWeekStory.Finish(_state);
            yield return dialogue.FadePanel(false,.3f);yield return Tween.Fade(screenFade,1,.35f);
            yield return dialogue.SetPortraits(Array.Empty<string>(),null,0);
            if(AwayFromCamper)SetRegionView(_state.exploringPlace);else SetJourneyImage(_state.inStore?"store":"journal");
            yield return Tween.Fade(screenFade,0,.6f);yield return EnterExploration(!replay);
        }
        void SaveWeekCheckpoint(){SaveSystem.AutoSave(_state,out _,false);}
        void SetWeekStage(string art)
        {
            if(art!="week-first-photo"&&art!="week-album"){SetJourneyImage(art);return;}
            SetJourneyImage("region-L6");
            if(_weekDisplay==null)
            {
                _weekDisplay=PanelUI.Rect(present.transform,"WeekKeepsake",0,0,1920,1080);
                _weekDisplay.anchorMin=_weekDisplay.anchorMax=_weekDisplay.pivot=Vector2.one*.5f;_weekDisplay.anchoredPosition=Vector2.zero;
            }
            PanelUI.Clear(_weekDisplay);_weekDisplay.gameObject.SetActive(true);_weekDisplay.SetAsLastSibling();
            PanelUI.Box(_weekDisplay,"Shade",0,0,1920,1080,new Color(0.05f,.07f,.06f,.78f));
            if(art=="week-album")
            {
                AddWeekImage(_weekDisplay,"Album","week-album",460,0,1000,750);
                PanelUI.Text(_weekDisplay,Font,"AlbumTitle","우리의 첫 여행 사진",570,228,360,45,25,PanelUI.Ink);
                PanelUI.Text(_weekDisplay,Font,"AlbumCaption","강변에서 남긴 풍경\n다음 길에도 함께 가져갈 기록",570,302,340,110,22,PanelUI.Ink);
                PanelUI.Box(_weekDisplay,"PrintBorder",1009,224,351,271,PanelUI.Cream);
                AddWeekImage(_weekDisplay,"Photo","week-first-photo",1021,236,327,247);
            }
            else{PanelUI.Box(_weekDisplay,"PrintBorder",497,58,926,690,PanelUI.Cream);AddWeekImage(_weekDisplay,"Photo","week-first-photo",515,76,890,654);}
        }
        void AddWeekImage(Transform parent,string name,string resource,float x,float y,float w,float h)
        {var image=PanelUI.Rect(parent,name,x,y,w,h).gameObject.AddComponent<Image>();image.sprite=Resources.Load<Sprite>("Live49/Stages/"+resource);image.preserveAspect=true;image.raycastTarget=false;}
        void ShowWeekProp(string scene)
        {
            if(scene!="region-L3"||FirstWeekStory.Pencils(_state))return;
            if(_weekProp==null)
            {
                _weekProp=PanelUI.Rect(present.transform,"WeekPencils",0,0,1920,1080);
                _weekProp.anchorMin=_weekProp.anchorMax=_weekProp.pivot=Vector2.one*.5f;_weekProp.anchoredPosition=Vector2.zero;
                AddWeekImage(_weekProp,"Pencils","week-pencils",1390,488,103,36);
            }
            _weekProp.gameObject.SetActive(true);_weekProp.SetAsLastSibling();
        }
    }
}
