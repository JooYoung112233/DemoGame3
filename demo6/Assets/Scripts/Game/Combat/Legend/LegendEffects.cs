using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Loot;
using Demo6.Core.Random;
using Demo6.Core.Stats;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 전설 고유 효과 3종의 묶음(장비 문서 6장, 12장 단계 4). PlayerController.Create가 검사 몸에 붙인다.
    /// 어떤 효과가 켜졌는지는 PlayerController.Sheet(StatSheet.HasLegendary·LegendaryRollPermille, 겹침 규칙 적용 뒤)로 읽고,
    /// 능력치 표가 바뀔 때만 효과 컴포넌트를 켜고 끈다. 꺼진 효과는 사건을 구독하지 않고 Update도 돌지 않는다(기본 상태 손맛·난수 그대로).
    /// 발동 자리: 연쇄 번개 = CombatEvents.PlayerBasicHit(동작 번호가 바뀐 첫 사건에서만 굴림, ChainLightningEffect),
    /// 불꽃 발자국 = 플레이어 위치를 매 프레임 읽어 걸은 거리 누적(전투 중 = LastCombatActionTime 3초 안, FlameStepsEffect),
    /// 연쇄 폭발 = CombatEvents.EnemyKilled(폭발로 죽은 적도 다시, 연쇄 하나에 처치 연출 처음 한 번, ChainBlastEffect).
    /// 피해는 Strike 한 곳에서 Enemy.TakeHit(…, DamageSource.Legend, 버팀 0, …)으로 넣는다. 체력 흡수·연쇄 번개 재발동을 받지 않는다
    /// (PlayerBasicHit을 내지 않고, PlayerController를 거치지 않음). 수치는 Core/Combat/LegendRules만 쓴다.
    /// 그림은 도형 임시 그림(번개 줄·폭발 고리·불길)이다. 짧은 그림 조각은 여기서 묶어 돌려 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LegendEffects : MonoBehaviour
    {
        /// <summary>전설 난수 흐름(플레이어 치명 난수와 따로). 효과가 꺼져 있으면 한 번도 뽑지 않는다.</summary>
        const ulong RngSeed = 20261003;
        const ulong RngStream = 61;
        /// <summary>번개·폭발 그림 조각은 손맛 효과처럼 캐릭터 위에 그린다(검기 2900, 파편 3200 사이).</summary>
        const int FxOrder = 2950;
        const int FxPoolMax = 96;

        static readonly Color BoltGlow = new Color(0.55f, 0.75f, 1f, 0.45f);
        static readonly Color BoltCore = new Color(0.92f, 0.97f, 1f, 0.95f);
        static readonly Color BlastRing = new Color(1f, 0.55f, 0.18f, 0.8f);
        static readonly Color BlastFill = new Color(1f, 0.42f, 0.12f, 0.35f);

        sealed class Fx
        {
            public SpriteRenderer Sprite;
            public float Age;
            public float Life;
            public Color Color;
            public Vector3 FromScale;
            public Vector3 ToScale;
        }

        PlayerController _player;
        StatSheet _sheet;
        bool _refreshed;
        ChainLightningEffect _lightning;
        FlameStepsEffect _flame;
        ChainBlastEffect _blast;
        readonly IRandom _rng = new Pcg32Random(RngSeed, RngStream);
        readonly List<Fx> _fx = new List<Fx>(32);
        readonly Stack<Fx> _fxFree = new Stack<Fx>(32);

        public PlayerController Player => _player;
        public ChainLightningEffect Lightning => _lightning;
        public FlameStepsEffect Flame => _flame;
        public ChainBlastEffect Blast => _blast;

        /// <summary>그 효과가 지금 켜져 있는가(장비·시험 손잡이 기준, 겹침 뒤).</summary>
        public bool Active(LegendaryEffect effect) => _player && _player.Sheet != null && _player.Sheet.HasLegendary(effect);

        /// <summary>그 효과의 세기 굴림‰(0~1000). 꺼져 있으면 -1.</summary>
        public int Strength(LegendaryEffect effect) => _player && _player.Sheet != null ? _player.Sheet.LegendaryRollPermille(effect) : -1;

        /// <summary>전설 난수(발동 굴림·치명·피해 흔들림). 효과 컴포넌트만 쓴다.</summary>
        internal IRandom Rng => _rng;

        void Awake()
        {
            _player = GetComponent<PlayerController>();
            _lightning = gameObject.AddComponent<ChainLightningEffect>();
            _flame = gameObject.AddComponent<FlameStepsEffect>();
            _blast = gameObject.AddComponent<ChainBlastEffect>();
            _lightning.Bind(this);
            _flame.Bind(this);
            _blast.Bind(this);
            Refresh();
        }

        void Update()
        {
            if (!_player) return;
            // 능력치 표는 ApplyStats마다 새 것이 들어온다. 바뀌었을 때만 켜고 끈다.
            if (!_refreshed || !ReferenceEquals(_player.Sheet, _sheet)) Refresh();
            if (_fx.Count > 0) TickFx();
        }

        /// <summary>능력치 표에 맞춰 효과 컴포넌트를 켜고 끈다. 불꽃 발자국·연쇄 폭발은 남은 불길·줄 선 폭발을 끝낼 때까지 스스로 켜 둔다.</summary>
        public void Refresh()
        {
            _refreshed = true;
            _sheet = _player ? _player.Sheet : null;
            _lightning.SetOn(Active(LegendaryEffect.ChainLightning));
            _flame.SetOn(Active(LegendaryEffect.FlameSteps));
            _blast.SetOn(Active(LegendaryEffect.ChainBlast));
        }

        /// <summary>
        /// 전설 피해 한 번. 피해 = 2차 5-2 공식(공격력 × 배율 × 보스면 1 + 보스 피해 × 치명이면 치명 피해 × 0.92~1.08 × 방어).
        /// 치명은 canCrit일 때만 플레이어 치명 확률·피해로 굴린다. 버팀 0, 넉백 없음, 출처 Legend.
        /// </summary>
        /// <returns>실제로 들어간 피해(무적·껍질·칸 밖 회피면 0).</returns>
        internal int Strike(Enemy enemy, LegendaryEffect effect, double percent, bool canCrit, out bool crit, out bool killed)
        {
            crit = false;
            killed = false;
            if (!enemy || enemy.Dead || !_player) return 0;
            crit = canCrit && _rng.NextDouble() < _player.CritChance;
            var sheet = _player.Sheet;
            double boss = sheet != null ? sheet.BossDamagePermille / 1000.0 : 0;
            int defense = enemy.Health ? enemy.Health.Defense : 0;
            int damage = DamageMath.ToMonster(_player.Attack, percent, crit, _player.CritDamage, DamageMath.Roll(_rng), defense,
                0, false, boss, enemy.IsBoss);
            int applied = enemy.TakeHit(damage, crit, DamageSource.Legend, 0f, false, 1f, out _, out _);
            if (applied <= 0) return 0;
            killed = enemy.Dead;
            CombatEvents.RaiseLegendDealt(effect, new DamageDealt(enemy, applied, DamageSource.Legend, crit, killed));
            return applied;
        }

        /// <summary>전설 피해로 쓰러뜨린 적 수를 플레이어 몫으로 센다(연속 처치는 늘 오르고, juice면 여러 마리 처치 연출).</summary>
        internal void CountKills(int kills, bool juice)
        {
            if (kills > 0 && _player) _player.AddKills(kills, juice);
        }

        // ── 도형 임시 그림 ──

        /// <summary>번개 한 줄(a → b). 꺾인 마디 셋, 밝은 심 + 푸른 번짐. 실제 시간 0.18초.</summary>
        internal void Bolt(Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.01f) return;
            Vector2 side = new Vector2(-d.y, d.x) / len;
            const int segments = 3;
            Vector2 prev = a;
            for (int i = 1; i <= segments; i++)
            {
                Vector2 next = i == segments ? b : a + d * (i / (float)segments) + side * UnityEngine.Random.Range(-0.28f, 0.28f);
                Segment(prev, next, 0.24f, BoltGlow, 0.18f);
                Segment(prev, next, 0.07f, BoltCore, 0.14f);
                prev = next;
            }
        }

        /// <summary>폭발 고리(반경 radius까지 퍼짐) + 속 번쩍임. 실제 시간 0.25초.</summary>
        internal void Burst(Vector2 at, float radius)
        {
            float size = radius * 2f;
            Spawn(ShapeSprites.Ring, at, 0f, Vector3.one * (size * 0.35f), Vector3.one * size, BlastRing, 0.25f);
            Spawn(ShapeSprites.Circle, at, 0f, Vector3.one * (size * 0.25f), Vector3.one * (size * 0.85f), BlastFill, 0.18f);
        }

        void Segment(Vector2 a, Vector2 b, float width, Color color, float life)
        {
            Vector2 d = b - a;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var scale = new Vector3(d.magnitude, width, 1f);
            Spawn(ShapeSprites.Square, (a + b) * 0.5f, angle, scale, scale, color, life);
        }

        void Spawn(Sprite sprite, Vector2 at, float angle, Vector3 fromScale, Vector3 toScale, Color color, float life)
        {
            if (_fx.Count >= FxPoolMax) return;
            var fx = _fxFree.Count > 0 ? _fxFree.Pop() : null;
            if (fx == null || !fx.Sprite)
            {
                var go = new GameObject("LegendFx");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = FxOrder;
                RenderMaterials.MakeUnlit(sr);
                fx = new Fx { Sprite = sr };
            }
            var t = fx.Sprite.transform;
            t.position = at;
            t.rotation = Quaternion.Euler(0f, 0f, angle);
            t.localScale = fromScale;
            fx.Sprite.sprite = sprite;
            fx.Sprite.color = color;
            fx.Sprite.enabled = true;
            fx.Age = 0f;
            fx.Life = life;
            fx.Color = color;
            fx.FromScale = fromScale;
            fx.ToScale = toScale;
            _fx.Add(fx);
        }

        void TickFx()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = _fx.Count - 1; i >= 0; i--)
            {
                var fx = _fx[i];
                if (!fx.Sprite)
                {
                    _fx.RemoveAt(i);
                    continue;
                }
                fx.Age += dt;
                float p = Mathf.Clamp01(fx.Age / fx.Life);
                var c = fx.Color;
                c.a *= 1f - p * p;
                fx.Sprite.color = c;
                fx.Sprite.transform.localScale = Vector3.Lerp(fx.FromScale, fx.ToScale, 1f - (1f - p) * (1f - p));
                if (fx.Age < fx.Life) continue;
                fx.Sprite.enabled = false;
                _fx.RemoveAt(i);
                _fxFree.Push(fx);
            }
        }

        void OnDestroy()
        {
            foreach (var fx in _fx)
                if (fx.Sprite) Destroy(fx.Sprite.gameObject);
            foreach (var fx in _fxFree)
                if (fx.Sprite) Destroy(fx.Sprite.gameObject);
            _fx.Clear();
            _fxFree.Clear();
        }
    }

    /// <summary>
    /// 적을 C# 참조 그대로 비교한다(전설 효과의 탄 시각 표·폭발 줄). 지워진 Unity 물체끼리는 == 가 같다고 나오므로 사전 키로는 참조 비교를 쓴다(TopDownView와 같은 이유).
    /// </summary>
    sealed class EnemyRef : IEqualityComparer<Enemy>
    {
        public static readonly EnemyRef Comparer = new EnemyRef();

        public bool Equals(Enemy a, Enemy b) => ReferenceEquals(a, b);

        public int GetHashCode(Enemy e) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(e);
    }
}
