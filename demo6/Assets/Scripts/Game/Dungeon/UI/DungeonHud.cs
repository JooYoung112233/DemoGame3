using System.Collections.Generic;
using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 던전 화면(IMGUI). 왼쪽 아래: 디아블로식 생명 구슬(짙은 피색, 체력) + 검은 쇠 판(무기·공격력, 레벨, 물약 병, 회오리·검풍·구르기 재사용,
    /// 경험치, 강화석·골드·지금 칸, 스킬 점수, 키 안내). 화면 아래 가운데 알림(DungeonEvents.Message·Discovered, 4줄까지, 3초 뒤 흐려짐),
    /// 층에 처음 들어갈 때 크고 느린 층 이름 카드("제1층 — 입구 갱도" + 음울한 한 줄 + 권장 레벨, 3차 초안 2-2), 레벨업 무거운 띠(4-3),
    /// 탐험 걸음 표시(2-8), 쓰러지면 붉게 어두워지는 화면과 큰 글자. 경험치를 얻으면 막대가 0.4초 밝게 번쩍이며 새로 찬 몫을 보인다(한 마리 RPG 요소).
    /// 다크 판타지 1차(기획/다크판타지-분위기-1차.md '글'·'화면 연출'): 짧고 음울한 말투, 검은 쇠·뼈색 틀, 바탕체 제목.
    /// 정식 화면은 Unity 개발 단계(uGUI)에서 다시 만든다. 여기서는 판정에 필요한 정보만 둔다. 글은 값이 바뀔 때만 다시 만든다(매 프레임 할당 없음).
    /// </summary>
    public sealed class DungeonHud : MonoBehaviour
    {
        const float PanelWidth = 470f;
        const float PanelHeight = 206f;
        const float Margin = 12f;
        const float OrbSize = 156f;
        const int ToastMax = 4;
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
        const string KeyHint = "WASD 이동 · 클릭 공격 · F 상호작용 · G 끼기 · M 지도 · K 스킬 · I 가방 · F1 기록";
        const string DownTitle = "쓰러졌다";
        const string DownLine = "마지막 말뚝 곁에서 다시 눈을 뜬다";
        const string BannerLine = "최대 체력이 오르고, 스킬 점수 1점이 생겼다  [K]";
        const string ExploreBadge = "탐험 걸음";

        /// <summary>층별 권장 레벨(3차 초안 4-3): 1, 3, 5, 7, 8, 10, 12, 13, 15, 16.</summary>
        static readonly int[] RecommendedLevels = { 1, 3, 5, 7, 8, 10, 12, 13, 15, 16 };

        /// <summary>층 이름 카드 아래 음울한 한 줄(층 번호 순). 없는 층은 FloorLineDefault.</summary>
        static readonly string[] FloorLines =
        {
            "내려간 자들은 아무도 올라오지 않았다.",
        };
        const string FloorLineDefault = "아래로 갈수록 숨이 무거워진다.";

        static readonly Color MessageColor = new Color(0.86f, 0.8f, 0.68f, 1f);
        static readonly Color QuietColor = new Color(0.66f, 0.61f, 0.53f, 1f);
        static readonly Color FindColor = new Color(0.93f, 0.79f, 0.52f, 1f);
        static readonly Color XpColor = new Color(0.5f, 0.42f, 0.24f, 1f);
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
        float _bannerAge = 999f;
        float _downAge;
        bool _stylesReady;
        GUIStyle _hudLabel;
        GUIStyle _hudRight;
        GUIStyle _smallRight;
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

        public static int RecommendedLevel(int floor) => RecommendedLevels[Mathf.Clamp(floor, 1, RecommendedLevels.Length) - 1];

        /// <summary>층 이름 카드의 음울한 한 줄.</summary>
        public static string FloorLine(int floor) => floor >= 1 && floor <= FloorLines.Length ? FloorLines[floor - 1] : FloorLineDefault;

        void Awake()
        {
            Instance = this;
            DungeonEvents.Message += OnMessage;
            DungeonEvents.Discovered += OnDiscovered;
            DungeonEvents.LevelUp += OnLevelUp;
        }

        void OnDestroy()
        {
            DungeonEvents.Message -= OnMessage;
            DungeonEvents.Discovered -= OnDiscovered;
            DungeonEvents.LevelUp -= OnLevelUp;
            if (Instance == this) Instance = null;
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
            var root = DungeonRoot.Instance;
            var player = root ? root.Player : null;
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
            _smallRight = new GUIStyle(DungeonUi.Small) { wordWrap = false, alignment = TextAnchor.UpperRight };
            SetWhite(_smallRight);
            _orbText = new GUIStyle(DungeonUi.Bold) { fontSize = 16, wordWrap = false, alignment = TextAnchor.MiddleCenter };
            SetWhite(_orbText);
            _orbCaption = new GUIStyle(DungeonUi.Small) { fontSize = 12, wordWrap = false, alignment = TextAnchor.MiddleCenter, font = DungeonUi.Serif };
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
            // 이 화면에는 누르는 것이 없다. 그리기 사건에서만 그린다(배치 사건 반복을 줄인다).
            if (Event.current.type != EventType.Repaint) return;
            var root = DungeonRoot.Instance;
            if (!root || root.State == null) return;
            DungeonUi.Begin();
            EnsureStyles();
            GUI.depth = 5;
            var player = root.Player;
            if (player && player.IsDown) DrawDown();
            if (player) DrawPanel(root, player);
            DrawToasts();
            DrawFloorCard(root);
            DrawLevelBanner();
            if (player && player.IsDown) DrawDownText();
        }

        void DrawPanel(DungeonRoot root, PlayerController p)
        {
            var progress = PlayerProgress.Instance;

            // ── 생명 구슬(체력) ──
            var orb = new Rect(Margin + 10f, DungeonUi.Height - Margin - 10f - OrbSize, OrbSize, OrbSize);
            var hp = p.Health;
            float frac = hp ? hp.Fraction : 0f;
            float bright = 1f;
            if (hp && frac > 0f && frac < 0.25f) bright = 0.82f + 0.22f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.2f));
            DungeonUi.Orb(orb, frac, bright);
            if (hp)
            {
                if (_hpText.Stale(hp.Current, hp.Max)) _hpText.Text = hp.Current + " / " + hp.Max;
                DungeonUi.ShadowLabel(new Rect(orb.x, orb.center.y - 22f, orb.width, 18f), "체력", _orbCaption, DungeonUi.BoneDim);
                DungeonUi.ShadowLabel(new Rect(orb.x, orb.center.y - 6f, orb.width, 24f), _hpText.Text, _orbText, DungeonUi.Bone);
            }

            // ── 검은 쇠 판 ──
            float x = orb.xMax + 22f;
            float y = DungeonUi.Height - PanelHeight - Margin;
            DungeonUi.Box(new Rect(x, y, PanelWidth, PanelHeight), 0.88f);
            float ix = x + 14f;
            float iy = y + 11f;
            float inner = PanelWidth - 28f;

            // 무기·공격력, 레벨. 등급이 붙은 이름(예: "희귀 대검")은 가방이 안다. 없으면 전투 규칙 이름.
            var inv = Inventory.Instance;
            object weaponKey = inv && inv.Equipped != null ? (object)inv.Equipped : p.Weapon;
            if (_weaponText.Stale(p.Attack, 0, weaponKey))
            {
                string weapon = inv && inv.Equipped != null ? inv.Equipped.DisplayName : p.Weapon != null ? p.Weapon.displayName : "맨손";
                _weaponText.Text = weapon + " · 공격력 " + p.Attack;
            }
            DungeonUi.ShadowLabel(new Rect(ix, iy, inner - 90f, 22f), _weaponText.Text, _hudLabel, DungeonUi.Bone);
            int level = progress ? progress.Level : 1;
            if (_levelText.Stale(level, 0)) _levelText.Text = "Lv " + level;
            DungeonUi.ShadowLabel(new Rect(ix + inner - 90f, iy, 90f, 22f), _levelText.Text, _hudRight, DungeonUi.Ember);
            iy += 28f;

            // 물약: 병 셋(가득 = 피색) + 남은 수·재사용.
            for (int i = 0; i < 3; i++)
                DungeonUi.Flask(new Rect(ix + i * 22f, iy, 18f, 24f), i < p.Potions);
            int potionTenths = p.PotionCooldown > 0f ? Mathf.CeilToInt(p.PotionCooldown * 10f) : 0;
            if (_potionText.Stale(p.Potions, potionTenths))
                _potionText.Text = potionTenths > 0 ? "물약 " + p.Potions + "/3  (" + (potionTenths / 10f).ToString("0.0") + ")" : "물약 " + p.Potions + "/3  [R]";
            DungeonUi.ShadowLabel(new Rect(ix + 74f, iy + 2f, inner - 74f, 22f), _potionText.Text, DungeonUi.Label, potionTenths > 0 ? DungeonUi.BoneDim : DungeonUi.Bone);
            iy += 32f;

            // 재사용.
            float cw = (inner - 12f) / 3f;
            Cooldown(0, new Rect(ix, iy, cw, 44f), "회오리 [우클릭]", p.WhirlCooldown, p.WhirlCooldownMax);
            Cooldown(1, new Rect(ix + cw + 6f, iy, cw, 44f), "검풍 [Q]", p.WaveCooldown, p.WaveCooldownMax);
            Cooldown(2, new Rect(ix + (cw + 6f) * 2f, iy, cw, 44f), "구르기 [Space]", p.DodgeCooldown, p.DodgeCooldownMax);
            iy += 52f;

            // 경험치(얻은 순간 0.4초 번쩍이며 새로 찬 몫을 보인다).
            float xpFrac = progress && progress.XpToNext > 0 ? (float)progress.Xp / progress.XpToNext : 0f;
            float flash = DrawXpBar(new Rect(ix, iy + 4f, 280f, 10f), xpFrac, progress);
            int xp = progress ? progress.Xp : -1;
            int toNext = progress ? progress.XpToNext : -1;
            if (_xpText.Stale(xp, toNext)) _xpText.Text = progress ? "경험치 " + xp + " / " + toNext : "경험치 -";
            DungeonUi.ShadowLabel(new Rect(ix + 290f, iy, inner - 290f, 20f), _xpText.Text, DungeonUi.Small,
                flash > 0f ? Color.Lerp(DungeonUi.BoneDim, DungeonUi.Ember, flash) : DungeonUi.BoneDim);
            iy += 22f;

            // 강화석·골드·지금 칸, 스킬 점수.
            var state = root.State;
            var cell = root.CurrentCell;
            if (_infoText.Stale(state.Stones, state.Gold, cell))
                _infoText.Text = "강화석 " + state.Stones + " · 골드 " + state.Gold + " · 지금: " + (cell != null ? cell.Name : "-");
            int points = progress ? progress.SkillPoints : 0;
            DungeonUi.ShadowLabel(new Rect(ix, iy, points > 0 ? inner - 150f : inner, 20f), _infoText.Text, DungeonUi.Small, DungeonUi.Bone);
            if (points > 0)
            {
                if (_pointText.Stale(points, 0)) _pointText.Text = "[K] 스킬 점수 " + points;
                DungeonUi.ShadowLabel(new Rect(ix + inner - 150f, iy, 150f, 20f), _pointText.Text, _smallRight, DungeonUi.Ember);
            }
            iy += 24f;

            // 키 안내.
            DungeonUi.Fill(new Rect(ix, iy - 3f, inner, 1f), new Color(DungeonUi.Bone.r, DungeonUi.Bone.g, DungeonUi.Bone.b, 0.12f));
            var old = GUI.color;
            GUI.color = new Color(DungeonUi.BoneDim.r, DungeonUi.BoneDim.g, DungeonUi.BoneDim.b, 0.9f);
            GUI.Label(new Rect(ix, iy, inner, 36f), KeyHint, DungeonUi.Small);
            GUI.color = old;

            // 탐험 걸음(2-8).
            var walk = ExploreWalk.Instance;
            if (walk && walk.Active)
            {
                var badge = new Rect(x, y - 34f, 124f, 26f);
                DungeonUi.Box(badge, 0.82f);
                DungeonUi.ShadowLabel(badge, ExploreBadge, DungeonUi.Center, DungeonUi.Ember);
            }
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
            DungeonUi.Fill(rect, SlotFill);
            if (cooling) DungeonUi.Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(remaining / max), rect.height), SlotShade);
            DungeonUi.Outline(rect, cooling ? DungeonUi.IronEdge : SlotReadyEdge, 1f);
            var prev = GUI.color;
            GUI.color = DungeonUi.BoneDim;
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 8f, 20f), label, DungeonUi.Small);
            GUI.color = prev;
            int tenths = cooling ? Mathf.CeilToInt(remaining * 10f) : 0;
            var cache = _cooldownText[slot];
            if (cache.Stale(tenths, 0)) cache.Text = tenths > 0 ? (tenths / 10f).ToString("0.0") + "초" : "준비";
            DungeonUi.ShadowLabel(new Rect(rect.x + 6f, rect.y + 20f, rect.width - 8f, 22f), cache.Text, _hudLabel, cooling ? DungeonUi.BoneDim : DungeonUi.Bone);
        }

        /// <summary>화면 아래 가운데, 판 위에 새 알림이 아래로 쌓인다. 양 끝이 흐려지는 검은 띠 위 바탕체 글.</summary>
        void DrawToasts()
        {
            if (_toasts.Count == 0) return;
            float reserved = ExplorationLog.Instance ? ExplorationLog.Instance.PanelReservedWidth : 0f;
            float cx = (DungeonUi.Width - reserved) * 0.5f;
            float y = DungeonUi.Height - PanelHeight - Margin - 52f;
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
        /// 층에 처음 들어가면 크고 느린 층 이름 카드(2-2, 기준 문서 '화면 연출'): "제1층 — 입구 갱도", 음울한 한 줄, 권장 레벨.
        /// 양 끝이 흐린 검은 띠 위에 바탕체 큰 글, 위아래 바랜 뼈색 줄. 약 3.6초 동안 천천히 떠올라 천천히 사라진다.
        /// </summary>
        void DrawFloorCard(DungeonRoot root)
        {
            if (_cardAge >= CardTime) return;
            if (_cardTitle == null)
            {
                string name = root.Map != null ? root.Map.Name : "";
                _cardTitle = "제" + root.Floor + "층 — " + name;
                _cardLine = FloorLine(root.Floor);
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
