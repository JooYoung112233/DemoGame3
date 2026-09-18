using System;
using Live49.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Live49.UI
{
    public sealed class TutorialPanel : MonoBehaviour
    {
        TMP_FontAsset _font;RectTransform _body;Action _close;int _selected;
        public bool IsOpen=>gameObject.activeSelf;
        public static TutorialPanel Create(Transform parent,TMP_FontAsset font,Action close)
        {
            var r=PanelUI.Box(parent,"TutorialPanel",0,0,1920,1080,new Color32(19,22,20,253),true);
            var p=r.gameObject.AddComponent<TutorialPanel>();p._font=font;p._close=close;
            PanelUI.Text(r,font,"Eyebrow","LIVE49 / 플레이 안내",90,64,1400,32,20,PanelUI.Gold);
            PanelUI.Text(r,font,"GuideTitle","첫 여정, 한 걸음씩",85,112,1400,70,48);
            PanelUI.Text(r,font,"Subtitle","실제로 마친 행동을 기준으로 안내해요. 읽는 것만으로 진행이 바뀌지는 않아요.",90,192,1450,44,24,PanelUI.Muted);
            PanelUI.Button(r,font,"CloseTutorial","돌아가기",1630,112,200,64,()=>p.Close());
            p._body=PanelUI.Rect(r,"GuideBody",90,274,1740,690);r.gameObject.SetActive(false);return p;
        }
        public void Open()
        {
            _selected=Math.Min(5,JourneyTutorial.Current(SaveSystem.Current).Chapter);
            gameObject.SetActive(true);transform.SetAsLastSibling();Refresh();
            EventSystem.current?.SetSelectedGameObject(transform.Find("CloseTutorial").gameObject);
        }
        void Refresh()
        {
            PanelUI.Clear(_body);var state=SaveSystem.Current;var step=JourneyTutorial.Current(state);var completed=JourneyTutorial.Completed(state);
            for(int i=0;i<7;i++)
            {
                int tab=i;string label=i==6?"생활 · 조작 · 저장":(i+1).ToString("00")+"  "+JourneyTutorial.Chapters[i]+(completed[i]?"  · 완료":step.Chapter==i?"  · 진행 중":"");
                var b=PanelUI.Button(_body,_font,"GuideChapter_"+i,label,0,i*88,570,76,()=>{_selected=tab;Refresh();});
                b.GetComponentInChildren<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
                b.GetComponent<Image>().color=i==_selected?new Color32(101,82,48,255):new Color32(38,39,32,255);
            }
            var now=PanelUI.Box(_body,"CurrentStep",608,0,1132,312,PanelUI.Ink);
            PanelUI.Text(now,_font,"NowEyebrow","지금 할 일",32,20,1068,34,21,PanelUI.Gold);
            PanelUI.Text(now,_font,"NowTitle",step.Title,30,64,1072,66,34);
            PanelUI.Text(now,_font,"NowDetail",step.Detail,32,144,1068,146,25,PanelUI.Cream).alignment=TextAlignmentOptions.TopLeft;
            var help=PanelUI.Box(_body,"GuideDetail",608,334,1132,348,PanelUI.Ink);
            PanelUI.Text(help,_font,"HelpTitle",_selected==6?"필요할 때 꺼내 보는 안내":JourneyTutorial.Chapters[_selected],32,20,1068,45,28,PanelUI.Gold);
            string text=_selected==6?"ESC  ·  메뉴 / 이전 화면으로 돌아가기\n설정 → 저장·불러오기  ·  수동 슬롯과 자동 저장 확인\n요리  ·  설비를 복구한 뒤 사용해요. 닫으면 조리·가열이 멈춰요.\n연료  ·  차량 이동과 발전에 함께 써요. 도보 이동에는 들지 않아요.\n하루 마치기  ·  캠핑카에서 선택해요. 저녁 기록은 선택이에요.":JourneyTutorial.Basics[_selected]+"\n\n"+(completed[_selected]?"이 단계는 이미 마쳤어요. 다시 읽어도 물자나 보상이 지급되지는 않아요.":_selected>step.Chapter?"앞선 안내를 따라가면 자연스럽게 이어져요. 모든 기능을 지금 한꺼번에 할 필요는 없어요.":"막히면 위 ‘지금 할 일’에서 현재 위치에 맞는 행동을 확인해요.");
            PanelUI.Text(help,_font,"HelpText",text,32,82,1068,246,24,PanelUI.Muted).alignment=TextAlignmentOptions.TopLeft;
        }
        public void Close(){if(!IsOpen)return;gameObject.SetActive(false);FreshInput.DiscardPending();_close();}
    }
}
