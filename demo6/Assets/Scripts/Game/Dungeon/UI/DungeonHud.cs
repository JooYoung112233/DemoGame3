using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 던전 전투 HUD: 하단 중앙 생명 막대와 실제 행동 5칸(기본공격·회오리(E)·검풍·구르기·물약), 그 왼쪽에 보기만 하는 무기 행동 칸(우클릭).
    /// 아이콘·키·재사용 오버레이·남은 초·현재 동작 강조를 표시한다. 수치 상세는 가방/강화 창에 둔다.
    /// 층 도착·레벨업·쓰러짐 연출과 최대 두 줄의 짧은 알림은 유지한다.
    /// </summary>
    public sealed partial class DungeonHud : MonoBehaviour
    {
        const float PanelWidth = 544f;
        const float PanelHeight = 112f;
        const float Margin = 16f;
        const float OrbSize = 144f;
        const int ToastMax = 2;
        const float ToastLife = 3f;
        const float ToastFade = 0.6f;
        /// <summary>층 이름 카드: 약 3.5초, 천천히 떠올라 천천히 사라진다(기준 문서 '화면 연출').</summary>
        const float CardTime = 3.6f;
        const float CardFadeIn = 0.9f;
        const float CardFadeOut = 1.1f;
        const float BannerTime = 3.2f;
        const float BannerFadeIn = 0.22f;
        const float BannerFadeOut = 0.9f;
        /// <summary>쓰러진 뒤 화면이 붉게 다 어두워지기까지.</summary>
        const float DownDarken = 1.2f;
        const string DownTitle = "쓰러졌다";
        const string DownLine = "마지막 말뚝 곁에서 다시 눈을 뜬다";
        const string BannerLine = "최대 체력이 오르고, 스킬 점수 1점이 생겼다  [K]";
        /// <summary>아는 길 걸음일 때 한 줄(전투·보스·무기 다듬기 1차 1-2, 예전 '탐험 걸음'). 웅크리면 CrouchRules.HudLabel '웅크림' 한 줄.</summary>
        const string ExploreBadge = "아는 길";

        /// <summary>층별 권장 레벨(3차 초안 4-3): 1, 3, 5, 7, 8, 10, 12, 13, 15, 16.</summary>
        static readonly int[] RecommendedLevels = { 1, 3, 5, 7, 8, 10, 12, 13, 15, 16 };

        /// <summary>층 이름 카드 아래 음울한 한 줄(층 번호 순). 없는 층은 FloorLineDefault.</summary>
        static readonly string[] FloorLines =
        {
            "내려간 자들은 아무도 올라오지 않았다.",
        };
        const string FloorLineDefault = "아래로 갈수록 숨이 무거워진다.";
        /// <summary>1층 첫 원정(손 지도) 카드 아랫줄.</summary>
        const string FloorOneFirstLine = "광업소 도면 그대로다";
        /// <summary>처음 밟는 원정이 아닌 층(밤사이 새로 지은 갱도) 카드 아랫줄.</summary>
        const string RebuiltLine = "밤사이 무너지고 다시 파였다";
        /// <summary>바구니로 다시 연 층에 내려섰을 때(프로필에서 처음 LandingLineTimes번).</summary>
        const string LandingLine = "승강장은 돌로 쌓아 그대로다. 그 너머 길은 지난번과 다르다.";
        const string LandingLineKey = "landing_line";
        const int LandingLineTimes = 3;
        /// <summary>
        /// 도착 글을 띄우기까지 기다리는 시간(초). 같은 순간에 승강장 말뚝 켜짐·레벨 오름·칸 이름 알림이 몰려
        /// 알림 상한(ToastMax)에 밀려 도착 글이 사라지지 않게, 그 알림들 뒤에 띄운다.
        /// </summary>
        const float ArrivalLineDelay = 0.6f;
        /// <summary>무기 행동 칸 자리(ActionRect 차례 −1 = 가운데에서 −228, 기본 공격 칸 왼쪽).</summary>
        public const int WeaponActSlot = -1;
        /// <summary>행동이 없는 무기의 무기 행동 칸 글 색(회색 '없음').</summary>
        static readonly Color ActNoneColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        static readonly Color MessageColor = new Color(0.86f, 0.8f, 0.68f, 1f);
        static readonly Color QuietColor = new Color(0.66f, 0.61f, 0.53f, 1f);
        static readonly Color FindColor = new Color(0.93f, 0.79f, 0.52f, 1f);
        static readonly Color XpColor = new Color32(0xD6, 0xB4, 0x48, 0xFF);
        /// <summary>경험치를 얻은 순간 새로 찬 몫·둘레 번짐(밝은 호박색).</summary>
        static readonly Color XpFlash = new Color(1f, 0.84f, 0.48f, 1f);
        static readonly Color SlotFill = new Color(0.07f, 0.062f, 0.056f, 1f);
        static readonly Color SlotShade = new Color(0f, 0f, 0f, 0.62f);
        static readonly Color SlotReadyEdge = new Color(0.46f, 0.38f, 0.25f, 1f);
        static readonly Color CardTitleColor = new Color(0.89f, 0.8f, 0.62f, 1f);
        static readonly Color CardLineColor = new Color(0.68f, 0.63f, 0.56f, 1f);
        static readonly Color DownTitleColor = new Color(0.72f, 0.08f, 0.06f, 1f);
        static readonly Color DownWash = new Color(0.17f, 0f, 0f, 1f);

        public static DungeonHud Instance { get; private set; }

        sealed class Toast
        {
            public string Text;
            public float Age;
            public bool Quiet;
            public Color Color;
            /// <summary>글 너비(처음 그릴 때 한 번 잰다).</summary>
            public float Width = -1f;
        }

        /// <summary>값이 바뀔 때만 글을 다시 만드는 작은 보관함.</summary>
        sealed class TextCache
        {
            int _a = int.MinValue;
            int _b = int.MinValue;
            object _key;
            public string Text;

            public bool Stale(int a, int b, object key = null)
            {
                if (Text != null && a == _a && b == _b && ReferenceEquals(key, _key)) return false;
                _a = a;
                _b = b;
                _key = key;
                return true;
            }
        }

        readonly List<Toast> _toasts = new List<Toast>();
        readonly GUIContent _measure = new GUIContent();
        float _cardAge;
        /// <summary>FloorEntered가 알린 '처음 밟는 원정인가'(알리기 전에는 DungeonRoot.FirstVisit).</summary>
        bool? _cardFirstVisit;
        /// <summary>FloorEntered가 정한 도착 글(ArrivalLineDelay 뒤에 띄움). 없으면 null.</summary>
        string _arrivalLine;
        float _arrivalDelay;
        float _bannerAge = 999f;
        float _downAge;
        bool _stylesReady;
        GUIStyle _hudLabel;
        GUIStyle _hudRight;
        GUIStyle _smallRight;
        GUIStyle _hudSmall;
        GUIStyle _healthLabel;
        GUIStyle _orbText;
        GUIStyle _orbCaption;
        GUIStyle _cardRec;
        GUIStyle _downStyle;

        string _cardTitle;
        string _cardLine;
        string _cardRecText;
        string _bannerTitle;
        readonly TextCache _weaponText = new TextCache();
        readonly TextCache _levelText = new TextCache();
        readonly TextCache _hpText = new TextCache();
        readonly TextCache _potionText = new TextCache();
        readonly TextCache _xpText = new TextCache();
        readonly TextCache _infoText = new TextCache();
        readonly TextCache _pointText = new TextCache();
        readonly TextCache[] _cooldownText = { new TextCache(), new TextCache(), new TextCache() };
        readonly TextCache _actTip = new TextCache();
        readonly GUIContent _actTipContent = new GUIContent();
        readonly TextCache _actState = new TextCache();
        readonly TextCache _actCooldown = new TextCache();
        /// <summary>NoWeaponActPressed를 듣고 있는 플레이어. 바뀌거나 사라지면 풀고 다시 건다(도메인 다시 불러오기가 꺼져 있어 남은 구독이 없게).</summary>
        PlayerController _hookedPlayer;

        public static int RecommendedLevel(int floor) => RecommendedLevels[Mathf.Clamp(floor, 1, RecommendedLevels.Length) - 1];

        /// <summary>층 이름 카드의 음울한 한 줄.</summary>
        public static string FloorLine(int floor) => floor >= 1 && floor <= FloorLines.Length ? FloorLines[floor - 1] : FloorLineDefault;

        /// <summary>
        /// 층 이름 카드 아랫줄(매판 새 탐험 1차 2-2): 1층 첫 원정은 "광업소 도면 그대로다", 처음 밟는 원정이 아니면(어느 층이든)
        /// "밤사이 무너지고 다시 파였다", 그 밖(2층부터 처음 밟는 원정)은 층마다 한 줄(FloorLine).
        /// </summary>
        public static string CardLine(int floor, bool firstVisit)
        {
            if (!firstVisit) return RebuiltLine;
            return floor == 1 ? FloorOneFirstLine : FloorLine(floor);
        }

        void Awake()
        {
            Instance = this;
            if(!GetComponent<DungeonMiniMap>())gameObject.AddComponent<DungeonMiniMap>();
            DungeonEvents.Message += OnMessage;
            DungeonEvents.Discovered += OnDiscovered;
            DungeonEvents.LevelUp += OnLevelUp;
            DungeonEvents.LandingLit += OnLandingLit;
            DungeonEvents.FloorEntered += OnFloorEntered;
        }

        void OnDestroy()
        {
            DungeonEvents.Message -= OnMessage;
            DungeonEvents.Discovered -= OnDiscovered;
            DungeonEvents.LevelUp -= OnLevelUp;
            DungeonEvents.LandingLit -= OnLandingLit;
            DungeonEvents.FloorEntered -= OnFloorEntered;
            HookPlayer(null);
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 행동 없는 무기 오른쪽 클릭 알림(5-8·5-9)을 들을 플레이어를 맞춘다. 같은 플레이어면 아무것도 안 한다.
        /// 사라진 플레이어(Unity null)도 C# 사건은 남아 있으므로 참조로 비교해 풀고, 새 플레이어에 다시 건다.
        /// </summary>
        void HookPlayer(PlayerController player)
        {
            if (!player) player = null;
            if (ReferenceEquals(_hookedPlayer, player)) return;
            if (!ReferenceEquals(_hookedPlayer, null)) _hookedPlayer.NoWeaponActPressed -= OnNoWeaponActPressed;
            _hookedPlayer = player;
            if (!ReferenceEquals(player, null)) player.NoWeaponActPressed += OnNoWeaponActPressed;
        }

        /// <summary>
        /// 행동이 없는 무기(나머지 6종)로 오른쪽 클릭: 프로필에서 처음 NoActHintTimes(2)번만 한 줄 알림(꾸러미 세기 rmb_none).
        /// 세기는 띄울 때만 올려 횟수가 끝없이 늘지 않는다. 프로필이 없으면(시험 장면) 띄우지 않는다.
        /// </summary>
        void OnNoWeaponActPressed()
        {
            var data = ProfileCarry.Data;
            if (data == null || data.Count(WeaponActCommon.NoActHintKey) >= WeaponActCommon.NoActHintTimes) return;
            if (data.Bump(WeaponActCommon.NoActHintKey) <= WeaponActCommon.NoActHintTimes)
                Push(WeaponActCommon.NoActHintText, false, MessageColor);
        }

        /// <summary>이 장면에서 처음 불을 켠 승강장 층(DungeonRoot가 FloorEntered 바로 앞에 알림). 없으면 0.</summary>
        int _litFloor;

        void OnLandingLit(int floor) => _litFloor = floor;

        /// <summary>
        /// 화면이 밝아질 때(밤 카드·승강장 고르기 뒤) 층 이름 카드를 처음부터 다시 띄우고 도착 글을 한 줄 정한다(ArrivalLineDelay 뒤에 띄움).
        /// 아래층 승강장에 처음 불을 켰으면(계단으로 왔든, 계단 앞 말뚝으로 줄만 늘이고 바구니로 처음 내려왔든) 그 글,
        /// 바구니로 다시 연 층이면 승강장 글(프로필에서 처음 3번, 꾸러미 세기).
        /// </summary>
        void OnFloorEntered(int floor, bool firstVisit, ArrivalKind arrival)
        {
            _cardFirstVisit = firstVisit;
            _cardTitle = null;
            _cardAge = 0f;
            _arrivalLine = null;
            // 오우거 굴에는 승강장이 없어 도착 글을 띄우지 않는다(굴 앞 말뚝 글은 말뚝이 띄움).
            var root = DungeonRoot.Instance;
            bool den = root && root.IsDen;
            if (!den && _litFloor == floor && floor > 1)
                _arrivalLine = floor + "층 승강장에 불을 켰다. 다음엔 여기서 시작한다.";
            else if (!den && arrival == ArrivalKind.Basket && !firstVisit && ProfileCarry.Data?.Bump(LandingLineKey) <= LandingLineTimes)
                _arrivalLine = LandingLine;
            _litFloor = 0;
            _arrivalDelay = ArrivalLineDelay;
            // 옛 '회오리는 이제 E' 알림은 지웠다(키 배치 1차 0장 7). 옛 꾸러미에 남은 whirl_e 세기는 이제 아무도 보지 않는다.
        }

        void Update()
        {
            // 멈춘 동안(지도·창)은 알림이 늙지 않아 놓치지 않는다. 첫 프레임의 긴 시간은 자른다.
            float dt = TimeScaleService.Paused ? 0f : Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                _toasts[i].Age += dt;
                if (_toasts[i].Age >= ToastLife + ToastFade) _toasts.RemoveAt(i);
            }
            _cardAge += dt;
            _bannerAge += dt;
            if (_arrivalLine != null)
            {
                _arrivalDelay -= dt;
                if (_arrivalDelay <= 0f)
                {
                    string line = _arrivalLine;
                    _arrivalLine = null;
                    DungeonEvents.Say(line);
                }
            }
            var root = DungeonRoot.Instance;
            var player = root ? root.Player : null;
            HookPlayer(player);
            if (player && player.IsDown) _downAge += dt;
            else _downAge = 0f;
        }

        // ── 알림 ─────────────────────────────────────────────

        void OnMessage(string text) => Push(text, false, MessageColor);

        void OnDiscovered(DiscoveryKind kind, Vector2 pos, string label)
        {
            switch (kind)
            {
                case DiscoveryKind.NewCell:
                    // 디아블로식 지역 이름: 조용히 이름만.
                    Push("— " + label + " —", true, QuietColor);
                    break;
                case DiscoveryKind.FloorComplete:
                    // 층 완전 탐험 알림은 레벨 모듈이 문구로 띄운다.
                    break;
                default:
                    Push(DiscoveryText(kind, label), false, FindColor);
                    break;
            }
        }

        void OnLevelUp(int level)
        {
            _bannerTitle = "레벨 " + level;
            _bannerAge = 0f;
        }

        /// <summary>알림 한 줄. 같은 글이 아직 떠 있으면 새로 쌓지 않고 다시 맨 아래로 올린다(F 연타 등).</summary>
        public void Push(string text, bool quiet, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            for (int i = 0; i < _toasts.Count; i++)
            {
                if (_toasts[i].Text != text) continue;
                var same = _toasts[i];
                _toasts.RemoveAt(i);
                same.Age = Mathf.Min(same.Age, 0.15f);
                _toasts.Add(same);
                return;
            }
            _toasts.Add(new Toast { Text = text, Quiet = quiet, Color = color });
            while (_toasts.Count > ToastMax) _toasts.RemoveAt(0);
        }

        /// <summary>발견 알림 머리글(짧게). 뜻을 풀어 주는 음울한 한 줄은 물체 쪽 알림(DungeonEvents.Say)이 붙인다.</summary>
        static string DiscoveryText(DiscoveryKind kind, string label)
        {
            string name = string.IsNullOrEmpty(label) ? KindName(kind) : label;
            switch (kind)
            {
                case DiscoveryKind.WallLamp: return "벽 등잔 — 어둠이 한 걸음 물러난다";
                case DiscoveryKind.Stake: return name + " — 희미한 불이 깃든다";
                case DiscoveryKind.HiddenRoom: return "숨은 방";
                case DiscoveryKind.Shortcut: return "지름길";
                case DiscoveryKind.Ability: return "손에 넣었다 · " + name;
                case DiscoveryKind.Story: return "남겨진 흔적 · " + name;
                case DiscoveryKind.Event: return "사건 · " + name;
                case DiscoveryKind.WoodChest: return name + " — 삐걱이며 열렸다";
                case DiscoveryKind.IronChest: return name + " — 녹슨 경첩이 비명을 지른다";
                case DiscoveryKind.Ore: return name + " — 돌이 부서져 내린다";
                default: return name;
            }
        }

        static string KindName(DiscoveryKind kind)
        {
            switch (kind)
            {
                case DiscoveryKind.WoodChest: return "나무 궤짝";
                case DiscoveryKind.IronChest: return "쇠 궤짝";
                case DiscoveryKind.Stake: return "말뚝";
                case DiscoveryKind.Story: return "이야기 물건";
                case DiscoveryKind.Event: return "사건";
                case DiscoveryKind.Ability: return "능력";
                case DiscoveryKind.Ore: return "광맥";
                case DiscoveryKind.Safe: return "금고";
                default: return "새 것";
            }
        }

        // ── 그리기 ───────────────────────────────────────────

        void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;
            _hudLabel = new GUIStyle(DungeonUi.Bold) { wordWrap = false };
            SetWhite(_hudLabel);
            _hudRight = new GUIStyle(DungeonUi.Bold) { wordWrap = false, alignment = TextAnchor.UpperRight };
            SetWhite(_hudRight);
            _smallRight = new GUIStyle(DungeonUi.Small) { wordWrap = false, clipping = TextClipping.Overflow, alignment = TextAnchor.UpperRight };
            _hudSmall = new GUIStyle(DungeonUi.Small) { wordWrap = false, clipping = TextClipping.Overflow };
            SetWhite(_hudSmall);
            _healthLabel = new GUIStyle(_hudSmall) { alignment = TextAnchor.MiddleCenter };
            SetWhite(_smallRight);
            _orbText = new GUIStyle(DungeonUi.Bold) { fontSize = 20, wordWrap = false, alignment = TextAnchor.MiddleCenter };
            SetWhite(_orbText);
            _orbCaption = new GUIStyle(DungeonUi.Small) { fontSize = 16, wordWrap = false, alignment = TextAnchor.MiddleCenter, font = DungeonUi.Serif };
            SetWhite(_orbCaption);
            _cardRec = new GUIStyle(DungeonUi.Small) { fontSize = 15, wordWrap = false, alignment = TextAnchor.MiddleCenter, font = DungeonUi.Serif };
            SetWhite(_cardRec);
            _downStyle = new GUIStyle(DungeonUi.Display) { fontSize = 76 };
            SetWhite(_downStyle);
        }

        static void SetWhite(GUIStyle s)
        {
            s.normal.textColor = Color.white;
            s.hover.textColor = Color.white;
        }

        void OnGUI()
        {
            // Painted action and utility buttons receive mouse events as well as repaint.
            var root = DungeonRoot.Instance;
            if (!root || root.State == null) return;
            DungeonUi.Begin();
            EnsureStyles();
            GUI.depth = 5;
            var player = root.Player;
            if (player && player.IsDown) DrawDown();
            if (player) DrawPanel(root, player);
            if (player && !player.IsDown) DrawWalkState(player);
            DrawToasts();
            DrawFloorCard(root);
            DrawLevelBanner();
            if (player && player.IsDown) DrawDownText();
        }

        public static Rect CombatRect => UiV45.CombatRect;

        /// <summary>걸음 상태 한 줄씩: 웅크림(CrouchRules.HudLabel), 아는 길(ExploreWalk.Active). 기능만 둔다(배치는 Unity UI 단계에서).</summary>
        void DrawWalkState(PlayerController p)
        {
            var r = CombatRect;
            float y = r.y - 34f;
            if (p.Crouching)
            {
                DungeonUi.ShadowLabel(new Rect(r.x + 14f, y, 186f, 22f), CrouchRules.HudLabel, DungeonUi.Small, DungeonUi.Bone);
                y -= 22f;
            }
            var walk = ExploreWalk.Instance;
            if (walk && walk.Active) DungeonUi.ShadowLabel(new Rect(r.x + 14f, y, 186f, 22f), ExploreBadge, DungeonUi.Small, DungeonUi.BoneDim);
        }


        public static Rect ActionRect(int index) => UiV45.ActionRect(index);
        public static Rect UtilityRect(int index) => UiV45.UtilityRect(index);
        public static Rect ExperienceRect => new Rect(16f, DungeonUi.Height - 12f, DungeonUi.Width - 32f, 7f);
        public static bool PointerOverHud
        {
            get
            {
                if (LootLabels.BlocksWorldPointer) return true;
                if (!Instance || UnityEngine.InputSystem.Mouse.current == null) return false;
                var p = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                p = new Vector2(p.x, Screen.height - p.y) / DungeonUi.Scale;
                // 무기 행동 칸(WeaponActSlot = −1)도 센다: 클릭은 받지 않지만 그 위 클릭이 공격으로 새지 않게.
                for (int i = WeaponActSlot; i < 5; i++) if (ApprovedUiV5.HudHitRect(ActionRect(i)).Contains(p)) return true;
                for (int i = 0; i < 2; i++) if (ApprovedUiV5.HudHitRect(UtilityRect(i)).Contains(p)) return true;
                if(UiV45.OrbRect.Contains(p))return true;
                if(PlayerController.Instance && PlayerController.Instance.GuardGaugeAlpha>0.001f && UiV45.GuardHitRect.Contains(p))return true;
                return DungeonMiniMap.Visible && DungeonMiniMap.PanelRect.Contains(p);
            }
        }

        void DrawPanel(DungeonRoot root, PlayerController p)
        {
            var progress = PlayerProgress.Instance;
            var hp = p.Health;
            UiV45.DrawBelt();
            UiV45.DrawOrb(p,progress?progress.Level:1);
            DrawAction(p,0,"attack","좌클릭","기본 공격 · 좌클릭",0,0,!p.IsDown,p.Pose==PlayerPose.Attack,0);
            // 회오리는 E(오른쪽 클릭은 무기 행동, 기획/세-무기-우클릭-소켓-1차.md 5-9). 칸 클릭(비트 2)은 그대로.
            // 배우지 않았거나 투지가 모자라면 회색(기획/스킬-자원-트리-1차.md). 말풍선에 까닭을 붙인다.
            DrawAction(p,1,"whirl",WeaponActCommon.WhirlKeyLabel,SkillTip("회오리 · "+WeaponActCommon.WhirlKeyLabel,p.WhirlKnown,Demo6.Core.Progression.SpiritRules.WhirlCost),p.WhirlCooldown,p.WhirlCooldownMax,!p.IsDown&&p.WhirlAffordable,p.Pose==PlayerPose.Whirl,0);
            DrawAction(p,2,"wave","Q",SkillTip("검풍 · Q",p.WaveKnown,Demo6.Core.Progression.SpiritRules.WaveCost),p.WaveCooldown,p.WaveCooldownMax,!p.IsDown&&p.WaveAffordable,p.Pose==PlayerPose.WaveCast,0);
            DrawAction(p,3,"dodge","Space","구르기 · Space",p.DodgeCooldown,p.DodgeCooldownMax,!p.IsDown,p.Pose==PlayerPose.Dodge,0);
            // 식은 주먹밥(묶음 5-7): 물약이 떨어지면 R로 먹는다. 칸 수 옆에 '+밥'.
            int rice=p.RiceBalls;
            DrawAction(p,4,"potion","R",rice>0?"물약 · R · 식은 주먹밥 "+rice+"(물약이 없을 때 R)":"물약 · R",p.PotionCooldown,p.PotionCooldownMax,!p.IsDown&&(p.Potions>0||rice>0),false,p.Potions);
            if(rice>0){var pr=ActionRect(4);DungeonUi.ShadowLabel(new Rect(pr.x+2,pr.y+2,30,18),"+밥",ApprovedUiV5.Style(11,TextAnchor.UpperLeft,true),ApprovedUiV5.Gold);}
            DrawWeaponAct(p);
            DrawUtility(0,"bag","I","가방",Inventory.BagWindow,false);
            DrawBagCount(); // 가방 칸 'n/칸'(재화 쓸 곳 1차 5-4, DungeonHud.Bag.cs)

            DrawUtility(1,"skills","K","스킬 트리",SkillPanel.ModalName,progress&&progress.SkillPoints>0);
            // 기름 병(묶음 5-6): 칸 띠 오른쪽 끝에 '기름 n'. 없으면 흐리게.
            if(root.Leg!=null)
            {
                var u=UtilityRect(1);var oilRect=new Rect(u.xMax+10,u.y+6,74,u.height-12);
                int oil=root.Leg.Oil;
                DungeonUi.ShadowLabel(oilRect,"기름 "+oil,ApprovedUiV5.Style(14,TextAnchor.MiddleLeft,true),oil>0?ApprovedUiV5.Gold:ApprovedUiV5.Muted);
                GUI.Label(oilRect,new GUIContent("","기름 병 "+oil+" — 벽 등잔 하나에 1병 · 궤짝·도시락통·막다른 곳에서 나온다"),GUIStyle.none);
            }
            float fraction = progress && progress.XpToNext>0 ? (float)progress.Xp/progress.XpToNext : 0;
            ApprovedUiV5.Image(new Rect(10,DungeonUi.Height-17,DungeonUi.Width-20,13),"xp-cradle");
            DrawXpBar(ExperienceRect,fraction,progress);
            var cell=root.CurrentCell;
            DungeonUi.ShadowLabel(new Rect(Margin,Margin,390,26),"제"+root.Floor+"층 · "+(cell!=null?cell.Name:"-"),DungeonUi.Small,DungeonUi.BoneDim);
            if (!DungeonUi.ModalOpen && Event.current.type==EventType.Repaint && !string.IsNullOrEmpty(GUI.tooltip))
            {
                var tip=new Rect(DungeonUi.Width*.5f-210,DungeonUi.Height-176,420,31);
                DungeonUi.Box(tip,.94f);DungeonUi.ShadowLabel(tip,GUI.tooltip,DungeonUi.SmallCenter,DungeonUi.Bone);
            }
        }

        static string SkillTip(string label,bool known,float cost)=>known?label+" · 투지 "+cost:label+" · 아직 모름(마을에서 배움)";

        void DrawAction(PlayerController player,int index,string icon,string key,string label,float remaining,float maximum,bool usable,bool active,int charges)
        {
            var r=ActionRect(index);bool cooling=remaining>0;bool previous=GUI.enabled;
            GUI.enabled=previous&&usable&&!cooling&&!DungeonUi.ModalOpen&&!TimeScaleService.Paused;
            bool click=GUI.Button(r,new GUIContent("",label+" · 클릭으로도 사용"),GUIStyle.none);
            GUI.enabled=previous;
            string actualIcon=icon=="attack"?(Inventory.Instance?.Equipment.Weapon?.Base.IconId??"wpn_longsword"):icon;
            ApprovedUiV5.Icon(new Rect(r.x+7,r.y+7,r.width-14,r.height-14),actualIcon,usable?Color.white:new Color(.48f,.48f,.48f,1),Inventory.Instance?.Equipment.Weapon?.Grade??Demo6.Core.Loot.Grade.Common);
            if (active) UiSkinArt.Selection(r);
            else if (!DungeonUi.ModalOpen&&r.Contains(Event.current.mousePosition)&&usable&&!cooling)
                DungeonUi.Outline(new Rect(r.x+3,r.y+3,r.width-6,r.height-6),DungeonUi.Ember);
            if(cooling)
            {
                float fraction=maximum>0?Mathf.Clamp01(remaining/maximum):1;
                DungeonUi.Fill(new Rect(r.x+4,r.y+4,r.width-8,(r.height-8)*fraction),new Color(0,0,0,.76f));
                ApprovedUiV5.Cooldown(new Rect(r.x,r.y+15,r.width,24),remaining>=10?Mathf.CeilToInt(remaining).ToString():remaining.ToString("0.0"));
            }
            if(index==4)DungeonUi.ShadowLabel(new Rect(r.xMax-24,r.y+3,20,22),charges.ToString(),DungeonUi.SmallCenter,DungeonUi.Bone);
            var keyRect=new Rect(r.x-2,r.yMax+1,r.width+4,17);
            ApprovedUiV5.Key(keyRect,key);
            if(click)player.GetComponent<PlayerInputReader>()?.QueueHudAction(index);
        }

        /// <summary>
        /// 무기 행동 칸(가운데 −228, 기획/세-무기-우클릭-소켓-1차.md 5-9, IMGUI 기능만): 행동 이름(막기·기 모으기·난사)과 키 '우클릭'.
        /// 누르는 행동이라 클릭은 받지 않는다(QueueHudAction 없음, 말풍선만). 난사는 재사용을 덮고, 행동 중이면 강조하며
        /// 기 모으기 단계·난사 판정 수·끊김을 둘째 줄에 보인다. 행동이 없는 무기(나머지 6종)는 회색 '없음'.
        /// </summary>
        void DrawWeaponAct(PlayerController p)
        {
            var r = ActionRect(WeaponActSlot);
            var kind = p.ActKind;
            bool has = kind != WeaponActKind.None;
            bool usable = has && !p.IsDown;
            float remaining = kind == WeaponActKind.Flurry ? p.ActCooldown : 0f;
            float maximum = p.ActCooldownMax;
            bool cooling = remaining > 0f;
            if (_actTip.Stale((int)kind, 0))
                _actTip.Text = has
                    ? WeaponActRules.Name(kind) + " · " + WeaponActCommon.ActKeyLabel
                    : "무기 행동 없음 · 회오리는 " + WeaponActCommon.WhirlKeyLabel;
            // 클릭을 받지 않는 글 칸(말풍선만). 칸 위 클릭은 PointerOverHud가 공격으로 새지 않게 막는다.
            _actTipContent.tooltip = _actTip.Text;
            GUI.Label(r, _actTipContent, GUIStyle.none);
            if (p.InWeaponAct) UiSkinArt.Selection(r);
            DungeonUi.ShadowLabel(new Rect(r.x+2, r.y + 4f, r.width-4, 20f), WeaponActRules.Name(kind), ApprovedUiV5.Style(11,TextAnchor.MiddleCenter,true), usable ? DungeonUi.Bone : ActNoneColor);
            if (cooling)
            {
                float fraction = maximum > 0f ? Mathf.Clamp01(remaining / maximum) : 1f;
                DungeonUi.Fill(new Rect(r.x + 4f, r.y + 4f, r.width - 8f, (r.height - 8f) * fraction), new Color(0f, 0f, 0f, 0.76f));
                int tenths = Mathf.CeilToInt(remaining * 10f);
                if (_actCooldown.Stale(tenths, 0)) _actCooldown.Text = remaining >= 10f ? Mathf.CeilToInt(remaining).ToString() : (tenths / 10f).ToString("0.0");
                DungeonUi.ShadowLabel(new Rect(r.x+2, r.y + 23f, r.width-4, 18f), _actCooldown.Text, ApprovedUiV5.Style(11,TextAnchor.MiddleCenter), DungeonUi.Bone);
            }
            else
            {
                // 둘째 줄: 끊김 경직 > 기 모으기 단계 > 놓아 베기 단계 > 난사 판정 수(행동 중에만).
                int a = -1;
                int b = 0;
                if (p.Flinching) a = 0;
                else if (p.InWeaponAct && kind == WeaponActKind.Charge) { a = p.ActPhase == WeaponActPhase.Release ? 2 : 1; b = a == 2 ? p.ReleaseLevel : p.ChargeLevel; }
                else if (p.InWeaponAct && kind == WeaponActKind.Flurry) { a = 3; b = p.ActHitsDone; }
                if (a >= 0)
                {
                    if (_actState.Stale(a, b))
                        _actState.Text = a == 0 ? WeaponActCommon.InterruptedWord
                            : a == 1 ? b + "단계"
                            : a == 2 ? "베기 " + b
                            : b + "/" + TwinFlurry.HitCount;
                    DungeonUi.ShadowLabel(new Rect(r.x+2, r.y + 23f, r.width-4, 18f), _actState.Text, ApprovedUiV5.Style(11,TextAnchor.MiddleCenter), a == 0 ? DungeonUi.BoneDim : DungeonUi.Ember);
                }
            }
            var keyRect = new Rect(r.x-2,r.yMax+1,r.width+4,17);
            ApprovedUiV5.Key(keyRect,WeaponActCommon.ActKeyLabel);
        }

        static void DrawUtility(int index,string icon,string key,string label,string modal,bool notify)
        {
            var r=UtilityRect(index);bool previous=GUI.enabled;GUI.enabled=previous&&!DungeonUi.ModalOpen;
            bool click=GUI.Button(r,new GUIContent("",label+" · "+key),GUIStyle.none);GUI.enabled=previous;
            ApprovedUiV5.Icon(new Rect(r.x+5,r.y+5,r.width-10,r.height-10),icon,Color.white);
            ApprovedUiV5.Key(new Rect(r.x,r.yMax+1,r.width,17),key);
            if(notify){DungeonUi.Fill(new Rect(r.xMax-9,r.y+3,6,6),DungeonUi.Ember);}
            if(click)DungeonUi.TryOpen(modal);
        }

        /// <summary>
        /// 경험치 막대. 얻은 뒤 0.4초(PlayerProgress.GainFlashSeconds) 동안: 막대 둘레가 호박색으로 번지고, 새로 찬 몫이 앞 0.14초에 차오르며
        /// 밝은 호박색에서 원래 색으로 식고, 차오르는 끝에 밝은 선이 선다. 처치·둥지 정리는 세게, 탐험 경험치는 약하게.
        /// 돌려주는 값은 번쩍임 세기(0~1, 글 색에 쓴다). 경험치 양·레벨 표는 건드리지 않는다.
        /// </summary>
        static float DrawXpBar(Rect r, float frac, PlayerProgress progress)
        {
            float k = 0f;
            float strength = 0f;
            if (progress)
            {
                float age = Time.unscaledTime - progress.GainFlashStart;
                if (age >= 0f && age < PlayerProgress.GainFlashSeconds)
                {
                    k = 1f - age / PlayerProgress.GainFlashSeconds;
                    strength = progress.GainFromKill ? 1f : 0.55f;
                }
            }
            if (k <= 0f)
            {
                DungeonUi.Bar(r, frac, XpColor);
                return 0f;
            }
            float glow = k * strength;
            // 둘레 번짐(막대 뒤에 깐다).
            DungeonUi.Fill(new Rect(r.x - 5f, r.y - 5f, r.width + 10f, r.height + 10f), new Color(XpFlash.r, XpFlash.g, XpFlash.b, 0.5f * glow));
            float from = Mathf.Clamp(progress.GainFromFraction, 0f, frac);
            DungeonUi.Bar(r, from, XpColor);
            // 새로 찬 몫: 앞 0.14초에 차오르고(끝이 느려짐) 밝은 색에서 식는다.
            float grow = Mathf.Clamp01((PlayerProgress.GainFlashSeconds - k * PlayerProgress.GainFlashSeconds) / 0.14f);
            grow = 1f - (1f - grow) * (1f - grow);
            float shown = Mathf.Lerp(from, frac, grow);
            float innerW = r.width - 2f;
            var seg = new Rect(r.x + 1f + innerW * from, r.y + 1f, innerW * (shown - from), r.height - 2f);
            if (seg.width > 0f)
            {
                DungeonUi.Fill(seg, Color.Lerp(XpColor, XpFlash, glow));
                DungeonUi.Fill(new Rect(seg.x, seg.y, seg.width, Mathf.Max(1f, seg.height * 0.3f)), new Color(1f, 1f, 1f, 0.25f * glow));
                // 차오르는 끝의 밝은 선.
                DungeonUi.Fill(new Rect(seg.xMax - 1f, r.y - 2f, 2f, r.height + 4f), new Color(1f, 0.95f, 0.8f, 0.9f * glow));
            }
            return glow;
        }

        void Cooldown(int slot, Rect rect, string label, float remaining, float max)
        {
            bool cooling = remaining > 0f && max > 0f;
            DungeonUi.Slot(rect);
            if (cooling) DungeonUi.Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(remaining / max), rect.height), SlotShade);
            DungeonUi.Outline(rect, cooling ? DungeonUi.IronEdge : SlotReadyEdge, 1f);
            var prev = GUI.color;
            GUI.color = DungeonUi.BoneDim;
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 8f, 28f), label, DungeonUi.Small);
            GUI.color = prev;
            int tenths = cooling ? Mathf.CeilToInt(remaining * 10f) : 0;
            var cache = _cooldownText[slot];
            if (cache.Stale(tenths, 0)) cache.Text = tenths > 0 ? (tenths / 10f).ToString("0.0") + "초" : "준비";
            DungeonUi.ShadowLabel(new Rect(rect.x + 6f, rect.y + 25f, rect.width - 8f, 24f), cache.Text, _hudLabel, cooling ? DungeonUi.BoneDim : DungeonUi.Bone);
        }

        /// <summary>화면 아래 가운데, 판 위에 새 알림이 아래로 쌓인다. 양 끝이 흐려지는 검은 띠 위 바탕체 글.</summary>
        void DrawToasts()
        {
            if (_toasts.Count == 0) return;
            float reserved = ExplorationLog.Instance ? ExplorationLog.Instance.PanelReservedWidth : 0f;
            float cx = (DungeonUi.Width - reserved) * 0.5f;
            float y = DungeonUi.Height - PanelHeight - Margin - 16f;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                float a = Mathf.Clamp01(t.Age / 0.18f) * (t.Age < ToastLife ? 1f : Mathf.Clamp01(1f - (t.Age - ToastLife) / ToastFade));
                var style = t.Quiet ? DungeonUi.ToastQuiet : DungeonUi.Toast;
                float h = t.Quiet ? 26f : 32f;
                if (t.Width < 0f)
                {
                    _measure.text = t.Text;
                    t.Width = style.CalcSize(_measure).x;
                }
                float w = Mathf.Min(DungeonUi.Width - 40f, t.Width + 160f);
                var r = new Rect(cx - w * 0.5f, y - h, w, h);
                DungeonUi.Strip(r, new Color(0f, 0f, 0f, (t.Quiet ? 0.5f : 0.72f) * a));
                DungeonUi.ShadowLabel(r, t.Text, style, new Color(t.Color.r, t.Color.g, t.Color.b, a * (t.Quiet ? 0.85f : 1f)));
                y -= h + 6f;
            }
        }

        /// <summary>
        /// 층에 처음 들어가면 크고 느린 층 이름 카드(2-2, 기준 문서 '화면 연출'): "제1층 — 입구 갱도", 아랫줄(CardLine), 권장 레벨.
        /// 양 끝이 흐린 검은 띠 위에 바탕체 큰 글, 위아래 바랜 뼈색 줄. 약 3.6초 동안 천천히 떠올라 천천히 사라진다.
        /// </summary>
        void DrawFloorCard(DungeonRoot root)
        {
            if (_cardAge >= CardTime) return;
            if (_cardTitle == null)
            {
                string name = root.Map != null ? root.Map.Name : "";
                // 오우거 굴(전투·보스 문서 3-8): "제2층 바닥 — 오우거 굴" / "안쪽에서 돌 씹는 소리가 난다". 권장 레벨은 층 그대로.
                _cardTitle = root.IsDen ? OgreDen.CardTitle : "제" + root.Floor + "층 — " + name;
                _cardLine = root.IsDen ? OgreDen.CardLine : CardLine(root.Floor, _cardFirstVisit ?? root.FirstVisit);
                _cardRecText = "권장 레벨 " + RecommendedLevel(root.Floor);
            }
            float a = Mathf.Clamp01(_cardAge / CardFadeIn) * Mathf.Clamp01((CardTime - _cardAge) / CardFadeOut);
            a = a * a * (3f - 2f * a);
            float w = DungeonUi.Width;
            // 천천히 가라앉는다(무게).
            float sink = 8f * Mathf.Clamp01(_cardAge / CardTime);
            float top = 92f + sink;
            DungeonUi.Strip(new Rect(0f, top, w, 168f), new Color(0f, 0f, 0f, 0.78f * a));
            var lineColor = new Color(DungeonUi.Bone.r, DungeonUi.Bone.g, DungeonUi.Bone.b, 0.42f * a);
            DungeonUi.Strip(new Rect(w * 0.2f, top + 10f, w * 0.6f, 1f), lineColor);
            DungeonUi.Strip(new Rect(w * 0.2f, top + 157f, w * 0.6f, 1f), lineColor);
            DungeonUi.ShadowLabel(new Rect(0f, top + 22f, w, 66f), _cardTitle, DungeonUi.Display,
                new Color(CardTitleColor.r, CardTitleColor.g, CardTitleColor.b, a));
            // 한 줄과 권장 레벨은 조금 늦게 떠오른다.
            float b = Mathf.Clamp01((_cardAge - 0.35f) / CardFadeIn) * Mathf.Clamp01((CardTime - _cardAge) / CardFadeOut);
            DungeonUi.ShadowLabel(new Rect(0f, top + 92f, w, 30f), _cardLine, DungeonUi.Subtitle,
                new Color(CardLineColor.r, CardLineColor.g, CardLineColor.b, 0.95f * b));
            DungeonUi.ShadowLabel(new Rect(0f, top + 124f, w, 24f), _cardRecText, _cardRec,
                new Color(DungeonUi.Ember.r, DungeonUi.Ember.g, DungeonUi.Ember.b, 0.8f * b));
        }

        /// <summary>레벨업 무거운 띠(4-3: 시간은 멈추지 않는다). 위에서 조금 내려앉으며 나타난다.</summary>
        void DrawLevelBanner()
        {
            if (_bannerAge >= BannerTime || _bannerTitle == null) return;
            float a = Mathf.Clamp01(_bannerAge / BannerFadeIn) * Mathf.Clamp01((BannerTime - _bannerAge) / BannerFadeOut);
            float drop = 1f - Mathf.Clamp01(_bannerAge / 0.3f);
            // 싸우는 중에 뜨므로 플레이어 머리 위(화면 가운데)를 가리지 않게 위쪽에 좁고 옅게(빨간 예고가 가려지지 않게).
            float top = 150f - 14f * drop * drop;
            float w = DungeonUi.Width;
            DungeonUi.Strip(new Rect(w * 0.2f, top, w * 0.6f, 104f), new Color(0f, 0f, 0f, 0.6f * a));
            var lineColor = new Color(DungeonUi.Ember.r, DungeonUi.Ember.g, DungeonUi.Ember.b, 0.45f * a);
            DungeonUi.Strip(new Rect(w * 0.3f, top + 6f, w * 0.4f, 2f), lineColor);
            DungeonUi.Strip(new Rect(w * 0.3f, top + 96f, w * 0.4f, 2f), lineColor);
            DungeonUi.ShadowLabel(new Rect(0f, top + 12f, w, 50f), _bannerTitle, DungeonUi.Banner,
                new Color(DungeonUi.Ember.r, DungeonUi.Ember.g, DungeonUi.Ember.b, a));
            DungeonUi.ShadowLabel(new Rect(0f, top + 64f, w, 26f), BannerLine, DungeonUi.Subtitle,
                new Color(DungeonUi.Bone.r, DungeonUi.Bone.g, DungeonUi.Bone.b, 0.85f * a));
        }

        /// <summary>쓰러짐: 화면이 붉게 어두워진다(판·알림보다 아래에 깔림).</summary>
        void DrawDown()
        {
            float k = Mathf.Clamp01(_downAge / DownDarken);
            k = k * k * (3f - 2f * k);
            var full = new Rect(0f, 0f, DungeonUi.Width, DungeonUi.Height);
            DungeonUi.Fill(full, new Color(DownWash.r, DownWash.g, DownWash.b, 0.55f * k));
            DungeonUi.Vignette(full, new Color(0f, 0f, 0f, 0.9f * k));
        }

        /// <summary>쓰러짐 큰 글자와 그 아래 작은 글(맨 위에).</summary>
        void DrawDownText()
        {
            float w = DungeonUi.Width;
            float k = Mathf.Clamp01(_downAge / 0.8f);
            float y = DungeonUi.Height * 0.38f;
            DungeonUi.ShadowLabel(new Rect(0f, y, w, 80f), DownTitle, _downStyle,
                new Color(DownTitleColor.r, DownTitleColor.g, DownTitleColor.b, k), 1f);
            float k2 = Mathf.Clamp01((_downAge - 0.5f) / 0.8f);
            DungeonUi.ShadowLabel(new Rect(0f, y + 84f, w, 30f), DownLine, DungeonUi.Subtitle,
                new Color(DungeonUi.Bone.r, DungeonUi.Bone.g, DungeonUi.Bone.b, 0.85f * k2));
        }
    }
}
