using System;

namespace Demo6.Core.Combat
{
    /// <summary>
    /// 3차 초안 3-4 버팀. 버팀 피해가 쌓여 0이 되면 '무너짐'.
    /// 마지막 버팀 피해 뒤 2.5초가 지나면 초당 최대치의 20%씩 찬다(보스 4초 뒤 10%).
    /// 무너졌다 일어나면 최대치가 25% 늘어난다(처음 값의 2배까지). 층 배율·레벨 보정은 받지 않는다.
    /// 시간은 호출하는 쪽의 게임 시간(초)이다.
    /// </summary>
    public sealed class PoiseMeter
    {
        public const double RegenDelay = 2.5;
        public const double RegenRate = 0.2;
        public const double BossRegenDelay = 4.0;
        public const double BossRegenRate = 0.1;
        public const double GrowthPerBreak = 0.25;
        public const double MaxGrowth = 2.0;

        readonly double _base;
        readonly bool _boss;
        double _lastDamage = double.NegativeInfinity;

        public double Max { get; private set; }
        public double Current { get; private set; }
        public int Breaks { get; private set; }

        public PoiseMeter(double max, bool boss = false)
        {
            _base = Math.Max(1, max);
            _boss = boss;
            Max = _base;
            Current = _base;
        }

        public double Fraction => Max > 0 ? Current / Max : 0;

        /// <returns>이번 피해로 무너졌는가.</returns>
        public bool Apply(double amount, double now)
        {
            if (amount <= 0 || Current <= 0) return false;
            _lastDamage = now;
            Current = Math.Max(0, Current - amount);
            if (Current > 0) return false;
            Breaks++;
            return true;
        }

        /// <summary>무너짐에서 일어날 때: 최대치를 늘리고 가득 채운다.</summary>
        public void Recover()
        {
            Max = Math.Min(_base * MaxGrowth, Max + _base * GrowthPerBreak);
            Current = Max;
            _lastDamage = double.NegativeInfinity;
        }

        public void Refill()
        {
            Current = Max;
        }

        public void Tick(double now, double dt)
        {
            if (dt <= 0 || Current <= 0 || Current >= Max) return;
            double delay = _boss ? BossRegenDelay : RegenDelay;
            if (now - _lastDamage < delay) return;
            Current = Math.Min(Max, Current + Max * (_boss ? BossRegenRate : RegenRate) * dt);
        }

        /// <summary>
        /// 기습 버팀 피해: 근접 일반 타격·마무리 ×3, 검풍·회오리 ×1.5, 한 번에 최대치의 70%까지.
        /// </summary>
        public static double AmbushDamage(double poiseDamage, bool melee, double max)
        {
            double v = poiseDamage * (melee ? 3.0 : 1.5);
            return Math.Min(v, max * 0.7);
        }
    }
}
