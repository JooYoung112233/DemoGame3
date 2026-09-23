using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Demo5.NightRun;

namespace Demo5.FrontEnd
{
    public sealed class SettlementGameMenu:MonoBehaviour
    {
        public GameObject View,Page,Review;
        public Button OpenButton,Resume,Save,Load,Settings,Title,Confirm,Cancel;
        public Text Hint;
        public GameSettingsDialog SettingsDialog;
        public SaveSlotPanel SavePanel;
        SettlementController owner;
        bool leaving;
        int closedFrame=-1;
        public bool IsOpen=>View.activeSelf;
        public void Initialize(SettlementController c)
        {
            owner=c;View.SetActive(false);Review.SetActive(false);GameSettings.ApplySaved();SettingsDialog.Initialize();SavePanel.Initialize();
            OpenButton.onClick.AddListener(Open);Resume.onClick.AddListener(Close);
            Save.onClick.AddListener(()=>OpenSlots(true));Load.onClick.AddListener(()=>OpenSlots(false));
            Settings.onClick.AddListener(()=>{Page.SetActive(false);View.transform.Find("Dim").gameObject.SetActive(false);SettingsDialog.Open();});
            SettingsDialog.Closed+=RestorePage;SavePanel.Closed+=RestorePage;SavePanel.LoadRequested+=LoadData;
            Title.onClick.AddListener(()=>{Page.SetActive(false);Review.SetActive(true);EventSystem.current?.SetSelectedGameObject(Cancel.gameObject);});
            Cancel.onClick.AddListener(()=>{Review.SetActive(false);RestorePage();});
            Confirm.onClick.AddListener(()=>{if(leaving)return;leaving=true;PartySelectionSession.Clear();SceneManager.LoadScene("StartMenu");});
        }
        public void Open()
        {
            if(IsOpen || leaving || owner.Campaign==null || owner.Campaign.Stage!=JourneyStage.Settlement || !owner.Main.interactable || !owner.Main.gameObject.activeInHierarchy)return;
            owner.Main.interactable=owner.Main.blocksRaycasts=false;View.SetActive(true);Page.SetActive(true);Review.SetActive(false);
            Hint.text="정착지에서 생활을 저장하고 이어갈 수 있습니다.";EventSystem.current?.SetSelectedGameObject(Resume.gameObject);
        }
        void RestorePage(){Page.SetActive(true);View.transform.Find("Dim").gameObject.SetActive(true);closedFrame=Time.frameCount;EventSystem.current?.SetSelectedGameObject(Resume.gameObject);}
        void OpenSlots(bool saving){Page.SetActive(false);View.transform.Find("Dim").gameObject.SetActive(false);SavePanel.Open(owner,saving?owner:null);}
        void LoadData(CampaignSaveData data)
        {
            if(leaving)return;
            if(!Application.CanStreamedLevelBeLoaded("Settlement")){SavePanel.Hint.text="정착지 화면을 열 수 없습니다.";return;}
            try { CampaignPersistence.Prepare(data,owner);leaving=true;SceneManager.LoadScene("Settlement"); }
            catch(System.Exception e){SavePanel.Hint.text=e.Message;}
        }
        public void Close()
        {
            if(!IsOpen || !Page.activeSelf || leaving)return;View.SetActive(false);owner.Main.interactable=owner.Main.blocksRaycasts=true;closedFrame=Time.frameCount;EventSystem.current?.SetSelectedGameObject(OpenButton.gameObject);
        }
        void Update()
        {
            if(Keyboard.current==null || !Keyboard.current.escapeKey.wasPressedThisFrame || closedFrame==Time.frameCount)return;
            if(IsOpen){if(Page.activeSelf)Close();else if(Review.activeSelf){Review.SetActive(false);RestorePage();}}
            else
            {
                // Other panels own Escape. Check last frame as well to avoid opening the menu on their close key.
                if(wasReady)Open();
            }
        }
        bool wasReady;
        void LateUpdate(){wasReady=owner!=null && owner.Main.gameObject.activeInHierarchy && owner.Main.interactable && !IsOpen;}
    }
}
