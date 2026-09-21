using System.Collections;
using System.Collections.Generic;
using Live49.Core;
using Live49.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Live49.Title
{
    // C0-00 title: menu focus/confirm, then the approved D00~D02 exit (menu fade + 3.5% push-in)
    // while the Game scene loads underneath (docs/03-콘티/챕터0/C0-OPENING-DIRECTION.ko.md).
    public class TitleController : MonoBehaviour
    {
        static readonly float[] RowY = { 492f, 574f, 656f, 738f,820f };

        [SerializeField] TitleMenuItem continueItem;
        [SerializeField] TitleMenuItem startItem;
        [SerializeField] TitleMenuItem settingsItem;
        [SerializeField] TitleMenuItem quitItem;
        [SerializeField] CanvasGroup menuGroup;
        [SerializeField] RectTransform backgroundZoom;
        [SerializeField] Camera titleCamera;
        [SerializeField] EventSystem eventSystem;

        [Header("D00~D02 채택값")]
        [SerializeField] float pressFeedback = 0.12f;
        [SerializeField] float menuFadeStart = 0.08f;
        [SerializeField] float menuFadeDuration = 0.40f;
        [SerializeField] float zoomStart = 0.15f;
        [SerializeField] float zoomDuration = 1.20f;
        [SerializeField] float zoomTo = 1.035f;

        readonly List<TitleMenuItem> _items = new List<TitleMenuItem>();
        int _focus;
        bool _locked;
        SettingsPanel _settings;
        SaveLoadPanel _saveLoad;
        TitleMenuItem _loadItem;
        int _modalClosedFrame = -1;

        void Start()
        {
            bool hasSave = SaveSlots.Latest()!=null;
            continueItem.gameObject.SetActive(hasSave);
            continueItem.Interactable = hasSave;
            if (hasSave) _items.Add(continueItem);
            _items.Add(startItem);
            _loadItem=Instantiate(startItem,startItem.transform.parent);_loadItem.name="LoadMenuItem";_loadItem.Configure("load","불러오기");_loadItem.Interactable=true;_items.Add(_loadItem);
            _items.Add(settingsItem);
            _items.Add(quitItem);

            for (int i = 0; i < _items.Count; i++)
            {
                var rt = (RectTransform)_items[i].transform;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -RowY[i]);
                _items[i].Owner = this;
            }

            _settings = SettingsPanel.Create(menuGroup.transform.parent, startItem.GetComponentInChildren<TMPro.TMP_Text>().font);
            _settings.Closed += () => { _modalClosedFrame = Time.frameCount; Focus(settingsItem); };
            _saveLoad=SaveLoadPanel.Create(menuGroup.transform.parent,startItem.GetComponentInChildren<TMPro.TMP_Text>().font,()=>{_modalClosedFrame=Time.frameCount;Focus(_loadItem);},()=>StartCoroutine(BeginGame(_loadItem)));
            settingsItem.Interactable = true;
            Focus(_items[0]);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (_locked || Time.frameCount == _modalClosedFrame || (_saveLoad!=null&&_saveLoad.IsOpen) || (_settings != null && _settings.IsOpen) || keyboard == null) return;

            if (keyboard.upArrowKey.wasPressedThisFrame) Move(-1);
            else if (keyboard.downArrowKey.wasPressedThisFrame) Move(1);
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
                Confirm(_items[_focus]);
        }

        void Move(int direction)
        {
            int next = _focus;
            for (int i = 0; i < _items.Count; i++)
            {
                next = (next + direction + _items.Count) % _items.Count;
                if (_items[next].Interactable) break;
            }
            Focus(_items[next]);
        }

        public void Focus(TitleMenuItem item)
        {
            if (_locked || (_saveLoad!=null&&_saveLoad.IsOpen) || (_settings != null && _settings.IsOpen)) return;
            _focus = _items.IndexOf(item);
            foreach (var it in _items) it.SetFocused(it == item);
        }

        public void Confirm(TitleMenuItem item)
        {
            if (_locked || (_saveLoad!=null&&_saveLoad.IsOpen) || (_settings != null && _settings.IsOpen) || !item.Interactable) return;
            switch (item.Id)
            {
                case "settings":
                    _settings.Open();
                    break;
                case "start":
                    SaveSystem.NewGame();
                    StartCoroutine(BeginGame(item));
                    break;
                case "continue":
                    var latest=SaveSlots.Latest();if(latest==null||!SaveSystem.QueueLoad(latest.Path,out _))return;
                    StartCoroutine(BeginGame(item));
                    break;
                case "load":
                    _saveLoad.Open(false,false);
                    break;
                case "quit":
                    Quit();
                    break;
            }
        }

        IEnumerator BeginGame(TitleMenuItem item)
        {
            Focus(item);
            _locked = true;
            FreshInput.ConsumeThisFrame(); // the start press must not reach the first line
            item.SetPressed(true);

            if (eventSystem != null) eventSystem.enabled = false;
            if (titleCamera != null && titleCamera.TryGetComponent(out AudioListener listener)) listener.enabled = false;

            var load = SceneFlow.LoadGameUnderTitle();
            float end = zoomStart + zoomDuration;
            float t = 0f;
            while (t < end)
            {
                t += SeqTime.Delta;
                if (t >= pressFeedback) item.SetPressed(false);
                menuGroup.alpha = 1f - Tween.Smooth(Tween.Seg(t, menuFadeStart, menuFadeDuration));
                float scale = Mathf.Lerp(1f, zoomTo, Tween.Smooth(Tween.Seg(t, zoomStart, zoomDuration)));
                backgroundZoom.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            while (!load.isDone) yield return null;
            SceneFlow.MarkTitleReady();
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
