using System;
using System.Linq;
using Live49.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace Live49.UI
{
    public sealed class SaveLoadPanel : MonoBehaviour
    {
        TMP_FontAsset _font;
        RectTransform _content;
        TMP_Text _heading,_subtitle;
        Action _closed,_loaded;
        Button _close,_saveTab,_loadTab;
        bool _saving,_allowSave;
        int _selected;
        string _confirmation,_message;
        public bool IsOpen=>gameObject.activeSelf;
        public static SaveLoadPanel Create(Transform parent,TMP_FontAsset font,Action close,Action loaded)
        {
            var root=PanelUI.Box(parent,"SaveLoadPanel",0,0,1920,1080,new Color(.035f,.04f,.035f,.98f),true);
            // Works under the centered game viewport and the title Canvas alike.
            root.anchorMin=root.anchorMax=root.pivot=Vector2.one*.5f;root.anchoredPosition=Vector2.zero;
            var p=root.gameObject.AddComponent<SaveLoadPanel>();p._font=font;p._closed=close;p._loaded=loaded;
            PanelUI.Text(root,font,"SaveEyebrow","LIVE49  /  여정 보관",90,60,1100,36,20,PanelUI.Gold);
            p._heading=PanelUI.Text(root,font,"SaveHeading","",86,106,1160,70,48);
            p._subtitle=PanelUI.Text(root,font,"SaveSubtitle","",90,183,1360,42,23,PanelUI.Muted);
            p._close=PanelUI.Button(root,font,"CloseSaveLoad","닫기",1680,105,150,64,p.Escape);
            p._saveTab=PanelUI.Button(root,font,"SaveMode","저장",90,251,274,62,()=>p.Mode(true));
            p._loadTab=PanelUI.Button(root,font,"LoadMode","불러오기",378,251,274,62,()=>p.Mode(false));
            p._content=PanelUI.Rect(root,"SaveContent",90,344,1740,650);root.gameObject.SetActive(false);return p;
        }
        public void Open(bool saving,bool allowSave)
        {
            _allowSave=allowSave;_saving=saving&&allowSave;_selected=_saving?1:(SaveSlots.Latest()?.Index??0);
            _confirmation=null;_message=null;gameObject.SetActive(true);transform.SetAsLastSibling();Refresh();FreshInput.DiscardPending();
        }
        void Mode(bool saving){if(saving&&!_allowSave)return;_saving=saving;_selected=saving&&_selected==0?1:_selected;_confirmation=null;_message=null;Refresh();}
        public void Escape()
        {
            if(_confirmation!=null){_confirmation=null;Refresh();return;}
            gameObject.SetActive(false);FreshInput.DiscardPending();_closed();
        }
        void Update(){if(Keyboard.current?.escapeKey.wasPressedThisFrame==true&&GameHud.Instance==null)Escape();}
        void Refresh()
        {
            PanelUI.Clear(_content);
            _heading.text=_saving?"지금의 여정을 남기기":"남겨 둔 여정으로";
            _subtitle.text=_saving?(SaveSystem.CanSave?"자동 저장과 별도로, 원하는 순간을 보관해요.":"대화나 화면 이동을 마친 뒤 저장할 수 있어요."):"저장한 날짜와 장소를 확인하고 이어가요.";
            _saveTab.gameObject.SetActive(_allowSave);
            _saveTab.GetComponent<Image>().color=new Color(PanelUI.Gold.r,PanelUI.Gold.g,PanelUI.Gold.b,_saving?.45f:.12f);
            _loadTab.GetComponent<Image>().color=new Color(PanelUI.Gold.r,PanelUI.Gold.g,PanelUI.Gold.b,_saving?.12f:.45f);
            var slots=SaveSlots.All();
            foreach(var slot in slots)
            {
                var entry=slot;float y=slot.Index*153;
                var row=PanelUI.Button(_content,_font,"SaveSlot_"+slot.Index,"",0,y,630,136,()=>{_selected=entry.Index;_confirmation=null;_message=null;Refresh();});
                row.GetComponent<Image>().color=new Color(PanelUI.Gold.r,PanelUI.Gold.g,PanelUI.Gold.b,_selected==slot.Index?.25f:.07f);
                if(_selected==slot.Index)PanelUI.Box(row.transform,"SelectedRule",0,0,3,136,PanelUI.Gold);
                PanelUI.Text(row.transform,_font,"SlotName",slot.Name,26,13,568,38,26);
                string summary=slot.Readable?(slot.State.day==0?"출발 전":slot.State.day+"일 차")+"  ·  "+SaveSlots.Place(slot.State):slot.Recoverable?"복구 가능한 이전 기록이 있어요.":slot.Exists?"읽을 수 없는 저장 파일":"비어 있는 슬롯";
                PanelUI.Text(row.transform,_font,"SlotSummary",summary,26,55,568,35,21,PanelUI.Muted);
                PanelUI.Text(row.transform,_font,"SlotTime",slot.Readable?SaveSlots.Stamp(slot.State):slot.Index==0?"진행 중 자동으로 기록돼요.":"",26,96,568,25,17,PanelUI.Muted);
            }
            var selected=slots[_selected];var state=selected.State??selected.Backup;
            var detail=PanelUI.Box(_content,"SaveDetail",662,0,1078,595,PanelUI.Ink);
            string art=SaveSlots.Art(state);var sprite=string.IsNullOrEmpty(art)?null:Resources.Load<Sprite>("Live49/Stages/"+art);
            if(sprite!=null){var img=PanelUI.Rect(detail,"SaveIllustration",24,25,620,349).gameObject.AddComponent<Image>();img.sprite=sprite;img.preserveAspect=true;img.raycastTarget=false;}
            else{PanelUI.Box(detail,"EmptyFrame",24,25,620,349,new Color(1,1,1,.025f));PanelUI.Text(detail,_font,"EmptySlot",selected.Exists?"기록을 확인해주세요.":"아직 남겨 둔 여정이 없어요.",60,153,550,82,28,PanelUI.Muted);}
            PanelUI.Text(detail,_font,"SelectedSlotName",selected.Name,680,25,370,52,33,PanelUI.Gold);
            string info=state==null?(_saving?"이 슬롯에 현재 진행을 남겨요.\n다른 수동 슬롯은 유지돼요.":"저장된 슬롯을 선택해주세요."):(state.day==0?"챕터 0 · 출발 전":"챕터 1 · "+state.day+"일 차")+"\n"+SaveSlots.Place(state)+"\n\n"+SaveSlots.Progress(state)+"\n\n"+SaveSlots.Stamp(state);
            PanelUI.Text(detail,_font,"SavedProgress",info,680,90,370,298,23,PanelUI.Muted);
            string prompt=_confirmation=="save"?"이 슬롯의 기록을 현재 진행으로 덮어쓸까요?":_confirmation=="load"?"현재 진행을 떠나 이 기록을 불러올까요?":_confirmation=="backup"?"이전 복구본을 불러올까요? 원본 파일은 유지해요.":_saving&&_selected==0?"자동 저장은 덮어쓸 수 없어요. 수동 슬롯을 선택해요.":_saving?"선택한 슬롯에 현재 진행을 저장합니다.":selected.Recoverable?"원본을 읽지 못했어요. 이전 복구본을 확인할 수 있어요.":"불러오면 저장 이후의 진행은 되돌아갑니다.";
            PanelUI.Text(detail,_font,"SlotActionPrompt",prompt,28,394,1022,68,25);
            if(_confirmation!=null)
            {
                var cancel=PanelUI.Button(detail,_font,"CancelSlotAction","돌아가기",28,489,488,70,()=>{_confirmation=null;Refresh();});
                PanelUI.Button(detail,_font,"ConfirmSlotAction",_confirmation=="save"?"덮어쓰기":_confirmation=="backup"?"복구본 불러오기":"불러오기",538,489,512,70,()=>Execute(selected));
                EventSystem.current?.SetSelectedGameObject(cancel.gameObject);
            }
            else
            {
                bool available=_saving?_selected>0&&SaveSystem.CanSave:selected.Readable||selected.Recoverable;
                var action=PanelUI.Button(detail,_font,"SlotPrimaryAction",_saving?"이 슬롯에 저장":selected.Recoverable?"복구본 확인":"이 기록 불러오기",28,489,1022,70,()=>
                {
                    if(_saving&&!selected.Exists&&!selected.Recoverable){Execute(selected);return;}
                    _confirmation=_saving?"save":selected.Recoverable?"backup":"load";Refresh();
                });action.interactable=available;
                EventSystem.current?.SetSelectedGameObject(_close.gameObject);
            }
            PanelUI.Text(_content,_font,"SaveResult",_message??"",662,609,1078,38,22,PanelUI.Gold);
        }
        void Execute(SaveSlotInfo slot)
        {
            if(_saving)
            {
                _message=SaveSlots.Save(slot.Index,out var error)?slot.Name+"에 저장했어요.":error;_confirmation=null;Refresh();return;
            }
            string path=slot.Recoverable?slot.Path+".bak":slot.Path;
            if(!SaveSystem.QueueLoad(path,out var loadError)){_message=loadError;_confirmation=null;Refresh();return;}
            gameObject.SetActive(false);FreshInput.DiscardPending();_loaded();
        }
    }
}
