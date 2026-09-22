using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    public sealed class TitleMenuController : MonoBehaviour
    {
        [Header("Pages — edit the referenced prefab children")]
        public CanvasGroup Menu;
        public GameObject SettingsPanel, LoadPanel, ConfirmPanel;
        public CanvasGroup Fade;
        public Button NewGameButton, ContinueButton, LoadButton, SettingsButton, ExitButton;
        public Button SettingsApply, SettingsCancel, LoadClose, ConfirmAccept, ConfirmCancel;
        public Slider Volume;
        public Toggle Fullscreen;
        public Text VolumeValue, ConfirmTitle, ConfirmBody, LoadMessage;
        [Header("Scene routing")]
        public string NewGameScene = "NightExpedition";
        [Range(0, 1)] public float FadeSeconds = .35f;
        const string VolumeKey = "demo5.settings.masterVolume", FullscreenKey = "demo5.settings.fullscreen";
        GameObject activeModal, returnFocus;
        float previousVolume;
        bool transitioning;
        public bool IsTransitioning => transitioning;
        public string OpenModalName => activeModal ? activeModal.name : "";

        void Awake()
        {
            SettingsPanel.SetActive(false); LoadPanel.SetActive(false); ConfirmPanel.SetActive(false);
            Fade.alpha = 0; Fade.blocksRaycasts = false;
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1));
            if (PlayerPrefs.HasKey(FullscreenKey)) Screen.fullScreen = PlayerPrefs.GetInt(FullscreenKey) == 1;
            // No campaign save service exists yet. Never imply that a dummy save can be resumed.
            ContinueButton.interactable = false;
            NewGameButton.onClick.AddListener(StartNewGame);
            SettingsButton.onClick.AddListener(OpenSettings);
            LoadButton.onClick.AddListener(OpenLoad);
            ExitButton.onClick.AddListener(OpenQuit);
            SettingsApply.onClick.AddListener(ApplySettings);
            SettingsCancel.onClick.AddListener(CancelSettings);
            LoadClose.onClick.AddListener(CloseModal);
            ConfirmCancel.onClick.AddListener(CloseModal);
            ConfirmAccept.onClick.AddListener(ConfirmQuit);
            Volume.onValueChanged.AddListener(PreviewVolume);
        }
        void Start() { Focus(NewGameButton.gameObject); }
        void Update()
        {
            if (transitioning || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (activeModal == SettingsPanel) CancelSettings();
            else if (activeModal) CloseModal();
            else OpenQuit();
        }
        void ShowModal(GameObject panel, GameObject firstFocus)
        {
            if (transitioning || activeModal) return;
            returnFocus = EventSystem.current ? EventSystem.current.currentSelectedGameObject : NewGameButton.gameObject;
            activeModal = panel; Menu.interactable = false; Menu.blocksRaycasts = false;
            panel.SetActive(true); Focus(firstFocus);
        }
        public void OpenSettings()
        {
            if (transitioning || activeModal) return;
            previousVolume = AudioListener.volume;
            Volume.SetValueWithoutNotify(previousVolume);
            Fullscreen.SetIsOnWithoutNotify(Screen.fullScreen);
            VolumeValue.text = Mathf.RoundToInt(previousVolume * 100) + "%";
            ShowModal(SettingsPanel, Volume.gameObject);
        }
        void PreviewVolume(float value)
        {
            if (activeModal != SettingsPanel) return;
            AudioListener.volume = value;
            VolumeValue.text = Mathf.RoundToInt(value * 100) + "%";
        }
        public void ApplySettings()
        {
            if (activeModal != SettingsPanel) return;
            AudioListener.volume = Volume.value;
            Screen.fullScreen = Fullscreen.isOn;
            PlayerPrefs.SetFloat(VolumeKey, Volume.value);
            PlayerPrefs.SetInt(FullscreenKey, Fullscreen.isOn ? 1 : 0);
            PlayerPrefs.Save(); CloseModal();
        }
        public void CancelSettings()
        {
            if (activeModal != SettingsPanel) return;
            AudioListener.volume = previousVolume; CloseModal();
        }
        public void OpenLoad()
        {
            if (activeModal || transitioning) return;
            LoadMessage.text = "저장된 여정이 없습니다.";
            ShowModal(LoadPanel, LoadClose.gameObject);
        }
        public void OpenQuit()
        {
            if (activeModal || transitioning) return;
            ConfirmTitle.text = "게임을 종료할까요?";
            ConfirmBody.text = "다음 여정에서 다시 만나요.";
            ShowModal(ConfirmPanel, ConfirmCancel.gameObject);
        }
        public void CloseModal()
        {
            if (transitioning || !activeModal) return;
            activeModal.SetActive(false); activeModal = null;
            Menu.interactable = true; Menu.blocksRaycasts = true;
            Focus(returnFocus ? returnFocus : NewGameButton.gameObject);
        }
        void ConfirmQuit()
        {
            if (activeModal != ConfirmPanel || transitioning) return;
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
        public void StartNewGame()
        {
            if (transitioning || activeModal) return;
            if (!Application.CanStreamedLevelBeLoaded(NewGameScene))
            {
                OpenLoad(); LoadMessage.text = "여정을 시작하지 못했습니다. 잠시 후 다시 시도해 주세요."; return;
            }
            PartySelectionSession.Clear();
            transitioning = true; Menu.interactable = false; Fade.blocksRaycasts = true;
            Focus(null); StartCoroutine(EnterJourney());
        }
        IEnumerator EnterJourney()
        {
            float elapsed = 0;
            while (elapsed < FadeSeconds) { elapsed += Time.unscaledDeltaTime; Fade.alpha = Mathf.Clamp01(elapsed / Mathf.Max(.01f, FadeSeconds)); yield return null; }
            Fade.alpha = 1;
            yield return SceneManager.LoadSceneAsync(NewGameScene);
        }
        static void Focus(GameObject target) { if (EventSystem.current) EventSystem.current.SetSelectedGameObject(target); }
        void OnDisable() { if (activeModal == SettingsPanel) AudioListener.volume = previousVolume; }
    }
}
