using System;
using Demo6.Core.Save;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 처음 화면(기획/저장-처음화면-멈춤창-1차.md 4-1~4-8·10장, IMGUI 기능만 — 배치 다듬기는 Unity 개발 단계). GameShell 물체에 붙어 장면을 넘어 산다.
    /// GameSession.TitleShowing일 때만 그린다(TownRoot.Awake가 GameSession.HoldTownForTitle로 마을을 짓지 않고 붙잡은 동안). GUI 순서 −300(맨 위), 먼저 화면 전체를 검게 칠한다.
    /// 그림: Resources의 UI/Title/title_bg·UI/Title/title_logo가 있으면 쓰고(한 번만 찾음), 없으면 검은 바탕에 제목 글자(바탕체 큰 글자). 그림 파일은 만들지 않는다(10장, 사용자 몫).
    /// 단추(ShellRules.TitleItems): 이어하기(읽을 수 있는 저장이 있을 때, 아래에 이어하기 줄) · 새로 시작 · 설정 · 시험 메뉴(에디터·개발용 빌드) · 끝내기.
    /// 저장 파일 살펴보기(GameSave.Probe)는 보이기 시작한 첫 프레임에 한 번, 이어하기가 실패한 뒤 다시 한 번 한다. 읽지 못함·더 새 판이면 그 글을 보인다(4-7).
    /// 새로 시작은 저장 파일이 있으면(읽지 못한 것 포함) 한 번 묻는다(4-5·4-8). Esc는 GameShell이 받아 설정·확인 화면만 닫는다(Back).
    /// 단추 일은 OnGUI에서 받아 다음 Update에서 한 번 한다. 장면을 불러오는 동안(GameFlow.Loading)은 단추를 막고, 처음 화면을 떠나는 동안 검은 막을 남겨 빈 장면이 비치지 않게 한다.
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        const int GuiDepth = -300;
        const string BackgroundPath = "UI/Title/title_bg";
        const string LogoPath = "UI/Title/title_logo";
        const float ColumnWidth = 380f;
        const float ButtonHeight = 50f;
        const float ButtonGap = 10f;
        const float Pad = 24f;

        enum Page { Main, Settings, ConfirmNew }

        static TitleScreen s_instance;

        Page _page;
        Action _pending;
        SaveProbe _probe;
        bool _wasShowing;
        bool _probeDirty;
        bool _continueFailed;
        /// <summary>처음 화면을 떠나는 중(장면 불러오기·끝내기) — 다음 장면이 뜰 때까지 검은 막만 남긴다.</summary>
        bool _curtain;
        bool _quitting;
        bool _artTried;
        Texture2D _background;
        Texture2D _logo;
        string _folderLine;
        GUIStyle _status;
        GUIStyle _line;
        GUIStyle _body;
        GUIStyle _styleSource;
        readonly GUIContent _measure = new GUIContent();

        /// <summary>처음 화면 안의 설정·새로 시작 확인이 열려 있다 — Esc가 그것만 닫는다(5-1 차례 2).</summary>
        public static bool SubOpen => GameSession.TitleShowing && s_instance && s_instance._page != Page.Main;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => s_instance = null;

        /// <summary>Esc: 설정·확인 화면이면 첫 화면으로. 첫 화면이면 아무것도 하지 않는다.</summary>
        public static void Back()
        {
            if (s_instance) s_instance._page = Page.Main;
        }

        /// <summary>GameFlow.Continue가 저장을 읽지 못했을 때: '저장을 읽지 못했다.'를 보이고 저장 파일을 다시 살펴본다.</summary>
        internal static void NoteContinueFailed()
        {
            if (!s_instance) return;
            s_instance._continueFailed = true;
            s_instance._probeDirty = true;
            s_instance._page = Page.Main;
        }

        void Awake() => s_instance = this;

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        void Update()
        {
            if (_curtain && !_quitting && !GameFlow.Loading) _curtain = false;

            bool showing = GameSession.TitleShowing;
            if (showing && !_wasShowing)
            {
                // 다시 보이기 시작함(게임을 켬·멈춤 창 '처음 화면으로'·시험 메뉴 '처음 화면으로'): 첫 화면부터, 저장 파일을 새로 살핀다.
                _page = Page.Main;
                _pending = null;
                _continueFailed = false;
                _probeDirty = true;
            }
            _wasShowing = showing;
            if (!showing)
            {
                _pending = null;
                return;
            }
            if (_probeDirty) Reprobe();

            if (_pending == null) return;
            var action = _pending;
            _pending = null;
            action();
        }

        void Reprobe()
        {
            _probeDirty = false;
            try
            {
                _probe = GameSave.Probe();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[처음 화면] 저장 파일을 살펴보지 못했다: " + e.Message);
                _probe = null;
            }
            _folderLine = null;
            if (!ShowTestMenu) return;
            try
            {
                _folderLine = ShellText.SaveFolderLine(GameSave.Slot.Folder);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[처음 화면] 저장 폴더를 알지 못했다: " + e.Message);
            }
        }

        static bool ShowTestMenu => ShellRules.ShowTestMenu(Application.isEditor, Debug.isDebugBuild);

        bool CanContinue => _probe != null && (_probe.State == SaveProbeState.Ready || _probe.State == SaveProbeState.FromBackup);

        bool Unreadable => _probe != null && (_probe.State == SaveProbeState.Broken || _probe.State == SaveProbeState.Newer);

        /// <summary>단추 일을 다음 Update로 넘긴다(한 프레임에 하나).</summary>
        void Do(Action action)
        {
            if (_pending == null) _pending = action;
        }

        void OnItem(TitleItem item)
        {
            switch (item)
            {
                case TitleItem.Continue:
                    _continueFailed = false;
                    GameFlow.Continue();
                    _curtain = GameFlow.Loading;
                    break;
                case TitleItem.NewGame:
                    if (ShellRules.AskBeforeNewGame(_probe != null && _probe.AnyFile)) _page = Page.ConfirmNew;
                    else StartNewGame();
                    break;
                case TitleItem.Settings:
                    _page = Page.Settings;
                    break;
                case TitleItem.TestMenu:
                    // 불러올 수 없으면 판을 끝내지 않고 처음 화면에 남는다(판을 끝내면 처음 화면이 사라진다).
                    if (!SceneTravel.CanLoad(TestLaunchSession.LauncherPath))
                    {
                        Debug.LogWarning($"[처음 화면] {TestLaunchSession.LauncherPath}를 불러올 수 없어 시험 메뉴로 가지 않는다");
                        break;
                    }
                    GameFlow.GoToTestLauncher();
                    _curtain = GameFlow.Loading;
                    break;
                case TitleItem.Quit:
                    _curtain = true;
                    _quitting = true;
                    GameFlow.Quit(false);
                    break;
            }
        }

        void StartNewGame()
        {
            _page = Page.Main;
            GameFlow.NewGame();
            _curtain = GameFlow.Loading;
        }

        void EnsureArt()
        {
            if (_artTried) return;
            _artTried = true;
            _background = Resources.Load<Texture2D>(BackgroundPath);
            _logo = Resources.Load<Texture2D>(LogoPath);
        }

        void EnsureStyles()
        {
            if (_status != null && _styleSource == DungeonUi.Center) return;
            _styleSource = DungeonUi.Center;
            _status = new GUIStyle(DungeonUi.Center) { fontSize = 17, wordWrap = true };
            _line = new GUIStyle(DungeonUi.SmallCenter) { fontSize = 15 };
            _body = new GUIStyle(DungeonUi.Label) { wordWrap = true };
        }

        void OnGUI()
        {
            bool showing = GameSession.TitleShowing;
            // GameFlow가 장면을 불러오는 동안도 검은 막만 칠한다: 멈춤 창 '처음 화면으로'·F1 '시험 메뉴로'는 멈춤 막을 먼저 걷고 LoadScene을 부르는데,
            // 장면은 다음 프레임에 바뀌어 그사이 마을·던전 화면이 한 프레임 비칠 수 있다(물결 4 반박 검토).
            bool loading = GameFlow.Loading;
            if (!showing && !_curtain && !loading) return;
            var prevMatrix = GUI.matrix;
            var prevColor = GUI.color;
            var prevEnabled = GUI.enabled;
            DungeonUi.Begin();
            EnsureStyles();
            GUI.depth = GuiDepth;
            var full = new Rect(0f, 0f, DungeonUi.Width, DungeonUi.Height);
            DungeonUi.Fill(full, Color.black);

            if (showing && !_curtain && !loading)
            {
                EnsureArt();
                if (_background) GUI.DrawTexture(full, _background, ScaleMode.ScaleAndCrop);
                DrawTitle();
                GUI.enabled = !GameFlow.Loading;
                switch (_page)
                {
                    case Page.Settings: DrawSettings(); break;
                    case Page.ConfirmNew: DrawConfirmNew(); break;
                    default: DrawMain(); break;
                }
                GUI.enabled = prevEnabled;
                if (!string.IsNullOrEmpty(_folderLine))
                    DungeonUi.ShadowLabel(new Rect(16f, DungeonUi.Height - 34f, DungeonUi.Width - 32f, 24f), _folderLine, _line, DungeonUi.BoneDim);
            }

            GUI.enabled = prevEnabled;
            GUI.matrix = prevMatrix;
            GUI.color = prevColor;
        }

        void DrawTitle()
        {
            float w = DungeonUi.Width;
            if (_logo)
            {
                GUI.DrawTexture(new Rect(w * 0.5f - 520f, 80f, 1040f, 220f), _logo, ScaleMode.ScaleToFit);
                return;
            }
            DungeonUi.ShadowLabel(new Rect(0f, 170f, w, 100f), ShellText.GameTitle, DungeonUi.Display, DungeonUi.TitleColor);
        }

        void DrawMain()
        {
            float x = (DungeonUi.Width - ColumnWidth) * 0.5f;
            float y = 400f;

            // 읽지 못함·더 새 판·이어하기 실패 글(4-7) — 단추 위에.
            string status = _continueFailed ? ShellText.ContinueFailed
                : _probe != null && _probe.State == SaveProbeState.Newer ? ShellText.SaveNewer
                : _probe != null && _probe.State == SaveProbeState.Broken ? ShellText.SaveBroken
                : null;
            if (!string.IsNullOrEmpty(status))
            {
                float sw = Mathf.Min(760f, DungeonUi.Width - 32f);
                DungeonUi.ShadowLabel(new Rect((DungeonUi.Width - sw) * 0.5f, 318f, sw, 64f), status, _status, DungeonUi.Rust);
            }

            var items = ShellRules.TitleItems(CanContinue, ShowTestMenu);
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (GUI.Button(new Rect(x, y, ColumnWidth, ButtonHeight), ShellText.TitleItemLabel(item))) Do(() => OnItem(item));
                y += ButtonHeight;
                if (item == TitleItem.Continue && !string.IsNullOrEmpty(_probe.ContinueLine))
                {
                    float lw = Mathf.Min(760f, DungeonUi.Width - 32f);
                    DungeonUi.ShadowLabel(new Rect((DungeonUi.Width - lw) * 0.5f, y + 3f, lw, 22f), _probe.ContinueLine, _line, DungeonUi.BoneDim);
                    y += 26f;
                }
                y += ButtonGap;
            }
        }

        Rect Panel(float width, float height)
            => DungeonUi.KeepOnScreen(new Rect((DungeonUi.Width - width) * 0.5f, Mathf.Max(300f, (DungeonUi.Height - height) * 0.5f + 80f), width, height));

        /// <summary>설정 화면: 제목 + 설정 내용(작성자 D의 SettingsPanel.Draw) + '돌아가기'.</summary>
        void DrawSettings()
        {
            var r = Panel(640f, 540f);
            DungeonUi.Box(r, 0.96f);
            float inner = r.width - Pad * 2f;
            float x = r.x + Pad;
            GUI.Label(new Rect(x, r.y + Pad - 4f, inner, 38f), ShellText.Settings, DungeonUi.Title);
            SettingsPanel.Draw(new Rect(x, r.y + Pad + 46f, inner, r.height - Pad * 2f - 46f - 46f - 12f));
            if (GUI.Button(new Rect(x, r.yMax - Pad - 46f, 220f, 46f), ShellText.Back)) Do(() => _page = Page.Main);
        }

        /// <summary>새로 시작 확인(4-8): 읽을 수 있는 저장이면 '지금 저장(원정 n번째 준비 · 레벨 n)은 …', 읽지 못한 저장이면 '읽지 못한 저장 파일은 따로 복사해 남겨 둔다.'</summary>
        void DrawConfirmNew()
        {
            string body = Unreadable ? ShellText.NewGameBodyUnreadable
                : _probe != null && _probe.Carry != null ? ShellText.NewGameBody(_probe.Carry.Expedition, _probe.Carry.Level)
                : ShellText.NewGameBodyPlain;
            const float width = 580f;
            float inner = width - Pad * 2f;
            _measure.text = body;
            float bodyHeight = Mathf.Max(26f, _body.CalcHeight(_measure, inner));
            float height = Pad + 40f + 10f + bodyHeight + 20f + 46f + Pad;
            var r = Panel(width, height);
            DungeonUi.Box(r, 0.96f);
            float x = r.x + Pad;
            float y = r.y + Pad - 4f;
            GUI.Label(new Rect(x, y, inner, 38f), ShellText.NewGameTitle, DungeonUi.Title);
            y += 50f;
            GUI.Label(new Rect(x, y, inner, bodyHeight), body, _body);
            float half = (inner - 12f) * 0.5f;
            float by = r.yMax - Pad - 46f;
            if (GUI.Button(new Rect(x, by, half, 46f), ShellText.NewGameConfirm)) Do(StartNewGame);
            if (GUI.Button(new Rect(x + half + 12f, by, half, 46f), ShellText.Back)) Do(() => _page = Page.Main);
        }
    }
}
