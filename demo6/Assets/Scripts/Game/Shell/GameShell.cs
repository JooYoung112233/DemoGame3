using Demo6.Core.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Demo6.Game
{
    /// <summary>
    /// 게임 껍데기(기획/저장-처음화면-멈춤창-1차.md 4-2·5-1). 게임을 켤 때(BeforeSceneLoad) 오래 사는 물체 'GameShell'(DontDestroyOnLoad)을 만들고
    /// 처음 화면(TitleScreen)·멈춤 창(PauseMenu)을 붙인다. 새 장면을 만들지 않는다 — 처음 화면은 첫 장면 Town을 붙잡은 채(TownRoot가 짓지 않음) 그 위에 그린다.
    /// Esc는 이 부품 한 곳이 받는다: 실행 차례 −1000이라 모든 장면 부품보다 먼저 돌고, 이때는 아직 어느 창도 이번 Esc를 보지 않았다.
    /// 판단은 Core ShellRules.OnEscape가 한다 — 창이 열려 있으면 물러나고(그 창이 스스로 닫는다), 없으면 멈춤 창을 연다.
    /// 그래서 창을 닫은 그 프레임에는 멈춤 창이 열리지 않고, 다음 Esc에서야 열린다(5-1).
    /// 놀이 시간(GameSession.TickPlayTime)도 여기서 매 프레임 센다(멈춤 창이 열린 동안은 세지 않음, 5-3).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameShell : MonoBehaviour
    {
        public static GameShell Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Instance = null;

        /// <summary>게임을 켤 때 한 번(첫 장면을 불러오기 전): 껍데기 물체를 만들고 처음 화면·멈춤 창 부품을 붙인다(4-2).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (Instance) return;
            var go = new GameObject("GameShell");
            DontDestroyOnLoad(go);
            go.AddComponent<GameShell>();
            go.AddComponent<TitleScreen>();
            go.AddComponent<PauseMenu>();
        }

        /// <summary>
        /// 지금 곳(5-1): 처음 화면이 보이면 Title, 던전 루트가 있으면 Dungeon, 마을 루트가 있으면 Town, 전투 시험장 루트가 있으면 CombatTest, 아니면 None(시험 메뉴 장면 등).
        /// 장면이 바뀌는 프레임에 마을·던전 루트가 겹치면 던전으로 본다(저장하지 않고 떠나기 전에 묻는 쪽, 3-4와 같은 뜻).
        /// </summary>
        public static ShellPlace Place
        {
            get
            {
                if (GameSession.TitleShowing) return ShellPlace.Title;
                if (DungeonRoot.Instance) return ShellPlace.Dungeon;
                if (TownRoot.Instance) return ShellPlace.Town;
                if (CombatTestRoot.Instance) return ShellPlace.CombatTest;
                return ShellPlace.None;
            }
        }

        void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        /// <summary>새 장면을 다 불러왔다(장면 Awake 뒤): GameFlow의 '불러오는 중'을 끈다.</summary>
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => GameFlow.SceneLoaded();

        void Update()
        {
            GameSession.TickPlayTime(Time.unscaledDeltaTime, PauseMenu.IsOpen);

            var kb = Keyboard.current;
            // J = 맡은 일 창(묶음 3 가-3). 열린 창은 스스로 J·Esc로 닫는다.
            if (kb != null && kb.jKey.wasPressedThisFrame && !QuestListWindow.IsOpen && ShellRules.CanOpenQuests(ReadEscState()))
                QuestListWindow.Open(false);
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            switch (ShellRules.OnEscape(ReadEscState()))
            {
                case EscAction.TitleBack: TitleScreen.Back(); break;
                case EscAction.PauseBack: PauseMenu.Back(); break;
                case EscAction.ClosePause: PauseMenu.Close(); break;
                case EscAction.OpenPause: PauseMenu.Open(); break;
            }
        }

        /// <summary>
        /// Esc 판단 상태(5-1 표). 다른 창 = 멈춤 창이 아닌 DungeonUi 창. 입력 막힘 = 창 없이 입력이 막힘(떠나는 암전·밤 카드 대기).
        /// 바쁨 = 창 없이 시간이 멈춤(던전 HoldForNight·HoldForLeaving, 전투 시험장 '멈춤' 단추 — 열면 닫을 때 그쪽 입력 막기를 풀어 버리므로 열지 않음, 5-3),
        /// 마을이 밝아지는 중·떠나는 중(TownRoot.Busy), 던전에서 올라가기·내려가기를 받지 않는 동안(DungeonRoot.CanLeave가 거짓 —
        /// 층에 들어서기 전, 들어서며 밝아지는 중, 말뚝 이동 암전, 쓰러진 뒤 다시 서기까지, 떠나는 중). 이 암전들은 시간을 멈추지도 입력을 막지도 않아
        /// 멈춤 창 뒤에서 실제 시간으로 흐르므로 열지 않는다(5-1 차례 7 '암전 중', 물결 4 반박 검토).
        /// 쓰러진 동안(약 1.5초 + 암전)도 열지 않기로 정했다: 다시 서기는 짧고, 그 사이 멈춤 창이 열리면 다시 서기 암전이 창 뒤에서 이어진다.
        /// </summary>
        static EscState ReadEscState()
        {
            var place = Place;
            bool modalOpen = DungeonUi.ModalOpen;
            var town = place == ShellPlace.Town ? TownRoot.Instance : null;
            var dungeon = place == ShellPlace.Dungeon ? DungeonRoot.Instance : null;
            return new EscState
            {
                Place = place,
                Loading = GameFlow.Loading,
                TitleSubOpen = TitleScreen.SubOpen,
                PauseOpen = PauseMenu.IsOpen,
                PauseSubOpen = PauseMenu.SubOpen,
                OtherModalOpen = modalOpen && DungeonUi.Modal != ShellRules.PauseModal,
                InputBlocked = PlayerInputReader.Blocked && !modalOpen,
                SceneBusy = (TimeScaleService.Paused && !modalOpen)
                            || (town && town.Busy)
                            || (dungeon && !dungeon.CanLeave),
            };
        }
    }
}
