using System;
using System.Collections.Generic;
using System.Text;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;
using Demo6.Core.TestStart;
using Demo6.Core.Town;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo6.Game
{
    /// <summary>
    /// 탐험 기록(3차 초안 7-3 판정 숫자 + 매판 새 탐험 1차 5장 '기록(더함)')과 시험 패널(F1), 층 기록 창("summary", 패널 단추로 연다).
    /// 시간은 멈춘 동안(창·말뚝 메뉴)을 빼고 실제 시간으로 잰다(히트스톱·느린 화면은 실제로 흐른 시간이라 센다).
    /// 재는 것: 층 시간, 전투 몫(DungeonLighting.InCombat), 새 것 간격(등잔·말뚝·새 칸 뺌, 2-8), 가장 긴 빈 구간(싸움·선택·장비 없는 구간),
    /// 되돌아간 걸음(전에 가 본 칸에 다시 들어와 걸은 거리 ÷ 전체), 막다른 곳 빈손(2-1 '헛걸음 0'), 마주침·쓰러짐·레벨·조사율,
    /// 처음 본 칸에서 보낸 몫(칸에 처음 들어가 나갈 때까지 머문 시간 ÷ 층 시간, 원정 누계).
    /// 층마다 장면을 다시 불러오므로 층 한 번 들른 기록(FloorRun: 원정·층·씨앗·시간·몫…)은 정적 목록에 남기고, 플레이를 새로 시작할 때 비운다.
    /// 계단을 쓰거나 바구니로 올라가면 장면이 바뀌므로 기록 창을 열지 않고 보고를 콘솔에 찍는다.
    /// 시험 패널 '갱도 씨앗': 지금 씨앗·지도 글자, '이 씨앗으로 다시'·'씨앗 +1'(DungeonRoot.RebuildWithSeed), '씨앗 10개 보기'(FloorGenerator).
    /// F1 시험 패널은 Unity 편집기에서만 연다(키 배치 1차 0장 5, DevPanelGate). 만든 게임에서는 F1이 아무 일도 하지 않는다.
    /// 판을 쉽게 바꾸는 손잡이·단추(어둠·무적·지도 전부 보기·시야·부채꼴·보는 칸 밖 가리기·적은 부채꼴 안만 보임(끌 때, 시야와 문 1차 13장)·
    /// 곡괭이·올라가기·굴로·의뢰 단추·씨앗 다시 짓기·순간 이동·무기 종류(키 배치 1차 0장 3)·강화석 +50·가방 채우기(재화 쓸 곳 1차 14장))를 쓰면
    /// 이번 판을 '시험 판'으로 적고(TestRunFlag), 판정 기록·층 기록·복사 글에 그 표시를 남긴다. 판정 때 견줘 보는 손잡이(걸음·웅크림 배율·기척·피 발자국·
    /// 정수리 시점·저절로 분해(일반)·저절로 줍기 여유 칸·칸 살핌 경험치·등 뒤 기척 소리·문 자리 비틀기)와 기록 보기·복사·층 기록 창·기록 처음부터·
    /// 씨앗 10개 보기는 적지 않는다.
    /// 앞 플레이나 전투 시험장 패널에서 바꿔 남은 시험 값(무적·버팀 룬 시험·돌충이 약하게·처형 회복·처형 문턱 등)이 기본과 다르면 층 기록을 열 때·옮길 때
    /// TestRunFlag.Marked가 '기본과 다른 시험 값'으로 적는다(반박 검토 Q7).
    /// </summary>
    public sealed class ExplorationLog : MonoBehaviour
    {
        public const string SummaryModal = "summary";

        // 7-3 합격 기준.
        public const float TargetFloorSeconds = 420f;
        /// <summary>2층 첫 탐험 목표(매판 새 탐험 1차 2-3 '2층(고른 지도, 8분)').</summary>
        public const float SecondFloorSeconds = 480f;
        public const float FloorTolerance = 0.25f;
        public const float CombatShareMin = 0.25f;
        public const float CombatShareMax = 0.35f;
        public const float NewThingMedianMax = 30f;
        public const float EmptyGapMax = 60f;
        public const float BacktrackMax = 0.30f;
        /// <summary>매판 새 탐험 5장: 원정 시간 가운데 처음 본 칸에서 보낸 몫 기준.</summary>
        public const float FreshShareMin = 0.60f;
        /// <summary>매판 새 탐험 5장 '쓰러진 직후 그만둠': 쓰러진 뒤 이 시간(실제 초) 안에 바구니로 올라가면 센다.</summary>
        public const float QuitAfterDownSeconds = 30f;

        /// <summary>같은 순간에 겹친 새 것(궤짝 열기 + 장비 떨어짐)은 하나로 센다.</summary>
        const float MergeWindow = 1.5f;
        /// <summary>한 프레임에 이보다 멀리 움직이면 순간 이동(말뚝 이동·다시 서기·시험 이동)으로 보고 걸음에 넣지 않는다.</summary>
        const float TeleportJump = 3f;
        /// <summary>멈춤 없이 한 프레임이 이보다 길면 끊긴 것(편집기 멈춤 등)으로 보고 자른다.</summary>
        const float MaxFrame = 0.5f;
        const float PanelWidth = 420f;
        const int RecentMax = 14;
        const int SeedPreviewCount = 10;
        /// <summary>시험 패널 '강화석 +50'(재화 쓸 곳 1차 14장).</summary>
        const int TestStones = 50;
        /// <summary>'가방 채우기' 장비 굴림 흐름 번호(피해 7·처치 보상 23·치명 31·궤짝 41과 겹치지 않음).</summary>
        const ulong FillBagStream = 67;

        static readonly string[] Questions =
        {
            "어둠 속을 걸을 때 '무섭고 궁금하다' 쪽인가, '답답하다' 쪽인가? (첫 판에만 답함)",
            "걷기만 하는 구간이 지루하지 않은가?",
            "곡괭이를 얻고 금 간 벽을 열었는가? 두 번째 원정 30초 안에 '바뀌었다'를 알아챘는가, 무엇으로 알았나?",
            "한 마리 한 마리가 기억에 남는가?",
            "마무리와 검풍을 아껴 쓰게 되는가, 아니면 늘 첫 타에 쓰는가?",
            "둥지가 아닌 보통 싸움이 끝날 때도 시원한가?",
            "M0a 시험장 10분과 1층 10분 가운데 다음 날 다시 켜고 싶은 쪽은? (A/B)",
            "상자에서 나온 무기를 끼고 세졌다고 느꼈나? 레벨업 뒤 무엇이 달라졌는지 말할 수 있나?",
            "이야기 없는 2층도 끝까지 돌고 싶은가?",
            "계단을 내려갈 때 '한 단 내려간다'는 느낌이 드는가?",
            "세 번째 원정을 스스로 시작했는가? 다시 밝히는 게 설렜나, 귀찮았나?",
            "갱도가 왜 바뀌는지 한 줄로 말할 수 있나?",
        };

        /// <summary>단일 칸 글꼴 후보(글자 지도가 줄 맞게). 앞에서부터 설치된 것을 쓴다.</summary>
        static readonly string[] MonoNames = { "Consolas", "D2Coding", "Cascadia Mono", "Courier New", "Lucida Console" };
        /// <summary>단일 칸 글꼴에 없는 한글을 채울 뒤 글꼴.</summary>
        static readonly string[] HangulFallbackNames = { "Malgun Gothic", "맑은 고딕", "Gulim", "굴림" };

        /// <summary>층 한 번 들른 기록이 어떻게 끝났나.</summary>
        public enum FloorRunEnd
        {
            /// <summary>아직 그 층에 있다.</summary>
            Live,
            /// <summary>계단(또는 '한 층 더 내려가기')으로 내려감.</summary>
            Stairs,
            /// <summary>바구니로 올라감.</summary>
            Ascend,
            /// <summary>계단·올라가기 없이 장면이 바뀜(시험 패널 씨앗 다시 짓기 등).</summary>
            Left,
        }

        /// <summary>
        /// 층 한 번 들른 기록(매판 새 탐험 1차 5장 '기록(더함)': 원정마다 씨앗과 함께). 장면을 다시 불러와도 남도록 정적 목록(Runs)에 둔다.
        /// 지금 층 기록은 그 층 ExplorationLog가 살아 있는 값으로 고쳐 쓴다.
        /// </summary>
        public sealed class FloorRun
        {
            public int Expedition;
            public int Floor;
            public ulong Seed;
            public bool FirstVisit;
            /// <summary>오우거 굴 장면(고정 돌방, 씨앗 없음).</summary>
            public bool Den;
            public ArrivalKind Arrival;
            /// <summary>바구니로 내려와 줄 끝(가장 깊은 켠 승강장)에서 시작했나.</summary>
            public bool FromRopeEnd;
            public FloorRunEnd End;
            /// <summary>층 시간(멈춘 시간 뺌)과 그 가운데 처음 본 칸에서 보낸 시간.</summary>
            public float Seconds;
            public float FreshSeconds;
            /// <summary>새 것 간격 중앙값(없으면 -1)과 새 것 수.</summary>
            public float MedianNew = -1f;
            public int NewThings;
            public float LongestGap;
            public int DeadEndEmpty;
            public int Deaths;
            public int Encounters;
            /// <summary>이 층을 도는 동안(또는 그 전에) 시험 도구를 썼나(TestRunFlag, 4장 Q7). 한 번 켜지면 끄지 않는다.</summary>
            public bool Test;
            /// <summary>바구니 출발을 이미 셌나(같은 층에 층 시작 알림이 두 번 와도 한 번만).</summary>
            internal bool StartCounted;

            public float FreshShare => Seconds > 0.01f ? FreshSeconds / Seconds : 0f;
        }

        static readonly List<FloorRun> s_runs = new List<FloorRun>();
        static int s_basketStarts;
        static int s_ropeEndStarts;
        static int s_ascents;
        static int s_ascentsAfterDown;
        static float s_lastDownRealtime = -999f;

        /// <summary>이번 플레이의 층 기록(오래된 것부터). 장면을 다시 불러와도 남는다.</summary>
        public static IReadOnlyList<FloorRun> Runs => s_runs;

        public static ExplorationLog Instance { get; private set; }

        /// <summary>F1 패널이 보이는가(큰 지도가 패널을 피해 그린다).</summary>
        public bool PanelVisible { get; private set; }
        /// <summary>마우스가 F1 패널 위에 있는가(패널 버튼을 누를 때 공격을 막는 데 쓸 수 있다).</summary>
        public bool PointerOverPanel { get; private set; }
        /// <summary>패널이 차지하는 기준 좌표 너비(여백 포함). 안 보이면 0.</summary>
        public float PanelReservedWidth => PanelVisible && !_hidden ? PanelWidth + 24f : 0f;

        public float FloorSeconds { get; private set; }
        public float CombatSeconds { get; private set; }
        /// <summary>계단을 처음 쓴 때의 층 시간. 아직이면 -1.</summary>
        public float FirstStairsSeconds { get; private set; } = -1f;
        /// <summary>이 층이 끝난 때(계단 또는 바구니로 올라가기)의 층 시간. 아직이면 -1.</summary>
        public float FloorEndSeconds { get; private set; } = -1f;
        public float LongestEmptyGap { get; private set; }
        public float CurrentEmptyGap { get; private set; }
        public float WalkedDistance { get; private set; }
        public float BacktrackDistance { get; private set; }
        /// <summary>처음 본 칸에서 보낸 시간(칸에 처음 들어가 나갈 때까지, 멈춘 시간 뺌).</summary>
        public float FreshSeconds { get; private set; }
        public int EncounterCount { get; private set; }
        public int Deaths { get; private set; }
        /// <summary>새 것(겹친 순간은 하나로) 개수.</summary>
        public int NewThingCount => _newTimes.Count;
        /// <summary>
        /// 막다른 곳 빈손(7-3): 받을 것이 하나도 없던 막다른 칸 수(지도 설계 검사, 기준 0).
        /// 다 가져간 뒤 다시 들른 칸은 되돌아감 쪽 이야기라 따로 보이기만 한다.
        /// </summary>
        public int DeadEndEmpty => _deadEndNone.Count;
        public float CombatShare => FloorSeconds > 0.01f ? CombatSeconds / FloorSeconds : 0f;
        public float BacktrackShare => WalkedDistance > 0.01f ? BacktrackDistance / WalkedDistance : 0f;

        readonly List<float> _newTimes = new List<float>();
        readonly List<string> _recent = new List<string>();
        readonly List<string> _deadEndNone = new List<string>();
        readonly List<string> _deadEndLooted = new List<string>();
        readonly List<(string text, int verdict)> _lines = new List<(string, int)>();
        readonly List<(string text, int verdict)> _runLines = new List<(string, int)>();

        bool _inRevisit;
        /// <summary>칸마다 처음 둘러보며 걸은 거리. 문턱만 넘었다 돌아온 칸은 다시 들어가도 아직 '되돌아감'이 아니다.</summary>
        readonly Dictionary<string, float> _explored = new Dictionary<string, float>();
        string _cellId;
        const float ExploredEnough = 8f;
        bool _hasLastPos;
        Vector2 _lastPos;
        bool _summaryPending;
        Rect _panelRect;
        Vector2 _scroll;
        Vector2 _summaryScroll;
        GUIStyle _smallButton;

        /// <summary>지금 칸에 처음 들어온 뒤 아직 나가지 않았다.</summary>
        bool _inFresh;
        FloorRunEnd _endedBy = FloorRunEnd.Live;
        /// <summary>이 장면(층)의 기록. 층 시작 알림(FloorEntered)이나 첫 1초에 만든다.</summary>
        FloorRun _run;
        /// <summary>결과 창·밤 카드·승강장 고르기 동안 패널·기록 창을 숨긴다(프레임마다 Update에서 정해 OnGUI 사건 사이에 바뀌지 않게).</summary>
        bool _hidden;
        /// <summary>패널 단추가 고른 일(씨앗 다시 짓기·올라가기). OnGUI 도중 화면을 바꾸지 않게 다음 Update에서 한다.</summary>
        Action _pendingAction;
        bool _wantSeedPreview;
        bool _wantFoldPreview;
        readonly List<string> _seedPreview = new List<string>();
        string _seedPreviewTitle = "";
        string _seedPreviewText = "";
        Font _mono;
        bool _monoTried;
        GUIStyle _monoStyle;

        // 판정 줄 색: 통과 / 벗어남 / 아직 / 참고.
        const int Pass = 1;
        const int Fail = 0;
        const int Pending = -1;
        const int Info = 2;

        /// <summary>플레이를 새로 시작할 때 층 기록을 비운다(도메인 다시 불러오기가 꺼져 있음). 장면을 다시 불러올 때는 남긴다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuns()
        {
            Instance = null;
            s_runs.Clear();
            s_basketStarts = 0;
            s_ropeEndStarts = 0;
            s_ascents = 0;
            s_ascentsAfterDown = 0;
            s_lastDownRealtime = -999f;
        }

        void Awake()
        {
            Instance = this;
            DungeonEvents.Discovered += OnDiscovered;
            DungeonEvents.GroupAwake += OnGroupAwake;
            DungeonEvents.ChoiceMade += OnChoice;
            DungeonEvents.GearDropped += OnGearDropped;
            DungeonEvents.GearEquipped += OnGearEquipped;
            DungeonEvents.CellEntered += OnCellEntered;
            DungeonEvents.StairsUsed += OnStairsUsed;
            DungeonEvents.FloorEntered += OnFloorEntered;
            DungeonEvents.ExpeditionEnding += OnExpeditionEnding;
            CombatEvents.PlayerDowned += OnPlayerDowned;
        }

        void OnDestroy()
        {
            DungeonEvents.Discovered -= OnDiscovered;
            DungeonEvents.GroupAwake -= OnGroupAwake;
            DungeonEvents.ChoiceMade -= OnChoice;
            DungeonEvents.GearDropped -= OnGearDropped;
            DungeonEvents.GearEquipped -= OnGearEquipped;
            DungeonEvents.CellEntered -= OnCellEntered;
            DungeonEvents.StairsUsed -= OnStairsUsed;
            DungeonEvents.FloorEntered -= OnFloorEntered;
            DungeonEvents.ExpeditionEnding -= OnExpeditionEnding;
            CombatEvents.PlayerDowned -= OnPlayerDowned;
            // 계단·올라가기 없이 장면이 바뀜(씨앗 다시 짓기 등): 지금까지 잰 값으로 닫는다.
            if (_run != null && _run.End == FloorRunEnd.Live)
            {
                Snapshot(_run);
                _run.End = FloorRunEnd.Left;
            }
            if (_mono) Destroy(_mono);
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            var card = NightCard.Instance;
            _hidden = card && card.Showing;
            var kb = Keyboard.current;
            if (kb != null)
            {
                // F1(키 배치 1차 0장 5): 닫기는 바로, 열기는 DevPanelGate(Unity 편집기에서만 연다. 만든 게임에서는 아무 일도 하지 않는다).
                if (kb.f1Key.wasPressedThisFrame)
                {
                    if (PanelVisible) PanelVisible = false;
                    else if (DevPanelGate.RequestOpen(() => PanelVisible = true)) PanelVisible = true;
                }
                if (kb.escapeKey.wasPressedThisFrame && DungeonUi.Modal == SummaryModal) DungeonUi.Close(SummaryModal);
            }
            if (_pendingAction != null)
            {
                var action = _pendingAction;
                _pendingAction = null;
                action();
            }
            if (_wantFoldPreview)
            {
                _wantFoldPreview = false;
                _seedPreview.Clear();
            }
            if (_wantSeedPreview)
            {
                _wantSeedPreview = false;
                BuildSeedPreview();
            }
            if (_summaryPending && !_hidden && DungeonUi.TryOpen(SummaryModal)) _summaryPending = false;
            UpdatePointer();
            Measure();
        }

        void UpdatePointer()
        {
            _panelRect = new Rect(DungeonUi.Width - PanelWidth - 12f, 12f, PanelWidth, DungeonUi.Height - 24f);
            var mouse = Mouse.current;
            if (!PanelVisible || _hidden || mouse == null)
            {
                PointerOverPanel = false;
                return;
            }
            Vector2 m = mouse.position.ReadValue();
            PointerOverPanel = _panelRect.Contains(new Vector2(m.x, Screen.height - m.y) / DungeonUi.Scale);
        }

        void Measure()
        {
            var root = DungeonRoot.Instance;
            if (!root || !root.Player || TimeScaleService.Paused) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxFrame);
            FloorSeconds += dt;
            if (_inFresh) FreshSeconds += dt;
            // 층 시작 알림이 오지 않아도(흐름이 아직 안 부름) 시간이 흐르기 시작하면 기록을 연다.
            if (_run == null && FloorSeconds >= 1f) EnsureRun();

            var lighting = DungeonLighting.Instance;
            bool combat = lighting && lighting.InCombat;
            if (combat)
            {
                CombatSeconds += dt;
                CurrentEmptyGap = 0f;
            }
            else
            {
                CurrentEmptyGap += dt;
                if (CurrentEmptyGap > LongestEmptyGap) LongestEmptyGap = CurrentEmptyGap;
            }

            var player = root.Player;
            Vector2 pos = player.Position;
            if (_hasLastPos && !player.IsDown)
            {
                float d = (pos - _lastPos).magnitude;
                if (d < TeleportJump)
                {
                    WalkedDistance += d;
                    if (_inRevisit) BacktrackDistance += d;
                    else if (_cellId != null)
                    {
                        _explored.TryGetValue(_cellId, out float had);
                        _explored[_cellId] = had + d;
                    }
                }
            }
            _lastPos = pos;
            _hasLastPos = true;
        }

        /// <summary>기록을 처음부터(시험 패널). 지도·연 곳은 그대로다. 지난 층 기록(Runs)은 남긴다.</summary>
        public void ResetMeasures()
        {
            FloorSeconds = 0f;
            CombatSeconds = 0f;
            FirstStairsSeconds = -1f;
            FloorEndSeconds = -1f;
            _endedBy = FloorRunEnd.Live;
            LongestEmptyGap = 0f;
            CurrentEmptyGap = 0f;
            WalkedDistance = 0f;
            BacktrackDistance = 0f;
            FreshSeconds = 0f;
            EncounterCount = 0;
            Deaths = 0;
            _newTimes.Clear();
            _recent.Clear();
            _deadEndNone.Clear();
            _deadEndLooted.Clear();
            _explored.Clear();
            _hasLastPos = false;
        }

        /// <summary>새 것 간격의 중앙값(시작 → 첫 새 것도 한 간격). 없으면 -1.</summary>
        public float MedianNewInterval()
        {
            if (_newTimes.Count == 0) return -1f;
            var gaps = new List<float>(_newTimes.Count);
            float prev = 0f;
            foreach (float t in _newTimes)
            {
                gaps.Add(t - prev);
                prev = t;
            }
            gaps.Sort();
            int n = gaps.Count;
            return n % 2 == 1 ? gaps[n / 2] : (gaps[n / 2 - 1] + gaps[n / 2]) * 0.5f;
        }

        // ── 층 기록(매판 새 탐험 5장) ────────────────────────────

        FloorRun NewRun(DungeonRoot root, int floor, bool firstVisit, ArrivalKind arrival)
        {
            var run = new FloorRun
            {
                Expedition = root ? root.Expedition : 1,
                Floor = floor,
                Seed = root ? root.Seed : 0UL,
                FirstVisit = firstVisit,
                Den = root && root.IsDen,
                Arrival = arrival,
                Test = TestRunFlag.Marked,
            };
            s_runs.Add(run);
            return run;
        }

        FloorRun EnsureRun()
        {
            if (_run != null) return _run;
            var root = DungeonRoot.Instance;
            if (!root) return null;
            _run = NewRun(root, root.Floor, root.FirstVisit, root.Arrival);
            return _run;
        }

        /// <summary>지금 층의 살아 있는 값을 기록에 옮긴다.</summary>
        void Snapshot(FloorRun run)
        {
            run.Seconds = FloorSeconds;
            run.FreshSeconds = FreshSeconds;
            run.MedianNew = MedianNewInterval();
            run.NewThings = NewThingCount;
            run.LongestGap = LongestEmptyGap;
            run.DeadEndEmpty = DeadEndEmpty;
            run.Deaths = Deaths;
            run.Encounters = EncounterCount;
            if (TestRunFlag.Marked) run.Test = true;
        }

        /// <summary>층 끝(계단·올라가기): 층 시간을 적고 기록을 닫는다. 처음 한 번만.</summary>
        void EndFloor(FloorRunEnd end)
        {
            if (FloorEndSeconds < 0f)
            {
                FloorEndSeconds = FloorSeconds;
                _endedBy = end;
            }
            var run = EnsureRun();
            if (run == null || run.End != FloorRunEnd.Live) return;
            Snapshot(run);
            run.End = end;
        }

        void OnFloorEntered(int floor, bool firstVisit, ArrivalKind arrival)
        {
            var root = DungeonRoot.Instance;
            if (_run != null && _run.End == FloorRunEnd.Live && _run.Floor == floor)
            {
                // 첫 1초에 먼저 연 기록: 알림 값으로 고친다.
                _run.FirstVisit = firstVisit;
                _run.Arrival = arrival;
                if (root) _run.Seed = root.Seed;
            }
            else
            {
                if (_run != null && _run.End == FloorRunEnd.Live)
                {
                    Snapshot(_run);
                    _run.End = FloorRunEnd.Left;
                }
                _run = NewRun(root, floor, firstVisit, arrival);
            }
            if (arrival == ArrivalKind.Basket && !_run.StartCounted)
            {
                _run.StartCounted = true;
                var data = ProfileCarry.Data;
                _run.FromRopeEnd = data != null && floor == data.RopeEnd(FloorRecipe.MaxTestFloor);
                s_basketStarts++;
                if (_run.FromRopeEnd) s_ropeEndStarts++;
            }
        }

        // ── 사건 ─────────────────────────────────────────────

        void OnDiscovered(DiscoveryKind kind, Vector2 pos, string label)
        {
            // 2-8: 숫자만 맞추는 '등잔 +1' 꼼수를 막으려고 등잔·말뚝은 새 것에서 뺀다. 새 칸·층 완전 탐험도 뺀다.
            switch (kind)
            {
                case DiscoveryKind.NewCell:
                case DiscoveryKind.WallLamp:
                case DiscoveryKind.Stake:
                case DiscoveryKind.FloorComplete:
                    return;
            }
            AddNewThing(string.IsNullOrEmpty(label) ? KindName(kind) : label);
        }

        void OnGroupAwake(int group)
        {
            EncounterCount++;
            AddNewThing("마주침");
        }

        void OnChoice(string text)
        {
            CurrentEmptyGap = 0f;
            AddNewThing("선택: " + text);
        }

        void OnGearDropped(string text)
        {
            // 가방에서 내려놓거나 넘침 끝에서 벗어 발밑에 둔 장비가 내려앉은 것은 새 장비가 아니다(반박 검토: 빈 구간·새 것 간격을 거짓으로 끊지 않게).
            var inv = Inventory.Instance;
            if (inv && inv.TakePutDownLanding(text)) return;
            CurrentEmptyGap = 0f;
            AddNewThing("장비: " + text);
        }

        void OnGearEquipped(string text) => CurrentEmptyGap = 0f;

        void OnPlayerDowned()
        {
            Deaths++;
            s_lastDownRealtime = Time.realtimeSinceStartup;
        }

        void OnCellEntered(DungeonCell cell, bool first)
        {
            // 처음 본 칸에서 보낸 몫: 처음 들어온 칸이면 나갈 때까지 센다.
            _inFresh = first && cell != null;
            _cellId = cell != null ? cell.Id : null;
            _inRevisit = !first && cell != null && _explored.TryGetValue(cell.Id, out float walked) && walked >= ExploredEnough;
            if (cell == null || !IsDeadEnd(cell)) return;
            var root = DungeonRoot.Instance;
            if (!root || root.State == null) return;
            bool any = false;
            foreach (var e in root.State.OneTime.Values)
            {
                if (BigMap.CellOf(e) != cell) continue;
                any = true;
                if (!e.Done) return; // 아직 받을 것이 있다.
            }
            var list = any ? _deadEndLooted : _deadEndNone;
            if (!list.Contains(cell.Id)) list.Add(cell.Id);
        }

        /// <summary>계단을 씀: 장면이 바로 바뀌므로 기록 창을 열지 않고 콘솔에 보고만 찍는다.</summary>
        void OnStairsUsed()
        {
            if (FirstStairsSeconds < 0f) FirstStairsSeconds = FloorSeconds;
            EndFloor(FloorRunEnd.Stairs);
            Debug.Log(BuildReport());
        }

        /// <summary>바구니로 올라가기 직전: 층 기록을 닫고, 쓰러진 직후(30초 안) 올라갔는지 센 뒤 콘솔에 보고를 찍는다.</summary>
        void OnExpeditionEnding()
        {
            s_ascents++;
            if (Time.realtimeSinceStartup - s_lastDownRealtime <= QuitAfterDownSeconds) s_ascentsAfterDown++;
            EndFloor(FloorRunEnd.Ascend);
            Debug.Log(BuildReport());
        }

        void AddNewThing(string label)
        {
            _recent.Add(DungeonUi.Clock(FloorSeconds) + "  " + label);
            if (_recent.Count > RecentMax) _recent.RemoveAt(0);
            if (_newTimes.Count > 0 && FloorSeconds - _newTimes[_newTimes.Count - 1] < MergeWindow) return;
            _newTimes.Add(FloorSeconds);
        }

        /// <summary>막다른 갈래(2-1): 길이 하나뿐인 칸. 입구·계단 앞은 갈래가 아니라 뺀다.</summary>
        static bool IsDeadEnd(DungeonCell cell) =>
            cell.Edges.Count == 1 && cell.Piece != PieceKind.Entrance && cell.Piece != PieceKind.StairsRoom;

        static string KindName(DiscoveryKind kind)
        {
            switch (kind)
            {
                case DiscoveryKind.WoodChest: return "나무 궤짝";
                case DiscoveryKind.IronChest: return "쇠 궤짝";
                case DiscoveryKind.Shortcut: return "지름길";
                case DiscoveryKind.Story: return "이야기 물건";
                case DiscoveryKind.HiddenRoom: return "숨은 방";
                case DiscoveryKind.Event: return "사건";
                case DiscoveryKind.Ability: return "새 능력";
                case DiscoveryKind.Ore: return "광맥";
                case DiscoveryKind.Safe: return "금고";
                default: return kind.ToString();
            }
        }

        static string ArrivalName(ArrivalKind arrival)
        {
            switch (arrival)
            {
                case ArrivalKind.Basket: return "바구니";
                case ArrivalKind.Stairs: return "계단";
                case ArrivalKind.Rebuild: return "다시 지음";
                default: return "첫 시작";
            }
        }

        static string EndName(FloorRunEnd end)
        {
            switch (end)
            {
                case FloorRunEnd.Stairs: return "계단";
                case FloorRunEnd.Ascend: return "올라감";
                case FloorRunEnd.Left: return "끊김";
                default: return "진행 중";
            }
        }

        static string EndUntil(FloorRunEnd end) => end == FloorRunEnd.Ascend ? "올라가기까지" : "계단까지";

        /// <summary>층 첫 탐험 목표 시간: 1층 7분(7-3), 2층 8분(매판 새 탐험 2-3).</summary>
        static float TargetSecondsFor(int floor) => floor == 2 ? SecondFloorSeconds : TargetFloorSeconds;

        // ── 판정 줄 ──────────────────────────────────────────

        static int Judge(bool ok) => ok ? Pass : Fail;

        static bool InFloorRange(float seconds, float target) =>
            seconds >= target * (1f - FloorTolerance) && seconds <= target * (1f + FloorTolerance);

        void BuildLines()
        {
            _lines.Clear();
            var root = DungeonRoot.Instance;
            if (_run != null && _run.End == FloorRunEnd.Live) Snapshot(_run);
            // 4장 Q7: 시험 도구를 쓴 판이면 맨 위에 적는다(숫자는 그대로 재되 재미 판정에는 참고만).
            if (TestRunFlag.Marked) _lines.Add(($"{TestRunFlag.Label} — 시험 도구를 쓴 판이라 판정은 참고만", Info));
            // 가방 손잡이(㉠·㉡)는 시험 판으로 적지 않지만, 앞 플레이에서 바꿔 남았을 수 있어 기본과 다르면 걸음 줄처럼 적는다(반박 검토).
            string bagKnobs = BagKnobsLine();
            if (bagKnobs != null) _lines.Add((bagKnobs, Info));

            int floor = root ? root.Floor : 1;
            float target = TargetSecondsFor(floor);
            float lo = target * (1f - FloorTolerance);
            float hi = target * (1f + FloorTolerance);
            string range = $"기준 {target / 60f:0}분 ±25% ({DungeonUi.Clock(lo)}~{DungeonUi.Clock(hi)})";
            if (FloorEndSeconds >= 0f)
                _lines.Add(($"{floor}층 탐험 {DungeonUi.Clock(FloorEndSeconds)} ({EndUntil(_endedBy)}, 지금 {DungeonUi.Clock(FloorSeconds)}) · {range}",
                    Judge(InFloorRange(FloorEndSeconds, target))));
            else
                _lines.Add(($"{floor}층 탐험 시간 {DungeonUi.Clock(FloorSeconds)} (계단·올라가기 전) · {range}", Pending));

            _lines.Add(($"전투 몫 {CombatShare * 100f:0}% (전투 {DungeonUi.Clock(CombatSeconds)}) · 기준 25~35%",
                FloorSeconds >= 60f ? Judge(CombatShare >= CombatShareMin && CombatShare <= CombatShareMax) : Pending));

            float median = MedianNewInterval();
            if (median < 0f) _lines.Add(("새 것 아직 없음 (등잔·말뚝·새 칸 뺌) · 기준 간격 중앙 30초 이하", Pending));
            else
                _lines.Add(($"새 것 간격 중앙 {median:0}초 ({NewThingCount}번, 등잔·말뚝·새 칸 뺌) · 기준 30초 이하",
                    NewThingCount >= 2 ? Judge(median <= NewThingMedianMax) : Pending));

            _lines.Add(($"가장 긴 빈 구간 {LongestEmptyGap:0}초 (지금 {CurrentEmptyGap:0}초, 싸움·선택·장비 없는 구간) · 기준 60초 이하",
                Judge(LongestEmptyGap <= EmptyGapMax)));

            _lines.Add(($"되돌아간 걸음 {BacktrackShare * 100f:0}% ({BacktrackDistance:0} / {WalkedDistance:0}유닛) · 기준 30% 이하",
                WalkedDistance >= 20f ? Judge(BacktrackShare <= BacktrackMax) : Pending));

            string where = _deadEndNone.Count + _deadEndLooted.Count > 0 ? " — " + string.Join(", ", AllDeadEnds()) : "";
            _lines.Add(($"막다른 곳 빈손 {DeadEndEmpty} (참고: 다 가져간 뒤 다시 들름 {_deadEndLooted.Count}){where} · 기준 0",
                Judge(DeadEndEmpty == 0)));

            int level = PlayerProgress.Instance ? PlayerProgress.Instance.Level : 1;
            float survey = root && root.State != null && root.World != null ? root.State.Survey(root.World.Cells.Count) : 0f;
            int expedition = root && root.State != null ? root.State.Expedition : 1;
            _lines.Add(($"마주침 {EncounterCount}번 · 쓰러짐 {Deaths}번 · 레벨 {level} · 조사 {survey * 100f:0}% · 원정 {expedition}번째", Info));

            // 매판 새 탐험 1차 5장 '기록(더함)'.
            AddFreshShareLine(expedition);
            AddFloorOneLines();
            _lines.Add(($"줄 끝에서 시작한 원정 {s_ropeEndStarts} / 바구니로 내려간 원정 {s_basketStarts} · 쓰러진 뒤 30초 안에 올라감 {s_ascentsAfterDown} / 올라감 {s_ascents} (기록만)", Info));

            BuildRunLines();
        }

        /// <summary>① 원정 시간 가운데 처음 본 칸에서 보낸 몫(이번 원정의 층들 누계, 기준 60% 이상).</summary>
        void AddFreshShareLine(int expedition)
        {
            float fresh = 0f;
            float total = 0f;
            foreach (var r in s_runs)
            {
                if (r.Expedition != expedition) continue;
                fresh += r.FreshSeconds;
                total += r.Seconds;
            }
            if (_run == null)
            {
                // 아직 기록을 열지 않은 지금 층.
                fresh += FreshSeconds;
                total += FloorSeconds;
            }
            float share = total > 0.01f ? fresh / total : 0f;
            _lines.Add(($"처음 본 칸에서 보낸 몫 {share * 100f:0}% (원정 {expedition}번째 누계 {DungeonUi.Clock(fresh)} / {DungeonUi.Clock(total)}) · 기준 60% 이상",
                total >= 60f ? Judge(share >= FreshShareMin) : Pending));
        }

        /// <summary>② 원정 2~4의 1층 탐험 시간(계단 또는 올라가기까지, 기준 7분 ±25%).</summary>
        void AddFloorOneLines()
        {
            bool any = false;
            foreach (var r in s_runs)
            {
                if (r.Floor != 1 || r.Expedition < 2 || r.Expedition > 4) continue;
                if (r.End != FloorRunEnd.Stairs && r.End != FloorRunEnd.Ascend) continue;
                any = true;
                _lines.Add(($"원정 {r.Expedition}번째 1층 탐험 {DungeonUi.Clock(r.Seconds)} ({EndUntil(r.End)}, 씨앗 {r.Seed}) · 기준 7분 ±25%",
                    Judge(InFloorRange(r.Seconds, TargetFloorSeconds))));
            }
            if (!any) _lines.Add(("원정 2~4번째의 1층 탐험 시간 — 아직 없음 (계단 또는 올라가기까지) · 기준 7분 ±25%", Pending));
        }

        /// <summary>③ 층 기록마다 씨앗과 함께: 시간, 처음 본 칸 몫, 새 것 간격 중앙값, 가장 긴 빈 구간, 막다른 곳 빈손(3차 기준 그대로).</summary>
        void BuildRunLines()
        {
            _runLines.Clear();
            foreach (var r in s_runs)
            {
                string median = r.MedianNew < 0f ? "없음" : $"{r.MedianNew:0}초";
                string start = ArrivalName(r.Arrival) + (r.FromRopeEnd ? "·줄 끝" : "");
                string test = r.Test ? "[" + TestRunMark.Title + "] " : "";
                _runLines.Add(($"{test}원정 {r.Expedition} · {r.Floor}층 · 씨앗 {r.Seed} · {MapKind(r.Den, r.FirstVisit)} · {start} — " +
                               $"{DungeonUi.Clock(r.Seconds)} {EndName(r.End)} · 처음 본 칸 {r.FreshShare * 100f:0}% · 새 것 간격 중앙 {median}({r.NewThings}번) · " +
                               $"가장 긴 빈 구간 {r.LongestGap:0}초 · 막다른 곳 빈손 {r.DeadEndEmpty} · 쓰러짐 {r.Deaths}", RunVerdict(r)));
            }
        }

        /// <summary>층 기록·씨앗 절의 지도 종류: 오우거 굴 / 고른 지도(처음 밟는 원정) / 새 갱도.</summary>
        static string MapKind(bool den, bool firstVisit) => den ? OgreDen.Name : firstVisit ? "고른 지도" : "새 갱도";

        static int RunVerdict(FloorRun r)
        {
            bool ok = (r.NewThings < 2 || r.MedianNew <= NewThingMedianMax) && r.LongestGap <= EmptyGapMax && r.DeadEndEmpty == 0;
            if (r.End == FloorRunEnd.Live) return ok ? Pending : Fail;
            if (r.End == FloorRunEnd.Left && r.Seconds < 60f) return Info;
            return Judge(ok);
        }

        IEnumerable<string> AllDeadEnds()
        {
            foreach (var id in _deadEndNone) yield return id + "(빈 곳)";
            foreach (var id in _deadEndLooted) yield return id + "(다시 들름)";
        }

        static string Mark(int verdict) => verdict == Pass ? "통과" : verdict == Fail ? "벗어남" : verdict == Pending ? "아직" : "참고";

        /// <summary>복사·콘솔용 글 기록.</summary>
        public string BuildReport()
        {
            BuildLines();
            var root = DungeonRoot.Instance;
            int floor = root ? root.Floor : 1;
            var sb = new StringBuilder();
            sb.AppendLine($"[{floor}층 탐험 기록 (3차 초안 7-3 · 매판 새 탐험 1차 5장){(TestRunFlag.Marked ? " · " + TestRunFlag.Label : "")}]");
            // 걸음 비교(1-2 '지금'과 B를 한 판씩)를 기록끼리 견줄 수 있게 지금 걸음 안을 적는다.
            sb.AppendLine($"걸음 {PaceName()} (나 {Tuning.MoveSpeedScale:0.00} · 적 {Tuning.EnemyMoveScale:0.00} · 빠른 걸음 {(ExploreWalk.FastEverywhere ? "모든 칸" : "아는 길만")})");
            foreach (var line in _lines) sb.Append("- [").Append(Mark(line.verdict)).Append("] ").AppendLine(line.text);
            sb.AppendLine("층 기록 (원정·씨앗별):");
            if (_runLines.Count == 0) sb.AppendLine("  아직 없음");
            foreach (var line in _runLines) sb.Append("- [").Append(Mark(line.verdict)).Append("] ").AppendLine(line.text);
            sb.AppendLine("최근 새 것:");
            foreach (var r in _recent) sb.Append("  ").AppendLine(r);
            return sb.ToString();
        }

        // ── 씨앗 10개 보기 ───────────────────────────────────────

        /// <summary>
        /// 지금 층을 씨앗 +1 ~ +10으로 '새 갱도'(처음 밟는 층이 아님)로 지어 글자 지도를 콘솔·패널에 보인다(5장 '시험 패널').
        /// 능력·받은 것은 꾸러미(ProfileCarry.Data)에 이번 장면에서 얻은 것을 더해 넣는다. 지금 지도는 바꾸지 않는다.
        /// </summary>
        void BuildSeedPreview()
        {
            var root = DungeonRoot.Instance;
            if (!root || root.State == null) return;
            var data = ProfileCarry.Data;
            var state = root.State;
            var once = new HashSet<string>();
            if (data != null) once.UnionWith(data.OnceDone);
            foreach (var e in state.OneTime.Values)
                if (e.Done && FloorRecipe.IsOnceItem(e.Id)) once.Add(e.Id);

            ulong baseSeed = root.Seed;
            _seedPreview.Clear();
            _seedPreviewTitle = $"{root.Floor}층 씨앗 {baseSeed + 1UL}~{baseSeed + SeedPreviewCount} (새 갱도로 지음)";
            var sb = new StringBuilder();
            sb.AppendLine("[씨앗 10개 보기] " + _seedPreviewTitle);
            for (int i = 1; i <= SeedPreviewCount; i++)
            {
                ulong seed = baseSeed + (ulong)i;
                string text;
                try
                {
                    var floor = FloorGenerator.Generate(new GeneratorInput
                    {
                        Floor = root.Floor,
                        Seed = seed,
                        FirstVisit = false,
                        DeepestFloor = Mathf.Max(data != null ? data.DeepestFloor : 1, root.Floor),
                        HasPickaxe = state.HasPickaxe || (data != null && data.HasPickaxe),
                        HasKey = state.HasKey || (data != null && data.HasKey),
                        OnceDone = once,
                        Night = data != null ? data.Night : NightEvent.None,
                    });
                    text = floor.Describe().TrimEnd('\n') + "\n" + (floor.Report != null ? floor.Report.ToString() : "검사 없음");
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    text = $"씨앗 {seed}: 짓기 실패 — {ex.Message}";
                }
                _seedPreview.Add(text);
                sb.AppendLine(text).AppendLine();
            }
            _seedPreviewText = sb.ToString();
            Debug.Log(_seedPreviewText);
        }

        /// <summary>시험 패널 손잡이·단추를 썼다: 이번 판을 '시험 판'(F1 시험 패널)으로 적는다(4장 Q7). 장면을 바꾸는 단추는 바꾸기 전에 불러 지금 층 기록에도 남긴다.</summary>
        static void NoteTestUse() => TestRunFlag.Note(TestRunReason.DevPanel);

        static void RebuildWithSeed(ulong seed)
        {
            NoteTestUse();
            var root = DungeonRoot.Instance;
            if (root) root.RebuildWithSeed(seed);
        }

        static void AscendForTest()
        {
            NoteTestUse();
            var root = DungeonRoot.Instance;
            if (root) root.Ascend();
        }

        /// <summary>시험 패널 '오우거 굴로(시험)': 지금 층에서 계단을 쓴 것처럼 같은 원정으로 굴 장면에 간다(DungeonRoot.GoToDenForTest).</summary>
        static void GoToDenForTest()
        {
            NoteTestUse();
            var root = DungeonRoot.Instance;
            if (root) root.GoToDenForTest();
        }

        /// <summary>
        /// 시험 패널 '오프닝 의뢰 받기'(마을과 의뢰 첫 판 1-6): 마을 오프닝을 건너뛴 것과 같다(이름 공개·의뢰 둘 받기·본 장면).
        /// 이미 이 층에 들어섰으므로 층 들어섬을 한 번 더 넣어 첫 의뢰가 다음 단계(바구니로 올라오기)로 넘어가게 한다.
        /// </summary>
        static void AcceptOpeningForTest()
        {
            NoteTestUse();
            var root = DungeonRoot.Instance;
            var result = TalkDirector.Skip(TownScript.Opening, 0, ProfileCarry.Ensure(), TownRoot.QuestCtx);
            DungeonEvents.Say(result.Accepted.Count > 0 ? "시험: 오프닝 의뢰를 받았다" : "시험: 받을 오프닝 의뢰가 없다");
            if (root && root.Playing) QuestTracker.Feed(QuestEvent.FloorEntered(root.Floor));
            QuestHud.Refresh();
        }

        /// <summary>시험 패널 '진행 중 의뢰 모두 달성'. 보상은 주지 않는다(마을에서 보고해 받음).</summary>
        static void AchieveQuestsForTest()
        {
            NoteTestUse();
            var updates = new QuestBook(ProfileCarry.Ensure()).AchieveAllActive();
            if (updates.Count == 0) DungeonEvents.Say("시험: 진행 중인 의뢰가 없다");
            foreach (var u in updates)
                if (u.Notice != null) DungeonEvents.Say(u.Notice);
            QuestHud.Refresh();
        }

        /// <summary>
        /// 시험 패널 '무기 종류(시험)'(키 배치 1차 0장 3): 숫자키 대신 이 단추로 무기 종류를 바꾼다. 예전 숫자키와 같은 길(PlayerController.SetWeapon)이라
        /// Inventory가 낀 무기를 WithBase로 바꿔 등급·굴림·강화·옵션·전설은 그대로 두고 종류만 바꾼다. 무기 행동 중이면 SetWeapon이 막아 그대로다.
        /// 실제로 바뀌었을 때만 이번 판을 '시험 판'(F1 시험 패널)으로 적는다.
        /// </summary>
        static void SetWeaponForTest(WeaponAttackRule weapon)
        {
            var player = PlayerController.Instance;
            if (!player || weapon == null) return;
            var before = player.Weapon;
            player.SetWeapon(weapon);
            if (player.Weapon != before) NoteTestUse();
        }

        /// <summary>시험 패널 '강화석 +50'(재화 쓸 곳 1차 14장): 이번 장면 강화석에 더한다(떠날 때 꾸러미에 담김).</summary>
        static void AddStonesForTest()
        {
            var root = DungeonRoot.Instance;
            if (!root || root.State == null) return;
            NoteTestUse();
            root.State.Stones += TestStones;
            DungeonEvents.Say("시험: 강화석 +" + TestStones);
        }

        /// <summary>
        /// 시험 패널 '가방 채우기'(재화 쓸 곳 1차 14장): 가방이 칸에 찰 때까지 이 층의 일반 장비(강화 +0, 부위를 돌려 가며)를 넣는다.
        /// Inventory에는 시험용으로 장비를 넣는 입구가 없어 꾸러미를 거친다(ExportTo → 가방 줄에 더함 → ImportFrom).
        /// 꾸러미의 장착·가방 줄은 떠날 때 ProfileCarry.Capture가 어차피 다시 적으므로 미리 적어도 결과가 같다.
        /// </summary>
        static void FillBagForTest()
        {
            var root = DungeonRoot.Instance;
            var inv = Inventory.Instance;
            if (!root || !inv) return;
            int add = inv.Capacity - inv.BagCount;
            if (add <= 0)
            {
                DungeonEvents.Say($"시험: 가방이 이미 찼다 ({inv.BagCount}/{inv.Capacity})");
                return;
            }
            NoteTestUse();
            var data = ProfileCarry.Ensure();
            inv.ExportTo(data);
            int floor = FloorScaling.Clamp(root.Floor);
            var rng = new Demo6.Core.Random.Pcg32Random((ulong)DateTime.UtcNow.Ticks, FillBagStream);
            for (int i = 0; i < add; i++) data.Bag.Add(CommonGear((GearPart)(i % GearSlots.PartCount), floor, rng));
            inv.ImportFrom(data);
            DungeonEvents.Say($"시험: 가방을 일반 장비로 채웠다 ({inv.BagCount}/{inv.Capacity})");
        }

        /// <summary>그 부위의 일반 장비 하나(강화 +0): 이 층에 풀린 종류 가운데 하나, 굴림 900~1100‰, 일반 옵션 줄(LootRules.RollGear와 같은 재료).</summary>
        static GearItem CommonGear(GearPart part, int floor, Demo6.Core.Random.IRandom rng)
        {
            var kinds = GearBaseTable.ForPart(part, floor);
            if (kinds.Count == 0) kinds = GearBaseTable.ForPart(part);
            var kind = kinds[rng.NextInt(0, kinds.Count)];
            int roll = rng.NextInt(GearMath.RollMinPermille, GearMath.RollMaxPermille + 1);
            return new GearItem(kind.Id, Grade.Common, floor, roll, 0, OptionTable.RollAll(part, Grade.Common, floor, rng));
        }

        // ── 화면 ─────────────────────────────────────────────

        void OnGUI()
        {
            var root = DungeonRoot.Instance;
            if (!root || root.State == null || _hidden) return;
            DungeonUi.Begin();
            if (_smallButton == null) _smallButton = new GUIStyle(GUI.skin.button) { fontSize = 14, wordWrap = true, padding = new RectOffset(5,5,4,4), margin=new RectOffset(2,2,3,3) };
            GUI.depth = -5;
            // 멈춤 창이 열려 있으면 F1 패널만 그리지 않는다(층 기록 창은 그대로, 저장·처음 화면·멈춤 창 1차 5-1·8-1).
            if (PanelVisible && !PauseMenu.IsOpen) DrawPanel(root);
            if (DungeonUi.Modal == SummaryModal) DrawSummary(root);
        }

        void DrawMeasures()
        {
            BuildLines();
            DrawLines(_lines);
        }

        static void DrawLines(List<(string text, int verdict)> lines)
        {
            var prev = GUI.color;
            foreach (var line in lines)
            {
                GUI.color = VerdictColor(line.verdict);
                GUILayout.Label(line.text, DungeonUi.Small, GUILayout.MaxWidth(Instance && Instance.PanelVisible ? PanelWidth - 64f : 780f));
            }
            GUI.color = prev;
        }

        static Color VerdictColor(int verdict)
        {
            switch (verdict)
            {
                case Pass: return new Color(0.6f, 0.95f, 0.6f);
                case Fail: return new Color(1f, 0.65f, 0.4f);
                case Pending: return new Color(0.75f, 0.75f, 0.75f);
                default: return Color.white;
            }
        }

        static void Section(string title)
        {
            GUILayout.Space(8f);
            GUILayout.Label(title, DungeonUi.Bold);
        }

        /// <summary>글자 지도용 단일 칸 글꼴(한글은 뒤 글꼴로). 설치된 것이 없으면 기본 글꼴.</summary>
        void EnsureMono()
        {
            if (_monoStyle != null) return;
            if (!_monoTried)
            {
                _monoTried = true;
                _mono = LoadMono();
            }
            _monoStyle = new GUIStyle(DungeonUi.Small) { font = _mono, fontSize = 13, wordWrap = true };
        }

        static Font LoadMono()
        {
            string[] installed;
            try
            {
                installed = Font.GetOSInstalledFontNames();
            }
            catch
            {
                return null;
            }
            if (installed == null || installed.Length == 0) return null;
            var found = new List<string>();
            AddInstalled(found, MonoNames, installed);
            if (found.Count == 0) return null;
            AddInstalled(found, HangulFallbackNames, installed);
            var font = Font.CreateDynamicFontFromOSFont(found.ToArray(), 13);
            if (font) font.hideFlags = HideFlags.DontSave;
            return font;
        }

        static void AddInstalled(List<string> found, string[] wants, string[] installed)
        {
            foreach (var want in wants)
            {
                foreach (var have in installed)
                {
                    if (!string.Equals(want, have, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!found.Contains(have)) found.Add(have);
                    break;
                }
            }
        }

        void DrawPanel(DungeonRoot root)
        {
            var state = root.State;
            DungeonUi.Box(_panelRect, 0.88f);
            if(DungeonUi.CloseButton(_panelRect,"닫기 · F1")){PanelVisible=false;return;}
            GUI.Label(new Rect(_panelRect.x+18f,_panelRect.y+16f,_panelRect.width-92f,36f),"개발 · 탐험 기록",DungeonUi.Title);
            GUILayout.BeginArea(new Rect(_panelRect.x + 16f, _panelRect.y + 60f, _panelRect.width - 32f, _panelRect.height - 76f));
            _scroll = GUILayout.BeginScrollView(_scroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar);
            GUILayout.BeginVertical(GUILayout.Width(_panelRect.width - 64f));

            GUILayout.Label("[F1] 닫기",DungeonUi.Small);
            var back = DevPanelExtras.DrawBackToLauncher(_smallButton); if (back != null) _pendingAction = back;
            GUILayout.Label("멈춘 시간(지도·창)은 빼고 잰다. 초록 = 기준 안, 주황 = 벗어남, 회색 = 아직 모자람.", DungeonUi.Small, GUILayout.MaxWidth(PanelWidth - 64f));

            Section("판정 기록 (7-3 · 매판 새 탐험 5장)");
            DrawMeasures();

            Section("시험 손잡이");
            // 4장 Q7: 판을 쉽게 바꾸는 손잡이(어둠·무적·지도 전부·시야·부채꼴·곡괭이·올라가기·굴로)를 바꾸면 시험 판으로 적는다.
            // 걸음·웅크림 배율·기척·피 발자국·정수리 시점은 판정 때 견줘 보는 손잡이라(걸음은 기록 글에 적힘) 적지 않는다.
            GUILayout.BeginHorizontal();
            var lighting = DungeonLighting.Instance;
            if (lighting)
            {
                bool dark = GUILayout.Toggle(lighting.DarknessOn, "어둠");
                if (dark != lighting.DarknessOn)
                {
                    lighting.DarknessOn = dark;
                    NoteTestUse();
                }
            }
            bool invincible = GUILayout.Toggle(Tuning.Invincible, "무적");
            if (invincible != Tuning.Invincible)
            {
                Tuning.Invincible = invincible;
                NoteTestUse();
            }
            // 투지(기획/스킬-자원-트리-1차.md): 쓰지 않기 손잡이와 가득 채우기.
            GUILayout.BeginHorizontal();
            bool spiritFree = GUILayout.Toggle(PlayerController.SpiritFree, "투지 쓰지 않기");
            if (spiritFree != PlayerController.SpiritFree)
            {
                PlayerController.SpiritFree = spiritFree;
                NoteTestUse();
            }
            if (PlayerController.Instance && GUILayout.Button("투지 가득", GUILayout.Width(90)))
            {
                PlayerController.Instance.FillSpirit();
                NoteTestUse();
            }
            GUILayout.EndHorizontal();
            // 쓰러짐 대가(시스템-컨텐츠-다듬기-검토-1차.md 묶음 5): 체력 60%·물약 그대로, 주머니 몫, 쉬기 다시 쓰기.
            bool penalty = GUILayout.Toggle(DownTuning.Penalty, "쓰러지면 체력 " + Mathf.RoundToInt(DownTuning.HpFraction * 100f) + "% · 물약 그대로");
            if (penalty != DownTuning.Penalty)
            {
                DownTuning.Penalty = penalty;
                NoteTestUse();
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("주머니 " + Mathf.RoundToInt(DownTuning.PouchShare * 100f) + "%", GUILayout.Width(90));
            float share = Mathf.Round(GUILayout.HorizontalSlider(DownTuning.PouchShare, 0f, 0.5f) * 20f) / 20f;
            if (!Mathf.Approximately(share, DownTuning.PouchShare))
            {
                DownTuning.PouchShare = share;
                NoteTestUse();
            }
            var downRoot = DungeonRoot.Instance;
            if (downRoot && downRoot.Leg != null && downRoot.Leg.Rested && GUILayout.Button("쉬기 다시", GUILayout.Width(80)))
            {
                downRoot.Leg.Rested = false;
                NoteTestUse();
            }
            if (downRoot && downRoot.Leg != null && GUILayout.Button("기름 +2", GUILayout.Width(70)))
            {
                downRoot.Leg.Oil = Demo6.Core.Dungeon.DownRules.AddOil(downRoot.Leg.Oil, 2);
                NoteTestUse();
            }
            GUILayout.EndHorizontal();
            var map = BigMap.Instance;
            if (map)
            {
                bool revealAll = GUILayout.Toggle(map.RevealAll, "지도 전부 보기");
                if (revealAll != map.RevealAll)
                {
                    map.RevealAll = revealAll;
                    NoteTestUse();
                }
            }
            GUILayout.EndHorizontal();

            var vision = VisionSystem.Instance;
            if (vision)
            {
                bool visionOn = GUILayout.Toggle(vision.VisionOn, "시야(벽에 가림·기억 안개)");
                if (visionOn != vision.VisionOn)
                {
                    vision.VisionOn = visionOn;
                    NoteTestUse();
                }
                GUI.enabled = vision.VisionOn;
                bool coneOn = GUILayout.Toggle(vision.ConeOn, "바라보는 쪽 부채꼴");
                if (coneOn != vision.ConeOn)
                {
                    vision.ConeOn = coneOn;
                    NoteTestUse();
                }
                // 시야와 문 1차 13장 손잡이(넘길 일 H1). 보는 칸 밖 가리기·적은 부채꼴 안만 보임을 끄면 판이 쉬워지므로 시험 판으로 적는다(Q7).
                // 칸 살핌 경험치·등 뒤 기척 소리·문 자리 비틀기는 견줘 보는 손잡이라 적지 않는다. 바뀔 때만 넣는다(속성 setter가 미룬 경험치를 줄 수 있음).
                bool roomClip = GUILayout.Toggle(vision.RoomClipOn, "보는 칸 밖 가리기");
                if (roomClip != vision.RoomClipOn)
                {
                    vision.RoomClipOn = roomClip;
                    if (!roomClip) NoteTestUse();
                }
                bool enemyCone = GUILayout.Toggle(vision.EnemyConeOn, "적은 부채꼴 안만 보임");
                if (enemyCone != vision.EnemyConeOn)
                {
                    vision.EnemyConeOn = enemyCone;
                    if (!enemyCone) NoteTestUse();
                }
                bool sweepXp = GUILayout.Toggle(vision.SweepXpOn, "칸 살핌 경험치");
                if (sweepXp != vision.SweepXpOn) vision.SweepXpOn = sweepXp;
                GUI.enabled = true;
                var viewCell = vision.ViewCell;
                int sweptPercent = viewCell != null ? Mathf.FloorToInt(vision.SweepProgress(viewCell) * 100f + 0.001f) : 0;
                GUILayout.Label($"보는 칸 {(viewCell != null ? viewCell.Name : "-")} · 살핌 {sweptPercent}%", DungeonUi.Small, GUILayout.MaxWidth(PanelWidth - 64f));
            }
            GUILayout.BeginHorizontal();
            BehindSounds.Enabled = GUILayout.Toggle(BehindSounds.Enabled, "등 뒤 기척 소리");
            DungeonWorld.DoorShiftOn = GUILayout.Toggle(DungeonWorld.DoorShiftOn, "문 자리 비틀기(다시 짓기 뒤)");
            GUILayout.EndHorizontal();
            // 정수리 시점 시험판(전투 시험장 '그림' 절과 같은 토글).
            TopDownView.Enabled = GUILayout.Toggle(TopDownView.Enabled, "정수리 시점(시험)");

            // 걸음(전투·보스·무기 다듬기 1차 1-2, 전투 시험장 '손맛 조절'과 같은 값). 비교안 단추는 나·적 배율을 함께 넣는다.
            // 0.01 단위라 기본값 B 0.80 / 0.93이 그대로 남는다. '지금'만 빠른 걸음을 처음 가는 칸에서도 켠다(예전 탐험 걸음).
            GUILayout.BeginHorizontal();
            GUILayout.Label("걸음 비교안 · " + PaceName(), DungeonUi.Small, GUILayout.Width(150f));
            foreach (var option in ExplorePace.Options)
            {
                if (!GUILayout.Button(option.Name, _smallButton)) continue;
                Tuning.MoveSpeedScale = option.PlayerScale;
                Tuning.EnemyMoveScale = option.EnemyScale;
                ExploreWalk.FastEverywhere = option.FastEverywhere;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label($"이동 속도 배율 {Tuning.MoveSpeedScale:0.00}", DungeonUi.Small, GUILayout.Width(150f));
            float moveScale = GUILayout.HorizontalSlider(Tuning.MoveSpeedScale, 0.5f, 1.2f);
            GUILayout.EndHorizontal();
            if (moveScale != Tuning.MoveSpeedScale) Tuning.MoveSpeedScale = Mathf.Round(moveScale * 100f) / 100f;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"적 걸음 배율 {Tuning.EnemyMoveScale:0.00}", DungeonUi.Small, GUILayout.Width(150f));
            float enemyScale = GUILayout.HorizontalSlider(Tuning.EnemyMoveScale, 0.7f, 1.1f);
            GUILayout.EndHorizontal();
            if (enemyScale != Tuning.EnemyMoveScale) Tuning.EnemyMoveScale = Mathf.Round(enemyScale * 100f) / 100f;
            GUILayout.BeginHorizontal();
            Tuning.MoveInertia = GUILayout.Toggle(Tuning.MoveInertia, "가감속(묵직함)");
            ExploreWalk.FastEverywhere = GUILayout.Toggle(ExploreWalk.FastEverywhere, "빠른 걸음 모든 칸(예전)");
            // Tuning.ResetToDefaults는 걸음·웅크림만이 아니라 '가방·재화' 절의 두 손잡이(저절로 분해(일반)·저절로 줍기 여유 칸)와 무적 같은 Tuning 값도 모두 기본으로 되돌린다.
            if (GUILayout.Button(ResetKnobsContent, _smallButton))
            {
                Tuning.ResetToDefaults();
                ExploreWalk.FastEverywhere = false;
            }
            GUILayout.EndHorizontal();
            // 걸음은 장비 이동 능력치를 넣은 값(PlayerController.WalkSpeed, 시작 장비 5.30)을 보인다. 굴쥐는 4.2 × 적 배율.
            float walk = PlayerController.Instance ? PlayerController.Instance.WalkSpeed : PlayerController.EquippedWalkSpeed;
            GUILayout.Label($"걸음 {walk * Tuning.MoveSpeedScale:0.00} / 아는 길 {ExploreWalk.ScaledSpeed:0.00} / 굴쥐 {ExplorePace.RatSpeed * Tuning.EnemyMoveScale:0.00} (배율 1이면 시작 장비 5.30 · 6.50 · 4.20)", DungeonUi.Small, GUILayout.MaxWidth(PanelWidth - 64f));
            // 웅크리기(결정 ③, 키 C): 걸음·소음 배율. 시야 좁히기(40° / 10 / 2.0)는 Tuning 기본값 그대로.
            GUILayout.BeginHorizontal();
            GUILayout.Label($"웅크림 걸음 × {Tuning.CrouchMoveScale:0.00}", DungeonUi.Small, GUILayout.Width(150f));
            float crouchMove = GUILayout.HorizontalSlider(Tuning.CrouchMoveScale, 0.3f, 1f);
            GUILayout.EndHorizontal();
            if (crouchMove != Tuning.CrouchMoveScale) Tuning.CrouchMoveScale = Mathf.Round(crouchMove * 100f) / 100f;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"웅크림 소음 × {Tuning.CrouchNoiseScale:0.00}", DungeonUi.Small, GUILayout.Width(150f));
            float crouchNoise = GUILayout.HorizontalSlider(Tuning.CrouchNoiseScale, 0.1f, 1f);
            GUILayout.EndHorizontal();
            if (crouchNoise != Tuning.CrouchNoiseScale) Tuning.CrouchNoiseScale = Mathf.Round(crouchNoise * 100f) / 100f;
            // 소리와 피(4장, 판정 때 끄고 켜 본다).
            GUILayout.BeginHorizontal();
            LurkSounds.Enabled = GUILayout.Toggle(LurkSounds.Enabled, "기척");
            GoreFootprints.Enabled = GUILayout.Toggle(GoreFootprints.Enabled, "피 발자국");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = !state.HasPickaxe;
            if (GUILayout.Button(state.HasPickaxe ? "곡괭이 있음" : "곡괭이 받기"))
            {
                state.HasPickaxe = true;
                NoteTestUse();
                DungeonEvents.Say("시험: 곡괭이를 받았다");
            }
            GUI.enabled = true;
            if (GUILayout.Button("바구니로 올라가기(시험)")) _pendingAction = AscendForTest;
            GUILayout.EndHorizontal();

            // 오우거 굴(전투·보스 문서 3-8): 굴 장면으로 바로 가기와 꾸러미 보스 기록.
            DrawDen(root);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("기록 처음부터")) ResetMeasures();
            if (GUILayout.Button("기록 복사")) GUIUtility.systemCopyBuffer = BuildReport();
            if (GUILayout.Button("층 기록 창")) _summaryPending = true;
            GUILayout.EndHorizontal();

            Section("무기 종류(시험)");
            DrawWeaponKinds();

            Section("가방·재화 (재화 쓸 곳 1차 14장)");
            DrawBagTools(root);
            Section("시험 도구 (저장·처음 화면·멈춤 창 1차 8-2)"); var tool = DevPanelExtras.DrawDungeonTools(root, _smallButton, PanelWidth - 64f); if (tool != null) _pendingAction = tool;

            Section("의뢰 (마을과 의뢰 첫 판)");
            DrawQuests(root);

            Section("갱도 씨앗");
            DrawSeeds(root);

            Section("층 기록 (원정·씨앗별)");
            if (_runLines.Count == 0) GUILayout.Label("아직 없음", DungeonUi.Small, GUILayout.MaxWidth(PanelWidth - 64f));
            else DrawLines(_runLines);

            Section("칸으로 순간 이동");
            if (root.World != null)
            {
                int col = 0;
                GUILayout.BeginHorizontal();
                foreach (var cell in root.World.Cells)
                {
                    if (col == 3)
                    {
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        col = 0;
                    }
                    string mark = cell.Visited ? "" : " ·";
                    if (GUILayout.Button(cell.Id + " " + cell.Name + mark, _smallButton, GUILayout.Width((_panelRect.width - 64f) / 3f), GUILayout.Height(40f))) TeleportTo(root, cell);
                    col++;
                }
                GUILayout.EndHorizontal();
                GUILayout.Label("· = 아직 안 간 칸. 순간 이동한 걸음은 되돌아간 걸음에 넣지 않는다.", DungeonUi.Small, GUILayout.MaxWidth(PanelWidth - 64f));
            }

            Section("최근 새 것");
            if (_recent.Count == 0) GUILayout.Label("아직 없음", DungeonUi.Small, GUILayout.MaxWidth(PanelWidth - 64f));
            for (int i = _recent.Count - 1; i >= 0; i--) GUILayout.Label(_recent[i], DungeonUi.Small, GUILayout.MaxWidth(PanelWidth - 64f));

            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>'의뢰' 절: 의뢰마다 상태 줄, 받기 전 기록(2층 계단 앞 말뚝), 시험 단추 둘. 보상은 마을에서 보고할 때만 들어간다.</summary>
        void DrawQuests(DungeonRoot root)
        {
            var carry = ProfileCarry.Ensure();
            var book = new QuestBook(carry);
            foreach (var q in QuestTable.All) GUILayout.Label(book.DebugLine(q.Id), DungeonUi.Small, GUILayout.MaxWidth(PanelWidth - 64f));
            GUILayout.Label("받기 전 기록 2층 계단 앞 말뚝: " + (TownSave.HasMilestone(carry, TownSave.MilestoneStairsF2) ? "있음" : "없음"), DungeonUi.Small, GUILayout.MaxWidth(PanelWidth - 64f));
            GUILayout.Label("올라가면: " + (root.AscendsToTown ? "마을(Town)" : "옛 흐름 — Town 장면을 찾지 못함"), DungeonUi.Small, GUILayout.MaxWidth(PanelWidth - 64f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("오프닝 의뢰 받기", _smallButton)) _pendingAction = AcceptOpeningForTest;
            if (GUILayout.Button("진행 중 의뢰 모두 달성", _smallButton)) _pendingAction = AchieveQuestsForTest;
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// '무기 종류(시험)' 절(키 배치 1차 0장 3, 전투 시험장 패널 '무기' 단추와 같은 모양): WeaponPresets.All 9종을 한 줄에 셋, 지금 무기는 눌린 단추.
        /// 누르면 다음 Update에서 SetWeaponForTest(종류만 바꿈·시험 판으로 적음). 숫자키로는 바꾸지 않는다(0장 2).
        /// </summary>
        void DrawWeaponKinds()
        {
            var player = PlayerController.Instance;
            if (!player)
            {
                GUILayout.Label("플레이어 없음", DungeonUi.Small, GUILayout.MaxWidth(PanelWidth - 64f));
                return;
            }
            var now = player.Weapon;
            var equipped = Inventory.Instance ? Inventory.Instance.Equipped : null;
            GUILayout.Label("지금 " + (now != null ? now.displayName : "-") + (equipped != null ? " · 낀 것 " + equipped.DisplayName : "") +
                            (player.InWeaponAct ? " · 무기 행동 중에는 바뀌지 않는다" : ""), DungeonUi.Small, GUILayout.MaxWidth(PanelWidth - 64f));
            var weapons = WeaponPresets.All;
            float width = (_panelRect.width - 64f) / 3f;
            for (int row = 0; row < weapons.Length; row += 3)
            {
                GUILayout.BeginHorizontal();
                for (int i = row; i < row + 3 && i < weapons.Length; i++)
                {
                    var w = weapons[i];
                    bool selected = now == w;
                    if (GUILayout.Toggle(selected, w.displayName, _smallButton, GUILayout.Width(width)) && !selected)
                        _pendingAction = () => SetWeaponForTest(w);
                }
                GUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// '가방·재화' 절(재화 쓸 곳 1차 14장 던전 F1): 가방 칸 줄, '저절로 분해(일반) 켬/끔'(㉡, Tuning.AutoSalvageCommon),
        /// '저절로 줍기 여유 칸 0/3'(㉠, Tuning.AutoPickupReserve), '강화석 +50', '가방 채우기(일반 장비로)'.
        /// 두 손잡이는 ㉠·㉡을 견줘 보는 값이라 시험 판으로 적지 않고, 강화석·가방 채우기는 판을 쉽게 바꾸므로 적는다(NoteTestUse).
        /// 두 손잡이는 Tuning 정적 값이라 다음 플레이까지 남는다(도메인 다시 불러오기 꺼짐). 기본과 다르면 판정 기록·복사 글에 한 줄(BagKnobsLine)을 남기고,
        /// '손맛 기본값으로'(Tuning.ResetToDefaults)를 누르면 걸음과 함께 기본(켬 · 0)으로 돌아간다.
        /// </summary>
        void DrawBagTools(DungeonRoot root)
        {
            var inv = Inventory.Instance;
            if (inv)
                GUILayout.Label($"가방 {inv.BagCount}/{inv.Capacity} · 넘침 끝 {inv.HardCap} · 강화석 {root.State.Stones}" + (inv.BagFull ? " · 가득" : inv.NearlyFull ? " · 거의 참" : ""),
                    DungeonUi.Small, GUILayout.MaxWidth(PanelWidth - 64f));
            GUILayout.BeginHorizontal();
            Tuning.AutoSalvageCommon = GUILayout.Toggle(Tuning.AutoSalvageCommon, ForgeText.AutoSalvageToggle(Tuning.AutoSalvageCommon));
            bool reserve = GUILayout.Toggle(Tuning.AutoPickupReserve > 0, ForgeText.AutoReserveToggle(Tuning.AutoPickupReserve));
            Tuning.AutoPickupReserve = reserve ? BagRules.AutoReserveSlots : 0;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("강화석 +" + TestStones, _smallButton)) _pendingAction = AddStonesForTest;
            GUI.enabled = inv != null;
            if (GUILayout.Button("가방 채우기(일반 장비로, 칸까지)", _smallButton)) _pendingAction = FillBagForTest;
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 가방 손잡이 줄(재화 쓸 곳 1차 5-2·14장): 두 값이 기본(저절로 분해 켬 · 여유 칸 0, Tuning.ResetToDefaults)과 다르면
        /// "가방 손잡이: 저절로 분해 끔 · 여유 칸 3 (기본 켬 · 0)", 기본이면 null. 판정 기록과 복사 글(BuildReport가 판정 줄을 옮김)에 들어간다.
        /// </summary>
        static string BagKnobsLine()
        {
            bool salvage = Tuning.AutoSalvageCommon;
            int reserve = Tuning.AutoPickupReserve;
            if (salvage == BagRules.AutoSalvageCommonDefault && reserve == 0) return null;
            return $"가방 손잡이: 저절로 분해 {OnOff(salvage)} · 여유 칸 {reserve} (기본 {OnOff(BagRules.AutoSalvageCommonDefault)} · 0)";
        }

        static string OnOff(bool on) => on ? "켬" : "끔";

        /// <summary>'손맛 기본값으로' 단추(말풍선: 가방 손잡이도 함께 돌아간다).</summary>
        static readonly GUIContent ResetKnobsContent = new GUIContent("손맛 기본값으로",
            "걸음·웅크림 배율과 함께 가방 손잡이(저절로 분해(일반)·저절로 줍기 여유 칸)도 기본으로 돌아간다");

        /// <summary>지금 걸음 배율이 맞는 비교안 이름(1-2). 슬라이더로 바꿔 어느 안과도 맞지 않으면 '직접'.</summary>
        static string PaceName()
        {
            foreach (var option in ExplorePace.Options)
                if (Mathf.Approximately(Tuning.MoveSpeedScale, option.PlayerScale)
                    && Mathf.Approximately(Tuning.EnemyMoveScale, option.EnemyScale)
                    && ExploreWalk.FastEverywhere == option.FastEverywhere)
                    return option.Name;
            return "직접";
        }

        /// <summary>
        /// 시험 패널 오우거 굴 줄: 꾸러미 보스 기록(처치·쓰러짐·굴 앞 말뚝·'보스방 앞' 보임)과 '오우거 굴로(시험)' 단추.
        /// 굴 안이거나 던전에 굴이 없으면 단추를 막는다.
        /// </summary>
        void DrawDen(DungeonRoot root)
        {
            var carry = ProfileCarry.Ensure();
            GUILayout.Label($"오우거 굴{(root.IsDen ? "(지금 여기)" : "")}: 처치 {BossLedger.Kills(carry, OgreDen.BossId)} · 쓰러짐 {BossLedger.Losses(carry, OgreDen.BossId)} · " +
                            $"굴 앞 말뚝 {(BossLedger.StakeLit(carry, OgreDen.FrontStakeId) ? "켬" : "아직")} · '보스방 앞' {(BossLedger.FrontLandingOpen(carry) ? "보임" : "없음")}",
                DungeonUi.Small, GUILayout.MaxWidth(PanelWidth - 64f));
            GUI.enabled = !root.IsDen && OgreDen.InDungeon;
            if (GUILayout.Button(OgreDen.InDungeon ? "오우거 굴로(시험)" : "오우거 굴 없음")) _pendingAction = GoToDenForTest;
            GUI.enabled = true;
        }

        /// <summary>'갱도 씨앗' 절: 지금 씨앗·지도 글자, 다시 짓기 단추 둘, 씨앗 10개 보기(글자 지도 + 복사). 오우거 굴은 고정 지도라 다시 짓기를 막는다.</summary>
        void DrawSeeds(DungeonRoot root)
        {
            EnsureMono();
            int traces = root.Traces != null ? root.Traces.Count : 0;
            GUILayout.Label($"씨앗 {root.Seed} · {MapKind(root.IsDen, root.FirstVisit)} · {root.Floor}층 · 원정 {root.Expedition}번째 · 흔적 {traces}", DungeonUi.Small, GUILayout.MaxWidth(PanelWidth - 64f));
            if (!string.IsNullOrEmpty(root.Glyphs)) GUILayout.Label(root.Glyphs.TrimEnd('\n'), _monoStyle);
            if (root.IsDen)
            {
                GUILayout.Label("굴은 고정 지도(돌로 쌓은 곳)라 씨앗으로 다시 짓지 않는다.", DungeonUi.Small, GUILayout.MaxWidth(PanelWidth - 64f));
                return;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("이 씨앗으로 다시", _smallButton))
            {
                ulong seed = root.Seed;
                _pendingAction = () => RebuildWithSeed(seed);
            }
            if (GUILayout.Button("씨앗 +1", _smallButton))
            {
                ulong seed = root.Seed + 1UL;
                _pendingAction = () => RebuildWithSeed(seed);
            }
            if (GUILayout.Button("씨앗 10개 보기", _smallButton)) _wantSeedPreview = true;
            GUILayout.EndHorizontal();

            if (_seedPreview.Count == 0) return;
            GUILayout.BeginHorizontal();
            GUILayout.Label(_seedPreviewTitle, DungeonUi.Small, GUILayout.MaxWidth(PanelWidth - 64f));
            if (GUILayout.Button("복사", _smallButton, GUILayout.Width(56f))) GUIUtility.systemCopyBuffer = _seedPreviewText;
            if (GUILayout.Button("접기", _smallButton, GUILayout.Width(56f))) _wantFoldPreview = true;
            GUILayout.EndHorizontal();
            foreach (var text in _seedPreview) GUILayout.Label(text, _monoStyle);
        }

        void TeleportTo(DungeonRoot root, DungeonCell cell)
        {
            var player = root.Player;
            if (!player || player.IsDown) return;
            NoteTestUse();
            player.Teleport(cell.Center);
            if (root.CameraRig) root.CameraRig.Snap();
            _hasLastPos = false;
        }

        void DrawSummary(DungeonRoot root)
        {
            float w = Mathf.Min(860f, DungeonUi.Width - 40f);
            float h = Mathf.Min(820f, DungeonUi.Height - 60f);
            var r = new Rect((DungeonUi.Width - w) * 0.5f, (DungeonUi.Height - h) * 0.5f, w, h);
            DungeonUi.Fill(new Rect(0f, 0f, DungeonUi.Width, DungeonUi.Height), new Color(0f, 0f, 0f, 0.45f));
            DungeonUi.Box(r, 0.95f);
            if(DungeonUi.CloseButton(r))DungeonUi.Close(SummaryModal);
            GUILayout.BeginArea(new Rect(r.x + 20f, r.y + 14f, r.width - 40f, r.height - 28f));
            GUILayout.Label($"{root.Floor}층 기록", DungeonUi.Title,GUILayout.MaxWidth(r.width-110f));
            GUILayout.Label("아래 숫자를 판정 기준(3차 7-3 · 매판 새 탐험 5장)과 견주고 판정 질문에 답한다. 계속 탐험하면 숫자는 이어서 잰다.", DungeonUi.Small);
            _summaryScroll = GUILayout.BeginScrollView(_summaryScroll);
            Section("판정 기록");
            DrawMeasures();
            Section("층 기록 (원정·씨앗별)");
            if (_runLines.Count == 0) GUILayout.Label("아직 없음", DungeonUi.Small);
            else DrawLines(_runLines);
            Section("판정 질문");
            for (int i = 0; i < Questions.Length; i++) GUILayout.Label($"{i + 1}. {Questions[i]}", DungeonUi.Label);
            GUILayout.EndScrollView();
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("기록 복사", GUILayout.Width(140f), GUILayout.Height(36f))) GUIUtility.systemCopyBuffer = BuildReport();
            GUILayout.FlexibleSpace();
            GUILayout.Label("[Esc] 계속 탐험",DungeonUi.Small,GUILayout.Width(200f),GUILayout.Height(36f));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}

