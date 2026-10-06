using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Demo5.FrontEnd
{
    public sealed class SaveSlotPanel:MonoBehaviour
    {
        public Text Heading,Hint,ActionLabel,ConfirmBody;
        public Button[] Slots;
        public Text[] SlotHeadings,SlotDetails;
        public Button Back,Action,Confirm,Cancel;
        public GameObject Review;
        public CanvasGroup Workspace;
        public event System.Action Closed;
        public event System.Action<CampaignSaveData> LoadRequested;
        SettlementController catalog,source;
        SaveSlotInfo[] infos;
        bool initialized,saving;
        int selected=-1;
        public int SelectedSlot=>selected;
        public bool IsOpen=>gameObject.activeSelf;
        public void Initialize()
        {
            if(initialized)return;initialized=true;gameObject.SetActive(false);Review.SetActive(false);
            for(int i=0;i<Slots.Length;i++){int slot=i;Slots[i].onClick.AddListener(()=>Select(slot));}
            Back.onClick.AddListener(Close);Action.onClick.AddListener(Ask);Cancel.onClick.AddListener(CancelReview);Confirm.onClick.AddListener(Execute);
        }
        public void Open(SettlementController dataCatalog,SettlementController saveSource=null)
        {
            Initialize();catalog=dataCatalog;source=saveSource;saving=source!=null;selected=-1;
            Heading.text=saving?"생활 저장":"생활 불러오기";ActionLabel.text=saving?"선택한 칸에 저장":"선택한 생활 불러오기";
            Review.SetActive(false);Workspace.interactable=Workspace.blocksRaycasts=true;gameObject.SetActive(true);Refresh();
            EventSystem.current?.SetSelectedGameObject(Slots[0].gameObject);
        }
        void Refresh()
        {
            infos=Enumerable.Range(0,CampaignSaveStore.SlotCount).Select(i=>CampaignSaveStore.Read(i,catalog)).ToArray();
            for(int i=0;i<Slots.Length;i++)
            {
                var info=infos[i];SlotHeadings[i].text="기록 "+(i+1)+(info.CanLoad?" · DAY "+info.Data.Day:info.Exists?" · 확인 필요":" · 빈 기록");
                SlotDetails[i].text=info.Error??(!info.Exists?"아직 저장한 생활이 없습니다.":"불러올 수 없습니다.");
                if(info.CanLoad){var d=info.Data;string place=new Demo5.NightRun.CampaignState().Sites[Array.IndexOf(CampaignPersistence.HomeIds,d.HomeId)].Name;SlotDetails[i].text=place+" · "+(d.Minute/60).ToString("00")+":"+(d.Minute%60).ToString("00")+" · "+d.Members.Length+"명\n"+string.Join(" · ",d.Members.Select(p=>p.Name))+"\n"+DateTime.Parse(d.SavedUtc).ToLocalTime().ToString("yyyy.MM.dd HH:mm")+" 저장";}
                Slots[i].GetComponent<Image>().color=i==selected?new Color(1,.81f,.46f):Color.white;
            }
            Action.interactable=selected>=0 && (saving || infos[selected].CanLoad);
            Hint.text=saving?"정착지의 물자·작업·탐험 기록을 함께 저장합니다.":source==null?"이어갈 기록을 선택하세요.":"불러오기 전 현재 생활을 저장해 주세요.";
        }
        public void Select(int slot){if(!IsOpen || Review.activeSelf || slot<0 || slot>=Slots.Length)return;selected=slot;Refresh();}
        void Ask()
        {
            if(!IsOpen || Review.activeSelf || !Action.interactable)return;
            // Re-read the selected file so stale list data never controls an overwrite or load.
            Refresh();if(!Action.interactable)return;
            if(saving && !infos[selected].Exists && infos[selected].Error==null){Execute();return;}
            ConfirmBody.text=saving?"기록 "+(selected+1)+"을 현재 생활로 덮어쓸까요?\n이 칸의 이전 기록이 교체됩니다.":"기록 "+(selected+1)+"의 생활을 불러올까요?\n저장하지 않은 진행은 사라집니다.";
            Review.SetActive(true);Workspace.interactable=Workspace.blocksRaycasts=false;EventSystem.current?.SetSelectedGameObject(Cancel.gameObject);
        }
        void Execute()
        {
            if(!IsOpen || selected<0)return;
            if(saving)
            {
                try
                {
                    var data=CampaignPersistence.Capture(source);
                    if(!CampaignSaveStore.Write(selected,data,catalog,out string error)){CancelReview();Hint.text=error;return;}
                    CancelReview();Refresh();Hint.text="기록 "+(selected+1)+"에 저장했습니다.";
                }
                catch(Exception e){CancelReview();Hint.text=e.Message;}
            }
            else
            {
                var info=CampaignSaveStore.Read(selected,catalog);
                if(!info.CanLoad){CancelReview();Refresh();Hint.text=info.Error??"저장 파일이 없습니다.";return;}
                CancelReview();LoadRequested?.Invoke(info.Data);
            }
        }
        public void CancelReview(){Review.SetActive(false);Workspace.interactable=Workspace.blocksRaycasts=true;EventSystem.current?.SetSelectedGameObject(Action.gameObject);}
        public void Close(){if(!IsOpen)return;Review.SetActive(false);gameObject.SetActive(false);Closed?.Invoke();}
        void Update(){if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame){if(Review.activeSelf)CancelReview();else Close();}}
    }
}
