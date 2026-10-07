using Demo6.Core.Combat;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 적 출혈 상태(도끼, BleedRule): 3초 동안 0.5초마다 공격력 × (합 ÷ 6)%를 DamageSource.Bleed로 넣는다(치명·히트스톱·체력 흡수·버팀 없음,
    /// 보스 피해 능력치와 무너짐 받는 피해 +30%는 받음). 새로 걸리면 시간만 다시 센다. 틱마다 피 방울 자국(GoreSystem.BleedDrip).
    /// 적 물체에 필요할 때 붙는 컴포넌트라 Enemy.cs를 고치지 않는다. 부르는 곳: PlayerController.SwingHit(⑤)이 bleedPercent &gt; 0인 단계가 맞으면 Apply, 보스 다시 도전(OgreBrain.ResetFight)이 Clear.
    /// 시간은 게임 시간(Time.time)이라 히트스톱·느린 화면·멈춤 동안에는 틱도 멈춘다. 첫 틱은 건 뒤 0.5초, 마지막(6번째) 틱은 3.0초.
    /// 공격력·보스 피해는 걸 때 읽어 둔다(출혈 도중 무기를 바꿔도 그 타의 값). 정적 값이 없어 SubsystemRegistration에서 지울 것이 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyBleed : MonoBehaviour
    {
        Enemy _enemy;
        PlayerController _source;
        int _attack;
        double _bossBonus;
        double _tickPercent;
        int _ticksLeft;
        float _nextTick;

        /// <summary>지금 피를 흘리는 중인가(남은 틱이 있고 적이 살아 있음).</summary>
        public bool Active => enabled && _ticksLeft > 0 && _enemy && !_enemy.Dead;
        /// <summary>남은 틱 수(0~6).</summary>
        public int TicksLeft => _ticksLeft;
        /// <summary>남은 시간(초, 게임 시간). 마지막 틱까지.</summary>
        public float Remaining => _ticksLeft > 0 ? Mathf.Max(0f, _nextTick - Time.time) + (_ticksLeft - 1) * BleedRule.TickInterval : 0f;

        /// <summary>출혈을 건다(이미 걸려 있으면 시간만 다시). totalPercent = 합(%), source = 공격력·보스 피해를 읽을 플레이어.</summary>
        public static void Apply(Enemy enemy, PlayerController source, float totalPercent)
        {
            if (!enemy || enemy.Dead || totalPercent <= 0f) return;
            if (!source) source = PlayerController.Instance;
            if (!source) return;
            var bleed = enemy.GetComponent<EnemyBleed>();
            if (!bleed) bleed = enemy.gameObject.AddComponent<EnemyBleed>();
            bleed.Begin(enemy, source, totalPercent);
        }

        /// <summary>남은 출혈을 거둔다(보스 다시 도전처럼 적을 처음 상태로 되돌릴 때). 걸려 있지 않으면 아무것도 하지 않는다.</summary>
        public static void Clear(Enemy enemy)
        {
            if (!enemy) return;
            var bleed = enemy.GetComponent<EnemyBleed>();
            if (bleed) bleed.Stop();
        }

        /// <summary>지금 피를 흘리는가(이름표 특성 줄·계측용).</summary>
        public static bool IsBleeding(Enemy enemy)
        {
            if (!enemy || enemy.Dead) return false;
            var bleed = enemy.GetComponent<EnemyBleed>();
            return bleed && bleed.Active;
        }

        void Begin(Enemy enemy, PlayerController source, float totalPercent)
        {
            _enemy = enemy;
            _source = source;
            _attack = source.Attack;
            var sheet = source.Sheet;
            _bossBonus = sheet != null ? sheet.BossDamagePermille / 1000.0 : 0.0;
            // 겹치지 않는다: 새로 걸리면 합·시간만 다시(남은 틱 6, 다음 틱 0.5초 뒤).
            _tickPercent = BleedRule.TickPercent(totalPercent);
            _ticksLeft = BleedRule.Ticks;
            _nextTick = Time.time + BleedRule.TickInterval;
            enabled = true;
        }

        void Update()
        {
            if (_ticksLeft <= 0 || !_enemy || _enemy.Dead)
            {
                Stop();
                return;
            }
            // 프레임이 길어도 지난 틱을 빠뜨리지 않는다(한 프레임에 여러 틱이면 차례로).
            while (_ticksLeft > 0 && Time.time >= _nextTick)
            {
                _nextTick += BleedRule.TickInterval;
                _ticksLeft--;
                Tick();
                if (!_enemy || _enemy.Dead)
                {
                    Stop();
                    return;
                }
            }
            if (_ticksLeft <= 0) Stop();
        }

        /// <summary>틱 한 번: 치명 없음, 굴림 1.0, 방어 0(기본공격과 같음), 보스면 보스 피해. 버팀 0, 마무리 아님.</summary>
        void Tick()
        {
            int damage = DamageMath.ToMonster(_attack, _tickPercent, false, 1.0, 1.0, 0, 0.0, false, _bossBonus, _enemy.IsBoss);
            int applied = _enemy.TakeHit(damage, false, DamageSource.Bleed, 0f, false, 1f, out _, out _);
            if (applied <= 0) return;
            GoreSystem.BleedDrip(_enemy);
            // 출혈로 쓰러뜨린 적도 플레이어 몫 처치(연속 처치·처치 회복). 여러 마리 처치 연출은 내지 않는다.
            if (_enemy.Dead)
            {
                var player = _source ? _source : PlayerController.Instance;
                if (player) player.AddKills(1, false);
            }
        }

        void Stop()
        {
            _ticksLeft = 0;
            enabled = false;
        }
    }
}
