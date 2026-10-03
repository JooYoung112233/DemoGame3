using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Loot;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 전설 연쇄 폭발(장비 문서 6장 '터지는 무덤', 수치 2차 6-5): 보스가 아닌 적이 쓰러지면 그 자리에서 반경 2.5, 공격력의 150~180% 폭발(치명 없음, 버팀 0).
    /// 폭발로 죽은 적도 다시 터진다. 연쇄 하나는 첫 폭발 포함 최대 12번, 폭발 사이 0.1초(LegendRules.BlastChain).
    /// 같은 프레임의 처치(한 번 휘둘러 여럿)는 한 연쇄로 묶고, 이 효과의 폭발로 난 처치(CombatEvents.EnemyKilled)는 지금 터지는 연쇄에 줄을 선다.
    /// 적 하나는 한 번만 터진다(이중 발동 막기). 여러 마리 처치 연출(히트스톱 0.08·느린 화면)은 연쇄 하나에 처음 한 번만(PlayerController.AddKills juice),
    /// 연속 처치 숫자는 폭발마다 계속 올린다. 피해는 처치 사건 안에서 넣지 않고 Update에서 줄 순서대로 넣는다(휘두르기 판정 도중에 적이 바뀌지 않게).
    /// 효과가 꺼지면 새 처치는 받지 않고, 줄 선 폭발을 다 터뜨린 뒤 스스로 Update를 멈춘다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChainBlastEffect : MonoBehaviour
    {
        /// <summary>폭발로 쓰러진 몸을 바깥으로 날리는 거리(처치 날림 계산에 넘기는 값).</summary>
        const float DeathPush = 0.6f;

        struct Pending
        {
            public Vector2 At;
            public double Time;
            public double Percent;
            public BlastChain Chain;
            /// <summary>연쇄 안 몇 번째 폭발(1~12).</summary>
            public int Index;
        }

        LegendEffects _hub;
        bool _on;
        readonly List<Pending> _queue = new List<Pending>(16);
        readonly HashSet<Enemy> _queued = new HashSet<Enemy>(EnemyRef.Comparer);
        readonly List<Enemy> _enemies = new List<Enemy>(32);
        BlastChain _frameChain;
        int _frameChainFrame = -1;
        /// <summary>지금 터뜨리는 연쇄. 이 폭발로 난 처치는 새 연쇄가 아니라 여기에 줄을 선다.</summary>
        BlastChain _exploding;

        /// <summary>터뜨린 폭발 수.</summary>
        public int Explosions { get; private set; }
        /// <summary>가장 긴 연쇄(폭발 수, 최대 12).</summary>
        public int LongestChain { get; private set; }
        /// <summary>줄 서 있는 폭발 수.</summary>
        public int PendingCount => _queue.Count;

        internal void Bind(LegendEffects hub) => _hub = hub;

        internal void SetOn(bool on)
        {
            _on = on;
            bool run = on || _queue.Count > 0;
            if (enabled != run) enabled = run;
        }

        void OnEnable() => CombatEvents.EnemyKilled += OnKilled;

        void OnDisable() => CombatEvents.EnemyKilled -= OnKilled;

        void OnKilled(Enemy enemy)
        {
            if (!_on || !_hub || !enemy) return;
            if (!LegendRules.TriggersBlast(enemy.IsBoss)) return;
            int strength = _hub.Strength(LegendaryEffect.ChainBlast);
            if (strength < 0) return;
            if (!_queued.Add(enemy)) return;

            BlastChain chain = _exploding;
            if (chain == null)
            {
                // 같은 프레임의 처치는 한 연쇄(한 번 휘둘러 여럿 = 한 흐름).
                if (_frameChain == null || _frameChainFrame != Time.frameCount || _frameChain.Full)
                {
                    _frameChain = new BlastChain();
                    _frameChainFrame = Time.frameCount;
                }
                chain = _frameChain;
            }
            if (!chain.TrySchedule(Time.time, out double at)) return;
            _queue.Add(new Pending
            {
                At = enemy.Position,
                Time = at,
                Percent = LegendRules.BlastPercent(strength),
                Chain = chain,
                Index = chain.Count,
            });
        }

        void Update()
        {
            double now = Time.time;
            for (int guard = 0; guard < 64 && _queue.Count > 0; guard++)
            {
                int pick = -1;
                double best = double.MaxValue;
                for (int i = 0; i < _queue.Count; i++)
                    if (_queue[i].Time <= now + 1e-6 && _queue[i].Time < best)
                    {
                        best = _queue[i].Time;
                        pick = i;
                    }
                if (pick < 0) break;
                var p = _queue[pick];
                _queue.RemoveAt(pick);
                Explode(p);
            }
            if (_queue.Count > 0) return;
            _queued.Clear();
            if (!_on) enabled = false;
        }

        void Explode(Pending p)
        {
            if (!_hub) return;
            float radius = (float)LegendRules.BlastRadius;
            _hub.Burst(p.At, radius);
            Sfx.PlayScaled(SfxKind.WallBreak, 0.45f, 1.3f);
            ScreenShake.Add(0.05f, 0.06f);

            // 처치 사건에서 적이 새로 생길 수 있어 목록을 먼저 옮겨 담는다.
            _enemies.Clear();
            foreach (var e in Enemy.All)
                if (e && !e.Dead) _enemies.Add(e);

            int kills = 0;
            _exploding = p.Chain;
            try
            {
                foreach (var e in _enemies)
                {
                    if (!e || e.Dead) continue;
                    Vector2 to = e.Position - p.At;
                    if (to.magnitude - e.Radius > radius) continue;
                    if (Physics2D.Linecast(p.At, e.Position, Layers.WallMask)) continue;
                    _hub.Strike(e, LegendaryEffect.ChainBlast, p.Percent, LegendRules.BlastCanCrit, out _, out bool killed);
                    if (!killed) continue;
                    kills++;
                    e.ApplyKnockback(to.sqrMagnitude > 0.0001f ? to : Vector2.up, DeathPush);
                }
            }
            finally
            {
                _exploding = null;
            }

            Explosions++;
            if (p.Index > LongestChain) LongestChain = p.Index;
            CombatEvents.RaiseLegendTriggered(LegendaryEffect.ChainBlast, p.Index);
            // 여러 마리 처치 연출은 연쇄 하나에 처음 한 번(처치가 난 첫 폭발). 연속 처치 수는 늘 올린다.
            _hub.CountKills(kills, kills > 0 && p.Chain.TakeJuice());
        }
    }
}
