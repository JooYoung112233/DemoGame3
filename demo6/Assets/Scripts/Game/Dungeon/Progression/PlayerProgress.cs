using System.Collections.Generic;
using Demo6.Core.Dungeon;
using Demo6.Core.Progression;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 경험치·레벨·스킬 1줄(3차 초안 4-3·4-5, M0b 7-2 '최소 레벨 + 스킬 1줄 4칸').
    /// 경험치는 이 컴포넌트만 준다(계약서): 발견(Discovered) × U(층), 처치(EnemyKilled), 둥지 정리(GroupCleared).
    /// 레벨당 맨몸 체력 +120(질긴 몸 랭크당 +15, 지난 레벨에도)·스킬 점수 1점. 공격력은 오르지 않는다.
    /// 레벨업 순간은 따뜻한 흰 고리 0.5초와 '스킬 점수 +1' 알림이고 시간은 멈추지 않는다.
    /// 층 완전 탐험(자물쇠 문 없이 갈 수 있는 칸을 모두 가 보고 그 칸의 한 번만 받는 것을 모두 끝냄)은 한 번 12U.
    /// 경험치 연출(한 마리 RPG 요소): 처치·둥지 정리 경험치는 그 자리(시체 위)에 "+n 경험치"를 크게(호박색) 띄우고(정예·둥지 정리는 더 크게),
    /// HUD 경험치 막대가 0.4초 밝게 번쩍이며 새로 찬 몫을 보이게 기록을 남긴다(GainFlashStart·GainFromFraction·GainFromKill).
    /// 탐험 경험치는 지금처럼 플레이어 위 작은 '+n'. 경험치 양은 바꾸지 않는다(4-3 값 그대로).
    /// </summary>
    public sealed class PlayerProgress : MonoBehaviour
    {
        /// <summary>얻은 경험치 '+n'(눈에 덜 띄는 바랜 금색).</summary>
        static readonly Color GainColor = new Color(0.8f, 0.77f, 0.62f, 0.85f);
        /// <summary>'레벨 n'(따뜻한 흰색).</summary>
        static readonly Color LevelColor = new Color(1f, 0.95f, 0.82f, 1f);
        /// <summary>처치 경험치 "+n 경험치"(짙은 호박색: 치명 피해 숫자의 밝은 노랑과 갈리게).</summary>
        static readonly Color KillGainColor = new Color(0.95f, 0.6f, 0.2f, 1f);
        static readonly Color KillGainEdge = new Color(0.08f, 0.03f, 0f, 0.9f);

        /// <summary>HUD 경험치 막대가 밝게 번쩍이는 시간(실제 시간).</summary>
        public const float GainFlashSeconds = 0.4f;
        const int MaxPopups = 10;
        /// <summary>처치 글자: 피해 숫자(0.7초) 뒤에 읽히게 조금 늦게 떠서 천천히 오른다.</summary>
        const float PopupDelay = 0.3f;
        const float PopupLife = 1.3f;
        const float PopupRise = 0.75f;
        const float PopupPop = 0.14f;
        const int PopupFont = 30;
        const int PopupFontBig = 40;

        public static PlayerProgress Instance { get; private set; }

        public int Level { get; private set; } = 1;
        /// <summary>이번 레벨 안에서 모은 경험치(최대 레벨이면 막대가 가득 차게 XpToNext와 같다).</summary>
        public int Xp { get; private set; }
        /// <summary>이번 레벨에서 다음 레벨까지 필요한 경험치(레벨 표 4-3).</summary>
        public int XpToNext { get; private set; } = 340;
        public int SkillPoints { get; private set; }
        /// <summary>누계 경험치.</summary>
        public int TotalXp { get; private set; }
        /// <summary>레벨·질긴 몸으로 정한 최대 체력.</summary>
        public int MaxHp { get; private set; } = LevelHp.BaseHp;
        /// <summary>층 완전 탐험 보상을 받았는가.</summary>
        public bool FloorCompleted { get; private set; }
        public bool IsMaxLevel => Level >= LevelTable.MaxLevel;
        /// <summary>경험치 막대 몫(0~1).</summary>
        public float XpFraction => XpToNext > 0 ? Mathf.Clamp01((float)Xp / XpToNext) : 1f;
        /// <summary>마지막 경험치를 얻은 실제 시각(HUD 막대 번쩍임 시작). 0.4초 안에 또 얻으면 이어서 늘어난다.</summary>
        public float GainFlashStart { get; private set; } = -999f;
        /// <summary>번쩍이는 동안 새로 찬 몫이 시작하는 막대 몫(0~1). 그 사이 레벨이 오르면 0.</summary>
        public float GainFromFraction { get; private set; }
        /// <summary>번쩍임에 처치·둥지 정리 경험치가 들어 있는가(세게 번쩍인다).</summary>
        public bool GainFromKill { get; private set; }

        struct Popup
        {
            public Vector2 World;
            public string Text;
            public float Born;
            public bool Big;
            /// <summary>처치 날림으로 날아가는 몸(시체가 될 자리)을 지워질 때까지 따라간다. 조각났거나 지워지면 마지막 자리에 머문다.</summary>
            public Enemy Follow;
            public float Lift;
        }

        readonly int[] _ranks = new int[SkillTree.Count];
        readonly Popup[] _popups = new Popup[MaxPopups];
        int _popupNext;
        /// <summary>마지막 처치 글자가 사라지는 실제 시각(그 뒤로는 OnGUI가 바로 돌아간다).</summary>
        float _popupsUntil = -1f;
        Camera _cam;
        GUIStyle _popupStyle;
        PlayerController _player;
        int _pendingGain;
        bool _checkComplete;
        /// <summary>완전 탐험에 들어가는 칸(입구에서 자물쇠 문을 빼고 갈 수 있는 칸).</summary>
        List<DungeonCell> _completeCells;

        int Floor => DungeonRoot.Instance ? DungeonRoot.Instance.Floor : 1;

        void Awake()
        {
            Instance = this;
            DungeonEvents.Discovered += OnDiscovered;
            DungeonEvents.GroupCleared += OnGroupCleared;
            DungeonEvents.PlayerRespawned += ApplyToPlayer;
            DungeonEvents.ExpeditionRestarted += ApplyToPlayer;
            CombatEvents.EnemyKilled += OnEnemyKilled;
            if (!GetComponent<SkillPanel>()) gameObject.AddComponent<SkillPanel>();
            RefreshXp();
        }

        void OnDestroy()
        {
            DungeonEvents.Discovered -= OnDiscovered;
            DungeonEvents.GroupCleared -= OnGroupCleared;
            DungeonEvents.PlayerRespawned -= ApplyToPlayer;
            DungeonEvents.ExpeditionRestarted -= ApplyToPlayer;
            CombatEvents.EnemyKilled -= OnEnemyKilled;
            if (Instance == this) Instance = null;
        }

        /// <summary>플레이어를 묶고 Lv 1 최대 체력(2,400)과 스킬 0점(M0a 그대로)을 넣는다.</summary>
        public void Init(PlayerController player)
        {
            _player = player;
            ApplyToPlayer();
            RefreshXp();
        }

        /// <summary>스킬 랭크(0~4).</summary>
        public int Rank(SkillId id)
        {
            int i = (int)id;
            return i >= 0 && i < _ranks.Length ? _ranks[i] : 0;
        }

        /// <summary>문자열 id(예: 'skill.tough_body')로 랭크를 읽는다. 없는 id면 0.</summary>
        public int Rank(string skillId)
        {
            var def = SkillTree.Find(skillId);
            return def != null ? Rank(def.Id) : 0;
        }

        /// <summary>[+]를 누를 수 있는가: 점수 1점 이상, Lv 2 이상, 4랭크 미만.</summary>
        public bool CanRankUp(SkillId id) => SkillTree.CanRankUp(Rank(id), Level, SkillPoints);

        /// <summary>점수 1점으로 랭크를 올리고 플레이어에 바로 넣는다(질긴 몸은 지난 레벨 체력도 다시 계산).</summary>
        public bool TryRankUp(SkillId id)
        {
            if (!CanRankUp(id)) return false;
            _ranks[(int)id]++;
            SkillPoints--;
            ApplyToPlayer();
            var def = SkillTree.Get(id);
            int rank = Rank(id);
            Sfx.Play(SfxKind.Pickup);
            DungeonEvents.Say($"{def.Name} {rank}/{SkillDef.MaxRank} — {def.EffectText(rank)}");
            return true;
        }

        /// <summary>시험 패널용: 경험치를 바로 더한다(기획 값이 아닌 시험 조작).</summary>
        public void GrantXp(int amount) => AddXp(amount, false);

        /// <summary>스킬 보정과 레벨 체력을 플레이어에 넣는다. 말뚝에서 다시 서거나 원정을 다시 시작할 때도 다시 넣는다.</summary>
        public void ApplyToPlayer()
        {
            if (!_player) return;
            _player.WhirlRadiusBonus = (float)SkillTree.WhirlRadiusBonus(Rank(SkillId.WideWhirl));
            _player.WavePercentBonus = (float)SkillTree.WavePercentBonus(Rank(SkillId.SharpWind));
            _player.FinisherDamageBonus = (float)SkillTree.FinisherDamageBonus(Rank(SkillId.Finisher));
            ApplyMaxHp();
        }

        void ApplyMaxHp()
        {
            MaxHp = LevelHp.MaxHp(Level, Rank(SkillId.ToughBody));
            if (_player && _player.Health) _player.SetMaxHp(MaxHp);
        }

        void OnDiscovered(DiscoveryKind kind, Vector2 pos, string label)
        {
            AddXp(XpRules.ForDiscovery(kind, Floor), false);
            if (kind != DiscoveryKind.FloorComplete) _checkComplete = true;
        }

        void OnEnemyKilled(Enemy e)
        {
            if (e == null || e.IsDummy) return;
            int floor = e.Floor > 0 ? e.Floor : Floor;
            int amount = XpRules.ForKill(e.Kind, e.IsElite, e.NoReward, floor, Level);
            if (amount <= 0) return;
            // 시체 위에 크게. 정예는 더 크게. 처치 날림(2~3유닛)으로 몸이 날아가므로 시체가 멈출 때까지 따라간다.
            // 피해 숫자 줄(몸 반지름 + 0.35 위)보다 아래, 시체 바로 위에서 시작해 숫자와 겹치지 않게 한다.
            float lift = e.Radius * 0.3f;
            SpawnPopup(e.Position + Vector2.up * lift, amount, e.IsElite, e, lift);
            AddXp(amount, true);
        }

        void OnGroupCleared(int group, bool nest, Vector2 pos)
        {
            if (!nest) return;
            int amount = XpRules.ForNestClear(Floor, Level);
            if (amount <= 0) return;
            // 둥지 정리: 둥지 자리에 더 크게('잠잠해졌다' 글자보다 아래에서 시작).
            SpawnPopup(pos + Vector2.up * 0.1f, amount, true);
            AddXp(amount, true);
        }

        /// <param name="kill">처치·둥지 정리 경험치(그 자리 큰 글자로 따로 보이므로 플레이어 위 작은 '+n'에 넣지 않는다).</param>
        void AddXp(int amount, bool kill)
        {
            if (amount <= 0) return;
            float fracBefore = XpFraction;
            TotalXp += amount;
            if (!kill) _pendingGain += amount;
            int before = Level;
            int target = LevelTable.LevelFor(TotalXp);
            if (target > Level)
            {
                Level = target;
                SkillPoints += target - before;
            }
            RefreshXp();
            MarkGainFlash(fracBefore, Level != before, kill);
            if (Level > before) OnLevelsGained(before);
        }

        /// <summary>HUD 막대 번쩍임 기록: 0.4초 안에 이어 얻으면 처음 시작 몫을 두고 시간만 늘린다. 레벨이 오르면 새 막대의 0부터.</summary>
        void MarkGainFlash(float fracBefore, bool leveled, bool kill)
        {
            float now = Time.unscaledTime;
            bool continuing = now - GainFlashStart < GainFlashSeconds;
            if (leveled) GainFromFraction = 0f;
            else if (!continuing) GainFromFraction = fracBefore;
            GainFromKill = kill || (continuing && GainFromKill);
            GainFlashStart = now;
        }

        /// <summary>처치 경험치 글자 하나(고리 버퍼, 글은 처치할 때 한 번 만든다).</summary>
        void SpawnPopup(Vector2 world, int amount, bool big, Enemy follow = null, float lift = 0f)
        {
            _popups[_popupNext] = new Popup
            {
                World = world,
                Text = "+" + amount + " 경험치",
                Born = Time.unscaledTime + PopupDelay,
                Big = big,
                Follow = follow,
                Lift = lift,
            };
            _popupNext = (_popupNext + 1) % MaxPopups;
            _popupsUntil = Mathf.Max(_popupsUntil, Time.unscaledTime + PopupDelay + PopupLife);
        }

        void RefreshXp()
        {
            if (IsMaxLevel)
            {
                XpToNext = LevelTable.Required(LevelTable.MaxLevel - 1);
                Xp = XpToNext;
            }
            else
            {
                XpToNext = LevelTable.Required(Level);
                Xp = Mathf.Max(0, TotalXp - LevelTable.Cumulative(Level));
            }
        }

        /// <summary>레벨업: 체력 다시 계산, 소리, 흰 고리 0.5초, '레벨 n', 알림. 시간은 멈추지 않는다(4-3).</summary>
        void OnLevelsGained(int before)
        {
            int gained = Level - before;
            ApplyMaxHp();
            Sfx.Play(SfxKind.LevelUp);
            if (_player)
            {
                LevelUpRing.Spawn(_player.transform);
                WorldOverlay.Text(_player.Position + Vector2.up * 1.1f, $"레벨 {Level}", LevelColor);
            }
            for (int level = before + 1; level <= Level; level++) DungeonEvents.RaiseLevelUp(level);
            DungeonEvents.Say($"몸에 힘이 차오른다 — 레벨 {Level} · 스킬 점수 +{gained} [K]");
        }

        void LateUpdate()
        {
            if (_pendingGain > 0)
            {
                // 한 프레임에 들어온 경험치는 '+n' 하나로 묶는다(새 칸과 궤짝이 겹쳐도 글자가 포개지지 않게).
                if (_player) WorldOverlay.Text(_player.Position + new Vector2(0.9f, 0.45f), "+" + _pendingGain, GainColor);
                _pendingGain = 0;
            }
            if (_checkComplete)
            {
                _checkComplete = false;
                CheckFloorComplete();
            }
        }

        /// <summary>
        /// 처치 경험치 글자: 0.14초 동안 크게 튀어나왔다 자리 잡고(글꼴 크기 대신 행렬로 키워 글꼴을 다시 만들지 않는다),
        /// 천천히 0.75유닛 떠오르며 마지막 30%에 흐려진다. 바탕체 굵은 호박색 + 어두운 테두리. 피해 숫자(WorldOverlay) 위, HUD 아래.
        /// </summary>
        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            float now = Time.unscaledTime;
            if (now > _popupsUntil) return;
            if (!_cam) _cam = Camera.main;
            if (!_cam) return;
            GUI.depth = 9;
            if (_popupStyle == null)
            {
                _popupStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow, font = DungeonUi.Serif };
                _popupStyle.normal.textColor = Color.white;
            }
            float ui = Screen.height / 1080f;
            var prevMatrix = GUI.matrix;
            var prevColor = GUI.color;
            for (int i = 0; i < MaxPopups; i++)
            {
                ref var slot = ref _popups[i];
                if (slot.Follow)
                {
                    // 날아가는 몸을 따라가다 지워지면(시체로 바뀐 자리) 거기 머문다. 조각났으면 처치 자리에 둔다.
                    if (slot.Follow.Dismembered) slot.Follow = null;
                    else slot.World = (Vector2)slot.Follow.transform.position + Vector2.up * slot.Lift;
                }
                else if (!ReferenceEquals(slot.Follow, null)) slot.Follow = null;
                var p = slot;
                if (p.Text == null) continue;
                float age = now - p.Born;
                if (age < 0f || age > PopupLife) continue;
                float t = age / PopupLife;
                float rise = 1f - (1f - t) * (1f - t);
                var sp = _cam.WorldToScreenPoint(p.World + Vector2.up * (PopupRise * rise));
                if (sp.z < 0f) continue;
                var center = new Vector2(sp.x, Screen.height - sp.y);
                float alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                float pop = age < PopupPop ? Mathf.Lerp(1.45f, 1f, age / PopupPop) : 1f;
                _popupStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt((p.Big ? PopupFontBig : PopupFont) * ui));
                // 화면 픽셀 좌표 기준(WorldOverlay와 같음)에서 글자 가운데를 축으로 키운다.
                GUI.matrix = Matrix4x4.identity;
                GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), center);
                var rect = new Rect(center.x - 200f, center.y - 30f, 400f, 60f);
                // 어두운 테두리(네 방향) + 아래 그림자 → 호박색 글.
                GUI.color = new Color(KillGainEdge.r, KillGainEdge.g, KillGainEdge.b, KillGainEdge.a * alpha);
                GUI.Label(new Rect(rect.x - 1.5f, rect.y, rect.width, rect.height), p.Text, _popupStyle);
                GUI.Label(new Rect(rect.x + 1.5f, rect.y, rect.width, rect.height), p.Text, _popupStyle);
                GUI.Label(new Rect(rect.x, rect.y - 1.5f, rect.width, rect.height), p.Text, _popupStyle);
                GUI.Label(new Rect(rect.x + 2f, rect.y + 2.5f, rect.width, rect.height), p.Text, _popupStyle);
                GUI.color = new Color(KillGainColor.r, KillGainColor.g, KillGainColor.b, alpha);
                GUI.Label(rect, p.Text, _popupStyle);
            }
            GUI.matrix = prevMatrix;
            GUI.color = prevColor;
        }

        /// <summary>
        /// 층 완전 탐험(4-3, 12U): 입구에서 자물쇠 문을 빼고 갈 수 있는 칸(1층 11칸, 사무실 O 제외)을 모두 가 보고,
        /// 그 칸에 있는 한 번만 받는 것(궤짝·등잔·말뚝·이야기·사건·곡괭이 등)을 모두 끝내면 한 번 준다.
        /// 발견 알림 처리 안에서 다시 알리지 않도록 LateUpdate에서 확인한다.
        /// </summary>
        void CheckFloorComplete()
        {
            if (FloorCompleted) return;
            var root = DungeonRoot.Instance;
            if (!root || root.Map == null || root.World == null || root.State == null) return;
            if (_completeCells == null)
            {
                var start = root.Map.Find("E");
                if (start == null) return;
                var reach = root.Map.Reachable(start, k => k != EdgeKind.Locked);
                _completeCells = new List<DungeonCell>();
                foreach (var mc in reach)
                {
                    var c = root.World.Find(mc.Id);
                    if (c != null) _completeCells.Add(c);
                }
            }
            if (_completeCells.Count == 0) return;

            foreach (var c in _completeCells)
                if (!c.Visited && !root.State.VisitedCells.Contains(c.Id)) return;

            foreach (var e in root.State.OneTime.Values)
            {
                if (e.Done) continue;
                // 광맥(원정마다 생김)·금고(열쇠)는 완전 탐험 조건에 넣지 않는다.
                if (e.Kind == DiscoveryKind.Ore || e.Kind == DiscoveryKind.Safe || e.Kind == DiscoveryKind.FloorComplete) continue;
                var cell = e.Cell ?? root.World.CellAt(e.Position);
                if (cell != null && _completeCells.Contains(cell)) return;
            }

            FloorCompleted = true;
            Vector2 at = _player ? _player.Position : _completeCells[0].Center;
            DungeonEvents.RaiseDiscovered(DiscoveryKind.FloorComplete, at, $"{root.Floor}층 완전 탐험");
            DungeonEvents.Say($"{root.Floor}층의 어둠을 모두 걸었다 — 완전 탐험");
        }
    }
}
