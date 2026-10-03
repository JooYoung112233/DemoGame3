using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Loot;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 전설 불꽃 발자국(장비 문서 6장 '잿불 걸음', 수치 2차 6-5): 전투 중(마지막 공격·스킬·피격에서 3초 안) 걸은 거리 1.2유닛마다 발밑에 불길을 깐다
    /// (걷기 5.3이면 약 0.23초마다. 장화 이동이 올라도 간격이 끊기지 않는다). 불길은 반경 0.8, 2.5초.
    /// 불 위 적은 0.5초마다 공격력의 50~60%(치명 없음, 버팀 0)를 받고 이동이 −30%(Enemy.ApplySlow 0.7)다. 불길이 겹쳐도 적 하나는 0.5초에 한 번만 탄다.
    /// 구르기로 옮긴 거리는 세지 않는다(불길 구르기는 다른 전설 몫). 순간 이동은 걸음이 아니다.
    /// 효과가 꺼지면 새 불길을 깔지 않고, 남은 불길이 다 꺼지면 스스로 Update를 멈춘다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlameStepsEffect : MonoBehaviour
    {
        const int PruneEveryFrames = 30;

        LegendEffects _hub;
        bool _on;
        bool _hasLast;
        Vector2 _lastPos;
        readonly FlameStepCounter _counter = new FlameStepCounter();
        readonly List<double> _offsets = new List<double>(4);
        readonly List<FlamePatch> _patches = new List<FlamePatch>(16);
        readonly List<Enemy> _enemies = new List<Enemy>(32);
        readonly Dictionary<Enemy, float> _lastBurn = new Dictionary<Enemy, float>(EnemyRef.Comparer);
        readonly List<Enemy> _prune = new List<Enemy>(16);

        /// <summary>지금 타는 불길 수.</summary>
        public int PatchCount => _patches.Count;
        /// <summary>깐 불길 수(발동 수).</summary>
        public int Placed { get; private set; }

        internal void Bind(LegendEffects hub) => _hub = hub;

        internal void SetOn(bool on)
        {
            _on = on;
            if (!on)
            {
                _counter.Reset();
                _hasLast = false;
            }
            bool run = on || _patches.Count > 0;
            if (enabled != run) enabled = run;
        }

        void Update()
        {
            float now = Time.time;
            var player = _hub ? _hub.Player : null;
            if (_on && player) Track(player, now);
            else _hasLast = false;
            if (_patches.Count > 0) Burn(now);
            else if (!_on)
            {
                _lastBurn.Clear();
                enabled = false;
                return;
            }
            if (_lastBurn.Count > 0 && Time.frameCount % PruneEveryFrames == 0) Prune(now);
        }

        /// <summary>걸은 거리를 모아 1.2유닛을 넘는 자리마다 불길을 깐다.</summary>
        void Track(PlayerController player, float now)
        {
            Vector2 pos = player.Position;
            if (!_hasLast || player.IsDown)
            {
                _lastPos = pos;
                _hasLast = true;
                _counter.Reset();
                return;
            }
            Vector2 delta = pos - _lastPos;
            _lastPos = pos;
            // 구르기 거리는 건너뛴다(모은 거리는 그대로 둔다).
            if (player.Pose == PlayerPose.Dodge) return;
            float dist = delta.magnitude;
            if (dist <= 0f) return;
            bool inCombat = LegendRules.InCombat(now, player.LastCombatActionTime);
            int count = _counter.Advance(dist, inCombat, _offsets);
            if (count <= 0) return;
            Vector2 dir = delta / dist;
            Vector2 start = pos - delta;
            for (int i = 0; i < count; i++)
                Place(start + dir * Mathf.Min(dist, (float)_offsets[i]));
        }

        /// <summary>
        /// 지금 세기로 그 자리에 불길 하나를 깐다(전투 시험장·eval 확인용: 굴쥐 발밑에 깔아 MoveSpeed 0.7배를 본다). 효과가 꺼져 있으면 null.
        /// </summary>
        public FlamePatch Place(Vector2 at)
        {
            int strength = _hub ? _hub.Strength(LegendaryEffect.FlameSteps) : -1;
            if (strength < 0) return null;
            var patch = FlamePatch.Spawn(at, LegendRules.FlamePercent(strength));
            _patches.Add(patch);
            Placed++;
            if (!enabled) enabled = true;
            CombatEvents.RaiseLegendTriggered(LegendaryEffect.FlameSteps, 1);
            return patch;
        }

        /// <summary>불길 위 적을 느리게 하고, 0.5초 간격이 된 적을 태운다(겹친 불길 가운데 가장 센 세기).</summary>
        void Burn(float now)
        {
            for (int i = _patches.Count - 1; i >= 0; i--)
            {
                var p = _patches[i];
                if (p && !p.Expired) continue;
                _patches.RemoveAt(i);
                // 다 탄 불길은 스스로 사라지지만, 같은 프레임에 겹쳐 두 번 보이지 않게 여기서도 지운다.
                if (p) Destroy(p.gameObject);
            }
            if (_patches.Count == 0) return;
            if (!_hub) return;

            // 처치 사건에서 적이 새로 생길 수 있어 목록을 먼저 옮겨 담는다.
            _enemies.Clear();
            foreach (var e in Enemy.All)
                if (e && !e.Dead) _enemies.Add(e);

            int kills = 0;
            float linger = (float)LegendRules.FlameSlowLinger;
            float slow = (float)LegendRules.FlameSlowFactor;
            foreach (var e in _enemies)
            {
                if (!e || e.Dead) continue;
                double percent = -1;
                foreach (var p in _patches)
                    if (p.Touches(e) && p.Percent > percent) percent = p.Percent;
                if (percent < 0) continue;
                e.ApplySlow(slow, linger);
                float last = _lastBurn.TryGetValue(e, out float t) ? t : float.NegativeInfinity;
                if (!LegendRules.CanBurn(now, last)) continue;
                _lastBurn[e] = now;
                _hub.Strike(e, LegendaryEffect.FlameSteps, percent, LegendRules.FlameCanCrit, out _, out bool killed);
                if (killed) kills++;
            }
            _hub.CountKills(kills, false);
        }

        /// <summary>쓰러진 적과, 탄 지 1초가 지나 더는 0.5초 간격을 막지 않는 적을 탄 시각 표에서 뺀다.</summary>
        void Prune(float now)
        {
            _prune.Clear();
            foreach (var kv in _lastBurn)
                if (!kv.Key || kv.Key.Dead || now - kv.Value > 1f)
                    _prune.Add(kv.Key);
            foreach (var e in _prune) _lastBurn.Remove(e);
        }

        void OnDisable()
        {
            _hasLast = false;
        }
    }
}
