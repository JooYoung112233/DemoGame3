using System.Collections.Generic;
using System.Text;
using Demo6.Core.Dungeon;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo6.Game
{
    /// <summary>
    /// 탐험 기록(3차 초안 7-3 판정 숫자)과 시험 패널(F1), 1층 기록 창("summary", 계단을 쓰면 열림).
    /// 시간은 멈춘 동안(창·말뚝 메뉴)을 빼고 실제 시간으로 잰다(히트스톱·느린 화면은 실제로 흐른 시간이라 센다).
    /// 재는 것: 층 시간, 전투 몫(DungeonLighting.InCombat), 새 것 간격(등잔·말뚝·새 칸 뺌, 2-8), 가장 긴 빈 구간(싸움·선택·장비 없는 구간),
    /// 되돌아간 걸음(전에 가 본 칸에 다시 들어와 걸은 거리 ÷ 전체), 막다른 곳 빈손(2-1 '헛걸음 0'), 마주침·쓰러짐·레벨·조사율.
    /// </summary>
    public sealed class ExplorationLog : MonoBehaviour
    {
        public const string SummaryModal = "summary";

        // 7-3 합격 기준.
        public const float TargetFloorSeconds = 420f;
        public const float FloorTolerance = 0.25f;
        public const float CombatShareMin = 0.25f;
        public const float CombatShareMax = 0.35f;
        public const float NewThingMedianMax = 30f;
        public const float EmptyGapMax = 60f;
        public const float BacktrackMax = 0.30f;

        /// <summary>같은 순간에 겹친 새 것(궤짝 열기 + 장비 떨어짐)은 하나로 센다.</summary>
        const float MergeWindow = 1.5f;
        /// <summary>한 프레임에 이보다 멀리 움직이면 순간 이동(말뚝 이동·다시 서기·시험 이동)으로 보고 걸음에 넣지 않는다.</summary>
        const float TeleportJump = 3f;
        /// <summary>멈춤 없이 한 프레임이 이보다 길면 끊긴 것(편집기 멈춤 등)으로 보고 자른다.</summary>
        const float MaxFrame = 0.5f;
        const float PanelWidth = 420f;
        const int RecentMax = 14;

        static readonly string[] Questions =
        {
            "어둠 속을 걸을 때 '무섭고 궁금하다' 쪽인가, '답답하다' 쪽인가? (첫 판에만 답함)",
            "걷기만 하는 구간이 지루하지 않은가?",
            "곡괭이를 얻고 되돌아가 금 간 벽을 열었는가? 두 번째 판에 말뚝에서 K까지 다시 갔는가?",
            "한 마리 한 마리가 기억에 남는가?",
            "마무리와 검풍을 아껴 쓰게 되는가, 아니면 늘 첫 타에 쓰는가?",
            "둥지가 아닌 보통 싸움이 끝날 때도 시원한가?",
            "M0a 시험장 10분과 1층 10분 가운데 다음 날 다시 켜고 싶은 쪽은? (A/B)",
            "상자에서 나온 무기를 끼고 세졌다고 느꼈나? 레벨업 뒤 무엇이 달라졌는지 말할 수 있나?",
            "이야기 없는 2층도 끝까지 돌고 싶은가?",
            "계단을 내려갈 때 '한 단 내려간다'는 느낌이 드는가?",
        };

        public static ExplorationLog Instance { get; private set; }

        /// <summary>F1 패널이 보이는가(큰 지도가 패널을 피해 그린다).</summary>
        public bool PanelVisible { get; private set; }
        /// <summary>마우스가 F1 패널 위에 있는가(패널 버튼을 누를 때 공격을 막는 데 쓸 수 있다).</summary>
        public bool PointerOverPanel { get; private set; }
        /// <summary>패널이 차지하는 기준 좌표 너비(여백 포함). 안 보이면 0.</summary>
        public float PanelReservedWidth => PanelVisible ? PanelWidth + 24f : 0f;

        public float FloorSeconds { get; private set; }
        public float CombatSeconds { get; private set; }
        /// <summary>계단을 처음 쓴 때의 층 시간(1층 첫 탐험). 아직이면 -1.</summary>
        public float FirstStairsSeconds { get; private set; } = -1f;
        public float LongestEmptyGap { get; private set; }
        public float CurrentEmptyGap { get; private set; }
        public float WalkedDistance { get; private set; }
        public float BacktrackDistance { get; private set; }
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

        // 판정 줄 색: 통과 / 벗어남 / 아직 / 참고.
        const int Pass = 1;
        const int Fail = 0;
        const int Pending = -1;
        const int Info = 2;

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
            CombatEvents.PlayerDowned -= OnPlayerDowned;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.f1Key.wasPressedThisFrame) PanelVisible = !PanelVisible;
                if (kb.escapeKey.wasPressedThisFrame && DungeonUi.Modal == SummaryModal) DungeonUi.Close(SummaryModal);
            }
            if (_summaryPending && DungeonUi.TryOpen(SummaryModal)) _summaryPending = false;
            UpdatePointer();
            Measure();
        }

        void UpdatePointer()
        {
            _panelRect = new Rect(DungeonUi.Width - PanelWidth - 12f, 12f, PanelWidth, DungeonUi.Height - 24f);
            var mouse = Mouse.current;
            if (!PanelVisible || mouse == null)
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

        /// <summary>기록을 처음부터(시험 패널). 지도·연 곳은 그대로다.</summary>
        public void ResetMeasures()
        {
            FloorSeconds = 0f;
            CombatSeconds = 0f;
            FirstStairsSeconds = -1f;
            LongestEmptyGap = 0f;
            CurrentEmptyGap = 0f;
            WalkedDistance = 0f;
            BacktrackDistance = 0f;
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
            CurrentEmptyGap = 0f;
            AddNewThing("장비: " + text);
        }

        void OnGearEquipped(string text) => CurrentEmptyGap = 0f;

        void OnPlayerDowned() => Deaths++;

        void OnCellEntered(DungeonCell cell, bool first)
        {
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

        void OnStairsUsed()
        {
            if (FirstStairsSeconds < 0f)
            {
                FirstStairsSeconds = FloorSeconds;
                Debug.Log(BuildReport());
            }
            _summaryPending = true;
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

        // ── 판정 줄 ──────────────────────────────────────────

        static int Judge(bool ok) => ok ? Pass : Fail;

        void BuildLines()
        {
            _lines.Clear();
            var root = DungeonRoot.Instance;

            float lo = TargetFloorSeconds * (1f - FloorTolerance);
            float hi = TargetFloorSeconds * (1f + FloorTolerance);
            string range = $"기준 7분 ±25% ({DungeonUi.Clock(lo)}~{DungeonUi.Clock(hi)})";
            if (FirstStairsSeconds >= 0f)
                _lines.Add(($"1층 첫 탐험 {DungeonUi.Clock(FirstStairsSeconds)} (계단까지, 지금 {DungeonUi.Clock(FloorSeconds)}) · {range}",
                    Judge(FirstStairsSeconds >= lo && FirstStairsSeconds <= hi)));
            else
                _lines.Add(($"탐험 시간 {DungeonUi.Clock(FloorSeconds)} (계단 전) · {range}", Pending));

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
        }

        IEnumerable<string> AllDeadEnds()
        {
            foreach (var id in _deadEndNone) yield return id + "(빈 곳)";
            foreach (var id in _deadEndLooted) yield return id + "(다시 들름)";
        }

        /// <summary>복사·콘솔용 글 기록.</summary>
        public string BuildReport()
        {
            BuildLines();
            var sb = new StringBuilder();
            sb.AppendLine("[1층 탐험 기록 (3차 초안 7-3)]");
            foreach (var line in _lines)
            {
                string mark = line.verdict == Pass ? "통과" : line.verdict == Fail ? "벗어남" : line.verdict == Pending ? "아직" : "참고";
                sb.Append("- [").Append(mark).Append("] ").AppendLine(line.text);
            }
            sb.AppendLine("최근 새 것:");
            foreach (var r in _recent) sb.Append("  ").AppendLine(r);
            return sb.ToString();
        }

        // ── 화면 ─────────────────────────────────────────────

        void OnGUI()
        {
            var root = DungeonRoot.Instance;
            if (!root || root.State == null) return;
            DungeonUi.Begin();
            if (_smallButton == null) _smallButton = new GUIStyle(GUI.skin.button) { fontSize = 12, wordWrap = false };
            GUI.depth = -5;
            if (PanelVisible) DrawPanel(root);
            if (DungeonUi.Modal == SummaryModal) DrawSummary(root);
        }

        void DrawMeasures()
        {
            BuildLines();
            var prev = GUI.color;
            foreach (var line in _lines)
            {
                GUI.color = VerdictColor(line.verdict);
                GUILayout.Label(line.text, DungeonUi.Small);
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

        void DrawPanel(DungeonRoot root)
        {
            var state = root.State;
            DungeonUi.Box(_panelRect, 0.88f);
            GUILayout.BeginArea(new Rect(_panelRect.x + 10f, _panelRect.y + 8f, _panelRect.width - 20f, _panelRect.height - 16f));
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("탐험 기록 (F1)", DungeonUi.Title);
            GUILayout.Label("멈춘 시간(지도·창)은 빼고 잰다. 초록 = 기준 안, 주황 = 벗어남, 회색 = 아직 모자람.", DungeonUi.Small);

            Section("판정 기록 (7-3)");
            DrawMeasures();

            Section("시험 손잡이");
            GUILayout.BeginHorizontal();
            var lighting = DungeonLighting.Instance;
            if (lighting)
            {
                bool dark = GUILayout.Toggle(lighting.DarknessOn, "어둠");
                if (dark != lighting.DarknessOn) lighting.DarknessOn = dark;
            }
            Tuning.Invincible = GUILayout.Toggle(Tuning.Invincible, "무적");
            var map = BigMap.Instance;
            if (map) map.RevealAll = GUILayout.Toggle(map.RevealAll, "지도 전부 보기");
            GUILayout.EndHorizontal();

            var vision = VisionSystem.Instance;
            if (vision)
            {
                GUILayout.BeginHorizontal();
                vision.VisionOn = GUILayout.Toggle(vision.VisionOn, "시야(벽에 가림·기억 안개)");
                GUI.enabled = vision.VisionOn;
                vision.ConeOn = GUILayout.Toggle(vision.ConeOn, "바라보는 쪽 부채꼴");
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            GUI.enabled = !state.HasPickaxe;
            if (GUILayout.Button(state.HasPickaxe ? "곡괭이 있음" : "곡괭이 받기"))
            {
                state.HasPickaxe = true;
                DungeonEvents.Say("시험: 곡괭이를 받았다");
            }
            GUI.enabled = true;
            if (GUILayout.Button("원정 다시 시작")) root.RestartExpedition(state.LastStakeId);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("기록 처음부터")) ResetMeasures();
            if (GUILayout.Button("기록 복사")) GUIUtility.systemCopyBuffer = BuildReport();
            if (GUILayout.Button("1층 기록 창")) _summaryPending = true;
            GUILayout.EndHorizontal();

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
                    if (GUILayout.Button(cell.Id + " " + cell.Name + mark, _smallButton, GUILayout.Width(124f), GUILayout.Height(24f))) TeleportTo(root, cell);
                    col++;
                }
                GUILayout.EndHorizontal();
                GUILayout.Label("· = 아직 안 간 칸. 순간 이동한 걸음은 되돌아간 걸음에 넣지 않는다.", DungeonUi.Small);
            }

            Section("최근 새 것");
            if (_recent.Count == 0) GUILayout.Label("아직 없음", DungeonUi.Small);
            for (int i = _recent.Count - 1; i >= 0; i--) GUILayout.Label(_recent[i], DungeonUi.Small);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void TeleportTo(DungeonRoot root, DungeonCell cell)
        {
            var player = root.Player;
            if (!player || player.IsDown) return;
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
            GUILayout.BeginArea(new Rect(r.x + 20f, r.y + 14f, r.width - 40f, r.height - 28f));
            GUILayout.Label($"{root.Floor}층 기록 — 계단에 닿았다", DungeonUi.Title);
            GUILayout.Label("M0b는 1층까지다. 아래 숫자를 판정 기준(7-3)과 견주고 판정 질문에 답한다. 계속 탐험하면 숫자는 이어서 잰다.", DungeonUi.Small);
            _summaryScroll = GUILayout.BeginScrollView(_summaryScroll);
            Section("판정 기록");
            DrawMeasures();
            Section("판정 질문");
            for (int i = 0; i < Questions.Length; i++) GUILayout.Label($"{i + 1}. {Questions[i]}", DungeonUi.Label);
            GUILayout.EndScrollView();
            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("기록 복사", GUILayout.Width(140f), GUILayout.Height(36f))) GUIUtility.systemCopyBuffer = BuildReport();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("계속 탐험", GUILayout.Width(200f), GUILayout.Height(36f))) DungeonUi.Close(SummaryModal);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
