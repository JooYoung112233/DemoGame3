using System;
using UnityEngine;

namespace Demo6.Game
{
    public enum DamageSource
    {
        Basic,
        Whirlwind,
        SwordWave,
        Enemy,
        /// <summary>전설 고유 효과(연쇄 번개·불꽃 발자국·연쇄 폭발, 장비 문서 6장). 체력 흡수·연쇄 번개 재발동을 받지 않는다.</summary>
        Legend,
    }

    public sealed class Health : MonoBehaviour
    {
        public int Max { get; private set; }
        public int Current { get; private set; }
        public int Defense { get; set; }
        public bool Dead { get; private set; }
        /// <summary>나무 허수아비: 체력이 줄지 않는다.</summary>
        public bool Infinite { get; set; }
        /// <summary>멧돼지 기절 중 받는 피해 +30% 등.</summary>
        public float DamageTakenMultiplier { get; set; } = 1f;
        /// <summary>구르기 무적처럼 외부에서 켜고 끄는 무적.</summary>
        public bool ExtraInvulnerable { get; set; }
        /// <summary>머리 위 체력바를 보일 실제 시간.</summary>
        public float BarVisibleUntil { get; private set; }

        float _invulnerableUntil;

        public bool IsInvulnerable => ExtraInvulnerable || Time.time < _invulnerableUntil;
        public float Fraction => Max > 0 ? (float)Current / Max : 0f;

        public event Action<int, bool> Damaged;
        public event Action Died;

        public void Init(int max, int defense)
        {
            Max = Mathf.Max(1, max);
            Current = Max;
            Defense = defense;
            Dead = false;
            _invulnerableUntil = 0f;
            ExtraInvulnerable = false;
            DamageTakenMultiplier = 1f;
        }

        public void GrantInvulnerability(float seconds) =>
            _invulnerableUntil = Mathf.Max(_invulnerableUntil, Time.time + seconds);

        /// <returns>실제로 들어간 피해. 무적이면 0.</returns>
        public int ApplyDamage(int amount, bool crit)
        {
            if (Dead || IsInvulnerable) return 0;
            amount = Mathf.Max(1, Mathf.RoundToInt(amount * DamageTakenMultiplier));
            if (!Infinite) Current = Mathf.Max(0, Current - amount);
            BarVisibleUntil = Time.unscaledTime + 3f;
            Damaged?.Invoke(amount, crit);
            if (Current <= 0 && !Dead)
            {
                Dead = true;
                Died?.Invoke();
            }
            return amount;
        }

        public int Heal(int amount)
        {
            if (Dead || amount <= 0) return 0;
            int before = Current;
            Current = Mathf.Min(Max, Current + amount);
            return Current - before;
        }

        /// <summary>최대 체력을 바꾼다. 늘어난 만큼 지금 체력도 늘리고, 줄면 최대치에 맞춘다.</summary>
        public void SetMax(int max)
        {
            max = Mathf.Max(1, max);
            int delta = max - Max;
            Max = max;
            Current = Mathf.Clamp(Current + Mathf.Max(0, delta), Dead ? 0 : 1, Max);
        }

        /// <summary>지금 체력을 정한다(계단으로 내려가 원정 몫을 이을 때). 1~최대로 자르고, 쓰러져 있으면 건드리지 않는다.</summary>
        public void SetCurrent(int value)
        {
            if (Dead) return;
            Current = Mathf.Clamp(value, 1, Max);
        }

        public void Revive()
        {
            Dead = false;
            Current = Max;
        }
    }

    public readonly struct DamageDealt
    {
        public readonly Enemy Target;
        public readonly int Amount;
        public readonly DamageSource Source;
        public readonly bool Crit;
        public readonly bool Killed;

        public DamageDealt(Enemy target, int amount, DamageSource source, bool crit, bool killed)
        {
            Target = target;
            Amount = amount;
            Source = source;
            Crit = crit;
            Killed = killed;
        }
    }

    /// <summary>
    /// 기본공격 판정 하나가 적을 하나 이상 맞혔다(장비 문서 6장 연쇄 번개의 발동 자리). PlayerController.SwingHit이 맞힌 판정마다 한 번 낸다.
    /// 연쇄 번개는 ActionId가 바뀐 첫 사건에서만 굴린다('동작마다 한 번').
    /// </summary>
    public readonly struct BasicHitInfo
    {
        /// <summary>기본공격 동작 번호(StartSwing마다 1씩 오름).</summary>
        public readonly int ActionId;
        /// <summary>이 동작 안 판정 번호(0부터, 쌍검 연타는 0·1).</summary>
        public readonly int HitIndex;
        public readonly bool Finisher;
        /// <summary>맞은 적 가운데 가장 가까운 적(판정 모양 기준).</summary>
        public readonly Enemy FirstTarget;
        public readonly int TargetsHit;
        public readonly bool AnyCrit;
        public readonly Vector2 Origin;
        public readonly Vector2 Direction;

        public BasicHitInfo(int actionId, int hitIndex, bool finisher, Enemy firstTarget, int targetsHit, bool anyCrit, Vector2 origin, Vector2 direction)
        {
            ActionId = actionId;
            HitIndex = hitIndex;
            Finisher = finisher;
            FirstTarget = firstTarget;
            TargetsHit = targetsHit;
            AnyCrit = anyCrit;
            Origin = origin;
            Direction = direction;
        }
    }

    /// <summary>계측용 전투 사건. 플레이 시작 때 구독을 비운다(도메인 다시 불러오기 꺼짐 대비).</summary>
    public static class CombatEvents
    {
        /// <summary>기본공격 판정이 적을 맞혔다(전설 연쇄 번개가 듣는다).</summary>
        public static event Action<BasicHitInfo> PlayerBasicHit;
        /// <summary>플레이어 치명 연출 하나를 냈다(장비 문서 3-4: 낸 단계, 마무리 단계였나). 동작·회오리 한 타·검풍 한 번마다 가장 무거운 하나.</summary>
        public static event Action<Demo6.Core.Combat.CritTier, bool> PlayerCritShown;
        public static event Action<DamageDealt> PlayerDealtDamage;
        public static event Action<Enemy> EnemyKilled;
        public static event Action<int> PlayerDamaged;
        public static event Action PlayerDowned;
        /// <summary>(피할 수 있었던 예고인가, 맞았는가)</summary>
        public static event Action<bool, bool> TelegraphResolved;
        /// <summary>버팀이 0이 되어 무너짐(3차).</summary>
        public static event Action<Enemy> EnemyBroken;
        /// <summary>잠든 적이 깸(3차 기습).</summary>
        public static event Action<Enemy> EnemyWoke;
        /// <summary>마주침 무리를 놓음(무리 번호, 정예 여부).</summary>
        public static event Action<int, bool> EncounterSpawned;
        /// <summary>마주침 무리가 모두 쓰러짐.</summary>
        public static event Action<int> EncounterCleared;
        /// <summary>기본공격 판정 순간(적을 맞혔든 아니든). 판자벽 부수기, 벽 '퉁' 소리에 쓴다.</summary>
        public static event Action<Vector2, Vector2, Demo6.Core.Combat.ComboStep, bool> PlayerSwing;
        /// <summary>회오리 베기 한 타(중심, 반경, 적을 맞혔나).</summary>
        public static event Action<Vector2, float, bool> PlayerWhirl;
        /// <summary>검풍이 벽에 막힌 자리와 방향.</summary>
        public static event Action<Vector2, Vector2> WaveHitWall;
        /// <summary>
        /// 전설 효과 하나가 발동했다(효과, 크기). 크기: 연쇄 번개 = 튕긴 수(0~4), 불꽃 발자국 = 1(불길 하나), 연쇄 폭발 = 연쇄 안 몇 번째 폭발(1~12).
        /// 계측(CombatStats 효과별 발동 수·최대 크기)이 듣는다.
        /// </summary>
        public static event Action<Demo6.Core.Loot.LegendaryEffect, int> LegendTriggered;
        /// <summary>전설 효과가 적에게 피해를 넣었다(효과, 들어간 피해). PlayerDealtDamage(출처 Legend) 바로 뒤에 온다. 출처별 몫의 '전설' 줄을 효과별로 나눌 때 쓴다.</summary>
        public static event Action<Demo6.Core.Loot.LegendaryEffect, DamageDealt> LegendDealt;

        public static void ResetStatics()
        {
            LegendTriggered = null;
            LegendDealt = null;
            PlayerBasicHit = null;
            PlayerCritShown = null;
            PlayerDealtDamage = null;
            EnemyKilled = null;
            PlayerDamaged = null;
            PlayerDowned = null;
            TelegraphResolved = null;
            EnemyBroken = null;
            EnemyWoke = null;
            EncounterSpawned = null;
            EncounterCleared = null;
            PlayerSwing = null;
            PlayerWhirl = null;
            WaveHitWall = null;
        }

        public static void RaiseBasicHit(in BasicHitInfo info) => PlayerBasicHit?.Invoke(info);
        public static void RaiseCritShown(Demo6.Core.Combat.CritTier tier, bool finisher) => PlayerCritShown?.Invoke(tier, finisher);
        public static void RaiseDealt(in DamageDealt d) => PlayerDealtDamage?.Invoke(d);
        public static void RaiseKilled(Enemy e) => EnemyKilled?.Invoke(e);
        public static void RaisePlayerDamaged(int amount) => PlayerDamaged?.Invoke(amount);
        public static void RaisePlayerDowned() => PlayerDowned?.Invoke();
        public static void RaiseTelegraph(bool avoidable, bool hit) => TelegraphResolved?.Invoke(avoidable, hit);
        public static void RaiseBroken(Enemy e) => EnemyBroken?.Invoke(e);
        public static void RaiseWoke(Enemy e) => EnemyWoke?.Invoke(e);
        public static void RaiseEncounterSpawned(int group, bool elite) => EncounterSpawned?.Invoke(group, elite);
        public static void RaiseEncounterCleared(int group) => EncounterCleared?.Invoke(group);
        public static void RaisePlayerSwing(Vector2 origin, Vector2 dir, Demo6.Core.Combat.ComboStep step, bool hitEnemy) => PlayerSwing?.Invoke(origin, dir, step, hitEnemy);
        public static void RaisePlayerWhirl(Vector2 origin, float radius, bool hitEnemy) => PlayerWhirl?.Invoke(origin, radius, hitEnemy);
        public static void RaiseWaveHitWall(Vector2 point, Vector2 dir) => WaveHitWall?.Invoke(point, dir);
        public static void RaiseLegendTriggered(Demo6.Core.Loot.LegendaryEffect effect, int size) => LegendTriggered?.Invoke(effect, size);
        public static void RaiseLegendDealt(Demo6.Core.Loot.LegendaryEffect effect, in DamageDealt d) => LegendDealt?.Invoke(effect, d);
    }
}
