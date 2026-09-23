using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Demo5.FrontEnd
{
    public static class GameSettings
    {
        public const string VolumeKey="demo5.settings.masterVolume", FullscreenKey="demo5.settings.fullscreen";
        public static void ApplySaved()
        {
            AudioListener.volume=Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey,1));
            if(PlayerPrefs.HasKey(FullscreenKey))Screen.fullScreen=PlayerPrefs.GetInt(FullscreenKey)==1;
        }
        public static void Save(float volume,bool fullscreen)
        {
            AudioListener.volume=Mathf.Clamp01(volume);Screen.fullScreen=fullscreen;
            PlayerPrefs.SetFloat(VolumeKey,AudioListener.volume);PlayerPrefs.SetInt(FullscreenKey,fullscreen?1:0);PlayerPrefs.Save();
        }
    }
    public sealed class GameSettingsDialog:MonoBehaviour
    {
        public Slider Volume;
        public Toggle Fullscreen;
        public Text VolumeValue;
        public Button Apply,Cancel;
        public event Action Closed;
        float previousVolume;
        bool initialized;
        public void Initialize()
        {
            if(initialized)return;initialized=true;previousVolume=AudioListener.volume;
            Apply.onClick.AddListener(Confirm);Cancel.onClick.AddListener(CancelChanges);
            Volume.onValueChanged.AddListener(v=>{AudioListener.volume=v;VolumeValue.text=Mathf.RoundToInt(v*100)+"%";});
            gameObject.SetActive(false);
        }
        public void Open()
        {
            Initialize();previousVolume=AudioListener.volume;
            Volume.SetValueWithoutNotify(previousVolume);Fullscreen.SetIsOnWithoutNotify(Screen.fullScreen);
            VolumeValue.text=Mathf.RoundToInt(previousVolume*100)+"%";gameObject.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(Volume.gameObject);
        }
        public void Confirm() { if(!gameObject.activeSelf)return;GameSettings.Save(Volume.value,Fullscreen.isOn);previousVolume=AudioListener.volume;Close(); }
        public void CancelChanges() { if(!gameObject.activeSelf)return;AudioListener.volume=previousVolume;Close(); }
        void Close() { gameObject.SetActive(false);Closed?.Invoke(); }
        void OnDisable() { if(initialized)AudioListener.volume=previousVolume; }
        void Update() { if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)CancelChanges(); }
    }
}
