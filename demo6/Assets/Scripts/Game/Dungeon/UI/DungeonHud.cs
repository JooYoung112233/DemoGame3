using System.Collections.Generic;
using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 던전 화면(IMGUI, 전투 시험 HUD와 같은 꼴). 왼쪽 아래 판: 무기·공격력, 체력, 물약, 회오리·검풍·구르기 재사용, 레벨·경험치, 스킬 점수,
    /// 강화석·골드, 지금 칸, 키 안내. 화면 아래 가운데 알림(DungeonEvents.Message·Discovered, 4줄까지, 3초 뒤 흐려짐),
    /// 층에 처음 들어갈 때 층 이름 카드와 권장 레벨(3차 초안 2-2), 레벨업 알림(4-3), 탐험 걸음 표시(2-8), 쓰러짐 문구.
    /// 정식 화면은 Unity 개발 단계(uGUI)에서 다시 만든다. 여기서는 판정에 필요한 정보만 둔다.
    /// </summary>
    public sealed class DungeonHud : MonoBehaviour
    {
        const float PanelWidth = 500f;
        const float PanelHeight = 206f;
        const float Margin = 12f;
        const int ToastMax = 4;
        const float ToastLife = 3f;
        const float ToastFade = 0.6f;
        const float CardTime = 3f;
        const float BannerTime = 2.5f;
        const string KeyHint = "WASD 이동 · 클릭 공격 · F 상호작용 · G 끼기 · M 지도 · K 스킬 · I 가방 · F1 기록";

        /// <summary>층별 권장 레벨(3차 초안 4-3): 1, 3, 5, 7, 8, 10, 12, 13, 15, 16.</summary>
        static readonly int[] RecommendedLevels = { 1, 3, 5, 7, 8, 10, 12, 13, 15, 16 };

        static readonly Color HpColor = new Color(0.72f, 0.28f, 0.24f, 1f);
        static readonly Color XpColor = new Color(0.86f, 0.74f, 0.38f, 1f);
        static readonly Color Warm = new Color(1f, 0.9f, 0.62f, 1f);
        static readonly Color FindColor = new Color(1f, 0.93f, 0.75f, 1f);

        public static DungeonHud Instance { get; private set; }

        sealed class Toast
        {
            public string Text;
            public float Age;
            public bool Quiet;
            public Color Color;
        }

        readonly List<Toast> _toasts = new List<Toast>();
        float _cardAge;
        float _bannerAge = 999f;
        int _bannerLevel;
        bool _stylesReady;
        GUIStyle _hudLabel;
        GUIStyle _hudRight;
        GUIStyle _toastStyle;
        GUIStyle _quietStyle;
        GUIStyle _smallRight;

        public static int RecommendedLevel(int floor) => RecommendedLevels[Mathf.Clamp(floor, 1, RecommendedLevels.Length) - 1];

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
        }

        // ── 알림 ─────────────────────────────────────────────

        void OnMessage(string text) => Push(text, false, Color.white);

        void OnDiscovered(DiscoveryKind kind, Vector2 pos, string label)
        {
            switch (kind)
            {
                case DiscoveryKind.NewCell:
                    Push("새 칸: " + label, true, Color.white);
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
            _bannerLevel = level;
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

        static string DiscoveryText(DiscoveryKind kind, string label)
        {
            string name = string.IsNullOrEmpty(label) ? KindName(kind) : label;
            switch (kind)
            {
                case DiscoveryKind.WallLamp: return "벽 등잔을 켰다";
                case DiscoveryKind.Stake: return name + " 켬";
                case DiscoveryKind.HiddenRoom: return "숨은 방을 찾았다";
                case DiscoveryKind.Shortcut: return "지름길이 열렸다";
                case DiscoveryKind.Ability: return "새 능력 · " + name;
                case DiscoveryKind.Story: return "이야기 · " + name;
                case DiscoveryKind.Event: return "사건 · " + name;
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
            _hudRight = new GUIStyle(DungeonUi.Bold) { wordWrap = false, alignment = TextAnchor.UpperRight };
            _smallRight = new GUIStyle(DungeonUi.Small) { wordWrap = false, alignment = TextAnchor.UpperRight };
            _toastStyle = new GUIStyle(DungeonUi.Center) { fontSize = 17, wordWrap = false };
            _quietStyle = new GUIStyle(DungeonUi.Center) { fontSize = 14, wordWrap = false };
        }

        void OnGUI()
        {
            var root = DungeonRoot.Instance;
            if (!root || root.State == null) return;
            DungeonUi.Begin();
            EnsureStyles();
            GUI.depth = 5;
            var player = root.Player;
            if (player) DrawPanel(root, player);
            DrawToasts();
            DrawFloorCard(root);
            DrawLevelBanner();
            if (player && player.IsDown) DrawDown();
        }

        void DrawPanel(DungeonRoot root, PlayerController p)
        {
            float x = Margin;
            float y = DungeonUi.Height - PanelHeight - Margin;
            DungeonUi.Box(new Rect(x, y, PanelWidth, PanelHeight));
            float ix = x + 12f;
            float iy = y + 10f;
            float inner = PanelWidth - 24f;
            var progress = PlayerProgress.Instance;

            // 무기·공격력, 레벨.
            // 등급이 붙은 이름(예: "희귀 대검")은 가방(작업자 A)이 안다. 없으면 전투 규칙 이름.
            var inv = Inventory.Instance;
            string weapon = inv && inv.Equipped != null ? inv.Equipped.DisplayName : p.Weapon != null ? p.Weapon.displayName : "맨손";
            GUI.Label(new Rect(ix, iy, inner - 90f, 22f), $"{weapon} · 공격력 {p.Attack}", _hudLabel);
            GUI.Label(new Rect(ix + inner - 90f, iy, 90f, 22f), $"Lv {(progress ? progress.Level : 1)}", _hudRight);
            iy += 26f;

            // 체력·물약.
            var hp = p.Health;
            DungeonUi.Bar(new Rect(ix, iy, 320f, 22f), hp ? hp.Fraction : 0f, HpColor, hp ? $"체력 {hp.Current} / {hp.Max}" : "");
            string potion = p.PotionCooldown > 0f ? $"물약 {p.Potions}/3 ({p.PotionCooldown:0.0})" : $"물약 {p.Potions}/3 [R]";
            GUI.Label(new Rect(ix + 332f, iy, inner - 332f, 22f), potion, DungeonUi.Label);
            iy += 30f;

            // 재사용.
            float cw = (inner - 12f) / 3f;
            Cooldown(new Rect(ix, iy, cw, 44f), "회오리 [우클릭]", p.WhirlCooldown, p.WhirlCooldownMax);
            Cooldown(new Rect(ix + cw + 6f, iy, cw, 44f), "검풍 [Q]", p.WaveCooldown, p.WaveCooldownMax);
            Cooldown(new Rect(ix + (cw + 6f) * 2f, iy, cw, 44f), "구르기 [Space]", p.DodgeCooldown, p.DodgeCooldownMax);
            iy += 52f;

            // 경험치.
            float xpFrac = progress && progress.XpToNext > 0 ? (float)progress.Xp / progress.XpToNext : 0f;
            DungeonUi.Bar(new Rect(ix, iy + 4f, 320f, 12f), xpFrac, XpColor);
            GUI.Label(new Rect(ix + 332f, iy, inner - 332f, 20f), progress ? $"경험치 {progress.Xp} / {progress.XpToNext}" : "경험치 -", DungeonUi.Small);
            iy += 22f;

            // 강화석·골드·지금 칸, 스킬 점수.
            var state = root.State;
            string cell = root.CurrentCell != null ? root.CurrentCell.Name : "-";
            GUI.Label(new Rect(ix, iy, inner - 150f, 20f), $"강화석 {state.Stones} · 골드 {state.Gold} · 지금: {cell}", DungeonUi.Small);
            int points = progress ? progress.SkillPoints : 0;
            if (points > 0)
            {
                var prev = GUI.color;
                GUI.color = Warm;
                GUI.Label(new Rect(ix + inner - 150f, iy, 150f, 20f), $"[K] 스킬 점수 {points}", _smallRight);
                GUI.color = prev;
            }
            iy += 22f;

            // 키 안내.
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            GUI.Label(new Rect(ix, iy, inner, 36f), KeyHint, DungeonUi.Small);
            GUI.color = old;

            // 탐험 걸음(2-8).
            var walk = ExploreWalk.Instance;
            if (walk && walk.Active)
            {
                var badge = new Rect(x, y - 32f, 120f, 26f);
                DungeonUi.Box(badge, 0.8f);
                old = GUI.color;
                GUI.color = Warm;
                GUI.Label(badge, "탐험 걸음", DungeonUi.Center);
                GUI.color = old;
            }
        }

        void Cooldown(Rect rect, string label, float remaining, float max)
        {
            DungeonUi.Fill(rect, new Color(1f, 1f, 1f, 0.1f));
            if (remaining > 0f && max > 0f)
                DungeonUi.Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(remaining / max), rect.height), new Color(0f, 0f, 0f, 0.55f));
            GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, rect.width - 8f, 20f), label, DungeonUi.Small);
            GUI.Label(new Rect(rect.x + 6f, rect.y + 20f, rect.width - 8f, 22f), remaining > 0f ? $"{remaining:0.0}초" : "준비", _hudLabel);
        }

        /// <summary>화면 아래 가운데, 판 위에 새 알림이 아래로 쌓인다.</summary>
        void DrawToasts()
        {
            if (_toasts.Count == 0) return;
            float reserved = ExplorationLog.Instance ? ExplorationLog.Instance.PanelReservedWidth : 0f;
            float cx = (DungeonUi.Width - reserved) * 0.5f;
            float y = DungeonUi.Height - PanelHeight - Margin - 52f;
            var prev = GUI.color;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                float a = Mathf.Clamp01(t.Age / 0.12f) * (t.Age < ToastLife ? 1f : Mathf.Clamp01(1f - (t.Age - ToastLife) / ToastFade));
                if (t.Quiet) a *= 0.7f;
                var style = t.Quiet ? _quietStyle : _toastStyle;
                float h = t.Quiet ? 26f : 32f;
                float w = Mathf.Min(DungeonUi.Width - 40f, style.CalcSize(new GUIContent(t.Text)).x + 40f);
                var r = new Rect(cx - w * 0.5f, y - h, w, h);
                DungeonUi.Box(r, 0.75f * a);
                GUI.color = new Color(t.Color.r, t.Color.g, t.Color.b, a);
                GUI.Label(r, t.Text, style);
                y -= h + 6f;
            }
            GUI.color = prev;
        }

        /// <summary>층에 처음 들어가면 층 이름 카드와 권장 레벨(2-2): "1층 · 입구 갱도", "권장 Lv 1".</summary>
        void DrawFloorCard(DungeonRoot root)
        {
            if (_cardAge >= CardTime) return;
            float a = Mathf.Clamp01(_cardAge / 0.3f) * Mathf.Clamp01((CardTime - _cardAge) / 0.6f);
            float w = 560f;
            var r = new Rect((DungeonUi.Width - w) * 0.5f, 70f, w, 100f);
            DungeonUi.Box(r, 0.8f * a);
            var prev = GUI.color;
            GUI.color = new Color(1f, 0.95f, 0.85f, a);
            string name = root.Map != null ? root.Map.Name : "";
            GUI.Label(new Rect(r.x, r.y + 10f, r.width, 46f), $"{root.Floor}층 · {name}", DungeonUi.BigCenter);
            GUI.color = new Color(1f, 1f, 1f, 0.85f * a);
            GUI.Label(new Rect(r.x, r.y + 58f, r.width, 30f), $"권장 Lv {RecommendedLevel(root.Floor)}", DungeonUi.Center);
            GUI.color = prev;
        }

        /// <summary>레벨업 알림(4-3: 시간은 멈추지 않는다).</summary>
        void DrawLevelBanner()
        {
            if (_bannerAge >= BannerTime) return;
            float a = Mathf.Clamp01(_bannerAge / 0.15f) * Mathf.Clamp01((BannerTime - _bannerAge) / 0.6f);
            float w = 520f;
            var r = new Rect((DungeonUi.Width - w) * 0.5f, 190f, w, 92f);
            DungeonUi.Box(r, 0.75f * a);
            var prev = GUI.color;
            GUI.color = new Color(Warm.r, Warm.g, Warm.b, a);
            GUI.Label(new Rect(r.x, r.y + 8f, r.width, 44f), $"레벨 업 · Lv {_bannerLevel}", DungeonUi.BigCenter);
            GUI.color = new Color(1f, 1f, 1f, 0.85f * a);
            GUI.Label(new Rect(r.x, r.y + 54f, r.width, 28f), "최대 체력 증가 · 스킬 점수 +1 [K]", DungeonUi.Center);
            GUI.color = prev;
        }

        void DrawDown()
        {
            float w = DungeonUi.Width;
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(new Rect(3f, DungeonUi.Height * 0.4f + 3f, w, 50f), "쓰러졌다…", DungeonUi.BigCenter);
            GUI.color = new Color(1f, 0.85f, 0.8f, 1f);
            GUI.Label(new Rect(0f, DungeonUi.Height * 0.4f, w, 50f), "쓰러졌다…", DungeonUi.BigCenter);
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.Label(new Rect(0f, DungeonUi.Height * 0.4f + 50f, w, 30f), "잠시 뒤 마지막 말뚝에서 다시 선다", DungeonUi.Center);
            GUI.color = prev;
        }
    }
}
