using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Loot;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 전설 연쇄 번개(장비 문서 6장 '벼락 갈래', 수치 2차 6-5): 기본공격이 맞을 때 30~40%로 발동, 다시 발동까지 0.2초.
    /// 처음 맞은 적에서 앞 적 몸 가장자리까지 거리 4 안의 가장 가까운 적으로 최대 4번 튕기고, 튕길 때마다 공격력의 110~130%(줄지 않음).
    /// 같은 적은 다시 맞지 않고, 벽 너머로는 건너가지 않는다. 치명은 플레이어 치명 확률·피해 그대로, 버팀은 깎지 않는다.
    /// 굴림은 동작마다 한 번: CombatEvents.PlayerBasicHit에서 ActionId가 바뀐 첫 사건만 본다(타마다 굴리면 쌍검이 약 2.5배 터짐).
    /// 번개 피해는 PlayerBasicHit을 내지 않아 다시 번개를 부르지 않는다. 번개로 쓰러뜨린 적은 연속 처치로만 센다(처치 연출은 휘두르기가 낸다).
    /// 꺼져 있으면 사건을 구독하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChainLightningEffect : MonoBehaviour
    {
        LegendEffects _hub;
        readonly LightningGate _gate = new LightningGate();
        readonly List<Enemy> _enemies = new List<Enemy>(32);
        readonly List<ChainPoint> _points = new List<ChainPoint>(32);
        System.Func<int, int, bool> _canLink;

        /// <summary>발동 수(튕김이 하나라도 난 발동).</summary>
        public int Procs { get; private set; }
        /// <summary>마지막 발동의 튕김 수(0~4).</summary>
        public int LastBounces { get; private set; }

        internal void Bind(LegendEffects hub) => _hub = hub;

        internal void SetOn(bool on)
        {
            if (enabled != on) enabled = on;
        }

        void Awake() => _canLink = CanLink;

        void OnEnable() => CombatEvents.PlayerBasicHit += OnBasicHit;

        void OnDisable() => CombatEvents.PlayerBasicHit -= OnBasicHit;

        void OnBasicHit(BasicHitInfo info)
        {
            if (!_hub) return;
            int strength = _hub.Strength(LegendaryEffect.ChainLightning);
            if (strength < 0) return;
            float now = Time.time;
            // 동작마다 한 번: 굴림 순간(번호가 바뀐 첫 사건, 다시 발동 대기 끝)이 아니면 난수를 뽑지 않는다.
            if (!_gate.IsRollMoment(info.ActionId, now)) return;
            if (_hub.Rng.NextDouble() >= LegendRules.LightningChance(strength)) return;

            Enemy first = info.FirstTarget ? info.FirstTarget : Nearest(info.Origin);
            if (!first) return;
            BuildPoints(first);
            var chain = LegendRules.PickChain(_points, 0, LegendRules.LightningMaxBounces, LegendRules.LightningRange, _canLink);
            if (chain.Count == 0) return;

            _gate.MarkProc(now);
            Procs++;
            LastBounces = chain.Count;
            double percent = LegendRules.LightningPercent(strength);
            Vector2 from = first.Position;
            int kills = 0;
            foreach (int index in chain)
            {
                var enemy = _enemies[index];
                Vector2 to = enemy.Position;
                _hub.Bolt(from, to);
                _hub.Strike(enemy, LegendaryEffect.ChainLightning, percent, LegendRules.LightningCanCrit, out _, out bool killed);
                if (killed) kills++;
                from = to;
            }
            Sfx.PlayScaled(SfxKind.Crit, 0.55f, 1.6f);
            CombatEvents.RaiseLegendTriggered(LegendaryEffect.ChainLightning, chain.Count);
            _hub.CountKills(kills, false);
        }

        /// <summary>후보 목록: 0번은 처음 맞은 적(쓰러졌어도 자리만 씀), 그 뒤로 살아 있는 적.</summary>
        void BuildPoints(Enemy first)
        {
            _enemies.Clear();
            _points.Clear();
            _enemies.Add(first);
            _points.Add(new ChainPoint(first.Position.x, first.Position.y, first.Radius, false));
            foreach (var e in Enemy.All)
            {
                if (!e || e == first || e.Dead) continue;
                _enemies.Add(e);
                _points.Add(new ChainPoint(e.Position.x, e.Position.y, e.Radius));
            }
        }

        bool CanLink(int from, int to) => !Physics2D.Linecast(_enemies[from].Position, _enemies[to].Position, Layers.WallMask);

        /// <summary>사건에 맞은 적이 없을 때(계약상 늘 있지만) 판정 자리에서 가장 가까운 산 적.</summary>
        static Enemy Nearest(Vector2 origin)
        {
            Enemy best = null;
            float bestDist = (float)LegendRules.LightningRange;
            foreach (var e in Enemy.All)
            {
                if (!e || e.Dead) continue;
                float d = (e.Position - origin).magnitude - e.Radius;
                if (d > bestDist) continue;
                best = e;
                bestDist = d;
            }
            return best;
        }
    }
}
