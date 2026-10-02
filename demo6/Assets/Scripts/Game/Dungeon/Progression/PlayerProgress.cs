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
    /// </summary>
    public sealed class PlayerProgress : MonoBehaviour
    {
        /// <summary>얻은 경험치 '+n'(눈에 덜 띄는 바랜 금색).</summary>
        static readonly Color GainColor = new Color(0.8f, 0.77f, 0.62f, 0.85f);
        /// <summary>'레벨 n'(따뜻한 흰색).</summary>
        static readonly Color LevelColor = new Color(1f, 0.95f, 0.82f, 1f);

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

        readonly int[] _ranks = new int[SkillTree.Count];
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
        public void GrantXp(int amount) => AddXp(amount);

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
            AddXp(XpRules.ForDiscovery(kind, Floor));
            if (kind != DiscoveryKind.FloorComplete) _checkComplete = true;
        }

        void OnEnemyKilled(Enemy e)
        {
            if (e == null || e.IsDummy) return;
            int floor = e.Floor > 0 ? e.Floor : Floor;
            AddXp(XpRules.ForKill(e.Kind, e.IsElite, e.NoReward, floor, Level));
        }

        void OnGroupCleared(int group, bool nest, Vector2 pos)
        {
            if (!nest) return;
            AddXp(XpRules.ForNestClear(Floor, Level));
        }

        void AddXp(int amount)
        {
            if (amount <= 0) return;
            TotalXp += amount;
            _pendingGain += amount;
            int before = Level;
            int target = LevelTable.LevelFor(TotalXp);
            if (target > Level)
            {
                Level = target;
                SkillPoints += target - before;
            }
            RefreshXp();
            if (Level > before) OnLevelsGained(before);
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
            DungeonEvents.Say($"레벨 {Level} — 스킬 점수 +{gained} [K]");
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
            DungeonEvents.Say($"{root.Floor}층 완전 탐험!");
        }
    }
}
