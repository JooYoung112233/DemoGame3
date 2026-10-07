using System;
using System.Collections.Generic;

namespace Demo6.Core.Time
{
    /// <summary>느린 화면 순위(4-3 표, 높을수록 앞). 처형 느린 화면은 마주침의 마지막 적·정예만 쓴다.</summary>
    public enum SlowPriority
    {
        /// <summary>여러 마리 처치 등 지금 있는 느린 화면.</summary>
        Normal = 0,
        /// <summary>회피 반격 0.12초 × 0.4.</summary>
        Counter = 1,
        /// <summary>처형 0.3초 × 0.35.</summary>
        Execution = 2,
        /// <summary>보스 2단계 전환 0.6초 × 0.5.</summary>
        BossPhase = 3,
        /// <summary>보스 처치 1.0초 × 0.3.</summary>
        BossKill = 4,
    }

    /// <summary>
    /// 기획 11-6 시간 제어. 일시정지(0) &gt; 히트스톱(0) &gt; 느린 화면(배율) 순으로 가장 높은 요청 하나가 이긴다.
    /// 모든 시간은 실제 시간(초)으로 진행한다.
    /// 멈춤 우선순위(기획/전투-보스-무기-다듬기-1차.md 4-3): 느린 화면이 겹치면 높은 순위만 쓰고(같은 순위는 예전 겹쳐 받기 규칙),
    /// 보스 순간(BossPhase·BossKill) 앞뒤 1.5초 안의 낮은 순위 느린 화면은 무시한다. 앞쪽은 보스 순간이 진행 중인 낮은 순위를 바로 덮어쓰고,
    /// 뒤쪽은 보스 느린 화면이 끝난 뒤 1.5초(실제 시간)까지 들어온 낮은 순위 요청을 버린다(보스 느린 화면 동안은 순위로 이미 버림).
    /// 순위 없는 요청은 Normal이다(그래서 순위를 쓰지 않으면 예전과 같다).
    /// 히트스톱 잠금(보스 처치): 잠금 동안 다른 히트스톱 요청을 무시한다. 잠근 요청 자신도 1초 0.2초 예산 안에서만 받는다.
    /// </summary>
    public sealed class TimeScaleArbiter
    {
        public const double HitStopBudget = 0.2;
        public const double HitStopBudgetWindow = 1.0;
        /// <summary>보스 느린 화면이 끝난 뒤 낮은 순위 느린 화면을 무시하는 시간(초).</summary>
        public const double BossMomentGuard = 1.5;

        readonly Queue<(double time, double amount)> _grants = new Queue<(double, double)>();
        double _now;
        double _hitStopRemaining;
        double _slowRemaining;
        double _slowScale = 1;
        SlowPriority _slowPriority = SlowPriority.Normal;
        double _bossGuardUntil = double.NegativeInfinity;
        double _hitStopLockUntil = double.NegativeInfinity;

        public bool Paused { get; set; }
        public double HitStopRemaining => _hitStopRemaining;
        public double SlowRemaining => _slowRemaining;
        /// <summary>지금 느린 화면 배율(없으면 1).</summary>
        public double SlowScale => _slowRemaining > 0 ? _slowScale : 1;
        /// <summary>지금 느린 화면의 순위(없으면 Normal).</summary>
        public SlowPriority SlowPriorityNow => _slowRemaining > 0 ? _slowPriority : SlowPriority.Normal;
        /// <summary>히트스톱 잠금 중인가(보스 처치 순간).</summary>
        public bool HitStopLocked => _now < _hitStopLockUntil;
        /// <summary>보스 순간 가드 안인가: 보스 느린 화면이 진행 중이거나 끝난 뒤 1.5초 안(낮은 순위 느린 화면을 무시하는 동안).</summary>
        public bool InBossGuard => (_slowRemaining > 0 && IsBossMoment(_slowPriority)) || _now <= _bossGuardUntil;

        static bool IsBossMoment(SlowPriority priority) => priority >= SlowPriority.BossPhase;

        public double Effective
        {
            get
            {
                if (Paused) return 0;
                if (_hitStopRemaining > 0) return 0;
                if (_slowRemaining > 0) return _slowScale;
                return 1;
            }
        }

        /// <summary>진행 중인 히트스톱보다 길 때만 덮어쓴다. 1초 안 누적 0.2초를 넘지 않는다. 잠금 중이면 무시한다.</summary>
        /// <returns>실제로 늘어난 시간.</returns>
        public double RequestHitStop(double seconds)
        {
            if (HitStopLocked) return 0;
            return GrantHitStop(seconds);
        }

        /// <summary>
        /// 잠그는 히트스톱(4-3 #1 보스 처치): 예산 안에서 seconds를 받고(앞선 잠금도 넘어섬), 지금부터 lockSeconds 동안 다른 히트스톱 요청을 무시한다.
        /// </summary>
        /// <returns>실제로 늘어난 시간.</returns>
        public double RequestHitStopLocked(double seconds, double lockSeconds)
        {
            double granted = GrantHitStop(seconds);
            if (lockSeconds > 0) _hitStopLockUntil = Math.Max(_hitStopLockUntil, _now + lockSeconds);
            return granted;
        }

        double GrantHitStop(double seconds)
        {
            if (seconds <= _hitStopRemaining) return 0;
            double extension = seconds - _hitStopRemaining;
            double granted = Math.Min(extension, BudgetLeft());
            if (granted <= 0) return 0;
            _hitStopRemaining += granted;
            _grants.Enqueue((_now, granted));
            return granted;
        }

        /// <summary>
        /// 순위가 있는 느린 화면(4-3 멈춤 우선순위). 진행 중인 것보다 높은 순위면 덮어쓰고, 낮으면 버리고, 같으면 예전 규칙(더 길거나 더 느릴 때 받음)이다.
        /// 보스 순간(BossPhase·BossKill)이 진행 중이거나 끝난 뒤 1.5초 안이면 낮은 순위 요청은 버린다(보스 순간끼리는 순위대로 받는다).
        /// </summary>
        /// <returns>받았는가(지금 느린 화면이 이 요청으로 바뀌었나).</returns>
        public bool RequestSlowMotion(double seconds, double scale, SlowPriority priority)
        {
            if (seconds <= 0) return false;
            if (!IsBossMoment(priority) && InBossGuard) return false;
            if (_slowRemaining <= 0 || priority > _slowPriority)
            {
                _slowRemaining = seconds;
                _slowScale = scale;
                _slowPriority = priority;
                return true;
            }
            if (priority < _slowPriority) return false;
            if (seconds >= _slowRemaining || scale < _slowScale)
            {
                _slowRemaining = Math.Max(_slowRemaining, seconds);
                _slowScale = scale;
                return true;
            }
            return false;
        }

        /// <summary>순위 없는 느린 화면(여러 마리 처치 등) = Normal 순위. 순위 있는 요청이 없으면 예전 겹쳐 받기와 같다.</summary>
        public bool RequestSlowMotion(double seconds, double scale) => RequestSlowMotion(seconds, scale, SlowPriority.Normal);

        public void Advance(double realSeconds)
        {
            if (realSeconds <= 0) return;
            double start = _now;
            _now += realSeconds;
            if (!Paused)
            {
                // 히트스톱이 먼저 흐르고, 남은 시간만 느린 화면을 깎는다.
                double left = realSeconds;
                if (_hitStopRemaining > 0)
                {
                    double used = Math.Min(_hitStopRemaining, left);
                    _hitStopRemaining -= used;
                    left -= used;
                }
                if (_slowRemaining > 0 && left > 0)
                {
                    double endAt = start + (realSeconds - left) + _slowRemaining;
                    _slowRemaining = Math.Max(0, _slowRemaining - left);
                    if (_slowRemaining <= 0)
                    {
                        // 보스 느린 화면이 끝났다: 그 끝에서 1.5초 동안 낮은 순위를 버린다.
                        if (IsBossMoment(_slowPriority)) _bossGuardUntil = Math.Max(_bossGuardUntil, endAt + BossMomentGuard);
                        _slowScale = 1;
                        _slowPriority = SlowPriority.Normal;
                    }
                }
            }
            while (_grants.Count > 0 && _grants.Peek().time <= _now - HitStopBudgetWindow)
                _grants.Dequeue();
        }

        public void Reset()
        {
            _grants.Clear();
            _now = 0;
            _hitStopRemaining = 0;
            _slowRemaining = 0;
            _slowScale = 1;
            _slowPriority = SlowPriority.Normal;
            _bossGuardUntil = double.NegativeInfinity;
            _hitStopLockUntil = double.NegativeInfinity;
            Paused = false;
        }

        double BudgetLeft()
        {
            double used = 0;
            foreach (var g in _grants)
                if (g.time > _now - HitStopBudgetWindow) used += g.amount;
            return Math.Max(0, HitStopBudget - used);
        }
    }
}
