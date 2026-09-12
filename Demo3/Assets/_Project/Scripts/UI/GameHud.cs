using System;
using System.Collections;
using System.Collections.Generic;
using Live49.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Live49.UI
{
    // One shared overlay across gameplay scenes. Narrative flow explicitly reveals the exploration HUD.
    [DefaultExecutionOrder(-1000)]
    public class GameHud : MonoBehaviour
    {
        public enum ActionId { Map, Cooking, Bag, Journal }
        public static GameHud Instance { get; private set; }
        static readonly Color Cream = new Color32(242, 228, 196, 255);
        static readonly Color Muted = new Color32(187, 166, 133, 255);
        static readonly Color Gold = new Color32(180, 145, 89, 255);
        static readonly Color Ink = new Color32(30, 27, 23, 238);
        readonly Dictionary<ActionId, Action> _actions = new Dictionary<ActionId, Action>();
        readonly List<Button> _menuButtons = new List<Button>();
        TMP_FontAsset _font;
        RectTransform _viewport, _location, _goal, _dock, _card;
        Button _endDayButton;
        Action _endDay;
        Func<string> _dayEndBlockReason;
        string _dayEndSummary;
        TMP_Text _place, _time, _objective, _notice;
        CanvasGroup _pause;
        SettingsPanel _settings;
        MapPanel _map;
        public MapPanel Map => _map;
        BagPanel _bag;
        public BagPanel Bag => _bag;
        ActivityPanel _activity;
        SaveLoadPanel _saveLoad;
        TMP_Text _autoSaveStatus;
        CanvasGroup _autoSaveCue;
        float _autoSaveCueLeft;
        public SaveLoadPanel SaveLoad => _saveLoad;
        public ActivityPanel Activity => _activity;
        public Action ActivitiesChanged;
        public string Objective => _objective.text;
        Button _menuButton;
        bool _exploring, _closing, _savedAudioPause;
        float _savedScale;
        int _changedFrame = -1;
        string _confirm;
        Coroutine _noticeRoutine, _fade;
        public bool IsPaused => GamePause.IsPaused;
        public bool IsExploring => _exploring;
        public SettingsPanel Settings => _settings;
        public Transform InteractionRoot => _viewport;

        public static GameHud Ensure(TMP_FontAsset font)
        {
            if (Instance != null) return Instance;
            var root = new GameObject("SharedGameHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(root);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = 200;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var hud = root.AddComponent<GameHud>();
            Instance = hud;
            hud._font = font;
            hud.Build();
            SceneManager.activeSceneChanged += hud.ActiveSceneChanged;
            return hud;
        }

        void Build()
        {
            _viewport = Rect(transform, "HUDViewport", 0, 0, 1920, 1080);
            _viewport.anchorMin = _viewport.anchorMax = _viewport.pivot = Vector2.one * .5f;
            _viewport.anchoredPosition = Vector2.zero;
            _location = Box(_viewport, "Location", 86, 78, 334, 112, Ink);
            Rule(_location, 0, 0, 2, 112, Gold);
            Icon(_location, HudIcon.Kind.Moon, 24, 29, 42);
            Label(_location, "머무는 곳", 88, 17, 204, 25, 16, Muted);
            _place = Label(_location, "캠핑카", 87, 44, 208, 38, 30, Cream);
            _place.textWrappingMode = TextWrappingModes.NoWrap;
            _time = Label(_location, "", 88, 83, 204, 21, 16, Muted);

            _goal = Rect(_viewport, "CurrentObjective", 92, 893, 710, 112);
            var goalBack = Box(_goal, "Shade", -6, 0, 610, 110, new Color(Ink.r, Ink.g, Ink.b, .83f));
            Rule(goalBack, 0, 0, 2, 110, new Color(Gold.r, Gold.g, Gold.b, .65f));
            Label(_goal, "지금 할 일", 22, 19, 530, 25, 17, Muted);
            _objective = Label(_goal, "", 22, 52, 540, 37, 27, Cream);

            _dock = Box(_viewport, "ActionDock", 1288, 867, 546, 138, Ink);
            Rule(_dock, 0, 0, 546, 1, Gold);
            var labels = new[] { "지도", "요리", "가방", "기록" };
            for (int i = 0; i < 4; i++)
            {
                var id = (ActionId)i;
                var b = MakeButton(_dock, "Action_" + id, "", 12 + i * 134, 10, 120, 116, () => InvokeAction(id));
                Icon(b.transform, (HudIcon.Kind)i, 42, 16, 36);
                Label(b.transform, labels[i], 0, 68, 120, 36, 23, Cream, TextAlignmentOptions.Center);
                if (i < 3) Rule(_dock, 139 + i * 134, 29, 1, 78, new Color(Gold.r, Gold.g, Gold.b, .18f));
            }
            _notice = Label(_viewport, "", 1210, 804, 624, 43, 21, Cream, TextAlignmentOptions.MidlineRight);
            _notice.rectTransform.anchoredPosition = new Vector2(1210, -721);
            _endDayButton = MakeButton(_viewport, "EndDayButton", "", 1560, 776, 274, 68, RequestDayEnd);
            _endDayButton.GetComponent<Image>().color = Ink;
            Icon(_endDayButton.transform, HudIcon.Kind.Moon, 22, 19, 30);
            Label(_endDayButton.transform, "하루 마치기", 70, 12, 185, 44, 25, Cream);
            _menuButton = MakeButton(_viewport, "PauseButton", "", 1696, 78, 138, 58, OpenPause);
            _menuButton.GetComponent<Image>().color = Ink;
            Icon(_menuButton.transform, HudIcon.Kind.Menu, 20, 18, 22);
            Label(_menuButton.transform, "메뉴", 55, 9, 69, 39, 22, Cream);
            var saveCue=Box(_viewport,"AutoSaveCue",1514,148,320,38,new Color(Ink.r,Ink.g,Ink.b,.72f));
            _autoSaveStatus=Label(saveCue,"",12,0,296,38,17,Muted,TextAlignmentOptions.Center);
            _autoSaveCue=saveCue.gameObject.AddComponent<CanvasGroup>();
            _autoSaveCue.alpha=0;_autoSaveCue.blocksRaycasts=false;_autoSaveCue.interactable=false;
            SaveSystem.AutoSaveCompleted+=OnAutoSave;

            var shade = Box(_viewport, "PauseOverlay", 0, 0, 1920, 1080, new Color(0, 0, 0, .80f));
            shade.GetComponent<Image>().raycastTarget = true;
            _pause = shade.gameObject.AddComponent<CanvasGroup>();
            _card = Box(shade, "PauseCard", 590, 190, 740, 700, new Color32(30, 27, 23, 252));
            _pause.gameObject.SetActive(false);
            _settings = SettingsPanel.Create(_viewport, _font);
            _settings.Closed += SettingsClosed;
            _settings.ConfigureSaveLoad(ShowSaveCard);
            SetNarrativeMode();
        }

        // Context is supplied by the scene; never invent days, supplies, or survival values in the HUD.
        public void SetExplorationContext(string place, string timeOfDay, string objective)
        {
            _exploring = true;
            _place.text = place;
            _time.text = timeOfDay;
            _objective.text = objective;
            ShowExplorationWidgets((_map == null || !_map.IsOpen) && (_bag == null || !_bag.IsOpen) && (_activity == null || !_activity.IsOpen));
        }
        void ShowExplorationWidgets(bool visible)
        {
            _location.gameObject.SetActive(visible); _dock.gameObject.SetActive(visible);
            _endDayButton.gameObject.SetActive(visible);
            _goal.gameObject.SetActive(visible && !string.IsNullOrWhiteSpace(_objective.text));
        }
        public void SetNarrativeMode()
        {
            _exploring = false;
            ShowExplorationWidgets(false);
            _notice.text = string.Empty;
        }
        public void BindAction(ActionId id, Action handler)
        {
            if (handler == null) _actions.Remove(id); else _actions[id] = handler;
        }
        public void ConfigureMap(IEnumerable<MapPanel.Place> places, Func<string, string> blockedReason, Action<string> travel)
        {
            if (_map == null) _map = MapPanel.Create(_viewport, _font, () => CloseMap());
            _map.SetPlaces(places);
            _map.ConfigureTravel(blockedReason, travel == null ? null : id => CloseMap(() => travel(id)));
            BindAction(ActionId.Map, OpenMap);
        }
        public void OpenMap()
        {
            if (_map == null || !_exploring || IsPaused || _closing) return;
            if (_activity != null && _activity.CookingActive) { ShowInteraction("조리가 진행 중이에요.", "주방에서 조리를 마친 뒤 이동해요."); return; }
            OpenPause();
            if (_fade != null) StopCoroutine(_fade);
            _pause.gameObject.SetActive(false);
            _map.Open();
            ShowExplorationWidgets(false);
            _notice.text = string.Empty;
        }
        public void ConfigureBag(BagContents contents)
        {
            if (_bag == null) _bag = BagPanel.Create(_viewport, _font, CloseBag);
            _bag.SetContents(contents);
            BindAction(ActionId.Bag, OpenBag);
        }
        public void ConfigureActivities()
        {
            BindAction(ActionId.Cooking, () => OpenActivity(ActivityPanel.Kind.Kitchen));
            BindAction(ActionId.Journal, () => OpenActivity(ActivityPanel.Kind.Journal));
        }
        public void OpenActivity(ActivityPanel.Kind kind)
        {
            if (!_exploring || IsPaused || _closing || SaveSystem.Current == null) return;
            if((kind==ActivityPanel.Kind.Kitchen||kind==ActivityPanel.Kind.Life||kind==ActivityPanel.Kind.Evening)&&(SaveSystem.Current.inStore||!string.IsNullOrEmpty(SaveSystem.Current.exploringPlace)))
            {ShowInteraction("캠핑카에서 요리해요", "캠핑카로 돌아온 뒤 주방을 이용해요.", context:"주변 탐색");return;}
            if (_activity == null) _activity = ActivityPanel.Create(_viewport, _font, CloseActivity);
            OpenPause(); if (_fade != null) StopCoroutine(_fade); _pause.gameObject.SetActive(false);
            _activity.Open(kind, SaveSystem.Current, _objective.text); ShowExplorationWidgets(false);
        }
        public void CloseActivity()
        {
            if (_activity == null || !_activity.IsOpen || _closing) return;
            _closing = true;
            _activity.Close(() =>
            {
                RestoreClock(); _closing = false; _changedFrame = Time.frameCount;
                ConfigureBag(SaveSystem.Current.Bag()); ShowExplorationWidgets(_exploring);
                ActivitiesChanged?.Invoke();
                EventSystem.current?.SetSelectedGameObject(null);
            });
        }
        public void OpenBag()
        {
            if (_bag == null || !_exploring || IsPaused || _closing) return;
            OpenPause();
            if (_fade != null) StopCoroutine(_fade);
            _pause.gameObject.SetActive(false);
            _bag.Open(); ShowExplorationWidgets(false); _notice.text = string.Empty;
        }
        public void CloseBag()
        {
            if (_bag == null || !_bag.IsOpen || _closing) return;
            _closing = true;
            _bag.Close(() =>
            {
                RestoreClock(); _closing = false; _changedFrame = Time.frameCount;
                ShowExplorationWidgets(_exploring);
                EventSystem.current?.SetSelectedGameObject(null);
            });
        }
        public void CloseMap(Action completed = null)
        {
            if (_map == null || !_map.IsOpen || _closing) return;
            _closing = true;
            _map.Close(() =>
            {
                RestoreClock(); _closing = false; _changedFrame = Time.frameCount;
                ShowExplorationWidgets(_exploring);
                EventSystem.current?.SetSelectedGameObject(null);
                completed?.Invoke();
            });
        }
        public void InvokeAction(ActionId id)
        {
            if (!_exploring || IsPaused) return;
            FreshInput.DiscardPending();
            if (_actions.TryGetValue(id, out var action)) { action(); return; }
            if (_noticeRoutine != null) StopCoroutine(_noticeRoutine);
            _noticeRoutine = StartCoroutine(Notice(id));
        }
        IEnumerator Notice(ActionId id)
        {
            string[] messages = { "지금은 지도를 펼칠 수 없어요.", "지금은 요리할 수 없어요.", "지금은 가방을 열 수 없어요.", "지금은 기록을 펼칠 수 없어요." };
            _notice.text = messages[(int)id];
            yield return new WaitForSecondsRealtime(2.8f);
            _notice.text = string.Empty;
        }

        // The story/day system owns prerequisites and the actual next-day transition.
        // Returning null/empty from blockedReason means ready; a missing handler never advances the story.
        public void ConfigureDayEnd(string summary, Func<string> blockedReason, Action finish)
        {
            _dayEndSummary = summary;
            _dayEndBlockReason = blockedReason;
            _endDay = finish;
        }
        string DayEndBlockReason()
        {
            if (SaveSystem.Current!=null&&CampLife.Busy(SaveSystem.Current)) return "주방에서 진행 중인 조리·가열을 먼저 마쳐주세요.";
            if (!_exploring) return "대화를 마친 뒤에 하루를 마칠 수 있어요.";
            string reason = _dayEndBlockReason?.Invoke();
            if (!string.IsNullOrWhiteSpace(reason)) return reason;
            return _endDay == null ? "지금은 하루를 마칠 수 없어요." : null;
        }
        public void RequestDayEnd()
        {
            if (!_exploring || IsPaused || _closing || SceneFlow.OpeningHandoffPending) return;
            OpenPause();
            ShowDayEndCard();
        }
        void ShowDayEndCard()
        {
            _confirm = "dayEnd";
            _changedFrame = Time.frameCount;
            string reason = DayEndBlockReason();
            bool ready = string.IsNullOrEmpty(reason);
            if(SaveSystem.Current!=null&&SaveSystem.Current.day>0)
            {
                ClearCard("LIVE49  /  하루의 끝",SaveSystem.Current.day+"일 차의 여정",ready?"오늘의 기록을 남기고 쉬어요.":reason);
                var s=SaveSystem.Current;
                Label(_card,"오늘의 식사 · "+(s.Has("life.meal."+s.day)?"마쳤어요":"아직이에요")+"\n\n남긴 한 장 · "+(s.Has("memory."+s.day)?s.Value("memory.title."+s.day):"아직 선택하지 않았어요.")+"\n\n방문·물자 내역은 기록장에서 볼 수 있어요.",64,258,612,176,23,Cream);
                MenuRow("더 둘러보기","현재 장소로 돌아가기",450,Resume,!ready);
                var memory=MenuRow("오늘 남길 한 장","기록할 장면 고르기",542,()=>{_closing=true;StartCoroutine(OpenEveningAfterPause());});
                memory.interactable=CampLife.Home(s);if(!memory.interactable)_menuButtons.Remove(memory);
                var end=MenuRow("하루 마치기",ready?"바로 쉬기 · 기록은 선택이에요":"캠핑카에서 준비를 마쳐주세요",634,ConfirmDayEnd,ready);
                if(!ready){end.interactable=false;_menuButtons.Remove(end);}WireNavigation();return;
            }
            ClearCard("LIVE49  /  하루의 끝", ready ? "오늘은 여기까지 할까요?" : "아직 남은 일이 있어요.",
                ready ? "불을 끄고 쉬어요.\n하루를 마치면 다음 날로 이어집니다." : reason);
            Label(_card, "오늘의 마무리", 64, 272, 612, 28, 17, Gold);
            Label(_card, string.IsNullOrWhiteSpace(_dayEndSummary) ? _objective.text : _dayEndSummary,
                64, 311, 612, 88, 24, Cream);
            MenuRow("더 둘러보기", "캠핑카로 돌아가기", 436, Resume, !ready);
            var finish = MenuRow("하루 마치기", ready ? "불을 끄고 쉬기" : "남은 일을 먼저 마쳐주세요", 538, ConfirmDayEnd, ready);
            if (!ready) { finish.interactable = false; _menuButtons.Remove(finish); }
            WireNavigation(); // Start on cancel so an accidental repeated Enter cannot finish the day.
        }
        public void ConfirmDayEnd()
        {
            if (_confirm != "dayEnd" || !IsPaused || _closing || _settings.IsOpen) return;
            if (!string.IsNullOrEmpty(DayEndBlockReason())) { ShowDayEndCard(); return; }
            _closing = true;
            if (_fade != null) StopCoroutine(_fade);
            StartCoroutine(FinishDay(_endDay));
        }
        IEnumerator OpenEveningAfterPause(){yield return ClosePause();OpenActivity(ActivityPanel.Kind.Evening);}
        IEnumerator FinishDay(Action finish)
        {
            yield return ClosePause();
            // Recheck after the closing animation as the active scene/context may have changed.
            if (_endDay == finish && string.IsNullOrEmpty(DayEndBlockReason()))
            {
                _endDay = null; // A second confirmation cannot dispatch the same day twice.
                finish();
            }
        }

        void Update()
        {
            bool available = !SceneFlow.OpeningHandoffPending;
            _menuButton.gameObject.SetActive(available && !IsPaused);
            bool cueVisible=available&&_exploring&&!IsPaused&&!_closing&&_autoSaveCueLeft>0;
            _autoSaveCue.alpha=cueVisible?Mathf.Min(Mathf.Clamp01((3-_autoSaveCueLeft)/.25f),Mathf.Clamp01(_autoSaveCueLeft/.5f)):0;
            if(cueVisible)_autoSaveCueLeft=Mathf.Max(0,_autoSaveCueLeft-Time.unscaledDeltaTime);
            if (!available || _closing || Time.frameCount == _changedFrame || _settings.IsOpen) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) HandleEscape();
        }
        void OnAutoSave(bool success)
        {
            _autoSaveStatus.text=success?"여정이 자동 저장되었어요":"자동 저장 실패 · 설정에서 확인";
            _autoSaveCueLeft=3;
        }
        public void HandleEscape()
        {
            if (_closing || SceneFlow.OpeningHandoffPending) return;
            if(_saveLoad!=null&&_saveLoad.IsOpen){_saveLoad.Escape();return;}
            if (_map != null && _map.IsOpen) { _map.Escape(); return; }
            if (_bag != null && _bag.IsOpen) { CloseBag(); return; }
            if (_activity != null && _activity.IsOpen) { _activity.RequestClose(); return; }
            if (_settings.IsOpen) { _settings.Close(); return; }
            if (!IsPaused) OpenPause();
            else if (_confirm == "load") ShowSaveCard();
            else if (_confirm == "dayEnd" || _confirm == "interaction") Resume();
            else if (_confirm != null) ShowPauseCard();
            else Resume();
        }
        public void OpenPause()
        {
            if (IsPaused || SceneFlow.OpeningHandoffPending) return;
            _savedScale = Time.timeScale; _savedAudioPause = AudioListener.pause;
            GamePause.IsPaused = true; Time.timeScale = 0; AudioListener.pause = true;
            FreshInput.DiscardPending();
            _changedFrame = Time.frameCount;
            _pause.gameObject.SetActive(true);
            ShowPauseCard();
            _fade = StartCoroutine(FadeOverlay(0, 1, .18f));
        }
        public void Resume()
        {
            if (_map != null && _map.IsOpen) { CloseMap(); return; }
            if (_bag != null && _bag.IsOpen) { CloseBag(); return; }
            if (_activity != null && _activity.IsOpen) { _activity.RequestClose(); return; }
            if (!IsPaused || _closing || _settings.IsOpen) return;
            _closing = true;
            if (_fade != null) StopCoroutine(_fade);
            StartCoroutine(ClosePause());
        }
        IEnumerator ClosePause()
        {
            yield return FadeOverlay(_pause.alpha, 0, .14f);
            _pause.gameObject.SetActive(false);
            _confirm = null;
            RestoreClock();
            _closing = false;
            ShowExplorationWidgets(_exploring);
            _changedFrame = Time.frameCount;
            EventSystem.current?.SetSelectedGameObject(null);
        }
        IEnumerator FadeOverlay(float from, float to, float duration)
        {
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            { _pause.alpha = Mathf.Lerp(from, to, Tween.Smooth(t / duration)); yield return null; }
            _pause.alpha = to;
        }
        void RestoreClock()
        {
            if (!IsPaused) return;
            Time.timeScale = _savedScale; AudioListener.pause = _savedAudioPause;
            GamePause.IsPaused = false;
            FreshInput.DiscardPending();
        }
        void ClearCard(string eyebrow, string title, string subtitle)
        {
            foreach (Transform child in _card) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            _menuButtons.Clear();
            Rule(_card, 54, 0, 632, 2, Gold);
            Label(_card, eyebrow, 64, 40, 612, 28, 17, Gold);
            Label(_card, title, 61, 90, 620, 62, 44, Cream);
            Label(_card, subtitle, 64, 166, 612, 54, 21, Muted);
            Rule(_card, 64, 239, 612, 1, new Color(Gold.r, Gold.g, Gold.b, .30f));
        }
        void ShowPauseCard()
        {
            _confirm = null; _changedFrame = Time.frameCount;
            _pause.gameObject.SetActive(true);
            ClearCard("LIVE49  /  잠시 멈춤", "잠시 쉬어가기", "준비가 되면, 머물던 순간에서 이어가요.");
            MenuRow("재개", "이야기 이어가기", 244, Resume, true);
            MenuRow("설정", "소리 · 대사 · 저장·불러오기", 346, OpenSettings);
            MenuRow("타이틀로", "시작 화면으로 돌아가기", 448, () => RequestExit(false));
            MenuRow("게임 종료", "여정에서 나가기", 550, () => RequestExit(true));
            WireNavigation();
        }
        public void OpenSettings()
        {
            if (!IsPaused || _closing) return;
            if (_fade != null) StopCoroutine(_fade);
            _pause.alpha = 1;
            _pause.gameObject.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(null);
            _settings.Open();
            ShowExplorationWidgets(false);
        }
        public void ShowSaveCard()
        {
            if (!IsPaused || _closing) return;
            ShowSaveLoad(SaveSystem.CanSave);
        }
        public void OpenSaveLoad(bool saving)
        {
            if(IsPaused||_closing||SceneFlow.OpeningHandoffPending)return;
            OpenPause();ShowSaveLoad(saving);
        }
        void ShowSaveLoad(bool saving)
        {
            if(_fade!=null)StopCoroutine(_fade);_pause.alpha=1;_pause.gameObject.SetActive(false);
            if(_saveLoad==null)_saveLoad=SaveLoadPanel.Create(_viewport,_font,()=>
            {
                _settings.Open();
            },LoadQueuedSlot);
            _saveLoad.Open(saving,true);ShowExplorationWidgets(false);
        }
        void LoadQueuedSlot()
        {
            RestoreClock();_closing=true;Instance=null;Destroy(gameObject);
            SceneManager.LoadSceneAsync(SceneNames.Game,LoadSceneMode.Single);
        }
        public void LoadSaved()
        {
            if (!IsPaused || _closing || _confirm != "load") return;
            if (!SaveSystem.QueueLoad(out var error)) { ShowSaveCard(); Label(_card,error,64,230,612,55,19,Muted); return; }
            RestoreClock(); _closing=true;
            // A fresh HUD prevents modal callbacks or an unfinished cooking session surviving a reload.
            Instance=null; Destroy(gameObject);
            SceneManager.LoadSceneAsync(SceneNames.Game,LoadSceneMode.Single);
        }
        void SettingsClosed()
        {
            FreshInput.DiscardPending();
            ShowPauseCard(); // Closing Settings returns one level; gameplay remains paused.
        }
        // Scene interactions share the existing modal and clock ownership, with no additional scene.
        public void ShowInteraction(string title, string description, string[] choices = null, Action<int> selected = null, string context="캠핑카")
        {
            if (!_exploring || IsPaused || _closing) return;
            if (choices != null && choices.Length > 2) throw new ArgumentException("Interaction supports up to two choices.");
            OpenPause();
            _confirm = "interaction";
            ClearCard("LIVE49  /  "+context, title, description);
            if (choices != null)
            {
                for (int i = 0; i < choices.Length; i++)
                {
                    int index = i;
                    var b = MakeButton(_card, "InteractionChoice_" + i, choices[i], 54, 306 + i * 98, 632, 80,
                        () => ChooseInteraction(() => selected?.Invoke(index)));
                    _menuButtons.Add(b);
                }
            }
            MenuRow("돌아가기", context+" 둘러보기", 550, Resume, choices == null || choices.Length == 0);
            WireNavigation();
        }
        public IEnumerator ShowArrival(string place,string detail)
        {
            var root=Box(_viewport,"ArrivalCard",510,398,900,242,new Color32(24,29,27,255));
            Rule(root,270,0,360,2,Gold);
            Label(root,"도 착",0,28,900,34,20,Gold,TextAlignmentOptions.Center);
            Label(root,place,30,78,840,68,42,Cream,TextAlignmentOptions.Center);
            Label(root,detail,30,166,840,40,22,Muted,TextAlignmentOptions.Center);
            var group=root.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;
            for(float t=0;t<.35f;t+=Time.deltaTime){group.alpha=Mathf.SmoothStep(0,1,t/.35f);yield return null;}
            group.alpha=1;yield return new WaitForSeconds(1.1f);
            for(float t=0;t<.35f;t+=Time.deltaTime){group.alpha=1-Mathf.SmoothStep(0,1,t/.35f);yield return null;}
            Destroy(root.gameObject);
        }
        void ChooseInteraction(Action selected)
        {
            if (_confirm != "interaction" || !IsPaused || _closing) return;
            _closing = true;
            if (_fade != null) StopCoroutine(_fade);
            StartCoroutine(FinishInteraction(selected));
        }
        IEnumerator FinishInteraction(Action selected)
        {
            yield return ClosePause();
            selected();
        }
        public void RequestExit(bool quitApplication)
        {
            if (!IsPaused || _settings.IsOpen || _closing) return;
            _confirm = quitApplication ? "quit" : "title";
            _changedFrame = Time.frameCount;
            ClearCard("LIVE49  /  돌아가기", quitApplication ? "게임을 종료할까요?" : "타이틀로 돌아갈까요?",
                SaveSystem.CanSave ? "현재 진행을 자동 저장한 뒤 돌아가요.\n수동 저장 슬롯은 그대로 유지돼요." :
                SaveSlots.Latest()!=null ? "마지막으로 저장된 지점에서 이어갈 수 있어요.\n지금 진행 중인 연출은 다시 볼 수 있어요." : "첫 자동 저장 지점에 아직 도착하지 않았어요.\n나가면 처음부터 다시 시작해요.");
            MenuRow("돌아가기", "이 순간에 머무르기", 330, ShowPauseCard, true);
            MenuRow(quitApplication ? "게임 종료" : "타이틀로", "현재 진행을 마치고 나가기", 442, ConfirmExit);
            WireNavigation();
        }
        public void ConfirmExit()
        {
            if ((_confirm != "quit" && _confirm != "title") || !IsPaused || _closing) return;
            bool quit = _confirm == "quit";
            if(SaveSystem.CanSave&&!SaveSystem.AutoSave(SaveSystem.Current,out var error,false))
            {
                ClearCard("LIVE49  /  저장 확인", "자동 저장을 마치지 못했어요",error);
                MenuRow("돌아가기", "설정에서 수동 저장하기",330,ShowPauseCard,true);
                MenuRow("다시 시도", "자동 저장 후 나가기",442,ConfirmExit);WireNavigation();return;
            }
            GamePreferences.Save();
            RestoreClock();
            _closing = true;
            if (quit)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            else SceneManager.LoadSceneAsync(SceneNames.Title, LoadSceneMode.Single);
        }
        Button MenuRow(string label, string detail, float y, Action action, bool primary = false)
        {
            var b = MakeButton(_card, "Button_" + label, "", 54, y, 632, 80, action);
            b.GetComponent<Image>().color = primary ? new Color(Gold.r, Gold.g, Gold.b, .21f) : new Color(1, 1, 1, .025f);
            if (primary) Rule(b.transform, 0, 0, 2, 80, Gold);
            Label(b.transform, label, 24, 15, 250, 48, 29, Cream);
            Label(b.transform, detail, 265, 22, 325, 36, 18, Muted, TextAlignmentOptions.MidlineRight);
            _menuButtons.Add(b);
            return b;
        }
        void WireNavigation()
        {
            for (int i = 0; i < _menuButtons.Count; i++)
            {
                _menuButtons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnUp = _menuButtons[(i + _menuButtons.Count - 1) % _menuButtons.Count],
                    selectOnDown = _menuButtons[(i + 1) % _menuButtons.Count] };
            }
            EventSystem.current?.SetSelectedGameObject(_menuButtons[0].gameObject);
        }
        void ActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            if (newScene.name == SceneNames.Title) { RestoreClock(); Destroy(gameObject); }
            else
            {
                if (_bag != null)
                {
                    if (_bag.IsOpen) { RestoreClock(); _closing = false; }
                    Destroy(_bag.gameObject); _bag = null;
                }
                if (_activity != null) { Destroy(_activity.gameObject); _activity = null; }
                if(_saveLoad!=null){Destroy(_saveLoad.gameObject);_saveLoad=null;}
                _actions.Clear(); ConfigureDayEnd(null, null, null); SetNarrativeMode();
                ActivitiesChanged=null;
                if (_map != null) { _map.SetPlaces(Array.Empty<MapPanel.Place>()); _map.ConfigureTravel(null, null); }
            }
        }
        void OnDestroy()
        {
            SaveSystem.AutoSaveCompleted-=OnAutoSave;
            SceneManager.activeSceneChanged -= ActiveSceneChanged;
            if (Instance == this) { RestoreClock(); Instance = null; }
        }

        RectTransform Rect(Transform p, string name, float x, float y, float w, float h)
        {
            var r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            r.SetParent(p, false); r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
        }
        RectTransform Box(Transform p, string name, float x, float y, float w, float h, Color color)
        {
            var r = Rect(p, name, x, y, w, h);
            var image = r.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
            return r;
        }
        void Rule(Transform p, float x, float y, float w, float h, Color color) => Box(p, "Rule", x, y, w, h, color);
        TMP_Text Label(Transform p, string text, float x, float y, float w, float h, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var t = Rect(p, "Label_" + text, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = _font; t.text = text; t.fontSize = size; t.color = color; t.alignment = alignment;
            t.enableAutoSizing = true; t.fontSizeMin = Mathf.Max(12, size - 4); t.fontSizeMax = size;
            t.raycastTarget = false; return t;
        }
        void Icon(Transform p, HudIcon.Kind kind, float x, float y, float size)
        {
            var icon = Rect(p, "Icon_" + kind, x, y, size, size).gameObject.AddComponent<HudIcon>();
            icon.Symbol = kind; icon.color = Gold; icon.raycastTarget = false; icon.SetVerticesDirty();
        }
        Button MakeButton(Transform p, string name, string label, float x, float y, float w, float h, Action action)
        {
            var r = Box(p, name, x, y, w, h, new Color(1, 1, 1, .035f));
            var image = r.GetComponent<Image>(); image.raycastTarget = true;
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = image;
            var colors = b.colors; colors.normalColor = new Color(.8f, .76f, .67f);
            colors.highlightedColor = colors.selectedColor = new Color(1.5f, 1.35f, 1.08f);
            colors.pressedColor = Gold; colors.fadeDuration = .12f; b.colors = colors;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => { FreshInput.DiscardPending(); action(); });
            var focus = Box(r, "FocusRule", 12, h - 2, w - 24, 2, Gold).gameObject.AddComponent<CanvasGroup>();
            focus.alpha = 0;
            r.gameObject.AddComponent<HudButtonFeedback>().Highlight = focus;
            if (label.Length > 0) Label(r, label, 0, 0, w, h, 24, Cream, TextAlignmentOptions.Center);
            return b;
        }
    }
}
